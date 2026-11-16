using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System;

public class GameController : MonoBehaviour
{
    [SerializeField] private string gameSceneName;
    [SerializeField] private float timeLimit = 90f;
    [SerializeField] public SinglePlayerBoardController board1;
    [SerializeField] public SinglePlayerBoardController board2;

    [SerializeField] private MazeData board1MazeData;
    [SerializeField] private MazeData board2MazeData;

    [Tooltip("The MazeBuilder object for each board (walls are built centred on it). Empty = the board's own transform.")]
    [SerializeField] private Transform board1MazeCentre;
    [SerializeField] private Transform board2MazeCentre;
    [SerializeField] private int numWallsToDrop = 10;

    public MazeController mazeController;
    public MazeController Maze => mazeController;

    public SinglePlayerController player;

    [Header("Angel Settings")]
    public float angelMoveSpeed = 1f;
    public float spawnDistanceFromPlayer = 5f;
    public float catchPenalty = 5f; // Seconds taken off a player when the angel catches them

    // Just a set variable for now
    public int wallsDroppedPerRound = 3;

    private Dictionary<ulong, bool> playerReady = new Dictionary<ulong, bool>();

    private Coroutine tS = null;
    private TutorialManager TM;



    public enum GameState {
        ReadyUp,
        CountDown,
        GameActive,
        GameEnd,
    }

    public GameVariable<GameState> state = new GameVariable<GameState>(GameState.ReadyUp);
    public GameVariable<float> GlobalTime = new GameVariable<float>(90f);
    public GameVariable<ulong> currentPlayer = new GameVariable<ulong>(0);
    public GameVariable<float> player1TimeGained = new GameVariable<float>(0);
    public GameVariable<float> player2TimeGained = new GameVariable<float>(0);
    public GameVariable<ulong> winnerDeclared = new GameVariable<ulong>(2);
    public GameVariable<int> player1Tokens = new GameVariable<int>(0);
    public GameVariable<int> player2Tokens = new GameVariable<int>(0);
    public GameVariable<int> player1TimeStops = new GameVariable<int>(0);
    public GameVariable<int> player2TimeStops = new GameVariable<int>(0);


    public GameVariable<bool> player1Ready = new GameVariable<bool>(false);
    public GameVariable<bool> player2Ready = new GameVariable<bool>(true);
    public GameVariable<int> countDownTime = new GameVariable<int>(3);

    public GameList<BlockData> board1Blocks = new GameList<BlockData>();
    public GameList<BlockData> board2Blocks = new GameList<BlockData>();
    public GameVariable<bool> boardsInitialised = new GameVariable<bool>(false);
    // Size of the maze grid (MazeData.width/height are cell counts; the grid is 2n + 1)
    public GameVariable<int> width = new GameVariable<int>(0);
    public GameVariable<int> height = new GameVariable<int>(0);

    public GameVariable<int> isTimeStop = new GameVariable<int>(0);

    // Server-authoritative angel tiles. The server pathfinds and writes these;
    // every peer reads them and animates its angel towards them.
    public GameVariable<Vector2Int> board1AngelGrid = new GameVariable<Vector2Int>(new Vector2Int(0,0));
    public GameVariable<Vector2Int> board2AngelGrid = new GameVariable<Vector2Int>(new Vector2Int(0,0));

    // Server-authoritative player tiles, so every peer places the players in the same spot
    public GameVariable<Vector2Int> board1PlayerGrid = new GameVariable<Vector2Int>(new Vector2Int(0,0));
    public GameVariable<Vector2Int> board2PlayerGrid = new GameVariable<Vector2Int>(new Vector2Int(0,0));


    // Controls when the angel can next move
    private float board1AngelTimer;
    private float board2AngelTimer;

    // Last angel tile each peer applied to its visuals
    private Vector2Int? board1AngelShown;
    private Vector2Int? board2AngelShown;

    // Last player tile each peer applied to its visuals
    private Vector2Int? board1PlayerShown;
    private Vector2Int? board2PlayerShown;

    void Start()
    {    
        playerReady[0] = false;
        playerReady[1] = true;
        StartMazeGeneration();
    }

    void Awake() {
        TM = GetComponent<TutorialManager>();
        TM.tutorialState.OnValueChanged += TutorialStateChanged;
    }

    private void StartMazeGeneration()
    {
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
        if (!boardsInitialised.Value) return;

        ShowAngel(board1, board1MazeData, board1MazeCentre, board1AngelGrid.Value, ref board1AngelShown);
        ShowAngel(board2, board2MazeData, board2MazeCentre, board2AngelGrid.Value, ref board2AngelShown);

        ShowPlayer(board1, board1MazeData, board1MazeCentre, board1PlayerGrid.Value, ref board1PlayerShown);
        ShowPlayer(board2, board2MazeData, board2MazeCentre, board2PlayerGrid.Value, ref board2PlayerShown);
    }

