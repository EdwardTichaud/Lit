using UnityEngine;

/// <summary>Authored enemy tuning, copied with CharacterData for each runtime instance.</summary>
[System.Serializable]
public sealed class EnemySettings
{
    [Tooltip("Poursuite et presentation du deplacement.") ]
     public EnemyController.CombatPositioningProfile LocomotionPositioning = new EnemyController.CombatPositioningProfile();
    [Tooltip("Poursuite et presentation du deplacement.") ]
    [Min(0f)] public float LocomotionFacingSpeedDegreesPerSecond = 540f;
    [Tooltip("Poursuite et presentation du deplacement.") ]
    [Min(0.01f)] public float LocomotionAnimatorDampTime = 0.08f;
    [Tooltip("Poursuite et presentation du deplacement.") ]
    [Header("InPlace cycle reference speeds")]
    [Min(.01f)] public float LocomotionWalkCycleSpeed = 1.8f;
    [Tooltip("Poursuite et presentation du deplacement.") ]
    [Min(.01f)] public float LocomotionRunCycleSpeed = 3.6f;
    [Header("Obstacle Clearance")]
    [Min(0f), Tooltip("Marge supplementaire entre le centre de l'ennemi et le bord du NavMesh. Elle evite les poses trop proches des murs et du decor." )]
    public float LocomotionNavMeshEdgeClearance = 0.45f;
    [Min(0.1f), Tooltip("Rayon maximal de recherche d'une destination qui respecte la marge decor." )]
    public float LocomotionClearanceSearchRadius = 2f;
    [Range(4, 24), Tooltip("Nombre de positions candidates evaluees autour d'une destination proche d'un obstacle." )]
    public int LocomotionClearanceSearchSamples = 12;
    [Tooltip("Poursuite et presentation du deplacement.") ]
     public bool LocomotionLogDiagnostics;
    [Tooltip("Diagnostic de configuration.") ]
     public bool ContractLogDiagnostics = true;
    [Tooltip("Recuperation si un evenement de fin manque.") ]
    [Min(.1f)] public float RecoveryExtraRecoverySeconds = 1f;
    [Tooltip("Recuperation si un evenement de fin manque.") ]
     public bool RecoveryLogDiagnostics;
    [Tooltip("Diagnostic des decisions.") ]
     public bool BrainLogDecisions;
    [Tooltip("Validation locale du NavMesh.") ]
    [Min(0.1f)] public float NavigationSampleDistance = 1.5f;
    [Tooltip("Validation locale du NavMesh.") ]
    [Min(0f)] public float NavigationReattachTolerance = 0.15f;
    [Tooltip("Validation locale du NavMesh.") ]
    [Min(0.02f)] public float NavigationRetryInterval = 0.25f;
    [Tooltip("Validation locale du NavMesh.") ]
    [Min(0.1f)] public float NavigationRebuildRequestInterval = 1f;
    [Tooltip("Validation locale du NavMesh.") ]
     public bool NavigationLogDiagnostics;

    [Tooltip("Presentation et reactions de combat.") ]
     public string ActorHitAnimatorState = "Hit";

    [Tooltip("Presentation et reactions de combat.") ]
    [Min(0f)] public float ActorHitAnimationTransitionSeconds = 0.06f;

    [Tooltip("Presentation et reactions de combat.") ]
     public string ActorIdleAnimatorState = "Idle";

    [Range(0.5f, 1f), Tooltip("Part de l'animation Hit jouee avant le retour fluide vers Idle.")]

    public float ActorHitRecoveryNormalizedTime = 0.9f;

    [Tooltip("Presentation et reactions de combat.") ]
    [Min(0f)] public float ActorHitRecoveryTransitionSeconds = 0.1f;

    [Tooltip("Presentation et reactions de combat.") ]
     public string ActorDeathAnimatorState = "Death";

    [Tooltip("Presentation et reactions de combat.") ]
    [Min(0f)] public float ActorDeathAnimationTransitionSeconds = 0.08f;

    [Tooltip("Presentation et reactions de combat.") ]
    [Min(0f)] public float ActorRetaliationDelaySeconds = 0.15f;

    [Tooltip("Collisions et mouvement des actions.") ]
    [Header("Grounding")]
    [Min(0.01f)] public float PhysicsGroundSkin = 0.03f;
    [Tooltip("Collisions et mouvement des actions.") ]
    [Min(0.05f)] public float PhysicsGroundProbeStartHeight = 0.35f;
    [Tooltip("Collisions et mouvement des actions.") ]
    [Min(0.1f)] public float PhysicsGroundProbeDistance = 4f;
    [Min(0.05f), Tooltip("Correction verticale maximale autorisee par une sonde de sol. Evite de raccrocher un acteur a un autre niveau du decor.")]
    public float PhysicsMaximumGroundSnapDistance = 0.75f;
    [Min(0.05f), Tooltip("Delai maximal apres une demande d'atterrissage avant le filet de securite vertical.")]
    public float PhysicsEmergencyLandingDelay = 0.45f;
    [Tooltip("Collisions et mouvement des actions.") ]
    [Range(0f, 1f)] public float PhysicsMinimumGroundNormal = 0.45f;
    [Tooltip("Collisions et mouvement des actions.") ]
     public LayerMask PhysicsGroundMask = ~0;
    [Tooltip("Collisions et mouvement des actions.") ]
    [Header("Diagnostics")]
     public bool PhysicsLogStateChanges;
    [Tooltip("Trace les ecritures de pose afin d'identifier un systeme qui deplace l'ennemi hors de son SceneMarker.")]
    public bool PhysicsLogPoseAudit = true;
    [Tooltip("Collisions et mouvement des actions.") ]
    [Min(0.5f)] public float PhysicsPoseJumpDiagnosticDistance = 0.5f;
    [Min(0.01f), Tooltip("Distance horizontale maximale acceptee depuis un unique delta de root motion ennemi.")]
    public float PhysicsMaximumRootMotionDeltaPerFrame = 0.5f;
    [Min(0.01f), Tooltip("Distance horizontale maximale appliquee par tick physique depuis le root motion accumule.")]
    public float PhysicsMaximumRootMotionDistancePerFixedUpdate = 0.75f;
    [Min(0.01f), Tooltip("Distance a partir de laquelle un repositionnement explicite d'action est journalise.")]
    public float PhysicsActionRepositionDiagnosticDistance = 1f;
    [Tooltip("Collisions et mouvement des actions.") ]
     public EnemyController.AnimationMovementMode PhysicsAnimationMovementMode;

}
