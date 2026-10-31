using UnityEngine;

// Room 2: the door to Room 3 is locked until the player has taken the key from the wall and used it on the door.
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
    [SerializeField] KeyInventory inventory;

    // Completes when the player taps the unlocked door.
    public async Awaitable Run(Texts texts)
    {
        nav.Enter();
        nav.onWalk = ui.HideInstruction; // the locked hint belongs to the door spot
        actionButtons.Show(texts);
        inventory.gameObject.SetActive(true);
        bool hasKey = false, unlocked = false;
        while (true)
        {
            var clicked = hasKey
                ? await nav.Next(texts, (doorButton, texts.goToDoor, doorSpot))
                : await nav.Next(texts, (doorButton, texts.goToDoor, doorSpot), (keyButton, texts.key, keySpot));
            if (clicked == doorButton && unlocked)
                break;
            if (clicked == doorButton && !hasKey)
            {
                ui.ShowInstruction(texts.lockedKey);
                continue;
            }
            ui.HideInstruction();
            actionButtons.Hide();
            if (clicked == doorButton)
            {
                await inventory.Use(key.transform);
                unlocked = true;
                stepDone.Show(texts.doorUnlocked);
            }
            else
            {
                await inventory.Take(key.transform);
                hasKey = true;
                stepDone.Show(texts.keyTaken);
            }
            actionButtons.Show(texts);
        }
        inventory.gameObject.SetActive(false);
        ui.HideInstruction();
        actionButtons.Hide();
        await player.StandUp();
    }
}
