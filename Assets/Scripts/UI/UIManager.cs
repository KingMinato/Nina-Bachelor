using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Dialog UI: one prompt panel with a message and one or two buttons, a single button for "Ansprechen",
// and an instruction line at the top. Looks and transitions come from the UI style (UiSkin).
// Messages are typed out; the buttons appear once the text is complete.
public class UIManager : MonoBehaviour
{
    [SerializeField] GameObject promptPanel;
    [SerializeField] TMP_Text promptText;
    [SerializeField] UiTypewriter typewriter;
    [SerializeField] GameObject speakerTag;
    [SerializeField] TMP_Text speakerLabel;
    [SerializeField] GameObject loader;
    [SerializeField] Button promptButton;
    [SerializeField] TMP_Text promptButtonLabel;
    [SerializeField] Button secondButton;
    [SerializeField] TMP_Text secondButtonLabel;
    [SerializeField] Button soloButton;
    [SerializeField] TMP_Text soloButtonLabel;
    [SerializeField] GameObject instructionPanel;
    [SerializeField] TMP_Text instructionText;
    [SerializeField] TMP_Text instructionCount;

    [Tooltip("Buttons stay disabled this long after they appear, so a double tap cannot skip the next prompt.")]
    [SerializeField] float inputDelaySeconds = 0.4f;

    // When the buttons of the last prompt appeared (realtime), the start of the decision time.
    public double OptionsShownAt { get; private set; }

    void Awake()
    {
        promptPanel.SetActive(false);
        soloButton.gameObject.SetActive(false);
        instructionPanel.SetActive(false);
    }

    // Instruction at the top of the screen, stays until hidden. count is e.g. "1 / 2" or empty.
    public void ShowInstruction(string text, string count = "")
    {
        bool wasShown = instructionPanel.activeSelf;
        bool textChanged = instructionText.text != text;
        bool countChanged = instructionCount.text != count;
        instructionText.text = text;
        instructionCount.text = count;
        instructionCount.gameObject.SetActive(count != "");
        // Show also cancels a hide that is still pending (instruction replaced right after the tutorial).
        UiSkin.Show(instructionPanel);
        if (!wasShown)
            return;
        if (textChanged)
            Pulse(instructionPanel);
        else if (countChanged)
            Pulse(instructionCount.gameObject);
    }

    public void HideInstruction()
    {
        UiSkin.Hide(instructionPanel);
    }

    // Shows the message and completes when the button is pressed. speaker puts a name tag on the panel.
    public async Awaitable ShowPrompt(string message, string buttonLabel, string speaker = null)
    {
        await Ask(message, buttonLabel, null, speaker);
    }

    // Shows the message with two buttons. Returns true for the first, false for the second.
    public async Awaitable<bool> ShowChoice(string message, string firstLabel, string secondLabel, string speaker = null)
    {
        return await Ask(message, firstLabel, secondLabel, speaker) == 0;
    }

    // Shows only a button and completes when it is pressed.
    public async Awaitable ShowButton(string label)
    {
        soloButtonLabel.text = label;
        soloButton.interactable = false;
        UiSkin.Show(soloButton.gameObject);
        await Awaitable.WaitForSecondsAsync(inputDelaySeconds);
        soloButton.interactable = true;

        var clicked = new AwaitableCompletionSource();
        void OnClick() => clicked.TrySetResult();
        soloButton.onClick.AddListener(OnClick);
        await clicked.Awaitable;
        soloButton.onClick.RemoveListener(OnClick);

        soloButton.interactable = false;
        UiSkin.Hide(soloButton.gameObject);
    }

    // Shows a message without a button, e.g. the end text, with a small loading animation.
    public void ShowMessage(string message)
    {
        promptButton.gameObject.SetActive(false);
        secondButton.gameObject.SetActive(false);
        speakerTag.SetActive(false);
        UiSkin.Show(promptPanel);
        loader.SetActive(true);
        _ = typewriter.Play(promptText, message);
    }

    // Returns the index of the pressed button. secondLabel null shows only the first button.
    async Awaitable<int> Ask(string message, string firstLabel, string secondLabel, string speaker)
    {
        promptButton.gameObject.SetActive(false);
        secondButton.gameObject.SetActive(false);
        loader.SetActive(false);
        speakerLabel.text = speaker ?? "";
        speakerTag.SetActive(!string.IsNullOrEmpty(speaker));
        promptButtonLabel.text = firstLabel;
        secondButtonLabel.text = secondLabel ?? "";
        UiSkin.Show(promptPanel);
        await typewriter.Play(promptText, message);

        SetInteractable(false);
        promptButton.gameObject.SetActive(true);
        secondButton.gameObject.SetActive(secondLabel != null);
        OptionsShownAt = Time.realtimeSinceStartupAsDouble;
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
        UiSkin.Hide(promptPanel);
        return index;
    }

    void SetInteractable(bool value)
    {
        promptButton.interactable = value;
        secondButton.interactable = value;
    }

    static void Pulse(GameObject go)
    {
        if (go.TryGetComponent(out UiSkin skin))
            skin.Pulse();
    }
}
