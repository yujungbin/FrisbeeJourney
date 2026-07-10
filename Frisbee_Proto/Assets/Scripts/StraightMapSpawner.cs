using System.Collections.Generic;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class StraightMapSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject roadTilePrefab;

    [Tooltip("DiscFollowTarget 또는 DiscCameraTarget을 넣으세요. 원반의 전방 진행 위치 기준으로 타일을 재사용합니다.")]
    [SerializeField] private Transform followTarget;

    [Tooltip("타일들을 이 부모 아래에 생성합니다. 비워두면 이 오브젝트 아래에 생성합니다.")]
    [SerializeField] private Transform tileParent;

    [Header("Pool Settings")]
    [SerializeField] private int poolSize = 5;

    [Tooltip("타겟 뒤쪽에 몇 개의 타일을 남겨둘지입니다.")]
    [SerializeField] private int tilesBehindTarget = 0;

    [Header("Tile Settings")]
    [SerializeField] private float tileLength = 250f;
    [SerializeField] private float startZ = 0f;

    [Tooltip("타일의 중심 X 위치입니다.")]
    [SerializeField] private float tileX = 0f;

    [Tooltip("타일의 Y 위치입니다.")]
    [SerializeField] private float tileY = 0f;

    [SerializeField] private Vector3 tileEulerRotation = Vector3.zero;

    [Header("Recycle Settings")]
    [Tooltip("타일 끝이 타겟보다 이 거리만큼 뒤로 가면 앞쪽으로 재사용합니다.")]
    [SerializeField] private float recycleBehindDistance = 120f;

    [Tooltip("타겟이 너무 멀리 순간이동하면 전체 타일 풀을 타겟 주변으로 재배치합니다.")]
    [SerializeField] private bool snapPoolWhenTargetJumps = true;

    [Tooltip("타겟이 현재 풀 범위를 이 타일 수만큼 벗어나면 전체 풀을 재배치합니다.")]
    [SerializeField] private float snapDistanceInTiles = 2.5f;

    [Header("Object Reset")]
    [SerializeField] private bool resetObjectsOnInitialSpawn = true;
    [SerializeField] private bool resetObjectsWhenRecycled = true;
    [SerializeField] private bool resetObjectsWhenSnapped = true;

    [Header("Generation Control")]
    [Tooltip("true면 StartScrolling이 호출된 뒤부터 생성/재사용을 시작합니다.")]
    [SerializeField] private bool generateOnlyAfterStartScrolling = false;

    [Header("Debug")]
    [SerializeField] private bool logRecycle = false;

    [Header("Test Input")]
    [SerializeField] private bool enableTestInput = false;

    private readonly List<GameObject> activeTiles = new List<GameObject>();

    private bool generationEnabled;
    private float lastTargetZ;
    private float targetForwardSpeed;

    public bool IsGenerationEnabled => generationEnabled;

    private void Start()
    {
        generationEnabled = !generateOnlyAfterStartScrolling;

        CreateTilePool();

        lastTargetZ = GetTargetZ();
        targetForwardSpeed = 0f;
    }

    private void Update()
    {
        HandleTestInput();

        if (followTarget == null)
            return;

        if (activeTiles.Count == 0)
            return;

        UpdateTargetSpeed();

        if (!generationEnabled)
            return;

        if (snapPoolWhenTargetJumps && IsTargetFarOutsidePool())
        {
            ResetTilesAroundTarget();
            return;
        }

        RecyclePassedTilesByTarget();
    }

    public void SetFollowTarget(Transform newFollowTarget)
    {
        followTarget = newFollowTarget;

        lastTargetZ = GetTargetZ();
        targetForwardSpeed = 0f;

        ResetTilesAroundTarget();
    }

    public void StartScrolling(float startSpeed)
    {
        // 기존 코드 호환용.
        // 이제 맵 자체를 startSpeed로 움직이지 않고,
        // followTarget 위치 기준으로 타일을 재사용합니다.
        generationEnabled = true;

        lastTargetZ = GetTargetZ();
        targetForwardSpeed = 0f;

        if (logRecycle)
            Debug.Log($"맵 생성 활성화. 입력 startSpeed는 더 이상 직접 사용하지 않음: {startSpeed}");
    }

    public void StopScrolling()
    {
        generationEnabled = false;
        targetForwardSpeed = 0f;
    }

    public void ResetTilesAroundTarget()
    {
        if (activeTiles.Count == 0)
            return;

        float firstZ = CalculateFirstTileZAroundTarget(GetTargetZ());

        for (int i = 0; i < activeTiles.Count; i++)
        {
            float z = firstZ + i * tileLength;

            PlaceTile(
                activeTiles[i],
                z,
                resetObjectsWhenSnapped
            );
        }

        lastTargetZ = GetTargetZ();
        targetForwardSpeed = 0f;

        if (logRecycle)
            Debug.Log("타일 풀을 FollowTarget 주변으로 재배치");
    }

    private void CreateTilePool()
    {
        if (roadTilePrefab == null)
        {
            Debug.LogError("Road Tile Prefab이 비어있음!");
            return;
        }

        activeTiles.Clear();

        if (tileParent == null)
            tileParent = transform;

        float firstZ = CalculateFirstTileZAroundTarget(GetTargetZ());

        for (int i = 0; i < poolSize; i++)
        {
            float spawnZ = firstZ + i * tileLength;

            Vector3 spawnPosition = new Vector3(
                tileX,
                tileY,
                spawnZ
            );

            Quaternion spawnRotation =
                Quaternion.Euler(tileEulerRotation);

            GameObject newTile = Instantiate(
                roadTilePrefab,
                spawnPosition,
                spawnRotation,
                tileParent
            );

            activeTiles.Add(newTile);

            if (resetObjectsOnInitialSpawn)
                ResetTileObjects(newTile);
        }
    }

    private void UpdateTargetSpeed()
    {
        float currentZ = GetTargetZ();
        float dt = Mathf.Max(0.0001f, Time.deltaTime);

        targetForwardSpeed = (currentZ - lastTargetZ) / dt;
        lastTargetZ = currentZ;
    }

    private void RecyclePassedTilesByTarget()
    {
        float targetZ = GetTargetZ();

        int safety = 0;
        int maxRecycleCount = Mathf.Max(10, poolSize * 4);

        while (activeTiles.Count > 0 &&
               IsFirstTileBehindTarget(targetZ) &&
               safety < maxRecycleCount)
        {
            RecycleFirstTileToFront();
            safety++;
        }

        if (safety >= maxRecycleCount)
        {
            Debug.LogWarning(
                "타일 재사용 루프가 너무 많이 실행되어 중단됨. " +
                "타겟이 크게 점프했다면 ResetTilesAroundTarget을 호출하세요."
            );
        }
    }

    private bool IsFirstTileBehindTarget(float targetZ)
    {
        GameObject firstTile = activeTiles[0];

        if (firstTile == null)
            return false;

        float firstTileEndZ =
            firstTile.transform.position.z +
            tileLength * 0.5f;

        return firstTileEndZ < targetZ - recycleBehindDistance;
    }

    private void RecycleFirstTileToFront()
    {
        GameObject firstTile = activeTiles[0];

        activeTiles.RemoveAt(0);

        GameObject lastTile = activeTiles[activeTiles.Count - 1];

        float newZ =
            lastTile.transform.position.z +
            tileLength;

        PlaceTile(
            firstTile,
            newZ,
            resetObjectsWhenRecycled
        );

        activeTiles.Add(firstTile);

        if (logRecycle)
            Debug.Log($"타일 재사용: {firstTile.name}, newZ: {newZ}");
    }

    private void PlaceTile(
        GameObject tile,
        float z,
        bool resetObjects)
    {
        if (tile == null)
            return;

        tile.transform.SetPositionAndRotation(
            new Vector3(tileX, tileY, z),
            Quaternion.Euler(tileEulerRotation)
        );

        if (resetObjects)
            ResetTileObjects(tile);
    }

    private void ResetTileObjects(GameObject tile)
    {
        if (tile == null)
            return;

        RoadTileObjectSpawner objectSpawner =
            tile.GetComponent<RoadTileObjectSpawner>();

        if (objectSpawner != null)
            objectSpawner.ResetTileObjects();
    }

    private float CalculateFirstTileZAroundTarget(float targetZ)
    {
        if (tileLength <= 0.0001f)
            return startZ;

        int targetTileIndex =
            Mathf.FloorToInt((targetZ - startZ) / tileLength);

        int safeTilesBehind =
            Mathf.Clamp(
                tilesBehindTarget,
                0,
                Mathf.Max(0, poolSize - 1)
            );

        int firstTileIndex =
            targetTileIndex - safeTilesBehind;

        return startZ + firstTileIndex * tileLength;
    }

    private bool IsTargetFarOutsidePool()
    {
        if (activeTiles.Count == 0)
            return false;

        float targetZ = GetTargetZ();

        GameObject firstTile = activeTiles[0];
        GameObject lastTile = activeTiles[activeTiles.Count - 1];

        if (firstTile == null || lastTile == null)
            return false;

        float firstStartZ =
            firstTile.transform.position.z -
            tileLength * 0.5f;

        float lastEndZ =
            lastTile.transform.position.z +
            tileLength * 0.5f;

        float margin =
            tileLength * Mathf.Max(1f, snapDistanceInTiles);

        bool tooFarBehind =
            targetZ < firstStartZ - margin;

        bool tooFarAhead =
            targetZ > lastEndZ + margin;

        return tooFarBehind || tooFarAhead;
    }

    private float GetTargetZ()
    {
        if (followTarget == null)
            return startZ;

        return followTarget.position.z;
    }

    private void HandleTestInput()
    {
        if (!enableTestInput)
            return;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.tKey.wasPressedThisFrame)
            StartScrolling(0f);

        if (Keyboard.current.yKey.wasPressedThisFrame)
            StopScrolling();

        if (Keyboard.current.rKey.wasPressedThisFrame)
            ResetTilesAroundTarget();
