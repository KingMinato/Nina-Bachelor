using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UI = MockupGUI;
using Object = UnityEngine.Object;

// Mockup Viewer (menu Mirror Game/Mockup Viewer): local review loop between the developer and Claude.
// Claude drops one folder per mockup into Assets/_Mockups/{Name}/ (git-ignored): a mockup.json (Mockup) plus its
// files (png/jpg, wav/mp3/ogg, prefab; anything else opens externally). The developer rates every variant here;
// the window writes feedback.json next to it, which Claude reads back. Prefabs render live on a neutral floor,
// optionally next to an avatar for size; in Play mode they can be spawned in front of the player.
// Ported from the WishOfHers Mockup Viewer.
public class MockupViewerWindow : EditorWindow
{
    public const string Root = "Assets/_Mockups";
    public const string DefaultReference = "Assets/Prefabs/Avatars/Avatar_humanC_m.prefab";
    const string ReferenceFolder = "Assets/Prefabs/Avatars";
    const string ReferencePref = "MirrorGame.MockupViewer.Reference";
    const float SidebarWidth = 264f;

    [Serializable] public class Variant { public string name, description; public string[] files; }
    [Serializable] public class Mockup { public string title, description, question; public float loop; public Variant[] variants; } // loop > 0 overrides the slider
    [Serializable] public class VariantFeedback { public string name, rating = "", comment = ""; } // rating: "", Gut, Anpassen, Nein
    [Serializable] public class Feedback { public bool decided; public string overall = ""; public List<VariantFeedback> variants = new List<VariantFeedback>(); }

    sealed class Entry { public string folder; public Mockup data; public Feedback fb; }

    static readonly (string label, Color color)[] Ratings =
        { ("Gut", UI.Green), ("Anpassen", UI.Amber), ("Nein", UI.Danger) };

    readonly List<Entry> entries = new List<Entry>();
    readonly List<string> references = new List<string>();
    readonly List<GameObject> spawned = new List<GameObject>();
    readonly Dictionary<string, Texture2D> thumbs = new Dictionary<string, Texture2D>();
    Stage stage;
    Vector2 sideScroll, mainScroll, panelScroll;
    double t0, lastTick;
    float pausedAt;
    AudioClip playingClip;
    // Viewport camera: RMB held = Unity-style fly (WASD/QE, Shift fast, wheel = speed).
    readonly HashSet<KeyCode> held = new HashSet<KeyCode>();
    bool flying, shift;
    float flySpeed = 2f;

    [SerializeField] string selected;
    [SerializeField] bool showList = true, showPreview = true, showOverall = true, showReference = true, paused;
    [SerializeField] string focus; // variant shown big; null = board
    [SerializeField] float loop = 3f;
    int focusFile;
    string lastFocusKey;
    bool focusAnimates, dragging, sheetDrawn;
    string drag;
    Vector2 dragStart;

    static GUIStyle wrapArea;
    static GUIStyle WrapArea => wrapArea ??= new GUIStyle(EditorStyles.textArea) { wordWrap = true };

    [MenuItem("Mirror Game/Mockup Viewer")]
    public static void Open()
    {
        var w = GetWindow<MockupViewerWindow>("Mockup Viewer");
        w.minSize = new Vector2(760, 480);
    }

    void OnEnable()
    {
        wantsMouseMove = true;
        t0 = EditorApplication.timeSinceStartup;
        EditorApplication.projectChanged += Reload;
        EditorApplication.update += Tick;
        Reload();
    }

    void OnDisable()
    {
        EditorApplication.projectChanged -= Reload;
        EditorApplication.update -= Tick;
        StopClips();
        ClearThumbs();
        stage?.Dispose();
        stage = null;
    }

    void OnFocus() => Reload();

    // Live prefab previews animate; everything else repaints on input only.
    void Tick()
    {
        double now = EditorApplication.timeSinceStartup;
        float dt = Mathf.Min((float)(now - lastTick), 0.1f);
        lastTick = now;
        if (flying && stage != null && held.Count > 0)
        {
            var move = new Vector3(Axis(KeyCode.D, KeyCode.A), Axis(KeyCode.E, KeyCode.Q), Axis(KeyCode.W, KeyCode.S));
            stage.Fly(move * (flySpeed * (shift ? 3f : 1f) * dt));
            Repaint();
        }
        if ((!paused && focusAnimates) || sheetDrawn) Repaint(); // sprite strips ignore the prefab pause
    }

    float Axis(KeyCode pos, KeyCode neg) => (held.Contains(pos) ? 1f : 0f) - (held.Contains(neg) ? 1f : 0f);

    Entry Current => entries.FirstOrDefault(e => e.folder == selected) ?? entries.FirstOrDefault();

    void Reload()
    {
        entries.Clear();
        if (Directory.Exists(Root))
            foreach (var dir in Directory.GetDirectories(Root).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
            {
                string folder = dir.Replace('\\', '/'), json = folder + "/mockup.json";
                if (!File.Exists(json)) continue;
                var data = JsonUtility.FromJson<Mockup>(File.ReadAllText(json));
                if (data == null) continue;
                entries.Add(new Entry { folder = folder, data = data, fb = LoadFeedback(folder, data) });
            }
        references.Clear();
        if (AssetDatabase.IsValidFolder(ReferenceFolder))
            references.AddRange(AssetDatabase.FindAssets("t:Prefab", new[] { ReferenceFolder })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p, StringComparer.OrdinalIgnoreCase));
        Repaint();
    }

