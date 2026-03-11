using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class ReplicatedWorldStateRegistryTests
{
    [TearDown]
    public void TearDown()
    {
        ReplicatedWorldStateRegistry.RestoreDefaultContributorsForTests();
    }

    [Test]
    public void CaptureJson_StoresMetadataAndSortedEntries()
    {
        FakeContributor late = new FakeContributor("lootContainers", 1, 300, "loot");
        FakeContributor early = new FakeContributor("characters", 1, 0, "characters");
        ReplicatedWorldStateRegistry.SetContributorsForTests(late, early);

        string json = ReplicatedWorldStateRegistry.CaptureJson("Maison", 42ul);
        ReplicatedWorldStateEnvelope envelope = JsonUtility.FromJson<ReplicatedWorldStateEnvelope>(json);

        Assert.That(envelope, Is.Not.Null);
        Assert.That(envelope.schemaVersion, Is.EqualTo(ReplicatedWorldStateRegistry.CurrentSchemaVersion));
        Assert.That(envelope.sceneName, Is.EqualTo("Maison"));
        Assert.That(envelope.sequence, Is.EqualTo(42ul));
        Assert.That(envelope.entries, Has.Count.EqualTo(2));
        Assert.That(envelope.entries[0].key, Is.EqualTo("characters"));
        Assert.That(envelope.entries[1].key, Is.EqualTo("lootContainers"));
    }

    [Test]
    public void TryApplyJson_AppliesContributorsInOrder()
    {
        List<string> applyOrder = new List<string>();
        FakeContributor late = new FakeContributor("lootContainers", 1, 300, "loot", applyOrder);
        FakeContributor early = new FakeContributor("characters", 1, 0, "characters", applyOrder);
        ReplicatedWorldStateRegistry.SetContributorsForTests(late, early);

        string json = ReplicatedWorldStateRegistry.CaptureJson("Maison", 7ul);
        bool applied = ReplicatedWorldStateRegistry.TryApplyJson(json, out string diagnostic);

        Assert.That(applied, Is.True, diagnostic);
        CollectionAssert.AreEqual(new[] { "characters", "lootContainers" }, applyOrder);
    }

    [Test]
    public void TryApplyJson_FailsWhenContributorVersionDoesNotMatch()
    {
        FakeContributor contributor = new FakeContributor("characters", 2, 0, "characters");
        ReplicatedWorldStateRegistry.SetContributorsForTests(contributor);

        ReplicatedWorldStateEnvelope envelope = new ReplicatedWorldStateEnvelope
        {
            sceneName = "Maison",
            sequence = 99ul,
            entries = new List<ReplicatedWorldStateEntry>
            {
                new ReplicatedWorldStateEntry
                {
                    key = "characters",
                    version = 1,
                    applyOrder = 0,
                    payload = "payload"
                }
            }
        };

        bool applied = ReplicatedWorldStateRegistry.TryApplyJson(JsonUtility.ToJson(envelope), out string diagnostic);

        Assert.That(applied, Is.False);
        StringAssert.Contains("version incompatible", diagnostic);
    }

    private sealed class FakeContributor : IReplicatedWorldStateContributor
    {
        private readonly string payload;
        private readonly List<string> applyOrder;

        public FakeContributor(string stateKey, int schemaVersion, int applyOrder, string payload, List<string> appliedKeys = null)
        {
            StateKey = stateKey;
            SchemaVersion = schemaVersion;
            ApplyOrder = applyOrder;
            this.payload = payload;
            this.applyOrder = appliedKeys;
        }

        public string StateKey { get; }
        public int SchemaVersion { get; }
        public int ApplyOrder { get; }

        public string CaptureState()
        {
            return payload;
        }

        public bool TryApplyState(string payload, out string diagnostic)
        {
            applyOrder?.Add(StateKey);
            diagnostic = "ok";
            return true;
        }
    }
}
