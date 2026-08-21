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

    [Header("Upgrade Title / Level")]
    [SerializeField] private TextMeshProUGUI liftTitleText;
    [SerializeField] private TextMeshProUGUI durabilityTitleText;
    [SerializeField] private TextMeshProUGUI incomeTitleText;

    [Header("Upgrade Cost")]
    [SerializeField] private TextMeshProUGUI liftCostText;
    [SerializeField] private TextMeshProUGUI durabilityCostText;
    [SerializeField] private TextMeshProUGUI incomeCostText;

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

            if (liftTitleText != null)
                liftTitleText.text = "비행 강화 -";

            if (durabilityTitleText != null)
                durabilityTitleText.text = "내구도 강화 -";

            if (incomeTitleText != null)
                incomeTitleText.text = "수입 강화 -";

            if (liftCostText != null)
                liftCostText.text = "-";

            if (durabilityCostText != null)
                durabilityCostText.text = "-";

            if (incomeCostText != null)
                incomeCostText.text = "-";

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
        // 비행 강화
        if (liftTitleText != null)
        {
            liftTitleText.text =
                BuildTitleLevelText(DiscUpgradeType.Lift);
        }

        if (liftCostText != null)
        {
            liftCostText.text =
                BuildCostText(DiscUpgradeType.Lift);
        }

        // 내구도 강화
        if (durabilityTitleText != null)
        {
            durabilityTitleText.text =
                BuildTitleLevelText(DiscUpgradeType.Durability);
        }

        if (durabilityCostText != null)
        {
            durabilityCostText.text =
                BuildCostText(DiscUpgradeType.Durability);
        }

        // 수입 강화
        if (incomeTitleText != null)
        {
            incomeTitleText.text =
                BuildTitleLevelText(DiscUpgradeType.Income);
        }

        if (incomeCostText != null)
        {
            incomeCostText.text =
                BuildCostText(DiscUpgradeType.Income);
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

    private string BuildTitleLevelText(
    DiscUpgradeType upgradeType)
    {
        if (progressionStore == null)
            return "-";

        string displayName =
            GetUpgradeDisplayName(upgradeType);

        int currentLevel =
            progressionStore.GetLevel(upgradeType);

        // 내부 레벨은 0부터 시작하지만
        // 화면에는 LV.1부터 표시
        int displayLevel = currentLevel + 1;

        return $"{displayName}\n LV.{displayLevel}";
    }

    private string BuildCostText(
        DiscUpgradeType upgradeType)
    {
        if (progressionStore == null)
            return "-";

        if (progressionStore.IsMaxLevel(upgradeType))
            return "MAX";

        int upgradeCost =
            progressionStore.GetUpgradeCost(upgradeType);

        return $"{upgradeCost:N0}";
    }

    private string GetUpgradeDisplayName(
        DiscUpgradeType upgradeType)
    {
        switch (upgradeType)
        {
            case DiscUpgradeType.Lift:
                return "비행 강화";

            case DiscUpgradeType.Durability:
                return "내구도 강화";

            case DiscUpgradeType.Income:
                return "수입 강화";

            default:
                return "알 수 없음";
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