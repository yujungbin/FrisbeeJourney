using System;

[Serializable]
public struct DiscRuntimeStats
{
    public float initialThrust;
    public float maxDurability;
    public float lift;

    public DiscRuntimeStats(float initialThrust, float maxDurability, float lift)
    {
        this.initialThrust = initialThrust;
        this.maxDurability = maxDurability;
        this.lift = lift;
    }
}