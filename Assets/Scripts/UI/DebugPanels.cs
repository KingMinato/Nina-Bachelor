using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Shared parts of the debug switchers (top-right column, laid out by UiStyleBuilder.LayoutDebug):
// one row per switcher showing its current value, tapping it opens the switcher's panel of titled button groups.
// Only one panel is open at a time.
public static class DebugPanels
{
    static readonly Color Normal = new Color(0.17f, 0.18f, 0.21f, 0.94f);
    static readonly Color Selected = new Color(0.98f, 0.76f, 0.3f, 1f);
    const string Muted = "#9AA3AD";
    const int Columns = 4;

    static GameObject open;
    static Button openRow;

    public static void Toggle(GameObject panel, Button row)
    {
        bool show = !panel.activeSelf;
        if (show && open != null && open != panel)
        {
            open.SetActive(false);
            Mark(openRow, false);
        }
        panel.SetActive(show);
        Mark(row, show);
        open = show ? panel : null;
        openRow = show ? row : null;
    }

    // "Avatar  humanC_big · m": name muted, value bright.
    public static void SetRow(Button row, string name, string value) =>
        row.GetComponentInChildren<TMP_Text>().text = $"<color={Muted}>{name}</color>   {value}";

    public static void Mark(Button button, bool selected)
    {
        button.image.color = selected ? Selected : Normal;
        button.GetComponentInChildren<TMP_Text>().color = selected ? new Color(0.12f, 0.1f, 0.06f) : Color.white;
    }

    // A titled group in a panel: header line, then a grid the buttons go into (AddButton).
    public static Transform Section(Transform panel, Button template, string title)
    {
        var header = Object.Instantiate(template.GetComponentInChildren<TMP_Text>(true), panel);
        header.name = "Header";
        header.text = title.ToUpperInvariant();
        header.fontSize = 16;
        header.characterSpacing = 4;
        header.alignment = TextAlignmentOptions.BottomLeft;
        header.color = new Color(0.6f, 0.64f, 0.68f);
        header.gameObject.AddComponent<LayoutElement>().preferredHeight = 30;

        var grid = new GameObject(title, typeof(RectTransform), typeof(GridLayoutGroup)).GetComponent<GridLayoutGroup>();
        grid.transform.SetParent(panel, false);
        grid.cellSize = new Vector2(190, 46);
        grid.spacing = new Vector2(6, 6);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Columns;
        return grid.transform;
    }

    public static Button AddButton(Transform grid, Button template, string label, UnityAction onClick)
    {
        var button = Object.Instantiate(template, grid);
        button.gameObject.SetActive(true);
        button.GetComponentInChildren<TMP_Text>().text = label;
        button.onClick.AddListener(onClick);
        Mark(button, false);
        return button;
    }
}
