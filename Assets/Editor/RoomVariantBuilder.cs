using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Builds the furnishing variants of Rooms 1-4 in the open scene (Main.unity) as mockups to compare:
// materials for the shell (floor, walls, ceiling, door leaf), furniture from the Synty packs and door models.
// The shell itself (walls, mirror openings, door, waypoints) is not changed. Rebuilding replaces all variants.
// Menu: Mirror Game/Build Room Variants. Requires the licensed Synty packs (Office, Dungeon, Western Frontier),
// which are not in the repository.
//
// Rules (CLAUDE.md, sections 5 and 8): everything visible in the mirrors is a child of Room1, nothing is
// static-batched, no extra lights (lighting must stay symmetric), nothing blocks the walk path or the mirrors.
public static class RoomVariantBuilder
{
    const string Office = "Assets/Synty/PolygonOffice";
    const string Dungeon = "Assets/Synty/PolygonDungeon";
    const string Western = "Assets/Synty/PolygonWesternFrontier";
    const string GenericTextures = "Assets/Synty/PolygonGeneric/Textures/";
    const string MaterialFolder = "Assets/Art/Environment/Rooms/";
    const string MeshFolder = "Assets/Art/Environment/ShellMeshes/";
    // Variant the game starts with (RoomVariants.startIndex).
    const string StartVariant = "Holz";

    // Room interiors are 2 * Side wide (inner wall faces), centres as in RoomBuilder.
    const float Side = 3f, Height = 3f, DoorHalfWidth = 0.6f;
    static readonly Vector3[] Centres = { Vector3.zero, new Vector3(6.2f, 0, 0), new Vector3(6.2f, 0, -6.2f), new Vector3(0, 0, -6.2f) };
    // Walls with a door, per room (for the baseboards).
    static readonly Wall[][] DoorWalls = { new[] { Wall.PlusX }, new[] { Wall.MinusX, Wall.MinusZ }, new[] { Wall.PlusZ, Wall.MinusX }, new[] { Wall.PlusX } };

    enum Wall { None, PlusX, MinusX, PlusZ, MinusZ }

    class Variant
    {
        public string name;
        public Material floor, wall, ceiling, door;
        public Transform[] rooms;
        public Transform[] doorModels;
        public bool plainDoorLeaf = true;
        public Transform room1 => rooms[0];
        public Transform room2 => rooms[1];
        public Transform room3 => rooms[2];
        public Transform room4 => rooms[3];
    }

    // Position relative to the centre of room i (0-based).
    static Vector3 In(int room, float dx, float y, float dz) => Centres[room] + new Vector3(dx, y, dz);

