using System;
using UnityEngine;

// Mirrors StreamingAssets/config.json. Field initializers are the built-in defaults,
// used for anything missing from the file or when the file fails to load.
[Serializable]
public class Config
{
    public string version = "0.1.0";
    public int moneyPerRound = 100;
    public int splitStep = 5;
    public int npcAcceptMaxPlayerKeeps = 90;
    public int round2NpcKeeps = 50;
    public int round4NpcKeeps = 75;
    public float turnAngle = 45f;
    public float submitTimeoutSeconds = 5f;
    public string appsScriptUrl = "";
    public string returnUrlTemplate = "";
    public TutorialStep[] tutorialSteps =
    {
        new TutorialStep("Dreh dich nach links.", TutorialStep.TurnLeft, 2),
        new TutorialStep("Dreh dich nach rechts.", TutorialStep.TurnRight, 2),
        new TutorialStep("Spring einmal.", TutorialStep.Jump, 1),
        new TutorialStep("Dreh dich einmal komplett um.", TutorialStep.TurnLeft, 8),
    };
    public Texts texts = new Texts();

    public static Config FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new Config();

        try
        {
            return JsonUtility.FromJson<Config>(json) ?? new Config();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Invalid config, using defaults: {e.Message}");
            return new Config();
        }
    }
}

[Serializable]
public class TutorialStep
{
    public const string TurnLeft = "turnLeft";
    public const string TurnRight = "turnRight";
    public const string Jump = "jump";

    public string text;
    public string action;
    public int count = 1;

    public TutorialStep() { }

    public TutorialStep(string text, string action, int count)
    {
        this.text = text;
        this.action = action;
        this.count = count;
    }
}

[Serializable]
public class Texts
{
    public string intro = "PLACEHOLDER";
    public string doorOpened = "PLACEHOLDER";
    public string goToDoor = "Zur Tür";
    public string talk = "Ansprechen";
    public string npcGreeting = "PLACEHOLDER";
    public string round1Intro = "PLACEHOLDER";
    public string round2Intro = "PLACEHOLDER";
    public string round3Intro = "PLACEHOLDER";
    public string round4Intro = "PLACEHOLDER";
    public string accept = "Annehmen";
    public string reject = "Ablehnen";
    public string confirm = "Bestätigen";
    public string npcAccepted = "PLACEHOLDER";
    public string npcRejected = "PLACEHOLDER";
    public string balance = "Kontostand";
    public string end = "Danke! Du wirst jetzt zur Umfrage weitergeleitet.";
    public string rotateDevice = "Bitte Gerät drehen";
}
