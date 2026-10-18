#if UNITY_INCLUDE_TESTS
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public sealed class ProductionCombatMigrationReadOnlyTests
{
    [Test, Explicit("Parent only: run in the isolated Unity project after import and compilation. This does not apply migration.")]
    public void RepeatedDryRunPreservesSourceBytesAndProducesSamePlan()
    {
        var before = ProductionCombatAudit.CaptureSnapshot();
        Assert.That(before.errors, Is.Empty);
        var setup = EditorSceneManager.GetSceneManagerSetup().Select(scene => scene.path + ":" + scene.isLoaded + ":" + scene.isActive).ToArray();
        var dirty = Enumerable.Range(0, SceneManager.sceneCount).Select(index => SceneManager.GetSceneAt(index).isDirty).ToArray();
        var first = ProductionCombatMigration.Run(false);
        var second = ProductionCombatMigration.Run(false);
        var after = ProductionCombatAudit.CaptureSnapshot();
        Assert.That(after.fingerprint, Is.EqualTo(before.fingerprint));
        Assert.That(after.revision, Is.EqualTo(before.revision));
        Assert.That(second.changedAssets, Is.EqualTo(first.changedAssets));
        Assert.That(second.entries.Where(entry => entry.category == "change").Select(entry => entry.path + ":" + entry.detail).OrderBy(value => value),
            Is.EqualTo(first.entries.Where(entry => entry.category == "change").Select(entry => entry.path + ":" + entry.detail).OrderBy(value => value)));
        Assert.That(EditorSceneManager.GetSceneManagerSetup().Select(scene => scene.path + ":" + scene.isLoaded + ":" + scene.isActive), Is.EqualTo(setup));
        Assert.That(Enumerable.Range(0, SceneManager.sceneCount).Select(index => SceneManager.GetSceneAt(index).isDirty), Is.EqualTo(dirty));
        Assert.That(first.applied || second.applied, Is.False);
    }
}
#endif