#endif
    }
}

//using System.Collections.Generic;
//using UnityEngine;

//public class StraightMapSpawner : MonoBehaviour
//{
//    [Header("References")]
//    [SerializeField] private GameObject roadTilePrefab;
//    [SerializeField] private Transform discFollower;

//    [Header("Pool Settings")]
//    [SerializeField] private int poolSize = 5;

//    [Header("Tile Settings")]
//    [SerializeField] private float tileLength = 250f;
//    [SerializeField] private float startZ = 0f;
//    [SerializeField] private float despawnZ = -400f;

//    [Header("Move Settings")]
//    [SerializeField] private float moveSpeed = 0f;
//    [SerializeField] private float deceleration = 3f;
//    [SerializeField] private float minMoveSpeed = 0f;
//    [SerializeField] private float maxMoveSpeed = 35f;

//    [Header("Test Input")]
//    [SerializeField] private float testSpeed = 12f;

//    private readonly List<GameObject> activeTiles = new List<GameObject>();

//    public float CurrentMoveSpeed => moveSpeed;
//    public bool IsScrolling => moveSpeed > 0.01f;

//    private void Start()
//    {
//        moveSpeed = 0f;
//        CreateTilePool();
//    }

//    private void Update()
//    {
//        HandleTestInput();

//        if (IsScrolling == false)
//        {
//            return;
//        }

