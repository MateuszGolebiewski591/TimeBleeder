using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement;

public class UIManager : MonoBehaviour
{
    [SerializeField] private GameObject readyUpPanel;
    [SerializeField] private GameObject countDownPanel;
    [SerializeField] private GameObject mainGamePanel;
    [SerializeField] private GameObject gameEndPanel;
    [SerializeField] private GameObject mainMenuButton;

    [SerializeField] private TextMeshProUGUI readyUpPanelText;
    [SerializeField] private TextMeshProUGUI countDownPanelText;
    [SerializeField] private TextMeshProUGUI mainTimerText;
    [SerializeField] private TextMeshProUGUI currentPlayerTime;
    [SerializeField] private TextMeshProUGUI opponentTime;
    [SerializeField] private TextMeshProUGUI winnerText;
    [SerializeField] private TextMeshProUGUI tokenText;

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
        ganc.player1TimeGained.OnValueChanged += UpdatePlayer1Time;
        ganc.player2TimeGained.OnValueChanged += UpdatePlayer2Time;
        ganc.winnerDeclared.OnValueChanged += UpdateGameOverScreen;
        ganc.player1Tokens.OnValueChanged += TokenCount1;
        ganc.player2Tokens.OnValueChanged += TokenCount2;
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
        if (newValue == GameAndNetworkController.GameState.GameEnd) {
            mainGamePanel.SetActive(false);
            gameEndPanel.SetActive(true);
        }
    }

    private void UpdateTimer(int prev, int next) {
        countDownPanelText.text = $"{next}";
    }

    private void UpdateGlobalTime(float prev, float nextf) {
        int next = (int)nextf;
        mainTimerText.text = $"{next/60}:{next%60:D2}";
    }

    private void UpdatePlayer1Time(float prev, float nextf) {
        int next = (int)nextf;
        if (player == 0) currentPlayerTime.text = $"Your Time: {next/60}:{next%60:D2}";
        else opponentTime.text = $"Enemy Time: {next/60}:{next%60:D2}";
    }

    private void UpdatePlayer2Time(float prev, float nextf) {
        int next = (int)nextf;
        if (player == 1) currentPlayerTime.text = $"Your Time: {next/60}:{next%60:D2}";
        else opponentTime.text = $"Your Time: {next/60}:{next%60:D2}";
    }

    private void UpdateGameOverScreen(ulong prev, ulong winner) {
        if (winner == player) winnerText.text = "You Win";
        else winnerText.text = "You Lose";
        StartCoroutine(MainMenuTimer());
    }

    private IEnumerator MainMenuTimer() {
        yield return new WaitForSeconds(5f);
        mainMenuButton.SetActive(true);
    }

    public void ReturnToMainMenu() {
        SceneManager.LoadSceneAsync("MainMenu");
    }

    private void TokenCount1(int prev, int nexti) {
        if (player == 1) return; 
        tokenText.text = $"Tokens: {nexti}";
    }

    private void TokenCount2(int prev, int nexti) {
        if (player == 0) return;
        tokenText.text = $"Tokens: {nexti}";
    }
}
