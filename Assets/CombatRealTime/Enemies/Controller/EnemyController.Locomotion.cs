using UnityEngine;
using UnityEngine.AI;

public sealed partial class EnemyController
{
    [System.Serializable]
    public sealed class CombatPositioningProfile
    {
        [Min(0.1f)]
        public float preferredDistance = 2.6f;
        [Min(0f)]
        public float minimumRetreatDistance = 1.2f;
        [Min(0f)]
        public float strafeRadius = 1.1f;
        [Min(0.1f)]
        public float strafeSideHoldSeconds = 1.35f;
        [Min(0.1f)]
        public float walkSpeed = 1.8f;
        [Min(0.1f)]
        public float runSpeed = 3.6f;
        [Min(0.1f)]
        public float runDistance = 8f;
        [Min(0f)]
        public float walkResumeDistance = 6f;
        [Min(0.02f)]
        public float pursuitRefreshSeconds = 0.15f;
        [Min(0.01f)]
        public float pursuitTargetMoveDistance = 0.5f;
        [Min(0f)]
        public float walkSpeedThreshold = 1.2f;
        [Min(0f)]
        public float runSpeedThreshold = 3.4f;
    }

    private static readonly int LocomotionCombatMoveX = Animator.StringToHash("CombatMoveX");
    private static readonly int LocomotionCombatMoveZ = Animator.StringToHash("CombatMoveZ");
    private static readonly int LocomotionCombatMoveSpeed = Animator.StringToHash("CombatMoveSpeed");
    private static readonly int LocomotionCommonX = Animator.StringToHash("HorizontalMovement");
    private static readonly int LocomotionCommonZ = Animator.StringToHash("ForwardMovement");
    private static readonly int LocomotionCommonMagnitude = Animator.StringToHash("CombatMoveMagnitude");
    private bool LocomotionUsesCommonAnimator => GetComponent<EnemyController>()?.HasProfile == true;
    public void SetFacingSpeed(float speed) => LocomotionProfileFacingSpeed = Mathf.Max(0f, speed);
    private float? LocomotionProfileFacingSpeed;
    public float EffectiveFacingSpeed => LocomotionProfileFacingSpeed ?? LocomotionFacingSpeedDegreesPerSecond;
    private static readonly int LocomotionPlaybackRate = Animator.StringToHash("CombatLocomotionPlaybackRate");
    private bool LocomotionHasPlaybackRate;
    private float LocomotionNextMotionDiagnostic;
    // Animator.HasState/CrossFade with an int expects the full state path.
    // A short-name hash silently prevented the enemy combat blend tree from
    // ever taking over, leaving NavMesh movement visually in Idle.
    private static readonly int LocomotionCombatLocomotion = Animator.StringToHash("Base Layer.CombatLocomotion");
    private static readonly int LocomotionCombatIdle = Animator.StringToHash("Base Layer.CombatIdle");
    private static readonly int LocomotionIdle = Animator.StringToHash("Base Layer.Idle");
    [SerializeField]
    private EnemyController LocomotionEnemy;
    [SerializeField]
    private NavMeshAgent LocomotionNavigationAgent;
    [SerializeField]
    private CharacterAnimationController LocomotionAnimationContract;
    [SerializeField]
    private EnemyController LocomotionPhysicsMotor;
    [SerializeField]
    private CombatTimeDomain LocomotionTimeDomain;
    private CombatPositioningProfile LocomotionPositioning { get => Configuration.LocomotionPositioning; set => Configuration.LocomotionPositioning = value; }
    private float LocomotionFacingSpeedDegreesPerSecond { get => Configuration.LocomotionFacingSpeedDegreesPerSecond; set => Configuration.LocomotionFacingSpeedDegreesPerSecond = value; }
    private float LocomotionAnimatorDampTime { get => Configuration.LocomotionAnimatorDampTime; set => Configuration.LocomotionAnimatorDampTime = value; }
    private float LocomotionWalkCycleSpeed { get => Configuration.LocomotionWalkCycleSpeed; set => Configuration.LocomotionWalkCycleSpeed = value; }
    private float LocomotionRunCycleSpeed { get => Configuration.LocomotionRunCycleSpeed; set => Configuration.LocomotionRunCycleSpeed = value; }
    private float LocomotionNavMeshEdgeClearance { get => Configuration.LocomotionNavMeshEdgeClearance; set => Configuration.LocomotionNavMeshEdgeClearance = value; }
    private float LocomotionClearanceSearchRadius { get => Configuration.LocomotionClearanceSearchRadius; set => Configuration.LocomotionClearanceSearchRadius = value; }
    private int LocomotionClearanceSearchSamples { get => Configuration.LocomotionClearanceSearchSamples; set => Configuration.LocomotionClearanceSearchSamples = value; }
    private bool LocomotionLogDiagnostics { get => Configuration.LocomotionLogDiagnostics; set => Configuration.LocomotionLogDiagnostics = value; }
    private Transform LocomotionCombatTarget;
    private float LocomotionNextSideChangeAt;
    private float LocomotionStrafeSide = 1f;
    private bool LocomotionNavigationRequested;
    private bool LocomotionWasNavigating;
    private bool LocomotionHasAnimatorParameters;
    private Animator LocomotionCachedAnimator;
    private int LocomotionLastReportedAnimatorStateHash;
    private bool LocomotionWasMovingVisually;
    private bool LocomotionAttackFacingLocked;
    private bool LocomotionReturnFacingActive;
    private Vector3 LocomotionReturnFacingDestination;
    private bool LocomotionBaseNavigationCaptured;
    private float LocomotionBaseNavigationSpeed;
    private float LocomotionBaseNavigationAcceleration;
    private float LocomotionBaseNavigationAngularSpeed;
    private float LocomotionRequestedNavigationSpeed;
    private bool LocomotionRunPhase;
    private NavMeshPath LocomotionPursuitPath;
    private float LocomotionNextPursuitUpdate;
    private Vector3 LocomotionLastPursuitTarget;
    private float LocomotionLastPursuitRange = -1f;
    public string PursuitFailure { get; private set; }

