using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Follows resolved scene/asset object references, excluding prefab inheritance plumbing.</summary>
public static class SceneIntegritySkinUsage
{
    [Serializable] public sealed class Assignment
    {
        public string context, owner, material;
        public bool rendererEnabled, objectActive, sssKeyword;
        public float materialId;
    }
    [Serializable] public sealed class Report
    {
        public string utc;
        public string[] scenes;
        public int resolvedObjects, prefabRoots, scriptableObjects;
        public List<Assignment> assignments = new();
        public List<string> failures = new();
    }
    private static readonly HashSet<string> Plumbing = new()
    {
        "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset", "m_Script"
    };
    private static readonly Queue<UnityEngine.Object> pending = new();
    private static readonly HashSet<string> visited = new();
    private static Report report;

    public static void RunBatch()
    {
        try { Run(); EditorApplication.Exit(report.failures.Count == 0 ? 0 : 1); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(2); }
    }

    [MenuItem("Lit/Validation/Inspect Reachable Skin Materials")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode required.");
        pending.Clear(); visited.Clear();
        report = new Report { utc = DateTime.UtcNow.ToString("O"),
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray() };
        foreach (string path in report.scenes)
        {
            Scene preview = default;
            try
            {
                preview = EditorSceneManager.OpenPreviewScene(path);
                foreach (var root in preview.GetRootGameObjects()) InspectHierarchy(root, path);
                Drain();
            }
            catch (Exception error) { report.failures.Add(path + " | " + error.Message); }
            finally { if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview); }
        }
        // Runtime Resources.Load may bypass a serialized scene reference. Include those roots conservatively.
        foreach (string path in AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/") && p.Contains("/Resources/") &&
            (p.EndsWith(".prefab") || p.EndsWith(".asset"))))
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset is GameObject || asset is ScriptableObject) pending.Enqueue(asset);
        }
        Drain();
        report.resolvedObjects = visited.Count;
        Directory.CreateDirectory("Library/SceneIntegrity");
        File.WriteAllText("Library/SceneIntegrity/reachable-skin-usage.json", JsonUtility.ToJson(report, true));
        Debug.Log("[SceneIntegritySkinUsage] " + report.assignments.Count + " actual renderer assignments; " + report.failures.Count + " failures. Source assets unchanged.");
    }

    private static void Drain()
    {
        while (pending.Count > 0)
        {
            var asset = pending.Dequeue();
            if (asset == null || !visited.Add(asset.GetEntityId().ToString())) continue;
            string path = AssetDatabase.GetAssetPath(asset);
            try
            {
                if (asset is GameObject root)
                {
                    report.prefabRoots++;
                    InspectHierarchy(root, path);
                }
                else if (asset is ScriptableObject)
                {
                    report.scriptableObjects++;
                    InspectReferences(asset);
                }
            }
            catch (Exception error) { report.failures.Add(path + " | " + error.Message); }
        }
    }

    private static void InspectHierarchy(GameObject root, string context)
    {
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        foreach (var material in renderer.sharedMaterials)
        {
            if (material == null || !material.name.StartsWith("Ga_Skin_")) continue;
            report.assignments.Add(new Assignment { context = context, owner = SceneIntegrityAudit.Hierarchy(renderer.transform),
                material = AssetDatabase.GetAssetPath(material), rendererEnabled = renderer.enabled,
                objectActive = renderer.gameObject.activeSelf,
                materialId = material.HasProperty("_MaterialID") ? material.GetFloat("_MaterialID") : -1,
                sssKeyword = material.IsKeywordEnabled("_MATERIAL_FEATURE_SUBSURFACE_SCATTERING") });
        }
        foreach (var component in root.GetComponentsInChildren<Component>(true))
            if (component != null) InspectReferences(component);
    }

    private static void InspectReferences(UnityEngine.Object owner)
    {
        var serialized = new SerializedObject(owner);
        var property = serialized.GetIterator();
        while (property.Next(true))
        {
            if (property.propertyType != SerializedPropertyType.ObjectReference || Plumbing.Contains(property.propertyPath.Split('.')[0])) continue;
            var reference = property.objectReferenceValue;
            if (reference == null || !EditorUtility.IsPersistent(reference)) continue;
            if (reference is ScriptableObject) pending.Enqueue(reference);
            else
            {
                var go = reference as GameObject;
                if (reference is Component component) go = component.gameObject;
                if (go != null) pending.Enqueue(go.transform.root.gameObject);
            }
        }
    }
}
