using UnityEngine;

// Runs the four ultimatum rounds and records each one in SessionData.
// There is no way out of the dialog: every step only offers the buttons that continue it.
public class UltimatumController : MonoBehaviour
{
    [SerializeField] UIManager ui;
    [SerializeField] SplitInputUI splitInput;
    [SerializeField] BalanceUI balanceUI;
    [SerializeField] RoundUI roundUI;
    [SerializeField] NpcLookAt npc;
    [SerializeField] float thinkMinSeconds = 1f;
    [SerializeField] float thinkMaxSeconds = 4f;

    public async Awaitable Run(Config config, SessionData session)
    {
        var texts = config.texts;
        if (config.showBalance)
            balanceUI.Show(texts.balance, texts.coins, session.Balance);
        foreach (string page in texts.npcGreeting)
            await ui.ShowPrompt(page, texts.next, texts.speaker);
        if (!await ui.ShowChoice(texts.rulesCheck, texts.yes, texts.no, texts.speaker))
            foreach (string page in texts.rulesExplain)
                await ui.ShowPrompt(page, texts.next, texts.speaker);

        for (int round = 1; round <= SessionData.RoundCount; round++)
        {
            roundUI.Show(texts.round, round, SessionData.RoundCount);
            string intro = RoundIntro(round, texts);
            RoundResult result;
            string resultLine;

            // Decision time runs from the options being shown (after the text is typed out) until the player confirms.
            // The NPC thinks after the player's split and before its own offer, outside the decision time.
            int decisionMs;
            if (UltimatumRules.PlayerSplits(round))
            {
                int keeps = await splitInput.Ask(intro, config, config.moneyPerRound / 2);
                decisionMs = MsSince(splitInput.OptionsShownAt);
                await Think(UltimatumRules.ThinkEffort(keeps, config));
                result = UltimatumRules.PlayerSplit(keeps, config);
                resultLine = result.Accepted ? texts.npcAccepted : texts.npcRejected;
            }
            else
            {
                int npcKeeps = UltimatumRules.NpcKeeps(round, config);
                await Think(UltimatumRules.ThinkEffort(npcKeeps, config));
                string offer = Texts.Fill(texts.npcOffer, config.moneyPerRound - npcKeeps, npcKeeps);
                bool accepted = await ui.ShowChoice(intro + "\n\n" + offer, texts.accept, texts.reject, texts.speaker);
                decisionMs = MsSince(ui.OptionsShownAt);
                result = UltimatumRules.NpcOffer(npcKeeps, accepted, config);
                resultLine = result.Accepted ? texts.playerAccepted : texts.playerRejected;
            }

            session.RecordRound(round, result.RecordedValue, decisionMs, result.PlayerGets);
            balanceUI.Set(session.Balance);

            string payout = Texts.Fill(texts.roundResult, result.PlayerGets, result.NpcGets);
            await ui.ShowPrompt(resultLine + "\n\n" + payout, texts.next, texts.speaker);
        }
    }

    static int MsSince(double shownAt) => (int)System.Math.Round((Time.realtimeSinceStartupAsDouble - shownAt) * 1000);

    // Easy: hand at the chin, short. Hard (effort from 0.5): hands on the head, long.
    async Awaitable Think(float effort)
    {
        npc.Think(effort < 0.5f ? NpcLookAt.Gesture.Chin : NpcLookAt.Gesture.Head);
        await Awaitable.WaitForSecondsAsync(Mathf.Lerp(thinkMinSeconds, thinkMaxSeconds, effort));
        npc.Think(NpcLookAt.Gesture.None);
        // Back in the idle pose before the answer appears.
        await Awaitable.WaitForSecondsAsync(0.6f);
    }

    static string RoundIntro(int round, Texts texts)
    {
        switch (round)
        {
            case 1: return texts.round1Intro;
            case 2: return texts.round2Intro;
            case 3: return texts.round3Intro;
            default: return texts.round4Intro;
        }
    }
}
