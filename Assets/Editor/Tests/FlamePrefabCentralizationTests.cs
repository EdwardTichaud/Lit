using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class FlamePrefabCentralizationTests
{
    private const string WallTorch = "Assets/Prefabs/Model_Flame_WallTorch.prefab";
    private const string Brazier = "Assets/Prefabs/Model_Flame_Brazier.prefab";

    [TestCase(WallTorch, false, 10, 1)]
    [TestCase(Brazier, true, 20, 2)]
    public void FlameModels_ContainTheirCompleteLocalPresentation(string path, bool expectedAncient, int expectedCost, int expectedLightCount)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Assert.That(prefab, Is.Not.Null, path);
        Flame flame = prefab.GetComponentInChildren<Flame>(true);
        Assert.That(flame, Is.Not.Null, path);
        Assert.That(flame.IsAncientFlame, Is.EqualTo(expectedAncient), path);
        Assert.That(flame.TorchPointCostToLight, Is.EqualTo(expectedCost), path);
        Assert.That(flame.useInteractInput, Is.True, path);
        Assert.That(prefab.GetComponentsInChildren<Light>(true).Length, Is.GreaterThanOrEqualTo(expectedLightCount), path);
        Assert.That(prefab.GetComponentsInChildren<LitContrastLight>(true).Length, Is.GreaterThanOrEqualTo(expectedLightCount), path);

        LitAtmosphereParticles atmosphere = prefab.GetComponentInChildren<LitAtmosphereParticles>(true);
        Assert.That(atmosphere, Is.Not.Null, path);
        Assert.That(atmosphere.ValidateConfiguration(out string reason), Is.True, reason);
    }

    [Test]
    public void LegacyFlameAssets_AreRemoved()
    {
        Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Interactive/Flame/Light_Flame_Fire_Common.prefab"), Is.Null);
        Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Interactive/Flame/Light_Flame_Fire_Ancient.prefab"), Is.Null);
        Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Interactive/Flame/Interactive_Flame_Ancient.prefab"), Is.Null);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void ReceiverLifecycleRespectsProfileShadowOwnership(bool withProfile)
    {
        var root = new GameObject("Shadow ownership lifecycle");
        root.SetActive(false);
        var lightObject = new GameObject("Practical light");
        lightObject.transform.SetParent(root.transform);
        var light = lightObject.AddComponent<Light>();
        var hd = lightObject.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>();
        if (withProfile) lightObject.AddComponent<LitContrastLight>();
        var receiver = root.AddComponent<FlameLightReceiver>();
        try
        {
            light.shadows = LightShadows.Hard;
            light.shadowNearPlane = .3f;
            hd.shadowDimmer = .62f;
            hd.normalBias = .2f;
            foreach (string callback in new[] { "Awake", "OnEnable", "OnValidate" })
            {
                typeof(FlameLightReceiver).GetMethod(callback, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(receiver, null);
                Assert.That(receiver.ControlsShadowing, Is.EqualTo(!withProfile), callback);
                Assert.That(hd.shadowDimmer, Is.EqualTo(withProfile ? .62f : 1f).Within(.001f), callback);
                Assert.That(light.shadows, Is.EqualTo(withProfile ? LightShadows.Hard : LightShadows.Soft), callback);
                Assert.That(light.shadowNearPlane, Is.EqualTo(withProfile ? .3f : .02f).Within(.001f), callback);
                Assert.That(hd.normalBias, Is.EqualTo(withProfile ? .2f : .03f).Within(.001f), callback);
            }
        }
        finally { Object.DestroyImmediate(root); }
    }
}
