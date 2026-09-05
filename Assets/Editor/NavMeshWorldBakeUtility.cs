using System.IO;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>Editor entry point for producing the optional zone NavMesh asset.</summary>
public static class NavMeshWorldBakeUtility
{
    [MenuItem("Lit/Navigation/Bake Zone NavMesh")]
    private static void BakeSelectedZone()
    {
        ZoneManifest manifest = Selection.activeObject as ZoneManifest;
        if (manifest == null)
        {
            Debug.LogWarning("[NavMeshWorld] Selectionnez un ZoneManifest avant le bake.");
            return;
        }

        if (string.IsNullOrWhiteSpace(manifest.PrimarySceneName))
        {
            Debug.LogError("[NavMeshWorld] Le ZoneManifest n'a pas de scene principale valide.");
            return;
        }

        Scene previousScene = SceneManager.GetActiveScene();
        string previousScenePath = previousScene.IsValid() ? previousScene.path : null;
        List<Scene> openedScenes = new List<Scene>();
        string primaryPath = FindScenePath(manifest.PrimarySceneName);
        if (string.IsNullOrWhiteSpace(primaryPath))
        {
            Debug.LogError("[NavMeshWorld] Scene principale absente des Build Settings : " + manifest.PrimarySceneName, manifest);
            return;
        }

        EditorSceneManager.OpenScene(primaryPath, OpenSceneMode.Single);
        openedScenes.Add(SceneManager.GetActiveScene());
        for (int i = 0; i < manifest.LoadingSceneNames.Count; i++)
        {
            string sceneName = manifest.LoadingSceneNames[i];
            string path = FindScenePath(sceneName);
            if (string.IsNullOrWhiteSpace(path))
            {
                Debug.LogError("[NavMeshWorld] Scene obligatoire absente des Build Settings : " + sceneName, manifest);
                RestoreScene(previousScene, previousScenePath);
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            openedScenes.Add(scene);
        }

        Physics.SyncTransforms();
        GameObject host = new GameObject("__NavMeshZoneBake");
        NavMeshSurface surface = host.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.layerMask = ~0;

        try
        {
            surface.BuildNavMesh();
            NavMeshData data = surface.navMeshData;
            if (data == null)
            {
                Debug.LogError("[NavMeshWorld] Le bake n'a produit aucun NavMeshData.", manifest);
                return;
            }

            SceneMarker[] markers = Object.FindObjectsByType<SceneMarker>(FindObjectsInactive.Exclude);
            for (int i = 0; i < markers.Length; i++)
            {
                SceneMarker marker = markers[i];
                if (marker == null || !marker.isActiveAndEnabled || !marker.UsesCharacter) continue;
                if (!NavMesh.SamplePosition(marker.transform.position, out NavMeshHit hit, 0.15f, NavMesh.AllAreas) ||
                    Mathf.Abs(hit.position.y - marker.transform.position.y) > 0.15f)
                {
                    Debug.LogError("[NavMeshWorld] Bake refuse : marker hors NavMesh local | marker=" +
                                   marker.name + " | attendu=" + marker.transform.position +
                                   " | sample=" + (hit.position == default ? "aucun" : hit.position.ToString()), marker);
                    return;
                }
            }

            string folder = "Assets/Navigation/NavMeshData";
            EnsureFolder(folder);
            string fileName = Sanitize(manifest.PrimarySceneName) + "_NavMeshData.asset";
            string path = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(folder, fileName));
            AssetDatabase.CreateAsset(Object.Instantiate(data), path);
            AssetDatabase.SaveAssets();

            SerializedObject serializedManifest = new SerializedObject(manifest);
            serializedManifest.FindProperty("bakedNavMeshData").objectReferenceValue = AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
            serializedManifest.FindProperty("bakedNavMeshVersion").stringValue = System.DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            serializedManifest.FindProperty("bakedNavMeshAgentTypeId").intValue = surface.agentTypeID;
            serializedManifest.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(manifest);
            AssetDatabase.SaveAssets();
            Debug.Log("[NavMeshWorld] NavMesh zone bake termine | zone=" + manifest.PrimarySceneName +
                      " | asset=" + path + ". Les scenes de la zone doivent etre chargees pour un bake complet.", manifest);
        }
        finally
        {
            Object.DestroyImmediate(host);
            for (int i = openedScenes.Count - 1; i >= 1; i--)
            {
                if (openedScenes[i].IsValid() && openedScenes[i].isLoaded)
                {
                    EditorSceneManager.CloseScene(openedScenes[i], true);
                }
            }
            RestoreScene(previousScene, previousScenePath);
        }
    }

    private static string FindScenePath(string sceneName)
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        for (int i = 0; i < scenes.Length; i++)
        {
            if (!scenes[i].enabled) continue;
            string path = scenes[i].path;
            if (string.Equals(Path.GetFileNameWithoutExtension(path), sceneName, System.StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }
        }
        return null;
    }

    private static void RestoreScene(Scene previousScene, string previousScenePath)
    {
        if (previousScene.IsValid() && previousScene.isLoaded)
        {
            EditorSceneManager.SetActiveScene(previousScene);
        }
        else if (!string.IsNullOrWhiteSpace(previousScenePath) && File.Exists(previousScenePath))
        {
            EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
        }
    }

    private static void EnsureFolder(string folder)
    {
        string[] parts = folder.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }
            current = next;
        }
    }

    private static string Sanitize(string value)
    {
        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalid, '_');
        }
        return value;
    }
}
