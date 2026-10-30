using UnityEngine;

// Runs the whole sequence: Init -> Tutorial -> GoToDoor -> WalkToNPC -> Dialog -> Submit -> Redirect.
public class GameFlow : MonoBehaviour
{
    [SerializeField] UIManager ui;
    [SerializeField] DebugOverlay debugOverlay;
    [SerializeField] DebugAvatarSwitcher avatarSwitcher;
    [SerializeField] DebugRoomSwitcher roomSwitcher;
    [SerializeField] DebugUiSwitcher uiSwitcher;
    [SerializeField] RoomVariants rooms;
    [SerializeField] UiStyles uiStyles;
    [SerializeField] UltimatumController ultimatum;
    [SerializeField] TutorialController tutorial;
    [SerializeField] PlayerActions playerActions;
    [SerializeField] ActionButtonsUI actionButtons;
    [SerializeField] Door door;
    [SerializeField] WorldButton doorButton;
    [SerializeField] WalkPath walkToDoor;
    [SerializeField] WalkPath walkToNpc;
    [SerializeField] AvatarSpawner avatarSpawner;
    [SerializeField] NpcLookAt npc;
    [SerializeField] CameraAim cameraAim;

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
        if (urlParams.RoomOverride != "" && !rooms.Apply(urlParams.RoomOverride))
            Debug.LogWarning("Unknown room variant: " + urlParams.RoomOverride);
        if (urlParams.UiOverride != "" && !uiStyles.Apply(urlParams.UiOverride))
            Debug.LogWarning("Unknown UI style: " + urlParams.UiOverride);
        if (urlParams.Debug)
        {
            debugOverlay.ShowRoom(rooms.Current.DisplayName);
            debugOverlay.ShowUi(UiStyles.Current.displayName);
        }

        SetState(GameState.Init);
        config = await ConfigLoader.LoadAsync();
        session = new SessionData(urlParams, config.version, Application.isMobilePlatform, Time.realtimeSinceStartupAsDouble);
        submitter = new ResultSubmitter(config.appsScriptUrl);
        playerActions.TurnAngle = config.turnAngle;

        // Available in the Unity Editor, in test runs (no pid or ?debug=1) and, while config.debugTools is on,
        // also in runs coming from the survey. Turn debugTools off before the pilot.
        if (urlParams.IsTest || config.debugTools || Application.isEditor)
        {
            avatarSwitcher.gameObject.SetActive(true);
            roomSwitcher.gameObject.SetActive(true);
            uiSwitcher.gameObject.SetActive(true);
        }

        string avatarId = urlParams.AvatarOverride != "" ? urlParams.AvatarOverride : config.SpeciesId(urlParams.Species);
        avatarSpawner.Spawn(avatarId, urlParams.AvatarGender);
        if (urlParams.Debug)
            debugOverlay.ShowAvatar(avatarId, urlParams.AvatarGender);

        if (!urlParams.Debug)
        {
            SetState(GameState.Tutorial);
            await tutorial.Run(config);
        }

        // The player may still be facing a mirror, so the arrows stay available to turn towards the closed door.
        SetState(GameState.GoToDoor);
        actionButtons.Show(config.texts, withJump: false);
        ui.ShowInstruction(config.texts.goToDoorHint);
        await doorButton.WaitForClick(config.texts.goToDoor);
        actionButtons.Hide();
        ui.HideInstruction();
        while (playerActions.IsBusy)
            await Awaitable.NextFrameAsync();

        SetState(GameState.WalkToNPC);
        await walkToDoor.Walk(playerActions.transform);
        await door.Open();
        await walkToNpc.Walk(playerActions.transform);
        npc.LookAt(cameraAim.transform);
        cameraAim.Aim(npc.Face);
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
