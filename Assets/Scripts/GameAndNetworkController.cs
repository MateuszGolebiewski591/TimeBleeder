using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using System.Collections;
using System;

public class GameAndNetworkController : NetworkBehaviour
{
    [SerializeField] private Transform playerPrefab;
    [SerializeField] private string gameSceneName;
    [SerializeField] private float timeLimit = 90f;
    [SerializeField] public BoardController board1;
    [SerializeField] public BoardController board2;

    [SerializeField] private MazeData board1MazeData;
    [SerializeField] private MazeData board2MazeData;

    public MazeController mazeController;
    public MazeController Maze => mazeController;

    private PlayerController player;

    [Header("Angel Settings")]
    public float angelMoveSpeed = 1f;
    public float spawnDistanceFromPlayer = 5f;

    private Dictionary<ulong, bool> playerReady = new Dictionary<ulong, bool>();
    private Dictionary<Vector2Int, List<Vector2Int>> floorGraph1 = new Dictionary<Vector2Int, List<Vector2Int>>();
    private Dictionary<Vector2Int, List<Vector2Int>> wallGraph1 = new Dictionary<Vector2Int, List<Vector2Int>>();

    private Dictionary<Vector2Int, List<Vector2Int>> floorGraph2 = new Dictionary<Vector2Int, List<Vector2Int>>();
    private Dictionary<Vector2Int, List<Vector2Int>> wallGraph2 = new Dictionary<Vector2Int, List<Vector2Int>>();


    public enum GameState {
        ReadyUp,
        CountDown,
        GameActive
    }

    public NetworkVariable<GameState> state;
    public NetworkVariable<float> GlobalTime;
    public NetworkVariable<float> Player1TimeGained;
    public NetworkVariable<float> Player2TimeGained;

    public NetworkList<BlockData> Board1Blocks = new NetworkList<BlockData>();
    public NetworkList<BlockData> Board2Blocks = new NetworkList<BlockData>();

    public NetworkVariable<bool> player1Ready;
    public NetworkVariable<bool> player2Ready;
    public NetworkVariable<int> countDownTime;

    // Controls when the angel can next move
    private float board1AngelTimer;
    private float board2AngelTimer;

    private void Start()
    {
        if (!IsServer) return;

        MazeController mazeController = GetComponent<MazeController>();

        InitialiseBoardMaze(board1, board1MazeData);
        InitialiseBoardMaze(board2, board2MazeData);
    }

    private void FixedUpdate()
    {
        TickAngel(board1, board1MazeData, ref board1AngelTimer);
        TickAngel(board2, board2MazeData, ref board2AngelTimer);
    }

    public MazeData GetMazeData(BoardController board)
    {
        if (board == board1) return board1MazeData;
        if (board == board2) return board2MazeData;
        return null;
    }

    private void InitialiseBoardMaze(BoardController board, MazeData mazeData)
    {
        if (board == null) return;
        if (mazeController == null) return;
        if (mazeData == null) return;

        if (!mazeController.GenerateMaze(mazeData, board.transform.position))
            return;

        float spawnDistance = Mathf.Clamp(spawnDistanceFromPlayer, 0f, mazeController.GetMazeSize(mazeData));

        if (board.player != null)
        {
            Vector2Int playerGrid = mazeController.FindRandomWalkablePosition(mazeData);
            PlaceOnGrid(board.player.transform, mazeData, playerGrid);

            if (board.angel != null)
            {
                Vector2Int angelGrid = mazeController.FindRandomWalkablePosition(mazeData, playerGrid, spawnDistance);
                PlaceOnGrid(board.angel.transform, mazeData, angelGrid);
            }
        }
    }

    private void PlaceOnGrid(Transform character, MazeData mazeData, Vector2Int gridPosition)
    {
        Vector3 world = mazeController.MazeGridToWorld(mazeData, gridPosition);
        character.position = new Vector3(world.x, character.position.y, world.z);
    }

