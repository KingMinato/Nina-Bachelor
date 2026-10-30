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

    // Shows the button and completes when it is tapped.
    public async Awaitable WaitForClick(string caption)
    {
        label.text = caption;
        button.interactable = false;
        gameObject.SetActive(true);
        await Awaitable.WaitForSecondsAsync(inputDelaySeconds);
        button.interactable = true;

        var clicked = new AwaitableCompletionSource();
        void OnClick() => clicked.TrySetResult();
        button.onClick.AddListener(OnClick);
        await clicked.Awaitable;
        button.onClick.RemoveListener(OnClick);

        button.interactable = false;
        gameObject.SetActive(false);
    }
}
