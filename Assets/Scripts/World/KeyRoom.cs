using UnityEngine;

// Room 2: the door to Room 3 is locked until the player has taken the key from the wall.
// Door and key can only be used from the spot in front of them (RoomNav).
public class KeyRoom : MonoBehaviour
{
    [SerializeField] UIManager ui;
    [SerializeField] ActionButtonsUI actionButtons;
    [SerializeField] PlayerActions player;
    [SerializeField] StepDoneUI stepDone;
    [SerializeField] RoomNav nav;
    [SerializeField] WorldButton doorButton;
    [SerializeField] Transform doorSpot;
    [SerializeField] WorldButton keyButton;
    [SerializeField] Transform keySpot;
    [SerializeField] GameObject key;

    // Completes when the player taps the door with the key.
    public async Awaitable Run(Texts texts)
    {
        nav.Enter();
        actionButtons.Show(texts);
        bool hasKey = false;
        while (true)
        {
            var clicked = hasKey
                ? await nav.Next(texts, (doorButton, texts.goToDoor, doorSpot))
                : await nav.Next(texts, (doorButton, texts.goToDoor, doorSpot), (keyButton, texts.key, keySpot));
            if (clicked == doorButton)
            {
                if (hasKey)
                    break;
                ui.ShowInstruction(texts.lockedKey);
                continue;
            }
            key.SetActive(false);
            hasKey = true;
            ui.HideInstruction();
            UiSfx.Play(UiSfx.Coin);
            stepDone.Show(texts.keyTaken);
        }
        ui.HideInstruction();
        actionButtons.Hide();
        await player.StandUp();
    }
}
