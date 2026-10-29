using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class TutorialProgressTests
{
    static TutorialStep Step(string action, int count) => new TutorialStep("x", action, count);

    [Test]
    public void StepAdvancesAfterRequiredCount()
    {
        var p = new TutorialProgress(new[] { Step(TutorialStep.TurnLeft, 2), Step(TutorialStep.Jump, 1) });
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
        var p = new TutorialProgress(new[] { Step(TutorialStep.TurnLeft, 2) });
        p.Register(TutorialStep.TurnLeft);
        p.Register(TutorialStep.TurnRight);
        p.Register(TutorialStep.Jump);
        Assert.AreEqual(1, p.Done);
        Assert.IsFalse(p.IsFinished);
    }

    [Test]
    public void CountBelowOneIsTreatedAsOne()
    {
        var p = new TutorialProgress(new[] { Step(TutorialStep.Jump, 0) });
        Assert.AreEqual(1, p.Required);
        Assert.IsTrue(p.Register(TutorialStep.Jump));
        Assert.IsTrue(p.IsFinished);
    }

    [Test]
    public void UnknownActionIsSkipped()
    {
        LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("unknown action"));
        var p = new TutorialProgress(new[] { Step("dance", 1), Step(TutorialStep.Jump, 1) });
        Assert.AreEqual(TutorialStep.Jump, p.Current.action);
    }

    [Test]
    public void EmptyOrMissingStepsAreFinished()
    {
        Assert.IsTrue(new TutorialProgress(new TutorialStep[0]).IsFinished);
        Assert.IsTrue(new TutorialProgress(null).IsFinished);
    }
}
