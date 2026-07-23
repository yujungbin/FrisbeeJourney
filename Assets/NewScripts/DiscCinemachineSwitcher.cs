using UnityEngine;
using Unity.Cinemachine;

public class DiscCinemachineSwitcher : MonoBehaviour
{
    [Header("Cinemachine Cameras")]
    [SerializeField] private CinemachineCamera launchCamera;
    [SerializeField] private CinemachineCamera followCamera;
    [SerializeField] private DiscSpeedCameraEffects speedCameraEffects;

    [Header("Targets")]
    [SerializeField] private Transform followTarget;
    [SerializeField] private Transform launchAnchor;
    [SerializeField] private DiscCameraTargetFollower followTargetFollower;

    [Header("Launch Camera")]
    [SerializeField] private bool preserveInitialLaunchCameraPose = true;
    [SerializeField] private bool clearFollowTargetBeforeLaunch = true;

    [Header("Priorities")]
    [SerializeField] private int launchCameraPriority = 20;
    [SerializeField] private int followCameraBeforeLaunchPriority = 0;
    [SerializeField] private int followCameraAfterLaunchPriority = 30;

    [Header("Debug")]
    [SerializeField] private bool logCameraSwitch = true;


    private Vector3 originalLaunchAnchorPosition;
    private Vector3 originalLaunchCameraPosition;
    private Quaternion originalLaunchCameraRotation;
    private bool hasOriginalLaunchPose;

    private void Awake()
    {
        if (followTargetFollower == null && followTarget != null)
            followTargetFollower = followTarget.GetComponent<DiscCameraTargetFollower>();

        CaptureOriginalLaunchPose();
    }

    private void Start()
    {
        ShowLaunchCamera();
    }

    private void CaptureOriginalLaunchPose()
    {
        if (launchCamera == null || launchAnchor == null)
            return;

        originalLaunchAnchorPosition = launchAnchor.position;
        originalLaunchCameraPosition = launchCamera.transform.position;
        originalLaunchCameraRotation = launchCamera.transform.rotation;

        hasOriginalLaunchPose = true;
    }

    public void ShowLaunchCamera()
    {
        if (speedCameraEffects != null)
            speedCameraEffects.ResetImmediately();

        if (!hasOriginalLaunchPose)
            CaptureOriginalLaunchPose();

        if (preserveInitialLaunchCameraPose)
            RepositionLaunchCameraByAnchorDelta();

        if (launchCamera != null)
        {
            launchCamera.Target = new CameraTarget
            {
                TrackingTarget = null,
                LookAtTarget = null,
                CustomLookAtTarget = false
            };

            launchCamera.Priority = launchCameraPriority;
            launchCamera.Prioritize();
        }

        if (followCamera != null)
        {
            followCamera.Priority = followCameraBeforeLaunchPriority;

            if (clearFollowTargetBeforeLaunch)
            {
                followCamera.Target = new CameraTarget
                {
                    TrackingTarget = null,
                    LookAtTarget = null,
                    CustomLookAtTarget = false
                };
            }
        }

        if (logCameraSwitch)
            Debug.Log("ShowLaunchCamera called.");
    }

    public void ShowLaunchCameraAt(Transform currentLaunchAnchor)
    {
        if (currentLaunchAnchor != null)
            launchAnchor = currentLaunchAnchor;

        ShowLaunchCamera();
    }

    public void BeginFollow()
    {
        BeginFollowImmediate();
    }

    public void BeginFollowImmediate()
    {
        if (followCamera == null)
        {
            Debug.LogWarning("BeginFollowImmediate failed: Follow Camera is null.");
            return;
        }

        if (followTarget == null)
        {
            Debug.LogWarning("BeginFollowImmediate failed: Follow Target is null.");
            return;
        }

        if (followTargetFollower == null)
            followTargetFollower = followTarget.GetComponent<DiscCameraTargetFollower>();

        // 카메라 전환 직전에 타겟을 최신 원반 위치로 강제 갱신.
        if (followTargetFollower != null)
            followTargetFollower.SnapToDisc();

        followCamera.Target = new CameraTarget
        {
            TrackingTarget = followTarget,
            LookAtTarget = null,
            CustomLookAtTarget = false
        };

        followCamera.Priority = followCameraAfterLaunchPriority;
        followCamera.Prioritize();

        if (logCameraSwitch)
        {
            Debug.Log(
                $"BeginFollowImmediate called. " +
                $"FollowCamera: {followCamera.name}, " +
                $"FollowTarget: {followTarget.name}, " +
                $"Priority: {followCamera.Priority}"
            );
        }
    }

    private void RepositionLaunchCameraByAnchorDelta()
    {
        if (launchCamera == null || launchAnchor == null || !hasOriginalLaunchPose)
            return;

        Vector3 anchorDelta =
            launchAnchor.position - originalLaunchAnchorPosition;

        launchCamera.transform.position =
            originalLaunchCameraPosition + anchorDelta;

        launchCamera.transform.rotation =
            originalLaunchCameraRotation;
    }

    public void RecaptureCurrentLaunchPose()
    {
        CaptureOriginalLaunchPose();
    }
}