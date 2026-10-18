using System;
using Unity.Netcode;
using UnityEngine;

namespace Lit.Combat.Migration.Network
{
    public struct CombatNetworkReactionSnapshot : INetworkSerializable, IEquatable<CombatNetworkReactionSnapshot>
    {
        public bool Active;
        public NetworkObjectReference Enemy;
        public int ActionId;
        public ulong World;
        public double OpenedAt, DodgeEndsAt;
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Active); serializer.SerializeValue(ref Enemy); serializer.SerializeValue(ref ActionId);
            serializer.SerializeValue(ref World); serializer.SerializeValue(ref OpenedAt); serializer.SerializeValue(ref DodgeEndsAt);
        }
        public bool Equals(CombatNetworkReactionSnapshot other) => Active == other.Active && Enemy.Equals(other.Enemy) &&
            ActionId == other.ActionId && World == other.World && OpenedAt.Equals(other.OpenedAt) && DodgeEndsAt.Equals(other.DodgeEndsAt);
    }

    public sealed partial class CombatNetworkActorRuntime
    {
        [Header("Enemy Reactions")]
        [SerializeField, Min(.01f)] private float networkDodgeWindowSeconds = .5f;
        [SerializeField, Min(.01f)] private float networkCounterPreImpactSeconds = .5f;
        [SerializeField, Min(.01f)] private float networkCounterRetrySeconds = .4f;
        private readonly NetworkVariable<CombatNetworkReactionSnapshot> reactionState = new NetworkVariable<CombatNetworkReactionSnapshot>(
            default, NetworkVariableReadPermission.Owner);
        private ICombatTarget reactionEnemy;
        private EnemyReactionTiming reactionTiming;
        private NetworkObject reactionEnemyIdentity;
        private int reactionActionId;
        private bool reactionContactClosed, reactionDodgeProtected, counterPressQueued;
        private double nextNetworkCounterAt, queuedCounterAt;
        private bool admittedGuardPress;
        internal bool ConsumeNetworkGuardPress()
        {
            bool admitted = admittedGuardPress;
            admittedGuardPress = false;
            return admitted;
        }

        public bool HasNetworkReaction => IsSpawned && reactionState.Value.Active;

        // Called on the SERVER runtime belonging to the actual victim, never the host facade.
        public bool ServerOpenEnemyReaction(NetworkObject enemy, EnemyReactionTiming timing)
        {
            if (!IsServer || !IsSpawned || !admitted || !IsAlive() || !IsAssigned(OwnerClientId) || authority == null ||
                HasNetworkCinematic || enemy == null || !authority.IsRegistered(enemy)) return false;
            var target = ResolveEnemy(enemy);
            if (target == null || target.IsDead || !target.IsAttackCommitted || timing != null && timing.ActionId != target.ActionSequenceId) return false;
            if (reactionEnemyIdentity == enemy && reactionActionId == target.ActionSequenceId) return false;
            ClearNetworkReaction();
            reactionEnemy = target; reactionEnemyIdentity = enemy; reactionActionId = target.ActionSequenceId;
            reactionTiming = timing != null && timing.IsValid ? timing : null;
            double now = NetworkManager.ServerTime.Time;
            reactionState.Value = new CombatNetworkReactionSnapshot { Active = true, Enemy = new NetworkObjectReference(enemy),
                ActionId = reactionActionId, World = admittedWorld, OpenedAt = now,
                DodgeEndsAt = now + Mathf.Max(.01f, networkDodgeWindowSeconds) };
            if (reactionTiming == null) Diagnose("Enemy counter unavailable for this action: validated clip timing missing; dodge/guard remain available.");
            return true;
        }

        internal void TickNetworkReactions()
        {
            if (!IsServer) return;
            if (reactionEnemyIdentity != null && !ServerReactionStillValid()) { ClearNetworkReaction(); return; }
            reactionTiming?.Capture();
            ResolveQueuedNetworkCounter();
            if (reactionState.Value.Active && NetworkManager.ServerTime.Time >= reactionState.Value.DodgeEndsAt &&
                (reactionTiming == null || reactionTiming.TryGetCurrentPhase(out double phase) && phase >= reactionTiming.ImpactSeconds))
            {
                var snapshot = reactionState.Value;
                snapshot.Active = false;
                reactionState.Value = snapshot;
            }
        }

        private bool ServerReactionStillValid() => IsSpawned && admitted && IsAlive() && IsAssigned(OwnerClientId) &&
            authority != null && reactionState.Value.World == admittedWorld && admittedWorld == authority.WorldGeneration &&
            !HasNetworkCinematic && reactionEnemyIdentity != null && authority.IsRegistered(reactionEnemyIdentity) &&
            reactionEnemy?.Root != null && !reactionEnemy.IsDead && reactionEnemy.IsAttackCommitted &&
            reactionEnemy.ActionSequenceId == reactionActionId;

        // Run before ordinary DodgeServerRpc mobility. true means the opportunity handled the press,
        // including a refused immediate roll; it must not be buffered/retried as a second dodge.
        internal bool TryServerReactionDodge(Vector3 direction, out bool accepted, double pressedAt = double.NaN)
        {
            accepted = false;
            if (!IsServer || !reactionState.Value.Active || reactionContactClosed || !ServerReactionStillValid() ||
                double.IsNaN(pressedAt) || double.IsInfinity(pressedAt) ||
                pressedAt < reactionState.Value.OpenedAt || pressedAt >= reactionState.Value.DodgeEndsAt ||
                pressedAt > NetworkManager.ServerTime.Time + .001 || NetworkManager.ServerTime.Time - pressedAt > .25) return false;
            var mobility = combatWorld != null ? combatWorld.GetComponent<CombatMobilityController>() : null;
            if (mobility == null || !CombatCommandPolicy.Finite(direction)) return true;
            accepted = mobility.TryDodgeActor(actor, direction);
            if (!accepted) return true;
            reactionDodgeProtected = true;
            var state = reactionState.Value; state.Active = false; reactionState.Value = state;
            counterPressQueued = false;
            StopCharge(); chargeRequiresRelease = true;
            return true;
        }

        // Guard input server routing calls this before its normal guard behavior. It only consumes
        // a successful counter; early/late presses can still become ordinary guard.
        internal bool TryServerCounterReaction(double pressedAt)
        {
            admittedGuardPress = false;
            double now = NetworkManager.ServerTime.Time;
            if (!IsServer || !CombatCommandPolicy.Finite((float)pressedAt) || pressedAt > now + .001 ||
                now - pressedAt > .25 || now < nextNetworkCounterAt) return false;
            nextNetworkCounterAt = now + Mathf.Max(.01f, networkCounterRetrySeconds);
            admittedGuardPress = true;
            if (!reactionState.Value.Active || reactionContactClosed || !ServerReactionStillValid() ||
                pressedAt < reactionState.Value.OpenedAt || reactionTiming == null) return false;
            queuedCounterAt = pressedAt; counterPressQueued = true;
            return ResolveQueuedNetworkCounter();
        }

        private bool ResolveQueuedNetworkCounter()
        {
            if (!counterPressQueued) return false;
            counterPressQueued = false;
            if (!IsServer || reactionContactClosed || !ServerReactionStillValid() || reactionTiming == null ||
                !reactionTiming.TryGetCurrentPhase(out double phase) || phase >= reactionTiming.ImpactSeconds) return false;
            // Use the current authoritative Animator sample. Never resurrect an input after contact.
            double age = Math.Max(0, NetworkManager.ServerTime.Time - queuedCounterAt);
            var state = reactionTiming.Animator.IsInTransition(0) ? reactionTiming.Animator.GetNextAnimatorStateInfo(0) :
                reactionTiming.Animator.GetCurrentAnimatorStateInfo(0);
            double rate = reactionTiming.Animator.speed * state.speed * state.speedMultiplier;
            double pressedPhase = phase - age * Math.Max(0, rate);
            if (!EnemyReactionTiming.Contains(pressedPhase, reactionTiming.ImpactSeconds, networkCounterPreImpactSeconds) ||
                networkCounterSkill == null || !catalogValid || !catalog.TryGetCinematic(networkCounterSkill, out int counterId) ||
                !CanPresentCinematic(networkCounterSkill, reactionEnemy, out _)) return false;
            var target = reactionEnemy;
            if (!TryAcquireSpecialCinematic(target)) return false;
            if (!StartServerCinematic(CombatNetworkCinematicKind.Counter, counterId, networkCounterSkill, target))
            { cinematicLease?.Release(); cinematicLease = null; return false; }
            ClearNetworkReaction();
            return true;
        }

        // Immediately BEFORE contact damage/clash. true means a counter replaced the incoming action.
        internal bool ServerResolveReactionAtContact(NetworkObject enemy, int actionId)
        {
            if (!IsServer || reactionEnemyIdentity != enemy || reactionActionId != actionId) return false;
            if (ResolveQueuedNetworkCounter()) return true;
            reactionContactClosed = true;
            var state = reactionState.Value; state.Active = false; reactionState.Value = state;
            return HasNetworkCinematic;
        }

        internal bool IsProtectedFromEnemyAction(NetworkObject enemy, int actionId) => IsServer && reactionDodgeProtected &&
            reactionEnemyIdentity == enemy && reactionActionId == actionId && ServerReactionStillValid();

        internal void ServerEndEnemyReaction(NetworkObject enemy, int actionId)
        {
            if (IsServer && reactionEnemyIdentity == enemy && reactionActionId == actionId) ClearNetworkReaction();
        }

        internal void ClearNetworkReaction()
        {
            counterPressQueued = reactionDodgeProtected = reactionContactClosed = false;
            reactionTiming = null; reactionEnemy = null; reactionEnemyIdentity = null; reactionActionId = 0;
            if (IsSpawned && IsServer) reactionState.Value = default;
        }
    }
}
