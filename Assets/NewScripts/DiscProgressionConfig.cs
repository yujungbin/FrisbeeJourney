using System;
using UnityEngine;
using UnityEngine.Serialization;


[Serializable]
public sealed class UpgradeCostRule
{
    [SerializeField, Min(0)]
    private int baseCost = 100;

    [SerializeField, Min(1f)]
    private float costGrowthPerLevel = 1.35f;


    public int GetCost(int currentLevel)
    {
        currentLevel = Mathf.Max(
            0,
            currentLevel
        );

        float cost =
            baseCost *
            Mathf.Pow(
                costGrowthPerLevel,
                currentLevel
            );

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
    // ==================================================
    // Lift Upgrade
    // ==================================================

    [Header("Lift Upgrade")]

    [Tooltip("레벨 0에서 사용하는 기본 Lift입니다.")]
    [FormerlySerializedAs("fixedLift")]
    [SerializeField, Range(0f, 1f)]
    private float baseLift = 0.65f;

    [Tooltip(
        "레벨 0에서 1로 올라갈 때 증가하는 Lift입니다. " +
        "0에서 1 사이로 제한됩니다."
    )]
    [SerializeField, Range(0f, 1f)]
    private float liftFirstLevelIncrease = 0.04f;

    [Tooltip(
        "다음 레벨의 증가량이 이전 증가량의 몇 배인지입니다. " +
        "1이면 매 레벨 증가량이 같고, 1보다 작으면 증가량이 점차 감소합니다."
    )]
    [FormerlySerializedAs("flightIncreaseRetention")]
    [SerializeField, Range(0.01f, 1f)]
    private float liftIncreaseRetention = 0.9f;

    [Tooltip(
        "Lift의 절대 최대값입니다. " +
        "0에서 1 사이로만 설정할 수 있습니다."
    )]
    [SerializeField, Range(0f, 1f)]
    private float maximumLift = 1f;

    [FormerlySerializedAs("flightMaxLevel")]
    [SerializeField, Min(1)]
    private int liftMaxLevel = 20;

    [FormerlySerializedAs("flightUpgradeCost")]
    [SerializeField]
    private UpgradeCostRule liftUpgradeCost =
        new UpgradeCostRule();


    // ==================================================
    // Durability Upgrade
    // ==================================================

    [Header("Durability Upgrade")]

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


    // ==================================================
    // Income Upgrade
    // ==================================================

    [Header("Income Upgrade")]

    [Tooltip("기본 코인 획득 배수입니다. 1이면 100%입니다.")]
    [SerializeField, Min(0f)]
    private float baseIncomeMultiplier = 1f;

    [Tooltip(
        "수입 레벨마다 일정하게 증가하는 배수입니다. " +
        "0.1이면 레벨마다 +10%입니다."
    )]
    [SerializeField, Min(0f)]
    private float incomeMultiplierPerLevel = 0.1f;

    [SerializeField, Min(1)]
    private int incomeMaxLevel = 20;

    [SerializeField]
    private UpgradeCostRule incomeUpgradeCost =
        new UpgradeCostRule();


    // ==================================================
    // Fixed Physics
    // ==================================================

    [Header("Fixed Physics")]

    [Tooltip(
        "업그레이드되지 않는 고정 Initial Thrust입니다."
    )]
    [FormerlySerializedAs("baseInitialThrust")]
    [SerializeField, Min(0.01f)]
    private float fixedInitialThrust = 18f;


    // ==================================================
    // Public Properties
    // ==================================================

    public float FixedInitialThrust =>
        fixedInitialThrust;

    public float BaseLift =>
        baseLift;

    public float MaximumLift =>
        maximumLift;


    // ==================================================
    // Validation
    // ==================================================

    private void OnValidate()
    {
        maximumLift = Mathf.Clamp01(
            maximumLift
        );

        baseLift = Mathf.Clamp(
            baseLift,
            0f,
            maximumLift
        );

        liftFirstLevelIncrease = Mathf.Clamp01(
            liftFirstLevelIncrease
        );

        liftIncreaseRetention = Mathf.Clamp(
            liftIncreaseRetention,
            0.01f,
            1f
        );

        liftMaxLevel = Mathf.Max(
            1,
            liftMaxLevel
        );


        baseMaxDurability = Mathf.Max(
            1f,
            baseMaxDurability
        );

        durabilityPerLevel = Mathf.Max(
            0f,
            durabilityPerLevel
        );

        durabilityMaxLevel = Mathf.Max(
            1,
            durabilityMaxLevel
        );


        baseIncomeMultiplier = Mathf.Max(
            0f,
            baseIncomeMultiplier
        );

        incomeMultiplierPerLevel = Mathf.Max(
            0f,
            incomeMultiplierPerLevel
        );

        incomeMaxLevel = Mathf.Max(
            1,
            incomeMaxLevel
        );


        fixedInitialThrust = Mathf.Max(
            0.01f,
            fixedInitialThrust
        );
    }


    // ==================================================
    // Lift
    // ==================================================

    public float GetLift(int level)
    {
        level = Mathf.Clamp(
            level,
            0,
            liftMaxLevel
        );

        if (level <= 0)
        {
            return Mathf.Clamp(
                baseLift,
                0f,
                maximumLift
            );
        }

        float totalIncrease;

        if (Mathf.Approximately(
            liftIncreaseRetention,
            1f))
        {
            // 매 레벨 같은 값만큼 증가
            totalIncrease =
                liftFirstLevelIncrease *
                level;
        }
        else
        {
            /*
             * 감소하는 등비수열의 합
             *
             * Lv.1 증가량:
             * firstIncrease
             *
             * Lv.2 증가량:
             * firstIncrease × retention
             *
             * Lv.3 증가량:
             * firstIncrease × retention²
             */
            totalIncrease =
                liftFirstLevelIncrease *
                (
                    1f -
                    Mathf.Pow(
                        liftIncreaseRetention,
                        level
                    )
                ) /
                (
                    1f -
                    liftIncreaseRetention
                );
        }

        return Mathf.Clamp(
            baseLift + totalIncrease,
            0f,
            maximumLift
        );
    }


    public float GetNextLiftIncrease(
        int currentLevel)
    {
        int maximumLevel =
            GetEffectiveLiftMaxLevel();

        currentLevel = Mathf.Clamp(
            currentLevel,
            0,
            maximumLevel
        );

        if (currentLevel >= maximumLevel)
            return 0f;

        float currentValue =
            GetLift(currentLevel);

        float nextValue =
            GetLift(currentLevel + 1);

        return Mathf.Max(
            0f,
            nextValue - currentValue
        );
    }


    private int GetEffectiveLiftMaxLevel()
    {
        int configuredMaximumLevel =
            Mathf.Max(
                0,
                liftMaxLevel
            );

        /*
         * maximumLift에 일찍 도달했다면
         * 그 뒤 레벨을 구매하지 못하게 실제 최대 레벨을 줄입니다.
         */
        for (int level = 0;
             level < configuredMaximumLevel;
             level++)
        {
            float currentValue =
                GetLift(level);

            float nextValue =
                GetLift(level + 1);

            if (currentValue >=
                maximumLift - 0.0001f)
            {
                return level;
            }

            if (nextValue <=
                currentValue + 0.0001f)
            {
                return level;
            }
        }

        return configuredMaximumLevel;
    }


    // ==================================================
    // Durability
    // ==================================================

    public float GetMaxDurability(int level)
    {
        level = Mathf.Clamp(
            level,
            0,
            durabilityMaxLevel
        );

        return
            baseMaxDurability +
            durabilityPerLevel *
            level;
    }


    // ==================================================
    // Income
    // ==================================================

    public float GetIncomeMultiplier(int level)
    {
        level = Mathf.Clamp(
            level,
            0,
            incomeMaxLevel
        );

        return
            baseIncomeMultiplier +
            incomeMultiplierPerLevel *
            level;
    }


    // ==================================================
    // Common Upgrade Queries
    // ==================================================

    public float GetValue(
        DiscUpgradeType type,
        int level)
    {
        switch (type)
        {
            case DiscUpgradeType.Lift:
                return GetLift(level);

            case DiscUpgradeType.Durability:
                return GetMaxDurability(level);

            case DiscUpgradeType.Income:
                return GetIncomeMultiplier(level);

            default:
                return 0f;
        }
    }


    public int GetMaxLevel(
        DiscUpgradeType type)
    {
        switch (type)
        {
            case DiscUpgradeType.Lift:
                return GetEffectiveLiftMaxLevel();

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
        int maximumLevel =
            GetMaxLevel(type);

        if (currentLevel >= maximumLevel)
            return -1;

        switch (type)
        {
            case DiscUpgradeType.Lift:
                return liftUpgradeCost != null
                    ? liftUpgradeCost.GetCost(
                        currentLevel
                    )
                    : 0;

            case DiscUpgradeType.Durability:
                return durabilityUpgradeCost != null
                    ? durabilityUpgradeCost.GetCost(
                        currentLevel
                    )
                    : 0;

            case DiscUpgradeType.Income:
                return incomeUpgradeCost != null
                    ? incomeUpgradeCost.GetCost(
                        currentLevel
                    )
                    : 0;

            default:
                return -1;
        }
    }


    // ==================================================
    // Runtime Stats
    // ==================================================

    public DiscRuntimeStats BuildRuntimeStats(
        int liftLevel,
        int durabilityLevel,
        int incomeLevel)
    {
        return new DiscRuntimeStats(
            initialThrust: fixedInitialThrust,
            maxDurability:
                GetMaxDurability(
                    durabilityLevel
                ),
            lift:
                GetLift(
                    liftLevel
                ),
            incomeMultiplier:
                GetIncomeMultiplier(
                    incomeLevel
                )
        );
    }
}