using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Keeps NavMesh as the sole owner of ordinary enemy movement while translating
/// its world velocity into a combat-facing Animator presentation. Root strafe
/// clips remain visual here: only committed actions may transfer root motion.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RealTimeCombatEnemy))]
public sealed class CombatEnemyLocomotionController : MonoBehaviour
{
    [System.Serializable]
    public sealed class CombatPositioningProfile
    {
        [Min(0.1f)] public float preferredDistance = 2.6f;
        [Min(0f)] public float minimumRetreatDistance = 1.2f;
        [Min(0f)] public float strafeRadius = 1.1f;
        [Min(0.1f)] public float strafeSideHoldSeconds = 1.35f;
        [Min(0.1f)] public float walkSpeed = 1.8f;
        [Min(0.1f)] public float runSpeed = 3.6f;
        [Min(0.1f)] public float runDistance = 8f;
        [Min(0f)] public float walkResumeDistance = 6f;
        [Min(0.02f)] public float pursuitRefreshSeconds = 0.15f;
        [Min(0.01f)] public float pursuitTargetMoveDistance = 0.5f;
        [Min(0f)] public float walkSpeedThreshold = 1.2f;
        [Min(0f)] public float runSpeedThreshold = 3.4f;
    }

    private static readonly int CombatMoveX = Animator.StringToHash("CombatMoveX");
    private static readonly int CombatMoveZ = Animator.StringToHash("CombatMoveZ");
    private static readonly int CombatMoveSpeed = Animator.StringToHash("CombatMoveSpeed");
    private static readonly int CommonX = Animator.StringToHash("HorizontalMovement");
    private static readonly int CommonZ = Animator.StringToHash("ForwardMovement");
    private static readonly int CommonMagnitude = Animator.StringToHash("CombatMoveMagnitude");
    private bool UsesCommonAnimator => GetComponent<EnemyCombatBrain>()?.HasProfile == true;
    public void SetFacingSpeed(float speed) => profileFacingSpeed = Mathf.Max(0f, speed);
    private float? profileFacingSpeed;
    public float EffectiveFacingSpeed => profileFacingSpeed ?? facingSpeedDegreesPerSecond;
    private static readonly int PlaybackRate = Animator.StringToHash("CombatLocomotionPlaybackRate");
    private bool hasPlaybackRate;
    private float nextMotionDiagnostic;
    // Animator.HasState/CrossFade with an int expects the full state path.
    // A short-name hash silently prevented the enemy combat blend tree from
    // ever taking over, leaving NavMesh movement visually in Idle.
    private static readonly int CombatLocomotion = Animator.StringToHash("Base Layer.CombatLocomotion");
    private static readonly int CombatIdle = Animator.StringToHash("Base Layer.CombatIdle");
    private static readonly int Idle = Animator.StringToHash("Base Layer.Idle");

    [SerializeField] private RealTimeCombatEnemy enemy;
    [SerializeField] private NavMeshAgent navigationAgent;
    [SerializeField] private CombatActorAnimationRoot animationContract;
    [SerializeField] private CombatEnemyPhysicsMotor physicsMotor;
    [SerializeField] private CombatTimeDomain timeDomain;
    [SerializeField] private CombatPositioningProfile positioning = new CombatPositioningProfile();
    [SerializeField, Min(0f)] private float facingSpeedDegreesPerSecond = 540f;
    [SerializeField, Min(0.01f)] private float animatorDampTime = 0.08f;
    [Header("InPlace cycle reference speeds")]
    [SerializeField, Min(.01f)] private float walkCycleSpeed = 1.8f;
    [SerializeField, Min(.01f)] private float runCycleSpeed = 3.6f;
    [Header("Obstacle Clearance")]
    [SerializeField, Min(0f), Tooltip("Marge supplementaire entre le centre de l'ennemi et le bord du NavMesh. Elle evite les poses trop proches des murs et du decor." )]
    private float navMeshEdgeClearance = 0.45f;
    [SerializeField, Min(0.1f), Tooltip("Rayon maximal de recherche d'une destination qui respecte la marge decor." )]
    private float clearanceSearchRadius = 2f;
    [SerializeField, Range(4, 24), Tooltip("Nombre de positions candidates evaluees autour d'une destination proche d'un obstacle." )]
    private int clearanceSearchSamples = 12;
    [SerializeField] private bool logDiagnostics;

