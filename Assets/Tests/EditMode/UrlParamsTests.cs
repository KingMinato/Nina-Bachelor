using System;
using NUnit.Framework;

public class UrlParamsTests
{
    const string Base = "https://example.github.io/game/";

    static UrlParams Parse(string query, int seed = 1) =>
        UrlParams.Parse(Base + query, new Random(seed));

    [Test]
    public void FullParams_AreUsedDirectly()
    {
        var p = Parse("?pid=R_abc123&g=w&s=3");
        Assert.AreEqual("R_abc123", p.Pid);
        Assert.AreEqual("w", p.RawGender);
        Assert.AreEqual("w", p.AvatarGender);
        Assert.AreEqual(3, p.Species);
        Assert.IsFalse(p.IsTest);
        Assert.IsFalse(p.Debug);
    }

    [TestCase("m", "m")]
    [TestCase("w", "w")]
    [TestCase("M", "m")]
    public void ValidGender_IsUsed(string g, string expected)
    {
        Assert.AreEqual(expected, Parse($"?pid=x&g={g}").AvatarGender);
    }

    [TestCase("?pid=x&g=x", "x")]
    [TestCase("?pid=x&g=", "")]
    [TestCase("?pid=x", "")]
    public void OtherGender_IsRandomButRawIsKept(string query, string expectedRaw)
    {
        var p = Parse(query);
        Assert.AreEqual(expectedRaw, p.RawGender);
        CollectionAssert.Contains(new[] { "m", "w" }, p.AvatarGender);
    }

    [Test]
    public void RandomGender_ProducesBothValues()
    {
        bool sawM = false, sawW = false;
        for (int seed = 0; seed < 50; seed++)
        {
            string ag = Parse("?pid=x&g=x", seed).AvatarGender;
            sawM |= ag == "m";
            sawW |= ag == "w";
        }
        Assert.IsTrue(sawM && sawW);
    }

    [TestCase("1", 1)]
    [TestCase("4", 4)]
    public void ValidSpecies_IsUsed(string s, int expected)
    {
        Assert.AreEqual(expected, Parse($"?pid=x&s={s}").Species);
    }

    [TestCase("?pid=x&s=0")]
    [TestCase("?pid=x&s=5")]
    [TestCase("?pid=x&s=abc")]
    [TestCase("?pid=x")]
    public void InvalidSpecies_IsRandomInRange(string query)
    {
        for (int seed = 0; seed < 20; seed++)
            Assert.That(Parse(query, seed).Species, Is.InRange(1, UrlParams.SpeciesCount));
    }

    [TestCase("")]
    [TestCase("?g=m&s=1")]
    [TestCase("?pid=&g=m")]
    public void MissingPid_GeneratesTestPid(string query)
    {
        var p = Parse(query);
        StringAssert.StartsWith("test-", p.Pid);
        Assert.IsTrue(p.IsTest);
    }

    [Test]
    public void DebugFlag_MarksAsTest()
    {
        var p = Parse("?pid=R_1&debug=1");
        Assert.IsTrue(p.Debug);
        Assert.IsTrue(p.IsTest);
        Assert.AreEqual("R_1", p.Pid);
    }

    [Test]
    public void EncodedValues_AreDecoded_AndFragmentIgnored()
    {
        var p = Parse("?pid=R%5Fa%20b&g=w#top");
        Assert.AreEqual("R_a b", p.Pid);
        Assert.AreEqual("w", p.AvatarGender);
    }

    [Test]
    public void EmptyUrl_IsHandled()
    {
        var p = UrlParams.Parse("", new Random(1));
        StringAssert.StartsWith("test-", p.Pid);
        Assert.That(p.Species, Is.InRange(1, UrlParams.SpeciesCount));
    }

    [Test]
    public void AvatarOverride_OnlyInTestRuns()
    {
        Assert.AreEqual("slime", Parse("?a=slime").AvatarOverride);
        Assert.AreEqual("cow", Parse("?pid=R_1&debug=1&a=cow").AvatarOverride);
        Assert.AreEqual("", Parse("?pid=R_1&a=cow").AvatarOverride);
        Assert.AreEqual("", Parse("?debug=1").AvatarOverride);
    }

    [Test]
    public void RoomOverride_OnlyInTestRuns()
    {
        Assert.AreEqual("Stein", Parse("?room=Stein").RoomOverride);
        Assert.AreEqual("Buero", Parse("?pid=R_1&debug=1&room=Buero").RoomOverride);
        Assert.AreEqual("", Parse("?pid=R_1&room=Buero").RoomOverride);
        Assert.AreEqual("", Parse("?debug=1").RoomOverride);
    }
}
