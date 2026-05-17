#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class FabHdrpMaterialRepair
{
    private static readonly string[] TargetRoots =
    {
        "Assets/0 - UnityPackages/Fab/Dungeon_Environment",
        "Assets/0 - UnityPackages/Fab/MedievalRuins_ScansFactory",
        "Assets/ScansFactory/MedievalRuins"
    };

    private static readonly HashSet<string> BrokenMedievalShaderGraphs = new HashSet<string>(StringComparer.Ordinal)
    {
        "Shader Graphs/S_Base",
        "Shader Graphs/S_BaseMasked",
        "Shader Graphs/S_BaseMaskedTranslucent",
        "Shader Graphs/S_Base BlendingWithTerrain"
    };

    [MenuItem("Tools/Rendering/Repair Fab HDRP Materials")]
    public static void RepairFromMenu()
    {
        RepairMaterials(showDialog: true);
    }

    public static void RunFromCommandLine()
    {
        RepairMaterials(showDialog: false);
    }

    private static void RepairMaterials(bool showDialog)
    {
        Shader litShader = FindRequiredShader("HDRP/Lit", "HDRenderPipeline/Lit");
        Shader unlitShader = Shader.Find("HDRP/Unlit") ?? Shader.Find("HDRenderPipeline/Unlit") ?? litShader;
        Shader terrainShader = Shader.Find("HDRP/TerrainLit") ?? Shader.Find("HDRenderPipeline/TerrainLit") ?? litShader;

        string[] roots = ExistingRoots();
        if (roots.Length == 0)
        {
            Debug.LogWarning("Fab HDRP repair skipped: no target Fab folders were found.");
            return;
        }

        int scanned = 0;
        int repaired = 0;
        int skipped = 0;

        AssetDatabase.StartAssetEditing();
        try
        {
            string[] materialGuids = AssetDatabase.FindAssets("t:Material", roots);
            foreach (string guid in materialGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    continue;
                }

                scanned++;

                string shaderName = material.shader != null ? material.shader.name : string.Empty;
                if (!ShouldRepair(path, shaderName))
                {
                    skipped++;
                    continue;
                }

                MaterialSnapshot snapshot = MaterialSnapshot.Capture(material, path, shaderName);
                Shader targetShader = ChooseTargetShader(snapshot, litShader, unlitShader, terrainShader);

                material.shader = targetShader;
                snapshot.ApplyTo(material);
                EditorUtility.SetDirty(material);
                repaired++;

                Debug.Log($"Fab HDRP repair: {path} moved from '{shaderName}' to '{targetShader.name}'.");
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        string summary = $"Fab HDRP repair complete. Scanned {scanned} materials, repaired {repaired}, skipped {skipped}.";
        Debug.Log(summary);

        if (showDialog)
        {
            EditorUtility.DisplayDialog("Fab HDRP Repair", summary, "OK");
        }
    }

    private static Shader FindRequiredShader(params string[] names)
    {
        foreach (string name in names)
        {
            Shader shader = Shader.Find(name);
            if (shader != null)
            {
                return shader;
            }
        }

        throw new InvalidOperationException("Could not find the HDRP Lit shader. Is the HDRP package installed?");
    }

    private static string[] ExistingRoots()
    {
        List<string> roots = new List<string>();
        foreach (string root in TargetRoots)
        {
            if (AssetDatabase.IsValidFolder(root))
            {
                roots.Add(root);
            }
        }

        return roots.ToArray();
    }

    private static bool ShouldRepair(string path, string shaderName)
    {
        if (string.IsNullOrEmpty(shaderName) || shaderName == "Hidden/InternalErrorShader")
        {
            return true;
        }

        if (shaderName.StartsWith("Unreal/", StringComparison.Ordinal))
        {
            return true;
        }

        if (shaderName == "Standard" ||
            shaderName.StartsWith("Legacy Shaders/", StringComparison.Ordinal) ||
            shaderName.StartsWith("Particles/", StringComparison.Ordinal))
        {
            return true;
        }

        if (shaderName.IndexOf("Universal Render Pipeline", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        if (path.IndexOf("MedievalRuins", StringComparison.OrdinalIgnoreCase) >= 0 &&
            BrokenMedievalShaderGraphs.Contains(shaderName))
        {
            return true;
        }

        return false;
    }

    private static Shader ChooseTargetShader(MaterialSnapshot snapshot, Shader litShader, Shader unlitShader, Shader terrainShader)
    {
        if (snapshot.IsTerrain)
        {
            return terrainShader;
        }

        if (snapshot.PreferUnlit)
        {
            return unlitShader;
        }

        return litShader;
    }

    private sealed class TextureSlot
    {
        public Texture Texture;
        public Vector2 Scale = Vector2.one;
        public Vector2 Offset = Vector2.zero;

        public bool HasTexture => Texture != null;

        public static TextureSlot Capture(Material material, params string[] propertyNames)
        {
            foreach (string propertyName in propertyNames)
            {
                if (!material.HasProperty(propertyName))
                {
                    continue;
                }

                Texture texture = material.GetTexture(propertyName);
                if (texture == null)
                {
                    continue;
                }

                return new TextureSlot
                {
                    Texture = texture,
                    Scale = material.GetTextureScale(propertyName),
                    Offset = material.GetTextureOffset(propertyName)
                };
            }

            return new TextureSlot();
        }

        public void Apply(Material material, params string[] propertyNames)
        {
            if (!HasTexture)
            {
                return;
            }

            foreach (string propertyName in propertyNames)
            {
                if (!material.HasProperty(propertyName))
                {
                    continue;
                }

                material.SetTexture(propertyName, Texture);
                material.SetTextureScale(propertyName, Scale);
                material.SetTextureOffset(propertyName, Offset);
            }
        }
    }

    private sealed class MaterialSnapshot
    {
        public TextureSlot BaseMap;
        public TextureSlot NormalMap;
        public TextureSlot MaskMap;
        public TextureSlot EmissionMap;
        public Color BaseColor = Color.white;
        public Color EmissionColor = Color.black;
        public float Metallic;
        public float Smoothness = 0.5f;
        public float Cutoff = 0.5f;
        public bool AlphaClip;
        public bool Transparent;
        public bool DoubleSided;
        public bool PreferUnlit;
        public bool IsTerrain;

        public static MaterialSnapshot Capture(Material material, string path, string shaderName)
        {
            MaterialSnapshot snapshot = new MaterialSnapshot
            {
                BaseMap = TextureSlot.Capture(material, "_BaseColorMap", "_MainTex", "_BaseMap", "Material_Texture2D_1", "Material_Texture2D_0", "_UnlitColorMap"),
                NormalMap = TextureSlot.Capture(material, "_NormalMap", "_BumpMap", "Material_Texture2D_0"),
                MaskMap = TextureSlot.Capture(material, "_MaskMap", "_MetallicGlossMap", "Material_Texture2D_2", "_OcclusionMap"),
                EmissionMap = TextureSlot.Capture(material, "_EmissiveColorMap", "_EmissionMap", "_UnlitColorMap"),
                BaseColor = CaptureColor(material, Color.white, "_BaseColor", "_Color", "_UnlitColor"),
                EmissionColor = CaptureColor(material, Color.black, "_EmissiveColor", "_EmissionColor"),
                Metallic = CaptureFloat(material, 0f, "_Metallic"),
                Smoothness = CaptureFloat(material, CaptureFloat(material, 0.5f, "_Glossiness"), "_Smoothness"),
                Cutoff = CaptureFloat(material, 0.5f, "_AlphaCutoff", "_Cutoff"),
                AlphaClip = material.IsKeywordEnabled("_ALPHATEST_ON") || CaptureFloat(material, 0f, "_AlphaCutoffEnable") > 0.5f,
                Transparent = material.renderQueue >= (int)RenderQueue.Transparent ||
                              material.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT") ||
                              CaptureFloat(material, 0f, "_SurfaceType") > 0.5f,
                DoubleSided = CaptureFloat(material, 0f, "_DoubleSidedEnable") > 0.5f ||
                              CaptureFloat(material, 2f, "_CullMode") == 0f ||
                              NameSuggestsDoubleSided(path),
                PreferUnlit = NameSuggestsUnlit(path, shaderName),
                IsTerrain = path.IndexOf("/TerrainData/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            shaderName.IndexOf("Terrain", StringComparison.OrdinalIgnoreCase) >= 0
            };

            if (NameSuggestsAlphaClip(path) || shaderName.IndexOf("Masked", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                snapshot.AlphaClip = true;
            }

            return snapshot;
        }

        public void ApplyTo(Material material)
        {
            BaseMap.Apply(material, "_BaseColorMap", "_MainTex", "_BaseMap", "_UnlitColorMap");
            NormalMap.Apply(material, "_NormalMap", "_BumpMap");
            MaskMap.Apply(material, "_MaskMap", "_MetallicGlossMap", "_OcclusionMap");
            EmissionMap.Apply(material, "_EmissiveColorMap", "_EmissionMap", "_UnlitColorMap");

            SetColorIfPresent(material, BaseColor, "_BaseColor", "_Color", "_UnlitColor");
            SetColorIfPresent(material, EmissionColor, "_EmissiveColor", "_EmissionColor");
            SetFloatIfPresent(material, Metallic, "_Metallic");
            SetFloatIfPresent(material, Smoothness, "_Smoothness", "_Glossiness");
            SetFloatIfPresent(material, Cutoff, "_AlphaCutoff", "_Cutoff");

            ConfigureSurface(material);
            ConfigureKeywords(material);
        }

        private void ConfigureSurface(Material material)
        {
            SetFloatIfPresent(material, Transparent ? 1f : 0f, "_SurfaceType");
            SetFloatIfPresent(material, AlphaClip ? 1f : 0f, "_AlphaCutoffEnable");
            SetFloatIfPresent(material, DoubleSided ? 1f : 0f, "_DoubleSidedEnable");
            SetFloatIfPresent(material, DoubleSided ? 0f : 2f, "_CullMode", "_CullModeForward", "_Cull");

            if (Transparent)
            {
                SetFloatIfPresent(material, 0f, "_BlendMode");
                SetFloatIfPresent(material, (float)BlendMode.SrcAlpha, "_SrcBlend", "_AlphaSrcBlend");
                SetFloatIfPresent(material, (float)BlendMode.OneMinusSrcAlpha, "_DstBlend", "_AlphaDstBlend");
                SetFloatIfPresent(material, 0f, "_ZWrite");
                material.SetOverrideTag("RenderType", "Transparent");
                material.renderQueue = Math.Max(material.renderQueue, (int)RenderQueue.Transparent);
            }
            else if (AlphaClip)
            {
                SetFloatIfPresent(material, 1f, "_ZWrite");
                material.SetOverrideTag("RenderType", "TransparentCutout");
                material.renderQueue = (int)RenderQueue.AlphaTest;
            }
            else
            {
                SetFloatIfPresent(material, 1f, "_ZWrite");
                material.SetOverrideTag("RenderType", string.Empty);
                material.renderQueue = (int)RenderQueue.Geometry;
            }
        }

        private void ConfigureKeywords(Material material)
        {
            SetKeyword(material, "_NORMALMAP", NormalMap.HasTexture);
            SetKeyword(material, "_NORMALMAP_TANGENT_SPACE", NormalMap.HasTexture);
            SetKeyword(material, "_MASKMAP", MaskMap.HasTexture);
            SetKeyword(material, "_ALPHATEST_ON", AlphaClip);
            SetKeyword(material, "_SURFACE_TYPE_TRANSPARENT", Transparent);
            SetKeyword(material, "_DOUBLESIDED_ON", DoubleSided);
            SetKeyword(material, "_EMISSION", EmissionMap.HasTexture || EmissionColor.maxColorComponent > 0f);
            SetKeyword(material, "_EMISSIVE_COLOR_MAP", EmissionMap.HasTexture);
        }
    }

    private static Color CaptureColor(Material material, Color fallback, params string[] propertyNames)
    {
        foreach (string propertyName in propertyNames)
        {
            if (material.HasProperty(propertyName))
            {
                return material.GetColor(propertyName);
            }
        }

        return fallback;
    }

    private static float CaptureFloat(Material material, float fallback, params string[] propertyNames)
    {
        foreach (string propertyName in propertyNames)
        {
            if (material.HasProperty(propertyName))
            {
                return material.GetFloat(propertyName);
            }
        }

        return fallback;
    }

    private static void SetColorIfPresent(Material material, Color value, params string[] propertyNames)
    {
        foreach (string propertyName in propertyNames)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetColor(propertyName, value);
            }
        }
    }

    private static void SetFloatIfPresent(Material material, float value, params string[] propertyNames)
    {
        foreach (string propertyName in propertyNames)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetFloat(propertyName, value);
            }
        }
    }

    private static void SetKeyword(Material material, string keyword, bool enabled)
    {
        if (enabled)
        {
            material.EnableKeyword(keyword);
        }
        else
        {
            material.DisableKeyword(keyword);
        }
    }

    private static bool NameSuggestsDoubleSided(string path)
    {
        return ContainsAny(path, "Cobweb", "SpiderWeb", "Branch", "Leaf", "Grass", "Fern", "Plant", "Burdock", "Nettle");
    }

    private static bool NameSuggestsAlphaClip(string path)
    {
        return ContainsAny(path, "Cobweb", "SpiderWeb", "Branch", "Leaf", "Grass", "Fern", "Plant", "Burdock", "Nettle");
    }

    private static bool NameSuggestsUnlit(string path, string shaderName)
    {
        return ContainsAny(path, "Particle", "Fire", "Smoke", "LightRay", "Sky") ||
               shaderName.IndexOf("Unlit", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool ContainsAny(string value, params string[] fragments)
    {
        foreach (string fragment in fragments)
        {
            if (value.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }
}
#endif
