using UnityEngine;
public class BoardController : MonoBehaviour
{
    [Header("Characters")]
    public GameObject player;
    public GameObject angel;
    public AngelMovement AngelMovement { get; private set; }

    [SerializeField] private MazeBuilder mazeBuilder;

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
    }

    // Shows which tiles can be used in this mode
    public void SwitchDisplay(PlayerController.Mode mode) {
        currentMode = mode;

        MazeBuilder builder = GetMazeBuilder();
        if (builder != null) builder.SetHighlightMode(mode);
    }

    public void HideDisplay() {
        MazeBuilder builder = GetMazeBuilder();
        if (builder != null) builder.ClearHighlights();
    }

    private MazeBuilder GetMazeBuilder()
    {
        if (mazeBuilder != null) return mazeBuilder;

        if (ganc == null) ganc = FindFirstObjectByType<GameAndNetworkController>();
        if (ganc == null) return null;

        bool isBoard1 = ganc.board1 == this;
        foreach (MazeBuilder builder in FindObjectsByType<MazeBuilder>(FindObjectsSortMode.None))
        {
            if (builder.isBoard1 == isBoard1)
            {
                mazeBuilder = builder;
                break;
            }
        }
        return mazeBuilder;
    }
}
