using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Placeholder UI: one prompt panel with a message and one or two buttons,
// plus a single button without text for in-world actions like "Ansprechen".
public class UIManager : MonoBehaviour
{
    [SerializeField] GameObject promptPanel;
    [SerializeField] TMP_Text promptText;
    [SerializeField] Button promptButton;
    [SerializeField] TMP_Text promptButtonLabel;
    [SerializeField] Button secondButton;
    [SerializeField] TMP_Text secondButtonLabel;
    [SerializeField] Button soloButton;
    [SerializeField] TMP_Text soloButtonLabel;

    [Tooltip("Buttons stay disabled this long after a prompt appears, so a double tap cannot skip the next prompt.")]
    [SerializeField] float inputDelaySeconds = 0.4f;

    void Awake()
    {
        promptPanel.SetActive(false);
        soloButton.gameObject.SetActive(false);
    }

    // Shows the message and completes when the button is pressed.
    public async Awaitable ShowPrompt(string message, string buttonLabel)
    {
        await Ask(message, buttonLabel, null);
    }

    // Shows the message with two buttons. Returns true for the first, false for the second.
    public async Awaitable<bool> ShowChoice(string message, string firstLabel, string secondLabel)
    {
        return await Ask(message, firstLabel, secondLabel) == 0;
    }

    // Shows only a button and completes when it is pressed.
    public async Awaitable ShowButton(string label)
    {
        soloButtonLabel.text = label;
        soloButton.interactable = false;
        soloButton.gameObject.SetActive(true);
        await Awaitable.WaitForSecondsAsync(inputDelaySeconds);
        soloButton.interactable = true;

        var clicked = new AwaitableCompletionSource();
        void OnClick() => clicked.TrySetResult();
        soloButton.onClick.AddListener(OnClick);
        await clicked.Awaitable;
        soloButton.onClick.RemoveListener(OnClick);

        soloButton.interactable = false;
        soloButton.gameObject.SetActive(false);
    }

    // Shows a message without a button, e.g. the end text.
    public void ShowMessage(string message)
    {
        promptText.text = message;
        promptButton.gameObject.SetActive(false);
        secondButton.gameObject.SetActive(false);
        promptPanel.SetActive(true);
    }

    // Returns the index of the pressed button. secondLabel null shows only the first button.
    async Awaitable<int> Ask(string message, string firstLabel, string secondLabel)
    {
        promptText.text = message;
        promptButtonLabel.text = firstLabel;
        promptButton.gameObject.SetActive(true);
        secondButtonLabel.text = secondLabel ?? "";
        secondButton.gameObject.SetActive(secondLabel != null);
        SetInteractable(false);
        promptPanel.SetActive(true);

        await Awaitable.WaitForSecondsAsync(inputDelaySeconds);
        SetInteractable(true);

        var clicked = new AwaitableCompletionSource<int>();
        void OnFirst() => clicked.TrySetResult(0);
        void OnSecond() => clicked.TrySetResult(1);
        promptButton.onClick.AddListener(OnFirst);
        secondButton.onClick.AddListener(OnSecond);
        int index = await clicked.Awaitable;
        promptButton.onClick.RemoveListener(OnFirst);
        secondButton.onClick.RemoveListener(OnSecond);

        SetInteractable(false);
        promptPanel.SetActive(false);
        return index;
    }

    void SetInteractable(bool value)
    {
        promptButton.interactable = value;
        secondButton.interactable = value;
    }
}
