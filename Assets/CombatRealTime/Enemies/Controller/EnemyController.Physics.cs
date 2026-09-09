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

public sealed partial class EnemyController
{
    private const int PhysicsGroundHitCapacity = 16;
    private const int PhysicsObstacleHitCapacity = 16;
    [SerializeField]
    private EnemyController PhysicsEnemy;
    [SerializeField]
    private NavMeshAgent PhysicsNavigationAgent;
    [SerializeField]
    private Rigidbody PhysicsBody;
    [SerializeField]
    private CapsuleCollider PhysicsBodyCollider;
    [SerializeField]
    private CombatTimeDomain PhysicsTimeDomain;
    private float PhysicsGroundSkin { get => Configuration.PhysicsGroundSkin; set => Configuration.PhysicsGroundSkin = value; }
    private float PhysicsGroundProbeStartHeight { get => Configuration.PhysicsGroundProbeStartHeight; set => Configuration.PhysicsGroundProbeStartHeight = value; }
    private float PhysicsGroundProbeDistance { get => Configuration.PhysicsGroundProbeDistance; set => Configuration.PhysicsGroundProbeDistance = value; }
    private float PhysicsMaximumGroundSnapDistance { get => Configuration.PhysicsMaximumGroundSnapDistance; set => Configuration.PhysicsMaximumGroundSnapDistance = value; }
    private float PhysicsEmergencyLandingDelay { get => Configuration.PhysicsEmergencyLandingDelay; set => Configuration.PhysicsEmergencyLandingDelay = value; }
    private float PhysicsMinimumGroundNormal { get => Configuration.PhysicsMinimumGroundNormal; set => Configuration.PhysicsMinimumGroundNormal = value; }
    private LayerMask PhysicsGroundMask { get => Configuration.PhysicsGroundMask; set => Configuration.PhysicsGroundMask = value; }
    private bool PhysicsLogStateChanges { get => Configuration.PhysicsLogStateChanges; set => Configuration.PhysicsLogStateChanges = value; }
    private bool PhysicsLogPoseAudit { get => Configuration.PhysicsLogPoseAudit; set => Configuration.PhysicsLogPoseAudit = value; }
    private float PhysicsPoseJumpDiagnosticDistance { get => Configuration.PhysicsPoseJumpDiagnosticDistance; set => Configuration.PhysicsPoseJumpDiagnosticDistance = value; }
    private float PhysicsMaximumRootMotionDeltaPerFrame { get => Configuration.PhysicsMaximumRootMotionDeltaPerFrame; set => Configuration.PhysicsMaximumRootMotionDeltaPerFrame = value; }
    private float PhysicsMaximumRootMotionDistancePerFixedUpdate { get => Configuration.PhysicsMaximumRootMotionDistancePerFixedUpdate; set => Configuration.PhysicsMaximumRootMotionDistancePerFixedUpdate = value; }
    private float PhysicsActionRepositionDiagnosticDistance { get => Configuration.PhysicsActionRepositionDiagnosticDistance; set => Configuration.PhysicsActionRepositionDiagnosticDistance = value; }
    private readonly RaycastHit[] PhysicsGroundHits = new RaycastHit[PhysicsGroundHitCapacity];
    private readonly RaycastHit[] PhysicsObstacleHits = new RaycastHit[PhysicsObstacleHitCapacity];
    private CharacterAnimationController PhysicsAnimationContract;
    private Vector3 PhysicsPendingPlanarRootMotion;
    private Quaternion PhysicsPendingRootRotation = Quaternion.identity;
    private EnemyActionMotionProfile PhysicsActiveMotionProfile;
    private Action PhysicsPendingCompletion;
    private float PhysicsVerticalVelocity;
    private float PhysicsAirborneStartedAt;
    private float PhysicsLandingRequestedAt;
    private float PhysicsLastConfirmedGroundY;
    private bool PhysicsHasLastConfirmedGroundY;
    private bool PhysicsLandingRequested;
    private bool PhysicsRushActive;
    private Vector3 PhysicsRushDirection;
    private float PhysicsRushRemainingDistance;
    private float PhysicsRushRemainingSeconds;
    private float PhysicsRushSpeed;
    private bool PhysicsNavigationSuppressed;
    private bool PhysicsHasObservedPose;
    private Vector3 PhysicsLastObservedPosition;
    private string PhysicsLastPosePhase = "initialisation";
    private Collider PhysicsLastLoggedGroundCollider;
    private Collider PhysicsLastRejectedGroundCollider;
    private bool PhysicsHasObservedNetworkState;
    private bool PhysicsLastNetworkTransformEnabled;
    private bool PhysicsLastNetworkTransformLocalSpace;
    private Transform PhysicsLastObservedParent;
    public CombatEnemyPhysicsState State { get; private set; } = CombatEnemyPhysicsState.Navigation;
    public enum AnimationMovementMode
    {
        AuthoredRootMotion,
        ScriptedOnly
    }

