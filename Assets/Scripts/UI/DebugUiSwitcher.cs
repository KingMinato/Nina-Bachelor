using UnityEngine;
using UnityEngine.UI;

// Test tool (starts inactive, GameFlow activates it in the Unity Editor and in test runs):
// switches the UI style instantly. The "UI" row opens a panel with all of them.
// The buttons are created from an inactive template button at startup.
public class DebugUiSwitcher : MonoBehaviour
{
    [SerializeField] UiStyles styles;
    [SerializeField] Button toggleButton;
    [SerializeField] GameObject panel;
    [SerializeField] Transform grid;
    [SerializeField] Button template;

    Button[] buttons;

    void Start()
    {
        var all = styles.Styles;
        var section = DebugPanels.Section(grid, template, "UI-Stil");
        buttons = new Button[all.Length];
        for (int i = 0; i < all.Length; i++)
        {
            var item = all[i];
            buttons[i] = DebugPanels.AddButton(section, template, item.displayName, () =>
            {
                styles.Apply(item);
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
        DebugPanels.SetRow(toggleButton, "UI", UiStyles.Current.displayName);
        var all = styles.Styles;
        for (int i = 0; i < all.Length; i++)
            DebugPanels.Mark(buttons[i], all[i] == UiStyles.Current);
    }
}
