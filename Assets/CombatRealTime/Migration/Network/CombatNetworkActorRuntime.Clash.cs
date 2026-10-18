using System;
using Unity.Netcode;
using UnityEngine;

namespace Lit.Combat.Migration.Network
{
    public struct CombatNetworkClashSnapshot : INetworkSerializable, IEquatable<CombatNetworkClashSnapshot>
    {
        public ulong Revision, Epoch, ActionGeneration;
        public NetworkObjectReference Player, Enemy;
        public int PlayerGeneration, EnemyGeneration, PlayerState, EnemyState;
        public bool Active;
        public double StartedAt;
        public float RecoilSeconds, RecoverySeconds, Distance, BlendSeconds;
        public Vector3 Contact, PlayerDirection;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Revision); serializer.SerializeValue(ref Epoch); serializer.SerializeValue(ref ActionGeneration);
            serializer.SerializeValue(ref Player); serializer.SerializeValue(ref Enemy);
            serializer.SerializeValue(ref PlayerGeneration); serializer.SerializeValue(ref EnemyGeneration);
            serializer.SerializeValue(ref PlayerState); serializer.SerializeValue(ref EnemyState); serializer.SerializeValue(ref Active);
            serializer.SerializeValue(ref StartedAt); serializer.SerializeValue(ref RecoilSeconds); serializer.SerializeValue(ref RecoverySeconds);
            serializer.SerializeValue(ref Distance); serializer.SerializeValue(ref BlendSeconds);
            serializer.SerializeValue(ref Contact); serializer.SerializeValue(ref PlayerDirection);
        }

        public bool Equals(CombatNetworkClashSnapshot other) => Revision == other.Revision && Epoch == other.Epoch &&
            ActionGeneration == other.ActionGeneration && Player.Equals(other.Player) && Enemy.Equals(other.Enemy) &&
            PlayerGeneration == other.PlayerGeneration && EnemyGeneration == other.EnemyGeneration &&
            PlayerState == other.PlayerState && EnemyState == other.EnemyState && Active == other.Active && StartedAt == other.StartedAt &&
            RecoilSeconds == other.RecoilSeconds && RecoverySeconds == other.RecoverySeconds && Distance == other.Distance &&
            BlendSeconds == other.BlendSeconds && Contact == other.Contact && PlayerDirection == other.PlayerDirection;
    }

    public sealed partial class CombatNetworkActorRuntime
    {
        private readonly NetworkVariable<CombatNetworkClashSnapshot> clashState = new NetworkVariable<CombatNetworkClashSnapshot>();
        private CombatLabClashController clashPublisher;
        private ulong appliedClashRevision;
        private int remoteClashSession, serverClashSession;
        private LitBrainsEnemy serverClashEnemy;
        private double remoteClashEndsAt;
        private Animator remoteClashEnemyAnimator;
        private LitBrainsEnemy remoteClashEnemy;
        private int remoteClashEnemyState, remoteClashEnemyGeneration;

        // Parent calls once per Update after actor/world references have been refreshed.
        private void TickClashReplication()
        {
            if (!IsSpawned || !isActiveAndEnabled || !IsPlayer) return;
            if (IsServer)
            {
                var controller = combatWorld != null && admitted ? combatWorld.GetComponent<CombatLabClashController>() : null;
                if (controller != clashPublisher)
                {
                    if (clashPublisher != null) clashPublisher.WorldClashResolved -= PublishWorldClash;
                    clashPublisher = controller;
                    if (clashPublisher != null) clashPublisher.WorldClashResolved += PublishWorldClash;
                }
                if (clashState.Value.Active && (presentation == null || !presentation.IsCurrentSession(serverClashSession) ||
                    serverClashEnemy == null || serverClashEnemy.IsDead || serverClashEnemy.IsCinematicSuspended ||
                    !serverClashEnemy.IsClashRecovering || NetworkManager.ServerTime.Time >=
                        clashState.Value.StartedAt + clashState.Value.RecoilSeconds + clashState.Value.RecoverySeconds))
                    FinishServerClash();
                return;
            }

            var snapshot = clashState.Value;
            if (snapshot.Revision != 0 && snapshot.Revision != appliedClashRevision)
                TryApplyRemoteClash(snapshot);
            if (remoteClashSession != 0 && (!snapshot.Active || !IsAlive() ||
                NetworkManager.ServerTime.Time >= remoteClashEndsAt || CombatCinematicReservations.ContainsParticipant(transform)))
                EndRemoteClashPresentation();
        }

        private void PublishWorldClash(Transform playerRoot, Transform enemyRoot, int playerGeneration, int enemyGeneration, Vector3 contact)
        {
            if (!IsServer || !IsSpawned || !admitted || playerRoot != transform || enemyRoot == null || clashPublisher == null ||
                !clashPublisher.TryGetClashPresentation(out float recoil, out float recovery, out float distance)) return;
            var enemyIdentity = enemyRoot.GetComponent<NetworkObject>();
            var enemy = enemyRoot.GetComponent<LitBrainsEnemy>();
            if (enemyIdentity == null || !enemyIdentity.IsSpawned || enemyIdentity.NetworkManager != NetworkManager || enemy == null) return;
            var action = actionState.Value;
            if (action.Generation == 0) return;
            var enemyAnimator = enemy.Animator;
            int enemyState = enemyAnimator == null ? 0 : enemyAnimator.IsInTransition(0)
                ? enemyAnimator.GetNextAnimatorStateInfo(0).fullPathHash : enemyAnimator.GetCurrentAnimatorStateInfo(0).fullPathHash;
            var snapshot = new CombatNetworkClashSnapshot {
                Revision = clashState.Value.Revision + 1, Epoch = action.Epoch, ActionGeneration = action.Generation,
                Player = new NetworkObjectReference(NetworkObject), Enemy = new NetworkObjectReference(enemyIdentity),
                PlayerGeneration = playerGeneration, EnemyGeneration = enemyGeneration,
                PlayerState = Animator.StringToHash(CombatActorPresentation.DamageReactionState(actor) ?? string.Empty),
                EnemyState = enemyState, Active = true, StartedAt = NetworkManager.ServerTime.Time,
                RecoilSeconds = recoil, RecoverySeconds = recovery, Distance = distance, BlendSeconds = clashPublisher.ClashBlendSeconds,
                Contact = contact, PlayerDirection = Vector3.ProjectOnPlane(transform.position - enemyRoot.position, Vector3.up).normalized
            };
            serverClashSession = presentation != null ? presentation.ActionGeneration : 0;
            serverClashEnemy = enemy;
            clashState.Value = snapshot;
            // Server already plays the burst/recoil. Host disarms only its own local inputs.
            DisarmClashOwner();
        }

        private bool TryApplyRemoteClash(CombatNetworkClashSnapshot snapshot)
        {
            if (snapshot.Revision <= appliedClashRevision) return true;
            bool newerAction = clientEpoch > snapshot.Epoch || clientEpoch == snapshot.Epoch && clientGeneration > snapshot.ActionGeneration;
            if (!snapshot.Active)
            {
                appliedClashRevision = snapshot.Revision;
                if (!newerAction && snapshot.ActionGeneration != 0) DisarmClashOwner();
                EndRemoteClashPresentation();
                return true;
            }
            if (!ClashSnapshotFinite(snapshot) || snapshot.PlayerGeneration < 0 || snapshot.EnemyGeneration < 0 ||
                snapshot.RecoilSeconds <= 0 || snapshot.RecoilSeconds > 10 || snapshot.RecoverySeconds < 0 || snapshot.RecoverySeconds > 10 ||
                snapshot.Distance < 0 || snapshot.Distance > 10 || snapshot.BlendSeconds < 0 || snapshot.BlendSeconds > 1)
            { appliedClashRevision = snapshot.Revision; return false; }
            if (!snapshot.Player.TryGet(out var playerIdentity, NetworkManager) ||
                !snapshot.Enemy.TryGet(out var enemyIdentity, NetworkManager)) return false;
            if (playerIdentity != NetworkObject || enemyIdentity == null || !IsAlive())
            { appliedClashRevision = snapshot.Revision; return false; }
            var enemy = enemyIdentity.GetComponent<LitBrainsEnemy>();
            if (enemy == null || enemy.IsDead || enemy.IsCinematicSuspended || CombatCinematicReservations.ContainsParticipant(transform))
            { appliedClashRevision = snapshot.Revision; return false; }
            // Neither a late clash snapshot nor its eventual cleanup may replace a newer action.
            if (newerAction)
            { appliedClashRevision = snapshot.Revision; return false; }
            double now = NetworkManager.ServerTime.Time;
            if (now >= snapshot.StartedAt + snapshot.RecoilSeconds + snapshot.RecoverySeconds)
            { appliedClashRevision = snapshot.Revision; DisarmClashOwner(); return true; }
            var world = combatWorld != null ? combatWorld : FindAnyObjectByType<CombatWorldRuntime>();
            var controller = world != null ? world.GetComponent<CombatLabClashController>() : null;
            if (controller == null || presentation == null || animator == null || !animator.isActiveAndEnabled) return false;
            appliedClashRevision = snapshot.Revision;
            EndRemoteClashPresentation();
            StopLocalPresentation();
            clientActionConsumed = true;
            DisarmClashOwner();
            remoteClashSession = presentation.BeginExternalPresentation(this);
            remoteClashEndsAt = snapshot.StartedAt + snapshot.RecoilSeconds + snapshot.RecoverySeconds;
            float elapsed = (float)Math.Max(0, now - snapshot.StartedAt);
            if (remoteClashSession != 0 && animator.HasState(0, snapshot.PlayerState))
                animator.CrossFadeInFixedTime(snapshot.PlayerState, snapshot.BlendSeconds, 0, elapsed);
            remoteClashEnemy = enemy;
            remoteClashEnemyAnimator = enemy.Animator;
            remoteClashEnemyGeneration = enemy.ActionSequenceId;
            remoteClashEnemyState = snapshot.EnemyState;
            if (remoteClashEnemyAnimator != null && remoteClashEnemyAnimator.isActiveAndEnabled &&
                remoteClashEnemyAnimator.HasState(0, snapshot.EnemyState))
                remoteClashEnemyAnimator.CrossFadeInFixedTime(snapshot.EnemyState, snapshot.BlendSeconds, 0, elapsed);
            controller.PlayClashCosmetics(snapshot.Contact);
            return true;
        }

        private static bool ClashSnapshotFinite(CombatNetworkClashSnapshot snapshot) =>
            !double.IsNaN(snapshot.StartedAt) && !double.IsInfinity(snapshot.StartedAt) &&
            CombatCommandPolicy.Finite(snapshot.Contact) && CombatCommandPolicy.Finite(snapshot.PlayerDirection) &&
            FiniteClash(snapshot.RecoilSeconds) && FiniteClash(snapshot.RecoverySeconds) && FiniteClash(snapshot.Distance) && FiniteClash(snapshot.BlendSeconds);
        private static bool FiniteClash(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private void DisarmClashOwner()
        {
            if (!IsOwner) return;
            var manager = RealTimeCombatManager.Instance;
            if (manager == null || manager.PlayerRoot != transform) return;
            manager.GetComponent<RealTimeCombatInput>()?.CancelBufferedBasicSkills();
            manager.GetComponent<SkillsManager>()?.ResetAllBasicSkillCombos();
        }

        private void FinishServerClash()
        {
            var snapshot = clashState.Value;
            if (snapshot.Active) { snapshot.Active = false; clashState.Value = snapshot; }
            serverClashSession = 0;
            serverClashEnemy = null;
        }

        private void EndRemoteClashPresentation()
        {
            int session = remoteClashSession; remoteClashSession = 0;
            if (session != 0 && presentation != null && presentation.IsCurrentSession(session)) presentation.EndExternalPresentation(session);
            // Only restore the cosmetic pose if no other enemy reaction has taken ownership.
            var enemyAnimator = remoteClashEnemyAnimator;
            var enemy = remoteClashEnemy;
            remoteClashEnemyAnimator = null; remoteClashEnemy = null;
            if (enemyAnimator == null || enemy == null || !enemyAnimator.isActiveAndEnabled || enemy.IsDead || enemy.IsCinematicSuspended ||
                enemy.ActionSequenceId != remoteClashEnemyGeneration || CombatCinematicReservations.ContainsParticipant(enemy.transform)) return;
            var current = enemyAnimator.GetCurrentAnimatorStateInfo(0);
            if (enemyAnimator.IsInTransition(0) || current.fullPathHash != remoteClashEnemyState) return;
            string state = enemy.combatProfile != null ? enemy.combatProfile.locomotionState : null;
            if (!string.IsNullOrEmpty(state) && enemyAnimator.HasState(0, Animator.StringToHash(state)))
            {
                var world = combatWorld != null ? combatWorld : FindAnyObjectByType<CombatWorldRuntime>();
                var controller = world != null ? world.GetComponent<CombatLabClashController>() : null;
                if (controller != null) enemyAnimator.CrossFadeInFixedTime(state, controller.ClashBlendSeconds, 0);
            }
        }

        // Parent calls from Retire before clearing combatWorld, including world invalidation/despawn.
        private void CleanupClashReplication()
        {
            if (clashPublisher != null) clashPublisher.WorldClashResolved -= PublishWorldClash;
            clashPublisher = null;
            if (IsServer && IsSpawned) FinishServerClash();
            EndRemoteClashPresentation();
            appliedClashRevision = clashState.Value.Revision;
        }
    }
}