    [MenuItem("Mirror Game/Build Room Variants")]
    public static void Build()
    {
        var world = GameObject.Find("World").transform;
        var rooms = new[] { world.Find("Room1"), world.Find("Room2"), world.Find("Room3"), world.Find("Room4") };
        var doors = world.Find("Doors");
        var hinges = new[] { doors.Find("Door1"), doors.Find("Door2"), doors.Find("Door3") };
        var doorLeaves = System.Array.ConvertAll(hinges, h => h.Find("DoorLeaf").gameObject);

        foreach (var t in rooms.Concat(hinges).Append(world))
        {
            var old = t.Find(t == world ? "RoomVariants" : "Variants");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
        }
        foreach (var leaf in doorLeaves)
            leaf.SetActive(true);

        Directory.CreateDirectory(MaterialFolder);
        // Shell meshes are regenerated, old ones (removed walls) must not linger.
        AssetDatabase.DeleteAsset(MeshFolder.TrimEnd('/'));
        Directory.CreateDirectory(MeshFolder);
        AssetDatabase.Refresh();
        foreach (var room in rooms)
            PrepareShell(room);
        PrepareShell(doors);

        var manager = new GameObject("RoomVariants");
        manager.transform.SetParent(world, false);
        var variants = new List<RoomVariant>();
        foreach (var (name, build) in new (string, System.Action<Variant>)[]
                 { ("Grau", Grau), ("Buero", Buero), ("Holz", Holz), ("Stein", Stein), ("Huette", Huette) })
        {
            var v = new Variant
            {
                name = name,
                rooms = System.Array.ConvertAll(rooms, r => Group(r, name)),
                doorModels = System.Array.ConvertAll(hinges, h => Group(h, name)),
            };
            build(v);
            variants.Add(CreateVariant(manager.transform, v, doorLeaves));
        }

        var roomVariants = manager.AddComponent<RoomVariants>();
        var so = new SerializedObject(roomVariants);
        SetArray(so.FindProperty("variants"), variants.ToArray());
        SetArray(so.FindProperty("rooms"), rooms.Append(doors).ToArray<Object>());
        SetArray(so.FindProperty("mirrorWorlds"), world.Find("MirrorWorlds").GetComponentsInChildren<MirrorWorld>(true));
        int start = Mathf.Max(0, variants.FindIndex(v => v.name == StartVariant));
        so.FindProperty("startIndex").intValue = start;
        so.ApplyModifiedPropertiesWithoutUndo();

        // The scene shows the start variant, the others stay inactive.
        foreach (var v in variants)
            v.SetPartsActive(false);
        variants[start].SetPartsActive(true);

        foreach (var user in new Object[] { Object.FindFirstObjectByType<GameFlow>(),
                     Object.FindFirstObjectByType<DebugRoomSwitcher>(FindObjectsInactive.Include) })
        {
            if (user == null)
                continue;
            var reference = new SerializedObject(user);
            reference.FindProperty("rooms").objectReferenceValue = roomVariants;
            reference.ApplyModifiedPropertiesWithoutUndo();
        }

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(world.gameObject.scene);
        EditorSceneManager.SaveScene(world.gameObject.scene);
        Debug.Log("Room variants built: " + string.Join(", ", variants.Select(v => v.DisplayName)));
    }

    // ---------------------------------------------------------------- variants

    // The greybox as it was, for comparison.
    static void Grau(Variant v) { }

    // Office: carpet, light grey walls, ceiling panel lights, plain office door.
    static void Buero(Variant v)
    {
        v.floor = Mat("Buero_Floor", GenericTextures + "Generic_Carpet.png", new Color(0.78f, 0.8f, 0.84f), 2f);
        v.wall = Mat("Buero_Wall", GenericTextures + "Generic_Plaster.png", new Color(0.87f, 0.9f, 0.95f), 3f);
        v.ceiling = Mat("Buero_Ceiling", null, new Color(0.92f, 0.92f, 0.92f));
        Baseboards(v, Mat("Buero_Trim", null, new Color(0.35f, 0.36f, 0.38f)));
        DoorModel(v, P(Office, "SM_Bld_Door_02"));
        for (int i = 0; i < 4; i++)
            Put(v.rooms[i], P(Office, "SM_Bld_Ceiling_Panel_Light_01"), In(i, i == 3 ? -0.8f : 0f, Height, 0f), ceiling: true);

        Put(v.room1, P(Office, "SM_Prop_Plant_13"), new Vector3(-2.4f, 0f, 2.4f), 20f);

        Put(v.room2, P(Office, "SM_Prop_Plant_13"), In(1, 2.4f, 0f, 2.4f), 70f);
        Put(v.room2, P(Office, "SM_Prop_Shelf_01"), In(1, -1.9f, 0f, -Side), 0f, wall: Wall.MinusZ);

        Put(v.room3, P(Office, "SM_Prop_Plant_02"), In(2, 2.5f, 0f, 2.5f));
        Put(v.room3, P(Office, "SM_Prop_WaterCooler_01"), In(2, -2.4f, 0f, -Side), 0f, wall: Wall.MinusZ);

        Put(v.room4, P(Office, "SM_Prop_Shelf_01"), In(3, -Side, 0f, -1.9f), 90f, wall: Wall.MinusX);
        Put(v.room4, P(Office, "SM_Prop_Plant_02"), In(3, -2.55f, 0f, 1.9f));
        Put(v.room4, P(Office, "SM_Prop_WaterCooler_01"), In(3, -2.2f, 0f, Side), 180f, wall: Wall.PlusZ);
        Put(v.room4, P(Office, "SM_Prop_Cabinets_02"), In(3, -1.4f, 0f, -Side), 0f, wall: Wall.MinusZ);
    }

