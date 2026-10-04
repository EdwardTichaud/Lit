using NUnit.Framework;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class LitFlameLightingTests
{
    private static readonly string[] FlameScenePaths =
    {
        "Assets/Scenes/District_1/District_1_Corridor_Flammes.unity",
        "Assets/Scenes/District_1/District_1_Rooms_Flammes.unity",
        "Assets/Scenes/District_1/District_1_PuitsDeLaReleve_Environment.unity",
        "Assets/Scenes/District_5.unity"
    };

    [TestCase("Assets/Interactive/Flame/Light_Flame_Fire_Common.prefab", 1)]
    [TestCase("Assets/Interactive/Flame/Light_Flame_Fire_Ancient.prefab", 2)]
    [TestCase("Assets/Characters/1_Squad/Lucian/Player_Model_Lucian.prefab", 1)]
    public void ContrastLightPrefabs_ContainExpectedProfiles(string path, int minimumProfiles)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Assert.That(prefab, Is.Not.Null, path);
        LitContrastLight[] profiles = prefab.GetComponentsInChildren<LitContrastLight>(true);
        Assert.That(profiles.Length, Is.GreaterThanOrEqualTo(minimumProfiles), path);
        foreach (LitContrastLight profile in profiles)
            Assert.That(profile.GetComponent<Light>(), Is.Not.Null, profile.name);
    }

    [Test]
    public void CastleCore_ContainsIceDustAtmosphere()
    {
        const string path = "Assets/Scenes/District_1/District_1_Core.unity";
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            bool found = false;
            foreach (GameObject root in scene.GetRootGameObjects())
                found |= root.GetComponentInChildren<LitAtmosphereParticles>(true) != null;
            Assert.That(found, Is.True, path);
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    [TestCaseSource(nameof(FlameScenePaths))]
    public void FlameScenes_DoNotKeepLegacyLightOverrides(string path)
    {
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            LitContrastLight[] profiles = Object.FindObjectsByType<LitContrastLight>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(profile => profile.gameObject.scene == scene)
                .ToArray();
            Assert.That(profiles, Is.Not.Empty, path);

            foreach (LitContrastLight profile in profiles)
            {
                Light light = profile.GetComponent<Light>();
                if (light == null || !PrefabUtility.IsPartOfPrefabInstance(light))
                    continue;

                var legacyOverrides = PrefabUtility.GetPropertyModifications(light)
                    ?.Where(modification => modification.propertyPath == "m_Range"
                        || modification.propertyPath == "m_Intensity"
                        || modification.propertyPath == "m_Type"
                        || modification.propertyPath == "m_InnerSpotAngle"
                        || modification.propertyPath.StartsWith("m_Color")
                        || modification.propertyPath.StartsWith("m_Shadows"))
                    .ToArray();
                Assert.That(legacyOverrides, Is.Null.Or.Empty, $"{path}: {profile.name} retains legacy Light overrides.");

                var hdLight = profile.GetComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>();
                if (hdLight == null || !PrefabUtility.IsPartOfPrefabInstance(hdLight))
                    continue;
                var hdOverrides = PrefabUtility.GetPropertyModifications(hdLight)
                    ?.Where(modification => modification.propertyPath != "m_Enabled")
                    .ToArray();
                Assert.That(hdOverrides, Is.Null.Or.Empty, $"{path}: {profile.name} retains legacy HDRP-light overrides.");
            }
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }
}
