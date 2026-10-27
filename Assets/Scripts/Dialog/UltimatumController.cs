using UnityEngine;

// Runs the four ultimatum rounds and records each one in SessionData.
// There is no way out of the dialog: every step only offers the buttons that continue it.
public class UltimatumController : MonoBehaviour
{
    [SerializeField] UIManager ui;
    [SerializeField] SplitInputUI splitInput;
    [SerializeField] BalanceUI balanceUI;

    public async Awaitable Run(Config config, SessionData session)
    {
        var texts = config.texts;
        balanceUI.Show(texts.balance, session.Balance);

        for (int round = 1; round <= SessionData.RoundCount; round++)
        {
            string intro = RoundIntro(round, texts);
            RoundResult result;
            string resultLine;

            // Decision time runs from the options being shown until the player confirms.
            double shownAt = Time.realtimeSinceStartupAsDouble;
            if (UltimatumRules.PlayerSplits(round))
            {
                int keeps = await splitInput.Ask(intro, config, config.moneyPerRound / 2);
                result = UltimatumRules.PlayerSplit(keeps, config);
                resultLine = result.Accepted ? texts.npcAccepted : texts.npcRejected;
            }
            else
            {
                int npcKeeps = UltimatumRules.NpcKeeps(round, config);
                string offer = Texts.Fill(texts.npcOffer, config.moneyPerRound - npcKeeps, npcKeeps);
                bool accepted = await ui.ShowChoice(intro + "\n\n" + offer, texts.accept, texts.reject);
                result = UltimatumRules.NpcOffer(npcKeeps, accepted, config);
                resultLine = result.Accepted ? texts.playerAccepted : texts.playerRejected;
            }
            int decisionMs = (int)System.Math.Round((Time.realtimeSinceStartupAsDouble - shownAt) * 1000);

            session.RecordRound(round, result.RecordedValue, decisionMs, result.PlayerGets);
            balanceUI.Set(session.Balance);

            string payout = Texts.Fill(texts.roundResult, result.PlayerGets, result.NpcGets);
            await ui.ShowPrompt(resultLine + "\n\n" + payout, texts.next);
        }
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
