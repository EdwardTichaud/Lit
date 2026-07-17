using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public sealed class LitIceMaisonPrefabGeneratorWindow : EditorWindow
{
    private Vector2 m_Scroll;
    private LitIceMaisonPrefabGenerator.AnalysisResult m_Analysis;
    private string m_LastMessage;
    private MessageType m_LastMessageType = MessageType.Info;

    [MenuItem("Lit/Shadergraph/Generate Maison Ice Prefabs")]
    private static void Open()
    {
        GetWindow<LitIceMaisonPrefabGeneratorWindow>("Maison Ice Prefabs");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("MAISON ICE PREFAB LIBRARY", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Analyse les MeshRenderer statiques et les instances de prefab sous World, puis crée une bibliothèque V3 "
            + "dédupliquée avant d'appliquer les références à la scène Maison.",
            MessageType.Info);

        if (GUILayout.Button("1 — ANALYZE MAISON", GUILayout.Height(32f)))
        {
            try
            {
                m_Analysis = LitIceMaisonPrefabGenerator.AnalyzeMaison(true);
                m_LastMessage = m_Analysis.ToDisplayMessage();
                m_LastMessageType = m_Analysis.Errors.Count > 0
                    ? MessageType.Error
                    : m_Analysis.Warnings.Count > 0 ? MessageType.Warning : MessageType.Info;
            }
            catch (Exception exception)
            {
                m_Analysis = null;
                m_LastMessage = exception.Message;
                m_LastMessageType = MessageType.Error;
                Debug.LogException(exception);
            }
        }

        if (m_Analysis != null)
            DrawAnalysis(m_Analysis);

        using (new EditorGUI.DisabledScope(m_Analysis == null || m_Analysis.Errors.Count > 0))
        {
            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.55f, 0.9f, 1f);
            if (GUILayout.Button("2 — GENERATE / UPDATE AND APPLY", GUILayout.Height(38f)))
            {
                if (EditorUtility.DisplayDialog(
                    "Generate Maison Ice Prefabs",
                    m_Analysis.ToDisplayMessage()
                    + "\n\nLes objets de Maison seront modifiés en place après la création des assets.",
                    "Generate", "Cancel"))
                {
                    try
                    {
                        // Re-analyse immediately before the destructive phase.
                        m_Analysis = LitIceMaisonPrefabGenerator.AnalyzeMaison(true);
                        LitIceMaisonPrefabGenerator.GenerationResult result =
                            LitIceMaisonPrefabGenerator.Generate(m_Analysis, true, true);
                        m_LastMessage = result.ToDisplayMessage();
                        m_LastMessageType = result.ErrorCount > 0
                            ? MessageType.Error
                            : result.WarningCount > 0 ? MessageType.Warning : MessageType.Info;
                    }
                    catch (OperationCanceledException)
                    {
                        m_LastMessage = "Generation cancelled before the scene was modified.";
                        m_LastMessageType = MessageType.Warning;
                    }
                    catch (Exception exception)
                    {
                        m_LastMessage = exception.Message;
                        m_LastMessageType = MessageType.Error;
                        Debug.LogException(exception);
                    }
                    finally
                    {
                        EditorUtility.ClearProgressBar();
                    }
                }
            }
            GUI.backgroundColor = previous;
        }

        if (!string.IsNullOrEmpty(m_LastMessage))
            EditorGUILayout.HelpBox(m_LastMessage, m_LastMessageType);
    }

    private void DrawAnalysis(LitIceMaisonPrefabGenerator.AnalysisResult analysis)
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Analysis", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Renderers", analysis.RendererCount.ToString());
        EditorGUILayout.LabelField("Unique meshes", analysis.Groups.Count.ToString());
        EditorGUILayout.LabelField("Mesh/material variants", analysis.VariantCount.ToString());
        EditorGUILayout.LabelField("Multi-material renderers", analysis.MultiMaterialRendererCount.ToString());
        EditorGUILayout.LabelField("Existing V3 materials", analysis.ExistingV3MaterialCount.ToString());

        if (analysis.Warnings.Count == 0 && analysis.Errors.Count == 0)
            return;

        m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll, GUILayout.MaxHeight(190f));
        foreach (string error in analysis.Errors)
            EditorGUILayout.HelpBox(error, MessageType.Error);
        foreach (string warning in analysis.Warnings.Take(80))
            EditorGUILayout.HelpBox(warning, MessageType.Warning);
        if (analysis.Warnings.Count > 80)
            EditorGUILayout.LabelField($"… {analysis.Warnings.Count - 80} additional warning(s) in Console.");
        EditorGUILayout.EndScrollView();
    }
}

internal static class LitIceMaisonPrefabGenerator
{
    internal const string ScenePath = "Assets/Scenes/Maison/Maison.unity";
    internal const string OutputRoot = "Assets/Environment/Prefabs_Ice";
    private const string SharedMapFolder = OutputRoot + "/_SharedMaps";
    private const string CatalogPath = OutputRoot + "/LitIcePrefabCatalog.asset";
    private const string ShaderV3Path = "Assets/Materials/IceShader/ShaderGraph_LitIceFrostedEdges_v3.shadergraph";
    private const string CanonicalMaterialPath = "Assets/Materials/IceShader/Material_LitIceFrostedEdges_v3.mat";
    private const string PillarTemplateName = "Material_LitIceFrostedEdges_SM_Pillar_01_2H";

    internal sealed class AnalysisResult
    {
        public Scene Scene;
        public GameObject WorldRoot;
        public readonly List<ModelGroup> Groups = new List<ModelGroup>();
        public readonly List<string> Warnings = new List<string>();
        public readonly List<string> Errors = new List<string>();
        public int RendererCount;
        public int MultiMaterialRendererCount;
        public int ExistingV3MaterialCount;
        public int VariantCount => Groups.Sum(group => group.Variants.Count);

        public string ToDisplayMessage()
        {
            return $"{RendererCount} renderer(s), {Groups.Count} unique mesh(es), "
                + $"{VariantCount} mesh/material variant(s), {Warnings.Count} warning(s), "
                + $"{Errors.Count} error(s).";
        }
    }

    internal sealed class GenerationResult
    {
        public int CreatedMeshCount;
        public int ReusedMeshCount;
        public int CreatedMaterialCount;
        public int ReusedMaterialCount;
        public int CreatedPrefabCount;
        public int UpdatedPrefabCount;
        public int UpdatedRendererCount;
        public int WarningCount;
        public int ErrorCount;

        public string ToDisplayMessage()
        {
            return $"Maison Ice complete: {CreatedMeshCount} mesh(es) created, "
                + $"{ReusedMeshCount} reused, {CreatedMaterialCount} material(s) created, "
                + $"{ReusedMaterialCount} reused, {CreatedPrefabCount} prefab(s) created, "
                + $"{UpdatedPrefabCount} updated, {UpdatedRendererCount} renderer(s) applied, "
                + $"{WarningCount} warning(s), {ErrorCount} error(s).";
        }
    }

    internal sealed class ScopeRepairResult
    {
        public int RestoredRendererCount;
        public int RemovedMeshCount;
        public int RemovedVariantCount;
        public int MovedAssetCount;

        public override string ToString()
        {
            return $"{RestoredRendererCount} non-static/excluded renderer(s) restored, "
                + $"{RemovedMeshCount} out-of-scope mesh group(s) removed, "
                + $"{RemovedVariantCount} out-of-scope variant(s) removed, "
                + $"{MovedAssetCount} retained asset(s) normalized.";
        }
    }

