using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.HighDefinition;

/// <summary>Explicit Maison-only preparation. Never changes shared material assets or physics.</summary>
[DisallowMultipleComponent]
public sealed class LitMaisonVisibilityZone : MonoBehaviour
{
    [SerializeField] private Shader nativeLitMaskShader;
    [SerializeField] private string[] environmentScenes = {
        "Maison_Environment_Near_1", "Maison_Environment_Near_2",
        "Maison_Environment_Near_3", "Maison_Environment_Near_4" };
    private static readonly HashSet<LitMaisonVisibilityZone> zones = new HashSet<LitMaisonVisibilityZone>();
    private readonly Dictionary<Material, Material> copies = new Dictionary<Material, Material>();
    private readonly List<Entry> entries = new List<Entry>();
    private readonly HashSet<Scene> preparedScenes = new HashSet<Scene>();
    private Bounds coverage;
    private bool hasCoverage;
    private int unsupported;
    private sealed class Entry
    {
        public MeshRenderer renderer;
        public LitCameraOcclusionGroup group;
        public Material[] original, assigned;
        public Scene scene;
    }
    public int PreparedRendererCount => entries.Count;
    public int UnsupportedRendererCount => unsupported;

    private void OnEnable()
    {
        if (!Application.isPlaying) return;
        if (nativeLitMaskShader == null)
        {
            var template = Resources.Load<Material>("MaisonVisibilityNativeLit");
            if (template != null) nativeLitMaskShader = template.shader;
        }
        zones.Add(this);
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
        StartCoroutine(PrepareLoadedScenes());
    }
    private System.Collections.IEnumerator PrepareLoadedScenes()
    {
        // Allow pending destruction from a same-frame disable/re-enable to finish first.
        yield return null;
        for (int i = 0; i < SceneManager.sceneCount; i++) PrepareScene(SceneManager.GetSceneAt(i));
    }
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => PrepareScene(scene);
    private void PrepareScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded || Array.IndexOf(environmentScenes, scene.name) < 0 ||
            !scene.path.StartsWith("Assets/Scenes/Maison/", StringComparison.Ordinal) || !preparedScenes.Add(scene)) return;
        int failed = 0;
        foreach (var root in scene.GetRootGameObjects())
        foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (!IsArchitecture(renderer.name) || renderer.GetComponentInParent<Animator>() != null ||
                renderer.GetComponent<LitCameraOcclusionGroup>() != null) continue;
            var originals = renderer.sharedMaterials;
            bool compatible = originals.Length > 0;
            foreach (var material in originals) compatible &= material != null &&
                (LitTacticalMaskProperties.Supports(material) ||
                (material.shader.name == "HDRP/Lit" && nativeLitMaskShader != null && nativeLitMaskShader.isSupported));
            if (!compatible) { failed++; continue; }
            var assigned = new Material[originals.Length];
            for (int i = 0; i < originals.Length; i++)
            {
                if (!copies.TryGetValue(originals[i], out var copy))
                { copy = CreateMaskMaterial(originals[i], nativeLitMaskShader); copies.Add(originals[i], copy); }
                assigned[i] = copy;
            }
            renderer.sharedMaterials = assigned;
            var group = renderer.gameObject.AddComponent<LitCameraOcclusionGroup>();
            group.Configure(new Renderer[] { renderer });
            entries.Add(new Entry { renderer = renderer, group = group, original = originals, assigned = assigned, scene = scene });
        }
        unsupported += failed;
        RebuildCoverage();
        if (failed > 0) Debug.LogWarning("[MaisonMask] " + scene.name + ": " + failed +
            " éléments architecturaux ont des matériaux incompatibles ; ils restent inchangés. Voir le diagnostic Maison.", this);
    }

    public static bool IsArchitecture(string name)
    {
        string key = name.ToLowerInvariant();
        if (key.Contains("web")) return false; // Transparent cobweb decorations are not architectural blockers.
        // Floors and walkable stairs stay visible; roof pieces are explicitly allowed.
        if (!key.Contains("roof") && (key.Contains("floor") || key.Contains("stairs") ||
            key.Contains("ground") || key.Contains("path") || key.Contains("plane"))) return false;
        return key.Contains("wall") || key.Contains("column") || key.Contains("collumn") ||
            key.Contains("crypt") || key.Contains("chapel") || key.Contains("arch") ||
            key.Contains("roof") || key.Contains("pillar") || key.Contains("rock") || key.Contains("ceiling");
    }

    public static Material CreateMaskMaterial(Material original, Shader nativeShader = null)
    {
        var copy = new Material(original) { name = original.name + " (Maison visibility)", hideFlags = HideFlags.DontSave };
        bool wasCutout = original.HasProperty("_AlphaCutoffEnable") && original.GetFloat("_AlphaCutoffEnable") > .5f;
        if (copy.HasProperty("_AlphaCutoffEnable")) copy.SetFloat("_AlphaCutoffEnable", 1);
        // Enabling the tactical shader must not introduce texture-alpha holes outside its circle.
        if (!wasCutout)
        {
            if (copy.HasProperty("_AlphaCutoff")) copy.SetFloat("_AlphaCutoff", 0);
            if (copy.HasProperty("_AlphaCutoffShadow")) copy.SetFloat("_AlphaCutoffShadow", 0);
        }
        HDMaterial.ValidateMaterial(copy);
        if (original.shader.name == "HDRP/Lit")
        {
            var keywords = copy.shaderKeywords;
            int queue = copy.renderQueue;
            copy.shader = nativeShader != null ? nativeShader : Shader.Find("Lit/Camera/Maison HDRP Lit");
            copy.shaderKeywords = keywords;
            copy.renderQueue = queue;
        }
        copy.EnableKeyword("_ALPHATEST_ON");
        return copy;
    }

    private void RebuildCoverage()
    {
        hasCoverage = false;
        foreach (var entry in entries)
        {
            if (entry.renderer == null || entry.renderer.bounds.size.sqrMagnitude < .001f) continue;
            if (!hasCoverage) { coverage = entry.renderer.bounds; hasCoverage = true; }
            else coverage.Encapsulate(entry.renderer.bounds);
        }
        if (hasCoverage) coverage.Expand(10);
    }
    public static bool ContainsPlayer(Vector3 player)
    {
        foreach (var zone in zones)
            if (zone != null && zone.isActiveAndEnabled && zone.hasCoverage && zone.coverage.Contains(player)) return true;
        return false;
    }

    public static void Collect(Camera camera, Vector3 target, float radius, List<LitCameraOcclusionGroup> result)
    {
        result.Clear();
        foreach (var zone in zones)
        {
            if (zone == null || !zone.isActiveAndEnabled) continue;
            foreach (var entry in zone.entries)
                if (entry.renderer != null && entry.renderer.gameObject.activeInHierarchy && entry.group != null &&
                    LitTacticalMaskGeometry.IntersectsOpening(camera, entry.renderer.bounds, target, radius)) result.Add(entry.group);
        }
    }
    private void OnSceneUnloaded(Scene scene)
    {
        for (int i = entries.Count - 1; i >= 0; i--)
            if (entries[i].scene == scene) { Release(entries[i]); entries.RemoveAt(i); }
        preparedScenes.Remove(scene);
        RebuildCoverage();
    }
    private static void Release(Entry entry)
    {
        if (entry.group != null) { entry.group.enabled = false; Destroy(entry.group); }
        if (entry.renderer == null) return;
        var current = entry.renderer.sharedMaterials;
        for (int i = 0; i < current.Length && i < entry.assigned.Length; i++)
            if (current[i] == entry.assigned[i]) current[i] = entry.original[i];
        entry.renderer.sharedMaterials = current;
    }
    private void OnDisable()
    {
        zones.Remove(this);
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        foreach (var entry in entries) Release(entry);
        entries.Clear(); preparedScenes.Clear(); hasCoverage = false; unsupported = 0;
        foreach (var copy in copies.Values) if (copy != null) Destroy(copy);
        copies.Clear();
    }
}
