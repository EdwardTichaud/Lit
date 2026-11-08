using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Reads saved scenes in previews and inspects their actual dependency assets, without saving them.</summary>
public static class SceneIntegrityAudit
{
    [Serializable] public sealed class Issue
    {
        public string asset, owner, property, detail;
    }
    [Serializable] public sealed class Report
    {
        public string utc;
        public int scenes, prefabs, materials;
        public List<string> scenePaths = new();
        public List<string> intentionalParticleEmittersWithoutMaterial = new();
        public List<Issue> issues = new();
        public bool passed;
    }
    private static Report report;
    private static string currentAsset;
    private static readonly HashSet<string> keys = new();

    [MenuItem("Lit/Validation/Audit All Saved Game Scenes")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode required.");
        report = new Report { utc = DateTime.UtcNow.ToString("O") }; keys.Clear();
        var paths = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" })
            .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p).ToArray();
        Application.logMessageReceived += OnLog;
        try
        {
            foreach (string path in paths)
            {
                currentAsset = path;
                Scene preview = default;
                try
                {
                    preview = EditorSceneManager.OpenPreviewScene(path);
                    foreach (var root in preview.GetRootGameObjects()) InspectHierarchy(root);
                    report.scenes++; report.scenePaths.Add(path);
                }
                catch (Exception error) { Add(path, "scene", "open", error.Message); }
                finally { if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview); }
            }
            foreach (string path in AssetDatabase.GetDependencies(paths, true).Distinct().OrderBy(p => p))
            {
                currentAsset = path;
                if (path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null) Add(path, "prefab", "load", "Prefab cannot be loaded");
                    else { InspectHierarchy(prefab); report.prefabs++; }
                }
                else if (path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
                {
                    var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (material == null) Add(path, "material", "load", "Material cannot be loaded");
                    else
                    {
                        InspectReferences(material, material.name); report.materials++;
                        if (material.shader == null || material.shader.name == "Hidden/InternalErrorShader")
                            Add(path, material.name, "shader", "Missing shader");
                        else if (ShaderUtil.ShaderHasError(material.shader))
                            Add(path, material.name, "shader", "Shader has compilation errors: " + material.shader.name);
                    }
                }
            }
            foreach (var issue in CycleProjectValidator.CollectIssues()) Add("Narrative", "cycle", "configuration", issue);
            foreach (var group in EditorBuildSettings.scenes.Where(s => s.enabled).GroupBy(s => Path.GetFileNameWithoutExtension(s.path)))
            {
                if (group.Count() > 1) Add("Build Settings", group.Key, "scene name", "Ambiguous enabled scene name");
                foreach (var entry in group)
                    if (!File.Exists(entry.path)) Add(entry.path, "Build Settings", "scene", "Enabled scene does not exist");
            }
        }
        finally
        {
            Application.logMessageReceived -= OnLog;
            report.passed = report.issues.Count == 0;
            Directory.CreateDirectory("Library/SceneIntegrity");
            File.WriteAllText("Library/SceneIntegrity/native.json", JsonUtility.ToJson(report, true));
        }
        Debug.Log($"[SceneIntegrity] {report.scenes} scenes, {report.prefabs} dependency prefabs, {report.materials} materials, {report.issues.Count} issues. Saved assets unchanged.");
    }
    public static void RunBatch()
    {
        try { Run(); EditorApplication.Exit(report.passed ? 0 : 1); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(2); }
    }
    public static void InspectModelCandidatesBatch()
    {
        var lines = new List<string>();
        foreach (string path in new[] { "Assets/Environment/Church/Altar/SacrificialAltar.fbx", "Assets/Environment/Church/Altar/RituelStone.fbx" })
            foreach (var mesh in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>())
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh, out string guid, out long fileID);
                lines.Add(path + " | " + mesh.name + " | " + guid + " | " + fileID + " | bounds=" + mesh.bounds + " | vertices=" + mesh.vertexCount);
            }
        Directory.CreateDirectory("Library/SceneIntegrity");
        File.WriteAllLines("Library/SceneIntegrity/model-candidates.txt", lines);
        EditorApplication.Exit(0);
    }
    private static void InspectHierarchy(GameObject root)
    {
        foreach (var transform in root.GetComponentsInChildren<Transform>(true))
        {
            string owner = Hierarchy(transform);
            if (PrefabUtility.GetPrefabInstanceStatus(transform.gameObject) == PrefabInstanceStatus.MissingAsset)
                Add(currentAsset, owner, "prefab", "Missing prefab source");
            int missingScripts = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
            if (missingScripts > 0) Add(currentAsset, owner, "scripts", missingScripts + " missing scripts");
            foreach (var component in transform.GetComponents<Component>())
            {
                if (component == null) continue;
                InspectReferences(component, owner + " (" + component.GetType().Name + ")");
                if (component is MeshFilter filter && filter.sharedMesh == null)
                    Add(currentAsset, owner, "MeshFilter.sharedMesh", "MeshFilter has no mesh");
                if (component is ParticleSystemRenderer meshEmitter && meshEmitter.enabled && meshEmitter.renderMode == ParticleSystemRenderMode.Mesh)
                {
                    // A valid source GUID can still point at an obsolete FBX sub-asset ID.
                    // Check the actual renderer mesh as well as serialized missing-reference sentinels.
                    var meshes = new Mesh[Mathf.Max(1, meshEmitter.meshCount)];
                    meshEmitter.GetMeshes(meshes);
                    for (int slot = 0; slot < meshes.Length; slot++)
                        if (meshes[slot] == null)
                            Add(currentAsset, owner, "ParticleSystemRenderer.meshes[" + slot + "]", "Enabled mesh emitter has no geometry");
                }
                if (component is ParticleSystemRenderer emitter && emitter.renderMode == ParticleSystemRenderMode.None)
                    report.intentionalParticleEmittersWithoutMaterial.Add(currentAsset + " | " + owner + " | Render Mode None; object references and trails still inspected");
                else if (component is Renderer renderer)
                {
                    var materials = renderer.sharedMaterials;
                    for (int slot = 0; slot < materials.Length; slot++)
                    {
                        if (materials[slot] != null) continue;
                        if (renderer is ParticleSystemRenderer particle && slot == 1 && particle.renderMode != ParticleSystemRenderMode.Mesh &&
                            particle.TryGetComponent<ParticleSystem>(out var system) && !system.trails.enabled)
                        {
                            report.intentionalParticleEmittersWithoutMaterial.Add(currentAsset + " | " + owner + " | optional trail material; Trails module disabled");
                            continue;
                        }
                        var graph = renderer.GetComponent<UnityEngine.VFX.VisualEffect>();
                        if (renderer.GetType().Name == "VFXRenderer" && graph != null && graph.visualEffectAsset != null)
                        {
                            report.intentionalParticleEmittersWithoutMaterial.Add(currentAsset + " | " + owner + " | materials generated by the assigned VFX Graph output");
                            continue;
                        }
                        Add(currentAsset, owner, "Renderer.sharedMaterials[" + slot + "]", "Empty material slot (" + renderer.GetType().Name + ")");
                    }
                }
                if (component is Terrain terrain && terrain.terrainData == null)
                    Add(currentAsset, owner, "Terrain.terrainData", "Terrain has no data");
            }
        }
    }
    private static void InspectReferences(UnityEngine.Object obj, string owner)
    {
        var serialized = new SerializedObject(obj);
        var property = serialized.GetIterator();
        while (property.Next(true))
            if (property.propertyType == SerializedPropertyType.ObjectReference &&
                property.objectReferenceValue == null && property.objectReferenceEntityIdValue != default)
            {
                string context = "";
                if (obj is Material material && property.propertyPath.StartsWith("m_SavedProperties.m_TexEnvs.", StringComparison.Ordinal))
                {
                    int end = property.propertyPath.IndexOf(']');
                    string textureName = serialized.FindProperty(property.propertyPath.Substring(0, end + 1) + ".first").stringValue;
                    context = "; texture=" + textureName + "; active shader property=" + material.HasProperty(textureName);
                }
                Add(currentAsset, owner, property.propertyPath, "Broken serialized reference" + context);
            }
    }
    internal static string Hierarchy(Transform transform)
    {
        string segment = transform.name;
        bool duplicate = transform.parent != null
            ? transform.parent.Cast<Transform>().Count(sibling => sibling.name == transform.name) > 1
            : transform.gameObject.scene.IsValid() && transform.gameObject.scene.GetRootGameObjects().Count(root => root.name == transform.name) > 1;
        if (duplicate) segment += "[" + transform.GetSiblingIndex() + "]";
        return transform.parent == null ? segment : Hierarchy(transform.parent) + "/" + segment;
    }
    private static void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            Add(currentAsset, "Unity", "load", message);
    }
    private static void Add(string asset, string owner, string property, string detail)
    {
        string key = asset + "|" + owner + "|" + property + "|" + detail;
        if (keys.Add(key)) report.issues.Add(new Issue { asset = asset, owner = owner, property = property, detail = detail });
    }
}
