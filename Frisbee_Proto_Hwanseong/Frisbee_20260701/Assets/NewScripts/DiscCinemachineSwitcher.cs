using UnityEngine;
using Unity.Cinemachine;

public class DiscCinemachineSwitcher : MonoBehaviour
{
    [Header("Cinemachine Cameras")]
    [SerializeField] private CinemachineCamera launchCamera;
    [SerializeField] private CinemachineCamera followCamera;

    [Header("Targets")]
    [SerializeField] private Transform followTarget;
    [SerializeField] private Transform launchAnchor;

    [Header("Launch Camera")]
    [Tooltip("처음 정상적인 Launch Camera 구도를 저장한 뒤, LaunchAnchor가 이동한 만큼 카메라도 평행이동합니다.")]
    [SerializeField] private bool preserveInitialLaunchCameraPose = true;

    [Tooltip("재투척 카메라로 돌아갈 때 Follow Camera의 타겟을 비웁니다.")]
    [SerializeField] private bool clearFollowTargetBeforeLaunch = true;

    [Header("Priorities")]
    [SerializeField] private int launchCameraPriority = 20;
    [SerializeField] private int followCameraBeforeLaunchPriority = 0;
    [SerializeField] private int followCameraAfterLaunchPriority = 30;

    private Vector3 originalLaunchAnchorPosition;
    private Vector3 originalLaunchCameraPosition;
    private Quaternion originalLaunchCameraRotation;
    private bool hasOriginalLaunchPose;

    private void Awake()
    {
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
        if (!hasOriginalLaunchPose)
            CaptureOriginalLaunchPose();

        if (preserveInitialLaunchCameraPose)
            RepositionLaunchCameraByAnchorDelta();

        if (launchCamera != null)
        {
            // Launch Camera는 고정 조준 카메라로 사용합니다.
            // 타겟을 비워두면 카메라가 이상한 방향으로 다시 회전하지 않습니다.
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
    }

    public void ShowLaunchCameraAt(Transform currentLaunchAnchor)
    {
        if (currentLaunchAnchor != null)
            launchAnchor = currentLaunchAnchor;

        ShowLaunchCamera();
    }

    public void BeginFollow()
    {
        if (followCamera == null || followTarget == null)
        {
            Debug.LogWarning("BeginFollow 실패: Follow Camera 또는 Follow Target이 비어 있습니다.");
            return;
        }

        followCamera.Target = new CameraTarget
        {
            TrackingTarget = followTarget,
            LookAtTarget = null,
            CustomLookAtTarget = false
        };

        followCamera.Priority = followCameraAfterLaunchPriority;
        followCamera.Prioritize();
    }

    private void RepositionLaunchCameraByAnchorDelta()
    {
        if (launchCamera == null || launchAnchor == null || !hasOriginalLaunchPose)
            return;

        Vector3 anchorDelta =
            launchAnchor.position - originalLaunchAnchorPosition;

        launchCamera.transform.position =
            originalLaunchCameraPosition + anchorDelta;

        // 회전은 처음 정상 구도를 그대로 유지합니다.
        // 여기서 TrackRoot.forward로 다시 회전시키면 180도 뒤집히는 문제가 생길 수 있습니다.
        launchCamera.transform.rotation =
            originalLaunchCameraRotation;
    }

    public void RecaptureCurrentLaunchPose()
    {
        CaptureOriginalLaunchPose();
    }
}