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

    [Tooltip("The MazeBuilder object for each board (walls are built centred on it). Empty = the board's own transform.")]
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
    // Size of the maze grid (MazeData.width/height are cell counts; the grid is 2n + 1)
    public NetworkVariable<int> width;
    public NetworkVariable<int> height;

    // Server-authoritative angel tiles. The server pathfinds and writes these;
    // every peer reads them and animates its angel towards them.
    public NetworkVariable<Vector2Int> board1AngelGrid = new NetworkVariable<Vector2Int>();
    public NetworkVariable<Vector2Int> board2AngelGrid = new NetworkVariable<Vector2Int>();


    // Controls when the angel can next move
    private float board1AngelTimer;
    private float board2AngelTimer;

    // Last angel tile each peer applied to its visuals
    private Vector2Int? board1AngelShown;
    private Vector2Int? board2AngelShown;

    private void StartMazeGeneration()
    {
        if (!IsServer) return;

        if (mazeController == null)
            mazeController = GetComponent<MazeController>();

        InitialiseBoardMaze(board1, board1MazeData, board1MazeCentre, board1AngelGrid);
        InitialiseBoardMaze(board2, board2MazeData, board2MazeCentre, board2AngelGrid);

        bool[,] board1data = board1MazeData.GetFloorData();
        bool[,] board2data = board2MazeData.GetFloorData();
        if (board1data == null || board2data == null)
        {
            Debug.LogError("GameAndNetworkController: maze generation failed, boards not sent.");
            return;
        }

        int gridWidth = board1data.GetLength(0);
        int gridHeight = board1data.GetLength(1);
        if (board2data.GetLength(0) != gridWidth || board2data.GetLength(1) != gridHeight)
        {
            Debug.LogError("GameAndNetworkController: both MazeData assets must have the same width and height.");
            return;
        }

        width.Value = gridWidth;
        height.Value = gridHeight;

        for (int i = 0; i < gridWidth; i++) {
            for (int j = 0; j < gridHeight; j++) {
                board1Blocks.Add(new BlockData(i, j, board1data[i, j], false));
                board2Blocks.Add(new BlockData(i, j, board2data[i, j], false));
            }
        }

        boardsInitialised.Value = true;
    }

    private void FixedUpdate()
    {
        if (!IsServer) return;

        if (state.Value != GameState.GameActive) return;
        if(currentPlayer.Value == 0)
        {
            TickAngel(board1, board1MazeData, board1AngelGrid, ref board1AngelTimer);
        }else
        {
            TickAngel(board2, board2MazeData, board2AngelGrid, ref board2AngelTimer);
        }
    }

    // Every peer (host included): animate the angels towards the server's chosen tiles
    private void Update()
    {
        if (!IsSpawned || !boardsInitialised.Value) return;

        ShowAngel(board1, board1MazeData, board1MazeCentre, board1AngelGrid.Value, ref board1AngelShown);
        ShowAngel(board2, board2MazeData, board2MazeCentre, board2AngelGrid.Value, ref board2AngelShown);
    }

    private void ShowAngel(BoardController board, MazeData mazeData, Transform mazeCentre, Vector2Int angelGrid, ref Vector2Int? shown)
    {
        if (board == null || board.angel == null || mazeData == null) return;
        if (shown == angelGrid) return;

        Transform root = GetMazeRoot(board, mazeCentre);
        if (root == null || width.Value <= 0 || height.Value <= 0) return;

        Vector3 target = MazeController.GridToWorld(root.position, width.Value, height.Value, mazeData.tileSize, angelGrid);
        bool snap = shown == null; // First placement jumps straight there

        if (board.AngelMovement != null) board.AngelMovement.SetTarget(target, snap);
        else board.angel.transform.position = target;

        shown = angelGrid;
    }

    public MazeData GetMazeData(BoardController board)
    {
        if (board == board1) return board1MazeData;
        if (board == board2) return board2MazeData;
        return null;
    }

    private Transform GetMazeRoot(BoardController board, Transform mazeCentre)
    {
        if (mazeCentre != null) return mazeCentre;
        return board != null ? board.transform : null;
    }

    private void InitialiseBoardMaze(BoardController board, MazeData mazeData, Transform mazeCentre, NetworkVariable<Vector2Int> angelGridVar)
    {
        if (board == null) return;
        if (mazeController == null) return;
        if (mazeData == null) return;

        if (!mazeController.GenerateMaze(mazeData, GetMazeRoot(board, mazeCentre)))
            return;

        float spawnDistance = Mathf.Clamp(spawnDistanceFromPlayer, 0f, mazeController.GetMazeSize(mazeData));

        if (board.player != null)
        {
            Vector2Int playerGrid = mazeController.FindRandomWalkablePosition(mazeData);
            PlaceOnGrid(board.player.transform, mazeData, playerGrid);

            if (board.angel != null)
            {
                // Every peer places the angel from this (see ShowAngel)
                angelGridVar.Value = mazeController.FindRandomWalkablePosition(mazeData, playerGrid, spawnDistance);
            }
        }
    }

    private void PlaceOnGrid(Transform character, MazeData mazeData, Vector2Int gridPosition)
    {
        character.position = mazeController.MazeGridToWorld(mazeData, gridPosition);
    }

    // Server only: pathfind one step towards the player and publish the new tile
    private void TickAngel(BoardController board, MazeData mazeData, NetworkVariable<Vector2Int> angelGridVar, ref float timer)
    {
        if (board == null || mazeData == null || !mazeData.IsGenerated || mazeController == null) return;
        if (board.angel == null || board.player == null || angelMoveSpeed <= 0f) return;

        timer += Time.fixedDeltaTime;
        if (timer < 1f / angelMoveSpeed) return;
        timer = 0f;

        Vector2Int playerGrid = mazeController.WorldToMazeGrid(mazeData, board.player.transform.position);
        angelGridVar.Value = AngelPathfinder.NextStep(mazeData.GetFloorData(), angelGridVar.Value, playerGrid);
    }

    // These update the server's maze data and the synced block list for the MazeBuilders to then update their visuals.
    public bool TryRaiseWall(BoardController board, Vector2Int position)
    {
        if (!IsServer) return false;
        MazeData mazeData = GetMazeData(board);
        if (mazeData == null || !mazeData.IsGenerated || mazeController == null) return false;

        // Use the angel's logical tile, which is already the tile it's moving into
        Vector2Int angelGrid = board == board1 ? board1AngelGrid.Value : board2AngelGrid.Value;
        Vector2Int playerGrid = mazeController.WorldToMazeGrid(mazeData, board.player.transform.position);

        if (!mazeController.CanRaiseWall(mazeData, position, angelGrid, playerGrid))
            return false;

        if (!mazeController.RaiseWall(mazeData, position))
            return false;

        PublishBlock(board, position, true);
        return true;
    }

    // Lowers any inner wall (the outer border can't be lowered)
    public bool TryLowerWall(BoardController board, Vector2Int position)
    {
        if (!IsServer) return false;
        MazeData mazeData = GetMazeData(board);
        if (mazeData == null || !mazeData.IsGenerated || mazeController == null) return false;

        if (!mazeController.CanLowerWall(mazeData, position))
            return false;

        if (!mazeController.LowerWall(mazeData, position))
            return false;

        PublishBlock(board, position, false);
        return true;
    }

    // Writes one tile to the board's synced list, which is what clients react to
    private void PublishBlock(BoardController board, Vector2Int position, bool isWall)
    {
        NetworkList<BlockData> blocks = board == board1 ? board1Blocks : board2Blocks;
        int index = position.x * height.Value + position.y; // Matches the fill order in StartMazeGeneration
        if (index < 0 || index >= blocks.Count) return;

        BlockData block = blocks[index];
        block.isFloor = !isWall;
        if (isWall) block.canBeRaised = false;
        blocks[index] = block;
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
            player2Ready.Value = false;
            playerReady[0] = false;
            playerReady[1] = false;
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
        // Late/duplicate clicks must not restart the countdown
        if (state.Value != GameState.ReadyUp) return;

        ulong playerID = rpcParams.Receive.SenderClientId;
        if (!playerReady.TryGetValue(playerID, out bool alreadyReady)) {
            Debug.LogWarning($"ReadyRPC from unexpected client {playerID}");
            return;
        }
        if (!alreadyReady) {
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
            // Ceil so the display reads 3, 2, 1 rather than 2, 1, 0
            countDownTime.Value = (int)Math.Ceiling(time);
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
                //RunYarjans
                break;
            }
            case PlayerController.Mode.lower : {
                // RunTarjans
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

    [Rpc(SendTo.Server)]
    public void AngelCaughtPlayerRPC(RpcParams rpcParams = default) {
        // Take 5 seconds penalty, force swap of players. 
        // Regenarate map 
    }


    private void RunTarjans() { // Called After every block placed / removed 

    }




}