    private AnimationMovementMode PhysicsAnimationMovementMode { get => Configuration.PhysicsAnimationMovementMode; set => Configuration.PhysicsAnimationMovementMode = value; }
    public bool ScriptedOnly => PhysicsAnimationMovementMode == AnimationMovementMode.ScriptedOnly;
    private bool PhysicsAdvanceActive;
    private float PhysicsAdvanceStartedAt, PhysicsAdvanceProgress;
    private Vector3 PhysicsAdvanceDirection;
    public void BeginEnemyAdvance()
    {
        if (PhysicsAdvanceActive || State != CombatEnemyPhysicsState.GroundedAction || PhysicsEnemy == null || PhysicsEnemy.ActiveSkill == null || PhysicsActiveMotionProfile == null || !PhysicsActiveMotionProfile.enableAdvance)
            return;
        PhysicsAdvanceActive = true;
        PhysicsAdvanceStartedAt = PhysicsLocalTime;
        PhysicsAdvanceProgress = 0f;
        PhysicsAdvanceDirection = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
    }

    public void EndEnemyAdvance() => PhysicsAdvanceActive = false;
    private Vector3 PhysicsAdvanceDelta(Vector3 position)
    {
        if (!PhysicsAdvanceActive || PhysicsActiveMotionProfile == null)
            return Vector3.zero;
        float t = Mathf.Clamp01((PhysicsLocalTime - PhysicsAdvanceStartedAt) / Mathf.Max(.01f, PhysicsActiveMotionProfile.advanceDuration));
        float progress = t >= 1f ? 1f : Mathf.Clamp01(PhysicsActiveMotionProfile.advanceProgress?.Evaluate(t) ?? t);
        progress = Mathf.Max(PhysicsAdvanceProgress, progress);
        Vector3 requested = PhysicsAdvanceDirection * ((progress - PhysicsAdvanceProgress) * PhysicsActiveMotionProfile.advanceDistance);
        PhysicsAdvanceProgress = progress;
        Vector3 allowed = PhysicsClampPlanarMotionToObstacles(position, requested);
        if (t >= 1f || allowed.sqrMagnitude + .000001f < requested.sqrMagnitude)
            EndEnemyAdvance();
        return allowed;
    }

    public bool IsDrivingActionRootMotion => State == CombatEnemyPhysicsState.GroundedAction || State == CombatEnemyPhysicsState.AirborneAction || State == CombatEnemyPhysicsState.Recovering;
    public bool IsAirborne => State == CombatEnemyPhysicsState.AirborneAction || State == CombatEnemyPhysicsState.Recovering;
    public bool IsOperational => enabled && PhysicsBody != null && PhysicsBodyCollider != null && PhysicsBody.gameObject.activeInHierarchy && PhysicsBodyCollider.enabled && !PhysicsBodyCollider.isTrigger;
    private float PhysicsLocalTime => PhysicsTimeDomain != null ? PhysicsTimeDomain.LocalTime : Time.time;
    private float PhysicsLocalFixedDeltaTime => PhysicsTimeDomain != null ? PhysicsTimeDomain.FixedDeltaTime : Time.fixedDeltaTime;


    private void PhysicsAwake()
    {
        PhysicsResolveReferences();
        PhysicsConfigureBody();
        AuditPose("Awake");
    }

    private void PhysicsLateUpdate()
    {
        if (!PhysicsLogPoseAudit)
        {
            return;
        }

        Vector3 currentPosition = transform.position;
        if (PhysicsHasObservedPose && Vector3.Distance(currentPosition, PhysicsLastObservedPosition) >= PhysicsPoseJumpDiagnosticDistance)
        {
            Debug.LogWarning("[CombatEnemyPoseAudit] Saut de pose sur '" + name + "' apres " + PhysicsLastPosePhase + " | precedent=" + PhysicsLastObservedPosition + " | actuel=" + currentPosition + ".", this);
            AuditPose("saut detecte");
        }

        PhysicsLastObservedPosition = currentPosition;
        PhysicsHasObservedPose = true;
        NetworkTransform networkTransform = GetComponent<NetworkTransform>();
        bool networkTransformEnabled = networkTransform != null && networkTransform.enabled;
        bool networkTransformLocalSpace = networkTransform != null && networkTransform.InLocalSpace;
        if (PhysicsHasObservedNetworkState && (networkTransformEnabled != PhysicsLastNetworkTransformEnabled || networkTransformLocalSpace != PhysicsLastNetworkTransformLocalSpace || transform.parent != PhysicsLastObservedParent))
        {
            AuditPose("changement parent ou NetworkTransform");
        }

        PhysicsLastNetworkTransformEnabled = networkTransformEnabled;
        PhysicsLastNetworkTransformLocalSpace = networkTransformLocalSpace;
        PhysicsLastObservedParent = transform.parent;
        PhysicsHasObservedNetworkState = true;
    }

    private void PhysicsOnDisable()
    {
        PhysicsEndEnemyRush();
        EndEnemyAdvance();
        PhysicsPendingCompletion = null;
        PhysicsPendingPlanarRootMotion = Vector3.zero;
        PhysicsPendingRootRotation = Quaternion.identity;
    }

