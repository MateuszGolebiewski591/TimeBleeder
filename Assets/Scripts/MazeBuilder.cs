using UnityEngine;

public class MazeBuilder : MonoBehaviour
{
    public bool buildFloor = true;

    [Header("Appearance")]
    [SerializeField] private float tileSize = 1f;
    [SerializeField] private float wallHeight = 0.5f; 
    [SerializeField] private GameObject wallPrefab; 
    [SerializeField] private Material material; 
    [SerializeField] private Material floorMaterial;

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

    private void Build(bool[,] walls, Transform buildRoot, Vector3 buildCentre)
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
                    buildRoot,
                    "Wall " + x + "," + y,
                    buildCentre + new Vector3(
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
                buildRoot,
                "Floor",
                buildCentre,
                new Vector3(gridW * tileSize, floorThickness, gridH * tileSize), null, floorMat);
        }
    }

    public void SpawnBlock(Transform parent, string blockName, Vector3 worldPos, Vector3 scale, GameObject prefab, Material mat)
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
    }
}
