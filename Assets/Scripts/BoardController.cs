using UnityEngine;
using Unity.Netcode;

public class BoardController : MonoBehaviour
{
    [Header("Characters")]
    public GameObject player;
    public GameObject angel;
    public AngelMovement AngelMovement { get; private set; }

    [Header("Networking")]
    private GameAndNetworkController ganc;
    private PlayerController.Mode currentMode = PlayerController.Mode.raise;

    void Awake()
    {
        if (angel != null)
            AngelMovement = angel.GetComponent<AngelMovement>();
    }

    void Start()
    {
        ganc = FindFirstObjectByType<GameAndNetworkController>();
        ganc.Board1Blocks.OnListChanged += UpdateBoard1;
        ganc.Board2Blocks.OnListChanged += UpdateBoard2;
    }

    public void SwitchDisplay(PlayerController.Mode mode) {
        currentMode = mode;
        switch (mode) {
            case PlayerController.Mode.raise : {
                break;
            }
            case PlayerController.Mode.lower : {
                break;
            }
            case PlayerController.Mode.move : {
                break;
            }
        }
    }

    private void UpdateBoard1(NetworkListEvent<BlockData> changeEvent) {

    }

    private void UpdateBoard2(NetworkListEvent<BlockData> changeEvent) {

    }
}
