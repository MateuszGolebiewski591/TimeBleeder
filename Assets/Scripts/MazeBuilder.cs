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
    [SerializeField] public bool isBoard1 = false;

    [Header("Wall Animation")]
    [Tooltip("Seconds a wall takes to rise out of / sink into the floor.")]
    [SerializeField] private float wallAnimDuration = 0.35f;
    [Tooltip("Shape of the rise/sink, 0 = fully sunk, 1 = fully raised.")]
    [SerializeField] private AnimationCurve wallAnimCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private bool widthReceived = false;
    private bool heightReceived = false;
    private int width;
    private int height;
    private bool initialised = false;
    private GameAndNetworkController ganc;

    private bool[,] wallMatrix;
    private bool[,] raisableMatrix; // canBeRaised from the server (Tarjan's output)
    private readonly Dictionary<Vector2Int, GameObject> wallObjects = new Dictionary<Vector2Int, GameObject>();
    private readonly Dictionary<Vector2Int, Coroutine> wallAnimations = new Dictionary<Vector2Int, Coroutine>();
    private Material wallMat;

    private bool highlightsOn = false;
    private PlayerController.Mode highlightMode;
    private bool highlightsDirty = false;
    private readonly Dictionary<Vector2Int, GameObject> floorHighlights = new Dictionary<Vector2Int, GameObject>();
    private Material fallbackHighlightMat;

    private NetworkVariable<Vector2Int> PlayerGridVar => isBoard1 ? ganc.board1PlayerGrid : ganc.board2PlayerGrid;


    private IEnumerator Start()
    {
        ganc = FindFirstObjectByType<GameAndNetworkController>();

        ganc.board1Blocks.OnListChanged += OnBoard1BlocksChanged;
        ganc.board2Blocks.OnListChanged += OnBoard2BlocksChanged;

        ganc.width.OnValueChanged += OnWidthChanged;
        ganc.height.OnValueChanged += OnHeightChanged;
        PlayerGridVar.OnValueChanged += OnPlayerGridChanged;

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
        PlayerGridVar.OnValueChanged -= OnPlayerGridChanged;
    }

    private void OnPlayerGridChanged(Vector2Int previous, Vector2Int next)
    {
        highlightsDirty = true; // Move targets depend on where the player is
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
        raisableMatrix = new bool[width, height];
        for (int i = 0; i < blocks.Count; i++)
        {
            BlockData block = blocks[i];
            if (!InBounds(block.posWidth, block.posHeight)) continue;
            // isFloor = open floor, so the wall matrix is its inverse
            wallMatrix[block.posWidth, block.posHeight] = !block.isFloor;
            raisableMatrix[block.posWidth, block.posHeight] = block.canBeRaised;
        }

        Build(wallMatrix, transform);
        highlightsDirty = true;
    }

    private void ResetBoard()
    {
        foreach (Coroutine c in wallAnimations.Values)
            if (c != null) StopCoroutine(c);
        wallAnimations.Clear();
        wallObjects.Clear();
        floorHighlights.Clear();
        Clear();
        wallMatrix = null;
        raisableMatrix = null;
        initialised = false;
    }

    // Called whenever the server changes one block. Only moves walls if the wall state changed.
    private void AdjustBlock(BlockData block)
    {
        if (wallMatrix == null || !InBounds(block.posWidth, block.posHeight)) return;

        Vector2Int pos = new Vector2Int(block.posWidth, block.posHeight);
        raisableMatrix[pos.x, pos.y] = block.canBeRaised;
        highlightsDirty = true;

        bool shouldBeWall = !block.isFloor;
        if (wallMatrix[pos.x, pos.y] == shouldBeWall) return; // e.g. only canBeRaised changed

        wallMatrix[pos.x, pos.y] = shouldBeWall;
        if (shouldBeWall) RaiseBlock(pos);
        else LowerBlock(pos);
    }

    // Wall rises up out of the floor
    private void RaiseBlock(Vector2Int pos)
    {
        if (!wallObjects.TryGetValue(pos, out GameObject wall) || wall == null)
        {
            wall = SpawnBlock(transform, "Wall " + pos.x + "," + pos.y, WallWorldPosition(pos, 0f),
                new Vector3(tileSize, wallHeight, tileSize), wallPrefab, wallMat);
            wallObjects[pos] = wall;
        }

        wall.SetActive(true);
        AnimateWall(pos, wall, true);
    }

    // Wall sinks into the floor, then is hidden (and reused if raised again)
    private void LowerBlock(Vector2Int pos)
    {
        if (!wallObjects.TryGetValue(pos, out GameObject wall) || wall == null) return;
        AnimateWall(pos, wall, false);
    }

    private void AnimateWall(Vector2Int pos, GameObject wall, bool raise)
    {
        if (wallAnimations.TryGetValue(pos, out Coroutine running) && running != null)
            StopCoroutine(running);

        wallAnimations[pos] = StartCoroutine(AnimateWallRoutine(pos, wall, raise));
    }

    private IEnumerator AnimateWallRoutine(Vector2Int pos, GameObject wall, bool raise)
    {
        Transform t = wall.transform;
        float sunkY = WallWorldPosition(pos, 0f).y;
        float raisedY = WallWorldPosition(pos, 1f).y;

        // Start from wherever it currently is, so reversing mid-animation is smooth
        float start = Mathf.InverseLerp(sunkY, raisedY, t.position.y);
        float end = raise ? 1f : 0f;
        float duration = wallAnimDuration * Mathf.Abs(end - start);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Lerp(start, end, Mathf.Clamp01(elapsed / duration));
            t.position = WallWorldPosition(pos, wallAnimCurve.Evaluate(progress));
            yield return null;
        }

        t.position = WallWorldPosition(pos, end);

        // Hide sunk walls so they can't be clicked or collided with under the floor
        if (!raise) wall.SetActive(false);

        wallAnimations.Remove(pos);
    }

    // World position of a wall on this tile. raised: 0 = top level with the floor, 1 = standing fully up
    private Vector3 WallWorldPosition(Vector2Int pos, float raised = 1f)
    {
        Vector3 centre = transform.position;
        return centre + new Vector3(
            (pos.x - (width - 1) * 0.5f) * tileSize,
            Mathf.Lerp(-wallHeight * 0.5f, wallHeight * 0.5f, raised),
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


    public void SetHighlightMode(PlayerController.Mode mode)
    {
        highlightsOn = true;
        highlightMode = mode;
        highlightsDirty = true;
    }

    public void ClearHighlights()
    {
        highlightsOn = false;
        highlightsDirty = true;
    }

    // As the map can update a lot, refresh once per frame
    private void LateUpdate()
    {
        if (!highlightsDirty || !initialised) return;
        highlightsDirty = false;
        RefreshHighlights();
    }

    private void RefreshHighlights()
    {
        HashSet<Vector2Int> moveTargets = highlightsOn && highlightMode == PlayerController.Mode.move
            ? GetMoveTargets()
            : null;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector2Int pos = new Vector2Int(x, y);
                bool isWall = wallMatrix[x, y];

                bool floorOn = false;
                bool wallOn = false;
                if (highlightsOn)
                {
                    switch (highlightMode)
                    {
                        case PlayerController.Mode.raise: floorOn = !isWall && raisableMatrix[x, y]; break;
                        case PlayerController.Mode.lower: wallOn = isWall && IsInnerTile(pos); break;
                        case PlayerController.Mode.move:  floorOn = moveTargets.Contains(pos); break;
                    }
                }

                SetFloorHighlight(pos, floorOn);
                SetWallHighlight(pos, wallOn);
            }
        }
    }

    // Tiles the player can move to, the floor tiles right next to them.
    public HashSet<Vector2Int> GetMoveTargets()
    {
        HashSet<Vector2Int> targets = new HashSet<Vector2Int>();
        Vector2Int player = PlayerGridVar.Value;
        Vector2Int[] directions = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };

        foreach (Vector2Int dir in directions)
        {
            Vector2Int next = player + dir;
            if (InBounds(next.x, next.y) && !wallMatrix[next.x, next.y])
                targets.Add(next);
        }
        return targets;
    }

    // Outer border walls can never be lowered
    private bool IsInnerTile(Vector2Int pos)
    {
        return pos.x > 0 && pos.y > 0 && pos.x < width - 1 && pos.y < height - 1;
    }

    private void SetWallHighlight(Vector2Int pos, bool on)
    {
        if (!wallObjects.TryGetValue(pos, out GameObject wall) || wall == null) return;

        Highlight highlight = wall.GetComponentInChildren<Highlight>(true);
        if (highlight == null) return;

        if (highlight.gameObject.activeSelf != on) highlight.gameObject.SetActive(on);
        if (on) highlight.TurnOn(); // Green
    }

    private void SetFloorHighlight(Vector2Int pos, bool on)
    {
        if (!floorHighlights.TryGetValue(pos, out GameObject marker) || marker == null)
        {
            if (!on) return; // Only create markers when first needed
            marker = CreateFloorHighlight(pos);
            floorHighlights[pos] = marker;
        }

        if (marker.activeSelf != on) marker.SetActive(on);
        if (on)
        {
            Highlight highlight = marker.GetComponent<Highlight>();
            if (highlight != null) highlight.TurnOn(); // Green
        }
    }

    // Floor doesn't have tiles like walls do so we make the highlights at runtime.
    // Copies the wall prefab's Highlight object so it uses the same material.
    private GameObject CreateFloorHighlight(Vector2Int pos)
    {
        Highlight template = wallPrefab != null ? wallPrefab.GetComponentInChildren<Highlight>(true) : null;

        GameObject marker;
        if (template != null)
        {
            marker = Instantiate(template.gameObject, transform);
        }
        else
        {
            marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.transform.SetParent(transform, false);
            if (fallbackHighlightMat == null) fallbackHighlightMat = MakeDefaultMaterial(new Color(0.1f, 1f, 0.1f));
            marker.GetComponent<Renderer>().sharedMaterial = fallbackHighlightMat;
        }

        const float floorTop = 0.05f; // Half the floor thickness
        const float markerThickness = 0.02f;

        marker.name = "Highlight " + pos.x + "," + pos.y;
        marker.SetActive(false);
        Vector3 wallPos = WallWorldPosition(pos);
        marker.transform.position = new Vector3(wallPos.x, transform.position.y + floorTop + markerThickness * 0.5f, wallPos.z);
        marker.transform.localRotation = Quaternion.identity;
        marker.transform.localScale = new Vector3(tileSize, markerThickness, tileSize);
        return marker;
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
