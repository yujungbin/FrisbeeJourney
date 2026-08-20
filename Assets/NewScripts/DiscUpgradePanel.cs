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
    private TextMeshProUGUI flightPowerText;

    [SerializeField]
    private TextMeshProUGUI durabilityText;

    [FormerlySerializedAs("liftText")]
    [SerializeField]
    private TextMeshProUGUI incomeText;

    [Header("Buttons")]
    [FormerlySerializedAs("initialThrustButton")]
    [SerializeField]
    private Button flightPowerButton;

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

        // OnChanged�� �ƴ϶� Changed�Դϴ�.
        // �ߺ� ������ �����ϱ� ���� ���� �����մϴ�.
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
        if (flightPowerButton != null)
        {
            flightPowerButton.onClick.RemoveListener(
                UpgradeFlightPower
            );

            flightPowerButton.onClick.AddListener(
                UpgradeFlightPower
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
        if (flightPowerButton != null)
        {
            flightPowerButton.onClick.RemoveListener(
                UpgradeFlightPower
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

    public void UpgradeFlightPower()
    {
        TryUpgrade(DiscUpgradeType.FlightPower);
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
                "Progression Store�� ����Ǿ� ���� �ʽ��ϴ�."
            );

            Refresh();
            return;
        }

        // UI �ܰ��� ���� �˻��Դϴ�.
        if (!CanUpgrade(upgradeType))
        {
            Debug.Log(
                $"{GetUpgradeDisplayName(upgradeType)} ���׷��̵� �Ұ�. " +
                "������ �����ϰų� �ִ� �����Դϴ�."
            );

            Refresh();
            return;
        }

        /*
         * Store.TryUpgrade() ���ο����� CanUpgrade()�� �ٽ� �˻��մϴ�.
         * Panel �˻�� UI���̰�, Store �˻�� ���� ������ ��ȣ���Դϴ�.
         */
        bool upgraded =
            progressionStore.TryUpgrade(upgradeType);

        if (!upgraded)
        {
            Debug.LogWarning(
                $"{GetUpgradeDisplayName(upgradeType)} ���׷��̵尡 " +
                "���� �˻翡�� �����߽��ϴ�."
            );

            Refresh();
        }

        /*
         * ������ ��� DiscProgressionStore.NotifyChanged()��
         * Changed �̺�Ʈ�� ȣ���ϰ�, �� �̺�Ʈ�� Refresh()�� ����˴ϴ�.
         */
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
                coinsText.text = "����: -";

            if (flightPowerText != null)
                flightPowerText.text = "�����\n������� ����";

            if (durabilityText != null)
                durabilityText.text = "������\n������� ����";

            if (incomeText != null)
                incomeText.text = "����\n������� ����";

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
        if (flightPowerText != null)
        {
            flightPowerText.text = BuildUpgradeText(
                DiscUpgradeType.FlightPower
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
        if (flightPowerButton != null)
        {
            flightPowerButton.interactable =
                CanUpgrade(DiscUpgradeType.FlightPower);
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
        if (flightPowerButton != null)
            flightPowerButton.interactable = interactable;

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
            return "������� ����";

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
                $"{displayName} Lv.{currentLevel}\n" +
                $"{FormatUpgradeValue(upgradeType, currentValue)}\n" +
                "MAX";
        }

        float nextValue =
            progressionStore.GetNextValue(upgradeType);

        int upgradeCost =
            progressionStore.GetUpgradeCost(upgradeType);

        return
            $"{displayName} Lv.{currentLevel}\n" +
            $"{FormatUpgradeValue(upgradeType, currentValue)}" +
            $" �� {FormatUpgradeValue(upgradeType, nextValue)}\n" +
            $"���: {upgradeCost:N0}";
    }

    private string GetUpgradeDisplayName(
        DiscUpgradeType upgradeType)
    {
        switch (upgradeType)
        {
            case DiscUpgradeType.FlightPower:
                return "�����";

            case DiscUpgradeType.Durability:
                return "������";

            case DiscUpgradeType.Income:
                return "����";

            default:
                return "�� �� ����";
        }
    }

    private string FormatUpgradeValue(
        DiscUpgradeType upgradeType,
        float value)
    {
        switch (upgradeType)
        {
            case DiscUpgradeType.FlightPower:
                // initialThrust ��
                return value.ToString("0.0");

            case DiscUpgradeType.Durability:
                // �ִ� ������
                return value.ToString("0");

            case DiscUpgradeType.Income:
                // ���� ȹ�� ���
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