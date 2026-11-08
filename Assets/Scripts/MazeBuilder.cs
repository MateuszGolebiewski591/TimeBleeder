using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

public class MazeBuilder : MonoBehaviour
{
    public bool buildFloor = true;

    [Header("Appearance")]
    [SerializeField] private float tileSize = 1f;
    [SerializeField] private float wallHeight = 0.5f;
    [SerializeField] private GameObject wallPrefab;
    [SerializeField] private Material material;
    [SerializeField] private Material floorMaterial;
    [SerializeField] private bool isBoard1 = false;

    private bool widthReceived = false;
    private bool heightReceived = false;
    private int width;
    private int height;
    private bool initialised = false;
    private GameAndNetworkController ganc;

    private bool[,] wallMatrix;
    private readonly Dictionary<Vector2Int, GameObject> wallObjects = new Dictionary<Vector2Int, GameObject>();
    private Material wallMat;


    private IEnumerator Start()
    {
        ganc = FindFirstObjectByType<GameAndNetworkController>();

        ganc.board1Blocks.OnListChanged += OnBoard1BlocksChanged;
        ganc.board2Blocks.OnListChanged += OnBoard2BlocksChanged;

        ganc.width.OnValueChanged += OnWidthChanged;
        ganc.height.OnValueChanged += OnHeightChanged;

        // On clients the initial values arrive when ganc spawns, without raising
        // OnValueChanged/OnListChanged, so wait for the spawn before reading them.
        while (!ganc.IsSpawned) yield return null;

        // Read their current values in case they arrived before Start.
        width = ganc.width.Value;
        height = ganc.height.Value;

        widthReceived = width > 0;
        heightReceived = height > 0;

        TryInitialise();
    }

    private void OnDestroy()
    {
        if (ganc == null) return;
        ganc.board1Blocks.OnListChanged -= OnBoard1BlocksChanged;
        ganc.board2Blocks.OnListChanged -= OnBoard2BlocksChanged;
        ganc.width.OnValueChanged -= OnWidthChanged;
        ganc.height.OnValueChanged -= OnHeightChanged;
    }

    private void OnBoard1BlocksChanged(NetworkListEvent<BlockData> changeEvent)
    {
        OnBlocksChanged(changeEvent, true);
    }

    private void OnBoard2BlocksChanged(NetworkListEvent<BlockData> changeEvent)
    {
        OnBlocksChanged(changeEvent, false);
    }

    private void OnBlocksChanged(NetworkListEvent<BlockData> changeEvent, bool fromBoard1)
    {
        if (!initialised)
        {
            // Either list filling up can complete initialisation
            TryInitialise();
            return;
        }

        // Both builders hear both lists, only react to our own board
        if (fromBoard1 != isBoard1) return;

        switch (changeEvent.Type)
        {
            case NetworkListEvent<BlockData>.EventType.Value:
            case NetworkListEvent<BlockData>.EventType.Add:
            case NetworkListEvent<BlockData>.EventType.Insert:
                AdjustBlock(changeEvent.Value);
                break;

            case NetworkListEvent<BlockData>.EventType.Clear:
                // Board is being regenerated: wipe it and rebuild once the list refills
                ResetBoard();
                break;

            case NetworkListEvent<BlockData>.EventType.Full:
                ResetBoard();
                TryInitialise();
                break;
        }
    }

    private void OnWidthChanged(int previous, int next)
    {
        width = next;
        widthReceived = next > 0;
        TryInitialise();
    }

    private void OnHeightChanged(int previous, int next)
    {
        height = next;
        heightReceived = next > 0;
        TryInitialise();
    }

    private void TryInitialise()
    {
        if (initialised || !widthReceived || !heightReceived)
            return;

        int expectedLength = width * height;

        if (ganc.board1Blocks.Count < expectedLength ||
            ganc.board2Blocks.Count < expectedLength)
            return;

        // We have received all the required data.
        initialised = true;

        if (isBoard1) InitialiseBoard1();
        else InitialiseBoard2();
    }

    private void InitialiseBoard1() {
        InitialiseFromBlocks(ganc.board1Blocks);
    }

    private void InitialiseBoard2() {
        InitialiseFromBlocks(ganc.board2Blocks);
    }

    // Fills wallMatrix (local record of what is shown) from a board's list and builds it
    private void InitialiseFromBlocks(NetworkList<BlockData> blocks)
    {
        wallMatrix = new bool[width, height];
        for (int i = 0; i < blocks.Count; i++)
        {
            BlockData block = blocks[i];
            if (!InBounds(block.posWidth, block.posHeight)) continue;
            // isFloor = open floor, so the wall matrix is its inverse
            wallMatrix[block.posWidth, block.posHeight] = !block.isFloor;
        }

        Build(wallMatrix, transform);
    }

