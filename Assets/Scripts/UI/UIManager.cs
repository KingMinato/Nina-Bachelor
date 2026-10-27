using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Placeholder UI: one prompt panel with a message and an optional button.
public class UIManager : MonoBehaviour
{
    [SerializeField] GameObject promptPanel;
    [SerializeField] TMP_Text promptText;
    [SerializeField] Button promptButton;
    [SerializeField] TMP_Text promptButtonLabel;

    [Tooltip("Button stays disabled this long after a prompt appears, so a double tap cannot skip the next prompt.")]
    [SerializeField] float inputDelaySeconds = 0.4f;

    void Awake()
    {
        promptPanel.SetActive(false);
    }

    // Shows the message and completes when the button is pressed.
    public async Awaitable ShowPrompt(string message, string buttonLabel)
    {
        promptText.text = message;
        promptButtonLabel.text = buttonLabel;
        promptButton.gameObject.SetActive(true);
        promptButton.interactable = false;
        promptPanel.SetActive(true);

        await Awaitable.WaitForSecondsAsync(inputDelaySeconds);
        promptButton.interactable = true;

        var clicked = new AwaitableCompletionSource();
        void OnClick() => clicked.TrySetResult();
        promptButton.onClick.AddListener(OnClick);
        await clicked.Awaitable;
        promptButton.onClick.RemoveListener(OnClick);

        promptPanel.SetActive(false);
    }

    // Shows a message without a button, e.g. the end text.
    public void ShowMessage(string message)
    {
        promptText.text = message;
        promptButton.gameObject.SetActive(false);
        promptPanel.SetActive(true);
    }
}