    static Feedback LoadFeedback(string folder, Mockup data)
    {
        string path = folder + "/feedback.json";
        var fb = File.Exists(path) ? JsonUtility.FromJson<Feedback>(File.ReadAllText(path)) ?? new Feedback() : new Feedback();
        foreach (var v in data.variants ?? Array.Empty<Variant>())
            if (fb.variants.All(f => f.name != v.name)) fb.variants.Add(new VariantFeedback { name = v.name });
        return fb;
    }

    static void SaveFeedback(Entry e) => File.WriteAllText(e.folder + "/feedback.json", JsonUtility.ToJson(e.fb, true));

    // ── Window ─────────────────────────────────────────────────────────────────

    void OnGUI()
    {
        if (Event.current.type == EventType.MouseMove) Repaint(); // hover states
        if (Event.current.type == EventType.Repaint) sheetDrawn = false;
        float h = position.height;
        var side = new Rect(0f, 0f, SidebarWidth, h);
        var main = new Rect(SidebarWidth, 0f, position.width - SidebarWidth, h);
        EditorGUI.DrawRect(side, UI.PanelBg);
        EditorGUI.DrawRect(main, UI.CanvasBg);
        EditorGUI.DrawRect(new Rect(side.xMax - 1f, 0f, 1f, h), UI.Border);

        GUILayout.BeginArea(side);
        DrawSidebar();
        GUILayout.EndArea();

        GUILayout.BeginArea(main);
        var cur = Current;
        var shown = focus == null ? null : cur?.data.variants?.FirstOrDefault(v => v.name == focus);
        if (shown == null) focusAnimates = false;
        if (shown != null) DrawFocus(cur, shown, main.size);
        else if (cur != null) DrawBoard(cur, main.width);
        else
        {
            Header("MOCKUP", "Nichts zum Anschauen");
            GUILayout.Label($"Claude legt Mockups unter {Root}/{{Name}}/ ab (mockup.json + Dateien).", UI.Caption);
        }
        GUILayout.EndArea();
    }

    static void Header(string caption, string title, Color? accent = null)
    {
        Rect r = GUILayoutUtility.GetRect(1f, 54f, GUILayout.ExpandWidth(true));
        if (accent.HasValue) UI.Rounded(new Rect(r.x + 12f, r.y + 26f, 12f, 12f), accent.Value, 3f);
        float x = accent.HasValue ? 30f : 12f;
        GUI.Label(new Rect(r.x + 12f, r.y + 8f, r.width - 24f, 14f), caption, UI.Caption);
        GUI.Label(new Rect(r.x + x, r.y + 22f, r.width - x - 12f, 22f), title, UI.Title);
        EditorGUI.DrawRect(new Rect(r.x, r.yMax - 1f, r.width, 1f), UI.Border);
        GUILayout.Space(8f);
    }

    static Color StatusColor(Entry e) => e.fb.decided ? UI.Green : UI.Amber;

    // ── Sidebar ────────────────────────────────────────────────────────────────

    void DrawSidebar()
    {
        Header("MOCKUP VIEWER", "Mockups");
        using var scroll = new GUILayout.ScrollViewScope(sideScroll, GUIStyle.none, GUI.skin.verticalScrollbar);
        sideScroll = scroll.scrollPosition;

        showList = UI.Card("Mockups", entries.Count.ToString(), showList, () =>
        {
            if (entries.Count == 0) GUILayout.Label("Noch keine Mockups.", UI.Caption);
            foreach (var e in entries)
                if (UI.Tile(e.data.title ?? Path.GetFileName(e.folder), StatusColor(e), e == Current)) { selected = e.folder; focus = null; StopClips(); }
            GUILayout.Label("Gelb = offen, grün = entschieden.", UI.Caption);
            GUILayout.Space(4f);
            if (UI.Button("Aktualisieren", false, 24f)) { AssetDatabase.Refresh(); ClearThumbs(); Reload(); }
            var cur = Current;
            if (cur != null && UI.Button("Ausgewähltes löschen", false, 24f) &&
                EditorUtility.DisplayDialog("Mockup löschen", $"{cur.folder} komplett löschen?", "Löschen", "Abbrechen"))
            {
                StopClips();
                AssetDatabase.DeleteAsset(cur.folder);
                Reload();
                GUIUtility.ExitGUI();
            }
        });

        showPreview = UI.Card("Vorschau", null, showPreview, () =>
        {
            EditorGUI.BeginChangeCheck();
            var names = new[] { "Keiner" }.Concat(references.Select(Path.GetFileNameWithoutExtension)).ToArray();
            int cur = references.IndexOf(ReferencePath) + 1;
            int next = EditorGUILayout.Popup("Größenvergleich", cur, names);
            if (next != cur) EditorPrefs.SetString(ReferencePref, next == 0 ? "" : references[next - 1]);
            showReference = EditorGUILayout.Toggle("Avatar zeigen", showReference);
            if (Current?.data.loop > 0f) EditorGUILayout.LabelField("Loop (s)", $"{Loop:0.0}  (aus mockup.json)");
            else loop = EditorGUILayout.Slider("Loop (s)", loop, 0.5f, 10f);
            if (EditorGUI.EndChangeCheck()) ClearThumbs();
            GUILayout.Label("Großansicht: Rechtsklick halten + WASD/QE fliegen (Shift schnell, Rad = Tempo), Linksziehen = drehen, Mitte = schieben, Rad = zoomen, Doppelklick = zurück. Skripte laufen erst im Spiel: dafür \"Im Spiel spawnen\" im Play Mode.", UI.Caption);
        });
    }

