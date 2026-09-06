using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>Samples animation motion on Lucian's avatar without running any gameplay component.</summary>
public sealed class PlayerInPlaceSampling : IDisposable
{
    public sealed class Samples
    {
        public float duration, humanScale;
        public Vector3[] positions;
        public float[] yaw;
        public float Distance => Vector3.ProjectOnPlane(positions[positions.Length - 1], Vector3.up).magnitude;
        public float MaxDisplacement
        {
            get { float max = 0; foreach (var p in positions) max = Mathf.Max(max, p.magnitude); return max; }
        }
        public float MaxYaw
        {
            get { float max = 0; foreach (var v in yaw) max = Mathf.Max(max, Mathf.Abs(v)); return max; }
        }
    }
    private readonly GameObject root;
    private Animator animator;
    private readonly Avatar avatar;
    private readonly Transform[] bones;
    private readonly Vector3[] restPositions;
    private readonly Quaternion[] restRotations;
    public PlayerInPlaceSampling()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerInPlaceAudit.LucianPath);
        var source = prefab.GetComponent<CombatActorAnimationRoot>().Animator;
        if (source == null || !source.isHuman || source.avatar == null)
            throw new InvalidOperationException("Lucian's humanoid Animator is required for motion sampling.");
        root = CopyHierarchy(source.transform);
        root.hideFlags = HideFlags.HideAndDontSave;
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        root.transform.localScale = source.transform.lossyScale;
        animator = root.AddComponent<Animator>();
        animator.avatar = source.avatar;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.fireEvents = false;
        animator.applyRootMotion = true;
        avatar = source.avatar;
        bones = root.GetComponentsInChildren<Transform>();
        restPositions = Array.ConvertAll(bones, t => t.localPosition);
        restRotations = Array.ConvertAll(bones, t => t.localRotation);
    }
    private static GameObject CopyHierarchy(Transform source)
    {
        var copy = new GameObject(source.name);
        copy.hideFlags = HideFlags.HideAndDontSave;
        copy.transform.localPosition = source.localPosition;
        copy.transform.localRotation = source.localRotation;
        copy.transform.localScale = source.localScale;
        foreach (Transform child in source)
        {
            var cloned = CopyHierarchy(child);
            cloned.transform.SetParent(copy.transform, false);
        }
        return copy;
    }
    public Samples Sample(AnimationClip clip)
    {
        // Rebind alone retains native root-motion history across different playable clips.
        // A fresh Animator and bind pose make the measurements independent of clip order.
        UnityEngine.Object.DestroyImmediate(animator);
        for (int i = 0; i < bones.Length; i++) bones[i].SetLocalPositionAndRotation(restPositions[i], restRotations[i]);
        animator = root.AddComponent<Animator>();
        animator.avatar = avatar;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.fireEvents = false;
        animator.applyRootMotion = true;
        int count = Mathf.Max(2, Mathf.CeilToInt(clip.length * 60f));
        var result = new Samples { duration = clip.length, positions = new Vector3[count + 1], yaw = new float[count + 1], humanScale = animator.humanScale };
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        animator.Rebind();
        var graph = PlayableGraph.Create("Player InPlace measurement");
        try
        {
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var playable = AnimationClipPlayable.Create(graph, clip);
            playable.SetApplyFootIK(false);
            playable.SetApplyPlayableIK(false);
            var output = AnimationPlayableOutput.Create(graph, "Lucian", animator);
            output.SetSourcePlayable(playable);
            graph.Play();
            graph.Evaluate(0);
            Quaternion rotation = Quaternion.identity;
            float dt = clip.length / count;
            for (int i = 1; i <= count; i++)
            {
                graph.Evaluate(dt);
                result.positions[i] = result.positions[i - 1] + animator.deltaPosition;
                float before = rotation.eulerAngles.y;
                rotation *= animator.deltaRotation;
                result.yaw[i] = result.yaw[i - 1] + Mathf.DeltaAngle(before, rotation.eulerAngles.y);
            }
        }
        finally { graph.Destroy(); }
        return result;
    }
    public void Dispose() { UnityEngine.Object.DestroyImmediate(root); }

    public static AnimationCurve Curve(float[] values)
    {
        var keys = new Keyframe[values.Length];
        for (int i = 0; i < keys.Length; i++) keys[i] = new Keyframe((float)i / (keys.Length - 1), values[i]);
        var curve = new AnimationCurve(keys);
        for (int i = 0; i < keys.Length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
        }
        return curve;
    }

    public static void Neutralize(AnimationClip clip, Samples source)
    {
        foreach (var binding in AnimationUtility.GetCurveBindings(clip))
        {
            if (binding.path != "") continue;
            string property = binding.propertyName;
            bool position = property.StartsWith("RootT.") || property.StartsWith("MotionT.") ||
                binding.type == typeof(Transform) && property.StartsWith("m_LocalPosition.");
            bool rotation = property.StartsWith("RootQ.") || property.StartsWith("MotionQ.") ||
                binding.type == typeof(Transform) && property.StartsWith("m_LocalRotation.");
            if (!position && !rotation) continue;
            var curve = AnimationUtility.GetEditorCurve(clip, binding);
            if (property.StartsWith("RootQ.")) continue; // Process the four channels together below.
            if (property.StartsWith("RootT."))
            {
                // Remove only the measured physical path, retaining body bob, lean and crouch.
                int axis = property.EndsWith(".x") ? 0 : property.EndsWith(".y") ? 1 : 2;
                var keys = new List<Keyframe>();
                for (int i = 0; i < source.positions.Length; i++)
                {
                    float t = clip.length * i / (source.positions.Length - 1);
                    keys.Add(new Keyframe(t, curve.Evaluate(t) - source.positions[i][axis] / Mathf.Max(.001f, source.humanScale)));
                }
                AnimationUtility.SetEditorCurve(clip, binding, new AnimationCurve(keys.ToArray()));
            }
            else AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.Constant(0, clip.length, curve.Evaluate(0)));
        }
        var rotations = new AnimationCurve[4];
        var corrected = new Keyframe[4][];
        const string axes = "xyzw";
        for (int axis = 0; axis < 4; axis++)
        {
            rotations[axis] = AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), "RootQ." + axes[axis]));
            corrected[axis] = new Keyframe[source.yaw.Length];
        }
        if (Array.TrueForAll(rotations, curve => curve != null))
        {
            Quaternion previous = Quaternion.identity;
            for (int i = 0; i < source.yaw.Length; i++)
            {
                float t = clip.length * i / (source.yaw.Length - 1);
                var body = new Quaternion(rotations[0].Evaluate(t), rotations[1].Evaluate(t), rotations[2].Evaluate(t), rotations[3].Evaluate(t));
                // Keep pitch/roll and residual body facing; remove the extracted physical yaw only.
                var value = Quaternion.Inverse(Quaternion.Euler(0, source.yaw[i], 0)) * body;
                if (i > 0 && Quaternion.Dot(previous, value) < 0) value = new Quaternion(-value.x, -value.y, -value.z, -value.w);
                for (int axis = 0; axis < 4; axis++) corrected[axis][i] = new Keyframe(t, value[axis]);
                previous = value;
            }
            for (int axis = 0; axis < 4; axis++)
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), "RootQ." + axes[axis]), new AnimationCurve(corrected[axis]));
        }
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopBlendOrientation = true;
        settings.loopBlendPositionXZ = true;
        settings.loopBlendPositionY = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
    }
}
