using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Explicit native authoring and audit; never generates content on Play.</summary>
public static partial class SecretPassagesAuthoring
{
    internal const string Folder = "Assets/Narrative/SecretPassages/Content";
    internal const string D1Scene = "Assets/Scenes/District_1/District_1_AbilityShortcuts.unity";
    private static SceneSetup[] Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode required.");
        for (int i = 0; i < SceneManager.sceneCount; i++) if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Unsaved scene.");
        if (SceneManager.sceneCount == 0 || string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
            EditorSceneManager.OpenScene(CourtsAuthoring.CorePath, OpenSceneMode.Single);
        return EditorSceneManager.GetSceneManagerSetup();
    }
    public static void InspectDistrictPassages()
    {
        var setup = Setup();
        try
        {
            var scene = EditorSceneManager.OpenScene(D1Scene, OpenSceneMode.Single);
            var passages = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TrouEtroit>(true)).ToArray();
            Directory.CreateDirectory("Library/Zone2Secrets");
            File.WriteAllLines("Library/Zone2Secrets/DistrictInspection.txt", new[] { "Actual logical passages: " + passages.Length }.Concat(
                passages.Select(p => $"{p.name}; id={NetcodeSceneIdUtility.GetStableId(p.transform)}; A={p.teleportPointA?.position}; B={p.teleportPointB?.position}")));
            Debug.Log("[SecretPassages] Inspected " + passages.Length + " logical District 1 passages. No scene saved.");
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
    }
}
