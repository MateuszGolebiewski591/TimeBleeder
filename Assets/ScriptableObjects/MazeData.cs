using UnityEngine;

[CreateAssetMenu(fileName = "MazeData", menuName = "Scriptable Objects/Maze Data")]
public class MazeData : ScriptableObject
{
    [Header("Maze Appearance")]
    [Min(1)] public int width = 15;
    [Min(1)] public int height = 15;

    [Header("Appearance")]
    public GameObject wallPrefab;
    public Material wallMaterial;
    public Material floorMaterial;

    [Min(0.01f)] public float tileSize = 1f;
    [Min(0.01f)] public float wallHeight = 0.5f;
    [SerializeField] private GameObject centreAnchorPrefab;
    public Vector3? anchorPosition => centreAnchorPrefab != null ? centreAnchorPrefab.transform.position : (Vector3?)null;

    // Runtime state of the generated maze, set by MazeController.GenerateMaze
    public Transform Root { get; private set; }
    public Vector3 Centre { get; private set; }
    private bool[,] walls;
    private bool[,] floor;

    public bool IsGenerated => Root != null && floor != null;

    public void SetInstance(Transform root, Vector3 centre)
    {
        Root = root;
        Centre = centre;
        walls = null;
        floor = null;
    }

    public bool[,] GetWallData()
    {
        return walls;
    }

    public bool[,] GetFloorData()
    {
        return floor;
    }

    public void SetMazeData(bool[,] wallData, bool[,] floorData)
    {
        walls = wallData;
        floor = floorData;
    }
}
