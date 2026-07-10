using UnityEngine;

public class DiscCameraTargetFollower : MonoBehaviour
{
    private enum UpdateMode
    {
        LateUpdate,
        Manual
    }

    [Header("Target")]
    [SerializeField] private Transform disc;
    [SerializeField] private DiscSlingshotController discController;
    [SerializeField] private Transform trackRoot;

    [Header("Update")]
    [SerializeField] private UpdateMode updateMode = UpdateMode.Manual;

    [Header("Base Offset")]
    [Tooltip("TrackRoot 기준 오프셋입니다. X는 좌우, Y는 높이, Z는 전방입니다.")]
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 0.6f, 0f);

    [Header("Screen Lateral Framing")]
    [Tooltip("true면 원반의 실제 좌우 물리 위치가 아니라 steer 입력으로 화면상 좌우 위치를 만듭니다.")]
    [SerializeField] private bool useSteerDrivenScreenOffset = true;

    [Tooltip("steer를 최대로 줬을 때 원반이 화면상 좌우로 이동하는 최대 거리입니다. 월드 단위입니다.")]
    [SerializeField] private float maxScreenLateralOffset = 1.4f;

    [Tooltip("steer 입력으로 원반이 화면 좌우로 이동하는 속도입니다. 값이 클수록 즉각적으로 움직입니다.")]
    [SerializeField] private float screenOffsetMoveSpeed = 4.5f;

    [Tooltip("steer를 놓았을 때 원반이 화면 중심으로 돌아오는 속도입니다.")]
    [SerializeField] private float screenOffsetReturnSpeed = 5.5f;

    [Tooltip("이 값보다 작은 steer 입력은 0으로 봅니다.")]
    [SerializeField] private float steerDeadZone = 0.05f;

    [Tooltip("1이면 선형, 2면 약한 steer 입력 반응이 줄어듭니다.")]
    [SerializeField] private float steerResponseExponent = 1f;

    [Tooltip("화면상 좌우 이동 방향이 반대로 느껴지면 켜세요.")]
    [SerializeField] private bool invertScreenOffset = false;

    [Header("Base Position Stabilization")]
    [Tooltip("0이면 원반 위치를 즉시 따라갑니다. 그래도 카메라 배경이 떨리면 0.01~0.03 정도만 사용하세요.")]
    [SerializeField] private float basePositionLag = 0f;

    [Tooltip("재투척/리셋 등으로 이 거리 이상 벌어지면 즉시 붙습니다.")]
    [SerializeField] private float snapDistance = 8f;

    [Header("Rotation")]
    [SerializeField] private bool useTrackForward = true;
    [SerializeField] private bool invertForward = false;

    [Header("Debug")]
    [SerializeField] private bool drawDebug = false;

    private bool initialized;

    private Vector3 smoothedBasePosition;
    private float currentScreenLateralOffset;

    private void Awake()
    {
        AutoResolveReferences();
    }

    private void OnValidate()
    {
        maxScreenLateralOffset = Mathf.Max(0f, maxScreenLateralOffset);
        screenOffsetMoveSpeed = Mathf.Max(0f, screenOffsetMoveSpeed);
        screenOffsetReturnSpeed = Mathf.Max(0f, screenOffsetReturnSpeed);
        steerDeadZone = Mathf.Max(0f, steerDeadZone);
        steerResponseExponent = Mathf.Max(0.05f, steerResponseExponent);
        basePositionLag = Mathf.Max(0f, basePositionLag);
        snapDistance = Mathf.Max(0.01f, snapDistance);
    }

    private void LateUpdate()
    {
        if (updateMode != UpdateMode.LateUpdate)
            return;

        ManualUpdateTarget(Time.deltaTime, snapIfFar: true);
    }

    public void ManualUpdateTarget(float deltaTime, bool snapIfFar)
    {
        if (disc == null)
            return;

        Vector3 desiredBasePosition = GetDesiredBasePosition();

        if (!initialized)
        {
            SnapToDisc();
            return;
        }

        if (snapIfFar &&
            Vector3.Distance(smoothedBasePosition, desiredBasePosition) >= snapDistance)
        {
            SnapToDisc();
            return;
        }

        smoothedBasePosition = DampVector(
            smoothedBasePosition,
            desiredBasePosition,
            basePositionLag,
            deltaTime
        );

        UpdateScreenLateralOffset(deltaTime);

        Vector3 forward = GetPlanarForward();
        Vector3 right = GetPlanarRight(forward);

        // 핵심:
        // currentScreenLateralOffset이 양수이면 원반이 화면 오른쪽에 보이도록,
        // 카메라 타겟은 원반 기준 왼쪽으로 이동합니다.
        Vector3 targetPosition =
            smoothedBasePosition -
            right * currentScreenLateralOffset;

        transform.position = targetPosition;
        transform.rotation = Quaternion.LookRotation(forward, Vector3.up);

        if (drawDebug)
            DrawDebug(right, forward);
    }

    public void SnapToDisc()
    {
        if (disc == null)
            return;

        smoothedBasePosition = GetDesiredBasePosition();
        currentScreenLateralOffset = 0f;

        Vector3 forward = GetPlanarForward();

        transform.position = smoothedBasePosition;
        transform.rotation = Quaternion.LookRotation(forward, Vector3.up);

        initialized = true;
    }

    private void AutoResolveReferences()
    {
        if (discController == null && disc != null)
            discController = disc.GetComponentInParent<DiscSlingshotController>();

        if (disc == null && discController != null)
            disc = discController.transform;
    }

    private void UpdateScreenLateralOffset(float deltaTime)
    {
        if (!useSteerDrivenScreenOffset)
        {
            currentScreenLateralOffset = MoveOffsetToward(
                currentScreenLateralOffset,
                0f,
                screenOffsetReturnSpeed,
                deltaTime
            );

            return;
        }

        float steer = 0f;

        if (discController != null && discController.IsFlying)
            steer = discController.CurrentSteerInput;

        if (Mathf.Abs(steer) <= steerDeadZone)
            steer = 0f;

        float targetOffset = 0f;

        if (Mathf.Abs(steer) > 0f)
        {
            float sign = Mathf.Sign(steer);

            float shapedInput = Mathf.Pow(
                Mathf.Clamp01(Mathf.Abs(steer)),
                steerResponseExponent
            );

            targetOffset = sign * shapedInput * maxScreenLateralOffset;
        }

        if (invertScreenOffset)
            targetOffset = -targetOffset;

        float speed =
            Mathf.Approximately(targetOffset, 0f)
                ? screenOffsetReturnSpeed
                : screenOffsetMoveSpeed;

        currentScreenLateralOffset = MoveOffsetToward(
            currentScreenLateralOffset,
            targetOffset,
            speed,
            deltaTime
        );
    }

    private float MoveOffsetToward(
        float current,
        float target,
        float speed,
        float deltaTime)
    {
        if (speed <= 0f)
            return target;

        return Mathf.MoveTowards(
            current,
            target,
            speed * deltaTime
        );
    }

    private Vector3 DampVector(
        Vector3 current,
        Vector3 target,
        float lagTime,
        float deltaTime)
    {
        if (lagTime <= 0.0001f)
            return target;

        float t = 1f - Mathf.Exp(
            -Mathf.Max(0.0001f, deltaTime) / lagTime
        );

        return Vector3.Lerp(current, target, t);
    }

    private Vector3 GetDesiredBasePosition()
    {
        Vector3 worldOffset = localOffset;

        if (trackRoot != null)
            worldOffset = trackRoot.TransformDirection(localOffset);

        return disc.position + worldOffset;
    }

    private Vector3 GetPlanarForward()
    {
        Vector3 forward;

        if (useTrackForward && trackRoot != null)
        {
            forward = trackRoot.forward;
        }
        else
        {
            forward = disc != null ? disc.forward : Vector3.forward;
        }

        if (invertForward)
            forward = -forward;

        forward.y = 0f;

        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;

        return forward.normalized;
    }

    private Vector3 GetPlanarRight(Vector3 forward)
    {
        Vector3 right = Vector3.Cross(Vector3.up, forward);

        if (right.sqrMagnitude < 0.0001f)
            return Vector3.right;

        return right.normalized;
    }

    private void DrawDebug(Vector3 right, Vector3 forward)
    {
        Debug.DrawLine(
            transform.position,
            transform.position + forward * 2f,
            Color.blue
        );

        Debug.DrawLine(
            smoothedBasePosition - right * maxScreenLateralOffset,
            smoothedBasePosition + right * maxScreenLateralOffset,
            Color.yellow
        );

        Debug.DrawLine(
            smoothedBasePosition,
            transform.position,
            Color.red
        );
    }
}