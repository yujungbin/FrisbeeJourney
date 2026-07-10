using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(DiscSlingshotController))]
public class DiscImpactReceiver : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DiscSlingshotController discController;
    [SerializeField] private DiscRunManager runManager;
    [SerializeField] private DiscDurability durability;

    [Header("Impact Filter")]
    [Tooltip("이 레이어에 속한 물체와 충돌했을 때만 처리합니다.")]
    [SerializeField] private LayerMask impactLayers = ~0;

    [Tooltip("이 속도보다 느린 충돌은 무시합니다.")]
    [SerializeField] private float minImpactSpeed = 0.1f;

    [Tooltip("OnCollisionStay에서도 데미지를 줄지 여부입니다. 기본은 false 추천입니다.")]
    [SerializeField] private bool handleCollisionStay = false;

    [Header("Damage")]
    [Tooltip("모든 ImpactDamageProfile의 최종 데미지에 곱해지는 전체 배율입니다.")]
    [SerializeField] private float globalDamageMultiplier = 1f;

    [Tooltip("첫 충돌 후 Settling 상태에서도 2차, 3차 충돌 데미지를 적용합니다.")]
    [SerializeField] private bool applyDamageWhileSettling = true;

    [Tooltip("Settling 중 추가 충돌 데미지 배율입니다. 1이면 동일 데미지, 0.5면 절반입니다.")]
    [SerializeField] private float settlingDamageMultiplier = 0.75f;

    [Tooltip("OnCollisionStay로 들어온 데미지 배율입니다. handleCollisionStay를 켤 때만 의미 있습니다.")]
    [SerializeField] private float stayDamageMultiplier = 0.5f;

    [Header("Damage Cooldown")]
    [Tooltip("모든 충돌 데미지 사이의 최소 간격입니다.")]
    [SerializeField] private float globalDamageCooldown = 0.05f;

    [Tooltip("같은 Collider에서 다시 데미지를 받을 수 있는 최소 간격입니다.")]
    [SerializeField] private float sameColliderDamageCooldown = 0.35f;

    [Header("Missing Profile")]
    [Tooltip("ImpactDamageProfile이 없는 물체에 부딪혔을 때도 첫 충돌이면 투척 종료 처리를 할지 여부입니다.")]
    [SerializeField] private bool endThrowWhenProfileMissing = true;

    [Tooltip("ImpactDamageProfile이 없는 물체에 부딪혔을 때 적용할 기본 데미지입니다.")]
    [SerializeField] private float fallbackDamageWhenProfileMissing = 0f;

    [Header("Debug")]
    [SerializeField] private bool logImpacts = true;

    private bool firstEndingImpactSentToRunManager;
    private float nextGlobalDamageTime;

    private readonly Dictionary<Collider, float> nextDamageTimeByCollider =
        new Dictionary<Collider, float>();

    private DiscSlingshotController subscribedController;

    private void Awake()
    {
        if (discController == null)
            discController = GetComponent<DiscSlingshotController>();

        if (durability == null)
            durability = GetComponent<DiscDurability>();
    }

    private void OnEnable()
    {
        SubscribeToDisc();
    }

    private void OnDisable()
    {
        UnsubscribeFromDisc();
    }

    private void OnValidate()
    {
        minImpactSpeed = Mathf.Max(0f, minImpactSpeed);
        globalDamageMultiplier = Mathf.Max(0f, globalDamageMultiplier);
        settlingDamageMultiplier = Mathf.Max(0f, settlingDamageMultiplier);
        stayDamageMultiplier = Mathf.Max(0f, stayDamageMultiplier);
        globalDamageCooldown = Mathf.Max(0f, globalDamageCooldown);
        sameColliderDamageCooldown = Mathf.Max(0f, sameColliderDamageCooldown);
        fallbackDamageWhenProfileMissing = Mathf.Max(0f, fallbackDamageWhenProfileMissing);
    }

    private void SubscribeToDisc()
    {
        if (discController == null)
            discController = GetComponent<DiscSlingshotController>();

        if (discController == null)
            return;

        if (subscribedController == discController)
            return;

        UnsubscribeFromDisc();

        discController.Launched += ResetImpactStateForNewThrow;
        subscribedController = discController;
    }

    private void UnsubscribeFromDisc()
    {
        if (subscribedController == null)
            return;

        subscribedController.Launched -= ResetImpactStateForNewThrow;
        subscribedController = null;
    }

    private void ResetImpactStateForNewThrow()
    {
        firstEndingImpactSentToRunManager = false;
        nextGlobalDamageTime = 0f;
        nextDamageTimeByCollider.Clear();

        if (logImpacts)
            Debug.Log("Impact receiver reset for new throw.");
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryHandleCollision(collision, CollisionPhase.Enter);
    }

    private void OnCollisionStay(Collision collision)
    {
        if (!handleCollisionStay)
            return;

        TryHandleCollision(collision, CollisionPhase.Stay);
    }

    private enum CollisionPhase
    {
        Enter,
        Stay
    }

    private void TryHandleCollision(Collision collision, CollisionPhase phase)
    {
        if (discController == null || runManager == null)
            return;

        bool canProcessFlyingImpact = discController.IsFlying;

        bool canProcessSettlingDamage =
            applyDamageWhileSettling &&
            discController.IsSettling;

        if (!canProcessFlyingImpact && !canProcessSettlingDamage)
            return;

        if (!IsLayerAllowed(collision.collider.gameObject.layer))
            return;

        float impactSpeed = collision.relativeVelocity.magnitude;

        if (impactSpeed < minImpactSpeed)
            return;

        DiscImpactInfo impactInfo = BuildImpactInfo(collision);

        ApplyPhaseDamageModifiers(ref impactInfo, phase);

        bool damageApplied = TryApplyDurabilityDamage(
            impactInfo,
            collision.collider
        );

        if (logImpacts)
        {
            Debug.Log(
                $"Disc impact {phase}: {impactInfo.sourceName}, " +
                $"state: {(discController.IsFlying ? "Flying" : discController.IsSettling ? "Settling" : "Other")}, " +
                $"speed: {impactInfo.impactSpeed:F2}, " +
                $"normalImpact: {impactInfo.normalImpact01:F2}, " +
                $"angleFactor: {impactInfo.angleDamageFactor:F2}, " +
                $"damage: {impactInfo.durabilityDamage:F1}, " +
                $"damageApplied: {damageApplied}, " +
                $"durability: {(durability != null ? durability.CurrentDurability.ToString("F1") : "none")}"
            );
        }

        TrySendFirstEndingImpactToRunManager(impactInfo);
    }

    private DiscImpactInfo BuildImpactInfo(Collision collision)
    {
        ImpactDamageProfile profile =
            collision.collider.GetComponentInParent<ImpactDamageProfile>();

        if (profile != null)
            return profile.BuildImpactInfo(collision, globalDamageMultiplier);

        if (logImpacts)
        {
            Debug.LogWarning(
                $"ImpactDamageProfile이 없는 물체와 충돌했습니다: {collision.collider.name}"
            );
        }

        Vector3 hitPoint = transform.position;
        Vector3 hitNormal = Vector3.up;

        if (collision.contactCount > 0)
        {
            ContactPoint contact = collision.GetContact(0);
            hitPoint = contact.point;
            hitNormal = contact.normal;
        }

        return new DiscImpactInfo
        {
            sourceName = collision.collider.name,
            impactSpeed = collision.relativeVelocity.magnitude,
            durabilityDamage = fallbackDamageWhenProfileMissing,
            normalImpact01 = 1f,
            angleDamageFactor = 1f,
            hitPoint = hitPoint,
            hitNormal = hitNormal,
            hitCollider = collision.collider,
            endsThrow = endThrowWhenProfileMissing
        };
    }

    private void ApplyPhaseDamageModifiers(
        ref DiscImpactInfo impactInfo,
        CollisionPhase phase)
    {
        float multiplier = 1f;

        if (discController != null && discController.IsSettling)
            multiplier *= settlingDamageMultiplier;

        if (phase == CollisionPhase.Stay)
            multiplier *= stayDamageMultiplier;

        impactInfo.durabilityDamage *= multiplier;
    }

    private bool TryApplyDurabilityDamage(
        DiscImpactInfo impactInfo,
        Collider hitCollider)
    {
        if (durability == null)
            return false;

        if (durability.IsBroken)
            return false;

        if (impactInfo.durabilityDamage <= 0f)
            return false;

        if (!CanApplyDamageFromCollider(hitCollider))
            return false;

        durability.ApplyDamage(impactInfo.durabilityDamage);

        RegisterDamageCooldown(hitCollider);

        return true;
    }

    private bool CanApplyDamageFromCollider(Collider hitCollider)
    {
        if (Time.time < nextGlobalDamageTime)
            return false;

        if (hitCollider != null &&
            nextDamageTimeByCollider.TryGetValue(hitCollider, out float nextColliderTime) &&
            Time.time < nextColliderTime)
        {
            return false;
        }

        return true;
    }

    private void RegisterDamageCooldown(Collider hitCollider)
    {
        nextGlobalDamageTime = Time.time + globalDamageCooldown;

        if (hitCollider != null)
        {
            nextDamageTimeByCollider[hitCollider] =
                Time.time + sameColliderDamageCooldown;
        }
    }

    private void TrySendFirstEndingImpactToRunManager(
        DiscImpactInfo impactInfo)
    {
        if (firstEndingImpactSentToRunManager)
            return;

        if (!discController.IsFlying)
            return;

        if (!impactInfo.endsThrow)
            return;

        firstEndingImpactSentToRunManager = true;

        runManager.HandleDiscImpact(impactInfo);
    }

    private bool IsLayerAllowed(int layer)
    {
        int mask = 1 << layer;
        return (impactLayers.value & mask) != 0;
    }
}