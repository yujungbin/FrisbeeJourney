using UnityEngine;

[DisallowMultipleComponent]
public sealed class DistanceCoinRewarder : MonoBehaviour
{
    public enum DistanceSource
    {
        // 원반이 실제로 이동한 궤적의 누적 길이
        TotalTravelDistance,

        // 맵 시작점에서 가장 멀리 전진한 거리
        ForwardProgress
    }

    [Header("References")]
    [SerializeField]
    private RunProgressTracker progressTracker;

    [SerializeField]
    private RunCoinBank coinBank;

    [Header("Reward")]
    [Tooltip(
        "코인 계산에 사용할 거리입니다. " +
        "Total Travel Distance는 실제 이동 궤적, " +
        "Forward Progress는 맵 전방 진행 거리입니다."
    )]
    [SerializeField]
    private DistanceSource distanceSource =
        DistanceSource.TotalTravelDistance;

    [Tooltip(
        "1미터당 획득하는 기본 코인입니다. " +
        "0.1이면 10m당 1코인, 0.25면 4m당 1코인입니다."
    )]
    [SerializeField, Min(0f)]
    private float coinsPerMeter = 0.1f;

    [Header("Debug")]
    [SerializeField]
    private bool logRewards = true;

    // 이번 런에서 이미 RunCoinBank에 전달한 기본 코인 수
    private int awardedBaseCoins;

    public float CoinsPerMeter => coinsPerMeter;

    public int AwardedBaseCoins =>
        awardedBaseCoins;

    public float CurrentRewardDistance =>
        GetRewardDistance();

    public int CurrentTargetBaseCoins =>
        CalculateTargetBaseCoins();

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnValidate()
    {
        coinsPerMeter = Mathf.Max(
            0f,
            coinsPerMeter
        );
    }

    /// <summary>
    /// 새로운 런을 시작할 때 호출합니다.
    /// RunCoinBank.ResetRun()과 함께 호출해야 합니다.
    /// </summary>
    public void ResetRun()
    {
        awardedBaseCoins = 0;

        if (logRewards)
        {
            Debug.Log(
                "Distance coin rewarder reset.",
                this
            );
        }
    }

    /// <summary>
    /// 현재까지 이동한 누적 거리를 기준으로
    /// 아직 지급하지 않은 코인을 RunCoinBank에 추가합니다.
    ///
    /// 반환값은 이번 호출로 새로 추가된 기본 코인 수입니다.
    /// </summary>
    public int AwardAvailableCoins()
    {
        ResolveReferences();

        if (progressTracker == null)
        {
            Debug.LogWarning(
                "DistanceCoinRewarder: " +
                "RunProgressTracker가 연결되어 있지 않습니다.",
                this
            );

            return 0;
        }

        if (coinBank == null)
        {
            Debug.LogWarning(
                "DistanceCoinRewarder: " +
                "RunCoinBank가 연결되어 있지 않습니다.",
                this
            );

            return 0;
        }

        int targetBaseCoins =
            CalculateTargetBaseCoins();

        int newBaseCoins = Mathf.Max(
            0,
            targetBaseCoins - awardedBaseCoins
        );

        if (newBaseCoins <= 0)
            return 0;

        awardedBaseCoins =
            targetBaseCoins;

        /*
         * RunCoinBank에서 IncomeMultiplier가 적용됩니다.
         * 여기서는 거리로 계산한 기본 코인만 전달합니다.
         */
        coinBank.AddPendingCoins(
            newBaseCoins
        );

        if (logRewards)
        {
            Debug.Log(
                $"Distance coin awarded | " +
                $"distance: {GetRewardDistance():F1}m, " +
                $"coinsPerMeter: {coinsPerMeter:F3}, " +
                $"newBaseCoins: {newBaseCoins}, " +
                $"totalBaseCoins: {awardedBaseCoins}, " +
                $"pendingCoins: {coinBank.PendingCoins}",
                this
            );
        }

        return newBaseCoins;
    }

    private int CalculateTargetBaseCoins()
    {
        float distance = Mathf.Max(
            0f,
            GetRewardDistance()
        );

        float rawCoins =
            distance * coinsPerMeter;

        /*
         * 아주 작은 부동소수점 오차로
         * 6.999999가 6으로 계산되는 상황을 줄입니다.
         */
        return Mathf.Max(
            0,
            Mathf.FloorToInt(
                rawCoins + 0.0001f
            )
        );
    }

    private float GetRewardDistance()
    {
        if (progressTracker == null)
            return 0f;

        switch (distanceSource)
        {
            case DistanceSource.TotalTravelDistance:
                return progressTracker.TotalDistance;

            case DistanceSource.ForwardProgress:
                return progressTracker.MaxForwardDistance;

            default:
                return 0f;
        }
    }

    private void ResolveReferences()
    {
        if (progressTracker == null)
        {
            progressTracker =
                GetComponent<RunProgressTracker>();
        }

        if (coinBank == null)
        {
            coinBank =
                GetComponent<RunCoinBank>();
        }
    }
}