    // ── Board: one column per rating, drag cards between them, click = focus ─────

    static Color RatingColor(string rating) => Ratings.FirstOrDefault(r => r.label == rating).color is var c && c.a > 0f ? c : UI.SubText;
    static VariantFeedback Fb(Entry e, Variant v) => e.fb.variants.First(f => f.name == v.name);
    static string FilePath(Entry e, string f) => f.StartsWith("Assets/") ? f : e.folder + "/" + f;
    string ReferencePath => EditorPrefs.GetString(ReferencePref, DefaultReference);
    string ShownReference => showReference ? ReferencePath : "";

    // Asset paths of a variant's images and prefabs (what fills a thumbnail / the focus view).
    static List<string> Visuals(Entry e, Variant v) => (v.files ?? Array.Empty<string>()).Select(f => FilePath(e, f))
        .Where(p => AssetDatabase.LoadMainAssetAtPath(p) is Texture2D || AssetDatabase.LoadMainAssetAtPath(p) is GameObject).ToList();

    void DrawBoard(Entry e, float width)
    {
        Header("MOCKUP", e.data.title ?? Path.GetFileName(e.folder), StatusColor(e));
        using var scroll = new GUILayout.ScrollViewScope(mainScroll, GUIStyle.none, GUI.skin.verticalScrollbar);
        mainScroll = scroll.scrollPosition;
        width -= 14f; // scrollbar

        var wrap = new GUIStyle(UI.Label) { wordWrap = true };
        GUILayout.BeginHorizontal();
        GUILayout.Space(12f);
        GUILayout.BeginVertical();
        if (!string.IsNullOrEmpty(e.data.description)) GUILayout.Label(e.data.description, wrap);
        if (!string.IsNullOrEmpty(e.data.question)) GUILayout.Label($"<b>{e.data.question}</b>", wrap);
        GUILayout.Label("Karte anklicken = groß ansehen + kommentieren, ziehen = bewerten.", UI.Caption);
        GUILayout.EndVertical();
        GUILayout.Space(12f);
        GUILayout.EndHorizontal();
        GUILayout.Space(8f);

        const float pad = 12f, gap = 8f, head = 32f;
        var vars = e.data.variants ?? Array.Empty<Variant>();
        var columns = new[] { "" }.Concat(Ratings.Select(r => r.label)).ToArray();
        float colW = (width - pad * 2f - gap * (columns.Length - 1)) / columns.Length;
        float cardW = colW - 12f, thumbH = cardW * 9f / 16f, cardH = thumbH + 46f;
        int most = Mathf.Max(1, columns.Max(c => vars.Count(v => Fb(e, v).rating == c || c == "" && !columns.Contains(Fb(e, v).rating))));
        Rect area = GUILayoutUtility.GetRect(width, head + most * (cardH + gap) + cardH * 0.5f);

        int id = GUIUtility.GetControlID(FocusType.Passive);
        var ev = Event.current;
        string dropOn = null;
        for (int i = 0; i < columns.Length; i++)
        {
            string rating = columns[i];
            Color color = RatingColor(rating);
            var col = new Rect(area.x + pad + i * (colW + gap), area.y, colW, area.height);
            bool drop = dragging && col.Contains(ev.mousePosition);
            if (drop) dropOn = rating;
            UI.Rounded(col, drop ? Color.Lerp(UI.PanelBg, color, 0.15f) : UI.PanelBg, 8f);
            UI.Outline(col, drop ? color : UI.Border, drop ? 1.5f : 1f, 8f);
            var h = new Rect(col.x + 10f, col.y, col.width - 20f, head);
            UI.Rounded(new Rect(h.x, h.center.y - 5f, 10f, 10f), color, 3f);
            GUI.Label(new Rect(h.x + 16f, h.y, h.width - 16f, h.height), (rating == "" ? "Offen" : rating).ToUpperInvariant(), UI.Header);

            float y = col.y + head;
            int count = 0;
            foreach (var v in vars)
            {
                string r = Fb(e, v).rating;
                if (r != rating && !(rating == "" && !columns.Contains(r))) continue;
                DrawCard(e, v, new Rect(col.x + 6f, y, cardW, cardH), thumbH, id);
                y += cardH + gap;
                count++;
            }
            UI.Pill(h, count.ToString(), UI.Border);
        }

        if (GUIUtility.hotControl == id)
        {
            if (ev.type == EventType.MouseDrag)
            {
                if ((ev.mousePosition - dragStart).sqrMagnitude > 25f) dragging = true;
                ev.Use();
                Repaint();
            }
            else if (ev.type == EventType.MouseUp)
            {
                GUIUtility.hotControl = 0;
                var v = vars.FirstOrDefault(x => x.name == drag);
                if (v != null && dragging && dropOn != null) { Fb(e, v).rating = dropOn; SaveFeedback(e); }
                else if (v != null && !dragging) { focus = v.name; focusFile = 0; }
                dragging = false;
                drag = null;
                ev.Use();
            }
        }
        if (dragging && ev.type == EventType.Repaint) // ghost under the cursor
        {
            var g = new Rect(ev.mousePosition.x - cardW * 0.5f, ev.mousePosition.y - 14f, cardW, 28f);
            UI.Rounded(g, new Color(UI.Accent.r, UI.Accent.g, UI.Accent.b, 0.85f));
            GUI.Label(new Rect(g.x + 10f, g.y, g.width - 20f, g.height), $"Variante {drag}", UI.Header);
        }

        EditorGUI.BeginChangeCheck();
        showOverall = UI.Card("Gesamt", null, showOverall, () =>
        {
            GUILayout.Label("Allgemeine Anmerkungen", UI.Caption);
            e.fb.overall = EditorGUILayout.TextArea(e.fb.overall, WrapArea, GUILayout.MinHeight(48f));
            GUILayout.Space(4f);
            if (UI.Button(e.fb.decided ? "Entschieden (wieder öffnen)" : "Entscheidung abschicken", !e.fb.decided, 28f, UI.Green))
                e.fb.decided = !e.fb.decided;
        });
        if (EditorGUI.EndChangeCheck()) SaveFeedback(e);
        GUILayout.Space(12f);
    }

