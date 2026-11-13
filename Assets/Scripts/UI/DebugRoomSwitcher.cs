using UnityEngine;
using UnityEngine.UI;

// Test tool (starts inactive, GameFlow activates it in the Unity Editor and in test runs):
// switches the room variant instantly. The "Raum" row opens a panel with all of them.
// The buttons are created from an inactive template button at startup.
public class DebugRoomSwitcher : MonoBehaviour
{
    [SerializeField] RoomVariants rooms;
    [SerializeField] Button toggleButton;
    [SerializeField] GameObject panel;
    [SerializeField] Transform grid;
    [SerializeField] Button template;

    Button[] buttons;

    void Start()
    {
        var all = rooms.Variants;
        var section = DebugPanels.Section(grid, template, "Raumvariante");
        buttons = new Button[all.Length];
        for (int i = 0; i < all.Length; i++)
        {
            var item = all[i];
            buttons[i] = DebugPanels.AddButton(section, template, item.DisplayName, () =>
            {
                rooms.Apply(item);
                Highlight();
            });
        }
        toggleButton.onClick.AddListener(() => DebugPanels.Toggle(panel, toggleButton));
        panel.SetActive(false);
        DebugPanels.Mark(toggleButton, false);
        Highlight();
    }

    void Highlight()
    {
        DebugPanels.SetRow(toggleButton, "Raum", rooms.Current.DisplayName);
        var all = rooms.Variants;
        for (int i = 0; i < all.Length; i++)
            DebugPanels.Mark(buttons[i], all[i] == rooms.Current);
    }
}
