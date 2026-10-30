using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One UI style (Pixel, Skizze, Holz): fonts and a look per skin key. Created by Editor/UiStyleBuilder.cs.
// Every UiSkin in the scene names a key, the current style decides how that element looks and moves.
[CreateAssetMenu(menuName = "Mirror Game/UI Style")]
public class UiStyle : ScriptableObject
{
    public string displayName;
    public TMP_FontAsset font;
    public TMP_FontAsset titleFont;
    [Tooltip("Typewriter speed of dialog texts, 0 shows them at once.")]
    public float charsPerSecond = 40;
    public UiLook[] looks = new UiLook[0];

    public UiLook Find(string key)
    {
        foreach (var look in looks)
            if (look.key == key)
                return look;
        return null;
    }
}

// Size: sizeDelta. Rect: anchors and offsets. Offsets: only offsets (for rects whose anchors a Slider drives).
public enum UiRectMode { Keep, Size, Rect, Offsets }

[Serializable]
public class UiLook
{
    public string key;

    [Header("Image")]
    [Tooltip("More than one sprite: the frames are cycled (boiling outline, blinking).")]
    public Sprite[] sprites = new Sprite[0];
    public float framesPerSecond = 8;
    public Color color = Color.white;
    public Image.Type imageType = Image.Type.Simple;
    public float pixelsPerUnit = 1;
    public bool preserveAspect;

    [Header("Text")]
    public bool titleFont;
    public Material fontMaterial;
    public float fontSize;
    public Color textColor = Color.black;
    public TextAlignmentOptions alignment = TextAlignmentOptions.Center;
    public FontStyles fontStyle;
    public float characterSpacing;
    public float lineSpacing;

    [Header("Rect")]
    public UiRectMode rectMode;
    public Vector2 anchorMin, anchorMax, offsetMin, offsetMax;
    public Vector2 size;
    public float rotation;

    [Header("Motion")]
    public UiMotion enter;
    public UiMotion exit;
    [Tooltip("Played instead of exit + enter when the element is shown again right after hiding (next dialog line).")]
    public UiMotion swap;
    public UiMotion idle;
    [Tooltip("Idle motion while the element is marked as wanted (tutorial highlight).")]
    public UiMotion wantIdle;
    public UiMotion press;
    [Tooltip("One-shot feedback, e.g. counter changed or balance gained.")]
    public UiMotion pulse;
    [Tooltip("Overrides the length of the idle motion if > 0.")]
    public float idleDuration;
}