    void DrawCard(Entry e, Variant v, Rect r, float thumbH, int boardId)
    {
        var ev = Event.current;
        var fb = Fb(e, v);
        var old = GUI.color;
        if (dragging && drag == v.name) GUI.color = new Color(1f, 1f, 1f, 0.35f);
        UI.Rounded(r, UI.IsHover(r) && !dragging ? UI.Hover : UI.CardBg);
        UI.Outline(r, UI.Border);

        var thumb = new Rect(r.x + 4f, r.y + 4f, r.width - 8f, thumbH - 4f);
        UI.Rounded(thumb, UI.CanvasBg, 4f);
        var visual = Visuals(e, v).FirstOrDefault();
        switch (visual != null ? AssetDatabase.LoadMainAssetAtPath(visual) : null)
        {
            case Texture2D tex: DrawImageFit(thumb, tex); break;
            case GameObject go:
                var t = PrefabThumb(visual, go);
                if (t != null) GUI.DrawTexture(thumb, t, ScaleMode.ScaleToFit, false);
                GUI.Label(new Rect(thumb.x + 6f, thumb.y + 4f, 60f, 14f), "3D", UI.Badge);
                break;
        }
        // Audio plays right on the card; stacked when there is no picture, small corner buttons otherwise.
        var clips = (v.files ?? Array.Empty<string>()).Select(f => AssetDatabase.LoadMainAssetAtPath(FilePath(e, f)) as AudioClip).Where(c => c != null).ToList();
        for (int i = 0; i < clips.Count; i++)
        {
            bool playing = playingClip == clips[i] && IsClipPlaying();
            var b = visual == null
                ? new Rect(thumb.x + 6f, thumb.y + 6f + i * 28f, thumb.width - 12f, 24f)
                : new Rect(thumb.x + 6f + i * 30f, thumb.yMax - 30f, 24f, 24f);
            if (b.yMax > thumb.yMax) break;
            if (UI.Button(b, visual == null ? $"{(playing ? "■" : "▶")}  {clips[i].name}" : playing ? "■" : "▶", playing, playing ? UI.Green : (Color?)null))
            {
                StopClips();
                if (!playing) { PlayClip(clips[i]); playingClip = clips[i]; }
            }
            if (playing) Repaint();
        }

        GUI.Label(new Rect(r.x + 10f, r.y + thumbH + 4f, r.width - 20f, 18f), $"Variante {v.name}", UI.Header);
        string note = !string.IsNullOrEmpty(fb.comment) ? "„" + fb.comment.Split('\n')[0] + "“" : v.description ?? "";
        GUI.Label(new Rect(r.x + 10f, r.y + thumbH + 22f, r.width - 20f, 18f), note, new GUIStyle(UI.Caption) { wordWrap = false, clipping = TextClipping.Clip });
        GUI.color = old;

        EditorGUIUtility.AddCursorRect(r, dragging ? MouseCursor.MoveArrow : MouseCursor.Link);
        if (ev.type == EventType.MouseDown && ev.button == 0 && r.Contains(ev.mousePosition))
        {
            GUIUtility.hotControl = boardId;
            drag = v.name;
            dragStart = ev.mousePosition;
            dragging = false;
            ev.Use();
        }
    }

    // ── Focus: one variant as big as the window allows, comment + rating on the right ─────

