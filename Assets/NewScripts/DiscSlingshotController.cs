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
    [Header("Vertical Control Launch Delay")]
    

    

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
    [Header("Dynamic Active Flight Direction")]
    [Tooltip("원반의 현재 수평 속도 방향을 Active Flight Forward로 사용합니다.")]
    [SerializeField]
    private bool useVelocityAsActiveFlightForward = true;

    [Tooltip(
        "수평 속도가 이 값보다 느리면 방향을 갱신하지 않고 " +
        "마지막으로 유효했던 방향을 유지합니다."
    )]
    [SerializeField, Min(0f)]
    private float minPlanarSpeedForActiveDirection = 0.25f;

    [Header("Track Boundary")]
    [SerializeField] private float laneHalfWidth = 4.5f;
    [SerializeField] private float boundarySpring = 40f;
    [SerializeField] private float boundaryDamping = 10f;

    [Header("Vertical Drag Input")]
    [SerializeField, Range(0.001f, 0.1f)]
    private float verticalDragDeadZone = 0.015f;

    [SerializeField, Range(0.02f, 0.5f)]
    private float verticalDragFullScreenRatio = 0.2f;

    [SerializeField, Min(0.1f)]
    private float verticalInputResponse = 6f;

    [SerializeField, Min(0.1f)]
    private float verticalInputReturnSpeed = 5f;

    [Tooltip("수직 조종 해제 후 재개되는 전방 보조 가속도의 상한")]
    [SerializeField, Min(0f)]
    private float maxForwardAssistAcceleration = 12f;


    [Header("Climb")]
    [SerializeField, Min(0f)]
    private float extraLiftPerPlanarSpeed = 0.08f;

    [SerializeField, Min(0f)]
    private float minExtraLiftCoefficient = 0f;

    [SerializeField, Min(0f)]
    private float maxExtraLiftCoefficient = 1.5f;

    [SerializeField, Min(0f)]
    private float climbBrakeAcceleration = 3f;

    [SerializeField, Min(0f)]
    private float climbBrakeMinPlanarSpeed = 4f;

    [SerializeField, Min(0.01f)]
    private float climbLowSpeedFadeRange = 3f;


    [Header("Dive")]
    [SerializeField, Min(0f)]
    private float diveDownAcceleration = 8f;

    [SerializeField, Min(0f)]
    private float diveForwardAcceleration = 5f;

    [Tooltip("하강 조작 중 전체 속력의 소프트 상한")]
    [SerializeField, Min(0.1f)]
    private float diveMaxSpeed = 35f;

    [SerializeField, Min(0.01f)]
    private float diveSpeedFadeRange = 5f;

    [SerializeField, Min(0f)]
    private float diveSpeedLimitGain = 4f;


    [Header("Flight Path Angle")]
    [SerializeField, Range(1f, 80f)]
    private float maxClimbAngle = 25f;

    [SerializeField, Range(1f, 80f)]
    private float maxDiveAngle = 35f;

    [SerializeField, Min(0f)]
    private float flightAngleLimitGain = 4f;

    [SerializeField, Min(0f)]
    private float maxAngleCorrectionAcceleration = 20f;

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

    [Header("Post Impact Vertical Control")]
    [SerializeField, Range(0f, 1f)]
    private float postImpactVerticalMultiplier = 0.15f;

    [Header("Post Impact Rotation")]
    [Tooltip("첫 충돌 이후 조건에 따라 Rigidbody Freeze Rotation을 해제합니다.")]
    [SerializeField] private bool unlockRotationAfterFirstImpact = true;

    [Tooltip("임계속도 1. 첫 충돌 이후 현재 속도가 이 값 이하로 떨어지면 Freeze Rotation을 해제합니다.")]
    [SerializeField] private float unlockRotationCurrentSpeedThreshold = 1f;

    [Tooltip("임계속도 2. 첫 충돌 순간의 속도가 이 값 이하이면 즉시 Freeze Rotation을 해제합니다.")]
    [SerializeField] private float unlockRotationImpactSpeedThreshold = 2f;

    [Tooltip("Freeze Rotation 해제 후 사용할 회전 감쇠입니다.")]
    [SerializeField] private float unlockedRotationAngularDamping = 1.5f;
    [Header("Post Impact View Panning")]

    [Tooltip(
    "충돌 후 터치 입력으로 카메라와 조향 기준 방향을 회전시킵니다."
)]
    [SerializeField]
    private bool allowPostImpactViewPanning = true;

    [Tooltip(
        "충돌 후 카메라/Forward가 터치 입력에 따라 회전하는 속도입니다. " +
        "단위는 degrees/second입니다."
    )]
    [SerializeField, Min(0f)]
    private float postImpactViewPanDegreesPerSecond = 40f;

    [Tooltip("충돌 후 카메라 패닝 입력의 데드존입니다.")]
    [SerializeField, Range(0f, 0.95f)]
    private float postImpactViewPanDeadZone = 0.08f;

    [Tooltip(
        "1보다 크면 화면 중앙 부근의 패닝이 부드러워집니다."
    )]
    [SerializeField, Min(0.05f)]
    private float postImpactViewPanExponent = 1.2f;

    [SerializeField]
    private bool invertPostImpactViewPan = false;

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
    [Header("Visual Pitch")]
    [SerializeField, Range(0f, 80f)]
    private float visualMaxClimbPitch = 40f;

    [SerializeField, Range(0f, 80f)]
    private float visualMaxDivePitch = 50f;
    [Header("Events")]
    [SerializeField] private UnityEvent onLaunched = new UnityEvent();

    #endregion

    #region Runtime Fields

    public event UnityAction Launched;
    public event System.Action<bool, float> ActiveTimeAdvanced;

    public DiscRunManager RunManager { get; private set; }

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

    private bool decreaseSteeringSpeed;
    private float launchAimYaw;
    private float postImpactSteeringSpeedCompensation;
    private float previousPredictedPostImpactPlanarSpeed = -1f;

    private Quaternion visualBankLocalRotation =
    Quaternion.identity;

    private int flightTouchId = -1;
    private bool flightMouseHeld;
    private float flightPointerStartY;

    private float verticalInputTarget;
    private float verticalInput;
    private float verticalControlBlend;

    private bool HasVerticalCommand =>
        Mathf.Abs(verticalInput) > 0.0001f ||
        verticalControlBlend > 0.0001f;
    private bool hasVerticalSnapshot;
    private bool verticalRestorePending;
    private bool verticalRestoreBlocked;

    private DiscState verticalSnapshotState;
    private float savedPlanarSpeed;
    private float savedVerticalSpeed;
    private Vector3 savedPlanarForward;

    private float visualPitch;
    private float visualBank;
    private Quaternion visualHeadingLocal = Quaternion.identity;

    private bool verticalInputArmed;
    private bool verticalPointerAuthorized;



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
   
    public float LaunchAimYaw => launchAimYaw;
    public Vector3 CurrentLaunchAimForward => GetLaunchAimForward();

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
        ResetFlightSteeringInput();
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
        minPlanarSpeedForActiveDirection =
    Mathf.Max(
        0f,
        minPlanarSpeedForActiveDirection
    );
        postImpactViewPanDegreesPerSecond =
    Mathf.Max(
        0f,
        postImpactViewPanDegreesPerSecond
    );

        postImpactViewPanDeadZone =
            Mathf.Clamp(
                postImpactViewPanDeadZone,
                0f,
                0.95f
            );

        postImpactViewPanExponent =
            Mathf.Max(
                0.05f,
                postImpactViewPanExponent
            );
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
                {
                    bool needsPostImpactInput =
                        allowPostImpactSteering ||
                        allowPostImpactViewPanning;

                    if (needsPostImpactInput)
                        ReadSteeringInput();
                    else
                        ResetFlightSteeringInput();

                    if (allowPostImpactViewPanning)
                    {
                        UpdatePostImpactViewDirection(
                            Time.deltaTime
                        );
                    }

                    break;
                }

            default:
                ResetFlightSteeringInput();
                break;
        }

        UpdateVisual();
    }

    private void FixedUpdate()
    {
        UpdateVerticalInput();
        bool restoredThisStep = TryRestoreVerticalSnapshot();
        if (state == DiscState.Dragging && rb.isKinematic)
        {
            //rb.MovePosition(dragTargetPosition);
            rb.position = dragTargetPosition;
        }

        bool launchedThisStep = false;

        if (hasPendingLaunch)
        {
            ExecutePhysicsLaunch();
            launchedThisStep = true;
        }

        if (state == DiscState.Flying && flightControlEnabled && !launchedThisStep && !restoredThisStep)
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
            if (!restoredThisStep)
            {
                if (flightControlEnabled)
                    ApplyPostImpactFlightControl();

                UpdatePostImpactRotationUnlock();
                UpdateSettlingStopReadiness();
            }
        }
        if (state == DiscState.Flying)
        {
            ActiveTimeAdvanced?.Invoke(true, Time.fixedDeltaTime);
        }
        else if (state == DiscState.Settling)
        {
            ActiveTimeAdvanced?.Invoke(false, Time.fixedDeltaTime);
        }
    }
    public void SetRunManager(DiscRunManager manager)
    {
        RunManager = manager;
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
        if (releaseVelocityScreen.magnitude >= maxFlickPixelsPerSecond)
        {
            releaseVelocityScreen = releaseVelocityScreen.normalized * maxFlickPixelsPerSecond;
        }
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

        pendingLaunchVelocity = throwDirection * launchSpeed;
        //pendingLaunchVelocity =
    //ClampFinalLaunchAngle(
        //pendingLaunchVelocity
    //);
        //pendingLaunchVelocity =
   // ClampInitialUpwardSpeed(
       // pendingLaunchVelocity
   // );
        //pendingLaunchVelocity =
   // ClampFinalLaunchVelocity(
       // pendingLaunchVelocity,
        //lastThrowPower01
    //);
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
        //Vector2 releaseVelocityScreen = GetRecentScreenVelocity();
        // if(releaseVelocityScreen.magnitude >= maxFlickPixelsPerSecond)
        //  {
        //    releaseVelocityScreen = releaseVelocityScreen.normalized * maxFlickPixelsPerSecond;
        // }
        //float power01 = CalculateThrowPower01(totalDragScreen, releaseVelocityScreen);
        //lastThrowPower01 = power01;
        // Vector3 finalLaunchVelocity =
        // ClampFinalLaunchVelocity(
        //  pendingLaunchVelocity,
        // power01
        //);

        //pendingLaunchVelocity = finalLaunchVelocity;

        ResetFlightSteeringInput();
        
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

        rb.AddForce(pendingLaunchVelocity, ForceMode.VelocityChange);
        Debug.Log(
    $"Execute LAUNCH | " +
    $"power: {lastThrowPower01:F2}, " +
    $"thrustRatio: {lastThrowThrustRatio:F2}, " +
    $"velocity: {pendingLaunchVelocity}, " +
    $"maxAngle: {maxThrowUpAngle:F2}",
    this
);


        hasPendingLaunch = false;

        verticalInputArmed = !HasPressedPointer();

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
        //Vector3 forward = GetTrackForward();
        //Vector3 right = GetTrackRight();
        Vector3 forward = GetLaunchAimForward();
        Vector3 right = GetLaunchAimRight();

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
                GetLaunchAimForward(),
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


        float newHorizontalSpeed = horizontalVelocity.magnitude;
        //Mathf.Sqrt(
        //Mathf.Max(
        //    0f,
        //   totalSpeed * totalSpeed -
        //  clampedUpwardSpeed *
        //  clampedUpwardSpeed
        // )
        // );

        return
            horizontalVelocity *
            newHorizontalSpeed +
            Vector3.up *
            clampedUpwardSpeed;
    }
    private bool CanUseVerticalPointer =>
    verticalInputArmed &&
    verticalPointerAuthorized &&
    !hasPendingLaunch &&
    (
        state == DiscState.Flying ||
        state == DiscState.Settling
    );

    private bool HasPressedPointer()
    {
        foreach (ETouch touch in ETouch.activeTouches)
        {
            if (touch.phase == ETouchPhase.Began ||
                touch.phase == ETouchPhase.Moved ||
                touch.phase == ETouchPhase.Stationary)
            {
                return true;
            }
        }

#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL
        if (Mouse.current != null &&
            Mouse.current.leftButton.isPressed)
        {
            return true;
        }
#endif

        return false;
    }

    private void TryArmVerticalInput()
    {
        if (verticalInputArmed || hasPendingLaunch)
            return;

        if (state != DiscState.Flying &&
            state != DiscState.Settling)
        {
            return;
        }

        // 발사 때 남아 있던 포인터가 모두 해제되어야 준비됩니다.
        if (!HasPressedPointer())
            verticalInputArmed = true;
    }

    private void BeginFlightControlPointer(Vector2 position)
    {
        flightPointerStartY = position.y;

        // 새로 시작한 포인터에만 권한을 부여합니다.
        // 이미 누르고 있던 포인터는 나중에 자동 승인되지 않습니다.
        verticalPointerAuthorized = verticalInputArmed;
    }

    #endregion

    #region Active Flight Direction

    //private void SetActiveFlightDirection(Vector3 throwDirection)
    //{
    //    Vector3 trackForward = GetTrackForward();

    //    Vector3 flatThrowDirection = Vector3.ProjectOnPlane(
    //        throwDirection,
    //        Vector3.up
    //    );

    //    if (flatThrowDirection.sqrMagnitude < 0.0001f)
    //        flatThrowDirection = trackForward;

    //    flatThrowDirection.Normalize();

    //    activeFlightForward = Vector3.Slerp(
    //        flatThrowDirection,
    //        trackForward,
    //        Mathf.Clamp01(forwardCorrectionStrength)
    //    ).normalized;

    //    activeFlightRight = Vector3.Cross(
    //        Vector3.up,
    //        activeFlightForward
    //    ).normalized;

    //    if (activeFlightRight.sqrMagnitude < 0.0001f)
    //        activeFlightRight = GetTrackRight();
    //}
    private void SetActiveFlightDirection(
    Vector3 throwDirection)
    {
        if (!SetActiveFlightBasis(throwDirection))
        {
            SetActiveFlightBasis(
                GetLaunchAimForward()
            );
        }
    }
    private void UpdateActiveFlightDirection()
    {
        // 실시간 속도 방향 모드에서는 Track 방향 보정을 적용하지 않음
        if (useVelocityAsActiveFlightForward)
        {
            TryUpdateActiveFlightDirectionFromVelocity();
            return;
        }

        // 기존 방식이 필요할 때만 실행
        if (forwardCorrectionTurnSpeed <= 0f)
            return;

        Vector3 trackForward =
            GetTrackForward();

        float maxRadians =
            forwardCorrectionTurnSpeed *
            Mathf.Deg2Rad *
            Time.fixedDeltaTime;

        activeFlightForward =
            Vector3.RotateTowards(
                GetActiveFlightForward(),
                trackForward,
                maxRadians,
                0f
            ).normalized;

        activeFlightRight =
            Vector3.Cross(
                Vector3.up,
                activeFlightForward
            ).normalized;

        if (activeFlightRight.sqrMagnitude < 0.0001f)
        {
            activeFlightRight =
                GetTrackRight();
        }
    }
    private void UpdatePostImpactViewDirection(
    float deltaTime)
    {
        if (state != DiscState.Settling)
            return;

        if (!allowPostImpactViewPanning)
            return;

        if (postImpactViewPanDegreesPerSecond <= 0f ||
            deltaTime <= 0f)
        {
            return;
        }

        float inputMagnitude =
            Mathf.Abs(steerInput);

        if (inputMagnitude <=
            postImpactViewPanDeadZone)
        {
            return;
        }

        /*
         * 데드존 바깥 부분을 다시 0~1로 정규화합니다.
         * 따라서 데드존 경계에서 갑자기 강한 회전이 발생하지 않습니다.
         */
        float normalizedMagnitude =
            Mathf.InverseLerp(
                postImpactViewPanDeadZone,
                1f,
                inputMagnitude
            );

        float shapedInput =
            Mathf.Sign(steerInput) *
            Mathf.Pow(
                normalizedMagnitude,
                postImpactViewPanExponent
            );

        if (invertPostImpactViewPan)
            shapedInput = -shapedInput;

        float yawDelta =
            shapedInput *
            postImpactViewPanDegreesPerSecond *
            deltaTime;

        Vector3 nextForward =
            Quaternion.AngleAxis(
                yawDelta,
                Vector3.up
            ) *
            CurrentActiveFlightForward;

        SetActiveFlightBasis(nextForward);
    }

    private bool SetActiveFlightBasis(
        Vector3 worldForward)
    {
        Vector3 planarForward =
            Vector3.ProjectOnPlane(
                worldForward,
                Vector3.up
            );

        if (planarForward.sqrMagnitude < 0.0001f)
            return false;

        activeFlightForward =
            planarForward.normalized;

        activeFlightRight =
            Vector3.Cross(
                Vector3.up,
                activeFlightForward
            );

        if (activeFlightRight.sqrMagnitude < 0.0001f)
        {
            activeFlightRight =
                GetTrackRight();
        }
        else
        {
            activeFlightRight.Normalize();
        }

        return true;
    }
    
    private bool TryUpdateActiveFlightDirectionFromVelocity()
    {
        if (!useVelocityAsActiveFlightForward)
            return false;

        if (rb == null)
            return false;

        if (state != DiscState.Flying)
        {
            return false;
        }

        Vector3 planarVelocity =
            Vector3.ProjectOnPlane(
                GetLinearVelocity(),
                Vector3.up
            );

        float minimumSpeedSquared =
            minPlanarSpeedForActiveDirection *
            minPlanarSpeedForActiveDirection;

        // 거의 멈췄을 때 물리 흔들림으로 방향이 계속 바뀌는 것을 방지
        if (planarVelocity.sqrMagnitude <
            Mathf.Max(0.0001f, minimumSpeedSquared))
        {
            return false;
        }

        activeFlightForward =
            planarVelocity.normalized;

        activeFlightRight =
            Vector3.Cross(
                Vector3.up,
                activeFlightForward
            ).normalized;

        return true;
    }
    public Vector3 CurrentActiveFlightForward
    {
        get
        {
            Vector3 forward =
                Vector3.ProjectOnPlane(
                    activeFlightForward,
                    Vector3.up
                );

            if (forward.sqrMagnitude < 0.0001f)
                forward = GetTrackForward();

            return forward.normalized;
        }
    }
    private Vector3 GetSteeringRightForControl()
    {
        if (inputCamera != null)
        {
            Vector3 cameraRight =
                Vector3.ProjectOnPlane(
                    inputCamera.transform.right,
                    Vector3.up
                );

            if (cameraRight.sqrMagnitude > 0.0001f)
                return cameraRight.normalized;
        }

        return Vector3.Cross(
            Vector3.up,
            CurrentActiveFlightForward
        ).normalized;
    }
    //private Vector3 GetActiveFlightForward()
    //{

    //    if (useVelocityAsActiveFlightForward && state == DiscState.Flying)
    //    {
    //        Vector3 planarVelocity = Vector3.ProjectOnPlane(
    //            GetLinearVelocity(),
    //            Vector3.up
    //        );

    //        if (planarVelocity.sqrMagnitude > 0.0001f)
    //        {
    //            activeFlightForward =
    //                planarVelocity.normalized;
    //        }
    //    }

    //    if (activeFlightForward.sqrMagnitude < 0.0001f)
    //        activeFlightForward = GetTrackForward();

    //    return activeFlightForward.normalized;
    //}
    private Vector3 GetActiveFlightForward()
    {
        TryUpdateActiveFlightDirectionFromVelocity();

        return CurrentActiveFlightForward;
    }

    private Vector3 GetActiveFlightRight()
    {
        Vector3 forward = GetActiveFlightForward();

        Vector3 right = Vector3.Cross(
            Vector3.up,
            forward
        );

        if (right.sqrMagnitude < 0.0001f)
            right = GetTrackRight();

        activeFlightRight = right.normalized;

        return activeFlightRight;
    }

    #endregion

    #region Flight Control

    private void ReadSteeringInput()
    {
        TryArmVerticalInput();

        steerInput = 0f;
        verticalInputTarget = 0f;

        if (flightTouchId >= 0)
        {
            foreach (ETouch touch in ETouch.activeTouches)
            {
                if (touch.touchId != flightTouchId)
                    continue;

                if (touch.phase == ETouchPhase.Ended ||
                    touch.phase == ETouchPhase.Canceled)
                {
                    break;
                }

                SetFlightPointerInput(touch.screenPosition);
                return;
            }

            EndFlightSteeringPointer();
            return;
        }

        if (!flightMouseHeld)
        {
            foreach (ETouch touch in ETouch.activeTouches)
            {
                if (touch.phase != ETouchPhase.Began ||
                    IsPointerOverUI(touch.screenPosition))
                {
                    continue;
                }

                flightTouchId = touch.touchId;
                BeginFlightControlPointer(touch.screenPosition);

                SetFlightPointerInput(touch.screenPosition);
                return;
            }
        }

#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL
        Mouse mouse = Mouse.current;

        if (mouse != null)
        {
            Vector2 position = mouse.position.ReadValue();

            if (!flightMouseHeld &&
                mouse.leftButton.wasPressedThisFrame &&
                !IsPointerOverUI(position))
            {
                flightMouseHeld = true;
                BeginFlightControlPointer(position);
            }

            if (flightMouseHeld)
            {
                if (mouse.leftButton.isPressed)
                {
                    SetFlightPointerInput(position);
                    return;
                }

                EndFlightSteeringPointer();
                return;
            }
        }

        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed ||
                Keyboard.current.leftArrowKey.isPressed)
            {
                steerInput -= 1f;
            }

            if (Keyboard.current.dKey.isPressed ||
                Keyboard.current.rightArrowKey.isPressed)
            {
                steerInput += 1f;
            }
        }
