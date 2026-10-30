using System.Collections.Generic;
using UnityEngine;

// Named UI animations of the three styles, ported from the CSS keyframes of the HTML prototypes.
// Offsets are in canvas pixels (y up), rotations in degrees (counter-clockwise), scale and alpha as factors.
public enum UiMotion
{
    None,
    // Pixel: stepped like sprite animation
    PixelPop, PixelOut, PixelGrow, PixelShrink, PixelFlicker, PixelPress, PixelBump, PixelRise, Bob, Blink, Spin, Tilt,
    // Skizze
    PopIn, PopOut, Slap, Tear, PaperIn, PaperOut, Shuffle, Squish, Wiggle, Wob, Hop, InkBump, InkRise,
    // Holz
    ShieldIn, ShieldOut, PlankUp, PlankDown, Drop, DropSmall, Unroll, Reroll, PanelUp, PanelDown, Rustle, Press, Swing,
    Glow, Stamp, Unfurl, GoldBump, GoldRise,
}

public struct UiPose
{
    public Vector2 Offset;
    public Vector2 Scale;
    public float Rotation;
    public float Alpha;

    public static readonly UiPose Identity = new UiPose { Scale = Vector2.one, Alpha = 1 };

    public bool Same(UiPose o) => Offset == o.Offset && Scale == o.Scale && Rotation == o.Rotation && Alpha == o.Alpha;

    public static UiPose operator *(UiPose a, UiPose b) => new UiPose
    {
        Offset = a.Offset + b.Offset,
        Scale = Vector2.Scale(a.Scale, b.Scale),
        Rotation = a.Rotation + b.Rotation,
        Alpha = a.Alpha * b.Alpha,
    };
}

public static class UiMotions
{
    struct Key
    {
        public float T;
        public UiPose Pose;
    }

    class Clip
    {
        public float Duration;
        public bool Loop;
        public bool Hold;   // jump from key to key (CSS steps), otherwise eased in between
        public Key[] Keys;
    }

    static Key K(float t, float x = 0, float y = 0, float sx = 1, float sy = 1, float rot = 0, float a = 1) =>
        new Key { T = t, Pose = new UiPose { Offset = new Vector2(x, y), Scale = new Vector2(sx, sy), Rotation = rot, Alpha = a } };

    static Key S(float t, float s, float rot = 0, float y = 0, float a = 1) => K(t, 0, y, s, s, rot, a);

    static Clip Once(float duration, params Key[] keys) => new Clip { Duration = duration, Keys = keys };
    static Clip Steps(float duration, params Key[] keys) => new Clip { Duration = duration, Hold = true, Keys = keys };
    static Clip LoopSmooth(float duration, params Key[] keys) => new Clip { Duration = duration, Loop = true, Keys = keys };
    static Clip LoopSteps(float duration, params Key[] keys) => new Clip { Duration = duration, Loop = true, Hold = true, Keys = keys };

