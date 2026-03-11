using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public interface IReplicatedWorldStateContributor
{
    string StateKey { get; }
    int SchemaVersion { get; }
    int ApplyOrder { get; }

    string CaptureState();
    bool TryApplyState(string payload, out string diagnostic);
}

[Serializable]
public class ReplicatedWorldStateEnvelope
{
    public int schemaVersion = ReplicatedWorldStateRegistry.CurrentSchemaVersion;
    public string sceneName;
    public ulong sequence;
    public List<ReplicatedWorldStateEntry> entries = new List<ReplicatedWorldStateEntry>();
}

[Serializable]
public class ReplicatedWorldStateEntry
{
    public string key;
    public int version;
    public int applyOrder;
    public string payload;
}

public static class ReplicatedWorldStateRegistry
{
    public const int CurrentSchemaVersion = 1;

    private static readonly Dictionary<string, IReplicatedWorldStateContributor> contributorsByKey = new Dictionary<string, IReplicatedWorldStateContributor>(StringComparer.Ordinal);
    private static readonly List<IReplicatedWorldStateContributor> orderedContributors = new List<IReplicatedWorldStateContributor>();
    private static bool defaultsRegistered;

    public static void RegisterContributor(IReplicatedWorldStateContributor contributor)
    {
        if (contributor == null || string.IsNullOrWhiteSpace(contributor.StateKey))
        {
            return;
        }

        if (contributorsByKey.TryGetValue(contributor.StateKey, out IReplicatedWorldStateContributor existing))
        {
            orderedContributors.Remove(existing);
        }

        contributorsByKey[contributor.StateKey] = contributor;
        orderedContributors.Add(contributor);
        orderedContributors.Sort((left, right) =>
        {
            int orderCompare = left.ApplyOrder.CompareTo(right.ApplyOrder);
            if (orderCompare != 0)
            {
                return orderCompare;
            }

            return string.Compare(left.StateKey, right.StateKey, StringComparison.Ordinal);
        });
    }

    public static string CaptureJson(string sceneName)
    {
        return CaptureJson(sceneName, 0ul);
    }

    public static string CaptureJson(string sceneName, ulong sequence)
    {
        EnsureDefaultsRegistered();

        ReplicatedWorldStateEnvelope envelope = new ReplicatedWorldStateEnvelope
        {
            sceneName = string.IsNullOrWhiteSpace(sceneName) ? string.Empty : sceneName.Trim(),
            sequence = sequence
        };

        for (int i = 0; i < orderedContributors.Count; i++)
        {
            IReplicatedWorldStateContributor contributor = orderedContributors[i];
            if (contributor == null)
            {
                continue;
            }

            string payload = contributor.CaptureState();
            if (string.IsNullOrWhiteSpace(payload))
            {
                continue;
            }

            envelope.entries.Add(new ReplicatedWorldStateEntry
            {
                key = contributor.StateKey,
                version = contributor.SchemaVersion,
                applyOrder = contributor.ApplyOrder,
                payload = payload
            });
        }

        return JsonUtility.ToJson(envelope);
    }

    public static bool TryApplyJson(string json, out string diagnostic)
    {
        EnsureDefaultsRegistered();

        diagnostic = string.Empty;
        if (string.IsNullOrWhiteSpace(json))
        {
            return true;
        }

        ReplicatedWorldStateEnvelope envelope = JsonUtility.FromJson<ReplicatedWorldStateEnvelope>(json);
        if (envelope == null)
        {
            diagnostic = "enveloppe invalide";
            return false;
        }

        if (envelope.schemaVersion != CurrentSchemaVersion)
        {
            diagnostic = $"schemaVersion incompatible ({envelope.schemaVersion})";
            return false;
        }

        if (envelope.entries == null || envelope.entries.Count == 0)
        {
            diagnostic = $"sequence={envelope.sequence} entries=0";
            return true;
        }

        List<ReplicatedWorldStateEntry> entries = new List<ReplicatedWorldStateEntry>(envelope.entries);
        entries.Sort((left, right) =>
        {
            int orderCompare = left.applyOrder.CompareTo(right.applyOrder);
            if (orderCompare != 0)
            {
                return orderCompare;
            }

            return string.Compare(left.key, right.key, StringComparison.Ordinal);
        });

        int unresolvedCount = 0;
        int failedCount = 0;
        StringBuilder builder = new StringBuilder();
        builder.Append("sequence=").Append(envelope.sequence);
        if (!string.IsNullOrWhiteSpace(envelope.sceneName))
        {
            builder.Append(" scene=").Append(envelope.sceneName);
        }

        for (int i = 0; i < entries.Count; i++)
        {
            ReplicatedWorldStateEntry entry = entries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.key))
            {
                unresolvedCount++;
                continue;
            }

            builder.Append("; ").Append(entry.key).Append("(v").Append(entry.version).Append(")=");

            if (!contributorsByKey.TryGetValue(entry.key, out IReplicatedWorldStateContributor contributor) || contributor == null)
            {
                unresolvedCount++;
                builder.Append("contributeur manquant");
                continue;
            }

