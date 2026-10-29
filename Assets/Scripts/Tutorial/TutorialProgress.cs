using System.Collections.Generic;
using UnityEngine;

// Counts performed actions against the configured tutorial steps.
// Steps with an unknown action are skipped, so a config typo cannot make the tutorial unfinishable.
public class TutorialProgress
{
    readonly List<TutorialStep> steps = new List<TutorialStep>();

    public int StepIndex { get; private set; }
    public int Done { get; private set; }

    public TutorialProgress(TutorialStep[] configSteps)
    {
        foreach (var step in configSteps ?? new TutorialStep[0])
        {
            if (step != null && IsKnown(step.action))
                steps.Add(step);
            else
                Debug.LogWarning($"Skipping tutorial step with unknown action '{step?.action}'");
        }
    }

    public bool IsFinished => StepIndex >= steps.Count;
    public TutorialStep Current => IsFinished ? null : steps[StepIndex];
    public int Required => IsFinished ? 0 : Mathf.Max(1, Current.count);

    // Returns true if the action advanced to the next step.
    public bool Register(string action)
    {
        if (IsFinished || action != Current.action)
            return false;

        Done++;
        if (Done < Required)
            return false;

        StepIndex++;
        Done = 0;
        return true;
    }

    static bool IsKnown(string action)
    {
        return action == TutorialStep.TurnLeft || action == TutorialStep.TurnRight || action == TutorialStep.Jump;
    }
}