    void DrawFocus(Entry e, Variant v, Vector2 size)
    {
        var vars = e.data.variants;
        int idx = Array.IndexOf(vars, v), n = vars.Length;
        // IMGUI keeps a focused TextArea's buffer across variants: drop focus on switch, or the old comment bleeds over.
        string focusKey = e.folder + "/" + v.name;
        if (lastFocusKey != focusKey) { lastFocusKey = focusKey; GUIUtility.keyboardControl = 0; }
        var ev = Event.current;
        if (ev.type == EventType.KeyDown && !EditorGUIUtility.editingTextField && !flying)
        {
            if (ev.keyCode == KeyCode.Escape) { focus = null; ev.Use(); }
            else if (ev.keyCode == KeyCode.LeftArrow || ev.keyCode == KeyCode.RightArrow)
            {
                focus = vars[(idx + (ev.keyCode == KeyCode.RightArrow ? 1 : n - 1)) % n].name;
                focusFile = 0;
                ev.Use();
            }
        }

        const float bar = 48f, panelW = 320f;
        var fb = Fb(e, v);
        EditorGUI.DrawRect(new Rect(0f, bar - 1f, size.x, 1f), UI.Border);
        if (UI.Button(new Rect(10f, 10f, 104f, 28f), "← Board")) focus = null;
        UI.Rounded(new Rect(126f, 18f, 12f, 12f), RatingColor(fb.rating), 3f);
        GUI.Label(new Rect(146f, 6f, size.x - 300f, 16f), (e.data.title ?? Path.GetFileName(e.folder)).ToUpperInvariant(), UI.Caption);
        GUI.Label(new Rect(146f, 20f, size.x - 300f, 22f), $"Variante {v.name}", UI.Title);
        if (UI.Button(new Rect(size.x - 148f, 10f, 40f, 28f), "◀")) { focus = vars[(idx + n - 1) % n].name; focusFile = 0; }
        GUI.Label(new Rect(size.x - 106f, 10f, 52f, 28f), $"{idx + 1} / {n}", new GUIStyle(UI.Label) { alignment = TextAnchor.MiddleCenter });
        if (UI.Button(new Rect(size.x - 50f, 10f, 40f, 28f), "▶")) { focus = vars[(idx + 1) % n].name; focusFile = 0; }

        // Big view. Several pictures/prefabs in one variant: chips switch between them.
        var view = new Rect(10f, bar + 10f, size.x - panelW - 30f, size.y - bar - 20f);
        var visuals = Visuals(e, v);
        focusFile = Mathf.Clamp(focusFile, 0, Mathf.Max(0, visuals.Count - 1));
        if (visuals.Count > 1)
        {
            for (int i = 0; i < visuals.Count; i++)
                if (UI.Button(new Rect(view.x + i * 154f, view.y, 150f, 24f), Path.GetFileNameWithoutExtension(visuals[i]), i == focusFile)) focusFile = i;
            view.yMin += 32f;
        }
        focusAnimates = false;
        UI.Rounded(view, UI.PanelBg, 6f);
        GameObject shownPrefab = null;
        switch (visuals.Count > 0 ? AssetDatabase.LoadMainAssetAtPath(visuals[focusFile]) : null)
        {
            case Texture2D tex:
                DrawImageFit(view, tex);
                EditorGUIUtility.AddCursorRect(view, MouseCursor.Zoom);
                if (GUI.Button(view, new GUIContent("", "In Originalgröße öffnen"), GUIStyle.none)) EditorUtility.OpenWithDefaultApp(Path.GetFullPath(visuals[focusFile]));
                break;
            case GameObject go:
                shownPrefab = go;
                focusAnimates = true;
                DrawPrefabView(view, go);
                break;
            default:
                GUI.Label(view, "Kein Bild / Prefab in dieser Variante – Dateien rechts.", new GUIStyle(UI.Caption) { alignment = TextAnchor.MiddleCenter });
                break;
        }

        // Side panel
        GUILayout.BeginArea(new Rect(size.x - panelW - 10f, bar + 10f, panelW, size.y - bar - 20f));
        using (var scroll = new GUILayout.ScrollViewScope(panelScroll, GUIStyle.none, GUI.skin.verticalScrollbar))
        {
            panelScroll = scroll.scrollPosition;
            var wrap = new GUIStyle(UI.Label) { wordWrap = true };
            if (!string.IsNullOrEmpty(e.data.question)) GUILayout.Label($"<b>{e.data.question}</b>", wrap);
            if (!string.IsNullOrEmpty(v.description)) GUILayout.Label(v.description, wrap);
            GUILayout.Space(6f);
            foreach (var f in v.files ?? Array.Empty<string>())
            {
                string p = FilePath(e, f);
                if (!visuals.Contains(p)) DrawFile(p);
            }
            if (shownPrefab != null) DrawPlayback(shownPrefab);

            EditorGUI.BeginChangeCheck();
            GUILayout.Space(8f);
            GUILayout.Label("BEWERTUNG", UI.Header);
            GUILayout.BeginHorizontal();
            foreach (var (label, color) in Ratings)
                if (UI.Button(label, fb.rating == label, 28f, color)) fb.rating = fb.rating == label ? "" : label;
            GUILayout.EndHorizontal();
            GUILayout.Space(6f);
            GUILayout.Label("Was ist gut, was soll anders?", UI.Caption);
            fb.comment = EditorGUILayout.TextArea(fb.comment, WrapArea, GUILayout.MinHeight(140f));
            if (EditorGUI.EndChangeCheck()) SaveFeedback(e);
            GUILayout.Label("←/→ blättern · Esc = Board", UI.Caption);
        }
        GUILayout.EndArea();
    }

    void DrawFile(string path)
    {
        if (AssetDatabase.LoadMainAssetAtPath(path) is AudioClip clip) { DrawAudio(clip); return; }
        if (!File.Exists(path)) GUILayout.Label($"<color=#E5534B>Fehlt: {path}</color>", UI.Caption);
        else if (UI.Tile($"{Path.GetFileName(path)}  <size=10>(extern öffnen)</size>", UI.SubText, false))
            EditorUtility.OpenWithDefaultApp(Path.GetFullPath(path));
    }

