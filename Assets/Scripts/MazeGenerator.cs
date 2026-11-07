using System.Collections.Generic;
using UnityEngine;

// Credits:
// Inspired by https://github.com/Apolok99/Maze-Random-Generator-Unity/tree/main

public class MazeGenerator : MonoBehaviour
{
    [SerializeField] MazeData mazeData;

    [Header("Maze size (number of passage cells)")]
    [Min(1)] public int width = 15;
    [Min(1)] public int height = 15;

    [Header("Appearance")]
    private GameObject wallPrefab => mazeData.wallPrefab;

    private float tileSize => mazeData.tileSize;
    private float wallHeight => mazeData.wallHeight;

    public bool buildFloor = true;

    public Material material;
    public Material floorMaterial;

    [Header("Camera")]
    public bool frameMainCamera = true; // Move camera above the board

    [Header("Randomness")]
    public bool useSeed = false;
    public int seed = 0;

    [Header("Behaviour")]
    public bool generateOnStart = false;

    public Vector3 centrePosition;

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

    [ContextMenu("Generate")]
    public void Generate()
    {
        Clear();
        System.Random rng = useSeed ? new System.Random(seed) : new System.Random();
        Carve(rng);
        BuildFloorMask();
        Build();
        mazeData.SetMazeData(walls, floor);
        // if (frameMainCamera && Application.isPlaying) FrameCamera();
    }

    private void FrameCamera()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        float sizeX = (2 * width + 1) * tileSize;
        float sizeZ = (2 * height + 1) * tileSize;
        Vector3 centre = transform.TransformPoint(new Vector3((sizeX - tileSize) * 0.5f, 0f, (sizeZ - tileSize) * 0.5f));

        float fovRad = cam.fieldOfView * Mathf.Deg2Rad;
        // The vertical field of view must cover the maze's depth, and the
        // horizontal one (vertical * aspect) must cover its width.
        float needed = Mathf.Max(sizeZ, sizeX / cam.aspect) * 0.5f;
        float h = needed / Mathf.Tan(fovRad * 0.5f) * 1.1f + wallHeight;

        cam.orthographic = false;
        cam.transform.position = centre + Vector3.up * h;
        cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }

    private Material MakeDefaultMaterial(Color color)
    {
        GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Material m = new Material(temp.GetComponent<Renderer>().sharedMaterial);
        m.color = color;
        if (Application.isPlaying) Destroy(temp); else DestroyImmediate(temp);
        return m;
    }

    [ContextMenu("Clear")]
    public void Clear()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }
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

    private void Build()
    {
        int gridW = walls.GetLength(0);
        int gridH = walls.GetLength(1);

        Material wallMat = material != null ? material : MakeDefaultMaterial(new Color(0.2f, 0.28f, 0.4f));
        Material floorMat = floorMaterial != null ? floorMaterial : MakeDefaultMaterial(new Color(0.85f, 0.85f, 0.8f));
        int wallCount = 0;
        
        for (int x = 0; x < gridW; x++)
        {
            for (int y = 0; y < gridH; y++)
            {
                if (!walls[x, y]) continue;
                SpawnBlock(
                    "Wall " + x + "," + y,
                    centrePosition + new Vector3(
                        (x - (gridW - 1) * 0.5f) * tileSize,
                        wallHeight * 0.5f,
                        (y - (gridH - 1) * 0.5f) * tileSize),
                    new Vector3(tileSize, wallHeight, tileSize),
                    wallPrefab, wallMat);
                wallCount++;
            }
        }

        if (buildFloor)
        {
            const float floorThickness = 0.1f;
            SpawnBlock(
                "Floor",
                new Vector3(0,0,0),
                new Vector3(gridW * tileSize, floorThickness, gridH * tileSize), null, floorMat);
        }
    }

    public void SpawnBlock(string blockName, Vector3 worldPos, Vector3 scale, GameObject prefab, Material mat)
    {
        GameObject go = prefab != null
            ? Instantiate(prefab, transform)
            : GameObject.CreatePrimitive(PrimitiveType.Cube);

        go.name = blockName;
        go.transform.SetParent(transform, false);
        go.transform.position = worldPos + centrePosition;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = scale;

        if (prefab == null && mat != null)
            go.GetComponent<Renderer>().sharedMaterial = mat;
    }
}