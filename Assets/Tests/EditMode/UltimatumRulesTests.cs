using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

public class UltimatumRulesTests
{
    static readonly Config C = new Config();

    [TestCase(1, true)]
    [TestCase(2, false)]
    [TestCase(3, true)]
    [TestCase(4, false)]
    public void PlayerSplits_InOddRounds(int round, bool expected)
    {
        Assert.AreEqual(expected, UltimatumRules.PlayerSplits(round));
    }

    [Test]
    public void NpcKeeps_ComesFromConfig()
    {
        Assert.AreEqual(50, UltimatumRules.NpcKeeps(2, C));
        Assert.AreEqual(75, UltimatumRules.NpcKeeps(4, C));
        var custom = Config.FromJson("{ \"round2NpcKeeps\": 40, \"round4NpcKeeps\": 80 }");
        Assert.AreEqual(40, UltimatumRules.NpcKeeps(2, custom));
        Assert.AreEqual(80, UltimatumRules.NpcKeeps(4, custom));
    }

    [TestCase(0, true)]
    [TestCase(50, true)]
    [TestCase(90, true)]
    [TestCase(95, false)]
    [TestCase(100, false)]
    public void PlayerSplit_NpcAcceptsUpToThreshold(int keeps, bool accepted)
    {
        var r = UltimatumRules.PlayerSplit(keeps, C);
        Assert.AreEqual(accepted, r.Accepted);
        Assert.AreEqual(keeps, r.RecordedValue);
        Assert.AreEqual(accepted ? keeps : 0, r.PlayerGets);
        Assert.AreEqual(accepted ? 100 - keeps : 0, r.NpcGets);
    }

    [Test]
    public void PlayerSplit_ThresholdComesFromConfig()
    {
        var c = Config.FromJson("{ \"npcAcceptMaxPlayerKeeps\": 70 }");
        Assert.IsTrue(UltimatumRules.PlayerSplit(70, c).Accepted);
        Assert.IsFalse(UltimatumRules.PlayerSplit(75, c).Accepted);
    }

    [TestCase(50, true, 50, 50, 1)]
    [TestCase(50, false, 0, 0, 0)]
    [TestCase(75, true, 25, 75, 1)]
    [TestCase(75, false, 0, 0, 0)]
    public void NpcOffer_PaysOnlyWhenAccepted(int npcKeeps, bool accepted, int playerGets, int npcGets, int recorded)
    {
        var r = UltimatumRules.NpcOffer(npcKeeps, accepted, C);
        Assert.AreEqual(accepted, r.Accepted);
        Assert.AreEqual(playerGets, r.PlayerGets);
        Assert.AreEqual(npcGets, r.NpcGets);
        Assert.AreEqual(recorded, r.RecordedValue);
    }

    [TestCase(0, 0)]
    [TestCase(42, 40)]
    [TestCase(43, 45)]
    [TestCase(-10, 0)]
    [TestCase(130, 100)]
    public void SnapToStep_RoundsAndClamps(int value, int expected)
    {
        Assert.AreEqual(expected, UltimatumRules.SnapToStep(value, C));
    }

    // Every accept/reject combination of the four rounds: round 1 keeps 60 or 95, round 3 keeps 90 or 100.
    static IEnumerable<TestCaseData> AllCombinations()
    {
        for (int mask = 0; mask < 16; mask++)
        {
            bool a1 = (mask & 1) != 0, a2 = (mask & 2) != 0, a3 = (mask & 4) != 0, a4 = (mask & 8) != 0;
            int expected = (a1 ? 60 : 0) + (a2 ? 50 : 0) + (a3 ? 90 : 0) + (a4 ? 25 : 0);
            yield return new TestCaseData(a1 ? 60 : 95, a2, a3 ? 90 : 100, a4, expected);
        }
    }

    [TestCaseSource(nameof(AllCombinations))]
    public void Balance_IsCorrectInAllCombinations(int r1Keeps, bool r2Accept, int r3Keeps, bool r4Accept, int expectedBalance)
    {
        var session = new SessionData(UrlParams.Parse("https://host/?pid=R_1", new System.Random(1)), "0.1.0", false, 0);
        var results = new[]
        {
            UltimatumRules.PlayerSplit(r1Keeps, C),
            UltimatumRules.NpcOffer(UltimatumRules.NpcKeeps(2, C), r2Accept, C),
            UltimatumRules.PlayerSplit(r3Keeps, C),
            UltimatumRules.NpcOffer(UltimatumRules.NpcKeeps(4, C), r4Accept, C),
        };
        for (int i = 0; i < results.Length; i++)
            session.RecordRound(i + 1, results[i].RecordedValue, 1000, results[i].PlayerGets);

        var f = session.ToFields().ToDictionary(x => x.Key, x => x.Value);
        Assert.AreEqual(expectedBalance, session.Balance);
        Assert.AreEqual(expectedBalance.ToString(), f["bal"]);
        Assert.AreEqual(r1Keeps.ToString(), f["r1"]);
        Assert.AreEqual(r2Accept ? "1" : "0", f["r2"]);
        Assert.AreEqual(r3Keeps.ToString(), f["r3"]);
        Assert.AreEqual(r4Accept ? "1" : "0", f["r4"]);
    }

    [Test]
    public void Fill_ReplacesAmounts()
    {
        Assert.AreEqual("Du: 60 CHF / Gegenüber: 40 CHF", Texts.Fill(C.texts.splitShares, 60, 40));
        Assert.AreEqual("", Texts.Fill(null, 1, 2));
    }
}
