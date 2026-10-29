using UnityEngine;

// Runs the whole sequence: Init -> Tutorial -> DoorOpen -> WalkToNPC -> Dialog -> Submit -> Redirect.
public class GameFlow : MonoBehaviour
{
    [SerializeField] UIManager ui;
    [SerializeField] DebugOverlay debugOverlay;
    [SerializeField] UltimatumController ultimatum;
    [SerializeField] TutorialController tutorial;
    [SerializeField] PlayerActions playerActions;
    [SerializeField] ActionButtonsUI actionButtons;
    [SerializeField] Door door;
    [SerializeField] WorldButton doorButton;
    [SerializeField] WalkPath walkPath;

    [Tooltip("Used in the Editor instead of the browser URL, e.g. ?pid=R_test&g=w&s=2&debug=1")]
    [SerializeField] string editorQuery = "";

    [Tooltip("Minimum time the end text stays visible before the redirect.")]
    [SerializeField] float minEndSeconds = 1.5f;

    public GameState State { get; private set; }

    Config config;
    UrlParams urlParams;
    SessionData session;
    IResultSubmitter submitter;

    async void Start()
    {
        urlParams = UrlParams.Parse(GetUrl(), new System.Random());
        if (urlParams.Debug)
            debugOverlay.Show(urlParams);

        SetState(GameState.Init);
        config = await ConfigLoader.LoadAsync();
        session = new SessionData(urlParams, config.version, Application.isMobilePlatform, Time.realtimeSinceStartupAsDouble);
        submitter = new ResultSubmitter(config.appsScriptUrl);
        playerActions.TurnAngle = config.turnAngle;

        if (!urlParams.Debug)
        {
            SetState(GameState.Tutorial);
            await tutorial.Run(config);
        }

        // The player may still be facing a mirror, so the arrows stay available to turn towards the door.
        SetState(GameState.DoorOpen);
        actionButtons.Show(config.texts, withJump: false);
        ui.ShowInstruction(config.texts.doorOpened);
        await door.Open();
        await doorButton.WaitForClick(config.texts.goToDoor);
        actionButtons.Hide();
        ui.HideInstruction();
        while (playerActions.IsBusy)
            await Awaitable.NextFrameAsync();

        SetState(GameState.WalkToNPC);
        await walkPath.Walk(playerActions.transform);
        await ui.ShowButton(config.texts.talk);

        SetState(GameState.Dialog);
        await ultimatum.Run(config, session);

        SetState(GameState.Submit);
        session.Finish(Time.realtimeSinceStartupAsDouble);
        ui.ShowMessage(config.texts.end);
        double endShownAt = Time.realtimeSinceStartupAsDouble;
        var fields = session.ToFields();

        if (urlParams.Debug)
            Debug.Log("Debug mode, not submitting: " + ResultJson.Build(fields));
        else
            await submitter.Submit(fields, config.submitTimeoutSeconds);

        // Keep the end text readable even when the POST returns instantly.
        float remaining = minEndSeconds - (float)(Time.realtimeSinceStartupAsDouble - endShownAt);
        if (remaining > 0)
            await Awaitable.WaitForSecondsAsync(remaining);

        SetState(GameState.Redirect);
        Redirector.Go(Redirector.BuildUrl(config.returnUrlTemplate, fields));
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
