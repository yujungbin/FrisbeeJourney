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
    private string coinsFormat = "코인: {0:N0}";

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

        // OnChanged가 아니라 Changed입니다.
        // 중복 구독을 방지하기 위해 먼저 제거합니다.
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
                "Progression Store가 연결되어 있지 않습니다."
            );

            Refresh();
            return;
        }

        // UI 단계의 사전 검사입니다.
        if (!CanUpgrade(upgradeType))
        {
            Debug.Log(
                $"{GetUpgradeDisplayName(upgradeType)} 업그레이드 불가. " +
                "코인이 부족하거나 최대 레벨입니다."
            );

            Refresh();
            return;
        }

        /*
         * Store.TryUpgrade() 내부에서도 CanUpgrade()를 다시 검사합니다.
         * Panel 검사는 UI용이고, Store 검사는 실제 데이터 보호용입니다.
         */
        bool upgraded =
            progressionStore.TryUpgrade(upgradeType);

        if (!upgraded)
        {
            Debug.LogWarning(
                $"{GetUpgradeDisplayName(upgradeType)} 업그레이드가 " +
                "최종 검사에서 실패했습니다."
            );

            Refresh();
        }

        /*
         * 성공한 경우 DiscProgressionStore.NotifyChanged()가
         * Changed 이벤트를 호출하고, 그 이벤트로 Refresh()가 실행됩니다.
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
                coinsText.text = "코인: -";

            if (flightPowerText != null)
                flightPowerText.text = "비행력\n연결되지 않음";

            if (durabilityText != null)
                durabilityText.text = "내구도\n연결되지 않음";

            if (incomeText != null)
                incomeText.text = "수입\n연결되지 않음";

            return;
        }

        // 보유 코인
        if (coinsText != null)
        {
            coinsText.text = string.Format(
                coinsFormat,
                progressionStore.Coins
            );
        }

        // 각 스탯 설명
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

        // 각 버튼의 활성화 상태
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
            return "연결되지 않음";

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
            $" → {FormatUpgradeValue(upgradeType, nextValue)}\n" +
            $"비용: {upgradeCost:N0}";
    }

    private string GetUpgradeDisplayName(
        DiscUpgradeType upgradeType)
    {
        switch (upgradeType)
        {
            case DiscUpgradeType.FlightPower:
                return "비행력";

            case DiscUpgradeType.Durability:
                return "내구도";

            case DiscUpgradeType.Income:
                return "수입";

            default:
                return "알 수 없음";
        }
    }

    private string FormatUpgradeValue(
        DiscUpgradeType upgradeType,
        float value)
    {
        switch (upgradeType)
        {
            case DiscUpgradeType.FlightPower:
                // initialThrust 값
                return value.ToString("0.0");

            case DiscUpgradeType.Durability:
                // 최대 내구도
                return value.ToString("0");

            case DiscUpgradeType.Income:
                // 코인 획득 배수
                return $"{value:0.00}배";

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