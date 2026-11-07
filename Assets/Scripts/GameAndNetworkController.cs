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

    [Tooltip("Scene objects marking where each maze is centred. Empty = the board's own position.")]
    [SerializeField] private Transform board1MazeCentre;
    [SerializeField] private Transform board2MazeCentre;

    public MazeController mazeController;
    public MazeController Maze => mazeController;

    private PlayerController player;

    [Header("Angel Settings")]
    public float angelMoveSpeed = 1f;
    public float spawnDistanceFromPlayer = 5f;

    private Dictionary<ulong, bool> playerReady = new Dictionary<ulong, bool>();
    


    public enum GameState {
        ReadyUp,
        CountDown,
        GameActive,
        GameEnd,
    }

    public NetworkVariable<GameState> state;
    public NetworkVariable<float> GlobalTime;
    public NetworkVariable<ulong> currentPlayer;
    public NetworkVariable<float> player1TimeGained;
    public NetworkVariable<float> player2TimeGained;


    public NetworkVariable<bool> player1Ready;
    public NetworkVariable<bool> player2Ready;
    public NetworkVariable<int> countDownTime;

    public NetworkList<BlockData> board1Blocks = new NetworkList<BlockData>();
    public NetworkList<BlockData> board2Blocks = new NetworkList<BlockData>();
    public NetworkVariable<bool> boardsInitialised;
    public NetworkVariable<int> width;
    public NetworkVariable<int> height;


    // Controls when the angel can next move
    private float board1AngelTimer;
    private float board2AngelTimer;

    private void StartMazeGeneration()
    {
        if (!IsServer) return;

        if (mazeController == null)
            mazeController = GetComponent<MazeController>();

        InitialiseBoardMaze(board1, board1MazeData, board1MazeCentre);
        InitialiseBoardMaze(board2, board2MazeData, board2MazeCentre);
        bool[,] board1data = board1MazeData.GetFloorData();
        bool[,] board2data = board2MazeData.GetFloorData();
        int lwidth = board1MazeData.width;
        int lheight = board1MazeData.height;
        width.Value = lwidth;
        height.Value = lheight; 

        List<BlockData> blocks1 = new List<BlockData>();
        for (int i = 0; i < lwidth; i++) {
            for (int j = 0; j < lheight; j++) {
                blocks1.Add(new BlockData(i, j, board1data[i, j]));
            }
        }
        foreach (BlockData block in blocks1) {
            board1Blocks.Add(block);
        }

        List<BlockData> blocks2 = new List<BlockData>();
        for (int i = 0; i < lwidth; i++) {
            for (int j = 0; j < lheight; j++) {
                blocks2.Add(new BlockData(i, j, board2data[i, j]));
            }
        }
        foreach (BlockData block in blocks2) {
            board2Blocks.Add(block);
        }
        boardsInitialised.Value = true;
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

    private void InitialiseBoardMaze(BoardController board, MazeData mazeData, Transform mazeCentre)
    {
        if (board == null) return;
        if (mazeController == null) return;
        if (mazeData == null) return;

        Vector3 centre = mazeCentre != null ? mazeCentre.position : board.transform.position;
        if (!mazeController.GenerateMaze(mazeData, centre))
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
        character.position = mazeController.MazeGridToWorld(mazeData, gridPosition);
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
            player1TimeGained.Value = 0;
            player2TimeGained.Value = 0;
            GlobalTime.Value = (int)timeLimit;
            player1Ready.Value = false;
            player1Ready.Value = false;
            playerReady.Add(0, false);
            playerReady.Add(1, false);
            countDownTime.Value = 3;
            currentPlayer.Value = 0;
            StartMazeGeneration();
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
    }

    private IEnumerator GlobalTimerCountdown() {
        while (state.Value == GameState.GameActive) {
            GlobalTime.Value -= Time.deltaTime;
            if (currentPlayer.Value == 0) player1TimeGained.Value += Time.deltaTime;
            else player2TimeGained.Value += Time.deltaTime;
            yield return null; 
        }
        currentPlayer.Value = 0;
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


    [Rpc(SendTo.Server)]
    public void SwitchTurnRPC(RpcParams rpcParams = default) {
        ulong playerID = rpcParams.Receive.SenderClientId;
        if (playerID != currentPlayer.Value) return;
        if (currentPlayer.Value == 0) currentPlayer.Value = 1;
        else currentPlayer.Value = 0;
    }

    
}
