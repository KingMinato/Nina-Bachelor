using TMPro;
using UnityEngine;

// "Runde 1 von 4" during the dialog. An empty text in the config hides it. Starts inactive.
public class RoundUI : MonoBehaviour
{
    [SerializeField] TMP_Text label;
    [SerializeField] UiSkin skin;

    public void Show(string template, int round, int total)
    {
        if (string.IsNullOrEmpty(template))
            return;
        label.text = template.Replace("{round}", round.ToString()).Replace("{total}", total.ToString());
        if (gameObject.activeSelf)
            skin.Pulse();
        else
            gameObject.SetActive(true);
    }
}
