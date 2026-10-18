using System.Collections.Generic;
using UnityEngine;

/// <summary>Session-owned threats, keyed by actor rather than by the local camera/player.</summary>
public sealed class CombatEngagementLedger
{
    private readonly Dictionary<Transform, Dictionary<Transform, ICombatTarget>> actors =
        new Dictionary<Transform, Dictionary<Transform, ICombatTarget>>();

    public int Count(Transform actor) => actor != null && actors.TryGetValue(actor, out var threats) ? threats.Count : 0;

    public bool Add(Transform actor, ICombatTarget threat)
    {
        if (actor == null || !SkillTargetingContract.Alive(threat) || actor == threat.Root) return false;
        if (!actors.TryGetValue(actor, out var threats))
            actors.Add(actor, threats = new Dictionary<Transform, ICombatTarget>());
        if (threats.ContainsKey(threat.Root)) return false;
        threats.Add(threat.Root, threat);
        return true;
    }

    public List<ICombatTarget> Snapshot(Transform actor) => actor != null && actors.TryGetValue(actor, out var threats)
        ? new List<ICombatTarget>(threats.Values) : new List<ICombatTarget>();

    public List<Transform> Prune(System.Func<Transform, ICombatTarget, bool> keep)
    {
        var changed = new List<Transform>();
        foreach (var pair in new List<KeyValuePair<Transform, Dictionary<Transform, ICombatTarget>>>(actors))
        {
            var removed = new List<Transform>();
            foreach (var threat in pair.Value)
                if (pair.Key == null || !SkillTargetingContract.Alive(threat.Value) || !keep(pair.Key, threat.Value)) removed.Add(threat.Key);
            if (removed.Count == 0) continue;
            foreach (var root in removed) pair.Value.Remove(root);
            if (pair.Value.Count == 0) actors.Remove(pair.Key);
            changed.Add(pair.Key);
        }
        return changed;
    }

    public List<Transform> Clear()
    {
        var changed = new List<Transform>(actors.Keys);
        actors.Clear();
        return changed;
    }
}
