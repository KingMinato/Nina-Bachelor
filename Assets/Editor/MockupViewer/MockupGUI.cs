using UnityEditor;
using UnityEngine;

// Flat, rounded IMGUI look for the Mockup Viewer (cards, tiles, pills, buttons).
// Every widget draws only on Repaint and handles clicks via an invisible button.
static class MockupGUI
{
    static bool Pro => EditorGUIUtility.isProSkin;

    public static Color PanelBg  => Pro ? Hex(0x1F2023) : Hex(0xDADADA);
    public static Color CanvasBg => Pro ? Hex(0x16171A) : Hex(0xC4C4C4);
    public static Color CardBg   => Pro ? Hex(0x2A2C30) : Hex(0xF2F2F2);
    public static Color Border   => Pro ? Hex(0x36383D) : Hex(0xC2C2C2);
    public static Color Hover    => Pro ? Hex(0x34373C) : Hex(0xE6E6E6);
    public static Color Text     => Pro ? Hex(0xE8E8EA) : Hex(0x1E1E1E);
    public static Color SubText  => Pro ? Hex(0x8E949C) : Hex(0x666A70);
    public static readonly Color Accent = Hex(0x4C8DFF);
    public static readonly Color Amber  = Hex(0xF2B33D);
    public static readonly Color Green  = Hex(0x4CC38A);
    public static readonly Color Danger = Hex(0xE5534B);

    static GUIStyle title, caption, header, badge, label, button;
    public static GUIStyle Title       => title   ??= Make(EditorStyles.boldLabel, 15, FontStyle.Bold);
    public static GUIStyle Caption     => caption ??= Make(EditorStyles.miniLabel, 10, FontStyle.Normal, sub: true, wrap: true);
    public static GUIStyle Header      => header  ??= Make(EditorStyles.boldLabel, 11, FontStyle.Bold);
    public static GUIStyle Badge       => badge   ??= Make(EditorStyles.miniBoldLabel, 9, FontStyle.Bold, align: TextAnchor.MiddleCenter);
    public static GUIStyle Label       => label   ??= Make(EditorStyles.label, 11, FontStyle.Normal);
    public static GUIStyle ButtonLabel => button  ??= Make(EditorStyles.boldLabel, 12, FontStyle.Bold, align: TextAnchor.MiddleCenter);

    static GUIStyle Make(GUIStyle from, int size, FontStyle font, bool sub = false, bool wrap = false,
                         TextAnchor align = TextAnchor.MiddleLeft)
    {
        var s = new GUIStyle(from) { fontSize = size, fontStyle = font, wordWrap = wrap, alignment = align, richText = true };
        s.normal.textColor = s.hover.textColor = sub ? SubText : Text;
        return s;
    }

    public static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);

    public static void Rounded(Rect r, Color fill, float radius = 6f)
    {
        if (Event.current.type == EventType.Repaint)
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, fill, 0f, radius);
    }

    public static void Outline(Rect r, Color color, float width = 1f, float radius = 6f)
    {
        if (Event.current.type == EventType.Repaint)
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color, width, radius);
    }

    public static bool IsHover(Rect r) => r.Contains(Event.current.mousePosition);

    // Small pill with a number, right-aligned in row.
    public static void Pill(Rect row, string text, Color fill)
    {
        float w = Mathf.Max(20f, Badge.CalcSize(new GUIContent(text)).x + 10f);
        var r = new Rect(row.xMax - w, row.center.y - 8f, w, 16f);
        Rounded(r, fill, 8f);
        GUI.Label(r, text, Badge);
    }

    static void Chevron(Vector2 c, bool open, Color color)
    {
        if (Event.current.type != EventType.Repaint) return;
        Handles.color = color;
        Vector3[] tri = open
            ? new Vector3[] { c + new Vector2(-4f, -2f), c + new Vector2(4f, -2f), c + new Vector2(0f, 3f) }
            : new Vector3[] { c + new Vector2(-2f, -4f), c + new Vector2(3f, 0f), c + new Vector2(-2f, 4f) };
        Handles.DrawAAConvexPolygon(tri);
    }

    // A rounded, collapsible card. Returns the new expanded state.
    public static bool Card(string title, string badge, bool expanded, System.Action body)
    {
        Rect card = EditorGUILayout.BeginVertical(new GUIStyle { padding = new RectOffset(10, 10, 4, 8), margin = new RectOffset(8, 8, 0, 8) });
        Rounded(card, CardBg);
        Outline(card, Border);

        Rect head = GUILayoutUtility.GetRect(1f, 26f, GUILayout.ExpandWidth(true));
        Chevron(new Vector2(head.x + 5f, head.center.y), expanded, SubText);
        GUI.Label(new Rect(head.x + 16f, head.y, head.width - 16f, head.height), title.ToUpperInvariant(), Header);
        if (badge != null) Pill(head, badge, Border);
        if (GUI.Button(head, GUIContent.none, GUIStyle.none)) expanded = !expanded;
        EditorGUIUtility.AddCursorRect(head, MouseCursor.Link);

        if (expanded)
        {
            GUILayout.Space(2f);
            body();
        }
        EditorGUILayout.EndVertical();
        return expanded;
    }

    // Selectable tile with a colour swatch; returns true when clicked.
    public static bool Tile(string text, Color swatch, bool selected, float height = 28f)
    {
        Rect r = GUILayoutUtility.GetRect(1f, height, GUILayout.ExpandWidth(true));
        r.height -= 3f;
        Rounded(r, selected ? Color.Lerp(CardBg, Accent, 0.18f) : IsHover(r) ? Hover : Color.clear);
        Outline(r, selected ? Accent : Border, selected ? 1.5f : 1f);
        Rounded(new Rect(r.x + 8f, r.center.y - 6f, 12f, 12f), swatch, 3f);
        GUI.Label(new Rect(r.x + 28f, r.y, r.width - 30f, r.height), text, Label);
        EditorGUIUtility.AddCursorRect(r, MouseCursor.Link);
        return GUI.Button(r, GUIContent.none, GUIStyle.none);
    }

    // Full-width rounded button. primary = accent-filled.
    public static bool Button(string text, bool primary, float height = 28f, Color? accent = null)
    {
        Rect r = GUILayoutUtility.GetRect(1f, height, GUILayout.ExpandWidth(true));
        return Button(r, text, primary, accent);
    }

    public static bool Button(Rect r, string text, bool primary = false, Color? accent = null)
    {
        Color a = accent ?? Accent;
        Rounded(r, primary ? (IsHover(r) ? Color.Lerp(a, Color.white, 0.12f) : a) : (IsHover(r) ? Hover : CardBg));
        if (!primary) Outline(r, Border);
        var style = new GUIStyle(ButtonLabel) { clipping = TextClipping.Clip };
        style.normal.textColor = primary ? Color.white : Text;
        GUI.Label(r, text, style);
        EditorGUIUtility.AddCursorRect(r, MouseCursor.Link);
        return GUI.Button(r, GUIContent.none, GUIStyle.none);
    }
}