    public void BeginEnemyAction(SkillSO skill)
    {
        EndEnemyAdvance();
        PhysicsResolveReferences();
        if (!IsOperational)
        {
            Debug.LogError("[CombatEnemyPhysicsMotor] Attaque refusee pour '" + name + "' : Rigidbody ou CapsuleCollider absent/inactif.", this);
            return;
        }

        AuditPose("attaque:debut");
        PhysicsActiveMotionProfile = skill != null ? skill.EnemyActionMotion : EnemyActionMotionProfile.GroundedDefault;
        PhysicsPendingCompletion = null;
        PhysicsPendingPlanarRootMotion = Vector3.zero;
        PhysicsPendingRootRotation = Quaternion.identity;
        PhysicsVerticalVelocity = 0f;
        PhysicsLandingRequested = false;
        PhysicsLandingRequestedAt = -1f;
        PhysicsRushActive = false;
        PhysicsRushDirection = Vector3.zero;
        PhysicsRushSpeed = 0f;
        PhysicsLastConfirmedGroundY = PhysicsBody != null ? PhysicsBody.position.y : transform.position.y;
        PhysicsHasLastConfirmedGroundY = true;
        PhysicsSuppressNavigation();
        PhysicsAnimationContract?.EnableRootMotionRelay();
        PhysicsSetState(CombatEnemyPhysicsState.GroundedAction, "attaque " + (skill != null ? skill.SkillName : "inconnue"));
        PhysicsSnapToGroundIfAvailable();
    }

    public void PhysicsBeginEnemyAirborne()
    {
        if (State != CombatEnemyPhysicsState.GroundedAction || PhysicsActiveMotionProfile == null || !PhysicsActiveMotionProfile.IsAirborne)
        {
            return;
        }

        EndEnemyAdvance();
        PhysicsVerticalVelocity = PhysicsActiveMotionProfile.initialUpwardSpeed;
        PhysicsAirborneStartedAt = PhysicsLocalTime;
        PhysicsLandingRequested = false;
        PhysicsLandingRequestedAt = -1f;
        PhysicsSetState(CombatEnemyPhysicsState.AirborneAction, "debut aerien");
    }

    public void PhysicsRequestEnemyLanding()
    {
        if (!IsAirborne)
        {
            return;
        }

        PhysicsLandingRequested = true;
        PhysicsLandingRequestedAt = PhysicsLocalTime;
        if (!PhysicsRushActive)
            PhysicsVerticalVelocity = Mathf.Min(PhysicsVerticalVelocity, -Mathf.Max(0.1f, PhysicsActiveMotionProfile.minimumLandingSpeed));
        PhysicsSetState(CombatEnemyPhysicsState.Recovering, "atterrissage demande");
    }

    /// <summary>Applies a short, fixed-direction 3D impulse with automatic completion.</summary>
    public void PhysicsBeginEnemyRush(Transform target)
    {
        if (PhysicsRushActive || !IsAirborne || PhysicsActiveMotionProfile == null || !PhysicsActiveMotionProfile.HasHomingRush || target == null)
        {
            return;
        }

        Vector3 offset = target.position - (PhysicsBody != null ? PhysicsBody.position : transform.position);
        PhysicsRushRemainingDistance = Mathf.Max(0f, offset.magnitude - PhysicsActiveMotionProfile.rushStoppingDistance);
        if (PhysicsRushRemainingDistance <= 0.0001f)
            return;
        PhysicsRushActive = true;
        PhysicsRushDirection = offset.normalized;
        PhysicsRushRemainingSeconds = Mathf.Max(0.01f, PhysicsActiveMotionProfile.rushImpulseDuration);
        PhysicsRushSpeed = Mathf.Max(0.1f, PhysicsActiveMotionProfile.rushMaximumSpeed);
        PhysicsVerticalVelocity = 0f;
        AuditPose("ruée:debut");
    }

    public void PhysicsEndEnemyRush()
    {
        if (!PhysicsRushActive)
        {
            return;
        }

        PhysicsRushActive = false;
        PhysicsRushDirection = Vector3.zero;
        PhysicsRushRemainingSeconds = 0f;
        PhysicsRushRemainingDistance = 0f;
        PhysicsRushSpeed = 0f;
        PhysicsVerticalVelocity = Mathf.Min(0f, PhysicsVerticalVelocity);
        if (PhysicsLandingRequested)
            PhysicsLandingRequestedAt = PhysicsLocalTime;
        AuditPose("ruée:fin");
    }

    public void CompleteEnemyAction(Action completion)
    {
        PhysicsEndEnemyRush();
        EndEnemyAdvance();
        AuditPose("attaque:fin demandee");
        PhysicsPendingCompletion = completion;
        if (State == CombatEnemyPhysicsState.GroundedAction || State == CombatEnemyPhysicsState.Navigation)
        {
            PhysicsSnapToGroundIfAvailable();
            PhysicsFinishRecovery();
            return;
        }

        PhysicsRequestEnemyLanding();
    }

