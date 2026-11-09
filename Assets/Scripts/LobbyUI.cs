using UnityEngine;
using UnityEngine.SceneManagement;

public class LobbyUI : MonoBehaviour
{
    public void ReturnToMainMenu() {
        SceneManager.LoadSceneAsync("MainMenu");
    }
}
