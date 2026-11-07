using UnityEngine;

[CreateAssetMenu(fileName = "MazeData", menuName = "Scriptable Objects/Maze Data")]
public class MazeData : ScriptableObject
{
    [Header("Appearance")]
    public GameObject wallPrefab;

    [Min(0.01f)] public float tileSize = 1f;
    [Min(0.01f)] public float wallHeight = 0.5f;


    private bool[,] walls;
    private bool[,] floor;

    public bool[,] GetWallData()
    {
        return walls;
    }

    public bool[,] GetFloorData()
    {
        return floor;
    }

    public void SetMazeData(bool[,] wallData, bool[,] floorData)
    {
        walls = wallData;
        floor = floorData;
    }
}