    // Fit into r; upscaled pixel art stays crisp, renders and photos stay smooth (in-memory only).
    // A "*_anim" PNG that is a horizontal strip of square frames plays at 8 fps (sprite animation previews).
    void DrawImageFit(Rect r, Texture2D tex)
    {
        int frames = tex.name.EndsWith("_anim") && tex.width > tex.height && tex.width % tex.height == 0 ? tex.width / tex.height : 1;
        float scale = Mathf.Min(r.width * frames / tex.width, r.height / tex.height);
        tex.filterMode = scale >= 1.5f ? FilterMode.Point : FilterMode.Bilinear;
        if (frames == 1) { GUI.DrawTexture(r, tex, ScaleMode.ScaleToFit); return; }
        sheetDrawn = true;
        int f = (int)(EditorApplication.timeSinceStartup * 8.0) % frames;
        float w = tex.height * scale;
        var dst = new Rect(r.center.x - w * 0.5f, r.center.y - w * 0.5f, w, w);
        GUI.DrawTextureWithTexCoords(dst, tex, new Rect((float)f / frames, 0f, 1f / frames, 1f));
    }

    void DrawAudio(AudioClip clip)
    {
        bool playing = playingClip == clip && IsClipPlaying();
        if (UI.Tile($"{(playing ? "■" : "▶")}  {clip.name}  <size=10>{clip.length:0.0} s</size>", playing ? UI.Green : UI.Accent, playing))
        {
            StopClips();
            if (!playing) { PlayClip(clip); playingClip = clip; }
        }
        if (playing) Repaint(); // flips back to ▶ when the clip ends
    }

    float Loop => Current?.data.loop > 0f ? Current.data.loop : loop;
    float AnimTime => paused ? pausedAt : (float)((EditorApplication.timeSinceStartup - t0) % Loop);

    // Static board thumbnail (default camera, mid-loop), rendered once per prefab.
    Texture PrefabThumb(string path, GameObject prefab)
    {
        if (thumbs.TryGetValue(path, out var tex) && tex != null) return tex;
        if (Event.current.type != EventType.Repaint) return null;
        stage ??= new Stage();
        stage.SetReference(ShownReference);
        return thumbs[path] = stage.RenderStatic(new Rect(0f, 0f, 480f, 270f), prefab, Loop * 0.5f, freeCam: false);
    }

    void ClearThumbs()
    {
        foreach (var t in thumbs.Values) if (t != null) DestroyImmediate(t);
        thumbs.Clear();
    }

    void DrawPrefabView(Rect r, GameObject prefab)
    {
        if (Event.current.type == EventType.Repaint)
        {
            stage ??= new Stage();
            stage.SetReference(ShownReference);
            GUI.DrawTexture(r, stage.Render(r, prefab, AnimTime), ScaleMode.StretchToFill, false);
        }
        ViewportInput(r);
        var hint = flying ? $"Fliegen · Tempo {flySpeed:0.#}" : "RMB + WASD fliegen · LMB drehen · MMB schieben · Doppelklick = Startansicht";
        GUI.Label(new Rect(r.x + 8f, r.yMax - 20f, r.width - 16f, 16f), hint, UI.Caption);
    }

    void DrawPlayback(GameObject prefab)
    {
        GUILayout.Space(4f);
        GUILayout.BeginHorizontal();
        if (UI.Button(paused ? "▶ Abspielen" : "❚❚ Pause", false, 24f))
        {
            if (!paused) pausedAt = AnimTime;
            else t0 = EditorApplication.timeSinceStartup - pausedAt;
            paused = !paused;
        }
        if (UI.Button("Neu starten", false, 24f)) { t0 = EditorApplication.timeSinceStartup; pausedAt = 0f; }
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
            if (UI.Button("Im Spiel spawnen", EditorApplication.isPlaying, 24f)) SpawnInGame(prefab);
        if (spawned.Count > 0 && UI.Button($"Spawns weg ({spawned.Count})", false, 24f))
        {
            foreach (var go in spawned) if (go) Destroy(go);
            spawned.Clear();
        }
        GUILayout.EndHorizontal();
    }

    // Scene-view controls: LMB orbit, RMB look + WASD/QE fly, MMB pan, wheel zoom (fly speed while RMB), double click reset.
    void ViewportInput(Rect r)
    {
        int id = GUIUtility.GetControlID(FocusType.Passive);
        var e = Event.current;
        if (stage == null) return;
        bool hot = GUIUtility.hotControl == id;
        if (hot || r.Contains(e.mousePosition))
            EditorGUIUtility.AddCursorRect(r, flying ? MouseCursor.FPS : MouseCursor.Orbit);
        switch (e.type)
        {
            case EventType.MouseDown when r.Contains(e.mousePosition):
                if (e.button == 0 && e.clickCount == 2) stage.ResetView();
                GUIUtility.hotControl = id;
                GUIUtility.keyboardControl = 0; // no text field eats WASD
                flying = e.button == 1;
                held.Clear();
                e.Use();
                break;
            case EventType.MouseDrag when hot:
                shift = e.shift;
                if (e.button == 0) stage.Orbit(e.delta.x * 0.4f, e.delta.y * 0.4f);
                else if (e.button == 1) stage.Look(e.delta.x * 0.25f, e.delta.y * 0.25f);
                else stage.Pan(e.delta.x, e.delta.y, r.height);
                e.Use();
                Repaint();
                break;
            case EventType.MouseUp when hot:
                GUIUtility.hotControl = 0;
                flying = false;
                held.Clear();
                e.Use();
                break;
            case EventType.ScrollWheel when hot || r.Contains(e.mousePosition):
                if (flying) flySpeed = Mathf.Clamp(flySpeed * (e.delta.y < 0f ? 1.2f : 1f / 1.2f), 0.2f, 50f);
                else stage.Zoom(e.delta.y);
                e.Use();
                Repaint();
                break;
            case EventType.KeyDown when flying && hot:
                shift = e.shift;
                if (e.keyCode != KeyCode.None) held.Add(e.keyCode);
                e.Use();
                break;
            case EventType.KeyUp when flying && hot:
                shift = e.shift;
                held.Remove(e.keyCode);
                e.Use();
                break;
        }
    }

