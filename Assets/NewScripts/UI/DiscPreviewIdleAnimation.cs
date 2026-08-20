using UnityEngine;

[DisallowMultipleComponent]
public sealed class DiscPreviewIdleAnimation : MonoBehaviour
{
    [Header("자동 회전")]
    [SerializeField]
    private Vector3 rotationAxis = Vector3.up;

    [SerializeField]
    private float idleRotationSpeed = 30f;

    [Header("둥실거림")]
    [SerializeField]
    private float floatingHeight = 0.05f;

    [SerializeField]
    private float floatingSpeed = 1.2f;

    [Header("드래그 회전")]
    [SerializeField]
    private float dragRotationSensitivity = 0.5f;

    [SerializeField]
    private bool invertDragDirection = false;

    [Header("회전 관성")]
    [SerializeField]
    private float inertiaDamping = 4f;

    [SerializeField]
    private float maxInertiaSpeed = 720f;

    private Vector3 startLocalPosition;

    private bool isDragging;
    private float inertiaSpeed;

    private void Awake()
    {
        startLocalPosition = transform.localPosition;
    }

    private void Update()
    {
        UpdateFloating();
        UpdateRotation();
    }

    public void BeginDrag()
    {
        isDragging = true;
        inertiaSpeed = 0f;
    }

    public void Drag(float horizontalDelta)
    {
        if (!isDragging)
        {
            return;
        }

        float direction = invertDragDirection ? 1f : -1f;

        float rotationAmount =
            horizontalDelta *
            dragRotationSensitivity *
            direction;

        transform.Rotate(
            Vector3.up,
            rotationAmount,
            Space.World
        );

        float deltaTime = Mathf.Max(
            Time.unscaledDeltaTime,
            0.0001f
        );

        inertiaSpeed = Mathf.Clamp(
            rotationAmount / deltaTime,
            -maxInertiaSpeed,
            maxInertiaSpeed
        );
    }

    public void EndDrag()
    {
        isDragging = false;
    }

    private void UpdateRotation()
    {
        if (isDragging)
        {
            return;
        }

        if (Mathf.Abs(inertiaSpeed) > 0.1f)
        {
            transform.Rotate(
                Vector3.up,
                inertiaSpeed * Time.unscaledDeltaTime,
                Space.World
            );

            inertiaSpeed *= Mathf.Exp(
                -inertiaDamping *
                Time.unscaledDeltaTime
            );

            return;
        }

        inertiaSpeed = 0f;

        Vector3 axis =
            rotationAxis.sqrMagnitude > 0f
                ? rotationAxis.normalized
                : Vector3.up;

        transform.Rotate(
            axis,
            idleRotationSpeed * Time.unscaledDeltaTime,
            Space.Self
        );
    }

    private void UpdateFloating()
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