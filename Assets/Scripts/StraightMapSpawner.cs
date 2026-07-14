using System.Collections.Generic;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class StraightMapSpawner : MonoBehaviour
{
    private enum TilePivotMode
    {
        Center,
        StartEdge
    }

    [Header("References")]
    [SerializeField] private GameObject roadTilePrefab;

    [Tooltip("DiscFollowTarget 또는 Disc를 넣으세요. 이 Transform의 Z 위치를 기준으로 타일을 재사용합니다.")]
    [SerializeField] private Transform followTarget;

    [Tooltip("생성된 타일을 담을 부모입니다. 비워두면 이 오브젝트 아래에 생성됩니다.")]
    [SerializeField] private Transform tileParent;

    [Header("Pool Settings")]
    [SerializeField] private int poolSize = 5;

    [Tooltip("타겟 뒤쪽에 몇 개의 타일을 남겨둘지입니다. 보통 0 또는 1.")]
    [SerializeField] private int tilesBehindTarget = 0;

    [Header("Tile Settings")]
    [Tooltip("RoadTilePrefab의 실제 Z 길이와 반드시 같아야 합니다.")]
    [SerializeField] private float tileLength = 250f;

    [Tooltip("0번 타일의 기준 Z 위치입니다.")]
    [SerializeField] private float startZ = 0f;

    [Tooltip("타일 프리팹의 pivot이 중앙이면 Center, 타일 시작점이면 StartEdge를 선택하세요.")]
    [SerializeField] private TilePivotMode tilePivotMode = TilePivotMode.Center;

    [SerializeField] private float tileX = 0f;
    [SerializeField] private float tileY = 0f;
    [SerializeField] private Vector3 tileEulerRotation = Vector3.zero;

    [Header("Generation Control")]
    [Tooltip("true면 StartScrolling이 호출된 뒤부터 타일 재사용을 시작합니다.")]
    [SerializeField] private bool generateOnlyAfterStartScrolling = false;

    [Tooltip("타겟이 너무 크게 앞으로 점프했을 때 여러 번 재사용하지 않고 풀 전체를 재배치합니다.")]
    [SerializeField] private int maxSequentialRecyclesPerFrame = 3;

    [Tooltip("타겟이 뒤로 크게 이동하면 풀을 타겟 주변으로 재배치합니다. 게임 리셋 대응용입니다.")]
    [SerializeField] private bool resetOnLargeBackwardJump = true;

    [Tooltip("타겟이 이 타일 수 이상 뒤로 이동하면 큰 backward jump로 봅니다.")]
    [SerializeField] private int backwardResetThresholdInTiles = 2;

    [Header("Object Reset")]
    [SerializeField] private bool resetObjectsOnInitialSpawn = true;
    [SerializeField] private bool resetObjectsWhenRecycled = true;
    [SerializeField] private bool resetObjectsWhenSnapped = true;

    [Header("Debug")]
    [SerializeField] private bool logGeneration = false;

    [Header("Test Input")]
    [SerializeField] private bool enableTestInput = false;

    private readonly List<GameObject> activeTiles = new List<GameObject>();

    private bool generationEnabled;
    private int currentFirstTileIndex;

    private float lastTargetZ;
    private float targetForwardSpeed;

    public bool IsGenerationEnabled => generationEnabled;
    public float CurrentTargetForwardSpeed => Mathf.Max(0f, targetForwardSpeed);
    public int CurrentFirstTileIndex => currentFirstTileIndex;

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

        SyncTilesToFollowTarget();
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
        // 기존 이벤트 연결 호환용.
        // 이제 startSpeed로 맵을 움직이지 않고, followTarget 위치를 기준으로 타일을 재사용합니다.
        generationEnabled = true;

        lastTargetZ = GetTargetZ();
        targetForwardSpeed = 0f;

        if (logGeneration)
        {
            Debug.Log(
                $"맵 생성 활성화. startSpeed는 호환용으로만 유지됨: {startSpeed}"
            );
        }
    }

    public void StopScrolling()
    {
        generationEnabled = false;
        targetForwardSpeed = 0f;

        if (logGeneration)
            Debug.Log("맵 생성 비활성화");
    }

    public void ResetTilesAroundTarget()
    {
        if (activeTiles.Count == 0)
        {
            CreateTilePool();
            return;
        }

        currentFirstTileIndex = CalculateDesiredFirstTileIndex();

        for (int i = 0; i < activeTiles.Count; i++)
        {
            int tileIndex = currentFirstTileIndex + i;

            PlaceTileByIndex(
                activeTiles[i],
                tileIndex,
                resetObjectsWhenSnapped
            );
        }

        lastTargetZ = GetTargetZ();
        targetForwardSpeed = 0f;

        if (logGeneration)
        {
            Debug.Log(
                $"타일 풀 리셋. targetZ: {GetTargetZ():F1}, " +
                $"firstTileIndex: {currentFirstTileIndex}"
            );
        }
    }

    private void CreateTilePool()
    {
        if (roadTilePrefab == null)
        {
            Debug.LogError("Road Tile Prefab이 비어있음!");
            return;
        }

        if (tileParent == null)
            tileParent = transform;

        activeTiles.Clear();

        currentFirstTileIndex = CalculateDesiredFirstTileIndex();

        for (int i = 0; i < poolSize; i++)
        {
            int tileIndex = currentFirstTileIndex + i;

            GameObject newTile = Instantiate(
                roadTilePrefab,
                tileParent
            );

            PlaceTileByIndex(
                newTile,
                tileIndex,
                resetObjectsOnInitialSpawn
            );

            activeTiles.Add(newTile);
        }

        if (logGeneration)
        {
            Debug.Log(
                $"타일 풀 생성. poolSize: {poolSize}, " +
                $"firstTileIndex: {currentFirstTileIndex}"
            );
        }
    }

    private void SyncTilesToFollowTarget()
    {
        int desiredFirstIndex = CalculateDesiredFirstTileIndex();

        int delta = desiredFirstIndex - currentFirstTileIndex;

        if (delta == 0)
            return;

        // 타겟이 뒤로 조금 흔들리는 경우는 무시합니다.
        // 게임 리셋처럼 크게 뒤로 이동한 경우만 전체 리셋합니다.
        if (delta < 0)
        {
            if (resetOnLargeBackwardJump &&
                Mathf.Abs(delta) >= backwardResetThresholdInTiles)
            {
                ResetTilesAroundTarget();
            }

            return;
        }

        // 너무 많이 앞으로 점프한 경우, 매 프레임 여러 개를 재사용하지 않고 바로 재배치합니다.
        if (delta >= poolSize || delta > maxSequentialRecyclesPerFrame)
        {
            ResetTilesAroundTarget();
            return;
        }

        for (int i = 0; i < delta; i++)
        {
            RecycleFirstTileToFront();
        }
    }

    private void RecycleFirstTileToFront()
    {
        if (activeTiles.Count == 0)
            return;

        GameObject firstTile = activeTiles[0];

        activeTiles.RemoveAt(0);

        int newTileIndex =
            currentFirstTileIndex +
            activeTiles.Count +
            1;

        PlaceTileByIndex(
            firstTile,
            newTileIndex,
            resetObjectsWhenRecycled
        );

        activeTiles.Add(firstTile);

        currentFirstTileIndex++;

        if (logGeneration)
        {
            Debug.Log(
                $"타일 재사용: {firstTile.name}, " +
                $"newTileIndex: {newTileIndex}, " +
                $"targetZ: {GetTargetZ():F1}, " +
                $"currentFirstTileIndex: {currentFirstTileIndex}"
            );
        }
    }

    private void PlaceTileByIndex(
        GameObject tile,
        int tileIndex,
        bool resetObjects)
    {
        if (tile == null)
            return;

        float z = GetTilePivotZ(tileIndex);

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

    private void UpdateTargetSpeed()
    {
        float currentZ = GetTargetZ();
        float dt = Mathf.Max(0.0001f, Time.deltaTime);

        targetForwardSpeed = (currentZ - lastTargetZ) / dt;
        lastTargetZ = currentZ;
    }

    private int CalculateDesiredFirstTileIndex()
    {
        int targetTileIndex = CalculateTileIndexAtZ(GetTargetZ());

        int safeTilesBehind = Mathf.Clamp(
            tilesBehindTarget,
            0,
            Mathf.Max(0, poolSize - 1)
        );

        return targetTileIndex - safeTilesBehind;
    }

    private int CalculateTileIndexAtZ(float z)
    {
        if (tileLength <= 0.0001f)
            return 0;

        switch (tilePivotMode)
        {
            case TilePivotMode.Center:
                // 0번 타일 중심이 startZ이고,
                // 타일 범위는 startZ - tileLength/2 ~ startZ + tileLength/2입니다.
                return Mathf.FloorToInt(
                    (z - startZ + tileLength * 0.5f) / tileLength
                );

            case TilePivotMode.StartEdge:
                // 0번 타일 시작점이 startZ이고,
                // 타일 범위는 startZ ~ startZ + tileLength입니다.
                return Mathf.FloorToInt(
                    (z - startZ) / tileLength
                );

            default:
                return 0;
        }
    }

    private float GetTilePivotZ(int tileIndex)
    {
        return startZ + tileIndex * tileLength;
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