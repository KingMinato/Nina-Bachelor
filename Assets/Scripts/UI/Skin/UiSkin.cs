using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Gives a UI element the look of the current UiStyle (sprite, colours, font, size) and plays its motions:
// enter when it becomes active, exit on Hide(), press when tapped, idle loops and sprite frame cycling.
// Logic scripts keep working with SetActive; UiSkin.Show/Hide only add the transitions.
[DisallowMultipleComponent]
public class UiSkin : MonoBehaviour, IPointerDownHandler
{
    // A Show() within this time after Hide() keeps the element and plays its swap motion instead (next dialog line).
    const float SwapWindow = 0.06f;

    [SerializeField] string key;
    [Tooltip("Element only some styles use (tape, rollers, highlighter): invisible when the current style has no look for the key.")]
    [SerializeField] bool decoration;
    [Tooltip("Seconds added to looping motions and sprite frames, so e.g. loader dots run one after another.")]
    [SerializeField] float phase;
    [Tooltip("Multiplies motion offsets, for world-space canvases whose units are not screen pixels.")]
    [SerializeField] float motionScale = 1;

    public string Key => key;
    public bool Wanted { get; set; }

    bool initialised;
    RectTransform rect;
    Image image;
    TMP_Text text;
    Selectable selectable;
    CanvasGroup group;

    Vector3 baseScale;
    float baseRotation;
    Vector2 origAnchorMin, origAnchorMax, origOffsetMin, origOffsetMax;
    bool anchorsChanged, offsetsChanged;

    UiStyle appliedStyle;
    UiLook look;
    bool visible = true;

    UiMotion oneShot;
    float oneShotStart;
    Action oneShotDone;
    float hideAt = -1;
    bool exiting;
    float idleStart;
    int frame = -1;

    UiPose lastPose = UiPose.Identity;
    Vector2 basePos, written;
    bool hasWritten;

    static float Now => Time.unscaledTime;

    public static void Show(GameObject go)
    {
        if (go.TryGetComponent(out UiSkin skin))
            skin.Show();
        else
            go.SetActive(true);
    }

    public static void Hide(GameObject go)
    {
        if (go.TryGetComponent(out UiSkin skin))
            skin.Hide();
        else
            go.SetActive(false);
    }

    void EnsureInit()
    {
        if (initialised)
            return;
        initialised = true;
        rect = (RectTransform)transform;
        image = GetComponent<Image>();
        text = GetComponent<TMP_Text>();
        selectable = GetComponent<Selectable>();
        baseScale = rect.localScale;
        baseRotation = rect.localEulerAngles.z;
        origAnchorMin = rect.anchorMin;
        origAnchorMax = rect.anchorMax;
        origOffsetMin = rect.offsetMin;
        origOffsetMax = rect.offsetMax;
    }

    public void Apply(UiStyle style)
    {
        EnsureInit();
        ResetPose();
        appliedStyle = style;
        look = style != null ? style.Find(key) : null;
        visible = look != null || !decoration;
        frame = -1;
        if (look != null)
        {
            ApplyImage();
            ApplyText(style);
            ApplyRect();
        }
        ApplyPose(UiPose.Identity, force: true);
    }

    void ApplyImage()
    {
        if (image == null)
            return;
        image.sprite = look.sprites.Length > 0 ? look.sprites[0] : null;
        image.type = look.imageType;
        image.pixelsPerUnitMultiplier = look.pixelsPerUnit;
        image.preserveAspect = look.preserveAspect;
        image.color = look.color;
    }

    void ApplyText(UiStyle style)
    {
        if (text == null)
            return;
        var font = look.titleFont && style.titleFont != null ? style.titleFont : style.font;
        if (font != null)
        {
            text.font = font;
            text.fontSharedMaterial = look.fontMaterial != null ? look.fontMaterial : font.material;
        }
        if (look.fontSize > 0)
        {
            text.enableAutoSizing = false;
            text.fontSize = look.fontSize;
        }
        text.color = look.textColor;
        text.alignment = look.alignment;
        text.fontStyle = look.fontStyle;
        text.characterSpacing = look.characterSpacing;
        text.lineSpacing = look.lineSpacing;
    }

    // Only restores what a previous style changed, so rects driven by sliders or layout groups stay untouched.
    void ApplyRect()
    {
        if (anchorsChanged)
        {
            rect.anchorMin = origAnchorMin;
            rect.anchorMax = origAnchorMax;
            anchorsChanged = false;
        }
        if (offsetsChanged)
        {
            rect.offsetMin = origOffsetMin;
            rect.offsetMax = origOffsetMax;
            offsetsChanged = false;
        }
        switch (look.rectMode)
        {
            case UiRectMode.Size:
                rect.sizeDelta = look.size;
                offsetsChanged = true;
                break;
            case UiRectMode.Rect:
                rect.anchorMin = look.anchorMin;
                rect.anchorMax = look.anchorMax;
                rect.offsetMin = look.offsetMin;
                rect.offsetMax = look.offsetMax;
                anchorsChanged = offsetsChanged = true;
                break;
            case UiRectMode.Offsets:
                rect.offsetMin = look.offsetMin;
                rect.offsetMax = look.offsetMax;
                offsetsChanged = true;
                break;
        }
    }

