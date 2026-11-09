using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    [SerializeField] GameObject mainMenuPanel;
    [SerializeField] GameObject acknoweledgementsPanel;
    
    public void StartMultiplayerGame() {
        SceneManager.LoadSceneAsync("Lobby");
    }

    public void ShowAcknowledgements() {
        mainMenuPanel.SetActive(false);
        acknoweledgementsPanel.SetActive(true);
    }

    public void HideAcknowledgements() {
        acknoweledgementsPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
    }

    public void QuitGame() {
        Application.Quit();
    }
}
