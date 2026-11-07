using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class NetworkAndGameController : NetworkBehaviour
{
    [SerializeField] private Transform playerPrefab;
    [SerializeField] private string gameSceneName;
    public override void OnNetworkSpawn()
    {
        if (IsServer) NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += SceneManager_OnLoadEventCompleted;
    }

    private void SceneManager_OnLoadEventCompleted(string sceneName, UnityEngine.SceneManagement.LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        if (!IsServer || sceneName != gameSceneName) return;
        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            if (NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject != null) continue;
            Transform player = Instantiate(playerPrefab);
            player.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId, true);
            Debug.Log($"Spawned player for client {clientId}");
        }
    }
}
