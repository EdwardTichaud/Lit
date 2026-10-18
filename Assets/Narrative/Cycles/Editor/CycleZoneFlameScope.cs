#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Read-only coverage from the same required additive scenes as the runtime zone.</summary>
public sealed class CycleZoneFlameScope : IDisposable
{
    private readonly List<Scene> opened = new List<Scene>();
    private readonly Scene previous = SceneManager.GetActiveScene();
    public Flame[] Flames { get; }

    public CycleZoneFlameScope(ZoneManifest manifest)
    {
        try
        {
            if (manifest == null || manifest.primaryScene == null)
                throw new InvalidOperationException("Zone coverage requires its authored primary scene.");
            var scenes = manifest.loadingScenes.Prepend(manifest.primaryScene).ToArray();
            if (scenes.Any(scene => scene == null))
                throw new InvalidOperationException("A required zone scene reference is missing; coverage cannot be validated.");
            var flames = new List<Flame>();
            foreach (var asset in scenes.Distinct())
            {
                string path = AssetDatabase.GetAssetPath(asset);
                var scene = SceneManager.GetSceneByPath(path);
                if (!scene.IsValid() || !scene.isLoaded)
                { scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive); opened.Add(scene); }
                flames.AddRange(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Flame>(true)));
            }
            Flames = flames.Where(flame => flame.isActiveAndEnabled).Distinct().ToArray();
        }
        catch { Dispose(); throw; }
    }

    public static bool Covers(Flame flame, Vector3 position, bool requireLit)
    {
        if (flame == null || !flame.isActiveAndEnabled || requireLit && !flame.IsLit) return false;
        var influence = new SerializedObject(flame).FindProperty("litInfluence");
        return influence.FindPropertyRelative("enabled").boolValue &&
            Vector3.Distance(position, flame.transform.TransformPoint(influence.FindPropertyRelative("center").vector3Value)) <=
            influence.FindPropertyRelative("radius").floatValue;
    }

    public void Dispose()
    {
        foreach (var scene in opened.AsEnumerable().Reverse())
            if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
        opened.Clear();
        if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
    }
}
#endif
