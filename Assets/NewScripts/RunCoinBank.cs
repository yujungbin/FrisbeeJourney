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

    // �Ҽ� ������ ���� �ʵ��� ���������� float�� �����մϴ�.
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
                "RunCoinBank: Progression Store�� ������� �ʾ� " +
                "�ӽ� ������ ������ �� �����ϴ�.",
                this
            );

            return false;
        }

        int payout = PendingCoins;

        if (payout > 0)
        {
            /*
             * DiscProgressionStore.AddCoins() �ȿ���
             * Save()�� NotifyChanged()�� ȣ��Ǿ�� �մϴ�.
             */
            progressionStore.AddCoins(payout);
        }

        // ������ �������Ƿ� �̹� ���� �ӽ� ������ ���ϴ�.
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
    public void CommitPendingCoinsFromUI()
    {
        CommitPendingCoins();
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