    public void InterruptEnemyAction(Action completion)
    {
        PhysicsEndEnemyRush();
        EndEnemyAdvance();
        AuditPose("attaque:interrompue");
        PhysicsPendingCompletion = completion;
        if (IsAirborne)
        {
            PhysicsRequestEnemyLanding();
            return;
        }

        PhysicsSnapToGroundIfAvailable();
        PhysicsFinishRecovery();
    }

    /// <summary>
    /// Last-resort completion for an authored attack which missed its final
    /// event. Unlike the normal path this remains safe when a scene was loaded
    /// with a broken motor, so the combat ledger can never stay locked forever.
    /// </summary>
    public void ForceCompleteEnemyAction(Action completion, string reason)
    {
        EndEnemyAdvance();
        PhysicsResolveReferences();
        PhysicsPendingCompletion = completion;
        PhysicsPendingPlanarRootMotion = Vector3.zero;
        PhysicsPendingRootRotation = Quaternion.identity;
        PhysicsEndEnemyRush();
        if (IsAirborne)
        {
            PhysicsRequestEnemyLanding();
            return;
        }

        if (PhysicsBody != null)
        {
            PhysicsSnapToGroundIfAvailable();
        }

        PhysicsFinishRecovery(reason);
    }

    public void EnterCinematic()
    {
        EndEnemyAdvance();
        PhysicsPendingCompletion = null;
        PhysicsPendingPlanarRootMotion = Vector3.zero;
        PhysicsPendingRootRotation = Quaternion.identity;
        PhysicsEndEnemyRush();
        PhysicsSuppressNavigation();
        PhysicsSetState(CombatEnemyPhysicsState.Cinematic, "cinematique");
        AuditPose("cinematique:debut");
    }

    public void ExitCinematic()
    {
        if (State != CombatEnemyPhysicsState.Cinematic)
        {
            return;
        }

        PhysicsSnapToGroundIfAvailable();
        PhysicsResumeNavigation();
        PhysicsSetState(CombatEnemyPhysicsState.Navigation, "fin cinematique");
        AuditPose("cinematique:fin");
    }

    public void ApplyActionRootMotion(Vector3 worldDeltaPosition, Quaternion deltaRotation)
    {
        if (ScriptedOnly)
            return;
        if (!IsDrivingActionRootMotion)
        {
            return;
        }

        // A 3D rush is the sole translation owner during that phase. The
        // clip may still contribute visual rotation, never translation.
        Vector3 planarDelta = PhysicsRushActive || PhysicsActiveMotionProfile != null && PhysicsActiveMotionProfile.IsAirborne ? Vector3.zero : Vector3.ProjectOnPlane(worldDeltaPosition, Vector3.up);
        float maximumDistance = Mathf.Max(0.01f, PhysicsMaximumRootMotionDeltaPerFrame);
        if (planarDelta.sqrMagnitude > maximumDistance * maximumDistance)
        {
            Debug.LogWarning("[CombatEnemyPhysicsMotor] Delta root motion borne sur '" + name + "' : " + planarDelta + " | state=" + State + ".", this);
            planarDelta = planarDelta.normalized * maximumDistance;
        }

        PhysicsPendingPlanarRootMotion += planarDelta;
        if (GetComponent<EnemyController>()?.HasProfile != true)
            PhysicsPendingRootRotation = deltaRotation * PhysicsPendingRootRotation;
    }

    /// <summary>
    /// Repositions an enemy on its current ground plane during an authored action.
    /// This is intended for Animation Events and deliberately leaves vertical motion
    /// under this motor's control.
    /// </summary>
    public void PhysicsSetActionPlanarPosition(Vector3 position)
    {
        PhysicsResolveReferences();
        Vector3 currentPosition = PhysicsBody != null ? PhysicsBody.position : transform.position;
        Vector3 planarOffset = Vector3.ProjectOnPlane(position - currentPosition, Vector3.up);
        if (planarOffset.sqrMagnitude >= PhysicsActionRepositionDiagnosticDistance * PhysicsActionRepositionDiagnosticDistance)
        {
            Debug.LogWarning("[CombatEnemyPhysicsMotor] Repositionnement explicite d'action sur '" + name + "' : " + planarOffset + " | state=" + State + ".", this);
        }

        currentPosition.x = position.x;
        currentPosition.z = position.z;
        PhysicsPendingPlanarRootMotion = Vector3.zero;
        if (PhysicsBody != null)
        {
            PhysicsBody.position = currentPosition;
        }
        else
        {
            transform.position = currentPosition;
        }

        if (PhysicsNavigationAgent != null && PhysicsNavigationAgent.isActiveAndEnabled && PhysicsNavigationAgent.isOnNavMesh)
        {
            PhysicsNavigationAgent.nextPosition = currentPosition;
        }

        Physics.SyncTransforms();
    }

