using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

// Builds Rooms 2-4 next to Room 1 in Main.unity after the thesis author's sketch (2026-10-31):
// Room 1 (mirrors, tutorial) -> Room 2 (key, mirror +x) -> Room 3 (levers, mirror -z) -> Room 4 (NPC).
// Room 2 sits at +x of Room 1, Room 3 below Room 2, Room 4 below Room 1. Every room has its own walls
// (0.1 m on each side of a shared wall), so each mirror copy of a room is complete. Doors live under World/Doors,
// the key and levers under World/Interactive: neither is part of a room copy (the mirror worlds sync the doors).
// Every room gets walking spots with arrows on the floor (World/Nav, RoomNav). The NPC stands at the back wall of Room 4.
// Wires GameFlow, KeyRoom, LeverRoom, UltimatumController, AvatarSpawner and the mirror worlds. Safe to rerun.
// Menu: Mirror Game/Build Rooms, then Mirror Game/Build Room Variants (furnishes all rooms).
public static class RoomBuilder
{
    const float Half = 3f, T = 0.1f, H = 3f, DoorHalfW = 0.6f, DoorH = 2.2f;
    const float MirrorW = 1.5f, MirrorH = 2.4f, MirrorBottom = 0.15f, FrameW = 0.06f, MirrorT = 0.02f;
    static readonly Vector3 R2 = new Vector3(6.2f, 0f, 0f), R3 = new Vector3(6.2f, 0f, -6.2f), R4 = new Vector3(0f, 0f, -6.2f);
    // Lever pivots from the viewer's left to right (viewer faces +x, so left is +z). Up = true.
    static readonly float[] LeverZ = { 0.4f, 0f, -0.4f };
    static readonly bool[] Solution = { false, true, true };

    enum Wall { Solid, Door, Mirror }

    static Material wallMat, floorMat, ceilMat, doorMat, frameMat, darkMat, glassMat;

