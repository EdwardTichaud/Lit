using System.Collections.Generic;
using UnityEngine;

/// <summary>One reveal envelope per protected target, shared by every occluding layer.</summary>
public sealed class LitTacticalMaskStack
{
    public sealed class Layer
    {
        public float playerStrength, enemyStrength;
        internal float playerSeen = float.NegativeInfinity, enemySeen = float.NegativeInfinity;
        internal bool compatible;
    }
    private readonly Dictionary<LitCameraOcclusionGroup, Layer> layers = new Dictionary<LitCameraOcclusionGroup, Layer>();
    private readonly List<LitCameraOcclusionGroup> expired = new List<LitCameraOcclusionGroup>();
    private float playerReveal, enemyReveal;
    public IReadOnlyDictionary<LitCameraOcclusionGroup, Layer> Layers => layers;
    public float PlayerReveal => playerReveal;
    public bool Contains(LitCameraOcclusionGroup group) => group != null && layers.ContainsKey(group);

    public void Mark(LitCameraOcclusionGroup group, bool enemy, float now)
    {
        if (group == null || !group.CanBypassCameraCollision()) return;
        if (!layers.TryGetValue(group, out var layer)) { layer = new Layer(); layers.Add(group, layer); }
        // A new wall/LOD behind an already open wall inherits its opening now,
        // rather than hiding the character for another independent fade-in.
        if (enemy) { layer.enemySeen = now; layer.enemyStrength = enemyReveal; }
        else { layer.playerSeen = now; layer.playerStrength = playerReveal; }
    }
    public void ResetEnemy()
    {
        enemyReveal = 0;
        foreach (var layer in layers.Values) { layer.enemySeen = float.NegativeInfinity; layer.enemyStrength = 0; }
    }
    public void Advance(float now, float dt, float fadeTime, float restoreDelay, bool hasEnemy)
    {
        bool playerBlocked = false, enemyBlocked = false;
        foreach (var pair in layers)
        {
            var layer = pair.Value;
            layer.compatible = pair.Key != null && pair.Key.isActiveAndEnabled && pair.Key.IsMaskCompatible(out _);
            playerBlocked |= layer.compatible && now - layer.playerSeen <= restoreDelay;
            enemyBlocked |= layer.compatible && hasEnemy && now - layer.enemySeen <= restoreDelay;
        }
        float step = Mathf.Clamp(dt, 0, .1f) / Mathf.Max(.01f, fadeTime);
        playerReveal = Mathf.MoveTowards(playerReveal, playerBlocked ? 1 : 0, step);
        enemyReveal = Mathf.MoveTowards(enemyReveal, enemyBlocked ? 1 : 0, step);
        expired.Clear();
        foreach (var pair in layers)
        {
            var layer = pair.Value;
            layer.playerStrength = layer.compatible && now - layer.playerSeen <= restoreDelay
                ? playerReveal : Mathf.MoveTowards(layer.playerStrength, 0, step);
            layer.enemyStrength = layer.compatible && hasEnemy && now - layer.enemySeen <= restoreDelay
                ? enemyReveal : Mathf.MoveTowards(layer.enemyStrength, 0, step);
            if (!layer.compatible || (layer.playerStrength == 0 && layer.enemyStrength == 0 &&
                now - Mathf.Max(layer.playerSeen, layer.enemySeen) > restoreDelay)) expired.Add(pair.Key);
        }
        foreach (var group in expired) layers.Remove(group);
    }
    public void Clear() { layers.Clear(); expired.Clear(); playerReveal = enemyReveal = 0; }
}
