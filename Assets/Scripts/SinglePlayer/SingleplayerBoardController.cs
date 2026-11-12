using UnityEngine;
public class SinglePlayerBoardController : MonoBehaviour
{
    [Header("Characters")]
    public GameObject player;
    public GameObject angel;
    public AngelMovement AngelMovement { get; private set; }
    public AngelMovement PlayerMovement { get; private set; } // Same slide-and-turn script as the angel

    [SerializeField] private SinglePlayerMazeBuilder mazeBuilder;

    [Header("Networking")]
    private GameController ganc;
    private SinglePlayerController.Mode currentMode = SinglePlayerController.Mode.raise;

    void Awake()
    {
        if (angel != null)
        {
            AngelMovement = angel.GetComponent<AngelMovement>();
            if (AngelMovement == null) AngelMovement = angel.AddComponent<AngelMovement>();
        }
        if (player != null)
        {
            PlayerMovement = player.GetComponent<AngelMovement>();
            if (PlayerMovement == null) PlayerMovement = player.AddComponent<AngelMovement>();
        }
    }

    void Start()
    {
        ganc = FindFirstObjectByType<GameController>();
    }

    // Shows which tiles can be used in this mode
    public void SwitchDisplay(SinglePlayerController.Mode mode) {
        currentMode = mode;

        SinglePlayerMazeBuilder builder = GetMazeBuilder();
        if (builder != null) builder.SetHighlightMode(mode);
    }

    public void HideDisplay() {
        SinglePlayerMazeBuilder builder = GetMazeBuilder();
        if (builder != null) builder.ClearHighlights();
    }

    public SinglePlayerMazeBuilder GetMazeBuilder()
    {
        if (mazeBuilder != null) return mazeBuilder;

        if (ganc == null) ganc = FindFirstObjectByType<GameController>();
        if (ganc == null) return null;

        bool isBoard1 = ganc.board1 == this;
        foreach (SinglePlayerMazeBuilder builder in FindObjectsByType<SinglePlayerMazeBuilder>(FindObjectsSortMode.None))
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