    // On the floor 2 m in front of the running game's camera, facing it.
    void SpawnInGame(GameObject prefab)
    {
        var cam = Camera.main;
        if (cam == null) return;
        var forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
        forward.Normalize();
        var pos = cam.transform.position + forward * 2f;
        pos.y = prefab.transform.position.y;
        spawned.RemoveAll(g => !g);
        spawned.Add(Instantiate(prefab, pos, Quaternion.LookRotation(-forward) * prefab.transform.rotation));
    }

    // ── Audio (raw clip, editor preview API) ────────────────────────────────────

    static readonly Type AudioUtil = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
    static MethodInfo AudioMethod(string name, params Type[] args)
        => AudioUtil?.GetMethod(name, BindingFlags.Static | BindingFlags.Public, null, args, null);

    static void PlayClip(AudioClip c)
        => AudioMethod("PlayPreviewClip", typeof(AudioClip), typeof(int), typeof(bool))?.Invoke(null, new object[] { c, 0, false });
    static void StopClips() => AudioMethod("StopAllPreviewClips")?.Invoke(null, null);
    static bool IsClipPlaying() => AudioMethod("IsPreviewClipPlaying")?.Invoke(null, null) is true;

    // ── Claude: render a prefab mockup to a PNG (to check it before showing it) ─────

