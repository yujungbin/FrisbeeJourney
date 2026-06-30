using System.Collections.Generic;
using UnityEngine;

public class StraightMapSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject roadTilePrefab;

    [Header("Spawn Settings")]
    [SerializeField] private int initialTileCount = 12;
    [SerializeField] private float tileLength = 10f;
    [SerializeField] private float startZ = 0f;

    [Header("Move Settings")]
    [SerializeField] private float moveSpeed = 0f;
    [SerializeField] private float deceleration = 3f;
    [SerializeField] private float minMoveSpeed = 0f;
    [SerializeField] private float maxMoveSpeed = 35f;
    [SerializeField] private float despawnZ = -20f;

    [Header("Test Input")]
    [SerializeField] private float testSpeed = 12f;

    private readonly List<GameObject> spawnedTiles = new List<GameObject>();

    public float CurrentMoveSpeed => moveSpeed;
    public bool IsScrolling => moveSpeed > 0.01f;

    private void Start()
    {
        moveSpeed = 0f;
        SpawnInitialTiles();
    }

    private void Update()
    {
        HandleTestInput();

        if (IsScrolling == false)
        {
            return;
        }

        MoveTiles();
        CheckDespawnAndSpawn();
        ApplyDeceleration();
    }

    public void StartScrolling(float startSpeed)
    {
        moveSpeed = Mathf.Clamp(startSpeed, minMoveSpeed, maxMoveSpeed);
        Debug.Log("맵 이동 시작! 속도: " + moveSpeed);
    }

    public void StopScrolling()
    {
        moveSpeed = 0f;
    }

    private void SpawnInitialTiles()
    {
        for (int i = 0; i < initialTileCount; i++)
        {
            float spawnZ = startZ + i * tileLength;
            SpawnTile(spawnZ);
        }
    }

    private void SpawnTile(float zPosition)
    {
        if (roadTilePrefab == null)
        {
            Debug.LogError("Road Tile Prefab이 비어있음!");
            return;
        }

        Vector3 spawnPosition = new Vector3(0f, 0f, zPosition);

        GameObject newTile = Instantiate(
            roadTilePrefab,
            spawnPosition,
            Quaternion.identity
        );

        spawnedTiles.Add(newTile);
    }

    private void MoveTiles()
    {
        Vector3 moveOffset = Vector3.back * moveSpeed * Time.deltaTime;

        for (int i = 0; i < spawnedTiles.Count; i++)
        {
            if (spawnedTiles[i] == null)
            {
                continue;
            }

            spawnedTiles[i].transform.position += moveOffset;
        }
    }

    private void CheckDespawnAndSpawn()
    {
        if (spawnedTiles.Count == 0)
        {
            return;
        }

        GameObject firstTile = spawnedTiles[0];

        if (firstTile.transform.position.z > despawnZ)
        {
            return;
        }

        spawnedTiles.RemoveAt(0);
        Destroy(firstTile);

        float lastZ = spawnedTiles[spawnedTiles.Count - 1].transform.position.z;
        float newSpawnZ = lastZ + tileLength;

        SpawnTile(newSpawnZ);
    }

    private void ApplyDeceleration()
    {
        moveSpeed -= deceleration * Time.deltaTime;
        moveSpeed = Mathf.Max(moveSpeed, 0f);
    }

    private void HandleTestInput()
    {
        // 테스트용: T를 누르면 강제로 맵 시작
        if (Input.GetKeyDown(KeyCode.T))
        {
            StartScrolling(testSpeed);
        }

        // 테스트용: Y를 누르면 맵 정지
        if (Input.GetKeyDown(KeyCode.Y))
        {
            StopScrolling();
        }
    }
}