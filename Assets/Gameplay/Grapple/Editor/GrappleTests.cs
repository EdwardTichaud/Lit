#if UNITY_INCLUDE_TESTS
using System.Linq;
using Lit.Traversal;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class GrappleTests
{
    [Test]
    public void SoloEquipmentAccumulatesAndRoundTripsAndToggleDoesNotRelock()
    {
        var state = new AbilitySaveData(); state.Unlock("observateur"); state.Unlock("bondisseur");
        Assert.That(state.AddEquipped("solo", "observateur"), Is.True);
        Assert.That(state.AddEquipped("solo", "bondisseur"), Is.True);
        Assert.That(state.AddEquipped("solo", "bondisseur"), Is.False);
        var copy = JsonUtility.FromJson<AbilitySaveData>(JsonUtility.ToJson(state));
        Assert.That(copy.HasEquipped("solo", "observateur") && copy.HasEquipped("solo", "bondisseur"), Is.True);
        copy.Toggle("solo", "observateur");
        Assert.That(copy.IsUnlocked("observateur") && !copy.HasEquipped("solo", "observateur") && copy.HasEquipped("solo", "bondisseur"), Is.True);
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
        var model = JsonUtility.FromJson<AbilitySaveData>("{\"unlockedIds\":[\"observateur\",\"bondisseur\"],\"slots\":[{\"playerId\":\"solo\",\"abilityId\":\"observateur\"}]}");
        Assert.That(model.HasEquipped("solo", "observateur"), Is.True);
        model.AddEquipped("solo", "bondisseur");
        Assert.That(model.slots.Count, Is.EqualTo(2));
    }
    [Test]
    public void CompleteRestoredTeamAllocationPreservesExchangesButNotDisconnectedOwners()
    {
        var model = new AbilitySaveData(); model.Unlock("observateur"); model.Unlock("bondisseur");
        model.AddEquipped("alice", "bondisseur"); model.AddEquipped("bob", "observateur");
        Assert.That(model.IsCompleteDistribution(new[] { "alice", "bob" }), Is.True);
        Assert.That(model.IsCompleteDistribution(new[] { "alice", "charlie" }), Is.False);
        model.Unlock("new");
        Assert.That(model.IsCompleteDistribution(new[] { "alice", "bob" }), Is.False);
    }
    [Test]
    public void ArcStartsAndEndsExactlyAndRisesThenDescends()
    {
        Vector3 a = new(0, 0, 0), b = new(0, 0, 25);
        Assert.That(GrappleAnchor.Evaluate(a, b, 4, 0), Is.EqualTo(a));
        Assert.That(GrappleAnchor.Evaluate(a, b, 4, 1), Is.EqualTo(b));
        Assert.That(GrappleAnchor.Evaluate(a, b, 4, .5f).y, Is.EqualTo(4).Within(.001));
        Assert.That(GrappleAnchor.Evaluate(a, b, 4, .25f).y, Is.LessThan(4));
        Assert.That(GrappleAnchor.Evaluate(a, b, 4, .75f).y, Is.LessThan(4));
    }
    [Test]
    public void BodySweepRejectsWallWithoutIgnoringCollisions()
    {
        var actor = new GameObject("Actor collider ownership");
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            wall.transform.position = new Vector3(9000, 1, 2);
            wall.transform.localScale = new Vector3(10, 3, .5f); Physics.SyncTransforms();
            Assert.That(GrappleAnchor.ClearBody(new Vector3(9000, 0, 0), new Vector3(9000, 0, 4), actor.transform), Is.False);
            Assert.That(GrappleAnchor.ClearBody(new Vector3(9000, 5, 0), new Vector3(9000, 5, 4), actor.transform), Is.True);
        }
        finally { Object.DestroyImmediate(actor); Object.DestroyImmediate(wall); }
    }
    [Test]
    public void LimbesAnchorsKeepTheFiveCycleGateAndSafeReturn()
    {
        var original = SceneManager.GetActiveScene();
        var scene = SceneManager.GetSceneByPath(GrappleAuthoring.ScenePath);
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(GrappleAuthoring.ScenePath, OpenSceneMode.Additive);
        try
        {
            var progression = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<LimbesZoneProgression>(true)).Single();
            var anchors = progression.arenaAccess.GetComponentsInChildren<GrappleAnchor>(true);
            Assert.That(progression.explorationCycles.Length, Is.EqualTo(5));
            Assert.That(anchors.Length, Is.EqualTo(2));
            Assert.That(anchors.Select(a => a.anchorId).Distinct().Count(), Is.EqualTo(2));
            Assert.That(anchors[0].routeId, Is.EqualTo(anchors[1].routeId));
            Assert.That(anchors[0].landing, Is.EqualTo(anchors[1].launch));
            Assert.That(anchors[0].launch, Is.EqualTo(anchors[1].landing));
            Assert.That(progression.arenaAccess.transform.Cast<Transform>().Where(t => t.name.StartsWith("Pont_final_")).All(t => t.gameObject.activeSelf),
                Is.True, "Keep campaign access until Bondisseur's narrative acquisition is configured.");
            bool wasActive = progression.arenaAccess.activeSelf;
            try
            {
                progression.arenaAccess.SetActive(true); Physics.SyncTransforms();
                Assert.That(anchors[0].SafeLanding() && anchors[1].SafeLanding(), Is.True);
                var sweepOwner = new GameObject("Grapple path test ownership");
                try
                {
                    foreach (var anchor in anchors)
                    {
                        Vector3 eye = anchor.launch.position + Vector3.up * 1.4f;
                        Vector3 sight = anchor.HookPosition - eye;
                        var hits = Physics.RaycastAll(eye, sight.normalized, sight.magnitude, ~0, QueryTriggerInteraction.Ignore);
                        Assert.That(hits.Select(h => h.collider.name), Is.Empty, "Hook must be visible from departure: " + anchor.anchorId);
                        Vector3 previous = anchor.launch.position;
                        for (int i = 1; i <= 32; i++)
                        {
                            var next = anchor.Position(anchor.launch.position, i / 32f);
                            Assert.That(GrappleAnchor.ClearBody(previous, next, sweepOwner.transform), Is.True, anchor.anchorId + " arc segment " + i);
                            previous = next;
                        }
                    }
                }
                finally { Object.DestroyImmediate(sweepOwner); }
            }
            finally { progression.arenaAccess.SetActive(wasActive); }
            var bond = AssetDatabase.LoadAssetAtPath<AbilitySO>("Assets/Resources/Abilities/Ability_Bondisseur.asset");
            Assert.That(bond != null && bond.abilityId == "bondisseur" && !bond.availableAtStart, Is.True);
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); if (original.isLoaded) SceneManager.SetActiveScene(original); }
    }
}
#endif
