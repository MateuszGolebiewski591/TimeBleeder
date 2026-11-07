using UnityEngine;
using System.Collections.Generic;

public class MazeController : MonoBehaviour
{
    [SerializeField] MazeData mazeData;
    public GameObject centreAnchor;

    private GameObject wallPrefab => mazeData.wallPrefab;
    private float tileSize => mazeData.tileSize;
    private float wallHeight => mazeData.wallHeight;

    private MazeGenerator mazeGenerator;

    private bool[,] walls;
    private bool[,] floor;

    void Awake()
    {
        mazeGenerator = GetComponent<MazeGenerator>();
    }

    public void GenerateMaze()
    {
        if (mazeGenerator == null)
        {
            Debug.LogError("MazeGenerator component not found on the GameObject.");
            return;
        }

        mazeGenerator.centrePosition = GetMazeCentre();
        mazeGenerator.Generate();
    }

    public int GetMazeSize()
    {
        return Mathf.Max(mazeGenerator.width, mazeGenerator.height);
    }

    public bool[,] GetFloor()
    {
        return mazeData.GetFloorData();
    }

    private Vector3 GetMazeCentre()
    {
        if (centreAnchor != null)
            return centreAnchor.transform.position;
        else
            return this.transform.position;
    }

    public Vector2Int WorldToMazeGrid(Vector3 worldPosition)
    {
        bool[,] mazeFloor = mazeData.GetFloorData();
        Vector3 mazeCentre = GetMazeCentre();
        float originX = mazeCentre.x - mazeFloor.GetLength(0) * tileSize * 0.5f;
        float originZ = mazeCentre.z - mazeFloor.GetLength(1) * tileSize * 0.5f;

        int x = Mathf.FloorToInt((worldPosition.x - originX) / tileSize);
        int z = Mathf.FloorToInt((worldPosition.z - originZ) / tileSize);
        return new Vector2Int(
            Mathf.Clamp(x, 0, mazeFloor.GetLength(0) - 1),
            Mathf.Clamp(z, 0, mazeFloor.GetLength(1) - 1));
    }

    // mazeFloor param kept so the angel can still call this the same way
    public Vector3 MazeGridToWorld(Vector2Int gridPosition, bool[,] mazeFloor)
    {
        Vector3 mazeCentre = GetMazeCentre();
        float originX = mazeCentre.x - mazeFloor.GetLength(0) * tileSize * 0.5f;
        float originZ = mazeCentre.z - mazeFloor.GetLength(1) * tileSize * 0.5f;

        float worldX = originX + (gridPosition.x + 0.5f) * tileSize;
        float worldZ = originZ + (gridPosition.y + 0.5f) * tileSize;
        return new Vector3(worldX, 0f, worldZ);
    }

    public bool IsWalkable(bool[,] mazeFloor, Vector2Int position)
    {
        return position.x >= 0 && position.x < mazeFloor.GetLength(0)
            && position.y >= 0 && position.y < mazeFloor.GetLength(1)
            && mazeFloor[position.x, position.y];
    }

    public void FindRandomWalkablePosition(out Vector2Int randomPosition)
    {
        bool[,] mazeFloor = mazeData.GetFloorData();
        int width = mazeFloor.GetLength(0);
        int height = mazeFloor.GetLength(1);

        int x;
        int y;

        do
        {
            x = Random.Range(0, width);
            y = Random.Range(0, height);
        } while (!mazeFloor[x, y]);

        randomPosition = new Vector2Int(x, y);
    }

    // Same as above but has to be at least minDistance away from a position
    // Old version was inefficient
    public void FindRandomWalkablePosition(Vector2Int awayFrom, float minDistance, out Vector2Int randomPosition)
    {
        bool[,] mazeFloor = mazeData.GetFloorData();
        List<Vector2Int> validPositions = new List<Vector2Int>();
        Vector2Int furthestPosition = awayFrom;
        float furthestDistance = -1f;

        for (int x = 0; x < mazeFloor.GetLength(0); x++)
        {
            for (int y = 0; y < mazeFloor.GetLength(1); y++)
            {
                if (!mazeFloor[x, y]) continue;

                Vector2Int position = new Vector2Int(x, y);
                float distance = Vector2Int.Distance(position, awayFrom);

                if (distance >= minDistance)
                    validPositions.Add(position);

                if (distance > furthestDistance)
                {
                    furthestDistance = distance;
                    furthestPosition = position;
                }
            }
        }

        // If nothing is far enough away just use the furthest position
        if (validPositions.Count > 0)
            randomPosition = validPositions[Random.Range(0, validPositions.Count)];
        else
            randomPosition = furthestPosition;
    }

    // Display all available wall positions to raise, that don't block the angel from reaching the player
    public List<Vector2Int> QueryWallRaise(Vector2Int angelGridPosition, Vector2Int playerGridPosition)
    {
        List<Vector2Int> wallPositions = GetAvailableWallPositions(angelGridPosition, playerGridPosition);

        return wallPositions;
    }

