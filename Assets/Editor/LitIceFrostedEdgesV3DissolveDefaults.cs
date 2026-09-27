using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Applies the project-wide dissolve defaults to materials using the Lit Ice Frosted Edges V3 shader.
/// The request file lets this run once after Unity has reimported the Shader Graph.
/// </summary>
[InitializeOnLoad]
public static class LitIceFrostedEdgesV3DissolveDefaults
{
    private const string ShaderPath = "Assets/Materials/IceShader/ShaderGraph_LitIceFrostedEdges_v3.shadergraph";
    private static readonly string ProjectPath = Directory.GetParent(Application.dataPath).FullName;
    private static readonly string RequestPath = Path.Combine(ProjectPath, "Library", "LitIceFrostedEdgesV3DissolveDefaults.request");
    private static readonly string ResultPath = Path.Combine(ProjectPath, "Library", "LitIceFrostedEdgesV3DissolveDefaults.result");

    static LitIceFrostedEdgesV3DissolveDefaults()
    {
        EditorApplication.delayCall += ProcessPendingRequest;
        EditorApplication.update += ProcessPendingRequest;
    }

    [InitializeOnLoadMethod]
    private static void ApplyPendingRequestAfterReload()
    {
        EditorApplication.delayCall += ProcessPendingRequest;
    }

    [MenuItem("Lit/Ice/Apply V3 Dissolve Defaults To Materials")]
    public static void ApplyFromMenu()
    {
        int updated = ApplyToAllMaterials();
        Debug.Log($"Applied Lit Ice Frosted Edges V3 dissolve defaults to {updated} material(s).");
    }

    private static void ProcessPendingRequest()
    {
        if (!File.Exists(RequestPath) || EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            return;
        }

        File.Delete(RequestPath);

        try
        {
            int updated = ApplyToAllMaterials();
            File.WriteAllText(ResultPath, $"success:{updated}");
            Debug.Log($"Applied Lit Ice Frosted Edges V3 dissolve defaults to {updated} material(s).");
        }
        catch (Exception exception)
        {
            File.WriteAllText(ResultPath, $"error:{exception}");
            Debug.LogException(exception);
        }
    }

    private static int ApplyToAllMaterials()
    {
        Shader targetShader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        if (targetShader == null)
        {
            throw new InvalidOperationException($"Unable to load shader at '{ShaderPath}'.");
        }

        int updated = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
        {
            string materialPath = AssetDatabase.GUIDToAssetPath(guid);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null || material.shader != targetShader)
            {
                continue;
            }

            material.SetFloat("_DissolveScale", 7f);
            material.SetFloat("_DissolveShapeBlend", 1f);
            EditorUtility.SetDirty(material);
            updated++;
        }

        AssetDatabase.SaveAssets();
        return updated;
    }
}