    private float LocomotionLocalTime => LocomotionTimeDomain != null ? LocomotionTimeDomain.LocalTime : Time.time;
    private float LocomotionLocalDeltaTime => LocomotionTimeDomain != null ? LocomotionTimeDomain.DeltaTime : Time.deltaTime;
    public CombatPositioningProfile Positioning => LocomotionPositioning;
    public bool IsNavigating => LocomotionNavigationRequested && LocomotionNavigationAgent != null && LocomotionNavigationAgent.isActiveAndEnabled && LocomotionNavigationAgent.isOnNavMesh && !LocomotionNavigationAgent.isStopped;


    private void LocomotionAwake()
    {
        LocomotionResolveReferences();
        LocomotionCaptureNavigationDefaults();
        LocomotionStrafeSide = Random.value < 0.5f ? -1f : 1f;
        LocomotionNextSideChangeAt = LocomotionLocalTime + LocomotionPositioning.strafeSideHoldSeconds;
    }

    private void LocomotionOnDisable()
    {
        if (LocomotionNavigationAgent != null && LocomotionNavigationAgent.isActiveAndEnabled)
        {
            LocomotionNavigationAgent.updateRotation = true;
        }
    }

#if UNITY_EDITOR


#endif
    private void LocomotionUpdate()
    {
        if (LocomotionEnemy != null && LocomotionEnemy.Health != null && LocomotionEnemy.Health.IsDead)
        {
            LocomotionCombatTarget = null;
            StopNavigation();
            return;
        }

        LocomotionApplyLocalNavigationScale();
        LocomotionUpdateAnimatorPresentation();
    }

    private void LocomotionLateUpdate()
    {
        if (LocomotionEnemy != null && LocomotionEnemy.Health != null && LocomotionEnemy.Health.IsDead)
            return;
        // Le root motion peut ecrire la rotation apres l'IA. On conserve donc
        // l'ennemi face a sa cible a la fin de l'image, sauf si une Timeline
        // cinematographique est explicitement proprietaire de sa pose.
        if (GetComponent<EnemyController>()?.IsSuspended != true && !LocomotionAttackFacingLocked && LocomotionCombatTarget != null && (LocomotionAnimationContract == null || !LocomotionAnimationContract.IsCinematicMotionActive))
        {
            FaceTarget(LocomotionCombatTarget.position);
        }
        else if (LocomotionReturnFacingActive && LocomotionCombatTarget == null && (LocomotionPhysicsMotor == null || !LocomotionPhysicsMotor.IsDrivingActionRootMotion) && (LocomotionAnimationContract == null || !LocomotionAnimationContract.IsCinematicMotionActive))
        {
            Vector3 facingPoint = LocomotionReturnFacingDestination;
            if (LocomotionNavigationAgent != null && LocomotionNavigationAgent.isActiveAndEnabled && LocomotionNavigationAgent.isOnNavMesh && !LocomotionNavigationAgent.isStopped && LocomotionNavigationAgent.hasPath && !LocomotionNavigationAgent.pathPending)
                facingPoint = LocomotionNavigationAgent.steeringTarget;
            FaceTarget(facingPoint);
        }
    }

