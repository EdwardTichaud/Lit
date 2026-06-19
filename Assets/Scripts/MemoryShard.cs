using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
[AddComponentMenu("Lit/Munin/Memory Shard")]
public sealed class MemoryShard : MuninChargeReward
{
    protected override void Reset()
    {
        base.Reset();
        ConfigureDefaults(
            MuninChargeRewardType.MemoryShard,
            amount: 1,
            refill: false,
            interact: true,
            trigger: false,
            requirements: false,
            ghost: false,
            consume: true,
            cooldown: 0f,
            requireLight: true,
            hideWhenConsumed: true);
    }
}