    private void ResetBoard()
    {
        wallObjects.Clear();
        Clear();
        wallMatrix = null;
        initialised = false;
    }

    // Called whenever the server changes one block. Only acts if the wall state changed.
    private void AdjustBlock(BlockData block)
    {
        if (wallMatrix == null || !InBounds(block.posWidth, block.posHeight)) return;

        Vector2Int pos = new Vector2Int(block.posWidth, block.posHeight);
        bool shouldBeWall = !block.isFloor;
        if (wallMatrix[pos.x, pos.y] == shouldBeWall) return; // e.g. only canBeRaised changed

        wallMatrix[pos.x, pos.y] = shouldBeWall;
        if (shouldBeWall) RaiseBlock(pos);
        else LowerBlock(pos);
    }

    // Instantly adds a wall. Animation can be added here later.
    private void RaiseBlock(Vector2Int pos)
    {
        if (wallObjects.TryGetValue(pos, out GameObject existing) && existing != null) return;

        wallObjects[pos] = SpawnBlock(transform, "Wall " + pos.x + "," + pos.y, WallWorldPosition(pos),
            new Vector3(tileSize, wallHeight, tileSize), wallPrefab, wallMat);
    }

    // Instantly removes a wall. Animation can be added here later.
    private void LowerBlock(Vector2Int pos)
    {
        if (!wallObjects.TryGetValue(pos, out GameObject wall)) return;

        wallObjects.Remove(pos);
        if (wall != null) Destroy(wall);
    }

    // World position of a standing wall on this tile
    private Vector3 WallWorldPosition(Vector2Int pos)
    {
        Vector3 centre = transform.position;
        return centre + new Vector3(
            (pos.x - (width - 1) * 0.5f) * tileSize,
            wallHeight * 0.5f,
            (pos.y - (height - 1) * 0.5f) * tileSize);
    }

    private bool InBounds(int x, int y)
    {
        return x >= 0 && x < width && y >= 0 && y < height;
    }

    // World point -> grid tile.
    public bool TryGetGridPosition(Vector3 worldPoint, out Vector2Int gridPosition)
    {
        gridPosition = default;
        if (!initialised) return false;

        Vector3 local = worldPoint - transform.position;
        int x = Mathf.FloorToInt(local.x / tileSize + width * 0.5f);
        int y = Mathf.FloorToInt(local.z / tileSize + height * 0.5f);
        if (!InBounds(x, y)) return false;

        gridPosition = new Vector2Int(x, y);
        return true;
    }

    public bool IsWall(Vector2Int gridPosition)
    {
        return wallMatrix != null && InBounds(gridPosition.x, gridPosition.y) && wallMatrix[gridPosition.x, gridPosition.y];
    }


    private Material MakeDefaultMaterial(Color color)
    {
        GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Material m = new Material(temp.GetComponent<Renderer>().sharedMaterial);
        m.color = color;
        if (Application.isPlaying) Destroy(temp); else DestroyImmediate(temp);
        return m;
    }

    public void Clear()
    {
        Clear(transform);
    }

    public void Clear(Transform root)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            GameObject child = root.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }
    }

    private void Build(bool[,] walls, Transform buildRoot)
    {
        Vector3 buildCentre = buildRoot.gameObject.transform.position;
        int gridW = walls.GetLength(0);
        int gridH = walls.GetLength(1);

        wallMat = material != null ? material : MakeDefaultMaterial(new Color(0.2f, 0.28f, 0.4f));
        Material floorMat = floorMaterial != null ? floorMaterial : MakeDefaultMaterial(new Color(0.85f, 0.85f, 0.8f));

        for (int x = 0; x < gridW; x++)
        {
            for (int y = 0; y < gridH; y++)
            {
                if (!walls[x, y]) continue;
                Vector2Int pos = new Vector2Int(x, y);
                wallObjects[pos] = SpawnBlock(
                    buildRoot,
                    "Wall " + pos.x + "," + pos.y,
                    WallWorldPosition(pos),
                    new Vector3(tileSize, wallHeight, tileSize),
                    wallPrefab, wallMat);
            }
        }

        if (buildFloor)
        {
            const float floorThickness = 0.1f;
            SpawnBlock(
                buildRoot,
                "Floor",
                buildCentre,
                new Vector3(gridW * tileSize, floorThickness, gridH * tileSize), null, floorMat);
        }
    }

    public GameObject SpawnBlock(Transform parent, string blockName, Vector3 worldPos, Vector3 scale, GameObject prefab, Material mat)
    {
        GameObject go = prefab != null
            ? Instantiate(prefab, parent)
            : GameObject.CreatePrimitive(PrimitiveType.Cube);

        go.name = blockName;
        go.transform.SetParent(parent, false);
        go.transform.position = worldPos;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = scale;

        if (prefab == null && mat != null)
            go.GetComponent<Renderer>().sharedMaterial = mat;

        return go;
    }
}