    public void SetReturnFacing(Vector3 destination)
    {
        LocomotionReturnFacingDestination = destination;
        LocomotionReturnFacingActive = true;
        if (LocomotionNavigationAgent != null)
            LocomotionNavigationAgent.updateRotation = false;
    }

    public void SetCombatTarget(Transform target)
    {
        LocomotionReturnFacingActive = false;
        if (LocomotionCombatTarget != target)
            LocomotionLastPursuitRange = -1f;
        LocomotionCombatTarget = target;
        LocomotionResolveReferences();
        // NavMesh continues to own translation, but its automatic yaw fights
        // the face-to-face combat rule. Rotation is applied once in LateUpdate
        // by FaceTarget instead.
        if (LocomotionNavigationAgent != null && LocomotionNavigationAgent.isActiveAndEnabled)
        {
            LocomotionNavigationAgent.updateRotation = target == null;
        }
    }

    /// <summary>
    /// Wind-up may still track the target. Once an authored event commits an
    /// attack, this prevents LateUpdate from silently turning the hit toward a
    /// player who has already dodged away.
    /// </summary>
    public void SetAttackFacingLocked(bool value)
    {
        LocomotionAttackFacingLocked = value;
    }

    public bool ApproachTarget(float attackDistance)
    {
        LocomotionResolveReferences();
        string blockReason = LocomotionCombatTarget == null ? "cible absente" : LocomotionNavigationAgent == null ? "agent absent" : !LocomotionNavigationAgent.isActiveAndEnabled ? "agent desactive" : !LocomotionNavigationAgent.isOnNavMesh ? "agent hors NavMesh" : GetComponent<EnemyController>()?.IsSuspended == true ? "suspension cinematique" : LocomotionPhysicsMotor != null && LocomotionPhysicsMotor.IsDrivingActionRootMotion ? "moteur physique: " + LocomotionPhysicsMotor.State : null;
        if (blockReason != null)
        {
            PursuitFailure = blockReason;
            StopNavigation();
            return false;
        }

        Vector3 away = transform.position - LocomotionCombatTarget.position;
        away.y = 0f;
        LocomotionSetMovementPace(away.magnitude);
        if (LocomotionLocalTime < LocomotionNextPursuitUpdate && Mathf.Abs(LocomotionLastPursuitRange - attackDistance) < .01f && (LocomotionLastPursuitTarget - LocomotionCombatTarget.position).sqrMagnitude < LocomotionPositioning.pursuitTargetMoveDistance * LocomotionPositioning.pursuitTargetMoveDistance)
            return PursuitFailure == null;
        LocomotionNextPursuitUpdate = LocomotionLocalTime + LocomotionPositioning.pursuitRefreshSeconds;
        LocomotionLastPursuitTarget = LocomotionCombatTarget.position;
        LocomotionLastPursuitRange = attackDistance;
        Vector3 radial = away.sqrMagnitude > .0001f ? away.normalized : -transform.forward;
        Vector3 destination = LocomotionCombatTarget.position + radial * attackDistance;
        LocomotionPursuitPath ??= new NavMeshPath();
        var filter = new NavMeshQueryFilter{agentTypeID = LocomotionNavigationAgent.agentTypeID, areaMask = LocomotionNavigationAgent.areaMask};
        if (!NavMesh.SamplePosition(destination, out NavMeshHit hit, .35f, filter) || !LocomotionNavigationAgent.CalculatePath(hit.position, LocomotionPursuitPath) || LocomotionPursuitPath.status != NavMeshPathStatus.PathComplete)
        {
            PursuitFailure = "destination locale ou chemin complet introuvable";
            StopNavigation();
            return false;
        }

        LocomotionNavigationAgent.stoppingDistance = .05f;
        LocomotionNavigationAgent.isStopped = false;
        bool accepted = LocomotionNavigationAgent.SetPath(LocomotionPursuitPath);
        PursuitFailure = accepted ? null : "chemin refuse par agent";
        if (!accepted)
        {
            StopNavigation();
            return false;
        }

        LocomotionNavigationRequested = LocomotionWasNavigating = true;
        return true;
    }

