using UnityEngine;

[CreateAssetMenu(
    fileName = "RoadTileObjectSpawnData",
    menuName = "Game/Road Tile Object Spawn Data"
)]
public class RoadTileObjectSpawnData : ScriptableObject
{
    [Header("Object List")]
    public SpawnObjectData[] spawnObjects;
}