    // Warm: wooden floor, warm white walls, round ceiling lamps, wooden door.
    static void Holz(Variant v)
    {
        v.floor = Mat("Holz_Floor", GenericTextures + "Generic_Wood.png", new Color(1f, 0.93f, 0.85f), 1.5f);
        v.wall = Mat("Holz_Wall", GenericTextures + "Generic_Plaster.png", new Color(1f, 0.97f, 0.91f), 3f);
        v.ceiling = Mat("Holz_Ceiling", null, new Color(0.95f, 0.93f, 0.88f));
        Baseboards(v, Mat("Holz_Trim", null, new Color(0.42f, 0.3f, 0.2f)));
        DoorModel(v, P(Office, "SM_Bld_Door_05"));
        for (int i = 0; i < 4; i++)
            Put(v.rooms[i], P(Office, "SM_Prop_Light_07"), In(i, i == 3 ? -0.8f : 0f, Height, 0f), ceiling: true);

        Put(v.room1, P(Office, "SM_Prop_Rug_05"), new Vector3(0f, 0.005f, 0f));

        Put(v.room2, P(Office, "SM_Prop_Shelf_05"), In(1, Side, 0f, 2.2f), -90f, wall: Wall.PlusX);
        Put(v.room2, P(Office, "SM_Prop_Plant_04"), In(1, -2.5f, 0f, -2.5f), 40f);

        Put(v.room3, P(Office, "SM_Prop_Table_03"), In(2, -1.9f, 0f, Side - 0.05f), 0f, wall: Wall.PlusZ);
        Put(v.room3, P(Western, "SM_Prop_Chair_01"), In(2, -2.3f, 0f, 1.9f), 160f);
        Put(v.room3, P(Office, "SM_Prop_Plant_04"), In(2, 2.5f, 0f, -2.5f), 10f);

        Put(v.room4, P(Office, "SM_Prop_Shelf_05"), In(3, -Side, 0f, -1.9f), 90f, wall: Wall.MinusX);
        Put(v.room4, P(Office, "SM_Prop_Table_03"), In(3, -1.4f, 0f, Side - 0.05f), 0f, wall: Wall.PlusZ);
        Put(v.room4, P(Western, "SM_Prop_Chair_01"), In(3, -1f, 0f, 2.1f), -160f);
        Put(v.room4, P(Office, "SM_Prop_Plant_04"), In(3, -2.5f, 0f, 1.6f), -40f);
    }

    // Stone: stone floor tiles, light stone walls, plank door, hanging lanterns (no light).
    // Colours above 1 brighten the darker textures: the walls only get ambient light.
    static void Stein(Variant v)
    {
        v.floor = Mat("Stein_Floor", null, new Color(0.55f, 0.55f, 0.55f)); // hidden under the stone tiles
        v.wall = Mat("Stein_Wall", GenericTextures + "Generic_Rock.png", new Color(1.45f, 1.4f, 1.32f), 3f);
        v.ceiling = Mat("Stein_Ceiling", GenericTextures + "Generic_Plaster.png", new Color(0.8f, 0.78f, 0.75f), 3f);
        Baseboards(v, Mat("Stein_Trim", null, new Color(0.4f, 0.39f, 0.37f)), 0.15f, 0.04f);
        DoorModel(v, P(Dungeon, "SM_Env_Door_03"));
        for (int i = 0; i < 4; i++)
        {
            Put(v.rooms[i], P(Dungeon, "SM_Env_Tiles_Texture_01"), In(i, 0f, 0.005f, 0f), scale: 1.2f);
            Put(v.rooms[i], P(Dungeon, "SM_Prop_Lantern_02"), In(i, i == 3 ? -0.8f : 0f, Height, 0f), ceiling: true);
        }

        Put(v.room2, P(Dungeon, "SM_Prop_Bench_01"), In(1, 2f, 0f, Side), 180f, wall: Wall.PlusZ);
        Put(v.room2, P(Dungeon, "SM_Prop_Vase_01"), In(1, -2.5f, 0f, -2.6f));

        Put(v.room3, P(Dungeon, "SM_Prop_Table_Round_01"), In(2, -2.3f, 0f, -2.3f));
        Put(v.room3, P(Dungeon, "SM_Prop_Stool_01"), In(2, -1.6f, 0f, -2.6f), 15f);
        Put(v.room3, P(Dungeon, "SM_Prop_Vase_01"), In(2, 2.5f, 0f, 2.5f));

        Put(v.room4, P(Dungeon, "SM_Prop_Bench_01"), In(3, -1f, 0f, Side), 180f, wall: Wall.PlusZ);
        Put(v.room4, P(Dungeon, "SM_Prop_Table_Round_01"), In(3, -2.1f, 0f, -2.1f));
        Put(v.room4, P(Dungeon, "SM_Prop_Stool_01"), In(3, -1.1f, 0f, -2.2f), -15f);
        Put(v.room4, P(Dungeon, "SM_Prop_Vase_01"), In(3, -2.6f, 0f, 2.6f));
    }

