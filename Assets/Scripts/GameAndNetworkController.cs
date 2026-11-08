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
    [SerializeField] private int numWallsToDrop = 10;

    public MazeController mazeController;
    public MazeController Maze => mazeController;

    private PlayerController player;

    [Header("Angel Settings")]
    public float angelMoveSpeed = 1f;
    public float spawnDistanceFromPlayer = 5f;
    public float catchPenalty = 5f; // Seconds taken off a player when the angel catches them

    // Just a set variable for now
    public int wallsDroppedPerRound = 3;

    private Dictionary<ulong, bool> playerReady = new Dictionary<ulong, bool>();

    private Coroutine tS = null;



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
    public NetworkVariable<ulong> winnerDeclared;
    public NetworkVariable<int> player1Tokens;
    public NetworkVariable<int> player2Tokens;
    public NetworkVariable<int> player1TimeStops;
    public NetworkVariable<int> player2TimeStops;


    public NetworkVariable<bool> player1Ready;
    public NetworkVariable<bool> player2Ready;
    public NetworkVariable<int> countDownTime;

    public NetworkList<BlockData> board1Blocks = new NetworkList<BlockData>();
    public NetworkList<BlockData> board2Blocks = new NetworkList<BlockData>();
    public NetworkVariable<bool> boardsInitialised;
    // Size of the maze grid (MazeData.width/height are cell counts; the grid is 2n + 1)
    public NetworkVariable<int> width;
    public NetworkVariable<int> height;

    public NetworkVariable<int> isTimeStop;

    // Server-authoritative angel tiles. The server pathfinds and writes these;
    // every peer reads them and animates its angel towards them.
    public NetworkVariable<Vector2Int> board1AngelGrid = new NetworkVariable<Vector2Int>();
    public NetworkVariable<Vector2Int> board2AngelGrid = new NetworkVariable<Vector2Int>();

    // Server-authoritative player tiles, so every peer places the players in the same spot
    public NetworkVariable<Vector2Int> board1PlayerGrid = new NetworkVariable<Vector2Int>();
    public NetworkVariable<Vector2Int> board2PlayerGrid = new NetworkVariable<Vector2Int>();


    // Controls when the angel can next move
    private float board1AngelTimer;
    private float board2AngelTimer;

    // Last angel tile each peer applied to its visuals
    private Vector2Int? board1AngelShown;
    private Vector2Int? board2AngelShown;

    // Last player tile each peer applied to its visuals
    private Vector2Int? board1PlayerShown;
    private Vector2Int? board2PlayerShown;

    private void StartMazeGeneration()
    {
        if (!IsServer) return;

        if (mazeController == null)
            mazeController = GetComponent<MazeController>();

        InitialiseBoardMaze(board1, board1MazeData, board1MazeCentre, board1AngelGrid, board1PlayerGrid);
        InitialiseBoardMaze(board2, board2MazeData, board2MazeCentre, board2AngelGrid, board2PlayerGrid);

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

        RunTarjans(board1);
        RunTarjans(board2);

        boardsInitialised.Value = true;
        DropRandomWalls(board1, numWallsToDrop*3);
        DropRandomWalls(board2, numWallsToDrop*3);
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

        ShowPlayer(board1, board1MazeData, board1MazeCentre, board1PlayerGrid.Value, ref board1PlayerShown);
        ShowPlayer(board2, board2MazeData, board2MazeCentre, board2PlayerGrid.Value, ref board2PlayerShown);
    }

    private void ShowPlayer(BoardController board, MazeData mazeData, Transform mazeCentre, Vector2Int playerGrid, ref Vector2Int? shown)
    {
        if (board == null || board.player == null || mazeData == null) return;
        if (shown == playerGrid) return;

        Transform root = GetMazeRoot(board, mazeCentre);
        if (root == null || width.Value <= 0 || height.Value <= 0) return;

        Vector3 target = MazeController.GridToWorld(root.position, width.Value, height.Value, mazeData.tileSize, playerGrid);
        // Jump straight there on first placement, or if it moved more than one tile (maze was regenerated)
        bool snap = shown == null || Vector2Int.Distance(shown.Value, playerGrid) > 1.5f;

        if (board.PlayerMovement != null) board.PlayerMovement.SetTarget(target, snap);
        else board.player.transform.position = target;

        shown = playerGrid;
    }

    //The player's tile. Use this rather than the player object's position
    private Vector2Int GetPlayerGrid(BoardController board)
    {
        return board == board1 ? board1PlayerGrid.Value : board2PlayerGrid.Value;
    }

    private void ShowAngel(BoardController board, MazeData mazeData, Transform mazeCentre, Vector2Int angelGrid, ref Vector2Int? shown)
    {
        if (board == null || board.angel == null || mazeData == null) return;
        if (shown == angelGrid) return;

        Transform root = GetMazeRoot(board, mazeCentre);
        if (root == null || width.Value <= 0 || height.Value <= 0) return;

        Vector3 target = MazeController.GridToWorld(root.position, width.Value, height.Value, mazeData.tileSize, angelGrid);
        // Jump straight there on first placement, or if it moved more than one tile (maze was regenerated)
        bool snap = shown == null || Vector2Int.Distance(shown.Value, angelGrid) > 1.5f;

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

    private void InitialiseBoardMaze(BoardController board, MazeData mazeData, Transform mazeCentre, NetworkVariable<Vector2Int> angelGridVar, NetworkVariable<Vector2Int> playerGridVar)
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
            PlaceOnGrid(board.player.transform, mazeData, playerGrid); // Server copy, used straight away
            playerGridVar.Value = playerGrid; // Every peer places the player from this (see ShowPlayer)

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

        Vector2Int playerGrid = GetPlayerGrid(board);
        Vector2Int nextAngelGrid = AngelPathfinder.NextStep(mazeData.GetFloorData(), angelGridVar.Value, playerGrid);

        // If the angel is actually on the grid, or about to step on, catch the player
        if (nextAngelGrid == playerGrid || angelGridVar.Value == playerGrid)
        {
            AngelCaughtPlayer(board);
            return;
        }

        if (nextAngelGrid == angelGridVar.Value) return;

        angelGridVar.Value = nextAngelGrid;
        RunTarjans(board); // Angel moved, so which tiles block it may have changed
    }

    // The angel reached the player on this board.
    // Time penalty, their turn ends, and their maze is regenerated.
    private void AngelCaughtPlayer(BoardController board)
    {
        AudioManager.Instance.PlayDeathRPC();
        bool isBoard1 = board == board1; // board1 = player 1 (client 0), see FixedUpdate

        NetworkVariable<float> timeGained = isBoard1 ? player1TimeGained : player2TimeGained;
        timeGained.Value = Mathf.Max(0f, timeGained.Value - catchPenalty);

        // End their turn (same as SwitchTurnRPC)
        SwitchTurn();

        StartCoroutine(RegenerateBoard(board));
    }

    private IEnumerator RegenerateBoard(BoardController board)
    {
        bool isBoard1 = board == board1;
        MazeData mazeData = GetMazeData(board);

        InitialiseBoardMaze(board, mazeData,
            isBoard1 ? board1MazeCentre : board2MazeCentre,
            isBoard1 ? board1AngelGrid : board2AngelGrid,
            isBoard1 ? board1PlayerGrid : board2PlayerGrid);

        bool[,] floor = mazeData.GetFloorData();
        if (floor == null) yield break;

        // Give the player a moment before the angel starts moving again
        if (isBoard1) board1AngelTimer = 0f;
        else board2AngelTimer = 0f;

        // Work out Tarjan's result first so each tile is only sent once
        HashSet<Vector2Int> raisable = new HashSet<Vector2Int>();
        if (board.player != null)
        {
            Vector2Int angelGrid = isBoard1 ? board1AngelGrid.Value : board2AngelGrid.Value;
            Vector2Int playerGrid = GetPlayerGrid(board);
            raisable.UnionWith(mazeController.GetAvailableWallPositions(mazeData, angelGrid, playerGrid));
        }

        // Send the changes 80 tiles per network tick. All at once is too big an update
        // for the connection and the client stops receiving anything.
        WaitForSeconds nextTick = new WaitForSeconds(1f / NetworkManager.NetworkConfig.TickRate);
        NetworkList<BlockData> blocks = isBoard1 ? board1Blocks : board2Blocks;
        int sent = 0;
        for (int i = 0; i < blocks.Count; i++)
        {
            BlockData old = blocks[i];
            Vector2Int pos = new Vector2Int(old.posWidth, old.posHeight);
            BlockData updated = new BlockData(pos.x, pos.y, floor[pos.x, pos.y], raisable.Contains(pos));
            if (updated.Equals(old)) continue;
            blocks[i] = updated;
            if (++sent % 80 == 0) yield return nextTick;
        }
        DropRandomWalls(board, numWallsToDrop);
    }

    // These update the server's maze data and the synced block list for the MazeBuilders to then update their visuals.
    public bool TryRaiseWall(BoardController board, Vector2Int position)
    {
        if (!IsServer) return false;
        MazeData mazeData = GetMazeData(board);
        if (mazeData == null || !mazeData.IsGenerated || mazeController == null) return false;

        // Use the angel's logical tile, which is already the tile it's moving into
        Vector2Int angelGrid = board == board1 ? board1AngelGrid.Value : board2AngelGrid.Value;
        Vector2Int playerGrid = GetPlayerGrid(board);

        if (!mazeController.CanRaiseWall(mazeData, position, angelGrid, playerGrid))
            return false;

        if (!mazeController.RaiseWall(mazeData, position))
            return false;

        PublishBlock(board, position, true);
        AudioManager.Instance.PlayPlaceBlockRPC();
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
        AudioManager.Instance.PlayDestroyBlockRPC();
        return true;
    }

    public bool TryMovePlayer(BoardController board, Vector2Int position, ulong playerID) {
        MazeBuilder builder = board.GetMazeBuilder();
        HashSet<Vector2Int> targets = builder.GetMoveTargets();
        if (!targets.Contains(position)) return false;
        if (playerID == 0) board1PlayerGrid.Value = position;
        else board2PlayerGrid.Value = position;
        AudioManager.Instance.PlayPlayerMoveRPC();
        return true;
    }

    public void DropRandomWalls(BoardController board, int count)
    {
        if (!IsServer || count <= 0 || mazeController == null) return;

        List<Vector2Int> candidates = mazeController.GetLowerableWalls(GetMazeData(board));
        for (int i = 0; i < count && candidates.Count > 0; i++)
        {
            int pick = UnityEngine.Random.Range(0, candidates.Count);
            TryLowerWall(board, candidates[pick]); // Handles syncing, animation and Tarjan's
            candidates.RemoveAt(pick);
        }
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
            winnerDeclared.Value = 2;
            player1Tokens.Value = 0;
            player2Tokens.Value = 0;
            player1TimeStops.Value = 0;
            player2TimeStops.Value = 0;
            isTimeStop.Value = 0;
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
        int prev = 0;
        while (time > 0) {
            // Ceil so the display reads 3, 2, 1 rather than 2, 1, 0
            int result = (int)Math.Ceiling(time);
            if (result != prev) {
                prev = result;
                AudioManager.Instance.PlayClockSoundRPC();
            }
            countDownTime.Value = result;
            time -= Time.deltaTime;
            yield return null;
        }
        state.Value = GameState.GameActive;
        player1Tokens.Value = 3;
        player1TimeStops.Value = 3;
        player2TimeStops.Value = 3;
        AudioManager.Instance.PlayStartGameRPC();
        StartCoroutine(GlobalTimerCountdown());
    }

    private IEnumerator GlobalTimerCountdown() {
        int prev = 0;
        int ro = 9;
        while (state.Value == GameState.GameActive && GlobalTime.Value > 0) {
            if (currentPlayer.Value == 0) player1TimeGained.Value += Time.deltaTime;
            else player2TimeGained.Value += Time.deltaTime;
            GlobalTime.Value -= Time.deltaTime;
            
            int val = (int)Math.Ceiling(GlobalTime.Value);
            if ((val / 10) < ro) {
                ro -= 1;
                BoardController board = currentPlayer.Value == 0 ? board1 : board2;
                DropRandomWalls(board, numWallsToDrop);
            }
            
            if (GlobalTime.Value < 10f) {
                if (prev != val) {
                    prev = val;
                    AudioManager.Instance.PlayClockSoundRPC();
                }
            }
            yield return null;
        }
        state.Value = GameState.GameEnd;
        if (player1TimeGained.Value > player2TimeGained.Value) winnerDeclared.Value = 0;
        else winnerDeclared.Value = 1;
    }

    [Rpc(SendTo.Server)]
    public void SelectTileRPC(PlayerController.Mode mode, Vector2Int blockID, int board, RpcParams rpcParams = default) {
        ulong playerID = rpcParams.Receive.SenderClientId;
        switch (mode) {
            // RunTarjans is called inside the functions themselves ()
            case PlayerController.Mode.raise : {
                bool outcome = TryRaiseWall(board == 1 ? board1 : board2, blockID);
                RunTarjans(board == 1 ? board1 : board2);
                if (outcome) {
                    if (playerID == 0) player1Tokens.Value -= 1;
                    else player2Tokens.Value -= 1;
                }
                break;
            }
            case PlayerController.Mode.lower : {
                bool outcome = TryLowerWall(board == 1 ? board1 : board2, blockID);
                RunTarjans(board == 1 ? board1 : board2);
                if (outcome) {
                    if (playerID == 0) player1Tokens.Value -= 1;
                    else player2Tokens.Value -= 1;
                }
                break;
            }
            case PlayerController.Mode.move : { 
                bool outcome = false;
                if (board == 1 && playerID == 0) outcome = TryMovePlayer(board1, blockID, playerID);
                else if (board == 2 && playerID == 1) outcome = TryMovePlayer(board2, blockID, playerID);
                if (outcome) {
                    if (playerID == 0) player1Tokens.Value -= 1;
                    else player2Tokens.Value -= 1; 
                }
                break;
            }
        }
    }


    [Rpc(SendTo.Server)]
    public void SwitchTurnRPC(RpcParams rpcParams = default) {
        ulong playerID = rpcParams.Receive.SenderClientId;
        if (playerID != currentPlayer.Value) return;
        SwitchTurn();
    }

    [Rpc(SendTo.Server)]
    public void TimeStopRPC(RpcParams rpcParams = default) {
        ulong playerID = rpcParams.Receive.SenderClientId;
        if (tS != null) return;
        if (playerID != currentPlayer.Value) return;
        if (playerID == 0) {
            if (player1TimeStops.Value <= 0) return;
            player1TimeStops.Value -= 1;
            tS = StartCoroutine(TimeStop());
        }
        else {
            if (player2TimeStops.Value <= 0) return;
            player2TimeStops.Value -= 1;
            tS = StartCoroutine(TimeStop());
        }
    }

    private IEnumerator TimeStop() {
        AudioManager.Instance.PlayTimeStopRPC();
        isTimeStop.Value = 1;
        angelMoveSpeed = 0f;
        yield return new WaitForSeconds(5f);
        angelMoveSpeed = 2.5f;
        tS = null;
        isTimeStop.Value = 0;
    }

    private void SwitchTurn() {
        if (currentPlayer.Value == 0) {
            currentPlayer.Value = 1;
            player2Tokens.Value = 3;
            DropRandomWalls(board1, numWallsToDrop);
        }
        else {
            currentPlayer.Value = 0;
            player1Tokens.Value = 3;
            DropRandomWalls(board2, numWallsToDrop);
        }
        if (tS != null) StopCoroutine(tS);
        tS = null;
        angelMoveSpeed = 2.5f;
        isTimeStop.Value = 0;
        AudioManager.Instance.PlaySwitchRPC();
    }

    // Depends on where the angel and player are, so call it after any wall change AND
    // whenever the angel (or player) moves to a new tile.

    private void RunTarjans(BoardController board) {
        if (!IsServer || board == null || mazeController == null) return;

        MazeData mazeData = GetMazeData(board);
        if (mazeData == null || !mazeData.IsGenerated) return;

        NetworkList<BlockData> blocks = board == board1 ? board1Blocks : board2Blocks;
        if (blocks.Count == 0) return;

        // No player = nothing to protect, so nothing is marked as raisable
        HashSet<Vector2Int> allowed = new HashSet<Vector2Int>();
        if (board.player != null)
        {
            Vector2Int angelGrid = board == board1 ? board1AngelGrid.Value : board2AngelGrid.Value;
            Vector2Int playerGrid = GetPlayerGrid(board);
            allowed.UnionWith(mazeController.GetAvailableWallPositions(mazeData, angelGrid, playerGrid));
        }

        for (int i = 0; i < blocks.Count; i++)
        {
            BlockData block = blocks[i];
            bool canBeRaised = allowed.Contains(new Vector2Int(block.posWidth, block.posHeight));
            if (block.canBeRaised == canBeRaised) continue; // Only send tiles that changed

            block.canBeRaised = canBeRaised;
            blocks[i] = block;
        }
    }
}
