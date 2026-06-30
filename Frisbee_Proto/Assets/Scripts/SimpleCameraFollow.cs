using UnityEngine;

public class SimpleCameraFollow : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Normal Follow Settings")]
    [SerializeField] private Vector3 normalOffset = new Vector3(0f, 6f, -8f);
    [SerializeField] private float followSpeed = 5f;
    [SerializeField] private float lookSpeed = 8f;

    [Header("Zoom Out Settings")]
    [SerializeField] private Vector3 zoomOutOffset = new Vector3(0f, 8f, -13f);
    [SerializeField] private float zoomOutDuration = 1.2f;
    [SerializeField] private float offsetLerpSpeed = 4f;

    [Header("FOV Settings")]
    [SerializeField] private bool useFovZoom = true;
    [SerializeField] private float normalFov = 60f;
    [SerializeField] private float zoomOutFov = 75f;
    [SerializeField] private float fovLerpSpeed = 4f;

    [Header("Look Settings")]
    [SerializeField] private float lookHeightOffset = 0.5f;

    private Camera cam;

    private Vector3 currentOffset;
    private Vector3 targetOffset;

    private float targetFov;
    private float zoomTimer;
    private bool isZoomOutActive = false;

    private void Awake()
    {
        cam = GetComponent<Camera>();

        currentOffset = normalOffset;
        targetOffset = normalOffset;

        targetFov = normalFov;

        if (cam != null)
        {
            cam.fieldOfView = normalFov;
        }
    }

    private void Update()
    {
        HandleZoomInput();
        UpdateZoomState();
        UpdateOffset();
        UpdateFov();
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        FollowTarget();
        LookAtTarget();
    }

    private void HandleZoomInput()
    {
        if (Input.GetKeyDown(KeyCode.I))
        {
            StartZoomOut();
        }
    }

    private void StartZoomOut()
    {
        isZoomOutActive = true;
        zoomTimer = zoomOutDuration;

        targetOffset = zoomOutOffset;
        targetFov = zoomOutFov;

        Debug.Log("카메라 줌아웃 시작");
    }

    private void UpdateZoomState()
    {
        if (isZoomOutActive == false)
        {
            return;
        }

        zoomTimer -= Time.deltaTime;

        if (zoomTimer <= 0f)
        {
            isZoomOutActive = false;

            targetOffset = normalOffset;
            targetFov = normalFov;

            Debug.Log("카메라 원위치 복귀");
        }
    }

    private void UpdateOffset()
    {
        currentOffset = Vector3.Lerp(
            currentOffset,
            targetOffset,
            offsetLerpSpeed * Time.deltaTime
        );
    }

    private void UpdateFov()
    {
        if (useFovZoom == false || cam == null)
        {
            return;
        }

        cam.fieldOfView = Mathf.Lerp(
            cam.fieldOfView,
            targetFov,
            fovLerpSpeed * Time.deltaTime
        );
    }

    private void FollowTarget()
    {
        Vector3 targetPosition = target.position + currentOffset;

        transform.position = Vector3.Lerp(
            transform.position,
            targetPosition,
            followSpeed * Time.deltaTime
        );
    }

    private void LookAtTarget()
    {
        Vector3 lookPosition = target.position;
        lookPosition.y += lookHeightOffset;

        Vector3 direction = lookPosition - transform.position;

        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction);

        transform.rotation = Quaternion.Lerp(
            transform.rotation,
            targetRotation,
            lookSpeed * Time.deltaTime
        );
    }
}