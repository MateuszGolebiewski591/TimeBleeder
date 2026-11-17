using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement;
using System;
using System.Collections.Generic;

public class UIManagerSingle : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject readyUpPanel;
    [SerializeField] private GameObject countDownPanel;
    [SerializeField] private GameObject mainGamePanel;
    [SerializeField] private GameObject gameEndPanel;
    [SerializeField] private GameObject mainMenuButton;
    [SerializeField] private GameObject timePanel;
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject helpPanel;
    [SerializeField] private GameObject tutorialPanels;

    [Header("Text")]
    [SerializeField] private TextMeshProUGUI readyUpPanelText;
    [SerializeField] private TextMeshProUGUI countDownPanelText;
    [SerializeField] private TextMeshProUGUI mainTimerText;
    [SerializeField] private TextMeshProUGUI currentPlayerTime;
    [SerializeField] private TextMeshProUGUI opponentTime;
    [SerializeField] private TextMeshProUGUI winnerText;
    [SerializeField] private TextMeshProUGUI tokenText;
    [SerializeField] private TextMeshProUGUI timeStopText; 

    [Header("Tutorial Panels")]
    [SerializeField] private GameObject introductionPanel; 
    [SerializeField] private GameObject showPlayerPanel;
    [SerializeField] private GameObject showAngelPanel;
    [SerializeField] private GameObject toggleBlockPlacePanel;
    [SerializeField] private GameObject placeBlockPanel;
    [SerializeField] private GameObject toggleBlockLowerPanel;
    [SerializeField] private GameObject lowerBlockPanel;
    [SerializeField] private GameObject togglePlayerMovePanel;
    [SerializeField] private GameObject playerMovePanel;
    [SerializeField] private GameObject turnEndPanel;
    [SerializeField] private GameObject switchBoardPanel;
    [SerializeField] private GameObject interferePanel;
    [SerializeField] private GameObject finalPanel;

    public GameController ganc; 
    private ulong player;
    private bool paused = false;
    private GameObject currentPanel;
    private GameObject returnPanel;
    public TutorialManager TM;

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
        currentPanel = readyUpPanel;
        TM = FindFirstObjectByType<TutorialManager>();
        TM.tutorialState.OnValueChanged += TutorialProgress;
        if (TM.isTutorialActive) tutorialPanels.SetActive(true);
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
            SetPanel(countDownPanel);
        }
        if (newValue == GameController.GameState.GameActive) {
            SetPanel(mainGamePanel);
        }
        if (newValue == GameController.GameState.GameEnd) {
            timePanel.SetActive(false);
            SetPanel(gameEndPanel);
        }
    }

    private void UpdateTimer(int next) {
        countDownPanelText.text = $"{next}";
    }

    private void UpdateGlobalTime(float nextf) {
        int next = (int)nextf;
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
        timeStopText.text = $"Time Stops ({nexti})";
    }

    private void TimeStopCount2(int nexti) {
        if (player == 0) return; 
        timeStopText.text = $"Time Stops ({nexti})";
    }

    public void UnPause() {
        if (SetPanel(returnPanel)) paused = false;
    }

    public void Pause() {
        if (paused) {
            UnPause();
            return;
        }
        if (SetPanel(pausePanel)) paused = true;
    }

    private bool SetPanel(GameObject newPanel) {
        if (currentPanel != mainGamePanel && newPanel == pausePanel) return false;
        currentPanel.SetActive(false);
        returnPanel = currentPanel;
        currentPanel = newPanel;
        currentPanel.SetActive(true);
        return true;
    }

    public void ToggleHelpPanel() {
        helpPanel.SetActive(!helpPanel.activeInHierarchy);
    }

    private void TutorialProgress(TutorialManager.TutorialStates newState) {
        switch (newState) {
            case TutorialManager.TutorialStates.IntroducePlayerState : {
                introductionPanel.SetActive(false);
                showPlayerPanel.SetActive(true);
                break;
            }
            case TutorialManager.TutorialStates.IntroduceAngelState : {
                showAngelPanel.SetActive(true);
                showPlayerPanel.SetActive(false);
                break;
            }
            case TutorialManager.TutorialStates.PlayState : {
                finalPanel.SetActive(false);
                break;
            }
            case TutorialManager.TutorialStates.CountDownState : {
                introductionPanel.SetActive(false);
                break;
            }
            case TutorialManager.TutorialStates.BlockPlaceToggleState : {
                showAngelPanel.SetActive(false);
                toggleBlockPlacePanel.SetActive(true);
                break;
            }
            case TutorialManager.TutorialStates.BlockPlacedState : {
                toggleBlockPlacePanel.SetActive(false);
                placeBlockPanel.SetActive(true);
                break;
            }
            case TutorialManager.TutorialStates.BlockLowerToggleState : {
                placeBlockPanel.SetActive(false);
                toggleBlockLowerPanel.SetActive(true);
                break;
            }
            case TutorialManager.TutorialStates.BlockLoweredState : {
                toggleBlockLowerPanel.SetActive(false);
                lowerBlockPanel.SetActive(true);
                break;
            } 
            case TutorialManager.TutorialStates.CharacterMoveToggleState : {
                lowerBlockPanel.SetActive(false);
                togglePlayerMovePanel.SetActive(true);
                break;
            }
            case TutorialManager.TutorialStates.CharacterMovedState : {
                togglePlayerMovePanel.SetActive(false);
                playerMovePanel.SetActive(true);
                break;
            }
            case TutorialManager.TutorialStates.EndTurnState : {
                playerMovePanel.SetActive(false);
                turnEndPanel.SetActive(true);
                break;
            }
            case TutorialManager.TutorialStates.SwitchBoardState : {
                turnEndPanel.SetActive(false);
                switchBoardPanel.SetActive(true);
                break;
            }
            case TutorialManager.TutorialStates.InterfereWithOpponentState : {
                switchBoardPanel.SetActive(false); 
                interferePanel.SetActive(true);
                break;
            }
            case TutorialManager.TutorialStates.FinalState : {
                interferePanel.SetActive(false);
                finalPanel.SetActive(true);
                break;
            }
        }
    }
}
