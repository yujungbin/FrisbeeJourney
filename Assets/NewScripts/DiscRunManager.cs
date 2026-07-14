using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class DiscRunManager : MonoBehaviour
{
    [System.Serializable]
    public class ThrowCountChangedEvent : UnityEvent<int, int> { }

    [System.Serializable]
    public class ThrowsRemainingChangedEvent : UnityEvent<int> { }

    private enum GameOverReason
    {
        Unknown,
        DurabilityBroken,
        NoThrowsRemaining
    }

    [Header("References")]
    [SerializeField] private DiscProgressionStore progressionStore;
    [SerializeField] private DiscSlingshotController discController;
    [SerializeField] private DiscDurability discDurability;
    [SerializeField] private DiscCinemachineSwitcher cameraSwitcher;

    [Header("Launch Anchor")]
    [SerializeField] private Transform launchAnchor;
    [SerializeField] private Transform trackRoot;

    [Header("Throw Limit")]
    [Tooltip("false면 투척 횟수 제한 없이 내구도가 0이 될 때까지 던질 수 있습니다.")]
    [SerializeField] private bool useThrowLimit = false;

    [Tooltip("Use Throw Limit이 true일 때 한 판에서 총 몇 번 던질 수 있는지입니다.")]
    [SerializeField] private int maxThrowsPerRun = 3;

    [Header("Retry")]
    [SerializeField] private bool rethrowFromImpactPoint = true;
    [SerializeField] private bool rethrowFromFinalStopPosition = true;

    [Tooltip("멈춘 위치에서 진행 방향 반대로 얼마나 물러나서 다시 던질지입니다.")]
    [SerializeField] private float rethrowBackOffset = 0.15f;

    [SerializeField] private float rethrowHeightOffset = 0f;
    [SerializeField] private float rethrowDelay = 0.6f;

    [Header("Settle After Impact")]
    [SerializeField] private float settleMaxWaitTime = 3.0f;

    [Tooltip("true면 settleMaxWaitTime이 지나면 저속 조건과 상관없이 강제로 던지기를 끝냅니다.")]
    [SerializeField] private bool forceFinishOnSettleTimeout = false;

    [SerializeField] private bool logSettlingStatus = true;
    [SerializeField] private float settlingStatusLogInterval = 0.5f;

    [Header("Auto Restart After Game Over")]
    [SerializeField] private bool restoreOriginalLaunchAnchorOnRunStart = true;
    [SerializeField] private bool autoRestartWhenDurabilityBroken = false;
    [SerializeField] private bool autoRestartWhenNoThrowsRemaining = true;
    [SerializeField] private float gameOverRestartDelay = 1.0f;

    [Header("Events")]
    [SerializeField] private UnityEvent onRunStarted = new UnityEvent();
    [SerializeField] private UnityEvent onRethrowReady = new UnityEvent();
    [SerializeField] private UnityEvent onGameOver = new UnityEvent();

    [Header("Throw Events")]
    [SerializeField]
    private ThrowCountChangedEvent onThrowCountChanged =
        new ThrowCountChangedEvent();

    [SerializeField]
    private ThrowsRemainingChangedEvent onThrowsRemainingChanged =
        new ThrowsRemainingChangedEvent();

    [SerializeField]
    private UnityEvent onNoThrowsRemaining =
        new UnityEvent();

    private Coroutine rethrowRoutine;
    private Coroutine gameOverRestartRoutine;

    private Vector3 originalLaunchAnchorPosition;
    private Quaternion originalLaunchAnchorRotation;
    private bool hasOriginalLaunchAnchor;

    private int throwsUsed;
    private bool runActive;

    private DiscSlingshotController subscribedDiscController;

    public int MaxThrowsPerRun => Mathf.Max(1, maxThrowsPerRun);
    public int ThrowsUsed => throwsUsed;

    public int ThrowsRemaining
    {
        get
        {
            if (!useThrowLimit)
                return -1;

            return Mathf.Max(0, MaxThrowsPerRun - throwsUsed);
        }
    }

    public bool HasThrowsRemaining
    {
        get
        {
            if (!useThrowLimit)
                return true;

            return ThrowsRemaining > 0;
        }
    }

    private void Awake()
    {
        CaptureOriginalLaunchAnchor();
    }

    private void OnEnable()
    {
        SubscribeToDiscEvents();
    }

    private void OnDisable()
    {
        UnsubscribeFromDiscEvents();
    }

    private void Start()
    {
        StartRun();
    }

    private void OnValidate()
    {
        maxThrowsPerRun = Mathf.Max(1, maxThrowsPerRun);
        rethrowDelay = Mathf.Max(0f, rethrowDelay);
        rethrowBackOffset = Mathf.Max(0f, rethrowBackOffset);
        settleMaxWaitTime = Mathf.Max(0f, settleMaxWaitTime);
        settlingStatusLogInterval = Mathf.Max(0.05f, settlingStatusLogInterval);
        gameOverRestartDelay = Mathf.Max(0f, gameOverRestartDelay);
    }

    public void StartRun()
    {
        StopRunningCoroutines();

        if (discController == null)
        {
            Debug.LogError("DiscController가 연결되어 있지 않습니다.");
            return;
        }

        if (!hasOriginalLaunchAnchor)
            CaptureOriginalLaunchAnchor();

        if (restoreOriginalLaunchAnchorOnRunStart)
            RestoreOriginalLaunchAnchor();

        SubscribeToDiscEvents();

        throwsUsed = 0;
        runActive = true;

        ApplyRuntimeStatsAndDurability();

        NotifyThrowCountChanged();
        ResetDiscForThrow();

        onRunStarted.Invoke();

        Debug.Log("새 게임 시작");
    }

    private void ApplyRuntimeStatsAndDurability()
    {
        if (progressionStore != null)
        {
            DiscRuntimeStats stats = progressionStore.BuildRuntimeStats();

            if (discController != null)
                discController.ApplyStats(stats);

            if (discDurability != null)
                discDurability.Initialize(stats.maxDurability);

            return;
        }

        // ProgressionStore가 없을 때의 fallback.
        // 업그레이드 시스템 없이 테스트할 때 사용됩니다.
        if (discDurability != null)
            discDurability.Initialize(discDurability.MaxDurability);
    }

    private void SubscribeToDiscEvents()
    {
        if (discController == null)
            return;

        if (subscribedDiscController == discController)
            return;

        UnsubscribeFromDiscEvents();

        discController.Launched += HandleDiscLaunched;
        subscribedDiscController = discController;
    }

    private void UnsubscribeFromDiscEvents()
    {
        if (subscribedDiscController == null)
            return;

        subscribedDiscController.Launched -= HandleDiscLaunched;
        subscribedDiscController = null;
    }

    private void HandleDiscLaunched()
    {
        if (!runActive)
            return;

        throwsUsed++;

        NotifyThrowCountChanged();

        if (useThrowLimit && !HasThrowsRemaining)
            onNoThrowsRemaining.Invoke();

        Debug.Log(
            useThrowLimit
                ? $"투척 {throwsUsed}/{MaxThrowsPerRun}, 남은 투척: {ThrowsRemaining}"
                : $"투척 {throwsUsed}, 투척 제한 없음"
        );
    }

    public void HandleDiscImpact(DiscImpactInfo impactInfo)
    {
        if (!runActive)
            return;

        if (rethrowRoutine != null)
            return;

        Debug.Log(
            $"Impact handled. " +
            $"source: {impactInfo.sourceName}, " +
            $"speed: {impactInfo.impactSpeed:F2}, " +
            $"damage: {impactInfo.durabilityDamage:F1}, " +
            $"durability: {(discDurability != null ? discDurability.CurrentDurability.ToString("F1") : "none")}"
        );

        if (discController != null)
            discController.BeginSettlingAfterImpact(impactInfo.impactSpeed);

        rethrowRoutine = StartCoroutine(
            SettleThenResolveImpactRoutine(impactInfo)
        );
    }

    private IEnumerator SettleThenResolveImpactRoutine(DiscImpactInfo impactInfo)
    {
        float elapsed = 0f;
        float nextLogTime = 0f;
        bool timeoutWarningShown = false;
        bool stoppedByTimeout = false;

        while (true)
        {
            if (discController == null)
                break;

            if (discDurability != null && discDurability.IsBroken)
                break;

            if (discController.IsSlowEnoughToStop())
                break;

            if (settleMaxWaitTime > 0f && elapsed >= settleMaxWaitTime)
            {
                if (forceFinishOnSettleTimeout)
                {
                    stoppedByTimeout = true;
                    break;
                }

                if (!timeoutWarningShown)
                {
                    timeoutWarningShown = true;

                    Debug.LogWarning(
                        $"Settling timeout reached, but throw will NOT finish yet. " +
                        $"Waiting for low-speed duration. " +
                        $"speed: {discController.CurrentSpeed:F2}, " +
                        $"lowTimer: {discController.LowSpeedTimer:F2}/" +
                        $"{discController.RequiredLowSpeedDurationToStop:F2}"
                    );
                }
            }

            if (logSettlingStatus && Time.time >= nextLogTime)
            {
                nextLogTime = Time.time + settlingStatusLogInterval;

                Debug.Log(
                    $"Settling wait | " +
                    $"elapsed: {elapsed:F2}, " +
                    $"speed: {discController.CurrentSpeed:F2}, " +
                    $"lowTimer: {discController.LowSpeedTimer:F2}/" +
                    $"{discController.RequiredLowSpeedDurationToStop:F2}, " +
                    $"durability: {(discDurability != null ? discDurability.CurrentDurability.ToString("F1") : "none")}, " +
                    $"ready: {discController.SettlingStopReady}"
                );
            }

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        if (discController != null)
            discController.StopDiscImmediately();

        if (stoppedByTimeout)
        {
            Debug.LogWarning(
                "Throw ended by timeout, not by low-speed detection."
            );
        }

        if (discDurability != null && discDurability.IsBroken)
        {
            rethrowRoutine = null;
            GameOver(GameOverReason.DurabilityBroken);
            yield break;
        }

        if (useThrowLimit && !HasThrowsRemaining)
        {
            rethrowRoutine = null;
            GameOver(GameOverReason.NoThrowsRemaining);
            yield break;
        }

        Vector3 rethrowPoint = impactInfo.hitPoint;

        if (rethrowFromFinalStopPosition && discController != null)
            rethrowPoint = discController.RigidbodyPosition;

        if (rethrowFromImpactPoint)
            MoveLaunchAnchorToPoint(rethrowPoint);

        if (rethrowDelay > 0f)
            yield return new WaitForSeconds(rethrowDelay);

        ResetDiscForThrow();

        rethrowRoutine = null;
        onRethrowReady.Invoke();
    }

    private void ResetDiscForThrow()
    {
        if (!runActive)
            return;

        if (useThrowLimit && !HasThrowsRemaining)
        {
            GameOver(GameOverReason.NoThrowsRemaining);
            return;
        }

        if (discDurability != null && discDurability.IsBroken)
        {
            GameOver(GameOverReason.DurabilityBroken);
            return;
        }

        if (discController != null)
            discController.ResetToLaunch();

        if (cameraSwitcher != null)
            cameraSwitcher.ShowLaunchCameraAt(launchAnchor);
    }

    private void GameOver(GameOverReason reason)
    {
        if (!runActive)
            return;

        runActive = false;

        StopRunningCoroutines();

        PlaceDiscAtOriginalLaunchAnchorForGameOver();

        NotifyThrowCountChanged();

        onGameOver.Invoke();

        if (ShouldAutoRestart(reason))
            ScheduleAutoRestart();
    }

    private bool ShouldAutoRestart(GameOverReason reason)
    {
        switch (reason)
        {
            case GameOverReason.DurabilityBroken:
                return autoRestartWhenDurabilityBroken;

            case GameOverReason.NoThrowsRemaining:
                return autoRestartWhenNoThrowsRemaining;

            default:
                return false;
        }
    }

    private void PlaceDiscAtOriginalLaunchAnchorForGameOver()
    {
        RestoreOriginalLaunchAnchor();

        if (discController != null)
            discController.PlaceAtLaunchAnchor(false);

        if (cameraSwitcher != null)
            cameraSwitcher.ShowLaunchCameraAt(launchAnchor);
    }

    private void ScheduleAutoRestart()
    {
        if (gameOverRestartRoutine != null)
            StopCoroutine(gameOverRestartRoutine);

        gameOverRestartRoutine = StartCoroutine(RestartAfterGameOverRoutine());
    }

    private IEnumerator RestartAfterGameOverRoutine()
    {
        if (gameOverRestartDelay > 0f)
            yield return new WaitForSeconds(gameOverRestartDelay);

        gameOverRestartRoutine = null;
        StartRun();
    }

    public void RestartRunFromOriginalLaunchAnchor()
    {
        StopRunningCoroutines();

        RestoreOriginalLaunchAnchor();
        StartRun();
    }

    private void StopRunningCoroutines()
    {
        if (rethrowRoutine != null)
        {
            StopCoroutine(rethrowRoutine);
            rethrowRoutine = null;
        }

        if (gameOverRestartRoutine != null)
        {
            StopCoroutine(gameOverRestartRoutine);
            gameOverRestartRoutine = null;
        }
    }

    private void CaptureOriginalLaunchAnchor()
    {
        if (launchAnchor == null)
            return;

        originalLaunchAnchorPosition = launchAnchor.position;
        originalLaunchAnchorRotation = launchAnchor.rotation;
        hasOriginalLaunchAnchor = true;
    }

    private void RestoreOriginalLaunchAnchor()
    {
        if (launchAnchor == null || !hasOriginalLaunchAnchor)
            return;

        launchAnchor.position = originalLaunchAnchorPosition;
        launchAnchor.rotation = originalLaunchAnchorRotation;
    }

    private void MoveLaunchAnchorToPoint(Vector3 point)
    {
        if (launchAnchor == null)
            return;

        Vector3 forward = GetTrackForward();

        Vector3 newAnchorPosition =
            point -
            forward * rethrowBackOffset +
            Vector3.up * rethrowHeightOffset;

        launchAnchor.position = newAnchorPosition;
    }

    private Vector3 GetTrackForward()
    {
        Vector3 forward = trackRoot != null
            ? trackRoot.forward
            : Vector3.forward;

        forward.y = 0f;

        if (forward.sqrMagnitude < 0.0001f)
            return Vector3.forward;

        return forward.normalized;
    }

    private void NotifyThrowCountChanged()
    {
        int maxForUi = useThrowLimit
            ? MaxThrowsPerRun
            : -1;

        int remainingForUi = useThrowLimit
            ? ThrowsRemaining
            : -1;

        onThrowCountChanged.Invoke(throwsUsed, maxForUi);
        onThrowsRemainingChanged.Invoke(remainingForUi);
    }
}