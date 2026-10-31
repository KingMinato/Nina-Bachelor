using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Arrow buttons at the left and right screen edge for turning, jump and crouch buttons at the bottom.
// Buttons are disabled while an action runs, so the player sees when the next input is accepted.
// The button the current tutorial step asks for is highlighted. Starts inactive in the scene.
public class ActionButtonsUI : MonoBehaviour
{
    [SerializeField] PlayerActions playerActions;
    [SerializeField] Button turnLeftButton;
    [SerializeField] Button turnRightButton;
    [SerializeField] Button jumpButton;
    [SerializeField] TMP_Text jumpLabel;
    [SerializeField] Button crouchButton;
    [SerializeField] TMP_Text crouchLabel;
    [Tooltip("Highlight rings, children of the buttons.")]
    [SerializeField] GameObject turnLeftWant;
    [SerializeField] GameObject turnRightWant;
    [SerializeField] GameObject jumpWant;
    [SerializeField] GameObject crouchWant;

    Texts texts;

    void Awake()
    {
        turnLeftButton.onClick.AddListener(playerActions.TurnLeft);
        turnRightButton.onClick.AddListener(playerActions.TurnRight);
        jumpButton.onClick.AddListener(playerActions.Jump);
        crouchButton.onClick.AddListener(playerActions.Crouch);
    }

    public void Show(Texts texts)
    {
        this.texts = texts;
        jumpLabel.text = texts.jump;
        SetWanted("");
        gameObject.SetActive(true);
        foreach (var button in new[] { turnLeftButton, turnRightButton, jumpButton, crouchButton })
            UiSkin.Show(button.gameObject);
    }

    public void Hide()
    {
        SetWanted("");
        UiSkin.Hide(turnLeftButton.gameObject);
        UiSkin.Hide(turnRightButton.gameObject);
        UiSkin.Hide(jumpButton.gameObject);
        UiSkin.Hide(crouchButton.gameObject);
    }

    // Highlights the button(s) for a tutorial action (TutorialStep.TurnLeft etc.), "" for none.
    public void SetWanted(string action)
    {
        bool left = action == TutorialStep.TurnLeft || action == TutorialStep.TurnAround;
        bool right = action == TutorialStep.TurnRight || action == TutorialStep.TurnAround;
        SetWanted(turnLeftButton, turnLeftWant, left);
        SetWanted(turnRightButton, turnRightWant, right);
        // crouchJump: first crouch, then jump.
        bool crouched = playerActions.IsCrouched;
        SetWanted(jumpButton, jumpWant, action == TutorialStep.Jump || action == TutorialStep.CrouchJump && crouched);
        SetWanted(crouchButton, crouchWant, action == TutorialStep.Crouch || action == TutorialStep.CrouchJump && !crouched);
    }

    static void SetWanted(Button button, GameObject ring, bool wanted)
    {
        ring.SetActive(wanted);
        foreach (var skin in button.GetComponentsInChildren<UiSkin>(true))
            skin.Wanted = wanted;
    }

    void Update()
    {
        bool ready = !playerActions.IsBusy;
        turnLeftButton.interactable = ready;
        turnRightButton.interactable = ready;
        jumpButton.interactable = ready;
        crouchButton.interactable = ready;
        if (texts != null)
            crouchLabel.text = playerActions.IsCrouched ? texts.standUp : texts.crouch;
    }
}
