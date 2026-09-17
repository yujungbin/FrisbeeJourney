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
    [SerializeField]
    private RunActiveTimeTracker runTimeTracker;

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

    [Header("Temporary Distance Coin Reward")]
    [SerializeField]
    private RunCoinBank runCoinBank;

    [SerializeField]
    private DistanceCoinRewarder distanceCoinRewarder;

    [Header("Scene Navigation")]
    [SerializeField]
    private GameSceneNavigator sceneNavigator;

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

    private bool finalResultShown;

    private bool finishLineCrossed;

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
        if (runTimeTracker == null)
        {
            runTimeTracker = GetComponent<RunActiveTimeTracker>();
        }

        CaptureOriginalLaunchAnchor();
    }
    private void OnEnable()
    {
        SubscribeToDiscEvents();
    }

    private void OnDisable()
    {
        runTimeTracker?.FinishRun(false);
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
        runTimeTracker?.FinishRun(false);
        StopRunningCoroutines();
        finalResultShown = false;
        finishLineCrossed = false;

        if (discController == null)
        {
            Debug.LogError("DiscController가 연결되어 있지 않습니다.");
            return;
        }
        discController.SetRunManager(this);

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
        if (runCoinBank != null)
            runCoinBank.ResetRun();

        if (distanceCoinRewarder != null)
            distanceCoinRewarder.ResetRun();
        if (runActive)
        {
            runTimeTracker?.BeginRun(discController);
        }
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
        //if (useThrowLimit && !HasThrowsRemaining)
            //onNoThrowsRemaining.Invoke();

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

    private IEnumerator SettleThenResolveImpactRoutine(
    DiscImpactInfo impactInfo)
    {
        float elapsed = 0f;
        float nextLogTime = 0f;
        bool timeoutWarningShown = false;
        bool stoppedByTimeout = false;

        string finishReason = "Unknown";

        while (true)
        {
            if (discController == null)
            {
                finishReason = "DiscControllerMissing";
                break;
            }

            /*
             * 내구도가 파괴되어도 여기서 즉시 break하지 않습니다.
             * 원반이 충분히 느려질 때까지 자연스럽게 기다립니다.
             */

            if (discController.IsSlowEnoughToStop())
            {
                finishReason = "LowSpeedDurationReached";
                break;
            }

            if (settleMaxWaitTime > 0f &&
                elapsed >= settleMaxWaitTime)
            {
                if (forceFinishOnSettleTimeout)
                {
                    stoppedByTimeout = true;
                    finishReason = "ForcedTimeout";
                    break;
                }

                if (!timeoutWarningShown)
                {
                    timeoutWarningShown = true;

                    Debug.LogWarning(
                        $"Settling timeout reached, but waiting continues. " +
                        $"speed: {discController.CurrentSpeed:F2}, " +
                        $"lowTimer: {discController.LowSpeedTimer:F2}/" +
                        $"{discController.RequiredLowSpeedDurationToStop:F2}"
                    );
                }
            }

            if (logSettlingStatus &&
                Time.time >= nextLogTime)
            {
                nextLogTime =
                    Time.time + settlingStatusLogInterval;

                Debug.Log(
                    $"Settling wait | " +
                    $"elapsed: {elapsed:F2}, " +
                    $"speed: {discController.CurrentSpeed:F2}, " +
                    $"lowTimer: {discController.LowSpeedTimer:F2}/" +
                    $"{discController.RequiredLowSpeedDurationToStop:F2}, " +
                    $"broken: {(discDurability != null && discDurability.IsBroken)}, " +
                    $"ready: {discController.SettlingStopReady}"
                );
            }

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        Debug.Log(
            $"Settling finished | " +
            $"reason: {finishReason}, " +
            $"speedBeforeStop: " +
            $"{(discController != null ? discController.CurrentSpeed : -1f):F2}, " +
            $"elapsed: {elapsed:F2}"
        );

        if (discController != null)
            discController.StopDiscImmediately();

        if (stoppedByTimeout)
        {
            Debug.LogWarning(
                "The disc was forcibly stopped by the settling timeout."
            );
        }

        if (progressTracker != null)
            progressTracker.EndThrow();

        // 이후 기존 결과 처리 코드 유지

        if (distanceCoinRewarder != null)
            distanceCoinRewarder.AwardAvailableCoins();


        // 1. 내구도 소진 결과
        if (discDurability != null && discDurability.IsBroken)
        {
            runTimeTracker?.FinishRun(false);
            rethrowRoutine = null;
            runActive = false;
            finalResultShown = true;

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

        //if (levelCompleted)
        //{
        //    runTimeTracker?.FinishRun(false);
        //    rethrowRoutine = null;
        //    runActive = false;
        //    finalResultShown = true;

        //    if (resultScreenController != null)
        //    {
        //        resultScreenController.ShowFinalCompleteResult();
        //    }
        //    else
        //    {
        //        Debug.LogError(
        //            "ResultScreenController가 연결되지 않아 " +
        //            "완주 결과 화면을 표시할 수 없습니다."
        //        );
        //    }

        //    yield break;
        //}
        // 결승선을 정상 통과했다면 이후 충돌로 내구도가 소진되어도 완주입니다.
        if (finishLineCrossed)
        {
            rethrowRoutine = null;
            runActive = false;
            finalResultShown = true;

            // 시간은 결승선에서 이미 확정했으므로 여기서는 저장하지 않습니다.
            if (resultScreenController != null)
            {
                resultScreenController.ShowFinalCompleteResult();
            }
            else
            {
                Debug.LogError(
                    "ResultScreenController가 연결되지 않아 " +
                    "완주 결과 화면을 표시할 수 없습니다.",
                    this
                );
            }

            yield break;
        }

        // 아래에는 기존 내구도 소진, 투척 횟수 소진 등의 처리를 유지합니다.


        // 3. 투척 횟수 소진
        if (useThrowLimit && ThrowsRemaining <= 0)
        {
            ShowNoThrowsFinalResult();
            yield break;
        }


        // 4. 다음 투척 위치 계산
        Vector3 rethrowPoint = impactInfo.hitPoint;

        if (rethrowFromFinalStopPosition &&
            discController != null)
        {
            rethrowPoint =
                discController.RigidbodyPosition;
        }

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
                "ResultScreenController is not assigned. " +
                "Moving directly to the next throw.",
                this
            );

            ResetDiscForThrow();
            onRethrowReady.Invoke();
        }

        yield break;


    }
    public bool HandleFinishLineCrossed(
    DiscSlingshotController crossingDisc)
    {
        if (!isActiveAndEnabled ||
            !runActive ||
            finalResultShown ||
            finishLineCrossed)
        {
            return false;
        }

        // 현재 이 RunManager가 관리하는 원반만 인정합니다.
        if (crossingDisc == null ||
            crossingDisc != discController)
        {
            return false;
        }

        // 발사 또는 충돌 후 이동 중일 때만 완주로 인정합니다.
        if (!discController.IsFlying &&
            !discController.IsSettling)
        {
            return false;
        }

        // 결승선 통과 전에 이미 파괴된 원반은 완주가 아닙니다.
        if (discDurability != null &&
            discDurability.IsBroken)
        {
            return false;
        }

        finishLineCrossed = true;
        finalResultShown = true;
        runActive = false;

        // 진행 중인 재투척 및 자동 재시작 처리를 중단합니다.
        StopRunningCoroutines();

        // 원반을 결승선 위치에서 즉시 정지시킵니다.
        if (discController != null)
            discController.StopDiscImmediately();

        // 마지막 투척의 거리 측정을 종료하고 완주 상태로 설정합니다.
        if (progressTracker != null)
        {
            progressTracker.EndThrow();
            progressTracker.MarkLevelCompleted();
        }

        // 결승선까지의 미지급 거리 코인을 계산합니다.
        if (distanceCoinRewarder != null)
            distanceCoinRewarder.AwardAvailableCoins();

        // 완주 기록을 저장합니다.
        runTimeTracker?.FinishRun(true);

        if (resultScreenController != null)
        {
            // COMPLETE / COLLECT 최종 결과 창을 즉시 표시합니다.
            resultScreenController.ShowFinalCompleteResult();
        }
        else
        {
            Debug.LogError(
                "DiscRunManager: ResultScreenController가 연결되지 않아 " +
                "완주 결과 화면을 표시할 수 없습니다.",
                this
            );
        }

        Debug.Log(
            "Finish line crossed. Final result screen opened.",
            this
        );

        return true;
    }
    private void ShowNoThrowsFinalResult()
    {
        if (finalResultShown)
            return;

        runTimeTracker?.FinishRun(false);

        finalResultShown = true;

        runActive = false;
        rethrowRoutine = null;

        Debug.Log(
            $"DiscRunManager: Showing no-throws final result. " +
            $"Throws used: {throwsUsed}/{MaxThrowsPerRun}, " +
            $"remaining: {ThrowsRemaining}",
            this
        );

        if (resultScreenController == null)
        {
            Debug.LogError(
                "DiscRunManager: ResultScreenController가 연결되어 있지 않아 " +
                "최종 결과 화면을 표시할 수 없습니다.",
                this
            );

            return;
        }

        resultScreenController.ShowFinalNoThrowsResult();
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
        if (!runActive || finalResultShown)
            return;

        // 먼저 종료 상태를 확정해 중복 처리를 막습니다.
        runActive = false;
        finalResultShown = true;

        StopRunningCoroutines();

        // 시작 위치로 이동시키지 않고 현재 위치에서 정지합니다.
        if (discController != null)
            discController.StopDiscImmediately();

        if (progressTracker != null)
            progressTracker.EndThrow();

        // 이미 계산된 코인은 제외하고 미지급분만 추가합니다.
        if (distanceCoinRewarder != null)
            distanceCoinRewarder.AwardAvailableCoins();

        runTimeTracker?.FinishRun(false);

        NotifyThrowCountChanged();

        if (resultScreenController == null)
        {
            Debug.LogError(
                "DiscRunManager: ResultScreenController가 연결되지 않아 " +
                "최종 결과 화면을 표시할 수 없습니다.",
                this
            );
        }
        else
        {
            switch (reason)
            {
                case GameOverReason.NoThrowsRemaining:
                    resultScreenController.ShowFinalNoThrowsResult();
                    break;

                case GameOverReason.DurabilityBroken:
                    resultScreenController.ShowFinalBrokenResult();
                    break;

                default:
                    Debug.LogError(
                        $"DiscRunManager: 처리되지 않은 종료 사유: {reason}",
                        this
                    );
                    break;
            }
        }

        onGameOver.Invoke();

        // 자동 재시작하지 않습니다.
        // 결과 화면에서 COLLECT를 눌렀을 때만 Scene을 이동합니다.
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