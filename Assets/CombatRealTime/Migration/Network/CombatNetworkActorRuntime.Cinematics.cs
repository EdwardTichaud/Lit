using System;
using Unity.Netcode;
using UnityEngine;

namespace Lit.Combat.Migration.Network
{
    public enum CombatNetworkCinematicKind : byte { None, Skill, Counter, Light }

    public struct CombatNetworkCinematicSnapshot : INetworkSerializable, IEquatable<CombatNetworkCinematicSnapshot>
    {
        public ulong Generation, World;
        public bool Active;
        public CombatNetworkCinematicKind Kind;
        public int SkillId;
        public NetworkObjectReference Target;
        public double StartedAt;
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Generation); serializer.SerializeValue(ref World);
            serializer.SerializeValue(ref Active); serializer.SerializeValue(ref Kind);
            serializer.SerializeValue(ref SkillId); serializer.SerializeValue(ref Target);
            serializer.SerializeValue(ref StartedAt);
        }
        public bool Equals(CombatNetworkCinematicSnapshot other) => Generation == other.Generation && World == other.World &&
            Active == other.Active && Kind == other.Kind && SkillId == other.SkillId && Target.Equals(other.Target) && StartedAt.Equals(other.StartedAt);
    }

    public sealed partial class CombatNetworkActorRuntime
    {
        [Header("Participant Cinematics")]
        [SerializeField] private CombatCinematicPlaybackService actorCinematicPlayback;
        [SerializeField] private CounterSkillSO networkCounterSkill;
        [SerializeField, Min(1)] private float networkClarityForS = 100;
        private readonly NetworkVariable<CombatNetworkCinematicSnapshot> cinematicState = new NetworkVariable<CombatNetworkCinematicSnapshot>();
        private readonly NetworkVariable<float> cinematicClarity = new NetworkVariable<float>(0, NetworkVariableReadPermission.Owner);
        private CombatCinematicReservationLease cinematicLease;
        private CombatActionTicket cinematicTicket;
        private ICombatTarget cinematicVictim;
        private UnityEngine.Object cinematicDefinition;
        private LitOpsiveLocomotionBridge.ExternalLockHandle cinematicMotionLock;
        private ulong shownCinematicGeneration, nextCinematicGeneration;
        private bool cinematicInputOwned, cinematicImpactConsumed, applyingCinematicImpact, stoppingCinematic;
        private bool cinematicConfirmationPending;
        private double cinematicDeadline;
        private int cinematicPresentationGeneration;

        public float NetworkClarity => cinematicClarity.Value;
        public bool HasNetworkCinematic => cinematicState.Value.Active;
        public float NetworkClarityForS => networkClarityForS;

        public bool ConfigureNetworkCinematics(CombatCinematicPlaybackService playback, CounterSkillSO counter, float clarityForS)
        {
            if (IsSpawned || playback == null || !CombatCommandPolicy.Finite(clarityForS) || clarityForS <= 0) return false;
            actorCinematicPlayback = playback; networkCounterSkill = counter; networkClarityForS = clarityForS;
            return true;
        }

        // Parent calls once after actor/authority registration, and every Update. No extra Update/engine.
        internal void TickNetworkCinematics()
        {
            if (!IsSpawned) return;
            var state = cinematicState.Value;
            if (IsServer && state.Active && cinematicConfirmationPending && authority != null && authority.IsCurrent(cinematicTicket))
            {
                cinematicConfirmationPending = false;
                PublishCooldowns();
                if (catalog.TryGet(state.SkillId, out var confirmed))
                {
                    RaiseConfirmedAction(confirmed.skill, cinematicTicket);
                    ConfirmedSupportClientRpc(confirmed.Id, new Unity.Collections.FixedString64Bytes(confirmed.CanonicalGuid),
                        cinematicTicket.AuthorityEpoch, cinematicTicket.Generation, GetSkillCooldownRemaining(confirmed.skill), NetworkManager.ServerTime.Time, Time.timeScale);
                }
            }
            if (IsServer && state.Active && (!admitted || !IsAlive() || authority == null ||
                admittedWorld != state.World || !IsAssigned(OwnerClientId) || cinematicVictim?.Root == null ||
                animator == null || !animator.isActiveAndEnabled || presentation == null ||
                !presentation.IsCurrentSession(cinematicPresentationGeneration) ||
                cinematicVictim.IsDead || Time.unscaledTimeAsDouble >= cinematicDeadline ||
                cinematicLease != null && !cinematicLease.IsActive))
            { StopNetworkCinematic(false); return; }
            if (!IsServer && state.Active && shownCinematicGeneration != state.Generation)
            {
                if (!state.Target.TryGet(out var target, NetworkManager) || ResolveEnemy(target)?.IsDead != false) return;
                if (!TryResolveCinematicDefinition(state.Kind, state.SkillId, out var definition)) return;
                if (BeginParticipantPlayback(state, definition, ResolveEnemy(target), out string error)) shownCinematicGeneration = state.Generation;
                else { CleanupParticipantPlayback(); shownCinematicGeneration = state.Generation; Diagnose("Confirmed cinematic presentation failed: " + error); }
            }
            else if (!state.Active && (shownCinematicGeneration != 0 || cinematicMotionLock != null)) CleanupParticipantPlayback();
        }

        public static bool IsNetworkCinematicParticipant(Transform root)
        {
            var manager = Unity.Netcode.NetworkManager.Singleton;
            if (root == null || manager == null || !manager.IsListening || manager.SpawnManager == null) return false;
            var identity = root.GetComponentInParent<NetworkObject>();
            if (identity == null) return false;
            foreach (var pair in manager.SpawnManager.SpawnedObjects)
            {
                var runtime = pair.Value.GetComponent<CombatNetworkActorRuntime>();
                if (runtime == null || !runtime.cinematicState.Value.Active) continue;
                if (runtime.NetworkObject == identity) return true;
                if (runtime.cinematicState.Value.Target.TryGet(out var target, manager) && target == identity) return true;
            }
            return false;
        }

        internal void GrantNetworkClarity(float amount)
        {
            if (IsServer && IsAssignedPlayer && CombatCommandPolicy.Finite(amount) && amount > 0 &&
                CombatCommandPolicy.Finite(networkClarityForS) && networkClarityForS > 0)
                cinematicClarity.Value = Mathf.Clamp(cinematicClarity.Value + amount, 0, networkClarityForS * 1.2f);
        }

        internal void ResetNetworkCinematicStateForSpawn()
        {
            StopNetworkCinematic();
            ClearNetworkReaction();
            if (IsServer && IsSpawned) { cinematicState.Value = default; cinematicClarity.Value = 0; }
        }

        internal void ClearNetworkCombatClarity()
        {
            if (IsServer && IsSpawned) cinematicClarity.Value = 0;
        }

        private bool TryResolveCinematicDefinition(CombatNetworkCinematicKind kind, int id, out UnityEngine.Object definition)
        {
            definition = null;
            if (kind == CombatNetworkCinematicKind.Skill)
            { if (catalogValid && catalog.TryGet(id, out var entry) && entry.skill.HasCombatCinematic) definition = entry.skill; }
            else if (catalogValid && catalog.TryGetCinematic(id, out var cinematic) &&
                (kind == CombatNetworkCinematicKind.Counter && cinematic is CounterSkillSO || kind == CombatNetworkCinematicKind.Light && cinematic is LightSkillSO))
                definition = cinematic;
            return definition != null;
        }

        private static bool GetCinematicRig(UnityEngine.Object definition, out CombatCinematicRig rig, out string playerTrack, out string enemyTrack)
        {
            rig = null; playerTrack = enemyTrack = null;
            if (definition is SkillSO skill && skill.HasCombatCinematic)
            { rig = skill.Cinematic.CombatCinematicRigPrefab; playerTrack = skill.Cinematic.PlayerAnimatorTrackName; enemyTrack = skill.Cinematic.EnemyAnimatorTrackName; }
            else if (definition is CounterSkillSO counter)
            { rig = counter.CombatCinematicRigPrefab; playerTrack = counter.PlayerAnimatorTrackName; enemyTrack = counter.EnemyAnimatorTrackName; }
            else if (definition is LightSkillSO light)
            { rig = light.CombatCinematicRigPrefab; playerTrack = light.PlayerAnimatorTrackName; enemyTrack = light.EnemyAnimatorTrackName; }
            return rig != null && rig.RuntimeTimeline != null && !string.IsNullOrWhiteSpace(playerTrack) && !string.IsNullOrWhiteSpace(enemyTrack);
        }

        private CombatCinematicContext ParticipantContext(UnityEngine.Object definition, ICombatTarget target, Action impact)
        {
            var local = RealTimeCombatManager.Instance;
            bool ownsView = IsOwner && local != null && local.PlayerRoot == transform;
            return new CombatCinematicContext(local, definition, impact, allowGameplayCameraFallback: true,
                transferTimelineRootMotion: IsServer, allowMissingTimelineBindings: true, explicitTarget: target,
                explicitPlayer: transform, explicitPlayerAnimator: animator, controlsLocalCamera: ownsView,
                appliesGameplayEffects: IsServer);
        }

        internal bool CanStartNetworkCinematic(CombatCommandContext context)
        {
            if (context.Skill is not SkillSO skill || !Owns(skill) || context.Target == null ||
                !catalog.TryGet(context.SkillId, out var entry) || !EnemyEligible(context.Target, entry, false)) return false;
            if (skill.targetingMode == SkillTargetingMode.LockedTarget && serverManualLock != context.Target) return false;
            var victim = ResolveEnemy(context.Target);
            return victim != null && !victim.IsAttackCommitted && CanPresentCinematic(skill, victim, out _);
        }

        internal bool TryNetworkCinematicPolicy(CombatNetworkSkillCatalog.Entry entry, out CombatCommandPolicy policy)
        {
            policy = null;
            var skill = entry.skill;
            if (!GetCinematicRig(skill, out var rig, out _, out _) || double.IsNaN(rig.RuntimeTimeline.duration) ||
                double.IsInfinity(rig.RuntimeTimeline.duration) || rig.RuntimeTimeline.duration <= 0 || rig.RuntimeTimeline.duration > 56) return false;
            float minimum = skill.useLaunchRange ? skill.minimumLaunchDistance : skill.minimumHitDistance;
            float maximum = skill.useLaunchRange ? skill.maximumLaunchDistance : skill.maximumHitDistance;
            try
            {
                policy = new CombatCommandPolicy(CombatCommandTargetMode.Required, minimum, maximum, entry.heightTolerance,
                    skill.cooldownSeconds, (float)rig.RuntimeTimeline.duration + 2, true, 1, Mathf.Max(maximum, skill.maximumHitDistance));
                return true;
            }
            catch (ArgumentOutOfRangeException) { return false; }
        }

        private bool CanPresentCinematic(UnityEngine.Object definition, ICombatTarget target, out string error)
        {
            error = "Actor cinematic pool, presentation, participant or rig unavailable.";
            if (!IsServer || !admitted || authority == null || !IsAlive() || !IsAssigned(OwnerClientId) ||
                HasNetworkCinematic || actorCinematicPlayback == null || actorCinematicPlayback.IsPlaying ||
                presentation == null || animator == null || !animator.isActiveAndEnabled || bridge == null || !bridge.Grounded ||
                bridge.IsCinematicMotionSessionActive || bridge.IsExternalLockActive && !presentation.OwnsOnlyActionMotionLock(bridge) ||
                target?.Root == null || target.IsDead || !GetCinematicRig(definition, out var rig, out var playerTrack, out var enemyTrack)) return false;
            var context = ParticipantContext(definition, target, null);
            if (double.IsNaN(rig.RuntimeTimeline.duration) || double.IsInfinity(rig.RuntimeTimeline.duration) ||
                rig.RuntimeTimeline.duration <= 0 || rig.RuntimeTimeline.duration > 56)
            { error = "Cinematic duration is invalid or exceeds the existing reservation lifetime."; return false; }
            if (!rig.ValidateSkillPlayback(context, playerTrack, enemyTrack, out error)) return false;
            if (rig.HasAuthoringStageLayout && (!rig.TryGetMidpointPlacement(context, out var placement, out error) ||
                !CounterSkillCombatController.ValidateParticipantStage(context, placement, out error))) return false;
            return true;
        }

        // Ordinary skills already own the pair reservation through their CombatCommandPolicy.
        internal bool StartNetworkCinematic(CombatCommandContext context)
        {
            if (!CanStartNetworkCinematic(context)) return false;
            cinematicTicket = context.Ticket;
            publishPending = supportPending = false;
            bool started = StartServerCinematic(CombatNetworkCinematicKind.Skill, context.SkillId, context.Skill, ResolveEnemy(context.Target));
            cinematicConfirmationPending = started;
            return started;
        }

        private bool StartServerCinematic(CombatNetworkCinematicKind kind, int id, UnityEngine.Object definition, ICombatTarget target)
        {
            if (nextCinematicGeneration == ulong.MaxValue) return false;
            cinematicDefinition = definition; cinematicVictim = target; cinematicImpactConsumed = false;
            var state = new CombatNetworkCinematicSnapshot { Active = true, Generation = ++nextCinematicGeneration,
                World = admittedWorld, Kind = kind, SkillId = id, Target = new NetworkObjectReference(target.Root.GetComponentInParent<NetworkObject>()),
                StartedAt = NetworkManager.ServerTime.Time };
            cinematicState.Value = state;
            if (!BeginParticipantPlayback(state, definition, target, out string error))
            { state.Active = false; cinematicState.Value = state; CleanupParticipantPlayback(); cinematicDefinition = null; cinematicVictim = null; return Diagnose("Cinematic start rejected: " + error); }
            GetCinematicRig(definition, out var rig, out _, out _);
            cinematicDeadline = Time.unscaledTimeAsDouble + Math.Min(58, Math.Max(1, rig.RuntimeTimeline.duration + 2));
            cinematicState.Value = state; shownCinematicGeneration = state.Generation;
            return true;
        }

        private bool BeginParticipantPlayback(CombatNetworkCinematicSnapshot state, UnityEngine.Object definition, ICombatTarget target, out string error)
        {
            error = "Participant presentation service missing.";
            if (actorCinematicPlayback == null || actorCinematicPlayback.IsPlaying || bridge == null || presentation == null ||
                !GetCinematicRig(definition, out var prefab, out var playerTrack, out var enemyTrack)) return false;
            cinematicVictim = target; cinematicDefinition = definition;
            if (presentation.IsActionActive) presentation.CancelAction();
            cinematicPresentationGeneration = presentation.BeginExternalPresentation(this);
            if (cinematicPresentationGeneration == 0 || !bridge.TryAcquireExternalLock(this, out cinematicMotionLock, disableGameplayInput: IsOwner)) return false;
            if (definition is SkillSO ordinary)
            {
                if (!presentation.ConfigureExternalSkill(cinematicPresentationGeneration, ordinary) ||
                    !presentation.SetExternalAttackContext(cinematicPresentationGeneration,
                        new PlayerAttackContext(cinematicPresentationGeneration, ordinary, transform.position,
                            Vector3.ProjectOnPlane(target.Root.position - transform.position, Vector3.up).normalized,
                            target, ordinary.targetingMode == SkillTargetingMode.LockedTarget))) return false;
            }
            if (IsServer) target.SetCinematicSuspended(true);
            var context = ParticipantContext(definition, target, () => ResolveNetworkCinematicImpact(state.Generation));
            if (context.ControlsLocalCamera)
            { InputModeCoordinator.Enter(this, InputMode.Cinematic); cinematicInputOwned = true; }
            CombatCinematicPlacement? placement = null;
            if (prefab.HasAuthoringStageLayout)
            {
                if (!prefab.TryGetMidpointPlacement(context, out var stage, out error)) return false;
                if (IsServer) placement = stage;
            }
            if (!actorCinematicPlayback.TryPlay(prefab, context, null, playerTrack, enemyTrack, placement,
                rig => OnNetworkCinematicStopped(state.Generation, rig), out error)) return false;
            var startSfx = definition is CounterSkillSO counter ? counter.StartSfx : definition is LightSkillSO light ? light.StartSfx : null;
            if (IsClient && startSfx != null) AudioManager.PlayClipAtPoint(startSfx, transform.position);
            if (!IsServer && actorCinematicPlayback.ActiveRig?.Director != null)
            {
                var director = actorCinematicPlayback.ActiveRig.Director;
                double age = Math.Max(0, NetworkManager.ServerTime.Time - state.StartedAt);
                director.time = Math.Min(age, Math.Max(0, director.duration - .0001));
                director.Evaluate();
            }
            return true;
        }

        private void OnNetworkCinematicStopped(ulong generation, CombatCinematicRig rig)
        {
            if (stoppingCinematic) return;
            if (IsServer)
            {
                if (cinematicState.Value.Generation == generation) StopNetworkCinematic(rig != null && rig.LastEndReason == CombatCinematicEndReason.Completed);
            }
            else if (shownCinematicGeneration == generation) CleanupParticipantPlayback();
        }

        // Actor AnimationEvents and Timeline signal adapters call this; never RPC an impact.
        internal bool TryHandleNetworkCinematicImpact(AnimationEvent source = null)
        {
            if (!HasNetworkCinematic || actorCinematicPlayback?.ActiveRig == null) return false;
            return actorCinematicPlayback.ActiveRig.TryResolveParticipantImpact(source);
        }

        private void ResolveNetworkCinematicImpact(ulong generation)
        {
            if (!IsServer || !HasNetworkCinematic || cinematicState.Value.Generation != generation || cinematicImpactConsumed ||
                cinematicVictim?.Root == null || cinematicVictim.IsDead || !IsAlive() || authority == null ||
                admittedWorld != cinematicState.Value.World) return;
            if (cinematicLease != null ? !cinematicLease.IsActive : !authority.IsCurrent(cinematicTicket)) return;
            var victim = cinematicVictim;
            var identity = victim.Root.GetComponentInParent<NetworkObject>();
            if (identity == null || !authority.IsRegistered(identity)) return;
            applyingCinematicImpact = true;
            try
            {
                if (cinematicState.Value.Kind == CombatNetworkCinematicKind.Skill && !authority.TryConsumeImpact(cinematicTicket, identity)) return;
                cinematicImpactConsumed = true;
                int damage = cinematicDefinition is SkillSO skill ? Mathf.Max(0, Mathf.RoundToInt(skill.Damages)) :
                    cinematicDefinition is CounterSkillSO counter ? counter.Damage : ((LightSkillSO)cinematicDefinition).Damage;
                var origin = cinematicDefinition is CounterSkillSO ? CombatImpactOrigin.Counter :
                    cinematicDefinition is LightSkillSO ? CombatImpactOrigin.LightSkill : CombatImpactOrigin.Basic;
                float interruption = cinematicDefinition is SkillSO ordinarySkill ? ordinarySkill.interruptionForce : 100;
                int applied = victim is ICombatImpactReceiver receiver ? receiver.ReceiveImpact(new CombatImpact(damage, interruption, origin, source: actor)) : 0;
                if (applied > 0 && cinematicDefinition is CounterSkillSO success) GrantNetworkClarity(success.ClarityGain);
                if (applied > 0 && cinematicDefinition is SkillSO ordinary) GrantNetworkClarity(ordinary.ClarityGainOnHit);
                if (applied > 0) CinematicDamageFeedbackClientRpc(new NetworkObjectReference(identity), applied, cinematicState.Value.Kind,
                    cinematicState.Value.SkillId);
                PublishHealthSnapshot(true);
            }
            finally { applyingCinematicImpact = false; }
        }

        [ClientRpc]
        private void CinematicDamageFeedbackClientRpc(NetworkObjectReference victim, int amount, CombatNetworkCinematicKind kind, int skillId)
        {
            if (amount > 0 && victim.TryGet(out var identity, NetworkManager))
            {
                CombatDamageWorldFeedback.ShowMessage(identity.transform, amount.ToString(), Color.white, 2);
                if (TryResolveCinematicDefinition(kind, skillId, out var definition))
                {
                    var sfx = definition is CounterSkillSO counter ? counter.ImpactSfx : definition is LightSkillSO light ? light.ImpactSfx : null;
                    if (sfx != null) AudioManager.PlayClipAtPoint(sfx, identity.transform.position);
                }
            }
        }

        internal bool CanApplyNetworkCinematicImpact(CombatCommandContext context, NetworkObject target) =>
            applyingCinematicImpact && SameTicket(context.Ticket, cinematicTicket) && HasNetworkCinematic &&
            cinematicVictim?.Root != null && target == cinematicVictim.Root.GetComponentInParent<NetworkObject>();

        internal void StopNetworkCinematic(bool completed = false)
        {
            if (stoppingCinematic) return;
            stoppingCinematic = true;
            try
            {
                bool ordinary = cinematicState.Value.Kind == CombatNetworkCinematicKind.Skill;
                cinematicConfirmationPending = false;
                if (IsServer && IsSpawned) { var state = cinematicState.Value; state.Active = false; cinematicState.Value = state; }
                CleanupParticipantPlayback();
                if (completed && cinematicDefinition is SkillSO skill)
                    CombatSkillCinematicController.ApplyParticipantExit(skill, animator, cinematicVictim);
                var lease = cinematicLease; cinematicLease = null; lease?.Release();
                if (IsServer && ordinary && authority != null && authority.IsCurrent(cinematicTicket))
                { if (completed) authority.Complete(cinematicTicket); else authority.Cancel(cinematicTicket); }
                cinematicDefinition = null; cinematicVictim = null; cinematicTicket = default;
            }
            finally { stoppingCinematic = false; }
        }

        private void CleanupParticipantPlayback()
        {
            shownCinematicGeneration = 0;
            actorCinematicPlayback?.StopActive();
            if (IsServer) cinematicVictim?.SetCinematicSuspended(false);
            cinematicMotionLock?.Dispose(); cinematicMotionLock = null;
            if (cinematicPresentationGeneration != 0 && presentation != null && presentation.IsCurrentSession(cinematicPresentationGeneration))
                presentation.EndExternalPresentation(cinematicPresentationGeneration);
            cinematicPresentationGeneration = 0;
            if (cinematicInputOwned) { InputModeCoordinator.Exit(this); cinematicInputOwned = false; }
        }

        public bool RequestLightCinematic(NetworkObject target)
        {
            if (!IsSpawned || !IsOwner || target == null || !target.IsSpawned || HasNetworkCinematic) return false;
            LightCinematicServerRpc(new NetworkObjectReference(target)); return true;
        }

        [ServerRpc]
        private void LightCinematicServerRpc(NetworkObjectReference reference, ServerRpcParams parameters = default)
        {
            if (!IsAssigned(parameters.Receive.SenderClientId) || !reference.TryGet(out var identity, NetworkManager) ||
                authority == null || !authority.IsRegistered(identity)) return;
            var skill = GetComponent<RealTimeCombatLoadout>()?.EquippedLightSkill;
            var target = ResolveEnemy(identity);
            float cost = skill != null ? networkClarityForS * RealTimeCombatManager.GetLightSkillClarityMultiplier(skill.RequiredRank) : float.PositiveInfinity;
            if (skill == null || !CombatCommandPolicy.Finite(cost) || cost <= 0 || cinematicClarity.Value < cost ||
                target == null || !skill.IsWithinCinematicStartRange(Vector3.ProjectOnPlane(target.Root.position - transform.position, Vector3.up).magnitude) ||
                !catalogValid || !catalog.TryGetCinematic(skill, out int lightId) ||
                !ClearLine(target.Root, ~0) || !CanPresentCinematic(skill, target, out _)) return;
            if (!TryAcquireSpecialCinematic(target)) return;
            if (StartServerCinematic(CombatNetworkCinematicKind.Light, lightId, skill, target)) cinematicClarity.Value -= cost;
            else { cinematicLease?.Release(); cinematicLease = null; }
        }

        private bool TryAcquireSpecialCinematic(ICombatTarget target)
        {
            if (hasAction) authority.Cancel(active.Ticket);
            StopCharge();
            if (!CombatCinematicReservations.TryAcquire(this, transform, target, out cinematicLease)) return false;
            cinematicLease.Released += OnCinematicLeaseReleased;
            return true;
        }

        private void OnCinematicLeaseReleased() { if (!stoppingCinematic) StopNetworkCinematic(); }
    }
}
