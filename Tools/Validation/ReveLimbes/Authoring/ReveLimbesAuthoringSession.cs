using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Temporary, explicitly requested authoring session. No action on Play and
// no regeneration of authored scenes. Remove once the new zone is delivered.
[InitializeOnLoad]
public static partial class ReveLimbesAuthoringSession
{
    private const string Output = "Library/ReveLimbesAuthoring";
    private const string Pack = "Assets/0 - UnityPackages/Fab/ModularDungeonCollection/Prefabs";
    private static bool running;
    [Serializable] private sealed class Catalog { public List<CatalogModule> modules = new(); }
    [Serializable] private sealed class CatalogModule
    {
        public string path;
        public Vector3 minimum, maximum;
        public int renderers, colliders;
        public List<string> materials = new();
    }
    static ReveLimbesAuthoringSession() => EditorApplication.update += Poll;
    private static void Poll()
    {
        if (AssetDatabase.IsAssetImportWorkerProcess()) return;
        PollMaterialAdaptation();
        PollPreview();
        PollExistingIntegration();
        PollCreation();
        PollQuestCreation();
        PollAccessCreation();
        string request = Output + "/Inspect.request";
        if (running || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isUpdating ||
            EditorApplication.isCompiling || !File.Exists(request)) return;
        File.Delete(request); running = true;
        try
        {
            var catalog = new Catalog();
            foreach (string path in Directory.GetFiles(Pack, "*.prefab").OrderBy(path => path))
            {
                GameObject root = PrefabUtility.LoadPrefabContents(path.Replace('\\', '/'));
                try
                {
                    var renderers = root.GetComponentsInChildren<Renderer>(true);
                    bool first = true; var bounds = new Bounds();
                    foreach (var renderer in renderers)
                    {
                        if (first) { bounds = renderer.bounds; first = false; }
                        else bounds.Encapsulate(renderer.bounds);
                    }
                    catalog.modules.Add(new CatalogModule
                    {
                        path = path.Replace('\\', '/'), minimum = bounds.min - root.transform.position,
                        maximum = bounds.max - root.transform.position, renderers = renderers.Length,
                        colliders = root.GetComponentsInChildren<Collider>(true).Length,
                        materials = renderers.SelectMany(renderer => renderer.sharedMaterials).Where(material => material != null)
                            .Select(material => AssetDatabase.GetAssetPath(material) + " | " + material.shader.name).Distinct().ToList()
                    });
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/Catalog.json", JsonUtility.ToJson(catalog, true));
            File.WriteAllText(Output + "/Inspect.result", "FINISHED modules=" + catalog.modules.Count + "; pid=" + System.Diagnostics.Process.GetCurrentProcess().Id);
        }
        catch (Exception error) { Directory.CreateDirectory(Output); File.WriteAllText(Output + "/Inspect.result", "FAILED " + error); }
        finally { running = false; }
    }
}
