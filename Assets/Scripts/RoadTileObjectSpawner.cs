using System.Collections.Generic;
using UnityEngine;

public class RoadTileObjectSpawner : MonoBehaviour
{
    [Header("Spawn Data")]
    [SerializeField] private RoadTileObjectSpawnData spawnData;

    [Header("Spawn Settings")]
    [SerializeField] private int minObjectCount = 5;
    [SerializeField] private int maxObjectCount = 12;

    [Header("Spawn Area")]
    [SerializeField] private float spawnAreaWidth = 20f;
    [SerializeField] private float spawnAreaLength = 200f;
    [SerializeField] private float spawnY = 1f;

    private readonly List<GameObject> spawnedObjects = new List<GameObject>();

    private void Start()
    {
        ResetTileObjects();
    }

    public void ResetTileObjects()
    {
        ClearObjects();

        if (CanSpawnFromData() == false)
        {
            Debug.LogWarning(gameObject.name + "에 유효한 Spawn Data가 없습니다. 오브젝트를 스폰하지 않습니다.");
            return;
        }

        SpawnRandomObjects();
    }

    private bool CanSpawnFromData()
    {
        if (spawnData == null)
        {
            return false;
        }

        if (spawnData.spawnObjects == null)
        {
            return false;
        }

        if (spawnData.spawnObjects.Length == 0)
        {
            return false;
        }

        return true;
    }

    private void ClearObjects()
    {
        for (int i = 0; i < spawnedObjects.Count; i++)
        {
            if (spawnedObjects[i] != null)
            {
                Destroy(spawnedObjects[i]);
            }
        }

        spawnedObjects.Clear();
    }

    private void SpawnRandomObjects()
    {
        int objectCount = Random.Range(minObjectCount, maxObjectCount + 1);

        for (int i = 0; i < objectCount; i++)
        {
            SpawnObject();
        }
    }

    private void SpawnObject()
    {
        SpawnObjectData selectedData = GetRandomObjectData();

        if (selectedData == null)
        {
            return;
        }

        if (selectedData.prefab == null)
        {
            Debug.LogWarning(selectedData.objectName + "의 Prefab이 비어있습니다.");
            return;
        }

        GameObject newObject = Instantiate(selectedData.prefab, transform);

        float randomX = Random.Range(-spawnAreaWidth * 0.5f, spawnAreaWidth * 0.5f);
        float randomZ = Random.Range(-spawnAreaLength * 0.5f, spawnAreaLength * 0.5f);

        newObject.transform.localPosition = new Vector3(
            randomX,
            spawnY + selectedData.yOffset,
            randomZ
        );

        float randomScale = Random.Range(
            selectedData.randomScaleRange.x,
            selectedData.randomScaleRange.y
        );

        Vector3 originalScale = newObject.transform.localScale;
        newObject.transform.localScale = originalScale * randomScale;

        float randomYRotation = Random.Range(0f, 360f);

        newObject.transform.localRotation = Quaternion.Euler(
            0f,
            randomYRotation,
            0f
        );

        spawnedObjects.Add(newObject);
    }

    private SpawnObjectData GetRandomObjectData()
    {
        float totalWeight = 0f;

        for (int i = 0; i < spawnData.spawnObjects.Length; i++)
        {
            SpawnObjectData data = spawnData.spawnObjects[i];

            if (data == null)
            {
                continue;
            }

            if (data.prefab == null)
            {
                continue;
            }

            if (data.spawnWeight <= 0f)
            {
                continue;
            }

            totalWeight += data.spawnWeight;
        }

        if (totalWeight <= 0f)
        {
            Debug.LogWarning(gameObject.name + "의 Spawn Data에 스폰 가능한 오브젝트가 없습니다.");
            return null;
        }

        float randomValue = Random.Range(0f, totalWeight);
        float currentWeight = 0f;

        for (int i = 0; i < spawnData.spawnObjects.Length; i++)
        {
            SpawnObjectData data = spawnData.spawnObjects[i];

            if (data == null)
            {
                continue;
            }

            if (data.prefab == null)
            {
                continue;
            }

            if (data.spawnWeight <= 0f)
            {
                continue;
            }

            currentWeight += data.spawnWeight;

            if (randomValue <= currentWeight)
            {
                return data;
            }
        }

        return null;
    }
}