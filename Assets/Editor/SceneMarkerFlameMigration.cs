using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Updates already baked Flame markers to the torch-cost authoring model.</summary>
[InitializeOnLoad]
public static class SceneMarkerFlameMigration
{
    private const string RequestPath = "Library/SceneMarkerFlameMigration.request";
    private const string ResultPath = "Library/SceneMarkerFlameMigration.result";

    static SceneMarkerFlameMigration()
    {
        EditorApplication.delayCall += RunRequestedMigration;
        EditorApplication.update += RunRequestedMigration;
    }

    [MenuItem("Lit/Scene Marker/Migrate Flame Markers To Torch")]
    public static void MigrateFlameMarkersToTorch()
    {
        int migrated = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                bool dirty = false;
                foreach (GameObject gameObject in Resources.FindObjectsOfTypeAll<GameObject>())
                {
                    if (gameObject.scene != scene || !gameObject.TryGetComponent(out SceneMarker marker) || !marker.UsesFlame)
                        continue;

                    Flame flame = marker.BakedFlameInstance != null
                        ? marker.BakedFlameInstance.GetComponentInChildren<Flame>(true)
                        : gameObject.GetComponentInChildren<Flame>(true);
                    if (flame == null)
                        continue;

                    marker.ConfigureBakedFlame(flame);
                    EditorUtility.SetDirty(marker);
                    EditorUtility.SetDirty(flame);
                    dirty = true;
                    migrated++;
                }

                if (dirty)
                    EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[SceneMarker] Migrated {migrated} Flame marker(s) to torch costs and shared light/influence radius.");
    }

    private static void RunRequestedMigration()
    {
        if (!File.Exists(RequestPath) || EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;

        try
        {
            File.Delete(RequestPath);
            MigrateFlameMarkersToTorch();
            File.WriteAllText(ResultPath, "success " + DateTime.UtcNow.ToString("O"));
        }
        catch (Exception exception)
        {
            File.WriteAllText(ResultPath, "failure\n" + exception);
            Debug.LogException(exception);
        }
    }
}