    public void NavigateTowardsTarget(float stoppingDistance)
    {
        if (LocomotionCombatTarget == null)
        {
            StopNavigation();
            return;
        }

        Vector3 toSelf = transform.position - LocomotionCombatTarget.position;
        toSelf.y = 0f;
        float distance = toSelf.magnitude;
        Vector3 away = distance > 0.001f ? toSelf / distance : -transform.forward;
        Vector3 destination;
        if (distance > stoppingDistance + 0.18f)
        {
            LocomotionSetMovementPace(distance);
            destination = LocomotionCombatTarget.position + away * stoppingDistance;
        }
        else if (distance < LocomotionPositioning.minimumRetreatDistance)
        {
            LocomotionSetMovementPace(0f);
            destination = LocomotionCombatTarget.position + away * Mathf.Max(LocomotionPositioning.minimumRetreatDistance, stoppingDistance);
        }
        else
        {
            LocomotionSetMovementPace(0f);
            if (LocomotionLocalTime >= LocomotionNextSideChangeAt)
            {
                LocomotionStrafeSide *= -1f;
                LocomotionNextSideChangeAt = LocomotionLocalTime + Mathf.Max(0.1f, LocomotionPositioning.strafeSideHoldSeconds);
            }

            Vector3 side = Vector3.Cross(Vector3.up, away) * LocomotionStrafeSide;
            destination = LocomotionCombatTarget.position + away * stoppingDistance + side * LocomotionPositioning.strafeRadius;
        }

        NavigateTo(destination, .15f);
    }

