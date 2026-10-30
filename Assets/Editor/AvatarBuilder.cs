using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Builds everything the avatar system needs from the asset store packs:
// the shared Animator Controller (+ overrides for non-humanoid creatures), one prefab per avatar,
// the NPC prefab and the AvatarDatabase. Run "Mirror Game/Build Humanoid Clips" first.
// Menu: Mirror Game/Build Avatars. Requires the licensed packs (Synty, Creatures_3D, Lil Pupinduy, Backrock),
// which are not in the repository.
//
// Size rule (decided 2026-10-30): humanoids keep their size, creatures are scaled to roughly human
// size, the camera sits at each avatar's own eyes.
public static class AvatarBuilder
{
    const string Gen = "Assets/Synty/PolygonGeneric/Prefabs/Characters/";
    const string Office = "Assets/Synty/PolygonOffice/Prefabs/Characters/";
    const string Dungeon = "Assets/Synty/PolygonDungeon/Prefabs/Characters/";
    const string Slime = "Assets/Creatures_3D/Slime/";
    const string Mushroom = "Assets/Lil Pupinduy/Character/Mashroom Character/";
    const string Cow = "Assets/Backrock/LowPolyAnimals/Cow/";
    const string ModularHero = "Assets/Synty/PolygonFantasyHeroCharacters/Prefabs/ModularCharacters.prefab";

    const string Anim = "Assets/Animations/";
    const string PrefabFolder = "Assets/Prefabs/Avatars/";
    const string Controller = Anim + "Avatar.controller";
    const string Database = "Assets/Data/AvatarDatabase.asset";

    // Humanoid eye position relative to the head bone (Synty rig).
    static readonly Vector3 HumanEyeOffset = new Vector3(0f, 0.1f, 0.08f);

    class Spec
    {
        public string id;
        public string male, female;      // prefab paths, female == male for single-variant species
        public float scale = 1f;
        public Vector3? eyes;            // in model space (unscaled), null = humanoid head bone + offset
        public string[] eyeBones;        // midpoint of these bones, alternative to eyes
        public string[] hidden;          // bone names collapsed in first person, null = humanoid head
        public string[] hiddenRenderers = new string[0]; // renderers moved to the first-person-hidden layer
        public string[] removeComponents = new string[0];
        public string overrideController; // null = shared humanoid controller
    }

    static readonly Spec[] Specs =
    {
        new Spec { id = "humanC", male = Gen + "SM_Gen_Chr_Street_Male_02.prefab", female = Gen + "SM_Gen_Chr_Street_Female_02.prefab" },
        new Spec { id = "humanI", male = Office + "SM_Chr_Business_Male_02.prefab", female = Office + "SM_Chr_Business_Female_02.prefab" },
        new Spec { id = "goblin", male = Dungeon + "SM_Chr_Goblin_Male_01.prefab", female = Dungeon + "SM_Chr_Goblin_Female_01.prefab" },
        new Spec { id = "robot", male = Gen + "SM_Gen_Chr_Robot_01.prefab" },
        new Spec { id = "skeleton", male = Gen + "SM_Gen_Chr_Skeleton_01.prefab" },
        // 1.0 m wide unscaled, 1.4 keeps it narrow enough to be seen whole in the mirror.
        new Spec { id = "slime", male = Slime + "Resources/Prefabs/slime.prefab", scale = 1.4f,
            eyeBones = new[] { "Eye.L", "Eye.R" }, hidden = new[] { "Head" },
            removeComponents = new[] { "CharacterController", "SlimeScript" }, overrideController = "Slime" },
        new Spec { id = "mushroom", male = Mushroom + "Prefabs/Mushroom_1.prefab", eyes = new Vector3(0f, 0.9f, 0.19f),
            hidden = new string[0], hiddenRenderers = new[] { "Hat", "Eyebrow" }, overrideController = "Mushroom" },
        new Spec { id = "cow", male = Cow + "Prefabs/Cow_Charolais.prefab", eyes = new Vector3(0f, 1.17f, 1.18f),
            hidden = new[] { "Head" }, overrideController = "Cow" },
    };

