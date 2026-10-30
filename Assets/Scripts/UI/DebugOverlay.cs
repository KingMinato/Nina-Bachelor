using TMPro;
using UnityEngine;

// Only shown with ?debug=1. Displays the current state and the received parameters.
// Starts inactive in the scene, GameFlow activates it.
public class DebugOverlay : MonoBehaviour
{
    [SerializeField] TMP_Text label;

    string paramsLine = "";

    public void Show(UrlParams p)
    {
        paramsLine = $"pid={p.Pid}  g='{p.RawGender}'  ag={p.AvatarGender}  s={p.Species}  test={p.IsTest}";
        gameObject.SetActive(true);
    }

    public void ShowAvatar(string speciesId)
    {
        paramsLine += $"  avatar={speciesId}";
    }

    public void SetState(GameState state)
    {
        label.text = $"{state}\n{paramsLine}";
    }
}
