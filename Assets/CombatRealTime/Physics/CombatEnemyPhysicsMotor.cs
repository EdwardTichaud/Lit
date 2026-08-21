using System;
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

    [SerializeField] private RealTimeCombatEnemy enemy;
    [SerializeField] private NavMeshAgent navigationAgent;
    [SerializeField] private Rigidbody body;
    [SerializeField] private CapsuleCollider bodyCollider;
    [Header("Grounding")]
    [SerializeField, Min(0.01f)] private float groundSkin = 0.03f;
    [SerializeField, Min(0.05f)] private float groundProbeStartHeight = 0.35f;
    [SerializeField, Min(0.1f)] private float groundProbeDistance = 4f;
    [SerializeField, Range(0f, 1f)] private float minimumGroundNormal = 0.45f;
    [SerializeField] private LayerMask groundMask = ~0;
    [Header("Diagnostics")]
    [SerializeField] private bool logStateChanges;

    private readonly RaycastHit[] groundHits = new RaycastHit[GroundHitCapacity];
    private CombatActorAnimationRoot animationContract;
    private Vector3 pendingPlanarRootMotion;
    private Quaternion pendingRootRotation = Quaternion.identity;
    private EnemyActionMotionProfile activeMotionProfile;
    private Action pendingCompletion;
    private float verticalVelocity;
    private float airborneStartedAt;
    private bool landingRequested;
    private bool navigationSuppressed;

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
    }

    private void OnDisable()
    {
        pendingCompletion = null;
        pendingPlanarRootMotion = Vector3.zero;
        pendingRootRotation = Quaternion.identity;
    }

    public void BeginEnemyAction(SkillSO skill)
    {
        ResolveReferences();
        activeMotionProfile = skill != null ? skill.EnemyActionMotion : EnemyActionMotionProfile.GroundedDefault;
        pendingCompletion = null;
        pendingPlanarRootMotion = Vector3.zero;
        pendingRootRotation = Quaternion.identity;
        verticalVelocity = 0f;
        landingRequested = false;
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
        SetState(CombatEnemyPhysicsState.AirborneAction, "debut aerien");
    }

    public void RequestEnemyLanding()
    {
        if (!IsAirborne)
        {
            return;
        }

        landingRequested = true;
        verticalVelocity = Mathf.Min(verticalVelocity, -Mathf.Max(0.1f, activeMotionProfile.minimumLandingSpeed));
        SetState(CombatEnemyPhysicsState.Recovering, "atterrissage demande");
    }

    public void CompleteEnemyAction(Action completion)
    {
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
        SuppressNavigation();
        SetState(CombatEnemyPhysicsState.Cinematic, "cinematique");
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
    }

    public void ApplyActionRootMotion(Vector3 worldDeltaPosition, Quaternion deltaRotation)
    {
        if (!IsDrivingActionRootMotion)
        {
            return;
        }

        pendingPlanarRootMotion += Vector3.ProjectOnPlane(worldDeltaPosition, Vector3.up);
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
        Quaternion rotation = pendingRootRotation * body.rotation;
        pendingRootRotation = Quaternion.identity;

        if (State == CombatEnemyPhysicsState.GroundedAction)
        {
            if (TryGetGroundY(position, out float groundY))
            {
                position.y = groundY;
            }

            MoveBody(position + planarDelta, rotation);
            return;
        }

        float gravity = activeMotionProfile != null ? activeMotionProfile.gravity : 32f;
        verticalVelocity = Mathf.Max(
            verticalVelocity - gravity * Time.fixedDeltaTime,
            -(activeMotionProfile != null ? activeMotionProfile.maximumFallSpeed : 28f));

        Vector3 nextPosition = position + planarDelta + Vector3.up * (verticalVelocity * Time.fixedDeltaTime);
        bool forceLanding = landingRequested ||
            (activeMotionProfile != null && Time.time - airborneStartedAt >= activeMotionProfile.maximumAirborneSeconds);
        if (TryGetGroundY(nextPosition, out float nextGroundY) &&
            nextPosition.y <= nextGroundY + groundSkin && verticalVelocity <= 0f)
        {
            nextPosition.y = nextGroundY;
            MoveBody(nextPosition, rotation);
            FinishRecovery();
            return;
        }

        if (forceLanding && TryGetGroundY(nextPosition, out nextGroundY))
        {
            nextPosition.y = Mathf.Max(nextGroundY, nextPosition.y);
        }

        MoveBody(nextPosition, rotation);
    }

    private void FinishRecovery()
    {
        Action completion = pendingCompletion;
        pendingCompletion = null;
        verticalVelocity = 0f;
        landingRequested = false;
        activeMotionProfile = null;
        ResumeNavigation();
        SetState(CombatEnemyPhysicsState.Navigation, "sol confirme");
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

        if (navigationAgent.isOnNavMesh)
        {
            navigationAgent.Warp(transform.position);
            navigationAgent.nextPosition = transform.position;
        }

        navigationAgent.updatePosition = true;
    }

    private void MoveBody(Vector3 position, Quaternion rotation)
    {
        body.MovePosition(position);
        body.MoveRotation(rotation);
        Physics.SyncTransforms();
    }

    private void SnapToGroundIfAvailable()
    {
        Vector3 position = body != null ? body.position : transform.position;
        if (!TryGetGroundY(position, out float groundY))
        {
            return;
        }

        position.y = groundY;
        if (body != null)
        {
            body.position = position;
        }
        else
        {
            transform.position = position;
        }

        Physics.SyncTransforms();
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
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = groundHits[i];
            if (hit.collider == null || hit.normal.y < minimumGroundNormal || IsOwnCollider(hit.collider))
            {
                continue;
            }

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                groundY = hit.point.y - bottomLocalY + groundSkin;
            }
        }

        return closestDistance < float.PositiveInfinity;
    }

    private bool IsOwnCollider(Collider collider)
    {
        Transform candidate = collider.transform;
        return candidate == transform || candidate.IsChildOf(transform);
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
