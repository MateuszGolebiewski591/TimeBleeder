using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement;
using System;
using System.Collections.Generic;

public class UIManagerSingle : MonoBehaviour
{
    [SerializeField] private GameObject readyUpPanel;
    [SerializeField] private GameObject countDownPanel;
    [SerializeField] private GameObject mainGamePanel;
    [SerializeField] private GameObject gameEndPanel;
    [SerializeField] private GameObject mainMenuButton;
    [SerializeField] private GameObject timePanel;

    [SerializeField] private TextMeshProUGUI readyUpPanelText;
    [SerializeField] private TextMeshProUGUI countDownPanelText;
    [SerializeField] private TextMeshProUGUI mainTimerText;
    [SerializeField] private TextMeshProUGUI currentPlayerTime;
    [SerializeField] private TextMeshProUGUI opponentTime;
    [SerializeField] private TextMeshProUGUI winnerText;
    [SerializeField] private TextMeshProUGUI tokenText;
    [SerializeField] private TextMeshProUGUI timeStopText; 

    public GameController ganc; 
    private ulong player;

    private void Start()
    {
        ganc = FindFirstObjectByType<GameController>();

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
        ganc.player1TimeStops.OnValueChanged += TimeStopCount1;
        ganc.player2TimeStops.OnValueChanged += TimeStopCount2;
        ganc.isTimeStop.OnValueChanged += TimeStop;
    }

    private void UpdateReadyUpPanel1(bool newStatus) {
        if (player == 0) {
            readyUpPanelText.text = "Waiting On Opponent";
        }
    }
    private void UpdateReadyUpPanel2(bool newStatus) {
        if (player == 1) {
            readyUpPanelText.text = "Waiting On Opponent";
        }
    }

    public void RegisterPlayer(ulong playerID) {
        player = playerID;
    }

    public void TimeStop(int nexti) {
        if (nexti == 1) timePanel.SetActive(true);
        else timePanel.SetActive(false);
    }

    private void GameStateChange(GameController.GameState newValue) {
        if (newValue == GameController.GameState.CountDown) {
            readyUpPanel.SetActive(false);
            countDownPanel.SetActive(true);
        }
        if (newValue == GameController.GameState.GameActive) {
            countDownPanel.SetActive(false);
            mainGamePanel.SetActive(true);
        }
        if (newValue == GameController.GameState.GameEnd) {
            mainGamePanel.SetActive(false);
            timePanel.SetActive(false);
            gameEndPanel.SetActive(true);
        }
    }

    private void UpdateTimer(int next) {
        countDownPanelText.text = $"{next}";
    }

    private void UpdateGlobalTime(float nextf) {
        int next = (int)nextf;
        mainTimerText.text = $"{next/60}:{next%60:D2}";
    }

    private void UpdatePlayer1Time(float nextf) {
        int next = (int)nextf;
        if (player == 0) currentPlayerTime.text = $"Your Time: {next/60}:{next%60:D2}";
        else opponentTime.text = $"Enemy Time: {next/60}:{next%60:D2}";
    }

    private void UpdatePlayer2Time(float nextf) {
        int next = (int)nextf;
        if (player == 1) currentPlayerTime.text = $"Your Time: {next/60}:{next%60:D2}";
        else opponentTime.text = $"Enemy Time: {next/60}:{next%60:D2}";
    }

    private void UpdateGameOverScreen(ulong winner) {
        if (winner == player) winnerText.text = "You Win";
        else winnerText.text = "You Lose";
        StartCoroutine(MainMenuTimer());
    }

    private IEnumerator MainMenuTimer() {
        yield return new WaitForSeconds(5f);
        mainMenuButton.SetActive(true);
    }

    public async void ReturnToMainMenu() {
        mainMenuButton.SetActive(false); // Stop double clicks while we leave
        SceneManager.LoadSceneAsync("MainMenu");
    }

    private void TokenCount1(int nexti) {
        if (player == 1) return; 
        tokenText.text = $"Tokens: {nexti}";
    }

    private void TokenCount2(int nexti) {
        if (player == 0) return;
        tokenText.text = $"Tokens: {nexti}";
    }

    private void TimeStopCount1(int nexti) {
        if (player == 1) return; 
        timeStopText.text = $"E - Stop Time ({nexti})";
    }

    private void TimeStopCount2(int nexti) {
        if (player == 0) return; 
        timeStopText.text = $"E - Stop Time ({nexti})";
    }
}
