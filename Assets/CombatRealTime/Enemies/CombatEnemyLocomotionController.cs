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
        [Min(0f)] public float walkSpeedThreshold = 1.2f;
        [Min(0f)] public float runSpeedThreshold = 3.4f;
    }

    private static readonly int CombatMoveX = Animator.StringToHash("CombatMoveX");
    private static readonly int CombatMoveZ = Animator.StringToHash("CombatMoveZ");
    private static readonly int CombatMoveSpeed = Animator.StringToHash("CombatMoveSpeed");
    // Animator.HasState/CrossFade with an int expects the full state path.
    // A short-name hash silently prevented the enemy combat blend tree from
    // ever taking over, leaving NavMesh movement visually in Idle.
    private static readonly int CombatLocomotion = Animator.StringToHash("Base Layer.CombatLocomotion");
    private static readonly int Idle = Animator.StringToHash("Idle");

    [SerializeField] private RealTimeCombatEnemy enemy;
    [SerializeField] private NavMeshAgent navigationAgent;
    [SerializeField] private CombatActorAnimationRoot animationContract;
    [SerializeField] private CombatEnemyPhysicsMotor physicsMotor;
    [SerializeField] private CombatPositioningProfile positioning = new CombatPositioningProfile();
    [SerializeField, Min(0f)] private float facingSpeedDegreesPerSecond = 540f;
    [SerializeField, Min(0.01f)] private float animatorDampTime = 0.08f;
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
        strafeSide = Random.value < 0.5f ? -1f : 1f;
        nextSideChangeAt = Time.time + positioning.strafeSideHoldSeconds;
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
        UpdateAnimatorPresentation();
    }

    private void LateUpdate()
    {
        // Le root motion peut ecrire la rotation apres l'IA. On conserve donc
        // l'ennemi face a sa cible a la fin de l'image, sauf si une Timeline
        // cinematographique est explicitement proprietaire de sa pose.
        if (combatTarget != null && (animationContract == null || !animationContract.IsCinematicMotionActive))
        {
            FaceTarget(combatTarget.position);
        }
    }

    public void SetCombatTarget(Transform target)
    {
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
            destination = combatTarget.position - away * Mathf.Max(0f, stoppingDistance - positioning.strafeRadius * 0.35f);
        }
        else if (distance < positioning.minimumRetreatDistance)
        {
            destination = combatTarget.position + away * Mathf.Max(positioning.minimumRetreatDistance, stoppingDistance);
        }
        else
        {
            if (Time.time >= nextSideChangeAt)
            {
                strafeSide *= -1f;
                nextSideChangeAt = Time.time + Mathf.Max(0.1f, positioning.strafeSideHoldSeconds);
            }

            Vector3 side = Vector3.Cross(Vector3.up, away) * strafeSide;
            destination = combatTarget.position + away * stoppingDistance + side * positioning.strafeRadius;
        }

        NavigateTo(destination, stoppingDistance);
        FaceTarget(combatTarget.position);
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

        destination = ResolveClearDestination(destination);
        navigationAgent.isStopped = false;
        navigationAgent.stoppingDistance = Mathf.Max(0f, stoppingDistance);
        navigationAgent.SetDestination(destination);
        navigationRequested = true;
        if (logDiagnostics && !wasNavigating)
        {
            Debug.Log("[CombatEnemyLocomotion] " + name + " navigation active | stop=" + navigationAgent.stoppingDistance.ToString("F2"), this);
        }
        wasNavigating = true;
    }

    public void StopNavigation()
    {
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
    }

    public void FaceTarget(Vector3 worldPosition)
    {
        Vector3 direction = worldPosition - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        transform.rotation = Quaternion.RotateTowards(transform.rotation,
            Quaternion.LookRotation(direction.normalized, Vector3.up), facingSpeedDegreesPerSecond * Time.deltaTime);
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
            hasAnimatorParameters = HasCombatLocomotionParameters(animator);
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
        bool actionOwnsAnimation = physicsMotor != null && physicsMotor.IsDrivingActionRootMotion;
        Vector3 velocity = navigationAgent != null && navigationAgent.isActiveAndEnabled && navigationAgent.isOnNavMesh && !navigationAgent.isStopped
            ? navigationAgent.velocity
            : Vector3.zero;
        velocity.y = 0f;
        float speed = velocity.magnitude;

        if (actionOwnsAnimation)
        {
            animator.SetFloat(CombatMoveX, 0f, animatorDampTime, Time.deltaTime);
            animator.SetFloat(CombatMoveZ, 0f, animatorDampTime, Time.deltaTime);
            animator.SetFloat(CombatMoveSpeed, 0f, animatorDampTime, Time.deltaTime);
            return;
        }

        if (speed > 0.03f)
        {
            Vector3 localVelocity = transform.InverseTransformDirection(velocity / speed);
            animator.SetFloat(CombatMoveX, Mathf.Clamp(localVelocity.x, -1f, 1f), animatorDampTime, Time.deltaTime);
            animator.SetFloat(CombatMoveZ, Mathf.Clamp(localVelocity.z, -1f, 1f), animatorDampTime, Time.deltaTime);
            animator.SetFloat(CombatMoveSpeed, speed, animatorDampTime, Time.deltaTime);
            if (animator.HasState(0, CombatLocomotion))
            {
                AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
                if (state.fullPathHash != CombatLocomotion)
                {
                    animator.CrossFade(CombatLocomotion, 0.08f, 0);
                }
            }
        }
        else
        {
            animator.SetFloat(CombatMoveX, 0f, animatorDampTime, Time.deltaTime);
            animator.SetFloat(CombatMoveZ, 0f, animatorDampTime, Time.deltaTime);
            animator.SetFloat(CombatMoveSpeed, 0f, animatorDampTime, Time.deltaTime);
        }
    }

    private void ResolveReferences()
    {
        enemy ??= GetComponent<RealTimeCombatEnemy>();
        navigationAgent ??= GetComponent<NavMeshAgent>();
        animationContract ??= GetComponent<CombatActorAnimationRoot>();
        physicsMotor ??= GetComponent<CombatEnemyPhysicsMotor>();
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
