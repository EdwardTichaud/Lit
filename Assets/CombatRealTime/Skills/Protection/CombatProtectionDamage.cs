using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public readonly struct ProtectedDamage
{
    public readonly int VictimDamage, TransferDamage;
    public readonly SquadCharacterController Protector;
    public ProtectedDamage(int victim, SquadCharacterController protector, int transfer)
    { VictimDamage = victim; Protector = protector; TransferDamage = transfer; }
}

/// <summary>All protections are evaluated before either health store is changed.</summary>
public static class CombatProtectionDamage
{
    public static bool HasAuthority => NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsServer;
    public static ProtectedDamage Resolve(SquadCharacterController victim, float amount,
        PlayerProtectionEffects guardOwner = null, float guardPrevented = 0)
    {
        if (!HasAuthority || victim == null || !victim.isActiveAndEnabled || victim.CurrentHp <= 0 || amount < 0 || amount == 0 && guardPrevented <= 0) return new ProtectedDamage(0, null, 0);
        if (victim.CharacterData != null && victim.CharacterData.isEnemy) return new ProtectedDamage(Mathf.RoundToInt(amount), null, 0);
        var effects = Object.FindObjectsByType<PlayerProtectionEffects>(FindObjectsInactive.Exclude);
        var credits = new Dictionary<PlayerProtectionEffects, float>();
        if (guardOwner != null && guardPrevented > 0) credits[guardOwner] = guardPrevented;
        float damage = Reduce(victim, amount, effects, credits);
        PlayerProtectionEffects link = null;
        foreach (var effect in effects)
            if (effect.HasLink && effect.LinkedAlly == victim && effect.Actor != victim &&
                (link == null || effect.LinkRemaining > link.LinkRemaining)) link = effect;
        float transferred = link != null ? damage * link.LinkSkill.protection.transferFraction : 0;
        float reducedTransfer = link != null ? Reduce(link.Actor, transferred, effects, credits) : 0;
        int victimDamage = Mathf.RoundToInt(damage - transferred), transferDamage = Mathf.RoundToInt(reducedTransfer);
        float theoretical = 0;
        foreach (var credit in credits) theoretical += credit.Value;
        // Rounding must not charge the riposte when no HP was actually saved.
        float actual = Mathf.Max(0, amount + guardPrevented - victimDamage - transferDamage);
        if (theoretical > 0)
            foreach (var credit in credits) credit.Key.AddPrevented(credit.Value * actual / theoretical);
        return new ProtectedDamage(victimDamage, link != null ? link.Actor : null, transferDamage);
    }
    private static void Credit(Dictionary<PlayerProtectionEffects, float> credits, PlayerProtectionEffects owner, float amount)
    { credits[owner] = (credits.TryGetValue(owner, out float previous) ? previous : 0) + amount; }
    private static float Reduce(SquadCharacterController victim, float damage, PlayerProtectionEffects[] effects, Dictionary<PlayerProtectionEffects, float> credits)
    {
        PlayerProtectionEffects aura = null;
        foreach (var effect in effects)
            if (effect.HasAura && Vector3.Distance(victim.transform.position, effect.transform.position) <= effect.AuraSkill.protection.radius &&
                (aura == null || effect.AuraSkill.protection.reduction > aura.AuraSkill.protection.reduction)) aura = effect;
        if (aura != null)
        {
            float prevented = damage * aura.AuraSkill.protection.reduction;
            damage -= prevented; Credit(credits, aura, prevented);
        }
        var own = victim.GetComponent<PlayerProtectionEffects>();
        if (own != null && own.HasLink)
        {
            float prevented = damage * own.LinkSkill.protection.reduction;
            damage -= prevented; Credit(credits, own, prevented);
        }
        return damage;
    }
    public static int Apply(SquadCharacterController victim, int amount, string source)
        => ApplyAmount(victim, amount, source);

    public static int ApplyAttack(SquadCharacterController victim, int amount, string source, bool guarded, bool guardFeedback = true)
    {
        if (!HasAuthority || victim == null || victim.CurrentHp <= 0) return 0;
        float remaining = guarded ? PlayerGuardState.ReduceAmount(victim, amount, guardFeedback, recordReserve: false) : amount;
        return ApplyAmount(victim, remaining, source, guarded ? PlayerProtectionEffects.Get(victim) : null, amount - remaining);
    }
    private static int ApplyAmount(SquadCharacterController victim, float amount, string source, PlayerProtectionEffects guardOwner = null, float guardPrevented = 0)
    {
        if (victim == null) return 0;
        var result = Resolve(victim, amount, guardOwner, guardPrevented);
        // Commit both precomputed shares even if the first health mutation kills a participant.
        int applied = victim.ApplyUnprotectedDamage(result.VictimDamage, source);
        if (result.Protector != null) result.Protector.ApplyUnprotectedDamage(result.TransferDamage, "Intercession: " + source);
        return applied;
    }
}
