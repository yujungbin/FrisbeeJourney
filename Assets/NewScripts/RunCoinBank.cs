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

    public void CommitPendingCoins()
    {
        if (progressionStore == null)
        {
            Debug.LogWarning(
                "RunCoinBank: Progression Store가 연결되지 않았습니다."
            );

            return;
        }

        int payout = PendingCoins;

        if (payout > 0)
            progressionStore.AddCoins(payout);

        pendingCoinCredit = 0f;
        NotifyChanged();
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