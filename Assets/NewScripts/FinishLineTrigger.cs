using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public sealed class FinishLineTrigger : MonoBehaviour
{
    private void Reset()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isActiveAndEnabled)
            return;

        Rigidbody body = other.attachedRigidbody;

        if (body == null)
            return;

        DiscSlingshotController disc =
            body.GetComponent<DiscSlingshotController>();

        if (disc == null || disc.RunManager == null)
            return;

        disc.RunManager.HandleFinishLineCrossed(disc);
    }
}