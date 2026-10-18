#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Linq;
using NUnit.Framework;

public sealed class CombatInventoryTests
{
    [Test]
    public void ZoneSceneLookupFindsAuthoredLightingOutsideScenesFolder()
    {
        Assert.That(ZoneManifestBuildSettingsSync.FindScenePath("District_1_Indoor_Lights"),
            Is.EqualTo("Assets/District_1_Indoor_Lights.unity"));
        Assert.That(UnityEditor.AssetDatabase.AssetPathToGUID("Assets/District_1_Indoor_Lights.unity"),
            Is.EqualTo("1131d151a2c066d4b9a6031f5ba56c91"));
    }

    [Test]
    public void DistrictRuntimeSceneNamesMatchTheAuthoredRequiredScenes()
    {
        var manifest = UnityEditor.AssetDatabase.LoadAssetAtPath<ZoneManifest>("Assets/Scenes/Maison/ZoneManifest_District_1.asset");
        Assert.That(manifest.loadingScenes, Has.None.Null);
        CollectionAssert.AreEqual(manifest.loadingScenes.Select(scene => scene.name).ToArray(), manifest.LoadingSceneNames.ToArray());
        Assert.That(manifest.LoadingSceneNames, Does.Contain("District_1_Indoor_Lights"));
    }

    public static IEnumerable Cases()
    {
        foreach (var test in CombatInventoryChecks.Cases()) yield return new TestCaseData(test.Value).SetName(test.Key);
    }
    [TestCaseSource(nameof(Cases))]
    public void ClassifiesInventoryWithoutAssetWrites(System.Action test) => test();
}
#endif
