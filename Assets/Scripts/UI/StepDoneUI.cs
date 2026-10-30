using TMPro;
using UnityEngine;

// Short "Geschafft!" after each completed tutorial step. Does not block the tutorial. Starts inactive.
public class StepDoneUI : MonoBehaviour
{
    [SerializeField] TMP_Text label;
    [SerializeField] float seconds = 1.1f;

    int shownCount;

    public async void Show(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;
        label.text = text;
        int id = ++shownCount;
        if (gameObject.activeSelf)
            gameObject.SetActive(false);
        UiSkin.Show(gameObject);
        UiSfx.Play(UiSfx.Done);
        await Awaitable.WaitForSecondsAsync(seconds);
        if (id == shownCount && this != null)
            UiSkin.Hide(gameObject);
    }
}
