using UnityEngine;

// Runs the whole sequence: Init -> Tutorial -> GoToDoor -> KeyRoom -> LeverRoom -> WalkToNPC -> Dialog -> Submit -> Redirect.
// Until the dialog the player can go back through every door.
public class GameFlow : MonoBehaviour
{
    [SerializeField] UIManager ui;
    [SerializeField] DebugOverlay debugOverlay;
    [SerializeField] OrientationOverlay orientationOverlay;
    [SerializeField] DebugAvatarSwitcher avatarSwitcher;
    [SerializeField] DebugRoomSwitcher roomSwitcher;
    [SerializeField] DebugUiSwitcher uiSwitcher;
    [SerializeField] RoomVariants rooms;
    [SerializeField] UiStyles uiStyles;
    [SerializeField] UltimatumController ultimatum;
    [SerializeField] TutorialController tutorial;
    [SerializeField] PlayerActions playerActions;
    [SerializeField] ActionButtonsUI actionButtons;
    [Tooltip("Room1..Room4 and their mirror worlds (same order, null = none). Only the rooms in use are active:" +
             " Room 4 overlaps the mirror world behind Room 1's bottom mirror.")]
    [SerializeField] GameObject[] roomGroups;
    [SerializeField] GameObject[] roomMirrors;
    [Tooltip("Room1..Room4.")]
    [SerializeField] RoomNav[] navs;
    [Tooltip("doors[i] leads from room i+1 to room i+2; walksToDoor[i] walks up to it, walksInto[i] into the next room" +
             " (its first point is the doorway).")]
    [SerializeField] Door[] doors;
    [SerializeField] WalkPath[] walksToDoor;
    [SerializeField] WalkPath[] walksInto;
    [SerializeField] WorldButton doorButton;
    [SerializeField] Transform doorSpot;
    [SerializeField] KeyRoom keyRoom;
    [SerializeField] LeverRoom leverRoom;
    [SerializeField] WorldButton npcButton;
    [SerializeField] Transform npcSpot;
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
        ShowRooms(0, 1);
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
        config.UseLanguage(urlParams.Language);
        session = new SessionData(urlParams, config.version, Application.isMobilePlatform, Time.realtimeSinceStartupAsDouble);
        submitter = new ResultSubmitter(config.appsScriptUrl);
        playerActions.TurnAngle = config.turnAngle;
        orientationOverlay.SetText(config.texts.rotateDevice);

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

        // The player walks around (arrows on the floor) and uses doors, key, levers from the spots in front of them,
        // back and forth through the doors, until talking to the NPC in Room 4.
        var states = new[] { GameState.GoToDoor, GameState.KeyRoom, GameState.LeverRoom, GameState.WalkToNPC };
        int room = 0;
        navs[0].Enter();
        ui.ShowInstruction(config.texts.goToDoorHint);
        while (true)
        {
            SetState(states[room]);
            bool forward = await RunRoom(room);
            if (forward && room == 3)
                break;
            if (forward)
                await GoThrough(room++);
            else
                await GoBack(room--);
            navs[room].Enter(back: !forward);
        }
        npc.LookAt(cameraAim.transform);
        cameraAim.Aim(npc.Face);

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

    // True: on through the room's door (Room 4: talk to the NPC); false: back through the door it was entered by.
    Awaitable<bool> RunRoom(int room) => room switch
    {
        0 => Room1(),
        1 => keyRoom.Run(config.texts),
        2 => leverRoom.Run(config.texts),
        _ => Room4(),
    };

    async Awaitable<bool> Room1()
    {
        actionButtons.Show(config.texts);
        await navs[0].Next(config.texts, (doorButton, config.texts.openDoor, doorSpot));
        actionButtons.Hide();
        ui.HideInstruction();
        await playerActions.StandUp();
        return true;
    }

    // The NPC stands at the back wall: walk up to it, then talk.
    async Awaitable<bool> Room4()
    {
        actionButtons.Show(config.texts);
        var clicked = await navs[3].Next(config.texts, (npcButton, config.texts.talk, npcSpot));
        actionButtons.Hide();
        await playerActions.StandUp();
        return clicked == npcButton;
    }

    // Walks up to the door, through it into the next room and closes it behind the player.
    async Awaitable GoThrough(int from)
    {
        Transform rig = playerActions.transform;
        cameraAim.Look(null);
        await walksToDoor[from].Walk(rig);
        ShowRooms(from, from + 1);
        await doors[from].Open();
        await walksInto[from].Walk(rig);
        await doors[from].Close();
        ShowRooms(from + 1, from + 1);
    }

    // Back through the door the room was entered by, to the spot in front of it in the previous room, facing into that room.
    async Awaitable GoBack(int from)
    {
        Transform rig = playerActions.transform;
        cameraAim.Look(null);
        ShowRooms(from - 1, from);
        await doors[from - 1].Open();
        var into = walksInto[from - 1];
        await into.WalkTo(rig, false, into.First, navs[from - 1].ExitSpot);
        await doors[from - 1].Close();
        ShowRooms(from - 1, from - 1);
    }

    void ShowRooms(int first, int last)
    {
        for (int i = 0; i < roomGroups.Length; i++)
        {
            bool active = i >= first && i <= last;
            roomGroups[i].SetActive(active);
            if (roomMirrors[i] != null)
                roomMirrors[i].SetActive(active);
        }
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