    // Cabin: wooden planks on walls and floor, lantern, simple wooden furniture.
    static void Huette(Variant v)
    {
        v.floor = Mat("Huette_Floor", GenericTextures + "Generic_Wood.png", new Color(1.05f, 0.95f, 0.85f), 1.5f);
        v.wall = Mat("Huette_Wall", GenericTextures + "Generic_Wood.png", new Color(1.5f, 1.4f, 1.28f), 2f);
        v.ceiling = Mat("Huette_Ceiling", GenericTextures + "Generic_Wood.png", new Color(1.3f, 1.2f, 1.1f), 2f);
        v.door = Mat("Huette_Door", GenericTextures + "Generic_Wood.png", new Color(0.75f, 0.65f, 0.55f), 1f);

        Put(v.room1, P(Western, "SM_Prop_Lantern_01"), new Vector3(-2.6f, 0f, 2.6f), 30f);

        Put(v.room2, P(Western, "SM_Prop_Lantern_01"), In(1, -2.6f, 0f, 2.6f), 30f);
        Put(v.room2, P(Office, "SM_Prop_Shelf_06"), In(1, Side, 0f, -2.2f), -90f, wall: Wall.PlusX);

        Put(v.room3, P(Western, "SM_Prop_PlantPot_02"), In(2, 2.5f, 0f, 2.5f));
        Put(v.room3, P(Western, "SM_Prop_Chair_01"), In(2, -2.3f, 0f, -2.3f), 45f);
        Put(v.room3, P(Western, "SM_Prop_Lantern_01"), In(2, 2.6f, 0f, -2.6f));

        var table = Put(v.room4, P(Western, "SM_Prop_Table_01"), In(3, -1.2f, 0f, Side - 0.1f), -90f, wall: Wall.PlusZ);
        Put(v.room4, P(Western, "SM_Prop_Chair_01"), In(3, -1.2f, 0f, 1.8f), -170f);
        Put(v.room4, P(Western, "SM_Prop_Lantern_01"), In(3, -1.5f, Bounds(table).max.y, 2.6f));
        Put(v.room4, P(Office, "SM_Prop_Shelf_06"), In(3, -Side, 0f, -1.9f), 90f, wall: Wall.MinusX);
        Put(v.room4, P(Western, "SM_Prop_PlantPot_02"), In(3, -2.5f, 0f, 2.5f));
    }

    // ---------------------------------------------------------------- helpers

