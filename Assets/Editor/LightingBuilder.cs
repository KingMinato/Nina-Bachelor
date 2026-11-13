using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Baked lighting for all four rooms (menu Mirror Game/Bake Lighting, safe to rerun; rerun after Build Rooms or
// Build Room Variants). Each room has its own mood: Room 1 bright (mirror tutorial), Room 2 warm, Room 3 dim with a
// desk lamp, Room 4 calm. Lights are baked only (nothing realtime on mobile): the shell gets lightmaps, which the
// mirror copies keep, everything else is lit by light probes (MirrorProbes mirrors them for the copies).
// Bakes the variant that is active (RoomVariants start variant); the others are not lit correctly.
public static class LightingBuilder
{
    const string SettingsPath = "Assets/Scenes/Main_Lighting.lighting";
    static readonly Vector3[] Centres = { Vector3.zero, new Vector3(6.2f, 0, 0), new Vector3(6.2f, 0, -6.2f), new Vector3(0, 0, -6.2f) };

    // Ceiling panel brightness and colour per room, the rooms' moods.
    static readonly float[] Panel = { 30f, 18f, 7f, 19f };
    static readonly Color[] PanelColour =
    {
        new Color(1f, 0.95f, 0.88f), new Color(1f, 0.85f, 0.66f), new Color(0.9f, 0.93f, 1f), new Color(1f, 0.9f, 0.78f),
    };
    static readonly Color Warm = new Color(1f, 0.78f, 0.52f);

    [MenuItem("Mirror Game/Bake Lighting")]
    public static void BuildAndBake()
    {
        Build();
        Lightmapping.BakeAsync();
        Debug.Log("Lighting set up, baking...");
    }

    public static void Build()
    {
        var world = GameObject.Find("World").transform;

        // The old realtime sun would add to the baked light (and cannot reach into the closed rooms anyway).
        var sun = GameObject.Find("Directional Light");
        if (sun != null)
            sun.SetActive(false);
        RenderSettings.sun = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.16f, 0.15f, 0.14f);

        var old = world.Find("Lighting");
        if (old != null)
            Object.DestroyImmediate(old.gameObject);
        var root = new GameObject("Lighting").transform;
        root.SetParent(world, false);

        for (int i = 0; i < 4; i++)
        {
            var group = new GameObject("Room" + (i + 1)).transform;
            group.SetParent(root, false);
            // Lamps are prefab instances (one light each, even when the model has several meshes).
            foreach (var t in world.Find("Room" + (i + 1)).GetComponentsInChildren<Transform>())
                if (PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject) && IsLamp(t.name))
                    LightFor(t, group, i);
            Probes(group, Centres[i]);
        }

        foreach (var name in new[] { "Room1", "Room2", "Room3", "Room4", "Doors" })
            MarkStatic(world.Find(name));

        var settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(SettingsPath);
        if (settings == null)
        {
            settings = new LightingSettings { name = "Main_Lighting" };
            AssetDatabase.CreateAsset(settings, SettingsPath);
        }
        settings.bakedGI = true;
        settings.realtimeGI = false;
        settings.lightmapper = LightingSettings.Lightmapper.ProgressiveGPU;
        settings.lightmapResolution = 16f;
        settings.lightmapPadding = 4;
        settings.lightmapMaxSize = 1024;
        settings.lightmapCompression = LightmapCompression.NormalQuality;
        settings.directionalityMode = LightmapsMode.NonDirectional;
        settings.ao = true;
        settings.aoMaxDistance = 0.6f;
        settings.aoExponentIndirect = 1f;
        settings.aoExponentDirect = 0f;
        settings.maxBounces = 3;
        settings.directSampleCount = 32;
        settings.indirectSampleCount = 256;
        settings.environmentSampleCount = 64;
        EditorUtility.SetDirty(settings);
        Lightmapping.lightingSettings = settings;

        EditorSceneManager.MarkSceneDirty(world.gameObject.scene);
    }

    // Every lamp in the furnishing gets its light: ceiling panel = soft area light, pendant and table lamps = warm
    // point lights in the shade, the Room 3 desk lamp = a spot onto the table.
    static void LightFor(Transform fixture, Transform parent, int room)
    {
        var renderers = fixture.GetComponentsInChildren<Renderer>();
        var b = renderers[0].bounds;
        foreach (var r in renderers)
            b.Encapsulate(r.bounds);
        string n = fixture.name;
        var down = Quaternion.Euler(90f, 0f, 0f);
        if (n.Contains("Light_07"))
        {
            var panel = AddLight(parent, n, LightType.Rectangle, new Vector3(b.center.x, b.min.y - 0.02f, b.center.z), down,
                PanelColour[room], Panel[room]);
            panel.areaSize = new Vector2(b.size.x, b.size.z) * 0.9f;
        }
        else if (n.Contains("Light_04") || n.Contains("Light_05"))
            AddLight(parent, n, LightType.Point, new Vector3(b.center.x, b.min.y + 0.12f, b.center.z), down, Warm, 2.5f).range = 4.5f;
        else if (n.Contains("DeskLamp_06") || n.Contains("DeskLamp_07"))
            AddLight(parent, n, LightType.Point, new Vector3(b.center.x, b.max.y - 0.12f, b.center.z), down, Warm, 1.1f).range = 3f;
        else if (n.Contains("Desklamp_02"))
        {
            var spot = AddLight(parent, n, LightType.Spot, new Vector3(b.center.x, b.max.y - 0.14f, b.center.z), down, Warm, 4f);
            spot.range = 3.5f;
            spot.spotAngle = 120f;
            spot.innerSpotAngle = 60f;
        }
    }

    static bool IsLamp(string name) => name.Contains("Light_0") || name.IndexOf("lamp", System.StringComparison.OrdinalIgnoreCase) >= 0;

    static Light AddLight(Transform parent, string name, LightType type, Vector3 pos, Quaternion rot, Color colour, float intensity)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(pos, rot);
        var light = go.AddComponent<Light>();
        light.type = type;
        light.lightmapBakeType = LightmapBakeType.Baked;
        light.color = colour;
        light.intensity = intensity;
        light.shadows = LightShadows.Soft;
        light.shadowRadius = 0.15f;
        return light;
    }

    // A probe grid filling the room, for everything not lightmapped (avatar, NPC, doors, furniture).
    static void Probes(Transform parent, Vector3 centre)
    {
        var group = new GameObject("Probes").AddComponent<LightProbeGroup>();
        group.transform.SetParent(parent, false);
        var positions = new System.Collections.Generic.List<Vector3>();
        foreach (float x in new[] { -2.5f, -0.85f, 0.85f, 2.5f })
            foreach (float z in new[] { -2.5f, -0.85f, 0.85f, 2.5f })
                foreach (float y in new[] { 0.3f, 1.3f, 2.5f })
                    positions.Add(centre + new Vector3(x, y, z));
        group.probePositions = positions.ToArray();
    }

    // Everything in the rooms takes part in the bake (casts shadows, bounces light). The shell gets lightmaps, the
    // rest is lit by the probes. Not static: transparent mirror glass and the lamps themselves
    // (they would block their own light). No batching flags: the mirror worlds copy Room 1 at runtime.
    static void MarkStatic(Transform root)
    {
        foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            var go = renderer.gameObject;
            var material = renderer.sharedMaterial;
            bool skip = material == null || material.renderQueue >= 2500
                || System.Array.Exists(go.GetComponentsInParent<Transform>(true), t => IsLamp(t.name));
            GameObjectUtility.SetStaticEditorFlags(go, skip ? 0 : StaticEditorFlags.ContributeGI);
            if (skip)
                continue;
            // Only the shell (our own meshes, UV2 from RoomVariantBuilder) is lightmapped: the pack meshes' second UVs
            // are not always lightmap UVs (the globe came out cyan).
            bool shell = go.GetComponent<RoomSurface>() != null && !root.name.StartsWith("Doors");
            renderer.receiveGI = shell ? ReceiveGI.Lightmaps : ReceiveGI.LightProbes;
            // The bake honours both: RoomBuilder's boxes had them off, so lamps shone through walls into the next room.
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }
    }
}
