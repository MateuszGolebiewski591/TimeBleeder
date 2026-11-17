using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;

public class NetworkConnector : NetworkBehaviour
{
    [SerializeField] private string gameSceneName;
    private bool gameStarting = false;

    public static NetworkConnector Instance {get; set;}

    void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        NetworkManager.Singleton.OnServerStarted += OnServerStarted;
    }

    public override void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
        }
        if (Instance == this) Instance = null;
        base.OnDestroy();
    }

    // Each new host session can start a game again
    private void OnServerStarted()
    {
        gameStarting = false;
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!NetworkManager.Singleton.IsServer) return;
        if (NetworkManager.Singleton.ConnectedClientsIds.Count == 2 && !gameStarting)
        {
            gameStarting = true;
            NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        // Only handle the local client's disconnection.
        if (clientId != NetworkManager.Singleton.LocalClientId)
            return;

        // Don't react to a normal host shutdown.
        if (NetworkManager.Singleton.IsServer)
            return;
        FindFirstObjectByType<UIManager>().ReturnToMainMenu();
    }
}
