using UnityEngine;

/// <summary>Guard state belongs to the victim, never to the local input singleton.</summary>
[DisallowMultipleComponent]
public sealed class PlayerGuardState : MonoBehaviour
{
    public bool IsHeld { get; private set; }
    public float DamageMultiplier { get; private set; } = .5f;
    private CounterSkillCombatController owner;
    public void Set(bool held, float multiplier, CounterSkillCombatController controller = null)
    {
        IsHeld = held; DamageMultiplier = Mathf.Clamp01(multiplier); owner = controller;
        if (!CombatProtectionDamage.HasAuthority) GetComponentInParent<NetworkCharacterInput>()?.SubmitProtectionGuard(held);
    }
    public static bool IsGuarding(SquadCharacterController victim) => victim != null &&
        victim.GetComponent<PlayerGuardState>() is PlayerGuardState guard && guard.IsHeld &&
        (guard.owner == null || guard.owner.IsGuardHeld && !guard.owner.IsCinematicPlaying);
    public static int Reduce(SquadCharacterController victim, int damage, bool feedback = true)
        => Mathf.CeilToInt(ReduceAmount(victim, damage, feedback));

    public static float ReduceAmount(SquadCharacterController victim, float damage, bool feedback = true, bool recordReserve = true)
    {
        if (!IsGuarding(victim)) return damage;
        var guard = victim.GetComponent<PlayerGuardState>();
        float reduced = Mathf.Max(0, damage) * guard.DamageMultiplier;
        if (recordReserve && CombatProtectionDamage.HasAuthority) victim.GetComponent<PlayerProtectionEffects>()?.AddPrevented(damage - reduced);
        if (feedback && guard.owner != null) guard.owner.PlayActorGuardedFeedback();
        return reduced;
    }
    private void OnDisable() { IsHeld = false; owner = null; }
}
