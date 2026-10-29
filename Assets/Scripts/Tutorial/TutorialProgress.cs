using System.Collections.Generic;
using UnityEngine;

// Counts performed actions against the configured tutorial steps.
// turnAround steps count full 360 degree turns in either direction, independent of the turn angle.
// Steps with an unknown action are skipped, so a config typo cannot make the tutorial unfinishable.
public class TutorialProgress
{
    readonly List<TutorialStep> steps = new List<TutorialStep>();
    readonly float turnAngle;

    public int StepIndex { get; private set; }
    public int Done { get; private set; }

    // Net rotation in the current turnAround step, negative = left.
    public float TurnedDegrees { get; private set; }

    public TutorialProgress(float turnAngle, TutorialStep[] configSteps)
    {
        this.turnAngle = Mathf.Abs(turnAngle);
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
        if (IsFinished || !Counts(action))
            return false;

        Done++;
        if (Done < Required)
            return false;

        StepIndex++;
        Done = 0;
        TurnedDegrees = 0f;
        return true;
    }

    bool Counts(string action)
    {
        if (Current.action != TutorialStep.TurnAround)
            return action == Current.action;

        if (action == TutorialStep.TurnLeft)
            TurnedDegrees -= turnAngle;
        else if (action == TutorialStep.TurnRight)
            TurnedDegrees += turnAngle;

        // Small tolerance for angles like 120 that add up with float error.
        if (Mathf.Abs(TurnedDegrees) < 359.9f)
            return false;
        TurnedDegrees = 0f;
        return true;
    }

    static bool IsKnown(string action)
    {
        return action == TutorialStep.TurnLeft || action == TutorialStep.TurnRight
            || action == TutorialStep.TurnAround || action == TutorialStep.Jump;
    }
}
