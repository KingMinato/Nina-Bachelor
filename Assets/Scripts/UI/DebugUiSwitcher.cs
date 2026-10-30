using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Test tool (starts inactive, GameFlow activates it in the Unity Editor and with ?debug=1):
// switches the UI style instantly. The "UI" button opens a grid with all styles and close.
// The grid buttons are created from an inactive template button at startup.
public class DebugUiSwitcher : MonoBehaviour
{
    static readonly Color Normal = Color.white;
    static readonly Color Selected = new Color(1f, 0.85f, 0.4f);

    [SerializeField] UiStyles styles;
    [SerializeField] DebugOverlay overlay;
    [SerializeField] Button toggleButton;
    [SerializeField] GameObject panel;
    [SerializeField] Transform grid;
    [SerializeField] Button template;

    Button[] styleButtons;

    void Start()
    {
        var all = styles.Styles;
        styleButtons = new Button[all.Length];
        for (int i = 0; i < all.Length; i++)
        {
            var style = all[i];
            styleButtons[i] = AddButton(style.displayName, () => Switch(style));
        }
        AddButton("Schließen", () => panel.SetActive(false));

        toggleButton.onClick.AddListener(() =>
        {
            panel.SetActive(!panel.activeSelf);
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

    void Switch(UiStyle style)
    {
        styles.Apply(style);
        overlay.ShowUi(style.displayName);
        Highlight();
    }

    void Highlight()
    {
        var all = styles.Styles;
        for (int i = 0; i < all.Length; i++)
            styleButtons[i].image.color = all[i] == UiStyles.Current ? Selected : Normal;
    }
}