            if (contributor.SchemaVersion != entry.version)
            {
                failedCount++;
                builder.Append("version incompatible");
                continue;
            }

            bool applied = contributor.TryApplyState(entry.payload, out string entryDiagnostic);
            if (!applied)
            {
                failedCount++;
            }

            builder.Append(string.IsNullOrWhiteSpace(entryDiagnostic) ? (applied ? "ok" : "echec") : entryDiagnostic);
        }

        diagnostic = builder.ToString();
        return unresolvedCount == 0 && failedCount == 0;
    }

    public static void SetContributorsForTests(params IReplicatedWorldStateContributor[] contributors)
    {
        contributorsByKey.Clear();
        orderedContributors.Clear();
        defaultsRegistered = true;

        if (contributors == null)
        {
            return;
        }

        for (int i = 0; i < contributors.Length; i++)
        {
            RegisterContributor(contributors[i]);
        }
    }

    public static void RestoreDefaultContributorsForTests()
    {
        contributorsByKey.Clear();
        orderedContributors.Clear();
        defaultsRegistered = false;
        EnsureDefaultsRegistered();
    }

    private static void EnsureDefaultsRegistered()
    {
        if (defaultsRegistered)
        {
            return;
        }

        defaultsRegistered = true;
        RegisterContributor(new CharacterStateWorldStateContributor());
        RegisterContributor(new BuilderWorldStateContributor());
        RegisterContributor(new BraseroWorldStateContributor());
        RegisterContributor(new LeverWorldStateContributor());
        RegisterContributor(new LootContainerWorldStateContributor());
    }

    private sealed class CharacterStateWorldStateContributor : IReplicatedWorldStateContributor
    {
        public string StateKey => "characters";
        public int SchemaVersion => 1;
        public int ApplyOrder => 0;

        public string CaptureState()
        {
            return CharacterStateStore.Instance != null
                ? CharacterStateStore.Instance.BuildSessionSnapshotJson()
                : string.Empty;
        }

        public bool TryApplyState(string payload, out string diagnostic)
        {
            CharacterStateStore store = CharacterStateStore.Instance;
            if (store == null)
            {
                diagnostic = "CharacterStateStore absent";
                return false;
            }

            bool applied = store.ApplySessionSnapshotJson(payload);
            diagnostic = applied ? "ok" : "snapshot personnages non applique";
            return applied;
        }
    }

    private sealed class BuilderWorldStateContributor : IReplicatedWorldStateContributor
    {
        public string StateKey => "builderControllers";
        public int SchemaVersion => 1;
        public int ApplyOrder => 100;

        public string CaptureState()
        {
            return BuilderControllerSessionSnapshot.CaptureJson();
        }

        public bool TryApplyState(string payload, out string diagnostic)
        {
            bool applied = BuilderControllerSessionSnapshot.TryApplyJson(
                payload,
                out int appliedControllers,
                out int unresolvedControllers,
                out int appliedBuildings);
            diagnostic = $"controllers={appliedControllers} buildings={appliedBuildings} unresolved={unresolvedControllers}";
            return applied;
        }
    }

    private sealed class BraseroWorldStateContributor : IReplicatedWorldStateContributor
    {
        public string StateKey => "braseros";
        public int SchemaVersion => 1;
        public int ApplyOrder => 200;

        public string CaptureState()
        {
            return BraseroSessionSnapshot.CaptureJson();
        }

        public bool TryApplyState(string payload, out string diagnostic)
        {
            bool applied = BraseroSessionSnapshot.TryApplyJson(payload, out int appliedCount, out int unresolvedCount);
            diagnostic = $"applied={appliedCount} unresolved={unresolvedCount}";
            return applied;
        }
    }

    private sealed class LeverWorldStateContributor : IReplicatedWorldStateContributor
    {
        public string StateKey => "levers";
        public int SchemaVersion => 1;
        public int ApplyOrder => 210;

        public string CaptureState()
        {
            return LeverSessionSnapshot.CaptureJson();
        }

        public bool TryApplyState(string payload, out string diagnostic)
        {
            bool applied = LeverSessionSnapshot.TryApplyJson(payload, out int appliedCount, out int unresolvedCount);
            diagnostic = $"applied={appliedCount} unresolved={unresolvedCount}";
            return applied;
        }
    }

    private sealed class LootContainerWorldStateContributor : IReplicatedWorldStateContributor
    {
        public string StateKey => "lootContainers";
        public int SchemaVersion => 1;
        public int ApplyOrder => 300;

        public string CaptureState()
        {
            return LootContainerSessionSnapshot.CaptureJson();
        }

        public bool TryApplyState(string payload, out string diagnostic)
        {
            bool applied = LootContainerSessionSnapshot.TryApplyJson(payload, out int appliedCount, out int unresolvedCount);
            diagnostic = $"applied={appliedCount} unresolved={unresolvedCount}";
            return applied;
        }
    }
}