    private void PhysicsFixedUpdate()
    {
        if (!IsDrivingActionRootMotion || PhysicsBody == null)
        {
            return;
        }

        Vector3 position = PhysicsBody.position;
        Vector3 planarDelta = PhysicsPendingPlanarRootMotion;
        PhysicsPendingPlanarRootMotion = Vector3.zero;
        float maximumFixedDistance = Mathf.Max(0.01f, PhysicsMaximumRootMotionDistancePerFixedUpdate);
        if (planarDelta.sqrMagnitude > maximumFixedDistance * maximumFixedDistance)
        {
            Debug.LogWarning("[CombatEnemyPhysicsMotor] Root motion accumule borne sur '" + name + "' : " + planarDelta + " | state=" + State + ".", this);
            planarDelta = planarDelta.normalized * maximumFixedDistance;
        }

        Quaternion rotation = PhysicsPendingRootRotation * PhysicsBody.rotation;
        PhysicsPendingRootRotation = Quaternion.identity;
        if (State == CombatEnemyPhysicsState.GroundedAction)
        {
            planarDelta += PhysicsAdvanceDelta(position);
            if (PhysicsTryGetGroundY(position, out float groundY))
            {
                position.y = groundY;
                PhysicsRememberGroundY(groundY);
            }

            planarDelta = PhysicsClampPlanarMotionToObstacles(position, planarDelta);
            PhysicsMoveBody(position + planarDelta, rotation);
            return;
        }

        bool airborneTimedOut = PhysicsActiveMotionProfile != null && PhysicsLocalTime - PhysicsAirborneStartedAt >= PhysicsActiveMotionProfile.maximumAirborneSeconds;
        if (PhysicsRushActive && airborneTimedOut)
            PhysicsEndEnemyRush();
        Vector3 motionDelta;
        if (PhysicsRushActive)
        {
            motionDelta = PhysicsResolveRushDelta(position);
            PhysicsVerticalVelocity = PhysicsLocalFixedDeltaTime > 0f ? motionDelta.y / PhysicsLocalFixedDeltaTime : 0f;
            if (!PhysicsRushActive)
                PhysicsVerticalVelocity = Mathf.Min(0f, PhysicsVerticalVelocity);
        }
        else
        {
            float gravity = PhysicsActiveMotionProfile != null ? PhysicsActiveMotionProfile.gravity : 32f;
            PhysicsVerticalVelocity = Mathf.Max(PhysicsVerticalVelocity - gravity * PhysicsLocalFixedDeltaTime, -(PhysicsActiveMotionProfile != null ? PhysicsActiveMotionProfile.maximumFallSpeed : 28f));
            planarDelta = PhysicsClampPlanarMotionToObstacles(position, planarDelta);
            motionDelta = planarDelta + Vector3.up * (PhysicsVerticalVelocity * PhysicsLocalFixedDeltaTime);
        }

        Vector3 nextPosition = position + motionDelta;
        bool forceLanding = PhysicsLandingRequested || (PhysicsActiveMotionProfile != null && PhysicsLocalTime - PhysicsAirborneStartedAt >= PhysicsActiveMotionProfile.maximumAirborneSeconds);
        if (PhysicsTryGetGroundY(nextPosition, out float nextGroundY) && nextPosition.y <= nextGroundY + PhysicsGroundSkin && PhysicsVerticalVelocity <= 0f)
        {
            nextPosition.y = nextGroundY;
            PhysicsRememberGroundY(nextGroundY);
            PhysicsMoveBody(nextPosition, rotation);
            PhysicsFinishRecovery();
            return;
        }

        if (forceLanding && PhysicsTryGetGroundY(nextPosition, out nextGroundY))
        {
            nextPosition.y = Mathf.Max(nextGroundY, nextPosition.y);
        }

        // A streamed scene can expose an unrelated collider on another world
        // height (the pose audit currently proves this for the Crypt floor).
        // Until a local physical floor is found, the height captured at the
        // beginning of this action is the only safe lower bound. This prevents
        // an authored jump from tunnelling below the visible combat floor while
        // preserving its ascent and all planar rush motion.
        if (PhysicsHasLastConfirmedGroundY && PhysicsVerticalVelocity <= 0f && nextPosition.y <= PhysicsLastConfirmedGroundY)
        {
            nextPosition.y = PhysicsLastConfirmedGroundY;
            PhysicsMoveBody(nextPosition, rotation);
            if (PhysicsLogPoseAudit)
            {
                Debug.LogWarning("[CombatEnemyPhysicsMotor] Atterrissage sur hauteur de securite pour '" + name + "' : aucune surface locale valide n'a ete detectee.", this);
            }

            PhysicsFinishRecovery();
            return;
        }

        // Ground layers can be authored incorrectly in a scene. Never let an
        // interrupted aerial action fall forever because one probe missed: use
        // the last physically confirmed floor height, preserving the current X/Z.
        bool emergencyLandingDue = (!PhysicsRushActive && PhysicsLandingRequested && PhysicsLocalTime - PhysicsLandingRequestedAt >= PhysicsEmergencyLandingDelay) || airborneTimedOut;
        if (emergencyLandingDue && PhysicsHasLastConfirmedGroundY)
        {
            nextPosition.y = PhysicsLastConfirmedGroundY;
            PhysicsMoveBody(nextPosition, rotation);
            Debug.LogWarning("[CombatEnemyPhysicsMotor] Atterrissage de securite sur '" + name + "' : aucune sonde de sol valide pendant la recuperation.", this);
            PhysicsFinishRecovery();
            return;
        }

        PhysicsMoveBody(nextPosition, rotation);
    }