    internal sealed class CompletionResult
    {
        public int ScannedRendererCount;
        public int UpdatedRendererCount;
        public int AlreadyCompleteCount;
        public int MissingCatalogCount;
        public int MissingVariantCount;
        public int ExcludedRendererCount;
        public readonly List<string> UnmatchedExamples = new List<string>();

        public string ToDisplayMessage()
        {
            return $"Maison Ice completion: {UpdatedRendererCount} renderer(s) updated, "
                + $"{AlreadyCompleteCount} already complete, "
                + $"{MissingCatalogCount} without a catalogued mesh, "
                + $"{MissingVariantCount} without an exact material variant, "
                + $"{ExcludedRendererCount} excluded.";
        }
    }

    internal sealed class ModelGroup
    {
        public string SourceMeshId;
        public string SafeMeshName;
        public string FolderName;
        public string FolderPath;
        public Mesh SourceMesh;
        public readonly List<ModelVariant> Variants = new List<ModelVariant>();
        public readonly List<Mesh> ExistingBakedMeshes = new List<Mesh>();
        public Mesh GeneratedBakedMesh;
        public string SourceDependencyHash;
    }

    internal sealed class ModelVariant
    {
        public string VariantId;
        public bool IsPrimary;
        public Material[] SourceMaterials;
        public readonly List<RendererRecord> Records = new List<RendererRecord>();
        public Material[] GeneratedMaterials;
        public string PrefabPath;
        public GameObject GeneratedPrefab;
        public MeshRenderer Representative => Records.Count > 0 ? Records[0].Renderer : null;
    }

    internal sealed class RendererRecord
    {
        public MeshRenderer Renderer;
        public MeshFilter Filter;
        public Mesh CurrentMesh;
        public Material[] CurrentMaterials;
    }

    private sealed class TextureSlot
    {
        public Texture Texture;
        public string PropertyName;
        public Vector2 Scale = Vector2.one;
        public Vector2 Offset = Vector2.zero;
    }

    private sealed class ExtractedMaskMaps
    {
        public Texture2D Metallic;
        public Texture2D Occlusion;
        public Texture2D Roughness;
    }

    internal static AnalysisResult AnalyzeMaison(bool openIfMissing, string meshNameFilter = null)
    {
        Scene scene = GetMaisonScene(openIfMissing);
        var result = new AnalysisResult { Scene = scene };
        result.WorldRoot = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "World");
        if (result.WorldRoot == null)
        {
            result.Errors.Add("Root GameObject 'World' was not found in Maison.");
            return result;
        }

