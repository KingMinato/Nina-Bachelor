using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// Builds the humanoid idle and jump clips shared by all humanoid avatars and the NPC.
// Poses are written as muscle values on top of a relaxed standing pose. Keys marked as grounded
// are corrected so the feet stand on the floor; then the body height is baked per frame so heel and toe
// never go below the floor, taking the rig's jump height into account. Measured on a reference model.
// Menu: Mirror Game/Build Humanoid Clips. Timing of the jump matches PlayerActions
// (0.15 s windup, 0.6 s in the air, 0.2 s landing).
public static class HumanoidClipBuilder
{
    const string Folder = "Assets/Animations/";
    const string ReferenceModel = "Assets/Synty/PolygonGeneric/Prefabs/Characters/SM_Gen_Chr_Street_Male_02.prefab";

    class Key
    {
        public float time;
        public bool grounded;
        public Dictionary<string, float> muscles;
    }

    // Arms hanging relaxed, legs straight, fingers slightly curled. Both sides get the same values.
    static readonly Dictionary<string, float> Stand = new Dictionary<string, float>
    {
        ["Upper Leg Front-Back"] = 0.6f, ["Upper Leg Twist In-Out"] = -0.06f, ["Lower Leg Stretch"] = 1f,
        ["Shoulder Front-Back"] = 0.3f, ["Arm Down-Up"] = -0.65f, ["Arm Front-Back"] = 0.3f,
        ["Arm Twist In-Out"] = 0f, ["Forearm Stretch"] = 0.85f, ["Forearm Twist In-Out"] = 0f,
        ["Fingers Stretched"] = 0.35f, ["Thumb Stretched"] = 0.5f,
    };