    public void NavigateTo(Vector3 destination, float stoppingDistance)
    {
        LocomotionResolveReferences();
        if (LocomotionEnemy != null && LocomotionEnemy.Health != null && LocomotionEnemy.Health.IsDead)
        {
            StopNavigation();
            return;
        }

        if (LocomotionPhysicsMotor != null && LocomotionPhysicsMotor.IsDrivingActionRootMotion)
        {
            StopNavigation();
            return;
        }

        if (LocomotionNavigationAgent == null || !LocomotionNavigationAgent.isActiveAndEnabled || !LocomotionNavigationAgent.isOnNavMesh)
        {
            // Navigation unavailable intentionally means no movement. A transform
            // fallback would compete with physics and can produce teleports.
            StopNavigation();
            return;
        }

        Vector3 requestedDestination = destination;
        int navigationAreaMask = LocomotionNavigationAgent.areaMask == 0 ? NavMesh.AllAreas : LocomotionNavigationAgent.areaMask;
        // Prefer the exact projected point. Clearance heuristics are only a
        // fallback: on a freshly built runtime mesh they can otherwise select
        // the current polygon and silently turn a valid reposition into a
        // zero-length path.
        if (!NavMesh.SamplePosition(requestedDestination, out NavMeshHit exactHit, .75f, navigationAreaMask))
        {
            destination = LocomotionResolveClearDestination(requestedDestination);
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
        if ((destination - transform.position).sqrMagnitude < 0.04f && (requestedDestination - transform.position).sqrMagnitude > 0.25f && NavMesh.SamplePosition(requestedDestination, out NavMeshHit directHit, LocomotionClearanceSearchRadius, navigationAreaMask))
        {
            destination = directHit.position;
        }
        else if ((destination - transform.position).sqrMagnitude < 0.04f && (requestedDestination - transform.position).sqrMagnitude > 0.25f)
        {
            Vector3 alternative = LocomotionFindNearbyProjectedDestination(requestedDestination, navigationAreaMask);
            if ((alternative - transform.position).sqrMagnitude > 0.04f)
            {
                destination = alternative;
            }
            else
            {
                Debug.LogWarning("[CombatEnemyLocomotion] Destination non projetable | requested=" + requestedDestination + " | resolved=" + destination + " | current=" + transform.position + " | areaMask=" + LocomotionNavigationAgent.areaMask, this);
            }
        }

        LocomotionNavigationAgent.isStopped = false;
        LocomotionNavigationAgent.stoppingDistance = Mathf.Max(0f, stoppingDistance);
        bool accepted = LocomotionNavigationAgent.SetDestination(destination);
        LocomotionNavigationRequested = true;
        if (LocomotionLogDiagnostics)
        {
            Debug.Log("[CombatEnemyLocomotion] " + name + " destination=" + destination + " accepted=" + accepted + " path=" + LocomotionNavigationAgent.pathStatus + " pending=" + LocomotionNavigationAgent.pathPending + " remaining=" + LocomotionNavigationAgent.remainingDistance.ToString("F2") + " velocity=" + LocomotionNavigationAgent.velocity + " onNavMesh=" + LocomotionNavigationAgent.isOnNavMesh, this);
        }

        if (LocomotionLogDiagnostics && !LocomotionWasNavigating)
        {
            Debug.Log("[CombatEnemyLocomotion] " + name + " navigation active | stop=" + LocomotionNavigationAgent.stoppingDistance.ToString("F2"), this);
        }

        LocomotionWasNavigating = true;
    }

    public void StopNavigation()
    {
        LocomotionReturnFacingActive = false;
        if (LocomotionLogDiagnostics && LocomotionWasNavigating)
        {
            Debug.Log("[CombatEnemyLocomotion] " + name + " navigation stopped.", this);
        }

        LocomotionNavigationRequested = false;
        LocomotionWasNavigating = false;
        if (LocomotionNavigationAgent != null && LocomotionNavigationAgent.isActiveAndEnabled && LocomotionNavigationAgent.isOnNavMesh)
        {
            LocomotionNavigationAgent.isStopped = true;
            LocomotionNavigationAgent.ResetPath();
        }

        LocomotionForceIdlePresentation();
    }

    public void FaceTarget(Vector3 worldPosition)
    {
        if (LocomotionEnemy != null && LocomotionEnemy.Health != null && LocomotionEnemy.Health.IsDead)
            return;
        if (LocomotionAttackFacingLocked || GetComponent<EnemyController>()?.IsSuspended == true)
            return;
        Vector3 direction = worldPosition - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction.normalized, Vector3.up), EffectiveFacingSpeed * LocomotionLocalDeltaTime);
    }

    private void LocomotionUpdateAnimatorPresentation()
    {
        if (LocomotionEnemy != null && LocomotionEnemy.Health != null && LocomotionEnemy.Health.IsDead)
            return;
        Animator animator = LocomotionAnimationContract != null ? LocomotionAnimationContract.Animator : null;
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return;
        }

        if (LocomotionCachedAnimator != animator)
        {
            LocomotionCachedAnimator = animator;
            LocomotionHasPlaybackRate = false;
            foreach (var parameter in animator.parameters)
                if (parameter.nameHash == LocomotionPlaybackRate && parameter.type == AnimatorControllerParameterType.Float)
                    LocomotionHasPlaybackRate = true;
            LocomotionHasAnimatorParameters = LocomotionUsesCommonAnimator || LocomotionHasCombatLocomotionParameters(animator);
            if (!LocomotionHasAnimatorParameters)
            {
                Debug.LogWarning("[CombatEnemyLocomotion] Animator '" + animator.name + "' ne possede pas encore les parametres CombatMoveX/Z/Speed. " + "Execute Lit/Combat/Configure Combat Locomotion puis relance le Play Mode.", this);
            }
        }

        if (!LocomotionHasAnimatorParameters)
        {
            return;
        }

        if (LocomotionLogDiagnostics)
        {
            AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
            if (currentState.fullPathHash != LocomotionLastReportedAnimatorStateHash)
            {
                LocomotionLastReportedAnimatorStateHash = currentState.fullPathHash;
                Debug.Log("[CombatEnemyLocomotion] " + name + " animator=" + currentState.fullPathHash + " | combatLocomotion=" + animator.HasState(0, LocomotionCombatLocomotion) + ".", this);
            }
        }

        // A pending retaliation can include its anticipation while the enemy is
        // still navigating. Only the physics motor marks the committed portion
        // of an action that must own the Animator presentation.
        bool actionOwnsAnimation = (LocomotionPhysicsMotor != null && LocomotionPhysicsMotor.IsDrivingActionRootMotion) || GetComponent<EnemyController>()?.OwnsPresentation == true || LocomotionEnemy.IsHitRecovering;
        Vector3 velocity = LocomotionNavigationAgent != null && LocomotionNavigationAgent.isActiveAndEnabled && LocomotionNavigationAgent.isOnNavMesh && !LocomotionNavigationAgent.isStopped ? LocomotionNavigationAgent.velocity : Vector3.zero;
        velocity.y = 0f;
        float speed = velocity.magnitude;
        if (LocomotionUsesCommonAnimator)
        {
            // The shared controller uses 1.1 for walk and 3.25 for run. The
            // previous 1/2 values never crossed its >2.5 run transitions.
            animator.SetFloat("LocomotionTier", LocomotionRunPhase ? 3.25f : 1.1f);
            animator.SetBool("CombatStrafeActive", true);
        }

        if (actionOwnsAnimation)
        {
            if (LocomotionLogDiagnostics && Time.unscaledTime >= LocomotionNextMotionDiagnostic)
            {
                LocomotionNextMotionDiagnostic = Time.unscaledTime + .5f;
                Debug.Log("[EnemyMotion] " + name + " phase=" + GetComponent<EnemyController>()?.Phase + " owner=Action/Suspension motor=" + LocomotionPhysicsMotor?.State + " navSpeed=" + speed.ToString("F2") + " state=" + animator.GetCurrentAnimatorStateInfo(0).fullPathHash, this);
            }

            animator.SetFloat(LocomotionUsesCommonAnimator ? LocomotionCommonX : LocomotionCombatMoveX, 0f, LocomotionAnimatorDampTime, LocomotionLocalDeltaTime);
            animator.SetFloat(LocomotionUsesCommonAnimator ? LocomotionCommonZ : LocomotionCombatMoveZ, 0f, LocomotionAnimatorDampTime, LocomotionLocalDeltaTime);
            animator.SetFloat(LocomotionUsesCommonAnimator ? LocomotionCommonMagnitude : LocomotionCombatMoveSpeed, 0f, LocomotionAnimatorDampTime, LocomotionLocalDeltaTime);
            LocomotionWasMovingVisually = false;
            return;
        }

        float scale = LocomotionTimeDomain != null ? LocomotionTimeDomain.Scale : 1f;
        float unscaledSpeed = scale > .0001f ? speed / scale : 0f;
        bool moving = ShouldPresentLocomotion(unscaledSpeed, LocomotionWasMovingVisually);
        if (LocomotionHasPlaybackRate)
            animator.SetFloat(LocomotionPlaybackRate, moving ? ResolvePlaybackRate(speed, scale, LocomotionRunPhase ? LocomotionRunCycleSpeed : LocomotionWalkCycleSpeed) : 0f, .08f, LocomotionLocalDeltaTime);
        if (LocomotionLogDiagnostics && Time.unscaledTime >= LocomotionNextMotionDiagnostic)
        {
            LocomotionNextMotionDiagnostic = Time.unscaledTime + .5f;
            Debug.Log("[EnemyMotion] " + name + " phase=" + GetComponent<EnemyController>()?.Phase + " owner=NavMesh speed=" + speed.ToString("F2") + " run=" + LocomotionRunPhase + " state=" + animator.GetCurrentAnimatorStateInfo(0).fullPathHash + " next=" + animator.GetNextAnimatorStateInfo(0).fullPathHash + " cadence=" + (LocomotionHasPlaybackRate ? animator.GetFloat(LocomotionPlaybackRate) : 1f) + " facing=" + EffectiveFacingSpeed, this);
        }

        if (moving)
        {
            Vector3 localVelocity = transform.InverseTransformDirection(velocity / speed);
            animator.SetFloat(LocomotionUsesCommonAnimator ? LocomotionCommonX : LocomotionCombatMoveX, Mathf.Clamp(localVelocity.x, -1f, 1f), LocomotionAnimatorDampTime, LocomotionLocalDeltaTime);
            animator.SetFloat(LocomotionUsesCommonAnimator ? LocomotionCommonZ : LocomotionCombatMoveZ, Mathf.Clamp(localVelocity.z, -1f, 1f), LocomotionAnimatorDampTime, LocomotionLocalDeltaTime);
            animator.SetFloat(LocomotionUsesCommonAnimator ? LocomotionCommonMagnitude : LocomotionCombatMoveSpeed, LocomotionUsesCommonAnimator ? 1f : speed, LocomotionAnimatorDampTime, LocomotionLocalDeltaTime);
            if (animator.HasState(0, LocomotionCombatLocomotion))
            {
                AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
                if (state.fullPathHash != LocomotionCombatLocomotion && (!animator.IsInTransition(0) || animator.GetNextAnimatorStateInfo(0).fullPathHash != LocomotionCombatLocomotion))
                {
                    animator.CrossFadeInFixedTime(LocomotionCombatLocomotion, 0.08f, 0);
                }
            }

            LocomotionWasMovingVisually = true;
        }
        else
        {
            animator.SetFloat(LocomotionUsesCommonAnimator ? LocomotionCommonX : LocomotionCombatMoveX, 0f, LocomotionAnimatorDampTime, LocomotionLocalDeltaTime);
            animator.SetFloat(LocomotionUsesCommonAnimator ? LocomotionCommonZ : LocomotionCombatMoveZ, 0f, LocomotionAnimatorDampTime, LocomotionLocalDeltaTime);
            animator.SetFloat(LocomotionUsesCommonAnimator ? LocomotionCommonMagnitude : LocomotionCombatMoveSpeed, 0f, LocomotionAnimatorDampTime, LocomotionLocalDeltaTime);
            LocomotionForceIdlePresentation();
        }
    }

    private void LocomotionForceIdlePresentation()
    {
        if (LocomotionEnemy != null && LocomotionEnemy.Health != null && LocomotionEnemy.Health.IsDead)
            return;
        if (GetComponent<EnemyController>()?.OwnsPresentation == true || LocomotionEnemy != null && LocomotionEnemy.IsHitRecovering)
            return;
        if (LocomotionPhysicsMotor != null && LocomotionPhysicsMotor.IsDrivingActionRootMotion)
        {
            return;
        }

        Animator animator = LocomotionAnimationContract != null ? LocomotionAnimationContract.Animator : null;
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return;
        }

        animator.SetFloat(LocomotionUsesCommonAnimator ? LocomotionCommonX : LocomotionCombatMoveX, 0f);
        animator.SetFloat(LocomotionUsesCommonAnimator ? LocomotionCommonZ : LocomotionCombatMoveZ, 0f);
        animator.SetFloat(LocomotionUsesCommonAnimator ? LocomotionCommonMagnitude : LocomotionCombatMoveSpeed, 0f);
        LocomotionWasMovingVisually = false;
        AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
        int idleState = animator.HasState(0, LocomotionCombatIdle) ? LocomotionCombatIdle : LocomotionIdle;
        bool enteringLocomotion = animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).fullPathHash == LocomotionCombatLocomotion;
        if (animator.HasState(0, idleState) && (currentState.fullPathHash == LocomotionCombatLocomotion || enteringLocomotion) && (!animator.IsInTransition(0) || animator.GetNextAnimatorStateInfo(0).fullPathHash != idleState))
        {
            animator.CrossFadeInFixedTime(idleState, 0.08f, 0);
        }
    }

    public static bool ShouldPresentLocomotion(float speed, bool wasMoving) => speed > (wasMoving ? .03f : .08f);
    public static float ResolvePlaybackRate(float speed, float localScale, float referenceSpeed) => localScale > .0001f && referenceSpeed > .0001f ? Mathf.Clamp(speed / (localScale * referenceSpeed), 0f, 1.35f) : 0f;
    private void LocomotionResolveReferences()
    {
        LocomotionEnemy ??= GetComponent<EnemyController>();
        LocomotionNavigationAgent = GetComponent<NavMeshAgent>();
        LocomotionAnimationContract ??= GetComponent<CharacterAnimationController>();
        LocomotionPhysicsMotor = GetComponent<EnemyController>();
        LocomotionTimeDomain ??= GetComponent<CombatTimeDomain>();
    }

    private void LocomotionCaptureNavigationDefaults()
    {
        if (LocomotionBaseNavigationCaptured || LocomotionNavigationAgent == null)
            return;
        LocomotionBaseNavigationCaptured = true;
        LocomotionBaseNavigationSpeed = LocomotionNavigationAgent.speed;
        LocomotionBaseNavigationAcceleration = LocomotionNavigationAgent.acceleration;
        LocomotionBaseNavigationAngularSpeed = LocomotionNavigationAgent.angularSpeed;
        LocomotionRequestedNavigationSpeed = LocomotionBaseNavigationSpeed;
    }

    private void LocomotionApplyLocalNavigationScale()
    {
        if (LocomotionNavigationAgent == null)
            return;
        LocomotionCaptureNavigationDefaults();
        float scale = LocomotionTimeDomain != null ? LocomotionTimeDomain.Scale : 1f;
        LocomotionNavigationAgent.speed = Mathf.Max(0.01f, LocomotionRequestedNavigationSpeed) * scale;
        LocomotionNavigationAgent.acceleration = LocomotionBaseNavigationAcceleration * scale;
        LocomotionNavigationAgent.angularSpeed = LocomotionBaseNavigationAngularSpeed * scale;
    }

    private void LocomotionSetMovementPace(float distanceToTarget)
    {
        if (LocomotionNavigationAgent == null)
            return;
        LocomotionCaptureNavigationDefaults();
        bool nextRunPhase = LocomotionRunPhase ? distanceToTarget > Mathf.Min(LocomotionPositioning.walkResumeDistance, LocomotionPositioning.runDistance) : distanceToTarget >= Mathf.Max(0.1f, LocomotionPositioning.runDistance);
        float nextSpeed = nextRunPhase ? Mathf.Max(LocomotionPositioning.walkSpeed, LocomotionPositioning.runSpeed) : Mathf.Max(0.1f, LocomotionPositioning.walkSpeed);
        if (LocomotionRunPhase == nextRunPhase && Mathf.Abs(LocomotionRequestedNavigationSpeed - nextSpeed) < 0.01f)
        {
            return;
        }

        LocomotionRunPhase = nextRunPhase;
        LocomotionRequestedNavigationSpeed = nextSpeed;
        if (LocomotionLogDiagnostics)
        {
            Debug.Log("[CombatEnemyLocomotion] " + name + " phase=" + (LocomotionRunPhase ? "Run" : "Walk") + " | distance=" + distanceToTarget.ToString("F2") + " | speed=" + LocomotionRequestedNavigationSpeed.ToString("F2"), this);
        }
    }

    private Vector3 LocomotionResolveClearDestination(Vector3 requestedDestination)
    {
        if (LocomotionNavigationAgent == null || LocomotionNavMeshEdgeClearance <= 0f || !NavMesh.SamplePosition(requestedDestination, out NavMeshHit requestedHit, LocomotionClearanceSearchRadius, LocomotionNavigationAgent.areaMask))
        {
            return requestedDestination;
        }

        Vector3 bestPosition = requestedHit.position;
        float bestClearance = LocomotionGetNavMeshEdgeClearance(bestPosition);
        if (bestClearance >= LocomotionNavMeshEdgeClearance)
        {
            return bestPosition;
        }

        Vector3 bestSafePosition = bestPosition;
        float bestSafeScore = float.PositiveInfinity;
        const int ringCount = 3;
        int samplesPerRing = Mathf.CeilToInt(LocomotionClearanceSearchSamples / (float)ringCount);
        for (int ring = 1; ring <= ringCount; ring++)
        {
            float radius = LocomotionClearanceSearchRadius * ring / ringCount;
            for (int index = 0; index < samplesPerRing; index++)
            {
                float angle = index * Mathf.PI * 2f / samplesPerRing;
                Vector3 probe = requestedHit.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                if (!NavMesh.SamplePosition(probe, out NavMeshHit candidateHit, 0.35f, LocomotionNavigationAgent.areaMask))
                {
                    continue;
                }

                float candidateClearance = LocomotionGetNavMeshEdgeClearance(candidateHit.position);
                if (candidateClearance > bestClearance)
                {
                    bestClearance = candidateClearance;
                    bestPosition = candidateHit.position;
                }

                if (candidateClearance < LocomotionNavMeshEdgeClearance)
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

    private Vector3 LocomotionFindNearbyProjectedDestination(Vector3 requestedDestination, int areaMask)
    {
        Vector3 best = transform.position;
        float bestScore = float.PositiveInfinity;
        int samples = Mathf.Max(8, LocomotionClearanceSearchSamples);
        for (int ring = 1; ring <= 3; ring++)
        {
            float radius = Mathf.Max(0.5f, LocomotionClearanceSearchRadius) * ring / 3f;
            for (int index = 0; index < samples; index++)
            {
                float angle = index * Mathf.PI * 2f / samples;
                Vector3 probe = requestedDestination + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                if (!NavMesh.SamplePosition(probe, out NavMeshHit hit, 0.5f, areaMask) || (hit.position - transform.position).sqrMagnitude <= 0.04f)
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

    private float LocomotionGetNavMeshEdgeClearance(Vector3 position)
    {
        return NavMesh.FindClosestEdge(position, out NavMeshHit edgeHit, LocomotionNavigationAgent.areaMask) ? edgeHit.distance : float.PositiveInfinity;
    }

    private static bool LocomotionHasCombatLocomotionParameters(Animator animator)
    {
        bool x = false;
        bool z = false;
        bool speed = false;
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            x |= parameter.nameHash == LocomotionCombatMoveX && parameter.type == AnimatorControllerParameterType.Float;
            z |= parameter.nameHash == LocomotionCombatMoveZ && parameter.type == AnimatorControllerParameterType.Float;
            speed |= parameter.nameHash == LocomotionCombatMoveSpeed && parameter.type == AnimatorControllerParameterType.Float;
        }

        return x && z && speed;
    }
}