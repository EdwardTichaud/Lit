#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds the short original invocation pose used by LightSkill 1 Furie.
/// The clip only rotates Lucian's rig, so the cinematic keeps its current
/// world position and the next dash remains responsible for movement.
/// </summary>
public static class LightSkillHyoiGattaiAnimationBuilder
{
    private const string ClipPath = "Assets/CombatRealTime/LightSkills/Animations/LightSkill_1_Furie_Start_Temp.anim";
    private const string LucianPrefabPath = "Assets/Characters/1_Squad/Lucian/Player_Model_Lucian.prefab";

    [InitializeOnLoadMethod]
    private static void BuildInitialClipAfterReload()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            if (clip != null && Mathf.Abs(clip.length - 1.1f) < 0.02f)
            {
                return;
            }

            Build();
        };
    }

    [MenuItem("Lit/Combat/Build LightSkill 1 Invocation Pose")]
    public static void Build()
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        if (clip == null)
        {
            throw new InvalidOperationException("Le clip de depart Furie est introuvable.");
        }

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(LucianPrefabPath);
        try
        {
            Animator animator = prefabRoot.GetComponentsInChildren<Animator>(true)
                .FirstOrDefault(candidate => candidate.runtimeAnimatorController != null);
            if (animator == null)
            {
                throw new InvalidOperationException("Animator Lucian introuvable.");
            }

            ClearCurves(clip);
            clip.frameRate = 60f;
            clip.wrapMode = WrapMode.Once;
            clip.legacy = false;

            // 0.00 neutral, 0.18 recoil, 0.48 opening, 0.82 fusion, 1.10 resolve.
            Pose(animator.transform, "pelvis", new[] { 0f, 7f, 3f, -4f, -2f }, Vector3.right, clip);
            Pose(animator.transform, "spine_01", new[] { 0f, -8f, -14f, 7f, 3f }, Vector3.right, clip);
            Pose(animator.transform, "spine_02", new[] { 0f, -6f, -12f, 10f, 4f }, Vector3.right, clip);
            Pose(animator.transform, "spine_03", new[] { 0f, -4f, -9f, 13f, 6f }, Vector3.right, clip);

            Pose(animator.transform, "clavicle_l", new[] { 0f, -5f, -20f, 8f, 3f }, Vector3.forward, clip);
            Pose(animator.transform, "clavicle_r", new[] { 0f, 5f, 20f, -8f, -3f }, Vector3.forward, clip);
            Pose(animator.transform, "upperarm_l", new[] { 0f, -12f, -52f, 24f, 10f }, Vector3.forward, clip);
            Pose(animator.transform, "upperarm_r", new[] { 0f, 12f, 52f, -24f, -10f }, Vector3.forward, clip);
            Pose(animator.transform, "lowerarm_l", new[] { 0f, -8f, -20f, 38f, 22f }, Vector3.right, clip);
            Pose(animator.transform, "lowerarm_r", new[] { 0f, -8f, -20f, 38f, 22f }, Vector3.right, clip);
            Pose(animator.transform, "hand_l", new[] { 0f, 0f, 8f, -18f, -10f }, Vector3.up, clip);
            Pose(animator.transform, "hand_r", new[] { 0f, 0f, -8f, 18f, 10f }, Vector3.up, clip);

            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssets();
            Debug.Log("[LightSkill] Pose d'invocation Furie creee.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static void Pose(Transform animationRoot, string boneName, float[] offsets, Vector3 axis, AnimationClip clip)
    {
        Transform bone = FindDescendant(animationRoot, boneName);
        if (bone == null)
        {
            Debug.LogWarning("[LightSkill] Os absent de la pose Furie: " + boneName);
            return;
        }

        string path = AnimationUtility.CalculateTransformPath(bone, animationRoot);
        Vector3 baseEuler = bone.localEulerAngles;
        float[] times = { 0f, 0.18f, 0.48f, 0.82f, 1.1f };
        SetAxisCurve(path, "localEulerAnglesRaw.x", baseEuler.x, axis.x, times, offsets, clip);
        SetAxisCurve(path, "localEulerAnglesRaw.y", baseEuler.y, axis.y, times, offsets, clip);
        SetAxisCurve(path, "localEulerAnglesRaw.z", baseEuler.z, axis.z, times, offsets, clip);
    }

    private static void SetAxisCurve(
        string path,
        string property,
        float baseValue,
        float axisWeight,
        float[] times,
        float[] offsets,
        AnimationClip clip)
    {
        if (Mathf.Approximately(axisWeight, 0f))
        {
            return;
        }

        Keyframe[] keys = new Keyframe[times.Length];
        for (int index = 0; index < times.Length; index++)
        {
            keys[index] = new Keyframe(times[index], baseValue + offsets[index] * axisWeight);
        }

        AnimationCurve curve = new AnimationCurve(keys);
        for (int index = 0; index < keys.Length; index++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, index, AnimationUtility.TangentMode.ClampedAuto);
            AnimationUtility.SetKeyRightTangentMode(curve, index, AnimationUtility.TangentMode.ClampedAuto);
        }

        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), property), curve);
    }

    private static Transform FindDescendant(Transform root, string name)
    {
        return root.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(candidate => candidate.name == name);
    }

    private static void ClearCurves(AnimationClip clip)
    {
        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
        {
            AnimationUtility.SetEditorCurve(clip, binding, null);
        }

        foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
        {
            AnimationUtility.SetObjectReferenceCurve(clip, binding, null);
        }

        AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
    }
}
#endif
