using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

// Builds the three UI styles (Pixel, Skizze, Holz) from the prototypes in the Mockup Viewer:
// imports the generated sprites (Tools/ui_sprites.py) and sounds (Tools/ui_sounds.py), creates TMP font assets
// from the OFL fonts, writes Data/UiStyles/<Style>.asset and adds the skin keys, decorations and new elements to Main.unity.
// Safe to rerun: existing objects are found by name and updated.
public static class UiStyleBuilder
{
    const string SpriteDir = "Assets/Art/UI/Styles";
    const string FontDir = "Assets/Art/UI/Fonts";
    const string StyleDir = "Assets/Data/UiStyles";
    const string SoundDir = "Assets/Art/UI/Sounds";
    const string Charset = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~ÄÖÜäöüßàâçéèêëîïôûùÀÉÈ„“”‚‘’–—…€°·×";
    const float Px3 = 1f / 3f;

    [MenuItem("Mirror Game/Build UI Styles")]
    public static void Build()
    {
        AssetDatabase.Refresh();
        ImportSprites();
        var styles = new[] { Pixel(), Skizze(), Holz() };
        BuildScene(styles);
        AssetDatabase.SaveAssets();
        Debug.Log("UI styles built: " + string.Join(", ", System.Array.ConvertAll(styles, s => s.displayName)));
    }

    // ------------------------------------------------------------------ assets

    [System.Serializable] class SpriteMeta { public string path; public int[] border; public bool point; }
    [System.Serializable] class SpriteList { public SpriteMeta[] sprites; }

