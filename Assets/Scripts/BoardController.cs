using UnityEngine;

public class BoardController : MonoBehaviour
{
    [Header("Characters")]
    public GameObject player;
    public GameObject angel;

    private MazeController mazeController;

    [Header("Angel Settings")]
    public float angelMoveSpeed = 1f;
    public float spawnDistanceFromPlayer = 5f;
    private AngelMovement angelMovement;
    private float angelMoveTimer;

    void Awake()
    {
        mazeController = GetComponent<MazeController>();
        angelMovement = angel.GetComponent<AngelMovement>();
    }

    void Start()
    {
        if (mazeController == null)
        {
            Debug.LogError("MazeController component not found on the GameObject.");
            return;
        }

        if(spawnDistanceFromPlayer > mazeController.GetMazeSize())
            spawnDistanceFromPlayer = mazeController.GetMazeSize();
        if(spawnDistanceFromPlayer < 0f)
            spawnDistanceFromPlayer = 0f;

        mazeController.GenerateMaze();

        if (player != null)
            SetPlayerStartPosition(player.transform.position);

        if (angel != null)
            SetAngelStartPosition(angel.transform.position, player.transform.position);
    }

    void FixedUpdate()
    {
        if (angelMovement == null || mazeController == null || angel == null || player == null || angelMoveSpeed <= 0f)
            return;

        angelMoveTimer += Time.fixedDeltaTime;
        float moveInterval = 1f / angelMoveSpeed;

        if (angelMoveTimer < moveInterval)
            return;

        angelMoveTimer = 0f;
        Vector2Int angelGridPosition = mazeController.WorldToMazeGrid(angel.transform.position);
        Vector2Int playerGridPosition = mazeController.WorldToMazeGrid(player.transform.position);

        angelMovement.MoveAngel(angelGridPosition, playerGridPosition, mazeController.MazeGridToWorld);
    }

    // Only raises the wall if the angel can still reach the player
    public bool TryRaiseWall(Vector2Int position)
    {
        Vector2Int angelGridPosition = mazeController.WorldToMazeGrid(angel.transform.position);
        Vector2Int playerGridPosition = mazeController.WorldToMazeGrid(player.transform.position);

        if (!mazeController.QueryWallRaise(position, angelGridPosition, playerGridPosition))
            return false;

        mazeController.RaiseWall(position);
        return true;
    }

    private void SetPlayerStartPosition(Vector3 position)
    {
        mazeController.FindRandomWalkablePosition(out Vector2Int randomPosition);
        Vector3 playerWorldPosition = mazeController.MazeGridToWorld(randomPosition, mazeController.GetFloor());

        if (player != null) player.transform.position = new Vector3(playerWorldPosition.x, position.y, playerWorldPosition.z);
        else Debug.LogError("Player GameObject reference is not set.");
    }

    private void SetAngelStartPosition(Vector3 position, Vector3 playerPosition)
    {
        // Find random position for the angel that is walkable and not too close to the player
        Vector2Int playerGridPosition = mazeController.WorldToMazeGrid(playerPosition);
        mazeController.FindRandomWalkablePosition(playerGridPosition, spawnDistanceFromPlayer, out Vector2Int randomPosition);

        Vector3 angelWorldPosition = mazeController.MazeGridToWorld(randomPosition, mazeController.GetFloor());

        if (angel != null) angel.transform.position = new Vector3(angelWorldPosition.x, position.y, angelWorldPosition.z);
        else Debug.LogError("Angel GameObject reference is not set.");
    }
}
