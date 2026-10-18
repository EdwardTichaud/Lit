using System;
using Lit.Combat.Migration.Network;
using Unity.Netcode;
using UnityEngine;

public partial class NetworkCharacterInput
{
    private ulong combatCommandSequence;

    // Result acknowledgement only. Never use this event to apply damage on a client.
    public event Action<ulong, CombatCommandRejection, CombatActionTicket> CombatCommandResult;

    public bool TryRequestCombatAction(SkillSO skill, NetworkObject target, Vector3 direction)
    {
        var runtime = GetComponent<CombatNetworkActorRuntime>();
        return runtime != null && runtime.TryGetSkillId(skill, out int id) && TryRequestCombatAction(id, target, direction);
    }

    // Compatibility with unchanged support RPCs: share their authoritative ready-at stores.
    // No local-manager cooldown is consulted or mutated here.
    internal bool IsLegacySupportReady(SkillSO skill)
    {
        if (skill == null) return false;
        var store = skill.IsHealingSkill ? healingReadyAt : skill.IsProtectionSkill ? protectionReadyAt : controlReadyAt;
        return !store.TryGetValue(skill, out float ready) || Time.time >= ready;
    }

    internal void MirrorAcceptedSupportCooldown(SkillSO skill)
    {
        if (!IsServer || skill == null || !(skill.IsHealingSkill || skill.IsProtectionSkill || skill.IsControlSkill)) return;
        var store = skill.IsHealingSkill ? healingReadyAt : skill.IsProtectionSkill ? protectionReadyAt : controlReadyAt;
        store[skill] = Mathf.Max(store.TryGetValue(skill, out float ready) ? ready : 0, Time.time + skill.cooldownSeconds);
    }

    public bool TryRequestCombatAction(int skillId, NetworkObject target, Vector3 direction)
    {
        if (!IsSpawned || !IsOwner || !IsAssignedToLocalClient() || IsGameplayInputBlocked() ||
            skillId <= 0 || !CombatCommandPolicy.Finite(direction) || combatCommandSequence == ulong.MaxValue ||
            (target != null && (!target.IsSpawned || target.NetworkManager != NetworkManager))) return false;
        CombatActionCommandServerRpc(++combatCommandSequence, skillId, target != null,
            target != null ? new NetworkObjectReference(target) : default, direction);
        return true;
    }

    [ServerRpc]
    private void CombatActionCommandServerRpc(ulong sequence, int skillId, bool hasTarget,
        NetworkObjectReference targetReference, Vector3 direction, ServerRpcParams rpcParams = default)
    {
        var runtime = GetComponent<CombatNetworkActorRuntime>();
        ulong sender = rpcParams.Receive.SenderClientId;
        if (runtime != null && runtime.TryBufferBasicCommand(sender, sequence, skillId, direction, accepted =>
        {
            if (accepted) ExecuteCombatActionCommand(sequence, skillId, hasTarget, targetReference, direction, sender);
            else CombatActionCommandResultClientRpc(sequence, (byte)CombatCommandRejection.ExecutorRejected, 0, 0, 0,
                "Buffered Basic discarded because its source session did not complete.",
                new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { sender } } });
        })) return;
        ExecuteCombatActionCommand(sequence, skillId, hasTarget, targetReference, direction, sender);
    }

    private void ExecuteCombatActionCommand(ulong sequence, int skillId, bool hasTarget,
        NetworkObjectReference targetReference, Vector3 direction, ulong sender)
    {
        var authority = NetworkManager != null ? NetworkManager.GetComponent<CombatCommandAuthority>() : null;
        CombatActionTicket ticket = default;
        NetworkObject target = null;
        CombatCommandRejection result;
        if (hasTarget && !targetReference.TryGet(out target, NetworkManager)) result = CombatCommandRejection.InvalidTarget;
        else result = authority != null
            ? authority.TryExecute(sender, NetworkObject, sequence, skillId, target, direction, out ticket)
            : CombatCommandRejection.UnregisteredActor;
        var runtime = GetComponent<CombatNetworkActorRuntime>();
        string diagnostic = (result == CombatCommandRejection.ExecutorRejected || result == CombatCommandRejection.ExecutorFailed) && runtime != null
            ? runtime.LastDiagnostic : result.ToString();
        if (diagnostic != null && diagnostic.Length > 256) diagnostic = diagnostic.Substring(0, 256);
        CombatActionCommandResultClientRpc(sequence, (byte)result, ticket.AuthorityEpoch, ticket.ActorId, ticket.Generation, diagnostic,
            new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { sender } } });
    }

    [ClientRpc]
    private void CombatActionCommandResultClientRpc(ulong sequence, byte result, ulong authorityEpoch,
        ulong actorId, ulong generation, string diagnostic, ClientRpcParams parameters = default)
    {
        if (result != (byte)CombatCommandRejection.None) GetComponent<CombatNetworkActorRuntime>()?.ReportCommandRejection((CombatCommandRejection)result, diagnostic);
        CombatCommandResult?.Invoke(sequence, (CombatCommandRejection)result,
            new CombatActionTicket(authorityEpoch, actorId, generation));
    }
}
