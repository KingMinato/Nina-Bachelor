using TMPro;
using UnityEngine;

// Shows the intro, then each tutorial step as an instruction until the player has performed it.
public class TutorialController : MonoBehaviour
{
    [SerializeField] UIManager ui;
    [SerializeField] ActionButtonsUI actionButtons;
    [SerializeField] PlayerActions playerActions;
    [SerializeField] GameObject instructionPanel;
    [SerializeField] TMP_Text instructionText;
    [SerializeField] TMP_Text progressText;

    TutorialProgress progress;
    AwaitableCompletionSource finished;

    void Awake()
    {
        instructionPanel.SetActive(false);
    }

    public async Awaitable Run(Config config)
    {
        await ui.ShowPrompt(config.texts.intro, config.texts.next);

        progress = new TutorialProgress(config.tutorialSteps);
        if (progress.IsFinished)
            return;

        finished = new AwaitableCompletionSource();
        playerActions.Performed += OnPerformed;
        actionButtons.Show(config.texts);
        instructionPanel.SetActive(true);
        UpdateInstruction();

        await finished.Awaitable;

        playerActions.Performed -= OnPerformed;
        actionButtons.Hide();
        instructionPanel.SetActive(false);
    }

    void OnPerformed(string action)
    {
        progress.Register(action);
        if (progress.IsFinished)
            finished.TrySetResult();
        else
            UpdateInstruction();
    }

    void UpdateInstruction()
    {
        instructionText.text = progress.Current.text;
        progressText.text = progress.Required > 1 ? $"{progress.Done} / {progress.Required}" : "";
    }
}