    static void ImportSprites()
    {
        var list = JsonUtility.FromJson<SpriteList>(File.ReadAllText(SpriteDir + "/sprites.json"));
        foreach (var meta in list.sprites)
        {
            string path = meta.path;
            var importer = (TextureImporter)AssetImporter.GetAtPath(SpriteDir + "/" + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = new Vector4(meta.border[0], meta.border[1], meta.border[2], meta.border[3]);
            importer.filterMode = meta.point ? FilterMode.Point : FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 1024;
            importer.textureCompression = meta.point ? TextureImporterCompression.Uncompressed : TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
        }
    }

    static Sprite Sp(string style, string name)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteDir}/{style}/{name}.png");
        if (sprite == null)
            Debug.LogError($"Missing sprite {style}/{name}");
        return sprite;
    }

    static Sprite[] One(string style, string name) => new[] { Sp(style, name) };
    static Sprite[] Frames(string style, string name, int count = 3)
    {
        var frames = new Sprite[count];
        for (int i = 0; i < count; i++)
            frames[i] = Sp(style, $"{name}_{i}");
        return frames;
    }

    static TMP_FontAsset Font(string file)
    {
        string path = $"{FontDir}/{file} SDF.asset";
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (existing != null)
            return existing;
        var font = AssetDatabase.LoadAssetAtPath<Font>($"{FontDir}/{file}.ttf");
        var asset = TMP_FontAsset.CreateFontAsset(font, 72, 8, GlyphRenderMode.SDFAA, 1024, 1024);
        asset.name = file + " SDF";
        AssetDatabase.CreateAsset(asset, path);
        asset.atlasTextures[0].name = file + " Atlas";
        AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
        asset.material.name = file + " Material";
        AssetDatabase.AddObjectToAsset(asset.material, asset);
        asset.TryAddCharacters(Charset, out _);
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        return asset;
    }

    // Font material with an outline and/or a hard drop shadow (TMP underlay).
    static Material FontMaterial(TMP_FontAsset font, string suffix, Color outline, float outlineWidth, Color shadow, float shadowOffset)
    {
        string path = $"{FontDir}/{font.name} {suffix}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(font.material);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.CopyPropertiesFromMaterial(font.material);
        if (outlineWidth > 0)
        {
            mat.EnableKeyword("OUTLINE_ON");
            mat.SetColor("_OutlineColor", outline);
            mat.SetFloat("_OutlineWidth", outlineWidth);
            mat.SetFloat("_FaceDilate", outlineWidth * 0.8f);
        }
        if (shadowOffset != 0)
        {
            mat.EnableKeyword("UNDERLAY_ON");
            mat.SetColor("_UnderlayColor", shadow);
            mat.SetFloat("_UnderlayOffsetY", -shadowOffset);
            mat.SetFloat("_UnderlaySoftness", 0.05f);
            mat.SetFloat("_UnderlayDilate", outlineWidth > 0 ? 0.6f : 0.2f);
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static UiStyle SaveStyle(string name, TMP_FontAsset font, TMP_FontAsset title, float cps, List<UiLook> looks)
    {
        Directory.CreateDirectory(StyleDir);
        string path = $"{StyleDir}/{name}.asset";
        var style = AssetDatabase.LoadAssetAtPath<UiStyle>(path);
        if (style == null)
        {
            style = ScriptableObject.CreateInstance<UiStyle>();
            AssetDatabase.CreateAsset(style, path);
        }
        style.displayName = name;
        style.font = font;
        style.titleFont = title;
        style.charsPerSecond = cps;
        style.looks = looks.ToArray();
        style.sounds = LoadSounds(name);
        EditorUtility.SetDirty(style);
        return style;
    }

    // All WAVs in Art/UI/Sounds/<Style> (Tools/ui_sounds.py), named after the file. Short clips: decompressed on load.
    static UiSound[] LoadSounds(string style)
    {
        var sounds = new List<UiSound>();
        string dir = $"{SoundDir}/{style}";
        if (!Directory.Exists(dir))
            return sounds.ToArray();
        foreach (var file in Directory.GetFiles(dir, "*.wav"))
        {
            string path = file.Replace("\\", "/");
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.7f;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = true;
            importer.SaveAndReimport();
            sounds.Add(new UiSound { name = Path.GetFileNameWithoutExtension(path), clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path) });
        }
        return sounds.ToArray();
    }

    static Color C(string hex, float a = 1)
    {
        ColorUtility.TryParseHtmlString(hex, out var c);
        c.a = a;
        return c;
    }

    // ------------------------------------------------------------------ look helpers

    static UiLook Img(string key, Sprite[] sprites, Image.Type type = Image.Type.Sliced, float ppu = 1, float fps = 8) =>
        new UiLook { key = key, sprites = sprites, imageType = type, pixelsPerUnit = ppu, framesPerSecond = fps };

    static UiLook Tint(string key, Color color) => new UiLook { key = key, color = color };

    static UiLook Txt(string key, float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.Center,
        bool title = false, Material material = null, FontStyles style = FontStyles.Normal, float lineSpacing = 0) =>
        new UiLook { key = key, fontSize = size, textColor = color, alignment = align, titleFont = title, fontMaterial = material, fontStyle = style, lineSpacing = lineSpacing };

    static UiLook Tinted(this UiLook l, Color c) { l.color = c; return l; }
    static UiLook Aspect(this UiLook l) { l.preserveAspect = true; return l; }
    static UiLook Rot(this UiLook l, float deg) { l.rotation = deg; return l; }
    static UiLook Size(this UiLook l, float w, float h) { l.rectMode = UiRectMode.Size; l.size = new Vector2(w, h); return l; }

    static UiLook Rect(this UiLook l, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
    {
        l.rectMode = UiRectMode.Rect;
        l.anchorMin = aMin; l.anchorMax = aMax; l.offsetMin = oMin; l.offsetMax = oMax;
        return l;
    }

    // Stretched over the parent with insets (positive = inwards).
    static UiLook Inset(this UiLook l, float left, float bottom, float right, float top) =>
        l.Rect(Vector2.zero, Vector2.one, new Vector2(left, bottom), new Vector2(-right, -top));

    // Fixed size at the left edge of the parent, vertically centred (dy moves it up).
    static UiLook AtLeft(this UiLook l, float x, float w, float h, float dy = 0) =>
        l.Rect(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(x, -h / 2 + dy), new Vector2(x + w, h / 2 + dy));

    // Fixed size above the top centre of the parent.
    static UiLook AboveTop(this UiLook l, float w, float h, float gap) =>
        l.Rect(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-w / 2, gap), new Vector2(w / 2, gap + h));

    static UiLook Offsets(this UiLook l, Vector2 oMin, Vector2 oMax)
    {
        l.rectMode = UiRectMode.Offsets;
        l.offsetMin = oMin; l.offsetMax = oMax;
        return l;
    }

    static UiLook M(this UiLook l, UiMotion enter = UiMotion.None, UiMotion exit = UiMotion.None, UiMotion swap = UiMotion.None,
        UiMotion idle = UiMotion.None, UiMotion want = UiMotion.None, UiMotion press = UiMotion.None, UiMotion pulse = UiMotion.None, float idleDuration = 0)
    {
        l.enter = enter; l.exit = exit; l.swap = swap; l.idle = idle; l.wantIdle = want; l.press = press; l.pulse = pulse; l.idleDuration = idleDuration;
        return l;
    }

    // ------------------------------------------------------------------ Pixel

    static UiStyle Pixel()
    {
        const string S = "Pixel";
        var font = Font("PressStart2P-Regular");
        var outline = FontMaterial(font, "Outline", C("#1a1833"), 0.25f, C("#000000", 0.35f), 0.5f);
        Color white = UnityEngine.Color.white, yel = C("#ffd23f"), ink = C("#1a1833"), dim = C("#8a88b8");
        var box = One(S, "box");
        var ring = new[] { Sp(S, "ring_0"), Sp(S, "ring_1") };
        var L = TextAlignmentOptions.TopLeft;

        var looks = new List<UiLook>
        {
            Img("Panel", box, ppu: Px3).M(UiMotion.PixelGrow, UiMotion.PixelShrink, UiMotion.PixelFlicker),
            Txt("Prompt.Text", 27, white, L, lineSpacing: 45).Inset(54, 180, 54, 44),
            Txt("Split.Text", 27, white, L, lineSpacing: 45).Rect(new Vector2(0, 1), Vector2.one, new Vector2(54, -200), new Vector2(-54, -40)),
            Txt("Split.Shares", 30, yel),
            Img("Button", One(S, "btn"), ppu: Px3).Size(340, 111).M(UiMotion.PixelPop, press: UiMotion.PixelPress),
            Txt("Button.Text", 24, ink).Inset(0, 15, 0, 0),
            Img("Panel.Speaker", One(S, "btn_yel"), ppu: 0.25f).Size(250, 64),
            Txt("Panel.Speaker.Text", 18, ink).Inset(0, 12, 0, 0),
            Img("Loader.Dot", new[] { Sp(S, "dot_0"), Sp(S, "dot_1"), Sp(S, "dot_1") }, Image.Type.Simple, fps: 3.33f),

            Img("Talk", box, ppu: Px3).Size(460, 115).M(UiMotion.PixelPop, UiMotion.PixelOut, press: UiMotion.PixelPress),
            Txt("Talk.Text", 28, white).Inset(100, 15, 24, 0),
            Img("Talk.Icon", One(S, "talk"), Image.Type.Simple).Tinted(yel).Aspect().AtLeft(34, 56, 33, 7).M(idle: UiMotion.Bob, idleDuration: 0.6f),

            Img("Instr", box, ppu: Px3).M(UiMotion.PixelPop, UiMotion.PixelOut, pulse: UiMotion.PixelPop),
            Txt("Instr.Text", 30, white, TextAlignmentOptions.MidlineLeft, lineSpacing: 30),
            Txt("Instr.Count", 30, yel).M(pulse: UiMotion.PixelBump),
            Img("Instr.Tri", One(S, "tri"), Image.Type.Simple).Tinted(yel).Aspect().AtLeft(22, 16, 28, 7).M(idle: UiMotion.Blink),

            Img("Arrow", box, ppu: Px3).Size(140, 155).M(UiMotion.PixelPop, UiMotion.PixelOut, press: UiMotion.PixelPress),
            Img("Arrow.Icon", One(S, "arrow"), Image.Type.Simple).Tinted(yel).Aspect().Inset(28, 43, 28, 28).M(want: UiMotion.Bob),
            Img("Arrow.Want", ring, ppu: Px3, fps: 4).Inset(-12, 3, -12, -12),
            Img("Jump", box, ppu: Px3).Size(360, 115).M(UiMotion.PixelPop, UiMotion.PixelOut, press: UiMotion.PixelPress),
            Txt("Jump.Text", 26, white).Inset(90, 15, 24, 0),
            Img("Jump.Icon", One(S, "up"), Image.Type.Simple).Tinted(yel).Aspect().AtLeft(36, 40, 40, 7).M(want: UiMotion.Bob),
            Img("Jump.Want", ring, ppu: Px3, fps: 4).Inset(-12, 3, -12, -12),

            Img("Step", box, ppu: Px3).Size(110, 125).M(press: UiMotion.PixelPress),
            Txt("Step.Text", 40, yel).Inset(0, 15, 0, 0),
            Img("Track", One(S, "block_off"), Image.Type.Tiled).Rect(new Vector2(0, 0.25f), new Vector2(1, 0.75f), Vector2.zero, Vector2.zero),
            new UiLook { key = "FillArea" }.Rect(new Vector2(0, 0.25f), new Vector2(1, 0.75f), Vector2.zero, Vector2.zero),
            Img("Fill", One(S, "block_on"), Image.Type.Tiled).Offsets(Vector2.zero, Vector2.zero),
            Tint("Handle", UnityEngine.Color.clear),

            Img("Balance", box, ppu: Px3).Size(330, 118).M(UiMotion.PixelPop, pulse: UiMotion.PixelFlicker),
            Img("Balance.Icon", One(S, "coin"), Image.Type.Simple).Aspect().AtLeft(22, 56, 56, 7).M(idle: UiMotion.Spin),
            Txt("Balance.Caption", 16, dim, TextAlignmentOptions.BottomLeft).Rect(new Vector2(0, 0.5f), Vector2.one, new Vector2(96, 10), new Vector2(-16, -14)),
            Txt("Balance.Value", 28, yel, TextAlignmentOptions.TopLeft).Rect(Vector2.zero, new Vector2(1, 0.5f), new Vector2(96, 18), new Vector2(-16, -2)),
            Txt("Balance.Gain", 30, yel, TextAlignmentOptions.Right, material: outline).M(UiMotion.PixelRise),
            Img("Round", box, ppu: Px3).Size(330, 88).M(UiMotion.PixelPop, pulse: UiMotion.PixelPop),
            Txt("Round.Text", 20, yel).Inset(0, 15, 0, 0),

            Tint("Done", UnityEngine.Color.clear).M(exit: UiMotion.PixelOut),
            Img("Done.Icon", One(S, "star"), Image.Type.Simple).Aspect().M(UiMotion.PixelPop, idle: UiMotion.Tilt),
            Txt("Done.Text", 64, yel, material: outline).M(UiMotion.PixelPop),

            Img("Door.Outline", ring, ppu: 1f / 20, fps: 3.33f).Inset(-80, -80, -80, -80).M(UiMotion.PixelPop),
            Img("Door.Tag", One(S, "btn_yel"), ppu: 1f / 12).Size(1000, 300).M(UiMotion.PixelPop),
            Txt("Door.Text", 100, ink).Inset(0, 56, 0, 0),
            Img("Door.Pointer", One(S, "down"), Image.Type.Simple).Tinted(yel).Aspect().AboveTop(160, 160, 30).M(idle: UiMotion.Bob),
        };
        return SaveStyle(S, font, font, 45, looks);
    }

    // ------------------------------------------------------------------ Skizze

    static UiStyle Skizze()
    {
        const string S = "Skizze";
        var font = Font("PatrickHand-Regular");
        Color ink = C("#26221e"), red = C("#e2463b");
        var paper = Frames(S, "paper");
        var circle = Frames(S, "circle");
        const float boil = 8.3f;
        var L = TextAlignmentOptions.TopLeft;

        var looks = new List<UiLook>
        {
            Img("Panel", paper, fps: boil).Rot(-0.5f).M(UiMotion.PaperIn, UiMotion.PaperOut, UiMotion.Shuffle),
            Img("Panel.Lines", One(S, "lines"), Image.Type.Tiled).Inset(10, 16, 16, 34),
            Tint("Panel.Margin", C("#e2463b", 0.6f)).Rect(Vector2.zero, new Vector2(0, 1), new Vector2(96, 16), new Vector2(99, -8)),
            Img("Panel.Tape", One(S, "tape"), Image.Type.Simple).AboveTop(180, 42, -22).Rot(-2),
            Txt("Prompt.Text", 44, ink, L, lineSpacing: -4).Inset(130, 180, 60, 36),
            Txt("Split.Text", 44, ink, L, lineSpacing: -4).Rect(new Vector2(0, 1), Vector2.one, new Vector2(130, -200), new Vector2(-60, -34)),
            Txt("Split.Shares", 50, ink),
            Img("Button", paper, fps: boil).Size(320, 104).M(UiMotion.PopIn, press: UiMotion.Squish),
            Txt("Button.Text", 50, ink).Inset(0, 6, 8, 0),
            Img("Button.Hl", One(S, "hl")).Inset(70, 24, 78, 50),
            Img("Panel.Speaker", One(S, "tag")).Size(230, 62).Rot(-3),
            Txt("Panel.Speaker.Text", 36, red),
            Img("Loader.Dot", Frames(S, "odot"), Image.Type.Simple, fps: boil).M(idle: UiMotion.Hop, idleDuration: 0.9f),

            Img("Talk", paper, fps: boil).Size(460, 116).M(UiMotion.PopIn, UiMotion.PopOut, press: UiMotion.Squish, idle: UiMotion.Wob),
            Txt("Talk.Text", 56, ink).Inset(96, 10, 30, 0),
            Img("Talk.Icon", One(S, "dots"), Image.Type.Simple).Tinted(ink).Aspect().AtLeft(40, 60, 60, 4),

            Img("Instr", One(S, "note")).Rot(-1.5f).M(UiMotion.Slap, UiMotion.Tear, pulse: UiMotion.Slap),
            Txt("Instr.Text", 56, ink, TextAlignmentOptions.MidlineLeft),
            Txt("Instr.Count", 46, ink).M(pulse: UiMotion.InkBump),
            Img("Instr.Tape", One(S, "tape"), Image.Type.Simple).AboveTop(170, 42, -22).Rot(3),

            Img("Arrow", circle, Image.Type.Simple, fps: boil).Size(156, 156).M(UiMotion.PopIn, UiMotion.PopOut, press: UiMotion.Squish),
            Img("Arrow.Icon", One(S, "turn"), Image.Type.Simple).Tinted(ink).Aspect().Inset(28, 32, 32, 28).M(want: UiMotion.Wiggle),
            Img("Arrow.Want", Frames(S, "ring"), Image.Type.Simple, fps: boil).Inset(-26, -26, -26, -26),
            Img("Jump", paper, fps: boil).Size(340, 110).M(UiMotion.PopIn, UiMotion.PopOut, press: UiMotion.Squish),
            Txt("Jump.Text", 54, ink).Inset(84, 8, 24, 0),
            Img("Jump.Icon", One(S, "up"), Image.Type.Simple).Tinted(ink).Aspect().AtLeft(30, 58, 58, 4).M(want: UiMotion.Wiggle),
            Img("Jump.Want", Frames(S, "ringrect"), fps: boil).Inset(-22, -18, -16, -18),

            Img("Step", circle, Image.Type.Simple, fps: boil).Size(104, 104).M(press: UiMotion.Squish),
            Txt("Step.Text", 64, ink).Inset(0, 8, 0, 0),
            Img("Track", Frames(S, "rail"), fps: boil).Rect(new Vector2(0, 0.42f), new Vector2(1, 0.58f), Vector2.zero, Vector2.zero),
            new UiLook { key = "FillArea" }.Rect(new Vector2(0, 0.36f), new Vector2(1, 0.64f), Vector2.zero, Vector2.zero),
            Img("Fill", One(S, "hl")).Offsets(Vector2.zero, Vector2.zero),
            Img("Handle", circle, Image.Type.Simple, fps: boil).Offsets(new Vector2(-40, 18), new Vector2(40, -18)),

            Img("Balance", One(S, "blue")).Size(250, 140).Rot(2.5f).M(UiMotion.PopIn, pulse: UiMotion.InkBump),
            Txt("Balance.Caption", 28, ink).Rect(new Vector2(0, 0.55f), Vector2.one, new Vector2(10, 0), new Vector2(-14, -12)),
            Txt("Balance.Value", 56, ink).Rect(Vector2.zero, new Vector2(1, 0.62f), new Vector2(10, 18), new Vector2(-14, 0)),
            Txt("Balance.Gain", 54, red, TextAlignmentOptions.Right).M(UiMotion.InkRise),
            Img("Round", One(S, "tag")).Size(250, 70).Rot(-2).M(UiMotion.Slap, pulse: UiMotion.Slap),
            Txt("Round.Text", 40, ink),

            Img("Done", paper, fps: boil).Rot(-2).M(UiMotion.PopIn, UiMotion.PopOut),
            Img("Done.Icon", One(S, "check"), Image.Type.Simple).Aspect().M(UiMotion.PopIn),
            Txt("Done.Text", 86, ink),

            Img("Door.Outline", Frames(S, "ringrect"), ppu: 0.2f, fps: boil).Inset(-70, -70, -70, -70).M(UiMotion.PopIn),
            Img("Door.Tag", paper, ppu: 0.25f, fps: boil).Size(900, 330).M(UiMotion.PopIn),
            Txt("Door.Text", 200, ink).Inset(0, 30, 40, 0),
            Img("Door.Pointer", One(S, "down"), Image.Type.Simple).Tinted(red).Aspect().AboveTop(240, 240, 20).M(idle: UiMotion.Hop),
        };
        return SaveStyle(S, font, font, 42, looks);
    }

    // ------------------------------------------------------------------ Holz

    static UiStyle Holz()
    {
        const string S = "Holz";
        var body = Font("Alegreya-Medium");
        var title = Font("Cinzel-Bold");
        Color dark = C("#3b2611"), cream = C("#f6e7c4"), gold = C("#e0b45a"), red = C("#8e1f1f");
        var shadow = FontMaterial(title, "Shadow", dark, 0, dark, 0.6f);
        var outline = FontMaterial(title, "Outline", dark, 0.22f, C("#000000", 0.5f), 0.6f);
        var plank = One(S, "plank");
        var shield = One(S, "shield");
        var L = TextAlignmentOptions.TopLeft;

        var looks = new List<UiLook>
        {
            Img("Panel", One(S, "panel")).M(UiMotion.PanelUp, UiMotion.PanelDown, UiMotion.Rustle),
            Txt("Prompt.Text", 40, dark, L).Inset(70, 180, 70, 54),
            Txt("Split.Text", 40, dark, L).Rect(new Vector2(0, 1), Vector2.one, new Vector2(70, -200), new Vector2(-70, -50)),
            Txt("Split.Shares", 44, dark, title: true),
            Img("Button", plank).Size(340, 112).M(UiMotion.DropSmall, press: UiMotion.Press),
            Txt("Button.Text", 36, cream, title: true, material: shadow).Inset(0, 10, 0, 0),
            Img("Panel.Speaker", plank, ppu: 2).Size(250, 60),
            Txt("Panel.Speaker.Text", 22, cream, title: true, material: shadow).Inset(0, 5, 0, 0),
            Img("Loader.Dot", One(S, "coin"), Image.Type.Simple).M(idle: UiMotion.Spin, idleDuration: 1.2f),

            Img("Talk", plank).Size(460, 116).M(UiMotion.PlankUp, UiMotion.PlankDown, press: UiMotion.Press, idle: UiMotion.Swing, idleDuration: 3f),
            Txt("Talk.Text", 42, cream, title: true, material: shadow).Inset(30, 12, 30, 0),

            Img("Instr", One(S, "scroll")).M(UiMotion.Unroll, UiMotion.Reroll, pulse: UiMotion.Rustle),
            Txt("Instr.Text", 46, dark, TextAlignmentOptions.MidlineLeft, style: FontStyles.Bold),
            Txt("Instr.Count", 36, red, title: true).M(pulse: UiMotion.GoldBump),
            Img("Instr.RollerL", One(S, "roller")).Rect(Vector2.zero, new Vector2(0, 1), new Vector2(-20, -16), new Vector2(20, 16)),
            Img("Instr.RollerR", One(S, "roller")).Rect(new Vector2(1, 0), Vector2.one, new Vector2(-20, -16), new Vector2(20, 16)),

            Img("Arrow", shield, Image.Type.Simple).Size(176, 186).M(UiMotion.ShieldIn, UiMotion.ShieldOut, press: UiMotion.Press),
            Img("Arrow.Icon", One(S, "turn"), Image.Type.Simple).Aspect().Inset(44, 54, 44, 34),
            Img("Arrow.Want", One(S, "glow_circle"), Image.Type.Simple).Inset(-21, -11, -21, -21).M(idle: UiMotion.Glow),
            Img("Jump", plank).Size(380, 118).M(UiMotion.PlankUp, UiMotion.PlankDown, press: UiMotion.Press),
            Txt("Jump.Text", 36, cream, title: true, material: shadow).Inset(84, 14, 30, 0),
            Img("Jump.Icon", One(S, "up"), Image.Type.Simple).Aspect().AtLeft(36, 54, 54, 5),
            Img("Jump.Want", One(S, "glow")).Inset(-26, -16, -26, -26).M(idle: UiMotion.Glow),

            Img("Step", shield, Image.Type.Simple).Size(112, 118).M(press: UiMotion.Press),
            Txt("Step.Text", 50, cream, title: true, material: shadow).Inset(0, 12, 0, 0),
            Img("Track", One(S, "rail")).Rect(new Vector2(0, 0.37f), new Vector2(1, 0.63f), Vector2.zero, Vector2.zero),
            new UiLook { key = "FillArea" }.Rect(new Vector2(0, 0.37f), new Vector2(1, 0.63f), Vector2.zero, Vector2.zero),
            Tint("Fill", UnityEngine.Color.clear),
            Img("Handle", One(S, "gem"), Image.Type.Simple).Aspect().Offsets(new Vector2(-46, 14), new Vector2(46, -14)),

            Img("Balance", plank).Size(340, 118).M(UiMotion.Drop, pulse: UiMotion.Press),
            Img("Balance.Icon", One(S, "pouch"), Image.Type.Simple).Aspect().AtLeft(20, 76, 76, 5),
            Txt("Balance.Caption", 17, C("#f6e7c4", 0.85f), TextAlignmentOptions.BottomLeft, title: true).Rect(new Vector2(0, 0.5f), Vector2.one, new Vector2(106, 10), new Vector2(-26, -20)),
            Txt("Balance.Value", 40, gold, TextAlignmentOptions.TopLeft, title: true, material: shadow).Rect(Vector2.zero, new Vector2(1, 0.5f), new Vector2(106, 20), new Vector2(-26, 6)),
            Txt("Balance.Gain", 40, gold, TextAlignmentOptions.Right, title: true, material: shadow).M(UiMotion.GoldRise),
            Img("Round", One(S, "banner"), Image.Type.Simple).Rect(new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -110), new Vector2(350, 0)).M(UiMotion.Unfurl, pulse: UiMotion.Unfurl),
            Txt("Round.Text", 28, gold, title: true).Inset(0, 34, 0, 10),

            Tint("Done", UnityEngine.Color.clear).M(exit: UiMotion.PlankDown),
            Img("Done.Icon", One(S, "seal"), Image.Type.Simple).Aspect().M(UiMotion.Stamp),
            Txt("Done.Text", 66, gold, title: true, material: outline).M(UiMotion.DropSmall),

            Img("Door.Outline", One(S, "glow"), ppu: 0.25f).Inset(-136, -136, -136, -136).M(idle: UiMotion.Glow),
            Img("Door.Tag", plank, ppu: 0.35f).Size(980, 310).M(UiMotion.Drop, idle: UiMotion.Swing),
            Txt("Door.Text", 130, cream, title: true, material: shadow).Inset(0, 36, 0, 0),
            Img("Door.Rope", One(S, "rope"), Image.Type.Simple).AboveTop(560, 240, -30),
        };
        return SaveStyle(S, body, title, 38, looks);
    }

    // ------------------------------------------------------------------ scene

    static void BuildScene(UiStyle[] styles)
    {
        var scene = EditorSceneManager.GetActiveScene();
        var ui = FindRoot("UI").transform;
        var door = FindRoot("World").transform.Find("DoorButton");

        // style manager on the canvas
        var uiStyles = GetOrAdd<UiStyles>(ui.gameObject);
        Set(uiStyles, "styles", styles);
        var source = GetOrAdd<AudioSource>(ui.gameObject);
        source.playOnAwake = false;
        source.spatialBlend = 0;
        GetOrAdd<UiSfx>(ui.gameObject);

        // prompt panel
        var prompt = ui.Find("PromptPanel");
        Skin(prompt, "Panel");
        var promptWriter = GetOrAdd<UiTypewriter>(prompt.gameObject);
        PanelDecorations(prompt);
        Skin(prompt.Find("Text"), "Prompt.Text");
        foreach (var name in new[] { "Buttons/Button", "Buttons/SecondButton" })
            SkinButton(prompt.Find(name));
        var speaker = Node(prompt, "Speaker", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(40, 0), new Vector2(250, 64));
        AddImage(speaker);
        Skin(speaker, "Panel.Speaker");
        var speakerLabel = Label(speaker, "Label", 30);
        Skin(speakerLabel.transform, "Panel.Speaker.Text");
        speaker.gameObject.SetActive(false);
        var loader = Node(prompt, "Loader", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(0, 90), new Vector2(200, 40));
        for (int i = 0; i < 3; i++)
        {
            var dot = Node(loader, "Dot" + i, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 52, 0), new Vector2(32, 32));
            AddImage(dot);
            Skin(dot, "Loader.Dot", phase: i * 0.3f);
        }
        loader.gameObject.SetActive(false);
        var uiManager = ui.GetComponent<UIManager>();
        Set(uiManager, "typewriter", promptWriter);
        Set(uiManager, "speakerTag", speaker.gameObject);
        Set(uiManager, "speakerLabel", speakerLabel);
        Set(uiManager, "loader", loader.gameObject);

        // split panel
        var split = ui.Find("SplitPanel");
        Skin(split, "Panel");
        var splitWriter = GetOrAdd<UiTypewriter>(split.gameObject);
        PanelDecorations(split);
        Skin(split.Find("Text"), "Split.Text");
        Skin(split.Find("Shares"), "Split.Shares");
        foreach (var name in new[] { "Minus", "Plus" })
        {
            Skin(split.Find(name), "Step");
            Skin(split.Find(name + "/Label"), "Step.Text");
        }
        Skin(split.Find("Slider/Background"), "Track");
        Skin(split.Find("Slider/Fill Area"), "FillArea");
        Skin(split.Find("Slider/Fill Area/Fill"), "Fill");
        Skin(split.Find("Slider/Handle Slide Area/Handle"), "Handle");
        SkinButton(split.Find("Confirm"));
        Set(split.GetComponent<SplitInputUI>(), "typewriter", splitWriter);
        // Compact layout, so the panel stays below the NPC's face.
        ((RectTransform)split).sizeDelta = new Vector2(1400, 600);
        SetY(split.Find("Shares"), -200, 70);
        SetY(split.Find("Minus"), -280);
        SetY(split.Find("Plus"), -280);
        SetY(split.Find("Slider"), -285);
        SetY(split.Find("Confirm"), 30);

        // balance plaque
        var balance = ui.Find("Balance");
        Skin(balance, "Balance");
        var oldLabel = balance.Find("Label");
        if (oldLabel != null)
            oldLabel.name = "Value";
        var value = balance.Find("Value").GetComponent<TMP_Text>();
        Skin(value.transform, "Balance.Value");
        var caption = Label(balance, "Caption", 20);
        Skin(caption.transform, "Balance.Caption");
        var icon = Node(balance, "Icon", Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60, 60));
        AddImage(icon);
        Skin(icon, "Balance.Icon", decoration: true);
        var gain = Node(balance, "Gain", new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(-10, -10), new Vector2(320, 70));
        var gainText = Label(gain, null, 30);
        Skin(gain, "Balance.Gain");
        gain.gameObject.SetActive(false);
        var balanceUI = balance.GetComponent<BalanceUI>();
        Set(balanceUI, "caption", caption);
        Set(balanceUI, "value", value);
        Set(balanceUI, "gainText", gainText);
        Set(balanceUI, "skin", balance.GetComponent<UiSkin>());

        // round label
        var round = Node(ui, "Round", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -20), new Vector2(330, 88));
        round.SetSiblingIndex(balance.GetSiblingIndex());
        AddImage(round);
        Skin(round, "Round");
        var roundLabel = Label(round, "Label", 24);
        Skin(roundLabel.transform, "Round.Text");
        var roundUI = GetOrAdd<RoundUI>(round.gameObject);
        Set(roundUI, "label", roundLabel);
        Set(roundUI, "skin", round.GetComponent<UiSkin>());
        round.gameObject.SetActive(false);
        Set(Object.FindFirstObjectByType<UltimatumController>(FindObjectsInactive.Include), "roundUI", roundUI);

        // instruction: panel hugs its text
        var instr = ui.Find("InstructionPanel");
        Skin(instr, "Instr");
        var layout = GetOrAdd<HorizontalLayoutGroup>(instr.gameObject);
        layout.padding = new RectOffset(64, 64, 26, 30);
        layout.spacing = 30;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        var fitter = GetOrAdd<ContentSizeFitter>(instr.gameObject);
        fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var instrText = instr.Find("Text");
        GetOrAdd<UiMaxWidth>(instrText.gameObject);
        instrText.GetComponent<TMP_Text>().textWrappingMode = TextWrappingModes.Normal;
        Skin(instrText, "Instr.Text");
        var count = instr.Find("Count");
        count.GetComponent<TMP_Text>().textWrappingMode = TextWrappingModes.NoWrap;
        Skin(count, "Instr.Count");
        Decoration(instr, "Tri", "Instr.Tri");
        Decoration(instr, "Tape", "Instr.Tape", front: true);
        Decoration(instr, "RollerL", "Instr.RollerL", front: true);
        Decoration(instr, "RollerR", "Instr.RollerR", front: true);

        // turn and jump buttons
        var actions = ui.Find("ActionButtons");
        var wants = new Dictionary<string, GameObject>();
        foreach (var name in new[] { "TurnLeft", "TurnRight" })
        {
            var button = actions.Find(name);
            Skin(button, "Arrow");
            var arrow = button.Find("Arrow");
            Skin(arrow, "Arrow.Icon");
            arrow.localScale = new Vector3(name == "TurnRight" ? -1 : 1, 1, 1);
            wants[name] = Want(button, "Arrow.Want");
        }
        var jump = actions.Find("Jump");
        Skin(jump, "Jump");
        Skin(jump.Find("Label"), "Jump.Text");
        var jumpIcon = Node(jump, "Icon", Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(50, 50));
        AddImage(jumpIcon);
        Skin(jumpIcon, "Jump.Icon");
        wants["Jump"] = Want(jump, "Jump.Want");
        var actionButtons = actions.GetComponent<ActionButtonsUI>();
        Set(actionButtons, "turnLeftWant", wants["TurnLeft"]);
        Set(actionButtons, "turnRightWant", wants["TurnRight"]);
        Set(actionButtons, "jumpWant", wants["Jump"]);

        // talk button
        var talk = ui.Find("SoloButton");
        Skin(talk, "Talk");
        Skin(talk.Find("Label"), "Talk.Text");
        var talkIcon = Node(talk, "Icon", Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(56, 40));
        AddImage(talkIcon);
        Skin(talkIcon, "Talk.Icon", decoration: true);

        // "Geschafft!" after each tutorial step
        var done = Node(ui, "StepDone", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -170), new Vector2(560, 300));
        done.SetSiblingIndex(instr.GetSiblingIndex() + 1);
        AddImage(done);
        Skin(done, "Done");
        var doneIcon = Node(done, "Icon", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -20), new Vector2(150, 150));
        AddImage(doneIcon);
        Skin(doneIcon, "Done.Icon");
        var doneLabel = Label(done, "Label", 60);
        var doneRect = doneLabel.rectTransform;
        doneRect.anchorMin = Vector2.zero;
        doneRect.anchorMax = new Vector2(1, 0);
        doneRect.pivot = new Vector2(0.5f, 0);
        doneRect.anchoredPosition = new Vector2(0, 20);
        doneRect.sizeDelta = new Vector2(0, 110);
        Skin(doneLabel.transform, "Done.Text");
        var stepDone = GetOrAdd<StepDoneUI>(done.gameObject);
        Set(stepDone, "label", doneLabel);
        done.gameObject.SetActive(false);
        Set(Object.FindFirstObjectByType<TutorialController>(FindObjectsInactive.Include), "stepDone", stepDone);

        // debug switcher "UI", a copy of the room switcher
        var uiSwitcher = ui.Find("DebugUiSwitcher");
        if (uiSwitcher == null)
        {
            var roomSwitcher = ui.Find("DebugRoomSwitcher");
            uiSwitcher = Object.Instantiate(roomSwitcher.gameObject, ui).transform;
            uiSwitcher.name = "DebugUiSwitcher";
            uiSwitcher.SetSiblingIndex(roomSwitcher.GetSiblingIndex() + 1);
            Object.DestroyImmediate(uiSwitcher.GetComponent<DebugRoomSwitcher>());
        }
        var switcher = GetOrAdd<DebugUiSwitcher>(uiSwitcher.gameObject);
        var toggle = uiSwitcher.Find("Toggle");
        toggle.GetComponentInChildren<TMP_Text>().text = "UI";
        Set(switcher, "styles", uiStyles);
        Set(switcher, "overlay", Object.FindFirstObjectByType<DebugOverlay>(FindObjectsInactive.Include));
        Set(switcher, "toggleButton", toggle.GetComponent<Button>());
        Set(switcher, "panel", uiSwitcher.Find("Panel").gameObject);
        Set(switcher, "grid", uiSwitcher.Find("Panel"));
        Set(switcher, "template", uiSwitcher.Find("ButtonTemplate").GetComponent<Button>());
        uiSwitcher.gameObject.SetActive(false);

        LayoutDebug(ui);
        BuildOrientationOverlay(ui);

        var flow = Object.FindFirstObjectByType<GameFlow>(FindObjectsInactive.Include);
        Set(flow, "uiSwitcher", switcher);
        Set(flow, "uiStyles", uiStyles);

        // door: the whole door is the button, with an outline and a tag
        var hit = door.Find("Button");
        hit.GetComponent<Image>().color = UnityEngine.Color.clear;
        var outline = Node(hit, "Outline", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        outline.SetSiblingIndex(0);
        AddImage(outline);
        Skin(outline, "Door.Outline", motionScale: 6);
        var tag = hit.Find("Tag");
        ((RectTransform)tag).anchoredPosition = new Vector2(0, -520);
        Skin(tag, "Door.Tag", motionScale: 6);
        Skin(tag.Find("Label"), "Door.Text");
        var rope = Node(tag, "Rope", Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400, 180));
        rope.SetSiblingIndex(0);
        AddImage(rope);
        Skin(rope, "Door.Rope", decoration: true);
        var pointer = Node(tag, "Pointer", Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(130, 130));
        AddImage(pointer);
        Skin(pointer, "Door.Pointer", decoration: true, motionScale: 6);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    static void SetY(Transform node, float y, float height = -1)
    {
        var rt = (RectTransform)node;
        rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, y);
        if (height > 0)
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, height);
    }

    static void PanelDecorations(Transform panel)
    {
        Decoration(panel, "Lines", "Panel.Lines");
        Decoration(panel, "Margin", "Panel.Margin", index: 1);
        Decoration(panel, "Tape", "Panel.Tape", front: true);
    }

    static void SkinButton(Transform button)
    {
        Skin(button, "Button");
        var label = button.Find("Label");
        Skin(label, "Button.Text");
        Decoration(button, "Hl", "Button.Hl");
    }

    // Image only some styles use. Ignored by layout groups, raycasts pass through.
    static Transform Decoration(Transform parent, string name, string key, bool front = false, int index = 0)
    {
        var node = Node(parent, name, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        AddImage(node);
        GetOrAdd<LayoutElement>(node.gameObject).ignoreLayout = true;
        Skin(node, key, decoration: true);
        if (front)
            node.SetAsLastSibling();
        else
            node.SetSiblingIndex(index);
        return node;
    }

    static GameObject Want(Transform button, string key)
    {
        var want = Node(button, "Want", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        want.SetSiblingIndex(0);
        AddImage(want);
        Skin(want, key);
        want.gameObject.SetActive(false);
        return want.gameObject;
    }

    static GameObject FindRoot(string name)
    {
        foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            if (root.name == name)
                return root;
        throw new System.Exception("Scene root not found: " + name);
    }

    static RectTransform Node(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var existing = parent.Find(name);
        RectTransform rt;
        if (existing != null)
        {
            rt = (RectTransform)existing;
        }
        else
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
        }
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
        return rt;
    }

    static Image AddImage(Transform node)
    {
        var image = GetOrAdd<Image>(node.gameObject);
        image.raycastTarget = false;
        return image;
    }

    static TMP_Text Label(Transform parent, string name, float size)
    {
        var node = name == null ? (RectTransform)parent : Node(parent, name, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        var text = GetOrAdd<TextMeshProUGUI>(node.gameObject);
        text.raycastTarget = false;
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }

    static void Skin(Transform node, string key, bool decoration = false, float phase = 0, float motionScale = 1)
    {
        var skin = GetOrAdd<UiSkin>(node.gameObject);
        var so = new SerializedObject(skin);
        so.FindProperty("key").stringValue = key;
        so.FindProperty("decoration").boolValue = decoration;
        so.FindProperty("phase").floatValue = phase;
        so.FindProperty("motionScale").floatValue = motionScale;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static T GetOrAdd<T>(GameObject go) where T : Component =>
        go.TryGetComponent(out T c) ? c : go.AddComponent<T>();

    static void Set(Component target, string field, Object value)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(field) ?? throw new System.Exception($"{target.GetType().Name}.{field} not found");
        prop.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void Set(Component target, string field, Object[] values)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(field);
        prop.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Debug tools small in the bottom-left corner: toggles stacked (UI, Raum, Avatar from the bottom),
    // overlay text above them, the opened grid right next to the toggles.
    static void LayoutDebug(Transform ui)
    {
        string[] switchers = { "DebugUiSwitcher", "DebugRoomSwitcher", "DebugAvatarSwitcher" };
        for (int i = 0; i < switchers.Length; i++)
        {
            var root = ui.Find(switchers[i]);
            var toggle = (RectTransform)root.Find("Toggle");
            toggle.anchorMin = toggle.anchorMax = toggle.pivot = Vector2.zero;
            toggle.anchoredPosition = new Vector2(16, 16 + i * 52);
            toggle.sizeDelta = new Vector2(120, 44);
            SmallText(toggle, 22);

            var panel = (RectTransform)root.Find("Panel");
            panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.zero;
            panel.anchoredPosition = new Vector2(148, 16);
            var grid = panel.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(170, 48);
            grid.spacing = new Vector2(8, 8);
            grid.padding = new RectOffset(10, 10, 10, 10);
            var fitter = GetOrAdd<ContentSizeFitter>(panel.gameObject);
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            SmallText(root.Find("ButtonTemplate"), 20);
        }

        var overlay = (RectTransform)ui.Find("DebugOverlay");
        overlay.anchorMin = overlay.anchorMax = overlay.pivot = Vector2.zero;
        overlay.anchoredPosition = new Vector2(16, 172);
        overlay.sizeDelta = new Vector2(1000, 60);
        var label = SmallText(overlay, 18);
        label.alignment = TextAlignmentOptions.BottomLeft;
    }

    // Portrait cover on top of everything (last child), shown by OrientationOverlay while the screen is taller than wide.
    static void BuildOrientationOverlay(Transform ui)
    {
        var root = Node(ui, "OrientationOverlay", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        root.SetAsLastSibling();
        var cover = Node(root, "Cover", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        var image = AddImage(cover);
        image.color = new UnityEngine.Color(0.08f, 0.08f, 0.1f, 1);
        image.raycastTarget = true;
        var label = Label(cover, "Label", 90);
        label.textWrappingMode = TextWrappingModes.Normal;
        label.color = UnityEngine.Color.white;
        label.text = "Bitte Gerät drehen";
        var rect = label.rectTransform;
        rect.offsetMin = new Vector2(60, 0);
        rect.offsetMax = new Vector2(-60, 0);
        cover.gameObject.SetActive(false);
        var overlay = GetOrAdd<OrientationOverlay>(root.gameObject);
        Set(overlay, "cover", cover.gameObject);
        Set(overlay, "label", label);
        Set(Object.FindFirstObjectByType<GameFlow>(FindObjectsInactive.Include), "orientationOverlay", overlay);
    }

    static TMP_Text SmallText(Transform t, float size)
    {
        var text = t.GetComponentInChildren<TMP_Text>(true);
        text.enableAutoSizing = false;
        text.fontSize = size;
        return text;
    }
}
