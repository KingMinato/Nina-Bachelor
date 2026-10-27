using System;

// Outcome of one ultimatum round.
public readonly struct RoundResult
{
    public readonly bool Accepted;
    public readonly int PlayerGets;
    public readonly int NpcGets;
    // Value stored as r1..r4: amount the player keeps (player splits) or 1/0 (NPC offers).
    public readonly int RecordedValue;

    public RoundResult(bool accepted, int playerGets, int npcGets, int recordedValue)
    {
        Accepted = accepted;
        PlayerGets = playerGets;
        NpcGets = npcGets;
        RecordedValue = recordedValue;
    }
}

// Pure game rules, no UI. Rounds are 1-based: odd rounds the player splits, even rounds the NPC offers.
public static class UltimatumRules
{
    public static bool PlayerSplits(int round) => round % 2 == 1;

    public static int NpcKeeps(int round, Config c)
    {
        switch (round)
        {
            case 2: return c.round2NpcKeeps;
            case 4: return c.round4NpcKeeps;
            default: throw new ArgumentOutOfRangeException(nameof(round), round, "NPC only offers in rounds 2 and 4");
        }
    }

    // Player keeps playerKeeps, the NPC accepts if that is at most npcAcceptMaxPlayerKeeps.
    public static RoundResult PlayerSplit(int playerKeeps, Config c)
    {
        bool accepted = playerKeeps <= c.npcAcceptMaxPlayerKeeps;
        return accepted
            ? new RoundResult(true, playerKeeps, c.moneyPerRound - playerKeeps, playerKeeps)
            : new RoundResult(false, 0, 0, playerKeeps);
    }

    // NPC keeps npcKeeps and offers the rest, the player accepts or rejects.
    public static RoundResult NpcOffer(int npcKeeps, bool accepted, Config c)
    {
        return accepted
            ? new RoundResult(true, c.moneyPerRound - npcKeeps, npcKeeps, 1)
            : new RoundResult(false, 0, 0, 0);
    }

    // Rounds to the nearest multiple of splitStep and clamps to 0..moneyPerRound.
    public static int SnapToStep(int value, Config c)
    {
        int step = Math.Max(1, c.splitStep);
        int snapped = (int)Math.Round(value / (double)step, MidpointRounding.AwayFromZero) * step;
        return Math.Clamp(snapped, 0, c.moneyPerRound);
    }
}
