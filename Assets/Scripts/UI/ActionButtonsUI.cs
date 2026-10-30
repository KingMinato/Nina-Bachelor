using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Arrow buttons at the left and right screen edge for turning, jump button at the bottom.
// Buttons are disabled while an action runs, so the player sees when the next input is accepted.
// The button the current tutorial step asks for is highlighted. Starts inactive in the scene.
public class ActionButtonsUI : MonoBehaviour
{
    [SerializeField] PlayerActions playerActions;
    [SerializeField] Button turnLeftButton;
    [SerializeField] Button turnRightButton;
    [SerializeField] Button jumpButton;
    [SerializeField] TMP_Text jumpLabel;
    [Tooltip("Highlight rings, children of the three buttons.")]
    [SerializeField] GameObject turnLeftWant;
    [SerializeField] GameObject turnRightWant;
    [SerializeField] GameObject jumpWant;

    void Awake()
    {
        turnLeftButton.onClick.AddListener(playerActions.TurnLeft);
        turnRightButton.onClick.AddListener(playerActions.TurnRight);
        jumpButton.onClick.AddListener(playerActions.Jump);
    }

    public void Show(Texts texts, bool withJump)
    {
        jumpLabel.text = texts.jump;
        SetWanted("");
        gameObject.SetActive(true);
        UiSkin.Show(turnLeftButton.gameObject);
        UiSkin.Show(turnRightButton.gameObject);
        if (withJump)
            UiSkin.Show(jumpButton.gameObject);
        else
            UiSkin.Hide(jumpButton.gameObject);
    }

    public void Hide()
    {
        SetWanted("");
        UiSkin.Hide(turnLeftButton.gameObject);
        UiSkin.Hide(turnRightButton.gameObject);
        UiSkin.Hide(jumpButton.gameObject);
    }

    // Highlights the button(s) for a tutorial action (TutorialStep.TurnLeft etc.), "" for none.
    public void SetWanted(string action)
    {
        bool left = action == TutorialStep.TurnLeft || action == TutorialStep.TurnAround;
        bool right = action == TutorialStep.TurnRight || action == TutorialStep.TurnAround;
        SetWanted(turnLeftButton, turnLeftWant, left);
        SetWanted(turnRightButton, turnRightWant, right);
        SetWanted(jumpButton, jumpWant, action == TutorialStep.Jump);
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
    }
}
