using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

// Reveals a dialog text letter by letter at the speed of the current UI style.
// Sits on the panel: tapping the panel (not its buttons) shows the rest at once.
public class UiTypewriter : MonoBehaviour, IPointerDownHandler
{
    [Tooltip("Pauses after punctuation are this many letters long.")]
    [SerializeField] float punctuationPause = 6;

    bool skip;

    public async Awaitable Play(TMP_Text text, string message)
    {
        text.text = message;
        text.maxVisibleCharacters = 0;
        text.ForceMeshUpdate();
        int count = text.textInfo.characterCount;
        skip = false;

        float elapsed = 0;
        int shown = 0;
        while (shown < count && !skip && text != null)
        {
            float speed = UiStyles.Current != null ? UiStyles.Current.charsPerSecond : 0;
            if (speed <= 0)
                break;
            elapsed += Time.unscaledDeltaTime * speed;
            while (shown < count && elapsed >= 1)
            {
                char c = text.textInfo.characterInfo[shown].character;
                elapsed -= ".,!?:".IndexOf(c) >= 0 ? punctuationPause : 1;
                if (shown % 2 == 0 && !char.IsWhiteSpace(c))
                    UiSfx.Play(UiSfx.Type);
                shown++;
            }
            text.maxVisibleCharacters = shown;
            await Awaitable.NextFrameAsync();
        }
        if (text != null)
            text.maxVisibleCharacters = 99999;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        skip = true;
    }
}
