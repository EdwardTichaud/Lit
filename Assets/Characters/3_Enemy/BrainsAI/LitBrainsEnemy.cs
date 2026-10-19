using UnityEngine;
using UnityEngine.AI;
using Ultrabolt.BrainsAI;

/// <summary>Brains AI enemy adapter: Lit perception and combat presentation, with decisions owned by Brain.</summary>
[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
[RequireComponent(typeof(AnimationEvents))]
[RequireComponent(typeof(LitBrainsAnimationEvents))]
public partial class LitBrainsEnemy : Brain, ICombatTarget, ICombatImpactReceiver, ICombatKnockoutReceiver, ICombatStunReceiver
{
    public CharacterData characterData;
    [SerializeField] public Transform lockPoint;
    public LitBrainsCombatProfile combatProfile;
    public LocomotionPresentationProfile presentationProfile;
    [Tooltip("Reconcile baked navigation height with the physical ground through NavMeshAgent.baseOffset. Opt-in per actor.")]
    public bool reconcileNavigationGroundHeight;
    [Tooltip("Optional test target. Empty selects a living registered player actor.")]
    public Transform detectionTargetOverride;
    [Min(0.02f)] public float detectionInterval = 0.1f;
    [Min(0.1f)] public float navMeshAttachmentTolerance = 0.5f;
    [Min(0.1f)] public float deathDisplaySeconds = 4f;
    [SerializeField] private int currentHealth;
    [SerializeField] private string detectionReason;
    [SerializeField] private bool navigationReady;
    private bool initialized;
    private float nextDetection;
    private float reactionUntil;
    private float deathUntil;
    private bool dead;
    private float locomotionBlend;
    private CombatTimeDomain presentationClock;
    private bool cinematicSuspended;
    private bool cinematicReattachPending;
    private int observedAttackState;
    private int impactActionSequence = -1;
    public int ActionSequenceId { get; private set; }
    public event System.Action<AnimationEvent> AttackImpact;
    public event System.Action<AnimationEvent> ReactionOpportunity;
    public Transform Root => transform;
    public Transform LockPoint => lockPoint != null ? lockPoint : transform;
    public Animator Animator => anim != null ? anim : GetComponent<Animator>();
    public bool IsAttackCommitted => (combatProfile == null || AttackPhase != LitBrainsAttackPhase.None) &&
        Animator != null && (Animator.GetCurrentAnimatorStateInfo(0).IsTag("Attack") ||
        Animator.IsInTransition(0) && Animator.GetNextAnimatorStateInfo(0).IsTag("Attack"));
    public int CurrentHealth => Health != null ? Health.CurrentHp : currentHealth;
    public bool IsDead => Health != null ? Health.IsDead : dead;
    public string DetectionReason => detectionReason;
    public bool NavigationReady => navigationReady;
    public bool IsCinematicSuspended => cinematicSuspended;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        authoredAgentBaseOffset = agent.baseOffset;
        anim = GetComponent<Animator>();
        // Additive scenes can arrive before their NavMesh is ready.
        agent.enabled = false;
        anim.applyRootMotion = false;
        fov.useExternalDetection = true;
        fov.enabled = false;
        ConfigureCharacterData(characterData);
    }

    protected override void Start() { } // Initialize the package only after attachment.

    protected override void Update()
    {
        if (Health == null || Health.SourceData != characterData) ConfigureCharacterData(characterData);
        BindNavigationWorld();
        if (!HasSimulationAuthority) { SuspendNavigation(); return; }
        // Flame loss freezes the committed action; it must not take the cancellation gate below.
        if (IsFlameDormant) return;
        TryFinishThresholdRetaliation();
        if (cinematicSuspended) return;
        if (initialized && Time.deltaTime <= 0) return;
        if (dead)
        {
            if (GetComponent<EnemyDeathPresentation>() != null) { EnsureDeathPose(); return; }
            var state = anim.GetCurrentAnimatorStateInfo(0);
            bool deathFinished = !HasCombatState(CombatDeathState) ||
                state.IsName(CombatDeathState) && !anim.IsInTransition(0) && state.normalizedTime >= 1f;
            if (Time.time >= deathUntil && deathFinished) gameObject.SetActive(false);
            return;
        }
        if (!EncounterAllowsDecisions)
        {
            CancelFluidAttack();
            StopFluidMovement();
            ResetPerception();
            SuspendNavigation();
            return;
        }
        if (chargedAirborne) { PlayChargedFloatHit(); return; }
        if (UsesProductionPatterns && ProductionMotionActive) { TickFluidCombat(); return; }
        if (!TryPrepareNavigation()) return;
        if (ThresholdStageSuspendsActions) { StopFluidMovement(); UpdateAnimator(); return; }
        UpdateNavigationGrounding();
        if (TickClashRecovery()) return;
        if (!initialized)
        {
            EnsureProductionHome();
            base.Start();
            initialized = true;
            ResetFluidCombat();
        }
        if (TickProductionEncounter()) return;
        if (Time.time >= nextDetection)
        {
            nextDetection = Time.time + detectionInterval;
            Transform target = ResolvePerceptionTarget();
            var taunt = GetComponent<EnemyTaunt>();
            if (!IsAttackCommitted && taunt != null && taunt.IsActive)
            {
                target = taunt.Target.transform;
                // Provocation establishes a known target; preparation still requires a clear attack path.
                fov.SetExternalTarget(target);
            }
            else if (IsAttackCommitted && CommittedAttackTarget != null) target = CommittedAttackTarget;
            UpdatePerception(target);
        }
        if (combatProfile != null)
        {
            TickFluidCombat();
            if (IsCounterStunned) { UpdateAnimator(); return; }
            base.Update();
            ApplyNavigationControl();
            return;
        }
        if (counterStunActive)
        {
            hurtRemaining = Mathf.Max(0, hurtRemaining - Time.deltaTime);
            if (hurtRemaining > 0) { StopFluidMovement(); UpdateAnimator(); return; }
            counterStunActive = false;
            ClearCounterStunVfx();
            anim.CrossFadeInFixedTime(CombatLocomotionState, .06f, 0);
        }
        bool reacting = Time.time < reactionUntil;
        StopControlConditions["LitBrainsEnemy.Hurt"] = reacting;
        bool actionAnimation = !anim.GetCurrentAnimatorStateInfo(0).IsTag("Moveable") ||
            anim.IsInTransition(0) && !anim.GetNextAnimatorStateInfo(0).IsTag("Moveable");
        if (reacting || actionAnimation)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            anim.SetFloat("MoveX", 0f);
            anim.SetFloat("MoveY", 0f);
            UpdateAnimator();
            // The package normally rotates/chases while playing an attack; leave the pose intact.
            return;
        }
        agent.isStopped = false;
        base.Update();
        ApplyNavigationControl();
    }

    protected override void UpdateAnimator()
    {
        if (UsesProductionPatterns) { if (!dead && !cinematicSuspended && anim != null) UpdateProductionAnimator(); return; }
        if (dead || cinematicSuspended || !navigationReady || anim == null || !agent.enabled || !agent.isOnNavMesh) return;
        ObserveAttackState();
        bool moving = anim.GetCurrentAnimatorStateInfo(0).IsTag("Moveable") &&
            (!anim.IsInTransition(0) || anim.GetNextAnimatorStateInfo(0).IsTag("Moveable"));
        Vector3 velocity = Vector3.ProjectOnPlane(agent.velocity, Vector3.up);
        if (!moving) velocity = Vector3.zero;
        float speed = velocity.magnitude;
        if (presentationProfile != null && presentationProfile.IsUsable(anim.avatar))
        {
            Vector3 localVelocity = transform.InverseTransformDirection(velocity);
            var direction = new Vector2(localVelocity.x, localVelocity.z).normalized;
            // The navigation request selects the gait; only obtained velocity drives activity/cadence.
            if (anim.speed <= 0) return;
            presentationProfile.Evaluate(speed / anim.speed, requestedNavigationSpeed > walkSpeed + .05f, direction, out float measuredBlend, out float cadence);
            anim.SetFloat("Speed", measuredBlend);
            anim.SetFloat(LocomotionPresentationProfile.PlaybackParameter, cadence);
            if (presentationClock == null) presentationClock = GetComponent<CombatTimeDomain>();
            float presentationDelta = presentationClock != null ? presentationClock.DeltaTime : Time.deltaTime;
            anim.SetFloat("MoveX", direction.x, .08f, presentationDelta);
            anim.SetFloat("MoveY", direction.y, .08f, presentationDelta);
            anim.SetBool("Grounded", IsGrounded);
            return;
        }
        float blend = speed < 0.05f ? 0f : speed <= walkSpeed
            ? Mathf.Lerp(0f, 0.5f, speed / Mathf.Max(0.01f, walkSpeed))
            : Mathf.Lerp(0.5f, 1f, Mathf.InverseLerp(walkSpeed, Mathf.Max(walkSpeed + 0.01f, runSpeed), speed));
        locomotionBlend = Mathf.MoveTowards(locomotionBlend, blend, Time.deltaTime / 0.08f);
        anim.SetFloat("Speed", locomotionBlend);
        Vector3 local = transform.InverseTransformDirection(velocity / Mathf.Max(0.05f, speed));
        anim.SetFloat("MoveX", speed < 0.05f ? 0 : local.x, 0.08f, Time.deltaTime);
        anim.SetFloat("MoveY", speed < 0.05f ? 0 : local.z, 0.08f, Time.deltaTime);
        anim.SetBool("Grounded", IsGrounded);
    }

    private void ObserveAttackState()
    {
        if (combatProfile != null) return;
        int hash = 0;
        if (anim != null)
        {
            var current = anim.GetCurrentAnimatorStateInfo(0);
            var next = anim.IsInTransition(0) ? anim.GetNextAnimatorStateInfo(0) : default;
            if (next.IsTag("Attack")) hash = next.fullPathHash;
            else if (current.IsTag("Attack")) hash = current.fullPathHash;
        }
        if (hash != 0 && hash != observedAttackState) ActionSequenceId++;
        observedAttackState = hash;
    }

    public void HandleOpenBrainsReactionOpportunity(AnimationEvent source)
    {
        if (!HasSimulationAuthority || !EncounterAllowsDecisions || dead || IsCounterStunned || cinematicSuspended || source == null || !source.animatorStateInfo.IsTag("Attack")) return;
        if (combatProfile != null && !AcceptFluidEvent(source)) return;
        ObserveAttackState();
        ReactionOpportunity?.Invoke(source);
    }

    public int ReceiveDamage(int damage)
    {
        int before = currentHealth;
        TakeDamage(damage);
        return before - currentHealth;
    }

    public void NotifyPlayerContact(Transform source)
    {
        if (!HasSimulationAuthority || !EncounterAllowsDecisions || dead || cinematicSuspended || source == null || fov == null) return;
        if (UsesProductionPatterns && !IsWithinPursuitBounds(source)) return;
        productionEngaged = true;
        if (!IsAttackCommitted || fov.CurrentTarget == null) fov.SetExternalTarget(source);
    }

    public void HandleResolveBrainsAttackImpact(AnimationEvent source)
    {
        if (!HasSimulationAuthority || !EncounterAllowsDecisions || dead || IsCounterStunned || cinematicSuspended || source == null || !source.animatorStateInfo.IsTag("Attack")) return;
        if (combatProfile != null && !AcceptFluidEvent(source)) return;
        if (!TryConfirmAttackImpact()) return;
        AttackImpact?.Invoke(source);
    }

    private bool IsLivingTarget(Transform target)
    {
        if (target == null || !target.gameObject.activeInHierarchy) return false;
        var player = target.GetComponentInParent<SquadCharacterController>();
        if (player != null) return player.CurrentHp > 0;
        var health = target.GetComponentInParent<CharacterInfo>();
        return health == null || !health.IsDead;
    }

    private void UpdatePerception(Transform target)
    {
        bool retained = target != null && fov.CurrentTarget == target;
        bool detected = false;
        if (characterData == null || characterData.vision == null) detectionReason = "perdue : fiche absente";
        else if (!IsLivingTarget(target)) detectionReason = "perdue : joueur absent, inactif ou mort";
        else if (retained)
        {
            var vision = characterData.vision;
            float distance = Vector3.Distance(transform.position + Vector3.up * vision.eyeHeight,
                target.position + Vector3.up * vision.targetHeight);
            detected = UsesProductionPatterns ? IsWithinPursuitBounds(target) : distance > Mathf.Epsilon && distance <= vision.maximumDistance;
            detectionReason = detected ? "poursuite : cible connue" : "perdue : hors portee";
        }
        else
        {
            detected = characterData.TryEvaluateVision(transform, target, out _, out _, out string reason);
            detectionReason = detected ? "reperee : " + reason : "non reperee : " + reason;
        }
        fov.SetExternalTarget(detected ? target : null);
        if (detected) productionEngaged = true;
        var manager = RealTimeCombatManager.Instance;
        if (detected && manager != null && manager.AllowsFreeAttacks && target == manager.PlayerRoot)
            manager.TryEngageFromEnemyPerception(target, this);
        else if (detected && HasSimulationAuthority)
        {
            var actor = target.GetComponentInParent<SquadCharacterController>();
            var world = manager != null ? manager.GetComponent<CombatWorldRuntime>() : FindAnyObjectByType<CombatWorldRuntime>();
            if (actor != null && world != null) world.TryEngageActor(actor, this);
        }
    }

    private bool HasAttackLineOfSight()
    {
        var target = fov != null ? fov.CurrentTarget : null;
        return characterData != null && characterData.vision != null && IsLivingTarget(target) &&
            characterData.vision.TryEvaluate(transform, target, characterData.vision.maximumDistance,
                360f, out _, out _, out _);
    }

    private bool TryConfirmAttackImpact()
    {
        // Consume the authored contact even when blocked: it cannot hit later after the wall disappears.
        if (combatProfile != null)
        {
            if (!TryCommitFluidImpact()) return false;
        }
        else
        {
            ObserveAttackState();
            if (impactActionSequence == ActionSequenceId) return false;
            impactActionSequence = ActionSequenceId;
        }
        return HasAttackLineOfSight();
    }

    private void ResetPerception()
    {
        nextDetection = 0;
        impactActionSequence = -1;
        detectionReason = "non reperee : reinitialisation";
        if (fov == null) return;
        bool investigate = fov.canInvestigate;
        fov.canInvestigate = false;
        // Package status callbacks reset the path; they are invalid while the
        // agent is suspended in flight or being destroyed during scene teardown.
        if (!initialized || agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            fov.SetExternalTarget(null);
            fov.ResolveInvestigation(float.PositiveInfinity);
            perceptionResetPending = false;
        }
        else perceptionResetPending = true;
        fov.ResetLastKnownPosition();
        fov.canInvestigate = investigate;
    }

    public void SetCinematicSuspended(bool suspended)
    {
        if (cinematicSuspended == suspended) return;
        cinematicSuspended = suspended;
        if (suspended)
        {
            if (productionLandingRecovery) { productionAirborne = true; ResetProductionLandingRecovery(); }
            ReleaseChargedHold();
            CancelClashRecovery();
            CancelFluidAttack();
            if (agent.enabled && agent.isOnNavMesh) { agent.ResetPath(); agent.isStopped = true; }
            agent.enabled = false;
        }
        else if (dead)
        {
            // The Timeline owned the pose when the lethal impact arrived. Start the
            // death clip after graph release, with its own full display duration.
            BeginDeathPresentation();
        }
        else
        {
            anim.ResetTrigger("Attack"); anim.ResetTrigger("Hurt");
            observedAttackState = 0;
            if (chargedAirborne) return;
            cinematicReattachPending = true;
            if (!TryPrepareNavigation()) SuspendNavigation();
            if (counterStunPending) BeginCounterStun();
            else if (counterStunActive) PlayCombatKnockout();
            else anim.CrossFade(CombatLocomotionState, .1f, 0);
        }
    }

    public bool PlaceForCinematic(Vector3 position, Quaternion rotation)
    {
        var world = NavMeshWorldService.Instance;
        if (world != null && !world.IsReady) { SuspendNavigation(); return false; }
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        float tolerance = Mathf.Max(.01f, Mathf.Min(.15f, navMeshAttachmentTolerance));
        if (!NavMesh.SamplePosition(position, out var hit, tolerance, filter) ||
            world != null && !world.TryValidatePosition(position, agent.areaMask, out _) ||
            Mathf.Abs(hit.position.y - position.y) > tolerance ||
            Vector3.ProjectOnPlane(hit.position - position, Vector3.up).magnitude > tolerance)
        { SuspendNavigation(); return false; }
        SuspendNavigation();
        Vector3 pose = hit.position;
        if (reconcileNavigationGroundHeight || presentationProfile != null && presentationProfile.IsUsable(anim.avatar) && presentationProfile.alignNavMeshGround)
            pose.y += agent.baseOffset;
        transform.SetPositionAndRotation(pose, rotation);
        return true;
    }

    private bool TryPrepareNavigation()
    {
        BindNavigationWorld();
        if (!HasSimulationAuthority || dead || cinematicSuspended || chargedAirborne || ProductionMotionActive || !EncounterAllowsDecisions) return false;
        if (cinematicReattachPending && !HasChargedLandingGround()) { SuspendNavigation(); return false; }
        var world = NavMeshWorldService.Instance;
        if (world != null && !world.IsReady)
        {
            if (agent.enabled) agent.enabled = false;
            fov.enabled = false;
            navigationReady = false;
            return false;
        }
        if (agent.enabled && agent.isOnNavMesh)
        {
            if (perceptionResetPending) ResetPerception();
            navigationReady = true;
            cinematicReattachPending = false;
            fov.enabled = true;
            return true;
        }
        if (world != null && !chargedLandingAttachment)
        {
            navigationReady = world.TryRegisterAgent(agent, transform.position);
            if (navigationReady) cinematicReattachPending = false;
            if (navigationReady && perceptionResetPending) ResetPerception();
            fov.enabled = navigationReady;
            return navigationReady;
        }
        agent.enabled = false;
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        float attachmentTolerance = chargedLandingAttachment ? .15f : navMeshAttachmentTolerance;
        Vector3 attachmentPosition = chargedLandingAttachment ? transform.position - Vector3.up * agent.baseOffset : transform.position;
        if (chargedLandingAttachment && !HasChargedLandingGround())
        { navigationReady = false; fov.enabled = false; ReportChargedLandingFailure(attachmentPosition); return false; }
        if (!NavMesh.SamplePosition(attachmentPosition, out var hit, attachmentTolerance, filter) ||
            chargedLandingAttachment && (Mathf.Abs(hit.position.y - attachmentPosition.y) > .15f ||
                Vector3.ProjectOnPlane(hit.position - attachmentPosition, Vector3.up).magnitude > .15f))
        {
            if (chargedLandingAttachment) ReportChargedLandingFailure(attachmentPosition);
            navigationReady = false;
            fov.enabled = false;
            return false;
        }
        if (!chargedLandingAttachment) transform.position = hit.position;
        if (world != null)
        {
            Vector3 pose = transform.position;
            navigationReady = world.TryRegisterAgent(agent, attachmentPosition);
            if (navigationReady) transform.position = pose;
        }
        else agent.enabled = true;
        navigationReady = agent.isOnNavMesh;
        if (navigationReady) cinematicReattachPending = false;
        if (navigationReady && perceptionResetPending) ResetPerception();
        if (navigationReady) chargedLandingAttachment = false;
        fov.enabled = navigationReady;
        return navigationReady;
    }

    /// <summary>Test-only health entry point, deliberately independent from Lit's combat.</summary>
    public void TakeDamage(int amount)
    {
        ReceiveImpact(new CombatImpact(amount, 20f, CombatImpactOrigin.Basic));
    }

    private void BeginDeathPresentation()
    {
        anim.SetBool("Dead", true);
        if (cinematicSuspended || GetComponent<EnemyDeathPresentation>() != null && IsDeathPosePending) return;
        if (GetComponent<EnemyDeathPresentation>() != null)
        {
            productionMotionActive = false;
            productionMotionSkill = null;
        }
        productionDeathPoseStarted = true;
        if (HasCombatState(CombatDeathState))
            anim.CrossFadeInFixedTime(CombatDeathState, .08f, 0, 0f);
        deathUntil = Time.time + deathDisplaySeconds;
    }

    [ContextMenu("Test/Hurt (10 HP)")] public void TestHurt() { if (Application.isPlaying) TakeDamage(10); }
    [ContextMenu("Test/Death")] public void TestDeath() { if (Application.isPlaying) TakeDamage(currentHealth); }
}
