using TMPro;
using UnityEngine;

// Only shown with ?debug=1: card at the top right with the game state and the received parameters.
// Avatar, room and UI style are shown by the switcher rows below it. Starts inactive, GameFlow activates it.
public class DebugOverlay : MonoBehaviour
{
    const string Muted = "#9AA3AD";

    [SerializeField] TMP_Text label;

    UrlParams p;
    GameState state;

    public void Show(UrlParams p)
    {
        this.p = p;
        gameObject.SetActive(true);
        Refresh();
    }

    public void SetState(GameState state)
    {
        this.state = state;
        Refresh();
    }

    void Refresh()
    {
        if (p == null)
            return;
        string g = p.RawGender == "" ? "–" : p.RawGender;
        label.text = $"<b>DEBUG</b>   {state}\n"
            + $"<color={Muted}>pid</color>  {p.Pid}\n"
            + $"<color={Muted}>Umfrage</color>  g {g} → {p.AvatarGender}   s {p.Species}   {p.Language}   {(p.IsTest ? "Test" : "echt")}";
    }
}
