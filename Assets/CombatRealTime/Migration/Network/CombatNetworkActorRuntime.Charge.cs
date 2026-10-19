using Unity.Netcode;
using UnityEngine;

namespace Lit.Combat.Migration.Network
{
    public struct CombatNetworkChargeTailSnapshot : INetworkSerializable, System.IEquatable<CombatNetworkChargeTailSnapshot>
    {
        public ulong Epoch, Generation;
        public int SkillId;
        public float NormalizedTime;
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Epoch); serializer.SerializeValue(ref Generation);
            serializer.SerializeValue(ref SkillId); serializer.SerializeValue(ref NormalizedTime);
        }
        public bool Equals(CombatNetworkChargeTailSnapshot other) => Epoch == other.Epoch && Generation == other.Generation &&
            SkillId == other.SkillId && NormalizedTime.Equals(other.NormalizedTime);
        public bool Matches(ulong epoch, ulong generation) => SkillId != 0 && Epoch == epoch && Generation == generation;
    }

    public sealed partial class CombatNetworkActorRuntime
    {
        private readonly NetworkVariable<int> chargeSkill = new NetworkVariable<int>();
        private readonly NetworkVariable<CombatNetworkChargeTailSnapshot> chargeTailState = new NetworkVariable<CombatNetworkChargeTailSnapshot>();
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
                authority == null || authority.IsCinematicParticipant(NetworkObject) || presentation == null ||
                !presentation.IsCurrentSession(presentationSession) || !authority.IsCurrent(active.Ticket) ||
                !catalog.TryGet(skill, out var entry) || authority.GetCooldownRemaining(NetworkObject, skill) > 0) return;
            if (skill.preparationMode == ChargedPreparationMode.BasicTail)
            {
                if (!presentation.TryBeginChargedBasicTail(skill, presentationSession, IsOwner)) return;
                chargePreparation = presentationSession;
                PublishChargeTail(entry.Id);
                chargeSkill.Value = entry.Id;
            }
            chargeHeld = true;
            chargePressedAt = Time.unscaledTimeAsDouble;
        }

        private bool CanCommitChargedSkill(ChargedSkillSO skill) => chargeCommit && skill == actor.CharacterData.chargedSkill;

        private void TickCharge()
        {
            if (!IsServer) { ApplyChargePresentation(); return; }
            presentation?.UpdateExternalChargedBasicTail(presentationSession);
            if (!chargeHeld) return;
            var skill = actor != null ? actor.CharacterData?.chargedSkill : null;
            if (!admitted || !IsAlive() || !IsAssigned(OwnerClientId) || bridge == null || !bridge.Grounded ||
                skill == null || authority == null || authority.IsCinematicParticipant(NetworkObject))
            { StopCharge(); chargeRequiresRelease = true; return; }
            if (skill.preparationMode == ChargedPreparationMode.BasicTail)
            {
                if (!hasAction || activeFamily != CombatNetworkSkillFamily.GroundBasic || chargePreparation != presentationSession ||
                    !presentation.IsCurrentSession(chargePreparation) || !presentation.IsPreparingBasicTail || !authority.IsCurrent(active.Ticket))
                { StopCharge(); chargeRequiresRelease = true; return; }
                PublishChargeTail(chargeSkill.Value);
                if (!ChargeDurationReached(chargePressedAt, Time.unscaledTimeAsDouble, skill.chargeSeconds) ||
                    !presentation.CanLaunchFromChargedBasicTail(skill)) return;
                chargeRequiresRelease = true;
                chargeCommit = true;
                try
                {
                    if (catalog.TryGet(skill, out var entry))
                    {
                        var result = authority.TryExecuteContinuation(NetworkObject, entry.Id, transform.forward, out _);
                        if (result != CombatCommandRejection.None) Diagnose("Charged tail continuation rejected: " + result);
                    }
                }
                finally { chargeCommit = false; StopCharge(); }
                return;
            }
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
            if (!ChargeDurationReached(chargePressedAt, Time.unscaledTimeAsDouble, skill.chargeSeconds)) return;
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
            if (session != 0 && presentation != null)
            {
                presentation.ReleaseChargedBasicTail(session);
                presentation.EndChargedPreparation(session);
            }
            if (IsServer && IsSpawned) { chargeSkill.Value = 0; chargeTailState.Value = default; }
        }

        internal static bool ChargeDurationReached(double pressedAt, double now, float seconds) =>
            !double.IsNaN(pressedAt) && !double.IsInfinity(pressedAt) && !double.IsNaN(now) && !double.IsInfinity(now) &&
            CombatCommandPolicy.Finite(seconds) && seconds > 0 && now >= pressedAt && now - pressedAt >= seconds;

        private bool IsChargedTailReplacement(SkillSO skill) => chargeHeld && chargeCommit &&
            skill == actor?.CharacterData?.chargedSkill && skill is ChargedSkillSO charged &&
            charged.preparationMode == ChargedPreparationMode.BasicTail && activeFamily == CombatNetworkSkillFamily.GroundBasic &&
            chargePreparation == presentationSession && presentation != null &&
            presentation.CanLaunchFromChargedBasicTail(charged);

        private void PublishChargeTail(int skillId)
        {
            var state = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
            chargeTailState.Value = new CombatNetworkChargeTailSnapshot {
                Epoch = active.Ticket.AuthorityEpoch, Generation = active.Ticket.Generation,
                SkillId = skillId, NormalizedTime = Mathf.Clamp01(state.normalizedTime)
            };
        }

        private void OnChargeChanged(int before, int after) { if (!IsServer) ApplyChargePresentation(); }
        private void ApplyChargePresentation()
        {
            if (IsServer || !IsSpawned || presentation == null) return;
            // Released tails keep ticking the existing session until its rate has returned to normal.
            presentation.UpdateExternalChargedBasicTail(presentationSession);
            if (chargeSkill.Value == 0)
            {
                int session = chargePreparation; chargePreparation = 0;
                presentation.ReleaseChargedBasicTail(session);
                presentation.EndChargedPreparation(session);
                return;
            }
            if (!catalogValid ||
                !catalog.TryGet(chargeSkill.Value, out var entry) || !(entry.skill is ChargedSkillSO skill)) return;
            if (skill.preparationMode == ChargedPreparationMode.BasicTail)
            {
                var snapshot = chargeTailState.Value;
                var action = actionState.Value;
                if (!action.Active || !snapshot.Matches(action.Epoch, action.Generation) || snapshot.SkillId != entry.Id ||
                    !catalog.TryGet(action.SkillId, out var basic) || !(basic.skill is BasicSkillsSO) ||
                    basic.CanonicalGuid != action.Guid.ToString()) return;
                // A late observer must use the actual held Basic pose, not elapsed wall time / clip length.
                if (presentationSession == 0)
                {
                    clientEpoch = action.Epoch; clientGeneration = action.Generation; clientEntry = basic;
                    clientCapturedDirection = action.Direction; clientCapturedTarget = null;
                    if (action.HasTarget && !action.Target.TryGet(out clientCapturedTarget, NetworkManager)) return;
                    if (!StartLocalPresentation(basic, snapshot.NormalizedTime, false)) return;
                    clientActionConsumed = true;
                }
                if (clientEpoch != snapshot.Epoch || clientGeneration != snapshot.Generation ||
                    !presentation.IsCurrentSession(presentationSession)) return;
                if (presentation.TryBeginChargedBasicTail(skill, presentationSession, IsOwner)) chargePreparation = presentationSession;
                return;
            }
            if (chargePreparation != 0 || presentation.IsActionActive) return;
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
