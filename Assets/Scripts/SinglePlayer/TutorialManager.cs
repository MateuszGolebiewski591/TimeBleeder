using UnityEngine;

public class TutorialManager : MonoBehaviour
{
    public bool isTutorialActive = true;
    public GameVariable<TutorialStates> tutorialState = new GameVariable<TutorialStates>();
    private int angelsteps = 0;

    public enum TutorialStates{
        WelcomeState,
        CountDownState,
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
        FinalState,
    }

    public enum ActionType{
        RaiseBlock,
        LowerBlock,
        MovePlayer,
        ToggleRaiseBlock,
        ToggleLowerBlock,
        TogglePlayerMove,
        SwitchedBoards,
        SwitchedBoards2,
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
                if (tutorialState.Value == TutorialStates.FinalState) {
                    tutorialState.Value = TutorialStates.PlayState;
                    isTutorialActive = false;
                    return true;
                }
                break;
            }
            case ActionType.ReadyUp : {
                if (tutorialState.Value == TutorialStates.WelcomeState) {
                    tutorialState.Value = TutorialStates.CountDownState;
                    return true; 
                }
                break;
            }
            case ActionType.CountdownFinished : {
                if (tutorialState.Value == TutorialStates.CountDownState) {
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
                if (tutorialState.Value == TutorialStates.InterfereWithOpponentState) return true;
                break;
            }
            case ActionType.RaiseBlock : {
                if (tutorialState.Value == TutorialStates.BlockPlacedState) {
                    tutorialState.Value = TutorialStates.BlockLowerToggleState;
                    return true;
                }
                if (tutorialState.Value == TutorialStates.InterfereWithOpponentState) {
                    tutorialState.Value = TutorialStates.FinalState;
                    return true;
                }
                break;
            }
            case ActionType.ToggleLowerBlock : {
                if (tutorialState.Value == TutorialStates.BlockLowerToggleState) {
                    tutorialState.Value = TutorialStates.BlockLoweredState;
                    return true;
                }
                if (tutorialState.Value == TutorialStates.InterfereWithOpponentState) return true;
                break;
            }
            case ActionType.LowerBlock : {
                if (tutorialState.Value == TutorialStates.BlockLoweredState) {
                    tutorialState.Value = TutorialStates.CharacterMoveToggleState;
                    return true;
                }
                if (tutorialState.Value == TutorialStates.InterfereWithOpponentState) {
                    tutorialState.Value = TutorialStates.FinalState;
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
            case ActionType.TurnEnded : {
                if (tutorialState.Value == TutorialStates.EndTurnState) {
                    tutorialState.Value = TutorialStates.SwitchBoardState;
                    return true;
                }
                break;
            }
            case ActionType.SwitchedBoards : {
                if (tutorialState.Value == TutorialStates.SwitchBoardState) {
                    tutorialState.Value = TutorialStates.InterfereWithOpponentState;
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
