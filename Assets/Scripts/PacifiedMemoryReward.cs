using UnityEngine;

[AddComponentMenu("Lit/Munin/Pacified Memory Reward")]
public sealed class PacifiedMemoryReward : MuninChargeReward
{
    protected override void Reset()
    {
        base.Reset();
        ConfigureDefaults(
            MuninChargeRewardType.PacifiedMemory,
            amount: 3,
            refill: false,
            interact: false,
            trigger: false,
            requirements: true,
            ghost: true,
            consume: true,
            cooldown: 0f,
            requireLight: false,
            hideWhenConsumed: false);
    }
}
