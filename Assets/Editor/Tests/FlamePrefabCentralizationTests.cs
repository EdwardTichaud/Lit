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
}
