using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;

public class SinglePlayerController : MonoBehaviour
{
    [SerializeField] private float camMovementTime = 0.4f;
    [SerializeField] private GameController ganc;   
    private GameController.GameState state = GameController.GameState.ReadyUp;
    private SinglePlayerBoardController board1;
    private SinglePlayerBoardController board2;
    [SerializeField] private UIManagerSingle UI;
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


    void Start()
    {
        ganc.player1Tokens.OnValueChanged += TokenCounter;
        ganc.player1TimeStops.OnValueChanged += TimeTokenCounter;   
        InitCameraPos();
        ganc.state.OnValueChanged += GameStateChange;
        UI.RegisterPlayer(0);
        board1 = ganc.board1;
        board2 = ganc.board2;
        ganc.currentPlayer.OnValueChanged += UpdateTurn;
        state = ganc.state.Value;
        UpdateTurn(ganc.currentPlayer.Value);
    }

    private void TokenCounter(int nexti) {
        tokenCount = nexti;
    } 

    private void TimeTokenCounter(int nexti) {
        timeStopCount = nexti;
    }

    private void UpdateTurn(ulong nextu) {
        if (nextu == 0) isMyTurn = true;
        else isMyTurn = false;
    }

    void Update() {
        if (Input.GetKeyDown(KeyCode.Escape)) UI.Pause();
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

    private void GameStateChange(GameController.GameState newValue) {
        state = newValue;
    }

    private void OnLeftClick() {
        if (state == GameController.GameState.ReadyUp) ganc.ReadyRPC(0);
        else if (state == GameController.GameState.GameActive) RayCast();
    }

    private void SwitchMode(Mode mode) {
        currentMode = mode;
        board1.SwitchDisplay(mode);
        board2.SwitchDisplay(mode);
    }

    private void RayCast() {
        Ray ray = GetComponent<Camera>().ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            if (hit.collider.gameObject.GetComponent<TimeTurnerSingle>() != null) {
                ganc.SwitchTurnRPC(0);
                return;
            }
            Vector3 blockPos = hit.collider.gameObject.transform.position;
            SinglePlayerMazeBuilder builder = hit.collider.GetComponentInParent<SinglePlayerMazeBuilder>();
            if (builder == null) return;
            if (!builder.TryGetGridPosition(hit.point - hit.normal * 0.01f, out Vector2Int pos)) return;
            if(tokenCount <= 0) return;
            int boardNum;
            if (builder.isBoard1) boardNum = 1;
            else boardNum = 2;
            if (boardNum == 1 && !isMyTurn) return; 
            ganc.SelectTileRPC(currentMode, pos, boardNum, 0);
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
        ganc.TimeStopRPC(0);
    }
}
