using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
internal static class LitIceMaisonVerificationRunner
{
    // Temporary validation bridge used only while verifying the generated library.
    private const string CommandPath = "Temp/LitIceMaisonCommand.txt";
    private const string ReportPath = "Temp/LitIceMaisonReport.txt";
    private static double s_NextPoll;

    static LitIceMaisonVerificationRunner()
    {
        EditorApplication.update -= Poll;
        EditorApplication.update += Poll;
    }

    private static void Poll()
    {
        if (EditorApplication.timeSinceStartup < s_NextPoll || !File.Exists(CommandPath))
            return;
        s_NextPoll = EditorApplication.timeSinceStartup + 0.5d;
        string command = File.ReadAllText(CommandPath).Trim();
        File.Delete(CommandPath);
        try
        {
            if (command == "REPAIR_SCOPE")
            {
                LitIceMaisonPrefabGenerator.ScopeRepairResult repair =
                    LitIceMaisonPrefabGenerator.RepairGeneratedScopeToStatic();
                File.WriteAllText(ReportPath,
                    "COMMAND=REPAIR_SCOPE\nRESULT=" + repair + "\n");
                return;
            }
            if (command == "REOPEN_VERIFY")
                EditorSceneManager.OpenScene(
                    LitIceMaisonPrefabGenerator.ScenePath, OpenSceneMode.Single);

            LitIceMaisonPrefabGenerator.AnalysisResult analysis =
                LitIceMaisonPrefabGenerator.AnalyzeMaison(false);
            if (command == "ANALYZE")
            {
                WriteAnalysis(analysis);
                return;
            }
            if (command == "REOPEN_VERIFY" || command == "VERIFY_CURRENT")
            {
                WriteVerification(analysis);
                return;
            }

            LitIceMaisonPrefabGenerator.AnalysisResult scope = analysis;
            if (command == "GENERATE_PILLAR")
                scope = LitIceMaisonPrefabGenerator.AnalyzeMaison(false, "SM_Pillar_01_2H");
            else if (command != "GENERATE_ALL")
                throw new InvalidOperationException("Unknown command: " + command);

            LitIceMaisonPrefabGenerator.GenerationResult result =
                LitIceMaisonPrefabGenerator.Generate(scope, true, false);
            File.WriteAllText(ReportPath,
                "COMMAND=" + command + "\n"
                + "ANALYSIS=" + scope.ToDisplayMessage() + "\n"
                + "RESULT=" + result.ToDisplayMessage() + "\n");
        }
        catch (Exception exception)
        {
            File.WriteAllText(ReportPath,
                "COMMAND=" + command + "\nERROR=" + exception + "\n");
            UnityEngine.Debug.LogException(exception);
        }
    }

    private static void WriteAnalysis(LitIceMaisonPrefabGenerator.AnalysisResult analysis)
    {
        LitIceMaisonPrefabGenerator.ModelGroup pillar = analysis.Groups
            .FirstOrDefault(group => group.SourceMesh.name == "SM_Pillar_01_2H");
        File.WriteAllText(ReportPath,
            "COMMAND=ANALYZE\n"
            + "SUMMARY=" + analysis.ToDisplayMessage() + "\n"
            + "MULTI_MATERIAL_RENDERERS=" + analysis.MultiMaterialRendererCount + "\n"
            + "EXISTING_V3_MATERIALS=" + analysis.ExistingV3MaterialCount + "\n"
            + "PILLAR_FOUND=" + (pillar != null) + "\n"
            + "PILLAR_VARIANTS=" + (pillar?.Variants.Count ?? 0) + "\n"
            + "PILLAR_INSTANCES=" + (pillar?.Variants.Sum(item => item.Records.Count) ?? 0) + "\n"
            + "WARNINGS=" + string.Join("\n- ", analysis.Warnings.Take(50)) + "\n"
            + "ERRORS=" + string.Join("\n- ", analysis.Errors) + "\n");
    }

    private static void WriteVerification(
        LitIceMaisonPrefabGenerator.AnalysisResult analysis)
    {
        GameObject world = analysis.WorldRoot;
        MeshRenderer[] allRenderers = world.GetComponentsInChildren<MeshRenderer>(true);
        MeshRenderer[] renderers = allRenderers
            .Where(renderer => renderer.gameObject.isStatic)
            .ToArray();
        int missingMeshes = renderers.Count(renderer =>
        {
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            return filter == null || filter.sharedMesh == null;
        });
        int missingMaterials = renderers.Sum(renderer =>
            renderer.sharedMaterials.Count(material => material == null));
        Shader v3 = AssetDatabase.LoadAssetAtPath<Shader>(
            "Assets/Materials/IceShader/ShaderGraph_LitIceFrostedEdges_v3.shadergraph");
        int nonV3Materials = renderers.Sum(renderer =>
            renderer.sharedMaterials.Count(material => material != null && material.shader != v3));
        LitIcePrefabCatalog catalog = AssetDatabase.LoadAssetAtPath<LitIcePrefabCatalog>(
            "Assets/Environment/Prefabs_Ice/LitIcePrefabCatalog.asset");
        string missingMeshDetails = string.Join(" | ", renderers
            .Where(renderer =>
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                return filter == null || filter.sharedMesh == null;
            })
            .Select(renderer => GetHierarchyPath(renderer.transform)));
        string missingMaterialDetails = string.Join(" | ", renderers
            .Where(renderer => renderer.sharedMaterials.Any(material => material == null))
            .Select(renderer => GetHierarchyPath(renderer.transform)));
        string nonV3Details = string.Join(" | ", renderers
            .Where(renderer => renderer.sharedMaterials.Any(
                material => material != null && material.shader != v3))
            .Select(renderer => GetHierarchyPath(renderer.transform) + " ["
                + string.Join(", ", renderer.sharedMaterials.Select(material =>
                    material != null ? material.name + ":" + material.shader.name : "null")) + "]"));
        File.WriteAllText(ReportPath,
            "COMMAND=VERIFY\n"
            + "ANALYSIS=" + analysis.ToDisplayMessage() + "\n"
            + "ALL_RENDERERS=" + allRenderers.Length + "\n"
            + "STATIC_RENDERERS=" + renderers.Length + "\n"
            + "CATALOG_MESHES=" + (catalog?.Entries.Count ?? 0) + "\n"
            + "CATALOG_VARIANTS=" + (catalog?.Entries.Sum(entry => entry.Variants.Count) ?? 0) + "\n"
            + "MISSING_MESHES=" + missingMeshes + "\n"
            + "MISSING_MATERIALS=" + missingMaterials + "\n"
            + "NON_V3_MATERIAL_SLOTS=" + nonV3Materials + "\n"
            + "MISSING_MESH_DETAILS=" + missingMeshDetails + "\n"
            + "MISSING_MATERIAL_DETAILS=" + missingMaterialDetails + "\n"
            + "NON_V3_DETAILS=" + nonV3Details + "\n");
    }

    private static string GetHierarchyPath(Transform transform)
    {
        string path = transform.name;
        while (transform.parent != null)
        {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }
        return path;
    }
}