    private void ShowPlayer(SinglePlayerBoardController board, MazeData mazeData, Transform mazeCentre, Vector2Int playerGrid, ref Vector2Int? shown)
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
    private Vector2Int GetPlayerGrid(SinglePlayerBoardController board)
    {
        return board == board1 ? board1PlayerGrid.Value : board2PlayerGrid.Value;
    }

    private void ShowAngel(SinglePlayerBoardController board, MazeData mazeData, Transform mazeCentre, Vector2Int angelGrid, ref Vector2Int? shown)
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

    public MazeData GetMazeData(SinglePlayerBoardController board)
    {
        if (board == board1) return board1MazeData;
        if (board == board2) return board2MazeData;
        return null;
    }

    private Transform GetMazeRoot(SinglePlayerBoardController board, Transform mazeCentre)
    {
        if (mazeCentre != null) return mazeCentre;
        return board != null ? board.transform : null;
    }

    private void InitialiseBoardMaze(SinglePlayerBoardController board, MazeData mazeData, Transform mazeCentre, GameVariable<Vector2Int> angelGridVar, GameVariable<Vector2Int> playerGridVar)
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
    private void TickAngel(SinglePlayerBoardController board, MazeData mazeData, GameVariable<Vector2Int> angelGridVar, ref float timer)
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
        if (!TM.ActionRecorded(TutorialManager.ActionType.TickAngel)) return;

