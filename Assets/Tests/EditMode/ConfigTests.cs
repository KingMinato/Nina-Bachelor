using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class ConfigTests
{
    [Test]
    public void EmptyJson_ReturnsDefaults()
    {
        var c = Config.FromJson("");
        Assert.AreEqual(100, c.moneyPerRound);
        Assert.AreEqual(4, c.tutorialSteps.Length);
        Assert.AreEqual("Zur Tür", c.texts.goToDoor);
    }

    [Test]
    public void InvalidJson_ReturnsDefaults()
    {
        LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Invalid config"));
        var c = Config.FromJson("{ not json");
        Assert.AreEqual(90, c.npcAcceptMaxPlayerKeeps);
    }

    [Test]
    public void PartialJson_KeepsDefaultsForMissingFields()
    {
        var c = Config.FromJson("{ \"turnAngle\": 30, \"texts\": { \"accept\": \"Ja\" } }");
        Assert.AreEqual(30f, c.turnAngle);
        Assert.AreEqual(5, c.splitStep);
        Assert.AreEqual("Ja", c.texts.accept);
        Assert.AreEqual("Ablehnen", c.texts.reject);
        Assert.AreEqual(4, c.tutorialSteps.Length);
    }

    [Test]
    public void StreamingAssetsConfig_Parses()
    {
        string json = File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "config.json"));
        var c = Config.FromJson(json);
        Assert.IsNotEmpty(c.version);
        Assert.IsNotEmpty(c.tutorialSteps);
        foreach (var step in c.tutorialSteps)
        {
            CollectionAssert.Contains(new[] { TutorialStep.TurnLeft, TutorialStep.TurnRight, TutorialStep.Jump }, step.action);
            Assert.Greater(step.count, 0);
        }
        StringAssert.Contains("{pid}", c.returnUrlTemplate);
    }
}
