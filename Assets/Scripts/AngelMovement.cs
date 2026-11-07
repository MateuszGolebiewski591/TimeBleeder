using UnityEngine;
using System.Collections.Generic; 

// Uses Dijkstra's algorithm to find the shortest path to the player, stepping one tile at a time

public class AngelMovement : MonoBehaviour
{
    private bool[,] mazeFloor; // true = open floor, the inverse of walls
    private Node[,] nodes; // Need nodes as the 2D array doesn't work with Dijkstra's
    
    private Vector2Int currentPosition; 
    private Vector2Int targetPosition;

    public void SetMazeData(bool[,] floor)
    {
        mazeFloor = floor;
        InitialiseGrid(floor);
    }

    public Vector2Int MoveTowardsPlayer(Vector2Int angelPosition, Vector2Int playerPosition)
    {
        currentPosition = angelPosition;
        targetPosition = playerPosition;

        List<Node> path = FindPath(currentPosition, targetPosition);
        return new Vector2Int(moveToNextTile(path).x, moveToNextTile(path).y);
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


    public void InitialiseGrid(bool[,] grid)
    {
        bool[,] walkableGrid = grid;
        int width = grid.GetLength(0);
        int height = grid.GetLength(1);
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

        for (int i = 0; i < 4; i++)
        {
            int nx = x + dx[i];
            int ny = y + dy[i];

            if (nx >= 0 && nx < width && ny >= 0 && ny < height)
            {
                if (walkableGrid[nx, ny])
                {
                    nodes[x, y].AddNeighbor(nodes[nx, ny], 1.0f); // Cost = 1
                }
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