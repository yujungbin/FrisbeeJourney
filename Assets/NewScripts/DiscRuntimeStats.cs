using System;
using UnityEngine;

[Serializable]
public struct DiscRuntimeStats
{
    [Min(0f)] public float initialThrust;
    [Min(1f)] public float maxDurability;
    [Min(0f)] public float lift;
    [Min(0f)] public float incomeMultiplier;

    [Min(0f)] public float levelZeroLift;
    [Min(1f)] public float climbMultiplier;
    [Min(1f)] public float diveMultiplier;

    public DiscRuntimeStats(
        float initialThrust,
        float maxDurability,
        float lift)
        : this(
            initialThrust,
            maxDurability,
            lift,
            1f)
    {
    }

    // 기존 호출에서는 조종력 배율 1을 사용합니다.
    public DiscRuntimeStats(
        float initialThrust,
        float maxDurability,
        float lift,
        float incomeMultiplier)
        : this(
            initialThrust,
            maxDurability,
            lift,
            incomeMultiplier,
            lift,
            1f,
            1f)
    {
    }

    public DiscRuntimeStats(
        float initialThrust,
        float maxDurability,
        float lift,
        float incomeMultiplier,
        float levelZeroLift,
        float climbMultiplier,
        float diveMultiplier)
    {
        this.initialThrust = Mathf.Max(0f, initialThrust);
        this.maxDurability = Mathf.Max(1f, maxDurability);
        this.lift = Mathf.Max(0f, lift);
        this.incomeMultiplier = Mathf.Max(0f, incomeMultiplier);

        this.levelZeroLift = Mathf.Clamp(
            levelZeroLift,
            0f,
            this.lift
        );

        this.climbMultiplier = Mathf.Max(1f, climbMultiplier);
        this.diveMultiplier = Mathf.Max(1f, diveMultiplier);
    }
}