    private void PhysicsFinishRecovery(string reason = "sol confirme")
    {
        Action completion = PhysicsPendingCompletion;
        PhysicsPendingCompletion = null;
        PhysicsVerticalVelocity = 0f;
        PhysicsLandingRequested = false;
        PhysicsLandingRequestedAt = -1f;
        PhysicsEndEnemyRush();
        if (completion == null && GetComponent<EnemyController>()?.ActiveSkill != null)
        {
            PhysicsSetState(CombatEnemyPhysicsState.GroundedAction, "atterri, attente fin du clip");
            return;
        }

        PhysicsActiveMotionProfile = null;
        PhysicsResumeNavigation();
        PhysicsSetState(CombatEnemyPhysicsState.Navigation, reason);
        AuditPose("recuperation:" + reason);
        completion?.Invoke();
    }

    private void PhysicsSuppressNavigation()
    {
        if (PhysicsNavigationAgent == null || !PhysicsNavigationAgent.isActiveAndEnabled)
        {
            return;
        }

        PhysicsNavigationAgent.isStopped = true;
        PhysicsNavigationAgent.ResetPath();
        PhysicsNavigationAgent.updatePosition = false;
        PhysicsNavigationSuppressed = true;
        AuditPose("NavMesh:suspendu");
    }

    private void PhysicsResumeNavigation()
    {
        if (!PhysicsNavigationSuppressed || PhysicsNavigationAgent == null)
        {
            return;
        }

        PhysicsNavigationSuppressed = false;
        PhysicsNavigationAgent.updatePosition = true;
        if (!PhysicsNavigationAgent.isActiveAndEnabled)
        {
            return;
        }

        int areaMask = PhysicsNavigationAgent.areaMask == 0 ? NavMesh.AllAreas : PhysicsNavigationAgent.areaMask;
        if (PhysicsNavigationAgent.isOnNavMesh && NavMesh.SamplePosition(transform.position, out NavMeshHit localHit, 0.15f, areaMask) && Vector3.Distance(localHit.position, transform.position) <= 0.15f)
        {
            AuditPose("NavMesh:avant Warp reprise");
            PhysicsNavigationAgent.Warp(transform.position);
            PhysicsNavigationAgent.nextPosition = transform.position;
            AuditPose("NavMesh:apres Warp reprise");
        }
        else
        {
            // Never let a stale NavMesh island pull an actor to another floor
            // after an aerial action. Behaviour will retry only a local attach.
            PhysicsNavigationAgent.enabled = false;
            if (PhysicsLogPoseAudit)
            {
                Debug.LogWarning("[CombatEnemyPhysicsMotor] Reprise NavMesh refusee pour '" + name + "' : aucune surface locale coherente.", this);
            }
        }

        PhysicsNavigationAgent.updatePosition = true;
    }

    private void PhysicsMoveBody(Vector3 position, Quaternion rotation)
    {
        PhysicsBody.MovePosition(position);
        PhysicsBody.MoveRotation(rotation);
        Physics.SyncTransforms();
    }

    private Vector3 PhysicsResolveRushDelta(Vector3 position)
    {
        if (!PhysicsRushActive || PhysicsActiveMotionProfile == null)
        {
            PhysicsEndEnemyRush();
            return Vector3.zero;
        }

        float deltaTime = Mathf.Min(PhysicsLocalFixedDeltaTime, PhysicsRushRemainingSeconds);
        float requestedDistance = Mathf.Min(PhysicsRushSpeed * deltaTime, PhysicsRushRemainingDistance);
        Vector3 normalizedDirection = PhysicsRushDirection;
        PhysicsRushRemainingSeconds = Mathf.Max(0f, PhysicsRushRemainingSeconds - deltaTime);
        PhysicsRushRemainingDistance = Mathf.Max(0f, PhysicsRushRemainingDistance - requestedDistance);
        if (PhysicsTryClampMotionToObstacle(position, normalizedDirection, requestedDistance, out float allowedDistance))
        {
            requestedDistance = allowedDistance;
            PhysicsEndEnemyRush();
            if (PhysicsLogStateChanges)
            {
                Debug.Log("[CombatEnemyPhysicsMotor] " + name + " ruée bloquee par decor.", this);
            }
        }

        if (PhysicsRushRemainingSeconds <= 0f || PhysicsRushRemainingDistance <= 0.0001f)
            PhysicsEndEnemyRush();
        return normalizedDirection * Mathf.Max(0f, requestedDistance);
    }

    private Vector3 PhysicsClampPlanarMotionToObstacles(Vector3 position, Vector3 planarDelta)
    {
        float requestedDistance = planarDelta.magnitude;
        if (requestedDistance <= 0.0001f)
        {
            return Vector3.zero;
        }

        Vector3 direction = planarDelta / requestedDistance;
        return PhysicsTryClampMotionToObstacle(position, direction, requestedDistance, out float allowedDistance) ? direction * allowedDistance : planarDelta;
    }

