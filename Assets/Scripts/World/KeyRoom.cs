using UnityEngine;

// Room 2: the door to Room 3 is locked until the player has taken the key from the wall.
public class KeyRoom : MonoBehaviour
{
    [SerializeField] UIManager ui;
    [SerializeField] ActionButtonsUI actionButtons;
    [SerializeField] PlayerActions player;
    [SerializeField] StepDoneUI stepDone;
    [SerializeField] WorldButton doorButton;
    [SerializeField] WorldButton keyButton;
    [SerializeField] GameObject key;
    [SerializeField] WalkPath walkToKey;

    // Completes when the player taps the door with the key.
    public async Awaitable Run(Texts texts)
    {
        actionButtons.Show(texts, withJump: true);
        bool hasKey = false;
        while (true)
        {
            var clicked = hasKey
                ? await WorldButton.FirstClick((doorButton, texts.goToDoor))
                : await WorldButton.FirstClick((doorButton, texts.goToDoor), (keyButton, texts.key));
            if (clicked == doorButton)
            {
                if (hasKey)
                    break;
                ui.ShowInstruction(texts.lockedKey);
                continue;
            }
            actionButtons.Hide();
            await player.StandUp();
            await walkToKey.Walk(player.transform);
            key.SetActive(false);
            hasKey = true;
            ui.HideInstruction();
            UiSfx.Play(UiSfx.Coin);
            stepDone.Show(texts.keyTaken);
            actionButtons.Show(texts, withJump: true);
        }
        ui.HideInstruction();
        actionButtons.Hide();
        await player.StandUp();
    }
}
