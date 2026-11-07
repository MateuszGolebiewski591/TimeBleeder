using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    public void StartMultiplayerGame() {
        SceneManager.LoadSceneAsync("Lobby");
    }
}
