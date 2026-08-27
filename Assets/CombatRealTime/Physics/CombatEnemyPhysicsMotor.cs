using System;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.AI;

public enum CombatEnemyPhysicsState
{
    Navigation,
    GroundedAction,
    AirborneAction,
    Recovering,
    Cinematic
}

/// <summary>
/// Unique owner of an enemy ActorRoot's vertical position during combat actions.
/// Navigation owns planar movement outside actions; root motion only contributes
/// planar deltas while this component owns an action.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public sealed class CombatEnemyPhysicsMotor : MonoBehaviour
{
    private const int GroundHitCapacity = 16;
    private const int ObstacleHitCapacity = 16;

    [SerializeField] private RealTimeCombatEnemy enemy;
    [SerializeField] private NavMeshAgent navigationAgent;
    [SerializeField] private Rigidbody body;
    [SerializeField] private CapsuleCollider bodyCollider;
    [Header("Grounding")]
    [SerializeField, Min(0.01f)] private float groundSkin = 0.03f;
    [SerializeField, Min(0.05f)] private float groundProbeStartHeight = 0.35f;
    [SerializeField, Min(0.1f)] private float groundProbeDistance = 4f;
    [SerializeField, Min(0.05f), Tooltip("Correction verticale maximale autorisee par une sonde de sol. Evite de raccrocher un acteur a un autre niveau du decor.")]
    private float maximumGroundSnapDistance = 0.75f;
    [SerializeField, Min(0.05f), Tooltip("Delai maximal apres une demande d'atterrissage avant le filet de securite vertical.")]
    private float emergencyLandingDelay = 0.45f;
    [SerializeField, Range(0f, 1f)] private float minimumGroundNormal = 0.45f;
    [SerializeField] private LayerMask groundMask = ~0;
    [Header("Diagnostics")]
    [SerializeField] private bool logStateChanges;
    [SerializeField, Tooltip("Trace les ecritures de pose afin d'identifier un systeme qui deplace l'ennemi hors de son SceneMarker.")]
    private bool logPoseAudit = true;
    [SerializeField, Min(0.5f)] private float poseJumpDiagnosticDistance = 0.5f;
    [SerializeField, Min(0.01f), Tooltip("Distance horizontale maximale acceptee depuis un unique delta de root motion ennemi.")]
    private float maximumRootMotionDeltaPerFrame = 0.5f;
    [SerializeField, Min(0.01f), Tooltip("Distance horizontale maximale appliquee par tick physique depuis le root motion accumule.")]
    private float maximumRootMotionDistancePerFixedUpdate = 0.75f;
    [SerializeField, Min(0.01f), Tooltip("Distance a partir de laquelle un repositionnement explicite d'action est journalise.")]
    private float actionRepositionDiagnosticDistance = 1f;

    private readonly RaycastHit[] groundHits = new RaycastHit[GroundHitCapacity];
    private readonly RaycastHit[] obstacleHits = new RaycastHit[ObstacleHitCapacity];
    private CombatActorAnimationRoot animationContract;
    private Vector3 pendingPlanarRootMotion;
    private Quaternion pendingRootRotation = Quaternion.identity;
    private EnemyActionMotionProfile activeMotionProfile;
    private Action pendingCompletion;
    private float verticalVelocity;
    private float airborneStartedAt;
    private float landingRequestedAt;
    private float lastConfirmedGroundY;
    private bool hasLastConfirmedGroundY;
    private bool landingRequested;
    private bool rushActive;
    private Transform rushTarget;
    private float rushSpeed;
    private bool navigationSuppressed;
    private bool hasObservedPose;
    private Vector3 lastObservedPosition;
    private string lastPosePhase = "initialisation";
    private Collider lastLoggedGroundCollider;
    private Collider lastRejectedGroundCollider;
    private bool hasObservedNetworkState;
    private bool lastNetworkTransformEnabled;
    private bool lastNetworkTransformLocalSpace;
    private Transform lastObservedParent;

    public CombatEnemyPhysicsState State { get; private set; } = CombatEnemyPhysicsState.Navigation;
    public bool IsDrivingActionRootMotion => State == CombatEnemyPhysicsState.GroundedAction ||
                                               State == CombatEnemyPhysicsState.AirborneAction ||
                                               State == CombatEnemyPhysicsState.Recovering;
    public bool IsAirborne => State == CombatEnemyPhysicsState.AirborneAction || State == CombatEnemyPhysicsState.Recovering;

    private void Reset()
    {
        enemy = GetComponent<RealTimeCombatEnemy>();
        navigationAgent = GetComponent<NavMeshAgent>();
        body = GetComponent<Rigidbody>();
        bodyCollider = GetComponent<CapsuleCollider>();
    }

    private void Awake()
    {
        ResolveReferences();
        ConfigureBody();
        AuditPose("Awake");
    }

    private void LateUpdate()
    {
        if (!logPoseAudit)
        {
            return;
        }

        Vector3 currentPosition = transform.position;
        if (hasObservedPose && Vector3.Distance(currentPosition, lastObservedPosition) >= poseJumpDiagnosticDistance)
        {
            Debug.LogWarning(
                "[CombatEnemyPoseAudit] Saut de pose sur '" + name + "' apres " + lastPosePhase +
                " | precedent=" + lastObservedPosition + " | actuel=" + currentPosition + ".",
                this);
            AuditPose("saut detecte");
        }

        lastObservedPosition = currentPosition;
        hasObservedPose = true;

        NetworkTransform networkTransform = GetComponent<NetworkTransform>();
        bool networkTransformEnabled = networkTransform != null && networkTransform.enabled;
        bool networkTransformLocalSpace = networkTransform != null && networkTransform.InLocalSpace;
        if (hasObservedNetworkState &&
            (networkTransformEnabled != lastNetworkTransformEnabled ||
             networkTransformLocalSpace != lastNetworkTransformLocalSpace ||
             transform.parent != lastObservedParent))
        {
            AuditPose("changement parent ou NetworkTransform");
        }

        lastNetworkTransformEnabled = networkTransformEnabled;
        lastNetworkTransformLocalSpace = networkTransformLocalSpace;
        lastObservedParent = transform.parent;
        hasObservedNetworkState = true;
    }

    private void OnDisable()
    {
        pendingCompletion = null;
        pendingPlanarRootMotion = Vector3.zero;
        pendingRootRotation = Quaternion.identity;
    }

    public void BeginEnemyAction(SkillSO skill)
    {
        AuditPose("attaque:debut");
        ResolveReferences();
        activeMotionProfile = skill != null ? skill.EnemyActionMotion : EnemyActionMotionProfile.GroundedDefault;
        pendingCompletion = null;
        pendingPlanarRootMotion = Vector3.zero;
        pendingRootRotation = Quaternion.identity;
        verticalVelocity = 0f;
        landingRequested = false;
        landingRequestedAt = -1f;
        rushActive = false;
        rushTarget = null;
        rushSpeed = 0f;
        lastConfirmedGroundY = body != null ? body.position.y : transform.position.y;
        hasLastConfirmedGroundY = true;
        SuppressNavigation();
        animationContract?.EnableRootMotionRelay();
        SetState(CombatEnemyPhysicsState.GroundedAction, "attaque " + (skill != null ? skill.SkillName : "inconnue"));
        SnapToGroundIfAvailable();
    }

    public void BeginEnemyAirborne()
    {
        if (State != CombatEnemyPhysicsState.GroundedAction || activeMotionProfile == null || !activeMotionProfile.IsAirborne)
        {
            return;
        }

        verticalVelocity = activeMotionProfile.initialUpwardSpeed;
        airborneStartedAt = Time.time;
        landingRequested = false;
        landingRequestedAt = -1f;
        SetState(CombatEnemyPhysicsState.AirborneAction, "debut aerien");
    }

    public void RequestEnemyLanding()
    {
        if (!IsAirborne)
        {
            return;
        }

        landingRequested = true;
        landingRequestedAt = Time.time;
        verticalVelocity = Mathf.Min(verticalVelocity, -Mathf.Max(0.1f, activeMotionProfile.minimumLandingSpeed));
        SetState(CombatEnemyPhysicsState.Recovering, "atterrissage demande");
    }

    /// <summary>Starts an authored homing rush. It owns only planar motion while the motor keeps vertical physics authoritative.</summary>
    public void BeginEnemyRush(Transform target)
    {
        if (!IsAirborne || activeMotionProfile == null || !activeMotionProfile.HasHomingRush || target == null)
        {
            return;
        }

        rushActive = true;
        rushTarget = target;
        rushSpeed = 0f;
        AuditPose("ruée:debut");
    }

    public void EndEnemyRush()
    {
        if (!rushActive)
        {
            return;
        }

        rushActive = false;
        rushTarget = null;
        rushSpeed = 0f;
        AuditPose("ruée:fin");
    }

    public void CompleteEnemyAction(Action completion)
    {
        AuditPose("attaque:fin demandee");
        pendingCompletion = completion;
        if (State == CombatEnemyPhysicsState.GroundedAction || State == CombatEnemyPhysicsState.Navigation)
        {
            SnapToGroundIfAvailable();
            FinishRecovery();
            return;
        }

        RequestEnemyLanding();
    }

    public void InterruptEnemyAction(Action completion)
    {
        AuditPose("attaque:interrompue");
        pendingCompletion = completion;
        if (IsAirborne)
        {
            RequestEnemyLanding();
            return;
        }

        SnapToGroundIfAvailable();
        FinishRecovery();
    }

    public void EnterCinematic()
    {
        pendingCompletion = null;
        pendingPlanarRootMotion = Vector3.zero;
        pendingRootRotation = Quaternion.identity;
        EndEnemyRush();
        SuppressNavigation();
        SetState(CombatEnemyPhysicsState.Cinematic, "cinematique");
        AuditPose("cinematique:debut");
    }

    public void ExitCinematic()
    {
        if (State != CombatEnemyPhysicsState.Cinematic)
        {
            return;
        }

        SnapToGroundIfAvailable();
        ResumeNavigation();
        SetState(CombatEnemyPhysicsState.Navigation, "fin cinematique");
        AuditPose("cinematique:fin");
    }

    public void ApplyActionRootMotion(Vector3 worldDeltaPosition, Quaternion deltaRotation)
    {
        if (!IsDrivingActionRootMotion)
        {
            return;
        }

        // A homing rush is the only planar action owner during that phase. The
        // clip may still contribute visual rotation, never translation.
        Vector3 planarDelta = rushActive ? Vector3.zero : Vector3.ProjectOnPlane(worldDeltaPosition, Vector3.up);
        float maximumDistance = Mathf.Max(0.01f, maximumRootMotionDeltaPerFrame);
        if (planarDelta.sqrMagnitude > maximumDistance * maximumDistance)
        {
            Debug.LogWarning(
                "[CombatEnemyPhysicsMotor] Delta root motion borne sur '" + name + "' : " + planarDelta +
                " | state=" + State + ".",
                this);
            planarDelta = planarDelta.normalized * maximumDistance;
        }

        pendingPlanarRootMotion += planarDelta;
        pendingRootRotation = deltaRotation * pendingRootRotation;
    }

    /// <summary>
    /// Repositions an enemy on its current ground plane during an authored action.
    /// This is intended for Animation Events and deliberately leaves vertical motion
    /// under this motor's control.
    /// </summary>
    public void SetActionPlanarPosition(Vector3 position)
    {
        ResolveReferences();

        Vector3 currentPosition = body != null ? body.position : transform.position;
        Vector3 planarOffset = Vector3.ProjectOnPlane(position - currentPosition, Vector3.up);
        if (planarOffset.sqrMagnitude >= actionRepositionDiagnosticDistance * actionRepositionDiagnosticDistance)
        {
            Debug.LogWarning(
                "[CombatEnemyPhysicsMotor] Repositionnement explicite d'action sur '" + name +
                "' : " + planarOffset + " | state=" + State + ".",
                this);
        }

        currentPosition.x = position.x;
        currentPosition.z = position.z;
        pendingPlanarRootMotion = Vector3.zero;

        if (body != null)
        {
            body.position = currentPosition;
        }
        else
        {
            transform.position = currentPosition;
        }

        if (navigationAgent != null && navigationAgent.isActiveAndEnabled && navigationAgent.isOnNavMesh)
        {
            navigationAgent.nextPosition = currentPosition;
        }

        Physics.SyncTransforms();
    }

    private void FixedUpdate()
    {
        if (!IsDrivingActionRootMotion || body == null)
        {
            return;
        }

        Vector3 position = body.position;
        Vector3 planarDelta = pendingPlanarRootMotion;
        pendingPlanarRootMotion = Vector3.zero;
        float maximumFixedDistance = Mathf.Max(0.01f, maximumRootMotionDistancePerFixedUpdate);
        if (planarDelta.sqrMagnitude > maximumFixedDistance * maximumFixedDistance)
        {
            Debug.LogWarning(
                "[CombatEnemyPhysicsMotor] Root motion accumule borne sur '" + name + "' : " +
                planarDelta + " | state=" + State + ".",
                this);
            planarDelta = planarDelta.normalized * maximumFixedDistance;
        }

        Quaternion rotation = pendingRootRotation * body.rotation;
        pendingRootRotation = Quaternion.identity;

        if (State == CombatEnemyPhysicsState.GroundedAction)
        {
            if (TryGetGroundY(position, out float groundY))
            {
                position.y = groundY;
                RememberGroundY(groundY);
            }

            planarDelta = ClampPlanarMotionToObstacles(position, planarDelta);
            MoveBody(position + planarDelta, rotation);
            return;
        }

        float gravity = activeMotionProfile != null ? activeMotionProfile.gravity : 32f;
        verticalVelocity = Mathf.Max(
            verticalVelocity - gravity * Time.fixedDeltaTime,
            -(activeMotionProfile != null ? activeMotionProfile.maximumFallSpeed : 28f));

        if (rushActive)
        {
            planarDelta += ResolveRushPlanarDelta(position);
        }

        planarDelta = ClampPlanarMotionToObstacles(position, planarDelta);

        Vector3 nextPosition = position + planarDelta + Vector3.up * (verticalVelocity * Time.fixedDeltaTime);
        bool forceLanding = landingRequested ||
            (activeMotionProfile != null && Time.time - airborneStartedAt >= activeMotionProfile.maximumAirborneSeconds);
        if (TryGetGroundY(nextPosition, out float nextGroundY) &&
            nextPosition.y <= nextGroundY + groundSkin && verticalVelocity <= 0f)
        {
            nextPosition.y = nextGroundY;
            RememberGroundY(nextGroundY);
            MoveBody(nextPosition, rotation);
            FinishRecovery();
            return;
        }

        if (forceLanding && TryGetGroundY(nextPosition, out nextGroundY))
        {
            nextPosition.y = Mathf.Max(nextGroundY, nextPosition.y);
        }

        // A streamed scene can expose an unrelated collider on another world
        // height (the pose audit currently proves this for the Crypt floor).
        // Until a local physical floor is found, the height captured at the
        // beginning of this action is the only safe lower bound. This prevents
        // an authored jump from tunnelling below the visible combat floor while
        // preserving its ascent and all planar rush motion.
        if (hasLastConfirmedGroundY && verticalVelocity <= 0f && nextPosition.y <= lastConfirmedGroundY)
        {
            nextPosition.y = lastConfirmedGroundY;
            MoveBody(nextPosition, rotation);
            if (logPoseAudit)
            {
                Debug.LogWarning("[CombatEnemyPhysicsMotor] Atterrissage sur hauteur de securite pour '" + name +
                                 "' : aucune surface locale valide n'a ete detectee.", this);
            }
            FinishRecovery();
            return;
        }

        // Ground layers can be authored incorrectly in a scene. Never let an
        // interrupted aerial action fall forever because one probe missed: use
        // the last physically confirmed floor height, preserving the current X/Z.
        bool airborneTimedOut = activeMotionProfile != null &&
                            Time.time - airborneStartedAt >= activeMotionProfile.maximumAirborneSeconds;
        bool emergencyLandingDue = (landingRequested && Time.time - landingRequestedAt >= emergencyLandingDelay) ||
                                   airborneTimedOut;
        if (emergencyLandingDue && hasLastConfirmedGroundY)
        {
            nextPosition.y = lastConfirmedGroundY;
            MoveBody(nextPosition, rotation);
            Debug.LogWarning("[CombatEnemyPhysicsMotor] Atterrissage de securite sur '" + name +
                             "' : aucune sonde de sol valide pendant la recuperation.", this);
            FinishRecovery();
            return;
        }

        MoveBody(nextPosition, rotation);
    }

    private void FinishRecovery()
    {
        Action completion = pendingCompletion;
        pendingCompletion = null;
        verticalVelocity = 0f;
        landingRequested = false;
        landingRequestedAt = -1f;
        EndEnemyRush();
        activeMotionProfile = null;
        ResumeNavigation();
        SetState(CombatEnemyPhysicsState.Navigation, "sol confirme");
        AuditPose("recuperation:sol confirme");
        completion?.Invoke();
    }

    private void SuppressNavigation()
    {
        if (navigationAgent == null || !navigationAgent.isActiveAndEnabled)
        {
            return;
        }

        navigationAgent.isStopped = true;
        navigationAgent.ResetPath();
        navigationAgent.updatePosition = false;
        navigationSuppressed = true;
        AuditPose("NavMesh:suspendu");
    }

    private void ResumeNavigation()
    {
        if (!navigationSuppressed || navigationAgent == null)
        {
            return;
        }

        navigationSuppressed = false;
        navigationAgent.updatePosition = true;
        if (!navigationAgent.isActiveAndEnabled)
        {
            return;
        }

        int areaMask = navigationAgent.areaMask == 0 ? NavMesh.AllAreas : navigationAgent.areaMask;
        if (navigationAgent.isOnNavMesh &&
            NavMesh.SamplePosition(transform.position, out NavMeshHit localHit, 0.15f, areaMask) &&
            Vector3.Distance(localHit.position, transform.position) <= 0.15f)
        {
            AuditPose("NavMesh:avant Warp reprise");
            navigationAgent.Warp(transform.position);
            navigationAgent.nextPosition = transform.position;
            AuditPose("NavMesh:apres Warp reprise");
        }
        else
        {
            // Never let a stale NavMesh island pull an actor to another floor
            // after an aerial action. Behaviour will retry only a local attach.
            navigationAgent.enabled = false;
            if (logPoseAudit)
            {
                Debug.LogWarning("[CombatEnemyPhysicsMotor] Reprise NavMesh refusee pour '" + name +
                                 "' : aucune surface locale coherente.", this);
            }
        }

        navigationAgent.updatePosition = true;
    }

    private void MoveBody(Vector3 position, Quaternion rotation)
    {
        body.MovePosition(position);
        body.MoveRotation(rotation);
        Physics.SyncTransforms();
    }

    private Vector3 ResolveRushPlanarDelta(Vector3 position)
    {
        if (rushTarget == null || activeMotionProfile == null)
        {
            EndEnemyRush();
            return Vector3.zero;
        }

        Vector3 targetPosition = rushTarget.position;
        Vector3 direction = targetPosition - position;
        direction.y = 0f;
        float distance = direction.magnitude;
        float stopDistance = Mathf.Max(0f, activeMotionProfile.rushStoppingDistance);
        float deltaTime = Time.fixedDeltaTime;

        if (distance <= stopDistance)
        {
            rushSpeed = Mathf.MoveTowards(rushSpeed, 0f, activeMotionProfile.rushDeceleration * deltaTime);
            if (rushSpeed <= 0.01f)
            {
                EndEnemyRush();
            }
            return Vector3.zero;
        }

        rushSpeed = Mathf.MoveTowards(rushSpeed, activeMotionProfile.rushMaximumSpeed, activeMotionProfile.rushAcceleration * deltaTime);
        float requestedDistance = Mathf.Min(rushSpeed * deltaTime, Mathf.Max(0f, distance - stopDistance));
        if (requestedDistance <= 0.0001f)
        {
            return Vector3.zero;
        }

        Vector3 normalizedDirection = direction / distance;
        if (TryClampPlanarMotionToObstacle(position, normalizedDirection, requestedDistance, out float allowedDistance))
        {
            requestedDistance = allowedDistance;
            rushSpeed = Mathf.MoveTowards(rushSpeed, 0f, activeMotionProfile.rushDeceleration * deltaTime);
            if (logStateChanges)
            {
                Debug.Log("[CombatEnemyPhysicsMotor] " + name + " ruée bloquee par decor.", this);
            }
        }

        return normalizedDirection * Mathf.Max(0f, requestedDistance);
    }

    private Vector3 ClampPlanarMotionToObstacles(Vector3 position, Vector3 planarDelta)
    {
        float requestedDistance = planarDelta.magnitude;
        if (requestedDistance <= 0.0001f)
        {
            return Vector3.zero;
        }

        Vector3 direction = planarDelta / requestedDistance;
        return TryClampPlanarMotionToObstacle(position, direction, requestedDistance, out float allowedDistance)
            ? direction * allowedDistance
            : planarDelta;
    }

    private bool TryClampPlanarMotionToObstacle(Vector3 position, Vector3 direction, float requestedDistance, out float allowedDistance)
    {
        allowedDistance = requestedDistance;
        if (bodyCollider == null || activeMotionProfile == null || requestedDistance <= 0f)
        {
            return false;
        }

        float scale = Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z));
        float radius = Mathf.Max(0.03f, bodyCollider.radius * scale);
        float height = Mathf.Max(radius * 2f, bodyCollider.height * Mathf.Abs(transform.lossyScale.y));
        Vector3 center = position + transform.TransformVector(bodyCollider.center);
        float cylinderHalf = Mathf.Max(0f, height * 0.5f - radius);
        Vector3 first = center + Vector3.up * cylinderHalf;
        Vector3 second = center - Vector3.up * cylinderHalf;
        int hitCount = Physics.CapsuleCastNonAlloc(first, second, radius, direction, obstacleHits,
            requestedDistance + activeMotionProfile.rushCollisionSkin,
            activeMotionProfile.rushBlockingMask, QueryTriggerInteraction.Ignore);
        float closestDistance = float.PositiveInfinity;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = obstacleHits[i];
            if (!IsBlockingEnvironmentCollider(hit.collider))
            {
                continue;
            }

            closestDistance = Mathf.Min(closestDistance, hit.distance);
        }

        if (float.IsPositiveInfinity(closestDistance))
        {
            return false;
        }

        allowedDistance = Mathf.Max(0f, closestDistance - activeMotionProfile.rushCollisionSkin);
        return true;
    }

    private void SnapToGroundIfAvailable()
    {
        Vector3 position = body != null ? body.position : transform.position;
        if (!TryGetGroundY(position, out float groundY))
        {
            return;
        }

        position.y = groundY;
        RememberGroundY(groundY);
        if (body != null)
        {
            body.position = position;
        }
        else
        {
            transform.position = position;
        }

        Physics.SyncTransforms();
        AuditPose("physique:SnapToGround");
    }

    private void RememberGroundY(float groundY)
    {
        lastConfirmedGroundY = groundY;
        hasLastConfirmedGroundY = true;
    }

    private bool TryGetGroundY(Vector3 actorPosition, out float groundY)
    {
        groundY = 0f;
        if (bodyCollider == null)
        {
            return false;
        }

        float scaledRadius = Mathf.Max(0.05f, bodyCollider.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z));
        float bottomLocalY = (bodyCollider.center.y - bodyCollider.height * 0.5f) * transform.lossyScale.y;
        Vector3 origin = actorPosition + Vector3.up * (bottomLocalY + groundProbeStartHeight);
        int hitCount = Physics.SphereCastNonAlloc(
            origin,
            scaledRadius * 0.9f,
            Vector3.down,
            groundHits,
            groundProbeStartHeight + groundProbeDistance,
            groundMask,
            QueryTriggerInteraction.Ignore);

        float closestDistance = float.PositiveInfinity;
        Collider selectedCollider = null;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = groundHits[i];
            if (hit.collider == null || hit.normal.y < minimumGroundNormal || !IsGroundCollider(hit.collider))
            {
                continue;
            }

            float candidateGroundY = hit.point.y - bottomLocalY + groundSkin;
            float verticalCorrection = Mathf.Abs(candidateGroundY - actorPosition.y);
            if (verticalCorrection > maximumGroundSnapDistance)
            {
                if (logPoseAudit && hit.collider != lastRejectedGroundCollider)
                {
                    lastRejectedGroundCollider = hit.collider;
                    Debug.LogWarning(
                        "[CombatEnemyPoseAudit] Sol ignore pour '" + name + "' : '" + hit.collider.name +
                        "' demanderait un decalage vertical de " + verticalCorrection + " m | actorY=" +
                        actorPosition.y + " | solY=" + candidateGroundY + ".",
                        this);
                }
                continue;
            }

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                groundY = candidateGroundY;
                selectedCollider = hit.collider;
            }
        }

        if (selectedCollider != null && logPoseAudit && selectedCollider != lastLoggedGroundCollider)
        {
            lastLoggedGroundCollider = selectedCollider;
            Debug.Log(
                "[CombatEnemyPoseAudit] Sol retenu pour '" + name + "' : '" + selectedCollider.name +
                "' | point=" + (groundY + bottomLocalY - groundSkin) + " | distance=" + closestDistance + ".",
                this);
        }

        return closestDistance < float.PositiveInfinity;
    }

    private bool IsOwnCollider(Collider collider)
    {
        Transform candidate = collider.transform;
        return candidate == transform || candidate.IsChildOf(transform);
    }

    private bool IsGroundCollider(Collider collider)
    {
        if (collider == null || IsOwnCollider(collider))
        {
            return false;
        }

        int layer = collider.gameObject.layer;
        return layer != LayerMask.NameToLayer("Character") &&
               layer != LayerMask.NameToLayer("Player") &&
               layer != LayerMask.NameToLayer("Enemy") &&
               layer != LayerMask.NameToLayer("UI") &&
               layer != LayerMask.NameToLayer("VisualEffect") &&
               layer != LayerMask.NameToLayer("Ignore Raycast");
    }

    private bool IsBlockingEnvironmentCollider(Collider collider)
    {
        return IsGroundCollider(collider) && !collider.isTrigger;
    }

    private void ResolveReferences()
    {
        enemy ??= GetComponent<RealTimeCombatEnemy>();
        navigationAgent ??= GetComponent<NavMeshAgent>();
        body ??= GetComponent<Rigidbody>();
        bodyCollider ??= GetComponent<CapsuleCollider>();
        animationContract ??= GetComponent<CombatActorAnimationRoot>();
    }

    private void ConfigureBody()
    {
        if (body == null || bodyCollider == null)
        {
            Debug.LogError("[CombatEnemyPhysicsMotor] Rigidbody cinematique et CapsuleCollider requis sur '" + name + "'.", this);
            enabled = false;
            return;
        }

        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        bodyCollider.isTrigger = false;
    }

    public void AuditPose(string phase)
    {
        lastPosePhase = string.IsNullOrWhiteSpace(phase) ? "inconnue" : phase;
        if (!logPoseAudit)
        {
            return;
        }

        ResolveReferences();
        NetworkObject networkObject = GetComponent<NetworkObject>();
        NetworkTransform networkTransform = GetComponent<NetworkTransform>();
        Animator animator = animationContract != null ? animationContract.Animator : null;
        string animatorState = animator != null && animator.runtimeAnimatorController != null
            ? animator.GetCurrentAnimatorStateInfo(0).fullPathHash.ToString()
            : "aucun";
        bool navActive = navigationAgent != null && navigationAgent.isActiveAndEnabled;
        bool navOnMesh = navActive && navigationAgent.isOnNavMesh;
        string navState = navigationAgent == null
            ? "absent"
            : "enabled=" + navigationAgent.enabled + ", onNavMesh=" + navOnMesh +
              ", stopped=" + (navOnMesh ? navigationAgent.isStopped.ToString() : "n/a") +
              ", updatePosition=" + navigationAgent.updatePosition +
              ", next=" + (navOnMesh ? navigationAgent.nextPosition.ToString() : "n/a");
        string networkState = networkObject == null
            ? "absent"
            : "spawned=" + networkObject.IsSpawned + ", networkTransform=" +
              (networkTransform != null ? "enabled=" + networkTransform.enabled + ", local=" + networkTransform.InLocalSpace : "absent");

        Debug.Log(
            "[CombatEnemyPoseAudit] " + lastPosePhase + " | actor='" + name + "' | world=" + transform.position +
            " | local=" + transform.localPosition + " | parent=" + (transform.parent != null ? transform.parent.name : "<none>") +
            " | rigidbody=" + (body != null ? body.position.ToString() : "absent") +
            " | nav=" + navState + " | network=" + networkState + " | animator=" + animatorState + ".",
            this);
    }

    private void SetState(CombatEnemyPhysicsState nextState, string reason)
    {
        if (State == nextState)
        {
            return;
        }

        State = nextState;
        if (logStateChanges)
        {
            Debug.Log("[CombatEnemyPhysicsMotor] " + name + " -> " + nextState + " (" + reason + ")", this);
        }
    }
}
