using System.Collections.Generic;
using UnityEngine;

public class StraightMapSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform discTarget;

    [Header("Map Tiles")]
    [SerializeField] private GameObject[] roadTilePrefabs;
    //[SerializeField] private GameObject roadTilePrefab;

    [Tooltip("게임 시작 시 미리 생성할 타일 개수")]
    [SerializeField] private int initialSpawnCount = 3;

    //[SerializeField] private int poolSize = 5;

    [Header("Tile Settings")]
    [SerializeField] private float tileLength = 250f;
    [SerializeField] private float startZ = 0f;

    [Tooltip("원반이 마지막 생성 위치에 이 거리만큼 가까워지면 다음 타일을 생성합니다.")]
    [SerializeField] private float spawnAheadDistance = 300f;
    
    [Tooltip("원반보다 이 거리 이상 뒤에 있는 타일은 삭제합니다.")]
    [SerializeField] private float destroyBehindDistance = 400f;

    private readonly List<GameObject> spawnedTiles =
    new List<GameObject>();

    private int nextTileIndex;
    private float nextSpawnZ;
    //[Header("Pool Settings")]
    //[SerializeField] private float recycleBehindDistance = 50f;
    //[SerializeField] private float despawnZ = -400f;

    //[Header("Move Settings")]
    //[SerializeField] private float moveSpeed = 0f;
    //[SerializeField] private float deceleration = 3f;
    //[SerializeField] private float minMoveSpeed = 0f;
    //[SerializeField] private float maxMoveSpeed = 35f;

    //[Header("Test Input")]
    //[SerializeField] private float testSpeed = 12f;

    //private readonly List<GameObject> activeTiles = new List<GameObject>();

    //public float CurrentMoveSpeed => moveSpeed;
    //public bool IsScrolling => moveSpeed > 0.01f;

    private void Start()
    {
        nextTileIndex = 0;
        nextSpawnZ = startZ;

        int spawnCount = Mathf.Min(
            initialSpawnCount,
            roadTilePrefabs.Length
        );

        for (int i = 0; i < spawnCount; i++)
        {
            SpawnNextTile();
        }
    }

    private void Update()
    {
        //HandleTestInput();

        //if (IsScrolling == false)
        //{
        //    return;
        //}

        //MoveTiles();
        //RecyclePassedTiles();
        if (discTarget == null)
            return;

        TrySpawnNextTile();
        DestroyPassedTiles();

    }

    private void TrySpawnNextTile()
    {
        if (nextTileIndex >= roadTilePrefabs.Length)
            return;

        float distanceToNextSpawn =
            nextSpawnZ - discTarget.position.z;

        if (distanceToNextSpawn <= spawnAheadDistance)
        {
            SpawnNextTile();
        }
    }

    private void SpawnNextTile()
    {
        if (nextTileIndex >= roadTilePrefabs.Length)
            return;

        GameObject tilePrefab = roadTilePrefabs[nextTileIndex];

        if (tilePrefab == null)
        {
            Debug.LogWarning(
                $"Road Tile 배열의 {nextTileIndex}번이 비어 있습니다."
            );

            nextTileIndex++;
            nextSpawnZ += tileLength;
            return;
        }

        Vector3 spawnPosition = new Vector3(
            0f,
            0f,
            nextSpawnZ
        );

        GameObject newTile = Instantiate(
            tilePrefab,
            spawnPosition,
            Quaternion.identity
        );

        spawnedTiles.Add(newTile);

        nextTileIndex++;
        nextSpawnZ += tileLength;
    }

    private void DestroyPassedTiles()
    {
        for (int i = spawnedTiles.Count - 1; i >= 0; i--)
        {
            GameObject tile = spawnedTiles[i];

            if (tile == null)
            {
                spawnedTiles.RemoveAt(i);
                continue;
            }

            float tileEndZ =
                tile.transform.position.z + tileLength;

            float distanceBehindDisc =
                discTarget.position.z - tileEndZ;

            if (distanceBehindDisc < destroyBehindDistance)
                continue;

            Destroy(tile);
            spawnedTiles.RemoveAt(i);
        }
    }

    //public void StartScrolling(float startSpeed)
    //{
    //    moveSpeed = Mathf.Clamp(startSpeed, minMoveSpeed, maxMoveSpeed);

//    Debug.Log("맵 이동 시작! 속도: " + moveSpeed);
//}

//public void StopScrolling()
//{
//    moveSpeed = 0f;
//}

//private void CreateTilePool()
//{
//    if (roadTilePrefab == null)
//    {
//        Debug.LogError("Road Tile Prefab이 비어있음!");
//        return;
//    }

//    for (int i = 0; i < poolSize; i++)
//    {
//        float spawnZ = startZ + i * tileLength;
//        Vector3 spawnPosition = new Vector3(0f, 0f, spawnZ);

//        GameObject newTile = Instantiate(
//            roadTilePrefab,
//            spawnPosition,
//            Quaternion.identity
//        );

//        activeTiles.Add(newTile);
//    }
//}

//private void MoveTiles()
//{
//    Vector3 moveOffset = Vector3.back * moveSpeed * Time.deltaTime;

//    for (int i = 0; i < activeTiles.Count; i++)
//    {
//        if (activeTiles[i] == null)
//        {
//            continue;
//        }

//        activeTiles[i].transform.position += moveOffset;
//    }
//}

//private void RecyclePassedTiles()
//{
//    if (discTarget == null || activeTiles.Count == 0)
//        return;

//    GameObject firstTile = activeTiles[0];

//    float tileEndZ =
//    firstTile.transform.position.z + tileLength;

//    if (discTarget.position.z < tileEndZ) return;
//    //if (firstTile.transform.position.z > despawnZ)
//    //{
//    //    return;
//    //}

//    activeTiles.RemoveAt(0);

//    GameObject lastTile = activeTiles[activeTiles.Count - 1];
//    //float lastZ = activeTiles[activeTiles.Count - 1].transform.position.z;
//    float recycledZ = lastZ + tileLength;

//    //firstTile.transform.position = new Vector3(0f, 0f, recycledZ);
//    firstTile.transform.position = new Vector3(
//    firstTile.transform.position.x,
//    firstTile.transform.position.y,
//    recycledZ
//);


//    RoadTileObjectSpawner objectSpawner = firstTile.GetComponent<RoadTileObjectSpawner>();

//    if (objectSpawner != null)
//    {
//        objectSpawner.ResetTileObjects();
//    }

//    activeTiles.Add(firstTile);

//    //Debug.Log("타일 재사용: " + firstTile.name);
//}

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
}