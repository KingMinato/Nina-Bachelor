using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Test tool (starts inactive, GameFlow activates it in the Unity Editor and in test runs):
// switches the room variant instantly. The "Raum" button opens a grid with all variants and close.
// The grid buttons are created from an inactive template button at startup.
public class DebugRoomSwitcher : MonoBehaviour
{
    static readonly Color Normal = Color.white;
    static readonly Color Selected = new Color(1f, 0.85f, 0.4f);

    [SerializeField] RoomVariants rooms;
    [SerializeField] DebugOverlay overlay;
    [SerializeField] Button toggleButton;
    [SerializeField] GameObject panel;
    [SerializeField] Transform grid;
    [SerializeField] Button template;

    Button[] variantButtons;

    void Start()
    {
        var variants = rooms.Variants;
        variantButtons = new Button[variants.Length];
        for (int i = 0; i < variants.Length; i++)
        {
            var variant = variants[i];
            variantButtons[i] = AddButton(variant.DisplayName, () => Switch(variant));
        }
        AddButton("Schließen", () => panel.SetActive(false));

        toggleButton.onClick.AddListener(() =>
        {
            DebugPanels.Toggle(panel);
            Highlight();
        });
        panel.SetActive(false);
    }

    Button AddButton(string label, UnityAction onClick)
    {
        var button = Instantiate(template, grid);
        button.gameObject.SetActive(true);
        button.GetComponentInChildren<TMP_Text>().text = label;
        button.onClick.AddListener(onClick);
        return button;
    }

    void Switch(RoomVariant variant)
    {
        rooms.Apply(variant);
        overlay.ShowRoom(variant.DisplayName);
        Highlight();
    }

    void Highlight()
    {
        var variants = rooms.Variants;
        for (int i = 0; i < variants.Length; i++)
            variantButtons[i].image.color = variants[i] == rooms.Current ? Selected : Normal;
    }
}
