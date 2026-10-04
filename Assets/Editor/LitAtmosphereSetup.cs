using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Builds the serialized HDRP particle systems used by the castle.</summary>
[InitializeOnLoad]
public static class LitAtmosphereSetup
{
    private const string CoreScenePath = "Assets/Scenes/District_1/District_1_Core.unity";
    private const string CommonFlamePath = "Assets/Interactive/Flame/Light_Flame_Fire_Common.prefab";
    private const string AncientFlamePath = "Assets/Interactive/Flame/Light_Flame_Fire_Ancient.prefab";
    private const string MaterialFolder = "Assets/Environment/Lighting/Materials";
    private const string RequestPath = "Library/LitAtmosphereRepair.request";
    private const string ResultPath = "Library/LitAtmosphereRepair.result";

    static LitAtmosphereSetup()
    {
        EditorApplication.delayCall += RunRequestedRepair;
        EditorApplication.update += RunRequestedRepair;
    }

    [MenuItem("Lit/Lighting/Rebuild Castle Atmosphere")]
    public static void RebuildCastleAtmosphere()
    {
        Materials materials = EnsureMaterials();
        RebuildFlamePrefab(CommonFlamePath, LitAtmosphereParticles.Profile.FlameEmbers, materials);
        RebuildFlamePrefab(AncientFlamePath, LitAtmosphereParticles.Profile.AncientFlameMix, materials);
        RebuildCoreScene(materials);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Lit Lighting] Castle atmosphere rebuilt with serialized particle systems and HDRP materials.");
    }

    private static void RunRequestedRepair()
    {
        if (!File.Exists(RequestPath) || EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;

        try
        {
            File.Delete(RequestPath);
            RebuildCastleAtmosphere();
            File.WriteAllText(ResultPath, "success " + DateTime.UtcNow.ToString("O"));
        }
        catch (Exception exception)
        {
            File.WriteAllText(ResultPath, "failure\n" + exception);
            Debug.LogException(exception);
        }
    }

    private static void RebuildCoreScene(Materials materials)
    {
        Scene scene = EditorSceneManager.OpenScene(CoreScenePath, OpenSceneMode.Additive);
        try
        {
            foreach (GameObject gameObject in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (gameObject.scene != scene || gameObject.name != "VFX_CastleIceDust") continue;
                UnityEngine.Object.DestroyImmediate(gameObject);
            }

            GameObject castleVolume = FindInScene(scene, "Castle_Volume");
            if (castleVolume == null)
                throw new InvalidOperationException("District_1_Core has no Castle_Volume root.");

            GameObject atmosphereRoot = new GameObject("VFX_CastleIceDust");
            atmosphereRoot.transform.SetParent(castleVolume.transform, false);
            atmosphereRoot.transform.localPosition = new Vector3(0f, 3f, 0f);
            LitAtmosphereParticles atmosphere = atmosphereRoot.AddComponent<LitAtmosphereParticles>();
            ParticleSystem dust = CreateParticleSystem(atmosphereRoot.transform, "Particles_IceDust");
            atmosphere.ConfigureAuthoring(LitAtmosphereParticles.Profile.CastleIceDust, new[] { dust }, materials.Dust, materials.Embers, materials.AncientMist);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void RebuildFlamePrefab(string path, LitAtmosphereParticles.Profile profile, Materials materials)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            LitAtmosphereParticles atmosphere = root.GetComponent<LitAtmosphereParticles>();
            if (atmosphere == null) atmosphere = root.AddComponent<LitAtmosphereParticles>();

            DeleteChild(root.transform, "AtmosphereParticles_Embers");
            DeleteChild(root.transform, "AtmosphereParticles_AncientMist");
            ParticleSystem embers = CreateParticleSystem(root.transform, "AtmosphereParticles_Embers");
            embers.transform.localPosition = new Vector3(0f, 0.08f, 0f);

            if (profile == LitAtmosphereParticles.Profile.AncientFlameMix)
            {
                ParticleSystem mist = CreateParticleSystem(root.transform, "AtmosphereParticles_AncientMist");
                mist.transform.localPosition = new Vector3(0f, 0.18f, 0f);
                atmosphere.ConfigureAuthoring(profile, new[] { embers, mist }, materials.Dust, materials.Embers, materials.AncientMist);
            }
            else atmosphere.ConfigureAuthoring(profile, new[] { embers }, materials.Dust, materials.Embers, materials.AncientMist);

            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static ParticleSystem CreateParticleSystem(Transform parent, string childName)
    {
        GameObject child = new GameObject(childName);
        child.transform.SetParent(parent, false);
        return child.AddComponent<ParticleSystem>();
    }

    private static void DeleteChild(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        if (child != null) UnityEngine.Object.DestroyImmediate(child.gameObject);
    }

    private static GameObject FindInScene(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                if (transform.name == name) return transform.gameObject;
        }
        return null;
    }

    private static Materials EnsureMaterials()
    {
        EnsureFolder("Assets/Environment");
        EnsureFolder("Assets/Environment/Lighting");
        EnsureFolder(MaterialFolder);
        Texture2D softParticleTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Hovl Studio/Procedural fire/Textures/Smoke26.png");
        if (softParticleTexture == null) throw new InvalidOperationException("Smoke26 texture is missing.");

        return new Materials
        {
            Dust = CreateOrLoadMaterial(MaterialFolder + "/M_CastleIceDust.mat", "HDRP/Lit", softParticleTexture, new Color(0.62f, 0.8f, 1f, 0.2f), false),
            Embers = CreateOrLoadMaterial(MaterialFolder + "/M_FlameEmbers.mat", "HDRP/Unlit", softParticleTexture, new Color(1f, 0.2f, 0.03f, 1f), true),
            AncientMist = CreateOrLoadMaterial(MaterialFolder + "/M_AncientFlameMist.mat", "HDRP/Lit", softParticleTexture, new Color(0.2f, 0.72f, 1f, 0.18f), false)
        };
    }

    private static Material CreateOrLoadMaterial(string path, string shaderName, Texture2D texture, Color color, bool emissive)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException($"Required HDRP shader '{shaderName}' is unavailable.");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        if (material.HasProperty("_BaseColorMap")) material.SetTexture("_BaseColorMap", texture);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_SurfaceType")) material.SetFloat("_SurfaceType", 1f);
        if (material.HasProperty("_BlendMode")) material.SetFloat("_BlendMode", emissive ? 1f : 0f);
        if (material.HasProperty("_AlphaCutoffEnable")) material.SetFloat("_AlphaCutoffEnable", 1f);
        if (material.HasProperty("_AlphaCutoff")) material.SetFloat("_AlphaCutoff", 0.035f);
        if (material.HasProperty("_TransparentZWrite")) material.SetFloat("_TransparentZWrite", 0f);
        if (material.HasProperty("_EnableFogOnTransparent")) material.SetFloat("_EnableFogOnTransparent", 1f);
        if (emissive && material.HasProperty("_EmissiveColor")) material.SetColor("_EmissiveColor", new Color(2.5f, 0.22f, 0.02f, 1f));
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string name = Path.GetFileName(path);
        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name)) throw new InvalidOperationException("Invalid asset folder: " + path);
        AssetDatabase.CreateFolder(parent, name);
    }

    private struct Materials
    {
        public Material Dust;
        public Material Embers;
        public Material AncientMist;
    }
}
