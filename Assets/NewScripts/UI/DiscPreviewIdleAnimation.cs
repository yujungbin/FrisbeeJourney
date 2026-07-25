using UnityEngine;

[DisallowMultipleComponent]
public sealed class DiscPreviewIdleAnimation : MonoBehaviour
{
    [Header("Rotation")]
    [SerializeField]
    private Vector3 rotationAxis =
        Vector3.up;

    [SerializeField]
    private float rotationSpeed =
        40f;

    [Header("Floating")]
    [SerializeField]
    private float floatingHeight =
        0.5f;

    [SerializeField]
    private float floatingSpeed =
        0.5f;

    private Vector3 startLocalPosition;

    private void Awake()
    {
        startLocalPosition =
            transform.localPosition;
    }

    private void Update()
    {
        RotatePreview();
        FloatPreview();
    }

    private void RotatePreview()
    {
        Vector3 axis =
            rotationAxis.sqrMagnitude > 0f
                ? rotationAxis.normalized
                : Vector3.up;

        transform.Rotate(
            axis,
            rotationSpeed * Time.unscaledDeltaTime,
            Space.Self
        );
    }

    private void FloatPreview()
    {
        float offset =
            Mathf.Sin(
                Time.unscaledTime *
                floatingSpeed
            ) *
            floatingHeight;

        transform.localPosition =
            startLocalPosition +
            Vector3.up * offset;
    }
}