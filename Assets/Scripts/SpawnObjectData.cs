using UnityEngine;

[System.Serializable]
public class SpawnObjectData
{
    public string objectName;
    public GameObject prefab;

    [Header("Spawn Weight")]
    public float spawnWeight = 1f;

    [Header("Scale")]
    public Vector2 randomScaleRange = new Vector2(1f, 1f);

    [Header("Y Offset")]
    public float yOffset = 0f;
}