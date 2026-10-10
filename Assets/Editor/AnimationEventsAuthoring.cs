using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

/// <summary>Install receivers for new authored components; reject incomplete game content before build.</summary>
[InitializeOnLoad]
public sealed class AnimationEventsAuthoring : IPreprocessBuildWithReport
{
    static AnimationEventsAuthoring() => ObjectFactory.componentWasAdded += OnComponentAdded;
    public int callbackOrder => 0;

    private static void OnComponentAdded(Component component)
    {
        if (component == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
        string path = AssetDatabase.GetAssetPath(component);
        var stage = PrefabStageUtility.GetPrefabStage(component.gameObject);
        if (stage != null) path = stage.assetPath;
        else if (string.IsNullOrEmpty(path) && component.gameObject.scene.path.StartsWith("Assets/", StringComparison.Ordinal)) path = component.gameObject.scene.path;
        if (!string.IsNullOrEmpty(path) && !IsGameAsset(path)) return;
        if (component is Animator || component is PlayableDirector)
            AnimationEvents.EnsureOn(component.gameObject);
    }

    public static bool IsGameAsset(string path)
    {
        string[] excluded = { "Assets/0 - UnityPackages/", "Assets/BrainsAI/", "Assets/_Recovery/", "Assets/Legacy/",
            "Assets/FreeGameSounds", "Assets/GVOZDY/", "Assets/Hovl Studio/", "Assets/North Ember Studios/",
            "Assets/Sci-Fi Dog/", "Assets/Werewolf/", "Assets/VFX/CharacterEffect/", "Assets/Characters/6_UCC_Opsive/Demo/" };
        return path.StartsWith("Assets/", StringComparison.Ordinal) && !excluded.Any(p => path.StartsWith(p, StringComparison.Ordinal));
    }

    public static void ValidateHierarchy(GameObject root, string path, List<string> errors)
    {
        foreach (var transform in root.GetComponentsInChildren<Transform>(true))
        {
            var go = transform.gameObject;
            bool required = go.GetComponent<Animator>() != null || go.GetComponent<PlayableDirector>() != null ||
                go.GetComponent<CharacterInfo>() != null || go.GetComponent<GhostController>() != null || go.GetComponent<LitBrainsEnemy>() != null;
            if (required && go.GetComponents<AnimationEvents>().Length != 1)
                errors.Add(path + ": " + go.name + " must have exactly one AnimationEvents.");
            var signals = go.GetComponent<SignalReceiver>();
            if (signals == null) continue;
            foreach (var signal in signals.GetRegisteredSignals())
            {
                if (signal == null) { errors.Add(path + ": missing Signal asset on " + go.name); continue; }
                var reaction = signals.GetReaction(signal);
                if (reaction == null) continue;
                for (int i = 0; i < reaction.GetPersistentEventCount(); i++)
                {
                    var target = reaction.GetPersistentTarget(i);
                    var method = reaction.GetPersistentMethodName(i);
                    if (target == null || !target.GetType().GetMethods().Any(m => m.Name == method))
                        errors.Add(path + ": invalid Signal " + method + " on " + go.name);
                    else if (target is CombatCinematicRig || target is LightSkillCinematicSequenceController || target is SpiritBondAnimationActions || target is LocomotionAnimationEvent)
                        errors.Add(path + ": Signal " + method + " must target AnimationEvents.");
                }
            }
        }
    }

    private static bool RepairHierarchy(GameObject root, bool previewOnly)
    {
        bool changed = false;
        foreach (var transform in root.GetComponentsInChildren<Transform>(true))
        {
            var go = transform.gameObject;
            if (go.GetComponent<Animator>() != null || go.GetComponent<PlayableDirector>() != null ||
                go.GetComponent<CharacterInfo>() != null || go.GetComponent<GhostController>() != null || go.GetComponent<LitBrainsEnemy>() != null)
            {
                if (go.GetComponent<AnimationEvents>() == null) { AnimationEvents.EnsureOn(go); changed = true; }
            }
            var events = go.GetComponent<AnimationEvents>();
            if (previewOnly && events != null && !events.PreviewOnly) { events.PreviewOnly = true; EditorUtility.SetDirty(events); changed = true; }
            var signals = go.GetComponent<SignalReceiver>();
            if (signals == null) continue;
            // Unassigned signal slots can never fire; remove the paired dead reactions.
            for (int i = signals.Count() - 1; i >= 0; i--)
                if (signals.GetSignalAssetAtIndex(i) == null) { signals.RemoveAtIndex(i); EditorUtility.SetDirty(signals); changed = true; }
        }
        return changed;
    }

    [MenuItem("Lit/Animation Events/Repair Game Content Receivers")]
    public static void RepairAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before repairing authored content.");
        var changed = new List<string>();
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!IsGameAsset(path) || !path.EndsWith(".prefab", StringComparison.Ordinal)) continue;
            var prefab = PrefabUtility.LoadPrefabContents(path);
            try { if (RepairHierarchy(prefab, path.Contains("AnimationLab"))) { PrefabUtility.SaveAsPrefabAsset(prefab, path); changed.Add(path); } }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
        }
        var active = SceneManager.GetActiveScene();
        try
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Scene"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!IsGameAsset(path)) continue;
                var scene = SceneManager.GetSceneByPath(path);
                bool opened = !scene.IsValid() || !scene.isLoaded;
                if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    bool modified = false;
                    foreach (var root in scene.GetRootGameObjects()) modified |= RepairHierarchy(root, path.Contains("AnimationLab"));
                    if (modified) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); changed.Add(path); }
                }
                finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
            }
        }
        finally { if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active); }
        File.WriteAllLines("Library/AnimationEvents-repaired.txt", changed);
        ValidateAll();
    }

    [MenuItem("Lit/Animation Events/Validate Game Content")]
    public static void ValidateAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before validating authored content.");
        var errors = new List<string>();
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!IsGameAsset(path) || !path.EndsWith(".prefab", StringComparison.Ordinal)) continue;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null) ValidateHierarchy(prefab, path, errors);
        }
        var active = SceneManager.GetActiveScene();
        try
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Scene"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!IsGameAsset(path)) continue;
                var scene = SceneManager.GetSceneByPath(path);
                bool opened = !scene.IsValid() || !scene.isLoaded;
                if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try { foreach (var root in scene.GetRootGameObjects()) ValidateHierarchy(root, path, errors); }
                finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
            }
        }
        finally { if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active); }
        if (errors.Count > 0) throw new BuildFailedException(string.Join("\n", errors));
        Debug.Log("[AnimationEvents] Game content validated; Timeline callbacks and receivers are valid.");
    }

    public void OnPreprocessBuild(BuildReport report) => ValidateAll();
}
