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
        NetworkManager.Singleton.ConnectionApprovalCallback += ApproveRequest;
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
    }

    private void ApproveRequest(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        response.Approved = true;
        response.CreatePlayerObject = false; //We spawn player in manually
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
}
