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
    [SerializeField] private Rigidbody discRigidbody;
    [SerializeField] private Transform trackRoot;

    [Header("Update")]
    [SerializeField] private UpdateMode updateMode = UpdateMode.Manual;

    [Header("Base Offset")]
    [Tooltip("TrackRoot 기준 오프셋입니다. X는 좌우, Y는 높이, Z는 전방입니다.")]
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 0.6f, 0f);

    [Header("Screen Lateral Framing")]
    [Tooltip("true면 steer 입력으로 화면상 좌우 위치를 추가로 만듭니다.")]
    [SerializeField] private bool useSteerDrivenScreenOffset = false;

    [SerializeField] private float maxScreenLateralOffset = 1.4f;
    [SerializeField] private float screenOffsetMoveSpeed = 4.5f;
    [SerializeField] private float screenOffsetReturnSpeed = 5.5f;
    [SerializeField] private float steerDeadZone = 0.05f;
    [SerializeField] private float steerResponseExponent = 1f;
    [SerializeField] private bool invertScreenOffset = false;

    [Header("Base Position Stabilization")]
    [SerializeField] private float basePositionLag = 0f;
    [SerializeField] private float snapDistance = 8f;

    [Header("Direction Panning")]
    [Tooltip("비행 중 원반의 수평 속도 방향을 카메라 진행 방향으로 사용합니다.")]
    [SerializeField] private bool useVelocityDirection = true;

    [Tooltip("이보다 느린 수평 속도에서는 TrackRoot 방향을 대신 사용합니다.")]
    [SerializeField] private float minimumPlanarSpeedForDirection = 0.25f;

    [Tooltip("카메라가 진행 방향으로 좌우 회전하는 최대 속도입니다. 0이면 즉시 회전합니다.")]
    [SerializeField] private float panRotationSpeed = 180f;

    [Tooltip("첫 충돌로 Settling 상태가 되면 충돌 직전의 카메라 방향을 유지합니다.")]
    [SerializeField] private bool freezeViewAfterImpact = true;

    [Header("Direction Fallback")]
    [Tooltip("속도 방향을 사용할 수 없을 때 TrackRoot.forward를 사용합니다.")]
    [SerializeField] private bool useTrackForward = true;

    [SerializeField] private bool invertForward = false;

    [Header("Debug")]
    [SerializeField] private bool drawDebug = false;

    private bool initialized;

    private Vector3 smoothedBasePosition;
    private float currentScreenLateralOffset;

    private Vector3 currentPlanarForward = Vector3.forward;
    private bool planarForwardInitialized;

    private bool viewFrozenAfterImpact;
    private bool wasFlying;

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

        minimumPlanarSpeedForDirection =
            Mathf.Max(0f, minimumPlanarSpeedForDirection);

        panRotationSpeed = Mathf.Max(0f, panRotationSpeed);
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

        UpdateImpactFreezeState();

        Vector3 desiredBasePosition = GetDesiredBasePosition();

        if (!initialized)
        {
            SnapToDisc();
            return;
        }

        if (snapIfFar &&
            Vector3.Distance(
                smoothedBasePosition,
                desiredBasePosition
            ) >= snapDistance)
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

        // 충돌 이후에는 좌우 화면 오프셋도 그대로 유지합니다.
        if (!viewFrozenAfterImpact)
            UpdateScreenLateralOffset(deltaTime);

        Vector3 forward = UpdatePlanarForward(deltaTime);
        Vector3 right = GetPlanarRight(forward);

        Vector3 targetPosition =
            smoothedBasePosition -
            right * currentScreenLateralOffset;

        transform.position = targetPosition;

        // forward의 y가 항상 0이므로 yaw만 변경됩니다.
        transform.rotation = Quaternion.LookRotation(
            forward,
            Vector3.up
        );

        if (drawDebug)
            DrawDebug(right, forward);
    }

    public void SnapToDisc()
    {
        if (disc == null)
            return;

        UpdateImpactFreezeState();

        smoothedBasePosition = GetDesiredBasePosition();

        // 충돌 후 snap이 발생해도 고정된 시점을 초기화하지 않습니다.
        if (!viewFrozenAfterImpact || !initialized)
        {
            currentScreenLateralOffset = 0f;
            SetPlanarForwardImmediately(
                GetDesiredPlanarForward()
            );
        }

        Vector3 forward = GetCurrentPlanarForward();

        transform.position =
            smoothedBasePosition -
            GetPlanarRight(forward) *
            currentScreenLateralOffset;

        transform.rotation = Quaternion.LookRotation(
            forward,
            Vector3.up
        );

        initialized = true;
    }

    private void AutoResolveReferences()
    {
        if (discController == null && disc != null)
        {
            discController =
                disc.GetComponentInParent<DiscSlingshotController>();
        }

        if (disc == null && discController != null)
            disc = discController.transform;

        if (discRigidbody == null && discController != null)
            discRigidbody = discController.GetComponent<Rigidbody>();

        if (discRigidbody == null && disc != null)
            discRigidbody = disc.GetComponentInParent<Rigidbody>();
    }

    private void UpdateImpactFreezeState()
    {
        if (discController == null)
        {
            viewFrozenAfterImpact = false;
            wasFlying = false;
            return;
        }

        bool isFlying = discController.IsFlying;

        if (!freezeViewAfterImpact)
        {
            viewFrozenAfterImpact = false;
            wasFlying = isFlying;
            return;
        }

        // 다음 투척이 시작되면 이전 충돌 고정을 해제합니다.
        if (isFlying && !wasFlying)
            viewFrozenAfterImpact = false;

        // 현재 프로젝트에서는 충돌 확정 시
        // Flying에서 Settling으로 전환됩니다.
        if (discController.IsSettling ||
            (wasFlying && !isFlying))
        {
            viewFrozenAfterImpact = true;
        }

        wasFlying = isFlying;
    }

    private Vector3 UpdatePlanarForward(float deltaTime)
    {
        if (!planarForwardInitialized)
        {
            SetPlanarForwardImmediately(
                GetDesiredPlanarForward()
            );
        }

        if (viewFrozenAfterImpact)
            return currentPlanarForward;

        Vector3 desiredForward =
            GetDesiredPlanarForward();

        if (panRotationSpeed <= 0f)
        {
            SetPlanarForwardImmediately(desiredForward);
            return currentPlanarForward;
        }

        if (deltaTime <= 0f)
            return currentPlanarForward;

        Quaternion currentRotation =
            Quaternion.LookRotation(
                currentPlanarForward,
                Vector3.up
            );

        Quaternion desiredRotation =
            Quaternion.LookRotation(
                desiredForward,
                Vector3.up
            );

        Quaternion nextRotation =
            Quaternion.RotateTowards(
                currentRotation,
                desiredRotation,
                panRotationSpeed * deltaTime
            );

        currentPlanarForward =
            Vector3.ProjectOnPlane(
                nextRotation * Vector3.forward,
                Vector3.up
            ).normalized;

        return currentPlanarForward;
    }

    private void SetPlanarForwardImmediately(Vector3 forward)
    {
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;

        currentPlanarForward = forward.normalized;
        planarForwardInitialized = true;
    }

    private Vector3 GetCurrentPlanarForward()
    {
        if (!planarForwardInitialized)
        {
            SetPlanarForwardImmediately(
                GetDesiredPlanarForward()
            );
        }

        return currentPlanarForward;
    }

    private Vector3 GetDesiredPlanarForward()
    {
        Vector3 forward = Vector3.zero;

        bool canUseFlightVelocity =
            discController == null ||
            discController.IsFlying;

        if (useVelocityDirection &&
            canUseFlightVelocity &&
            discRigidbody != null)
        {
            Vector3 planarVelocity =
                Vector3.ProjectOnPlane(
                    GetDiscVelocity(),
                    Vector3.up
                );

            float minimumSpeedSquared =
                minimumPlanarSpeedForDirection *
                minimumPlanarSpeedForDirection;

            if (planarVelocity.sqrMagnitude >=
                minimumSpeedSquared)
            {
                forward = planarVelocity.normalized;
            }
        }

        if (forward.sqrMagnitude < 0.0001f &&
             discController != null &&
             discController.IsFlying)
        {
            forward =
                discController.CurrentLaunchAimForward;
        }

        // 발사 직전 또는 속도가 너무 낮을 때 사용할 방향입니다.
        if (forward.sqrMagnitude < 0.0001f)
        {
            if (useTrackForward && trackRoot != null)
                forward = trackRoot.forward;
            else if (disc != null)
                forward = disc.forward;
            else
                forward = Vector3.forward;
        }

        if (invertForward)
            forward = -forward;

        forward.y = 0f;

        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;

        return forward.normalized;
    }

    private Vector3 GetDiscVelocity()
    {
#if UNITY_6000_0_OR_NEWER
        return discRigidbody.linearVelocity;
#else
        return discRigidbody.velocity;
#endif
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

        if (discController != null &&
            discController.IsFlying)
        {
            steer = discController.CurrentSteerInput;
        }

        if (Mathf.Abs(steer) <= steerDeadZone)
            steer = 0f;

        float targetOffset = 0f;

        if (Mathf.Abs(steer) > 0f)
        {
            float shapedInput = Mathf.Pow(
                Mathf.Clamp01(Mathf.Abs(steer)),
                steerResponseExponent
            );

            targetOffset =
                Mathf.Sign(steer) *
                shapedInput *
                maxScreenLateralOffset;
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
            -Mathf.Max(0.0001f, deltaTime) /
            lagTime
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

    private Vector3 GetPlanarRight(Vector3 forward)
    {
        Vector3 right =
            Vector3.Cross(Vector3.up, forward);

        if (right.sqrMagnitude < 0.0001f)
            return Vector3.right;

        return right.normalized;
    }

    private void DrawDebug(
        Vector3 right,
        Vector3 forward)
    {
        Debug.DrawLine(
            transform.position,
            transform.position + forward * 2f,
            Color.blue
        );

        Debug.DrawLine(
            smoothedBasePosition -
            right * maxScreenLateralOffset,
            smoothedBasePosition +
            right * maxScreenLateralOffset,
            Color.yellow
        );

        Debug.DrawLine(
            smoothedBasePosition,
            transform.position,
            Color.red
        );
    }
}