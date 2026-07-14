using System;
using UnityEngine;

[Serializable]
public class DiscStatDefinition
{
    public DiscStatType statType;
    public string displayName;

    [Header("Value")]
    public float baseValue = 10f;
    public float valuePerLevel = 1f;
    public int maxLevel = 10;

    [Header("Cost")]
    public int baseCost = 100;
    public float costMultiplier = 1.25f;

    public float GetValue(int level)
    {
        int clampedLevel = Mathf.Clamp(level, 0, maxLevel);
        return baseValue + valuePerLevel * clampedLevel;
    }

    public int GetUpgradeCost(int currentLevel)
    {
        if (currentLevel >= maxLevel)
            return -1;

        int clampedLevel = Mathf.Clamp(currentLevel, 0, maxLevel);
        float safeMultiplier = Mathf.Max(1f, costMultiplier);

        return Mathf.Max(1, Mathf.RoundToInt(baseCost * Mathf.Pow(safeMultiplier, clampedLevel)));
    }
}

[CreateAssetMenu(menuName = "Disc Game/Disc Stats Config", fileName = "DiscStatsConfig")]
public class DiscStatsConfig : ScriptableObject
{
    [Header("Initial Thrust")]
    public DiscStatDefinition initialThrust = new DiscStatDefinition
    {
        statType = DiscStatType.InitialThrust,
        displayName = "초기 추진력",
        baseValue = 18f,
        valuePerLevel = 2f,
        maxLevel = 10,
        baseCost = 100,
        costMultiplier = 1.25f
    };

    [Header("Durability")]
    public DiscStatDefinition durability = new DiscStatDefinition
    {
        statType = DiscStatType.Durability,
        displayName = "내구도",
        baseValue = 100f,
        valuePerLevel = 15f,
        maxLevel = 10,
        baseCost = 120,
        costMultiplier = 1.3f
    };

    [Header("Lift")]
    public DiscStatDefinition lift = new DiscStatDefinition
    {
        statType = DiscStatType.Lift,
        displayName = "양력",
        baseValue = 0.65f,
        valuePerLevel = 0.05f,
        maxLevel = 10,
        baseCost = 150,
        costMultiplier = 1.35f
    };

    public DiscStatDefinition GetDefinition(DiscStatType type)
    {
        switch (type)
        {
            case DiscStatType.InitialThrust:
                return initialThrust;

            case DiscStatType.Durability:
                return durability;

            case DiscStatType.Lift:
                return lift;

            default:
                return initialThrust;
        }
    }

    public DiscRuntimeStats BuildRuntimeStats(
        int initialThrustLevel,
        int durabilityLevel,
        int liftLevel)
    {
        return new DiscRuntimeStats(
            initialThrust.GetValue(initialThrustLevel),
            durability.GetValue(durabilityLevel),
            lift.GetValue(liftLevel)
        );
    }
}