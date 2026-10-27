using UnityEngine;

// Runs the whole sequence: Init -> Tutorial -> DoorOpen -> WalkToNPC -> Dialog -> Submit -> Redirect.
public class GameFlow : MonoBehaviour
{
    [SerializeField] UIManager ui;
    [SerializeField] DebugOverlay debugOverlay;

    [Tooltip("Used in the Editor instead of the browser URL, e.g. ?pid=R_test&g=w&s=2&debug=1")]
    [SerializeField] string editorQuery = "";

    public GameState State { get; private set; }

    Config config;
    UrlParams urlParams;
    SessionData session;

    async void Start()
    {
        urlParams = UrlParams.Parse(GetUrl(), new System.Random());
        if (urlParams.Debug)
            debugOverlay.Show(urlParams);

        SetState(GameState.Init);
        config = await ConfigLoader.LoadAsync();
        session = new SessionData(urlParams, config.version, Application.isMobilePlatform, Time.realtimeSinceStartupAsDouble);

        if (!urlParams.Debug)
        {
            SetState(GameState.Tutorial);
            await ui.ShowPrompt("Tutorial (Platzhalter)", "Weiter");
        }

        SetState(GameState.DoorOpen);
        await ui.ShowPrompt(config.texts.doorOpened, config.texts.goToDoor);

        SetState(GameState.WalkToNPC);
        await ui.ShowPrompt(config.texts.npcGreeting, config.texts.talk);

        SetState(GameState.Dialog);
        await ui.ShowPrompt("Dialog (Platzhalter)", "Weiter");

        SetState(GameState.Submit);
        session.Finish(Time.realtimeSinceStartupAsDouble);
        ui.ShowMessage(config.texts.end);
        Debug.Log("Submit (placeholder): " + string.Join("&", session.ToFields().ConvertAll(f => $"{f.Key}={f.Value}")));

        SetState(GameState.Redirect);
    }

    string GetUrl()
    {
        string url = Application.absoluteURL;
        return string.IsNullOrEmpty(url) ? editorQuery : url;
    }

    void SetState(GameState state)
    {
        State = state;
        debugOverlay.SetState(state);
    }
}
