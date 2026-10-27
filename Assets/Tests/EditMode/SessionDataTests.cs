using System;
using System.Linq;
using NUnit.Framework;

public class SessionDataTests
{
    static SessionData Create(string query = "?pid=R_1&g=x&s=2", bool mobile = false, double start = 10)
    {
        var p = UrlParams.Parse("https://host/" + query, new Random(1));
        return new SessionData(p, "0.1.0", mobile, start);
    }

    [Test]
    public void Fields_AreInSheetColumnOrder()
    {
        string[] expected = { "pid", "g", "ag", "s", "r1", "r2", "r3", "r4", "t1", "t2", "t3", "t4", "bal", "dev", "dur", "test", "ver" };
        CollectionAssert.AreEqual(expected, Create().ToFields().Select(f => f.Key).ToArray());
    }

    [Test]
    public void Rounds_AreRecordedAndBalanceSums()
    {
        var s = Create();
        s.RecordRound(1, 60, 1200, 60);
        s.RecordRound(2, 1, 800, 50);
        s.RecordRound(3, 95, 1500, 0);
        s.RecordRound(4, 0, 900, 0);

        var f = s.ToFields().ToDictionary(x => x.Key, x => x.Value);
        Assert.AreEqual("60", f["r1"]);
        Assert.AreEqual("1", f["r2"]);
        Assert.AreEqual("95", f["r3"]);
        Assert.AreEqual("0", f["r4"]);
        Assert.AreEqual("1200", f["t1"]);
        Assert.AreEqual("900", f["t4"]);
        Assert.AreEqual("110", f["bal"]);
    }

    [Test]
    public void Params_AreCopied()
    {
        var f = Create("?pid=R_9&g=x&s=3", mobile: true).ToFields().ToDictionary(x => x.Key, x => x.Value);
        Assert.AreEqual("R_9", f["pid"]);
        Assert.AreEqual("x", f["g"]);
        CollectionAssert.Contains(new[] { "m", "w" }, f["ag"]);
        Assert.AreEqual("3", f["s"]);
        Assert.AreEqual("mobile", f["dev"]);
        Assert.AreEqual("0", f["test"]);
        Assert.AreEqual("0.1.0", f["ver"]);
    }

    [Test]
    public void TestRun_IsFlagged()
    {
        var f = Create("?g=m").ToFields().ToDictionary(x => x.Key, x => x.Value);
        Assert.AreEqual("1", f["test"]);
        Assert.AreEqual("pc", f["dev"]);
    }

    [Test]
    public void Duration_IsRoundedSecondsSinceStart()
    {
        var s = Create(start: 10);
        s.Finish(10 + 287.6);
        Assert.AreEqual(288, s.DurationSeconds);
    }

    [Test]
    public void TextAndNumberFields_AreMarked()
    {
        var fields = Create().ToFields().ToDictionary(x => x.Key, x => x.IsNumber);
        Assert.IsFalse(fields["pid"]);
        Assert.IsFalse(fields["g"]);
        Assert.IsFalse(fields["ver"]);
        Assert.IsTrue(fields["r1"]);
        Assert.IsTrue(fields["bal"]);
        Assert.IsTrue(fields["test"]);
    }
}
