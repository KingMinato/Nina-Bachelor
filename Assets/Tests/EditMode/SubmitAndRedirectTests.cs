using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

public class SubmitAndRedirectTests
{
    static List<SessionData.Field> Fields(string query = "?pid=R_1&g=m&s=2")
    {
        var s = new SessionData(UrlParams.Parse("https://host/" + query, new System.Random(1)), "0.1.0", false, 0);
        s.RecordRound(1, 60, 1200, 60);
        s.RecordRound(2, 1, 800, 50);
        return s.ToFields();
    }

    [Test]
    public void Json_HasNumbersUnquotedAndTextQuoted()
    {
        string json = ResultJson.Build(Fields());
        StringAssert.StartsWith("{\"pid\":\"R_1\",\"g\":\"m\",\"ag\":\"m\",\"s\":2,\"r1\":60,\"r2\":1,", json);
        StringAssert.Contains("\"bal\":110,\"dev\":\"pc\"", json);
        StringAssert.EndsWith("\"test\":0,\"ver\":\"0.1.0\"}", json);
    }

    [Test]
    public void Json_EscapesSpecialCharacters()
    {
        var fields = new List<SessionData.Field> { new SessionData.Field("pid", "a\"b\\c\nd\u0001", false) };
        Assert.AreEqual("{\"pid\":\"a\\\"b\\\\c\\nd\\u0001\"}", ResultJson.Build(fields));
    }

    [Test]
    public void Url_FillsAndEncodesValues()
    {
        var fields = new List<SessionData.Field>
        {
            new SessionData.Field("pid", "R_1 &x=ü", false),
            new SessionData.Field("bal", "110", true),
        };
        Assert.AreEqual("https://q/?pid=R_1%20%26x%3D%C3%BC&bal=110&other={other}",
            Redirector.BuildUrl("https://q/?pid={pid}&bal={bal}&other={other}", fields));
    }

    [Test]
    public void Url_EmptyTemplate_GivesEmptyUrl()
    {
        Assert.AreEqual("", Redirector.BuildUrl(null, Fields()));
    }

    [Test]
    public void ConfigTemplate_HasNoUnfilledPlaceholders()
    {
        var c = Config.FromJson(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "config.json")));
        string url = Redirector.BuildUrl(c.returnUrlTemplate, Fields());
        StringAssert.DoesNotContain("{", url);
        foreach (var f in Fields())
            StringAssert.Contains(f.Key + "=", url);
    }
}
