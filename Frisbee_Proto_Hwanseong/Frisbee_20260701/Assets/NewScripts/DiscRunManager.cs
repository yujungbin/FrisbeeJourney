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
        NoThrowsRemaining
    }

    [Header("References")]
    [SerializeField] private DiscProgressionStore progressionStore;
    [SerializeField] private DiscSlingshotController discController;
    [SerializeField] private DiscCinemachineSwitcher cameraSwitcher;

    [Header("Launch Anchor")]
    [SerializeField] private Transform launchAnchor;
    [SerializeField] private Transform trackRoot;

    [Header("Throw Limit")]
    [Tooltip("한 판에서 총 몇 번 던질 수 있는지. 최초 투척도 포함됩니다.")]
    [SerializeField] private int maxThrowsPerRun = 3;

    [Header("Retry")]
    [SerializeField] private bool rethrowFromImpactPoint = true;

    [Tooltip("멈춘 위치에서 진행 방향 반대로 얼마나 물러나서 다시 던질지입니다. 처음에는 0~0.25 추천.")]
    [SerializeField] private float rethrowBackOffset = 0.15f;

    [Tooltip("멈춘 디스크 위치보다 LaunchAnchor를 얼마나 위로 올릴지입니다. 보통 0 추천.")]
    [SerializeField] private float rethrowHeightOffset = 0f;

    [SerializeField] private float rethrowDelay = 0.6f;

    [Header("Debug")]
    [SerializeField] private bool logLaunchAnchorMove = true;

    [Header("Settle After Impact")]
    [SerializeField] private float settleStableTime = 0.25f;
    [SerializeField] private float settleMaxWaitTime = 3.0f;

    [Tooltip("true면 settleMaxWaitTime이 지나면 저속 조건과 상관없이 강제로 던지기를 끝냅니다. 테스트 중에는 false 추천.")]
    [SerializeField] private bool forceFinishOnSettleTimeout = false;

    [SerializeField] private bool logSettlingStatus = true;
    [SerializeField] private float settlingStatusLogInterval = 0.5f;

    [Tooltip("true면 처음 부딪힌 지점이 아니라 튕긴 뒤 최종 정지 위치에서 다시 던집니다.")]
    [SerializeField] private bool rethrowFromFinalStopPosition = true;

    [Header("Auto Restart After Game Over")]
    [SerializeField] private bool restoreOriginalLaunchAnchorOnRunStart = true;
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

    private float launchAnchorY;
    private int throwsUsed;
    private bool runActive;

    private DiscSlingshotController subscribedDiscController;

    public int MaxThrowsPerRun => Mathf.Max(1, maxThrowsPerRun);
    public int ThrowsUsed => throwsUsed;
    public int ThrowsRemaining => Mathf.Max(0, MaxThrowsPerRun - throwsUsed);
    public bool HasThrowsRemaining => ThrowsRemaining > 0;

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
        settleStableTime = Mathf.Max(0f, settleStableTime);
        settleMaxWaitTime = Mathf.Max(0.1f, settleMaxWaitTime);
        gameOverRestartDelay = Mathf.Max(0f, gameOverRestartDelay);
    }

    public void StartRun()
    {
        if (gameOverRestartRoutine != null)
        {
            StopCoroutine(gameOverRestartRoutine);
            gameOverRestartRoutine = null;
        }

        if (rethrowRoutine != null)
        {
            StopCoroutine(rethrowRoutine);
            rethrowRoutine = null;
        }

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

        maxThrowsPerRun = Mathf.Max(1, maxThrowsPerRun);
        throwsUsed = 0;
        runActive = true;

        // 업그레이드 시스템이 있으면 스탯 적용.
        // progressionStore가 없어도 기본 스탯으로 동작 가능.
        if (progressionStore != null)
        {
            DiscRuntimeStats stats = progressionStore.BuildRuntimeStats();
            discController.ApplyStats(stats);
        }

        NotifyThrowCountChanged();
        ResetDiscForThrow();

        onRunStarted.Invoke();

        Debug.Log("새 게임 시작");
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

        throwsUsed = Mathf.Clamp(
            throwsUsed + 1,
            0,
            MaxThrowsPerRun
        );

        NotifyThrowCountChanged();

        if (!HasThrowsRemaining)
            onNoThrowsRemaining.Invoke();

        Debug.Log($"투척 {throwsUsed}/{MaxThrowsPerRun}, 남은 투척 횟수: {ThrowsRemaining}");
    }

    public void HandleDiscImpact(DiscImpactInfo impactInfo)
    {
        if (!runActive)
            return;

        if (rethrowRoutine != null)
            return;

        Debug.Log(
            $"충돌: {impactInfo.sourceName}, " +
            $"속도: {impactInfo.impactSpeed:F1}, " +
            $"남은 투척: {ThrowsRemaining}"
        );

        if (discController != null)
            discController.BeginSettlingAfterImpact();

        rethrowRoutine = StartCoroutine(SettleThenResolveImpactRoutine(impactInfo));
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

            if (discController.IsSlowEnoughToStop())
                break;

            if (settleMaxWaitTime > 0f && elapsed >= settleMaxWaitTime)
            {
                if (forceFinishOnSettleTimeout)
                {
                    stoppedByTimeout = true;

                    //Debug.LogWarning(
                    //    $"Settling forced by timeout. " +
                    //    $"elapsed: {elapsed:F2}, " +
                    //    $"speed: {discController.CurrentSpeed:F2}, " +
                    //    $"lowTimer: {discController.LowSpeedTimer:F2}/" +
                    //    $"{discController.RequiredLowSpeedDurationToStop:F2}"
                    //);

                    break;
                }

                if (!timeoutWarningShown)
                {
                    timeoutWarningShown = true;

                    //Debug.LogWarning(
                    //    $"Settling timeout reached, but throw will NOT finish yet. " +
                    //    $"Waiting for low-speed duration. " +
                    //    $"elapsed: {elapsed:F2}, " +
                    //    $"speed: {discController.CurrentSpeed:F2}, " +
                    //    $"lowTimer: {discController.LowSpeedTimer:F2}/" +
                    //    $"{discController.RequiredLowSpeedDurationToStop:F2}"
                    //);
                }
            }

            if (logSettlingStatus && Time.time >= nextLogTime)
            {
                nextLogTime = Time.time + settlingStatusLogInterval;

                //Debug.Log(
                //    $"Settling wait | " +
                //    $"elapsed: {elapsed:F2}, " +
                //    $"speed: {discController.CurrentSpeed:F2}, " +
                //    $"lowTimer: {discController.LowSpeedTimer:F2}/" +
                //    $"{discController.RequiredLowSpeedDurationToStop:F2}, " +
                //    $"ready: {discController.SettlingStopReady}"
                //);
            }

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        if (discController != null)
            discController.StopDiscImmediately();

        if (stoppedByTimeout)
        {
            Debug.LogWarning(
                "Throw ended by timeout, not by low-speed detection. " +
                "If this is not desired, turn Force Finish On Settle Timeout off."
            );
        }

        if (!HasThrowsRemaining)
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

        if (!HasThrowsRemaining)
        {
            GameOver(GameOverReason.NoThrowsRemaining);
            return;
        }

        if (discController != null)
            discController.ResetToLaunch();

        if (cameraSwitcher != null)
            cameraSwitcher.ShowLaunchCameraAt(launchAnchor);
    }

    private void CaptureOriginalLaunchAnchor()
    {
        if (launchAnchor == null)
            return;

        originalLaunchAnchorPosition = launchAnchor.position;
        originalLaunchAnchorRotation = launchAnchor.rotation;
        launchAnchorY = originalLaunchAnchorPosition.y;
        hasOriginalLaunchAnchor = true;
    }

    private void RestoreOriginalLaunchAnchor()
    {
        if (launchAnchor == null || !hasOriginalLaunchAnchor)
            return;

        launchAnchor.position = originalLaunchAnchorPosition;
        launchAnchor.rotation = originalLaunchAnchorRotation;
        launchAnchorY = originalLaunchAnchorPosition.y;
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

        if (logLaunchAnchorMove)
        {
            Debug.Log(
                $"LaunchAnchor moved. " +
                $"Stop point: {point}, " +
                $"New anchor: {newAnchorPosition}, " +
                $"Back offset: {rethrowBackOffset}, " +
                $"Height offset: {rethrowHeightOffset}"
            );
        }
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
        onThrowCountChanged.Invoke(throwsUsed, MaxThrowsPerRun);
        onThrowsRemainingChanged.Invoke(ThrowsRemaining);
    }

    private void GameOver(GameOverReason reason)
    {
        if (!runActive)
            return;

        runActive = false;

        if (rethrowRoutine != null)
        {
            StopCoroutine(rethrowRoutine);
            rethrowRoutine = null;
        }

        // 게임오버가 되면 원반을 즉시 원래 LaunchAnchor 위치에 보이게 한다.
        PlaceDiscAtOriginalLaunchAnchorForGameOver();

        NotifyThrowCountChanged();

        onGameOver.Invoke();

        if (reason == GameOverReason.NoThrowsRemaining &&
            autoRestartWhenNoThrowsRemaining)
        {
            ScheduleAutoRestart();
        }
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

        RestoreOriginalLaunchAnchor();

        gameOverRestartRoutine = null;
        StartRun();
    }

    public void RestartRunFromOriginalLaunchAnchor()
    {
        if (gameOverRestartRoutine != null)
        {
            StopCoroutine(gameOverRestartRoutine);
            gameOverRestartRoutine = null;
        }

        if (rethrowRoutine != null)
        {
            StopCoroutine(rethrowRoutine);
            rethrowRoutine = null;
        }

        RestoreOriginalLaunchAnchor();
        StartRun();
    }
    private void PlaceDiscAtOriginalLaunchAnchorForGameOver()
    {
        RestoreOriginalLaunchAnchor();

        if (discController != null)
            discController.PlaceAtLaunchAnchor(false);

        if (cameraSwitcher != null)
            cameraSwitcher.ShowLaunchCameraAt(launchAnchor);
    }
}