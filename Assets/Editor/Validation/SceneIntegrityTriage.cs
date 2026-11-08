using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

/// <summary>Classifies the audit against current build scenes, retaining prototype findings.</summary>
public static class SceneIntegrityTriage
{
    [Serializable] public sealed class Finding
    {
        public SceneIntegrityAudit.Issue issue;
        public string scope;
        public string[] usedBy;
        public List<string> evidence = new();
    }
    [Serializable] public sealed class Report
    {
        public string utc;
        public string[] enabledScenes;
        public List<Finding> findings = new();
    }

    public static void RunBatch()
    {
        try { Run(); EditorApplication.Exit(0); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    public static void RefreshAuditAndTriageBatch()
    {
        try
        {
            SceneIntegrityAudit.Run();
            Run();
            // This command produces an inventory, not a declaration that every finding is resolved.
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    public static void InspectVfxMeshAssetsBatch()
    {
        var lines = new List<string>();
        foreach (string path in new[] {
            "Assets/0 - UnityPackages/UnityAssets/SrRubfish_VFX_03/Meshes/FX_MS_ExamplesMeshes_01.fbx",
            "Assets/Environment/Prefabs_Ice/Symphonie_Sphere/Mesh_IceEdges_Symphonie_Sphere.asset" })
            foreach (var mesh in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>())
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh, out string guid, out long localId);
                lines.Add(path + " | " + mesh.name + " | " + guid + " | " + localId + " | " + mesh.vertexCount);
            }
        File.WriteAllLines("Library/SceneIntegrity/vfx-mesh-identities.txt", lines);
        EditorApplication.Exit(0);
    }

    [MenuItem("Lit/Validation/Classify Scene Integrity Findings")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode required.");
        var original = JsonUtility.FromJson<SceneIntegrityAudit.Report>(File.ReadAllText("Library/SceneIntegrity/native.json"));
        var enabled = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        var dependencies = enabled.ToDictionary(p => p, p => new HashSet<string>(AssetDatabase.GetDependencies(p, true)));
        var report = new Report { utc = DateTime.UtcNow.ToString("O"), enabledScenes = enabled };
        foreach (var group in original.issues.GroupBy(i => i.asset))
        {
            var uses = dependencies.Where(d => d.Value.Contains(group.Key)).Select(d => d.Key).ToArray();
            GameObject[] roots = Array.Empty<GameObject>();
            Scene preview = default;
            try
            {
                if (group.Key.EndsWith(".unity"))
                {
                    preview = EditorSceneManager.OpenPreviewScene(group.Key);
                    roots = preview.GetRootGameObjects();
                }
                else if (group.Key.EndsWith(".prefab"))
                {
                    var root = AssetDatabase.LoadAssetAtPath<GameObject>(group.Key);
                    if (root != null) roots = new[] { root };
                }
                foreach (var issue in group)
                {
                    var finding = new Finding { issue = issue, usedBy = uses,
                        scope = group.Key.EndsWith(".unity")
                            ? enabled.Contains(group.Key) ? "Enabled game scene" : "Scene outside current build"
                            : uses.Length > 0 ? "Dependency of enabled game scene" : "Dependency outside current build" };
                    var transforms = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
                    var target = transforms.FirstOrDefault(t => Hierarchy(t) == issue.owner);
                    int suffix = issue.owner.LastIndexOf(" (", StringComparison.Ordinal);
                    if (target == null && suffix >= 0 && issue.owner.EndsWith(")", StringComparison.Ordinal))
                        target = transforms.FirstOrDefault(t => Hierarchy(t) == issue.owner.Substring(0, suffix));
                    if (target != null) Describe(target.gameObject, finding.evidence);
                    if (group.Key.EndsWith(".mat")) DescribeMaterial(AssetDatabase.LoadAssetAtPath<Material>(group.Key), finding.evidence);
                    report.findings.Add(finding);
                }
            }
            finally { if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview); }
        }
        Directory.CreateDirectory("Library/SceneIntegrity");
        File.WriteAllText("Library/SceneIntegrity/triage.json", JsonUtility.ToJson(report, true));
        Debug.Log("[SceneIntegrityTriage] " + report.findings.Count + " findings classified; assets unchanged.");
    }

    private static void Describe(GameObject owner, List<string> evidence)
    {
        evidence.Add("activeSelf=" + owner.activeSelf + "; world=" + owner.transform.position + "; scale=" + owner.transform.lossyScale);
        foreach (var component in owner.GetComponents<Component>().Where(c => c != null))
            evidence.Add("component=" + component.GetType().FullName);
        foreach (var renderer in owner.GetComponents<Renderer>())
        {
            evidence.Add("renderer=" + renderer.GetType().Name + "; enabled=" + renderer.enabled + "; bounds=" + renderer.bounds);
            foreach (var mat in renderer.sharedMaterials) DescribeMaterial(mat, evidence);
        }
        if (owner.TryGetComponent<MeshFilter>(out var filter))
            evidence.Add("visual=" + DescribeMesh(filter.sharedMesh));
        if (owner.TryGetComponent<MeshCollider>(out var collider))
            evidence.Add("collision=" + DescribeMesh(collider.sharedMesh) + "; enabled=" + collider.enabled + "; trigger=" + collider.isTrigger);
        if (owner.TryGetComponent<UnityEngine.ProBuilder.ProBuilderMesh>(out var procedural))
            evidence.Add("ProBuilder vertices=" + procedural.vertexCount + "; faces=" + procedural.faceCount + "; state=" + procedural.meshSyncState);
        if (owner.TryGetComponent<ParticleSystem>(out var particles))
            evidence.Add("particles emission=" + particles.emission.enabled + "; timeRate=" + particles.emission.rateOverTime.constantMax +
                "; distanceRate=" + particles.emission.rateOverDistance.constantMax + "; bursts=" + particles.emission.burstCount +
                "; children=" + owner.GetComponentsInChildren<ParticleSystem>(true).Length +
                "; renderMode=" + particles.GetComponent<ParticleSystemRenderer>()?.renderMode);
        if (owner.TryGetComponent<PlayableDirector>(out var director) && director.playableAsset != null)
        {
            evidence.Add("timeline=" + AssetDatabase.GetAssetPath(director.playableAsset));
            foreach (var output in director.playableAsset.outputs)
                evidence.Add("output=" + output.streamName + "; key=" + output.sourceObject?.name + "; value=" + director.GetGenericBinding(output.sourceObject)?.name);
        }
        if (owner.TryGetComponent<UnityEngine.UI.Image>(out var image))
            evidence.Add("UI sprite=" + AssetDatabase.GetAssetPath(image.sprite) + "; material=" + AssetDatabase.GetAssetPath(image.material));
        foreach (var component in owner.GetComponents<Component>().Where(c => c != null))
        {
            var serialized = new SerializedObject(component);
            var property = serialized.GetIterator();
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference ||
                    property.objectReferenceValue != null || property.objectReferenceEntityIdValue == default) continue;
                evidence.Add("broken=" + component.GetType().Name + "." + property.propertyPath);
            }
        }
    }

    private static string DescribeMesh(Mesh mesh) => mesh == null ? "MISSING" :
        AssetDatabase.GetAssetPath(mesh) + " | " + mesh.name + " | vertices=" + mesh.vertexCount + " | bounds=" + mesh.bounds;
    private static void DescribeMaterial(Material material, List<string> evidence)
    {
        if (material == null) { evidence.Add("material=MISSING"); return; }
        evidence.Add("material=" + AssetDatabase.GetAssetPath(material) + "; shader=" + material.shader?.name);
        foreach (string slot in new[] { "_BaseColorMap", "_MainTex", "_BaseMap", "_NormalMap", "_EmissionMap", "_SubsurfaceMaskMap", "_ThicknessMap" })
            if (material.HasProperty(slot)) evidence.Add(slot + "=" + AssetDatabase.GetAssetPath(material.GetTexture(slot)));
    }
    private static string Hierarchy(Transform item) => SceneIntegrityAudit.Hierarchy(item);
}
