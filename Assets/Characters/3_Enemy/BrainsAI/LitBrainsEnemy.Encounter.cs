using Unity.Netcode;
using UnityEngine;

/// <summary>Encounter owners supply gates and filtering, never a second enemy brain.</summary>
public interface ILitBrainsEncounterPolicy
{
    bool AllowsDecisions { get; }
    bool AllowsDamage { get; }
    bool IsReturning { get; }
    bool IsFlameDormant { get; }
    bool FilterIncomingDamage(int requested, SquadCharacterController source, out int permitted);
}

public partial class LitBrainsEnemy
{
    private MonoBehaviour[] encounterPolicies;
    public void RefreshEncounterPolicies() => encounterPolicies = GetComponents<MonoBehaviour>();
    public bool HasSimulationAuthority
    {
        get
        {
            if (!CombatProtectionDamage.HasAuthority) return false;
            var network = NetworkManager.Singleton;
            if (network == null || !network.IsListening) return true;
            var identity = GetComponentInParent<NetworkObject>();
            return identity != null && identity.IsSpawned;
        }
    }

    private bool EncounterAllowsDecisions => EvaluateEncounterGate(false);
    private bool EncounterAllowsDamage => EvaluateEncounterGate(true);
    private bool EncounterIsReturning => HasEncounterState(false);
    private bool EncounterIsFlameDormant => HasEncounterState(true);

    private bool HasEncounterState(bool dormant)
    {
        if (encounterPolicies == null) RefreshEncounterPolicies();
        foreach (var component in encounterPolicies)
            if (component != null && component.enabled && component is ILitBrainsEncounterPolicy policy &&
                (dormant ? policy.IsFlameDormant : policy.IsReturning)) return true;
        return false;
    }

    private bool EvaluateEncounterGate(bool damage)
    {
        if (encounterPolicies == null) RefreshEncounterPolicies();
        foreach (var component in encounterPolicies)
        {
            if (component == null || !component.enabled || component == this) continue;
            if (component is ILitBrainsEncounterPolicy policy &&
                (policy.IsFlameDormant || !(damage ? policy.AllowsDamage : policy.AllowsDecisions))) return false;
            if (component is IBossEncounterBehaviour boss &&
                (boss.IsBossResolved || !damage && boss.SuppressDefaultEnemyBrain)) return false;
        }
        return true;
    }

    private bool FilterEncounterDamage(int requested, SquadCharacterController source, out int permitted)
    {
        permitted = Mathf.Max(0, requested);
        if (!EncounterAllowsDamage) return false;
        foreach (var component in encounterPolicies)
        {
            if (component == null || !component.enabled || component == this) continue;
            int next;
            if (component is ILitBrainsEncounterPolicy policy)
            {
                if (!policy.FilterIncomingDamage(permitted, source, out next)) { permitted = 0; return false; }
                permitted = Mathf.Clamp(next, 0, permitted);
            }
            if (component is IBossEncounterBehaviour boss)
            {
                if (!boss.FilterIncomingDamage(permitted, source, out next)) { permitted = 0; return false; }
                permitted = Mathf.Clamp(next, 0, permitted);
            }
        }
        return true;
    }

    private string CombatLocomotionState => combatProfile != null ? combatProfile.locomotionState : "Locomotion";
    private string CombatDeathState => combatProfile != null ? combatProfile.deathState : "Base Layer.Death";
    private string CombatKnockoutState => combatProfile != null ? combatProfile.knockoutState : "Knocked Out";
    private bool HasCombatState(string state) => anim != null && !string.IsNullOrWhiteSpace(state) &&
        (anim.HasState(0, Animator.StringToHash(state)) || anim.HasState(0, Animator.StringToHash("Base Layer." + state)));
    private void PlayCombatHurt()
    {
        string state = combatProfile != null ? combatProfile.hurtState : "Hurt";
        if (HasCombatState(state)) anim.CrossFadeInFixedTime(state, combatProfile != null ? combatProfile.hurtBlendSeconds : .04f, 0);
    }

    private void PlayCombatKnockout()
    {
        if (HasCombatState(CombatKnockoutState)) anim.CrossFadeInFixedTime(CombatKnockoutState, .04f, 0);
        else PlayCombatHurt();
    }
}