#endif
    }

    private void SetFlightPointerInput(Vector2 position)
    {
        float halfWidth = Mathf.Max(1f, Screen.width * 0.5f);

        steerInput = Mathf.Clamp(
            (position.x - halfWidth) / halfWidth,
            -1f,
            1f
        );

        if (!CanUseVerticalPointer)
        {
            flightPointerStartY = position.y;
            verticalInputTarget = 0f;
            return;
        }

        float delta =
            (position.y - flightPointerStartY) /
            Mathf.Max(1f, Screen.height);

        float deadZone = Mathf.Max(0f, verticalDragDeadZone);

        float fullDrag = Mathf.Max(
            deadZone + 0.001f,
            verticalDragFullScreenRatio
        );

        float amount = Mathf.InverseLerp(
            deadZone,
            fullDrag,
            Mathf.Abs(delta)
        );

        verticalInputTarget = Mathf.Sign(delta) * amount;
    }

    private void UpdateVerticalInput()
    {
        bool allowed =
            CanUseVerticalPointer &&
            flightControlEnabled &&
            (
                state == DiscState.Flying ||
                (
                    state == DiscState.Settling &&
                    allowPostImpactSteering &&
                    postImpactVerticalMultiplier > 0f
                )
            );

        if (!allowed)
        {
            verticalInputTarget = 0f;
            verticalInput = 0f;
            verticalControlBlend = 0f;
            return;
        }

        if (verticalRestorePending)
            return;

        if (!hasVerticalSnapshot &&
            !verticalRestoreBlocked &&
            Mathf.Abs(verticalInputTarget) > 0.0001f)
        {
            CaptureVerticalSnapshot();
        }

        float rate = Mathf.Abs(verticalInputTarget) < 0.0001f
            ? verticalInputReturnSpeed
            : verticalInputResponse;

        float step =
            Mathf.Max(0.1f, rate) * Time.fixedDeltaTime;

        verticalInput = Mathf.MoveTowards(
            verticalInput,
            verticalInputTarget,
            step
        );

        float blendTarget =
            Mathf.Abs(verticalInputTarget) > 0.0001f ? 1f : 0f;

        verticalControlBlend = Mathf.MoveTowards(
            verticalControlBlend,
            blendTarget,
            step
        );
    }

    private void ResetFlightSteeringInput()
    {
        flightTouchId = -1;
        flightMouseHeld = false;

        verticalInputArmed = false;
        verticalPointerAuthorized = false;

        verticalInputTarget = 0f;
        verticalInput = 0f;
        verticalControlBlend = 0f;
        steerInput = 0f;

        hasVerticalSnapshot = false;
        verticalRestorePending = false;
        verticalRestoreBlocked = false;
    }

    private static float SmoothBand(
        float value,
        float from,
        float to)
    {
        return Mathf.SmoothStep(
            0f,
            1f,
            Mathf.InverseLerp(
                from,
                Mathf.Max(from + 0.001f, to),
                value
            )
        );
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
    private void ApplyVerticalFlightControl(
    float authority,
    float baseLiftMultiplier)
    {
        if (!CanUseVerticalPointer || !HasVerticalCommand || authority <= 0f)
        {
            return;
        }

        Vector3 velocity = GetLinearVelocity();

        Vector3 planarVelocity =
            Vector3.ProjectOnPlane(velocity, Vector3.up);

        float planarSpeed = planarVelocity.magnitude;
        float speed = velocity.magnitude;

        // 카메라 방향과 관계없이 실제 이동 방향으로 가속·감속합니다.
        Vector3 planarForward = planarSpeed > 0.001f
            ? planarVelocity / planarSpeed
            : GetActiveFlightForward();

        float up = Mathf.Max(0f, verticalInput) * authority;
        float down = Mathf.Max(0f, -verticalInput) * authority;

        float gravity = Mathf.Max(0f, -Physics.gravity.y);

        // 1. 저속에서 상승 양력과 상승 브레이크를 함께 줄입니다.
        float floor = Mathf.Max(0f, climbBrakeMinPlanarSpeed);

        float lowSpeedScale = SmoothBand(
            planarSpeed,
            floor,
            floor + Mathf.Max(0.01f, climbLowSpeedFadeRange)
        );

        float minCoefficient =
            Mathf.Max(0f, minExtraLiftCoefficient);

        float coefficient = Mathf.Clamp(
            planarSpeed * Mathf.Max(0f, extraLiftPerPlanarSpeed),
            minCoefficient,
            Mathf.Max(minCoefficient, maxExtraLiftCoefficient)
        );

        Vector3 acceleration =
            Vector3.up *
            (gravity * coefficient * up * lowSpeedScale);

        acceleration -=
            planarForward *
            (
                Mathf.Max(0f, climbBrakeAcceleration) *
                up *
                lowSpeedScale
            );

        // 2. 하강 가속: 최고 속도에 접근하면 전방·하방 가속 모두 감소
        float limit = Mathf.Max(0.1f, diveMaxSpeed);

        float speedLimitBlend = SmoothBand(
            speed,
            Mathf.Max(0f, limit - diveSpeedFadeRange),
            limit
        );

        acceleration +=
            down *
            (1f - speedLimitBlend) *
            (
                planarForward * Mathf.Max(0f, diveForwardAcceleration) -
                Vector3.up * Mathf.Max(0f, diveDownAcceleration)
            );

        // 기존 양력과 중력의 합. 중력은 Rigidbody가 실제 적용합니다.
        float baseY =
            Physics.gravity.y +
            CalculateBaseLiftAcceleration(
                velocity,
                baseLiftMultiplier
            );

        // 3. 이동 경로의 상승·하강각을 가속도로 제한
        // vy = planarSpeed * tan(angle)을 기준으로 계산합니다.
        float controlBlend = verticalControlBlend;

        // 거의 정지하면 비행각의 의미가 약해지므로 보정을 줄입니다.
        controlBlend *= SmoothBand(
            planarSpeed,
            0f,
            Mathf.Max(0.1f, floor)
        );

        float upSlope = Mathf.Tan(
            Mathf.Clamp(maxClimbAngle, 1f, 80f) * Mathf.Deg2Rad
        );

        float downSlope = Mathf.Tan(
            Mathf.Clamp(maxDiveAngle, 1f, 80f) * Mathf.Deg2Rad
        );

        float gain = Mathf.Max(0f, flightAngleLimitGain);

        float planarAcceleration =
            Vector3.Dot(acceleration, planarForward);

        // 수평 감속으로 비행각이 커지는 효과도 함께 반영합니다.
        float minNetY =
            -planarAcceleration * downSlope +
            gain * (-planarSpeed * downSlope - velocity.y);

        float maxNetY =
            planarAcceleration * upSlope +
            gain * (planarSpeed * upSlope - velocity.y);

        float netY = baseY + acceleration.y;

        float limitedNetY = Mathf.Clamp(
            netY,
            Mathf.Min(minNetY, maxNetY),
            Mathf.Max(minNetY, maxNetY)
        );

        float correctionLimit =
            Mathf.Max(0f, maxAngleCorrectionAcceleration);

        if (gain > 0f)
        {
            acceleration.y +=
                Mathf.Clamp(
                    limitedNetY - netY,
                    -correctionLimit,
                    correctionLimit
                ) *
                controlBlend;
        }

        // 4. 하강 중 속도 제한용 항력
        // 추가 가속만 끄면 중력으로 계속 빨라질 수 있어 이를 보완합니다.
        if (down > 0f && speed > 0.001f)
        {
            Vector3 direction = velocity / speed;

            float drive = Vector3.Dot(
                Vector3.up * baseY + acceleration,
                direction
            );

            float drag =
                Mathf.Max(0f, drive) * speedLimitBlend +
                Mathf.Max(0f, speed - limit) *
                Mathf.Max(0f, diveSpeedLimitGain);

            acceleration -=
                direction * (drag * verticalControlBlend);
        }

        rb.AddForce(
            acceleration,
            ForceMode.Acceleration
        );
    }
    private void ApplyFlightControl(
    bool allowForwardAssist,
    float steeringMultiplier,
    float liftMultiplier,
    bool applyBoundary,
    float steeringInputScale = 1f)
    {
        if (state != DiscState.Flying &&
            state != DiscState.Settling)
        {
            return;
        }

        Vector3 velocity = GetLinearVelocity();
        Vector3 forward = GetActiveFlightForward();

        Vector3 steeringRight = GetActiveFlightRight();

        // 수직 조종 중에는 상승 감속·하강 가속에 간섭하지 않습니다.
        if (allowForwardAssist &&
            forwardAssistEnabled &&
            !HasVerticalCommand)
        {
            float acceleration =
                Mathf.Max(
                    0f,
                    GetActiveTargetForwardSpeed() -
                    Vector3.Dot(velocity, forward)
                ) *
                forwardSpeedGain;

            // 해제 후 급격한 재가속을 줄이는 가속도 상한입니다.
            acceleration = Mathf.Min(
                acceleration,
                Mathf.Max(0f, maxForwardAssistAcceleration)
            );

            rb.AddForce(
                forward * acceleration,
                ForceMode.Acceleration
            );
        }

        if (steeringMultiplier > 0f && maxLateralSpeed > 0f)
        {
            float input =
                steerInput * Mathf.Clamp01(steeringInputScale);

            float alongInput =
                Vector3.Dot(velocity, steeringRight) *
                Mathf.Sign(input);

            // 해당 방향의 횡속도가 높으면 추가 횡가속을 줄입니다.
            // 반대 방향으로 조작할 때는 제동할 수 있습니다.
            float room = 1f - SmoothBand(
                alongInput,
                maxLateralSpeed * 0.75f,
                maxLateralSpeed
            );

            rb.AddForce(
                steeringRight *
                (
                    input *
                    lateralAcceleration *
                    steeringMultiplier *
                    room
                ),
                ForceMode.Acceleration
            );
        }

        if (liftMultiplier > 0f)
            ApplyLift(liftMultiplier);

        if (applyBoundary)
            ApplyBoundaryForce(GetTrackRight());

        float verticalAuthority =
            state == DiscState.Flying
                ? 1f
                : (
                    allowPostImpactSteering
                        ? postImpactVerticalMultiplier
                        : 0f
                );

        ApplyVerticalFlightControl(
            verticalAuthority,
            liftMultiplier
        );
    }

    private void ApplyPostImpactFlightControl()
    {
        Vector3 velocity = GetLinearVelocity();
        float speed = velocity.magnitude;

        Vector3 planarVelocity =
            Vector3.ProjectOnPlane(
                velocity,
                Vector3.up
            );

        float planarSpeed =
            planarVelocity.magnitude;

        if(speed <= postImpactControlOffSpeed)
{
            flightControlEnabled = false;
            ResetFlightSteeringInput();
            return;
        }

        float safeFullEffectSpeed = Mathf.Max(
            postImpactControlOffSpeed + 0.01f,
            postImpactSteeringFullEffectSpeed
        );

        float steeringSpeedScale = Mathf.InverseLerp(
            postImpactControlOffSpeed,
            safeFullEffectSpeed,
            planarSpeed
        );

        ApplyPostImpactSteeringForce(
            planarVelocity,
            steeringSpeedScale
        );

        if (postImpactLiftMultiplier > 0f)
        {
            ApplyLift(
                postImpactLiftMultiplier
            );
        }

        ApplyVerticalFlightControl(
    allowPostImpactSteering
        ? postImpactVerticalMultiplier
        : 0f,
    postImpactLiftMultiplier
);
        ApplyPostImpactForwardAcceleration(speed);
    }
    private void ApplyPostImpactSteeringForce(
    Vector3 planarVelocity,
    float steeringInputScale)
    {
        if (!allowPostImpactSteering)
            return;

        float effectiveSteerInput =
            steerInput *
            Mathf.Clamp01(steeringInputScale);

        if (Mathf.Abs(effectiveSteerInput) < 0.0001f)
            return;

        /*
         * Settling 진입 시 고정된 카메라 기준 right입니다.
         */
        Vector3 steeringRight = GetSteeringRightForControl();

        Vector3 steeringAcceleration =
            steeringRight *
            (
                effectiveSteerInput *
                lateralAcceleration *
                postImpactSteeringMultiplier
            );

        if (planarVelocity.sqrMagnitude > 0.0001f)
        {
            Vector3 movementDirection =
                planarVelocity.normalized;

            /*
             * 양수이면 조향 가속도가 현재 이동 방향으로
             * 운동에너지를 추가하는 성분입니다.
             */
            float accelerationAlongMovement =
                Vector3.Dot(
                    steeringAcceleration,
                    movementDirection
                );

            /*
             * 속력을 증가시키는 성분만 제거합니다.
             *
             * 수직 성분: 방향 전환이므로 유지
             * 음수 성분: 반대 조향에 의한 감속이므로 유지
             */
            if (accelerationAlongMovement > 0f)
            {
                steeringAcceleration -=
                    movementDirection *
                    accelerationAlongMovement;
            }
        }

        rb.AddForce(
            steeringAcceleration,
            ForceMode.Acceleration
        );
    }


    private void ApplyPostImpactForwardAcceleration(float currentSpeed)
    {
        if (HasVerticalCommand)
            return;
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

    private float CalculateBaseLiftAcceleration(
    Vector3 velocity,
    float multiplier)
    {
        float planarSpeed =
            Vector3.ProjectOnPlane(
                velocity,
                Vector3.up
            ).magnitude;

        float speedFactor = Mathf.Clamp01(
            planarSpeed /
            Mathf.Max(0.01f, GetActiveTargetForwardSpeed())
        );

        float rawAcceleration =
            Mathf.Max(0f, -Physics.gravity.y) *
            runtimeStats.lift *
            speedFactor *
            Mathf.Max(0f, multiplier);

        return LimitLiftAcceleration(
            rawAcceleration,
            velocity
        );
    }

    private void ApplyLift(float multiplier)
    {
        float acceleration = CalculateBaseLiftAcceleration(
            GetLinearVelocity(),
            multiplier
        );

        rb.AddForce(
            Vector3.up * acceleration,
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

    private void CaptureVerticalSnapshot()
    {
        Vector3 velocity = GetLinearVelocity();
        Vector3 planar = Vector3.ProjectOnPlane(
            velocity,
            Vector3.up
        );

        savedPlanarSpeed = planar.magnitude;
        savedVerticalSpeed = velocity.y;

        savedPlanarForward = savedPlanarSpeed > 0.001f
            ? planar / savedPlanarSpeed
            : GetActiveFlightForward();

        verticalSnapshotState = state;
        hasVerticalSnapshot = true;
    }

    private void EndFlightSteeringPointer()
    {
        verticalRestorePending =
            hasVerticalSnapshot &&
            !verticalRestoreBlocked;

        if (!verticalRestorePending)
            hasVerticalSnapshot = false;

        verticalRestoreBlocked = false;

        flightTouchId = -1;
        flightMouseHeld = false;

        // 해제 시 추가 양력·브레이크·다이브 힘을 즉시 제거합니다.
        verticalInputTarget = 0f;
        verticalInput = 0f;
        verticalControlBlend = 0f;
        steerInput = 0f;

        verticalPointerAuthorized = false;
    }

    private void InvalidateVerticalSnapshot()
    {
        hasVerticalSnapshot = false;
        verticalRestorePending = false;

        // 충돌 후 같은 터치로 이전 속도를 다시 저장하지 않습니다.
        verticalRestoreBlocked =
            flightTouchId >= 0 || flightMouseHeld;
    }

    private bool TryRestoreVerticalSnapshot()
    {
        if (!verticalRestorePending)
            return false;

        verticalRestorePending = false;

        bool canRestore =
            hasVerticalSnapshot &&
            rb != null &&
            !rb.isKinematic &&
            flightControlEnabled &&
            state == verticalSnapshotState &&
            (
                state == DiscState.Flying ||
                state == DiscState.Settling
            );

        hasVerticalSnapshot = false;

        if (!canRestore)
            return false;

        Vector3 currentVelocity = GetLinearVelocity();

        Vector3 currentPlanar = Vector3.ProjectOnPlane(
            currentVelocity,
            Vector3.up
        );

        // 좌우 조종으로 바뀐 현재 진행 방향은 유지합니다.
        Vector3 forward = currentPlanar.sqrMagnitude > 0.0001f
            ? currentPlanar.normalized
            : savedPlanarForward;

        Vector3 restoredVelocity =
            forward * savedPlanarSpeed +
            Vector3.up * savedVerticalSpeed;

        rb.AddForce(
            restoredVelocity - currentVelocity,
            ForceMode.VelocityChange
        );

        // 복원 프레임에서도 기본 양력은 적용합니다.
        float liftMultiplier = state == DiscState.Flying
            ? 1f
            : postImpactLiftMultiplier;

        rb.AddForce(
            Vector3.up * CalculateBaseLiftAcceleration(
                restoredVelocity,
                liftMultiplier
            ),
            ForceMode.Acceleration
        );

        verticalInput = 0f;
        verticalControlBlend = 0f;

        if (state == DiscState.Settling)
        {
            lowSpeedTimer = 0f;
            settlingStopReady = false;
        }

        // 아래에서 추가할 Visual 변수·메서드입니다.
        visualPitch = GetVisualFlightPitch(restoredVelocity);

        return true;
    }

    #endregion

    #region Settling / Rotation / Stop

    public void BeginSettlingAfterImpact(float firstImpactSpeed)
    {
        InvalidateVerticalSnapshot();
        if (state != DiscState.Flying)
        {
            Debug.LogWarning(
                $"BeginSettlingAfterImpact ignored | " +
                $"current state: {state}, " +
                $"impact speed: {firstImpactSpeed:F2}",
                this
            );

            return;
        }

        FreezeActiveFlightDirectionForSettling();

        state = DiscState.Settling;

        //ResetPostImpactSteeringSpeedCompensation();

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

    private void ResetPostImpactSteeringSpeedCompensation()
    {
        postImpactSteeringSpeedCompensation = 0f;
        previousPredictedPostImpactPlanarSpeed = -1f;
    }
    private void FreezeActiveFlightDirectionForSettling()
    {
        Vector3 initialForward = Vector3.zero;

        if (inputCamera != null)
        {
            initialForward =
                Vector3.ProjectOnPlane(
                    inputCamera.transform.forward,
                    Vector3.up
                );
        }

        if (initialForward.sqrMagnitude < 0.0001f)
        {
            initialForward =
                Vector3.ProjectOnPlane(
                    activeFlightForward,
                    Vector3.up
                );
        }

        if (initialForward.sqrMagnitude < 0.0001f)
        {
            initialForward =
                Vector3.ProjectOnPlane(
                    GetLinearVelocity(),
                    Vector3.up
                );
        }

        if (initialForward.sqrMagnitude < 0.0001f)
            initialForward = GetTrackForward();

        SetActiveFlightBasis(initialForward);
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
    private void OnCollisionEnter(Collision collision)
    {
        InvalidateVerticalSnapshot();
    }

    private void OnCollisionStay(Collision collision)
    {
        InvalidateVerticalSnapshot();
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
        ResetFlightSteeringInput();
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
        ResetFlightSteeringInput();
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        launchAimYaw = 0f;
        ResetPostImpactSteeringSpeedCompensation();

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
        return Quaternion.LookRotation(
            GetLaunchAimForward(),
            Vector3.up
        );
    }
    //private Quaternion GetReadyRotation()
    //{
    //    Vector3 forward = Vector3.forward;

    //    if (launchAnchor != null)
    //    {
    //        forward = launchAnchor.forward;
    //    }
    //    else if (trackRoot != null)
    //    {
    //        forward = trackRoot.forward;
    //    }

    //    forward.y = 0f;

    //    if (forward.sqrMagnitude < 0.0001f)
    //        forward = Vector3.forward;

    //    return Quaternion.LookRotation(
    //        forward.normalized,
    //        Vector3.up
    //    );
    //}

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
    public void SetLaunchAimYaw(float yawDegrees)
    {
        // 버튼은 Ready 상태에서만 작동합니다.
        if (!IsReady)
            return;

        launchAimYaw = Mathf.DeltaAngle(0f, yawDegrees);

        Quaternion readyRotation = GetReadyRotation();

        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (rb != null)
            rb.rotation = readyRotation;

        transform.rotation = readyRotation;
    }

    private Vector3 GetLaunchAimForward()
    {
        Vector3 baseForward;

        if (launchAnchor != null)
            baseForward = launchAnchor.forward;
        else
            baseForward = GetTrackForward();

        baseForward = Vector3.ProjectOnPlane(
            baseForward,
            Vector3.up
        );

        if (baseForward.sqrMagnitude < 0.0001f)
            baseForward = Vector3.forward;

        baseForward.Normalize();

        return (
            Quaternion.AngleAxis(
                launchAimYaw,
                Vector3.up
            ) * baseForward
        ).normalized;
    }

    private Vector3 GetLaunchAimRight()
    {
        Vector3 right = Vector3.Cross(
            Vector3.up,
            GetLaunchAimForward()
        );

        if (right.sqrMagnitude < 0.0001f)
            return Vector3.right;

        return right.normalized;
    }

    #endregion

    #region Visual

    private void UpdateVisual()
    {
        if (visualRoot == null || visualRoot == transform)
            return;

        // 조종 방향을 기준으로 Visual 자세를 잡을 수 있는 상태
        bool controlledPose =
            state == DiscState.Flying ||
            (
                state == DiscState.Settling &&
                flightControlEnabled &&
                !postImpactRotationUnlocked &&
                !rotationStoppedAfterLowSpeed &&
                !settlingStopReady
            );

        // 좌우 뱅크는 수직 조종 여부와 독립적으로 판단합니다.
        bool canShowSteeringBank =
            (
                state == DiscState.Flying &&
                flightControlEnabled
            ) ||
            (
                state == DiscState.Settling &&
                flightControlEnabled &&
                allowPostImpactSteering &&
                !rotationStoppedAfterLowSpeed &&
                !settlingStopReady
            );

        bool shouldSpin =
            state == DiscState.Flying ||
            (
                state == DiscState.Settling &&
                controlledPose &&
                spinWhilePostImpactMoving
            );

        // 1. 스핀
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
                (
                    spinAngle +
                    spinDegreesPerSecond *
                    speedFactor *
                    Time.deltaTime
                ) % 360f;
        }

        // 2. 좌우 기울기: 수직 조종 조건 밖에서 계산
        float targetBank = canShowSteeringBank
            ? -steerInput * bankAngle
            : 0f;

        // 3. 위아래 기울기와 진행 방향
        float targetPitch = 0f;

        if (controlledPose)
        {
            Vector3 forward = Vector3.ProjectOnPlane(
                GetActiveFlightForward(),
                Vector3.up
            );

            if (forward.sqrMagnitude > 0.0001f)
            {
                Quaternion parentRotation =
                    visualRoot.parent != null
                        ? visualRoot.parent.rotation
                        : Quaternion.identity;

                Quaternion worldHeading =
                    Quaternion.LookRotation(
                        forward.normalized,
                        Vector3.up
                    );

                visualHeadingLocal =
                    Quaternion.Inverse(parentRotation) *
                    worldHeading;
            }

            // 이 메서드 내부에서 수직 조종 여부를 검사합니다.
            // 0을 반환하더라도 좌우 뱅크 계산은 계속됩니다.
            targetPitch = GetVisualFlightPitch(
                GetLinearVelocity()
            );
        }

        // 4. 피치와 뱅크를 각각 보간
        float visualBlend =
            1f - Mathf.Exp(
                -Mathf.Max(0f, visualLerp) * Time.deltaTime
            );

        visualPitch = Mathf.Lerp(
            visualPitch,
            targetPitch,
            visualBlend
        );

        visualBank = Mathf.Lerp(
            visualBank,
            targetBank,
            visualBlend
        );

        Quaternion pitchRotation = Quaternion.AngleAxis(
            -visualPitch,
            Vector3.right
        );

        Quaternion bankRotation = Quaternion.AngleAxis(
            visualBank,
            Vector3.forward
        );

        Quaternion spinRotation = Quaternion.AngleAxis(
            spinAngle,
            Vector3.up
        );

        // 5. 좌우 뱅크를 포함한 최종 회전
        visualRoot.localRotation =
            visualHeadingLocal *
            pitchRotation *
            bankRotation *
            spinRotation *
            visualInitialLocalRotation;
    }
    private void ResetVisualPose()
    {
        spinAngle = 0f;

        visualPitch = 0f;
        visualBank = 0f;
        visualHeadingLocal = Quaternion.identity;

        if (visualRoot == null)
            return;

        visualRoot.localPosition = Vector3.zero;
        visualRoot.localRotation = visualInitialLocalRotation;
    }
    private float GetVisualFlightPitch(Vector3 velocity)
    {
        bool showVerticalPitch =
            CanUseVerticalPointer &&
            (
                HasVerticalCommand ||
                hasVerticalSnapshot
            );

        // 유효한 수직 조종을 시작하기 전에는 기본 피치를 0으로 표시합니다.
        if (!showVerticalPitch ||
            velocity.sqrMagnitude < 0.0001f)
        {
            return 0f;
        }

        float planarSpeed = Vector3.ProjectOnPlane(
            velocity,
            Vector3.up
        ).magnitude;

        float angle = Mathf.Atan2(
            velocity.y,
            planarSpeed
        ) * Mathf.Rad2Deg;

        return Mathf.Clamp(
            angle,
            -visualMaxDivePitch,
            visualMaxClimbPitch
        );
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