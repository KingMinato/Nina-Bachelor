using System;
using System.Collections.Generic;

// Incoming parameters from Qualtrics Survey A, plus the values derived from them.
public class UrlParams
{
    public const int SpeciesCount = 4;

    public string Pid;
    public string RawGender;     // exactly as received, "" if missing
    public string AvatarGender;  // "m" or "w"
    public int Species;          // 1..SpeciesCount
    public bool Debug;
    public bool IsTest;          // no pid provided or debug mode
    public string AvatarOverride; // avatar species id from "a", test runs only, "" otherwise
    public string RoomOverride;   // room variant name from "room", test runs only, "" otherwise
    public string UiOverride;     // UI style name from "ui", test runs only, "" otherwise

    public static UrlParams Parse(string url, Random rng)
    {
        var query = ParseQuery(url);
        query.TryGetValue("pid", out string pid);
        query.TryGetValue("g", out string g);
        query.TryGetValue("s", out string s);
        query.TryGetValue("debug", out string debug);
        query.TryGetValue("a", out string avatar);
        query.TryGetValue("room", out string room);
        query.TryGetValue("ui", out string ui);

        var p = new UrlParams
        {
            RawGender = g ?? "",
            AvatarGender = ResolveGender(g, rng),
            Species = ResolveSpecies(s, rng),
            Debug = debug == "1",
        };

        bool hasPid = !string.IsNullOrWhiteSpace(pid);
        p.Pid = hasPid ? pid.Trim() : $"test-{DateTime.UtcNow:yyyyMMddHHmmss}";
        p.IsTest = !hasPid || p.Debug;
        // Lets testers look at any avatar, also those not mapped to a species. Never in real runs.
        p.AvatarOverride = p.IsTest ? avatar?.Trim() ?? "" : "";
        p.RoomOverride = p.IsTest ? room?.Trim() ?? "" : "";
        p.UiOverride = p.IsTest ? ui?.Trim() ?? "" : "";
        return p;
    }

    static string ResolveGender(string g, Random rng)
    {
        string normalized = g?.Trim().ToLowerInvariant();
        if (normalized == "m" || normalized == "w")
            return normalized;
        return rng.Next(2) == 0 ? "m" : "w";
    }

    static int ResolveSpecies(string s, Random rng)
    {
        if (int.TryParse(s, out int species) && species >= 1 && species <= SpeciesCount)
            return species;
        return rng.Next(1, SpeciesCount + 1);
    }

    static Dictionary<string, string> ParseQuery(string url)
    {
        var result = new Dictionary<string, string>();
        if (string.IsNullOrEmpty(url))
            return result;

        int start = url.IndexOf('?');
        if (start < 0)
            return result;

        string query = url.Substring(start + 1);
        int hash = query.IndexOf('#');
        if (hash >= 0)
            query = query.Substring(0, hash);

        foreach (string pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int eq = pair.IndexOf('=');
            string key = Decode(eq < 0 ? pair : pair.Substring(0, eq));
            string value = eq < 0 ? "" : Decode(pair.Substring(eq + 1));
            result[key] = value; // last occurrence wins
        }
        return result;
    }

    static string Decode(string s) => Uri.UnescapeDataString(s.Replace('+', ' '));
}
