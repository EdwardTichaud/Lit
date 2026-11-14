#if UNITY_INCLUDE_TESTS
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class ExplorationAbilityTests
{
    [TestCase(.0001f, 0f, false)]
    [TestCase(.002f, 0f, true)]
    [TestCase(0f, 1f, true)]
    public void ClingReappliesOnlyMeaningfulPoseChanges(float distance, float angle, bool expected)
    {
        var traversal = typeof(Lit.Traversal.ClingPoint).Assembly.GetType("Lit.Traversal.ClingTraversal");
        var changed = traversal.GetMethod("PoseChanged",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var pose = new Pose(Vector3.right * distance, Quaternion.Euler(0, angle, 0));
        Assert.That(changed.Invoke(null, new object[] { Vector3.zero, Quaternion.identity, pose }), Is.EqualTo(expected));
    }

    [TestCase(2.0f, true)]
    [TestCase(.35f, false)]
    public void ClingFitsTheRealRotatedFaceAndRejectsHandOnlyContact(float wallHeight, bool expected)
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            wall.transform.SetPositionAndRotation(new Vector3(8, 1.05f, 12), Quaternion.Euler(0, 37f, 0));
            wall.transform.localScale = new Vector3(2f, wallHeight, .25f);
            Physics.SyncTransforms();
            Vector3 facing = wall.transform.forward;
            Vector3 anchor = new Vector3(8, .05f, 12) - facing * .75f;
            object[] args = { wall.GetComponent<Collider>(), anchor, facing, 1.05f,
                Vector3.up, .4f, 1.8f, null };
            bool accepted = (bool)typeof(Lit.Traversal.ClingPoint)
                .GetMethod("TryFitSurface", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                .Invoke(null, args);
            Assert.That(accepted, Is.EqualTo(expected));
            if (!accepted) return;
            var pose = (Pose)args[7];
            Assert.That(Vector3.Dot(pose.rotation * Vector3.forward, facing), Is.GreaterThan(.999f));
            Assert.That(Vector3.Dot(wall.transform.position - pose.position, facing), Is.EqualTo(.55f).Within(.003f));
            Assert.That(pose.position.y, Is.EqualTo(anchor.y).Within(.003f));
        }
        finally { Object.DestroyImmediate(wall); }
    }

    [Test]
    public void SoloEquipmentAccumulatesAndRoundTripsAndToggleDoesNotRelock()
    {
        var state = new AbilitySaveData(); state.Unlock("observateur"); state.Unlock("grimpeur");
        Assert.That(state.AddEquipped("solo", "observateur"), Is.True);
        Assert.That(state.AddEquipped("solo", "grimpeur"), Is.True);
        Assert.That(state.AddEquipped("solo", "grimpeur"), Is.False);
        var copy = JsonUtility.FromJson<AbilitySaveData>(JsonUtility.ToJson(state));
        Assert.That(copy.HasEquipped("solo", "observateur") && copy.HasEquipped("solo", "grimpeur"), Is.True);
        copy.Toggle("solo", "observateur");
        Assert.That(copy.IsUnlocked("observateur") && !copy.HasEquipped("solo", "observateur") && copy.HasEquipped("solo", "grimpeur"), Is.True);
        Assert.That(copy.AddEquipped("solo", "locked"), Is.False);
        var serializer = new SnapshotSerializer();
        var restored = serializer.Deserialize(serializer.Serialize(new WorldSnapshot { Abilities = state }));
        Assert.That(restored.Abilities.slots.Count, Is.EqualTo(2));
    }
    [TestCase(1, 4, 0, 0, 0)]
    [TestCase(2, 2, 2, 0, 0)]
    [TestCase(3, 2, 1, 1, 0)]
    [TestCase(4, 1, 1, 1, 1)]
    public void AllocationCoversAllCapabilitiesWithoutDuplicates(int count, int a, int b, int c, int d)
    {
        var state = new AbilitySaveData();
        foreach (string id in new[] { "observer", "jumper", "passage", "fourth" }) state.Unlock(id);
        var players = Enumerable.Range(0, count).Select(i => "player" + i).ToArray();
        state.Distribute(players); state.Distribute(players);
        int[] expected = { a, b, c, d };
        for (int i = 0; i < count; i++) Assert.That(state.slots.Count(s => s.playerId == players[i]), Is.EqualTo(expected[i]));
        Assert.That(state.slots.Select(s => s.abilityId).Distinct().Count(), Is.EqualTo(4));
        state.Distribute(new[] { "remaining" });
        Assert.That(state.slots.All(s => s.playerId == "remaining"), Is.True);
        Assert.That(state.slots.Count, Is.EqualTo(4));
    }
    [Test]
    public void LegacyEquipmentStillResolvesAndAddPreservesIt()
    {
        var model = JsonUtility.FromJson<AbilitySaveData>("{\"unlockedIds\":[\"observateur\",\"grimpeur\"],\"slots\":[{\"playerId\":\"solo\",\"abilityId\":\"observateur\"}]}");
        Assert.That(model.HasEquipped("solo", "observateur"), Is.True);
        model.AddEquipped("solo", "grimpeur");
        Assert.That(model.slots.Count, Is.EqualTo(2));
    }
    [Test]
    public void CompleteRestoredTeamAllocationPreservesExchangesButNotDisconnectedOwners()
    {
        var model = new AbilitySaveData(); model.Unlock("observateur"); model.Unlock("grimpeur");
        model.AddEquipped("alice", "grimpeur"); model.AddEquipped("bob", "observateur");
        Assert.That(model.IsCompleteDistribution(new[] { "alice", "bob" }), Is.True);
        Assert.That(model.IsCompleteDistribution(new[] { "alice", "charlie" }), Is.False);
        model.Unlock("new");
        Assert.That(model.IsCompleteDistribution(new[] { "alice", "bob" }), Is.False);
    }
    [Test]
    public void SavedClimberMigratesUnlockAndEquipmentAndDropsRetiredAbility()
    {
        var root = new GameObject("Ability migration test");
        root.SetActive(false);
        try
        {
            var service = root.AddComponent<AbilityService>();
            var catalog = (System.Collections.Generic.List<AbilitySO>)service.Catalog;
            catalog.AddRange(Resources.LoadAll<AbilitySO>("Abilities"));
            var old = new AbilitySaveData { equipmentVersion = 2 };
            old.Unlock("accrocheur"); old.Unlock("bondisseur");
            old.AddEquipped("solo", "accrocheur"); old.AddEquipped("solo", "bondisseur");
            old.AddEquipped("alice", "accrocheur");
            service.Restore(old);
            var current = service.Capture();
            Assert.That(service.Resolve("accrocheur").displayName, Is.EqualTo("Grimpeur"));
            Assert.That(current.IsUnlocked("accrocheur") || current.IsUnlocked("bondisseur"), Is.False);
            Assert.That(current.HasEquipped("solo", "grimpeur") && current.HasEquipped("alice", "grimpeur"), Is.True);
            old.slots.Clear();
            service.Restore(old);
            Assert.That(service.Capture().HasEquipped("solo", "grimpeur"), Is.False);
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void LimbesKeepsAbilityFreePortalBeyondTheFiveCycleGate()
    {
        const string path = "Assets/Scenes/Reve/Reve_Limbes_Progression.unity";
        var active = SceneManager.GetActiveScene();
        var scene = SceneManager.GetSceneByPath(path);
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            var progression = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<LimbesZoneProgression>(true)).Single();
            Assert.That(progression.explorationCycles.Length, Is.EqualTo(5));
            var portal = progression.arenaAccess.GetComponentsInChildren<PortalController>(true)
                .Single(p => p.name == "Portail_vers_la_Gargouille");
            Assert.That(portal.gameObject.activeSelf, Is.True);
            var portalData = new SerializedObject(portal);
            var destinations = portalData.FindProperty("destinationPoints");
            Assert.That(destinations.arraySize, Is.GreaterThan(0));
            for (int i = 0; i < destinations.arraySize; i++)
                Assert.That(destinations.GetArrayElementAtIndex(i).objectReferenceValue, Is.Not.Null);
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); if (active.isLoaded) SceneManager.SetActiveScene(active); }
    }
}
#endif
