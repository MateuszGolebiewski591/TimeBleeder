using UnityEngine;
using Unity.Netcode;
using System.Collections;
using UnityEngine.InputSystem;

public class PlayerController : NetworkBehaviour
{
    private GameAndNetworkController ganc;   
    private GameAndNetworkController.GameState state = GameAndNetworkController.GameState.ReadyUp;
    private UIManager UI;
    public PlayerInfo player1Info;
    public PlayerInfo player2Info;
    public PlayerInfo localPlayerInfo;
    private Vector3 initialPosition;
    private Vector3 otherPosition;


    private IEnumerator Start()
    {
        if (IsOwner) {
            while (ganc == null)
            {
                ganc = FindFirstObjectByType<GameAndNetworkController>();
                yield return null;
            }
            while (!ganc.IsSpawned) yield return null;
            if (OwnerClientId == 0) localPlayerInfo = player1Info;
            else if (OwnerClientId == 1) localPlayerInfo = player2Info;
            InitCameraPos();
            ganc.state.OnValueChanged += GameStateChange;
            UI = FindFirstObjectByType<UIManager>();
            UI.RegisterPlayer(OwnerClientId);
            GetComponent<PlayerInput>().enabled = true;
            gameObject.GetComponent<Camera>().enabled = true; 
            gameObject.GetComponent<AudioListener>().enabled = true;
        }
        yield return null;
    }

    void Update() {
        if (!IsOwner) return;
        if (Input.GetKeyDown(KeyCode.Mouse0)) OnLeftClick();
        if (Input.GetKeyDown(KeyCode.Alpha1)) { }
        if (Input.GetKeyDown(KeyCode.Alpha2)) { }
        if (Input.GetKeyDown(KeyCode.Alpha3)) { }
        if (Input.GetKeyDown(KeyCode.E)) { }
        if (Input.GetKeyDown(KeyCode.W)) { }
        if (Input.GetKeyDown(KeyCode.S)) { }
    }

    private void InitCameraPos() {
        initialPosition = localPlayerInfo.mainPosition;
        otherPosition = localPlayerInfo.otherPosition;
        transform.position = initialPosition;
        transform.rotation = localPlayerInfo.rotation;
    }

    private void GameStateChange(GameAndNetworkController.GameState prev, GameAndNetworkController.GameState newValue) {
        state = newValue;
    }

    private void OnLeftClick() {
        if (state == GameAndNetworkController.GameState.ReadyUp) ganc.ReadyRPC();
        else if (state == GameAndNetworkController.GameState.GameActive) RayCast();
    }

    private void RayCast() {

    }
    
}
