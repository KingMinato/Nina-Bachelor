using TMPro;
using UnityEngine;
using UnityEngine.UI;

// A button placed in the world on a world-space canvas covering an object, e.g. the whole door.
// The player taps the object itself instead of a screen button; an outline shows that it is tappable.
// Starts inactive in the scene.
public class WorldButton : MonoBehaviour
{
    [SerializeField] Button button;
    [SerializeField] TMP_Text label;

    [Tooltip("Button stays disabled this long after it appears, so a double tap cannot trigger it by accident.")]
    [SerializeField] float inputDelaySeconds = 0.4f;

    // Middle of the tappable area, i.e. of the object it covers.
    public Vector3 Center => button.transform.position;

    // Shows all buttons (empty caption = no tag) and returns the one tapped first; all are hidden again.
    public static async Awaitable<WorldButton> FirstClick(params (WorldButton button, string caption)[] options)
    {
        foreach (var (b, caption) in options)
        {
            if (b.label != null)
            {
                b.label.text = caption;
                b.label.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(caption));
            }
            b.button.interactable = false;
            b.gameObject.SetActive(true);
        }
        await Awaitable.WaitForSecondsAsync(options[0].button.inputDelaySeconds);

        var clicked = new AwaitableCompletionSource<WorldButton>();
        var handlers = new UnityEngine.Events.UnityAction[options.Length];
        for (int i = 0; i < options.Length; i++)
        {
            var b = options[i].button;
            handlers[i] = () => clicked.TrySetResult(b);
            b.button.onClick.AddListener(handlers[i]);
            b.button.interactable = true;
        }
        var result = await clicked.Awaitable;
        for (int i = 0; i < options.Length; i++)
        {
            var b = options[i].button;
            b.button.onClick.RemoveListener(handlers[i]);
            b.button.interactable = false;
            b.gameObject.SetActive(false);
        }
        return result;
    }
}
