using TMPro;
using UnityEngine;

// "Bitte Gerät drehen": covers the whole screen and blocks all input while the screen is taller than wide.
// The game is designed for landscape only. Sits on the UI canvas as the last child, the cover is a child.
public class OrientationOverlay : MonoBehaviour
{
    [SerializeField] GameObject cover;
    [SerializeField] TMP_Text label;

    public void SetText(string text)
    {
        label.text = text;
    }

    void Update()
    {
        bool portrait = Screen.height > Screen.width;
        if (cover.activeSelf == portrait)
            return;
        cover.SetActive(portrait);
        if (portrait && UiStyles.Current != null && UiStyles.Current.font != null)
            label.font = UiStyles.Current.font;
    }
}
