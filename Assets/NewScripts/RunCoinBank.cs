using UnityEngine;
using UnityEngine.Events;

public class RunCoinBank : MonoBehaviour
{
    [System.Serializable]
    public class CoinChangedEvent : UnityEvent<int> { }

    [Header("References")]
    [SerializeField]
    private DiscProgressionStore progressionStore;

    [Header("Events")]
    [SerializeField]
    private CoinChangedEvent onPendingCoinsChanged =
        new CoinChangedEvent();

    // 소수 배율을 잃지 않도록 내부적으로 float로 누적합니다.
    private float pendingCoinCredit;

    public int PendingCoins =>
        Mathf.FloorToInt(
            pendingCoinCredit + 0.0001f
        );

    public float PendingCoinCredit =>
        pendingCoinCredit;

    public void ResetRun()
    {
        pendingCoinCredit = 0f;
        NotifyChanged();
    }

    public void AddPendingCoins(int baseAmount)
    {
        if (baseAmount <= 0)
            return;

        float incomeMultiplier =
            progressionStore != null
                ? progressionStore.IncomeMultiplier
                : 1f;

        pendingCoinCredit +=
            baseAmount * incomeMultiplier;

        NotifyChanged();
    }

    public bool CommitPendingCoins()
    {
        if (progressionStore == null)
        {
            Debug.LogError(
                "RunCoinBank: Progression Store가 연결되지 않아 " +
                "임시 코인을 저장할 수 없습니다.",
                this
            );

            return false;
        }

        int payout = PendingCoins;

        if (payout > 0)
        {
            /*
             * DiscProgressionStore.AddCoins() 안에서
             * Save()와 NotifyChanged()가 호출되어야 합니다.
             */
            progressionStore.AddCoins(payout);
        }

        // 정산이 끝났으므로 이번 런의 임시 코인은 비웁니다.
        pendingCoinCredit = 0f;
        NotifyChanged();

        Debug.Log(
            $"Run coins committed | " +
            $"payout: {payout}, " +
            $"total coins: {progressionStore.Coins}",
            this
        );

        return true;
    }

    public void DiscardPendingCoins()
    {
        pendingCoinCredit = 0f;
        NotifyChanged();
    }

    private void NotifyChanged()
    {
        onPendingCoinsChanged.Invoke(PendingCoins);
    }
}