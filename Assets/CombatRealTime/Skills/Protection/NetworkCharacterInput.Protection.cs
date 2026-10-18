using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public partial class NetworkCharacterInput
{
    private readonly Dictionary<SkillSO, float> protectionReadyAt = new Dictionary<SkillSO, float>();
    private bool protectionPending;
    private float protectionPendingUntil, nextProtectionSync;
    private int lastProtectionHp = -1;
    private bool protectionRemoteGuard;
    private int protectionGuardGeneration;

    private static SkillSO LoadProtection(string name) => string.IsNullOrEmpty(name) || name.Length > 128 || name.Contains("/") || name.Contains("\\")
        ? null : Resources.Load<SkillSO>("CombatSupportSkills/" + name);

    public bool TryRequestProtectionSkill(SkillSO skill, SquadCharacterController ally, ICombatTarget enemy)
    {
        var runtime = GetComponent<Lit.Combat.Migration.Network.CombatNetworkActorRuntime>();
        if (runtime != null)
        {
            Transform recipient = skill != null && skill.protection.kind == SkillProtectionKind.JusteRetour ? enemy?.Root : ally != null ? ally.transform : null;
            return runtime.TryRequestSkill(skill, recipient != null ? recipient.GetComponentInParent<NetworkObject>() : null, transform.forward);
        }
        if (!IsOwner || !IsSpawned || protectionPending || skill == null || !skill.IsProtectionSkill || LoadProtection(skill.name) != skill) return false;
        var identity = skill.protection.kind == SkillProtectionKind.JusteRetour ? enemy?.Root?.GetComponentInParent<NetworkObject>() :
            ally != null ? ally.GetComponentInParent<NetworkObject>() : GetComponent<NetworkObject>();
        if (identity == null || !identity.IsSpawned) return false;
        protectionPending = true; protectionPendingUntil = Time.unscaledTime + 5;
        UseProtectionServerRpc(skill.name, identity);
        return true;
    }
    [ServerRpc]
    private void UseProtectionServerRpc(string assetName, NetworkObjectReference target)
    {
        var skill = LoadProtection(assetName);
        bool accepted = false;
        if (GetComponent<Lit.Combat.Migration.Network.CombatNetworkActorRuntime>() == null &&
            skill != null && skill.IsProtectionSkill && controller != null && controller.CharacterData != null &&
            controller.CharacterData.combatSkills.Contains(skill) && SkillHealing.CanCast(controller) &&
            (!protectionReadyAt.TryGetValue(skill, out float ready) || Time.time >= ready) && target.TryGet(out var identity))
        {
            if (skill.protection.kind == SkillProtectionKind.JusteRetour)
            {
                var enemy = ResolveProtectionEnemy(identity);
                var strike = controller.GetComponent<PlayerJusticeStrike>();
                if (strike == null) strike = controller.gameObject.AddComponent<PlayerJusticeStrike>();
                accepted = strike.TryStart(skill, enemy);
                if (accepted) ProtectionStrikeClientRpc(assetName, target);
            }
            else accepted = SkillProtection.Activate(skill, controller, identity.GetComponentInChildren<SquadCharacterController>());
            if (accepted) protectionReadyAt[skill] = Time.time + skill.cooldownSeconds;
        }
        ProtectionResultClientRpc(assetName, accepted, new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } } });
    }
    private static ICombatTarget ResolveProtectionEnemy(NetworkObject identity)
    {
        if (identity == null) return null;
        var legacy = identity.GetComponentInChildren<EnemyController>();
        return legacy != null ? new LegacyCombatTarget(legacy) : identity.GetComponentInChildren<LitBrainsEnemy>();
    }
    [ClientRpc]
    private void ProtectionStrikeClientRpc(string assetName, NetworkObjectReference enemy)
    {
        if (IsServer || controller == null || !enemy.TryGet(out var identity)) return;
        var strike = controller.GetComponent<PlayerJusticeStrike>();
        if (strike == null) strike = controller.gameObject.AddComponent<PlayerJusticeStrike>();
        strike.TryStart(LoadProtection(assetName), ResolveProtectionEnemy(identity), cosmeticOnly: true);
    }
    [ClientRpc]
    private void ProtectionResultClientRpc(string assetName, bool accepted, ClientRpcParams parameters = default)
    {
        protectionPending = false;
        var skill = LoadProtection(assetName);
        if (accepted && controller != null && skill != null) RealTimeCombatManager.Instance?.ConfirmHealingSkill(skill, controller.transform, skill.cooldownSeconds);
    }
    private void TickProtectionNetwork()
    {
        if (protectionPending && Time.unscaledTime >= protectionPendingUntil) protectionPending = false;
        if (GetComponent<Lit.Combat.Migration.Network.CombatNetworkActorRuntime>() != null)
        {
            if (IsServer && protectionRemoteGuard && (controller == null || controller.CurrentHp <= 0 ||
                controller.GetComponent<PlayerActionPresentationController>()?.IsCurrentSession(protectionGuardGeneration) != true)) ClearRemoteProtectionGuard();
            return;
        }
        if (!IsSpawned || !IsServer || controller == null || Time.unscaledTime < nextProtectionSync) return;
        nextProtectionSync = Time.unscaledTime + .1f;
        if (controller.CurrentHp != lastProtectionHp)
        {
            lastProtectionHp = controller.CurrentHp;
            ProtectionHealthClientRpc(controller.CurrentHp, controller.MaxHp);
        }
        var effects = controller.GetComponent<PlayerProtectionEffects>();
        if (effects == null) return;
        var ally = effects.LinkedAlly != null ? effects.LinkedAlly.GetComponentInParent<NetworkObject>() : null;
        ProtectionStateClientRpc(effects.AuraSkill != null ? effects.AuraSkill.name : "",
            effects.HasLink ? effects.LinkSkill.name : "", ally != null && ally.IsSpawned ? new NetworkObjectReference(ally) : default,
            effects.LinkRemaining, effects.Reserve);
        if (!SkillHealing.CanCast(controller) && protectionRemoteGuard) ClearRemoteProtectionGuard();
    }
    [ClientRpc]
    private void ProtectionHealthClientRpc(int hp, int maximum)
    {
        if (IsServer || controller == null) return;
        int before = controller.CurrentHp;
        controller.SetHealth(hp, maximum);
        if (hp < before)
        {
            RealTimeCombatManager.Instance?.InterruptBasicComboOnDamage(controller.transform);
            CombatDamageWorldFeedback.Show(controller.transform, before - hp, new Color(1f, .48f, .48f), 2.05f);
        }
    }
    [ClientRpc]
    private void ProtectionStateClientRpc(string aura, string link, NetworkObjectReference ally, float remaining, float reserve)
    {
        if (IsServer || controller == null) return;
        PlayerProtectionEffects.Get(controller).ApplyRemoteState(LoadProtection(aura), LoadProtection(link),
            ally.TryGet(out var identity) ? identity.GetComponentInChildren<SquadCharacterController>() : null, remaining, reserve);
    }
    public void SubmitProtectionGuard(bool held, double inputTimestamp = double.NaN)
    {
        if (!IsSpawned || !IsOwner) return;
        var runtime = GetComponent<Lit.Combat.Migration.Network.CombatNetworkActorRuntime>();
        if (runtime != null) ProtectionGuardServerRpc(held, runtime.CaptureNetworkInputTime(inputTimestamp));
        else if (!IsServer) ProtectionGuardServerRpc(held, NetworkManager.ServerTime.Time);
    }
    internal void ClearActorCombatGuard() => ClearRemoteProtectionGuard();
    public void ResetJusticeReserveForCombatEnd()
    {
        if (!IsSpawned || !IsOwner) return;
        ResetJusticeReserveServerRpc();
    }
    [ServerRpc]
    private void ResetJusticeReserveServerRpc() => controller?.GetComponent<PlayerProtectionEffects>()?.ResetReserve();
    [ServerRpc]
    private void ProtectionGuardServerRpc(bool held, double pressedAt)
    {
        if (controller == null) return;
        var runtime = GetComponent<Lit.Combat.Migration.Network.CombatNetworkActorRuntime>();
        if (runtime != null)
        {
            if (!held) { ClearRemoteProtectionGuard(); return; }
            if (runtime.TryServerCounterReaction(pressedAt)) { ClearRemoteProtectionGuard(); return; }
            if (protectionRemoteGuard) return;
            var counter = CounterSkillCombatController.Instance;
            if (counter != null) protectionRemoteGuard = counter.TryBeginGuardForActor(controller, out protectionGuardGeneration);
            if (protectionRemoteGuard) runtime.PublishGuardPresentation(protectionGuardGeneration);
            return;
        }
        bool allowed = held && SkillHealing.CanCast(controller);
        var state = controller.GetComponent<PlayerGuardState>();
        if (state == null) state = controller.gameObject.AddComponent<PlayerGuardState>();
        state.Set(allowed, CounterSkillCombatController.Instance != null ? CounterSkillCombatController.Instance.GuardDamageMultiplier : .4f);
        protectionRemoteGuard = allowed;
    }
    private void ClearRemoteProtectionGuard()
    {
        int generation = protectionGuardGeneration; protectionGuardGeneration = 0;
        var presentation = controller != null ? controller.GetComponent<PlayerActionPresentationController>() : null;
        if (generation != 0 && presentation != null && presentation.IsCurrentSession(generation)) presentation.EndExternalPresentation(generation);
        if (protectionRemoteGuard && controller != null) controller.GetComponent<PlayerGuardState>()?.Set(false, .4f);
        protectionRemoteGuard = false;
    }
    private void ClearProtectionResources()
    {
        protectionPending = false; protectionReadyAt.Clear(); lastProtectionHp = -1;
        ClearRemoteProtectionGuard();
        if (controller != null && IsServer)
        {
            var effects = controller.GetComponent<PlayerProtectionEffects>();
            if (effects != null) { effects.enabled = false; }
        }
    }
}