    private void TickAngel(BoardController board, MazeData mazeData, ref float timer)
    {
        if (board == null || mazeData == null || !mazeData.IsGenerated || mazeController == null) return;
        if (board.AngelMovement == null || board.angel == null || board.player == null || angelMoveSpeed <= 0f) return;

        timer += Time.fixedDeltaTime;
        if (timer < 1f / angelMoveSpeed) return;
        timer = 0f;

        Vector2Int angelGrid = mazeController.WorldToMazeGrid(mazeData, board.angel.transform.position);
        Vector2Int playerGrid = mazeController.WorldToMazeGrid(mazeData, board.player.transform.position);
        board.AngelMovement.MoveAngel(angelGrid, playerGrid, (grid, _) => mazeController.MazeGridToWorld(mazeData, grid));
    }

    // Only raises the wall if the angel can still reach the player
    public bool TryRaiseWall(BoardController board, Vector2Int position)
    {
        MazeData mazeData = GetMazeData(board);
        if (mazeData == null || !mazeData.IsGenerated || mazeController == null) return false;

        Vector2Int angelGrid = mazeController.WorldToMazeGrid(mazeData, board.angel.transform.position);
        Vector2Int playerGrid = mazeController.WorldToMazeGrid(mazeData, board.player.transform.position);

        if (!mazeController.CanRaiseWall(mazeData, position, angelGrid, playerGrid))
            return false;

        mazeController.RaiseWall(mazeData, position);
        return true;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer) {
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += SceneManager_OnLoadEventCompleted;
            state.Value = GameState.ReadyUp;
            Player1TimeGained.Value = 0;
            Player2TimeGained.Value = 0;
            GlobalTime.Value = (int)timeLimit;
            player1Ready.Value = false;
            player1Ready.Value = false;
            playerReady.Add(0, false);
            playerReady.Add(1, false);
            countDownTime.Value = 3;
        }
    }

    private void SceneManager_OnLoadEventCompleted(string sceneName, UnityEngine.SceneManagement.LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        if (!IsServer || sceneName != gameSceneName) return;
        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            if (NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject != null) continue;
            Transform player = Instantiate(playerPrefab);
            player.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId, true);
        }
    }

    [Rpc(SendTo.Server)]
    public void ReadyRPC(RpcParams rpcParams = default) {
        ulong playerID = rpcParams.Receive.SenderClientId;
        if (!playerReady[playerID]) {
            playerReady[playerID] = true;
            if (playerID == 0) player1Ready.Value = true;
            else if (playerID == 1) player2Ready.Value = true;
        }
        bool ready = true;
        foreach (ulong key in playerReady.Keys) {
            if (!playerReady[key]) ready = false;
        }
        if (ready) {
            state.Value = GameState.CountDown;
            StartCoroutine(CountDownTimer()); 
        }
    }

    private IEnumerator CountDownTimer() {
        float time = 3f; 
        while (time > 0) {
            countDownTime.Value = (int)Math.Floor(time);
            time -= Time.deltaTime;
            yield return null;
        }
        state.Value = GameState.GameActive;
        StartCoroutine(GlobalTimerCountdown());
        Player1Turn();
    }

    private IEnumerator GlobalTimerCountdown() {
        while (state.Value == GameState.GameActive) {
            GlobalTime.Value -= Time.deltaTime;
            yield return null; 
        }
    }

    private void Player1Turn() {

    }

    private void Player2Turn() {

    }

    [Rpc(SendTo.Server)]
    public void SelectTileRPC(PlayerController.Mode mode, Vector2Int blockID, RpcParams rpcParams = default) {
        switch (mode) {
            case PlayerController.Mode.raise : {
                break;
            }
            case PlayerController.Mode.lower : {
                break;
            }
            case PlayerController.Mode.move : {
                break;
            }
        }
    }


    /*private void ComputerBoardGraphs(bool[,] floor, bool[,] walls, Dictionary<Vector2Int, List<Vector2Int>> floorGraphnum, Dictionary<Vector2Int, List<Vector2Int>> wallGraphnum) {
        int width = matrix.GetLength(0);
        int height = matrix.GetLength(1);
        for (int i = 0; i < width; i++) {
            for (int j = 0; j < height; j++) {
                
            }
        }
    }*/
}
