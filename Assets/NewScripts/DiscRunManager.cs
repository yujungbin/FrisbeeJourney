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

    [Header("Result UI")]
    [SerializeField] private ResultScreenController resultScreenController;
    [SerializeField] private RunProgressTracker progressTracker;

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
        if (!TryApplyRuntimeStatsAndDurability())
        {
            Debug.LogError(
                "DiscRunManager: 스탯 초기화 실패로 Run을 시작하지 않습니다.",
                this
            );

            return;
        }

        runActive = true;

        NotifyThrowCountChanged();
        ResetDiscForThrow();

        if (progressTracker != null)
            progressTracker.ResetRun();

        onRunStarted.Invoke();

        Debug.Log("Run started.");
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
    private bool TryApplyRuntimeStatsAndDurability()
    {
        if (progressionStore == null)
        {
            Debug.LogError(
                "DiscRunManager: " +
                "Progression Store가 연결되지 않았습니다.",
                this
            );

            return false;
        }

        if (progressionStore.Config == null)
        {
            Debug.LogError(
                "DiscRunManager: " +
                "DiscProgressionStore의 Config가 연결되지 않았습니다. " +
                "DiscProgressionConfig asset을 연결하세요.",
                progressionStore
            );

            return false;
        }

        DiscRuntimeStats stats =
            progressionStore.BuildRuntimeStats();

        if (discController == null)
        {
            Debug.LogError(
                "DiscRunManager: Disc Controller가 연결되지 않았습니다.",
                this
            );

            return false;
        }

        // initialThrust와 lift 적용
        discController.ApplyStats(stats);

        if (discDurability == null)
        {
            Debug.LogError(
                "DiscRunManager: Disc Durability가 연결되지 않았습니다.",
                this
            );

            return false;
        }

        // maxDurability와 currentDurability 초기화
        discDurability.Initialize(
            stats.maxDurability
        );

        return true;
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
        // 게임이 진행 중이 아닐 때 발생한 발사 이벤트는 무시합니다.
        if (!runActive)
            return;

        // 실제 발사가 완료된 횟수를 증가시킵니다.
        // 투척 제한을 사용하지 않더라도 총 던진 횟수는 기록합니다.
        if (useThrowLimit)
        {
            throwsUsed = Mathf.Clamp(
                throwsUsed + 1,
                0,
                MaxThrowsPerRun
            );
        }
        else
        {
            throwsUsed++;
        }

        // 이번 투척의 비행 거리 측정을 시작합니다.
        if (progressTracker != null)
            progressTracker.BeginThrow();

        // 남은 투척 횟수 UI를 갱신합니다.
        NotifyThrowCountChanged();

        // 마지막 허용 투척이 실제로 발사된 순간 호출합니다.
        // 여기서 바로 게임오버시키지는 않습니다.
        // 원반이 충돌하고 완전히 멈춘 뒤 RunManager가 게임오버를 결정합니다.
        if (useThrowLimit && !HasThrowsRemaining)
            onNoThrowsRemaining.Invoke();

        if (useThrowLimit)
        {
            Debug.Log(
                $"Disc launched. " +
                $"Throws used: {throwsUsed}/{MaxThrowsPerRun}, " +
                $"throws remaining: {ThrowsRemaining}"
            );
        }
        else
        {
            Debug.Log(
                $"Disc launched. " +
                $"Throws used: {throwsUsed}, " +
                $"throw limit: unlimited"
            );
        }
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

        if (progressTracker != null)
            progressTracker.EndThrow();


        // 1. 내구도 소진 결과
        if (discDurability != null && discDurability.IsBroken)
        {
            rethrowRoutine = null;
            runActive = false;

            if (resultScreenController != null)
            {
                resultScreenController.ShowFinalBrokenResult();
            }
            else
            {
                Debug.LogError(
                    "ResultScreenController가 연결되지 않아 " +
                    "내구도 소진 결과 화면을 표시할 수 없습니다."
                );
            }

            yield break;
        }


        // 2. 맵 완주 결과
        bool levelCompleted =
            progressTracker != null &&
            progressTracker.LevelProgress01 >= 1f;

        if (levelCompleted)
        {
            rethrowRoutine = null;
            runActive = false;

            if (resultScreenController != null)
            {
                resultScreenController.ShowFinalCompleteResult();
            }
            else
            {
                Debug.LogError(
                    "ResultScreenController가 연결되지 않아 " +
                    "완주 결과 화면을 표시할 수 없습니다."
                );
            }

            yield break;
        }


        // 3. 투척 횟수 소진
        if (useThrowLimit && !HasThrowsRemaining)
        {
            rethrowRoutine = null;

            // 결과 화면을 아직 따로 만들지 않았다면 기존 GameOver 사용.
            GameOver(GameOverReason.NoThrowsRemaining);

            yield break;
        }


        // 4. 다음 투척 위치 계산
        Vector3 rethrowPoint = impactInfo.hitPoint;

        if (rethrowFromFinalStopPosition && discController != null)
            rethrowPoint = discController.RigidbodyPosition;

        if (rethrowFromImpactPoint)
            MoveLaunchAnchorToPoint(rethrowPoint);


        // 5. 중간 결과 화면
        rethrowRoutine = null;

        if (resultScreenController != null)
        {
            resultScreenController.ShowIntermediateResult();
        }
        else
        {
            Debug.LogWarning(
                "ResultScreenController가 연결되지 않아 " +
                "중간 결과 화면 없이 바로 재투척 상태로 이동합니다."
            );

            ResetDiscForThrow();
            onRethrowReady.Invoke();
        }

        yield break;


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

    public void ContinueAfterIntermediateResult()
    {
        if (!runActive)
            return;

        if (discDurability != null && discDurability.IsBroken)
            return;

        if (useThrowLimit && !HasThrowsRemaining)
            return;

        ResetDiscForThrow();
        onRethrowReady.Invoke();
    }
}