    [MenuItem("Mirror Game/Build Rooms")]
    public static void Build()
    {
        wallMat = Load("Wall"); floorMat = Load("Floor"); ceilMat = Load("Ceiling"); doorMat = Load("Door");
        frameMat = Load("MirrorFrame"); darkMat = Load("MirrorDark"); glassMat = Load("MirrorGlass");
        var gold = Mat("Key", new Color(0.95f, 0.75f, 0.3f), 0.6f, 1f);
        var iron = Mat("LeverIron", new Color(0.12f, 0.12f, 0.13f), 0.3f, 0.5f);
        var red = Mat("LeverRed", new Color(0.55f, 0.04f, 0.09f), 0.3f);
        var knob = Mat("LeverKnob", new Color(0.72f, 0.48f, 0.34f), 0.2f);
        var board = Mat("Sign", new Color(1f, 0.65f, 0.38f), 0.1f);

        var world = GameObject.Find("World").transform;
        var room1 = world.Find("Room1");
        // The NPC is a scene root at first, under Room 4 after a build: take it out before Room 4 is rebuilt.
        var npc = world.Find("Room4/NPC");
        if (npc == null)
            npc = System.Array.Find(world.gameObject.scene.GetRootGameObjects(), g => g.name == "NPC").transform;
        npc.SetParent(null, true);
        foreach (var name in new[] { "Room2", "Room3", "Room4", "Interactive", "WalkToNpc", "WalkIntoRoom2", "WalkToKey",
                     "WalkToDoor2", "WalkIntoRoom3", "WalkToLevers", "WalkToDoor3", "WalkIntoRoom4", "Door2Button", "Door3Button",
                     "KeyButton", "Lever1Button", "Lever2Button", "Lever3Button", "NpcButton", "Nav", "Door1BackButton", "Door2BackButton",
                     "Door3BackButton" })
            Remove(world, name);
        var uiRoot = GameObject.Find("UI").transform;
        Remove(uiRoot, "NavArrows");

        // Door 1 moves out of Room 1, so it is shared by the copies of Room 1 and Room 2.
        var doors = Child(world, "Doors");
        var oldHinge = room1.Find("DoorHinge");
        if (oldHinge != null)
        {
            oldHinge.SetParent(doors, true);
            oldHinge.name = "Door1";
        }
        var door1 = doors.Find("Door1").GetComponent<Door>();
        Remove(doors, "Door2");
        Remove(doors, "Door3");
        var door2 = DoorAt(doors, "Door2", new Vector3(R2.x - DoorHalfW, 0f, -Half - T), 90f);
        var door3 = DoorAt(doors, "Door3", new Vector3(Half + T, 0f, R3.z + DoorHalfW), 180f);

        // Room 1's door wall gets the room-own thickness (0.1 m), Room 2 has its own wall behind it.
        foreach (var n in new[] { "WallDoorA", "WallDoorB", "WallDoorLintel" })
        {
            var w = room1.Find(n);
            w.localPosition = new Vector3(Half + T / 2f, w.localPosition.y, w.localPosition.z);
            w.localScale = new Vector3(T, w.localScale.y, w.localScale.z);
        }
        Remove(room1, "FloorSill");
        Sill(room1, new Vector3(Half + T / 2f, 0f, 0f), 90f);

        var room2 = Room(world, "Room2", R2, plusX: Wall.Mirror, minusX: Wall.Door, plusZ: Wall.Solid, minusZ: Wall.Door);
        var room3 = Room(world, "Room3", R3, plusX: Wall.Solid, minusX: Wall.Door, plusZ: Wall.Door, minusZ: Wall.Mirror);
        var room4 = Room(world, "Room4", R4, plusX: Wall.Door, minusX: Wall.Solid, plusZ: Wall.Solid, minusZ: Wall.Solid);
        var interactive = Child(world, "Interactive");

        // Room 2: hook board on the +z wall, the key hangs on it.
        float keyY = 1.35f, wallZ = Half;
        Box("KeyBoard", room2, new Vector3(R2.x, keyY, wallZ - 0.015f), new Vector3(0.45f, 0.5f, 0.03f), board);
        Box("KeyHook", room2, new Vector3(R2.x, keyY + 0.14f, wallZ - 0.05f), new Vector3(0.03f, 0.03f, 0.06f), iron);
        var key = Child(interactive, "Key");
        key.position = new Vector3(R2.x, keyY, wallZ - 0.06f);
        Prim(PrimitiveType.Cylinder, "Bow", key, new Vector3(0f, 0.08f, 0f), new Vector3(0.14f, 0.012f, 0.14f), gold).localRotation = Quaternion.Euler(90f, 0f, 0f);
        Box("Shaft", key, new Vector3(0f, -0.07f, 0f), new Vector3(0.025f, 0.22f, 0.025f), gold, local: true);
        Box("Bit1", key, new Vector3(0.035f, -0.14f, 0f), new Vector3(0.05f, 0.025f, 0.02f), gold, local: true);
        Box("Bit2", key, new Vector3(0.03f, -0.095f, 0f), new Vector3(0.04f, 0.025f, 0.02f), gold, local: true);

        // Room 3: lever plate on the +x wall, sign with the solution above it.
        float plateX = R3.x + Half, pivotY = 0.95f;
        Box("LeverPlate", room3, new Vector3(plateX - 0.02f, pivotY, R3.z), new Vector3(0.04f, 0.55f, 1.3f), iron);
        Box("Sign", room3, new Vector3(plateX - 0.015f, 1.47f, R3.z), new Vector3(0.03f, 0.32f, 1.3f), board);
        // Door symbol on the left of the sign stands for "Türöffnung".
        Box("SignDoor", room3, new Vector3(plateX - 0.035f, 1.47f, R3.z + 0.5f), new Vector3(0.01f, 0.22f, 0.13f), knob);
        var levers = new Lever[3];
        var mini = new Vector3[3];
        for (int i = 0; i < 3; i++)
        {
            Box("Slot" + (i + 1), room3, new Vector3(plateX - 0.045f, pivotY, R3.z + LeverZ[i]), new Vector3(0.02f, 0.42f, 0.07f), iron);
            levers[i] = LeverAt(interactive, "Lever" + (i + 1), new Vector3(plateX - 0.06f, pivotY, R3.z + LeverZ[i]), 0.26f, red, knob, false);
            mini[i] = new Vector3(plateX - 0.035f, 1.47f, R3.z + 0.18f - i * 0.22f);
            var sign = LeverAt(room3, "SignLever" + (i + 1), mini[i], 0.1f, red, knob, Solution[i]);
            Object.DestroyImmediate(sign); // static picture, no Lever component needed
        }

        // NPC at the back wall of Room 4, facing the door to Room 3. Hidden with the room (it is inside the bottom mirror copy of Room 1).
        npc.SetParent(room4, true);
        npc.SetPositionAndRotation(new Vector3(R4.x - 2.4f, 0f, R4.z), Quaternion.Euler(0f, 90f, 0f));

        // Mirror worlds grouped per room, so GameFlow can switch them with their room.
        var mirrorRoot = world.Find("MirrorWorlds");
        var mw1 = Child(mirrorRoot, "Room1");
        foreach (var n in new[] { "MirrorWorldLeft", "MirrorWorldTop", "MirrorWorldBottom" })
        {
            var t = mirrorRoot.Find(n);
            if (t != null)
                t.SetParent(mw1, true);
        }
        foreach (var w in mw1.GetComponentsInChildren<MirrorWorld>(true))
            Set(w, "sharedParts", new Object[] { door1.transform });
        Remove(mirrorRoot, "Room2");
        Remove(mirrorRoot, "Room3");
        var mw2 = Child(mirrorRoot, "Room2");
        var mw3 = Child(mirrorRoot, "Room3");
        MirrorWorldAt(mw2, "MirrorWorldRight", R2, 90f, room2, door1.transform, door2.transform);
        MirrorWorldAt(mw3, "MirrorWorldBottom", R3, 180f, room3, door2.transform, door3.transform);

        // Walks through the doors: Room 1 -> Room 2 centre facing its mirror -> Room 3 centre facing its mirror
        // -> just inside Room 4, facing the NPC. Each starts at the door spot of the room navigation.
        var walkToDoor = world.Find("WalkToDoor").GetComponent<WalkPath>();
        var intoRoom2 = Path(world, "WalkIntoRoom2", (new Vector3(Half + T, 0, 0), 90f), (R2, 90f));
        var toDoor2 = Path(world, "WalkToDoor2", (R2 + new Vector3(0, 0, -2f), 180f));
        var intoRoom3 = Path(world, "WalkIntoRoom3", (new Vector3(R2.x, 0, -Half - T), 180f), (R3, 180f));
        var toDoor3 = Path(world, "WalkToDoor3", (R3 + new Vector3(-2f, 0, 0), 270f));
        var intoRoom4 = Path(world, "WalkIntoRoom4", (new Vector3(Half + T, 0, R4.z), 270f), (R4 + new Vector3(1.8f, 0, 0), 270f));

        // Tappable objects: copies of the styled door button of Room 1.
        var doorButton = world.Find("DoorButton");
        // Door tags at mid height (like Door 1, see UiStyleBuilder), so small avatars (eyes at 0.85 m) see them too.
        const float DoorTagY = -1100;
        var door2Button = WorldButtonAt(doorButton, "Door2Button", new Vector3(R2.x, 0f, -Half + 0.02f), 180f, 0.001f, new Vector2(1200, 2200), DoorTagY);
        var door3Button = WorldButtonAt(doorButton, "Door3Button", new Vector3(Half + T + 0.12f, 0f, R3.z), 270f, 0.001f, new Vector2(1200, 2200), DoorTagY);
        var keyButton = WorldButtonAt(doorButton, "KeyButton", new Vector3(R2.x, keyY - 0.3f, wallZ - 0.08f), 0f, 0.0005f, new Vector2(1100, 1200), -1250);
        var leverButtons = new WorldButton[3];
        for (int i = 0; i < 3; i++)
            leverButtons[i] = WorldButtonAt(doorButton, "Lever" + (i + 1) + "Button", new Vector3(plateX - 0.15f, pivotY - 0.33f, R3.z + LeverZ[i]),
                90f, 0.0005f, new Vector2(720, 1320), -520);

        // The same doors from the other side, to go back.
        var door1Back = WorldButtonAt(doorButton, "Door1BackButton", new Vector3(Half + T + 0.12f, 0f, 0f), 270f, 0.001f, new Vector2(1200, 2200), DoorTagY);
        var door2Back = WorldButtonAt(doorButton, "Door2BackButton", new Vector3(R2.x, 0f, -Half - T - 0.12f), 0f, 0.001f, new Vector2(1200, 2200), DoorTagY);
        var door3Back = WorldButtonAt(doorButton, "Door3BackButton", new Vector3(Half - 0.02f, 0f, R4.z), 90f, 0.001f, new Vector2(1200, 2200), DoorTagY);

        var npcButton = WorldButtonAt(doorButton, "NpcButton", new Vector3(R4.x - 2.1f, 0f, R4.z), 270f, 0.001f, new Vector2(900, 2000), -1150);

        // Walking spots per room, spot 0 = home. Door spots: where the walk through the door starts.
        var nav = Child(world, "Nav");
        var walker = nav.gameObject.AddComponent<WalkPath>();
        var navUi = UiNode("NavArrows", uiRoot);
        navUi.anchorMin = Vector2.zero;
        navUi.anchorMax = Vector2.one;
        navUi.sizeDelta = Vector2.zero;
        navUi.SetSiblingIndex(0); // below panels and buttons
        var arrowTemplate = ArrowTemplate(navUi);
        var spot = new System.Collections.Generic.Dictionary<string, Transform>();
        RoomNav Nav(string name, params (string id, Vector3 pos, float yaw)[] spots)
        {
            var root = Child(nav, name);
            var n = root.gameObject.AddComponent<RoomNav>();
            var spotList = new Object[spots.Length];
            var arrows = new Object[spots.Length];
            for (int i = 0; i < spots.Length; i++)
            {
                var t = Child(root, "Spot_" + spots[i].id);
                t.SetPositionAndRotation(spots[i].pos, Quaternion.Euler(0, spots[i].yaw, 0));
                spot[name + "/" + spots[i].id] = t;
                spotList[i] = t;
                var arrow = Object.Instantiate(arrowTemplate, navUi);
                arrow.name = name + "_" + spots[i].id;
                arrows[i] = arrow.GetComponent<WorldButton>();
            }
            Set(n, "spots", spotList);
            Set(n, "arrows", arrows);
            return n;
        }
        var nav1 = Nav("Room1", ("Home", Vector3.zero, 90f), ("MirrorLeft", new Vector3(-1.8f, 0, 0), 270f),
            ("MirrorTop", new Vector3(0, 0, 1.8f), 0f), ("MirrorBottom", new Vector3(0, 0, -1.8f), 180f),
            ("Door", new Vector3(1.5f, 0, 0), 90f));
        var nav2 = Nav("Room2", ("Home", R2, 90f), ("Mirror", R2 + new Vector3(1.8f, 0, 0), 90f),
            ("Key", R2 + new Vector3(0, 0, 1.6f), 0f), ("Back", R2 + new Vector3(-1.5f, 0, 0), 270f), ("Door", R2 + new Vector3(0, 0, -1.5f), 180f));
        var nav3 = Nav("Room3", ("Home", R3, 180f), ("Mirror", R3 + new Vector3(0, 0, -1.8f), 180f),
            ("Levers", R3 + new Vector3(1.2f, 0, 0), 90f), ("Back", R3 + new Vector3(0, 0, 1.5f), 0f), ("Door", R3 + new Vector3(-1.5f, 0, 0), 270f));
        var nav4 = Nav("Room4", ("Home", R4 + new Vector3(1.8f, 0, 0), 270f), ("Npc", R4 + new Vector3(-1f, 0, 0), 270f));
        Object.DestroyImmediate(arrowTemplate);

        // Wiring.
        var game = GameObject.Find("Game");
        var flow = game.GetComponent<GameFlow>();
        var ui = Object.FindFirstObjectByType<UIManager>(FindObjectsInactive.Include);
        var actions = Object.FindFirstObjectByType<ActionButtonsUI>(FindObjectsInactive.Include);
        var player = Object.FindFirstObjectByType<PlayerActions>();
        var stepDone = Object.FindFirstObjectByType<StepDoneUI>(FindObjectsInactive.Include);

        foreach (var n in new[] { nav1, nav2, nav3, nav4 })
            Set(n, ("player", player), ("actionButtons", actions), ("walker", walker));
        Set(nav1, ("exitSpot", spot["Room1/Door"]));
        Set(nav2, ("backDoor", door1Back), ("backSpot", spot["Room2/Back"]), ("exitSpot", spot["Room2/Door"]));
        Set(nav3, ("backDoor", door2Back), ("backSpot", spot["Room3/Back"]), ("exitSpot", spot["Room3/Door"]));
        // Room 4 is entered right next to the door: it is tappable from home.
        Set(nav4, ("backDoor", door3Back), ("backSpot", spot["Room4/Home"]));
        var keyRoom = GetOrAdd<KeyRoom>(game);
        Set(keyRoom, ("ui", ui), ("actionButtons", actions), ("player", player), ("stepDone", stepDone), ("nav", nav2),
            ("doorButton", door2Button), ("doorSpot", spot["Room2/Door"]), ("keyButton", keyButton), ("keySpot", spot["Room2/Key"]),
            ("key", key.gameObject));
        var leverRoom = GetOrAdd<LeverRoom>(game);
        Set(leverRoom, ("ui", ui), ("actionButtons", actions), ("player", player), ("stepDone", stepDone), ("nav", nav3),
            ("doorButton", door3Button), ("doorSpot", spot["Room3/Door"]), ("leverSpot", spot["Room3/Levers"]));
        Set(leverRoom, "levers", levers);
        Set(leverRoom, "leverButtons", leverButtons);
        var so = new SerializedObject(leverRoom);
        var sol = so.FindProperty("solution");
        sol.arraySize = Solution.Length;
        for (int i = 0; i < Solution.Length; i++)
            sol.GetArrayElementAtIndex(i).boolValue = Solution[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        Set(flow, ("doorSpot", spot["Room1/Door"]), ("keyRoom", keyRoom), ("leverRoom", leverRoom),
            ("npcButton", npcButton), ("npcSpot", spot["Room4/Npc"]));
        Set(flow, "navs", new Object[] { nav1, nav2, nav3, nav4 });
        Set(flow, "doors", new Object[] { door1, door2, door3 });
        Set(flow, "walksToDoor", new Object[] { walkToDoor, toDoor2, toDoor3 });
        Set(flow, "walksInto", new Object[] { intoRoom2, intoRoom3, intoRoom4 });
        Set(Object.FindFirstObjectByType<UltimatumController>(FindObjectsInactive.Include), ("npc", npc.GetComponentInChildren<NpcLookAt>()));
        Set(flow, "roomGroups", new Object[] { room1.gameObject, room2.gameObject, room3.gameObject, room4.gameObject });
        Set(flow, "roomMirrors", new Object[] { mw1.gameObject, mw2.gameObject, mw3.gameObject, null });
        Set(player, ("eyes", Camera.main.transform));
        Set(game.GetComponent<AvatarSpawner>(), "mirrorWorlds", mirrorRoot.GetComponentsInChildren<MirrorWorld>(true));

        var scene = world.gameObject.scene;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Rooms 2-4 built. Run Mirror Game/Build Room Variants next.");
    }

    // ---------------------------------------------------------------- shell

    static Transform Room(Transform world, string name, Vector3 centre, Wall plusX, Wall minusX, Wall plusZ, Wall minusZ)
    {
        var room = Child(world, name);
        room.position = centre;
        Box("Floor", room, new Vector3(0, -0.05f, 0), new Vector3(2 * Half, 0.1f, 2 * Half), floorMat, local: true);
        Box("Ceiling", room, new Vector3(0, H + 0.05f, 0), new Vector3(2 * Half, 0.1f, 2 * Half), ceilMat, local: true);
        Side(room, "PlusZ", 0f, plusZ);
        Side(room, "PlusX", 90f, plusX);
        Side(room, "MinusZ", 180f, minusZ);
        Side(room, "MinusX", 270f, minusX);
        return room;
    }

    // One wall, built facing +z (inner face at z = Half) and turned by yaw around the room centre.
    static void Side(Transform room, string side, float yaw, Wall kind)
    {
        var g = Child(room, "Side" + side);
        g.localRotation = Quaternion.Euler(0, yaw, 0);
        float z = Half + T / 2f, full = Half + T;
        if (kind == Wall.Solid)
        {
            Box("Wall" + side, g, new Vector3(0, H / 2, z), new Vector3(2 * full, H, T), wallMat, local: true);
            return;
        }
        if (kind == Wall.Door)
        {
            float w = full - DoorHalfW;
            Box("Wall" + side + "A", g, new Vector3(-(DoorHalfW + w / 2), H / 2, z), new Vector3(w, H, T), wallMat, local: true);
            Box("Wall" + side + "B", g, new Vector3(DoorHalfW + w / 2, H / 2, z), new Vector3(w, H, T), wallMat, local: true);
            Box("Wall" + side + "Lintel", g, new Vector3(0, (H + DoorH) / 2, z), new Vector3(2 * DoorHalfW, H - DoorH, T), wallMat, local: true);
            Sill(g, new Vector3(0, 0, z), 0f);
            return;
        }
        // Body-sized mirror opening with frame and glass, thin walls around it (like Room 1).
        float e = Half + MirrorT, mz = Half + MirrorT / 2, mw = MirrorW / 2, top = MirrorBottom + MirrorH;
        Box("Wall" + side + "Left", g, new Vector3(-(e + mw) / 2, H / 2, mz), new Vector3(e - mw, H, MirrorT), wallMat, local: true);
        Box("Wall" + side + "Right", g, new Vector3((e + mw) / 2, H / 2, mz), new Vector3(e - mw, H, MirrorT), wallMat, local: true);
        Box("Wall" + side + "Below", g, new Vector3(0, MirrorBottom / 2, mz), new Vector3(MirrorW, MirrorBottom, MirrorT), wallMat, local: true);
        Box("Wall" + side + "Above", g, new Vector3(0, (top + H) / 2, mz), new Vector3(MirrorW, H - top, MirrorT), wallMat, local: true);
        float cy = MirrorBottom + MirrorH / 2, fz = Half - 0.02f, ow = MirrorW + 2 * FrameW;
        Box("FrameTop", g, new Vector3(0, top + FrameW / 2, fz), new Vector3(ow, FrameW, 0.04f), frameMat, local: true);
        Box("FrameBottom", g, new Vector3(0, MirrorBottom - FrameW / 2, fz), new Vector3(ow, FrameW, 0.04f), frameMat, local: true);
        Box("FrameLeft", g, new Vector3(-(MirrorW + FrameW) / 2, cy, fz), new Vector3(FrameW, MirrorH, 0.04f), frameMat, local: true);
        Box("FrameRight", g, new Vector3((MirrorW + FrameW) / 2, cy, fz), new Vector3(FrameW, MirrorH, 0.04f), frameMat, local: true);
        var glass = Box("Glass", g, new Vector3(0, cy, Half + 0.003f), new Vector3(MirrorW, MirrorH, 0.002f), glassMat, local: true);
        var mirror = g.gameObject.AddComponent<MirrorWall>();
        Set(mirror, ("glass", glass.GetComponent<MeshRenderer>()), ("darkMaterial", darkMat));
        g.name = "MirrorWall" + side;
    }

    // Floor under the doorway, as deep as the room's own wall: the floors of both rooms end at their walls.
    static void Sill(Transform parent, Vector3 localPos, float yaw)
    {
        var t = Box("FloorSill", parent, localPos + new Vector3(0, -0.05f, 0), new Vector3(2 * DoorHalfW, 0.1f, T), floorMat, local: true);
        t.localRotation = Quaternion.Euler(0, yaw, 0);
    }

    static Door DoorAt(Transform doors, string name, Vector3 pos, float yaw)
    {
        var hinge = Child(doors, name);
        hinge.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
        var leaf = Box("DoorLeaf", hinge, new Vector3(0, DoorH / 2, DoorHalfW), new Vector3(0.06f, DoorH - 0.01f, 2 * DoorHalfW - 0.02f), doorMat, local: true);
        GameObjectUtility.SetStaticEditorFlags(leaf.gameObject, 0);
        return hinge.gameObject.AddComponent<Door>();
    }

    static void MirrorWorldAt(Transform parent, string name, Vector3 centre, float yaw, Transform room, params Transform[] shared)
    {
        var t = Child(parent, name);
        t.rotation = Quaternion.Euler(0, yaw, 0);
        t.position = centre + t.rotation * new Vector3(0, 0, Half);
        t.localScale = new Vector3(1, 1, -1);
        var mw = t.gameObject.AddComponent<MirrorWorld>();
        Set(mw, ("room", room), ("ownMirror", room.GetComponentInChildren<MirrorWall>()));
        Set(mw, "sharedParts", shared);
    }

    // Pivot with local x along the wall and z out of it (wall at +x, facing -x), handle along local +y.
    static Lever LeverAt(Transform parent, string name, Vector3 pos, float length, Material bar, Material knob, bool up)
    {
        var pivot = Child(parent, name);
        pivot.SetPositionAndRotation(pos, Quaternion.Euler(0, -90, 0));
        Box("Bar", pivot, new Vector3(0, length / 2, 0), new Vector3(length * 0.16f, length, length * 0.16f), bar, local: true);
        Prim(PrimitiveType.Sphere, "Knob", pivot, new Vector3(0, length, 0), Vector3.one * length * 0.32f, knob);
        var lever = pivot.gameObject.AddComponent<Lever>();
        Set(lever, "startUp", up);
        pivot.localRotation = Quaternion.Euler(0, -90, 0) * Quaternion.Euler(up ? 35f : 145f, 0, 0);
        return lever;
    }

    // Copy of the door button, pivot at the bottom centre. tagY: tag position from the top of the area (canvas units).
    static WorldButton WorldButtonAt(Transform template, string name, Vector3 pos, float yaw, float scale, Vector2 size, float tagY)
    {
        var go = Object.Instantiate(template.gameObject, template.parent);
        go.name = name;
        var rt = (RectTransform)go.transform;
        rt.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
        rt.localScale = Vector3.one * scale;
        rt.sizeDelta = size;
        var tag = (RectTransform)rt.Find("Button/Tag");
        tag.anchoredPosition = new Vector2(0, tagY);
        go.SetActive(false);
        return go.GetComponent<WorldButton>();
    }

    // Screen button for walking, styled per UI style (skin keys Nav, Nav.Icon). RoomNav keeps it over a floor point.
    static GameObject ArrowTemplate(Transform parent)
    {
        var root = UiNode("Arrow", parent);
        root.sizeDelta = new Vector2(170, 170);
        var hit = UiNode("Button", root);
        hit.sizeDelta = new Vector2(170, 170);
        var button = hit.gameObject.AddComponent<Button>();
        button.targetGraphic = hit.gameObject.AddComponent<Image>();
        button.transition = Selectable.Transition.None;
        SkinKey(hit, "Nav");
        var icon = UiNode("Icon", hit);
        icon.anchorMin = Vector2.zero;
        icon.anchorMax = Vector2.one;
        icon.sizeDelta = Vector2.zero;
        icon.gameObject.AddComponent<Image>().raycastTarget = false;
        SkinKey(icon, "Nav.Icon");
        Set(root.gameObject.AddComponent<WorldButton>(), ("button", button));
        root.gameObject.SetActive(false);
        return root.gameObject;
    }

    static RectTransform UiNode(string name, Transform parent)
    {
        var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rt.gameObject.layer = parent.gameObject.layer;
        rt.SetParent(parent, false);
        return rt;
    }

    static void SkinKey(Transform node, string key)
    {
        var skin = new SerializedObject(node.gameObject.AddComponent<UiSkin>());
        skin.FindProperty("key").stringValue = key;
        skin.ApplyModifiedPropertiesWithoutUndo();
    }

    static WalkPath Path(Transform parent, string name, params (Vector3 pos, float yaw)[] points)
    {
        var root = Child(parent, name);
        var walk = root.gameObject.AddComponent<WalkPath>();
        var wps = new Object[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            var wp = Child(root, "Waypoint" + i);
            wp.SetPositionAndRotation(points[i].pos, Quaternion.Euler(0, points[i].yaw, 0));
            wps[i] = wp;
        }
        Set(walk, "waypoints", wps);
        return walk;
    }

    // ---------------------------------------------------------------- helpers

    static Transform Child(Transform parent, string name)
    {
        var t = parent.Find(name);
        if (t != null)
            return t;
        t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
    }

    static void Remove(Transform parent, string name)
    {
        var t = parent.Find(name);
        if (t != null)
            Object.DestroyImmediate(t.gameObject);
    }

    // No static batching: rooms are copied into the mirror worlds at runtime (see CLAUDE.md section 5).
    static Transform Box(string name, Transform parent, Vector3 pos, Vector3 size, Material mat, bool local = false)
    {
        var t = Prim(PrimitiveType.Cube, name, parent, Vector3.zero, size, mat);
        if (local)
            t.localPosition = pos;
        else
            t.position = pos;
        return t;
    }

    static Transform Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        return go.transform;
    }

    static Material Load(string name) => AssetDatabase.LoadAssetAtPath<Material>($"Assets/Materials/{name}.mat");

    static Material Mat(string name, Color color, float smoothness, float metallic = 0f)
    {
        string path = $"Assets/Materials/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Smoothness", smoothness);
        mat.SetFloat("_Metallic", metallic);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static T GetOrAdd<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    static void Set(Object target, params (string field, Object value)[] refs)
    {
        var so = new SerializedObject(target);
        foreach (var (field, value) in refs)
            so.FindProperty(field).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void Set(Object target, string field, bool value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(field).boolValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void Set(Object target, string field, Object[] values)
    {
        var so = new SerializedObject(target);
        var array = so.FindProperty(field);
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
