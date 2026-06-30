using UnityEngine;

public struct DiscImpactInfo
{
    public string sourceName;
    public float impactSpeed;
    public Vector3 hitPoint;
    public Vector3 hitNormal;
    public Collider hitCollider;
}

[RequireComponent(typeof(DiscSlingshotController))]
public class DiscImpactReceiver : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DiscSlingshotController discController;
    [SerializeField] private DiscRunManager runManager;

    [Header("Impact Filter")]
    [Tooltip("이 레이어에 속한 물체와 충돌했을 때만 정지 처리합니다.")]
    [SerializeField] private LayerMask impactLayers = ~0;

    [Tooltip("이 속도보다 느린 충돌은 무시합니다.")]
    [SerializeField] private float minImpactSpeed = 0.1f;

    [Tooltip("OnCollisionEnter를 놓쳤을 때를 대비해 OnCollisionStay에서도 처리합니다.")]
    [SerializeField] private bool handleCollisionStay = true;

    [Header("Debug")]
    [SerializeField] private bool logImpacts = true;

    private bool impactHandledThisThrow;
    private DiscSlingshotController subscribedController;

    private void Awake()
    {
        if (discController == null)
            discController = GetComponent<DiscSlingshotController>();
    }

    private void OnEnable()
    {
        SubscribeToDisc();
    }

    private void OnDisable()
    {
        UnsubscribeFromDisc();
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

        discController.Launched += ResetImpactLock;
        subscribedController = discController;
    }

    private void UnsubscribeFromDisc()
    {
        if (subscribedController == null)
            return;

        subscribedController.Launched -= ResetImpactLock;
        subscribedController = null;
    }

    private void ResetImpactLock()
    {
        impactHandledThisThrow = false;

        if (logImpacts)
            Debug.Log("Impact lock reset for new throw.");
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryHandleCollision(collision, "Enter");
    }

    private void OnCollisionStay(Collision collision)
    {
        if (!handleCollisionStay)
            return;

        TryHandleCollision(collision, "Stay");
    }

    private void TryHandleCollision(Collision collision, string phase)
    {
        if (impactHandledThisThrow)
            return;

        if (discController == null || runManager == null)
            return;

        // 비행 중일 때만 첫 충돌을 처리한다.
        // Settling 중 추가 충돌은 무시한다.
        if (!discController.IsFlying)
            return;

        if (!IsLayerAllowed(collision.collider.gameObject.layer))
            return;

        float impactSpeed = collision.relativeVelocity.magnitude;

        if (impactSpeed < minImpactSpeed)
            return;

        DiscImpactInfo impactInfo = BuildImpactInfo(collision, impactSpeed);

        impactHandledThisThrow = true;

        if (logImpacts)
        {
            Debug.Log(
                $"Disc impact {phase}: {impactInfo.sourceName}, " +
                $"speed: {impactInfo.impactSpeed:F2}"
            );
        }

        runManager.HandleDiscImpact(impactInfo);
    }

    private bool IsLayerAllowed(int layer)
    {
        int mask = 1 << layer;
        return (impactLayers.value & mask) != 0;
    }

    private DiscImpactInfo BuildImpactInfo(Collision collision, float impactSpeed)
    {
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
            impactSpeed = impactSpeed,
            hitPoint = hitPoint,
            hitNormal = hitNormal,
            hitCollider = collision.collider
        };
    }
}