        Shader v3Shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderV3Path);
        LitIcePrefabCatalog catalog = AssetDatabase.LoadAssetAtPath<LitIcePrefabCatalog>(CatalogPath);
        var groups = new Dictionary<string, ModelGroup>(StringComparer.Ordinal);

        foreach (MeshRenderer renderer in result.WorldRoot.GetComponentsInChildren<MeshRenderer>(true))
        {
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null || EditorUtility.IsPersistent(renderer))
                continue;
            if (!renderer.gameObject.isStatic
                && !PrefabUtility.IsPartOfPrefabInstance(renderer))
                continue;
            if (IsExcludedRenderer(renderer))
                continue;
            if ((renderer.hideFlags & HideFlags.DontSave) != 0)
                continue;

            Mesh currentMesh = filter.sharedMesh;
            Mesh sourceMesh = ResolveSourceMesh(filter, currentMesh, catalog, result.Warnings);
            if (sourceMesh == null)
                continue;
            if (!EditorUtility.IsPersistent(sourceMesh))
            {
                result.Warnings.Add(
                    $"{GetHierarchyPath(renderer.transform)}: scene-owned mesh "
                    + $"'{sourceMesh.name}' cannot be added to the reusable Ice catalog.");
                continue;
            }
            if (!string.IsNullOrEmpty(meshNameFilter)
                && !string.Equals(sourceMesh.name, meshNameFilter, StringComparison.OrdinalIgnoreCase))
                continue;

            string sourceId = GetStableObjectId(sourceMesh);
            if (!groups.TryGetValue(sourceId, out ModelGroup group))
            {
                group = new ModelGroup
                {
                    SourceMeshId = sourceId,
                    SourceMesh = sourceMesh,
                    SafeMeshName = SanitizeName(sourceMesh.name),
                    SourceDependencyHash = GetDependencyHash(sourceMesh)
                };
                groups.Add(sourceId, group);
            }

            if (LitIceEdgeMaskBaker.IsCurrentBakedMesh(currentMesh)
                && !group.ExistingBakedMeshes.Contains(currentMesh))
                group.ExistingBakedMeshes.Add(currentMesh);

            Material[] currentMaterials = renderer.sharedMaterials ?? Array.Empty<Material>();
            Material[] sourceMaterials = ResolveSourceMaterials(
                renderer, currentMaterials, sourceId, catalog, v3Shader);
            string variantId = GetMaterialSetId(sourceMaterials);
            ModelVariant variant = group.Variants.FirstOrDefault(item => item.VariantId == variantId);
            if (variant == null)
            {
                variant = new ModelVariant
                {
                    VariantId = variantId,
                    SourceMaterials = sourceMaterials
                };
                group.Variants.Add(variant);
            }

            variant.Records.Add(new RendererRecord
            {
                Renderer = renderer,
                Filter = filter,
                CurrentMesh = currentMesh,
                CurrentMaterials = currentMaterials
            });
            result.RendererCount++;
            if (currentMaterials.Length > 1)
                result.MultiMaterialRendererCount++;
            result.ExistingV3MaterialCount += currentMaterials.Count(
                material => material != null && material.shader == v3Shader);
        }

        result.Groups.AddRange(groups.Values.OrderBy(group => group.SafeMeshName, StringComparer.Ordinal));
        AssignFolderNamesAndPrimaryVariants(result.Groups, v3Shader);
        foreach (ModelGroup group in result.Groups)
        {
            group.FolderPath = OutputRoot + "/" + group.FolderName;
            if (group.SourceMesh.subMeshCount <= 0)
                result.Warnings.Add($"{group.SourceMesh.name}: mesh has no submesh.");
        }

        foreach (string warning in result.Warnings)
            Debug.LogWarning("[Lit Ice Maison] " + warning);
        Debug.Log("[Lit Ice Maison] Analysis: " + result.ToDisplayMessage());
        return result;
    }

    [MenuItem("Lit/Shadergraph/Complete Missing Maison Ice Assignments")]
    private static void CompleteMissingCatalogAssignmentsMenu()
    {
        if (!EditorUtility.DisplayDialog(
                "Complete missing Maison Ice assignments?",
                "This pass updates MeshRenderers under World that still use source assets, "
                + "but only when an exact mesh/material match already exists in the Ice catalog. "
                + "Unknown renderers and UI are preserved.",
                "Complete",
                "Cancel"))
        {
            return;
        }

        try
        {
            CompletionResult result = CompleteMissingCatalogAssignments(true);
            EditorUtility.DisplayDialog("Maison Ice completion", result.ToDisplayMessage(), "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Maison Ice completion failed", exception.Message, "OK");
        }
    }

    internal static CompletionResult CompleteMissingCatalogAssignments(bool openIfMissing)
    {
        Scene scene = GetMaisonScene(openIfMissing);
        GameObject world = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "World");
        LitIcePrefabCatalog catalog = AssetDatabase.LoadAssetAtPath<LitIcePrefabCatalog>(CatalogPath);
        Shader v3Shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderV3Path);
        if (world == null || catalog == null || v3Shader == null)
            throw new InvalidOperationException(
                "Maison World root, LitIcePrefabCatalog or ShaderGraph V3 is missing.");

        var result = new CompletionResult();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Complete missing Maison Ice assignments");
        bool sceneChanged = false;
        try
        {
            foreach (MeshRenderer renderer in world.GetComponentsInChildren<MeshRenderer>(true))
            {
                result.ScannedRendererCount++;
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null
                    || EditorUtility.IsPersistent(renderer)
                    || (renderer.hideFlags & HideFlags.DontSave) != 0
                    || IsExcludedRenderer(renderer))
                {
                    result.ExcludedRendererCount++;
                    continue;
                }

                Mesh currentMesh = filter.sharedMesh;
                LitIcePrefabCatalogEntry entry = catalog.FindByBakedMesh(currentMesh)
                    ?? catalog.FindBySourceId(GetStableObjectId(currentMesh));
                if (entry == null || entry.BakedMesh == null)
                {
                    result.MissingCatalogCount++;
                    AddUnmatchedExample(result, renderer, "mesh absent from catalog");
                    continue;
                }

                Material[] currentMaterials = renderer.sharedMaterials ?? Array.Empty<Material>();
                LitIcePrefabVariantEntry variant = entry.Variants.FirstOrDefault(item =>
                    item != null
                    && item.Materials != null
                    && item.Materials.SequenceEqual(currentMaterials));
                if (variant == null)
                {
                    string[] currentMaterialIds = currentMaterials
                        .Select(GetStableObjectId)
                        .ToArray();
                    variant = entry.Variants.FirstOrDefault(item =>
                        item != null
                        && item.SourceMaterialIds != null
                        && item.SourceMaterialIds.SequenceEqual(currentMaterialIds));
                }
                if (variant == null || variant.Materials == null
                    || variant.Materials.Count == 0
                    || variant.Materials.Any(material => material == null
                        || material.shader != v3Shader))
                {
                    result.MissingVariantCount++;
                    AddUnmatchedExample(result, renderer, "material variant absent from catalog");
                    continue;
                }

                Material[] desiredMaterials = variant.Materials.ToArray();
                bool meshChanged = currentMesh != entry.BakedMesh;
                bool materialsChanged = !currentMaterials.SequenceEqual(desiredMaterials);
                if (!meshChanged && !materialsChanged)
                {
                    result.AlreadyCompleteCount++;
                    continue;
                }

                if (meshChanged)
                {
                    Undo.RecordObject(filter, "Assign catalogued Lit Ice mesh");
                    filter.sharedMesh = entry.BakedMesh;
                    EditorUtility.SetDirty(filter);
                    if (PrefabUtility.IsPartOfPrefabInstance(filter))
                        PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
                }
                if (materialsChanged)
                {
                    Undo.RecordObject(renderer, "Assign catalogued Lit Ice materials");
                    renderer.sharedMaterials = desiredMaterials;
                    EditorUtility.SetDirty(renderer);
                    if (PrefabUtility.IsPartOfPrefabInstance(renderer))
                        PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                }

                sceneChanged = true;
                result.UpdatedRendererCount++;
            }

            if (sceneChanged)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                    throw new InvalidOperationException("Maison could not be saved.");
            }
            Undo.CollapseUndoOperations(undoGroup);
        }
        catch
        {
            Undo.RevertAllDownToGroup(undoGroup);
            throw;
        }

        foreach (string example in result.UnmatchedExamples)
            Debug.LogWarning("[Lit Ice Maison Completion] " + example);
        Debug.Log("[Lit Ice Maison Completion] " + result.ToDisplayMessage());
        return result;
    }

    private static void AddUnmatchedExample(
        CompletionResult result, MeshRenderer renderer, string reason)
    {
        const int MaxExamples = 50;
        if (result.UnmatchedExamples.Count >= MaxExamples)
            return;
        Mesh mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
        result.UnmatchedExamples.Add(
            $"{GetHierarchyPath(renderer.transform)} ({mesh?.name}): {reason}.");
    }

    private static string GetHierarchyPath(Transform value)
    {
        if (value == null)
            return "<missing renderer>";
        var names = new Stack<string>();
        for (Transform current = value; current != null; current = current.parent)
            names.Push(current.name);
        return string.Join("/", names);
    }

    internal static ScopeRepairResult RepairGeneratedScopeToStatic()
    {
        Scene scene = GetMaisonScene(false);
        GameObject world = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "World");
        LitIcePrefabCatalog catalog = AssetDatabase.LoadAssetAtPath<LitIcePrefabCatalog>(CatalogPath);
        if (world == null || catalog == null)
            throw new InvalidOperationException("Maison World root or LitIcePrefabCatalog is missing.");

        var result = new ScopeRepairResult();
        bool sceneChanged = false;
        foreach (MeshRenderer renderer in world.GetComponentsInChildren<MeshRenderer>(true))
        {
            if ((renderer.gameObject.isStatic
                    || PrefabUtility.IsPartOfPrefabInstance(renderer))
                && !IsExcludedRenderer(renderer))
                continue;
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            if (filter == null)
                continue;
            LitIcePrefabCatalogEntry entry = catalog.FindByBakedMesh(filter.sharedMesh);
            if (entry?.SourceMesh == null)
                continue;
            LitIcePrefabVariantEntry variant = entry.Variants.FirstOrDefault(item =>
                item.Materials != null && item.Materials.SequenceEqual(renderer.sharedMaterials));
            if (variant == null)
                continue;

            Undo.RecordObject(filter, "Restore out-of-scope source mesh");
            Undo.RecordObject(renderer, "Restore out-of-scope source materials");
            filter.sharedMesh = entry.SourceMesh;
            renderer.sharedMaterials = variant.SourceMaterials?.ToArray() ?? Array.Empty<Material>();
            EditorUtility.SetDirty(filter);
            EditorUtility.SetDirty(renderer);
            if (PrefabUtility.IsPartOfPrefabInstance(filter))
                PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
            if (PrefabUtility.IsPartOfPrefabInstance(renderer))
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            sceneChanged = true;
            result.RestoredRendererCount++;
        }

        if (sceneChanged)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Maison could not be saved after scope repair.");
        }

        AnalysisResult activeAnalysis = AnalyzeMaison(false);
        var activeBySource = activeAnalysis.Groups.ToDictionary(
            group => group.SourceMeshId, StringComparer.Ordinal);

        foreach (LitIcePrefabCatalogEntry entry in catalog.Entries.ToArray())
        {
            if (!activeBySource.TryGetValue(entry.SourceMeshId, out ModelGroup activeGroup))
            {
                if (!string.IsNullOrEmpty(entry.FolderPath))
                    AssetDatabase.DeleteAsset(entry.FolderPath);
                catalog.Entries.Remove(entry);
                result.RemovedMeshCount++;
                continue;
            }

            var activeVariantIds = new HashSet<string>(
                activeGroup.Variants.Select(variant => variant.VariantId), StringComparer.Ordinal);
            List<LitIcePrefabVariantEntry> retained = entry.Variants
                .Where(variant => activeVariantIds.Contains(variant.VariantId))
                .ToList();
            var retainedMaterialPaths = new HashSet<string>(retained
                .SelectMany(variant => variant.Materials ?? new List<Material>())
                .Where(material => material != null)
                .Select(AssetDatabase.GetAssetPath), StringComparer.Ordinal);

            foreach (LitIcePrefabVariantEntry removed in entry.Variants
                .Where(variant => !activeVariantIds.Contains(variant.VariantId)).ToArray())
            {
                if (!string.IsNullOrEmpty(removed.PrefabPath))
                    AssetDatabase.DeleteAsset(removed.PrefabPath);
                foreach (Material material in removed.Materials ?? new List<Material>())
                {
                    string path = AssetDatabase.GetAssetPath(material);
                    if (!string.IsNullOrEmpty(path) && !retainedMaterialPaths.Contains(path))
                        AssetDatabase.DeleteAsset(path);
                }
                entry.Variants.Remove(removed);
                result.RemovedVariantCount++;
            }

            foreach (ModelVariant activeVariant in activeGroup.Variants)
            {
                LitIcePrefabVariantEntry catalogVariant = entry.Variants.FirstOrDefault(
                    variant => variant.VariantId == activeVariant.VariantId);
                if (catalogVariant == null)
                    continue;
                for (int slot = 0; slot < catalogVariant.Materials.Count; slot++)
                {
                    Material material = catalogVariant.Materials[slot];
                    if (material == null)
                        continue;
                    string sourcePath = AssetDatabase.GetAssetPath(material);
                    string desiredName = GetMaterialName(activeGroup, activeVariant, slot);
                    string desiredPath = activeGroup.FolderPath + "/" + desiredName + ".mat";
                    if (sourcePath == desiredPath)
                        continue;
                    if (AssetDatabase.LoadAssetAtPath<Material>(desiredPath) != null)
                        AssetDatabase.DeleteAsset(desiredPath);
                    string error = AssetDatabase.MoveAsset(sourcePath, desiredPath);
                    if (!string.IsNullOrEmpty(error))
                        throw new InvalidOperationException(error);
                    Material moved = AssetDatabase.LoadAssetAtPath<Material>(desiredPath);
                    moved.name = desiredName;
                    EditorUtility.SetDirty(moved);
                    catalogVariant.Materials[slot] = moved;
                    result.MovedAssetCount++;
                }

                string prefabName = activeGroup.FolderName + "_Ice"
                    + (activeVariant.IsPrimary ? string.Empty : "_Variant_" + activeVariant.VariantId);
                string desiredPrefabPath = activeGroup.FolderPath + "/" + prefabName + ".prefab";
                if (!string.IsNullOrEmpty(catalogVariant.PrefabPath)
                    && catalogVariant.PrefabPath != desiredPrefabPath)
                {
                    if (AssetDatabase.LoadAssetAtPath<GameObject>(desiredPrefabPath) != null)
                        AssetDatabase.DeleteAsset(desiredPrefabPath);
                    string error = AssetDatabase.MoveAsset(
                        catalogVariant.PrefabPath, desiredPrefabPath);
                    if (!string.IsNullOrEmpty(error))
                        throw new InvalidOperationException(error);
                    catalogVariant.PrefabPath = desiredPrefabPath;
                    catalogVariant.Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(desiredPrefabPath);
                    result.MovedAssetCount++;
                }
            }
        }

        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Generate(activeAnalysis, true, false);
        return result;
    }

    internal static GenerationResult Generate(
        AnalysisResult analysis, bool applyToScene, bool showProgress)
    {
        if (analysis == null)
            throw new ArgumentNullException(nameof(analysis));
        if (analysis.Errors.Count > 0)
            throw new InvalidOperationException(string.Join("\n", analysis.Errors));

        EnsureAssetFolder(OutputRoot);
        EnsureAssetFolder(SharedMapFolder);
        Shader v3Shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderV3Path);
        if (v3Shader == null)
            throw new InvalidOperationException("ShaderGraph_LitIceFrostedEdges_v3 could not be loaded.");

        Material template = FindTemplateMaterial(v3Shader);
        if (template == null)
            throw new InvalidOperationException("No V3 template material could be loaded.");

        var result = new GenerationResult { WarningCount = analysis.Warnings.Count };
        var createdPaths = new List<string>();
        bool catalogExisted = AssetDatabase.LoadAssetAtPath<LitIcePrefabCatalog>(CatalogPath) != null;
        LitIcePrefabCatalog catalog = GetOrCreateCatalog();
        if (!catalogExisted)
            createdPaths.Add(CatalogPath);
        var extractedMaskCache = new Dictionary<Texture, ExtractedMaskMaps>();
        int totalSteps = Math.Max(1, analysis.Groups.Count + analysis.VariantCount);
        int step = 0;

        try
        {
            foreach (ModelGroup group in analysis.Groups)
            {
                CheckCancelled(showProgress, step++, totalSteps,
                    "Baking " + group.SourceMesh.name);
                EnsureAssetFolder(group.FolderPath);
                GenerateBakedMesh(group, catalog, result, createdPaths);
            }

            foreach (ModelGroup group in analysis.Groups)
            foreach (ModelVariant variant in group.Variants)
            {
                CheckCancelled(showProgress, step++, totalSteps,
                    "Creating " + group.SourceMesh.name);
                GenerateVariantAssets(
                    group, variant, template, v3Shader, catalog,
                    extractedMaskCache, analysis.Warnings, result, createdPaths);
            }

            result.WarningCount = analysis.Warnings.Count;
            foreach (string warning in analysis.Warnings)
                Debug.LogWarning("[Lit Ice Maison] " + warning);
            catalog.Version = LitIcePrefabCatalog.CurrentVersion;
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            if (applyToScene)
                ApplyToScene(analysis, result);

            AssetDatabase.SaveAssets();
            Debug.Log("[Lit Ice Maison] " + result.ToDisplayMessage());
            return result;
        }
        catch
        {
            // Existing assets are preserved. Only assets created by this failed
            // run are removed; the scene is not touched until all assets validate.
            foreach (string path in createdPaths.OrderByDescending(path => path.Length))
                AssetDatabase.DeleteAsset(path);
            AssetDatabase.SaveAssets();
            throw;
        }
        finally
        {
            if (showProgress)
                EditorUtility.ClearProgressBar();
        }
    }

    private static void GenerateBakedMesh(
        ModelGroup group, LitIcePrefabCatalog catalog, GenerationResult result,
        List<string> createdPaths)
    {
        string targetPath = group.FolderPath + "/Mesh_IceEdges_" + group.FolderName + ".asset";
        bool targetExisted = AssetDatabase.LoadAssetAtPath<Mesh>(targetPath) != null;
        LitIcePrefabCatalogEntry existingEntry = catalog.FindBySourceId(group.SourceMeshId);
        bool forceRebuild = existingEntry != null
            && (existingEntry.BakeVersion != LitIceEdgeMaskBaker.BakeVersion
                || existingEntry.SourceDependencyHash != group.SourceDependencyHash);

        Mesh baked = AssetDatabase.LoadAssetAtPath<Mesh>(targetPath);
        if (baked == null && group.ExistingBakedMeshes.Count > 0)
        {
            Mesh movable = group.ExistingBakedMeshes.FirstOrDefault(mesh =>
            {
                string path = AssetDatabase.GetAssetPath(mesh);
                return !string.IsNullOrEmpty(path)
                    && path.StartsWith("Assets/Materials/IceShader/BakedMeshes/", StringComparison.Ordinal);
            });
            if (movable != null)
            {
                string sourcePath = AssetDatabase.GetAssetPath(movable);
                string error = AssetDatabase.MoveAsset(sourcePath, targetPath);
                if (string.IsNullOrEmpty(error))
                {
                    baked = AssetDatabase.LoadAssetAtPath<Mesh>(targetPath);
                    baked.name = LitIceEdgeMaskBaker.BakedVersionMarker + group.FolderName;
                    EditorUtility.SetDirty(baked);
                }
            }
        }

        if (baked == null || forceRebuild || !LitIceEdgeMaskBaker.IsCurrentBakedMesh(baked))
        {
            baked = LitIceEdgeMaskBaker.BakeMeshToAsset(
                group.SourceMesh, targetPath, forceRebuild);
            if (!targetExisted)
            {
                createdPaths.Add(targetPath);
                result.CreatedMeshCount++;
            }
            else result.ReusedMeshCount++;
        }
        else result.ReusedMeshCount++;

        group.GeneratedBakedMesh = baked;
        LitIcePrefabCatalogEntry entry = existingEntry ?? new LitIcePrefabCatalogEntry();
        if (existingEntry == null)
            catalog.Entries.Add(entry);
        entry.SourceMeshId = group.SourceMeshId;
        entry.SourceMeshName = group.SourceMesh.name;
        entry.SourceAssetPath = AssetDatabase.GetAssetPath(group.SourceMesh);
        entry.SourceDependencyHash = group.SourceDependencyHash;
        entry.BakeVersion = LitIceEdgeMaskBaker.BakeVersion;
        entry.FolderPath = group.FolderPath;
        entry.SourceMesh = group.SourceMesh;
        entry.BakedMesh = baked;
    }

    private static void GenerateVariantAssets(
        ModelGroup group,
        ModelVariant variant,
        Material template,
        Shader v3Shader,
        LitIcePrefabCatalog catalog,
        Dictionary<Texture, ExtractedMaskMaps> extractedMaskCache,
        List<string> warnings,
        GenerationResult result,
        List<string> createdPaths)
    {
        int slotCount = Math.Max(1, variant.SourceMaterials?.Length ?? 0);
        variant.GeneratedMaterials = new Material[slotCount];
        for (int slot = 0; slot < slotCount; slot++)
        {
            Material source = variant.SourceMaterials != null && slot < variant.SourceMaterials.Length
                ? variant.SourceMaterials[slot]
                : null;
            string materialName = GetMaterialName(group, variant, slot);
            string targetPath = group.FolderPath + "/" + materialName + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(targetPath);
            if (material == null)
            {
                Material existingV3 = FindExistingV3Material(group, variant, slot, v3Shader);
                if (existingV3 != null && CanMoveExistingMaterial(existingV3, targetPath))
                {
                    string oldPath = AssetDatabase.GetAssetPath(existingV3);
                    string moveError = AssetDatabase.MoveAsset(oldPath, targetPath);
                    if (string.IsNullOrEmpty(moveError))
                    {
                        material = AssetDatabase.LoadAssetAtPath<Material>(targetPath);
                        material.name = materialName;
                        EditorUtility.SetDirty(material);
                    }
                }
            }

            if (material == null)
            {
                Material existingV3 = FindExistingV3Material(group, variant, slot, v3Shader);
                material = new Material(existingV3 != null ? existingV3 : template)
                {
                    name = materialName,
                    shader = v3Shader
                };
                ApplySourceAppearance(material, source, extractedMaskCache, warnings);
                SetFloatIfPresent(material, 0f, "_FlameInfluenceRadius");
                SetFloatIfPresent(material, 0f, "_TransitionProgress");
                SetVectorIfPresent(material, Vector4.zero, "_FlameCenter");
                AssetDatabase.CreateAsset(material, targetPath);
                createdPaths.Add(targetPath);
                result.CreatedMaterialCount++;
            }
            else
            {
                result.ReusedMaterialCount++;
                if (material.shader != v3Shader)
                {
                    material.shader = v3Shader;
                    EditorUtility.SetDirty(material);
                }
            }
            variant.GeneratedMaterials[slot] = material;
        }

        string prefabName = group.FolderName + "_Ice"
            + (variant.IsPrimary ? string.Empty : "_Variant_" + variant.VariantId);
        variant.PrefabPath = group.FolderPath + "/" + prefabName + ".prefab";
        GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(variant.PrefabPath);
        bool prefabExisted = existingPrefab != null;
        bool prefabNeedsUpdate = !PrefabMatches(existingPrefab, group, variant);
        if (prefabNeedsUpdate)
            CreateOrUpdatePrefab(group, variant, prefabName);
        variant.GeneratedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(variant.PrefabPath);
        if (prefabExisted && prefabNeedsUpdate) result.UpdatedPrefabCount++;
        else
        {
            if (!prefabExisted)
            {
                result.CreatedPrefabCount++;
                createdPaths.Add(variant.PrefabPath);
            }
        }

        LitIcePrefabCatalogEntry catalogEntry = catalog.FindBySourceId(group.SourceMeshId);
        LitIcePrefabVariantEntry variantEntry = catalogEntry.Variants
            .FirstOrDefault(entry => entry.VariantId == variant.VariantId);
        if (variantEntry == null)
        {
            variantEntry = new LitIcePrefabVariantEntry { VariantId = variant.VariantId };
            catalogEntry.Variants.Add(variantEntry);
        }
        variantEntry.PrefabPath = variant.PrefabPath;
        variantEntry.Prefab = variant.GeneratedPrefab;
        variantEntry.SourceMaterials = (variant.SourceMaterials ?? Array.Empty<Material>()).ToList();
        variantEntry.SourceMaterialIds = variantEntry.SourceMaterials.Select(GetStableObjectId).ToList();
        variantEntry.Materials = variant.GeneratedMaterials.ToList();
    }

    private static void ApplyToScene(AnalysisResult analysis, GenerationResult result)
    {
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Apply Maison Ice Prefabs");
        bool sceneChanged = false;
        try
        {
            foreach (ModelGroup group in analysis.Groups)
            foreach (ModelVariant variant in group.Variants)
            foreach (RendererRecord record in variant.Records)
            {
                bool meshChanged = record.Filter.sharedMesh != group.GeneratedBakedMesh;
                bool materialsChanged = !record.Renderer.sharedMaterials.SequenceEqual(
                    variant.GeneratedMaterials);
                if (!meshChanged && !materialsChanged)
                    continue;

                if (meshChanged)
                {
                    Undo.RecordObject(record.Filter, "Assign baked Lit Ice mesh");
                    record.Filter.sharedMesh = group.GeneratedBakedMesh;
                    EditorUtility.SetDirty(record.Filter);
                    if (PrefabUtility.IsPartOfPrefabInstance(record.Filter))
                        PrefabUtility.RecordPrefabInstancePropertyModifications(record.Filter);
                }
                if (materialsChanged)
                {
                    Undo.RecordObject(record.Renderer, "Assign Lit Ice materials");
                    record.Renderer.sharedMaterials = variant.GeneratedMaterials;
                    EditorUtility.SetDirty(record.Renderer);
                    if (PrefabUtility.IsPartOfPrefabInstance(record.Renderer))
                        PrefabUtility.RecordPrefabInstancePropertyModifications(record.Renderer);
                }
                sceneChanged = true;
                result.UpdatedRendererCount++;
            }

            if (sceneChanged)
            {
                EditorSceneManager.MarkSceneDirty(analysis.Scene);
                if (!EditorSceneManager.SaveScene(analysis.Scene, ScenePath))
                    throw new InvalidOperationException("Maison could not be saved.");
            }
            Undo.CollapseUndoOperations(undoGroup);
        }
        catch
        {
            Undo.RevertAllDownToGroup(undoGroup);
            throw;
        }
    }

    private static void CreateOrUpdatePrefab(ModelGroup group, ModelVariant variant, string prefabName)
    {
        MeshRenderer representative = variant.Representative;
        if (representative == null)
            throw new InvalidOperationException(group.SourceMesh.name + " has no representative renderer.");

        var root = new GameObject(prefabName)
        {
            layer = representative.gameObject.layer,
            tag = representative.gameObject.tag
        };
        try
        {
            GameObjectUtility.SetStaticEditorFlags(
                root, GameObjectUtility.GetStaticEditorFlags(representative.gameObject));
            MeshFilter filter = root.AddComponent<MeshFilter>();
            MeshRenderer renderer = root.AddComponent<MeshRenderer>();
            filter.sharedMesh = group.GeneratedBakedMesh;
            renderer.sharedMaterials = variant.GeneratedMaterials;
            renderer.shadowCastingMode = representative.shadowCastingMode;
            renderer.receiveShadows = representative.receiveShadows;
            renderer.lightProbeUsage = representative.lightProbeUsage;
            renderer.reflectionProbeUsage = representative.reflectionProbeUsage;
            renderer.motionVectorGenerationMode = representative.motionVectorGenerationMode;
            renderer.allowOcclusionWhenDynamic = representative.allowOcclusionWhenDynamic;
            renderer.renderingLayerMask = representative.renderingLayerMask;
            renderer.rendererPriority = representative.rendererPriority;

            foreach (Collider sourceCollider in representative.GetComponents<Collider>())
            {
                Collider destination = root.AddComponent(sourceCollider.GetType()) as Collider;
                if (destination == null)
                    continue;
                EditorUtility.CopySerialized(sourceCollider, destination);
                if (destination is MeshCollider destinationMesh
                    && sourceCollider is MeshCollider sourceMesh)
                    destinationMesh.sharedMesh = sourceMesh.sharedMesh ?? group.SourceMesh;
            }

            PrefabUtility.SaveAsPrefabAsset(root, variant.PrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static bool PrefabMatches(
        GameObject prefab, ModelGroup group, ModelVariant variant)
    {
        if (prefab == null)
            return false;
        MeshFilter filter = prefab.GetComponent<MeshFilter>();
        MeshRenderer renderer = prefab.GetComponent<MeshRenderer>();
        MeshRenderer representative = variant.Representative;
        if (filter == null || renderer == null || representative == null)
            return false;
        if (filter.sharedMesh != group.GeneratedBakedMesh
            || !renderer.sharedMaterials.SequenceEqual(variant.GeneratedMaterials))
            return false;
        if (prefab.layer != representative.gameObject.layer
            || prefab.tag != representative.gameObject.tag
            || GameObjectUtility.GetStaticEditorFlags(prefab)
                != GameObjectUtility.GetStaticEditorFlags(representative.gameObject))
            return false;
        Collider[] prefabColliders = prefab.GetComponents<Collider>();
        Collider[] sourceColliders = representative.GetComponents<Collider>();
        if (prefabColliders.Length != sourceColliders.Length)
            return false;
        for (int i = 0; i < prefabColliders.Length; i++)
        {
            if (prefabColliders[i].GetType() != sourceColliders[i].GetType())
                return false;
            if (prefabColliders[i] is MeshCollider prefabMesh
                && sourceColliders[i] is MeshCollider sourceMesh
                && prefabMesh.sharedMesh != (sourceMesh.sharedMesh ?? group.SourceMesh))
                return false;
        }
        return true;
    }

    private static void ApplySourceAppearance(
        Material destination,
        Material source,
        Dictionary<Texture, ExtractedMaskMaps> extractedMaskCache,
        List<string> warnings)
    {
        if (destination == null || source == null)
            return;

        TextureSlot baseMap = CaptureTexture(source,
            "_BaseTexture", "_BaseColorMap", "_BaseMap", "_MainTex", "_ColorMap", "_AlbedoMap",
            "Material_Texture2D_1", "_UnlitColorMap");
        TextureSlot normalMap = CaptureTexture(source,
            "_NormalTexture", "_Normals", "_NormalMap", "_BumpMap", "Material_Texture2D_0");
        TextureSlot roughnessMap = CaptureTexture(source,
            "_BaseRoughnessTexture", "_RoughnessMap", "_RoughnessTexture");
        TextureSlot metallicMap = CaptureTexture(source,
            "_BaseMetallicTexture", "_MetallicMap", "_MetallicTexture");
        TextureSlot occlusionMap = CaptureTexture(source,
            "_BaseOcclusionTexture", "_OcclusionMap", "_OcclusionTexture");
        TextureSlot maskMap = CaptureTexture(source,
            "_MaskMap", "_MetallicGlossMap", "Material_Texture2D_2");

        ApplyTexture(destination, "_BaseTexture", baseMap);
        ApplyTexture(destination, "_NormalTexture", normalMap);

        if (roughnessMap?.Texture != null)
        {
            ApplyTexture(destination, "_BaseRoughnessTexture", roughnessMap);
            SetFloatIfPresent(destination, 1f, "_UseBaseRoughnessTexture");
        }
        if (metallicMap?.Texture != null)
        {
            ApplyTexture(destination, "_BaseMetallicTexture", metallicMap);
            SetFloatIfPresent(destination, 1f, "_UseBaseMetallicTexture");
        }
        if (occlusionMap?.Texture != null)
        {
            ApplyTexture(destination, "_BaseOcclusionTexture", occlusionMap);
            SetFloatIfPresent(destination, 1f, "_UseBaseOcclusionTexture");
        }

        if (maskMap?.Texture is Texture2D maskTexture
            && (roughnessMap?.Texture == null || metallicMap?.Texture == null || occlusionMap?.Texture == null))
        {
            try
            {
                if (!extractedMaskCache.TryGetValue(maskTexture, out ExtractedMaskMaps extracted))
                {
                    bool hdrpMask = maskMap.PropertyName != "_MetallicGlossMap";
                    extracted = ExtractMaskMaps(maskTexture, hdrpMask);
                    extractedMaskCache.Add(maskTexture, extracted);
                }
                if (metallicMap?.Texture == null && extracted.Metallic != null)
                {
                    destination.SetTexture("_BaseMetallicTexture", extracted.Metallic);
                    SetFloatIfPresent(destination, 1f, "_UseBaseMetallicTexture");
                }
                if (roughnessMap?.Texture == null && extracted.Roughness != null)
                {
                    destination.SetTexture("_BaseRoughnessTexture", extracted.Roughness);
                    SetFloatIfPresent(destination, 1f, "_UseBaseRoughnessTexture");
                }
                if (occlusionMap?.Texture == null && extracted.Occlusion != null)
                {
                    destination.SetTexture("_BaseOcclusionTexture", extracted.Occlusion);
                    SetFloatIfPresent(destination, 1f, "_UseBaseOcclusionTexture");
                }
            }
            catch (Exception exception)
            {
                warnings.Add($"{source.name}: mask extraction failed ({exception.Message}). Scalar fallbacks retained.");
            }
        }

        if (TryGetColor(source, out Color baseColor, "_BaseColor", "_Color"))
            SetColorIfPresent(destination, baseColor, "_BaseColor");
        if (TryGetFloat(source, out float normalStrength,
            "_BaseNormalStrength", "_NormalsStrength", "_NormalStrength", "_BumpScale"))
            SetFloatIfPresent(destination, normalStrength, "_BaseNormalStrength");
        if (TryGetFloat(source, out float smoothness, "_BaseSmoothness", "_Smoothness", "_Glossiness"))
            SetFloatIfPresent(destination, smoothness, "_BaseSmoothness");
        else if (TryGetFloat(source, out float roughness, "_Roughness"))
            SetFloatIfPresent(destination, 1f - Mathf.Sqrt(Mathf.Clamp01(roughness)), "_BaseSmoothness");
        if (TryGetFloat(source, out float metallic, "_BaseMetallic", "_Metallic"))
            SetFloatIfPresent(destination, metallic, "_BaseMetallic");
        if (TryGetFloat(source, out float useScale, "_UseScaleTiling"))
            SetFloatIfPresent(destination, useScale, "_UseScaleTiling");
        if (TryGetFloat(source, out float tiling, "_TilingMultiplier"))
            SetFloatIfPresent(destination, tiling, "_TilingMultiplier");

        if (baseMap?.Texture == null)
            warnings.Add($"{source.name} ({source.shader?.name}): no recognized Base/Color texture; BaseColor fallback retained.");
        EditorUtility.SetDirty(destination);
    }

    private static ExtractedMaskMaps ExtractMaskMaps(Texture2D source, bool hasOcclusionChannel)
    {
        string sourceId = ShortHash(GetStableObjectId(source));
        string baseName = SanitizeName(source.name) + "_" + sourceId;
        string metallicPath = SharedMapFolder + "/" + baseName + "_Metallic.png";
        string roughnessPath = SharedMapFolder + "/" + baseName + "_Roughness.png";
        string occlusionPath = SharedMapFolder + "/" + baseName + "_Occlusion.png";
        var result = new ExtractedMaskMaps
        {
            Metallic = AssetDatabase.LoadAssetAtPath<Texture2D>(metallicPath),
            Roughness = AssetDatabase.LoadAssetAtPath<Texture2D>(roughnessPath),
            Occlusion = hasOcclusionChannel
                ? AssetDatabase.LoadAssetAtPath<Texture2D>(occlusionPath)
                : null
        };
        if (result.Metallic != null && result.Roughness != null
            && (!hasOcclusionChannel || result.Occlusion != null))
            return result;

        RenderTexture previous = RenderTexture.active;
        RenderTexture renderTexture = RenderTexture.GetTemporary(
            source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        Texture2D readable = null;
        Texture2D metallic = null;
        Texture2D roughness = null;
        Texture2D occlusion = null;
        try
        {
            Graphics.Blit(source, renderTexture);
            RenderTexture.active = renderTexture;
            readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true);
            readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            readable.Apply(false, false);
            Color32[] pixels = readable.GetPixels32();
            var metallicPixels = new Color32[pixels.Length];
            var roughnessPixels = new Color32[pixels.Length];
            var occlusionPixels = hasOcclusionChannel ? new Color32[pixels.Length] : null;
            for (int i = 0; i < pixels.Length; i++)
            {
                byte m = pixels[i].r;
                byte r = (byte)(255 - pixels[i].a);
                metallicPixels[i] = new Color32(m, m, m, 255);
                roughnessPixels[i] = new Color32(r, r, r, 255);
                if (hasOcclusionChannel)
                {
                    byte o = pixels[i].g;
                    occlusionPixels[i] = new Color32(o, o, o, 255);
                }
            }

            metallic = CreateDataTexture(source.width, source.height, metallicPixels);
            roughness = CreateDataTexture(source.width, source.height, roughnessPixels);
            if (hasOcclusionChannel)
                occlusion = CreateDataTexture(source.width, source.height, occlusionPixels);
            WriteTextureAsset(metallic, metallicPath);
            WriteTextureAsset(roughness, roughnessPath);
            if (hasOcclusionChannel)
                WriteTextureAsset(occlusion, occlusionPath);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureDataTexture(metallicPath, source.mipmapCount > 1);
            ConfigureDataTexture(roughnessPath, source.mipmapCount > 1);
            if (hasOcclusionChannel)
                ConfigureDataTexture(occlusionPath, source.mipmapCount > 1);
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(renderTexture);
            UnityEngine.Object.DestroyImmediate(readable);
            UnityEngine.Object.DestroyImmediate(metallic);
            UnityEngine.Object.DestroyImmediate(roughness);
            UnityEngine.Object.DestroyImmediate(occlusion);
        }

        result.Metallic = AssetDatabase.LoadAssetAtPath<Texture2D>(metallicPath);
        result.Roughness = AssetDatabase.LoadAssetAtPath<Texture2D>(roughnessPath);
        result.Occlusion = hasOcclusionChannel
            ? AssetDatabase.LoadAssetAtPath<Texture2D>(occlusionPath)
            : null;
        return result;
    }

    private static Texture2D CreateDataTexture(int width, int height, Color32[] pixels)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        return texture;
    }

    private static void WriteTextureAsset(Texture2D texture, string assetPath)
    {
        string absolute = Path.GetFullPath(assetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute));
        File.WriteAllBytes(absolute, texture.EncodeToPNG());
    }

    private static void ConfigureDataTexture(string path, bool mipmaps)
    {
        if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            return;
        importer.sRGBTexture = false;
        importer.alphaSource = TextureImporterAlphaSource.None;
        importer.mipmapEnabled = mipmaps;
        importer.textureCompression = TextureImporterCompression.Compressed;
        importer.SaveAndReimport();
    }

    private static Mesh ResolveSourceMesh(
        MeshFilter filter, Mesh current, LitIcePrefabCatalog catalog, List<string> warnings)
    {
        if (!LitIceEdgeMaskBaker.IsCurrentBakedMesh(current))
            return current;
        LitIcePrefabCatalogEntry catalogEntry = catalog?.FindByBakedMesh(current);
        if (catalogEntry?.SourceMesh != null)
            return catalogEntry.SourceMesh;

        MeshFilter original = PrefabUtility.GetCorrespondingObjectFromOriginalSource(filter);
        if (original != null && original.sharedMesh != null
            && !LitIceEdgeMaskBaker.IsCurrentBakedMesh(original.sharedMesh))
            return original.sharedMesh;

        MeshCollider collider = filter.GetComponent<MeshCollider>();
        if (collider != null && collider.sharedMesh != null
            && !LitIceEdgeMaskBaker.IsCurrentBakedMesh(collider.sharedMesh))
            return collider.sharedMesh;

        warnings.Add($"{filter.name}: original mesh could not be resolved from baked mesh '{current.name}'.");
        return current;
    }

    private static Material[] ResolveSourceMaterials(
        MeshRenderer renderer,
        Material[] current,
        string sourceMeshId,
        LitIcePrefabCatalog catalog,
        Shader v3Shader)
    {
        LitIcePrefabCatalogEntry catalogEntry = catalog?.FindBySourceId(sourceMeshId);
        if (catalogEntry != null)
        {
            LitIcePrefabVariantEntry known = catalogEntry.Variants.FirstOrDefault(entry =>
                entry.Materials != null && entry.Materials.SequenceEqual(current));
            if (known?.SourceMaterials != null && known.SourceMaterials.Count > 0)
                return known.SourceMaterials.ToArray();
        }

        MeshRenderer original = PrefabUtility.GetCorrespondingObjectFromOriginalSource(renderer);
        Material[] originalMaterials = original != null ? original.sharedMaterials : null;
        if (originalMaterials == null || originalMaterials.Length == 0)
            return current.ToArray();

        int length = Math.Max(current.Length, originalMaterials.Length);
        var resolved = new Material[length];
        for (int i = 0; i < length; i++)
        {
            Material currentMaterial = i < current.Length ? current[i] : null;
            Material originalMaterial = i < originalMaterials.Length ? originalMaterials[i] : null;
            resolved[i] = currentMaterial != null && currentMaterial.shader != v3Shader
                ? currentMaterial
                : originalMaterial ?? currentMaterial;
        }
        return resolved;
    }

    private static void AssignFolderNamesAndPrimaryVariants(List<ModelGroup> groups, Shader v3Shader)
    {
        foreach (IGrouping<string, ModelGroup> sameName in groups.GroupBy(
            group => group.SafeMeshName, StringComparer.OrdinalIgnoreCase))
        {
            bool collision = sameName.Count() > 1;
            foreach (ModelGroup group in sameName)
                group.FolderName = collision
                    ? group.SafeMeshName + "__" + ShortHash(group.SourceMeshId)
                    : group.SafeMeshName;
        }

        foreach (ModelGroup group in groups)
        {
            string expected = "Material_LitIceFrostedEdges_" + group.FolderName;
            ModelVariant primary = group.Variants
                .OrderByDescending(variant => variant.Records.Any(record =>
                    record.CurrentMaterials.Any(material => material != null
                        && material.shader == v3Shader && material.name == expected)))
                .ThenBy(variant => variant.VariantId, StringComparer.Ordinal)
                .FirstOrDefault();
            if (primary != null)
                primary.IsPrimary = true;
        }
    }

    private static Material FindTemplateMaterial(Shader v3Shader)
    {
        Material exact = AssetDatabase.FindAssets(PillarTemplateName + " t:Material")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<Material>)
            .FirstOrDefault(material => material != null
                && material.name == PillarTemplateName
                && material.shader == v3Shader);
        return exact ?? AssetDatabase.LoadAssetAtPath<Material>(CanonicalMaterialPath);
    }

    private static bool IsExcludedRenderer(MeshRenderer renderer)
    {
        return renderer == null
            || renderer.GetComponent<TMPro.TMP_Text>() != null
            || renderer.GetComponentInParent<Canvas>() != null
            || HasAncestorNamed(renderer.transform, "GlobalVolume");
    }

    private static bool HasAncestorNamed(Transform value, string objectName)
    {
        for (Transform current = value; current != null; current = current.parent)
            if (string.Equals(current.name, objectName, StringComparison.Ordinal))
                return true;
        return false;
    }

    private static Material FindExistingV3Material(
        ModelGroup group, ModelVariant variant, int slot, Shader v3Shader)
    {
        return variant.Records
            .Select(record => slot < record.CurrentMaterials.Length ? record.CurrentMaterials[slot] : null)
            .FirstOrDefault(material => material != null && material.shader == v3Shader);
    }

    private static bool CanMoveExistingMaterial(Material material, string targetPath)
    {
        if (material == null)
            return false;
        string sourcePath = AssetDatabase.GetAssetPath(material);
        if (string.IsNullOrEmpty(sourcePath) || sourcePath == targetPath)
            return false;
        if (sourcePath == CanonicalMaterialPath)
            return false;
        return material.name.StartsWith("Material_LitIceFrostedEdges_", StringComparison.Ordinal)
            && string.Equals(material.name, Path.GetFileNameWithoutExtension(targetPath),
                StringComparison.Ordinal);
    }

    private static string GetMaterialName(ModelGroup group, ModelVariant variant, int slot)
    {
        string name = "Material_LitIceFrostedEdges_" + group.FolderName;
        if (!variant.IsPrimary)
            name += "_Variant_" + variant.VariantId;
        if (slot > 0)
            name += "_Slot_" + (slot + 1).ToString("00");
        return name;
    }

    private static TextureSlot CaptureTexture(Material material, params string[] propertyNames)
    {
        foreach (string propertyName in propertyNames)
        {
            if (!material.HasProperty(propertyName))
                continue;
            Texture texture = material.GetTexture(propertyName);
            if (texture == null)
                continue;
            return new TextureSlot
            {
                Texture = texture,
                PropertyName = propertyName,
                Scale = material.GetTextureScale(propertyName),
                Offset = material.GetTextureOffset(propertyName)
            };
        }
        return null;
    }

    private static void ApplyTexture(Material material, string destinationProperty, TextureSlot slot)
    {
        if (material == null || slot?.Texture == null || !material.HasProperty(destinationProperty))
            return;
        material.SetTexture(destinationProperty, slot.Texture);
        material.SetTextureScale(destinationProperty, slot.Scale);
        material.SetTextureOffset(destinationProperty, slot.Offset);
    }

    private static bool TryGetFloat(Material material, out float value, params string[] names)
    {
        foreach (string name in names)
            if (material.HasProperty(name))
            {
                value = material.GetFloat(name);
                return true;
            }
        value = 0f;
        return false;
    }

    private static bool TryGetColor(Material material, out Color value, params string[] names)
    {
        foreach (string name in names)
            if (material.HasProperty(name))
            {
                value = material.GetColor(name);
                return true;
            }
        value = Color.white;
        return false;
    }

    private static void SetFloatIfPresent(Material material, float value, params string[] names)
    {
        foreach (string name in names)
            if (material.HasProperty(name)) material.SetFloat(name, value);
    }

    private static void SetColorIfPresent(Material material, Color value, params string[] names)
    {
        foreach (string name in names)
            if (material.HasProperty(name)) material.SetColor(name, value);
    }

    private static void SetVectorIfPresent(Material material, Vector4 value, params string[] names)
    {
        foreach (string name in names)
            if (material.HasProperty(name)) material.SetVector(name, value);
    }

    private static string GetMaterialSetId(IEnumerable<Material> materials)
    {
        string value = string.Join("|", (materials ?? Array.Empty<Material>()).Select(GetStableObjectId));
        return ShortHash(value);
    }

    private static string GetStableObjectId(UnityEngine.Object value)
    {
        if (value == null)
            return "null";
        if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out string guid, out long localId))
            return guid + ":" + localId;
        if (value is Mesh mesh)
            return $"builtin:{mesh.name}:{mesh.vertexCount}:{mesh.subMeshCount}";
        return "instance:" + value.name + ":" + value.GetType().FullName;
    }

    private static string GetDependencyHash(Mesh mesh)
    {
        string path = AssetDatabase.GetAssetPath(mesh);
        return string.IsNullOrEmpty(path)
            ? Hash128.Compute(GetStableObjectId(mesh)).ToString()
            : AssetDatabase.GetAssetDependencyHash(path).ToString();
    }

    private static string ShortHash(string value)
    {
        string hash = Hash128.Compute(value ?? string.Empty).ToString();
        return hash.Substring(0, 8);
    }

    private static string SanitizeName(string value)
    {
        string name = string.IsNullOrWhiteSpace(value) ? "UnnamedMesh" : value.Trim();
        foreach (char invalid in Path.GetInvalidFileNameChars())
            name = name.Replace(invalid, '_');
        name = name.Replace('/', '_').Replace('\\', '_');
        while (name.Contains("  ")) name = name.Replace("  ", " ");
        name = name.Trim(' ', '.');
        if (name.Length > 80)
            name = name.Substring(0, 70) + "_" + ShortHash(name);
        return string.IsNullOrEmpty(name) ? "UnnamedMesh" : name;
    }

    private static LitIcePrefabCatalog GetOrCreateCatalog()
    {
        LitIcePrefabCatalog catalog = AssetDatabase.LoadAssetAtPath<LitIcePrefabCatalog>(CatalogPath);
        if (catalog != null)
            return catalog;
        catalog = ScriptableObject.CreateInstance<LitIcePrefabCatalog>();
        catalog.name = "LitIcePrefabCatalog";
        AssetDatabase.CreateAsset(catalog, CatalogPath);
        return catalog;
    }

    private static Scene GetMaisonScene(bool openIfMissing)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene loaded = SceneManager.GetSceneAt(i);
            if (loaded.path == ScenePath)
                return loaded;
        }
        if (!openIfMissing)
            throw new InvalidOperationException("Maison is not loaded.");
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            throw new OperationCanceledException("Scene opening cancelled.");
        return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    private static void EnsureAssetFolder(string folder)
    {
        if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder))
            return;
        string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
        EnsureAssetFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }

    private static void CheckCancelled(
        bool showProgress, int step, int total, string message)
    {
        if (showProgress && EditorUtility.DisplayCancelableProgressBar(
            "Maison Ice Prefabs", message, Mathf.Clamp01((float)step / total)))
            throw new OperationCanceledException();
    }
}
