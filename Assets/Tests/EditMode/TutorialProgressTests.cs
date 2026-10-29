using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class TutorialProgressTests
{
    static TutorialStep Step(string action, int count) => new TutorialStep("x", action, count);

    [Test]
    public void StepAdvancesAfterRequiredCount()
    {
        var p = new TutorialProgress(90f, new[] { Step(TutorialStep.TurnLeft, 2), Step(TutorialStep.Jump, 1) });
        Assert.IsFalse(p.Register(TutorialStep.TurnLeft));
        Assert.AreEqual(1, p.Done);
        Assert.IsTrue(p.Register(TutorialStep.TurnLeft));
        Assert.AreEqual(TutorialStep.Jump, p.Current.action);
        Assert.AreEqual(0, p.Done);
        Assert.IsTrue(p.Register(TutorialStep.Jump));
        Assert.IsTrue(p.IsFinished);
    }

    [Test]
    public void WrongActionDoesNotCount()
    {
        var p = new TutorialProgress(90f, new[] { Step(TutorialStep.TurnLeft, 2) });
        p.Register(TutorialStep.TurnLeft);
        p.Register(TutorialStep.TurnRight);
        p.Register(TutorialStep.Jump);
        Assert.AreEqual(1, p.Done);
        Assert.IsFalse(p.IsFinished);
    }

    [Test]
    public void CountBelowOneIsTreatedAsOne()
    {
        var p = new TutorialProgress(90f, new[] { Step(TutorialStep.Jump, 0) });
        Assert.AreEqual(1, p.Required);
        Assert.IsTrue(p.Register(TutorialStep.Jump));
        Assert.IsTrue(p.IsFinished);
    }

    [Test]
    public void UnknownActionIsSkipped()
    {
        LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("unknown action"));
        var p = new TutorialProgress(90f, new[] { Step("dance", 1), Step(TutorialStep.Jump, 1) });
        Assert.AreEqual(TutorialStep.Jump, p.Current.action);
    }

    [Test]
    public void TurnAround_FullTurnLeftOrRightCounts()
    {
        var left = new TutorialProgress(90f, new[] { Step(TutorialStep.TurnAround, 1) });
        for (int i = 0; i < 3; i++)
            Assert.IsFalse(left.Register(TutorialStep.TurnLeft));
        Assert.AreEqual(-270f, left.TurnedDegrees);
        Assert.IsTrue(left.Register(TutorialStep.TurnLeft));
        Assert.IsTrue(left.IsFinished);

        var right = new TutorialProgress(90f, new[] { Step(TutorialStep.TurnAround, 1) });
        for (int i = 0; i < 3; i++)
            right.Register(TutorialStep.TurnRight);
        Assert.IsTrue(right.Register(TutorialStep.TurnRight));
    }

    [Test]
    public void TurnAround_UsesNetRotationAndIgnoresJump()
    {
        var p = new TutorialProgress(90f, new[] { Step(TutorialStep.TurnAround, 1) });
        p.Register(TutorialStep.TurnLeft);
        p.Register(TutorialStep.TurnRight);
        p.Register(TutorialStep.Jump);
        Assert.AreEqual(0f, p.TurnedDegrees);
        for (int i = 0; i < 3; i++)
            p.Register(TutorialStep.TurnLeft);
        Assert.IsFalse(p.IsFinished);
        Assert.IsTrue(p.Register(TutorialStep.TurnLeft));
    }

    [Test]
    public void TurnAround_DependsOnTurnAngle()
    {
        var p = new TutorialProgress(45f, new[] { Step(TutorialStep.TurnAround, 1) });
        for (int i = 0; i < 7; i++)
            Assert.IsFalse(p.Register(TutorialStep.TurnRight));
        Assert.IsTrue(p.Register(TutorialStep.TurnRight));
    }

    [Test]
    public void EmptyOrMissingStepsAreFinished()
    {
        Assert.IsTrue(new TutorialProgress(90f, new TutorialStep[0]).IsFinished);
        Assert.IsTrue(new TutorialProgress(90f, null).IsFinished);
    }
}