    static RoomVariant CreateVariant(Transform manager, Variant v, GameObject[] doorLeaves)
    {
        var go = new GameObject(v.name);
        go.transform.SetParent(manager, false);
        var variant = go.AddComponent<RoomVariant>();
        var groups = v.rooms.Concat(v.doorModels).ToArray();
        var parts = new List<Object>(groups.Select(t => t.gameObject));
        if (v.plainDoorLeaf)
            parts.AddRange(doorLeaves);
        foreach (var t in groups)
            foreach (var child in t.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(child.gameObject, 0);

        var so = new SerializedObject(variant);
        so.FindProperty("displayName").stringValue = v.name;
        so.FindProperty("floor").objectReferenceValue = v.floor;
        so.FindProperty("wall").objectReferenceValue = v.wall;
        so.FindProperty("ceiling").objectReferenceValue = v.ceiling;
        so.FindProperty("door").objectReferenceValue = v.door;
        SetArray(so.FindProperty("parts"), parts.ToArray());
        so.ApplyModifiedPropertiesWithoutUndo();
        return variant;
    }

    static Transform Group(Transform room, string name)
    {
        var variants = room.Find("Variants");
        if (variants == null)
        {
            variants = new GameObject("Variants").transform;
            variants.SetParent(room, false);
        }
        var group = new GameObject(name).transform;
        group.SetParent(variants, false);
        return group;
    }

    // Finds a prefab by file name inside a pack folder.
    static GameObject P(string pack, string name)
    {
        foreach (var guid in AssetDatabase.FindAssets(name + " t:Prefab", new[] { pack }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) == name)
                return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
        Debug.LogWarning("Prefab not found: " + pack + "/" + name);
        return null;
    }

    // Places a prefab so that its bounds are centred on pos (x, z) and stand on pos.y (or hang from it).
    // wall: the bounds touch the wall plane given by that coordinate of pos instead of being centred on it.
    static GameObject Put(Transform parent, GameObject prefab, Vector3 pos, float yaw = 0f,
        Wall wall = Wall.None, bool ceiling = false, float scale = 1f)
    {
        if (prefab == null)
            return null;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, yaw, 0f));
        go.transform.localScale *= scale;
        Strip(go);

