using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lit.Combat.Migration.Network
{
    /// <summary>Opt-in server command gate. Does not replicate presentation or apply damage.</summary>
    [DisallowMultipleComponent]
    public sealed class CombatCommandAuthority : MonoBehaviour
    {
        private sealed class SkillRegistration
        {
            public ScriptableObject Skill;
            public CombatCommandPolicy Policy;
        }

        private sealed class ActorRegistration
        {
            public NetworkObject Identity;
            public Func<bool> IsAlive;
            public Func<ulong, bool> IsAssigned;
            public ICombatCommandExecutor Executor;
            public ulong Owner;
            public ulong LastSequence;
            public double NextRequestAt;
            public readonly Dictionary<int, SkillRegistration> Skills = new Dictionary<int, SkillRegistration>();
            public Dictionary<ScriptableObject, double> Cooldowns;
        }

        private sealed class ActionState
        {
            public CombatCommandContext Context;
            public CombatCommandPolicy Policy;
            public ICombatCommandExecutor Executor;
            public double ExpiresAt;
            public bool Started;
            public bool HasTarget;
            public ulong TargetId;
            public readonly HashSet<(ulong, int)> Impacts = new HashSet<(ulong, int)>();
        }

        private NetworkManager manager;
        private NavMeshWorldService world;
        private ulong epoch;
        private ulong nextGeneration;
        private bool clearing;
        private bool executing;
        private bool checkingImpact;
        private readonly Dictionary<ulong, ActorRegistration> actors = new Dictionary<ulong, ActorRegistration>();
        private readonly Dictionary<NetworkObject, Dictionary<ScriptableObject, double>> retainedCooldowns =
            new Dictionary<NetworkObject, Dictionary<ScriptableObject, double>>();
        private readonly Dictionary<ulong, ActionState> actions = new Dictionary<ulong, ActionState>();
        private readonly Dictionary<ulong, CombatActionTicket> reservations = new Dictionary<ulong, CombatActionTicket>();
        private readonly Dictionary<CombatCinematicReservationLease, double> cinematicLeases =
            new Dictionary<CombatCinematicReservationLease, double>();

        public bool IsAuthoritative => isActiveAndEnabled && manager != null && manager.IsListening && manager.IsServer && !clearing;
        public ulong WorldGeneration { get; private set; }
        public event Action WorldInvalidated;
        public event Action<NetworkObject, CombatActionEndReason> ActorUnregistered;

        public bool IsCinematicParticipant(NetworkObject actor)
        {
            if (!IsAuthoritative || !IsValidIdentity(actor)) return false;
            foreach (var lease in cinematicLeases.Keys)
                if (lease.IsActive && (lease.PlayerId == actor.NetworkObjectId || lease.TargetId == actor.NetworkObjectId)) return true;
            return false;
        }

        public float GetCooldownRemaining(NetworkObject actor, ScriptableObject skill)
        {
            if (!IsAuthoritative || skill == null || !TryActor(actor, out var registration) ||
                !registration.Cooldowns.TryGetValue(skill, out double ready)) return 0;
            return (float)Math.Max(0, ready - Time.timeAsDouble);
        }

        public bool IsRegistered(NetworkObject actor, ICombatCommandExecutor executor = null)
        {
            if (!IsAuthoritative) return false;
            RefreshWorldBinding();
            return TryActor(actor, out var registration) && (executor == null || ReferenceEquals(registration.Executor, executor));
        }

        public bool TryAttachExecutor(NetworkObject actor, ICombatCommandExecutor executor, Func<ulong, bool> isAssigned)
        {
            if (!IsAuthoritative || executor == null || isAssigned == null || !TryActor(actor, out var registration) ||
                registration.Executor != null && !ReferenceEquals(registration.Executor, executor)) return false;
            registration.Executor = executor;
            registration.IsAssigned = isAssigned;
            return true;
        }

        public void CopyRegisteredActors(List<NetworkObject> destination)
        {
            destination.Clear();
            if (!IsAuthoritative) return;
            RefreshWorldBinding();
            Sweep();
            foreach (var actor in actors.Values)
                if (IsValidIdentity(actor.Identity) && Alive(actor)) destination.Add(actor.Identity);
        }

        // Attach this component to the same NetworkManager used by the actor RPCs.
        private void OnEnable()
        {
            manager = GetComponent<NetworkManager>();
            epoch = BitConverter.ToUInt64(Guid.NewGuid().ToByteArray(), 0);
            if (epoch == 0) epoch = 1;
            if (manager != null) manager.OnClientDisconnectCallback += OnDisconnect;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            RefreshWorldBinding();
        }

        private void OnDisable()
        {
            if (manager != null) manager.OnClientDisconnectCallback -= OnDisconnect;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            if (world != null) world.StateChanged -= OnWorldStateChanged;
            world = null;
            Clear(CombatActionEndReason.AuthorityDisabled);
        }

        public bool RegisterActor(NetworkObject actor, Func<bool> isAlive,
            ICombatCommandExecutor executor = null, Func<ulong, bool> isAssigned = null)
        {
            if (!IsAuthoritative || !IsValidIdentity(actor) || isAlive == null) return false;
            RefreshWorldBinding();
            // Explicit unregister is required to replace a registration. Do not reset cooldowns accidentally.
            if (actors.ContainsKey(actor.NetworkObjectId)) return false;
            if (!retainedCooldowns.TryGetValue(actor, out var cooldowns))
            {
                cooldowns = new Dictionary<ScriptableObject, double>();
                retainedCooldowns.Add(actor, cooldowns);
            }
            actors.Add(actor.NetworkObjectId, new ActorRegistration
            {
                Identity = actor, IsAlive = isAlive, Executor = executor,
                IsAssigned = isAssigned, Owner = actor.OwnerClientId, Cooldowns = cooldowns
            });
            return true;
        }

        // The per-actor registry is an equipped allowlist, not a global asset catalog.
        public bool RegisterSkill(NetworkObject actor, int skillId, ScriptableObject skill, CombatCommandPolicy policy)
        {
            if (!IsAuthoritative || skillId <= 0 || skill == null || policy == null ||
                !TryActor(actor, out var registration) || registration.Executor == null ||
                actions.ContainsKey(actor.NetworkObjectId) || registration.Skills.ContainsKey(skillId)) return false;
            registration.Skills.Add(skillId, new SkillRegistration { Skill = skill, Policy = policy });
            return true;
        }

        public bool UnregisterSkill(NetworkObject actor, int skillId)
        {
            if (!IsAuthoritative || !TryActor(actor, out var registration)) return false;
            if (actions.TryGetValue(actor.NetworkObjectId, out var action) && action.Context.SkillId == skillId)
                Finish(action.Context.Ticket, CombatActionEndReason.Cancelled);
            return registration.Skills.Remove(skillId);
        }

        public bool TryAcquire(object owner, NetworkObject player, NetworkObject target,
            ICombatTarget combatTarget, out CombatCinematicReservationLease lease)
        {
            lease = null;
            if (!IsAuthoritative || owner == null || player == target ||
                !TryActor(player, out var source) || !TryActor(target, out var victim) ||
                !Alive(source) || !Alive(victim) || !Assigned(source, source.Owner) ||
                combatTarget?.Root == null || combatTarget.Root.GetComponentInParent<NetworkObject>() != target) return false;
            RefreshWorldBinding();
            Sweep();
            if (!TryActor(player, out _) || !TryActor(target, out _) ||
                reservations.ContainsKey(player.NetworkObjectId) || reservations.ContainsKey(target.NetworkObjectId) ||
                actions.ContainsKey(player.NetworkObjectId) || actions.ContainsKey(target.NetworkObjectId) ||
                nextGeneration == ulong.MaxValue) return false;
            var ticket = new CombatActionTicket(epoch, player.NetworkObjectId, ++nextGeneration);
            var candidate = new CombatCinematicReservationLease(owner, player.transform, combatTarget, ticket,
                player.NetworkObjectId, target.NetworkObjectId, ReleaseLease);
            if (!candidate.ParticipantsAlive()) return false;
            reservations.Add(candidate.PlayerId, ticket);
            reservations.Add(candidate.TargetId, ticket);
            cinematicLeases.Add(candidate, Time.unscaledTimeAsDouble + 60);
            lease = candidate;
            return true;
        }

        private void ReleaseLease(CombatCinematicReservationLease lease)
        {
            cinematicLeases.Remove(lease);
            Release(lease.PlayerId, lease.Ticket);
            Release(lease.TargetId, lease.Ticket);
        }

        public void UnregisterActor(NetworkObject actor, CombatActionEndReason reason = CombatActionEndReason.Despawn)
        {
            if (!IsAuthoritative || actor == null || !actors.TryGetValue(actor.NetworkObjectId, out var registration) || registration.Identity != actor) return;
            RemoveActor(actor.NetworkObjectId, reason);
        }

        public CombatCommandRejection TryExecute(ulong senderClientId, NetworkObject actor, ulong sequence,
            int skillId, NetworkObject target, Vector3 direction, out CombatActionTicket ticket)
        {
            ticket = default;
            if (!IsAuthoritative) return CombatCommandRejection.NotServer;
            if (executing) return CombatCommandRejection.Busy;
            executing = true;
            try { return Execute(senderClientId, actor, sequence, skillId, target, direction, out ticket); }
            finally { executing = false; }
        }

        // Trusted server continuation after a confirmed hold; it does not consume the owner's RPC sequence.
        internal CombatCommandRejection TryExecuteContinuation(NetworkObject actor, int skillId, Vector3 direction, out CombatActionTicket ticket)
        {
            ticket = default;
            if (!IsAuthoritative || executing || !TryActor(actor, out var registration)) return CombatCommandRejection.Busy;
            executing = true;
            try { return Execute(actor.OwnerClientId, actor, registration.LastSequence, skillId, null, direction, out ticket, true); }
            finally { executing = false; }
        }

        // Read-only admission for one bounded intent. Execution still consumes sequence/rate through TryExecute.
        internal bool CanDeferCommand(ulong sender, NetworkObject actor, ulong sequence, int skillId)
        {
            return IsAuthoritative && TryActor(actor, out var registration) &&
                actor.OwnerClientId == sender && registration.Owner == sender && manager.ConnectedClients.ContainsKey(sender) &&
                Assigned(registration, sender) && Alive(registration) && sequence > registration.LastSequence &&
                Time.unscaledTimeAsDouble >= registration.NextRequestAt && registration.Executor != null &&
                registration.Skills.ContainsKey(skillId);
        }

        private CombatCommandRejection Execute(ulong senderClientId, NetworkObject actor, ulong sequence,
            int skillId, NetworkObject target, Vector3 direction, out CombatActionTicket ticket, bool trustedContinuation = false)
        {
            ticket = default;
            RefreshWorldBinding();
            Sweep();
            if (!TryActor(actor, out var registration)) return CombatCommandRejection.UnregisteredActor;
            if (actor.OwnerClientId != senderClientId || registration.Owner != senderClientId ||
                !manager.ConnectedClients.ContainsKey(senderClientId)) return CombatCommandRejection.Ownership;
            if (!Assigned(registration, senderClientId)) return CombatCommandRejection.Assignment;
            if (!trustedContinuation && (sequence == 0 || sequence <= registration.LastSequence)) return CombatCommandRejection.Replay;
            if (!trustedContinuation) registration.LastSequence = sequence;
            double now = Time.unscaledTimeAsDouble;
            if (now < registration.NextRequestAt) return CombatCommandRejection.RateLimited;
            registration.NextRequestAt = now + 0.05;
            if (!CombatCommandPolicy.Finite(direction)) return CombatCommandRejection.InvalidCommand;
            if (!Alive(registration)) return CombatCommandRejection.Dead;
            if (reservations.ContainsKey(actor.NetworkObjectId))
                return CombatCommandRejection.Busy;
            if (registration.Executor == null || !registration.Skills.TryGetValue(skillId, out var skill) || skill.Skill == null)
                return CombatCommandRejection.UnknownSkill;
            var policy = skill.Policy;
            if (policy.TargetMode == CombatCommandTargetMode.Self) target = actor;
            if (policy.TargetMode == CombatCommandTargetMode.None && target != null ||
                policy.TargetMode == CombatCommandTargetMode.Required && (target == null || target == actor) ||
                policy.TargetMode == CombatCommandTargetMode.RequiredOrSelf && target == null)
                return CombatCommandRejection.InvalidTarget;
            if (target != null)
            {
                if (!TryActor(target, out var targetRegistration) || !Alive(targetRegistration))
                    return CombatCommandRejection.InvalidTarget;
                if (reservations.ContainsKey(target.NetworkObjectId)) return CombatCommandRejection.Busy;
                if (!InRange(actor, target, policy.MinimumStartRange, policy.MaximumStartRange, policy.MaximumVerticalSeparation))
                    return CombatCommandRejection.OutOfRange;
                if (policy.ReserveCinematicPair && actions.ContainsKey(target.NetworkObjectId)) return CombatCommandRejection.Busy;
            }
            if (registration.Cooldowns.TryGetValue(skill.Skill, out double readyAt) && Time.timeAsDouble < readyAt)
                return CombatCommandRejection.Cooldown;
            if (nextGeneration == ulong.MaxValue) return CombatCommandRejection.InvalidCommand;
            var candidate = new CombatActionTicket(epoch, actor.NetworkObjectId, ++nextGeneration);
            var planar = new Vector3(direction.x, 0, direction.z);
            // Normalize after bounding input to avoid overflow in sqrMagnitude.
            float largest = Mathf.Max(Mathf.Abs(planar.x), Mathf.Abs(planar.z));
            if (largest > 0) planar = (planar / largest).normalized;
            var context = new CombatCommandContext(candidate, actor, target, skill.Skill, skillId, planar);
            try
            {
                if (actions.TryGetValue(actor.NetworkObjectId, out var outgoing))
                {
                    if (!(registration.Executor is ICombatCommandReplacementExecutor replacement) ||
                        !replacement.CanReplace(outgoing.Context, context)) return CombatCommandRejection.Busy;
                    Finish(outgoing.Context.Ticket, CombatActionEndReason.Cancelled);
                }
                if (!registration.Executor.CanStart(context)) return CombatCommandRejection.ExecutorRejected;
            }
            catch (Exception exception) { Debug.LogException(exception, this); return CombatCommandRejection.ExecutorFailed; }
            if (!TryActor(actor, out var currentActor) || !ReferenceEquals(currentActor, registration) ||
                !Alive(registration) || !Assigned(registration, senderClientId) ||
                actions.ContainsKey(actor.NetworkObjectId) || reservations.ContainsKey(actor.NetworkObjectId))
                return CombatCommandRejection.ExecutorRejected;
            var state = new ActionState
            {
                Context = context, Executor = registration.Executor, Policy = policy,
                ExpiresAt = now + policy.MaximumDurationSeconds,
                HasTarget = target != null, TargetId = target != null ? target.NetworkObjectId : 0
            };
            actions.Add(actor.NetworkObjectId, state);
            if (policy.ReserveCinematicPair)
            {
                reservations.Add(actor.NetworkObjectId, candidate);
                reservations.Add(target.NetworkObjectId, candidate);
            }
            // Install cooldown before execution to close reentrant start paths. Failed starts retain it.
            registration.Cooldowns[skill.Skill] = Time.timeAsDouble + policy.CooldownSeconds;
            try
            {
                if (!state.Executor.TryStart(context))
                {
                    Finish(candidate, CombatActionEndReason.ExecutorFailed);
                    return CombatCommandRejection.ExecutorRejected;
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                Finish(candidate, CombatActionEndReason.ExecutorFailed);
                return CombatCommandRejection.ExecutorFailed;
            }
            if (!TryState(candidate, out _)) return CombatCommandRejection.ExecutorFailed;
            state.Started = true;
            ticket = candidate;
            return CombatCommandRejection.None;
        }

        public bool IsCurrent(CombatActionTicket ticket)
        {
            if (!IsAuthoritative) return false;
            RefreshWorldBinding();
            Sweep();
            return TryState(ticket, out var state) && state.Started;
        }

        /// <summary>Call only from a server-authoritative impact/contact event before applying server damage.</summary>
        public bool TryConsumeImpact(CombatActionTicket ticket, NetworkObject target, int impactEvent = 0)
        {
            if (checkingImpact || !IsCurrent(ticket) || !TryState(ticket, out var state) ||
                impactEvent < 0 || impactEvent >= state.Policy.ImpactEventCount ||
                !TryActor(target, out var victim) || !Alive(victim) ||
                (state.Context.Target != null && state.Context.Target != target && state.Policy.TargetMode != CombatCommandTargetMode.Optional) ||
                state.Impacts.Contains((target.NetworkObjectId, impactEvent)) ||
                !InRange(state.Context.Actor, target, 0, state.Policy.MaximumImpactRange, state.Policy.MaximumVerticalSeparation)) return false;
            if (reservations.TryGetValue(target.NetworkObjectId, out var reserved) && !Same(reserved, ticket)) return false;
            checkingImpact = true;
            try
            {
                if (!state.Executor.CanApplyImpact(state.Context, target, impactEvent)) return false;
            }
            catch (Exception exception) { Debug.LogException(exception, this); return false; }
            finally { checkingImpact = false; }
            // The callback may have cancelled the action (for example on a lethal contact).
            return IsCurrent(ticket) && state.Impacts.Add((target.NetworkObjectId, impactEvent));
        }

        public bool Complete(CombatActionTicket ticket) => IsAuthoritative && Finish(ticket, CombatActionEndReason.Completed);
        public bool Cancel(CombatActionTicket ticket) => IsAuthoritative && Finish(ticket, CombatActionEndReason.Cancelled);
        public bool IsReserved(NetworkObject actor)
        {
            if (!IsAuthoritative || actor == null) return false;
            RefreshWorldBinding();
            Sweep();
            return reservations.ContainsKey(actor.NetworkObjectId);
        }

        public void ClearForSceneTransition()
        {
            if (IsAuthoritative) Clear(CombatActionEndReason.SceneChanged);
        }

        private bool Finish(CombatActionTicket ticket, CombatActionEndReason reason)
        {
            if (!TryState(ticket, out var state)) return false;
            actions.Remove(ticket.ActorId);
            Release(ticket.ActorId, ticket);
            if (state.HasTarget) Release(state.TargetId, ticket);
            try { state.Executor.End(state.Context, reason); }
            catch (Exception exception) { Debug.LogException(exception, this); }
            return true;
        }

        private void Release(ulong actorId, CombatActionTicket ticket)
        {
            if (reservations.TryGetValue(actorId, out var current) && Same(current, ticket)) reservations.Remove(actorId);
        }

        private bool TryState(CombatActionTicket ticket, out ActionState state)
        {
            state = null;
            return ticket.AuthorityEpoch == epoch && ticket.Generation != 0 &&
                actions.TryGetValue(ticket.ActorId, out state) && Same(state.Context.Ticket, ticket);
        }

        private static bool Same(CombatActionTicket a, CombatActionTicket b) =>
            a.AuthorityEpoch == b.AuthorityEpoch && a.ActorId == b.ActorId && a.Generation == b.Generation;

        private bool IsValidIdentity(NetworkObject actor) => actor != null && actor.IsSpawned &&
            actor.gameObject.scene.IsValid() && actor.gameObject.scene.isLoaded &&
            actor.NetworkManager == manager && manager.SpawnManager != null &&
            manager.SpawnManager.SpawnedObjects.TryGetValue(actor.NetworkObjectId, out var spawned) && spawned == actor;

        private bool TryActor(NetworkObject actor, out ActorRegistration registration)
        {
            registration = null;
            return IsValidIdentity(actor) && actors.TryGetValue(actor.NetworkObjectId, out registration) && registration.Identity == actor;
        }

        private static bool Alive(ActorRegistration actor)
        {
            try { return actor.Identity != null && actor.Identity.gameObject.activeInHierarchy && actor.IsAlive(); }
            catch { return false; }
        }

        private static bool Assigned(ActorRegistration actor, ulong sender)
        {
            try { return actor.IsAssigned != null && actor.IsAssigned(sender); }
            catch { return false; }
        }

        private static bool InRange(NetworkObject actor, NetworkObject target, float minimum, float maximum, float vertical)
        {
            Vector3 delta = target.transform.position - actor.transform.position;
            if (!CombatCommandPolicy.Finite(delta) || Mathf.Abs(delta.y) > vertical) return false;
            double squared = (double)delta.x * delta.x + (double)delta.z * delta.z;
            return squared >= (double)minimum * minimum && squared <= (double)maximum * maximum;
        }

        private void Update()
        {
            RefreshWorldBinding();
            if (IsAuthoritative) Sweep();
            else if (actors.Count != 0 || actions.Count != 0) Clear(CombatActionEndReason.AuthorityDisabled);
        }

        private void Sweep()
        {
            foreach (var pair in new List<KeyValuePair<ulong, ActorRegistration>>(actors))
            {
                var actor = pair.Value;
                if (!IsValidIdentity(actor.Identity)) RemoveActor(pair.Key, CombatActionEndReason.Despawn);
                else if (!Alive(actor)) RemoveActor(pair.Key, CombatActionEndReason.Death);
                else if (actor.Owner != actor.Identity.OwnerClientId) RemoveActor(pair.Key, CombatActionEndReason.OwnershipChanged);
            }
            foreach (var state in new List<ActionState>(actions.Values))
            {
                if (Time.unscaledTimeAsDouble >= state.ExpiresAt) Finish(state.Context.Ticket, CombatActionEndReason.Expired);
                else if (actors.TryGetValue(state.Context.Ticket.ActorId, out var actor) && !Assigned(actor, actor.Owner))
                    Finish(state.Context.Ticket, CombatActionEndReason.Cancelled);
            }
            foreach (var pair in new List<KeyValuePair<CombatCinematicReservationLease, double>>(cinematicLeases))
                if (!pair.Key.ParticipantsAlive() || Time.unscaledTimeAsDouble >= pair.Value ||
                    !actors.TryGetValue(pair.Key.PlayerId, out var source) || !Assigned(source, source.Owner))
                    pair.Key.Release();
        }

        private void RemoveActor(ulong actorId, CombatActionEndReason reason)
        {
            // Remove registration first so an End callback cannot restart a retiring actor.
            actors.TryGetValue(actorId, out var registration);
            actors.Remove(actorId);
            foreach (var lease in new List<CombatCinematicReservationLease>(cinematicLeases.Keys))
                if (lease.PlayerId == actorId || lease.TargetId == actorId) lease.Release();
            foreach (var state in new List<ActionState>(actions.Values))
                if (state.Context.Ticket.ActorId == actorId ||
                    state.HasTarget && state.TargetId == actorId)
                    Finish(state.Context.Ticket, reason);
            reservations.Remove(actorId);
            if (registration != null)
            {
                try { ActorUnregistered?.Invoke(registration.Identity, reason); }
                catch (Exception exception) { Debug.LogException(exception, this); }
            }
        }

        private void Clear(CombatActionEndReason reason)
        {
            if (clearing) return;
            clearing = true;
            WorldGeneration++;
            try
            {
                foreach (var state in new List<ActionState>(actions.Values)) Finish(state.Context.Ticket, reason);
                foreach (var lease in new List<CombatCinematicReservationLease>(cinematicLeases.Keys)) lease.Release();
                reservations.Clear();
                actors.Clear();
                retainedCooldowns.Clear();
                try { WorldInvalidated?.Invoke(); }
                catch (Exception exception) { Debug.LogException(exception, this); }
            }
            finally { clearing = false; }
        }

        private void OnDisconnect(ulong clientId)
        {
            foreach (var pair in new List<KeyValuePair<ulong, ActorRegistration>>(actors))
                if (pair.Value.Owner == clientId) RemoveActor(pair.Key, CombatActionEndReason.Disconnect);
        }

        private void RefreshWorldBinding()
        {
            var current = NavMeshWorldService.Instance;
            if (ReferenceEquals(world, current)) return;
            if (world != null) world.StateChanged -= OnWorldStateChanged;
            // Replacement of the world service is a world boundary, not an additive scene change.
            Clear(CombatActionEndReason.SceneChanged);
            world = current;
            if (world != null) world.StateChanged += OnWorldStateChanged;
        }

        private void OnWorldStateChanged(NavMeshWorldState state)
        {
            if (state == NavMeshWorldState.Invalidating || state == NavMeshWorldState.Unloaded)
                Clear(CombatActionEndReason.SceneChanged);
        }

        private void OnSceneUnloaded(Scene scene)
        {
            foreach (var pair in new List<KeyValuePair<ulong, ActorRegistration>>(actors))
                if (pair.Value.Identity == null || pair.Value.Identity.gameObject.scene == scene)
                    RemoveActor(pair.Key, CombatActionEndReason.SceneChanged);
        }
    }
}
