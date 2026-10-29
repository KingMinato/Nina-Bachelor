using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Arrow buttons at the left and right screen edge for turning, jump button at the bottom.
// Buttons are disabled while an action runs, so the player sees when the next input is accepted.
// Starts inactive in the scene.
public class ActionButtonsUI : MonoBehaviour
{
    [SerializeField] PlayerActions playerActions;
    [SerializeField] Button turnLeftButton;
    [SerializeField] Button turnRightButton;
    [SerializeField] Button jumpButton;
    [SerializeField] TMP_Text jumpLabel;

    void Awake()
    {
        turnLeftButton.onClick.AddListener(playerActions.TurnLeft);
        turnRightButton.onClick.AddListener(playerActions.TurnRight);
        jumpButton.onClick.AddListener(playerActions.Jump);
    }

    public void Show(Texts texts, bool withJump)
    {
        jumpLabel.text = texts.jump;
        jumpButton.gameObject.SetActive(withJump);
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
        turnRightButton.interactable = ready;
        jumpButton.interactable = ready;
    }
}
