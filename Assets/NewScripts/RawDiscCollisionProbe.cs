using UnityEngine;

[DisallowMultipleComponent]
public sealed class RawDiscCollisionProbe : MonoBehaviour
{
    [SerializeField]
    private DiscSlingshotController discController;

    [SerializeField, Min(0.05f)]
    private float stayLogInterval = 0.2f;

    private float nextStayLogTime;

    private void Awake()
    {
        if (discController == null)
        {
            discController =
                GetComponent<DiscSlingshotController>();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        LogCollision("ENTER", collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        if (Time.time < nextStayLogTime)
            return;

        nextStayLogTime =
            Time.time + stayLogInterval;

        LogCollision("STAY", collision);
    }

    private void OnCollisionExit(Collision collision)
    {
        LogCollision("EXIT", collision);
    }

    private void LogCollision(
        string phase,
        Collision collision)
    {
        Debug.LogWarning(
            $"RAW COLLISION {phase} | " +
            $"other: {collision.collider.name}, " +
            $"layer: {LayerMask.LayerToName(collision.collider.gameObject.layer)}, " +
            $"relativeSpeed: {collision.relativeVelocity.magnitude:F3}, " +
            $"flying: {(discController != null && discController.IsFlying)}, " +
            $"settling: {(discController != null && discController.IsSettling)}, " +
            $"contacts: {collision.contactCount}",
            this
        );
    }
}
