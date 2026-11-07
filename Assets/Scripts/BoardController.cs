using UnityEngine;

public class BoardController : MonoBehaviour
{
    [Header("Characters")]
    public GameObject player;
    public GameObject angel;

    [Header("Maze")]
    public GameObject centreAnchor;
    private MazeGenerator mazeGenerator;
    private float tileSize = 1f;

    [Header("Angel Settings")]
    public float angelMoveSpeed = 1f;
    private AngelMovement angelMovement;
    private float angelMoveTimer;

    void Start()
    {
        mazeGenerator = GetComponent<MazeGenerator>();
        angelMovement = angel.GetComponent<AngelMovement>();

        if (mazeGenerator != null)
        {
            if (centreAnchor != null)
                mazeGenerator.centrePosition = centreAnchor.gameObject.transform.position;
            else
                mazeGenerator.centrePosition = this.transform.position;

            tileSize = mazeGenerator.tileSize;
        }

        if (mazeGenerator != null) mazeGenerator.Generate();
        else Debug.LogError("MazeGenerator component not found on the GameObject.");

        if (mazeGenerator != null && player != null)
            SetPlayerStartPosition(mazeGenerator.GetFloorData(), player.transform.position);

        if (mazeGenerator != null && angel != null)
            SetAngelStartPosition(mazeGenerator.GetFloorData(), angel.transform.position);
    }

    void FixedUpdate()
    {
        if (angelMovement == null || mazeGenerator == null || angel == null || player == null || angelMoveSpeed <= 0f)
            return;

        angelMoveTimer += Time.fixedDeltaTime;
        float moveInterval = 1f / angelMoveSpeed;

        if (angelMoveTimer >= moveInterval)
        {
            angelMoveTimer = 0f;
            bool[,] mazeFloor = mazeGenerator.GetFloorData();

            angelMovement.SetMazeData(mazeFloor);

            Vector3 angelPosition = angel.transform.position;
            Vector3 playerPosition = player.transform.position;

            Vector2Int angelGridPosition = WorldToMazeGrid(angelPosition, mazeFloor);
            Vector2Int playerGridPosition = WorldToMazeGrid(playerPosition, mazeFloor);

            angelMovement.MoveAngel(angelGridPosition, playerGridPosition, MazeGridToWorld);
        }
    }


    Vector2Int WorldToMazeGrid(Vector3 worldPosition, bool[,] mazeFloor)
    {
        Vector3 mazeCentre = centreAnchor != null ? centreAnchor.transform.position : Vector3.zero;
        float originX = mazeCentre.x - mazeFloor.GetLength(0) * tileSize * 0.5f;
        float originZ = mazeCentre.z - mazeFloor.GetLength(1) * tileSize * 0.5f;

        int x = Mathf.FloorToInt((worldPosition.x - originX) / tileSize);
        int z = Mathf.FloorToInt((worldPosition.z - originZ) / tileSize);
        return new Vector2Int(
            Mathf.Clamp(x, 0, mazeFloor.GetLength(0) - 1),
            Mathf.Clamp(z, 0, mazeFloor.GetLength(1) - 1));
    }

    Vector3 MazeGridToWorld(Vector2Int gridPosition, bool[,] mazeFloor)
    {
        Vector3 mazeCentre = centreAnchor != null ? centreAnchor.transform.position : Vector3.zero;
        float originX = mazeCentre.x - mazeFloor.GetLength(0) * tileSize * 0.5f;
        float originZ = mazeCentre.z - mazeFloor.GetLength(1) * tileSize * 0.5f;

        float worldX = originX + (gridPosition.x + 0.5f) * tileSize;
        float worldZ = originZ + (gridPosition.y + 0.5f) * tileSize;
        return new Vector3(worldX, 0f, worldZ);
    }

    private void SetPlayerStartPosition(bool[,] mazeFloor, Vector3 position)
    {
        FindRandomWalkablePosition(mazeFloor, out Vector2Int randomPosition);
        Vector3 playerWorldPosition = MazeGridToWorld(randomPosition, mazeGenerator.GetFloorData());

        if (player != null) player.transform.position = new Vector3(playerWorldPosition.x, position.y, playerWorldPosition.z);
        else Debug.LogError("Player GameObject reference is not set.");
    }

    private void SetAngelStartPosition(bool[,] mazeFloor, Vector3 position)
    {
        FindRandomWalkablePosition(mazeFloor, out Vector2Int randomPosition);
        Vector3 angelWorldPosition = MazeGridToWorld(randomPosition, mazeGenerator.GetFloorData());
        
        if (angel != null) angel.transform.position = new Vector3(angelWorldPosition.x, position.y, angelWorldPosition.z);
        else Debug.LogError("Angel GameObject reference is not set.");
    }

    private void FindRandomWalkablePosition(bool[,] mazeFloor, out Vector2Int randomPosition)
    {
        int width = mazeFloor.GetLength(0);
        int height = mazeFloor.GetLength(1);

        int x;
        int y;

        do
        {
            x = Random.Range(0, width);
            y = Random.Range(0, height);
        } while (!mazeFloor[x, y]);

        randomPosition = new Vector2Int(x, y);
    }
}