    public bool CanRaiseWall(Vector2Int position, Vector2Int angelGridPosition, Vector2Int playerGridPosition)
    {
        return GetAvailableWallPositions(angelGridPosition, playerGridPosition).Contains(position);
    }

    // Uses Tarjan's articulation points, starting the search from the angel.
    public List<Vector2Int> GetAvailableWallPositions(Vector2Int angelGridPosition, Vector2Int playerGridPosition)
    {
        floor = mazeData.GetFloorData();
        int width = floor.GetLength(0);
        int height = floor.GetLength(1);

        HashSet<Vector2Int> blockingPositions = FindBlockingPositions(floor, angelGridPosition, playerGridPosition);
        List<Vector2Int> wallPositions = new List<Vector2Int>();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (!floor[x, y]) continue;

                Vector2Int position = new Vector2Int(x, y);
                if (position == angelGridPosition || position == playerGridPosition) continue;
                if (blockingPositions.Contains(position)) continue;

                wallPositions.Add(position);
            }
        }

        return wallPositions;
    }

    private HashSet<Vector2Int> FindBlockingPositions(bool[,] mazeFloor, Vector2Int angelGridPosition, Vector2Int playerGridPosition)
    {
        HashSet<Vector2Int> blockingPositions = new HashSet<Vector2Int>();
        if (!IsWalkable(mazeFloor, angelGridPosition) || !IsWalkable(mazeFloor, playerGridPosition))
            return blockingPositions;

        int width = mazeFloor.GetLength(0);
        int height = mazeFloor.GetLength(1);

        int[] dx = { 1, -1, 0, 0 }; // Cardinal directions
        int[] dy = { 0, 0, 1, -1 };

        int[,] disc = new int[width, height]; // Order each tile was discovered in, 0 = not visited
        int[,] low = new int[width, height]; // Earliest discovered tile reachable from this tile's subtree
        int[,] nextDirection = new int[width, height]; // Which neighbour to check next
        Vector2Int[,] parent = new Vector2Int[width, height];
        int timer = 0;

        // Depth first search with our own stack, as recursion could get very deep in a long maze
        Stack<Vector2Int> stack = new Stack<Vector2Int>();
        disc[angelGridPosition.x, angelGridPosition.y] = ++timer;
        low[angelGridPosition.x, angelGridPosition.y] = timer;
        parent[angelGridPosition.x, angelGridPosition.y] = angelGridPosition;
        stack.Push(angelGridPosition);

        while (stack.Count > 0)
        {
            Vector2Int current = stack.Peek();

            if (nextDirection[current.x, current.y] < 4)
            {
                int i = nextDirection[current.x, current.y]++;
                Vector2Int next = new Vector2Int(current.x + dx[i], current.y + dy[i]);
                if (!IsWalkable(mazeFloor, next)) continue;

                if (disc[next.x, next.y] == 0)
                {
                    parent[next.x, next.y] = current;
                    disc[next.x, next.y] = ++timer;
                    low[next.x, next.y] = timer;
                    stack.Push(next);
                }
                else if (next != parent[current.x, current.y])
                {
                    // Back edge, so there is a loop
                    low[current.x, current.y] = Mathf.Min(low[current.x, current.y], disc[next.x, next.y]);
                }
            }
            else
            {
                // Finished this tile, pass its low value up to the parent
                stack.Pop();
                Vector2Int currentParent = parent[current.x, current.y];
                low[currentParent.x, currentParent.y] = Mathf.Min(low[currentParent.x, currentParent.y], low[current.x, current.y]);
            }
        }

        // Angel can't reach the player anyway
        if (disc[playerGridPosition.x, playerGridPosition.y] == 0)
            return blockingPositions;

        // Walk from the player back to the angel checking each tile on the way
        Vector2Int child = playerGridPosition;
        Vector2Int position = parent[child.x, child.y];
        while (child != angelGridPosition && position != angelGridPosition)
        {
            if (low[child.x, child.y] >= disc[position.x, position.y])
                blockingPositions.Add(position);

            child = position;
            position = parent[position.x, position.y];
        }

        return blockingPositions;
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

        if (walls[position.x, position.y])
            return; // Already a wall

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
            wallPrefab, mazeGenerator.material);
    }

    public void LowerWall(Vector2Int position)
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

        walls[position.x, position.y] = false; // Lower wall
        floor[position.x, position.y] = true; // Add floor

        mazeData.SetMazeData(walls, floor);

        // Destroy the wall GameObject at this position
        string wallName = "Wall " + position.x + "," + position.y;
        Transform wallTransform = transform.Find(wallName);
        if (wallTransform != null)
        {
            Destroy(wallTransform.gameObject);
        }
    }
}
