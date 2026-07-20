using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class BreakableMasterShaderMaterialSync
{
    private const string BreakableShaderName = "Shader Graphs/ShaderGraph_MasterShader_Breakable";
    private const string AutoConfigureLoadedMaterialsKey = "Lit.Shaders.AutoConfigureBreakableLoadedMaterials";
    private const double LoadedMaterialScanInterval = 10.0;

    private static double nextLoadedMaterialScanTime;
    private static bool AutoConfigureLoadedMaterials =>
        EditorPrefs.GetBool(AutoConfigureLoadedMaterialsKey, false);

    static BreakableMasterShaderMaterialSync()
    {
        if (!AutoConfigureLoadedMaterials)
        {
            return;
        }

        EditorApplication.delayCall += ConfigureLoadedMaterials;
        EditorApplication.projectChanged += ConfigureLoadedMaterials;
        EditorApplication.update += ConfigureLoadedMaterialsPeriodically;
    }

    [MenuItem("Tools/Lit/Shaders/Fix Breakable MasterShader Materials")]
    private static void ConfigureProjectMaterialsMenu()
    {
        int changedCount = ConfigureProjectMaterials();
        Debug.Log($"Breakable MasterShader: configured {changedCount} material(s).");
    }

    private static void ConfigureLoadedMaterialsPeriodically()
    {
        if (EditorApplication.timeSinceStartup < nextLoadedMaterialScanTime)
        {
            return;
        }

        nextLoadedMaterialScanTime = EditorApplication.timeSinceStartup + LoadedMaterialScanInterval;
        ConfigureLoadedMaterials();
    }

    private static void ConfigureLoadedMaterials()
    {
        Material[] materials = Resources.FindObjectsOfTypeAll<Material>();
        for (int i = 0; i < materials.Length; i++)
        {
            ConfigureMaterial(materials[i], markDirty: EditorUtility.IsPersistent(materials[i]));
        }
    }

    private static int ConfigureProjectMaterials()
    {
        string[] materialGuids = AssetDatabase.FindAssets("t:Material");
        int changedCount = 0;

        for (int i = 0; i < materialGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(materialGuids[i]);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (ConfigureMaterial(material, markDirty: true))
            {
                changedCount++;
            }
        }

        if (changedCount > 0)
        {
            AssetDatabase.SaveAssets();
        }

        return changedCount;
    }

    private static bool ConfigureMaterial(Material material, bool markDirty)
    {
        if (material == null || material.shader == null || material.shader.name != BreakableShaderName)
        {
            return false;
        }

        bool changed = false;
        changed |= SetFloatIfDifferent(material, "_AlphaClip", 1f);
        changed |= SetFloatIfDifferent(material, "_AlphaCutoffEnable", 1f);
        changed |= SetFloatIfDifferent(material, "_BUILTIN_AlphaClip", 1f);

        changed |= EnableKeywordIfMissing(material, "_ALPHATEST_ON");
        changed |= EnableKeywordIfMissing(material, "_BUILTIN_ALPHATEST_ON");
        changed |= EnableKeywordIfMissing(material, "_BUILTIN_AlphaClip");

        if (changed && markDirty)
        {
            EditorUtility.SetDirty(material);
        }

        return changed;
    }

    private static bool SetFloatIfDifferent(Material material, string propertyName, float value)
    {
        if (!material.HasProperty(propertyName) || Mathf.Approximately(material.GetFloat(propertyName), value))
        {
            return false;
        }

        material.SetFloat(propertyName, value);
        return true;
    }

    private static bool EnableKeywordIfMissing(Material material, string keyword)
    {
        if (material.IsKeywordEnabled(keyword))
        {
            return false;
        }

        material.EnableKeyword(keyword);
        return true;
    }
}