    private bool PhysicsTryClampMotionToObstacle(Vector3 position, Vector3 direction, float requestedDistance, out float allowedDistance)
    {
        allowedDistance = requestedDistance;
        if (PhysicsBodyCollider == null || PhysicsActiveMotionProfile == null || requestedDistance <= 0f)
        {
            return false;
        }

        float scale = Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z));
        float radius = Mathf.Max(0.03f, PhysicsBodyCollider.radius * scale);
        float height = Mathf.Max(radius * 2f, PhysicsBodyCollider.height * Mathf.Abs(transform.lossyScale.y));
        Vector3 center = position + transform.TransformVector(PhysicsBodyCollider.center);
        float cylinderHalf = Mathf.Max(0f, height * 0.5f - radius);
        Vector3 first = center + Vector3.up * cylinderHalf;
        Vector3 second = center - Vector3.up * cylinderHalf;
        int hitCount = Physics.CapsuleCastNonAlloc(first, second, radius, direction, PhysicsObstacleHits, requestedDistance + PhysicsActiveMotionProfile.rushCollisionSkin, PhysicsActiveMotionProfile.rushBlockingMask, QueryTriggerInteraction.Ignore);
        float closestDistance = float.PositiveInfinity;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = PhysicsObstacleHits[i];
            if (!PhysicsIsBlockingEnvironmentCollider(hit.collider))
            {
                continue;
            }

            closestDistance = Mathf.Min(closestDistance, hit.distance);
        }

        if (float.IsPositiveInfinity(closestDistance))
        {
            return false;
        }

        allowedDistance = Mathf.Max(0f, closestDistance - PhysicsActiveMotionProfile.rushCollisionSkin);
        return true;
    }

    private void PhysicsSnapToGroundIfAvailable()
    {
        Vector3 position = PhysicsBody != null ? PhysicsBody.position : transform.position;
        if (!PhysicsTryGetGroundY(position, out float groundY))
        {
            return;
        }

        position.y = groundY;
        PhysicsRememberGroundY(groundY);
        if (PhysicsBody != null)
        {
            PhysicsBody.position = position;
        }
        else
        {
            transform.position = position;
        }

        Physics.SyncTransforms();
        AuditPose("physique:SnapToGround");
    }

    private void PhysicsRememberGroundY(float groundY)
    {
        PhysicsLastConfirmedGroundY = groundY;
        PhysicsHasLastConfirmedGroundY = true;
    }

    private bool PhysicsTryGetGroundY(Vector3 actorPosition, out float groundY)
    {
        groundY = 0f;
        if (PhysicsBodyCollider == null)
        {
            return false;
        }

        float scaledRadius = Mathf.Max(0.05f, PhysicsBodyCollider.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z));
        float bottomLocalY = (PhysicsBodyCollider.center.y - PhysicsBodyCollider.height * 0.5f) * transform.lossyScale.y;
        float probeRadius = scaledRadius * 0.9f;
        // Start with the bottom of the probe above the feet, not its center.
        // Otherwise a radius larger than startHeight overlaps the floor.
        Vector3 origin = actorPosition + Vector3.up * (bottomLocalY + PhysicsGroundProbeStartHeight + probeRadius);
        int hitCount = Physics.SphereCastNonAlloc(origin, probeRadius, Vector3.down, PhysicsGroundHits, PhysicsGroundProbeStartHeight + PhysicsGroundProbeDistance, PhysicsGroundMask, QueryTriggerInteraction.Ignore);
        float closestDistance = float.PositiveInfinity;
        Collider selectedCollider = null;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = PhysicsGroundHits[i];
            // Initial-overlap sphere casts have no usable surface contact.
            // Their zero point must not be interpreted as world ground Y=0.
            if (hit.collider == null || hit.distance <= 0f || hit.normal.y < PhysicsMinimumGroundNormal || !PhysicsIsGroundCollider(hit.collider))
            {
                continue;
            }

            float candidateGroundY = hit.point.y - bottomLocalY + PhysicsGroundSkin;
            float verticalCorrection = Mathf.Abs(candidateGroundY - actorPosition.y);
            if (verticalCorrection > PhysicsMaximumGroundSnapDistance)
            {
                if (PhysicsLogPoseAudit && hit.collider != PhysicsLastRejectedGroundCollider)
                {
                    PhysicsLastRejectedGroundCollider = hit.collider;
                    Debug.LogWarning("[CombatEnemyPoseAudit] Sol ignore pour '" + name + "' : '" + hit.collider.name + "' demanderait un decalage vertical de " + verticalCorrection + " m | actorY=" + actorPosition.y + " | solY=" + candidateGroundY + ".", this);
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

        if (selectedCollider != null && PhysicsLogPoseAudit && selectedCollider != PhysicsLastLoggedGroundCollider)
        {
            PhysicsLastLoggedGroundCollider = selectedCollider;
            Debug.Log("[CombatEnemyPoseAudit] Sol retenu pour '" + name + "' : '" + selectedCollider.name + "' | point=" + (groundY + bottomLocalY - PhysicsGroundSkin) + " | distance=" + closestDistance + ".", this);
        }

        return closestDistance < float.PositiveInfinity;
    }

    private bool PhysicsIsOwnCollider(Collider collider)
    {
        Transform candidate = collider.transform;
        return candidate == transform || candidate.IsChildOf(transform);
    }

    private bool PhysicsIsGroundCollider(Collider collider)
    {
        if (collider == null || PhysicsIsOwnCollider(collider) || collider.GetComponentInParent<CharacterInfo>(true) != null || collider.GetComponentInParent<EnemyController>(true) != null || collider.GetComponentInParent<Opsive.UltimateCharacterController.Character.UltimateCharacterLocomotion>(true) != null)
        {
            return false;
        }

        int layer = collider.gameObject.layer;
        return layer != LayerMask.NameToLayer("Character") && layer != LayerMask.NameToLayer("Player") && layer != LayerMask.NameToLayer("Enemy") && layer != LayerMask.NameToLayer("UI") && layer != LayerMask.NameToLayer("VisualEffect") && layer != LayerMask.NameToLayer("Ignore Raycast");
    }

    private bool PhysicsIsBlockingEnvironmentCollider(Collider collider)
    {
        return PhysicsIsGroundCollider(collider) && !collider.isTrigger;
    }

    private void PhysicsResolveReferences()
    {
        // The runtime contract can be added/reloaded while iterating on a
        // prefab. Refresh every reference so an old serialized null cannot
        // keep this motor disabled after its Rigidbody/Capsule exists.
        PhysicsEnemy = GetComponent<EnemyController>();
        PhysicsNavigationAgent = GetComponent<NavMeshAgent>();
        PhysicsBody = GetComponent<Rigidbody>();
        PhysicsBodyCollider = GetComponent<CapsuleCollider>();
        PhysicsAnimationContract = GetComponent<CharacterAnimationController>();
        PhysicsTimeDomain = GetComponent<CombatTimeDomain>();
    }

    private void PhysicsConfigureBody()
    {
        if (PhysicsBody == null || PhysicsBodyCollider == null)
        {
            Debug.LogError("[CombatEnemyPhysicsMotor] Rigidbody cinematique et CapsuleCollider requis sur '" + name + "'.", this);
            enabled = false;
            return;
        }

        PhysicsBody.isKinematic = true;
        PhysicsBody.useGravity = false;
        PhysicsBody.interpolation = RigidbodyInterpolation.Interpolate;
        PhysicsBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        PhysicsBody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        PhysicsBodyCollider.isTrigger = false;
    }

    public void AuditPose(string phase)
    {
        PhysicsLastPosePhase = string.IsNullOrWhiteSpace(phase) ? "inconnue" : phase;
        if (!PhysicsLogPoseAudit)
        {
            return;
        }

        PhysicsResolveReferences();
        NetworkObject networkObject = GetComponent<NetworkObject>();
        NetworkTransform networkTransform = GetComponent<NetworkTransform>();
        Animator animator = PhysicsAnimationContract != null ? PhysicsAnimationContract.Animator : null;
        string animatorState = animator != null && animator.runtimeAnimatorController != null ? animator.GetCurrentAnimatorStateInfo(0).fullPathHash.ToString() : "aucun";
        bool navActive = PhysicsNavigationAgent != null && PhysicsNavigationAgent.isActiveAndEnabled;
        bool navOnMesh = navActive && PhysicsNavigationAgent.isOnNavMesh;
        string navState = PhysicsNavigationAgent == null ? "absent" : "enabled=" + PhysicsNavigationAgent.enabled + ", onNavMesh=" + navOnMesh + ", stopped=" + (navOnMesh ? PhysicsNavigationAgent.isStopped.ToString() : "n/a") + ", updatePosition=" + PhysicsNavigationAgent.updatePosition + ", next=" + (navOnMesh ? PhysicsNavigationAgent.nextPosition.ToString() : "n/a");
        string networkState = networkObject == null ? "absent" : "spawned=" + networkObject.IsSpawned + ", networkTransform=" + (networkTransform != null ? "enabled=" + networkTransform.enabled + ", local=" + networkTransform.InLocalSpace : "absent");
        Debug.Log("[CombatEnemyPoseAudit] " + PhysicsLastPosePhase + " | actor='" + name + "' | world=" + transform.position + " | local=" + transform.localPosition + " | parent=" + (transform.parent != null ? transform.parent.name : "<none>") + " | rigidbody=" + (PhysicsBody != null ? PhysicsBody.position.ToString() : "absent") + " | nav=" + navState + " | network=" + networkState + " | animator=" + animatorState + ".", this);
    }

    private void PhysicsSetState(CombatEnemyPhysicsState nextState, string reason)
    {
        if (State == nextState)
        {
            return;
        }

        State = nextState;
        if (PhysicsLogStateChanges)
        {
            Debug.Log("[CombatEnemyPhysicsMotor] " + name + " -> " + nextState + " (" + reason + ")", this);
        }
    }
}