using TMPro;
using UnityEngine;

public class DiscUpgradePanel : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DiscProgressionStore progressionStore;

    [Header("Texts")]
    [SerializeField] private TextMeshProUGUI coinsText;
    [SerializeField] private TextMeshProUGUI initialThrustText;
    [SerializeField] private TextMeshProUGUI durabilityText;
    [SerializeField] private TextMeshProUGUI liftText;

    private void OnEnable()
    {
        if (progressionStore != null)
            progressionStore.OnChanged += Refresh;

        Refresh();
    }

    private void OnDisable()
    {
        if (progressionStore != null)
            progressionStore.OnChanged -= Refresh;
    }

    public void UpgradeInitialThrust()
    {
        TryUpgrade(DiscStatType.InitialThrust);
    }

    public void UpgradeDurability()
    {
        TryUpgrade(DiscStatType.Durability);
    }

    public void UpgradeLift()
    {
        TryUpgrade(DiscStatType.Lift);
    }

    private void TryUpgrade(DiscStatType type)
    {
        if (progressionStore == null)
            return;

        progressionStore.TryUpgrade(type);
        Refresh();
    }

    private void Refresh()
    {
        if (progressionStore == null || progressionStore.StatsConfig == null)
            return;

        if (coinsText != null)
            coinsText.text = $"Coins: {progressionStore.Coins}";

        if (initialThrustText != null)
            initialThrustText.text = BuildStatText(DiscStatType.InitialThrust);

        if (durabilityText != null)
            durabilityText.text = BuildStatText(DiscStatType.Durability);

        if (liftText != null)
            liftText.text = BuildStatText(DiscStatType.Lift);
    }

    private string BuildStatText(DiscStatType type)
    {
        DiscStatDefinition definition =
            progressionStore.StatsConfig.GetDefinition(type);

        int level = progressionStore.GetLevel(type);
        float value = definition.GetValue(level);
        int cost = definition.GetUpgradeCost(level);

        string costText = cost < 0 ? "MAX" : $"{cost} Coins";

        return
            $"{definition.displayName}\n" +
            $"Lv. {level}/{definition.maxLevel}\n" +
            $"Value: {value:F2}\n" +
            $"Upgrade: {costText}";
    }
}