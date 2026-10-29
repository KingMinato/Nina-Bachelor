using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Turn left / jump / turn right buttons for the tutorial, plus A/D/Space (and arrow keys) on PC.
// Buttons are disabled while an action runs, so the player sees when the next input is accepted.
// Starts inactive in the scene, TutorialController shows it.
public class ActionButtonsUI : MonoBehaviour
{
    [SerializeField] PlayerActions playerActions;
    [SerializeField] Button turnLeftButton;
    [SerializeField] Button jumpButton;
    [SerializeField] Button turnRightButton;
    [SerializeField] TMP_Text turnLeftLabel;
    [SerializeField] TMP_Text jumpLabel;
    [SerializeField] TMP_Text turnRightLabel;

    void Awake()
    {
        turnLeftButton.onClick.AddListener(playerActions.TurnLeft);
        jumpButton.onClick.AddListener(playerActions.Jump);
        turnRightButton.onClick.AddListener(playerActions.TurnRight);
    }

    public void Show(Texts texts)
    {
        turnLeftLabel.text = texts.turnLeft;
        jumpLabel.text = texts.jump;
        turnRightLabel.text = texts.turnRight;
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    void Update()
    {
        bool ready = !playerActions.IsBusy;
        turnLeftButton.interactable = ready;
        jumpButton.interactable = ready;
        turnRightButton.interactable = ready;

        var kb = Keyboard.current;
        if (kb == null || !ready)
            return;
        if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame)
            playerActions.TurnLeft();
        else if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame)
            playerActions.TurnRight();
        else if (kb.spaceKey.wasPressedThisFrame)
            playerActions.Jump();
    }
}