    private Transform combatTarget;
    private float nextSideChangeAt;
    private float strafeSide = 1f;
    private bool navigationRequested;
    private bool wasNavigating;
    private bool hasAnimatorParameters;
    private Animator cachedAnimator;
    private int lastReportedAnimatorStateHash;
    private bool wasMovingVisually;
    private bool attackFacingLocked;
    private bool returnFacingActive;
    private Vector3 returnFacingDestination;
    private bool baseNavigationCaptured;
    private float baseNavigationSpeed;
    private float baseNavigationAcceleration;
    private float baseNavigationAngularSpeed;
    private float requestedNavigationSpeed;
    private bool runPhase;
    private NavMeshPath pursuitPath;
    private float nextPursuitUpdate;
    private Vector3 lastPursuitTarget;
    private float lastPursuitRange = -1f;
    public string PursuitFailure { get; private set; }

    private float LocalTime => timeDomain != null ? timeDomain.LocalTime : Time.time;
    private float LocalDeltaTime => timeDomain != null ? timeDomain.DeltaTime : Time.deltaTime;

    public CombatPositioningProfile Positioning => positioning;
    public bool IsNavigating => navigationRequested && navigationAgent != null && navigationAgent.isActiveAndEnabled && navigationAgent.isOnNavMesh && !navigationAgent.isStopped;

    private void Reset()
    {
        enemy = GetComponent<RealTimeCombatEnemy>();
        navigationAgent = GetComponent<NavMeshAgent>();
        animationContract = GetComponent<CombatActorAnimationRoot>();
        physicsMotor = GetComponent<CombatEnemyPhysicsMotor>();
    }

    private void Awake()
    {
        ResolveReferences();
        CaptureNavigationDefaults();
        strafeSide = Random.value < 0.5f ? -1f : 1f;
        nextSideChangeAt = LocalTime + positioning.strafeSideHoldSeconds;
    }

