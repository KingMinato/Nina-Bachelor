using UnityEngine;

// Holds the available UI styles and applies one to every UiSkin in the scene (also inactive ones).
// The start style is applied in Awake; the debug switcher and the URL parameter "ui" switch at runtime.
public class UiStyles : MonoBehaviour
{
    [SerializeField] UiStyle[] styles;
    [SerializeField] int startIndex;

    public static UiStyle Current { get; private set; }

    public UiStyle[] Styles => styles;

    void Awake()
    {
        Apply(styles[startIndex]);
    }

    // Returns false if no style has this name (case-insensitive).
    public bool Apply(string name)
    {
        foreach (var style in styles)
        {
            if (string.Equals(style.displayName, name, System.StringComparison.OrdinalIgnoreCase))
            {
                Apply(style);
                return true;
            }
        }
        return false;
    }

    public void Apply(UiStyle style)
    {
        Current = style;
        foreach (var skin in FindObjectsByType<UiSkin>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            skin.Apply(style);
    }
}
