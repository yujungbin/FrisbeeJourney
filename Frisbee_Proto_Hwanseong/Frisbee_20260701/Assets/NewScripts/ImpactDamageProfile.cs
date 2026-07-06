using UnityEngine;

public class ImpactDamageProfile : MonoBehaviour
{
    [Header("Info")]
    [SerializeField] private string surfaceName = "Surface";

    [Header("Throw Behavior")]
    [Tooltip("true면 이 물체와 부딪힌 후 투척이 Settling 상태로 넘어갑니다.")]
    [SerializeField] private bool endThrowOnImpact = true;

    [Header("Damage")]
    [Tooltip("유효 충돌 시 기본으로 들어가는 데미지입니다.")]
    [SerializeField] private float baseDamage = 5f;

    [Tooltip("충돌 속도 1당 추가 데미지입니다.")]
    [SerializeField] private float damagePerSpeed = 1f;

    [Tooltip("이 속도보다 느리면 데미지를 0으로 봅니다.")]
    [SerializeField] private float minDamageSpeed = 0.5f;

    [Tooltip("0보다 크면 충돌 1회당 최대 데미지를 제한합니다.")]
    [SerializeField] private float maxDamage = 50f;

    [Tooltip("물체별 최종 데미지 배율입니다.")]
    [SerializeField] private float damageMultiplier = 1f;

    [Header("Angle")]
    [Tooltip("스치듯 부딪혔을 때의 최소 데미지 비율입니다. 0.25면 정면 충돌 대비 25%입니다.")]
    [SerializeField, Range(0f, 1f)]
    private float glancingDamageMultiplier = 0.35f;

    [Tooltip("충돌 각도 민감도입니다. 값이 클수록 정면 충돌과 스침 충돌의 차이가 커집니다.")]
    [SerializeField] private float angleExponent = 1.5f;

    [Header("Special")]
    [SerializeField] private bool instantBreak = false;

    public bool EndThrowOnImpact => endThrowOnImpact;

    private void OnValidate()
    {
        baseDamage = Mathf.Max(0f, baseDamage);
        damagePerSpeed = Mathf.Max(0f, damagePerSpeed);
        minDamageSpeed = Mathf.Max(0f, minDamageSpeed);
        maxDamage = Mathf.Max(0f, maxDamage);
        damageMultiplier = Mathf.Max(0f, damageMultiplier);
        angleExponent = Mathf.Max(0.05f, angleExponent);
    }

    public DiscImpactInfo BuildImpactInfo(
        Collision collision,
        float globalDamageMultiplier)
    {
        float impactSpeed = collision.relativeVelocity.magnitude;

        Vector3 hitPoint = transform.position;
        Vector3 hitNormal = Vector3.up;

        if (collision.contactCount > 0)
        {
            ContactPoint contact = collision.GetContact(0);
            hitPoint = contact.point;
            hitNormal = contact.normal;
        }

        float normalImpact01 = CalculateNormalImpact01(
            collision.relativeVelocity,
            hitNormal
        );

        float angleFactor = CalculateAngleFactor(normalImpact01);

        float damage = CalculateDamage(
            impactSpeed,
            angleFactor,
            globalDamageMultiplier
        );

        return new DiscImpactInfo
        {
            sourceName = string.IsNullOrWhiteSpace(surfaceName)
                ? gameObject.name
                : surfaceName,

            impactSpeed = impactSpeed,
            durabilityDamage = damage,

            normalImpact01 = normalImpact01,
            angleDamageFactor = angleFactor,

            hitPoint = hitPoint,
            hitNormal = hitNormal,

            hitCollider = collision.collider,
            endsThrow = endThrowOnImpact
        };
    }

    private float CalculateNormalImpact01(
        Vector3 relativeVelocity,
        Vector3 hitNormal)
    {
        if (relativeVelocity.sqrMagnitude < 0.0001f ||
            hitNormal.sqrMagnitude < 0.0001f)
        {
            return 0f;
        }

        Vector3 velocityDirection = relativeVelocity.normalized;
        Vector3 normalDirection = hitNormal.normalized;

        // 정면 충돌이면 1에 가깝고, 스치면 0에 가깝습니다.
        return Mathf.Clamp01(
            Mathf.Abs(Vector3.Dot(velocityDirection, normalDirection))
        );
    }

    private float CalculateAngleFactor(float normalImpact01)
    {
        float shapedAngle = Mathf.Pow(
            Mathf.Clamp01(normalImpact01),
            angleExponent
        );

        return Mathf.Lerp(
            glancingDamageMultiplier,
            1f,
            shapedAngle
        );
    }

    private float CalculateDamage(
        float impactSpeed,
        float angleFactor,
        float globalDamageMultiplier)
    {
        if (instantBreak)
            return 999999f;

        if (impactSpeed < minDamageSpeed)
            return 0f;

        float effectiveSpeed =
            Mathf.Max(0f, impactSpeed - minDamageSpeed);

        float rawDamage =
            baseDamage +
            effectiveSpeed * damagePerSpeed;

        float finalDamage =
            rawDamage *
            angleFactor *
            damageMultiplier *
            Mathf.Max(0f, globalDamageMultiplier);

        if (maxDamage > 0f)
            finalDamage = Mathf.Min(finalDamage, maxDamage);

        return Mathf.Max(0f, finalDamage);
    }
}