using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lit.Combat.Migration.Network
{
    public sealed class CombatCinematicReservationLease : IDisposable
    {
        private Action<CombatCinematicReservationLease> release;
        internal readonly object Owner;
        internal readonly Transform Player;
        internal readonly ICombatTarget Target;
        internal readonly CombatActionTicket Ticket;
        internal readonly ulong PlayerId;
        internal readonly ulong TargetId;
        public bool IsActive => release != null;
        public event Action Released;

        internal CombatCinematicReservationLease(object owner, Transform player, ICombatTarget target,
            CombatActionTicket ticket, ulong playerId, ulong targetId, Action<CombatCinematicReservationLease> release)
        {
            Owner = owner; Player = player; Target = target; Ticket = ticket;
            PlayerId = playerId; TargetId = targetId; this.release = release;
        }

        public void Release()
        {
            var callback = release;
            if (callback == null) return;
            release = null;
            callback(this);
            var listeners = Released;
            Released = null;
            if (listeners == null) return;
            foreach (Action listener in listeners.GetInvocationList())
            {
                try { listener(); }
                catch (Exception exception) { Debug.LogException(exception); }
            }
        }

        public void Dispose() => Release();

        internal bool ParticipantsAlive()
        {
            try
            {
                if (Owner == null || Owner is UnityEngine.Object unityOwner && unityOwner == null ||
                    Owner is Behaviour behaviour && !behaviour.isActiveAndEnabled ||
                    Player == null || !Player.gameObject.activeInHierarchy || Target?.Root == null ||
                    !Target.Root.gameObject.activeInHierarchy || Target.IsDead ||
                    !Player.gameObject.scene.IsValid() || !Player.gameObject.scene.isLoaded ||
                    !Target.Root.gameObject.scene.IsValid() || !Target.Root.gameObject.scene.isLoaded) return false;
                var controller = Player.GetComponentInParent<SquadCharacterController>();
                return controller != null && controller.isActiveAndEnabled && controller.CurrentHp > 0;
            }
            catch { return false; }
        }
    }

    /// <summary>Controller-facing pair locks. Online clients never acquire local authority.</summary>
    public sealed class CombatCinematicReservations : MonoBehaviour
    {
        private static readonly List<CombatCinematicReservationLease> localLeases = new List<CombatCinematicReservationLease>();
        private static CombatCinematicReservations cleanup;
        private NavMeshWorldService world;

        public static bool ContainsParticipant(Transform actor)
        {
            if (actor == null) return false;
            var network = NetworkManager.Singleton;
            if (network != null && network.IsListening)
                return network.IsServer ? network.GetComponent<CombatCommandAuthority>() is CombatCommandAuthority authority &&
                    authority.IsCinematicParticipant(actor.GetComponentInParent<NetworkObject>()) : CombatNetworkActorRuntime.IsNetworkCinematicParticipant(actor);
            foreach (var lease in localLeases)
                if (lease.IsActive && (Related(lease.Player, actor) || Related(lease.Target?.Root, actor))) return true;
            return false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            ReleaseLocal();
            cleanup = null;
        }

        public static bool TryAcquire(object owner, Transform player, ICombatTarget target,
            out CombatCinematicReservationLease lease)
        {
            lease = null;
            if (owner == null || player == null || target?.Root == null || Related(player, target.Root)) return false;
            var manager = NetworkManager.Singleton;
            var playerIdentity = player.GetComponentInParent<NetworkObject>();
            var targetIdentity = target.Root.GetComponentInParent<NetworkObject>();
            if (manager != null && manager.IsListening)
            {
                ReleaseLocal();
                if (!manager.IsServer || playerIdentity == null || targetIdentity == null) return false;
                var authority = manager.GetComponent<CombatCommandAuthority>();
                return authority != null && authority.TryAcquire(owner, playerIdentity, targetIdentity, target, out lease);
            }
            // A spawned identity or shutdown in progress is not an offline session.
            if (manager != null && manager.ShutdownInProgress ||
                playerIdentity != null && playerIdentity.IsSpawned || targetIdentity != null && targetIdentity.IsSpawned) return false;
            SweepLocal();
            var candidate = new CombatCinematicReservationLease(owner, player, target, default, 0, 0,
                released => localLeases.Remove(released));
            if (!candidate.ParticipantsAlive()) return false;
            foreach (var current in localLeases)
                if (Related(current.Player, player) || Related(current.Target.Root, player) ||
                    Related(current.Player, target.Root) || Related(current.Target.Root, target.Root)) return false;
            if (Application.isPlaying && cleanup == null)
            {
                var root = new GameObject("Combat Cinematic Reservation Cleanup");
                DontDestroyOnLoad(root);
                cleanup = root.AddComponent<CombatCinematicReservations>();
            }
            localLeases.Add(candidate);
            lease = candidate;
            return true;
        }

        private static bool Related(Transform a, Transform b) => a != null && b != null &&
            (a == b || a.IsChildOf(b) || b.IsChildOf(a));

        private static void SweepLocal()
        {
            foreach (var lease in new List<CombatCinematicReservationLease>(localLeases))
                if (!lease.ParticipantsAlive()) lease.Release();
        }

        private static void ReleaseLocal()
        {
            foreach (var lease in new List<CombatCinematicReservationLease>(localLeases)) lease.Release();
        }

        private void OnEnable()
        {
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            RefreshWorldBinding();
        }

        private void OnDisable()
        {
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            if (world != null) world.StateChanged -= OnWorldStateChanged;
            world = null;
            ReleaseLocal();
            if (cleanup == this) cleanup = null;
        }

        private void Update()
        {
            RefreshWorldBinding();
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) ReleaseLocal();
            else SweepLocal();
        }

        private void RefreshWorldBinding()
        {
            var current = NavMeshWorldService.Instance;
            if (ReferenceEquals(world, current)) return;
            if (world != null) world.StateChanged -= OnWorldStateChanged;
            ReleaseLocal();
            world = current;
            if (world != null) world.StateChanged += OnWorldStateChanged;
        }

        private void OnWorldStateChanged(NavMeshWorldState state)
        {
            if (state == NavMeshWorldState.Invalidating || state == NavMeshWorldState.Unloaded) ReleaseLocal();
        }

        private void OnSceneUnloaded(Scene scene)
        {
            foreach (var lease in new List<CombatCinematicReservationLease>(localLeases))
                if (lease.Player == null || lease.Target?.Root == null ||
                    lease.Player.gameObject.scene == scene || lease.Target.Root.gameObject.scene == scene) lease.Release();
        }
    }
}
