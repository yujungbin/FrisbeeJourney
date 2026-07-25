using UnityEngine;

public class CoinPickup : MonoBehaviour
{
    [SerializeField] private int amount = 1;
    [SerializeField] private DiscProgressionStore progressionStore;
    [SerializeField] private RunCoinBank runCoinBank;

    private bool collected;

    private void OnTriggerEnter(Collider other)
    {
        if (collected)
            return;

        DiscSlingshotController disc =
            other.GetComponentInParent<DiscSlingshotController>();

        if (disc == null)
            return;

        if (progressionStore == null)
        {
            Debug.LogWarning("CoinPickup에 DiscProgressionStore가 연결되어 있지 않습니다.");
            return;
        }

        collected = true;

        runCoinBank.AddPendingCoins(amount);

        gameObject.SetActive(false);
    }
}