#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.IO;

namespace RPGFantasySFX
{
public static class RPGFantasySFXDemoSceneBuilder
{
    private const string RootFolder = "Assets/RPGFantasySFX";
    private const string DemoFolder = RootFolder + "/Demo";
    private const string ScenePath = DemoFolder + "/RPGFantasySFX_Demo.unity";

    [MenuItem("Tools/RPG Fantasy SFX/Create Demo Scene")]
    public static void CreateDemoScene()
    {
        if (!AssetDatabase.IsValidFolder(RootFolder))
        {
            EditorUtility.DisplayDialog("RPG Fantasy SFX", "Could not find " + RootFolder +
                ". Please import the sound pack into Assets/RPGFantasySFX first, or edit RootFolder in this script.", "OK");
            return;
        }
        if (!AssetDatabase.IsValidFolder(DemoFolder))
            AssetDatabase.CreateFolder(RootFolder, "Demo");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        var root = new GameObject("RPG Fantasy SFX Demo");
        var controller = root.AddComponent<RPGFantasySFXDemoController>();

        var clips = new List<AudioClip>();
        string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { RootFolder });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip != null) clips.Add(clip);
        }
        clips.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase));
        controller.SetClips(clips);

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("RPG Fantasy SFX Demo Created",
            "Created " + ScenePath + "\nFound " + clips.Count + " audio clips.\n\nOpen the scene and press Play to preview the sounds.",
            "OK");
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
    }
}
}
#endif
