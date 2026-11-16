using UnityEngine;

public class TutorialManager : MonoBehaviour
{
    public bool isTutorialActive = true;
    public GameVariable<TutorialStates> tutorialState = new GameVariable<TutorialStates>();

    public enum TutorialStates{
        WelcomeState,
        IntroducePlayerState,
        IntroduceAngelState,
        BlockPlaceToggleState,
        BlockPlacedState,
        BlockLowerToggleState,
        BlockLoweredState,
        CharacterMoveToggleState,
        CharacterMovedState,
        EndTurnState,
        SwitchBoardState,
        InterfereWithOpponentState,
        LetAngelCaptureState
    }

    public enum ActionType{
        RaiseBlock,
        LowerBlock,
        MovePlayer,
        SwitchedBoards,
        TurnEnded,
        LeftClick,
        CountdownFinished,
    }

    public bool ActionRecorded(ActionType type) {
        if (!isTutorialActive) return true;
        switch (type) {
            case ActionType.LeftClick: {
                if (tutorialState.Value == TutorialStates.WelcomeState) {
                    return true; 
                }
                break;
            }
            case ActionType.CountdownFinished : {
                if (tutorialState.Value == TutorialStates.WelcomeState) {
                    tutorialState.Value = TutorialStates.IntroducePlayerState;
                    return true; 
                }
                break;
            }
        }
        return false;
    }

    void Start() {
        tutorialState.Value = TutorialStates.WelcomeState;
    }
}
