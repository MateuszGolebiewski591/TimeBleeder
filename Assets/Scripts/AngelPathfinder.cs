using UnityEngine;
using System.Collections.Generic;

// Server-side angel logic
public static class AngelPathfinder
{
    public static Vector2Int NextStep(bool[,] floor, Vector2Int from, Vector2Int to)
    {
        if (floor == null) return from;

        Node[,] nodes = BuildGraph(floor);
        List<Node> path = FindPath(nodes, from, to);

        if (path != null && path.Count > 1)
            return new Vector2Int(path[1].X, path[1].Y);
        return from;
    }

    // Need nodes as the 2D array doesn't work with Dijkstra's
    private static Node[,] BuildGraph(bool[,] walkableGrid)
    {
        int width = walkableGrid.GetLength(0);
        int height = walkableGrid.GetLength(1);
        Node[,] nodes = new Node[width, height];

        // Create nodes only for walkable tiles
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (walkableGrid[x, y])
                    nodes[x, y] = new Node(x, y);
            }
        }

        // Connect only after every walkable node has been created.
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (walkableGrid[x, y])
                    ConnectNeighbors(nodes, x, y, width, height, walkableGrid);
            }
        }

        return nodes;
    }

    private static void ConnectNeighbors(Node[,] nodes, int x, int y, int width, int height, bool[,] walkableGrid)
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

    private static List<Node> FindPath(Node[,] nodes, Vector2Int start, Vector2Int end)
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

    private static List<Node> ReconstructPath(Node endNode)
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