    // NPC "V": grey hood with face cloth and a long robe from the Modular Fantasy Hero kit.
    static readonly string[] NpcParts =
    {
        "Chr_Head_Male_01", "Chr_HeadCoverings_No_Hair_06", "Chr_HeadCoverings_No_FacialHair_04",
        "Chr_Torso_Male_08", "Chr_ArmUpperRight_Male_08", "Chr_ArmUpperLeft_Male_08", "Chr_ArmLowerRight_Male_08",
        "Chr_ArmLowerLeft_Male_08", "Chr_HandRight_Male_08", "Chr_HandLeft_Male_08", "Chr_Hips_Male_08",
        "Chr_LegRight_Male_08", "Chr_LegLeft_Male_08",
    };

    static readonly Dictionary<string, string> NpcColors = new Dictionary<string, string>
    {
        ["_Color_Primary"] = "#6b6e72", ["_Color_Secondary"] = "#55585c", ["_Color_Leather_Primary"] = "#4a4c50",
        ["_Color_Leather_Secondary"] = "#3e4044", ["_Color_Metal_Primary"] = "#77797c", ["_Color_Metal_Secondary"] = "#636568",
        ["_Color_Metal_Dark"] = "#3a3c3f", ["_Color_Hair"] = "#3a3a3a", ["_Color_Skin"] = "#d9b8a0", ["_Color_Eyes"] = "#3d3a36",
        ["_Color_Stubble"] = "#d9b8a0",
    };

    [MenuItem("Mirror Game/Build Avatars")]
    public static void Build()
    {
        var controller = BuildController();
        var overrides = new Dictionary<string, AnimatorOverrideController>
        {
            ["Slime"] = Override(controller, "Slime", Clip(Slime + "AnimationClips/idle.anim"), Clip(Slime + "AnimationClips/jump.anim")),
            ["Mushroom"] = Override(controller, "Mushroom", Clip(Mushroom + "Animations/Anim_Mushroom_Idle.fbx"), MushroomJump()),
            ["Cow"] = Override(controller, "Cow", Clip(Cow + "Cow.fbx", "Idle"), Clip(Cow + "Cow.fbx", "Run")),
        };

        var entries = new List<AvatarDatabase.Species>();
        foreach (var spec in Specs)
        {
            RuntimeAnimatorController ctrl = spec.overrideController == null ? controller : overrides[spec.overrideController];
            var male = BuildAvatar(spec, spec.male, spec.female == null ? spec.id : spec.id + "_m", ctrl);
            var female = spec.female == null ? male : BuildAvatar(spec, spec.female, spec.id + "_w", ctrl);
            entries.Add(new AvatarDatabase.Species { id = spec.id, male = male, female = female });
        }

        var db = AssetDatabase.LoadAssetAtPath<AvatarDatabase>(Database);
        if (db == null)
        {
            db = ScriptableObject.CreateInstance<AvatarDatabase>();
            AssetDatabase.CreateAsset(db, Database);
        }
        var so = new SerializedObject(db);
        var list = so.FindProperty("species");
        list.arraySize = entries.Count;
        for (int i = 0; i < entries.Count; i++)
        {
            var e = list.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("id").stringValue = entries[i].id;
            e.FindPropertyRelative("male").objectReferenceValue = entries[i].male;
            e.FindPropertyRelative("female").objectReferenceValue = entries[i].female;
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        BuildNpc(controller);
        AssetDatabase.SaveAssets();
        Debug.Log("Avatars built: " + string.Join(", ", entries.Select(e => e.id)));
    }

    // Idle -> Jump (trigger, speed from JumpSpeed) -> Idle. Shared by all avatars and the NPC.
    static AnimatorController BuildController()
    {
        // Rebuilt in place so the asset keeps its GUID.
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Controller)
                         ?? AnimatorController.CreateAnimatorControllerAtPath(Controller);
        foreach (var p in controller.parameters)
            controller.RemoveParameter(p);
        var states = controller.layers[0].stateMachine;
        foreach (var child in states.states)
            states.RemoveState(child.state);
        controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("JumpSpeed", AnimatorControllerParameterType.Float);
        controller.parameters = controller.parameters.Select(p =>
        {
            if (p.name == "JumpSpeed") p.defaultFloat = 1f;
            return p;
        }).ToArray();

        var sm = controller.layers[0].stateMachine;
        var idle = sm.AddState("Idle");
        idle.motion = Clip(Anim + "Humanoid_Idle.anim");
        var jump = sm.AddState("Jump");
        jump.motion = Clip(Anim + "Humanoid_Jump.anim");
        jump.speedParameterActive = true;
        jump.speedParameter = "JumpSpeed";
        sm.defaultState = idle;

        var toJump = idle.AddTransition(jump);
        toJump.AddCondition(AnimatorConditionMode.If, 0, "Jump");
        toJump.hasExitTime = false;
        toJump.duration = 0.05f;
        var back = jump.AddTransition(idle);
        back.hasExitTime = true;
        back.exitTime = 0.95f;
        back.duration = 0.05f;
        return controller;
    }

