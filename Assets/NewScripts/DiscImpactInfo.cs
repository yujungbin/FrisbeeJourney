using UnityEngine;

public struct DiscImpactInfo
{
    public string sourceName;

    public float impactSpeed;
    public float durabilityDamage;

    public float normalImpact01;
    public float angleDamageFactor;

    public Vector3 hitPoint;
    public Vector3 hitNormal;

    public Collider hitCollider;

    public bool endsThrow;
}
