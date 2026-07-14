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

    private void Awake()
    {
        if (brain == null)
            brain = GetComponent<CinemachineBrain>();
    }

    private void LateUpdate()
    {
        if (updateFollowTargetBeforeBrain && followTargetFollower != null)
        {
            followTargetFollower.ManualUpdateTarget(
                Time.deltaTime,
                snapIfFar: true
            );
        }

        if (manualUpdateCinemachine && brain != null)
        {
            brain.ManualUpdate();
        }
    }
}