    static AnimatorOverrideController Override(AnimatorController controller, string name, AnimationClip idle, AnimationClip jump)
    {
        string path = Anim + name + ".overrideController";
        var o = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
        if (o == null)
        {
            o = new AnimatorOverrideController(controller);
            AssetDatabase.CreateAsset(o, path);
        }
        o.runtimeAnimatorController = controller;
        o["Humanoid_Idle"] = idle;
        o["Humanoid_Jump"] = jump;
        EditorUtility.SetDirty(o);
        return o;
    }

    // The mushroom has separate take-off and landing clips; play them back to back as one jump.
    // Saved inside the (ignored) pack folder because it is derived from licensed data.
    static AnimationClip MushroomJump()
    {
        var parts = new[] { Clip(Mushroom + "Animations/Anim_Mushroom_Jump_In.fbx"), Clip(Mushroom + "Animations/Anim_Mushroom_Jump_Out.fbx") };
        var clip = new AnimationClip { frameRate = parts[0].frameRate };
        var curves = new Dictionary<EditorCurveBinding, AnimationCurve>();
        float offset = 0f;
        foreach (var part in parts)
        {
            foreach (var binding in AnimationUtility.GetCurveBindings(part))
            {
                if (!curves.TryGetValue(binding, out var curve))
                    curves[binding] = curve = new AnimationCurve();
                foreach (var key in AnimationUtility.GetEditorCurve(part, binding).keys)
                {
                    var k = key;
                    k.time += offset;
                    curve.AddKey(k);
                }
            }
            offset += part.length;
        }
        foreach (var pair in curves)
            AnimationUtility.SetEditorCurve(clip, pair.Key, pair.Value);

        string path = Mushroom + "Animations/Mushroom_Jump_Combined.anim";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    static AvatarBody BuildAvatar(Spec spec, string sourcePath, string name, RuntimeAnimatorController controller)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
        var root = new GameObject("Avatar_" + name);
        try
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(source, root.transform);
            model.transform.localScale = Vector3.one * spec.scale;
            foreach (var type in spec.removeComponents)
                foreach (var c in model.GetComponentsInChildren<Component>(true).Where(c => c != null && c.GetType().Name == type).ToArray())
                    Object.DestroyImmediate(c);

            var animator = model.GetComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            // The player's own body is mostly outside the camera view, but must keep animating for the mirrors.
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var bones = model.GetComponentsInChildren<Transform>(true);
            Vector3 eyes;
            Transform[] hidden;
            if (spec.hidden == null)
            {
                var head = animator.GetBoneTransform(HumanBodyBones.Head);
                eyes = head.position + HumanEyeOffset * spec.scale;
                hidden = new[] { head };
            }
            else
            {
                eyes = spec.eyeBones != null
                    ? spec.eyeBones.Select(n => Find(bones, n).position).Aggregate(Vector3.zero, (a, b) => a + b) / spec.eyeBones.Length
                    : spec.eyes.Value * spec.scale;
                hidden = spec.hidden.Select(n => Find(bones, n)).ToArray();
            }

            // Separate meshes (hat, eyebrows) are simply not rendered by the first-person camera.
            int hiddenLayer = LayerMask.NameToLayer(MirrorWorld.FirstPersonHiddenLayer);
            foreach (var r in model.GetComponentsInChildren<Renderer>(true).Where(r => spec.hiddenRenderers.Contains(r.name)))
                r.gameObject.layer = hiddenLayer;

            // Root on the ground below the eyes.
            model.transform.localPosition = new Vector3(-eyes.x, 0f, -eyes.z);
            var eyesMarker = new GameObject("Eyes").transform;
            eyesMarker.SetParent(root.transform, false);
            eyesMarker.localPosition = new Vector3(0f, eyes.y, 0f);

            var jumpClip = controller is AnimatorOverrideController o ? o["Humanoid_Jump"] : Clip(Anim + "Humanoid_Jump.anim");
            var body = root.AddComponent<AvatarBody>();
            var so = new SerializedObject(body);
            so.FindProperty("animator").objectReferenceValue = animator;
            so.FindProperty("eyes").objectReferenceValue = eyesMarker;
            var list = so.FindProperty("firstPersonHidden");
            list.arraySize = hidden.Length;
            for (int i = 0; i < hidden.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = hidden[i];
            so.FindProperty("jumpClipSeconds").floatValue = jumpClip.length;
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabFolder + root.name + ".prefab");
            return prefab.GetComponent<AvatarBody>();
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // Unpacked copy of the modular character with only the NPC parts, in a grey material.
    static void BuildNpc(AnimatorController controller)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModularHero));
        try
        {
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.name = "NPC";
            // The kit ships a demo randomizer (with on-screen help text and keyboard input).
            foreach (var script in go.GetComponentsInChildren<MonoBehaviour>(true))
                Object.DestroyImmediate(script);

            string matPath = "Assets/Art/NPC/NPC_Grey.mat";
            var renderers = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(renderers[0].sharedMaterial);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            foreach (var pair in NpcColors)
            {
                ColorUtility.TryParseHtmlString(pair.Value, out var color);
                mat.SetColor(pair.Key, color);
            }
            EditorUtility.SetDirty(mat);

            foreach (var r in renderers)
            {
                if (NpcParts.Contains(r.name))
                {
                    r.gameObject.SetActive(true);
                    r.sharedMaterial = mat;
                }
                else
                {
                    Object.DestroyImmediate(r.gameObject);
                }
            }
            // Empty category groups left over from the modular kit.
            foreach (var t in go.GetComponentsInChildren<Transform>(true).Reverse())
                if (t != null && t != go.transform && t.childCount == 0 && t.GetComponents<Component>().Length == 1 && !IsBone(go, t))
                    Object.DestroyImmediate(t.gameObject);

            var animator = go.GetComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            PrefabUtility.SaveAsPrefabAsset(go, "Assets/Prefabs/NPC.prefab");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    static bool IsBone(GameObject root, Transform t)
    {
        foreach (var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            if (r.bones.Contains(t) || r.rootBone == t)
                return true;
        return false;
    }

    static Transform Find(Transform[] bones, string name)
    {
        var bone = bones.FirstOrDefault(b => b.name == name);
        if (bone == null)
            throw new System.Exception("Bone not found: " + name);
        return bone;
    }

    static AnimationClip Clip(string path, string name = null)
    {
        var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview") && (name == null || c.name == name));
        if (clip == null)
            throw new System.Exception($"Clip not found: {path} {name}");
        return clip;
    }
}
