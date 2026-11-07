using UnityEngine;
using TMPro;

public class UIManager : MonoBehaviour
{
    [SerializeField] private GameObject readyUpPanel;
    [SerializeField] private GameObject countDownPanel;
    [SerializeField] private GameObject mainGamePanel;


    [SerializeField] private TextMeshProUGUI readyUpPanelText;
    [SerializeField] private TextMeshProUGUI countDownPanelText;
    [SerializeField] private TextMeshProUGUI mainTimerText;
    [SerializeField] private TextMeshProUGUI currentPlayerTime;
    [SerializeField] private TextMeshProUGUI opponentTime;

    private GameAndNetworkController ganc; 
    private ulong player;

    private void Start()
    {
        ganc = FindFirstObjectByType<GameAndNetworkController>();

        ganc.player1Ready.OnValueChanged += UpdateReadyUpPanel1;
        ganc.player2Ready.OnValueChanged += UpdateReadyUpPanel2;
        ganc.state.OnValueChanged += GameStateChange;
        ganc.countDownTime.OnValueChanged += UpdateTimer;
        ganc.GlobalTime.OnValueChanged += UpdateGlobalTime;
    }

    private void UpdateReadyUpPanel1(bool previousStatus, bool newStatus) {
        if (player == 0) {
            readyUpPanelText.text = "Waiting On Opponent";
        }
    }
    private void UpdateReadyUpPanel2(bool previousStatus, bool newStatus) {
        if (player == 1) {
            readyUpPanelText.text = "Waiting On Opponent";
        }
    }

    public void RegisterPlayer(ulong playerID) {
        player = playerID;
    }

    private void GameStateChange(GameAndNetworkController.GameState prev, GameAndNetworkController.GameState newValue) {
        if (newValue == GameAndNetworkController.GameState.CountDown) {
            readyUpPanel.SetActive(false);
            countDownPanel.SetActive(true);
        }
        if (newValue == GameAndNetworkController.GameState.GameActive) {
            countDownPanel.SetActive(false);
            mainGamePanel.SetActive(true);
        }
    }

    private void UpdateTimer(int prev, int next) {
        countDownPanelText.text = $"{next}";
    }

    private void UpdateGlobalTime(float prev, float nextf) {
        int next = (int)nextf;
        mainTimerText.text = $"{(int)90/60}:{next%60}";
    }
}
