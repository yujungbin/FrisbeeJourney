using UnityEngine;

public class GuideCollectible : MonoBehaviour
{
    [Header("Collector")]
    [SerializeField] private string collectorTag = "Player";

    [Header("Coin")]
    [SerializeField, Min(1)] private int baseCoinValue = 1;

    [SerializeField] private RunCoinBank coinBank;

    private bool isCollected;

    private void Awake()
    {
        // 동적으로 생성되는 코인이라 Inspector 연결이 안 되어 있으면
        // 현재 씬의 RunCoinBank를 자동으로 찾습니다.
        if (coinBank == null)
        {
            coinBank = FindFirstObjectByType<RunCoinBank>();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isCollected)
            return;

        Transform rootObject = other.transform.root;

        if (!rootObject.CompareTag(collectorTag))
            return;

        isCollected = true;

        Collect();
    }

    private void Collect()
    {
        if (coinBank != null)
        {
            coinBank.AddPendingCoins(baseCoinValue);
        }
        else
        {
            Debug.LogWarning(
                "GuideCollectible: RunCoinBank를 찾을 수 없습니다.",
                this
            );
        }

        Destroy(gameObject);
    }
}