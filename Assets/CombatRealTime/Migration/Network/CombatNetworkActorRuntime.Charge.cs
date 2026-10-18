using Unity.Netcode;
using UnityEngine;

namespace Lit.Combat.Migration.Network
{
    public sealed partial class CombatNetworkActorRuntime
    {
        private readonly NetworkVariable<int> chargeSkill = new NetworkVariable<int>();
        private bool chargeHeld, chargeCommit, chargeRequiresRelease;
        private double chargePressedAt;
        private int chargePreparation;
        private NetworkObject serverManualLock, sentManualLock;

        public bool RequestChargeHold(bool held)
        {
            if (!IsSpawned || !IsOwner || !IsAssigned(OwnerClientId)) return false;
            ChargeHoldServerRpc(held);
            return true;
        }

        [ServerRpc]
        private void ChargeHoldServerRpc(bool held, ServerRpcParams parameters = default)
        {
            if (!IsServer || !IsAssigned(parameters.Receive.SenderClientId)) return;
            if (!held) { chargeRequiresRelease = false; StopCharge(); return; }
            var skill = actor != null ? actor.CharacterData?.chargedSkill : null;
            if (chargeHeld || chargeRequiresRelease || skill == null || !Owns(skill) || !IsAlive() ||
                bridge == null || !bridge.Grounded || !hasAction || activeFamily != CombatNetworkSkillFamily.GroundBasic ||
                authority.GetCooldownRemaining(NetworkObject, skill) > 0) return;
            chargeHeld = true;
            chargePressedAt = Time.unscaledTimeAsDouble;
        }

        private bool CanCommitChargedSkill(ChargedSkillSO skill) => chargeCommit && skill == actor.CharacterData.chargedSkill;

        private void TickCharge()
        {
            if (!IsServer) { ApplyChargePresentation(); return; }
            if (!chargeHeld) return;
            var skill = actor != null ? actor.CharacterData?.chargedSkill : null;
            if (!admitted || !IsAlive() || !IsAssigned(OwnerClientId) || bridge == null || !bridge.Grounded ||
                skill == null || authority == null || authority.IsCinematicParticipant(NetworkObject))
            { StopCharge(); chargeRequiresRelease = true; return; }
            if (hasAction)
            {
                if (activeFamily != CombatNetworkSkillFamily.GroundBasic) { StopCharge(); chargeRequiresRelease = true; }
                return;
            }
            if (chargePreparation == 0)
            {
                if (presentation.IsActionActive || presentation.LastEndReason != PlayerActionPresentationController.ActionEndReason.Completed)
                { StopCharge(); chargeRequiresRelease = true; return; }
                chargePreparation = presentation.BeginChargedPreparation(skill, IsOwner);
                if (chargePreparation == 0) { StopCharge(); chargeRequiresRelease = true; return; }
                if (!catalog.TryGet(skill, out var entry)) { StopCharge(); return; }
                chargeSkill.Value = entry.Id;
            }
            if (!presentation.IsCurrentSession(chargePreparation)) { StopCharge(); chargeRequiresRelease = true; return; }
            if (Time.unscaledTimeAsDouble - chargePressedAt <= skill.chargeSeconds) return;
            StopCharge();
            chargeRequiresRelease = true;
            chargeCommit = true;
            try
            {
                if (catalog.TryGet(skill, out var entry))
                {
                    var result = authority.TryExecuteContinuation(NetworkObject, entry.Id, transform.forward, out _);
                    if (result != CombatCommandRejection.None) Diagnose("Charged continuation rejected: " + result);
                }
            }
            finally { chargeCommit = false; }
        }

        private void StopCharge()
        {
            chargeHeld = false;
            int session = chargePreparation; chargePreparation = 0;
            if (session != 0 && presentation != null) presentation.EndChargedPreparation(session);
            if (IsServer && IsSpawned) chargeSkill.Value = 0;
        }

        private void OnChargeChanged(int before, int after) { if (!IsServer) ApplyChargePresentation(); }
        private void ApplyChargePresentation()
        {
            if (IsServer || !IsSpawned || presentation == null) return;
            if (chargeSkill.Value == 0)
            {
                int session = chargePreparation; chargePreparation = 0;
                presentation.EndChargedPreparation(session);
                return;
            }
            if (chargePreparation != 0 || presentation.IsActionActive || !catalogValid ||
                !catalog.TryGet(chargeSkill.Value, out var entry) || !(entry.skill is ChargedSkillSO skill)) return;
            chargePreparation = presentation.BeginChargedPreparation(skill, IsOwner);
        }

        private void PublishManualLockIntent(bool force = false)
        {
            if (!IsOwner || actor == null) return;
            var manager = RealTimeCombatManager.Instance;
            if (manager == null || manager.PlayerRoot != transform) return;
            var root = manager.LockedCombatTarget?.Root;
            var identity = root != null ? root.GetComponentInParent<NetworkObject>() : null;
            if (!force && identity == sentManualLock) return;
            sentManualLock = identity;
            SetManualLockServerRpc(identity != null, identity != null ? new NetworkObjectReference(identity) : default);
        }

        [ServerRpc]
        private void SetManualLockServerRpc(bool hasTarget, NetworkObjectReference reference, ServerRpcParams parameters = default)
        {
            if (!IsServer || !IsAssigned(parameters.Receive.SenderClientId)) return;
            serverManualLock = null;
            if (hasTarget && reference.TryGet(out var identity, NetworkManager) && authority != null && authority.IsRegistered(identity) &&
                ResolveEnemy(identity) is ICombatTarget target && !target.IsDead) serverManualLock = identity;
        }
    }
}
