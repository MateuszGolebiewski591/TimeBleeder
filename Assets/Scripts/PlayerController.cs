using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class PlayerController : NetworkBehaviour
{
    private GameAndNetworkController ganc;   
    public PlayerInfo player1Info;
    public PlayerInfo player2Info;
    public PlayerInfo localPlayerInfo;
    private Vector3 initialPosition;
    private Vector3 otherPosition;

    private IEnumerator Start()
    {
        if (IsOwner) {
            while (ganc == null)
            {
                ganc = FindFirstObjectByType<GameAndNetworkController>();
                yield return null;
            }
            while (!ganc.IsSpawned) yield return null;
            if (OwnerClientId == 0) localPlayerInfo = player1Info;
            else if (OwnerClientId == 1) localPlayerInfo = player2Info;
            InitCameraPos();
            gameObject.GetComponent<Camera>().enabled = true; 
            gameObject.GetComponent<AudioListener>().enabled = true;
        }
        yield return null;
    }

    private void InitCameraPos() {
        initialPosition = localPlayerInfo.mainPosition;
        otherPosition = localPlayerInfo.otherPosition;
        transform.position = initialPosition;
        transform.rotation = localPlayerInfo.rotation;
    }
    
}
