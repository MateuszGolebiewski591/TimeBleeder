using UnityEngine;
using System.Collections.Generic; 

// Uses Dijkstra's algorithm to find the shortest path to the player, stepping one tile at a time

public class AngelMovement : MonoBehaviour
{
    public float smoothMoveSpeed = 5f; // Speed at which the angel moves (smoothly)
    public float smoothRotationSpeed = 360f; // Rotation speed in degrees per second

    [SerializeField] MazeData mazeData;
    private Node[,] nodes; // Need nodes as the 2D array doesn't work with Dijkstra's
    
    private Vector2Int currentPosition; 
    private Vector2Int targetPosition;
    private Vector3 movementTarget;
    private Vector3 movementStartPosition;
    private Vector2Int movementGridTarget;
    private bool isMoving;
    private bool returningToStart;
    private Quaternion rotationTarget;
    private bool isRotating;

    // Passes mazeGridToWorld so angel can just do it itself
    public void MoveAngel(Vector2Int angelGridPosition, Vector2Int playerGridPosition, System.Func<Vector2Int, bool[,], Vector3> mazeGridToWorld)
    {
        // Keep the current destination until the angel reaches it, rather than
        // recalculating a new destination while it is still moving.
        if (isMoving)
            return;

        InitialiseGrid(); // Ensure the grid is initialized before moving

        // Move the angel
        Vector2Int nextPosition = MoveTowardsPlayer(angelGridPosition, playerGridPosition);
        Vector2Int delta = nextPosition - angelGridPosition;
        if (delta == Vector2Int.zero)
            return;

        Vector2Int step;
        if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y) && delta.x != 0)
            step = new Vector2Int((int)Mathf.Sign(delta.x), 0);
        else
            step = new Vector2Int(0, (int)Mathf.Sign(delta.y));

        Vector2Int stepPosition = angelGridPosition + step;

        bool[,] floor = mazeData.GetFloorData();
        if (!IsWalkable(floor, stepPosition) && delta.x != 0 && delta.y != 0)
        {
            // If the preferred side of a diagonal route is blocked, try its other side.
            step = step.x != 0
                ? new Vector2Int(0, (int)Mathf.Sign(delta.y))
                : new Vector2Int((int)Mathf.Sign(delta.x), 0);
            stepPosition = angelGridPosition + step;
        }

        if (IsWalkable(floor, stepPosition))
        {
            // Set the facing direction
            rotationTarget = Quaternion.LookRotation(new Vector3(step.x, 0f, step.y), Vector3.up);
            isRotating = true;

            movementTarget = mazeGridToWorld(stepPosition, floor);
            movementStartPosition = transform.position;
            movementGridTarget = stepPosition;
            returningToStart = false;
            isMoving = true;
        }
    }

    // Smoothed rotation and movement
    private void Update()
    {
        if (isRotating)
        {
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                rotationTarget,
                smoothRotationSpeed * Time.deltaTime);

            if (Quaternion.Angle(transform.rotation, rotationTarget) < 0.1f)
            {
                transform.rotation = rotationTarget;
                isRotating = false;
            }
        }

        if (!isMoving)
            return;

        // A wall may be placed after the path was calculated but before this
        // step finishes. Abort the step instead of entering the blocked tile.
        if (!returningToStart && !IsWalkable(mazeData.GetFloorData(), movementGridTarget))
        {
            movementTarget = movementStartPosition;
            returningToStart = true;
        }

        transform.position = Vector3.MoveTowards(transform.position, movementTarget, smoothMoveSpeed * Time.deltaTime);
        if ((transform.position - movementTarget).sqrMagnitude < 0.0001f)
        {
            transform.position = movementTarget;
            isMoving = false;
            returningToStart = false;
        }
    }

    private bool IsWalkable(bool[,] mazeFloor, Vector2Int position)
    {
        return position.x >= 0 && position.x < mazeFloor.GetLength(0)
            && position.y >= 0 && position.y < mazeFloor.GetLength(1)
            && mazeFloor[position.x, position.y];
    }

    public Vector2Int MoveTowardsPlayer(Vector2Int angelPosition, Vector2Int playerPosition)
    {
        currentPosition = angelPosition;
        targetPosition = playerPosition;

        List<Node> path = FindPath(currentPosition, targetPosition);
        return moveToNextTile(path);
    }

    private Vector2Int moveToNextTile(List<Node> path)
    {
        if (path != null && path.Count > 1)
        {
            Node nextNode = path[1];
            Vector2Int nextPosition = new Vector2Int(nextNode.X, nextNode.Y);

            return nextPosition;
        }
        else
        {
            // No path found or already at the target, stay in place
            return currentPosition;
        }
    }


    public void InitialiseGrid()
    {
        bool[,] walkableGrid = mazeData.GetFloorData();
        int width = walkableGrid.GetLength(0);
        int height = walkableGrid.GetLength(1);
        nodes = new Node[width, height];

        // Create nodes only for walkable tiles
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (walkableGrid[x, y])
                {
                    nodes[x, y] = new Node(x, y);
                }
            }
        }

        // Connect only after every walkable node has been created.
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (walkableGrid[x, y])
                    ConnectNeighbors(x, y, width, height, walkableGrid);
            }
        }
    }

    private void ConnectNeighbors(int x, int y, int width, int height, bool[,] walkableGrid)
    {
        int[] dx = { 1, -1, 0, 0 }; // Cardinal directions
        int[] dy = { 0, 0, 1, -1 };

        // No diagonal movement, so only connect cardinal neighbors (N, S, E, W)
        for (int i = 0; i < 4; i++)
        {
            int nx = x + dx[i];
            int ny = y + dy[i];

            if (nx >= 0 && nx < width && ny >= 0 && ny < height && walkableGrid[nx, ny])
            {
                nodes[x, y].AddNeighbor(nodes[nx, ny], 1.0f); // Cost=1
            }
        }
    }


    public List<Node> FindPath(Vector2Int start, Vector2Int end)
    {
        if (nodes == null ||
            start.x < 0 || start.y < 0 || start.x >= nodes.GetLength(0) || start.y >= nodes.GetLength(1) ||
            end.x < 0 || end.y < 0 || end.x >= nodes.GetLength(0) || end.y >= nodes.GetLength(1))
        {
            return null;
        }

        Node startNode = nodes[start.x, start.y];
        Node endNode = nodes[end.x, end.y];
        
        if (startNode == null || endNode == null) return null;

        // Initialise distances
        foreach (var node in nodes)
        {
            if (node != null)
            {
                node.Distance = Mathf.Infinity;
                node.Previous = null;
            }
        }
        startNode.Distance = 0;


        List<Node> openSet = new List<Node> { startNode };
        HashSet<Node> closedSet = new HashSet<Node>();

        while (openSet.Count > 0)
        {
            // Get node with lowest distance
            openSet.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            Node current = openSet[0];
            openSet.RemoveAt(0);

            if (current == endNode)
                return ReconstructPath(endNode);

            closedSet.Add(current);

            foreach (var neighbor in current.Neighbors)
            {
                if (closedSet.Contains(neighbor)) continue;

                float newDist = current.Distance + neighbor.Weight;
                if (newDist < neighbor.Distance)
                {
                    neighbor.Distance = newDist;
                    neighbor.Previous = current;
                    if (!openSet.Contains(neighbor))
                        openSet.Add(neighbor);
                }
            }
        }
        return null; // No path found
    }

    private List<Node> ReconstructPath(Node endNode)
    {
        List<Node> path = new List<Node>();
        Node current = endNode;
        while (current != null)
        {
            path.Add(current);
            current = current.Previous;
        }
        path.Reverse();
        return path;
    }
}


public class Node
{
    public int X, Y;
    public float Distance = Mathf.Infinity;
    public Node Previous;
    public float Weight = 1.0f;
    public List<Node> Neighbors = new List<Node>();

    public Node(int x, int y) { X = x; Y = y; }
    public void AddNeighbor(Node node, float weight)
    {
        Neighbors.Add(node);
        Weight = weight;
    }
}   