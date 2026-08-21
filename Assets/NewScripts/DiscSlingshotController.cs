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

    #region Inspector - References

    [Header("References")]
    [SerializeField] private Camera inputCamera;
    [SerializeField] private Transform launchAnchor;
    [SerializeField] private Transform trackRoot;
    [SerializeField] private Transform visualRoot;

    [Header("Camera")]
    [SerializeField] private DiscCinemachineSwitcher cameraSwitcher;

    [Tooltip("true면 손을 놓고 발사가 예약되는 순간 바로 Follow Camera로 전환합니다.")]
    [SerializeField] private bool beginCameraFollowImmediatelyOnRelease = true;

    #endregion

    #region Inspector - Default Stats

    //[Header("Default Stats")]
    //[SerializeField] private float defaultInitialThrust = 18f;
    //[SerializeField] private float defaultMaxDurability = 100f;
    //[SerializeField] private float defaultLift = 0.65f;

    #endregion

    #region Inspector - Input / Throw

    [Header("Touch Start")]
    [SerializeField] private bool requireTouchOnDisc = true;
    [SerializeField] private LayerMask discHitMask = ~0;

    [Header("Pokemon Ball Throw")]
    [SerializeField] private float maxDragPixels = 500f;
    [SerializeField] private float minDragPixelsToThrow = 45f;

    [Tooltip("이 속도보다 빠르면 플릭 던지기로 인정합니다. 단위: pixels/second")]
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

    #endregion

    #region Inspector - Throw Power / Direction

    [Header("Throw Power Scaling")]
    [Tooltip("던지는 세기가 초기 추진력에 얼마나 영향을 줄지입니다. 0이면 항상 최대 추진력, 1이면 완전히 던지는 세기에 비례합니다.")]
    [SerializeField, Range(0f, 1f)] private float throwPowerToInitialThrust = 1f;

    [Tooltip("던지는 세기 반응 곡선입니다. 1이면 선형, 2 이상이면 약한 던지기가 더 약해집니다.")]
    [SerializeField] private float throwPowerResponseExponent = 1f;

    [Tooltip("비행 중 targetForwardSpeed도 던지는 세기에 맞춰 낮출지입니다.")]
    [SerializeField] private bool scaleForwardTargetSpeedWithThrowPower = true;

    [Header("Throw Direction")]
    [Tooltip("false면 아래로 드래그해도 뒤로 날아가지 않고 최소한 앞으로 보정됩니다.")]
    [SerializeField] private bool allowBackwardThrow = false;

    [SerializeField, Range(0f, 0.5f)] private float minForwardInputWhenBackwardDisabled = 0.05f;

    [SerializeField] private float minThrowUpAngle = 3f;
    [SerializeField] private float maxThrowUpAngle = 14f;

    [Header("Throw Height Control")]
    [Tooltip(
    "값이 클수록 위쪽으로 강하게 드래그했을 때만 " +
    "Max Throw Up Angle에 가까워집니다."
)]
    [SerializeField, Min(0.1f)]
    private float throwUpInputExponent = 2f;

    [Tooltip(
        "강한 투척에서도 허용할 최대 초기 상승 속도입니다. " +
        "Max Throw Angle과 별도로 적용됩니다."
    )]
    [SerializeField, Min(0f)]
    private float maxInitialUpwardSpeed = 0.75f;

    [Header("Throw Direction Preservation")]
    [Tooltip("던진 방향을 얼마나 TrackForward 쪽으로 보정할지입니다. 0이면 던진 방향 유지, 1이면 기존처럼 앞으로 강하게 보정합니다.")]
    [SerializeField, Range(0f, 1f)] private float forwardCorrectionStrength = 0.25f;

    [Tooltip("비행 중 시간이 지나면서 TrackForward 쪽으로 서서히 돌아가는 속도입니다. 0이면 추가 보정 없음. 단위: degrees/second")]
    [SerializeField] private float forwardCorrectionTurnSpeed = 0f;

    [Tooltip("좌우 조종 방향도 던진 방향 기준으로 할지입니다. false면 트랙 기준 좌우 조종을 유지합니다.")]
    [SerializeField] private bool steeringRelativeToThrowDirection = false;

    #endregion

    #region Inspector - Flight
    [Header("Lift Rise Limiter")]
    [Tooltip(
    "수직 상승 속도가 이 값부터 Lift가 감소하기 시작합니다."
)]
    [SerializeField]
    private float liftFadeStartUpSpeed = 0f;

    [Tooltip(
        "수직 상승 속도가 이 값 이상이면 " +
        "Lift가 Lift Scale At Cutoff 수준까지 감소합니다."
    )]
    [SerializeField, Min(0.01f)]
    private float liftCutoffUpSpeed = 1f;

    [Tooltip(
        "빠르게 상승할 때 유지할 최소 Lift 비율입니다. " +
        "0이면 상승 중 Lift를 완전히 끕니다."
    )]
    [SerializeField, Range(0f, 1f)]
    private float liftScaleAtCutoff = 0.05f;

    [Tooltip(
        "Lift의 최대 위쪽 가속도를 중력의 몇 배까지 허용할지입니다. " +
        "1보다 낮으면 Lift만으로 계속 상승하지 않습니다."
    )]
    [SerializeField, Range(0f, 1f)]
    private float maxLiftToGravityRatio = 0.97f;

    [Header("Flight")]
    [Tooltip("최소 발사 속도 = 초기 추진력 × 이 값")]
    [SerializeField, Range(0f, 1f)] private float minLaunchSpeedRatio = 0.25f;

    [Tooltip("비행 중 유지하려는 기본 전방 속도 = 초기 추진력 × 이 값")]
    [SerializeField] private float targetForwardSpeedRatio = 0.85f;

    [SerializeField] private float forwardSpeedGain = 4f;
    [SerializeField] private float lateralAcceleration = 30f;
    [SerializeField] private float maxLateralSpeed = 8f;

    [Header("Track Boundary")]
    [SerializeField] private float laneHalfWidth = 4.5f;
    [SerializeField] private float boundarySpring = 40f;
    [SerializeField] private float boundaryDamping = 10f;

    #endregion

    #region Inspector - Post Impact

    [Header("Post Impact Control")]
    [Tooltip("충돌 후에도 이 속도보다 빠르면 약한 비행 제어를 유지합니다.")]
    [SerializeField] private float postImpactControlOffSpeed = 0.2f;

    [SerializeField]
    private bool allowPostImpactSteering = true;

    [Tooltip("충돌 후 좌우 조종이 얼마나 남아 있을지입니다. 0이면 조종 없음.")]
    [SerializeField, Range(0f, 1f)] private float postImpactSteeringMultiplier = 0.15f;
    [Tooltip(
    "충돌 후 이 속도 이상이면 Post Impact Steering이 " +
    "설정된 최대 강도로 적용됩니다."
)]
    [SerializeField, Min(0.01f)]
    private float postImpactSteeringFullEffectSpeed = 8f;

    [Tooltip("충돌 후 양력을 얼마나 남길지입니다. 자연스럽게 떨어져 멈추게 하려면 0 추천.")]
    [SerializeField, Range(0f, 1f)] private float postImpactLiftMultiplier = 0f;

    [Tooltip("충돌 후에도 속도에 비례해서 시각적 회전을 잠깐 유지합니다.")]
    [SerializeField] private bool spinWhilePostImpactMoving = true;

    [Header("Post Impact Forward Assist")]
    [Tooltip("충돌 이후 현재 속도에 비례해 전방 가속을 추가합니다. 0이면 비활성화됩니다.")]
    [SerializeField] private float postImpactForwardAccelerationCoefficient = 0.15f;

    [Tooltip("충돌 이후 전방 가속의 최대값입니다. 0 이하이면 제한하지 않습니다.")]
    [SerializeField] private float postImpactMaxForwardAcceleration = 2.5f;

    [Tooltip("속도가 이 값 이하이면 충돌 후 전방 가속을 끕니다.")]
    [SerializeField] private float postImpactForwardAccelerationMinSpeed = 0.2f;

    [Header("Post Impact Rotation")]
    [Tooltip("첫 충돌 이후 조건에 따라 Rigidbody Freeze Rotation을 해제합니다.")]
    [SerializeField] private bool unlockRotationAfterFirstImpact = true;

    [Tooltip("임계속도 1. 첫 충돌 이후 현재 속도가 이 값 이하로 떨어지면 Freeze Rotation을 해제합니다.")]
    [SerializeField] private float unlockRotationCurrentSpeedThreshold = 1f;

    [Tooltip("임계속도 2. 첫 충돌 순간의 속도가 이 값 이하이면 즉시 Freeze Rotation을 해제합니다.")]
    [SerializeField] private float unlockRotationImpactSpeedThreshold = 2f;

    [Tooltip("Freeze Rotation 해제 후 사용할 회전 감쇠입니다.")]
    [SerializeField] private float unlockedRotationAngularDamping = 1.5f;

    #endregion

    #region Inspector - Settling / Stop

    [Header("Settling After Impact")]
    [SerializeField] private float settlingLinearDamping = 2.5f;
    [SerializeField] private float settlingAngularDamping = 8f;

    [Tooltip("충돌 후 바닥에서 계속 미끄러지지 않도록 수평 속도를 줄이는 값입니다.")]
    [SerializeField] private float settlingHorizontalBrake = 12f;

    [Header("Settling Stop Condition")]
    [Tooltip("충돌 후 이 시간 전에는 정지 판정을 하지 않습니다.")]
    [SerializeField] private float minSettlingTimeBeforeStop = 0.35f;

    [Tooltip("이 속도 이하를 저속 상태로 봅니다.")]
    [SerializeField] private float stopLinearSpeed = 0.55f;

    [Tooltip("저속 상태가 이 시간만큼 연속 유지되어야 정지 처리됩니다.")]
    [SerializeField] private float requiredLowSpeedDurationToStop = 0.8f;

    [Header("Rotation Stop After Low Speed")]
    [Tooltip("저속 상태가 Required Low Speed Duration 동안 유지되면 회전을 강제로 멈춥니다.")]
    [SerializeField] private bool stopRotationWhenLowSpeedStable = true;

    [Tooltip("저속 지속 조건을 만족한 순간 Rigidbody 회전을 다시 고정합니다.")]
    [SerializeField] private bool freezeRotationWhenLowSpeedStable = true;

    [Tooltip("저속 지속 조건을 만족한 뒤 적용할 회전 감쇠값입니다.")]
    [SerializeField] private float lowSpeedStableAngularDamping = 20f;

    [Header("Settling Debug")]
    [SerializeField] private bool logSettlingStopCheck = false;
    [SerializeField] private float settlingLogInterval = 0.5f;

    #endregion

    #region Inspector - Damping / Visual / Events

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

    #endregion

    #region Runtime Fields

    public event UnityAction Launched;

    private Rigidbody rb;
    private DiscState state = DiscState.Ready;

    private DiscRuntimeStats runtimeStats;

    private Vector3 anchorPosition;
    private Vector3 dragTargetPosition;
    private Vector3 fingerOffsetWorld;

    private Vector2 dragStartScreen;
    private Vector2 totalDragScreen;

    private readonly List<PointerSample> pointerSamples = new List<PointerSample>(12);

    private int activeFingerId = -1;
    private bool mouseDragging;

    private bool hasPendingLaunch;
    private bool launchEventsPending;
    private Vector3 pendingLaunchVelocity;

    private bool flightControlEnabled;
    private bool forwardAssistEnabled;

    private float targetForwardSpeed;
    private float activeTargetForwardSpeed;
    private float lastThrowPower01;
    private float lastThrowThrustRatio = 1f;

    private Vector3 activeFlightForward;
    private Vector3 activeFlightRight;

    private bool postImpactRotationUnlocked;
    private bool rotationStoppedAfterLowSpeed;

    private float settlingStartedTime;
    private float lowSpeedTimer;
    private bool settlingStopReady;
    private float nextSettlingLogTime;

    private float steerInput;
    private float spinAngle;
    private Quaternion visualInitialLocalRotation;

    private bool runtimeStatsInitialized;

    #endregion

    #region Public Properties

    public bool IsFlying => state == DiscState.Flying;
    public bool IsReady => state == DiscState.Ready;
    public bool IsSettling => state == DiscState.Settling;

    public float CurrentSteerInput => steerInput;

    public Vector3 RigidbodyPosition => rb != null ? rb.position : transform.position;

    public float CurrentSpeed => GetLinearVelocity().magnitude;
    public float LowSpeedTimer => lowSpeedTimer;
    public float RequiredLowSpeedDurationToStop => requiredLowSpeedDurationToStop;
    public bool SettlingStopReady => settlingStopReady;
    public bool RotationStoppedAfterLowSpeed => rotationStoppedAfterLowSpeed;

    public bool RuntimeStatsInitialized => runtimeStatsInitialized;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (inputCamera == null)
            inputCamera = Camera.main;

        if (visualRoot != null)
            visualInitialLocalRotation = visualRoot.localRotation;
        else
            visualInitialLocalRotation = Quaternion.identity;

        //ApplyStats(new DiscRuntimeStats(
        //    defaultInitialThrust,
        //    defaultMaxDurability,
        //    defaultLift
        //));

        ConfigureRigidbodyForReadyOrFlying();
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
        //defaultInitialThrust = Mathf.Max(1f, defaultInitialThrust);
        //defaultMaxDurability = Mathf.Max(1f, defaultMaxDurability);
        //defaultLift = Mathf.Max(0f, defaultLift);

        maxDragPixels = Mathf.Max(1f, maxDragPixels);
        minDragPixelsToThrow = Mathf.Max(0f, minDragPixelsToThrow);
        minFlickPixelsPerSecond = Mathf.Max(0f, minFlickPixelsPerSecond);
        maxFlickPixelsPerSecond = Mathf.Max(minFlickPixelsPerSecond + 1f, maxFlickPixelsPerSecond);
        releaseVelocitySampleTime = Mathf.Max(0.02f, releaseVelocitySampleTime);

        maxDragWorldDistance = Mathf.Max(0.1f, maxDragWorldDistance);
        maxDragYOffset = Mathf.Max(minDragYOffset, maxDragYOffset);

        minThrowUpAngle = Mathf.Max(0f, minThrowUpAngle);
        maxThrowUpAngle = Mathf.Max(minThrowUpAngle, maxThrowUpAngle);

        throwPowerResponseExponent = Mathf.Max(0.05f, throwPowerResponseExponent);

        forwardCorrectionTurnSpeed = Mathf.Max(0f, forwardCorrectionTurnSpeed);

        targetForwardSpeedRatio = Mathf.Max(0f, targetForwardSpeedRatio);
        forwardSpeedGain = Mathf.Max(0f, forwardSpeedGain);
        lateralAcceleration = Mathf.Max(0f, lateralAcceleration);
        maxLateralSpeed = Mathf.Max(0f, maxLateralSpeed);

        laneHalfWidth = Mathf.Max(0.1f, laneHalfWidth);
        boundarySpring = Mathf.Max(0f, boundarySpring);
        boundaryDamping = Mathf.Max(0f, boundaryDamping);

        postImpactControlOffSpeed = Mathf.Max(0f, postImpactControlOffSpeed);
        postImpactForwardAccelerationCoefficient = Mathf.Max(0f, postImpactForwardAccelerationCoefficient);
        postImpactMaxForwardAcceleration = Mathf.Max(0f, postImpactMaxForwardAcceleration);
        postImpactForwardAccelerationMinSpeed = Mathf.Max(0f, postImpactForwardAccelerationMinSpeed);

        unlockRotationCurrentSpeedThreshold = Mathf.Max(0f, unlockRotationCurrentSpeedThreshold);
        unlockRotationImpactSpeedThreshold = Mathf.Max(0f, unlockRotationImpactSpeedThreshold);
        unlockedRotationAngularDamping = Mathf.Max(0f, unlockedRotationAngularDamping);

        settlingLinearDamping = Mathf.Max(0f, settlingLinearDamping);
        settlingAngularDamping = Mathf.Max(0f, settlingAngularDamping);
        settlingHorizontalBrake = Mathf.Max(0f, settlingHorizontalBrake);

        minSettlingTimeBeforeStop = Mathf.Max(0f, minSettlingTimeBeforeStop);
        stopLinearSpeed = Mathf.Max(0.01f, stopLinearSpeed);
        requiredLowSpeedDurationToStop = Mathf.Max(0f, requiredLowSpeedDurationToStop);
        lowSpeedStableAngularDamping = Mathf.Max(0f, lowSpeedStableAngularDamping);
        settlingLogInterval = Mathf.Max(0.05f, settlingLogInterval);

        flyingLinearDamping = Mathf.Max(0f, flyingLinearDamping);
        flyingAngularDamping = Mathf.Max(0f, flyingAngularDamping);
        stoppedLinearDamping = Mathf.Max(0f, stoppedLinearDamping);
    }

    private void Update()
    {
        switch (state)
        {
            case DiscState.Ready:
            case DiscState.Dragging:
                ReadThrowInput();
                break;

            case DiscState.Flying:
                ReadSteeringInput();
                break;

            case DiscState.Settling:
                ReadSteeringInput();
                break;

            default:
                steerInput = 0f;
                break;
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
            ExecutePhysicsLaunch();
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

            //ApplySettlingBrake();

            UpdatePostImpactRotationUnlock();
            UpdateSettlingStopReadiness();
        }
    }

    #endregion

    #region Input - Throw

    private void ReadThrowInput()
    {
        if (ETouch.activeTouches.Count > 0)
        {
            ReadTouchThrowInput();
            return;
        }

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
        if (!runtimeStatsInitialized)
        {
            Debug.LogError(
                "DiscSlingshotController: " +
                "Runtime Stats가 적용되지 않았습니다. " +
                "DiscRunManager의 Progression Store와 " +
                "DiscProgressionConfig 연결을 확인하세요.",
                this
            );

            return;
        }
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
        if (!runtimeStatsInitialized)
        {
            Debug.LogError(
                "Runtime Stats가 적용되지 않아 발사를 취소합니다.",
                this
            );

            CancelDrag();
            return;
        }

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
        lastThrowPower01 = power01;

        lastThrowThrustRatio =
            thrustRatio;

        pendingLaunchVelocity = throwDirection * launchSpeed;
        pendingLaunchVelocity =
    ClampFinalLaunchAngle(
        pendingLaunchVelocity
    );
        pendingLaunchVelocity =
    ClampInitialUpwardSpeed(
        pendingLaunchVelocity
    );
        pendingLaunchVelocity =
    ClampFinalLaunchVelocity(
        pendingLaunchVelocity,
        lastThrowPower01
    );
        hasPendingLaunch = true;
        launchEventsPending = true;

        state = DiscState.Flying;
        flightControlEnabled = true;
        forwardAssistEnabled = true;

        activeFingerId = -1;
        mouseDragging = false;
        pointerSamples.Clear();

        if (beginCameraFollowImmediatelyOnRelease && cameraSwitcher != null)
            cameraSwitcher.BeginFollow();
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

    #endregion

    #region Launch Execution

    private void ExecutePhysicsLaunch()
    {
        Vector2 releaseVelocityScreen = GetRecentScreenVelocity();
        float power01 = CalculateThrowPower01(totalDragScreen, releaseVelocityScreen);
        //lastThrowPower01 = power01;
        Vector3 finalLaunchVelocity =
        ClampFinalLaunchVelocity(
            pendingLaunchVelocity,
            power01
        );

        pendingLaunchVelocity = finalLaunchVelocity;
        rb.isKinematic = false;

        //SetLinearVelocity(Vector3.zero);
        rb.angularVelocity = Vector3.zero;

        SetLinearDamping(flyingLinearDamping);
        SetAngularDamping(flyingAngularDamping);

        rb.constraints &= ~RigidbodyConstraints.FreezePositionY;
        rb.constraints &= ~RigidbodyConstraints.FreezeRotation;

        state = DiscState.Flying;
        flightControlEnabled = true;
        forwardAssistEnabled = true;

        postImpactRotationUnlocked = false;
        rotationStoppedAfterLowSpeed = false;
        settlingStopReady = false;
        lowSpeedTimer = 0f;

        rb.AddForce(finalLaunchVelocity, ForceMode.VelocityChange);

        hasPendingLaunch = false;

        InvokeLaunchEventsAfterPhysicsLaunch();
    }

    private void InvokeLaunchEventsAfterPhysicsLaunch()
    {
        if (!launchEventsPending)
            return;

        launchEventsPending = false;

        Launched?.Invoke();

        // 카메라는 ReleaseDrag에서 즉시 전환합니다.
        // onLaunched에는 사운드, UI, 파티클 같은 부가 이벤트만 연결하는 것을 추천합니다.
        onLaunched.Invoke();
    }

    #endregion

    #region Pointer Sampling / Throw Calculation

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

        float influencedPower = Mathf.Lerp(
            1f,
            shapedPower,
            throwPowerToInitialThrust
        );

        float ratio = Mathf.Lerp(
            minLaunchSpeedRatio,
            1f,
            influencedPower
        );

        return Mathf.Clamp01(ratio);
    }

    private Vector3 BuildThrowDirection(Vector2 screenVector)
    {
        Vector3 forward = GetTrackForward();
        Vector3 right = GetTrackRight();

        if (screenVector.sqrMagnitude < 0.0001f)
        {
            return AddUpAngle(
                forward,
                minThrowUpAngle
            );
        }

        Vector2 input = screenVector.normalized;

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

        flatDirection = Vector3.ProjectOnPlane(
            flatDirection,
            Vector3.up
        );

        if (flatDirection.sqrMagnitude < 0.0001f)
        {
            flatDirection = Vector3.ProjectOnPlane(
                forward,
                Vector3.up
            );
        }

        if (flatDirection.sqrMagnitude < 0.0001f)
            flatDirection = Vector3.forward;

        flatDirection.Normalize();

        float rawUpward01 =
            Mathf.Clamp01(input.y);

        // 위쪽 입력이 충분히 클 때만 최대 상승각에 접근
        float shapedUpward01 =
            Mathf.Pow(
                rawUpward01,
                throwUpInputExponent
            );

        float minimumAngle =
            Mathf.Min(
                minThrowUpAngle,
                maxThrowUpAngle
            );

        float maximumAngle =
            Mathf.Max(
                minThrowUpAngle,
                maxThrowUpAngle
            );

        float upAngle = Mathf.Lerp(
            minimumAngle,
            maximumAngle,
            shapedUpward01
        );

        return AddUpAngle(
            flatDirection,
            upAngle
        );
    }

    private Vector3 AddUpAngle(
    Vector3 flatDirection,
    float angleDegrees)
    {
        Vector3 planarDirection =
            Vector3.ProjectOnPlane(
                flatDirection,
                Vector3.up
            );

        if (planarDirection.sqrMagnitude < 0.0001f)
        {
            planarDirection = Vector3.ProjectOnPlane(
                GetTrackForward(),
                Vector3.up
            );
        }

        if (planarDirection.sqrMagnitude < 0.0001f)
            planarDirection = Vector3.forward;

        planarDirection.Normalize();

        float minimumAngle =
            Mathf.Min(
                minThrowUpAngle,
                maxThrowUpAngle
            );

        float maximumAngle =
            Mathf.Max(
                minThrowUpAngle,
                maxThrowUpAngle
            );

        float clampedAngle = Mathf.Clamp(
            angleDegrees,
            minimumAngle,
            maximumAngle
        );

        float angleRadians =
            clampedAngle * Mathf.Deg2Rad;

        Vector3 direction =
            planarDirection *
            Mathf.Cos(angleRadians) +
            Vector3.up *
            Mathf.Sin(angleRadians);

        return direction.normalized;
    }
    private Vector3 ClampInitialUpwardSpeed(
    Vector3 launchVelocity)
    {
        // 아래 방향 투척이나 이미 낮은 Y 속도는 그대로 유지
        if (launchVelocity.y <= maxInitialUpwardSpeed)
            return launchVelocity;

        float totalSpeed =
            launchVelocity.magnitude;

        if (totalSpeed <= 0.0001f)
            return Vector3.zero;

        Vector3 horizontalVelocity =
            Vector3.ProjectOnPlane(
                launchVelocity,
                Vector3.up
            );

        if (horizontalVelocity.sqrMagnitude < 0.0001f)
        {
            horizontalVelocity =
                Vector3.ProjectOnPlane(
                    GetTrackForward(),
                    Vector3.up
                );
        }

        if (horizontalVelocity.sqrMagnitude < 0.0001f)
            horizontalVelocity = Vector3.forward;

        horizontalVelocity.Normalize();

        float clampedUpwardSpeed =
            Mathf.Min(
                maxInitialUpwardSpeed,
                totalSpeed
            );

        /*
         * 전체 발사 속도는 유지하고,
         * 줄어든 Y 속도만큼 수평 속도로 재분배합니다.
         *
         * 강한 투척:
         * 더 높이 뜨는 대신 더 멀리 날아가게 됩니다.
         */
        float newHorizontalSpeed =
            Mathf.Sqrt(
                Mathf.Max(
                    0f,
                    totalSpeed * totalSpeed -
                    clampedUpwardSpeed *
                    clampedUpwardSpeed
                )
            );

        return
            horizontalVelocity *
            newHorizontalSpeed +
            Vector3.up *
            clampedUpwardSpeed;
    }

    #endregion

    #region Active Flight Direction

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

        activeFlightForward = Vector3.Slerp(
            flatThrowDirection,
            trackForward,
            Mathf.Clamp01(forwardCorrectionStrength)
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

    #endregion

    #region Flight Control

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
    private bool CanReadSteeringInput()
    {
        if (state == DiscState.Flying)
            return true;

        if (state == DiscState.Settling &&
            allowPostImpactSteering)
        {
            return true;
        }

        return false;
    }
    private float CalculateLiftRiseScale(
    Vector3 velocity)
    {
        float upwardSpeed =
            Vector3.Dot(
                velocity,
                Vector3.up
            );

        float rise01 = Mathf.InverseLerp(
            liftFadeStartUpSpeed,
            Mathf.Max(
                liftFadeStartUpSpeed + 0.01f,
                liftCutoffUpSpeed
            ),
            upwardSpeed
        );

        return Mathf.Lerp(
            1f,
            liftScaleAtCutoff,
            rise01
        );
    }
    private float LimitLiftAcceleration(
    float rawLiftAcceleration,
    Vector3 velocity)
    {
        float riseScale =
            CalculateLiftRiseScale(velocity);

        float limitedLiftAcceleration =
            Mathf.Max(
                0f,
                rawLiftAcceleration
            ) *
            riseScale;

        /*
         * 현재 World Up 기준 아래쪽 중력 가속도 크기.
         * 기본 중력에서는 약 9.81입니다.
         */
        float downwardGravityAcceleration =
            Mathf.Max(
                0.01f,
                -Vector3.Dot(
                    Physics.gravity,
                    Vector3.up
                )
            );

        float maximumLiftAcceleration =
            downwardGravityAcceleration *
            maxLiftToGravityRatio;

        return Mathf.Min(
            limitedLiftAcceleration,
            maximumLiftAcceleration
        );
    }
    private void ApplyFlightControl(
        bool allowForwardAssist,
        float steeringMultiplier,
        float liftMultiplier,
        bool applyBoundary,
        float steeringInputScale = 1f)
    {
        if (state != DiscState.Flying && state != DiscState.Settling)
            return;

        Vector3 forward = GetActiveFlightForward();
        Vector3 sideClampRight = GetActiveFlightRight();

        Vector3 steeringRight = steeringRelativeToThrowDirection
            ? sideClampRight
            : GetTrackRight();

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

            float forwardAcceleration =
                (GetActiveTargetForwardSpeed() - forwardSpeed) * forwardSpeedGain;

            rb.AddForce(
                forward * forwardAcceleration,
                ForceMode.Acceleration
            );
        }

        if (steeringMultiplier > 0f)
        {
            float effectiveSteerInput =
            steerInput *
            Mathf.Clamp01(steeringInputScale);
            rb.AddForce(
                steeringRight *
                (effectiveSteerInput * lateralAcceleration * steeringMultiplier),
                ForceMode.Acceleration
            );
        }

        if (liftMultiplier > 0f)
            ApplyLift(liftMultiplier);

        if (applyBoundary)
            ApplyBoundaryForce(boundaryRight);
    }

    private void ApplyPostImpactFlightControl()
    {
        Vector3 velocity =
            GetLinearVelocity();

        float speed =
            velocity.magnitude;
        float steeringSpeed =
            Vector3.ProjectOnPlane(
            velocity,
            Vector3.up
         ).magnitude;

        if (speed <= postImpactControlOffSpeed)
        {
            flightControlEnabled = false;
            steerInput = 0f;
            return;
        }

        /*
         * postImpactControlOffSpeed에서는 0,
         * postImpactSteeringFullEffectSpeed에서는 1이 됩니다.
         */
        float safeFullEffectSpeed =
            Mathf.Max(
                postImpactControlOffSpeed + 0.01f,
                postImpactSteeringFullEffectSpeed
            );

        float steeringSpeedScale =
            Mathf.InverseLerp(
                postImpactControlOffSpeed,
                safeFullEffectSpeed,
                steeringSpeed
            );

        ApplyFlightControl(
            allowForwardAssist: false,
            steeringMultiplier:
                postImpactSteeringMultiplier,
            liftMultiplier:
                postImpactLiftMultiplier,
            applyBoundary: false,
            steeringInputScale:
                steeringSpeedScale
        );

        ApplyPostImpactForwardAcceleration(speed);
    }
    

    private void ApplyPostImpactForwardAcceleration(float currentSpeed)
    {
        if (postImpactForwardAccelerationCoefficient <= 0f)
            return;

        if (currentSpeed <= postImpactForwardAccelerationMinSpeed)
            return;

        Vector3 forward = GetActiveFlightForward();

        float acceleration =
            currentSpeed * postImpactForwardAccelerationCoefficient;

        if (postImpactMaxForwardAcceleration > 0f)
        {
            acceleration = Mathf.Min(
                acceleration,
                postImpactMaxForwardAcceleration
            );
        }

        rb.AddForce(
            forward * acceleration,
            ForceMode.Acceleration
        );
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

        float finalLiftAcceleration =
        LimitLiftAcceleration(
            liftAcceleration,
            velocity
        );

        rb.AddForce(
            Vector3.up *
            finalLiftAcceleration,
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
    private Vector3 ClampFinalLaunchAngle(
    Vector3 launchVelocity)
    {
        float speed = launchVelocity.magnitude;

        if (speed <= 0.0001f)
            return Vector3.zero;

        Vector3 horizontalVelocity =
            Vector3.ProjectOnPlane(
                launchVelocity,
                Vector3.up
            );

        float horizontalSpeed =
            horizontalVelocity.magnitude;

        if (horizontalSpeed <= 0.0001f)
            return launchVelocity;

        float currentAngle =
            Mathf.Atan2(
                launchVelocity.y,
                horizontalSpeed
            ) * Mathf.Rad2Deg;

        // 위쪽 각도만 maxThrowAngle로 제한합니다.
        // 아래로 던지는 입력은 그대로 허용합니다.
        float clampedAngle =
            Mathf.Min(
                currentAngle,
                maxThrowUpAngle
            );

        float clampedAngleRadians =
            clampedAngle *
            Mathf.Deg2Rad;

        Vector3 horizontalDirection =
            horizontalVelocity /
            horizontalSpeed;

        Vector3 clampedDirection =
            horizontalDirection *
            Mathf.Cos(clampedAngleRadians) +
            Vector3.up *
            Mathf.Sin(clampedAngleRadians);
        Debug.Log(
    $"FINAL LAUNCH | " +
    $"power: {lastThrowPower01:F2}, " +
    $"thrustRatio: {lastThrowThrustRatio:F2}, " +
    $"velocity: {pendingLaunchVelocity}, " +
    $"angle: {clampedAngle:F2}, " +
    $"maxAngle: {maxThrowUpAngle:F2}",
    this
);

        return clampedDirection.normalized *
               speed;
    }
    private Vector3 ClampFinalLaunchVelocity(
     Vector3 launchVelocity,
     float throwPower01)
    {
        float actualSpeed =
            launchVelocity.magnitude;

        if (actualSpeed <= 0.0001f)
            return Vector3.zero;

        throwPower01 =
            Mathf.Clamp01(throwPower01);

        /*
         * 현재 투척 세기에 해당하는 정상 발사 속도입니다.
         */
        float currentThrustRatio;

        float configuredCurrentLaunchSpeed =
            CalculateLaunchSpeedFromThrowPower(
                throwPower01,
                out currentThrustRatio
            );

        /*
         * Throw Power 01이 1일 때의 최대 발사 속도입니다.
         *
         * 기존에 가정했던 maxThrowPower를
         * 이 값이 대신합니다.
         */
        float maximumThrustRatio;

        float configuredMaximumLaunchSpeed =
            CalculateLaunchSpeedFromThrowPower(
                1f,
                out maximumThrustRatio
            );

        configuredCurrentLaunchSpeed =
            Mathf.Max(
                0f,
                configuredCurrentLaunchSpeed
            );

        configuredMaximumLaunchSpeed =
            Mathf.Max(
                0f,
                configuredMaximumLaunchSpeed
            );

        float safeMaximumAngle =
            Mathf.Clamp(
                maxThrowUpAngle,
                0f,
                89f
            );

        float maximumAngleRadians =
            safeMaximumAngle *
            Mathf.Deg2Rad;

        /*
         * Y 속도 계산에 사용할 기준 속도입니다.
         *
         * 1. 실제 최종 Velocity보다 클 수 없음
         * 2. 현재 투척 세기로 계산된 속도보다 클 수 없음
         * 3. 최대 투척 세기의 속도보다 클 수 없음
         *
         * 따라서 약한 투척과 강한 투척 모두
         * Max Throw Up Angle을 넘지 않습니다.
         */
        float upwardSpeedReference =
            Mathf.Min(
                actualSpeed,
                Mathf.Min(
                    configuredCurrentLaunchSpeed,
                    configuredMaximumLaunchSpeed
                )
            );

        /*
         * 최대 허용 Y 속도:
         *
         * 현재 투척 속도
         * × sin(Max Throw Up Angle)
         */
        float maximumAllowedUpwardSpeed =
            upwardSpeedReference *
            Mathf.Sin(maximumAngleRadians);

        /*
         * 아래로 날아가는 Y 속도는 제한하지 않습니다.
         * 이미 최대 Y 속도 이하라면 그대로 반환합니다.
         */
        if (launchVelocity.y <=
            maximumAllowedUpwardSpeed)
        {
            return launchVelocity;
        }

        Vector3 horizontalVelocity =
            Vector3.ProjectOnPlane(
                launchVelocity,
                Vector3.up
            );

        Vector3 horizontalDirection;

        if (horizontalVelocity.sqrMagnitude >
            0.0001f)
        {
            horizontalDirection =
                horizontalVelocity.normalized;
        }
        else
        {
            horizontalDirection =
                Vector3.ProjectOnPlane(
                    GetTrackForward(),
                    Vector3.up
                );

            if (horizontalDirection.sqrMagnitude <
                0.0001f)
            {
                horizontalDirection =
                    Vector3.forward;
            }

            horizontalDirection.Normalize();
        }

        /*
         * 전체 실제 속력은 유지합니다.
         *
         * 초과한 Y 속도는 버리는 것이 아니라
         * 수평 속도로 재분배합니다.
         *
         * 따라서 강한 투척은 높이 뜨는 대신
         * 앞으로 더 멀리 나갑니다.
         */
        float newHorizontalSpeed = horizontalVelocity.magnitude;
            //Mathf.Sqrt(
              //  Mathf.Max(
                //    0f,
                  //  actualSpeed * actualSpeed -
                    //maximumAllowedUpwardSpeed *
                    //maximumAllowedUpwardSpeed
                //)
            //);

        return
            horizontalDirection *
            newHorizontalSpeed +
            Vector3.up *
            maximumAllowedUpwardSpeed;
    }

    #endregion

    #region Settling / Rotation / Stop

    public void BeginSettlingAfterImpact(float firstImpactSpeed)
    {
        if(state != DiscState.Flying)
        {
            Debug.LogWarning(
                $"BeginSettlingAfterImpact ignored | " +
                $"current state: {state}, " +
                $"impact speed: {firstImpactSpeed:F2}",
                this
            );

            return;
        }

        state = DiscState.Settling;

        flightControlEnabled = true;
        forwardAssistEnabled = false;

        settlingStartedTime = Time.time;
        lowSpeedTimer = 0f;
        settlingStopReady = false;
        rotationStoppedAfterLowSpeed = false;
        nextSettlingLogTime = 0f;

        activeFingerId = -1;
        mouseDragging = false;
        hasPendingLaunch = false;
        launchEventsPending = false;
        if (!allowPostImpactSteering)
            steerInput = 0f;

        rb.isKinematic = false;

        SetLinearDamping(settlingLinearDamping);
        SetAngularDamping(settlingAngularDamping);

        postImpactRotationUnlocked = false;

        if (unlockRotationAfterFirstImpact &&
            firstImpactSpeed <= unlockRotationImpactSpeedThreshold)
        {
            UnlockRotationAfterImpact("impact speed threshold");
        }
        else
        {
            rb.angularVelocity = Vector3.zero;
            rb.constraints &= ~RigidbodyConstraints.FreezeRotation;
        }
    }

    public void BeginSettlingAfterImpact()
    {
        BeginSettlingAfterImpact(GetLinearVelocity().magnitude);
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

    private void UpdatePostImpactRotationUnlock()
    {
        if (!unlockRotationAfterFirstImpact)
            return;

        if (postImpactRotationUnlocked)
            return;

        if (state != DiscState.Settling)
            return;

        float speed = GetLinearVelocity().magnitude;

        if (speed <= unlockRotationCurrentSpeedThreshold)
            UnlockRotationAfterImpact("current speed threshold");
    }

    private void UnlockRotationAfterImpact(string reason)
    {
        if (!unlockRotationAfterFirstImpact)
            return;

        if (postImpactRotationUnlocked)
            return;

        postImpactRotationUnlocked = true;

        rb.constraints &= ~RigidbodyConstraints.FreezeRotation;
        SetAngularDamping(unlockedRotationAngularDamping);
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

        if (linearSlowEnough)
        {
            lowSpeedTimer += Time.fixedDeltaTime;
        }
        else
        {
            lowSpeedTimer = 0f;
            settlingStopReady = false;
            return;
        }

        if (lowSpeedTimer >= requiredLowSpeedDurationToStop)
        {
            StopRotationAfterLowSpeedStable();
            settlingStopReady = true;
        }
        else
        {
            settlingStopReady = false;
        }

        if (logSettlingStopCheck && Time.time >= nextSettlingLogTime)
        {
            nextSettlingLogTime = Time.time + settlingLogInterval;

            Debug.Log(
                $"Settling stop check | " +
                $"speed: {velocity.magnitude:F2}, " +
                $"lowTimer: {lowSpeedTimer:F2}/{requiredLowSpeedDurationToStop:F2}, " +
                $"rotationStopped: {rotationStoppedAfterLowSpeed}, " +
                $"ready: {settlingStopReady}"
            );
        }
    }

    private void StopRotationAfterLowSpeedStable()
    {
        if (!stopRotationWhenLowSpeedStable)
            return;

        if (rotationStoppedAfterLowSpeed)
            return;

        rotationStoppedAfterLowSpeed = true;

        rb.angularVelocity = Vector3.zero;
        SetAngularDamping(lowSpeedStableAngularDamping);

        if (freezeRotationWhenLowSpeedStable)
            rb.constraints |= RigidbodyConstraints.FreezeRotation;
    }

    public bool IsSlowEnoughToStop()
    {
        return state == DiscState.Settling && settlingStopReady;
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
        rotationStoppedAfterLowSpeed = false;
        postImpactRotationUnlocked = false;

        activeFingerId = -1;
        mouseDragging = false;
        hasPendingLaunch = false;
        launchEventsPending = false;
        steerInput = 0f;

        rb.isKinematic = false;

        SetLinearVelocity(Vector3.zero);
        rb.angularVelocity = Vector3.zero;

        SetLinearDamping(stoppedLinearDamping);
        SetAngularDamping(stoppedLinearDamping);

        rb.constraints |= RigidbodyConstraints.FreezeRotation;

        rb.isKinematic = true;
    }

    #endregion

    #region Soft Obstacle Support

    public void ApplySoftObstaclePass(
        Vector3 sourcePosition,
        float speedLossRatio,
        float deflectionDegrees,
        float activeFlightDirectionBlend,
        float targetForwardSpeedLossBlend,
        float minHorizontalSpeedAfterEffect,
        float verticalSpeedMultiplier)
    {
        if (state != DiscState.Flying)
            return;

        Vector3 velocity = GetLinearVelocity();

        Vector3 horizontalVelocity = Vector3.ProjectOnPlane(
            velocity,
            Vector3.up
        );

        Vector3 verticalVelocity = velocity - horizontalVelocity;

        float horizontalSpeed = horizontalVelocity.magnitude;

        if (horizontalSpeed <= 0.001f)
            return;

        Vector3 currentDirection = horizontalVelocity / horizontalSpeed;

        float sideSign = CalculateSoftObstacleDeflectionSide(
            sourcePosition,
            currentDirection
        );

        Vector3 deflectedDirection =
            Quaternion.AngleAxis(
                deflectionDegrees * sideSign,
                Vector3.up
            ) * currentDirection;

        float targetHorizontalSpeed =
            horizontalSpeed * (1f - Mathf.Clamp01(speedLossRatio));

        if (minHorizontalSpeedAfterEffect > 0f &&
            horizontalSpeed > minHorizontalSpeedAfterEffect)
        {
            targetHorizontalSpeed = Mathf.Max(
                targetHorizontalSpeed,
                minHorizontalSpeedAfterEffect
            );
        }

        Vector3 newVelocity =
            deflectedDirection.normalized * targetHorizontalSpeed +
            verticalVelocity * Mathf.Clamp(verticalSpeedMultiplier, 0f, 1.5f);

        SetLinearVelocity(newVelocity);

        UpdateActiveFlightDirectionAfterSoftObstacle(
            deflectedDirection,
            activeFlightDirectionBlend
        );

        ReduceActiveTargetForwardSpeedAfterSoftObstacle(
            targetHorizontalSpeed,
            targetForwardSpeedLossBlend
        );
    }

    private float CalculateSoftObstacleDeflectionSide(
        Vector3 sourcePosition,
        Vector3 movementDirection)
    {
        Vector3 offsetFromSource = Vector3.ProjectOnPlane(
            rb.position - sourcePosition,
            Vector3.up
        );

        if (offsetFromSource.sqrMagnitude < 0.0001f)
            return 1f;

        Vector3 rightOfMovement = Vector3.Cross(
            Vector3.up,
            movementDirection
        );

        if (rightOfMovement.sqrMagnitude < 0.0001f)
            return 1f;

        rightOfMovement.Normalize();

        float side = Vector3.Dot(
            offsetFromSource.normalized,
            rightOfMovement
        );

        return side >= 0f ? 1f : -1f;
    }

    private void UpdateActiveFlightDirectionAfterSoftObstacle(
        Vector3 newDirection,
        float blend)
    {
        float clampedBlend = Mathf.Clamp01(blend);

        if (clampedBlend <= 0f)
            return;

        Vector3 flatDirection = Vector3.ProjectOnPlane(
            newDirection,
            Vector3.up
        );

        if (flatDirection.sqrMagnitude < 0.0001f)
            return;

        flatDirection.Normalize();

        activeFlightForward = Vector3.Slerp(
            GetActiveFlightForward(),
            flatDirection,
            clampedBlend
        ).normalized;

        activeFlightRight = Vector3.Cross(
            Vector3.up,
            activeFlightForward
        ).normalized;

        if (activeFlightRight.sqrMagnitude < 0.0001f)
            activeFlightRight = GetTrackRight();
    }

    private void ReduceActiveTargetForwardSpeedAfterSoftObstacle(
        float targetHorizontalSpeed,
        float blend)
    {
        float clampedBlend = Mathf.Clamp01(blend);

        if (clampedBlend <= 0f)
            return;

        float currentTargetSpeed = GetActiveTargetForwardSpeed();

        float reducedTargetSpeed = Mathf.Min(
            currentTargetSpeed,
            targetHorizontalSpeed
        );

        activeTargetForwardSpeed = Mathf.Lerp(
            currentTargetSpeed,
            reducedTargetSpeed,
            clampedBlend
        );
    }

    #endregion

    #region Position / Reset

    public void ApplyStats(DiscRuntimeStats stats)
    {
        /*
         * DiscSlingshotController는 비행 물리만 담당합니다.
         * maxDurability와 incomeMultiplier는 여기서 사용하지 않습니다.
         */

        runtimeStats = stats;

        runtimeStats.initialThrust = Mathf.Max(
            0.01f,
            stats.initialThrust
        );

        runtimeStats.lift = Mathf.Max(
            0f,
            stats.lift
        );

        targetForwardSpeed =
            runtimeStats.initialThrust *
            Mathf.Max(0f, targetForwardSpeedRatio);

        activeTargetForwardSpeed =
            targetForwardSpeed;

        runtimeStatsInitialized = true;

        Debug.Log(
            $"Disc flight stats applied | " +
            $"Initial Thrust: {runtimeStats.initialThrust:F2}, " +
            $"Lift: {runtimeStats.lift:F2}",
            this
        );
    }

    public void ResetToLaunch()
    {
        PlaceAtLaunchAnchor(true);
        //False to block input after gameover.
    }

    public void PlaceAtLaunchAnchor(bool readyForInput)
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        Vector3 targetPosition = launchAnchor != null
            ? launchAnchor.position
            : transform.position;

        Quaternion targetRotation = GetReadyRotation();

        // 입력 상태 초기화
        activeFingerId = -1;
        mouseDragging = false;

        // 발사 예약 상태 초기화
        hasPendingLaunch = false;
        launchEventsPending = false;
        pendingLaunchVelocity = Vector3.zero;

        // 비행 / 충돌 후 제어 상태 초기화
        flightControlEnabled = false;
        forwardAssistEnabled = false;

        // 조종 입력 초기화
        steerInput = 0f;

        // 던지기 세기 / 목표 속도 초기화
        activeTargetForwardSpeed = 0f;
        lastThrowPower01 = 0f;
        lastThrowThrustRatio = 1f;

        // 이번 투척의 기준 비행 방향 초기화
        activeFlightForward = GetTrackForward();
        activeFlightRight = GetTrackRight();

        // Settling / 정지 판정 상태 초기화
        lowSpeedTimer = 0f;
        settlingStopReady = false;
        rotationStoppedAfterLowSpeed = false;
        postImpactRotationUnlocked = false;

        // 드래그 상태 초기화
        totalDragScreen = Vector2.zero;
        pointerSamples.Clear();

        // 물리 상태 초기화
        rb.isKinematic = false;

        SetLinearVelocity(Vector3.zero);
        rb.angularVelocity = Vector3.zero;

        SetLinearDamping(flyingLinearDamping);
        SetAngularDamping(flyingAngularDamping);

        rb.useGravity = true;

        // Y 이동은 허용
        rb.constraints &= ~RigidbodyConstraints.FreezePositionY;

        // Ready 상태에서는 회전을 잠가서 시작 자세를 안정적으로 유지
        rb.constraints |= RigidbodyConstraints.FreezeRotation;

        // 핵심: 위치뿐 아니라 회전도 반드시 초기화
        rb.position = targetPosition;
        rb.rotation = targetRotation;

        transform.SetPositionAndRotation(
            targetPosition,
            targetRotation
        );

        dragTargetPosition = targetPosition;
        anchorPosition = targetPosition;

        // Ready 상태에서는 드래그 전까지 물리 시뮬레이션에 의해 움직이지 않게 함
        rb.isKinematic = true;

        // Visual 자식 오브젝트의 시각적 회전도 초기화
        ResetVisualPose();

        state = readyForInput
            ? DiscState.Ready
            : DiscState.Stopped;

        // Transform과 Physics 상태 동기화
        Physics.SyncTransforms();
    }

    private void ConfigureRigidbodyForReadyOrFlying()
    {
        rb.useGravity = true;
        rb.constraints &= ~RigidbodyConstraints.FreezePositionY;
        //rb.constraints |= RigidbodyConstraints.FreezeRotation;

        SetLinearDamping(flyingLinearDamping);
        SetAngularDamping(flyingAngularDamping);
    }
    private Quaternion GetReadyRotation()
    {
        Vector3 forward = Vector3.forward;

        if (launchAnchor != null)
        {
            forward = launchAnchor.forward;
        }
        else if (trackRoot != null)
        {
            forward = trackRoot.forward;
        }

        forward.y = 0f;

        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;

        return Quaternion.LookRotation(
            forward.normalized,
            Vector3.up
        );
    }

    #endregion

    #region Screen / UI Helpers

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

    #endregion

    #region Direction Helpers

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

    #endregion

    #region Visual

    private void UpdateVisual()
    {
        if (visualRoot == null)
            return;

        bool shouldSpin =
            state == DiscState.Flying ||
            (
                state == DiscState.Settling &&
                flightControlEnabled &&
                spinWhilePostImpactMoving &&
                !postImpactRotationUnlocked &&
                !rotationStoppedAfterLowSpeed &&
                !settlingStopReady
            );

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
    private void ResetVisualPose()
    {
        spinAngle = 0f;

        if (visualRoot == null)
            return;

        visualRoot.localPosition = Vector3.zero;
        visualRoot.localRotation = Quaternion.identity;
    }

    #endregion

    #region Rigidbody Compatibility Helpers

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

    #endregion
}