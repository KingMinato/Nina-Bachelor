using System;
using System.Collections.Generic;
using System.Globalization;

// All values recorded during one run. Only ResultSubmitter and Redirector read it at the end.
public class SessionData
{
    public const int RoundCount = 4;

    public readonly struct Field
    {
        public readonly string Key;
        public readonly string Value;
        public readonly bool IsNumber;

        public Field(string key, string value, bool isNumber)
        {
            Key = key;
            Value = value;
            IsNumber = isNumber;
        }
    }

    public readonly string Pid;
    public readonly string RawGender;
    public readonly string AvatarGender;
    public readonly int Species;
    public readonly string Device;
    public readonly bool IsTest;
    public readonly string Version;
    readonly double startTime;

    // Rounds 1/3: amount the player keeps. Rounds 2/4: 1 = accepted, 0 = rejected.
    public readonly int[] RoundValues = new int[RoundCount];
    public readonly int[] DecisionMs = new int[RoundCount];
    public int Balance { get; private set; }
    public int DurationSeconds { get; private set; }

    public SessionData(UrlParams urlParams, string version, bool isMobile, double startTime)
    {
        Pid = urlParams.Pid;
        RawGender = urlParams.RawGender;
        AvatarGender = urlParams.AvatarGender;
        Species = urlParams.Species;
        IsTest = urlParams.IsTest;
        Device = isMobile ? "mobile" : "pc";
        Version = version;
        this.startTime = startTime;
    }

    // round is 1-based, matching r1..r4 in the output.
    public void RecordRound(int round, int value, int decisionMs, int playerGain)
    {
        RoundValues[round - 1] = value;
        DecisionMs[round - 1] = decisionMs;
        Balance += playerGain;
    }

    public void Finish(double now)
    {
        DurationSeconds = (int)Math.Round(now - startTime);
    }

    // Output fields in the order of the Sheet columns and the return URL.
    public List<Field> ToFields()
    {
        var fields = new List<Field>
        {
            Text("pid", Pid),
            Text("g", RawGender),
            Text("ag", AvatarGender),
            Number("s", Species),
        };
        for (int i = 0; i < RoundCount; i++)
            fields.Add(Number($"r{i + 1}", RoundValues[i]));
        for (int i = 0; i < RoundCount; i++)
            fields.Add(Number($"t{i + 1}", DecisionMs[i]));
        fields.Add(Number("bal", Balance));
        fields.Add(Text("dev", Device));
        fields.Add(Number("dur", DurationSeconds));
        fields.Add(Number("test", IsTest ? 1 : 0));
        fields.Add(Text("ver", Version));
        return fields;
    }

    static Field Text(string key, string value) => new Field(key, value ?? "", false);
    static Field Number(string key, int value) => new Field(key, value.ToString(CultureInfo.InvariantCulture), true);
}
