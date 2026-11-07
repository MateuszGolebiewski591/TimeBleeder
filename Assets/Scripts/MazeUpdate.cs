using UnityEngine;

public class MazeUpdate : MonoBehaviour
{
    [SerializeField] MazeData mazeData;

    private GameObject wallPrefab => mazeData.wallPrefab;
    private float tileSize => mazeData.tileSize;
    private float wallHeight => mazeData.wallHeight;

    private MazeGenerator mazeGenerator;

    private bool[,] walls;
    private bool[,] floor;

    private void Start()
    {
        mazeGenerator = GetComponent<MazeGenerator>();
    }
    public void RaiseWall(Vector2Int position)
    {
        walls = mazeData.GetWallData();
        floor = mazeData.GetFloorData();
        
        if (walls == null || floor == null)
        {
            Debug.LogError("Maze data not initialized.");
            return;
        }

        if (position.x < 0 || position.x >= walls.GetLength(0) || position.y < 0 || position.y >= walls.GetLength(1))
        {
            Debug.LogError("Position out of bounds.");
            return;
        }

        walls[position.x, position.y] = true; // Raise wall
        floor[position.x, position.y] = false; // Remove floor

        mazeData.SetMazeData(walls, floor);

        mazeGenerator.SpawnBlock(
            "Wall " + position.x + "," + position.y,
            new Vector3(
                (position.x - (walls.GetLength(0) - 1) * 0.5f) * tileSize,
                wallHeight / 2f,
                (position.y - (walls.GetLength(1) - 1) * 0.5f) * tileSize),
            new Vector3(tileSize, wallHeight, tileSize),
            wallPrefab, null);
    }
}
