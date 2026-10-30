using TMPro;
using UnityEngine;

// Only shown with ?debug=1. Displays the current state and the received parameters.
// Starts inactive in the scene, GameFlow activates it.
public class DebugOverlay : MonoBehaviour
{
    [SerializeField] TMP_Text label;

    string paramsLine = "";
    string avatarLine = "";
    string roomLine = "";
    string uiLine = "";
    GameState state;

    public void Show(UrlParams p)
    {
        paramsLine = $"pid={p.Pid}  g='{p.RawGender}'  ag={p.AvatarGender}  s={p.Species}  test={p.IsTest}";
        gameObject.SetActive(true);
    }

    public void ShowAvatar(string speciesId, string gender)
    {
        avatarLine = $"  avatar={speciesId} {gender}";
        Refresh();
    }

    public void ShowRoom(string name)
    {
        roomLine = $"  room={name}";
        Refresh();
    }

    public void ShowUi(string name)
    {
        uiLine = $"  ui={name}";
        Refresh();
    }

    public void SetState(GameState state)
    {
        this.state = state;
        Refresh();
    }

    void Refresh()
    {
        label.text = $"{state}\n{paramsLine}{avatarLine}{roomLine}{uiLine}";
    }
}
