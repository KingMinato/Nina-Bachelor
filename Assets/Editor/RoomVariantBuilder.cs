using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Builds the furnishing variants of Room 1 and Room 2 in the open scene (Main.unity) as mockups to compare:
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
    const string StartVariant = "Stein";

    // Room interiors (inner wall faces). Room 1 is centred at the origin, Room 2 behind the door wall.
    // Both rooms span z = -Side..Side, Room 1 also x = -Side..Side.
    const float Side = 3f, R2MinX = 3.2f, R2MaxX = 9.2f, Height = 3f, DoorHalfWidth = 0.6f;

    enum Wall { None, PlusX, MinusX, PlusZ, MinusZ }

    class Variant
    {
        public string name;
        public Material floor, wall, ceiling, door;
        public Transform room1, room2, doorModel;
        public bool plainDoorLeaf = true;
    }

    [MenuItem("Mirror Game/Build Room Variants")]
    public static void Build()
    {
        var world = GameObject.Find("World").transform;
        var room1 = world.Find("Room1");
        var room2 = world.Find("Room2");
        var hinge = room1.Find("DoorHinge");
        var doorLeaf = hinge.Find("DoorLeaf").gameObject;

        foreach (var t in new[] { room1, room2, hinge, world })
        {
            var old = t.Find(t == world ? "RoomVariants" : "Variants");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
        }
        doorLeaf.SetActive(true);

        Directory.CreateDirectory(MaterialFolder);
        Directory.CreateDirectory(MeshFolder);
        PrepareShell(room1);
        PrepareShell(room2);

        var manager = new GameObject("RoomVariants");
        manager.transform.SetParent(world, false);
        var variants = new List<RoomVariant>();
        foreach (var (name, build) in new (string, System.Action<Variant>)[]
                 { ("Grau", Grau), ("Buero", Buero), ("Holz", Holz), ("Stein", Stein), ("Beton", Beton), ("Huette", Huette) })
        {
            var v = new Variant
            {
                name = name,
                room1 = Group(room1, name),
                room2 = Group(room2, name),
                doorModel = Group(hinge, name),
            };
            build(v);
            variants.Add(CreateVariant(manager.transform, v, doorLeaf));
        }

        var rooms = manager.AddComponent<RoomVariants>();
        var so = new SerializedObject(rooms);
        SetArray(so.FindProperty("variants"), variants.ToArray());
        SetArray(so.FindProperty("rooms"), new Object[] { room1, room2 });
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
            reference.FindProperty("rooms").objectReferenceValue = rooms;
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

        Put(v.room1, P(Office, "SM_Bld_Ceiling_Panel_Light_01"), new Vector3(0f, Height, 0f), ceiling: true);
        Put(v.room1, P(Office, "SM_Prop_Plant_13"), new Vector3(-2.4f, 0f, 2.4f), 20f);

        Put(v.room2, P(Office, "SM_Bld_Ceiling_Panel_Light_01"), new Vector3(7f, Height, 0f), ceiling: true);
        Put(v.room2, P(Office, "SM_Prop_Shelf_01"), new Vector3(R2MaxX, 0f, -1.9f), -90f, wall: Wall.PlusX);
        Put(v.room2, P(Office, "SM_Prop_Plant_02"), new Vector3(8.75f, 0f, 1.9f));
        Put(v.room2, P(Office, "SM_Prop_WaterCooler_01"), new Vector3(8.4f, 0f, Side), 180f, wall: Wall.PlusZ);
        Put(v.room2, P(Office, "SM_Prop_Cabinets_02"), new Vector3(7.6f, 0f, -Side), 0f, wall: Wall.MinusZ);
    }

    // Warm: wooden floor, warm white walls, round ceiling lamps, wooden door.
    static void Holz(Variant v)
    {
        v.floor = Mat("Holz_Floor", GenericTextures + "Generic_Wood.png", new Color(1f, 0.93f, 0.85f), 1.5f);
        v.wall = Mat("Holz_Wall", GenericTextures + "Generic_Plaster.png", new Color(1f, 0.97f, 0.91f), 3f);
        v.ceiling = Mat("Holz_Ceiling", null, new Color(0.95f, 0.93f, 0.88f));
        Baseboards(v, Mat("Holz_Trim", null, new Color(0.42f, 0.3f, 0.2f)));
        DoorModel(v, P(Office, "SM_Bld_Door_05"));

        Put(v.room1, P(Office, "SM_Prop_Light_07"), new Vector3(0f, Height, 0f), ceiling: true);
        Put(v.room1, P(Office, "SM_Prop_Rug_05"), new Vector3(0f, 0.005f, 0f));

        Put(v.room2, P(Office, "SM_Prop_Light_07"), new Vector3(7f, Height, 0f), ceiling: true);
        Put(v.room2, P(Office, "SM_Prop_Shelf_05"), new Vector3(R2MaxX, 0f, -1.9f), -90f, wall: Wall.PlusX);
        Put(v.room2, P(Office, "SM_Prop_Table_03"), new Vector3(7.6f, 0f, Side - 0.05f), 0f, wall: Wall.PlusZ);
        Put(v.room2, P(Western, "SM_Prop_Chair_01"), new Vector3(7.2f, 0f, 2.1f), 160f);
        Put(v.room2, P(Office, "SM_Prop_Plant_04"), new Vector3(8.7f, 0f, 1.6f), 40f);
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

        foreach (var (room, x) in new[] { (v.room1, 0f), (v.room2, 6.2f) })
            Put(room, P(Dungeon, "SM_Env_Tiles_Texture_01"), new Vector3(x, 0.005f, 0f), scale: 1.2f);
        Put(v.room1, P(Dungeon, "SM_Prop_Lantern_02"), new Vector3(0f, Height, 0f), ceiling: true);

        Put(v.room2, P(Dungeon, "SM_Prop_Lantern_02"), new Vector3(7f, Height, 0f), ceiling: true);
        Put(v.room2, P(Dungeon, "SM_Prop_Bench_01"), new Vector3(7.2f, 0f, Side), 180f, wall: Wall.PlusZ);
        Put(v.room2, P(Dungeon, "SM_Prop_Table_Round_01"), new Vector3(8.3f, 0f, -2.1f));
        Put(v.room2, P(Dungeon, "SM_Prop_Stool_01"), new Vector3(7.3f, 0f, -2.2f), 15f);
        Put(v.room2, P(Dungeon, "SM_Prop_Vase_01"), new Vector3(8.8f, 0f, 2.6f));
    }

    // Gallery: grey concrete floor and walls, almost empty.
    static void Beton(Variant v)
    {
        v.floor = Mat("Beton_Floor", GenericTextures + "Generic_Concrete.png", new Color(0.66f, 0.7f, 0.76f), 3f);
        v.wall = Mat("Beton_Wall", GenericTextures + "Generic_Concrete.png", new Color(1.15f, 1.2f, 1.28f), 3f);
        v.ceiling = Mat("Beton_Ceiling", null, new Color(0.96f, 0.96f, 0.96f));
        v.door = Mat("Beton_Door", null, new Color(0.82f, 0.82f, 0.8f));

        Put(v.room1, P(Office, "SM_Prop_Light_06"), new Vector3(0f, Height, 0f), ceiling: true);

        Put(v.room2, P(Office, "SM_Prop_Light_06"), new Vector3(7f, Height, 0f), ceiling: true);
        Put(v.room2, P(Office, "SM_Prop_Benchseat_01"), new Vector3(7.2f, 0f, -Side), 0f, wall: Wall.MinusZ);
        var cube = Put(v.room2, P(Office, "SM_Prop_Concrete_Cube_01"), new Vector3(8.6f, 0f, 2.4f), 10f);
        Put(v.room2, P(Office, "SM_Prop_Plant_11"), new Vector3(8.6f, Bounds(cube).max.y, 2.4f));
    }

    // Cabin: wooden planks on walls and floor, lantern, simple wooden furniture.
    static void Huette(Variant v)
    {
        v.floor = Mat("Huette_Floor", GenericTextures + "Generic_Wood.png", new Color(1.05f, 0.95f, 0.85f), 1.5f);
        v.wall = Mat("Huette_Wall", GenericTextures + "Generic_Wood.png", new Color(1.5f, 1.4f, 1.28f), 2f);
        v.ceiling = Mat("Huette_Ceiling", GenericTextures + "Generic_Wood.png", new Color(1.3f, 1.2f, 1.1f), 2f);
        v.door = Mat("Huette_Door", GenericTextures + "Generic_Wood.png", new Color(0.75f, 0.65f, 0.55f), 1f);

        Put(v.room1, P(Western, "SM_Prop_Lantern_01"), new Vector3(-2.6f, 0f, 2.6f), 30f);

        var table = Put(v.room2, P(Western, "SM_Prop_Table_01"), new Vector3(7.4f, 0f, Side - 0.1f), 90f, wall: Wall.PlusZ);
        Put(v.room2, P(Western, "SM_Prop_Chair_01"), new Vector3(7.4f, 0f, 1.8f), 170f);
        Put(v.room2, P(Western, "SM_Prop_Lantern_01"), new Vector3(7.7f, Bounds(table).max.y, 2.6f));
        Put(v.room2, P(Office, "SM_Prop_Shelf_06"), new Vector3(R2MaxX, 0f, -1.9f), -90f, wall: Wall.PlusX);
        Put(v.room2, P(Western, "SM_Prop_PlantPot_02"), new Vector3(8.7f, 0f, 2.5f));
    }

    // ---------------------------------------------------------------- helpers

    static RoomVariant CreateVariant(Transform manager, Variant v, GameObject doorLeaf)
    {
        var go = new GameObject(v.name);
        go.transform.SetParent(manager, false);
        var variant = go.AddComponent<RoomVariant>();
        var parts = new List<Object> { v.room1.gameObject, v.room2.gameObject, v.doorModel.gameObject };
        if (v.plainDoorLeaf)
            parts.Add(doorLeaf);
        foreach (var t in new[] { v.room1, v.room2, v.doorModel })
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
        var hinge = v.doorModel.parent.parent;
        var leaf = hinge.Find("DoorLeaf");
        var target = leaf.GetComponent<Renderer>().bounds;

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, v.doorModel);
        Strip(go);
        // The model's width is its local x: turn it so the width runs along the door opening (z).
        go.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        var b = Bounds(go);
        go.transform.localScale = new Vector3(target.size.z / b.size.z, target.size.y / b.size.y, 1f);
        b = Bounds(go);
        go.transform.position += target.center - b.center;
    }

    static void Baseboards(Variant v, Material material, float height = 0.1f, float depth = 0.02f)
    {
        void Board(Transform parent, float x0, float z0, float x1, float z1, Vector3 normal)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = "Baseboard";
            go.transform.SetParent(parent, false);
            var size = new Vector3(Mathf.Max(Mathf.Abs(x1 - x0), depth), height, Mathf.Max(Mathf.Abs(z1 - z0), depth));
            go.transform.localScale = size;
            go.transform.position = new Vector3((x0 + x1) / 2f, height / 2f, (z0 + z1) / 2f) + normal * depth / 2f;
            go.GetComponent<Renderer>().sharedMaterial = material;
        }
        float d = DoorHalfWidth;
        // Room 1: left (mirror), top (mirror), bottom (mirror), door wall.
        Board(v.room1, -Side, -Side, -Side, Side, Vector3.right);
        Board(v.room1, -Side, Side, Side, Side, Vector3.back);
        Board(v.room1, -Side, -Side, Side, -Side, Vector3.forward);
        Board(v.room1, Side, -Side, Side, -d, Vector3.left);
        Board(v.room1, Side, d, Side, Side, Vector3.left);
        // Room 2: door wall, far wall, side walls.
        Board(v.room2, R2MinX, -Side, R2MinX, -d, Vector3.right);
        Board(v.room2, R2MinX, d, R2MinX, Side, Vector3.right);
        Board(v.room2, R2MaxX, -Side, R2MaxX, Side, Vector3.left);
        Board(v.room2, R2MinX, Side, R2MaxX, Side, Vector3.back);
        Board(v.room2, R2MinX, -Side, R2MaxX, -Side, Vector3.forward);
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
        for (var p = t.parent; p != null && p.name != "Room1" && p.name != "Room2"; p = p.parent)
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
