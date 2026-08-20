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

    [Tooltip("���� 0���� ����ϴ� �⺻ Lift�Դϴ�.")]
    [FormerlySerializedAs("fixedLift")]
    [SerializeField, Range(0f, 1f)]
    private float baseLift = 0.65f;

    [Tooltip(
        "���� 0���� 1�� �ö� �� �����ϴ� Lift�Դϴ�. " +
        "0���� 1 ���̷� ���ѵ˴ϴ�."
    )]
    [SerializeField, Range(0f, 1f)]
    private float liftFirstLevelIncrease = 0.04f;

    [Tooltip(
        "���� ������ �������� ���� �������� �� �������Դϴ�. " +
        "1�̸� �� ���� �������� ����, 1���� ������ �������� ���� �����մϴ�."
    )]
    [FormerlySerializedAs("flightIncreaseRetention")]

    [SerializeField, Range(0.01f, 1f)]
    private float liftIncreaseRetention = 0.9f;

    [Tooltip(
        "Lift�� ���� �ִ밪�Դϴ�. " +
        "0���� 1 ���̷θ� ������ �� �ֽ��ϴ�."
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

    [Tooltip("�⺻ ���� ȹ�� ����Դϴ�. 1�̸� 100%�Դϴ�.")]
    [SerializeField, Min(0f)]
    private float baseIncomeMultiplier = 1f;

    [Tooltip(
        "���� �������� �����ϰ� �����ϴ� ����Դϴ�. " +
        "0.1�̸� �������� +10%�Դϴ�."
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
        "���׷��̵���� �ʴ� ���� Initial Thrust�Դϴ�."
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
            // �� ���� ���� ����ŭ ����
            totalIncrease =
                liftFirstLevelIncrease *
                level;
        }
        else
        {

            /*
             * �����ϴ� �������� ��
             *
             * Lv.1 ������:
             * firstIncrease
             *
             * Lv.2 ������:
             * firstIncrease �� retention
             *
             * Lv.3 ������:
             * firstIncrease �� retention��
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
         * maximumLift�� ���� �����ߴٸ�
         * �� �� ������ �������� ���ϰ� ���� �ִ� ������ ���Դϴ�.
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