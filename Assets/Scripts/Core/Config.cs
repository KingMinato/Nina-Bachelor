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
    // 90:10 is rejected (decided 2026-10-31).
    public int npcAcceptMaxPlayerKeeps = 85;
    public int round2NpcKeeps = 50;
    public int round4NpcKeeps = 75;
    public float turnAngle = 90f;
    // Avatar species id (from the AvatarDatabase) for each Qualtrics species value s = 1..4.
    public string[] species = { "humanC", "goblin", "slime", "cow" };
    public float submitTimeoutSeconds = 5f;
    public string appsScriptUrl = "";
    public string returnUrlTemplate = "";
    // Shows the debug switchers also to runs with a pid (testing via the survey). Must be false for the pilot.
    public bool debugTools;
    // Balance plaque during the dialog (may encourage playing for a high score, to be decided with the supervisor).
    public bool showBalance = true;
    public TutorialStep[] tutorialSteps =
    {
        new TutorialStep("Nach links drehen", TutorialStep.TurnLeft, 2),
        new TutorialStep("Nach rechts drehen", TutorialStep.TurnRight, 2),
        new TutorialStep("Eine komplette Umdrehung vollführen", TutorialStep.TurnAround, 1),
        new TutorialStep("Einmal springen", TutorialStep.Jump, 1),
        new TutorialStep("Ducken und wieder aufstehen", TutorialStep.Crouch, 1),
        new TutorialStep("Ducken und direkt hochspringen", TutorialStep.CrouchJump, 1),
    };
    public Texts texts = new Texts();
    // Used with ?lang=en. Missing keys keep the German default.
    public Texts textsEn = new Texts();

    // Switches texts and tutorial step texts to English (lang = "EN"), everything else stays.
    public void UseLanguage(string lang)
    {
        if (lang != "EN")
            return;
        texts = textsEn;
        foreach (var step in tutorialSteps)
            if (!string.IsNullOrEmpty(step.textEn))
                step.text = step.textEn;
    }

    public string SpeciesId(int s) => species != null && s >= 1 && s <= species.Length ? species[s - 1] : "";

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
    // One full 360 degree turn, left or right, made of any number of turn steps.
    public const string TurnAround = "turnAround";
    public const string Jump = "jump";
    // Crouch, then stand up again: counted when standing up.
    public const string Crouch = "crouch";
    public const string CrouchJump = "crouchJump";
    // Performed actions only, not used as tutorial steps.
    public const string StandUp = "standUp";

    public string text;
    public string textEn;
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
    // Pages shown one after another, each with a "next" button.
    public string[] intro = { "PLACEHOLDER" };
    // Shown after the tutorial while the closed door can be tapped.
    public string goToDoorHint = "PLACEHOLDER";
    public string jump = "Springen";
    public string crouch = "Ducken";
    public string standUp = "Aufstehen";
    // Shown briefly after each completed tutorial step, empty = not shown.
    public string stepDone = "Geschafft!";
    // Door tags: open, locked (key or levers missing), unlock (player holds the key).
    public string openDoor = "Öffnen";
    public string doorLocked = "Verschlossen";
    public string unlock = "Aufschließen";
    // Room 2: tag on the key, door locked without it, short notes when taken and when the door is unlocked.
    public string key = "Schlüssel";
    public string lockedKey = "Hm, die Tür ist verschlossen. Ob es wohl irgendwo einen Schlüssel hat?";
    public string keyTaken = "Schlüssel gefunden!";
    public string doorUnlocked = "Aufgeschlossen!";
    // Room 3: door locked until the levers are set like on the sign, short note when solved.
    public string lockedLevers = "Hm, wieder verschlossen. Vielleicht wenn ich die Hebel richtig betätige …?";
    public string leversSolved = "Klick!";
    public string talk = "Ansprechen";
    public string[] npcGreeting = { "PLACEHOLDER" };
    // Asked after the greeting with yes/no; "no" shows rulesExplain first.
    public string rulesCheck = "Alles klar?";
    public string yes = "Ja";
    public string no = "Nein";
    public string[] rulesExplain = { "PLACEHOLDER" };
    // Name tag above the NPC's dialog lines, empty = no tag.
    public string speaker = "Wächter";
    // Round label during the dialog, empty = hidden.
    public string round = "Runde {round} von {total}";
    public string round1Intro = "PLACEHOLDER";
    public string round2Intro = "PLACEHOLDER";
    public string round3Intro = "PLACEHOLDER";
    public string round4Intro = "PLACEHOLDER";
    public string accept = "Annehmen";
    public string reject = "Ablehnen";
    public string confirm = "Bestätigen";
    public string npcAccepted = "PLACEHOLDER";
    public string npcRejected = "PLACEHOLDER";
    // NPC's reply after the player accepted or rejected its offer.
    public string playerAccepted = "Alles klar!";
    public string playerRejected = "Alles klar!";
    // {player} and {npc} are replaced with the amounts.
    public string splitShares = "Du: {player} Münzen / Wächter: {npc} Münzen";
    public string npcOffer = "Ich behalte {npc} Münzen, du bekommst {player} Münzen.";
    public string roundResult = "Du erhältst {player} Münzen, ich erhalte {npc} Münzen.";
    public string next = "Weiter";
    public string balance = "Kontostand";
    // Unit after amounts on the balance plaque.
    public string coins = "Münzen";
    public string end = "Vielen Dank für deine Teilnahme. Du wirst nun wieder zur Umfrage zurückkehren.";
    public string rotateDevice = "Bitte Gerät drehen";

    public static string Fill(string template, int player, int npc)
    {
        return (template ?? "").Replace("{player}", player.ToString()).Replace("{npc}", npc.ToString());
    }
}
