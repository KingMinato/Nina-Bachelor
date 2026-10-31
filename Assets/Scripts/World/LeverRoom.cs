using System;
using System.Collections.Generic;
using UnityEngine;

// Room 3: the door to the NPC is locked until the levers are set like on the sign above them.
public class LeverRoom : MonoBehaviour
{
    [SerializeField] UIManager ui;
    [SerializeField] ActionButtonsUI actionButtons;
    [SerializeField] PlayerActions player;
    [SerializeField] StepDoneUI stepDone;
    [SerializeField] WorldButton doorButton;
    [SerializeField] Lever[] levers;
    [SerializeField] WorldButton[] leverButtons;
    [Tooltip("Lever positions that open the door, true = up. Must match the sign.")]
    [SerializeField] bool[] solution;
    [SerializeField] WalkPath walkToLevers;

    public static bool Matches(bool[] positions, bool[] solution)
    {
        for (int i = 0; i < solution.Length; i++)
            if (positions[i] != solution[i])
                return false;
        return true;
    }

    // Completes when the player taps the unlocked door.
    public async Awaitable Run(Texts texts)
    {
        actionButtons.Show(texts, withJump: true);
        bool solved = false, atLevers = false;
        while (true)
        {
            var options = new List<(WorldButton, string)> { (doorButton, texts.goToDoor) };
            if (!solved)
                foreach (var b in leverButtons)
                    options.Add((b, ""));
            var clicked = await WorldButton.FirstClick(options.ToArray());
            if (clicked == doorButton)
            {
                if (solved)
                    break;
                ui.ShowInstruction(texts.lockedLevers);
                continue;
            }
            // The first lever tap walks the player up to the levers.
            if (!atLevers)
            {
                actionButtons.Hide();
                await player.StandUp();
                await walkToLevers.Walk(player.transform);
                actionButtons.Show(texts, withJump: true);
                atLevers = true;
            }
            await levers[Array.IndexOf(leverButtons, clicked)].Toggle();
            solved = Matches(Array.ConvertAll(levers, l => l.Up), solution);
            if (solved)
            {
                ui.HideInstruction();
                UiSfx.Play(UiSfx.Door);
                stepDone.Show(texts.leversSolved);
            }
        }
        ui.HideInstruction();
        actionButtons.Hide();
        await player.StandUp();
    }
}
