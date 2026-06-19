using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
[AddComponentMenu("Lit/Munin/Vigil Altar")]
public sealed class VigilAltar : MuninChargeReward
{
    protected override void Reset()
    {
        base.Reset();
        ConfigureDefaults(
            MuninChargeRewardType.VigilAltar,
            amount: 5,
            refill: true,
            interact: true,
            trigger: false,
            requirements: false,
            ghost: false,
            consume: false,
            cooldown: 60f,
            requireLight: false,
            hideWhenConsumed: false);
    }
}
