using UnityEngine;

public class DiscCameraTargetFollower : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform disc;
    [SerializeField] private Transform trackRoot;

    [Header("Offset")]
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 0.6f, 0f);

    [Header("Rotation")]
    [SerializeField] private bool useTrackForward = true;

    private void LateUpdate()
    {
        if (disc == null)
            return;

        Vector3 worldOffset = localOffset;

        if (trackRoot != null)
            worldOffset = trackRoot.TransformDirection(localOffset);

        transform.position = disc.position + worldOffset;

        Vector3 forward;

        if (useTrackForward && trackRoot != null)
        {
            forward = trackRoot.forward;
        }
        else
        {
            forward = disc.forward;
        }

        forward.y = 0f;

        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;

        transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
    }
}