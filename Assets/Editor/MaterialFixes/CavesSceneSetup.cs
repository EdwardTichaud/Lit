using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class CavesSceneSetup
{
    private const string Path = "Assets/Scenes/District_3_Caves.unity";
    private const string Output = "Library/CavesScene";
    static CavesSceneSetup() => EditorApplication.update += Poll;
    private static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Output + "/Setup.request")) return;
        File.Delete(Output + "/Setup.request");
        try { Setup(); } catch (Exception error) { File.WriteAllText(Output + "/Result.txt", "FAIL: " + error); }
    }
    [MenuItem("Lit/Camera/Configure Caves Zoom And Audit")]
    public static void Setup()
    {
        Directory.CreateDirectory(Output);
        var scene = SceneManager.GetSceneByPath(Path);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(Path, OpenSceneMode.Additive);
        try
        {
            File.Copy(Path, Output + "/District_3_Caves.before.unity", true);
            var roots = scene.GetRootGameObjects();
            var renderers = roots.SelectMany(root => root.GetComponentsInChildren<MeshRenderer>(true)).ToArray();
            Bounds bounds = default; bool hasBounds = false;
            foreach (var renderer in renderers)
            {
                if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; } else bounds.Encapsulate(renderer.bounds);
            }
            if (!hasBounds) throw new InvalidOperationException("No cave geometry found.");
            bounds.Expand(4);
            var go = roots.FirstOrDefault(root => root.name == "Camera_CaveBounds");
            if (go == null) { go = new GameObject("Camera_CaveBounds"); SceneManager.MoveGameObjectToScene(go, scene); }
            var box = go.GetComponent<BoxCollider>();
            if (box == null) box = go.AddComponent<BoxCollider>();
            go.transform.SetPositionAndRotation(bounds.center, Quaternion.identity); go.transform.localScale = Vector3.one;
            box.center = Vector3.zero; box.size = bounds.size; box.isTrigger = true;
            var limit = go.GetComponent<LitTacticalBounds>();
            if (limit == null) limit = go.AddComponent<LitTacticalBounds>();
            limit.SetMaximumZoomFraction(1f / 3f);
            EditorUtility.SetDirty(limit); EditorUtility.SetDirty(box);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Caves scene save failed.");

            var filters = roots.SelectMany(root => root.GetComponentsInChildren<MeshFilter>(true)).Where(filter => filter.sharedMesh != null).ToArray();
            var meshes = filters.Select(filter => filter.sharedMesh).Distinct().ToArray();
            var materials = renderers.SelectMany(renderer => renderer.sharedMaterials).Where(material => material != null).Distinct().ToArray();
            var textures = new HashSet<Texture>();
            foreach (var material in materials)
                foreach (string property in material.GetTexturePropertyNames())
                    if (material.GetTexture(property) != null) textures.Add(material.GetTexture(property));
            var lights = roots.SelectMany(root => root.GetComponentsInChildren<Light>(true)).ToArray();
            var probes = roots.SelectMany(root => root.GetComponentsInChildren<ReflectionProbe>(true)).ToArray();
            var report = new System.Text.StringBuilder();
            report.AppendLine($"Mesh renderers: {renderers.Length}; active/enabled: {renderers.Count(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy)}");
            report.AppendLine($"Unique meshes: {meshes.Length}; triangles across all mesh instances (all LOD levels included): {filters.Sum(filter => (long)Triangles(filter.sharedMesh))}");
            report.AppendLine($"LOD groups: {roots.Sum(root => root.GetComponentsInChildren<LODGroup>(true).Length)}; mesh colliders: {roots.Sum(root => root.GetComponentsInChildren<MeshCollider>(true).Length)}");
            report.AppendLine($"Unique materials: {materials.Length}; double-sided materials: {materials.Count(material => material.HasProperty("_DoubleSidedEnable") && material.GetFloat("_DoubleSidedEnable") > .5f)}");
            report.AppendLine($"Referenced textures: {textures.Count}; reported resident allocation: {textures.Sum(texture => Profiler.GetRuntimeMemorySizeLong(texture)) / 1048576d:F1} MiB; textures >=4096: {textures.Count(texture => texture.width >= 4096 || texture.height >= 4096)}");
            report.AppendLine($"Lights: {lights.Length}; shadowed: {lights.Count(light => light.shadows != LightShadows.None)}; reflection probes: {probes.Length}; realtime probes: {probes.Count(probe => probe.mode == UnityEngine.Rendering.ReflectionProbeMode.Realtime)}");
            report.AppendLine($"Static occluders: {renderers.Count(renderer => GameObjectUtility.AreStaticEditorFlagsSet(renderer.gameObject, StaticEditorFlags.OccluderStatic))}; static occludees: {renderers.Count(renderer => GameObjectUtility.AreStaticEditorFlagsSet(renderer.gameObject, StaticEditorFlags.OccludeeStatic))}");
            report.AppendLine("Heaviest unique meshes (triangle count / instance count):");
            foreach (var mesh in meshes.OrderByDescending(Triangles).Take(8)) report.AppendLine($"{mesh.name}: {Triangles(mesh)} / {filters.Count(filter => filter.sharedMesh == mesh)}");
            File.WriteAllText(Output + "/Audit.txt", report.ToString());
            CheckLimits();
            File.WriteAllText(Output + "/Result.txt", "PASS: cave bounds authored at 1/3 zoom; scene saved; scoped limit assertions passed. Geometry, lights and texture quality were not changed.\n" + report);
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }
    private static ulong Triangles(Mesh mesh)
    {
        ulong indices = 0; for (int i = 0; i < mesh.subMeshCount; i++) if (mesh.GetTopology(i) == MeshTopology.Triangles) indices += mesh.GetIndexCount(i);
        return indices / 3;
    }
    private static void CheckLimits()
    {
        var fixture = new GameObject("Isolated zoom assertions");
        try
        {
            fixture.transform.position = Vector3.one * 100000;
            var box = fixture.AddComponent<BoxCollider>(); box.size = Vector3.one * 10; box.isTrigger = true;
            var scope = fixture.AddComponent<LitTacticalBounds>(); scope.SetMaximumZoomFraction(1f / 3f);
            typeof(LitTacticalBounds).GetMethod("OnEnable", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(scope, null);
            var normal = new Vector2(1, 20);
            if (Mathf.Abs(LitTacticalBounds.ResolveDistanceLimits(fixture.transform.position, normal).y - 7.333333f) > .001f) throw new Exception("One-third limit failed.");
            if (LitTacticalBounds.ResolveDistanceLimits(fixture.transform.position + Vector3.right * 20, normal) != normal) throw new Exception("Limit leaked outside its zone.");
            box.enabled = false;
            if (LitTacticalBounds.ResolveDistanceLimits(fixture.transform.position, normal) != normal) throw new Exception("Disabled zone kept its limit.");
        }
        finally
        {
            var scope = fixture.GetComponent<LitTacticalBounds>();
            if (scope != null) typeof(LitTacticalBounds).GetMethod("OnDisable", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(scope, null);
            Object.DestroyImmediate(fixture);
        }
    }
}
