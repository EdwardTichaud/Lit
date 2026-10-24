using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Lit.Combat.Migration.Network
{
    /// <summary>Opt-in actor-owned server executor. Must be authored on the NetworkObject BEFORE spawn.</summary>
    [DefaultExecutionOrder(-100), DisallowMultipleComponent, RequireComponent(typeof(NetworkObject))]
    public sealed partial class CombatNetworkActorRuntime : NetworkBehaviour, ICombatCommandExecutor, ICombatCommandReplacementExecutor
    {
        [SerializeField] private CombatNetworkSkillCatalog catalog;
        private SquadCharacterController actor;
        private CharacterInfo characterHealth;
        private PlayerActionPresentationController presentation;
        private CharacterAnimationController animationActor;
        private Animator animator;
        private LitOpsiveLocomotionBridge bridge;
        private PlayerStateMotionController stateMotion;
        private NetworkCharacterInput input;
        private CombatCommandAuthority authority;
        private CombatWorldRuntime combatWorld;
        private WorldInteractionService assignmentService;
        private readonly List<NetworkObject> candidates = new List<NetworkObject>();
        private readonly HashSet<int> registeredSkills = new HashSet<int>();
        private readonly HashSet<string> diagnostics = new HashSet<string>();
        private bool catalogValid, everRegistered, admitted, spawnAdmissionPending;
        private ulong admittedWorld;
        private double nextRefresh;
        private CombatCommandContext active;
        private CombatNetworkSkillCatalog.Entry activeEntry;
        private CombatNetworkSkillFamily activeFamily;
        private bool hasAction, publishPending, supportPending, serverEventActive, movementSuppressed, ending;
        private int presentationSession;
        private double actionStartedAt;
        private bool observedAnimatorState;
        private CombatNetworkAnimationEvents relay;

        private readonly NetworkVariable<CombatNetworkActionSnapshot> actionState = new NetworkVariable<CombatNetworkActionSnapshot>();
        private readonly NetworkVariable<CombatNetworkHealthSnapshot> healthState = new NetworkVariable<CombatNetworkHealthSnapshot>();
        private readonly NetworkVariable<CombatNetworkProtectionSnapshot> protectionState = new NetworkVariable<CombatNetworkProtectionSnapshot>();
        private readonly NetworkVariable<CombatNetworkPassiveSnapshot> passiveState = new NetworkVariable<CombatNetworkPassiveSnapshot>();
        private readonly NetworkVariable<CombatNetworkThreatSnapshot> engagementState = new NetworkVariable<CombatNetworkThreatSnapshot>(
            default, NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);
        // NGO also synchronizes inactive scene actors, before Unity invokes
        // their Awake. Keep these instances alive from construction onward;
        // NetworkBehaviour owns their registration and disposal.
        private readonly NetworkList<CombatNetworkControlSnapshot> controlStates = new NetworkList<CombatNetworkControlSnapshot>();
        private readonly NetworkList<CombatNetworkCooldownSnapshot> cooldownStates =
            new NetworkList<CombatNetworkCooldownSnapshot>(null, NetworkVariableReadPermission.Owner);
        private readonly Dictionary<int, CombatNetworkCooldownSnapshot> acceptedCooldowns = new Dictionary<int, CombatNetworkCooldownSnapshot>();

        public string LastDiagnostic { get; private set; }
        public CombatNetworkSkillCatalog Catalog => catalog;
        public bool IsAssignedPlayer => IsSpawned && IsPlayer && IsAssigned(OwnerClientId);
        public event Action<SkillSO, CombatActionTicket> ConfirmedAction;
        public event Action<SkillSO, int, ICombatTarget> ServerImpactApplied;

        private void Awake()
        {
            ResolveReferences();
        }

        private void ResolveReferences()
        {
            actor = GetComponent<SquadCharacterController>();
            characterHealth = GetComponent<CharacterInfo>();
            presentation = GetComponent<PlayerActionPresentationController>();
            animationActor = GetComponent<CharacterAnimationController>();
            animator = animationActor != null ? animationActor.Animator : GetComponentInChildren<Animator>();
            bridge = GetComponent<LitOpsiveLocomotionBridge>();
            stateMotion = GetComponent<PlayerStateMotionController>();
            input = GetComponent<NetworkCharacterInput>();
        }

        public override void OnNetworkSpawn()
        {
            ResolveReferences();
            InitializeNetworkThresholdTransport();
            catalogValid = catalog != null && catalog.Rebuild(out _);
            appliedDodgeRevision = 0;
            appliedGuardRevision = 0;
            if (!catalogValid && IsPlayer) Diagnose("No valid authored network skill catalog; this actor can only be a registered target.");
            actionState.OnValueChanged += OnActionStateChanged;
            chargeSkill.OnValueChanged += OnChargeChanged;
            appliedHealthRevision = 0;
            healthState.OnValueChanged += OnHealthChanged;
            if (actor != null) actor.HealthChanged += OnActorHealthChanged;
            if (characterHealth != null) characterHealth.HealthChanged += OnCharacterHealthChanged;
            protectionState.OnValueChanged += OnProtectionChanged;
            passiveState.OnValueChanged += OnPassiveChanged;
            engagementState.OnValueChanged += OnEngagementChanged;
            controlStates.OnListChanged += OnControlStatesChanged;
            if (animator != null && IsPlayer)
            {
                relay = animator.GetComponent<CombatNetworkAnimationEvents>();
                if (relay == null) Diagnose("Player Animator is missing the authored CombatNetworkAnimationEvents relay; Basic execution is unavailable.");
                else if (!relay.Bind(this)) Diagnose("Animator already has another network combat owner.");
            }
            if (IsServer)
            {
                chargeHeld = chargeCommit = chargeRequiresRelease = false;
                chargePreparation = 0; chargeSkill.Value = 0;
                dodgeState.Value = default;
                guardState.Value = default;
                floatSkill.Value = 0;
                ResetNetworkCinematicStateForSpawn();
                nextNetworkCounterAt = 0;
                actionState.Value = default; passiveState.Value = default; protectionState.Value = default;
                controlStates.Clear(); cooldownStates.Clear();
                authority = NetworkManager.GetComponent<CombatCommandAuthority>();
                BindAuthorityEvents();
                spawnAdmissionPending = true;
                RefreshServerRegistration();
                PublishHealthSnapshot(true);
            }
            else
            {
                ApplyClientHealth();
                ApplyClientAction(actionState.Value);
                ApplyClientProtection(); ApplyClientPassive(); ReconcileClientControls();
            }
            ApplyOwnerEngagement();
        }

        public override void OnNetworkDespawn()
        {
            Retire(CombatActionEndReason.Despawn);
            actionState.OnValueChanged -= OnActionStateChanged;
            chargeSkill.OnValueChanged -= OnChargeChanged;
            healthState.OnValueChanged -= OnHealthChanged;
            if (actor != null) actor.HealthChanged -= OnActorHealthChanged;
            if (characterHealth != null) characterHealth.HealthChanged -= OnCharacterHealthChanged;
            protectionState.OnValueChanged -= OnProtectionChanged;
            passiveState.OnValueChanged -= OnPassiveChanged;
            engagementState.OnValueChanged -= OnEngagementChanged;
            controlStates.OnListChanged -= OnControlStatesChanged;
            if (relay != null) relay.Unbind(this);
        }

        public override void OnNetworkPreDespawn() => PublishHealthSnapshot(true);

        public override void OnGainedOwnership()
        {
            appliedThreatRevision = 0;
            if (!IsServer)
            {
                StopCharge(); // Recreate presentation with the new owner's camera scope.
                CleanupParticipantPlayback();
            }
            ApplyOwnerEngagement();
        }
        public override void OnLostOwnership()
        {
            appliedThreatRevision = 0;
            if (!IsServer) { StopCharge(); CleanupParticipantPlayback(); }
            else StopNetworkCinematic();
        }

        private void OnDisable()
        {
            if (IsSpawned) { PublishHealthSnapshot(true); Retire(CombatActionEndReason.Cancelled); }
        }

        private void OnEnable()
        {
            if (IsSpawned && IsServer)
            {
                if (authority != null && admittedWorld == authority.WorldGeneration) admitted = true;
                nextRefresh = 0;
            }
        }

        private bool IsPlayer => actor != null && actor.CharacterData != null && !actor.CharacterData.isEnemy;
        private bool IsAlive()
        {
            if (!isActiveAndEnabled || !gameObject.activeInHierarchy) return false;
            if (IsPlayer) return actor.isActiveAndEnabled && actor.CurrentHp > 0;
            var enemy = ResolveEnemy(NetworkObject);
            return enemy != null && !enemy.IsDead;
        }

        // Explicit admission after a world boundary. Persistent old-world roots are NOT auto-admitted.
        public bool AdmitToCurrentWorld()
        {
            if (!IsSpawned || !IsServer || !isActiveAndEnabled || authority == null || !authority.IsAuthoritative ||
                NavMeshWorldService.Instance != null && !NavMeshWorldService.Instance.IsReady) return false;
            admitted = true; admittedWorld = authority.WorldGeneration; spawnAdmissionPending = false;
            RefreshServerRegistration();
            return authority.IsRegistered(NetworkObject);
        }

        public bool TryGetSkillId(SkillSO skill, out int skillId)
        {
            skillId = 0;
            if (!catalogValid) return Diagnose("This actor has no valid authored network skill catalog.");
            if (!catalog.TryGet(skill, out var entry)) return Diagnose("Skill is not present in this actor's authored GUID network catalog.");
            if (!Owns(skill)) return Diagnose("Skill is not equipped in the actor's live loadout.");
            if (CombatNetworkSkillCatalog.Classify(skill, out string reason) == CombatNetworkSkillFamily.Unsupported)
                return Diagnose(reason);
            skillId = entry.Id;
            return true;
        }

        public bool TryRequestSkill(SkillSO skill, NetworkObject target, Vector3 direction)
        {
            if (!IsSpawned || !IsOwner || input == null) return Diagnose("Command was not queued: actor is unspawned, not owned, or missing NetworkCharacterInput.");
            if (!TryGetSkillId(skill, out int id)) return false;
            if (skill.targetingMode == SkillTargetingMode.LockedTarget)
            {
                var manager = RealTimeCombatManager.Instance;
                if (manager == null || manager.PlayerRoot != transform || manager.LockedCombatTarget?.Root != target?.transform)
                    return Diagnose("Skill requires a manually locked target belonging to this actor.");
                PublishManualLockIntent(true);
            }
            if (skill.targetingMode == SkillTargetingMode.FreeAttack || skill.targetingMode == SkillTargetingMode.CasterArea ||
                skill.targetingMode == SkillTargetingMode.AllyArea) target = null;
            return input.TryRequestCombatAction(id, target, direction) || Diagnose("Command was not queued: input is blocked, target identity invalid, or direction non-finite.");
        }

        private void Update()
        {
            if (!IsSpawned) return;
            if (IsOwner) PublishManualLockIntent();
            if (IsServer)
            {
                ValidatePersistentEffects();
                if (Time.unscaledTimeAsDouble >= nextRefresh)
                {
                    nextRefresh = Time.unscaledTimeAsDouble + .2;
                    RefreshServerRegistration();
                    PublishPersistentState();
                }
                MonitorServerAction();
            }
            else
            {
                ApplyClientHealth();
                if (actionState.Value.Active && presentationSession == 0) ApplyClientAction(actionState.Value);
                ReconcileClientControls();
                ApplyClientProtection();
                ApplyClientPassive();
            }
            if (IsOwner) ApplyOwnerEngagement();
            TickCharge();
            TickDodgePresentation();
            TickGuardPresentation();
            TickFloatPresentation();
            TickNetworkReactions();
            TickNetworkCinematics();
            TickClashReplication();
            if (!IsServer && clientEntry != null && presentation != null && presentation.IsCurrentSession(presentationSession))
            {
                var state = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
                presentation.UpdateExternalSkillProgress(presentationSession, state.normalizedTime);
                if (presentation.IsRecoveryOpen && clientEntry.skill.Presentation.allowMoveAfterRecovery) ReleaseMovementSuppression();
            }
        }

        private void LateUpdate()
        {
            if (!IsSpawned || !IsServer || !hasAction || authority == null) return;
            if (!authority.IsCurrent(active.Ticket)) return;
            if (publishPending)
            {
                publishPending = false;
                actionState.Value = new CombatNetworkActionSnapshot
                {
                    Epoch = active.Ticket.AuthorityEpoch, Generation = active.Ticket.Generation,
                    SkillId = activeEntry.Id, Guid = new FixedString64Bytes(activeEntry.CanonicalGuid),
                    Active = true, Direction = active.Direction, StartedAt = NetworkManager.ServerTime.Time,
                    HasTarget = active.Target != null, Target = active.Target != null ? new NetworkObjectReference(active.Target) : default,
                    CooldownRemaining = GetSkillCooldownRemaining(activeEntry.skill), CooldownTimeScale = Time.timeScale
                };
                PublishCooldowns();
                RaiseConfirmedAction(activeEntry.skill, active.Ticket);
            }
            if (supportPending)
            {
                supportPending = false;
                ExecuteSupport();
            }
        }

        private void RefreshServerRegistration()
        {
            if (!IsSpawned || !IsServer || !isActiveAndEnabled) return;
            if (authority == null) authority = NetworkManager.GetComponent<CombatCommandAuthority>();
            BindAuthorityEvents();
            if (authority == null || !authority.IsAuthoritative) { Diagnose("CombatCommandAuthority is missing/disabled on this NetworkManager."); return; }
            // Synchronize service replacement/invalidation before comparing the world generation.
            authority.IsRegistered(NetworkObject);
            if (admitted && admittedWorld != authority.WorldGeneration)
            {
                ReleaseParticipantFlows();
                admitted = false; spawnAdmissionPending = false; registeredSkills.Clear();
                ClearOwnedEffects(); StopLocalPresentation();
                Diagnose("World registration invalidated. World owner must call AdmitToCurrentWorld after admitting this actor to the new world.");
            }
            if (spawnAdmissionPending && (NavMeshWorldService.Instance == null || NavMeshWorldService.Instance.IsReady))
            { admitted = true; admittedWorld = authority.WorldGeneration; spawnAdmissionPending = false; }
            if (!admitted || !IsAlive()) return;
            if (!authority.IsRegistered(NetworkObject))
            {
                registeredSkills.Clear();
                if (!authority.RegisterActor(NetworkObject, IsAlive, IsPlayer ? this : null, IsPlayer ? IsAssigned : null)) return;
                everRegistered = true;
            }
            if (IsPlayer && !authority.IsRegistered(NetworkObject, this) && !authority.TryAttachExecutor(NetworkObject, this, IsAssigned))
            { Diagnose("Actor already has a different trusted executor; registration was not replaced."); return; }
            if (IsPlayer && authority.IsRegistered(NetworkObject, this)) everRegistered = true;
            BindWorldAndAssignments();
            if (!IsPlayer || !catalogValid) return;
            foreach (var entry in catalog.Entries)
            {
                if (entry == null || !Owns(entry.skill)) continue;
                var family = CombatNetworkSkillCatalog.Classify(entry.skill, out string reason);
                if (family == CombatNetworkSkillFamily.Unsupported) { Diagnose(entry.skill.name + ": " + reason); continue; }
                if (registeredSkills.Contains(entry.Id)) continue;
                if (TryPolicy(entry, family, out var policy) && authority.RegisterSkill(NetworkObject, entry.Id, entry.skill, policy)) registeredSkills.Add(entry.Id);
            }
            var removed = new List<int>();
            foreach (int id in registeredSkills)
                if (!catalog.TryGet(id, out var entry) || !Owns(entry.skill)) removed.Add(id);
            foreach (int id in removed) { authority.UnregisterSkill(NetworkObject, id); registeredSkills.Remove(id); }
        }

        private void BindWorldAndAssignments()
        {
            var service = WorldInteractionService.Instance;
            if (assignmentService != service)
            {
                if (assignmentService != null) assignmentService.AssignmentsChanged -= OnAssignmentsChanged;
                assignmentService = service;
                if (assignmentService != null) assignmentService.AssignmentsChanged += OnAssignmentsChanged;
            }
            var world = FindAnyObjectByType<CombatWorldRuntime>();
            if (combatWorld == world) return;
            if (combatWorld != null) combatWorld.ThreatsChanged -= OnWorldThreatsChanged;
            combatWorld = world;
            if (combatWorld != null) combatWorld.ThreatsChanged += OnWorldThreatsChanged;
        }

        private void OnAssignmentsChanged()
        {
            if (hasAction && !IsAssigned(OwnerClientId)) authority?.Cancel(active.Ticket);
            nextRefresh = 0;
        }

        private bool IsAssigned(ulong clientId)
        {
            var service = WorldInteractionService.Instance;
            if (!IsPlayer || service == null || !service.IsSpawned || service.NetworkManager != NetworkManager ||
                !service.TryGetAssignedCharacterId(clientId, out string assigned)) return false;
            var identity = GetComponent<NetcodeCharacterIdentity>();
            string id = identity != null ? identity.CharacterId : NetcodeCharacterIdentity.GetCharacterId(actor.CharacterData);
            return !string.IsNullOrEmpty(id) && string.Equals(id, assigned, StringComparison.Ordinal);
        }

        private bool Owns(SkillSO skill)
        {
            if (!IsPlayer || skill == null) return false;
            var data = actor.CharacterData;
            if (skill is ChargedSkillSO) return data.chargedSkill == skill;
            if (skill is BasicSkillsSO basic) return basic.Context == BasicSkillContext.Airborne
                ? data.airBasicSkills != null && data.airBasicSkills.Contains(basic)
                : data.groundBasicSkills != null && data.groundBasicSkills.Contains(basic);
            return data.combatSkills != null && data.combatSkills.Contains(skill);
        }

        private bool TryPolicy(CombatNetworkSkillCatalog.Entry entry, CombatNetworkSkillFamily family, out CombatCommandPolicy policy)
        {
            policy = null;
            var skill = entry.skill;
            if (family == CombatNetworkSkillFamily.Cinematic) return TryNetworkCinematicPolicy(entry, out policy);
            CombatCommandTargetMode target = CombatCommandTargetMode.None;
            float minimum = 0, range = 0, impactRange = 0;
            float duration = 3;
            if (CombatNetworkSkillCatalog.IsNativeAttack(family))
            {
                target = skill.targetingMode == SkillTargetingMode.FreeAttack || skill.targetingMode == SkillTargetingMode.CasterArea ? CombatCommandTargetMode.None :
                    skill.targetingMode == SkillTargetingMode.RequiredTarget || skill.targetingMode == SkillTargetingMode.LockedTarget ? CombatCommandTargetMode.Required : CombatCommandTargetMode.Optional;
                minimum = skill.useLaunchRange ? skill.minimumLaunchDistance : skill.minimumHitDistance;
                range = skill.useLaunchRange ? skill.maximumLaunchDistance : skill.maximumHitDistance;
                impactRange = skill.maximumHitDistance;
                duration = skill.AnimationClip.length * 3 + 2;
                if (family == CombatNetworkSkillFamily.GroundImpact)
                { impactRange = skill.groundImpact.radius; duration += skill.groundImpact.maximumDescentSeconds; }
            }
            else if (family == CombatNetworkSkillFamily.HealSelf) target = CombatCommandTargetMode.Self;
            else if (family == CombatNetworkSkillFamily.PasDeRecul) duration = skill.control.durationSeconds + 2;
            else if (family == CombatNetworkSkillFamily.Sommation) impactRange = skill.protection.radius;
            else if (family == CombatNetworkSkillFamily.HealArea) impactRange = skill.healingRadius;
            else if (family == CombatNetworkSkillFamily.ProtectionLink)
            { target = CombatCommandTargetMode.RequiredOrSelf; range = impactRange = skill.protection.radius; }
            else if (family == CombatNetworkSkillFamily.Revelation || family == CombatNetworkSkillFamily.Entrave)
            { target = CombatCommandTargetMode.Required; range = impactRange = skill.control.range; }
            else if (family == CombatNetworkSkillFamily.Regeneration || family == CombatNetworkSkillFamily.LifeSteal)
            { target = CombatCommandTargetMode.Required; range = impactRange = skill.healingRadius; }
            try
            {
                policy = new CombatCommandPolicy(target, minimum, range, entry.heightTolerance,
                    skill.cooldownSeconds, duration, family == CombatNetworkSkillFamily.Cinematic, 1, impactRange);
                return true;
            }
            catch (ArgumentOutOfRangeException) { return Diagnose(skill.name + ": invalid server skill policy."); }
        }

        private void ReleaseParticipantFlows()
        {
            CleanupClashReplication();
            StopNetworkCinematic();
            ClearNetworkReaction();
            CleanupNetworkThresholdTransport();
            EndRemoteDodgePresentation();
            EndRemoteGuardPresentation();
        }

        private void Retire(CombatActionEndReason reason)
        {
            ReleaseParticipantFlows();
            StopOwnedTransientMotion();
            StopCharge();
            serverManualLock = sentManualLock = null;
            if (IsServer && authority != null && everRegistered) authority.UnregisterActor(NetworkObject, reason);
            admitted = false; spawnAdmissionPending = false; registeredSkills.Clear();
            ClearOwnedEffects(); StopLocalPresentation();
            if (assignmentService != null) assignmentService.AssignmentsChanged -= OnAssignmentsChanged;
            if (combatWorld != null) combatWorld.ThreatsChanged -= OnWorldThreatsChanged;
            assignmentService = null; combatWorld = null;
            UnbindAuthorityEvents();
        }

        private bool Diagnose(string reason)
        {
            LastDiagnostic = reason;
            if (diagnostics.Add(reason)) Debug.LogWarning("[CombatNetworkActor] " + name + ": " + reason, this);
            return false;
        }

        private static ICombatTarget ResolveEnemy(NetworkObject identity)
        {
            if (identity == null) return null;
            var encounter = EnemyEncounterActors.Resolve(identity);
            return encounter != null && encounter.EncounterRoot != null && encounter.EncounterRoot.GetComponentInParent<NetworkObject>() == identity
                ? encounter.EncounterTarget : null;
        }
    }
}
