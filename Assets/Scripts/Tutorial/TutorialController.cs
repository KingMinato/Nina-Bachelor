using UnityEngine;

// Shows the intro, then each tutorial step as an instruction until the player has performed it.
public class TutorialController : MonoBehaviour
{
    [SerializeField] UIManager ui;
    [SerializeField] ActionButtonsUI actionButtons;
    [SerializeField] PlayerActions playerActions;
    [SerializeField] StepDoneUI stepDone;

    TutorialProgress progress;
    Texts texts;
    AwaitableCompletionSource finished;

    public async Awaitable Run(Config config)
    {
        texts = config.texts;
        await ui.ShowPrompt(config.texts.intro, config.texts.next);

        progress = new TutorialProgress(config.turnAngle, config.tutorialSteps);
        if (progress.IsFinished)
            return;

        finished = new AwaitableCompletionSource();
        playerActions.Performed += OnPerformed;
        actionButtons.Show(config.texts, withJump: true);
        UpdateInstruction();

        await finished.Awaitable;

        playerActions.Performed -= OnPerformed;
        actionButtons.Hide();
        ui.HideInstruction();
    }

    void OnPerformed(string action)
    {
        int stepIndex = progress.StepIndex;
        progress.Register(action);
        if (progress.StepIndex != stepIndex)
            stepDone.Show(texts.stepDone);
        if (progress.IsFinished)
            finished.TrySetResult();
        else
            UpdateInstruction();
    }

    void UpdateInstruction()
    {
        string count = progress.Required > 1 ? $"{progress.Done} / {progress.Required}" : "";
        if (progress.Current.action == TutorialStep.TurnAround)
            count = $"{Mathf.Abs(progress.TurnedDegrees):0}° / 360°" + (count == "" ? "" : $"  ({count})");
        ui.ShowInstruction(progress.Current.text, count);
        actionButtons.SetWanted(progress.Current.action);
    }
}