        var b = Bounds(go);
        var offset = new Vector3(pos.x - b.center.x, ceiling ? pos.y - b.max.y : pos.y - b.min.y, pos.z - b.center.z);
        switch (wall)
        {
            case Wall.PlusX: offset.x = pos.x - b.max.x; break;
            case Wall.MinusX: offset.x = pos.x - b.min.x; break;
            case Wall.PlusZ: offset.z = pos.z - b.max.z; break;
            case Wall.MinusZ: offset.z = pos.z - b.min.z; break;
        }
        go.transform.position += offset;
        return go;
    }

    // Colliders are not needed, lights and particles would break the symmetric mirror lighting.
    static void Strip(GameObject go)
    {
        foreach (var c in go.GetComponentsInChildren<Component>(true))
            if (c is Collider || c is Light || c is ParticleSystem || c is LODGroup)
                Object.DestroyImmediate(c);
    }

    static Bounds Bounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        var b = renderers[0].bounds;
        foreach (var r in renderers)
            b.Encapsulate(r.bounds);
        return b;
    }

    // A door model from a pack, fitted into the door opening on the hinge (replaces the plain leaf).
    static void DoorModel(Variant v, GameObject prefab)
    {
        if (prefab == null)
            return;
        v.plainDoorLeaf = false;
        foreach (var group in v.doorModels)
        {
            var hinge = group.parent.parent;
            // Fitted with the hinge turned to identity (leaf along z), so world bounds line up with the leaf.
            var turned = hinge.rotation;
            hinge.rotation = Quaternion.identity;
            var target = hinge.Find("DoorLeaf").GetComponent<Renderer>().bounds;

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
            Strip(go);
            // The model's width is its local x: turn it so the width runs along the door opening (z).
            go.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            var b = Bounds(go);
            go.transform.localScale = new Vector3(target.size.z / b.size.z, target.size.y / b.size.y, 1f);
            b = Bounds(go);
            go.transform.position += target.center - b.center;
            hinge.rotation = turned;
        }
    }

    static void Baseboards(Variant v, Material material, float height = 0.1f, float depth = 0.02f)
    {
        void Board(Transform parent, Vector3 a, Vector3 b, Vector3 normal)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = "Baseboard";
            go.transform.SetParent(parent, false);
            go.transform.localScale = new Vector3(Mathf.Max(Mathf.Abs(b.x - a.x), depth), height, Mathf.Max(Mathf.Abs(b.z - a.z), depth));
            go.transform.position = (a + b) / 2f + Vector3.up * height / 2f + normal * depth / 2f;
            go.GetComponent<Renderer>().sharedMaterial = material;
        }
        var sides = new[] { (Wall.PlusX, Vector3.right), (Wall.MinusX, Vector3.left), (Wall.PlusZ, Vector3.forward), (Wall.MinusZ, Vector3.back) };
        for (int r = 0; r < 4; r++)
        {
            foreach (var (wall, outward) in sides)
            {
                Vector3 mid = Centres[r] + outward * Side, along = new Vector3(outward.z, 0f, outward.x);
                if (System.Array.IndexOf(DoorWalls[r], wall) < 0)
                {
                    Board(v.rooms[r], mid - along * Side, mid + along * Side, -outward);
                    continue;
                }
                Board(v.rooms[r], mid - along * Side, mid - along * DoorHalfWidth, -outward);
                Board(v.rooms[r], mid + along * DoorHalfWidth, mid + along * Side, -outward);
            }
        }
    }

    static Material Mat(string name, string texture, Color color, float tileMetres = 1f, float smoothness = 0.1f)
    {
        string path = MaterialFolder + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetColor("_BaseColor", color);
        mat.SetTexture("_BaseMap", texture != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(texture) : null);
        mat.SetTextureScale("_BaseMap", Vector2.one / tileMetres);
        mat.SetFloat("_Smoothness", smoothness);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // Tags the shell renderers with RoomSurface and gives them meshes with UVs in metres,
    // so the tiling textures of the variants have the same scale everywhere.
    static void PrepareShell(Transform room)
    {
        foreach (var filter in room.GetComponentsInChildren<MeshFilter>(true))
        {
            string n = filter.name;
            RoomSurface.Kind kind;
            if (n == "Floor") kind = RoomSurface.Kind.Floor;
            else if (n == "Ceiling") kind = RoomSurface.Kind.Ceiling;
            else if (n == "DoorLeaf") kind = RoomSurface.Kind.Door;
            else if (n.StartsWith("Wall")) kind = RoomSurface.Kind.Wall;
            else continue; // mirror frames and glass stay as they are

            var surface = filter.GetComponent<RoomSurface>();
            if (surface == null)
                surface = filter.gameObject.AddComponent<RoomSurface>();
            var so = new SerializedObject(surface);
            so.FindProperty("kind").enumValueIndex = (int)kind;
            so.ApplyModifiedPropertiesWithoutUndo();
            filter.sharedMesh = MetreUvBox(filter.transform, room.name);
        }
    }

    // Unit cube (like Unity's) whose UVs are the world position projected onto each face's plane, in metres.
    // Saved as an asset; rebuilding updates the existing asset.
    static Mesh MetreUvBox(Transform t, string roomName)
    {
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();
        foreach (var n in new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back })
        {
            Vector3 a = n.x != 0 ? Vector3.forward : Vector3.right;
            Vector3 b = n.y != 0 ? Vector3.forward : Vector3.up;
            int start = vertices.Count;
            var worldNormal = t.TransformDirection(n);
            foreach (var (sa, sb) in new[] { (-0.5f, -0.5f), (0.5f, -0.5f), (0.5f, 0.5f), (-0.5f, 0.5f) })
            {
                var v = n * 0.5f + a * sa + b * sb;
                vertices.Add(v);
                normals.Add(n);
                uvs.Add(Project(t.TransformPoint(v), worldNormal));
            }
            bool front = Vector3.Dot(Vector3.Cross(a, b), n) > 0f;
            triangles.AddRange(front
                ? new[] { start, start + 1, start + 2, start, start + 2, start + 3 }
                : new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
        }

        string name = roomName + "_" + PathFrom(t).Replace('/', '_');
        string path = MeshFolder + name + ".asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        bool create = mesh == null;
        if (create)
            mesh = new Mesh();
        mesh.Clear();
        mesh.name = name;
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        if (create)
            AssetDatabase.CreateAsset(mesh, path);
        else
            EditorUtility.SetDirty(mesh);
        return mesh;
    }

    static Vector2 Project(Vector3 p, Vector3 n)
    {
        n = new Vector3(Mathf.Abs(n.x), Mathf.Abs(n.y), Mathf.Abs(n.z));
        if (n.y >= n.x && n.y >= n.z) return new Vector2(p.x, p.z);
        if (n.x >= n.z) return new Vector2(p.z, p.y);
        return new Vector2(p.x, p.y);
    }

    static string PathFrom(Transform t)
    {
        string path = t.name;
        for (var p = t.parent; p != null && !p.name.StartsWith("Room") && p.name != "Doors"; p = p.parent)
            path = p.name + "/" + path;
        return path;
    }

    static void SetArray(SerializedProperty array, Object[] values)
    {
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }
}
