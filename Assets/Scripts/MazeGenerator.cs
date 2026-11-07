using System.Collections.Generic;
using UnityEngine;

// Credits:
// Inspired by https://github.com/Apolok99/Maze-Random-Generator-Unity/tree/main

public class MazeGenerator : MonoBehaviour
{
    private MazeData mazeData;
    public int width { get; private set; }
    public int height { get; private set; }

    [Header("Randomness")]
    public bool useSeed = false;
    public int seed = 0;

    [Header("Behaviour")]
    public bool generateOnStart = false;

    // Where the maze currently being built goes
    private Transform buildRoot;
    private Vector3 buildCentre;

    private bool[,] walls; // true = wall, false = open passage. Indexed [x, y].
    private bool[,] floor; // true = open floor, the inverse of walls

    private static readonly Vector2Int[] Directions =
    {
        new Vector2Int(0, 1),   // north
        new Vector2Int(1, 0),   // east
        new Vector2Int(0, -1),  // south
        new Vector2Int(-1, 0),  // west
    };

    private void Start()
    {
        if (generateOnStart) Generate();
    }

    public void Generate()
    {
        Generate(mazeData, transform, transform.position);
    }

    // Builds the maze as children of root, centred on centre
    public void Generate(MazeData mazeData, Transform root, Vector3 centre)
    {
        if (mazeData == null)
        {
            Debug.LogError("MazeGenerator: no MazeData assigned.");
            return;
        }

        this.mazeData = mazeData;
        buildRoot = root;
        buildCentre = centre;
        SetVariables();
        System.Random rng = useSeed ? new System.Random(seed) : new System.Random();
        Carve(rng);
        BuildFloorMask();
        mazeData.SetMazeData(walls, floor);
    }

    private void SetVariables()
    {
        width = mazeData.width;
        height = mazeData.height;
    }

    // Recursive backtracker algorithm
    private void Carve(System.Random rng)
    {
        int gridW = 2 * width + 1;
        int gridH = 2 * height + 1;

        walls = new bool[gridW, gridH];
        for (int x = 0; x < gridW; x++)
            for (int y = 0; y < gridH; y++)
                walls[x, y] = true;

        bool[,] visited = new bool[width, height];
        Stack<Vector2Int> stack = new Stack<Vector2Int>();
        List<Vector2Int> options = new List<Vector2Int>(4);

        Vector2Int start = new Vector2Int(0, 0);
        visited[start.x, start.y] = true;
        walls[2 * start.x + 1, 2 * start.y + 1] = false;
        stack.Push(start);

        while (stack.Count > 0)
        {
            Vector2Int current = stack.Peek();

            options.Clear();
            foreach (Vector2Int d in Directions)
            {
                Vector2Int n = current + d;
                if (n.x >= 0 && n.x < width && n.y >= 0 && n.y < height && !visited[n.x, n.y])
                    options.Add(n);
            }

            if (options.Count == 0)
            {
                stack.Pop(); // dead end: backtrack
                continue;
            }

            Vector2Int next = options[rng.Next(options.Count)];
            visited[next.x, next.y] = true;

            // Open the wall tile between the two cells, then the new cell itself.
            int cx = 2 * current.x + 1, cy = 2 * current.y + 1;
            int nx = 2 * next.x + 1, ny = 2 * next.y + 1;
            walls[(cx + nx) / 2, (cy + ny) / 2] = false;
            walls[nx, ny] = false;

            stack.Push(next);
        }
    }

    private void BuildFloorMask()
    {
        int gridW = walls.GetLength(0);
        int gridH = walls.GetLength(1);
        floor = new bool[gridW, gridH];

        for (int x = 0; x < gridW; x++)
            for (int y = 0; y < gridH; y++)
                floor[x, y] = !walls[x, y];
    }
}