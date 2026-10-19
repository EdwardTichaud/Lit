#if UNITY_INCLUDE_TESTS
using System;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public sealed class CombatBootstrapZoneSelectionTests
{
    [Test]
    public void DefaultKeepsTheExistingDistrictRecipe()
    {
        Assert.AreEqual("Assets/Scenes/Maison/ZoneManifest_District_1.asset",
            CombatBootstrapPlayValidation.ResolveManifestPath(new[] { "-batchmode" }));
    }

    [Test]
    public void ExplicitManifestDoesNotFallBackToDistrict()
    {
        const string path = "Assets/Scenes/Maison/ZoneManifest_Maison.asset";
        Assert.AreEqual(path, CombatBootstrapPlayValidation.ResolveManifestPath(
            new[] { "-combatMigrationZoneManifest", path }));
    }

    [TestCase("-batchmode")]
    [TestCase("Library/Untracked.asset")]
    [TestCase("Assets/Scenes/Maison/Maison_Core.unity")]
    public void InvalidExplicitSelectionIsRejectedInsteadOfTestingAnotherZone(string value)
    {
        Assert.Throws<ArgumentException>(() => CombatBootstrapPlayValidation.ResolveManifestPath(
            new[] { "-combatMigrationZoneManifest", value }));
    }

    [Test]
    public void MissingManifestArgumentIsRejected()
    {
        Assert.Throws<ArgumentException>(() => CombatBootstrapPlayValidation.ResolveManifestPath(
            new[] { "-combatMigrationZoneManifest" }));
    }

    [Test]
    public void MaisonUsesItsExistingLucianTransformAsThePrimarySpawn()
    {
        const string path = "Assets/Scenes/Maison/Maison_Core.unity";
        var scene = SceneManager.GetSceneByPath(path);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        try
        {
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            var spawn = ZoneSpawnPoint.FindFirstInScene("Maison_Core");
            Assert.That(spawn, Is.Not.Null, "An unregistered Transform leaves new-game placement at the squad origin.");
            Assert.That(spawn.name, Is.EqualTo("LucianSpawnPoint"));
            Assert.That(spawn.transform.parent.name, Is.EqualTo("0_SpawnPoints"));
            Assert.That(spawn.SpawnId, Is.EqualTo("Default"));
            Assert.That(spawn.PartySlot, Is.Zero);
            Assert.That(spawn.GetComponents<ZoneSpawnPoint>(), Has.Length.EqualTo(1));
        }
        finally { if (opened && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true); }
    }
}
#endif
