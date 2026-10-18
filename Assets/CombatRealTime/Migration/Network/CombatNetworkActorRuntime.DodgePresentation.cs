using System;
using Unity.Netcode;
using UnityEngine;

namespace Lit.Combat.Migration.Network
{
    public struct CombatNetworkDodgeSnapshot : INetworkSerializable, IEquatable<CombatNetworkDodgeSnapshot>
    {
        public ulong Revision;
        public int State;
        public bool Active;
        public double StartedAt;
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Revision); serializer.SerializeValue(ref State);
            serializer.SerializeValue(ref Active); serializer.SerializeValue(ref StartedAt);
        }
        public bool Equals(CombatNetworkDodgeSnapshot other) => Revision == other.Revision && State == other.State &&
            Active == other.Active && StartedAt == other.StartedAt;
    }

    public sealed partial class CombatNetworkActorRuntime
    {
        private readonly NetworkVariable<CombatNetworkDodgeSnapshot> dodgeState = new NetworkVariable<CombatNetworkDodgeSnapshot>();
        private ulong appliedDodgeRevision;
        private int remoteDodgeSession;
        private readonly NetworkVariable<CombatNetworkDodgeSnapshot> guardState = new NetworkVariable<CombatNetworkDodgeSnapshot>();
        private ulong appliedGuardRevision;
        private int remoteGuardSession;

        internal void PublishGuardPresentation(int session)
        {
            if (!IsServer || !IsSpawned || session == 0 || animator == null) return;
            int state = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0).fullPathHash : animator.GetCurrentAnimatorStateInfo(0).fullPathHash;
            ulong revision = guardState.Value.Revision + 1;
            guardState.Value = new CombatNetworkDodgeSnapshot { Revision = revision, State = state, Active = true, StartedAt = NetworkManager.ServerTime.Time };
            presentation.RegisterActionCleanup(session, () =>
            {
                if (!IsSpawned || !IsServer || guardState.Value.Revision != revision) return;
                var snapshot = guardState.Value;
                snapshot.Active = false;
                guardState.Value = snapshot;
            });
        }

        private void TickGuardPresentation()
        {
            if (IsServer || presentation == null || animator == null) return;
            var snapshot = guardState.Value;
            if (!snapshot.Active)
            {
                EndRemoteGuardPresentation();
                appliedGuardRevision = snapshot.Revision;
                return;
            }
            if (snapshot.Revision == appliedGuardRevision || !animator.isActiveAndEnabled || !animator.HasState(0, snapshot.State)) return;
            StopLocalPresentation();
            clientActionConsumed = true;
            EndRemoteGuardPresentation();
            remoteGuardSession = presentation.BeginExternalPresentation(this, allowMobility: true);
            if (remoteGuardSession == 0) return;
            appliedGuardRevision = snapshot.Revision;
            animator.CrossFadeInFixedTime(snapshot.State, .08f, 0, (float)Math.Max(0, NetworkManager.ServerTime.Time - snapshot.StartedAt));
        }

        private void EndRemoteGuardPresentation()
        {
            int session = remoteGuardSession;
            remoteGuardSession = 0;
            if (session != 0 && presentation != null && presentation.IsCurrentSession(session)) presentation.EndExternalPresentation(session);
        }

        private void PublishDodgePresentation(int state, int session)
        {
            if (!IsServer || !IsSpawned || session == 0) return;
            dodgeState.Value = new CombatNetworkDodgeSnapshot
            {
                Revision = dodgeState.Value.Revision + 1, State = state,
                Active = true, StartedAt = NetworkManager.ServerTime.Time
            };
            presentation.RegisterActionCleanup(session, () =>
            {
                if (!IsSpawned || !IsServer || serverDodgeSession != 0 && serverDodgeSession != session) return;
                var snapshot = dodgeState.Value;
                snapshot.Active = false;
                dodgeState.Value = snapshot;
            });
        }

        private void TickDodgePresentation()
        {
            if (IsServer || presentation == null || animator == null) return;
            var snapshot = dodgeState.Value;
            if (!snapshot.Active)
            {
                EndRemoteDodgePresentation();
                appliedDodgeRevision = snapshot.Revision;
                return;
            }
            if (snapshot.Revision == appliedDodgeRevision || !animator.isActiveAndEnabled ||
                !animator.HasState(0, snapshot.State)) return;
            StopLocalPresentation();
            clientActionConsumed = true;
            EndRemoteDodgePresentation();
            remoteDodgeSession = presentation.BeginExternalPresentation(this, allowMobility: true);
            if (remoteDodgeSession == 0) return;
            appliedDodgeRevision = snapshot.Revision;
            float elapsed = (float)Math.Max(0, NetworkManager.ServerTime.Time - snapshot.StartedAt);
            animator.CrossFadeInFixedTime(snapshot.State, .05f, 0, elapsed);
        }

        private void EndRemoteDodgePresentation()
        {
            int session = remoteDodgeSession;
            remoteDodgeSession = 0;
            if (session != 0 && presentation != null && presentation.IsCurrentSession(session))
                presentation.EndExternalPresentation(session);
        }
    }
}
