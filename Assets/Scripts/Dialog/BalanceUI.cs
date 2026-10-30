using TMPro;
using UnityEngine;

// Balance plaque, visible from the start of the dialog until the redirect. Gains count up
// and show a short "+N CHF". Starts inactive in the scene, UltimatumController activates it.
public class BalanceUI : MonoBehaviour
{
    [SerializeField] TMP_Text caption;
    [SerializeField] TMP_Text value;
    [SerializeField] TMP_Text gainText;
    [SerializeField] UiSkin skin;
    [SerializeField] float countSeconds = 0.9f;
    [SerializeField] float gainSeconds = 1.3f;

    int shown;
    int target;

    public void Show(string caption, int balance)
    {
        this.caption.text = caption;
        shown = target = balance;
        value.text = Format(balance);
        gainText.gameObject.SetActive(false);
        gameObject.SetActive(true);
    }

    public void Set(int balance)
    {
        int gain = balance - target;
        target = balance;
        if (gain <= 0 || !isActiveAndEnabled)
        {
            shown = balance;
            value.text = Format(balance);
            return;
        }
        gainText.text = "+" + Format(gain);
        gainText.gameObject.SetActive(false);
        gainText.gameObject.SetActive(true);
        skin.Pulse();
        CountUp(shown, balance);
        HideGain();
    }

    async void CountUp(int from, int to)
    {
        float start = Time.unscaledTime;
        while (this != null && target == to)
        {
            float p = Mathf.Clamp01((Time.unscaledTime - start) / countSeconds);
            shown = Mathf.RoundToInt(Mathf.Lerp(from, to, p));
            value.text = Format(shown);
            if (p >= 1)
                break;
            await Awaitable.NextFrameAsync();
        }
    }

    async void HideGain()
    {
        string text = gainText.text;
        await Awaitable.WaitForSecondsAsync(gainSeconds);
        if (this != null && gainText.text == text)
            gainText.gameObject.SetActive(false);
    }

    static string Format(int amount) => $"{amount} CHF";
}