    // Same image the window shows. Call via `unity cmd eval`. reference "" = no avatar next to it.
    public static string RenderPng(string prefabPath, float time, string outPng, string reference = DefaultReference, int w = 1280, int h = 720)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) return "Prefab fehlt: " + prefabPath;
        var stage = new Stage();
        try
        {
            stage.SetReference(reference);
            var tex = stage.RenderStatic(new Rect(0, 0, w, h), prefab, time);
            File.WriteAllBytes(outPng, tex.EncodeToPNG());
            DestroyImmediate(tex);
            return outPng;
        }
        finally { stage.Dispose(); }
    }

    // ── Preview stage: neutral floor + optional avatar for size + one instance per prefab ─────

    sealed class Stage : IDisposable
    {
        const float Fov = 35f, ViewPitch = 12f, ViewYaw = 205f; // slightly from above, 3/4 front view (avatars face +z)

        readonly PreviewRenderUtility pru = new PreviewRenderUtility();
        readonly Dictionary<GameObject, GameObject> instances = new Dictionary<GameObject, GameObject>();
        readonly Dictionary<GameObject, (Vector3 target, float distance)> framing = new Dictionary<GameObject, (Vector3, float)>();
        readonly Material floorMaterial;
        GameObject reference;
        string referencePath;
        // Free camera: off = auto framing; taken over on the first user input, world-fixed afterwards.
        bool free;
        Vector3 camPos;
        float camYaw, camPitch, pivotDist = 4f, lastDistance = 4f;

        Quaternion CamRot => Quaternion.Euler(camPitch, camYaw, 0f);
        Vector3 Pivot => camPos + CamRot * Vector3.forward * pivotDist;

        public Stage()
        {
            var cam = pru.camera;
            cam.nearClipPlane = 0.02f;
            cam.farClipPlane = 200f;
            cam.fieldOfView = Fov;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = UI.Hex(0x3A3D42);
            pru.ambientColor = new Color(0.5f, 0.5f, 0.55f);
            pru.lights[0].intensity = 1.2f;
            pru.lights[0].transform.rotation = Quaternion.Euler(50f, 150f, 0f);
            pru.lights[1].intensity = 0.5f;

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            floorMaterial = new Material(shader) { color = UI.Hex(0x8A8D92), hideFlags = HideFlags.HideAndDontSave };
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane); // 10 x 10 m
            floor.name = "MockupFloor";
            floor.hideFlags = HideFlags.HideAndDontSave;
            Object.DestroyImmediate(floor.GetComponent<Collider>());
            floor.transform.localScale = new Vector3(2f, 1f, 2f);
            floor.GetComponent<MeshRenderer>().sharedMaterial = floorMaterial;
            pru.AddSingleGO(floor);
        }

        void TakeOver()
        {
            if (free) return;
            var tf = pru.camera.transform;
            camPos = tf.position;
            camYaw = tf.eulerAngles.y;
            camPitch = Mathf.DeltaAngle(0f, tf.eulerAngles.x);
            pivotDist = lastDistance;
            free = true;
        }

        public void Orbit(float yaw, float pitch)
        {
            TakeOver();
            var pivot = Pivot;
            camYaw += yaw;
            camPitch = Mathf.Clamp(camPitch + pitch, -89f, 89f);
            camPos = pivot - CamRot * Vector3.forward * pivotDist;
        }

        public void Look(float yaw, float pitch)
        {
            TakeOver();
            camYaw += yaw;
            camPitch = Mathf.Clamp(camPitch + pitch, -89f, 89f);
        }

        public void Fly(Vector3 local) { TakeOver(); camPos += CamRot * local; }

        public void Pan(float dx, float dy, float viewHeight)
        {
            TakeOver();
            // World units per pixel at the pivot distance, so the pivot sticks to the mouse.
            float k = 2f * pivotDist * Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad) / Mathf.Max(1f, viewHeight);
            camPos += CamRot * new Vector3(-dx * k, dy * k, 0f);
        }

        public void Zoom(float d)
        {
            TakeOver();
            float step = pivotDist * d * 0.05f;
            camPos -= CamRot * Vector3.forward * step;
            pivotDist = Mathf.Max(0.1f, pivotDist + step);
        }

        public void ResetView() => free = false;

        // Avatar prefab shown next to the mockup for size; "" = none.
        public void SetReference(string path)
        {
            if (path == referencePath) return;
            referencePath = path;
            framing.Clear();
            free = false;
            if (reference != null) Object.DestroyImmediate(reference);
            var prefab = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
            reference = prefab != null ? pru.InstantiatePrefabInScene(prefab) : null;
        }

        void Prepare(GameObject prefab, float t, bool freeCam)
        {
            if (!instances.TryGetValue(prefab, out var inst) || inst == null)
            {
                instances[prefab] = inst = pru.InstantiatePrefabInScene(prefab);
                // Fixed seed: Simulate(restart) every frame would otherwise re-roll every particle each repaint (flicker).
                foreach (var ps in inst.GetComponentsInChildren<ParticleSystem>(true))
                {
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    ps.useAutoRandomSeed = false;
                }
                framing.Remove(prefab);
            }
            foreach (var kv in instances) if (kv.Value != null) kv.Value.SetActive(kv.Key == prefab);
            inst.transform.SetPositionAndRotation(Vector3.up * prefab.transform.position.y, prefab.transform.rotation);
            Sample(inst, t);

            var rot = Quaternion.Euler(ViewPitch, ViewYaw, 0f);
            // Framing is measured once per prefab (at the first sample), so animation does not shake the camera.
            if (!framing.TryGetValue(prefab, out var frame))
            {
                var b = Bounds(inst);
                if (reference != null)
                {
                    var right = Vector3.ProjectOnPlane(rot * Vector3.right, Vector3.up).normalized;
                    float side = Mathf.Max(b.extents.x, b.extents.z) + 0.5f;
                    reference.transform.SetPositionAndRotation(new Vector3(b.center.x, 0f, b.center.z) + right * side, Quaternion.identity);
                    Sample(reference, 0f);
                    b.Encapsulate(Bounds(reference));
                }
                float radius = Mathf.Max(0.3f, b.extents.magnitude);
                frame = (b.center, radius / Mathf.Sin(Fov * 0.5f * Mathf.Deg2Rad) * 1.05f);
                framing[prefab] = frame;
            }
            if (reference != null) Sample(reference, t);
            lastDistance = frame.distance;

            var cam = pru.camera;
            if (free && freeCam) cam.transform.SetPositionAndRotation(camPos, CamRot);
            else cam.transform.SetPositionAndRotation(frame.target - rot * Vector3.forward * frame.distance, rot);

            // Script-driven mockups (no Play mode in the preview): any component with
            // public void MockupSample(float t, Camera cam) poses itself for time t.
            foreach (var mb in inst.GetComponentsInChildren<MonoBehaviour>(true))
                mb?.GetType().GetMethod("MockupSample", new[] { typeof(float), typeof(Camera) })?.Invoke(mb, new object[] { t, cam });
        }

        // ponytail: restart + simulate to t every frame, cost grows with the loop length; fine for short loops.
        static void Sample(GameObject go, float t)
        {
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
                if (ps.transform.parent == null || ps.transform.parent.GetComponentInParent<ParticleSystem>(true) == null)
                    ps.Simulate(t, true, true, true);
            foreach (var a in go.GetComponentsInChildren<Animator>(true))
            {
                a.cullingMode = AnimatorCullingMode.AlwaysAnimate; // culled animators (e.g. the NPC) never update in a preview scene
                a.Rebind();
                a.Update(t);
            }
        }

        static Bounds Bounds(GameObject go)
        {
            Bounds? b = null;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer || !r.enabled) continue;
                if (b == null) b = r.bounds;
                else { var x = b.Value; x.Encapsulate(r.bounds); b = x; }
            }
            return b ?? new Bounds(go.transform.position + Vector3.up, Vector3.one * 2f);
        }

        public Texture Render(Rect r, GameObject prefab, float t)
        {
            Prepare(prefab, t, freeCam: true);
            pru.BeginPreview(r, GUIStyle.none);
            pru.Render(true);
            return pru.EndPreview();
        }

        public Texture2D RenderStatic(Rect r, GameObject prefab, float t, bool freeCam = true)
        {
            Prepare(prefab, t, freeCam);
            pru.BeginStaticPreview(r);
            pru.Render(true);
            return pru.EndStaticPreview();
        }

        public void Dispose()
        {
            Object.DestroyImmediate(floorMaterial);
            pru.Cleanup();
        }
    }
}

// Mockup images import uncompressed and full size, so what is shown is what was rendered.
class MockupTextureImport : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(MockupViewerWindow.Root + "/")) return;
        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Default;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.npotScale = TextureImporterNPOTScale.None;
        ti.maxTextureSize = 8192;
        ti.isReadable = false;
    }
}
