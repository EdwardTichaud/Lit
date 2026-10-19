#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using System.Reflection;
using UnityEngine;

public sealed class CombatPlayerHealthPersistenceTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void InactiveMarkerProviderCapturesAndRestoresTheActualOwnedEnemyWithoutAwake(bool providerOnActor)
    {
        var root = new GameObject("Inactive marker persistence fixture");
        root.SetActive(false);
        var actor = new GameObject("Baked enemy");
        actor.transform.SetParent(root.transform);
        try
        {
            var marker = root.AddComponent<SceneMarker>();
            typeof(SceneMarker).GetField("runtimeInstance", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(marker, actor);
            var health = actor.AddComponent<CharacterInfo>();
            health.SetHealth(0, 100);
            var provider = (providerOnActor ? actor : root).AddComponent<PersistentSceneMarkerCharacterState>();
            Assert.That(actor.GetComponent<SceneMarker>(), Is.Null, "The provider must not create an empty nested marker.");
            var payload = provider.CaptureState(null);
            health.SetHealth(100, 100);
            provider.ApplyState(payload, PersistentApplyPhase.ApplyGameplayState, null);
            Assert.That(health.CurrentHp, Is.Zero);
            Assert.That(health.IsDead, Is.True);
            Assert.That(actor.transform.parent, Is.EqualTo(root.transform));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void SoloSpawnUsesTheSameStableRuntimeCharacterIdentityAcrossReloads()
    {
        var catalog = Resources.Load<CombatSkillKitCatalog>("CombatSkillKits/Default");
        var data = Object.Instantiate(catalog.GetCharacter(CombatSkillKit.Lucian));
        var managerRoot = new GameObject("Solo identity fixture");
        managerRoot.SetActive(false);
        var prefab = new GameObject("Character prefab identity fixture");
        prefab.SetActive(false);
        var original = prefab.AddComponent<PersistentNetworkObject>();
        original.AssignSceneIdentity("GlobalObjectId_V1-0-00000000000000000000000000000000-0-0");
        data.worldPrefab = prefab;
        data.starterItemsWithQuantity.Clear();
        GameObject first = null;
        GameObject second = null;
        try
        {
            var manager = managerRoot.AddComponent<SquadManager>();
            var spawn = typeof(SquadManager).GetMethod("SpawnCharacterAt", BindingFlags.Instance | BindingFlags.NonPublic);
            first = (GameObject)spawn.Invoke(manager, new object[] { data, Vector3.zero, Quaternion.identity, false });
            Assert.That(first, Is.Not.Null);
            var persistent = first.GetComponent<PersistentNetworkObject>();
            Assert.That(persistent.ObjectKind, Is.EqualTo(PersistentObjectKind.RuntimeSpawned));
            Assert.That(persistent.PersistentId, Is.EqualTo("runtime:character:" + data.UniqueId));
            Assert.That(persistent.RuntimePrefabId, Is.EqualTo("character:" + data.UniqueId));
            string id = persistent.PersistentId;
            Object.DestroyImmediate(first);
            second = (GameObject)spawn.Invoke(manager, new object[] { data, Vector3.one, Quaternion.Euler(0, 90, 0), false });
            Assert.That(second.GetComponent<PersistentNetworkObject>().PersistentId, Is.EqualTo(id));
            Assert.That(original.ObjectKind, Is.EqualTo(PersistentObjectKind.ScenePlaced), "Do not mutate prefab authoring at runtime.");
        }
        finally
        {
            if (first != null) Object.DestroyImmediate(first);
            if (second != null) Object.DestroyImmediate(second);
            Object.DestroyImmediate(managerRoot);
            Object.DestroyImmediate(prefab);
            Object.DestroyImmediate(data);
        }
    }

    [TestCase(37, 37)]
    [TestCase(0, 0)]
    [TestCase(-8, 0)]
    [TestCase(150, 100)]
    public void RestoreUsesSavedHealthWithinTheActorsAuthoredMaximum(int saved, int expected)
    {
        var root = new GameObject("Health persistence fixture");
        root.SetActive(false);
        try
        {
            var actor = root.AddComponent<SquadCharacterController>();
            actor.SetHealth(100, 100);
            var entry = JsonUtility.FromJson<CharacterSaveEntry>(JsonUtility.ToJson(
                new CharacterSaveEntry { healthInitialized = true, currentHp = saved }));
            CharacterStateStore.RestoreCharacterHealth(actor, entry);
            Assert.That(actor.CurrentHp, Is.EqualTo(expected));
            Assert.That(actor.MaxHp, Is.EqualTo(100));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void OldSaveWithoutHealthDoesNotTurnTheActorIntoADeadCharacter()
    {
        var entry = JsonUtility.FromJson<CharacterSaveEntry>("{\"characterId\":\"legacy\",\"inSquad\":true}");
        Assert.That(entry.healthInitialized, Is.False);
        var root = new GameObject("Legacy health fixture");
        root.SetActive(false);
        try
        {
            var actor = root.AddComponent<SquadCharacterController>();
            actor.SetHealth(73, 100);
            CharacterStateStore.RestoreCharacterHealth(actor, entry);
            Assert.That(actor.CurrentHp, Is.EqualTo(73));
            CharacterStateStore.RestoreCharacterHealth(actor, null);
            CharacterStateStore.RestoreCharacterHealth(null, entry);
            Assert.That(actor.CurrentHp, Is.EqualTo(73));
        }
        finally { Object.DestroyImmediate(root); }
    }
}
#endif
