using UnityEngine;

public class DiscLaunchController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private StraightMapSpawner mapSpawner;
    [SerializeField] private Camera mainCamera;

    [Header("Throw Settings")]
    [SerializeField] private int maxThrowCount = 3;
    [SerializeField] private float minDragDistance = 80f;
    [SerializeField] private float minLaunchSpeed = 8f;
    [SerializeField] private float maxLaunchSpeed = 30f;
    [SerializeField] private float dragPowerMultiplier = 0.04f;

    [Header("Disc Visual Flight")]
    [SerializeField] private float takeOffLerpSpeed = 6f;
    [SerializeField] private float landingLerpSpeed = 4f;

    [Header("Disc Rotation")]
    [SerializeField] private float spinSpeed = 900f;
    [SerializeField] private float tiltAmount = 25f;
    [SerializeField] private float tiltLerpSpeed = 8f;

    [Header("Keyboard Move Test")]
    [SerializeField] private float keyboardMoveSpeed = 4f;
    [SerializeField] private float maxHorizontalOffset = 20f;

    [Header("Power Settings")]
    [SerializeField] private float maxDragDistance = 500f;
    [SerializeField] private float minFlightHeight = 0.8f;
    [SerializeField] private float maxFlightHeight = 2.5f;
    [SerializeField] private float minForwardOffset = 0.5f;
    [SerializeField] private float maxForwardOffset = 2.5f;

    private int currentThrowCount = 0;

    private bool isDragging = false;
    private bool isFlyingVisual = false;

    private Vector2 dragStartPosition;
    private Vector2 currentPointerPosition;

    private Vector3 startPosition;
    private Quaternion startRotation;

    private Vector3 targetVisualPosition;
    private Quaternion targetVisualRotation;

    private float spinAngle = 0f;

    private float currentHorizontalOffset = 200f;
    private float launchDirectionOffsetX = 0f;
    private float launchDirectionOffsetZ = 0f;

    private Vector2 finalDragVector;
    private float currentPower01 = 0f;

    private void Awake()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        startPosition = transform.position;
        startRotation = transform.rotation;

        targetVisualPosition = startPosition;
        targetVisualRotation = startRotation;
    }

    private void Update()
    {
        HandleInput();
        HandleKeyboardMove();
        UpdateDiscVisual();
    }

    private void HandleInput()
    {
        if (GetPointerDown(out Vector2 downPosition))
        {
            StartDrag(downPosition);
        }

        if (GetPointerHeld(out Vector2 heldPosition))
        {
            UpdateDrag(heldPosition);
        }

        if (GetPointerUp())
        {
            EndDrag();
        }
    }

    private void StartDrag(Vector2 screenPosition)
    {
        if (currentThrowCount >= maxThrowCount)
        {
            Debug.Log("더 이상 던질 수 없음");
            return;
        }

        isDragging = true;
        dragStartPosition = screenPosition;
        currentPointerPosition = screenPosition;
    }

    private void UpdateDrag(Vector2 screenPosition)
    {
        if (isDragging == false)
        {
            return;
        }

        currentPointerPosition = screenPosition;
        finalDragVector = currentPointerPosition - dragStartPosition;

        currentPower01 = Mathf.InverseLerp(
            minDragDistance,
            maxDragDistance,
            finalDragVector.magnitude
        );

        currentPower01 = Mathf.Clamp01(currentPower01);
    }

    private void EndDrag()
    {
        if (isDragging == false)
        {
            return;
        }

        isDragging = false;

        if (finalDragVector.y < minDragDistance)
        {
           
            return;
        }

        Launch(finalDragVector);
    }

    private void Launch(Vector2 dragVector)
    {
        if (mapSpawner == null)
        {
            Debug.LogError("Map Spawner가 연결되지 않음!");
            return;
        }

        currentThrowCount++;

        float dragPower = dragVector.magnitude * dragPowerMultiplier;

        float power01 = Mathf.InverseLerp(
         minDragDistance,
         maxDragDistance,
         dragVector.magnitude
        );

        power01 = Mathf.Clamp01(power01);

        float launchSpeed = Mathf.Lerp(
            minLaunchSpeed,
            maxLaunchSpeed,
            power01
        );

        mapSpawner.StartScrolling(launchSpeed);

        StartDiscFlightVisual(dragVector);

    }

    private void StartDiscFlightVisual(Vector2 dragVector)
    {
        isFlyingVisual = true;

        float power01 = Mathf.InverseLerp(
            minDragDistance,
            maxDragDistance,
            dragVector.magnitude
        );

        power01 = Mathf.Clamp01(power01);

        Vector2 dragDirection = dragVector.normalized;

        float horizontalDrag = Mathf.Clamp(dragDirection.x, -1f, 1f);
        float forwardDrag = Mathf.Clamp01(dragDirection.y);

        float currentFlightHeight = Mathf.Lerp(
            minFlightHeight,
            maxFlightHeight,
            power01
        );

        float currentForwardOffset = Mathf.Lerp(
            minForwardOffset,
            maxForwardOffset,
            power01
        );

        launchDirectionOffsetX = horizontalDrag * 5f;
        launchDirectionOffsetZ = forwardDrag * currentForwardOffset;

        targetVisualPosition = startPosition;
        targetVisualPosition.x += launchDirectionOffsetX;
        targetVisualPosition.y += currentFlightHeight;
        targetVisualPosition.z += launchDirectionOffsetZ;

        targetVisualRotation = startRotation * Quaternion.Euler(
            0f,
            0f,
            -horizontalDrag * tiltAmount
        );

    }

    private void UpdateDiscVisual()
    {
        if (mapSpawner != null && mapSpawner.IsScrolling)
        {
            isFlyingVisual = true;
        }
        else
        {
            isFlyingVisual = false;
        }

        if (isFlyingVisual)
        {
            Vector3 finalTargetPosition = targetVisualPosition;
            finalTargetPosition.x += currentHorizontalOffset;

            transform.position = Vector3.Lerp(
                transform.position,
                finalTargetPosition,
                takeOffLerpSpeed * Time.deltaTime
                        );

            spinAngle += spinSpeed * Time.deltaTime;

            Quaternion spinRotation = Quaternion.Euler(
                0f,
                spinAngle,
                0f
            );

            Quaternion finalRotation = targetVisualRotation * spinRotation;

            transform.rotation = Quaternion.Lerp(
                transform.rotation,
                finalRotation,
                tiltLerpSpeed * Time.deltaTime
            );
        }
        else
        {
            currentHorizontalOffset = Mathf.Lerp(
            currentHorizontalOffset,
            0f,
            landingLerpSpeed * Time.deltaTime
            );

            transform.position = Vector3.Lerp(
                transform.position,
                startPosition,
                landingLerpSpeed * Time.deltaTime
            );

            transform.rotation = Quaternion.Lerp(
                transform.rotation,
                startRotation,
                landingLerpSpeed * Time.deltaTime
            );


        }
    }

    private bool GetPointerDown(out Vector2 position)
    {
        position = Vector2.zero;

        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Began)
            {
                position = touch.position;
                return true;
            }
        }

        if (Input.GetMouseButtonDown(0))
        {
            position = Input.mousePosition;
            return true;
        }

        return false;
    }

    private bool GetPointerHeld(out Vector2 position)
    {
        position = Vector2.zero;

        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
            {
                position = touch.position;
                return true;
            }
        }

        if (Input.GetMouseButton(0))
        {
            position = Input.mousePosition;
            return true;
        }

        return false;
    }

    private bool GetPointerUp()
    {
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                return true;
            }
        }

        if (Input.GetMouseButtonUp(0))
        {
            return true;
        }

        return false;
    }
    private void HandleKeyboardMove()
    {
        if (isFlyingVisual == false)
        {
            return;
        }

        float inputX = 0f;

        if (Input.GetKey(KeyCode.LeftArrow))
        {
            inputX = -1f;
        }
        else if (Input.GetKey(KeyCode.RightArrow))
        {
            inputX = 1f;
        }

        currentHorizontalOffset += inputX * keyboardMoveSpeed * Time.deltaTime;
        currentHorizontalOffset = Mathf.Clamp(
            currentHorizontalOffset,
            -maxHorizontalOffset,
            maxHorizontalOffset
        );
    }
}

