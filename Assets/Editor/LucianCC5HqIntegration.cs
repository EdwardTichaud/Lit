#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class LucianCC5HqIntegration
{
    private const string CharacterRoot = "Assets/Lucian_CC5_Embed";
    private const string SourcePrefabPath = CharacterRoot + "/Prefabs/Lucian_CC5_Character_Model.prefab";
    private const string HqPrefabPath = CharacterRoot + "/Prefabs/Lucian_CC5_Unity_HQ.prefab";
    private const string SourceMaterialsPath = CharacterRoot + "/Materials/Lucian_CC5_Character_Model";
    private const string HqMaterialsPath = CharacterRoot + "/Materials/Lucian_Unity_HQ";
    private const string RenderTestScenePath = "Assets/Scenes/CharacterTests/Lucian_RenderTest.unity";
    private const string ReportPath = CharacterRoot + "/Lucian_CC5_Unity_HQ_Report.txt";
    private const int CharacterTextureMaxSize = 4096;

    private static readonly string[] TextureRoots =
    {
        CharacterRoot + "/Lucian_CC5_Character_Model.fbm",
        CharacterRoot + "/textures"
    };

    [MenuItem("Tools/Lit/Characters/Lucian/Build CC5 HQ Integration")]
    public static void BuildFromMenu()
    {
        BuildIntegration(showDialog: true);
    }

    public static void RunFromCommandLine()
    {
        BuildIntegration(showDialog: false);
    }

    [MenuItem("Tools/Lit/Characters/Lucian/Validate CC5 HQ Integration")]
    public static void ValidateFromMenu()
    {
        ValidationReport validation = ValidateIntegration(throwOnError: false);
        EditorUtility.DisplayDialog("Lucian CC5 HQ Validation", validation.ToSummary(), "OK");
    }

    public static void ValidateFromCommandLine()
    {
        ValidateIntegration(throwOnError: true);
    }

    private static void BuildIntegration(bool showDialog)
    {
        string pipeline = DetectRenderPipeline();
        if (!pipeline.Contains("HDRP", StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogWarning($"Lucian CC5 HQ: current pipeline is '{pipeline}'. The source materials are HDRP-oriented.");
        }

        EnsureFolderPath(HqMaterialsPath);
        EnsureFolderPath(Path.GetDirectoryName(RenderTestScenePath).Replace('\\', '/'));

        MaterialBuildReport materialReport = CreateOrUpdateHqMaterials();
        TextureBuildReport textureReport = ConfigureTextureImporters();
        PrefabBuildReport prefabReport = CreateOrUpdateHqPrefab(materialReport.MaterialMapBySourcePath);
        CreateOrUpdateRenderTestScene();
        ValidationReport validation = ValidateIntegration(throwOnError: true);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string report = BuildReport(pipeline, materialReport, textureReport, prefabReport, validation);
        File.WriteAllText(ReportPath, report, Encoding.UTF8);
        AssetDatabase.ImportAsset(ReportPath);

        Debug.Log(report);
        if (showDialog)
        {
            EditorUtility.DisplayDialog("Lucian CC5 HQ Integration", validation.ToSummary(), "OK");
        }
    }

    private static string DetectRenderPipeline()
    {
        RenderPipelineAsset pipelineAsset = GraphicsSettings.currentRenderPipeline;
        if (pipelineAsset == null)
        {
            pipelineAsset = GraphicsSettings.defaultRenderPipeline;
        }

        if (pipelineAsset == null)
        {
            return "Built-in";
        }

        string typeName = pipelineAsset.GetType().FullName ?? pipelineAsset.GetType().Name;
        if (typeName.IndexOf("HighDefinition", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return $"HDRP ({pipelineAsset.name})";
        }

        if (typeName.IndexOf("Universal", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return $"URP ({pipelineAsset.name})";
        }

        return $"{typeName} ({pipelineAsset.name})";
    }

    private static MaterialBuildReport CreateOrUpdateHqMaterials()
    {
        string[] materialGuids = AssetDatabase.FindAssets("t:Material", new[] { SourceMaterialsPath });
        MaterialBuildReport report = new MaterialBuildReport();

        foreach (string guid in materialGuids)
        {
            string sourcePath = AssetDatabase.GUIDToAssetPath(guid);
            Material source = AssetDatabase.LoadAssetAtPath<Material>(sourcePath);
            if (source == null)
            {
                continue;
            }

            string targetPath = $"{HqMaterialsPath}/{source.name}_Unity_HQ.mat";
            bool created = false;
            if (AssetDatabase.LoadAssetAtPath<Material>(targetPath) == null)
            {
                if (!AssetDatabase.CopyAsset(sourcePath, targetPath))
                {
                    Debug.LogWarning($"Lucian CC5 HQ: could not copy material '{sourcePath}' to '{targetPath}'.");
                    continue;
                }

                created = true;
            }

            AssetDatabase.ImportAsset(targetPath);
            Material target = AssetDatabase.LoadAssetAtPath<Material>(targetPath);
            if (target == null)
            {
                Debug.LogWarning($"Lucian CC5 HQ: copied material could not be loaded at '{targetPath}'.");
                continue;
            }

            target.name = Path.GetFileNameWithoutExtension(targetPath);
            if (ConfigureMaterialForHq(target, source))
            {
                report.ShaderFallbacks++;
            }

            EditorUtility.SetDirty(target);

            report.Scanned++;
            if (created)
            {
                report.Created++;
            }
            else
            {
                report.Updated++;
            }

            report.MaterialMapBySourcePath[sourcePath] = target;
        }

        AssetDatabase.SaveAssets();
        return report;
    }

    private static bool ConfigureMaterialForHq(Material material, Material source)
    {
        string materialName = material.name.ToLowerInvariant();
        bool isHairOrAlpha = materialName.Contains("hair") ||
                             materialName.Contains("lash") ||
                             materialName.Contains("eyelash") ||
                             materialName.Contains("female_angled") ||
                             materialName.Contains("transparency");

        bool isSkin = materialName.Contains("ga_skin") || materialName.Contains("_skin_");
        bool isCornea = materialName.Contains("cornea");
        bool isEye = materialName.Contains("eye");
        bool shaderFallbackApplied = false;

        if (HasMissingShader(material))
        {
            shaderFallbackApplied = ApplyHdrpLitFallback(material, source, isHairOrAlpha, isCornea, isEye);
        }

        if (isHairOrAlpha)
        {
            SetFloatIfPresent(material, "_SurfaceType", 1f);
            SetFloatIfPresent(material, "_AlphaCutoffEnable", 1f);
            SetFloatIfPresent(material, "_AlphaCutoff", 0.45f);
            SetFloatIfPresent(material, "_AlphaCutoffPrepass", 0.45f);
            SetFloatIfPresent(material, "_AlphaCutoffPostpass", 0.45f);
            SetFloatIfPresent(material, "_AlphaCutoffShadow", 0.45f);
            SetFloatIfPresent(material, "_UseShadowThreshold", 1f);
            SetFloatIfPresent(material, "_DoubleSidedEnable", 1f);
            SetFloatIfPresent(material, "_TransparentBackfaceEnable", 1f);
            SetFloatIfPresent(material, "_TransparentDepthPrepassEnable", 1f);
            SetFloatIfPresent(material, "_TransparentDepthPostpassEnable", 1f);

            if (material.HasProperty("_AlphaClip") && material.GetFloat("_AlphaClip") <= 0.01f)
            {
                material.SetFloat("_AlphaClip", 0.6f);
            }

            material.renderQueue = Math.Max(material.renderQueue, (int)RenderQueue.Transparent);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_DOUBLESIDED_ON");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }

        if (isSkin)
        {
            SetFloatIfPresent(material, "_DoubleSidedEnable", 1f);
            SetFloatIfPresent(material, "_TransmissionEnable", 1f);
            if (material.HasProperty("_SubsurfaceMask") && material.GetFloat("_SubsurfaceMask") <= 0f)
            {
                material.SetFloat("_SubsurfaceMask", 0.75f);
            }
        }

        if (isCornea)
        {
            SetFloatIfPresent(material, "_CorneaSmoothness", 1f);
            SetFloatIfPresent(material, "_ScleraSmoothness", 0.96f);
            SetFloatIfPresent(material, "_IOR", 1.4f);
        }

        return shaderFallbackApplied;
    }

    private static TextureBuildReport ConfigureTextureImporters()
    {
        TextureBuildReport report = new TextureBuildReport();
        List<string> existingRoots = new List<string>();
        foreach (string root in TextureRoots)
        {
            if (AssetDatabase.IsValidFolder(root))
            {
                existingRoots.Add(root);
            }
        }

        if (existingRoots.Count == 0)
        {
            return report;
        }

        string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", existingRoots.ToArray());
        foreach (string guid in textureGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                continue;
            }

            report.Scanned++;
            bool changed = ConfigureTextureImporter(path, importer, report);
            if (!changed)
            {
                continue;
            }

            report.Changed++;
            if (report.ChangedPaths.Count < 80)
            {
                report.ChangedPaths.Add(path);
            }

            importer.SaveAndReimport();
        }

        return report;
    }

    private static bool ConfigureTextureImporter(string path, TextureImporter importer, TextureBuildReport report)
    {
        string lower = path.ToLowerInvariant();
        bool isNormal = IsNormalTexture(lower);
        bool isData = IsDataTexture(lower);
        bool isHairAlpha = IsHairAlphaTexture(lower, isNormal, isData);
        bool changed = false;

        if (isNormal)
        {
            changed |= SetTextureType(importer, TextureImporterType.NormalMap);
            changed |= SetSrgb(importer, false);
            report.NormalMaps++;
        }
        else
        {
            changed |= SetTextureType(importer, TextureImporterType.Default);
            changed |= SetSrgb(importer, !isData);
            if (isData)
            {
                report.LinearDataMaps++;
            }
        }

        if (isHairAlpha)
        {
            if (importer.alphaSource != TextureImporterAlphaSource.FromInput)
            {
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                changed = true;
            }

            if (!importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                changed = true;
            }

            if (!importer.mipMapsPreserveCoverage)
            {
                importer.mipMapsPreserveCoverage = true;
                changed = true;
            }

            if (!Mathf.Approximately(importer.alphaTestReferenceValue, 0.6f))
            {
                importer.alphaTestReferenceValue = 0.6f;
                changed = true;
            }

            report.HairAlphaTextures++;
        }

        changed |= ConfigurePlatform(importer, "DefaultTexturePlatform", report);
        changed |= ConfigurePlatform(importer, "Standalone", report);
        return changed;
    }

    private static bool IsNormalTexture(string lowerPath)
    {
        string fileName = Path.GetFileNameWithoutExtension(lowerPath);
        return fileName.EndsWith("_normal", StringComparison.Ordinal) ||
               fileName.EndsWith("_n", StringComparison.Ordinal) ||
               fileName.EndsWith("normal", StringComparison.Ordinal) ||
               fileName.EndsWith("scleran", StringComparison.Ordinal) ||
               fileName.EndsWith("irisn", StringComparison.Ordinal);
    }

    private static bool IsDataTexture(string lowerPath)
    {
        string fileName = Path.GetFileNameWithoutExtension(lowerPath);
        return fileName.EndsWith("_hdrp", StringComparison.Ordinal) ||
               fileName.Contains("metallicalpha") ||
               fileName.EndsWith("_ao", StringComparison.Ordinal) ||
               fileName.Contains("gradao") ||
               fileName.Contains("gummask") ||
               fileName.Contains("irismask") ||
               fileName.Contains("root map") ||
               fileName.Contains("id map") ||
               fileName.Contains("flow map") ||
               fileName.Contains("weightmap") ||
               fileName.Contains("hspecmap");
    }

    private static bool IsHairAlphaTexture(string lowerPath, bool isNormal, bool isData)
    {
        if (isNormal || isData)
        {
            return false;
        }

        return lowerPath.Contains("hair") ||
               lowerPath.Contains("lash") ||
               lowerPath.Contains("eyelash") ||
               lowerPath.Contains("female_angled") ||
               lowerPath.Contains("transparency");
    }

    private static bool SetTextureType(TextureImporter importer, TextureImporterType type)
    {
        if (importer.textureType == type)
        {
            return false;
        }

        importer.textureType = type;
        return true;
    }

    private static bool SetSrgb(TextureImporter importer, bool srgb)
    {
        if (importer.sRGBTexture == srgb)
        {
            return false;
        }

        importer.sRGBTexture = srgb;
        return true;
    }

    private static bool ConfigurePlatform(TextureImporter importer, string platformName, TextureBuildReport report)
    {
        TextureImporterPlatformSettings settings = platformName == "DefaultTexturePlatform"
            ? importer.GetDefaultPlatformTextureSettings()
            : importer.GetPlatformTextureSettings(platformName);

        bool changed = false;
        settings.name = platformName;

        if (settings.maxTextureSize < CharacterTextureMaxSize)
        {
            settings.maxTextureSize = CharacterTextureMaxSize;
            report.PlatformMaxSizeRaised++;
            changed = true;
        }

        if (settings.textureCompression == TextureImporterCompression.CompressedLQ ||
            settings.compressionQuality < 100)
        {
            settings.textureCompression = TextureImporterCompression.CompressedHQ;
            settings.compressionQuality = 100;
            settings.crunchedCompression = false;
            report.CompressionRaised++;
            changed = true;
        }

        if (platformName == "Standalone" && !settings.overridden)
        {
            settings.overridden = true;
            changed = true;
        }

        if (changed)
        {
            importer.SetPlatformTextureSettings(settings);
        }

        return changed;
    }

    private static PrefabBuildReport CreateOrUpdateHqPrefab(Dictionary<string, Material> materialMapBySourcePath)
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefabPath);
        if (source == null)
        {
            throw new InvalidOperationException($"Lucian CC5 HQ: source prefab not found at '{SourcePrefabPath}'.");
        }

        GameObject root = PrefabUtility.LoadPrefabContents(SourcePrefabPath);
        PrefabBuildReport report = new PrefabBuildReport();

        try
        {
            root.name = "Lucian_CC5_Unity_HQ";
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            report.Renderers = renderers.Length;

            foreach (Renderer renderer in renderers)
            {
                Material[] materials = renderer.sharedMaterials;
                bool rendererChanged = false;

                for (int i = 0; i < materials.Length; i++)
                {
                    Material material = materials[i];
                    if (material == null)
                    {
                        continue;
                    }

                    string materialPath = AssetDatabase.GetAssetPath(material);
                    if (!materialMapBySourcePath.TryGetValue(materialPath, out Material hqMaterial))
                    {
                        continue;
                    }

                    materials[i] = hqMaterial;
                    rendererChanged = true;
                    report.MaterialSlotsReplaced++;
                }

                if (rendererChanged)
                {
                    renderer.sharedMaterials = materials;
                    EditorUtility.SetDirty(renderer);
                }
            }

            PrefabUtility.SaveAsPrefabAsset(root, HqPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return report;
    }

    private static void CreateOrUpdateRenderTestScene()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HqPrefabPath);
        if (prefab == null)
        {
            throw new InvalidOperationException($"Lucian CC5 HQ: HQ prefab not found at '{HqPrefabPath}'.");
        }

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject lucian = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (lucian == null)
        {
            throw new InvalidOperationException("Lucian CC5 HQ: could not instantiate HQ prefab in render test scene.");
        }

        lucian.name = "Lucian_CC5_Unity_HQ";
        lucian.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        GameObject cameraObject = new GameObject("Lucian_RenderTest_Camera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.transform.position = new Vector3(0f, 1.45f, -3.2f);
        LookAt(camera.transform, new Vector3(0f, 1.05f, 0f));
        camera.fieldOfView = 35f;
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 50f;
        cameraObject.tag = "MainCamera";

        GameObject lightingRoot = new GameObject("Lucian_RenderTest_Lighting");
        CreateLight(lightingRoot.transform, "Key", LightType.Directional, new Vector3(42f, -32f, 0f), 3.2f, Color.white);
        CreateLight(lightingRoot.transform, "Fill", LightType.Point, new Vector3(-1.8f, 1.5f, -1.5f), 25f, new Color(0.72f, 0.83f, 1f));
        CreateLight(lightingRoot.transform, "Rim", LightType.Point, new Vector3(1.4f, 1.8f, 1.4f), 35f, new Color(1f, 0.82f, 0.67f));

        GameObject probeObject = new GameObject("Lucian_RenderTest_ReflectionProbe");
        ReflectionProbe probe = probeObject.AddComponent<ReflectionProbe>();
        probe.mode = ReflectionProbeMode.Realtime;
        probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
        probe.size = new Vector3(5f, 4f, 5f);
        probe.transform.position = new Vector3(0f, 1.1f, 0f);

        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Lucian_RenderTest_Floor";
        floor.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
        floor.transform.position = Vector3.zero;

        EditorSceneManager.SaveScene(scene, RenderTestScenePath);
    }

    private static void CreateLight(Transform parent, string name, LightType type, Vector3 positionOrEuler, float intensity, Color color)
    {
        GameObject lightObject = new GameObject("Lucian_RenderTest_" + name + "Light");
        lightObject.transform.SetParent(parent);
        Light light = lightObject.AddComponent<Light>();
        light.type = type;
        light.intensity = intensity;
        light.color = color;

        if (type == LightType.Directional)
        {
            lightObject.transform.rotation = Quaternion.Euler(positionOrEuler);
        }
        else
        {
            lightObject.transform.position = positionOrEuler;
            light.range = 5f;
        }
    }

    private static void LookAt(Transform transform, Vector3 target)
    {
        Vector3 direction = target - transform.position;
        if (direction.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }
    }

    private static ValidationReport ValidateIntegration(bool throwOnError)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HqPrefabPath);
        if (prefab == null)
        {
            throw new InvalidOperationException($"Lucian CC5 HQ validation failed: missing prefab '{HqPrefabPath}'.");
        }

        ValidationReport report = new ValidationReport();
        Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
        SkinnedMeshRenderer[] skinnedRenderers = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        report.Renderers = renderers.Length;
        report.SkinnedRenderers = skinnedRenderers.Length;

        foreach (Renderer renderer in renderers)
        {
            foreach (Material material in renderer.sharedMaterials)
            {
                if (material == null)
                {
                    report.MissingMaterials++;
                    continue;
                }

                report.MaterialSlots++;
                if (material.shader == null || material.shader.name == "Hidden/InternalErrorShader")
                {
                    report.MissingShaders++;
                    report.ProblemMaterials.Add(AssetDatabase.GetAssetPath(material));
                    continue;
                }

                ValidateMaterialTextures(material, report);
                ValidateHairMaterial(material, report);
            }
        }

        foreach (SkinnedMeshRenderer skinnedRenderer in skinnedRenderers)
        {
            Mesh mesh = skinnedRenderer.sharedMesh;
            if (mesh != null && mesh.blendShapeCount > 0)
            {
                report.BlendShapeMeshes++;
                report.BlendShapes += mesh.blendShapeCount;
            }
        }

        Animator animator = prefab.GetComponentInChildren<Animator>(true);
        report.HasAnimator = animator != null;
        if (animator != null && animator.avatar != null)
        {
            report.AvatarValid = animator.avatar.isValid;
            report.AvatarHumanoid = animator.avatar.isHuman;
        }

        report.RenderTestSceneExists = AssetDatabase.LoadAssetAtPath<SceneAsset>(RenderTestScenePath) != null;

        if (throwOnError && !report.IsValid)
        {
            throw new InvalidOperationException(report.ToSummary());
        }

        Debug.Log(report.ToSummary());
        return report;
    }

    private static void ValidateMaterialTextures(Material material, ValidationReport report)
    {
        string[] texturePropertyNames = material.GetTexturePropertyNames();
        foreach (string propertyName in texturePropertyNames)
        {
            Texture texture = material.GetTexture(propertyName);
            if (texture == null)
            {
                continue;
            }

            report.AssignedTextures++;
            if (propertyName.IndexOf("Normal", StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            string texturePath = AssetDatabase.GetAssetPath(texture);
            TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer == null || importer.textureType != TextureImporterType.NormalMap)
            {
                report.NormalMapImportIssues++;
                report.ProblemTextures.Add(texturePath);
            }
        }
    }

    private static void ValidateHairMaterial(Material material, ValidationReport report)
    {
        string lowerName = material.name.ToLowerInvariant();
        bool isHair = lowerName.Contains("hair") ||
                      lowerName.Contains("lash") ||
                      lowerName.Contains("female_angled") ||
                      lowerName.Contains("transparency");

        if (!isHair)
        {
            return;
        }

        report.HairMaterials++;
        bool alphaCutoffEnabled = material.HasProperty("_AlphaCutoffEnable") && material.GetFloat("_AlphaCutoffEnable") > 0.5f;
        bool alphaClipPresent = material.HasProperty("_AlphaClip") || material.HasProperty("_Cutoff") || material.HasProperty("_AlphaCutoff");
        if (!alphaCutoffEnabled && !alphaClipPresent)
        {
            report.HairAlphaIssues++;
            report.ProblemMaterials.Add(AssetDatabase.GetAssetPath(material));
        }
    }

    private static void EnsureFolderPath(string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath) || AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        string[] parts = folderPath.Split('/');
        if (parts.Length == 0 || parts[0] != "Assets")
        {
            throw new InvalidOperationException($"Lucian CC5 HQ: folder path must be under Assets: '{folderPath}'.");
        }

        string current = "Assets";
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }

    private static void SetFloatIfPresent(Material material, string propertyName, float value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetFloat(propertyName, value);
        }
    }

    private static bool HasMissingShader(Material material)
    {
        return material.shader == null || material.shader.name == "Hidden/InternalErrorShader";
    }

    private static bool ApplyHdrpLitFallback(Material material, Material source, bool isHairOrAlpha, bool isCornea, bool isEye)
    {
        Shader litShader = Shader.Find("HDRP/Lit");
        if (litShader == null)
        {
            litShader = Shader.Find("Standard");
        }

        if (litShader == null)
        {
            Debug.LogWarning($"Lucian CC5 HQ: no HDRP/Lit or Standard shader available for fallback on '{material.name}'.");
            return false;
        }

        Texture baseMap = GetFirstTexture(
            source,
            material,
            "_BaseColorMap",
            "_MainTex",
            "_DiffuseMap",
            "_CorneaDiffuseMap",
            "_ScleraDiffuseMap",
            "_IrisMap");
        Texture normalMap = GetFirstTexture(source, material, "_NormalMap", "_BumpMap", "_ScleraNormalMap");
        Texture maskMap = GetFirstTexture(source, material, "_MaskMap", "_MetallicGlossMap");

        Color baseColor;
        bool hasBaseColor = TryGetFirstColor(
            source,
            material,
            out baseColor,
            "_BaseColor",
            "_DiffuseColor",
            "_VertexBaseColor",
            "_IrisColor");

        material.shader = litShader;

        SetTextureIfPresent(material, "_BaseColorMap", baseMap);
        SetTextureIfPresent(material, "_MainTex", baseMap);
        SetTextureIfPresent(material, "_NormalMap", normalMap);
        SetTextureIfPresent(material, "_BumpMap", normalMap);
        SetTextureIfPresent(material, "_MaskMap", maskMap);
        SetTextureIfPresent(material, "_MetallicGlossMap", maskMap);

        if (hasBaseColor)
        {
            SetColorIfPresent(material, "_BaseColor", baseColor);
            SetColorIfPresent(material, "_Color", baseColor);
        }

        if (isHairOrAlpha)
        {
            material.EnableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_DOUBLESIDED_ON");
        }

        if (isCornea || isEye)
        {
            SetFloatIfPresent(material, "_Smoothness", 0.95f);
            SetFloatIfPresent(material, "_AORemapMin", 1f);
        }

        Debug.LogWarning($"Lucian CC5 HQ: material '{material.name}' used a missing Reallusion shader, assigned '{litShader.name}' fallback.");
        return true;
    }

    private static Texture GetFirstTexture(Material primary, Material fallback, params string[] propertyNames)
    {
        Texture texture = GetFirstTextureFromMaterial(primary, propertyNames);
        return texture != null ? texture : GetFirstTextureFromMaterial(fallback, propertyNames);
    }

    private static Texture GetFirstTextureFromMaterial(Material material, string[] propertyNames)
    {
        if (material == null)
        {
            return null;
        }

        foreach (string propertyName in propertyNames)
        {
            try
            {
                Texture texture = material.GetTexture(propertyName);
                if (texture != null)
                {
                    return texture;
                }
            }
            catch (ArgumentException)
            {
            }
        }

        return null;
    }

    private static bool TryGetFirstColor(Material primary, Material fallback, out Color color, params string[] propertyNames)
    {
        if (TryGetFirstColorFromMaterial(primary, out color, propertyNames))
        {
            return true;
        }

        return TryGetFirstColorFromMaterial(fallback, out color, propertyNames);
    }

    private static bool TryGetFirstColorFromMaterial(Material material, out Color color, string[] propertyNames)
    {
        color = Color.white;
        if (material == null)
        {
            return false;
        }

        foreach (string propertyName in propertyNames)
        {
            try
            {
                color = material.GetColor(propertyName);
                return true;
            }
            catch (ArgumentException)
            {
            }
        }

        return false;
    }

    private static void SetTextureIfPresent(Material material, string propertyName, Texture texture)
    {
        if (texture != null && material.HasProperty(propertyName))
        {
            material.SetTexture(propertyName, texture);
        }
    }

    private static void SetColorIfPresent(Material material, string propertyName, Color value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetColor(propertyName, value);
        }
    }

    private static string BuildReport(
        string pipeline,
        MaterialBuildReport materialReport,
        TextureBuildReport textureReport,
        PrefabBuildReport prefabReport,
        ValidationReport validation)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Lucian CC5 Unity HQ Integration Report");
        builder.AppendLine($"Pipeline: {pipeline}");
        builder.AppendLine($"Source prefab: {SourcePrefabPath}");
        builder.AppendLine($"HQ prefab: {HqPrefabPath}");
        builder.AppendLine($"Render test scene: {RenderTestScenePath}");
        builder.AppendLine();
        builder.AppendLine($"Materials scanned: {materialReport.Scanned}");
        builder.AppendLine($"Materials created: {materialReport.Created}");
        builder.AppendLine($"Materials updated: {materialReport.Updated}");
        builder.AppendLine($"Material shader fallbacks applied: {materialReport.ShaderFallbacks}");
        builder.AppendLine($"Prefab renderers: {prefabReport.Renderers}");
        builder.AppendLine($"Prefab material slots replaced: {prefabReport.MaterialSlotsReplaced}");
        builder.AppendLine();
        builder.AppendLine($"Textures scanned: {textureReport.Scanned}");
        builder.AppendLine($"Textures changed: {textureReport.Changed}");
        builder.AppendLine($"Normal maps recognized: {textureReport.NormalMaps}");
        builder.AppendLine($"Linear data maps configured: {textureReport.LinearDataMaps}");
        builder.AppendLine($"Hair alpha textures configured: {textureReport.HairAlphaTextures}");
        builder.AppendLine($"Platform max-size updates: {textureReport.PlatformMaxSizeRaised}");
        builder.AppendLine($"Compression quality updates: {textureReport.CompressionRaised}");
        builder.AppendLine();
        builder.AppendLine(validation.ToSummary());

        if (textureReport.ChangedPaths.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Changed texture importers:");
            foreach (string path in textureReport.ChangedPaths)
            {
                builder.AppendLine($"- {path}");
            }
        }

        return builder.ToString();
    }

    private sealed class MaterialBuildReport
    {
        public int Scanned;
        public int Created;
        public int Updated;
        public int ShaderFallbacks;
        public readonly Dictionary<string, Material> MaterialMapBySourcePath = new Dictionary<string, Material>(StringComparer.Ordinal);
    }

    private sealed class TextureBuildReport
    {
        public int Scanned;
        public int Changed;
        public int NormalMaps;
        public int LinearDataMaps;
        public int HairAlphaTextures;
        public int PlatformMaxSizeRaised;
        public int CompressionRaised;
        public readonly List<string> ChangedPaths = new List<string>();
    }

    private sealed class PrefabBuildReport
    {
        public int Renderers;
        public int MaterialSlotsReplaced;
    }

    private sealed class ValidationReport
    {
        public int Renderers;
        public int SkinnedRenderers;
        public int MaterialSlots;
        public int MissingMaterials;
        public int MissingShaders;
        public int AssignedTextures;
        public int NormalMapImportIssues;
        public int HairMaterials;
        public int HairAlphaIssues;
        public int BlendShapeMeshes;
        public int BlendShapes;
        public bool HasAnimator;
        public bool AvatarValid;
        public bool AvatarHumanoid;
        public bool RenderTestSceneExists;
        public readonly List<string> ProblemMaterials = new List<string>();
        public readonly List<string> ProblemTextures = new List<string>();

        public bool IsValid =>
            Renderers > 0 &&
            SkinnedRenderers > 0 &&
            MaterialSlots > 0 &&
            MissingMaterials == 0 &&
            MissingShaders == 0 &&
            NormalMapImportIssues == 0 &&
            HairAlphaIssues == 0 &&
            HasAnimator &&
            AvatarValid &&
            AvatarHumanoid &&
            RenderTestSceneExists;

        public string ToSummary()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Validation:");
            builder.AppendLine($"- Renderers: {Renderers}, skinned renderers: {SkinnedRenderers}");
            builder.AppendLine($"- Material slots: {MaterialSlots}, missing materials: {MissingMaterials}, missing shaders: {MissingShaders}");
            builder.AppendLine($"- Assigned textures: {AssignedTextures}, normal map import issues: {NormalMapImportIssues}");
            builder.AppendLine($"- Hair materials: {HairMaterials}, hair alpha issues: {HairAlphaIssues}");
            builder.AppendLine($"- Blendshape meshes: {BlendShapeMeshes}, blendshapes: {BlendShapes}");
            builder.AppendLine($"- Animator: {HasAnimator}, avatar valid: {AvatarValid}, humanoid: {AvatarHumanoid}");
            builder.AppendLine($"- Render test scene exists: {RenderTestSceneExists}");
            builder.AppendLine($"- Result: {(IsValid ? "OK" : "Needs attention")}");

            if (ProblemMaterials.Count > 0)
            {
                builder.AppendLine("Problem materials:");
                foreach (string path in UniqueFirst(ProblemMaterials, 20))
                {
                    builder.AppendLine($"  - {path}");
                }
            }

            if (ProblemTextures.Count > 0)
            {
                builder.AppendLine("Problem textures:");
                foreach (string path in UniqueFirst(ProblemTextures, 20))
                {
                    builder.AppendLine($"  - {path}");
                }
            }

            return builder.ToString();
        }

        private static IEnumerable<string> UniqueFirst(List<string> values, int maxCount)
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            int count = 0;
            foreach (string value in values)
            {
                if (!seen.Add(value))
                {
                    continue;
                }

                yield return value;
                count++;
                if (count >= maxCount)
                {
                    yield break;
                }
            }
        }
    }
}
#endif