    private void OnDisable()
    {
        if (navigationAgent != null && navigationAgent.isActiveAndEnabled)
        {
            navigationAgent.updateRotation = true;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ResolveReferences();
        navMeshEdgeClearance = Mathf.Max(0f, navMeshEdgeClearance);
        clearanceSearchRadius = Mathf.Max(0.1f, clearanceSearchRadius);
        clearanceSearchSamples = Mathf.Clamp(clearanceSearchSamples, 4, 24);
    }
#endif

    private void Update()
    {
        ApplyLocalNavigationScale();
        UpdateAnimatorPresentation();
    }

    private void LateUpdate()
    {
        // Le root motion peut ecrire la rotation apres l'IA. On conserve donc
        // l'ennemi face a sa cible a la fin de l'image, sauf si une Timeline
        // cinematographique est explicitement proprietaire de sa pose.
        if (GetComponent<EnemyCinematicState>()?.IsSuspended != true && !attackFacingLocked && combatTarget != null && (animationContract == null || !animationContract.IsCinematicMotionActive))
        {
            FaceTarget(combatTarget.position);
        }
        else if (returnFacingActive && combatTarget == null &&
            (physicsMotor == null || !physicsMotor.IsDrivingActionRootMotion) &&
            (animationContract == null || !animationContract.IsCinematicMotionActive))
        {
            Vector3 facingPoint = returnFacingDestination;
            if (navigationAgent != null && navigationAgent.isActiveAndEnabled && navigationAgent.isOnNavMesh &&
                !navigationAgent.isStopped && navigationAgent.hasPath && !navigationAgent.pathPending)
                facingPoint = navigationAgent.steeringTarget;
            FaceTarget(facingPoint);
        }
    }

    public void SetReturnFacing(Vector3 destination)
    {
        returnFacingDestination = destination;
        returnFacingActive = true;
        if (navigationAgent != null) navigationAgent.updateRotation = false;
    }

    public void SetCombatTarget(Transform target)
    {
        returnFacingActive = false;
        if (combatTarget != target) lastPursuitRange = -1f;
        combatTarget = target;
        ResolveReferences();
        // NavMesh continues to own translation, but its automatic yaw fights
        // the face-to-face combat rule. Rotation is applied once in LateUpdate
        // by FaceTarget instead.
        if (navigationAgent != null && navigationAgent.isActiveAndEnabled)
        {
            navigationAgent.updateRotation = target == null;
        }
    }

    /// <summary>
    /// Wind-up may still track the target. Once an authored event commits an
    /// attack, this prevents LateUpdate from silently turning the hit toward a
    /// player who has already dodged away.
    /// </summary>
    public void SetAttackFacingLocked(bool value)
    {
        attackFacingLocked = value;
    }

    public bool ApproachTarget(float attackDistance)
    {
        ResolveReferences();
        string blockReason = combatTarget == null ? "cible absente" :
            navigationAgent == null ? "agent absent" :
            !navigationAgent.isActiveAndEnabled ? "agent desactive" :
            !navigationAgent.isOnNavMesh ? "agent hors NavMesh" :
            GetComponent<EnemyCinematicState>()?.IsSuspended == true ? "suspension cinematique" :
            physicsMotor != null && physicsMotor.IsDrivingActionRootMotion ? "moteur physique: " + physicsMotor.State : null;
        if (blockReason != null)
        {
            PursuitFailure = blockReason;
            StopNavigation();
            return false;
        }

        Vector3 away = transform.position - combatTarget.position;
        away.y = 0f;
        SetMovementPace(away.magnitude);
        if (LocalTime < nextPursuitUpdate && Mathf.Abs(lastPursuitRange - attackDistance) < .01f &&
            (lastPursuitTarget - combatTarget.position).sqrMagnitude <
            positioning.pursuitTargetMoveDistance * positioning.pursuitTargetMoveDistance)
            return PursuitFailure == null;

        nextPursuitUpdate = LocalTime + positioning.pursuitRefreshSeconds;
        lastPursuitTarget = combatTarget.position;
        lastPursuitRange = attackDistance;
        Vector3 radial = away.sqrMagnitude > .0001f ? away.normalized : -transform.forward;
        Vector3 destination = combatTarget.position + radial * attackDistance;
        pursuitPath ??= new NavMeshPath();
        var filter = new NavMeshQueryFilter { agentTypeID = navigationAgent.agentTypeID, areaMask = navigationAgent.areaMask };
        if (!NavMesh.SamplePosition(destination, out NavMeshHit hit, .35f, filter) ||
            !navigationAgent.CalculatePath(hit.position, pursuitPath) || pursuitPath.status != NavMeshPathStatus.PathComplete)
        {
            PursuitFailure = "destination locale ou chemin complet introuvable";
            StopNavigation();
            return false;
        }

        navigationAgent.stoppingDistance = .05f;
        navigationAgent.isStopped = false;
        bool accepted = navigationAgent.SetPath(pursuitPath);
        PursuitFailure = accepted ? null : "chemin refuse par agent";
        if (!accepted) { StopNavigation(); return false; }
        navigationRequested = wasNavigating = true;
        return true;
    }

    public void NavigateTowardsTarget(float stoppingDistance)
    {
        if (combatTarget == null)
        {
            StopNavigation();
            return;
        }

        Vector3 toSelf = transform.position - combatTarget.position;
        toSelf.y = 0f;
        float distance = toSelf.magnitude;
        Vector3 away = distance > 0.001f ? toSelf / distance : -transform.forward;
        Vector3 destination;

        if (distance > stoppingDistance + 0.18f)
        {
            SetMovementPace(distance);
            destination = combatTarget.position + away * stoppingDistance;
        }
        else if (distance < positioning.minimumRetreatDistance)
        {
            SetMovementPace(0f);
            destination = combatTarget.position + away * Mathf.Max(positioning.minimumRetreatDistance, stoppingDistance);
        }
        else
        {
            SetMovementPace(0f);
            if (LocalTime >= nextSideChangeAt)
            {
                strafeSide *= -1f;
                nextSideChangeAt = LocalTime + Mathf.Max(0.1f, positioning.strafeSideHoldSeconds);
            }

            Vector3 side = Vector3.Cross(Vector3.up, away) * strafeSide;
            destination = combatTarget.position + away * stoppingDistance + side * positioning.strafeRadius;
        }

        NavigateTo(destination, .15f);
    }

    public void NavigateTo(Vector3 destination, float stoppingDistance)
    {
        ResolveReferences();
        if (physicsMotor != null && physicsMotor.IsDrivingActionRootMotion)
        {
            StopNavigation();
            return;
        }

        if (navigationAgent == null || !navigationAgent.isActiveAndEnabled || !navigationAgent.isOnNavMesh)
        {
            // Navigation unavailable intentionally means no movement. A transform
            // fallback would compete with physics and can produce teleports.
            StopNavigation();
            return;
        }

        Vector3 requestedDestination = destination;
        int navigationAreaMask = navigationAgent.areaMask == 0 ? NavMesh.AllAreas : navigationAgent.areaMask;
        // Prefer the exact projected point. Clearance heuristics are only a
        // fallback: on a freshly built runtime mesh they can otherwise select
        // the current polygon and silently turn a valid reposition into a
        // zero-length path.
        if (!NavMesh.SamplePosition(requestedDestination, out NavMeshHit exactHit, .75f, navigationAreaMask))
        {
            destination = ResolveClearDestination(requestedDestination);
        }
        else
        {
            destination = exactHit.position;
        }
        // A clearance probe must never collapse a valid lateral reposition onto
        // the actor's current point. This can happen on a narrow or freshly
        // built runtime NavMesh when FindClosestEdge reports the current polygon
        // as the only safe candidate. Preserve the requested point when Unity
        // can project it locally; otherwise the brain would remain in Chase
        // with a completed path and zero velocity forever.
        if ((destination - transform.position).sqrMagnitude < 0.04f &&
            (requestedDestination - transform.position).sqrMagnitude > 0.25f &&
            NavMesh.SamplePosition(requestedDestination, out NavMeshHit directHit, clearanceSearchRadius,
                navigationAreaMask))
        {
            destination = directHit.position;
        }
        else if ((destination - transform.position).sqrMagnitude < 0.04f &&
                 (requestedDestination - transform.position).sqrMagnitude > 0.25f)
        {
            Vector3 alternative = FindNearbyProjectedDestination(requestedDestination, navigationAreaMask);
            if ((alternative - transform.position).sqrMagnitude > 0.04f)
            {
                destination = alternative;
            }
            else
            {
                Debug.LogWarning("[CombatEnemyLocomotion] Destination non projetable | requested=" + requestedDestination +
                                 " | resolved=" + destination + " | current=" + transform.position +
                                 " | areaMask=" + navigationAgent.areaMask, this);
            }
        }
        navigationAgent.isStopped = false;
        navigationAgent.stoppingDistance = Mathf.Max(0f, stoppingDistance);
        bool accepted = navigationAgent.SetDestination(destination);
        navigationRequested = true;
        if (logDiagnostics)
        {
            Debug.Log("[CombatEnemyLocomotion] " + name + " destination=" + destination +
                      " accepted=" + accepted + " path=" + navigationAgent.pathStatus +
                      " pending=" + navigationAgent.pathPending +
                      " remaining=" + navigationAgent.remainingDistance.ToString("F2") +
                      " velocity=" + navigationAgent.velocity +
                      " onNavMesh=" + navigationAgent.isOnNavMesh, this);
        }
        if (logDiagnostics && !wasNavigating)
        {
            Debug.Log("[CombatEnemyLocomotion] " + name + " navigation active | stop=" + navigationAgent.stoppingDistance.ToString("F2"), this);
        }
        wasNavigating = true;
    }

    public void StopNavigation()
    {
        returnFacingActive = false;
        if (logDiagnostics && wasNavigating)
        {
            Debug.Log("[CombatEnemyLocomotion] " + name + " navigation stopped.", this);
        }
        navigationRequested = false;
        wasNavigating = false;
        if (navigationAgent != null && navigationAgent.isActiveAndEnabled && navigationAgent.isOnNavMesh)
        {
            navigationAgent.isStopped = true;
            navigationAgent.ResetPath();
        }

        ForceIdlePresentation();
    }

    public void FaceTarget(Vector3 worldPosition)
    {
        if (attackFacingLocked || GetComponent<EnemyCinematicState>()?.IsSuspended == true) return;
        Vector3 direction = worldPosition - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        transform.rotation = Quaternion.RotateTowards(transform.rotation,
            Quaternion.LookRotation(direction.normalized, Vector3.up), EffectiveFacingSpeed * LocalDeltaTime);
    }

    private void UpdateAnimatorPresentation()
    {
        Animator animator = animationContract != null ? animationContract.Animator : null;
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return;
        }

        if (cachedAnimator != animator)
        {
            cachedAnimator = animator;
            hasPlaybackRate = false;
            foreach (var parameter in animator.parameters)
                if (parameter.nameHash == PlaybackRate && parameter.type == AnimatorControllerParameterType.Float)
                    hasPlaybackRate = true;
            hasAnimatorParameters = UsesCommonAnimator || HasCombatLocomotionParameters(animator);
            if (!hasAnimatorParameters)
            {
                Debug.LogWarning("[CombatEnemyLocomotion] Animator '" + animator.name + "' ne possede pas encore les parametres CombatMoveX/Z/Speed. " +
                                 "Execute Lit/Combat/Configure Combat Locomotion puis relance le Play Mode.", this);
            }
        }

        if (!hasAnimatorParameters)
        {
            return;
        }

        if (logDiagnostics)
        {
            AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
            if (currentState.fullPathHash != lastReportedAnimatorStateHash)
            {
                lastReportedAnimatorStateHash = currentState.fullPathHash;
                Debug.Log("[CombatEnemyLocomotion] " + name + " animator=" + currentState.fullPathHash +
                          " | combatLocomotion=" + animator.HasState(0, CombatLocomotion) + ".", this);
            }
        }

        // A pending retaliation can include its anticipation while the enemy is
        // still navigating. Only the physics motor marks the committed portion
        // of an action that must own the Animator presentation.
        bool actionOwnsAnimation = (physicsMotor != null && physicsMotor.IsDrivingActionRootMotion) ||
            GetComponent<EnemyCombatBrain>()?.OwnsPresentation == true || enemy.IsHitRecovering;
        Vector3 velocity = navigationAgent != null && navigationAgent.isActiveAndEnabled && navigationAgent.isOnNavMesh && !navigationAgent.isStopped
            ? navigationAgent.velocity
            : Vector3.zero;
        velocity.y = 0f;
        float speed = velocity.magnitude;
        if (UsesCommonAnimator)
        {
            // The shared controller uses 1.1 for walk and 3.25 for run. The
            // previous 1/2 values never crossed its >2.5 run transitions.
            animator.SetFloat("LocomotionTier", runPhase ? 3.25f : 1.1f);
            animator.SetBool("CombatStrafeActive", true);
        }

        if (actionOwnsAnimation)
        {
            if (logDiagnostics && Time.unscaledTime >= nextMotionDiagnostic)
            {
                nextMotionDiagnostic = Time.unscaledTime + .5f;
                Debug.Log("[EnemyMotion] " + name + " phase=" + GetComponent<EnemyCombatBrain>()?.Phase +
                    " owner=Action/Suspension motor=" + physicsMotor?.State +
                    " navSpeed=" + speed.ToString("F2") +
                    " state=" + animator.GetCurrentAnimatorStateInfo(0).fullPathHash, this);
            }
            animator.SetFloat(UsesCommonAnimator ? CommonX : CombatMoveX, 0f, animatorDampTime, LocalDeltaTime);
            animator.SetFloat(UsesCommonAnimator ? CommonZ : CombatMoveZ, 0f, animatorDampTime, LocalDeltaTime);
            animator.SetFloat(UsesCommonAnimator ? CommonMagnitude : CombatMoveSpeed, 0f, animatorDampTime, LocalDeltaTime);
            wasMovingVisually = false;
            return;
        }

        float scale = timeDomain != null ? timeDomain.Scale : 1f;
        float unscaledSpeed = scale > .0001f ? speed / scale : 0f;
        bool moving = ShouldPresentLocomotion(unscaledSpeed, wasMovingVisually);
        if (hasPlaybackRate)
            animator.SetFloat(PlaybackRate, moving ? ResolvePlaybackRate(speed, scale,
                runPhase ? runCycleSpeed : walkCycleSpeed) : 0f, .08f, LocalDeltaTime);
        if (logDiagnostics && Time.unscaledTime >= nextMotionDiagnostic)
        {
            nextMotionDiagnostic = Time.unscaledTime + .5f;
            Debug.Log("[EnemyMotion] " + name + " phase=" + GetComponent<EnemyCombatBrain>()?.Phase +
                " owner=NavMesh speed=" + speed.ToString("F2") + " run=" + runPhase +
                " state=" + animator.GetCurrentAnimatorStateInfo(0).fullPathHash +
                " next=" + animator.GetNextAnimatorStateInfo(0).fullPathHash +
                " cadence=" + (hasPlaybackRate ? animator.GetFloat(PlaybackRate) : 1f) +
                " facing=" + EffectiveFacingSpeed, this);
        }
        if (moving)
        {
            Vector3 localVelocity = transform.InverseTransformDirection(velocity / speed);
            animator.SetFloat(UsesCommonAnimator ? CommonX : CombatMoveX, Mathf.Clamp(localVelocity.x, -1f, 1f), animatorDampTime, LocalDeltaTime);
            animator.SetFloat(UsesCommonAnimator ? CommonZ : CombatMoveZ, Mathf.Clamp(localVelocity.z, -1f, 1f), animatorDampTime, LocalDeltaTime);
            animator.SetFloat(UsesCommonAnimator ? CommonMagnitude : CombatMoveSpeed, UsesCommonAnimator ? 1f : speed, animatorDampTime, LocalDeltaTime);
            if (animator.HasState(0, CombatLocomotion))
            {
                AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
                if (state.fullPathHash != CombatLocomotion &&
                    (!animator.IsInTransition(0) || animator.GetNextAnimatorStateInfo(0).fullPathHash != CombatLocomotion))
                {
                    animator.CrossFadeInFixedTime(CombatLocomotion, 0.08f, 0);
                }
            }
            wasMovingVisually = true;
        }
        else
        {
            animator.SetFloat(UsesCommonAnimator ? CommonX : CombatMoveX, 0f, animatorDampTime, LocalDeltaTime);
            animator.SetFloat(UsesCommonAnimator ? CommonZ : CombatMoveZ, 0f, animatorDampTime, LocalDeltaTime);
            animator.SetFloat(UsesCommonAnimator ? CommonMagnitude : CombatMoveSpeed, 0f, animatorDampTime, LocalDeltaTime);
            ForceIdlePresentation();
        }
    }