//        MoveTiles();
//        RecyclePassedTiles();
//        ApplyDeceleration();
//    }

//    public void StartScrolling(float startSpeed)
//    {
//        moveSpeed = Mathf.Clamp(startSpeed, minMoveSpeed, maxMoveSpeed);

//        Debug.Log("맵 이동 시작! 속도: " + moveSpeed);
//    }

//    public void StopScrolling()
//    {
//        moveSpeed = 0f;
//    }

//    private void CreateTilePool()
//    {
//        if (roadTilePrefab == null)
//        {
//            Debug.LogError("Road Tile Prefab이 비어있음!");
//            return;
//        }

//        for (int i = 0; i < poolSize; i++)
//        {
//            float spawnZ = startZ + i * tileLength;
//            Vector3 spawnPosition = new Vector3(0f, 0f, spawnZ);

//            GameObject newTile = Instantiate(
//                roadTilePrefab,
//                spawnPosition,
//                Quaternion.identity
//            );

//            activeTiles.Add(newTile);
//        }
//    }

//    private void MoveTiles()
//    {
//        Vector3 moveOffset = Vector3.back * moveSpeed * Time.deltaTime;

//        for (int i = 0; i < activeTiles.Count; i++)
//        {
//            if (activeTiles[i] == null)
//            {
//                continue;
//            }

//            activeTiles[i].transform.position += moveOffset;
//        }
//    }

//    private void RecyclePassedTiles()
//    {
//        if (activeTiles.Count == 0)
//        {
//            return;
//        }

//        GameObject firstTile = activeTiles[0];

//        if (firstTile.transform.position.z > despawnZ)
//        {
//            return;
//        }

//        activeTiles.RemoveAt(0);

//        float lastZ = activeTiles[activeTiles.Count - 1].transform.position.z;
//        float recycledZ = lastZ + tileLength;

//        firstTile.transform.position = new Vector3(0f, 0f, recycledZ);

//        RoadTileObjectSpawner objectSpawner = firstTile.GetComponent<RoadTileObjectSpawner>();

//        if (objectSpawner != null)
//        {
//            objectSpawner.ResetTileObjects();
//        }

//        activeTiles.Add(firstTile);

//        Debug.Log("타일 재사용: " + firstTile.name);
//    }

//    private void ApplyDeceleration()
//    {
//        moveSpeed -= deceleration * Time.deltaTime;
//        moveSpeed = Mathf.Max(moveSpeed, 0f);
//    }

//    private void HandleTestInput()
//    {
//        if (Input.GetKeyDown(KeyCode.T))
//        {
//            StartScrolling(testSpeed);
//        }

//        if (Input.GetKeyDown(KeyCode.Y))
//        {
//            StopScrolling();
//        }
//    }
//}