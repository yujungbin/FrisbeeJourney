using System.Collections.Generic;
using UnityEngine;

public class StraightMapSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject roadTilePrefab;

    [Header("Pool Settings")]
    [SerializeField] private int poolSize = 5;

    [Header("Tile Settings")]
    [SerializeField] private float tileLength = 250f;
    [SerializeField] private float startZ = 0f;
    [SerializeField] private float despawnZ = -400f;

    [Header("Move Settings")]
    [SerializeField] private float moveSpeed = 0f;
    [SerializeField] private float deceleration = 3f;
    [SerializeField] private float minMoveSpeed = 0f;
    [SerializeField] private float maxMoveSpeed = 35f;

    [Header("Test Input")]
    [SerializeField] private float testSpeed = 12f;

    private readonly List<GameObject> activeTiles = new List<GameObject>();

    public float CurrentMoveSpeed => moveSpeed;
    public bool IsScrolling => moveSpeed > 0.01f;

    private void Start()
    {
        moveSpeed = 0f;
        CreateTilePool();
    }

    private void Update()
    {
        HandleTestInput();

        if (IsScrolling == false)
        {
            return;
        }

        MoveTiles();
        RecyclePassedTiles();
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

    private void CreateTilePool()
    {
        if (roadTilePrefab == null)
        {
            Debug.LogError("Road Tile Prefab이 비어있음!");
            return;
        }

        for (int i = 0; i < poolSize; i++)
        {
            float spawnZ = startZ + i * tileLength;
            Vector3 spawnPosition = new Vector3(0f, 0f, spawnZ);

            GameObject newTile = Instantiate(
                roadTilePrefab,
                spawnPosition,
                Quaternion.identity
            );

            activeTiles.Add(newTile);
        }
    }

    private void MoveTiles()
    {
        Vector3 moveOffset = Vector3.back * moveSpeed * Time.deltaTime;

        for (int i = 0; i < activeTiles.Count; i++)
        {
            if (activeTiles[i] == null)
            {
                continue;
            }

            activeTiles[i].transform.position += moveOffset;
        }
    }

    private void RecyclePassedTiles()
    {
        if (activeTiles.Count == 0)
        {
            return;
        }

        GameObject firstTile = activeTiles[0];

        if (firstTile.transform.position.z > despawnZ)
        {
            return;
        }

        activeTiles.RemoveAt(0);

        float lastZ = activeTiles[activeTiles.Count - 1].transform.position.z;
        float recycledZ = lastZ + tileLength;

        firstTile.transform.position = new Vector3(0f, 0f, recycledZ);

        RoadTileObjectSpawner objectSpawner = firstTile.GetComponent<RoadTileObjectSpawner>();

        if (objectSpawner != null)
        {
            objectSpawner.ResetTileObjects();
        }

        activeTiles.Add(firstTile);

        Debug.Log("타일 재사용: " + firstTile.name);
    }

    private void ApplyDeceleration()
    {
        moveSpeed -= deceleration * Time.deltaTime;
        moveSpeed = Mathf.Max(moveSpeed, 0f);
    }

    private void HandleTestInput()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            StartScrolling(testSpeed);
        }

        if (Input.GetKeyDown(KeyCode.Y))
        {
            StopScrolling();
        }
    }
}