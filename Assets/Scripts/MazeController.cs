using UnityEngine;
using System.Collections.Generic;

// Does all maze work for whichever MazeData it is given. It holds no maze of its own,
// so one MazeController can run any number of mazes.
public class MazeController : MonoBehaviour
{
    private MazeGenerator mazeGenerator;
    private readonly List<MazeData> generatedMazes = new List<MazeData>();

    void Awake()
    {
        mazeGenerator = GetComponent<MazeGenerator>();
    }

    // Centre is the data's anchor if it has one, otherwise fallbackCentre, otherwise this object's position
    public bool GenerateMaze(MazeData mazeData, Vector3? fallbackCentre = null)
    {
        if (mazeGenerator == null)
        {
            Debug.LogError("MazeGenerator component not found on the GameObject.");
            return false;
        }

        if (mazeData == null)
        {
            Debug.LogError("MazeController: GenerateMaze was given no MazeData.");
            return false;
        }

        if (mazeData.Root != null)
            Destroy(mazeData.Root.gameObject);

        Vector3 centre = mazeData.anchorPosition ?? fallbackCentre ?? transform.position;
        Transform root = new GameObject("Maze - " + mazeData.name).transform;
        root.SetParent(transform, false);

        mazeData.SetInstance(root, centre);
        mazeGenerator.Generate(mazeData, root, centre);

        if (!generatedMazes.Contains(mazeData))
            generatedMazes.Add(mazeData);
        return true;
    }

    // Finds which generated maze an object (e.g. a clicked wall) belongs to
    public MazeData FindMazeData(Transform mazeObject)
    {
        foreach (MazeData mazeData in generatedMazes)
        {
            if (mazeData.Root != null && mazeObject.IsChildOf(mazeData.Root))
                return mazeData;
        }
        return null;
    }

    public int GetMazeSize(MazeData mazeData)
    {
        return Mathf.Max(mazeData.width, mazeData.height);
    }

    public Vector2Int WorldToMazeGrid(MazeData mazeData, Vector3 worldPosition)
    {
        bool[,] mazeFloor = mazeData.GetFloorData();
        float tileSize = mazeData.tileSize;
        float originX = mazeData.Centre.x - mazeFloor.GetLength(0) * tileSize * 0.5f;
        float originZ = mazeData.Centre.z - mazeFloor.GetLength(1) * tileSize * 0.5f;

        int x = Mathf.FloorToInt((worldPosition.x - originX) / tileSize);
        int z = Mathf.FloorToInt((worldPosition.z - originZ) / tileSize);
        return new Vector2Int(
            Mathf.Clamp(x, 0, mazeFloor.GetLength(0) - 1),
            Mathf.Clamp(z, 0, mazeFloor.GetLength(1) - 1));
    }

    public Vector3 MazeGridToWorld(MazeData mazeData, Vector2Int gridPosition)
    {
        bool[,] mazeFloor = mazeData.GetFloorData();
        float tileSize = mazeData.tileSize;
        float originX = mazeData.Centre.x - mazeFloor.GetLength(0) * tileSize * 0.5f;
        float originZ = mazeData.Centre.z - mazeFloor.GetLength(1) * tileSize * 0.5f;

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

    public Vector2Int FindRandomWalkablePosition(MazeData mazeData)
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

        return new Vector2Int(x, y);
    }

    // Same as above but has to be at least minDistance away from a position
    public Vector2Int FindRandomWalkablePosition(MazeData mazeData, Vector2Int awayFrom, float minDistance)
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
            return validPositions[Random.Range(0, validPositions.Count)];
        return furthestPosition;
    }

    public bool CanRaiseWall(MazeData mazeData, Vector2Int position, Vector2Int angelGridPosition, Vector2Int playerGridPosition)
    {
        return GetAvailableWallPositions(mazeData, angelGridPosition, playerGridPosition).Contains(position);
    }

    // All floor tiles a wall can be raised on without blocking the angel from reaching the player.
    // Uses Tarjan's articulation points, starting the search from the angel.
    public List<Vector2Int> GetAvailableWallPositions(MazeData mazeData, Vector2Int angelGridPosition, Vector2Int playerGridPosition)
    {
        bool[,] floor = mazeData.GetFloorData();
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


    private bool InBounds(bool[,] grid, Vector2Int position)
    {
        return position.x >= 0 && position.x < grid.GetLength(0) && position.y >= 0 && position.y < grid.GetLength(1);
    }

    public void RaiseWall(MazeData mazeData, Vector2Int position)
    {
        if (!mazeData.IsGenerated)
        {
            Debug.LogError("Maze data not initialized.");
            return;
        }

        bool[,] walls = mazeData.GetWallData();
        bool[,] floor = mazeData.GetFloorData();

        if (!InBounds(walls, position))
        {
            Debug.LogError("Position out of bounds.");
            return;
        }

        if (walls[position.x, position.y])
            return; // Already a wall

        walls[position.x, position.y] = true;
        floor[position.x, position.y] = false;

        float tileSize = mazeData.tileSize;
        mazeGenerator.SpawnBlock(
            mazeData.Root,
            "Wall " + position.x + "," + position.y,
            mazeData.Centre + new Vector3(
                (position.x - (walls.GetLength(0) - 1) * 0.5f) * tileSize,
                mazeData.wallHeight / 2f,
                (position.y - (walls.GetLength(1) - 1) * 0.5f) * tileSize),
            new Vector3(tileSize, mazeData.wallHeight, tileSize),
            mazeData.wallPrefab, mazeData.wallMaterial);
    }

    public void LowerWall(MazeData mazeData, Vector2Int position)
    {
        if (!mazeData.IsGenerated)
        {
            Debug.LogError("Maze data not initialized.");
            return;
        }

        bool[,] walls = mazeData.GetWallData();
        bool[,] floor = mazeData.GetFloorData();

        if (!InBounds(walls, position))
        {
            Debug.LogError("Position out of bounds.");
            return;
        }

        walls[position.x, position.y] = false;
        floor[position.x, position.y] = true;

        Transform wallTransform = mazeData.Root.Find("Wall " + position.x + "," + position.y);
        if (wallTransform != null)
            Destroy(wallTransform.gameObject);
    }
}
