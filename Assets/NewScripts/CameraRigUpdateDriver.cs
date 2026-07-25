using UnityEngine;
using Unity.Cinemachine;

[DefaultExecutionOrder(20000)]
public class CameraRigUpdateDriver : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CinemachineBrain brain;
    [SerializeField] private DiscCameraTargetFollower followTargetFollower;

    [Header("Manual Update")]
    [Tooltip("Cinemachine Brain의 Update Method를 Manual Update로 설정했을 때 true로 둡니다.")]
    [SerializeField] private bool manualUpdateCinemachine = true;

    [Tooltip("Cinemachine을 갱신하기 직전에 Follow Target을 먼저 갱신합니다.")]
    [SerializeField] private bool updateFollowTargetBeforeBrain = true;
    [SerializeField] private DiscSpeedCameraEffects speedCameraEffects;

    private void Awake()
    {
        if (brain == null)
            brain = GetComponent<CinemachineBrain>();
    }

    private void LateUpdate()
    {
        // 1. DiscCameraTarget의 위치와 회전부터 갱신
        if (updateFollowTargetBeforeBrain &&
            followTargetFollower != null)
        {
            followTargetFollower.ManualUpdateTarget(
                Time.deltaTime,
                snapIfFar: true
            );
        }

        // 2. 속도 기반 FOV와 Follow Offset 갱신
        if (speedCameraEffects != null)
        {
            speedCameraEffects.ManualUpdateEffect(
                Time.deltaTime
            );
        }

        // 3. 마지막으로 Cinemachine이 카메라 위치를 계산
        if (manualUpdateCinemachine && brain != null)
        {
            brain.ManualUpdate();
        }
    }
}
