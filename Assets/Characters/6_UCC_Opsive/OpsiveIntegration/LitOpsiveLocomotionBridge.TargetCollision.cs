using System;
using System.Collections.Generic;
using UnityEngine;

public partial class LitOpsiveLocomotionBridge
{
    public sealed class TargetCollisionHandle : IDisposable
    {
        private LitOpsiveLocomotionBridge bridge;
        internal readonly List<Collider> targets = new List<Collider>();
        internal readonly List<(Collider, Collider)> pairs = new List<(Collider, Collider)>();
        public bool IsReleased => bridge == null;
        public object Owner { get; }
        internal TargetCollisionHandle(LitOpsiveLocomotionBridge source, object owner) { bridge = source; Owner = owner; }
        public void Dispose()
        {
            var previous = bridge; bridge = null;
            if (previous != null) previous.ReleaseTargetCollision(this);
        }
    }
    private sealed class CollisionRecord { public int count; public bool previous; }
    private readonly Dictionary<Collider, CollisionRecord> bypassTargets = new Dictionary<Collider, CollisionRecord>();
    private readonly Dictionary<(Collider, Collider), CollisionRecord> bypassPairs = new Dictionary<(Collider, Collider), CollisionRecord>();
    private readonly List<TargetCollisionHandle> targetCollisionHandles = new List<TargetCollisionHandle>();
    public Collider[] ScriptedMotionColliders => locomotion != null ? locomotion.Colliders : Array.Empty<Collider>();
    public int TargetCollisionBypassCount => targetCollisionHandles.Count;

    public bool TryAcquireTargetCollisionBypass(object owner, Transform target, out TargetCollisionHandle handle)
    {
        handle = null; ResolveReferences();
        if (owner == null || target == null || locomotion == null || !isActiveAndEnabled || !locomotion.isActiveAndEnabled) return false;
        handle = new TargetCollisionHandle(this, owner);
        targetCollisionHandles.Add(handle);
        foreach (var collider in target.GetComponentsInChildren<Collider>())
        {
            if (collider.isTrigger || !collider.enabled || collider.transform.IsChildOf(transform)) continue;
            if (!bypassTargets.TryGetValue(collider, out var record))
            {
                bool ignored = false;
                for (int i = 0; i < locomotion.IgnoredColliderCount; i++) ignored |= locomotion.IgnoredColliders[i] == collider;
                record = new CollisionRecord { previous = ignored };
                bypassTargets.Add(collider, record);
                if (!ignored) locomotion.AddIgnoredCollider(collider);
            }
            record.count++; handle.targets.Add(collider);
            foreach (var body in ScriptedMotionColliders)
            {
                if (body == null || body.isTrigger || !body.enabled) continue;
                var pair = (body, collider);
                if (!bypassPairs.TryGetValue(pair, out var pairRecord))
                {
                    pairRecord = new CollisionRecord { previous = Physics.GetIgnoreCollision(body, collider) };
                    bypassPairs.Add(pair, pairRecord);
                    Physics.IgnoreCollision(body, collider, true);
                }
                pairRecord.count++; handle.pairs.Add(pair);
            }
        }
        return true;
    }
    private void ReleaseTargetCollision(TargetCollisionHandle handle)
    {
        if (!targetCollisionHandles.Remove(handle)) return;
        foreach (var pair in handle.pairs)
            if (bypassPairs.TryGetValue(pair, out var record) && --record.count == 0)
            {
                if (pair.Item1 != null && pair.Item2 != null) Physics.IgnoreCollision(pair.Item1, pair.Item2, record.previous);
                bypassPairs.Remove(pair);
            }
        foreach (var collider in handle.targets)
            if (bypassTargets.TryGetValue(collider, out var record) && --record.count == 0)
            {
                if (collider != null && locomotion != null && !record.previous) locomotion.RemoveIgnoredCollider(collider);
                bypassTargets.Remove(collider);
            }
    }
    private void ReleaseTargetCollisionBypasses()
    {
        foreach (var handle in targetCollisionHandles.ToArray()) handle.Dispose();
    }
}
