using System;
using System.Collections.Generic;
using UnityEngine;

// Room 3: the door to the NPC is locked until the levers are set like on the sign above them.
// Door and levers can only be used from the spot in front of them (RoomNav).
public class LeverRoom : MonoBehaviour
{
    [SerializeField] UIManager ui;
    [SerializeField] ActionButtonsUI actionButtons;
    [SerializeField] PlayerActions player;
    [SerializeField] StepDoneUI stepDone;
    [SerializeField] RoomNav nav;
    [SerializeField] WorldButton doorButton;
    [SerializeField] Transform doorSpot;
    [SerializeField] Lever[] levers;
    [SerializeField] WorldButton[] leverButtons;
    [SerializeField] Transform leverSpot;
    [Tooltip("Lever positions that open the door, true = up. Must match the sign.")]
    [SerializeField] bool[] solution;

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
        nav.Enter();
        nav.onWalk = ui.HideInstruction; // the locked hint belongs to the door spot
        actionButtons.Show(texts);
        bool solved = false;
        while (true)
        {
            var targets = new List<(WorldButton, string, Transform)> { (doorButton, texts.goToDoor, doorSpot) };
            if (!solved)
                foreach (var b in leverButtons)
                    targets.Add((b, "", leverSpot));
            var clicked = await nav.Next(texts, targets.ToArray());
            if (clicked == doorButton)
            {
                if (solved)
                    break;
                ui.ShowInstruction(texts.lockedLevers);
                continue;
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
