using UnityEngine;

public class RunProgressTracker : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform disc;
    [SerializeField] private Transform trackRoot;
    [SerializeField] private Transform levelOrigin;

    [Header("Level")]
    [SerializeField] private float levelLength = 1000f;

    private bool tracking;

    private Vector3 lastDiscPosition;
    private Vector3 runOriginPosition;

    private float currentThrowDistance;
    private float totalDistance;
    private float maxForwardDistance;

    public float CurrentThrowDistance => currentThrowDistance;
    public float TotalDistance => totalDistance;
    public float MaxForwardDistance => maxForwardDistance;

    public float LevelProgress01
    {
        get
        {
            if (levelLength <= 0f)
                return 0f;

            return Mathf.Clamp01(maxForwardDistance / levelLength);
        }
    }

    private void Update()
    {
        if (!tracking || disc == null)
            return;

        Vector3 currentPosition = disc.position;

        float segmentDistance =
            Vector3.Distance(lastDiscPosition, currentPosition);

        currentThrowDistance += segmentDistance;
        totalDistance += segmentDistance;

        UpdateForwardProgress(currentPosition);

        lastDiscPosition = currentPosition;
    }

    public void ResetRun()
    {
        tracking = false;

        currentThrowDistance = 0f;
        totalDistance = 0f;
        maxForwardDistance = 0f;

        runOriginPosition = levelOrigin != null
            ? levelOrigin.position
            : disc != null
                ? disc.position
                : Vector3.zero;

        lastDiscPosition = disc != null
            ? disc.position
            : runOriginPosition;
    }

    public void BeginThrow()
    {
        if (disc == null)
            return;

        currentThrowDistance = 0f;
        lastDiscPosition = disc.position;
        tracking = true;
    }

    public void EndThrow()
    {
        tracking = false;
    }

    private void UpdateForwardProgress(Vector3 currentPosition)
    {
        Vector3 forward = trackRoot != null
            ? trackRoot.forward
            : Vector3.forward;

        forward.y = 0f;

        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;

        forward.Normalize();

        float forwardDistance = Vector3.Dot(
            currentPosition - runOriginPosition,
            forward
        );

        maxForwardDistance = Mathf.Max(
            maxForwardDistance,
            forwardDistance
        );
    }
}