    [MenuItem("Mirror Game/Build Humanoid Clips")]
    public static void Build()
    {
        var idle = new List<Key>
        {
            K(0f, true),
            K(1.5f, true, ("Chest Front-Back", -0.06f), ("Spine Front-Back", 0.03f), ("Arm Down-Up", -0.6f), ("Head Nod Down-Up", 0.04f)),
            K(3f, true),
        };
        // Muscle directions: Upper Leg Front-Back 0.6 = straight, lower = forward; Lower Leg Stretch 1 = straight;
        // Arm Front-Back lower = forward; Foot Up-Down higher = toes down; Spine/Chest Front-Back higher = bend forward.
        var jump = new List<Key>
        {
            K(0f, true),
            K(0.15f, true, ("Upper Leg Front-Back", -0.1f), ("Lower Leg Stretch", -0.2f), ("Foot Up-Down", -0.3f),
                ("Spine Front-Back", 0.35f), ("Chest Front-Back", 0.15f), ("Arm Front-Back", 0.7f), ("Arm Down-Up", -0.6f), ("Forearm Stretch", 0.8f)),
            K(0.32f, false, ("Foot Up-Down", 0.5f), ("Spine Front-Back", -0.05f), ("Arm Down-Up", 0.45f), ("Arm Front-Back", -0.5f), ("Forearm Stretch", 0.9f)),
            K(0.5f, false, ("Upper Leg Front-Back", 0f), ("Lower Leg Stretch", 0f), ("Foot Up-Down", 0.2f),
                ("Arm Down-Up", 0.2f), ("Arm Front-Back", -0.3f)),
            K(0.72f, false, ("Upper Leg Front-Back", 0.4f), ("Lower Leg Stretch", 0.8f), ("Foot Up-Down", 0.2f),
                ("Arm Down-Up", 0f), ("Arm Front-Back", -0.2f)),
            K(0.8f, true, ("Upper Leg Front-Back", 0.1f), ("Lower Leg Stretch", 0.1f), ("Foot Up-Down", -0.3f),
                ("Spine Front-Back", 0.25f), ("Arm Down-Up", -0.3f), ("Arm Front-Back", -0.3f)),
            K(0.95f, true),
        };

        var reference = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ReferenceModel));
        try
        {
            var animator = reference.GetComponent<Animator>();
            Save(BuildClip(idle, animator, loop: true, t => 0f), "Humanoid_Idle");
            Save(BuildClip(jump, animator, loop: false, JumpLift), "Humanoid_Jump");
        }
        finally
        {
            Object.DestroyImmediate(reference);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Humanoid clips built.");
    }

    static Key K(float time, bool grounded, params (string muscle, float value)[] overrides)
    {
        var muscles = new Dictionary<string, float>(Stand);
        foreach (var (muscle, value) in overrides)
            muscles[muscle] = value;
        return new Key { time = time, grounded = grounded, muscles = muscles };
    }

    // Height of the player rig above the floor at time t of the jump clip, as moved by PlayerActions
    // (0.15 s windup, 0.6 s in the air with 0.5 m peak, then landing).
    static float JumpLift(float t)
    {
        float x = (t - 0.15f) / 0.6f;
        return x <= 0f || x >= 1f ? 0f : 4f * 0.5f * x * (1f - x);
    }

    static AnimationClip BuildClip(List<Key> keys, Animator reference, bool loop, System.Func<float, float> lift)
    {
        var handler = new HumanPoseHandler(reference.avatar, reference.transform);
        var restPose = new HumanPose();
        handler.GetHumanPose(ref restPose);
        var feet = new Feet(reference);
        float restFeet = feet.Lowest();

        var curves = new AnimationCurve[HumanTrait.MuscleCount];
        for (int i = 0; i < curves.Length; i++)
            curves[i] = new AnimationCurve();
        var keyedY = new AnimationCurve();

        // Grounded keys are moved so the feet stand on the floor, keys in the air use the standing height.
        foreach (var key in keys)
        {
            var pose = Pose(restPose, key.muscles);
            if (key.grounded)
            {
                for (int pass = 0; pass < 3; pass++)
                {
                    handler.SetHumanPose(ref pose);
                    pose.bodyPosition.y -= feet.Lowest() - restFeet;
                }
            }
            for (int i = 0; i < HumanTrait.MuscleCount; i++)
                curves[i].AddKey(key.time, pose.muscles[i]);
            keyedY.AddKey(key.time, pose.bodyPosition.y);
        }
        Smooth(keyedY);
        foreach (var curve in curves)
            Smooth(curve);

        // Every frame: raise the body wherever heel or toe would go below the floor (the rig may be lifted).
        var rootY = new AnimationCurve();
        float end = keys[keys.Count - 1].time;
        int frames = Mathf.RoundToInt(end * 30f);
        for (int f = 0; f <= frames; f++)
        {
            float t = end * f / frames;
            var pose = Pose(restPose, null);
            for (int i = 0; i < HumanTrait.MuscleCount; i++)
                pose.muscles[i] = curves[i].Evaluate(t);
            pose.bodyPosition.y = keyedY.Evaluate(t);
            float floor = -lift(t);
            for (int pass = 0; pass < 3; pass++)
            {
                handler.SetHumanPose(ref pose);
                float below = floor - (feet.Lowest() - restFeet);
                if (below <= 0f)
                    break;
                pose.bodyPosition.y += below;
            }
            rootY.AddKey(new Keyframe(t, pose.bodyPosition.y / reference.humanScale));
        }
        for (int i = 0; i < rootY.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(rootY, i, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyRightTangentMode(rootY, i, AnimationUtility.TangentMode.Linear);
        }
        handler.Dispose();

        var clip = new AnimationClip { frameRate = 30 };
        for (int i = 0; i < HumanTrait.MuscleCount; i++)
            AnimationUtility.SetEditorCurve(clip, Binding(CurveName(HumanTrait.MuscleName[i])), curves[i]);
        Set(clip, "RootT.x", Constant(keys, restPose.bodyPosition.x / reference.humanScale));
        AnimationUtility.SetEditorCurve(clip, Binding("RootT.y"), rootY);
        Set(clip, "RootT.z", Constant(keys, restPose.bodyPosition.z / reference.humanScale));
        Set(clip, "RootQ.x", Constant(keys, restPose.bodyRotation.x));
        Set(clip, "RootQ.y", Constant(keys, restPose.bodyRotation.y));
        Set(clip, "RootQ.z", Constant(keys, restPose.bodyRotation.z));
        Set(clip, "RootQ.w", Constant(keys, restPose.bodyRotation.w));

        // No root motion: everything is baked into the pose, based on the original root.
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        settings.loopBlendOrientation = true;
        settings.loopBlendPositionY = true;
        settings.loopBlendPositionXZ = true;
        settings.keepOriginalOrientation = true;
        settings.keepOriginalPositionY = true;
        settings.keepOriginalPositionXZ = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        return clip;
    }

    // "Left Arm Down-Up" -> "Arm Down-Up"; fingers share "Fingers Stretched" / "Thumb Stretched".
    static float MuscleValue(Dictionary<string, float> muscles, string name)
    {
        string side = Regex.Replace(name, "^(Left|Right) ", "");
        if (muscles.TryGetValue(side, out float value))
            return value;
        var finger = Regex.Match(side, @"^(Thumb|Index|Middle|Ring|Little) \d Stretched$");
        if (finger.Success)
            return muscles[finger.Groups[1].Value == "Thumb" ? "Thumb Stretched" : "Fingers Stretched"];
        return 0f;
    }

    // Finger muscles are named differently in clips: "Left Index 1 Stretched" -> "LeftHand.Index.1 Stretched".
    static string CurveName(string muscle)
    {
        var m = Regex.Match(muscle, @"^(Left|Right) (Thumb|Index|Middle|Ring|Little) (.+)$");
        return m.Success ? $"{m.Groups[1].Value}Hand.{m.Groups[2].Value}.{m.Groups[3].Value}" : muscle;
    }

    // Heel and toe tip of both feet, stored as offsets from the foot and toe bones in the rest pose.
    class Feet
    {
        readonly Transform[] bones;
        readonly Vector3[] points;

        public Feet(Animator a)
        {
            var leftFoot = a.GetBoneTransform(HumanBodyBones.LeftFoot);
            var rightFoot = a.GetBoneTransform(HumanBodyBones.RightFoot);
            var leftToes = a.GetBoneTransform(HumanBodyBones.LeftToes);
            var rightToes = a.GetBoneTransform(HumanBodyBones.RightToes);
            bones = new[] { leftFoot, rightFoot, leftToes, rightToes };
            points = new[] { Heel(leftFoot, leftToes), Heel(rightFoot, rightToes), Tip(leftToes, leftFoot), Tip(rightToes, rightFoot) };
        }

        public float Lowest()
        {
            float lowest = float.MaxValue;
            for (int i = 0; i < bones.Length; i++)
                lowest = Mathf.Min(lowest, bones[i].TransformPoint(points[i]).y, bones[i].position.y);
            return lowest;
        }

        // Below the ankle at toe height, a bit behind it.
        static Vector3 Heel(Transform foot, Transform toes)
        {
            Vector3 forward = Vector3.ProjectOnPlane(toes.position - foot.position, Vector3.up).normalized;
            return foot.InverseTransformPoint(new Vector3(foot.position.x, toes.position.y, foot.position.z) - forward * 0.05f);
        }

        // In front of the toe joint.
        static Vector3 Tip(Transform toes, Transform foot)
        {
            Vector3 forward = Vector3.ProjectOnPlane(toes.position - foot.position, Vector3.up).normalized;
            return toes.InverseTransformPoint(toes.position + forward * 0.08f);
        }
    }

    static HumanPose Pose(HumanPose rest, Dictionary<string, float> muscles)
    {
        var pose = new HumanPose { bodyPosition = rest.bodyPosition, bodyRotation = rest.bodyRotation, muscles = new float[HumanTrait.MuscleCount] };
        if (muscles != null)
            for (int i = 0; i < HumanTrait.MuscleCount; i++)
                pose.muscles[i] = MuscleValue(muscles, HumanTrait.MuscleName[i]);
        return pose;
    }

    static void Smooth(AnimationCurve curve)
    {
        for (int i = 0; i < curve.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
        }
    }

    static EditorCurveBinding Binding(string property) => EditorCurveBinding.FloatCurve("", typeof(Animator), property);

    static AnimationCurve Constant(List<Key> keys, float value) =>
        AnimationCurve.Constant(0f, keys[keys.Count - 1].time, value);

    static void Set(AnimationClip clip, string property, AnimationCurve curve)
    {
        Smooth(curve);
        AnimationUtility.SetEditorCurve(clip, Binding(property), curve);
    }

    static void Save(AnimationClip clip, string name)
    {
        string path = Folder + name + ".anim";
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(clip, path);
            return;
        }
        // Keep the asset (and its GUID) so controllers referencing it stay intact.
        EditorUtility.CopySerialized(clip, existing);
        existing.name = name;
    }
}
