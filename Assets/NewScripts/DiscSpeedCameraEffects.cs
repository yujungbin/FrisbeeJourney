using UnityEngine;
using Unity.Cinemachine;

[DisallowMultipleComponent]
public sealed class DiscSpeedCameraEffects : MonoBehaviour
{
    public enum UpdateMode
    {
        LateUpdate,
        Manual
    }

    [Header("References")]
    [Tooltip("CM_DiscFollow의 CinemachineCamera 컴포넌트입니다.")]
    [SerializeField] private CinemachineCamera followCamera;

    [Tooltip("CM_DiscFollow의 CinemachineFollow 컴포넌트입니다.")]
    [SerializeField] private CinemachineFollow followComponent;

    [Tooltip("Disc 루트의 Rigidbody입니다.")]
    [SerializeField] private Rigidbody discRigidbody;

    [Header("Update")]
    [Tooltip(
        "CameraRigUpdateDriver를 사용하면 Manual, " +
        "일반 Smart Update 구조라면 LateUpdate를 사용합니다."
    )]
    [SerializeField] private UpdateMode updateMode = UpdateMode.Manual;

    [Header("Speed Measurement")]
    [Tooltip(
        "켜면 수평 속도만 사용합니다. " +
        "낙하나 튕김으로 인한 수직 속도가 FOV에 영향을 주지 않게 합니다."
    )]
    [SerializeField] private bool useHorizontalSpeed = true;

    [Tooltip("이 속도 이하에서는 기본 FOV와 기본 거리를 사용합니다.")]
    [SerializeField] private float effectStartSpeed = 2f;

    [Tooltip("이 속도 이상에서는 최대 FOV와 최대 카메라 거리를 사용합니다.")]
    [SerializeField] private float fullEffectSpeed = 20f;

    [Tooltip(
        "속도를 카메라 효과 강도 0~1로 변환하는 곡선입니다. " +
        "기본 EaseInOut은 저속과 고속 구간의 변화를 부드럽게 합니다."
    )]
    [SerializeField]
    private AnimationCurve speedResponseCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Field Of View")]
    [Tooltip("최고 속도에서 기본 FOV에 추가되는 값입니다.")]
    [SerializeField] private float additionalFovAtMaxSpeed = 12f;

    [Tooltip("최종 FOV의 최소 제한입니다.")]
    [SerializeField] private float minimumFov = 20f;

    [Tooltip("최종 FOV의 최대 제한입니다.")]
    [SerializeField] private float maximumFov = 90f;

    [Header("Follow Distance")]
    [Tooltip(
        "최고 속도에서 카메라가 기본 Follow Offset보다 " +
        "추가로 멀어지는 거리입니다."
    )]
    [SerializeField] private float additionalDistanceAtMaxSpeed = 2.5f;

    [Header("Response")]
    [Tooltip(
        "가속할 때 카메라가 멀어지고 FOV가 넓어지는 반응 속도입니다. " +
        "값이 클수록 빠르게 반응합니다."
    )]
    [SerializeField] private float zoomOutSharpness = 8f;

    [Tooltip(
        "감속할 때 카메라가 가까워지고 FOV가 좁아지는 반응 속도입니다. " +
        "값이 클수록 빠르게 원래 구도로 돌아옵니다."
    )]
    [SerializeField] private float zoomInSharpness = 5f;

    [Header("Debug")]
    [SerializeField] private bool logCurrentValues = false;
    [SerializeField] private float logInterval = 0.5f;

    private float baseFieldOfView;
    private Vector3 baseFollowOffset;

    private float currentEffect01;
    private float nextLogTime;
    private bool initialized;

    private void Reset()
    {
        followCamera = GetComponent<CinemachineCamera>();
        followComponent = GetComponent<CinemachineFollow>();
    }

    private void Awake()
    {
        ResolveReferences();
        CaptureBaseSettings();
    }

    private void OnValidate()
    {
        effectStartSpeed = Mathf.Max(0f, effectStartSpeed);

        fullEffectSpeed = Mathf.Max(
            effectStartSpeed + 0.01f,
            fullEffectSpeed
        );

        additionalFovAtMaxSpeed =
            Mathf.Max(0f, additionalFovAtMaxSpeed);

        minimumFov = Mathf.Clamp(minimumFov, 1f, 179f);
        maximumFov = Mathf.Clamp(maximumFov, minimumFov, 179f);

        additionalDistanceAtMaxSpeed =
            Mathf.Max(0f, additionalDistanceAtMaxSpeed);

        zoomOutSharpness = Mathf.Max(0f, zoomOutSharpness);
        zoomInSharpness = Mathf.Max(0f, zoomInSharpness);

        logInterval = Mathf.Max(0.05f, logInterval);
    }

    private void LateUpdate()
    {
        if (updateMode != UpdateMode.LateUpdate)
            return;

        UpdateCameraEffect(Time.deltaTime);
    }

