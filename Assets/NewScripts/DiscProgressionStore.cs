using System;
using UnityEngine;
using UnityEngine.Events;

public sealed class DiscProgressionStore : MonoBehaviour
{
    [Serializable]
    public sealed class CoinsChangedEvent : UnityEvent<int> { }

    [Header("Configuration")]
    [SerializeField]
    private DiscProgressionConfig config;

    [Header("Defaults")]
    [SerializeField, Min(0)]
    private int defaultCoins = 0;

    [Header("Save")]
    [SerializeField]
    private string saveKeyPrefix = "DiscGame.";

    [Header("Events")]
    [SerializeField]
    private UnityEvent onProgressionChanged =
        new UnityEvent();

    [SerializeField]
    private CoinsChangedEvent onCoinsChanged =
        new CoinsChangedEvent();

    private int coins;
    private int flightPowerLevel;
    private int durabilityLevel;
    private int incomeLevel;

    public event Action Changed;

    public DiscProgressionConfig Config => config;

    public int Coins => coins;
    public int FlightPowerLevel => flightPowerLevel;
    public int DurabilityLevel => durabilityLevel;
    public int IncomeLevel => incomeLevel;

    public float InitialThrust =>
        config != null
            ? config.GetInitialThrust(flightPowerLevel)
            : 18f;

    public float MaxDurability =>
        config != null
            ? config.GetMaxDurability(durabilityLevel)
            : 100f;

    public float IncomeMultiplier =>
        config != null
            ? config.GetIncomeMultiplier(incomeLevel)
            : 1f;

    private string CoinsKey =>
        saveKeyPrefix + "Coins";

    private string FlightLevelKey =>
        saveKeyPrefix + "FlightPowerLevel";

    private string DurabilityLevelKey =>
        saveKeyPrefix + "DurabilityLevel";

    private string IncomeLevelKey =>
        saveKeyPrefix + "IncomeLevel";

    private void Awake()
    {
        Load();
    }

    private void OnValidate()
    {
        defaultCoins = Mathf.Max(0, defaultCoins);

        if (string.IsNullOrWhiteSpace(saveKeyPrefix))
            saveKeyPrefix = "DiscGame.";
    }

    public void Load()
    {
        coins = Mathf.Max(
            0,
            PlayerPrefs.GetInt(
                CoinsKey,
                defaultCoins
            )
        );

        flightPowerLevel = LoadLevel(
            FlightLevelKey,
            DiscUpgradeType.FlightPower
        );

        durabilityLevel = LoadLevel(
            DurabilityLevelKey,
            DiscUpgradeType.Durability
        );

        incomeLevel = LoadLevel(
            IncomeLevelKey,
            DiscUpgradeType.Income
        );

        NotifyChanged();
    }

    public void Save()
    {
        PlayerPrefs.SetInt(CoinsKey, coins);
        PlayerPrefs.SetInt(
            FlightLevelKey,
            flightPowerLevel
        );
        PlayerPrefs.SetInt(
            DurabilityLevelKey,
            durabilityLevel
        );
        PlayerPrefs.SetInt(
            IncomeLevelKey,
            incomeLevel
        );

        PlayerPrefs.Save();
    }

    public DiscRuntimeStats BuildRuntimeStats()
    {
        if (config == null)
        {
            Debug.LogWarning(
                "DiscProgressionStore: Config가 연결되지 않아 " +
                "기본 런타임 스탯을 사용합니다."
            );

            return new DiscRuntimeStats(
                initialThrust: 18f,
                maxDurability: 100f,
                lift: 0.65f,
                incomeMultiplier: 1f
            );
        }

        return config.BuildRuntimeStats(
            flightPowerLevel,
            durabilityLevel,
            incomeLevel
        );
    }

    public int GetLevel(DiscUpgradeType type)
    {
        switch (type)
        {
            case DiscUpgradeType.FlightPower:
                return flightPowerLevel;

            case DiscUpgradeType.Durability:
                return durabilityLevel;

            case DiscUpgradeType.Income:
                return incomeLevel;

            default:
                return 0;
        }
    }

    public float GetCurrentValue(DiscUpgradeType type)
    {
        if (config == null)
            return 0f;

        return config.GetValue(
            type,
            GetLevel(type)
        );
    }

    public float GetNextValue(DiscUpgradeType type)
    {
        if (config == null)
            return 0f;

        int currentLevel = GetLevel(type);
        int maxLevel = config.GetMaxLevel(type);

        if (currentLevel >= maxLevel)
            return config.GetValue(type, currentLevel);

        return config.GetValue(
            type,
            currentLevel + 1
        );
    }

    public int GetUpgradeCost(DiscUpgradeType type)
    {
        if (config == null)
            return -1;

        return config.GetUpgradeCost(
            type,
            GetLevel(type)
        );
    }

    public bool IsMaxLevel(DiscUpgradeType type)
    {
        if (config == null)
            return true;

        return GetLevel(type) >=
               config.GetMaxLevel(type);
    }

    public bool CanUpgrade(DiscUpgradeType type)
    {
        if (config == null)
            return false;

        if (IsMaxLevel(type))
            return false;

        int cost = GetUpgradeCost(type);

        return cost >= 0 && coins >= cost;
    }

    public bool TryUpgrade(DiscUpgradeType type)
    {
        if (!CanUpgrade(type))
            return false;

        int currentLevel = GetLevel(type);
        int cost = GetUpgradeCost(type);

        coins -= cost;
        SetLevel(type, currentLevel + 1);

        Save();
        NotifyChanged();

        Debug.Log(
            $"{type} 업그레이드 완료. " +
            $"레벨: {GetLevel(type)}, " +
            $"남은 코인: {coins}"
        );

        return true;
    }

    public void AddCoins(int amount)
    {
        if (amount <= 0)
            return;

        long newCoins = (long)coins + amount;

        coins = (int)Mathf.Clamp(
            newCoins,
            0L,
            int.MaxValue
        );

        Save();
        NotifyChanged();
    }

    public bool TrySpendCoins(int amount)
    {
        if (amount < 0)
            return false;

        if (coins < amount)
            return false;

        coins -= amount;

        Save();
        NotifyChanged();

        return true;
    }

    public void ResetAllProgress()
    {
        coins = defaultCoins;
        flightPowerLevel = 0;
        durabilityLevel = 0;
        incomeLevel = 0;

        Save();
        NotifyChanged();
    }

    private int LoadLevel(
        string key,
        DiscUpgradeType type)
    {
        int loadedLevel = Mathf.Max(
            0,
            PlayerPrefs.GetInt(key, 0)
        );

        if (config == null)
            return loadedLevel;

        return Mathf.Clamp(
            loadedLevel,
            0,
            config.GetMaxLevel(type)
        );
    }

    private void SetLevel(
        DiscUpgradeType type,
        int level)
    {
        int maxLevel =
            config != null
                ? config.GetMaxLevel(type)
                : int.MaxValue;

        level = Mathf.Clamp(level, 0, maxLevel);

        switch (type)
        {
            case DiscUpgradeType.FlightPower:
                flightPowerLevel = level;
                break;

            case DiscUpgradeType.Durability:
                durabilityLevel = level;
                break;

            case DiscUpgradeType.Income:
                incomeLevel = level;
                break;
        }
    }

    private void NotifyChanged()
    {
        Changed?.Invoke();
        onProgressionChanged.Invoke();
        onCoinsChanged.Invoke(coins);
    }
}