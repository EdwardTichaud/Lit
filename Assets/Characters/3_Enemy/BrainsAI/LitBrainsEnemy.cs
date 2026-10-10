using UnityEngine;
using UnityEngine.AI;
using Ultrabolt.BrainsAI;

/// <summary>Brains AI enemy adapter: Lit perception and combat presentation, with decisions owned by Brain.</summary>
[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
[RequireComponent(typeof(AnimationEvents))]
public partial class LitBrainsEnemy : Brain, ICombatTarget, ICombatImpactReceiver, ICombatKnockoutReceiver
{
    public CharacterData characterData;
    public LitBrainsCombatProfile combatProfile;
    public LocomotionPresentationProfile presentationProfile;
    [Tooltip("Reconcile baked navigation height with the physical ground through NavMeshAgent.baseOffset. Opt-in per actor.")]
    public bool reconcileNavigationGroundHeight;
    [Tooltip("Optional test target. Empty uses Lit's controlled player.")]
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
    private Vector3 cinematicAgentPosition;
    private int observedAttackState;
    private int impactActionSequence = -1;
    public int ActionSequenceId { get; private set; }
    public event System.Action<AnimationEvent> AttackImpact;
    public event System.Action<AnimationEvent> ReactionOpportunity;
    public Transform Root => transform;
    public Transform LockPoint => transform;
    public Animator Animator => anim != null ? anim : GetComponent<Animator>();
    public bool IsAttackCommitted => (combatProfile == null || AttackPhase != LitBrainsAttackPhase.None) &&
        Animator != null && (Animator.GetCurrentAnimatorStateInfo(0).IsTag("Attack") ||
        Animator.IsInTransition(0) && Animator.GetNextAnimatorStateInfo(0).IsTag("Attack"));
    public int CurrentHealth => currentHealth;
    public bool IsDead => dead;
    public string DetectionReason => detectionReason;
    public bool NavigationReady => navigationReady;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        authoredAgentBaseOffset = agent.baseOffset;
        anim = GetComponent<Animator>();
        // Additive scenes can arrive before their NavMesh is ready.
        agent.enabled = false;
        anim.applyRootMotion = false;
        currentHealth = characterData != null ? Mathf.Max(1, characterData.hp) : 100;
        fov.useExternalDetection = true;
        fov.enabled = false;
    }

    protected override void Start() { } // Initialize the package only after attachment.

    protected override void Update()
    {
        if (cinematicSuspended) return;
        if (initialized && Time.deltaTime <= 0) return;
        if (dead)
        {
            var state = anim.GetCurrentAnimatorStateInfo(0);
            bool deathFinished = !anim.HasState(0, Animator.StringToHash("Base Layer.Death")) ||
                state.IsName("Death") && !anim.IsInTransition(0) && state.normalizedTime >= 1f;
            if (Time.time >= deathUntil && deathFinished) gameObject.SetActive(false);
            return;
        }
        if (!TryPrepareNavigation()) return;
        UpdateNavigationGrounding();
        if (!initialized)
        {
            base.Start();
            initialized = true;
            ResetFluidCombat();
        }
        if (Time.time >= nextDetection)
        {
            nextDetection = Time.time + detectionInterval;
            Transform target = detectionTargetOverride != null ? detectionTargetOverride : LocalPlayerContext.LocalCharacterRoot;
            UpdatePerception(target);
        }
        if (combatProfile != null)
        {
            TickFluidCombat();
            if (IsCounterStunned) { UpdateAnimator(); return; }
            base.Update();
            return;
        }
        if (counterStunActive)
        {
            hurtRemaining = Mathf.Max(0, hurtRemaining - Time.deltaTime);
            if (hurtRemaining > 0) { StopFluidMovement(); UpdateAnimator(); return; }
            counterStunActive = false;
            ClearCounterStunVfx();
            anim.CrossFadeInFixedTime("Locomotion", .06f, 0);
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
    }

    protected override void UpdateAnimator()
    {
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
            presentationProfile.Evaluate(speed / anim.speed, agent.speed > walkSpeed + .05f, direction, out float measuredBlend, out float cadence);
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
        if (dead || IsCounterStunned || cinematicSuspended || source == null || !source.animatorStateInfo.IsTag("Attack")) return;
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

    public void HandleResolveBrainsAttackImpact(AnimationEvent source)
    {
        if (dead || IsCounterStunned || cinematicSuspended || source == null || !source.animatorStateInfo.IsTag("Attack")) return;
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
            detected = distance > Mathf.Epsilon && distance <= vision.maximumDistance;
            detectionReason = detected ? "poursuite : cible connue" : "perdue : hors portee";
        }
        else
        {
            detected = characterData.TryEvaluateVision(transform, target, out _, out _, out string reason);
            detectionReason = detected ? "reperee : " + reason : "non reperee : " + reason;
        }
        fov.SetExternalTarget(detected ? target : null);
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
        fov.SetExternalTarget(null);
        fov.ResolveInvestigation(float.PositiveInfinity);
        fov.ResetLastKnownPosition();
        fov.canInvestigate = investigate;
    }

    public void SetCinematicSuspended(bool suspended)
    {
        if (cinematicSuspended == suspended) return;
        cinematicSuspended = suspended;
        if (suspended)
        {
            CancelFluidAttack();
            cinematicAgentPosition = transform.position;
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
            if (!PlaceForCinematic(transform.position, transform.rotation))
                transform.position = cinematicAgentPosition;
            TryPrepareNavigation();
            if (counterStunPending) BeginCounterStun();
            else if (counterStunActive) anim.CrossFadeInFixedTime("Knocked Out", .04f, 0);
            else anim.CrossFade("Locomotion", .1f, 0);
        }
    }

    public bool PlaceForCinematic(Vector3 position, Quaternion rotation)
    {
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        if (!NavMesh.SamplePosition(position, out var hit, 2f, filter)) return false;
        if (agent.enabled && agent.isOnNavMesh && !agent.Warp(hit.position)) return false;
        Vector3 pose = hit.position;
        if (reconcileNavigationGroundHeight || presentationProfile != null && presentationProfile.IsUsable(anim.avatar) && presentationProfile.alignNavMeshGround)
            pose.y += agent.baseOffset;
        transform.SetPositionAndRotation(pose, rotation);
        return true;
    }

    private bool TryPrepareNavigation()
    {
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
            navigationReady = true;
            fov.enabled = true;
            return true;
        }
        agent.enabled = false;
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        if (!NavMesh.SamplePosition(transform.position, out var hit, navMeshAttachmentTolerance, filter))
        {
            navigationReady = false;
            fov.enabled = false;
            return false;
        }
        transform.position = hit.position;
        agent.enabled = true;
        navigationReady = agent.isOnNavMesh;
        fov.enabled = navigationReady;
        return navigationReady;
    }

    /// <summary>Test-only health entry point, deliberately independent from Lit's combat.</summary>
    public void TakeDamage(int amount)
    {
        if (combatProfile != null)
        {
            ReceiveImpact(new CombatImpact(amount, 20f, CombatImpactOrigin.Basic));
            return;
        }
        if (dead || amount <= 0) return;
        currentHealth = Mathf.Max(0, currentHealth - amount);
        anim.ResetTrigger("Attack");
        if (agent.enabled && agent.isOnNavMesh) { agent.ResetPath(); agent.isStopped = true; agent.velocity = Vector3.zero; }
        if (currentHealth == 0)
        {
            counterStunPending = counterStunActive = false;
            ClearCounterStunVfx();
            dead = true;
            StopAllCoroutines();
            fov.canInvestigate = false;
            fov.SetExternalTarget(null);
            fov.enabled = false;
            anim.ResetTrigger("Hurt");
            BeginDeathPresentation();
            agent.enabled = false;
            foreach (var collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
        }
        else if (!IsCounterStunned)
        {
            reactionUntil = Time.time + 0.8f;
            anim.SetTrigger("Hurt");
        }
    }

    private void BeginDeathPresentation()
    {
        anim.SetBool("Dead", true);
        if (cinematicSuspended) return;
        if (anim.HasState(0, Animator.StringToHash("Base Layer.Death")))
            anim.CrossFadeInFixedTime("Base Layer.Death", .08f, 0, 0f);
        deathUntil = Time.time + deathDisplaySeconds;
    }

    [ContextMenu("Test/Hurt (10 HP)")] public void TestHurt() { if (Application.isPlaying) TakeDamage(10); }
    [ContextMenu("Test/Death")] public void TestDeath() { if (Application.isPlaying) TakeDamage(currentHealth); }
}
