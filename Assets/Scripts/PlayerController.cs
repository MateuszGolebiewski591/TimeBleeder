using UnityEngine;
using Unity.Netcode;
using System.Collections;
using UnityEngine.InputSystem;

public class PlayerController : NetworkBehaviour
{
    [SerializeField] private float camMovementTime = 0.4f;
    private GameAndNetworkController ganc;   
    private GameAndNetworkController.GameState state = GameAndNetworkController.GameState.ReadyUp;
    private BoardController board1;
    private BoardController board2;
    private UIManager UI;
    public PlayerInfo player1Info;
    public PlayerInfo player2Info;
    public PlayerInfo localPlayerInfo;
    private Vector3 initialPosition = Vector3.zero;
    private Vector3 otherPosition = Vector3.zero;
    private Mode currentMode;

    private Coroutine movingForward = null;
    private Coroutine movingBackward = null;
    private bool isMyTurn = false;
    private int tokenCount = 0;
    private int timeStopCount = 0;

    public enum Mode{
        raise, 
        lower, 
        move,
    }


    private IEnumerator Start()
    {
        if (IsOwner) {
            while (ganc == null)
            {
                ganc = FindFirstObjectByType<GameAndNetworkController>();
                yield return null;
            }
            while (!ganc.IsSpawned) yield return null;
            if (OwnerClientId == 0) {
                localPlayerInfo = player1Info;
                ganc.player1Tokens.OnValueChanged += TokenCounter;
                ganc.player1TimeStops.OnValueChanged += TimeTokenCounter;
            }
            else if (OwnerClientId == 1) {
                localPlayerInfo = player2Info;
                ganc.player2Tokens.OnValueChanged += TokenCounter;
                ganc.player2TimeStops.OnValueChanged += TimeTokenCounter;
            }
            InitCameraPos();
            ganc.state.OnValueChanged += GameStateChange;
            UI = FindFirstObjectByType<UIManager>();
            UI.RegisterPlayer(OwnerClientId);
            GetComponent<PlayerInput>().enabled = true;
            gameObject.GetComponent<Camera>().enabled = true; 
            gameObject.GetComponent<AudioListener>().enabled = true;
            board1 = ganc.board1;
            board2 = ganc.board2;
            ganc.currentPlayer.OnValueChanged += UpdateTurn;
            // OnValueChanged only fires on changes, so pick up the current values too
            state = ganc.state.Value;
            UpdateTurn(ganc.currentPlayer.Value, ganc.currentPlayer.Value);
        }
        yield return null;
    }

    private void TokenCounter(int pred, int nexti) {
        tokenCount = nexti;
    } 

    private void TimeTokenCounter(int pred, int nexti) {
        timeStopCount = nexti;
    }

    private void UpdateTurn(ulong prev, ulong nextu) {
        if (nextu == OwnerClientId) isMyTurn = true;
        else isMyTurn = false;
    }

    void Update() {
        if (!IsOwner) return;
        if (Input.GetKeyDown(KeyCode.Mouse0)) OnLeftClick();
        if (Input.GetKeyDown(KeyCode.Alpha1)) SwitchMode(Mode.raise);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SwitchMode(Mode.lower);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SwitchMode(Mode.move);
        if (Input.GetKeyDown(KeyCode.E)) ZaWorldo();
        if (Input.GetKeyDown(KeyCode.W)) {
            if (Vector3.Distance(transform.position, initialPosition) < 0.1f && movingForward == null && movingBackward == null) movingForward = StartCoroutine(MoveForwardOverTime());
        }
        if (Input.GetKeyDown(KeyCode.S)) {
            if (Vector3.Distance(transform.position, otherPosition) < 0.1f && movingForward == null && movingBackward == null) movingBackward = StartCoroutine(MoveBackwardOverTime());
        }
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

    private void SwitchMode(Mode mode) {
        currentMode = mode;
        board1.SwitchDisplay(mode);
        board2.SwitchDisplay(mode);
    }

    private void RayCast() {
        if (!isMyTurn) return;
        Ray ray = GetComponent<Camera>().ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            if (hit.collider.gameObject.GetComponent<TimeTurner>() != null) {
                hit.collider.gameObject.GetComponent<TimeTurner>().TriggerAnimation();
                ganc.SwitchTurnRPC();
                return;
            }
            Vector3 blockPos = hit.collider.gameObject.transform.position;
            MazeBuilder builder = hit.collider.GetComponentInParent<MazeBuilder>();
            if (builder == null) return;
            if (!builder.TryGetGridPosition(hit.point - hit.normal * 0.01f, out Vector2Int pos)) return;
            if(tokenCount <= 0) return;
            int boardNum;
            if (builder.isBoard1) boardNum = 1;
            else boardNum = 2;
            ganc.SelectTileRPC(currentMode, pos, boardNum);

        }
    }

    private IEnumerator MoveForwardOverTime() {
        float timeElapsed = 0;
        while (timeElapsed < camMovementTime) {
            transform.position = Vector3.Lerp(transform.position, otherPosition, timeElapsed/camMovementTime);
            timeElapsed += Time.deltaTime;
            yield return null; 
        }
        transform.position = otherPosition;
        movingForward = null;
    }

    private IEnumerator MoveBackwardOverTime() {
        float timeElapsed = 0;
        while (timeElapsed < camMovementTime) {
            transform.position = Vector3.Lerp(transform.position, initialPosition, timeElapsed/camMovementTime);
            timeElapsed += Time.deltaTime;
            yield return null; 
        }
        transform.position = initialPosition;
        movingBackward = null;
    }

    private void ZaWorldo() {
        if (timeStopCount <= 0) return;
        if (!isMyTurn) return;
        ganc.TimeStopRPC();
    }
}
