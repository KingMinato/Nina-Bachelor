using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Layout size of a text that grows with its content up to a maximum width, then wraps.
// Lets panels with a ContentSizeFitter hug short instructions without running off screen on long ones.
[RequireComponent(typeof(TMP_Text))]
public class UiMaxWidth : MonoBehaviour, ILayoutElement
{
    [SerializeField] float maxWidth = 1300;

    TMP_Text text;
    TMP_Text Text => text != null ? text : text = GetComponent<TMP_Text>();

    public float minWidth => -1;
    public float preferredWidth => Mathf.Min(Text.GetPreferredValues(Text.text, Mathf.Infinity, Mathf.Infinity).x, maxWidth);
    public float flexibleWidth => -1;
    public float minHeight => -1;
    public float preferredHeight => Text.GetPreferredValues(Text.text, preferredWidth, Mathf.Infinity).y;
    public float flexibleHeight => -1;
    public int layoutPriority => 2;

    public void CalculateLayoutInputHorizontal() { }
    public void CalculateLayoutInputVertical() { }
}
