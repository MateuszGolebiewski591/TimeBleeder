using UnityEngine;
using System.Collections.Generic;

public class BoardController : MonoBehaviour
{
    [Header("Characters")]
    public GameObject player;
    public GameObject angel;

    [Header("Maze")]
    [SerializeField] MazeData mazeData;
    public GameObject centreAnchor;
    private MazeGenerator mazeGenerator;
    private float tileSize = 1f;
    private MazeUpdate mazeUpdate;

    [Header("Angel Settings")]
    public float angelMoveSpeed = 1f;
    public float spawnDistanceFromPlayer = 5f;
    private AngelMovement angelMovement;
    private float angelMoveTimer;

    void Awake()
    {
        mazeUpdate = GetComponent<MazeUpdate>();
        mazeGenerator = GetComponent<MazeGenerator>();
        angelMovement = angel.GetComponent<AngelMovement>();
    } 
    void Start()
    {
        if(spawnDistanceFromPlayer > Mathf.Max(mazeGenerator.width, mazeGenerator.height))
            spawnDistanceFromPlayer = Mathf.Max(mazeGenerator.width, mazeGenerator.height);
        if(spawnDistanceFromPlayer < 0f)
            spawnDistanceFromPlayer = 0f;

        if (mazeGenerator != null)
        {
            if (centreAnchor != null)
                mazeGenerator.centrePosition = centreAnchor.gameObject.transform.position;
            else
                mazeGenerator.centrePosition = this.transform.position;

            tileSize = mazeData.tileSize;
        }

        if (mazeGenerator != null) mazeGenerator.Generate();
        else Debug.LogError("MazeGenerator component not found on the GameObject.");

        bool[,] mazeFloor = mazeData.GetFloorData();
        if (mazeGenerator != null && player != null)
            SetPlayerStartPosition(mazeFloor, player.transform.position);

        if (mazeGenerator != null && angel != null)
            SetAngelStartPosition(mazeFloor, angel.transform.position, player.transform.position);
    }

    void FixedUpdate()
    {
        if (angelMovement == null || mazeGenerator == null || angel == null || player == null || angelMoveSpeed <= 0f)
            return;

        angelMoveTimer += Time.fixedDeltaTime;
        float moveInterval = 1f / angelMoveSpeed;

        if (angelMoveTimer < moveInterval)
            return;

        angelMoveTimer = 0f;
        bool[,] mazeFloor = mazeData.GetFloorData();
        Vector2Int angelGridPosition = WorldToMazeGrid(angel.transform.position, mazeFloor);
        Vector2Int playerGridPosition = WorldToMazeGrid(player.transform.position, mazeFloor);

        // Add at most one wall per angel move, without trapping either character.
        // -- Random wall testing
        // if (mazeUpdate != null)
        // {
        //     List<Vector2Int> wallCandidates = new List<Vector2Int>();
        //     for (int x = 0; x < mazeFloor.GetLength(0); x++)
        //     {
        //         for (int y = 0; y < mazeFloor.GetLength(1); y++)
        //         {
        //             if (mazeFloor[x, y]
        //                 && new Vector2Int(x, y) != angelGridPosition
        //                 && new Vector2Int(x, y) != playerGridPosition)
        //             {
        //                 wallCandidates.Add(new Vector2Int(x, y));
        //             }
        //         }
        //     }

        //     if (wallCandidates.Count > 0)
        //         mazeUpdate.RaiseWall(wallCandidates[Random.Range(0, wallCandidates.Count)]);
        // }

        mazeFloor = mazeData.GetFloorData();
        angelGridPosition = WorldToMazeGrid(angel.transform.position, mazeFloor);
        playerGridPosition = WorldToMazeGrid(player.transform.position, mazeFloor);

        angelMovement.MoveAngel(angelGridPosition, playerGridPosition, MazeGridToWorld);
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
        Vector3 playerWorldPosition = MazeGridToWorld(randomPosition, mazeFloor);

        if (player != null) player.transform.position = new Vector3(playerWorldPosition.x, position.y, playerWorldPosition.z);
        else Debug.LogError("Player GameObject reference is not set.");
    }

    private void SetAngelStartPosition(bool[,] mazeFloor, Vector3 position, Vector3 playerPosition)
    {
        // Find random position for the angel that is walkable and not too close to the player
        FindRandomWalkablePosition(mazeFloor, out Vector2Int randomPosition);

        if (Vector2Int.Distance(randomPosition, WorldToMazeGrid(playerPosition, mazeFloor)) < spawnDistanceFromPlayer)
        {
            // If the random position is too close to the player, find another one
            SetAngelStartPosition(mazeFloor, position, playerPosition);
            return;
        }

        Vector3 angelWorldPosition = MazeGridToWorld(randomPosition, mazeFloor);
        
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
