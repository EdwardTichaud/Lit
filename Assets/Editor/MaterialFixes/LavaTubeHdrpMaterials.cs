using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Scoped converter: preserves material GUIDs and therefore all mesh/prefab references.
[InitializeOnLoad]
public static class LavaTubeHdrpMaterials
{
    private const string Root = "Assets/BillemotdonggulLavaTubePack";
    private const string Output = "Library/LavaTubeMaterials";
    static LavaTubeHdrpMaterials() => EditorApplication.update += Poll;
    private static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Output + "/Convert.request")) return;
        File.Delete(Output + "/Convert.request");
        try { Convert(); } catch (Exception error) { File.WriteAllText(Output + "/Result.txt", "FAIL: " + error); }
    }

    [MenuItem("Lit/Materials/Convert Lava Tube Pack To HDRP")]
    public static void Convert()
    {
        Directory.CreateDirectory(Output);
        var shader = Shader.Find("HDRP/Lit");
        if (shader == null || !shader.isSupported || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("HDRP/Lit unavailable or unsupported.");
        var paths = AssetDatabase.FindAssets("t:Material", new[] { Root }).Select(AssetDatabase.GUIDToAssetPath).ToArray();
        int converted = 0, missingColor = 0;
        foreach (string path in paths)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var original = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Material/" + Path.GetFileName(path)) ?? material;
            var color = TextureFor(original, "Material_Texture2D_1") ?? TextureFor(material, "_BaseColorMap") ?? TextureFor(material, "_MainTex");
            var normal = TextureFor(original, "Material_Texture2D_0") ?? TextureFor(material, "_NormalMap") ?? TextureFor(material, "_BumpMap");
            var occlusion = TextureFor(original, "Material_Texture2D_2") ?? TextureFor(material, "_MaskMap") ?? TextureFor(material, "_OcclusionMap");
            // The original custom shaders use raw texture slots; HDRP requires a normal-map import.
            ConfigureTexture(normal, true); ConfigureTexture(occlusion, false);
            material.shader = shader;
            material.SetTexture("_BaseColorMap", color); material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_NormalMap", normal); material.SetFloat("_NormalMapSpace", 0);
            material.SetTexture("_MaskMap", occlusion);
            material.SetFloat("_Metallic", 0); material.SetFloat("_MetallicRemapMin", 0); material.SetFloat("_MetallicRemapMax", 0);
            material.SetFloat("_Smoothness", .15f); material.SetFloat("_SmoothnessRemapMin", .15f); material.SetFloat("_SmoothnessRemapMax", .15f);
            material.SetFloat("_AORemapMin", 0); material.SetFloat("_AORemapMax", 1);
            material.SetFloat("_SurfaceType", 0); material.SetFloat("_AlphaCutoffEnable", 0);
            material.SetFloat("_DoubleSidedEnable", 1); material.SetFloat("_DoubleSidedNormalMode", 1);
            material.SetColor("_EmissiveColor", Color.black); material.SetTexture("_EmissiveColorMap", null);
            material.renderQueue = -1;
            UnityEditor.Rendering.HighDefinition.HDShaderUtils.ResetMaterialKeywords(material);
            EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material);
            converted++; if (color == null) missingColor++;
        }
        File.WriteAllText(Output + "/Result.txt", $"PASS: {converted} materials converted in place to HDRP/Lit. Missing base maps: {missingColor}. Shader supported and error-free. Prefab/material GUIDs preserved.\n");
    }

    private static Texture TextureFor(Material material, string property)
    {
        var entries = new SerializedObject(material).FindProperty("m_SavedProperties.m_TexEnvs");
        for (int i = 0; i < entries.arraySize; i++)
        {
            var entry = entries.GetArrayElementAtIndex(i);
            if (entry.FindPropertyRelative("first").stringValue == property)
                return entry.FindPropertyRelative("second.m_Texture").objectReferenceValue as Texture;
        }
        return null;
    }

    private static void ConfigureTexture(Texture texture, bool normal)
    {
        if (texture == null || !(AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture)) is TextureImporter importer)) return;
        if (normal && importer.textureType != TextureImporterType.NormalMap)
        { importer.textureType = TextureImporterType.NormalMap; importer.SaveAndReimport(); }
        else if (!normal && importer.sRGBTexture)
        { importer.sRGBTexture = false; importer.SaveAndReimport(); }
    }
}
