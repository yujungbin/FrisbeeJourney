using UnityEngine;

[DisallowMultipleComponent]
public sealed class LaunchReadyPanController : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private DiscSlingshotController discController;

    [SerializeField]
    private DiscCinemachineSwitcher cameraSwitcher;

    [Tooltip("왼쪽/오른쪽 버튼을 담고 있는 부모 오브젝트입니다.")]
    [SerializeField]
    private GameObject controlsRoot;

    [Header("Panning")]
    [Tooltip("버튼을 누르고 있을 때 초당 회전 각도입니다.")]
    [SerializeField, Range(5f, 120f)]
    private float panDegreesPerSecond = 35f;

    [Tooltip("버튼을 한 번 눌렀을 때 회전할 각도입니다.")]
    [SerializeField, Range(1f, 30f)]
    private float tapStepDegrees = 7.5f;

    private int heldDirection;

    private bool CanPan =>
        discController != null &&
        cameraSwitcher != null &&
        discController.IsReady;

    private void Update()
    {
        bool canPan = CanPan;

        if (controlsRoot != null &&
            controlsRoot.activeSelf != canPan)
        {
            controlsRoot.SetActive(canPan);
        }

        if (!canPan)
        {
            heldDirection = 0;
            return;
        }

        if (heldDirection == 0)
            return;

        float nextAngle =
            cameraSwitcher.CurrentLaunchPanAngle +
            heldDirection *
            panDegreesPerSecond *
            Time.unscaledDeltaTime;

        ApplyPanAngle(nextAngle);
    }

    private void OnDisable()
    {
        heldDirection = 0;
    }

    public void BeginPanLeft()
    {
        if (CanPan)
            heldDirection = -1;
    }

    public void BeginPanRight()
    {
        if (CanPan)
            heldDirection = 1;
    }

    public void EndPan()
    {
        heldDirection = 0;
    }

    public void PanLeftOneStep()
    {
        if (!CanPan)
            return;

        ApplyPanAngle(
            cameraSwitcher.CurrentLaunchPanAngle -
            tapStepDegrees
        );
    }

    public void PanRightOneStep()
    {
        if (!CanPan)
            return;

        ApplyPanAngle(
            cameraSwitcher.CurrentLaunchPanAngle +
            tapStepDegrees
        );
    }

    public void ResetPan()
    {
        heldDirection = 0;

        if (cameraSwitcher != null)
            cameraSwitcher.ResetLaunchPan();

        if (discController != null &&
            discController.IsReady)
        {
            discController.SetLaunchAimYaw(0f);
        }
    }

    private void ApplyPanAngle(float requestedAngle)
    {
        cameraSwitcher.SetLaunchPanAngle(requestedAngle);

        // 카메라에서 제한된 최종 각도를 그대로 투척 방향에 적용
        discController.SetLaunchAimYaw(
            cameraSwitcher.CurrentLaunchPanAngle
        );
    }
}