    public void ManualUpdateEffect(float deltaTime)
    {
        if (updateMode != UpdateMode.Manual)
            return;

        UpdateCameraEffect(deltaTime);
    }

    /// <summary>
    /// 현재 Follow Offset과 FOV를 저속 기준값으로 다시 저장합니다.
    /// Inspector에서 기본 카메라 구도를 바꾼 뒤 호출할 수 있습니다.
    /// </summary>
    public void CaptureBaseSettings()
    {
        ResolveReferences();

        if (followCamera == null || followComponent == null)
        {
            Debug.LogError(
                "DiscSpeedCameraEffects: " +
                "Follow Camera 또는 Cinemachine Follow가 연결되지 않았습니다."
            );

            initialized = false;
            return;
        }

        baseFieldOfView =
            followCamera.Lens.FieldOfView;

        baseFollowOffset =
            followComponent.FollowOffset;

        currentEffect01 = 0f;
        initialized = true;

        ApplyEffect(0f);
    }

    /// <summary>
    /// FOV와 Follow Offset을 즉시 기본값으로 되돌립니다.
    /// 게임 리셋이나 새 판 시작 시 사용할 수 있습니다.
    /// </summary>
    public void ResetImmediately()
    {
        if (!initialized)
            CaptureBaseSettings();

        currentEffect01 = 0f;
        ApplyEffect(0f);
    }

    private void ResolveReferences()
    {
        if (followCamera == null)
            followCamera = GetComponent<CinemachineCamera>();

        if (followComponent == null)
            followComponent = GetComponent<CinemachineFollow>();
    }

    private void UpdateCameraEffect(float deltaTime)
    {
        if (!initialized)
        {
            CaptureBaseSettings();

            if (!initialized)
                return;
        }

        if (discRigidbody == null)
            return;

        float speed = GetDiscSpeed();

        float rawEffect01 = Mathf.InverseLerp(
            effectStartSpeed,
            fullEffectSpeed,
            speed
        );

        float targetEffect01 = Mathf.Clamp01(
            speedResponseCurve.Evaluate(rawEffect01)
        );

        // 가속할 때와 감속할 때 서로 다른 반응 속도를 사용합니다.
        float sharpness =
            targetEffect01 > currentEffect01
                ? zoomOutSharpness
                : zoomInSharpness;

        if (sharpness <= 0f)
        {
            currentEffect01 = targetEffect01;
        }
        else
        {
            float t = 1f - Mathf.Exp(
                -sharpness * Mathf.Max(0f, deltaTime)
            );

            currentEffect01 = Mathf.Lerp(
                currentEffect01,
                targetEffect01,
                t
            );
        }

        ApplyEffect(currentEffect01);

        if (logCurrentValues && Time.time >= nextLogTime)
        {
            nextLogTime = Time.time + logInterval;

            Debug.Log(
                $"Speed Camera Effect | " +
                $"speed: {speed:F2}, " +
                $"effect: {currentEffect01:F2}, " +
                $"FOV: {followCamera.Lens.FieldOfView:F1}, " +
                $"offset: {followComponent.FollowOffset}"
            );
        }
    }

    private float GetDiscSpeed()
    {
        Vector3 velocity = GetDiscVelocity();

        if (useHorizontalSpeed)
        {
            velocity = Vector3.ProjectOnPlane(
                velocity,
                Vector3.up
            );
        }

        return velocity.magnitude;
    }

    private Vector3 GetDiscVelocity()
    {
#if UNITY_6000_0_OR_NEWER
        return discRigidbody.linearVelocity;
#else
        return discRigidbody.velocity;
#endif
    }

    private void ApplyEffect(float effect01)
    {
        effect01 = Mathf.Clamp01(effect01);

        ApplyFieldOfView(effect01);
        ApplyFollowDistance(effect01);
    }

    private void ApplyFieldOfView(float effect01)
    {
        LensSettings lens = followCamera.Lens;

        lens.FieldOfView = Mathf.Clamp(
            baseFieldOfView +
            additionalFovAtMaxSpeed * effect01,
            minimumFov,
            maximumFov
        );

        followCamera.Lens = lens;
    }

    private void ApplyFollowDistance(float effect01)
    {
        Vector3 farOffset = baseFollowOffset;

        /*
         * 일반적인 Follow Offset은 Z가 음수입니다.
         * 예: (0, 2.5, -6)
         *
         * 기본 Z가 음수이면 더 멀어질수록 더 음수가 되고,
         * 기본 Z가 양수이면 더 멀어질수록 더 양수가 됩니다.
         */
        float zDirection =
            Mathf.Abs(baseFollowOffset.z) > 0.001f
                ? Mathf.Sign(baseFollowOffset.z)
                : -1f;

        farOffset.z =
            baseFollowOffset.z +
            zDirection * additionalDistanceAtMaxSpeed;

        followComponent.FollowOffset = Vector3.Lerp(
            baseFollowOffset,
            farOffset,
            effect01
        );
    }
}