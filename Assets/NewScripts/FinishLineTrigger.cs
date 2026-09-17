using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public sealed class FinishLineTrigger : MonoBehaviour
{
    private bool consumed;

    private void Awake()
    {
        ConfigureCollider();
    }

    private void OnEnable()
    {
        consumed = false;
    }

    private void Reset()
    {
        ConfigureCollider();
    }

    private void ConfigureCollider()
    {
        BoxCollider triggerCollider = GetComponent<BoxCollider>();
        triggerCollider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (consumed || !isActiveAndEnabled)
            return;

        Rigidbody body = other.attachedRigidbody;

        if (body == null)
            return;

        DiscSlingshotController disc =
            body.GetComponent<DiscSlingshotController>();

        if (disc == null)
        {
            disc =
                body.GetComponentInParent<DiscSlingshotController>();
        }

        if (disc == null)
            return;

        DiscRunManager runManager = disc.RunManager;

        if (runManager == null)
        {
            Debug.LogError(
                "FinishLineTrigger: 원반에 DiscRunManager가 연결되어 있지 않습니다.",
                this
            );

            return;
        }

        // 정상적인 결승선 통과로 처리된 경우 중복 실행을 막습니다.
        consumed = runManager.HandleFinishLineCrossed(disc);
    }
}