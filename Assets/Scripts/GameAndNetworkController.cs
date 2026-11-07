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
    private PlayerController player;

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
