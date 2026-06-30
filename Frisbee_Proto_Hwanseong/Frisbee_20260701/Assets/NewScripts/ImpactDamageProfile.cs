using UnityEngine;

public struct ImpactDamageResult
{
    public string sourceName;
    public float impactSpeed;
    public float damage;
    public Vector3 hitPoint;
    public Vector3 hitNormal;
    public bool stopFlightAndAllowRethrow;
}

public class ImpactDamageProfile : MonoBehaviour
{
    [Header("Info")]
    [SerializeField] private string surfaceName = "Obstacle";

    [Header("Damage")]
    [SerializeField] private float baseDamage = 10f;
    [SerializeField] private float damagePerImpactSpeed = 1.5f;
    [SerializeField] private float minImpactSpeed = 2f;
    [SerializeField] private float maxDamage = 50f;

    [Header("Behavior")]
    [SerializeField] private bool stopFlightAndAllowRethrow = true;
    [SerializeField] private bool instantBreak = false;

    public ImpactDamageResult BuildResult(Collision collision)
    {
        float impactSpeed = collision.relativeVelocity.magnitude;

        float damage = 0f;

        if (instantBreak)
        {
            damage = 999999f;
        }
        else if (impactSpeed >= minImpactSpeed)
        {
            float extraSpeed = impactSpeed - minImpactSpeed;
            damage = baseDamage + extraSpeed * damagePerImpactSpeed;

            if (maxDamage > 0f)
                damage = Mathf.Min(damage, maxDamage);
        }

        Vector3 hitPoint = transform.position;
        Vector3 hitNormal = Vector3.up;

        if (collision.contactCount > 0)
        {
            ContactPoint contact = collision.GetContact(0);
            hitPoint = contact.point;
            hitNormal = contact.normal;
        }

        return new ImpactDamageResult
        {
            sourceName = surfaceName,
            impactSpeed = impactSpeed,
            damage = damage,
            hitPoint = hitPoint,
            hitNormal = hitNormal,
            stopFlightAndAllowRethrow = stopFlightAndAllowRethrow
        };
    }
}