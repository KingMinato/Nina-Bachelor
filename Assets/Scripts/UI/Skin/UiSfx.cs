using UnityEngine;

// Plays the short UI sounds of the current UiStyle. Sits on the UI canvas next to UiStyles.
// Styles without a clip for a name stay silent.
[RequireComponent(typeof(AudioSource))]
public class UiSfx : MonoBehaviour
{
    public const string Click = "click";
    public const string Turn = "turn";
    public const string Jump = "jump";
    public const string Land = "land";
    public const string Step = "step";
    public const string Door = "door";
    public const string Instruction = "instr";
    public const string Count = "count";
    public const string Done = "done";
    public const string Panel = "panel";
    public const string Type = "type";
    public const string Tick = "tick";
    public const string Coin = "coin";
    public const string End = "end";

    static UiSfx instance;
    AudioSource source;

    void Awake()
    {
        instance = this;
        source = GetComponent<AudioSource>();
    }

    public static void Play(string name)
    {
        if (instance == null || UiStyles.Current == null)
            return;
        var clip = UiStyles.Current.Sound(name);
        if (clip != null)
            instance.source.PlayOneShot(clip);
    }
}