    static readonly Dictionary<UiMotion, Clip> Clips = new Dictionary<UiMotion, Clip>
    {
        [UiMotion.PixelPop] = Steps(0.36f, K(0, sx: 0, sy: 0.15f), K(0.25f, sx: 1.12f, sy: 0.15f), K(0.5f, sx: 1.12f, sy: 1.15f), K(0.75f, sx: 0.96f), K(1)),
        [UiMotion.PixelOut] = Steps(0.27f, K(0), K(0.33f, sx: 1.06f, sy: 0.35f), K(0.66f, sx: 0.35f, sy: 0.12f), K(1, sx: 0, sy: 0)),
        [UiMotion.PixelGrow] = Steps(0.4f, K(0, sy: 0.08f), K(0.4f, sy: 0.4f), K(0.6f, sy: 0.75f), K(0.8f, sy: 1.04f), K(1)),
        [UiMotion.PixelShrink] = Steps(0.27f, K(0), K(0.33f, sy: 0.5f), K(0.66f, sy: 0.1f), K(1, sx: 0, sy: 0.1f)),
        [UiMotion.PixelFlicker] = Steps(0.2f, K(0), K(0.25f, a: 0.2f), K(0.5f), K(0.75f, a: 0.2f), K(1)),
        [UiMotion.PixelPress] = Steps(0.12f, K(0, y: -8), K(1)),
        [UiMotion.PixelBump] = Steps(0.3f, S(0, 1.8f), S(0.33f, 1.3f), S(0.66f, 1.1f), K(1)),
        [UiMotion.PixelRise] = Steps(0.8f, K(0), K(0.25f, y: 24), K(0.5f, y: 48), K(0.75f, y: 72), K(1, y: 96, a: 0)),
        [UiMotion.Bob] = LoopSteps(0.5f, K(0), K(0.5f, y: 8), K(1)),
        [UiMotion.Blink] = LoopSteps(1f, K(0), K(0.5f, a: 0), K(1)),
        [UiMotion.Spin] = LoopSteps(0.8f, K(0), K(0.25f, sx: 0.55f), K(0.5f, sx: 0.15f), K(0.75f, sx: 0.55f), K(1)),
        [UiMotion.Tilt] = LoopSteps(0.5f, K(0), K(0.5f, rot: 15), K(1)),

        [UiMotion.PopIn] = Once(0.45f, S(0, 0.2f, 12, a: 0), S(0.6f, 1.12f, -3), K(1)),
        [UiMotion.PopOut] = Once(0.25f, K(0), S(1, 0.3f, -10, a: 0)),
        [UiMotion.Slap] = Once(0.5f, S(0, 1.18f, 14, 170, 0), S(0.55f, 0.96f, -2, -10), S(0.75f, 1.01f, 1, 4), K(1)),
        [UiMotion.Tear] = Once(0.45f, K(0), K(0.3f, y: -6, rot: 4), K(1, x: 420, y: 260, rot: -32, a: 0)),
        [UiMotion.PaperIn] = Once(0.55f, K(0, y: -560, rot: -4), K(0.7f, y: 10, rot: 1.2f), K(1)),
        [UiMotion.PaperOut] = Once(0.35f, K(0), K(1, y: -620, rot: 5)),
        [UiMotion.Shuffle] = Once(0.3f, K(0), K(0.4f, x: -14, rot: 0.8f), K(0.7f, x: 8, rot: -0.4f), K(1)),
        [UiMotion.Squish] = Once(0.25f, K(0), K(0.35f, sx: 0.86f, sy: 0.8f, rot: 5), S(0.7f, 1.06f, -2), K(1)),
        [UiMotion.Wiggle] = LoopSteps(0.5f, K(0, rot: 7), K(0.5f, rot: -7), K(1, rot: 7)),
        [UiMotion.Wob] = LoopSteps(1f, K(0, rot: 2), K(0.5f, rot: -2), K(1, rot: 2)),
        [UiMotion.Hop] = LoopSmooth(0.7f, K(0), K(0.3f, y: 18), K(0.6f), K(1)),
        [UiMotion.InkBump] = Once(0.4f, S(0, 1.7f, 10), S(0.6f, 0.9f, -4), K(1)),
        [UiMotion.InkRise] = Once(1.3f, K(0, y: -20, rot: 6, a: 0), K(0.2f, rot: 6), K(1, y: 90, rot: 6, a: 0)),

        [UiMotion.ShieldIn] = Once(0.55f, S(0, 0, 120), S(0.7f, 1.08f, -8), K(1)),
        [UiMotion.ShieldOut] = Once(0.3f, K(0), S(1, 0, -90)),
        [UiMotion.PlankUp] = Once(0.5f, K(0, y: -180), K(0.7f, y: 10), K(1)),
        [UiMotion.PlankDown] = Once(0.3f, K(0), K(1, y: -200)),
        [UiMotion.Drop] = Once(0.6f, K(0, y: 260, rot: 8), K(0.6f, y: -16, rot: -3), K(0.8f, y: 6, rot: 1), K(1)),
        [UiMotion.DropSmall] = Once(0.4f, K(0, y: 46, rot: 4, a: 0), K(0.6f, y: -6, rot: -2), K(1)),
        [UiMotion.Unroll] = Once(0.65f, K(0, sx: 0), K(1)),
        [UiMotion.Reroll] = Once(0.45f, K(0), K(1, sx: 0)),
        [UiMotion.PanelUp] = Once(0.55f, K(0, y: -560), K(0.7f, y: 14), K(0.85f, y: -4), K(1)),
        [UiMotion.PanelDown] = Once(0.35f, K(0), K(1, y: -600)),
        [UiMotion.Rustle] = Once(0.3f, K(0), K(0.3f, y: -6, sy: 0.97f), K(0.6f, sy: 1.01f), K(1)),
        [UiMotion.Press] = Once(0.22f, K(0), S(0.35f, 0.92f, 0, -4), K(1)),
        [UiMotion.Swing] = LoopSmooth(2.4f, K(0, rot: 3), K(0.5f, rot: -3), K(1, rot: 3)),
        [UiMotion.Glow] = LoopSmooth(1.6f, K(0, a: 0.55f), K(0.5f), K(1, a: 0.55f)),
        [UiMotion.Stamp] = Once(0.45f, S(0, 2.6f, 35, a: 0), S(0.55f, 0.88f, -4), S(0.75f, 1.05f, 2), K(1)),
        [UiMotion.Unfurl] = Once(0.55f, K(0, sy: 0), K(0.7f, sy: 1.08f), K(1)),
        [UiMotion.GoldBump] = Once(0.45f, S(0, 1.7f), K(1)),
        [UiMotion.GoldRise] = Once(1.3f, K(0, y: -10, a: 0), K(0.15f), K(1, y: 80, a: 0)),
    };

    public static float Duration(UiMotion motion) => Clips.TryGetValue(motion, out var clip) ? clip.Duration : 0;

    public static bool IsLoop(UiMotion motion) => Clips.TryGetValue(motion, out var clip) && clip.Loop;

    // Pose at time t (seconds since start). duration overrides the clip length if > 0.
    public static UiPose Evaluate(UiMotion motion, float t, float duration = 0)
    {
        if (!Clips.TryGetValue(motion, out var clip))
            return UiPose.Identity;
        float length = duration > 0 ? duration : clip.Duration;
        float u = clip.Loop ? Mathf.Repeat(t / length, 1f) : Mathf.Clamp01(t / length);

        var keys = clip.Keys;
        if (u <= keys[0].T)
            return keys[0].Pose;
        for (int i = 1; i < keys.Length; i++)
        {
            if (u > keys[i].T)
                continue;
            if (clip.Hold)
                return keys[i - 1].Pose;
            float f = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(keys[i - 1].T, keys[i].T, u));
            return Lerp(keys[i - 1].Pose, keys[i].Pose, f);
        }
        return keys[keys.Length - 1].Pose;
    }

    static UiPose Lerp(UiPose a, UiPose b, float f) => new UiPose
    {
        Offset = Vector2.LerpUnclamped(a.Offset, b.Offset, f),
        Scale = Vector2.LerpUnclamped(a.Scale, b.Scale, f),
        Rotation = Mathf.LerpUnclamped(a.Rotation, b.Rotation, f),
        Alpha = Mathf.LerpUnclamped(a.Alpha, b.Alpha, f),
    };
}
