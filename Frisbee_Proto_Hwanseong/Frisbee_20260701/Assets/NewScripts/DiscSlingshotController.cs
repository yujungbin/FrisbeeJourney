using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;

using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using ETouchPhase = UnityEngine.InputSystem.TouchPhase;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class DiscSlingshotController : MonoBehaviour
{
    private enum DiscState
    {
        Ready,
        Dragging,
        Flying,
        Settling,
        Stopped
    }

    private struct PointerSample
    {
        public Vector2 screenPosition;
        public float time;

        public PointerSample(Vector2 screenPosition, float time)
        {
            this.screenPosition = screenPosition;
            this.time = time;
        }
    }

    [Header("References")]
    [SerializeField] private Camera inputCamera;
    [SerializeField] private Transform launchAnchor;
    [SerializeField] private Transform trackRoot;
    [SerializeField] private Transform visualRoot;

    [Header("Default Stats")]
    [Tooltip("DiscRunManager가 ApplyStats를 호출하기 전 사용할 기본 추진력입니다.")]
    [SerializeField] private float defaultInitialThrust = 18f;

    [Tooltip("DiscRunManager가 ApplyStats를 호출하기 전 사용할 기본 내구도입니다.")]
    [SerializeField] private float defaultMaxDurability = 100f;

    [Tooltip("DiscRunManager가 ApplyStats를 호출하기 전 사용할 기본 양력입니다.")]
    [SerializeField] private float defaultLift = 0.65f;

    [Header("Touch Start")]
    [SerializeField] private bool requireTouchOnDisc = true;
    [SerializeField] private LayerMask discHitMask = ~0;

    [Header("Pokemon Ball Throw")]
    [Tooltip("화면에서 이 픽셀 이상 드래그해도 최대 드래그로 취급합니다.")]
    [SerializeField] private float maxDragPixels = 500f;

    [Tooltip("이 픽셀보다 짧게 움직이면 던지지 않고 원위치됩니다.")]
    [SerializeField] private float minDragPixelsToThrow = 45f;

    [Tooltip("이 속도보다 빠르게 놓으면 플릭 던지기로 인정합니다. 단위: pixels/second")]
    [SerializeField] private float minFlickPixelsPerSecond = 250f;

    [Tooltip("이 속도 이상이면 최대 플릭 파워로 취급합니다. 단위: pixels/second")]
    [SerializeField] private float maxFlickPixelsPerSecond = 1800f;

    [Tooltip("마지막 몇 초 동안의 손가락 이동으로 플릭 속도를 계산할지입니다.")]
    [SerializeField] private float releaseVelocitySampleTime = 0.12f;

    [Tooltip("천천히 길게 드래그했을 때도 어느 정도 힘이 들어가게 하는 보정값입니다.")]
    [SerializeField, Range(0f, 1f)] private float slowDragPowerAssist = 0.45f;

    [Tooltip("드래그 중 손가락과 원반 중심의 처음 오프셋을 유지합니다.")]
    [SerializeField] private bool keepFingerOffset = true;

    [Tooltip("드래그 중 원반이 시작점에서 너무 멀리 이동하지 않도록 제한합니다.")]
    [SerializeField] private float maxDragWorldDistance = 4.5f;

    [Tooltip("드래그 중 원반의 최소 높이입니다. LaunchAnchor 기준 상대 Y입니다.")]
    [SerializeField] private float minDragYOffset = -0.2f;

    [Tooltip("드래그 중 원반의 최대 높이입니다. LaunchAnchor 기준 상대 Y입니다.")]
    [SerializeField] private float maxDragYOffset = 2.5f;

    [Header("Throw Direction")]
    [Tooltip("false면 아래로 드래그해도 뒤로 날아가지 않고 최소한 앞으로 보정됩니다.")]
    [SerializeField] private bool allowBackwardThrow = false;

    [SerializeField, Range(0f, 0.5f)]
    private float minForwardInputWhenBackwardDisabled = 0.12f;

    [Tooltip("낮게 던질 때의 위쪽 각도입니다.")]
    [SerializeField] private float minThrowUpAngle = 3f;

    [Tooltip("위로 강하게 던질 때의 위쪽 각도입니다.")]
    [SerializeField] private float maxThrowUpAngle = 14f;

    [Header("Flight")]
    [Tooltip("최소 발사 속도 = 초기 추진력 × 이 값")]
    [SerializeField, Range(0f, 1f)] private float minLaunchSpeedRatio = 0.45f;

    [Tooltip("비행 중 유지하려는 전방 속도 = 초기 추진력 × 이 값")]
    [SerializeField] private float targetForwardSpeedRatio = 0.85f;

    [SerializeField] private float forwardSpeedGain = 4f;
    [SerializeField] private float lateralAcceleration = 30f;
    [SerializeField] private float maxLateralSpeed = 8f;

    [Header("Track Boundary")]
    [SerializeField] private float laneHalfWidth = 4.5f;
    [SerializeField] private float boundarySpring = 40f;
    [SerializeField] private float boundaryDamping = 10f;

    [Header("Settling After Impact")]
    [Tooltip("충돌 후 튕기거나 미끄러질 때 사용할 선형 감쇠값입니다.")]
    [SerializeField] private float settlingLinearDamping = 2.5f;

    [Tooltip("충돌 후 사용할 회전 감쇠값입니다.")]
    [SerializeField] private float settlingAngularDamping = 8f;

    [Tooltip("충돌 후 바닥에서 계속 미끄러지지 않도록 수평 속도를 직접 줄이는 감속값입니다.")]
    [SerializeField] private float settlingHorizontalBrake = 3f;

    [Tooltip("이 속도 이하가 되면 정지한 것으로 봅니다. 단위: m/s")]
    [SerializeField] private float stopLinearSpeed = 0.55f;

    [Tooltip("회전 속도까지 정지 조건에 포함할지입니다. 원반 게임에서는 false 추천입니다.")]
    [SerializeField] private bool requireAngularSlowToStop = false;

    [SerializeField] private float stopAngularSpeed = 1.5f;

    [Header("Settling Stop Condition")]
    [Tooltip("충돌 후 이 시간 전에는 정지 판정을 절대 하지 않습니다.")]
    [SerializeField] private float minSettlingTimeBeforeStop = 0.35f;

    [Tooltip("속도가 Stop Linear Speed 이하인 상태가 이 시간만큼 연속 유지되어야 정지 처리됩니다.")]
    [SerializeField] private float requiredLowSpeedDurationToStop = 0.8f;

    [Tooltip("현재 저속 상태가 얼마나 연속 유지되었는지 확인용입니다.")]
    [SerializeField] private bool logLowSpeedTimer = false;

    [Header("Settling Debug")]
    [SerializeField] private bool logSettlingStopCheck = false;
    [SerializeField] private float settlingLogInterval = 0.5f;

    [Header("Post Impact Control")]
    [Tooltip("충돌 후에도 이 속도보다 빠르면 약한 비행 제어를 유지합니다.")]
    [SerializeField] private float postImpactControlOffSpeed = 0.5f;

    [Tooltip("충돌 후 좌우 조종이 얼마나 남아 있을지입니다. 0이면 조종 없음.")]
    [SerializeField, Range(0f, 1f)] private float postImpactSteeringMultiplier = 0.15f;

    [Tooltip("충돌 후 양력을 얼마나 남길지입니다. 자연스럽게 떨어져 멈추게 하려면 0 추천.")]
    [SerializeField, Range(0f, 1f)] private float postImpactLiftMultiplier = 0f;


    [Tooltip("충돌 후에도 속도에 비례해서 시각적 회전을 잠깐 유지합니다.")]
    [SerializeField] private bool spinWhilePostImpactMoving = true;

    [Header("Damping")]
    [SerializeField] private float flyingLinearDamping = 0.05f;
    [SerializeField] private float flyingAngularDamping = 0.05f;
    [SerializeField] private float stoppedLinearDamping = 4f;

    [Header("Visual")]
    [SerializeField] private float spinDegreesPerSecond = 900f;
    [SerializeField] private float bankAngle = 18f;
    [SerializeField] private float visualLerp = 12f;

    [Header("Events")]
    [SerializeField] private UnityEvent onLaunched = new UnityEvent();

    [Header("Throw Power Scaling")]
    [Tooltip("던지는 세기가 초기 추진력에 얼마나 영향을 줄지입니다. 0이면 항상 최대 추진력, 1이면 완전히 던지는 세기에 비례합니다.")]
    [SerializeField, Range(0f, 1f)]
    private float throwPowerToInitialThrust = 1f;

    [Tooltip("던지는 세기 반응 곡선입니다. 1이면 선형, 2 이상이면 약한 던지기가 더 약해지고, 0.5면 약한 던지기도 비교적 강해집니다.")]
    [SerializeField]
    private float throwPowerResponseExponent = 1f;

    [Tooltip("비행 중 targetForwardSpeed도 던지는 세기에 맞춰 낮출지입니다. 켜는 것을 추천합니다.")]
    [SerializeField]
    private bool scaleForwardTargetSpeedWithThrowPower = true;

    [Header("Throw Direction Preservation")]
    [Tooltip("던진 방향을 얼마나 TrackForward 쪽으로 보정할지입니다. 0이면 던진 방향 유지, 1이면 기존처럼 앞으로 강하게 보정합니다.")]
    [SerializeField, Range(0f, 1f)]
    private float forwardCorrectionStrength = 0.15f;

    [Tooltip("비행 중 시간이 지나면서 TrackForward 쪽으로 서서히 돌아가는 속도입니다. 0이면 추가 보정 없음. 단위: degrees/second")]
    [SerializeField]
    private float forwardCorrectionTurnSpeed = 0f;

    [Tooltip("좌우 조종 방향도 던진 방향 기준으로 할지입니다. false면 화면/트랙 기준 좌우 조종을 유지합니다.")]
    [SerializeField]
    private bool steeringRelativeToThrowDirection = false;

    



    public event UnityAction Launched;

    private Rigidbody rb;
    private DiscState state = DiscState.Ready;

    private DiscRuntimeStats runtimeStats;

    private Vector3 anchorPosition;
    private Vector3 dragTargetPosition;
    private Vector3 fingerOffsetWorld;

    private Vector2 dragStartScreen;
    private Vector2 totalDragScreen;

    private bool flightControlEnabled;
    private bool forwardAssistEnabled;
    private float settlingStartedTime;

    private readonly List<PointerSample> pointerSamples = new List<PointerSample>(12);

    private int activeFingerId = -1;
    private bool mouseDragging;

    private bool hasPendingLaunch;
    private Vector3 pendingLaunchVelocity;

    private float targetForwardSpeed;
    private float steerInput;
    private float spinAngle;
    private Quaternion visualInitialLocalRotation;

    public bool IsFlying => state == DiscState.Flying;
    public bool IsReady => state == DiscState.Ready;
    public bool IsSettling => state == DiscState.Settling;
    public Vector3 RigidbodyPosition => rb != null ? rb.position : transform.position;

    private float activeTargetForwardSpeed;
    private float lastThrowPower01;
    private float lastThrowThrustRatio = 1f;

    private Vector3 activeFlightForward;
    private Vector3 activeFlightRight;

    private float lowSpeedTimer;

    private bool settlingStopReady;
    private float nextSettlingLogTime;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (inputCamera == null)
            inputCamera = Camera.main;

        if (visualRoot != null)
            visualInitialLocalRotation = visualRoot.localRotation;

        ApplyStats(new DiscRuntimeStats(
            defaultInitialThrust,
            defaultMaxDurability,
            defaultLift
        ));

        rb.useGravity = true;

        // 양력과 바닥 충돌을 사용하므로 Y 위치 고정은 꺼야 합니다.
        rb.constraints &= ~RigidbodyConstraints.FreezePositionY;

        // 실제 Rigidbody 회전은 막고, 시각 회전은 Visual Root에서만 처리합니다.
        rb.constraints |= RigidbodyConstraints.FreezeRotation;

        SetLinearDamping(flyingLinearDamping);
        SetAngularDamping(flyingAngularDamping);
    }

    private void OnEnable()
    {
        EnhancedTouchSupport.Enable();
    }

    private void OnDisable()
    {
        EnhancedTouchSupport.Disable();
    }

    private void Start()
    {
        ResetToLaunch();
    }

    private void OnValidate()
    {
        defaultInitialThrust = Mathf.Max(1f, defaultInitialThrust);
        defaultMaxDurability = Mathf.Max(1f, defaultMaxDurability);
        defaultLift = Mathf.Max(0f, defaultLift);

        maxDragPixels = Mathf.Max(1f, maxDragPixels);
        minDragPixelsToThrow = Mathf.Max(0f, minDragPixelsToThrow);

        minFlickPixelsPerSecond = Mathf.Max(0f, minFlickPixelsPerSecond);
        maxFlickPixelsPerSecond = Mathf.Max(
            minFlickPixelsPerSecond + 1f,
            maxFlickPixelsPerSecond
        );

        releaseVelocitySampleTime = Mathf.Max(0.02f, releaseVelocitySampleTime);

        maxDragWorldDistance = Mathf.Max(0.1f, maxDragWorldDistance);
        maxDragYOffset = Mathf.Max(minDragYOffset, maxDragYOffset);

        minThrowUpAngle = Mathf.Max(0f, minThrowUpAngle);
        maxThrowUpAngle = Mathf.Max(minThrowUpAngle, maxThrowUpAngle);

        targetForwardSpeedRatio = Mathf.Max(0f, targetForwardSpeedRatio);
        forwardSpeedGain = Mathf.Max(0f, forwardSpeedGain);
        lateralAcceleration = Mathf.Max(0f, lateralAcceleration);
        maxLateralSpeed = Mathf.Max(0f, maxLateralSpeed);

        laneHalfWidth = Mathf.Max(0.1f, laneHalfWidth);
        boundarySpring = Mathf.Max(0f, boundarySpring);
        boundaryDamping = Mathf.Max(0f, boundaryDamping);

        settlingLinearDamping = Mathf.Max(0f, settlingLinearDamping);
        settlingAngularDamping = Mathf.Max(0f, settlingAngularDamping);
        settlingHorizontalBrake = Mathf.Max(0f, settlingHorizontalBrake);
        stopLinearSpeed = Mathf.Max(0.01f, stopLinearSpeed);
        stopAngularSpeed = Mathf.Max(0.01f, stopAngularSpeed);

        flyingLinearDamping = Mathf.Max(0f, flyingLinearDamping);
        flyingAngularDamping = Mathf.Max(0f, flyingAngularDamping);
        stoppedLinearDamping = Mathf.Max(0f, stoppedLinearDamping);

        throwPowerResponseExponent = Mathf.Max(0.05f, throwPowerResponseExponent);

        forwardCorrectionTurnSpeed = Mathf.Max(0f, forwardCorrectionTurnSpeed);

        requiredLowSpeedDurationToStop = Mathf.Max(0f, requiredLowSpeedDurationToStop);
        minSettlingTimeBeforeStop = Mathf.Max(0f, minSettlingTimeBeforeStop);
    }

    private void Update()
    {
        if (state == DiscState.Ready || state == DiscState.Dragging)
        {
            ReadThrowInput();
        }
        else if (state == DiscState.Flying)
        {
            ReadSteeringInput();
        }
        else
        {
            steerInput = 0f;
        }

        UpdateVisual();
    }

    private void FixedUpdate()
    {
        if (state == DiscState.Dragging && rb.isKinematic)
        {
            rb.MovePosition(dragTargetPosition);
        }

        bool launchedThisStep = false;

        if (hasPendingLaunch)
        {
            rb.isKinematic = false;

            SetLinearVelocity(Vector3.zero);
            rb.angularVelocity = Vector3.zero;

            SetLinearDamping(flyingLinearDamping);
            SetAngularDamping(flyingAngularDamping);

            state = DiscState.Flying;
            flightControlEnabled = true;
            forwardAssistEnabled = true;

            rb.AddForce(pendingLaunchVelocity, ForceMode.VelocityChange);

            hasPendingLaunch = false;
            launchedThisStep = true;
        }

        if (state == DiscState.Flying && flightControlEnabled && !launchedThisStep)
        {
            UpdateActiveFlightDirection();

            ApplyFlightControl(
                allowForwardAssist: true,
                steeringMultiplier: 1f,
                liftMultiplier: 1f,
                applyBoundary: true
            );
        }
        else if (state == DiscState.Settling)
        {
            if (flightControlEnabled)
                ApplyPostImpactFlightControl();

            ApplySettlingBrake();

            UpdateSettlingStopReadiness();
        }
    }

    private void ReadThrowInput()
    {
        bool hasTouch = ETouch.activeTouches.Count > 0;

        if (hasTouch)
        {
            ReadTouchThrowInput();
            return;
        }

        // 모바일에서 Ended 프레임을 놓쳐 activeTouches가 0이 된 경우를 대비합니다.
        if (state == DiscState.Dragging && activeFingerId >= 0)
        {
            ReleaseDrag();
            return;
        }

        ReadMouseThrowInput();
    }

    private void ReadTouchThrowInput()
    {
        if (state == DiscState.Ready)
        {
            foreach (ETouch touch in ETouch.activeTouches)
            {
                if (touch.phase != ETouchPhase.Began)
                    continue;

                Vector2 position = touch.screenPosition;

                if (IsPointerOverUI(position))
                    continue;

                if (requireTouchOnDisc && !ScreenHitsDisc(position))
                    continue;

                BeginDrag(touch.touchId, position);
                break;
            }

            return;
        }

        if (state != DiscState.Dragging)
            return;

        foreach (ETouch touch in ETouch.activeTouches)
        {
            if (touch.touchId != activeFingerId)
                continue;

            Vector2 position = touch.screenPosition;

            if (touch.phase == ETouchPhase.Moved ||
                touch.phase == ETouchPhase.Stationary)
            {
                UpdateDrag(position);
            }
            else if (touch.phase == ETouchPhase.Ended)
            {
                UpdateDrag(position);
                ReleaseDrag();
            }
            else if (touch.phase == ETouchPhase.Canceled)
            {
                CancelDrag();
            }

            return;
        }

        // 활성 터치 목록에서 기존 fingerId를 찾지 못한 경우 안전하게 발사 처리합니다.
        ReleaseDrag();
    }

    private void ReadMouseThrowInput()
    {
#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL
        if (Mouse.current == null)
            return;

        Vector2 mousePosition = Mouse.current.position.ReadValue();

        if (state == DiscState.Ready &&
            Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (IsPointerOverUI(mousePosition))
                return;

            if (requireTouchOnDisc && !ScreenHitsDisc(mousePosition))
                return;

            mouseDragging = true;
            BeginDrag(-1, mousePosition);
        }

        if (state == DiscState.Dragging && mouseDragging)
        {
            if (Mouse.current.leftButton.isPressed)
                UpdateDrag(mousePosition);

            if (Mouse.current.leftButton.wasReleasedThisFrame)
            {
                UpdateDrag(mousePosition);
                ReleaseDrag();
            }
        }
#endif
    }

    private void BeginDrag(int fingerId, Vector2 screenPosition)
    {
        state = DiscState.Dragging;
        activeFingerId = fingerId;

        anchorPosition = launchAnchor != null
            ? launchAnchor.position
            : transform.position;

        rb.isKinematic = true;
        rb.position = anchorPosition;

        dragTargetPosition = anchorPosition;
        dragStartScreen = screenPosition;
        totalDragScreen = Vector2.zero;

        pointerSamples.Clear();
        AddPointerSample(screenPosition);

        if (ScreenToCameraPlane(screenPosition, out Vector3 pointerWorld))
        {
            fingerOffsetWorld = keepFingerOffset
                ? anchorPosition - pointerWorld
                : Vector3.zero;
        }
        else
        {
            fingerOffsetWorld = Vector3.zero;
        }

        UpdateDrag(screenPosition);
    }

    private void UpdateDrag(Vector2 screenPosition)
    {
        AddPointerSample(screenPosition);

        Vector2 rawDrag = screenPosition - dragStartScreen;

        if (rawDrag.magnitude > maxDragPixels)
            rawDrag = rawDrag.normalized * maxDragPixels;

        totalDragScreen = rawDrag;

        if (!ScreenToCameraPlane(screenPosition, out Vector3 pointerWorld))
            return;

        Vector3 rawTarget = pointerWorld + fingerOffsetWorld;
        dragTargetPosition = ClampDragTarget(rawTarget);
    }

    private void ReleaseDrag()
    {
        Vector2 releaseVelocityScreen = GetRecentScreenVelocity();

        bool hasEnoughDistance =
            totalDragScreen.magnitude >= minDragPixelsToThrow;

        bool hasEnoughFlick =
            releaseVelocityScreen.magnitude >= minFlickPixelsPerSecond;

        if (!hasEnoughDistance && !hasEnoughFlick)
        {
            CancelDrag();
            return;
        }

        Vector2 throwScreenVector = hasEnoughFlick
            ? releaseVelocityScreen
            : totalDragScreen;

        Vector3 throwDirection = BuildThrowDirection(throwScreenVector);

        // 이번 투척의 비행 기준 방향을 저장한다.
        // forwardCorrectionStrength 값에 따라 던진 방향과 TrackForward 사이로 보정된다.
        SetActiveFlightDirection(throwDirection);

        float power01 = CalculateThrowPower01(totalDragScreen, releaseVelocityScreen);

        float launchSpeed = CalculateLaunchSpeedFromThrowPower(
            power01,
            out float thrustRatio
        );

        lastThrowPower01 = power01;
        lastThrowThrustRatio = thrustRatio;

        activeTargetForwardSpeed = CalculateActiveTargetForwardSpeed(thrustRatio);

        rb.position = dragTargetPosition;

        pendingLaunchVelocity = throwDirection * launchSpeed;
        hasPendingLaunch = true;
        state = DiscState.Flying;
        flightControlEnabled = true;
        activeFingerId = -1;
        mouseDragging = false;
        pointerSamples.Clear();

        Launched?.Invoke();
        onLaunched.Invoke();
    }

    private void CancelDrag()
    {
        state = DiscState.Ready;
        activeFingerId = -1;
        mouseDragging = false;

        totalDragScreen = Vector2.zero;
        pointerSamples.Clear();

        dragTargetPosition = anchorPosition;

        rb.isKinematic = true;
        rb.position = anchorPosition;
    }

    private void AddPointerSample(Vector2 screenPosition)
    {
        float now = Time.unscaledTime;

        pointerSamples.Add(new PointerSample(screenPosition, now));

        while (pointerSamples.Count > 2 &&
               now - pointerSamples[0].time > releaseVelocitySampleTime)
        {
            pointerSamples.RemoveAt(0);
        }
    }

    private Vector2 GetRecentScreenVelocity()
    {
        if (pointerSamples.Count < 2)
            return Vector2.zero;

        PointerSample oldest = pointerSamples[0];
        PointerSample newest = pointerSamples[pointerSamples.Count - 1];

        float dt = Mathf.Max(0.001f, newest.time - oldest.time);
        return (newest.screenPosition - oldest.screenPosition) / dt;
    }

    private float CalculateThrowPower01(
        Vector2 dragDistance,
        Vector2 releaseVelocity)
    {
        float drag01 = Mathf.Clamp01(dragDistance.magnitude / maxDragPixels);

        float flick01 = Mathf.InverseLerp(
            minFlickPixelsPerSecond,
            maxFlickPixelsPerSecond,
            releaseVelocity.magnitude
        );

        float slowDragPower = drag01 * slowDragPowerAssist;

        return Mathf.Clamp01(Mathf.Max(slowDragPower, flick01));
    }

    private Vector3 BuildThrowDirection(Vector2 screenVector)
    {
        if (screenVector.sqrMagnitude < 0.0001f)
            return AddUpAngle(GetTrackForward(), minThrowUpAngle);

        Vector2 input = screenVector.normalized;

        Vector3 forward = GetTrackForward();
        Vector3 right = GetTrackRight();

        float forwardInput = input.y;

        if (!allowBackwardThrow)
        {
            forwardInput = Mathf.Max(
                forwardInput,
                minForwardInputWhenBackwardDisabled
            );
        }

        Vector3 flatDirection =
            right * input.x +
            forward * forwardInput;

        flatDirection.y = 0f;

        if (flatDirection.sqrMagnitude < 0.0001f)
            flatDirection = forward;

        flatDirection.Normalize();

        float upward01 = Mathf.Clamp01(input.y);

        float upAngle = Mathf.Lerp(
            minThrowUpAngle,
            maxThrowUpAngle,
            upward01
        );

        return AddUpAngle(flatDirection, upAngle);
    }

    private Vector3 AddUpAngle(Vector3 flatDirection, float angleDegrees)
    {
        flatDirection = Vector3.ProjectOnPlane(flatDirection, Vector3.up);

        if (flatDirection.sqrMagnitude < 0.0001f)
            flatDirection = GetTrackForward();

        flatDirection.Normalize();

        float angleRad = angleDegrees * Mathf.Deg2Rad;

        Vector3 direction =
            flatDirection * Mathf.Cos(angleRad) +
            Vector3.up * Mathf.Sin(angleRad);

        return direction.normalized;
    }

    private void ReadSteeringInput()
    {
        float input = 0f;

        if (ETouch.activeTouches.Count > 0)
        {
            foreach (ETouch touch in ETouch.activeTouches)
            {
                Vector2 position = touch.screenPosition;

                if (IsPointerOverUI(position))
                    continue;

                float halfWidth = Screen.width * 0.5f;

                input = Mathf.Clamp(
                    (position.x - halfWidth) / halfWidth,
                    -1f,
                    1f
                );

                break;
            }
        }
        else
        {
#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL
            if (Keyboard.current != null)
            {
                if (Keyboard.current.aKey.isPressed ||
                    Keyboard.current.leftArrowKey.isPressed)
                {
                    input -= 1f;
                }

                if (Keyboard.current.dKey.isPressed ||
                    Keyboard.current.rightArrowKey.isPressed)
                {
                    input += 1f;
                }
            }
#endif
        }

        steerInput = Mathf.Clamp(input, -1f, 1f);
    }

    private void ApplyFlightControl(
    bool allowForwardAssist,
    float steeringMultiplier,
    float liftMultiplier,
    bool applyBoundary)
    {
        if (state != DiscState.Flying && state != DiscState.Settling)
            return;

        // 이번 투척에서 원반이 기본적으로 나아가려는 방향.
        // forwardCorrectionStrength가 낮을수록 던진 방향에 가깝다.
        Vector3 forward = GetActiveFlightForward();

        // 속도 제한은 activeFlightForward 기준의 좌우 방향으로 처리한다.
        // 그래야 오른쪽 위로 던진 속도가 바로 잘려나가지 않는다.
        Vector3 sideClampRight = GetActiveFlightRight();

        // 실제 좌우 조종은 선택 가능.
        // false면 기존처럼 트랙 기준 좌우 조종.
        // true면 던진 방향 기준 좌우 조종.
        Vector3 steeringRight = steeringRelativeToThrowDirection
            ? sideClampRight
            : GetTrackRight();

        // 경계는 항상 트랙 기준으로 보는 것이 안전하다.
        Vector3 boundaryRight = GetTrackRight();

        Vector3 velocity = GetLinearVelocity();

        float sideSpeed = Vector3.Dot(velocity, sideClampRight);

        if (Mathf.Abs(sideSpeed) > maxLateralSpeed)
        {
            float clampedSideSpeed = Mathf.Sign(sideSpeed) * maxLateralSpeed;

            velocity -= sideClampRight * (sideSpeed - clampedSideSpeed);

            SetLinearVelocity(velocity);
        }

        if (allowForwardAssist && forwardAssistEnabled)
        {
            float forwardSpeed = Vector3.Dot(velocity, forward);

            float currentTargetForwardSpeed = GetActiveTargetForwardSpeed();

            float forwardAcceleration =
                (currentTargetForwardSpeed - forwardSpeed) * forwardSpeedGain;

            rb.AddForce(
                forward * forwardAcceleration,
                ForceMode.Acceleration
            );
        }

        if (steeringMultiplier > 0f)
        {
            rb.AddForce(
                steeringRight *
                (steerInput * lateralAcceleration * steeringMultiplier),
                ForceMode.Acceleration
            );
        }

        if (liftMultiplier > 0f)
        {
            ApplyLift(liftMultiplier);
        }

        if (applyBoundary)
        {
            ApplyBoundaryForce(boundaryRight);
        }
    }

    private void ApplyLift(float multiplier)
    {
        if (multiplier <= 0f)
            return;

        Vector3 velocity = GetLinearVelocity();
        Vector3 horizontalVelocity = Vector3.ProjectOnPlane(velocity, Vector3.up);

        float speedFactor = Mathf.Clamp01(
            horizontalVelocity.magnitude / GetActiveTargetForwardSpeed()
        );
        float liftAcceleration =
            -Physics.gravity.y *
            runtimeStats.lift *
            speedFactor *
            multiplier;

        rb.AddForce(
            Vector3.up * liftAcceleration,
            ForceMode.Acceleration
        );
    }

    private void ApplyBoundaryForce(Vector3 right)
    {
        float sidePosition = Vector3.Dot(rb.position - anchorPosition, right);

        if (Mathf.Abs(sidePosition) <= laneHalfWidth)
            return;

        float sign = Mathf.Sign(sidePosition);
        float overshoot = Mathf.Abs(sidePosition) - laneHalfWidth;
        float sideSpeed = Vector3.Dot(GetLinearVelocity(), right);

        float acceleration =
            -sign * boundarySpring * overshoot -
            boundaryDamping * sideSpeed;

        rb.AddForce(
            right * acceleration,
            ForceMode.Acceleration
        );
    }

    private void ApplySettlingBrake()
    {
        Vector3 velocity = GetLinearVelocity();

        Vector3 horizontalVelocity = Vector3.ProjectOnPlane(
            velocity,
            Vector3.up
        );

        Vector3 verticalVelocity = velocity - horizontalVelocity;

        if (horizontalVelocity.sqrMagnitude > 0.0001f)
        {
            Vector3 brakedHorizontalVelocity = Vector3.MoveTowards(
                horizontalVelocity,
                Vector3.zero,
                settlingHorizontalBrake * Time.fixedDeltaTime
            );

            SetLinearVelocity(brakedHorizontalVelocity + verticalVelocity);
        }
    }

    private void ApplyPostImpactFlightControl()
    {
        float speed = GetLinearVelocity().magnitude;

        if (speed <= postImpactControlOffSpeed)
        {
            flightControlEnabled = false;
            steerInput = 0f;

            Debug.Log("Post-impact flight control disabled due to low speed.");
            return;
        }

        // 충돌 후 제어.
        // 전방 재가속은 절대 허용하지 않습니다.
        ApplyFlightControl(
            allowForwardAssist: false,
            steeringMultiplier: postImpactSteeringMultiplier,
            liftMultiplier: postImpactLiftMultiplier,
            applyBoundary: false
        );
    }

    private bool ScreenToCameraPlane(Vector2 screenPosition, out Vector3 worldPosition)
    {
        worldPosition = default;

        if (inputCamera == null)
            return false;

        Ray ray = inputCamera.ScreenPointToRay(
            new Vector3(screenPosition.x, screenPosition.y, 0f)
        );

        Plane plane = new Plane(
            -inputCamera.transform.forward,
            anchorPosition
        );

        if (!plane.Raycast(ray, out float enter))
            return false;

        worldPosition = ray.GetPoint(enter);
        return true;
    }

    private Vector3 ClampDragTarget(Vector3 rawTarget)
    {
        Vector3 offset = rawTarget - anchorPosition;

        if (maxDragWorldDistance > 0f &&
            offset.magnitude > maxDragWorldDistance)
        {
            rawTarget =
                anchorPosition +
                offset.normalized * maxDragWorldDistance;
        }

        float minY = anchorPosition.y + minDragYOffset;
        float maxY = anchorPosition.y + maxDragYOffset;

        rawTarget.y = Mathf.Clamp(rawTarget.y, minY, maxY);

        return rawTarget;
    }

    private bool ScreenHitsDisc(Vector2 screenPosition)
    {
        if (inputCamera == null)
            return true;

        Ray ray = inputCamera.ScreenPointToRay(
            new Vector3(screenPosition.x, screenPosition.y, 0f)
        );

        if (!Physics.Raycast(
                ray,
                out RaycastHit hit,
                500f,
                discHitMask,
                QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        return hit.rigidbody == rb ||
               hit.collider.GetComponentInParent<DiscSlingshotController>() == this;
    }

    private bool IsPointerOverUI(Vector2 screenPosition)
    {
        if (EventSystem.current == null)
            return false;

        PointerEventData eventData = new PointerEventData(EventSystem.current)
        {
            position = screenPosition
        };

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        return results.Count > 0;
    }

    private Vector3 GetTrackForward()
    {
        Vector3 forward = trackRoot != null
            ? trackRoot.forward
            : Vector3.forward;

        forward.y = 0f;

        if (forward.sqrMagnitude < 0.0001f)
            return Vector3.forward;

        return forward.normalized;
    }

    private Vector3 GetTrackRight()
    {
        Vector3 right = trackRoot != null
            ? trackRoot.right
            : Vector3.right;

        right.y = 0f;

        if (right.sqrMagnitude < 0.0001f)
            return Vector3.right;

        return right.normalized;
    }

    private void UpdateVisual()
    {
        if (visualRoot == null)
            return;

        bool shouldSpin =
            state == DiscState.Flying ||
            (state == DiscState.Settling &&
             flightControlEnabled &&
             spinWhilePostImpactMoving);

        if (shouldSpin)
        {
            float speedFactor = 1f;

            if (state == DiscState.Settling)
            {
                speedFactor = Mathf.Clamp01(
                    GetLinearVelocity().magnitude /
                    Mathf.Max(0.01f, postImpactControlOffSpeed)
                );
            }

            spinAngle =
                (spinAngle +
                 spinDegreesPerSecond *
                 speedFactor *
                 Time.deltaTime) % 360f;
        }

        Quaternion spin = Quaternion.Euler(0f, spinAngle, 0f);
        Quaternion bank = Quaternion.Euler(0f, 0f, -steerInput * bankAngle);

        Quaternion targetRotation =
            visualInitialLocalRotation *
            bank *
            spin;

        float t = 1f - Mathf.Exp(-visualLerp * Time.deltaTime);

        visualRoot.localRotation = Quaternion.Slerp(
            visualRoot.localRotation,
            targetRotation,
            t
        );
    }

    public void ApplyStats(DiscRuntimeStats stats)
    {
        runtimeStats = stats;

        runtimeStats.initialThrust = Mathf.Max(1f, runtimeStats.initialThrust);
        runtimeStats.maxDurability = Mathf.Max(1f, runtimeStats.maxDurability);
        runtimeStats.lift = Mathf.Max(0f, runtimeStats.lift);

        targetForwardSpeed =
            runtimeStats.initialThrust *
            targetForwardSpeedRatio;
        activeTargetForwardSpeed = targetForwardSpeed;
    }

    public void ResetToLaunch()
    {
        PlaceAtLaunchAnchor(true);
    }
    public void PlaceAtLaunchAnchor(bool readyForInput)
    {
        anchorPosition = launchAnchor != null
            ? launchAnchor.position
            : transform.position;

        activeFingerId = -1;
        mouseDragging = false;
        hasPendingLaunch = false;

        flightControlEnabled = false;
        forwardAssistEnabled = false;

        lowSpeedTimer = 0f;
        settlingStopReady = false;

// activeTargetForwardSpeed = 0f;
        //lastThrowPower01 = 0f;
        //lastThrowThrustRatio = 1f;

        activeFlightForward = GetTrackForward();
        activeFlightRight = GetTrackRight();

        totalDragScreen = Vector2.zero;
        pointerSamples.Clear();

        steerInput = 0f;

        rb.isKinematic = false;

        SetLinearVelocity(Vector3.zero);
        rb.angularVelocity = Vector3.zero;

        SetLinearDamping(flyingLinearDamping);
        SetAngularDamping(flyingAngularDamping);

        rb.useGravity = true;
        rb.constraints &= ~RigidbodyConstraints.FreezePositionY;
        rb.constraints |= RigidbodyConstraints.FreezeRotation;

        rb.position = anchorPosition;
        transform.position = anchorPosition;

        dragTargetPosition = anchorPosition;

        rb.isKinematic = true;

        spinAngle = 0f;

        if (visualRoot != null)
            visualRoot.localRotation = visualInitialLocalRotation;

        state = readyForInput
            ? DiscState.Ready
            : DiscState.Stopped;
    }
    public void BeginSettlingAfterImpact()
    {
        if (state == DiscState.Stopped)
            return;

        state = DiscState.Settling;

        // 충돌 후에도 잠깐 제어는 유지할 수 있지만,
        // targetForwardSpeed로 다시 앞으로 가속하는 기능은 즉시 꺼야 합니다.
        flightControlEnabled = true;
        forwardAssistEnabled = false;

        settlingStartedTime = Time.time;
        lowSpeedTimer = 0f;
        settlingStopReady = false;
        nextSettlingLogTime = 0f;

        activeFingerId = -1;
        mouseDragging = false;
        hasPendingLaunch = false;
        steerInput = 0f;

        rb.isKinematic = false;

        SetLinearDamping(settlingLinearDamping);
        SetAngularDamping(settlingAngularDamping);

        rb.angularVelocity = Vector3.zero;
        rb.constraints |= RigidbodyConstraints.FreezeRotation;

        Debug.Log("Disc entered Settling. Forward assist disabled.");
    }

    public bool IsSlowEnoughToStop()
    {
        if (state != DiscState.Settling && state != DiscState.Stopped)
            return false;

        if (Time.time - settlingStartedTime < minSettlingTimeBeforeStop)
        {
            lowSpeedTimer = 0f;
            return false;

        }
        Vector3 velocity = GetLinearVelocity();

        bool linearSlowEnough =
            velocity.sqrMagnitude <= stopLinearSpeed * stopLinearSpeed;

        bool angularSlowEnough = true;

        if (requireAngularSlowToStop)
        {
            angularSlowEnough =
                rb.angularVelocity.sqrMagnitude <= stopAngularSpeed * stopAngularSpeed;
        }

        if (linearSlowEnough && angularSlowEnough)
        {
            

            if (logLowSpeedTimer)
            {
                Debug.Log(
                    $"Low speed timer: {lowSpeedTimer:F2} / " +
                    $"{requiredLowSpeedDurationToStop:F2}, " +
                    $"speed: {velocity.magnitude:F2}"
                );
            }
        }
        else
        {
            lowSpeedTimer = 0f;
        }

        return lowSpeedTimer >= requiredLowSpeedDurationToStop;
    }

    public bool StopDisc()
    {
        if (!IsSlowEnoughToStop())
            return false;

        StopDiscImmediately();
        return true;
    }

    public void StopDiscImmediately()
    {
        state = DiscState.Stopped;

        flightControlEnabled = false;
        forwardAssistEnabled = false;

        lowSpeedTimer = 0f;
        settlingStopReady = false;

        activeFingerId = -1;
        mouseDragging = false;
        hasPendingLaunch = false;
        steerInput = 0f;

        rb.isKinematic = false;

        SetLinearVelocity(Vector3.zero);
        rb.angularVelocity = Vector3.zero;

        SetLinearDamping(stoppedLinearDamping);
        SetAngularDamping(stoppedLinearDamping);

        rb.isKinematic = true;

        Debug.Log("Disc stopped.");
    }

    private Vector3 GetLinearVelocity()
    {
#if UNITY_6000_0_OR_NEWER
        return rb.linearVelocity;
#else
        return rb.velocity;
#endif
    }

    private void SetLinearVelocity(Vector3 velocity)
    {
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = velocity;
#else
        rb.velocity = velocity;
#endif
    }

    private void SetLinearDamping(float value)
    {
#if UNITY_6000_0_OR_NEWER
        rb.linearDamping = value;
#else
        rb.drag = value;
#endif
    }

    private void SetAngularDamping(float value)
    {
#if UNITY_6000_0_OR_NEWER
        rb.angularDamping = value;
#else
        rb.angularDrag = value;
#endif
    }
    private float CalculateLaunchSpeedFromThrowPower(
    float throwPower01,
    out float thrustRatio)
    {
        thrustRatio = CalculateThrowThrustRatio(throwPower01);

        return runtimeStats.initialThrust * thrustRatio;
    }

    private float CalculateThrowThrustRatio(float throwPower01)
    {
        float clampedPower = Mathf.Clamp01(throwPower01);

        float shapedPower = Mathf.Pow(
            clampedPower,
            throwPowerResponseExponent
        );

        // throwPowerToInitialThrust = 0이면 항상 1, 즉 최대 추진력.
        // throwPowerToInitialThrust = 1이면 shapedPower를 그대로 사용.
        float influencedPower = Mathf.Lerp(
            1f,
            shapedPower,
            throwPowerToInitialThrust
        );

        // 최소 발사 속도 비율을 반영.
        // minLaunchSpeedRatio가 0.45면 아무리 약해도 최대 추진력의 45%는 나감.
        float ratio = Mathf.Lerp(
            minLaunchSpeedRatio,
            1f,
            influencedPower
        );

        return Mathf.Clamp01(ratio);
    }

    private float CalculateActiveTargetForwardSpeed(float thrustRatio)
    {
        if (!scaleForwardTargetSpeedWithThrowPower)
            return targetForwardSpeed;

        return targetForwardSpeed * Mathf.Clamp01(thrustRatio);
    }

    private float GetActiveTargetForwardSpeed()
    {
        if (activeTargetForwardSpeed > 0.01f)
            return activeTargetForwardSpeed;

        return Mathf.Max(0.01f, targetForwardSpeed);
    }
    private void SetActiveFlightDirection(Vector3 throwDirection)
    {
        Vector3 trackForward = GetTrackForward();

        Vector3 flatThrowDirection = Vector3.ProjectOnPlane(
            throwDirection,
            Vector3.up
        );

        if (flatThrowDirection.sqrMagnitude < 0.0001f)
            flatThrowDirection = trackForward;

        flatThrowDirection.Normalize();

        float correction = Mathf.Clamp01(forwardCorrectionStrength);

        activeFlightForward = Vector3.Slerp(
            flatThrowDirection,
            trackForward,
            correction
        ).normalized;

        activeFlightRight = Vector3.Cross(
            Vector3.up,
            activeFlightForward
        ).normalized;

        if (activeFlightRight.sqrMagnitude < 0.0001f)
            activeFlightRight = GetTrackRight();
    }

    private void UpdateActiveFlightDirection()
    {
        if (forwardCorrectionTurnSpeed <= 0f)
            return;

        Vector3 trackForward = GetTrackForward();

        float maxRadians =
            forwardCorrectionTurnSpeed *
            Mathf.Deg2Rad *
            Time.fixedDeltaTime;

        activeFlightForward = Vector3.RotateTowards(
            GetActiveFlightForward(),
            trackForward,
            maxRadians,
            0f
        ).normalized;

        activeFlightRight = Vector3.Cross(
            Vector3.up,
            activeFlightForward
        ).normalized;

        if (activeFlightRight.sqrMagnitude < 0.0001f)
            activeFlightRight = GetTrackRight();
    }

    private Vector3 GetActiveFlightForward()
    {
        if (activeFlightForward.sqrMagnitude < 0.0001f)
            activeFlightForward = GetTrackForward();

        return activeFlightForward.normalized;
    }

    private Vector3 GetActiveFlightRight()
    {
        if (activeFlightRight.sqrMagnitude < 0.0001f)
        {
            activeFlightRight = Vector3.Cross(
                Vector3.up,
                GetActiveFlightForward()
            ).normalized;
        }

        if (activeFlightRight.sqrMagnitude < 0.0001f)
            activeFlightRight = GetTrackRight();

        return activeFlightRight.normalized;
    }
    private void UpdateSettlingStopReadiness()
    {
        if (state != DiscState.Settling)
        {
            lowSpeedTimer = 0f;
            settlingStopReady = false;
            return;
        }

        float settlingElapsed = Time.time - settlingStartedTime;

        if (settlingElapsed < minSettlingTimeBeforeStop)
        {
            lowSpeedTimer = 0f;
            settlingStopReady = false;
            return;
        }

        Vector3 velocity = GetLinearVelocity();

        bool linearSlowEnough =
            velocity.sqrMagnitude <= stopLinearSpeed * stopLinearSpeed;

        bool angularSlowEnough = true;

        if (requireAngularSlowToStop)
        {
            angularSlowEnough =
                rb.angularVelocity.sqrMagnitude <= stopAngularSpeed * stopAngularSpeed;
        }

        if (linearSlowEnough && angularSlowEnough)
        {
            lowSpeedTimer += Time.fixedDeltaTime;
        }
        else
        {
            lowSpeedTimer = 0f;
        }

        settlingStopReady =
            lowSpeedTimer >= requiredLowSpeedDurationToStop;

        if (logSettlingStopCheck && Time.time >= nextSettlingLogTime)
        {
            nextSettlingLogTime = Time.time + settlingLogInterval;

            Debug.Log(
                $"Settling check | " +
                $"elapsed: {settlingElapsed:F2}, " +
                $"speed: {velocity.magnitude:F2}, " +
                $"lowTimer: {lowSpeedTimer:F2}/{requiredLowSpeedDurationToStop:F2}, " +
                $"ready: {settlingStopReady}"
            );
        }
    }
}