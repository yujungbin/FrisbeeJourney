using System;
using UnityEngine;

[Serializable]
public struct DiscRuntimeStats
{
    [Min(0f)] public float initialThrust;
    [Min(1f)] public float maxDurability;
    [Min(0f)] public float lift;
    [Min(0f)] public float incomeMultiplier;

    // 기존 코드와 호환되는 생성자
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

    // 수입 배수까지 포함한 새 생성자
    public DiscRuntimeStats(
        float initialThrust,
        float maxDurability,
        float lift,
        float incomeMultiplier)
    {
        this.initialThrust = Mathf.Max(0f, initialThrust);
        this.maxDurability = Mathf.Max(1f, maxDurability);
        this.lift = Mathf.Max(0f, lift);
        this.incomeMultiplier = Mathf.Max(0f, incomeMultiplier);
    }
}