using System;
using UnityEngine;

[Serializable]
public struct RunResultSnapshot
{
    public float lastFlightDistance;
    public float totalFlightDistance;

    public int pendingCoins;

    public float currentDurability;
    public float maxDurability;

    [Range(0f, 1f)]
    public float levelProgress01;

    public bool levelCompleted;
}
