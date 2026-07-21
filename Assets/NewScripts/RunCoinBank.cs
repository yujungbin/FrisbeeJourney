using UnityEngine;
using UnityEngine.Events;

public class RunCoinBank : MonoBehaviour
{
    [System.Serializable]
    public class CoinChangedEvent : UnityEvent<int> { }

    [Header("References")]
    [SerializeField] private DiscProgressionStore progressionStore;

    [Header("Events")]
    [SerializeField]
    private CoinChangedEvent onPendingCoinsChanged =
        new CoinChangedEvent();

    private int pendingCoins;

    public int PendingCoins => pendingCoins;

    public void ResetRun()
    {
        pendingCoins = 0;
        NotifyChanged();
    }

    public void AddPendingCoins(int amount)
    {
        if (amount <= 0)
            return;

        pendingCoins += amount;
        NotifyChanged();
    }

    public void CommitPendingCoins()
    {
        if (pendingCoins <= 0)
            return;

        if (progressionStore == null)
        {
            Debug.LogWarning(
                "RunCoinBank: DiscProgressionStore가 연결되어 있지 않습니다."
            );

            return;
        }

        progressionStore.AddCoins(pendingCoins);

        pendingCoins = 0;
        NotifyChanged();
    }

    private void NotifyChanged()
    {
        onPendingCoinsChanged.Invoke(pendingCoins);
    }
}