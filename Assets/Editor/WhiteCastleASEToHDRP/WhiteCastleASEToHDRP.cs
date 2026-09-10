// WhiteCastleASEToHDRP.cs
// Place this file in an "Editor" folder, for example:
// Assets/Editor/WhiteCastleASEToHDRP.cs
//
// Usage:
// 1) Select the WhiteCastle root folder in the Project window.
// 2) Tools > White Castle > Convert selected folder ASE materials to HDRP
//
// The converter:
// - only touches Materials using ASE/ASE_Standart* shaders
// - snapshots all source values before changing shaders (important for Material Variants)
// - converts them to HDRP/Lit
// - reassigns Albedo, Normal, Height and Emission when present
// - creates an HDRP Mask Map (R=Metallic, G=AO=1, B=DetailMask=1, A=Smoothness)
// - preserves the Albedo tiling/offset
//
// IMPORTANT: Commit / backup your project before running it.

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class WhiteCastleASEToHDRP
{
    private const string MenuPath = "Tools/White Castle/Convert selected folder ASE materials to HDRP";
    private const string HdrpShaderName = "HDRP/Lit";

    private sealed class Snapshot
    {
        public Material mat;
        public string path;

        public Texture albedo;
        public Texture normal;
        public Texture smoothnessMap;
        public Texture metallicMap;
        public Texture heightMap;
        public Texture emissionMap;

        public Color albedoColor = Color.white;
        public Color emissionColor = Color.black;

        public float metallic = 0f;
        public float smoothness = 0.5f;
        public float normalScale = 1f;

        public Vector2 tiling = Vector2.one;
        public Vector2 offset = Vector2.zero;

        public bool smoothFromMap = false;
        public bool emissionEnabled = false;

        public bool hasParent;
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateConvert()
    {
        return Selection.activeObject != null &&
               AssetDatabase.Contains(Selection.activeObject);
    }

    [MenuItem(MenuPath)]
    private static void ConvertSelectedFolder()
    {
        string root = GetSelectedFolderPath();
        if (string.IsNullOrEmpty(root))
        {
            EditorUtility.DisplayDialog(
                "White Castle → HDRP",
                "Select the WhiteCastle folder (or a subfolder) in the Project window first.",
                "OK");
            return;
        }

        Shader hdrp = Shader.Find(HdrpShaderName);
        if (hdrp == null)
        {
            EditorUtility.DisplayDialog(
                "HDRP/Lit not found",
                "Shader.Find(\"HDRP/Lit\") returned null.\n\n" +
                "Make sure HDRP is installed and assigned in Project Settings > Graphics / Quality.",
                "OK");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Material", new[] { root });
        var materials = guids
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(p => new { path = p, mat = AssetDatabase.LoadAssetAtPath<Material>(p) })
            .Where(x => x.mat != null)
            .Where(x => IsASEStandard(x.mat.shader))
            .ToList();

        if (materials.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "White Castle → HDRP",
                $"No material using ASE/ASE_Standart* was found under:\n{root}",
                "OK");
            return;
        }

        bool proceed = EditorUtility.DisplayDialog(
            "Convert White Castle materials",
            $"Found {materials.Count} ASE material(s) under:\n{root}\n\n" +
            "They will be modified in place and HDRP Mask Maps will be generated.\n\n" +
            "Commit / backup first. Continue?",
            "Convert",
            "Cancel");

        if (!proceed)
            return;

        // Snapshot EVERYTHING before touching any shader.
        // This is important because Material Variants may inherit their shader from a parent.
        var snapshots = materials
            .Select(x => Capture(x.mat, x.path))
            .ToList();

        // Convert parent/base materials first, then variants.
        snapshots = snapshots
            .OrderBy(s => s.hasParent ? 1 : 0)
            .ThenBy(s => s.path)
            .ToList();

        int converted = 0;
        var errors = new List<string>();

        try
        {
            AssetDatabase.StartAssetEditing();

            for (int i = 0; i < snapshots.Count; i++)
            {
                Snapshot s = snapshots[i];
                EditorUtility.DisplayProgressBar(
                    "White Castle → HDRP",
                    s.path,
                    (float)i / Math.Max(1, snapshots.Count));

                try
                {
                    ConvertOne(s, hdrp);
                    converted++;
                }
                catch (Exception ex)
                {
                    errors.Add($"{s.path}\n{ex.Message}");
                    Debug.LogException(ex, s.mat);
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        string message =
            $"Converted {converted}/{snapshots.Count} material(s) to HDRP/Lit.";

        if (errors.Count > 0)
        {
            message += $"\n\n{errors.Count} error(s). Check the Console.";
            Debug.LogError("White Castle → HDRP conversion errors:\n\n" +
                           string.Join("\n\n", errors));
        }

        EditorUtility.DisplayDialog("White Castle → HDRP", message, "OK");
    }

    private static Snapshot Capture(Material m, string path)
    {
        var s = new Snapshot
        {
            mat = m,
            path = path,
            hasParent = HasMaterialVariantParent(m),

            albedo = GetTexture(m,
                "_Albedo", "_AlbedoMap", "_MainTex", "_BaseMap", "_BaseColorMap"),

            normal = GetTexture(m,
                "_NormalMap", "_BumpMap", "_Normal"),

            smoothnessMap = GetTexture(m,
                "_SmoothnessMap", "_GlossMap", "_GlossinessMap", "_RoughnessMap"),

            metallicMap = GetTexture(m,
                "_MetallicMap", "_MetallicGlossMap", "_MetalMap"),

            heightMap = GetTexture(m,
                "_HeightMap", "_ParallaxMap", "_DisplacementMap"),

            emissionMap = GetTexture(m,
                "_EmissionMap", "_EmissiveColorMap"),

            albedoColor = GetColor(m, Color.white,
                "_AlbedoColor", "_Color", "_BaseColor"),

            emissionColor = GetColor(m, Color.black,
                "_EmissionColor", "_EmissiveColor"),

            metallic = GetFloat(m, 0f,
                "_Metallic", "_MetallicValue"),

            smoothness = GetFloat(m, 0.5f,
                "_Smoothness", "_Glossiness"),

            normalScale = GetFloat(m, 1f,
                "_NormalMapDepth", "_NormalScale", "_BumpScale"),

            smoothFromMap = GetBool(m,
                "_SmoothFromMapSwitch", "_SmoothFromMap"),

            emissionEnabled = GetBool(m,
                "_EmissionSwitch", "_EmissionEnabled")
        };

        // Prefer transform from the source Albedo property.
        string albedoProp = FirstExistingProperty(m,
            "_Albedo", "_AlbedoMap", "_MainTex", "_BaseMap", "_BaseColorMap");

        if (!string.IsNullOrEmpty(albedoProp))
        {
            s.tiling = m.GetTextureScale(albedoProp);
            s.offset = m.GetTextureOffset(albedoProp);
        }

        return s;
    }

    private static void ConvertOne(Snapshot s, Shader hdrp)
    {
        Material m = s.mat;

        // For a variant, converting its parent first can already change the effective shader.
        // If it is still ASE, try to assign HDRP/Lit directly.
        if (m.shader == null || IsASEStandard(m.shader))
            m.shader = hdrp;

        // If this is a Material Variant and the shader is inherited,
        // the parent conversion should have made it HDRP/Lit.
        if (m.shader == null || m.shader.name != HdrpShaderName)
        {
            throw new InvalidOperationException(
                $"Material shader is '{(m.shader ? m.shader.name : "null")}', expected '{HdrpShaderName}'. " +
                "If this is a Material Variant, convert its parent material too.");
        }

        // Base Color / Albedo
        if (m.HasProperty("_BaseColor"))
            m.SetColor("_BaseColor", s.albedoColor);

        if (s.albedo != null && m.HasProperty("_BaseColorMap"))
        {
            m.SetTexture("_BaseColorMap", s.albedo);
            m.SetTextureScale("_BaseColorMap", s.tiling);
            m.SetTextureOffset("_BaseColorMap", s.offset);
        }

        // Normal
        if (s.normal != null && m.HasProperty("_NormalMap"))
        {
            m.SetTexture("_NormalMap", s.normal);
            m.SetTextureScale("_NormalMap", s.tiling);
            m.SetTextureOffset("_NormalMap", s.offset);

            if (m.HasProperty("_NormalScale"))
                m.SetFloat("_NormalScale", s.normalScale);

            m.EnableKeyword("_NORMALMAP_TANGENT_SPACE");
        }

        // Height
        if (s.heightMap != null && m.HasProperty("_HeightMap"))
        {
            m.SetTexture("_HeightMap", s.heightMap);
            m.SetTextureScale("_HeightMap", s.tiling);
            m.SetTextureOffset("_HeightMap", s.offset);
        }

        // HDRP Mask Map:
        // R = Metallic
        // G = Ambient Occlusion
        // B = Detail Mask
        // A = Smoothness
        //
        // This pack exposes a MetallicMap and SmoothnessMap separately,
        // so create a proper HDRP packed map.
        Texture2D mask = CreateMaskMap(s);
        if (mask != null && m.HasProperty("_MaskMap"))
        {
            m.SetTexture("_MaskMap", mask);
            m.SetTextureScale("_MaskMap", s.tiling);
            m.SetTextureOffset("_MaskMap", s.offset);
            m.EnableKeyword("_MASKMAP");
        }

        // Keep scalar values too; they are useful when one channel/map is absent.
        if (m.HasProperty("_Metallic"))
            m.SetFloat("_Metallic", Mathf.Clamp01(s.metallic));

        if (m.HasProperty("_Smoothness"))
            m.SetFloat("_Smoothness", Mathf.Clamp01(s.smoothness));

        // Emission
        if (s.emissionMap != null && m.HasProperty("_EmissiveColorMap"))
        {
            m.SetTexture("_EmissiveColorMap", s.emissionMap);
            m.SetTextureScale("_EmissiveColorMap", s.tiling);
            m.SetTextureOffset("_EmissiveColorMap", s.offset);
            m.EnableKeyword("_EMISSIVE_COLOR_MAP");
        }

        if (m.HasProperty("_EmissiveColor"))
        {
            // If there is an emission map but the old color is black,
            // use white so the texture is not multiplied down to black.
            Color c = s.emissionColor;
            if (s.emissionMap != null &&
                c.maxColorComponent <= 0.0001f)
                c = Color.white;

            m.SetColor("_EmissiveColor", c);
        }

        // Common HDRP defaults for an opaque Lit material.
        SetFloatIfExists(m, "_SurfaceType", 0f);       // Opaque
        SetFloatIfExists(m, "_BlendMode", 0f);
        SetFloatIfExists(m, "_AlphaCutoffEnable", 0f);
        SetFloatIfExists(m, "_DoubleSidedEnable", 0f);

        EditorUtility.SetDirty(m);
    }

    private static Texture2D CreateMaskMap(Snapshot s)
    {
        // Generate a map if at least Metallic or Smoothness comes from a texture.
        if (s.metallicMap == null && s.smoothnessMap == null)
            return null;

        int width = 4;
        int height = 4;

        if (s.metallicMap != null)
        {
            width = Mathf.Max(width, s.metallicMap.width);
            height = Mathf.Max(height, s.metallicMap.height);
        }

        if (s.smoothnessMap != null)
        {
            width = Mathf.Max(width, s.smoothnessMap.width);
            height = Mathf.Max(height, s.smoothnessMap.height);
        }

        Color[] metallicPixels = s.metallicMap != null
            ? ReadTextureResampled(s.metallicMap, width, height)
            : null;

        Color[] smoothPixels = s.smoothnessMap != null
            ? ReadTextureResampled(s.smoothnessMap, width, height)
            : null;

        var pixels = new Color32[width * height];

        for (int i = 0; i < pixels.Length; i++)
        {
            float metallic = metallicPixels != null
                ? metallicPixels[i].r
                : Mathf.Clamp01(s.metallic);

            float smoothness = smoothPixels != null
                ? smoothPixels[i].r
                : Mathf.Clamp01(s.smoothness);

            // If the source property is actually a Roughness map, invert it.
            // The White Castle shader shown in the Inspector calls it SmoothnessMap,
            // therefore no inversion is done for that known slot.
            //
            // AO and Detail Mask are not exposed in the shown ASE shader,
            // so use neutral values = 1.
            pixels[i] = new Color(
                Mathf.Clamp01(metallic),
                1f,
                1f,
                Mathf.Clamp01(smoothness));
        }

        Texture2D outTex = new Texture2D(
            width, height, TextureFormat.RGBA32, true, true);

        outTex.name = Path.GetFileNameWithoutExtension(s.path) + "_MaskMap";
        outTex.SetPixels32(pixels);
        outTex.Apply(true, false);

        string matDir = Path.GetDirectoryName(s.path)?.Replace("\\", "/") ?? "Assets";
        string maskDir = matDir + "/_HDRPMaskMaps";

        EnsureAssetFolder(maskDir);

        string pngPath = maskDir + "/" + outTex.name + ".png";
        File.WriteAllBytes(pngPath, outTex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(outTex);

        AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceSynchronousImport);

        TextureImporter importer = AssetImporter.GetAtPath(pngPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
    }

    private static Color[] ReadTextureResampled(Texture source, int width, int height)
    {
        RenderTexture previous = RenderTexture.active;
        RenderTexture rt = RenderTexture.GetTemporary(
            width,
            height,
            0,
            RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.Linear);

        try
        {
            Graphics.Blit(source, rt);
            RenderTexture.active = rt;

            Texture2D readable = new Texture2D(
                width, height, TextureFormat.RGBA32, false, true);

            readable.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
            readable.Apply(false, false);

            Color[] pixels = readable.GetPixels();
            UnityEngine.Object.DestroyImmediate(readable);
            return pixels;
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
        }
    }

    private static bool IsASEStandard(Shader shader)
    {
        if (shader == null)
            return false;

        return shader.name.Equals("ASE/ASE_Standart", StringComparison.OrdinalIgnoreCase) ||
               shader.name.StartsWith("ASE/ASE_Standart", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasMaterialVariantParent(Material material)
    {
        // Serialized lookup avoids depending on a particular Unity version's Material.parent API.
        try
        {
            var so = new SerializedObject(material);
            SerializedProperty parent = so.FindProperty("m_Parent");
            return parent != null && parent.objectReferenceValue != null;
        }
        catch
        {
            return false;
        }
    }

    private static string GetSelectedFolderPath()
    {
        UnityEngine.Object selected = Selection.activeObject;
        if (selected == null)
            return null;

        string path = AssetDatabase.GetAssetPath(selected);
        if (string.IsNullOrEmpty(path))
            return null;

        if (AssetDatabase.IsValidFolder(path))
            return path;

        return Path.GetDirectoryName(path)?.Replace("\\", "/");
    }

    private static void EnsureAssetFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
            return;

        string parent = Path.GetDirectoryName(folder)?.Replace("\\", "/");
        string name = Path.GetFileName(folder);

        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureAssetFolder(parent);

        AssetDatabase.CreateFolder(parent, name);
    }

    private static string FirstExistingProperty(Material m, params string[] names)
    {
        foreach (string n in names)
            if (m.HasProperty(n))
                return n;
        return null;
    }

    private static Texture GetTexture(Material m, params string[] names)
    {
        string p = FirstExistingProperty(m, names);
        return p != null ? m.GetTexture(p) : null;
    }

    private static float GetFloat(Material m, float fallback, params string[] names)
    {
        string p = FirstExistingProperty(m, names);
        return p != null ? m.GetFloat(p) : fallback;
    }

    private static bool GetBool(Material m, params string[] names)
    {
        string p = FirstExistingProperty(m, names);
        return p != null && m.GetFloat(p) > 0.5f;
    }

    private static Color GetColor(Material m, Color fallback, params string[] names)
    {
        string p = FirstExistingProperty(m, names);
        return p != null ? m.GetColor(p) : fallback;
    }

    private static void SetFloatIfExists(Material m, string property, float value)
    {
        if (m.HasProperty(property))
            m.SetFloat(property, value);
    }
}
#endif
