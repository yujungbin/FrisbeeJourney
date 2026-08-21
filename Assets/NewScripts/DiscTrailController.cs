using UnityEngine;

[DisallowMultipleComponent]
public class DiscTrailController : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private DiscSlingshotController discController;

    [SerializeField]
    private Rigidbody discRigidbody;

    [Tooltip("원반 양쪽의 Trail Renderer를 연결하세요.")]
    [SerializeField]
    private TrailRenderer[] trailRenderers;


    [Header("State Control")]
    [Tooltip("첫 충돌 이후 Settling 상태에서도 Trail을 표시합니다.")]
    [SerializeField]
    private bool emitWhileSettling = true;

    [Tooltip("원반이 정지하거나 Ready 상태로 돌아가면 이전 Trail을 즉시 삭제합니다.")]
    [SerializeField]
    private bool clearWhenThrowEnds = true;

    [Tooltip("컴포넌트가 비활성화될 때 Trail을 삭제합니다.")]
    [SerializeField]
    private bool clearOnDisable = true;


    [Header("Speed Thresholds")]
    [Tooltip("이 속도 미만에서는 Trail을 생성하지 않습니다.")]
    [SerializeField]
    private float minSpeed = 2f;

    [Tooltip("이 속도 이상에서는 최대 Trail Time을 사용합니다.")]
    [SerializeField]
    private float maxSpeed = 25f;

    [Tooltip("낙하 속도를 제외하고 수평 속도만 사용할지 여부입니다.")]
    [SerializeField]
    private bool useHorizontalSpeed = false;


    [Header("Trail Length")]
    [Tooltip("Min Speed 부근에서의 Trail 잔상 시간입니다.")]
    [SerializeField]
    private float minTrailTime = 0.05f;

    [Tooltip("Max Speed 이상에서의 Trail 잔상 시간입니다.")]
    [SerializeField]
    private float maxTrailTime = 0.35f;

    [Tooltip("Trail Time이 목표값으로 변하는 속도입니다. 0이면 즉시 변경됩니다.")]
    [SerializeField]
    private float trailTimeResponse = 12f;

    [Tooltip("속도에 따른 Trail 길이 변화 곡선입니다.")]
    [SerializeField]
    private AnimationCurve speedToTrailCurve =
        AnimationCurve.Linear(0f, 0f, 1f, 1f);


    [Header("Low Speed")]
    [Tooltip(
        "속도가 Min Speed 아래로 떨어질 때 기존 잔상까지 즉시 지웁니다. " +
        "끄면 기존 잔상은 Trail Time에 따라 자연스럽게 사라집니다."
    )]
    [SerializeField]
    private bool clearWhenBelowMinSpeed = false;


    [Header("Debug")]
    [SerializeField]
    private bool logStateChanges = false;

    private DiscSlingshotController subscribedController;

    private bool hasActuallyLaunched;
    private bool isEmitting;

    private float currentSpeed;
    private float currentTrailTime;

    private Vector3 lastPosition;


    public float CurrentSpeed => currentSpeed;
    public float CurrentTrailTime => currentTrailTime;
    public bool IsEmitting => isEmitting;


    private void Awake()
    {
        ResolveReferences();

        lastPosition = transform.position;
        currentTrailTime = minTrailTime;

        SetEmission(false, true);
        SetAllTrailTimes(minTrailTime);
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeToDiscController();

        hasActuallyLaunched = false;
        lastPosition = transform.position;

        SetEmission(false, true);
    }

    private void OnDisable()
    {
        UnsubscribeFromDiscController();

        hasActuallyLaunched = false;

        SetEmission(
            false,
            clearOnDisable
        );
    }

    private void OnValidate()
    {
        minSpeed = Mathf.Max(0f, minSpeed);

        maxSpeed = Mathf.Max(
            minSpeed + 0.01f,
            maxSpeed
        );

        minTrailTime = Mathf.Max(0f, minTrailTime);

        maxTrailTime = Mathf.Max(
            minTrailTime,
            maxTrailTime
        );

        trailTimeResponse = Mathf.Max(
            0f,
            trailTimeResponse
        );
    }

    private void Update()
    {
        UpdateCurrentSpeed();

        bool validThrowState = IsValidTrailState();

        // 실제 발사 이벤트가 발생하지 않았거나
        // Flying/Settling 상태가 아니라면 Trail을 끕니다.
        if (!hasActuallyLaunched || !validThrowState)
        {
            if (isEmitting)
            {
                SetEmission(
                    false,
                    clearWhenThrowEnds
                );
            }

            return;
        }

        UpdateTrailFromSpeed();
    }


    // --------------------------------------------------
    // Launch event
    // --------------------------------------------------

    private void HandleDiscLaunched()
    {
        hasActuallyLaunched = true;

        // Ready/Dragging 중 만들어졌을 수 있는 흔적을 삭제합니다.
        SetEmission(false, true);

        currentTrailTime = minTrailTime;
        SetAllTrailTimes(currentTrailTime);

        lastPosition = transform.position;

        if (logStateChanges)
        {
            Debug.Log(
                "DiscTrailController: 실제 발사 완료. Trail 제어 시작."
            );
        }
    }


    // --------------------------------------------------
    // Speed control
    // --------------------------------------------------

    private void UpdateTrailFromSpeed()
    {
        if (currentSpeed < minSpeed)
        {
            if (isEmitting)
            {
                SetEmission(
                    false,
                    clearWhenBelowMinSpeed
                );
            }

            return;
        }

        float speedRatio = Mathf.InverseLerp(
            minSpeed,
            maxSpeed,
            currentSpeed
        );

        float curvedRatio = Mathf.Clamp01(
            speedToTrailCurve.Evaluate(speedRatio)
        );

        float targetTrailTime = Mathf.Lerp(
            minTrailTime,
            maxTrailTime,
            curvedRatio
        );

        if (trailTimeResponse <= 0f)
        {
            currentTrailTime = targetTrailTime;
        }
        else
        {
            float t = 1f - Mathf.Exp(
                -trailTimeResponse * Time.deltaTime
            );

            currentTrailTime = Mathf.Lerp(
                currentTrailTime,
                targetTrailTime,
                t
            );
        }

        SetAllTrailTimes(currentTrailTime);

        if (!isEmitting)
        {
            // Trail을 다시 켤 때 이전 위치와 연결되는 긴 선을 방지합니다.
            SetEmission(true, true);
        }
    }

    private void UpdateCurrentSpeed()
    {
        Vector3 velocity;

        if (discRigidbody != null &&
            !discRigidbody.isKinematic)
        {
#if UNITY_6000_0_OR_NEWER
            velocity = discRigidbody.linearVelocity;
#else
            velocity = discRigidbody.velocity;
#endif
        }
        else
        {
            float deltaTime = Mathf.Max(
                Time.deltaTime,
                0.0001f
            );

            velocity =
                (transform.position - lastPosition) /
                deltaTime;
        }

        lastPosition = transform.position;

        if (useHorizontalSpeed)
        {
            velocity = Vector3.ProjectOnPlane(
                velocity,
                Vector3.up
            );
        }

        currentSpeed = velocity.magnitude;
    }


    // --------------------------------------------------
    // State
    // --------------------------------------------------

    private bool IsValidTrailState()
    {
        if (discController == null)
            return false;

        if (discController.IsFlying)
            return true;

        if (emitWhileSettling &&
            discController.IsSettling)
        {
            return true;
        }

        return false;
    }


    // --------------------------------------------------
    // Trail operations
    // --------------------------------------------------

    private void SetEmission(
        bool emit,
        bool clear)
    {
        isEmitting = emit;

        if (trailRenderers == null)
            return;

        for (int i = 0; i < trailRenderers.Length; i++)
        {
            TrailRenderer trail = trailRenderers[i];

            if (trail == null)
                continue;

            // Clear 전에 emission을 먼저 꺼야
            // 같은 프레임에 흔적이 다시 생성되지 않습니다.
            trail.emitting = false;

            if (clear)
                trail.Clear();

            trail.emitting = emit;
        }

        if (logStateChanges)
        {
            Debug.Log(
                emit
                    ? $"Trail ON | Speed: {currentSpeed:F2}, Time: {currentTrailTime:F2}"
                    : $"Trail OFF | Speed: {currentSpeed:F2}"
            );
        }
    }

    private void SetAllTrailTimes(float trailTime)
    {
        if (trailRenderers == null)
            return;

        for (int i = 0; i < trailRenderers.Length; i++)
        {
            TrailRenderer trail = trailRenderers[i];

            if (trail == null)
                continue;

            trail.time = trailTime;
        }
    }


    // --------------------------------------------------
    // References
    // --------------------------------------------------

    private void ResolveReferences()
    {
        if (discController == null)
        {
            discController =
                GetComponent<DiscSlingshotController>();
        }

        if (discRigidbody == null)
        {
            discRigidbody =
                GetComponent<Rigidbody>();
        }

        if (trailRenderers == null ||
            trailRenderers.Length == 0)
        {
            trailRenderers =
                GetComponentsInChildren<TrailRenderer>(true);
        }
    }

    private void SubscribeToDiscController()
    {
        if (discController == null)
            return;

        if (subscribedController == discController)
            return;

        UnsubscribeFromDiscController();

        discController.Launched += HandleDiscLaunched;
        subscribedController = discController;
    }

    private void UnsubscribeFromDiscController()
    {
        if (subscribedController == null)
            return;

        subscribedController.Launched -= HandleDiscLaunched;
        subscribedController = null;
    }


    // --------------------------------------------------
    // Public methods
    // --------------------------------------------------

    public void StopAndClearTrails()
    {
        hasActuallyLaunched = false;
        SetEmission(false, true);
    }

    public void ClearTrails()
    {
        if (trailRenderers == null)
            return;

        for (int i = 0; i < trailRenderers.Length; i++)
        {
            TrailRenderer trail = trailRenderers[i];

            if (trail != null)
                trail.Clear();
        }
    }
}