    void OnEnable()
    {
        EnsureInit();
        if (appliedStyle != UiStyles.Current || look == null && UiStyles.Current != null && !decoration)
            Apply(UiStyles.Current);
        hideAt = -1;
        exiting = false;
        idleStart = Now;
        if (look != null && visible && look.enter != UiMotion.None)
            Play(look.enter);
        UpdatePose();
    }

    void OnDisable()
    {
        oneShot = UiMotion.None;
        oneShotDone = null;
        hideAt = -1;
        exiting = false;
        ResetPose();
    }

    public void Show()
    {
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
            return;
        }
        if (hideAt >= 0)
        {
            hideAt = -1;
            if (look != null && look.swap != UiMotion.None)
                Play(look.swap);
        }
        else if (exiting)
        {
            exiting = false;
            Play(look != null ? look.enter : UiMotion.None);
        }
    }

    public void Hide()
    {
        if (!gameObject.activeInHierarchy)
        {
            gameObject.SetActive(false);
            return;
        }
        if (hideAt < 0 && !exiting)
            hideAt = Now + SwapWindow;
    }

    public void Pulse()
    {
        if (look != null && isActiveAndEnabled)
            Play(look.pulse);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (look != null && selectable != null && selectable.IsInteractable() && hideAt < 0 && !exiting)
            Play(look.press);
    }

    void Play(UiMotion motion, Action done = null)
    {
        oneShot = motion;
        oneShotStart = Now;
        oneShotDone = done;
        if (motion == UiMotion.None)
        {
            oneShotDone = null;
            done?.Invoke();
        }
    }

    void Update()
    {
        if (hideAt >= 0 && Now >= hideAt)
        {
            hideAt = -1;
            if (look != null && visible && look.exit != UiMotion.None)
            {
                exiting = true;
                Play(look.exit, () => gameObject.SetActive(false));
            }
            else
            {
                gameObject.SetActive(false);
                return;
            }
        }
        if (look != null && visible && image != null && look.sprites.Length > 1)
        {
            int f = Mathf.FloorToInt((Now + phase) * look.framesPerSecond) % look.sprites.Length;
            if (f != frame)
            {
                frame = f;
                image.sprite = look.sprites[f];
            }
        }
        UpdatePose();
    }

    void UpdatePose()
    {
        var pose = UiPose.Identity;
        if (oneShot != UiMotion.None)
        {
            float t = Now - oneShotStart;
            if (t >= UiMotions.Duration(oneShot))
            {
                var done = oneShotDone;
                oneShot = UiMotion.None;
                oneShotDone = null;
                if (done != null)
                {
                    done();
                    return;
                }
            }
            else
            {
                pose = UiMotions.Evaluate(oneShot, t);
            }
        }
        if (look != null && visible)
        {
            var idle = Wanted && look.wantIdle != UiMotion.None ? look.wantIdle : look.idle;
            if (idle != UiMotion.None)
                pose = pose * UiMotions.Evaluate(idle, Now - idleStart + phase, look.idleDuration);
        }
        ApplyPose(pose);
    }

    void ApplyPose(UiPose pose, bool force = false)
    {
        bool raycasts = visible && hideAt < 0 && !exiting;
        if (!force && pose.Same(lastPose) && (group == null || group.blocksRaycasts == raycasts))
            return;
        lastPose = pose;

        // Layout groups may move the element at any time: whatever position we did not write ourselves is the new base.
        // anchoredPosition is derived from localPosition, so reading it back is only accurate to a few 1e-5 px.
        var current = rect.anchoredPosition;
        if (!hasWritten || !Near(current, written))
            basePos = current;
        written = basePos + pose.Offset * motionScale;
        rect.anchoredPosition = written;
        hasWritten = true;
        rect.localScale = new Vector3(baseScale.x * pose.Scale.x, baseScale.y * pose.Scale.y, baseScale.z);
        rect.localRotation = Quaternion.Euler(0, 0, baseRotation + (look != null ? look.rotation : 0) + pose.Rotation);

        float alpha = visible ? pose.Alpha : 0;
        if (group == null && (alpha < 1 || !raycasts))
            group = gameObject.AddComponent<CanvasGroup>();
        if (group != null)
        {
            group.alpha = alpha;
            group.blocksRaycasts = raycasts;
        }
    }

    static bool Near(Vector2 a, Vector2 b) => (a - b).sqrMagnitude < 0.01f;

    void ResetPose()
    {
        if (!initialised)
            return;
        if (hasWritten && Near(rect.anchoredPosition, written))
            rect.anchoredPosition = basePos;
        hasWritten = false;
        lastPose = UiPose.Identity;
        rect.localScale = baseScale;
        rect.localRotation = Quaternion.Euler(0, 0, baseRotation + (look != null ? look.rotation : 0));
        if (group != null)
        {
            group.alpha = visible ? 1 : 0;
            group.blocksRaycasts = visible;
        }
    }
}