    private void ForceIdlePresentation()
    {
        if (GetComponent<EnemyCombatBrain>()?.OwnsPresentation == true || enemy != null && enemy.IsHitRecovering) return;
        if (physicsMotor != null && physicsMotor.IsDrivingActionRootMotion)
        {
            return;
        }

        Animator animator = animationContract != null ? animationContract.Animator : null;
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return;
        }

        animator.SetFloat(UsesCommonAnimator ? CommonX : CombatMoveX, 0f);
        animator.SetFloat(UsesCommonAnimator ? CommonZ : CombatMoveZ, 0f);
        animator.SetFloat(UsesCommonAnimator ? CommonMagnitude : CombatMoveSpeed, 0f);
        wasMovingVisually = false;
        AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
        int idleState = animator.HasState(0, CombatIdle) ? CombatIdle : Idle;
        bool enteringLocomotion = animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).fullPathHash == CombatLocomotion;
        if (animator.HasState(0, idleState) && (currentState.fullPathHash == CombatLocomotion || enteringLocomotion) &&
            (!animator.IsInTransition(0) || animator.GetNextAnimatorStateInfo(0).fullPathHash != idleState))
        {
            animator.CrossFadeInFixedTime(idleState, 0.08f, 0);
        }
    }

    public static bool ShouldPresentLocomotion(float speed, bool wasMoving) => speed > (wasMoving ? .03f : .08f);

    public static float ResolvePlaybackRate(float speed, float localScale, float referenceSpeed) =>
        localScale > .0001f && referenceSpeed > .0001f
            ? Mathf.Clamp(speed / (localScale * referenceSpeed), 0f, 1.35f) : 0f;

    private void ResolveReferences()
    {
        enemy ??= GetComponent<RealTimeCombatEnemy>();
        navigationAgent = GetComponent<NavMeshAgent>();
        animationContract ??= GetComponent<CombatActorAnimationRoot>();
        physicsMotor = GetComponent<CombatEnemyPhysicsMotor>();
        timeDomain ??= GetComponent<CombatTimeDomain>();
    }

    private void CaptureNavigationDefaults()
    {
        if (baseNavigationCaptured || navigationAgent == null) return;
        baseNavigationCaptured = true;
        baseNavigationSpeed = navigationAgent.speed;
        baseNavigationAcceleration = navigationAgent.acceleration;
        baseNavigationAngularSpeed = navigationAgent.angularSpeed;
        requestedNavigationSpeed = baseNavigationSpeed;
    }

    private void ApplyLocalNavigationScale()
    {
        if (navigationAgent == null) return;
        CaptureNavigationDefaults();
        float scale = timeDomain != null ? timeDomain.Scale : 1f;
        navigationAgent.speed = Mathf.Max(0.01f, requestedNavigationSpeed) * scale;
        navigationAgent.acceleration = baseNavigationAcceleration * scale;
        navigationAgent.angularSpeed = baseNavigationAngularSpeed * scale;
    }

    private void SetMovementPace(float distanceToTarget)
    {
        if (navigationAgent == null) return;
        CaptureNavigationDefaults();

        bool nextRunPhase = runPhase
            ? distanceToTarget > Mathf.Min(positioning.walkResumeDistance, positioning.runDistance)
            : distanceToTarget >= Mathf.Max(0.1f, positioning.runDistance);
        float nextSpeed = nextRunPhase
            ? Mathf.Max(positioning.walkSpeed, positioning.runSpeed)
            : Mathf.Max(0.1f, positioning.walkSpeed);

        if (runPhase == nextRunPhase && Mathf.Abs(requestedNavigationSpeed - nextSpeed) < 0.01f)
        {
            return;
        }

        runPhase = nextRunPhase;
        requestedNavigationSpeed = nextSpeed;
        if (logDiagnostics)
        {
            Debug.Log("[CombatEnemyLocomotion] " + name + " phase=" +
                      (runPhase ? "Run" : "Walk") + " | distance=" + distanceToTarget.ToString("F2") +
                      " | speed=" + requestedNavigationSpeed.ToString("F2"), this);
        }
    }

    private Vector3 ResolveClearDestination(Vector3 requestedDestination)
    {
        if (navigationAgent == null || navMeshEdgeClearance <= 0f ||
            !NavMesh.SamplePosition(requestedDestination, out NavMeshHit requestedHit, clearanceSearchRadius, navigationAgent.areaMask))
        {
            return requestedDestination;
        }

        Vector3 bestPosition = requestedHit.position;
        float bestClearance = GetNavMeshEdgeClearance(bestPosition);
        if (bestClearance >= navMeshEdgeClearance)
        {
            return bestPosition;
        }

        Vector3 bestSafePosition = bestPosition;
        float bestSafeScore = float.PositiveInfinity;
        const int ringCount = 3;
        int samplesPerRing = Mathf.CeilToInt(clearanceSearchSamples / (float)ringCount);
        for (int ring = 1; ring <= ringCount; ring++)
        {
            float radius = clearanceSearchRadius * ring / ringCount;
            for (int index = 0; index < samplesPerRing; index++)
            {
                float angle = index * Mathf.PI * 2f / samplesPerRing;
                Vector3 probe = requestedHit.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                if (!NavMesh.SamplePosition(probe, out NavMeshHit candidateHit, 0.35f, navigationAgent.areaMask))
                {
                    continue;
                }

                float candidateClearance = GetNavMeshEdgeClearance(candidateHit.position);
                if (candidateClearance > bestClearance)
                {
                    bestClearance = candidateClearance;
                    bestPosition = candidateHit.position;
                }

                if (candidateClearance < navMeshEdgeClearance)
                {
                    continue;
                }

                float score = (candidateHit.position - requestedHit.position).sqrMagnitude;
                if (score < bestSafeScore)
                {
                    bestSafeScore = score;
                    bestSafePosition = candidateHit.position;
                }
            }
        }

        if (bestSafeScore < float.PositiveInfinity)
        {
            return bestSafePosition;
        }

        // A narrow corridor may not contain a fully safe endpoint. Keep the
        // enemy on the widest available NavMesh point instead of steering it
        // deliberately into the closest wall.
        return bestPosition;
    }

    private Vector3 FindNearbyProjectedDestination(Vector3 requestedDestination, int areaMask)
    {
        Vector3 best = transform.position;
        float bestScore = float.PositiveInfinity;
        int samples = Mathf.Max(8, clearanceSearchSamples);
        for (int ring = 1; ring <= 3; ring++)
        {
            float radius = Mathf.Max(0.5f, clearanceSearchRadius) * ring / 3f;
            for (int index = 0; index < samples; index++)
            {
                float angle = index * Mathf.PI * 2f / samples;
                Vector3 probe = requestedDestination + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                if (!NavMesh.SamplePosition(probe, out NavMeshHit hit, 0.5f, areaMask) ||
                    (hit.position - transform.position).sqrMagnitude <= 0.04f)
                {
                    continue;
                }

                float score = (hit.position - requestedDestination).sqrMagnitude;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = hit.position;
                }
            }
        }

        return best;
    }

    private float GetNavMeshEdgeClearance(Vector3 position)
    {
        return NavMesh.FindClosestEdge(position, out NavMeshHit edgeHit, navigationAgent.areaMask)
            ? edgeHit.distance
            : float.PositiveInfinity;
    }

    private static bool HasCombatLocomotionParameters(Animator animator)
    {
        bool x = false;
        bool z = false;
        bool speed = false;
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            x |= parameter.nameHash == CombatMoveX && parameter.type == AnimatorControllerParameterType.Float;
            z |= parameter.nameHash == CombatMoveZ && parameter.type == AnimatorControllerParameterType.Float;
            speed |= parameter.nameHash == CombatMoveSpeed && parameter.type == AnimatorControllerParameterType.Float;
        }
        return x && z && speed;
    }
}
