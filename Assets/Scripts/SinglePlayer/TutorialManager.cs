using UnityEngine;

public class TutorialManager : MonoBehaviour
{
    public bool isTutorialActive = true;
    public GameVariable<TutorialStates> tutorialState = new GameVariable<TutorialStates>();
    private int angelsteps = 0;

    public enum TutorialStates{
        WelcomeState,
        PlayState,
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
        ToggleRaiseBlock,
        ToggleLowerBlock,
        TogglePlayerMove,
        SwitchedBoards,
        TurnEnded,
        LeftClick,
        CountdownFinished,
        TickAngel,
        ReadyUp,
    }

    public bool ActionRecorded(ActionType type) {
        if (!isTutorialActive) return true;
        switch (type) {
            case ActionType.LeftClick : {
                if (tutorialState.Value == TutorialStates.WelcomeState) {
                    return true; 
                }
                if (tutorialState.Value == TutorialStates.IntroducePlayerState) {
                    tutorialState.Value = TutorialStates.IntroduceAngelState;
                    return true;
                }
                if (tutorialState.Value == TutorialStates.IntroduceAngelState) {
                    tutorialState.Value = TutorialStates.BlockPlaceToggleState;
                    return true;
                }
                break;
            }
            case ActionType.ReadyUp : {
                if (tutorialState.Value == TutorialStates.WelcomeState) {
                    return true; 
                }
                break;
            }
            case ActionType.CountdownFinished : {
                if (tutorialState.Value == TutorialStates.WelcomeState) {
                    tutorialState.Value = TutorialStates.PlayState;
                    return true; 
                }
                break;
            }
            case ActionType.TickAngel : {
                if (!(tutorialState.Value == TutorialStates.PlayState)) return false;
                if (angelsteps < 3) angelsteps++;
                else tutorialState.Value = TutorialStates.IntroducePlayerState;
                return true; 
            }
            case ActionType.ToggleRaiseBlock : {
                if (tutorialState.Value == TutorialStates.BlockPlaceToggleState) {
                    tutorialState.Value = TutorialStates.BlockPlacedState;
                    return true;
                }
                break;
            }
            case ActionType.RaiseBlock : {
                if (tutorialState.Value == TutorialStates.BlockPlacedState) {
                    tutorialState.Value = TutorialStates.BlockLowerToggleState;
                    return true;
                }
                break;
            }
            case ActionType.ToggleLowerBlock : {
                if (tutorialState.Value == TutorialStates.BlockLowerToggleState) {
                    tutorialState.Value = TutorialStates.BlockLoweredState;
                    return true;
                }
                break;
            }
            case ActionType.LowerBlock : {
                if (tutorialState.Value == TutorialStates.BlockLoweredState) {
                    tutorialState.Value = TutorialStates.CharacterMoveToggleState;
                    return true;
                }
                break;
            }
            case ActionType.TogglePlayerMove : {
                if (tutorialState.Value == TutorialStates.CharacterMoveToggleState) {
                    tutorialState.Value = TutorialStates.CharacterMovedState;
                    return true;
                }
                break;
            }
            case ActionType.MovePlayer : {
                if (tutorialState.Value == TutorialStates.CharacterMovedState) {
                    tutorialState.Value = TutorialStates.EndTurnState;
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
