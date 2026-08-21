using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public sealed class DiscUpgradePanel : MonoBehaviour
{
    #region Inspector References

    [Header("Progression")]
    [SerializeField]
    private DiscProgressionStore progressionStore;

    [Header("Texts")]
    [FormerlySerializedAs("coinText")]
    [SerializeField]
    private TextMeshProUGUI coinsText;

    [FormerlySerializedAs("initialThrustText")]
    [SerializeField]
    private TextMeshProUGUI liftText;

    [SerializeField]
    private TextMeshProUGUI durabilityText;

    [FormerlySerializedAs("liftText")]
    [SerializeField]
    private TextMeshProUGUI incomeText;

    [Header("Buttons")]
    [FormerlySerializedAs("LiftButton")]
    [SerializeField]
    private Button liftButton;

    [SerializeField]
    private Button durabilityButton;

    [FormerlySerializedAs("liftButton")]
    [SerializeField]
    private Button incomeButton;

    [Header("Text Formats")]
    [SerializeField]
    private string coinsFormat = "����: {0:N0}";

    #endregion


    #region Unity Lifecycle

    private void OnEnable()
    {
        SubscribeToStore();
        RegisterButtonEvents();
        Refresh();
    }

    private void OnDisable()
    {
        UnsubscribeFromStore();
        UnregisterButtonEvents();
    }

    #endregion


    #region Store Subscription

    private void SubscribeToStore()
    {
        if (progressionStore == null)
            return;

    
        progressionStore.Changed -= Refresh;
        progressionStore.Changed += Refresh;
    }

    private void UnsubscribeFromStore()
    {
        if (progressionStore == null)
            return;

        progressionStore.Changed -= Refresh;
    }

    #endregion


    #region Button Registration

    private void RegisterButtonEvents()
    {
        if (liftButton != null)
        {
            liftButton.onClick.RemoveListener(
                UpgradeLift
            );

            liftButton.onClick.AddListener(
                UpgradeLift
            );
        }

        if (durabilityButton != null)
        {
            durabilityButton.onClick.RemoveListener(
                UpgradeDurability
            );

            durabilityButton.onClick.AddListener(
                UpgradeDurability
            );
        }

        if (incomeButton != null)
        {
            incomeButton.onClick.RemoveListener(
                UpgradeIncome
            );

            incomeButton.onClick.AddListener(
                UpgradeIncome
            );
        }
    }

    private void UnregisterButtonEvents()
    {
        if (liftButton != null)
        {
            liftButton.onClick.RemoveListener(
                UpgradeLift
            );
        }

        if (durabilityButton != null)
        {
            durabilityButton.onClick.RemoveListener(
                UpgradeDurability
            );
        }

        if (incomeButton != null)
        {
            incomeButton.onClick.RemoveListener(
                UpgradeIncome
            );
        }
    }

    #endregion


    #region Public Upgrade Buttons

    public void UpgradeLift()
    {
        TryUpgrade(DiscUpgradeType.Lift);
    }

    public void UpgradeDurability()
    {
        TryUpgrade(DiscUpgradeType.Durability);
    }

    public void UpgradeIncome()
    {
        TryUpgrade(DiscUpgradeType.Income);
    }

    #endregion


    #region Upgrade Logic

    private void TryUpgrade(DiscUpgradeType upgradeType)
    {
        if (progressionStore == null)
        {
            Debug.LogWarning(
                "DiscUpgradePanel: " +
                "Progression Store is not connected."
            );

            Refresh();
            return;
        }


        if (!CanUpgrade(upgradeType))
        {
            Debug.Log(
                $"{GetUpgradeDisplayName(upgradeType)} Cannot upgrade. " +
                "Ran out of coin or max level."
            );

            Refresh();
            return;
        }

        bool upgraded =
            progressionStore.TryUpgrade(upgradeType);

        if (!upgraded)
        {
            Debug.LogWarning(
                $"{GetUpgradeDisplayName(upgradeType)} 업그레이드가 " +
                "Failed at final test."
            );

            Refresh();
        }


    }

    private bool CanUpgrade(DiscUpgradeType upgradeType)
    {
        return progressionStore != null &&
               progressionStore.CanUpgrade(upgradeType);
    }

    #endregion


    #region UI Refresh

    private void Refresh()
    {
        if (progressionStore == null)
        {
            SetAllButtonsInteractable(false);

            if (coinsText != null)
                coinsText.text = "Coin: -";

            if (liftText != null)
                liftText.text = "Lift\nNo data";

            if (durabilityText != null)
                durabilityText.text = "Durability\nNo data";

            if (incomeText != null)
                incomeText.text = "Income\nNo data";
            
            return;
        }

        // ���� ����
        if (coinsText != null)
        {
            coinsText.text = string.Format(
                coinsFormat,
                progressionStore.Coins
            );
        }

        // �� ���� ����
        if (liftText != null)
        {
            liftText.text = BuildUpgradeText(
                DiscUpgradeType.Lift
            );
        }

        if (durabilityText != null)
        {
            durabilityText.text = BuildUpgradeText(
                DiscUpgradeType.Durability
            );
        }

        if (incomeText != null)
        {
            incomeText.text = BuildUpgradeText(
                DiscUpgradeType.Income
            );
        }

        // �� ��ư�� Ȱ��ȭ ����
        if (liftButton != null)
        {
            liftButton.interactable =
                CanUpgrade(DiscUpgradeType.Lift);
        }

        if (durabilityButton != null)
        {
            durabilityButton.interactable =
                CanUpgrade(DiscUpgradeType.Durability);
        }

        if (incomeButton != null)
        {
            incomeButton.interactable =
                CanUpgrade(DiscUpgradeType.Income);
        }
    }

    private void SetAllButtonsInteractable(bool interactable)
    {
        if (liftButton != null)
            liftButton.interactable = interactable;

        if (durabilityButton != null)
            durabilityButton.interactable = interactable;

        if (incomeButton != null)
            incomeButton.interactable = interactable;
    }

    #endregion


    #region Text Building

    private string BuildUpgradeText(
        DiscUpgradeType upgradeType)
    {
        if (progressionStore == null)
            return "No data";

        string displayName =
            GetUpgradeDisplayName(upgradeType);

        int currentLevel =
            progressionStore.GetLevel(upgradeType);

        float currentValue =
            progressionStore.GetCurrentValue(upgradeType);

        bool isMaxLevel =
            progressionStore.IsMaxLevel(upgradeType);

        if (isMaxLevel)
        {
            return
                $"{displayName}\n" +
                $"{FormatUpgradeValue(upgradeType, currentValue)}\n" +
                "MAX";
        }

        float nextValue =
            progressionStore.GetNextValue(upgradeType);

        int upgradeCost =
            progressionStore.GetUpgradeCost(upgradeType);

        return
            //$"{displayName}\n" +
            //$"{FormatUpgradeValue(upgradeType, currentValue)}" +
            //$" -> {FormatUpgradeValue(upgradeType, nextValue)}\n" +
            $"{upgradeCost:N0}";
    }

    private string GetUpgradeDisplayName(
        DiscUpgradeType upgradeType)
    {
        switch (upgradeType)
        {
            case DiscUpgradeType.Lift:
                return "Lift";

            case DiscUpgradeType.Durability:
                return "Durability";

            case DiscUpgradeType.Income:
                return "Income";

            default:
                return "Unknown";
        }
    }

    private string FormatUpgradeValue(
        DiscUpgradeType upgradeType,
        float value)
    {
        switch (upgradeType)
        {
            case DiscUpgradeType.Lift:
                
                return value.ToString("0.00");

            case DiscUpgradeType.Durability:
                
                return value.ToString("0");

            case DiscUpgradeType.Income:
                
                return $"{value:0.00}��";

            default:
                return value.ToString("0.##");
        }
    }

    #endregion


    #region Panel Visibility

    public void Show()
    {
        gameObject.SetActive(true);
        Refresh();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void Toggle()
    {
        gameObject.SetActive(!gameObject.activeSelf);

        if (gameObject.activeSelf)
            Refresh();
    }

    #endregion
}