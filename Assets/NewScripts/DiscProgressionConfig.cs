using System;
using UnityEngine;

[Serializable]
public sealed class UpgradeCostRule
{
    [SerializeField, Min(0)]
    private int baseCost = 100;

    [SerializeField, Min(1f)]
    private float costGrowthPerLevel = 1.35f;

    public int GetCost(int currentLevel)
    {
        currentLevel = Mathf.Max(0, currentLevel);

        float cost =
            baseCost *
            Mathf.Pow(costGrowthPerLevel, currentLevel);

        return Mathf.Max(
            0,
            Mathf.RoundToInt(cost)
        );
    }
}

[CreateAssetMenu(
    fileName = "DiscProgressionConfig",
    menuName = "Disc Game/Progression/Disc Progression Config"
)]
public sealed class DiscProgressionConfig : ScriptableObject
{
    [Header("Flight Power / Initial Thrust")]
    [SerializeField, Min(0.01f)]
    private float baseInitialThrust = 18f;

    [Tooltip("레벨 0에서 1로 올라갈 때 증가하는 비행력입니다.")]
    [SerializeField, Min(0f)]
    private float flightFirstLevelIncrease = 3f;

    [Tooltip("다음 레벨 증가량이 이전 증가량의 몇 배인지입니다.")]
    [SerializeField, Range(0.01f, 1f)]
    private float flightIncreaseRetention = 0.85f;

    [SerializeField, Min(1)]
    private int flightMaxLevel = 20;

    [SerializeField]
    private UpgradeCostRule flightUpgradeCost =
        new UpgradeCostRule();

    [Header("Durability")]
    [SerializeField, Min(1f)]
    private float baseMaxDurability = 100f;

    [Tooltip("내구도는 레벨마다 이 값만큼 일정하게 증가합니다.")]
    [SerializeField, Min(0f)]
    private float durabilityPerLevel = 10f;

    [SerializeField, Min(1)]
    private int durabilityMaxLevel = 20;

    [SerializeField]
    private UpgradeCostRule durabilityUpgradeCost =
        new UpgradeCostRule();

    [Header("Income Multiplier")]
    [Tooltip("기본 코인 획득 배수입니다. 1이면 100%입니다.")]
    [SerializeField, Min(0f)]
    private float baseIncomeMultiplier = 1f;

    [Tooltip("수입 레벨마다 일정하게 증가하는 배수입니다. 0.1이면 레벨마다 +10%입니다.")]
    [SerializeField, Min(0f)]
    private float incomeMultiplierPerLevel = 0.1f;

    [SerializeField, Min(1)]
    private int incomeMaxLevel = 20;

    [SerializeField]
    private UpgradeCostRule incomeUpgradeCost =
        new UpgradeCostRule();

    [Header("Fixed Physics")]
    [Tooltip("현재는 업그레이드하지 않는 기존 양력값입니다.")]
    [SerializeField, Min(0f)]
    private float fixedLift = 0.9f;

    private void OnValidate()
    {
        baseInitialThrust = Mathf.Max(0.01f, baseInitialThrust);
        flightFirstLevelIncrease = Mathf.Max(0f, flightFirstLevelIncrease);
        flightIncreaseRetention =
            Mathf.Clamp(flightIncreaseRetention, 0.01f, 1f);
        flightMaxLevel = Mathf.Max(1, flightMaxLevel);

        baseMaxDurability = Mathf.Max(1f, baseMaxDurability);
        durabilityPerLevel = Mathf.Max(0f, durabilityPerLevel);
        durabilityMaxLevel = Mathf.Max(1, durabilityMaxLevel);

        baseIncomeMultiplier = Mathf.Max(0f, baseIncomeMultiplier);
        incomeMultiplierPerLevel =
            Mathf.Max(0f, incomeMultiplierPerLevel);
        incomeMaxLevel = Mathf.Max(1, incomeMaxLevel);

        fixedLift = Mathf.Max(0f, fixedLift);
    }

    public float GetInitialThrust(int level)
    {
        level = Mathf.Clamp(level, 0, flightMaxLevel);

        if (level <= 0)
            return baseInitialThrust;

        float totalIncrease;

        if (Mathf.Approximately(flightIncreaseRetention, 1f))
        {
            totalIncrease =
                flightFirstLevelIncrease * level;
        }
        else
        {
            // 감소하는 등비수열의 합
            totalIncrease =
                flightFirstLevelIncrease *
                (1f - Mathf.Pow(
                    flightIncreaseRetention,
                    level)) /
                (1f - flightIncreaseRetention);
        }

        return baseInitialThrust + totalIncrease;
    }

    public float GetNextFlightIncrease(int currentLevel)
    {
        currentLevel = Mathf.Clamp(
            currentLevel,
            0,
            flightMaxLevel
        );

        if (currentLevel >= flightMaxLevel)
            return 0f;

        return flightFirstLevelIncrease *
               Mathf.Pow(
                   flightIncreaseRetention,
                   currentLevel
               );
    }

    public float GetMaxDurability(int level)
    {
        level = Mathf.Clamp(
            level,
            0,
            durabilityMaxLevel
        );

        return baseMaxDurability +
               durabilityPerLevel * level;
    }

    public float GetIncomeMultiplier(int level)
    {
        level = Mathf.Clamp(
            level,
            0,
            incomeMaxLevel
        );

        return baseIncomeMultiplier +
               incomeMultiplierPerLevel * level;
    }

    public float GetValue(
        DiscUpgradeType type,
        int level)
    {
        switch (type)
        {
            case DiscUpgradeType.FlightPower:
                return GetInitialThrust(level);

            case DiscUpgradeType.Durability:
                return GetMaxDurability(level);

            case DiscUpgradeType.Income:
                return GetIncomeMultiplier(level);

            default:
                return 0f;
        }
    }

    public int GetMaxLevel(DiscUpgradeType type)
    {
        switch (type)
        {
            case DiscUpgradeType.FlightPower:
                return flightMaxLevel;

            case DiscUpgradeType.Durability:
                return durabilityMaxLevel;

            case DiscUpgradeType.Income:
                return incomeMaxLevel;

            default:
                return 0;
        }
    }

    public int GetUpgradeCost(
        DiscUpgradeType type,
        int currentLevel)
    {
        if (currentLevel >= GetMaxLevel(type))
            return -1;

        switch (type)
        {
            case DiscUpgradeType.FlightPower:
                return flightUpgradeCost != null
                    ? flightUpgradeCost.GetCost(currentLevel)
                    : 0;

            case DiscUpgradeType.Durability:
                return durabilityUpgradeCost != null
                    ? durabilityUpgradeCost.GetCost(currentLevel)
                    : 0;

            case DiscUpgradeType.Income:
                return incomeUpgradeCost != null
                    ? incomeUpgradeCost.GetCost(currentLevel)
                    : 0;

            default:
                return -1;
        }
    }

    public DiscRuntimeStats BuildRuntimeStats(
        int flightLevel,
        int durabilityLevel,
        int incomeLevel)
    {
        return new DiscRuntimeStats(
            initialThrust: GetInitialThrust(flightLevel),
            maxDurability: GetMaxDurability(durabilityLevel),
            lift: fixedLift,
            incomeMultiplier: GetIncomeMultiplier(incomeLevel)
        );
    }
}