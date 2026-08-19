using TMPro;
using UnityEngine;

public class CoinBalanceUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DiscProgressionStore progressionStore;
    [SerializeField] private TMP_Text coinText;

    private void Awake()
    {
        if (coinText == null)
            coinText = GetComponent<TMP_Text>();

        if (progressionStore == null)
            progressionStore = FindFirstObjectByType<DiscProgressionStore>();
    }

    private void OnEnable()
    {
        if (progressionStore != null)
            progressionStore.Changed += Refresh;

        Refresh();
    }

    private void OnDisable()
    {
        if (progressionStore != null)
            progressionStore.Changed -= Refresh;
    }

    private void Refresh()
    {
        if (coinText == null)
            return;

        if (progressionStore == null)
        {
            coinText.text = "0";
            return;
        }

        coinText.text = progressionStore.Coins.ToString();
    }
}