        angelGridVar.Value = nextAngelGrid;
        RunTarjans(board); // Angel moved, so which tiles block it may have changed
    }

    // The angel reached the player on this board.
    // Time penalty, their turn ends, and their maze is regenerated.
    private void AngelCaughtPlayer(SinglePlayerBoardController board)
    {
        AudioManager.Instance.PlayDeathRPC();
        bool isBoard1 = board == board1; // board1 = player 1 (client 0), see FixedUpdate

        GameVariable<float> timeGained = isBoard1 ? player1TimeGained : player2TimeGained;
        timeGained.Value = Mathf.Max(0f, timeGained.Value - catchPenalty);

        // End their turn (same as SwitchTurnRPC)
        SwitchTurn();

        StartCoroutine(RegenerateBoard(board));
    }

    private IEnumerator RegenerateBoard(SinglePlayerBoardController board)
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
        WaitForSeconds nextTick = new WaitForSeconds(1f / 60);
        GameList<BlockData> blocks = isBoard1 ? board1Blocks : board2Blocks;
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
    public bool TryRaiseWall(SinglePlayerBoardController board, Vector2Int position)
    {
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
    public bool TryLowerWall(SinglePlayerBoardController board, Vector2Int position)
    {
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

    public bool TryMovePlayer(SinglePlayerBoardController board, Vector2Int position, ulong playerID) {
        SinglePlayerMazeBuilder builder = board.GetMazeBuilder();
        HashSet<Vector2Int> targets = builder.GetMoveTargets();
        if (!targets.Contains(position)) return false;
        if (playerID == 0) board1PlayerGrid.Value = position;
        else board2PlayerGrid.Value = position;
        AudioManager.Instance.PlayPlayerMoveRPC();
        return true;
    }

    public void DropRandomWalls(SinglePlayerBoardController board, int count)
    {
        if (count <= 0 || mazeController == null) return;
        if (TM.isTutorialActive) return;

        List<Vector2Int> candidates = mazeController.GetLowerableWalls(GetMazeData(board));
        for (int i = 0; i < count && candidates.Count > 0; i++)
        {
            int pick = UnityEngine.Random.Range(0, candidates.Count);
            TryLowerWall(board, candidates[pick]); // Handles syncing, animation and Tarjan's
            candidates.RemoveAt(pick);
        }
    }

    // Writes one tile to the board's synced list, which is what clients react to
    private void PublishBlock(SinglePlayerBoardController board, Vector2Int position, bool isWall)
    {
        GameList<BlockData> blocks = board == board1 ? board1Blocks : board2Blocks;
        int index = position.x * height.Value + position.y; // Matches the fill order in StartMazeGeneration
        if (index < 0 || index >= blocks.Count) return;

        BlockData block = blocks[index];
        block.isFloor = !isWall;
        if (isWall) block.canBeRaised = false;
        blocks[index] = block;
    }

    public void ReadyRPC(ulong playerID) {
        // Late/duplicate clicks must not restart the countdown
        if (state.Value != GameState.ReadyUp) return;
        if (!TM.ActionRecorded(TutorialManager.ActionType.ReadyUp)) return;

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
        player2Tokens.Value = 1;
        player1TimeStops.Value = 3;
        player2TimeStops.Value = 3;
        AudioManager.Instance.PlayStartGameRPC();
        TM.ActionRecorded(TutorialManager.ActionType.CountdownFinished);
        StartCoroutine(GlobalTimerCountdown());
    }

    private IEnumerator GlobalTimerCountdown() {
        int prev = 0;
        int ro = 9;
        float p1acum = 0;
        float p2acum = 0;
        while (state.Value == GameState.GameActive && GlobalTime.Value > 0) {
            if (isTimeStop.Value == 0) {
                if (currentPlayer.Value == 0) {
                    player1TimeGained.Value += Time.deltaTime;
                    p1acum += Time.deltaTime;
                    p2acum = 0;
                    if (p1acum > 15f) {
                        DropRandomWalls(board1, numWallsToDrop*2);
                        p1acum = 0;
                    }
                }
                else {
                    player2TimeGained.Value += Time.deltaTime;
                    p2acum += Time.deltaTime;
                    p1acum = 0;
                    if (p2acum > 15f) {
                        DropRandomWalls(board2, numWallsToDrop*2);
                        p2acum = 0;
                    }
                }
                GlobalTime.Value -= Time.deltaTime;
                
                int val = (int)Math.Ceiling(GlobalTime.Value);
                if ((val / 10) < ro) {
                    ro -= 1;
                    SinglePlayerBoardController board = currentPlayer.Value == 0 ? board1 : board2;
                    DropRandomWalls(board, numWallsToDrop);
                }
                
                if (GlobalTime.Value < 10f) {
                    if (prev != val) {
                        prev = val;
                        AudioManager.Instance.PlayClockSoundRPC();
                    }
                }
            }
            yield return null;
        }
        state.Value = GameState.GameEnd;
        if (player1TimeGained.Value > player2TimeGained.Value) winnerDeclared.Value = 0;
        else winnerDeclared.Value = 1;
    }

    public void SelectTileRPC(SinglePlayerController.Mode mode, Vector2Int blockID, int board, ulong playerID) {
        Debug.Log("Ray received: " + mode);
        switch (mode) {
            // RunTarjans is called inside the functions themselves ()
            case SinglePlayerController.Mode.raise : {
                if (!TM.ActionRecorded(TutorialManager.ActionType.RaiseBlock)) return;
                bool outcome = TryRaiseWall(board == 1 ? board1 : board2, blockID);
                print(outcome);
                RunTarjans(board == 1 ? board1 : board2);
                if (outcome) {
                    if (playerID == 0) player1Tokens.Value -= 1;
                    else player2Tokens.Value -= 1;
                }
                break;
            }
            case SinglePlayerController.Mode.lower : {
                if (!TM.ActionRecorded(TutorialManager.ActionType.LowerBlock)) return;
                bool outcome = TryLowerWall(board == 1 ? board1 : board2, blockID);
                print(outcome);
                RunTarjans(board == 1 ? board1 : board2);
                if (outcome) {
                    if (playerID == 0) player1Tokens.Value -= 1;
                    else player2Tokens.Value -= 1;
                }
                break;
            }
            case SinglePlayerController.Mode.move : { 
                if (!TM.ActionRecorded(TutorialManager.ActionType.MovePlayer)) return;
                bool outcome = false;
                print(outcome);
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

    public void SwitchTurnRPC(ulong playerID) {
        if (playerID != currentPlayer.Value) return;
        SwitchTurn();
    }

    public void TimeStopRPC(ulong playerID) {
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
            player1Tokens.Value = 1;
            DropRandomWalls(board1, numWallsToDrop);
        }
        else {
            currentPlayer.Value = 0;
            player1Tokens.Value = 3;
            player2Tokens.Value = 1;
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

    private void RunTarjans(SinglePlayerBoardController board) {
        if (board == null || mazeController == null) return;

        MazeData mazeData = GetMazeData(board);
        if (mazeData == null || !mazeData.IsGenerated) return;

        GameList<BlockData> blocks = board == board1 ? board1Blocks : board2Blocks;
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

    private void TutorialStateChanged(TutorialManager.TutorialStates newState) {
        switch (newState) {
            case TutorialManager.TutorialStates.IntroducePlayerState : {
                Renderer pieceRenderer = board1.player.gameObject.GetComponentInChildren<Renderer>(); 
                pieceRenderer.material.color = Color.red;
                Material mat = pieceRenderer.material;
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", Color.red * 2f);
                break;
            }
            case TutorialManager.TutorialStates.IntroduceAngelState : {
                Renderer pieceRenderer = board1.player.gameObject.GetComponentInChildren<Renderer>(); 
                pieceRenderer.material.color = Color.white;
                Material mat = pieceRenderer.material;
                mat.SetColor("_EmissionColor", Color.black);
                mat.DisableKeyword("_EMISSION");
                pieceRenderer = board1.angel.gameObject.GetComponentInChildren<Renderer>(); 
                pieceRenderer.material.color = Color.red;
                mat = pieceRenderer.material;
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", Color.red * 2f);
                break;
            }
            case TutorialManager.TutorialStates.BlockPlaceToggleState : {
                Renderer pieceRenderer = board1.angel.gameObject.GetComponentInChildren<Renderer>(); 
                pieceRenderer.material.color = Color.white;
                Material mat = pieceRenderer.material;
                mat.SetColor("_EmissionColor", Color.black);
                mat.DisableKeyword("_EMISSION");
                break;
            }
        }
    }
}
