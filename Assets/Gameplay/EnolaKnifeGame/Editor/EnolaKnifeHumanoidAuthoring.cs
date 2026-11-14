using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

[InitializeOnLoad]
internal static class EnolaKnifeHumanoidAuthoring
{
    static EnolaKnifeHumanoidAuthoring() => EditorApplication.update += Poll;

    private static void Poll()
    {
        string request = EnolaKnifeAuthoring.Root + "/Humanoid.request";
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
            EditorApplication.isUpdating || !File.Exists(request)) return;
        File.Delete(request);
        try { Install(); File.WriteAllText(EnolaKnifeAuthoring.Root + "/Humanoid.result", "PASS"); }
        catch (Exception e) { File.WriteAllText(EnolaKnifeAuthoring.Root + "/Humanoid.result", e.ToString()); }
    }

    [MenuItem("Lit/Enola/Bind humanoid knife clips")]
    internal static void Install()
    {
        const string root = "Assets/Gameplay/EnolaKnifeGame/";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(root + "EnolaKnife.controller");
        var sources = controller.layers[0].stateMachine.states.Select(s =>
        {
            string path = "Assets/TricksUp/FFFAnimPack/Prefabs/" + s.state.name + ".FBX";
            var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal));
            if (clip == null || !clip.humanMotion)
                throw new InvalidOperationException($"{path} lacks a Humanoid animation (found {clip?.name ?? "none"}).");
            return (s.state, clip);
        }).ToArray();

        foreach (var (state, source) in sources)
        {
            string path = root + state.name + "_Humanoid.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = UnityEngine.Object.Instantiate(source);
                clip.name = state.name + "_Humanoid";
                AssetDatabase.CreateAsset(clip, path);
            }
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = state.name == EnolaKnifeGame.Idle;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            state.motion = clip;
            EditorUtility.SetDirty(state);
        }
        AssetDatabase.SaveAssets();
    }
}
