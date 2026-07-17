using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

internal static class LitIceV3PerformanceUtility
{
    private const string ShaderPath =
        "Assets/Materials/IceShader/ShaderGraph_LitIceFrostedEdges_v3.shadergraph";
    private const string ShaderName = "LIT/Ice/Lit Ice Frosted Edges V3";
    private static readonly string[] MaterialSearchRoots =
    {
        "Assets/Materials/IceShader",
        "Assets/Environment/Prefabs_Ice"
    };

    [MenuItem("Lit/Shadergraph/Optimize Ice V3 Materials For Runtime")]
    private static void OptimizeAllV3MaterialsMenu()
    {
        int optimizedCount = OptimizeAllV3Materials(recordUndo: true);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Lit Ice] Runtime performance render state applied to {optimizedCount} Ice V3 material(s).");
    }

    internal static int OptimizeAllV3Materials(bool recordUndo)
    {
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        string[] existingRoots = MaterialSearchRoots
            .Where(AssetDatabase.IsValidFolder)
            .ToArray();
        if (existingRoots.Length == 0)
            existingRoots = new[] { "Assets" };

        int count = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Material", existingRoots))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!IsIceV3Material(material, shader))
                continue;

            ApplyRuntimePerformanceState(material, recordUndo);
            count++;
        }
        return count;
    }

    internal static bool IsIceV3Material(Material material, Shader shader = null)
    {
        if (material == null || material.shader == null)
            return false;

        shader ??= AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        return material.shader == shader
            || string.Equals(material.shader.name, ShaderName, StringComparison.Ordinal);
    }

    internal static void ApplyRuntimePerformanceState(Material material, bool recordUndo = false)
    {
        if (material == null)
            return;

        if (recordUndo)
            Undo.RecordObject(material, "Optimize Ice V3 material for runtime");

        material.SetOverrideTag("RenderType", "Opaque");
        material.renderQueue = 2000;
        material.doubleSidedGI = false;

        material.EnableKeyword("_DISABLE_DECALS");
        material.EnableKeyword("_DISABLE_SSR");
        material.DisableKeyword("_DISABLE_SSR_TRANSPARENT");
        material.DisableKeyword("_DOUBLESIDED_ON");
        material.DisableKeyword("_ENABLE_FOG_ON_TRANSPARENT");
        material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");

        SetFloatIfPresent(material, "_AlphaDstBlend", 0f);
        SetFloatIfPresent(material, "_AlphaSrcBlend", 1f);
        SetFloatIfPresent(material, "_BlendMode", 0f);
        SetFloatIfPresent(material, "_CullMode", 2f);
        SetFloatIfPresent(material, "_CullModeForward", 2f);
        SetFloatIfPresent(material, "_DoubleSidedEnable", 0f);
        SetFloatIfPresent(material, "_DstBlend", 0f);
        SetFloatIfPresent(material, "_DstBlend2", 0f);
        SetFloatIfPresent(material, "_EnableBlendModePreserveSpecularLighting", 0f);
        SetFloatIfPresent(material, "_EnableFogOnTransparent", 0f);
        SetFloatIfPresent(material, "_OpaqueCullMode", 2f);
        SetFloatIfPresent(material, "_RenderQueueType", 1f);
        SetFloatIfPresent(material, "_SrcBlend", 1f);
        SetFloatIfPresent(material, "_SrcBlend2", 1f);
        SetFloatIfPresent(material, "_SurfaceType", 0f);
        SetFloatIfPresent(material, "_TransparentCullMode", 2f);
        SetFloatIfPresent(material, "_TransparentDepthPostpassEnable", 0f);
        SetFloatIfPresent(material, "_TransparentDepthPrepassEnable", 0f);
        SetFloatIfPresent(material, "_TransparentZWrite", 0f);
        SetFloatIfPresent(material, "_ZWrite", 1f);

        material.SetShaderPassEnabled("TransparentBackface", false);
        material.SetShaderPassEnabled("TransparentDepthPrepass", false);
        material.SetShaderPassEnabled("TransparentDepthPostpass", false);
        material.SetShaderPassEnabled("RayTracingPrepass", false);
        material.SetShaderPassEnabled("MOTIONVECTORS", false);

        EditorUtility.SetDirty(material);
    }

    private static void SetFloatIfPresent(Material material, string propertyName, float value)
    {
        if (material.HasProperty(propertyName))
            material.SetFloat(propertyName, value);
    }
}
