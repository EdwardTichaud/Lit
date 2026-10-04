using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class MuninGameplayRemovalTests
{
    [TestCase("Assets/Characters/1_Squad/Lucian/Player_Model_Lucian.prefab")]
    [TestCase("Assets/Characters/1_Squad/Link/Player_Model_Link.prefab")]
    [TestCase("Assets/Characters/1_Squad/Mia/Player_Model_Mia.prefab")]
    [TestCase("Assets/Characters/1_Squad/Luna/Player_Model_Luna.prefab")]
    public void PlayerPrefabs_DoNotContainPlayableMunin(string path)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Assert.That(prefab, Is.Not.Null, path);
        Assert.That(prefab.GetComponentInChildren<MuninController>(true), Is.Null, path);
        Assert.That(prefab.GetComponentInChildren<SpiritBondController>(true), Is.Null, path);
    }

    [TestCase("Assets/Prefabs/Model_Flame_WallTorch.prefab")]
    [TestCase("Assets/Prefabs/Model_Flame_Brazier.prefab")]
    public void Flames_UseNearbyDirectInteraction(string path)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Flame flame = prefab.GetComponentInChildren<Flame>(true);
        Assert.That(flame, Is.Not.Null, path);
        Assert.That(flame.useInteractInput, Is.True, path);
        Assert.That(flame.GetInteractionMaxDistance(null), Is.LessThanOrEqualTo(1.01f), path);
    }
}
