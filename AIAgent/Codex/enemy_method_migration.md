# Arbitrage des methodes ennemies

Inventaire avant fusion. Les responsabilites sont conservees sauf les arbitrages detailles dans le guide.

| Source | Methode | Destination | Decision |
|---|---|---|---|
| CombatEnemyLocomotionController | SetFacingSpeed | SetFacingSpeed | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyLocomotionController | Reset | LocomotionReset | Supprimer : ancien hook Unity remplace par le cycle de vie central | 
| CombatEnemyLocomotionController | Awake | LocomotionAwake | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyLocomotionController | OnDisable | LocomotionOnDisable | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyLocomotionController | OnValidate | LocomotionOnValidate | Supprimer : ancien hook Unity remplace par le cycle de vie central | 
| CombatEnemyLocomotionController | Update | LocomotionUpdate | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyLocomotionController | LateUpdate | LocomotionLateUpdate | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyLocomotionController | SetReturnFacing | SetReturnFacing | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyLocomotionController | SetCombatTarget | SetCombatTarget | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyLocomotionController | SetAttackFacingLocked | SetAttackFacingLocked | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyLocomotionController | ApproachTarget | ApproachTarget | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyLocomotionController | NavigateTowardsTarget | NavigateTowardsTarget | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyLocomotionController | NavigateTo | NavigateTo | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyLocomotionController | StopNavigation | StopNavigation | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyLocomotionController | FaceTarget | FaceTarget | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyLocomotionController | UpdateAnimatorPresentation | LocomotionUpdateAnimatorPresentation | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyLocomotionController | ForceIdlePresentation | LocomotionForceIdlePresentation | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyLocomotionController | ShouldPresentLocomotion | ShouldPresentLocomotion | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyLocomotionController | ResolvePlaybackRate | ResolvePlaybackRate | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyLocomotionController | ResolveReferences | LocomotionResolveReferences | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyLocomotionController | CaptureNavigationDefaults | LocomotionCaptureNavigationDefaults | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyLocomotionController | ApplyLocalNavigationScale | LocomotionApplyLocalNavigationScale | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyLocomotionController | SetMovementPace | LocomotionSetMovementPace | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyLocomotionController | ResolveClearDestination | LocomotionResolveClearDestination | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyLocomotionController | FindNearbyProjectedDestination | LocomotionFindNearbyProjectedDestination | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyLocomotionController | GetNavMeshEdgeClearance | LocomotionGetNavMeshEdgeClearance | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyLocomotionController | HasCombatLocomotionParameters | LocomotionHasCombatLocomotionParameters | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyRuntimeContract | HasRequiredComponents | HasRequiredComponents | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyRuntimeContract | DescribeRequiredComponents | DescribeRequiredComponents | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyRuntimeContract | Reset | ContractReset | Supprimer : ancien hook Unity remplace par le cycle de vie central | 
| CombatEnemyRuntimeContract | Awake | ContractAwake | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyRuntimeContract | ValidateContract | ValidateContract | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyRuntimeContract | DisableCombatSystems | DisableCombatSystems | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyRuntimeContract | TraceAnimationEvent | TraceAnimationEvent | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyRuntimeContract | ResolveReferences | ContractResolveReferences | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyRuntimeContract | ResolveAnimator | ContractResolveAnimator | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyRuntimeContract | Present | ContractPresent | Integrer puis centraliser les appels de cycle de vie | 
| EnemyAttackRecoverySafety | Reset | RecoveryReset | Supprimer : ancien hook Unity remplace par le cycle de vie central | 
| EnemyAttackRecoverySafety | Awake | RecoveryAwake | Integrer puis centraliser les appels de cycle de vie | 
| EnemyAttackRecoverySafety | OnEnable | RecoveryOnEnable | Integrer puis centraliser les appels de cycle de vie | 
| EnemyAttackRecoverySafety | OnDisable | RecoveryOnDisable | Integrer puis centraliser les appels de cycle de vie | 
| EnemyAttackRecoverySafety | OnRetaliationStarted | RecoveryOnRetaliationStarted | Integrer puis centraliser les appels de cycle de vie | 
| EnemyAttackRecoverySafety | ReportAirborneCheckpoint | RecoveryReportAirborneCheckpoint | Supprimer : coroutine de diagnostic non suivie; recuperation centralisee par identifiant d’action | 
| EnemyAttackRecoverySafety | WatchAttack | RecoveryWatchAttack | Integrer puis centraliser les appels de cycle de vie | 
| EnemyAttackRecoverySafety | CompleteFallback | RecoveryCompleteFallback | Remplacer par CompleteAction(sequence, recovery), commun aux evenements et au timeout | 
| EnemyAttackRecoverySafety | CancelSafety | RecoveryCancelSafety | Integrer puis centraliser les appels de cycle de vie | 
| EnemyAttackRecoverySafety | ResolveReferences | RecoveryResolveReferences | Integrer puis centraliser les appels de cycle de vie | 
| EnemyCinematicState | SetSuspended | SetSuspended | Integrer puis centraliser les appels de cycle de vie | 
| EnemyCinematicState | Place | Place | Integrer puis centraliser les appels de cycle de vie | 
| EnemyCombatBrain | ResolvePlayerController | BrainResolvePlayerController | Integrer puis centraliser les appels de cycle de vie | 
| EnemyCombatBrain | Awake | BrainAwake | Integrer puis centraliser les appels de cycle de vie | 
| EnemyCombatBrain | Start | BrainStart | Integrer puis centraliser les appels de cycle de vie | 
| EnemyCombatBrain | OnDisable | BrainOnDisable | Integrer puis centraliser les appels de cycle de vie | 
| EnemyCombatBrain | Update | BrainUpdate | Integrer puis centraliser les appels de cycle de vie | 
| EnemyCombatBrain | ResolveProfile | BrainResolveProfile | Integrer puis centraliser les appels de cycle de vie | 
| EnemyCombatBrain | RegisterThreat | RegisterThreat | Integrer puis centraliser les appels de cycle de vie | 
| EnemyCombatBrain | ResolveApproachDistance | ResolveApproachDistance | Integrer puis centraliser les appels de cycle de vie | 
| EnemyCombatBrain | TryResolveApproach | BrainTryResolveApproach | Integrer puis centraliser les appels de cycle de vie | 
| EnemyCombatBrain | IsPatternEquipped | BrainIsPatternEquipped | Integrer puis centraliser les appels de cycle de vie | 
| EnemyCombatBrain | ShouldPreferMelee | BrainShouldPreferMelee | Integrer puis centraliser les appels de cycle de vie | 
| EnemyCombatBrain | IsPatternAvailable | BrainIsPatternAvailable | Integrer puis centraliser les appels de cycle de vie | 
| EnemyCombatBrain | ExplainAttackWait | BrainExplainAttackWait | Integrer puis centraliser les appels de cycle de vie | 
| EnemyCombatBrain | ChoosePattern | BrainChoosePattern | Integrer puis centraliser les appels de cycle de vie | 
| EnemyCombatBrain | CanStart | BrainCanStart | Integrer puis centraliser les appels de cycle de vie | 
| EnemyCombatBrain | StartStep | BrainStartStep | Integrer puis centraliser les appels de cycle de vie | 
| EnemyCombatBrain | LockAttackDirection | LockAttackDirection | Integrer puis centraliser les appels de cycle de vie | 
| EnemyCombatBrain | ResolveAnimationAttackEnded | ResolveAnimationAttackEnded | Integrer puis centraliser les appels de cycle de vie | 
| EnemyCombatBrain | ResolveAttackSafetyTimeout | ResolveAttackSafetyTimeout | Integrer puis centraliser les appels de cycle de vie | 
| EnemyCombatBrain | CompleteStep | BrainCompleteStep | Integrer puis centraliser les appels de cycle de vie | 
| EnemyCombatBrain | Suspend | Suspend | Integrer puis centraliser les appels de cycle de vie | 
| EnemyCombatBrain | EnterStagger | EnterStagger | Integrer puis centraliser les appels de cycle de vie | 
| EnemyCombatBrain | CancelAction | CancelAction | Integrer puis centraliser les appels de cycle de vie | 
| EnemyCombatBrain | TickReturn | BrainTickReturn | Integrer puis centraliser les appels de cycle de vie | 
| EnemyCombatBrain | ResolveGuardDamage | ResolveGuardDamage | Integrer puis centraliser les appels de cycle de vie | 
| EnemyCombatBrain | Distance | BrainDistance | Integrer puis centraliser les appels de cycle de vie | 
| EnemyCombatBrain | SetPhase | BrainSetPhase | Integrer puis centraliser les appels de cycle de vie | 
| EnemyNavigationController | Reset | NavigationReset | Supprimer : ancien hook Unity remplace par le cycle de vie central | 
| EnemyNavigationController | Awake | NavigationAwake | Integrer puis centraliser les appels de cycle de vie | 
| EnemyNavigationController | OnEnable | NavigationOnEnable | Integrer puis centraliser les appels de cycle de vie | 
| EnemyNavigationController | OnDisable | NavigationOnDisable | Integrer puis centraliser les appels de cycle de vie | 
| EnemyNavigationController | OnValidate | NavigationOnValidate | Supprimer : ancien hook Unity remplace par le cycle de vie central | 
| EnemyNavigationController | EnsureReady | EnsureReady | Integrer puis centraliser les appels de cycle de vie | 
| EnemyNavigationController | Stop | Stop | Integrer puis centraliser les appels de cycle de vie | 
| EnemyNavigationController | RequestRebuild | NavigationRequestRebuild | Integrer puis centraliser les appels de cycle de vie | 
| EnemyNavigationController | BindManager | NavigationBindManager | Integrer puis centraliser les appels de cycle de vie | 
| EnemyNavigationController | BindWorld | NavigationBindWorld | Integrer puis centraliser les appels de cycle de vie | 
| EnemyNavigationController | OnWorldReady | NavigationOnWorldReady | Integrer puis centraliser les appels de cycle de vie | 
| EnemyNavigationController | OnWorldStateChanged | NavigationOnWorldStateChanged | Integrer puis centraliser les appels de cycle de vie | 
| EnemyNavigationController | OnWorldBuildFailed | NavigationOnWorldBuildFailed | Integrer puis centraliser les appels de cycle de vie | 
| EnemyNavigationController | OnNavMeshRebuilt | NavigationOnNavMeshRebuilt | Integrer puis centraliser les appels de cycle de vie | 
| EnemyNavigationController | ClearFailure | NavigationClearFailure | Integrer puis centraliser les appels de cycle de vie | 
| EnemyNavigationController | ReportFailure | NavigationReportFailure | Integrer puis centraliser les appels de cycle de vie | 
| EnemySkills | Reset | SkillsReset | Supprimer : ancien hook Unity remplace par le cycle de vie central | 
| EnemySkills | Awake | SkillsAwake | Integrer puis centraliser les appels de cycle de vie | 
| EnemySkills | SetActiveSkill | SetActiveSkill | Integrer puis centraliser les appels de cycle de vie | 
| EnemySkills | ChooseRetaliationSkill | ChooseRetaliationSkill | Integrer puis centraliser les appels de cycle de vie | 
| EnemySkills | SetActiveSkill | SetActiveSkill | Integrer puis centraliser les appels de cycle de vie | 
| EnemySkills | PlaySkill | PlaySkill | Integrer puis centraliser les appels de cycle de vie | 
| EnemySkills | PlayActiveSkill | PlayActiveSkill | Integrer puis centraliser les appels de cycle de vie | 
| EnemySkills | ReturnToIdle | ReturnToIdle | Integrer puis centraliser les appels de cycle de vie | 
| EnemySkills | ExecuteEnemyAttack | ExecuteEnemyAttack | Integrer puis centraliser les appels de cycle de vie | 
| EnemySkills | PlayOutcomeFeedback | PlayOutcomeFeedback | Integrer puis centraliser les appels de cycle de vie | 
| EnemySkills | PlayVfxCue | SkillsPlayVfxCue | Integrer puis centraliser les appels de cycle de vie | 
| EnemySkills | PlayProjectileCue | SkillsPlayProjectileCue | Integrer puis centraliser les appels de cycle de vie | 
| EnemySkills | ResolveReferences | SkillsResolveReferences | Integrer puis centraliser les appels de cycle de vie | 
| EnemySkills | PlayCueAudio | SkillsPlayCueAudio | Integrer puis centraliser les appels de cycle de vie | 
| RealTimeCombatEnemy | Reset | ActorReset | Supprimer : ancien hook Unity remplace par le cycle de vie central | 
| RealTimeCombatEnemy | Awake | ActorAwake | Integrer puis centraliser les appels de cycle de vie | 
| RealTimeCombatEnemy | OnDisable | ActorOnDisable | Integrer puis centraliser les appels de cycle de vie | 
| RealTimeCombatEnemy | OnValidate | ActorOnValidate | Supprimer : ancien hook Unity remplace par le cycle de vie central | 
| RealTimeCombatEnemy | Update | ActorUpdate | Supprimer : ancien hook Unity remplace par le cycle de vie central | 
| RealTimeCombatEnemy | RefreshPlayerVisibility | RefreshPlayerVisibility | Integrer puis centraliser les appels de cycle de vie | 
| RealTimeCombatEnemy | ReceiveLightDamage | ReceiveLightDamage | Integrer puis centraliser les appels de cycle de vie | 
| RealTimeCombatEnemy | ReceiveDamage | ReceiveDamage | Integrer puis centraliser les appels de cycle de vie | 
| RealTimeCombatEnemy | ForceDefeatFromThreshold | ForceDefeatFromThreshold | Integrer puis centraliser les appels de cycle de vie | 
| RealTimeCombatEnemy | TryStartRetaliation | TryStartRetaliation | Integrer puis centraliser les appels de cycle de vie | 
| RealTimeCombatEnemy | TryStartThresholdFailureRetaliation | TryStartThresholdFailureRetaliation | Integrer puis centraliser les appels de cycle de vie | 
| RealTimeCombatEnemy | TryStartAutonomousAttack | TryStartAutonomousAttack | Integrer puis centraliser les appels de cycle de vie | 
| RealTimeCombatEnemy | CompleteAutonomousAttack | CompleteAutonomousAttack | Integrer puis centraliser les appels de cycle de vie | 
| RealTimeCombatEnemy | PeekRetaliationSkill | PeekRetaliationSkill | Integrer puis centraliser les appels de cycle de vie | 
| RealTimeCombatEnemy | CompleteRetaliation | CompleteRetaliation | Integrer puis centraliser les appels de cycle de vie | 
| RealTimeCombatEnemy | CompleteRetaliationAndPrepareNext | CompleteRetaliationAndPrepareNext | Integrer puis centraliser les appels de cycle de vie | 
| RealTimeCombatEnemy | CompleteEnemyAttackWhenGrounded | CompleteEnemyAttackWhenGrounded | Integrer puis centraliser les appels de cycle de vie | 
| RealTimeCombatEnemy | BeginEnemyAirborne | BeginEnemyAirborne | Integrer puis centraliser les appels de cycle de vie | 
| RealTimeCombatEnemy | RequestEnemyLanding | RequestEnemyLanding | Integrer puis centraliser les appels de cycle de vie | 
| RealTimeCombatEnemy | BeginEnemyRush | BeginEnemyRush | Integrer puis centraliser les appels de cycle de vie | 
| RealTimeCombatEnemy | EndEnemyRush | EndEnemyRush | Integrer puis centraliser les appels de cycle de vie | 
| RealTimeCombatEnemy | SetActionPlanarPosition | SetActionPlanarPosition | Integrer puis centraliser les appels de cycle de vie | 
| RealTimeCombatEnemy | SetLockPresentation | SetLockPresentation | Integrer puis centraliser les appels de cycle de vie | 
| RealTimeCombatEnemy | PlayHitAnimation | PlayHitAnimation | Integrer puis centraliser les appels de cycle de vie | 
| RealTimeCombatEnemy | CancelHitRecovery | CancelHitRecovery | Integrer puis centraliser les appels de cycle de vie | 
| RealTimeCombatEnemy | PlayDeathAnimation | PlayDeathAnimation | Integrer puis centraliser les appels de cycle de vie | 
| RealTimeCombatEnemy | PlayDeathAnimationAfterGrounding | ActorPlayDeathAnimationAfterGrounding | Integrer puis centraliser les appels de cycle de vie | 
| RealTimeCombatEnemy | ReturnToIdleAnimation | ReturnToIdleAnimation | Integrer puis centraliser les appels de cycle de vie | 
| RealTimeCombatEnemy | RecoverFromHit | ActorRecoverFromHit | Integrer puis centraliser les appels de cycle de vie | 
| RealTimeCombatEnemy | RestoreRootMotionAfterHit | ActorRestoreRootMotionAfterHit | Integrer puis centraliser les appels de cycle de vie | 
| RealTimeCombatEnemy | StoreLightDamageForRetaliation | ActorStoreLightDamageForRetaliation | Integrer puis centraliser les appels de cycle de vie | 
| RealTimeCombatEnemy | ResolveCombatAnimator | ActorResolveCombatAnimator | Integrer puis centraliser les appels de cycle de vie | 
| RealTimeCombatEnemy | ResolveLockPoint | ActorResolveLockPoint | Integrer puis centraliser les appels de cycle de vie | 
| RealTimeCombatEnemy | MigrateLegacyHitState | ActorMigrateLegacyHitState | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyPhysicsMotor | BeginEnemyAdvance | BeginEnemyAdvance | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyPhysicsMotor | EndEnemyAdvance | EndEnemyAdvance | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyPhysicsMotor | AdvanceDelta | PhysicsAdvanceDelta | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyPhysicsMotor | Reset | PhysicsReset | Supprimer : ancien hook Unity remplace par le cycle de vie central | 
| CombatEnemyPhysicsMotor | Awake | PhysicsAwake | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyPhysicsMotor | LateUpdate | PhysicsLateUpdate | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyPhysicsMotor | OnDisable | PhysicsOnDisable | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyPhysicsMotor | BeginEnemyAction | BeginEnemyAction | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyPhysicsMotor | BeginEnemyAirborne | PhysicsBeginEnemyAirborne | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyPhysicsMotor | RequestEnemyLanding | PhysicsRequestEnemyLanding | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyPhysicsMotor | BeginEnemyRush | PhysicsBeginEnemyRush | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyPhysicsMotor | EndEnemyRush | PhysicsEndEnemyRush | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyPhysicsMotor | CompleteEnemyAction | CompleteEnemyAction | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyPhysicsMotor | InterruptEnemyAction | InterruptEnemyAction | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyPhysicsMotor | ForceCompleteEnemyAction | ForceCompleteEnemyAction | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyPhysicsMotor | EnterCinematic | EnterCinematic | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyPhysicsMotor | ExitCinematic | ExitCinematic | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyPhysicsMotor | ApplyActionRootMotion | ApplyActionRootMotion | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyPhysicsMotor | SetActionPlanarPosition | PhysicsSetActionPlanarPosition | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyPhysicsMotor | FixedUpdate | PhysicsFixedUpdate | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyPhysicsMotor | FinishRecovery | PhysicsFinishRecovery | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyPhysicsMotor | SuppressNavigation | PhysicsSuppressNavigation | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyPhysicsMotor | ResumeNavigation | PhysicsResumeNavigation | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyPhysicsMotor | MoveBody | PhysicsMoveBody | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyPhysicsMotor | ResolveRushDelta | PhysicsResolveRushDelta | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyPhysicsMotor | ClampPlanarMotionToObstacles | PhysicsClampPlanarMotionToObstacles | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyPhysicsMotor | TryClampMotionToObstacle | PhysicsTryClampMotionToObstacle | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyPhysicsMotor | SnapToGroundIfAvailable | PhysicsSnapToGroundIfAvailable | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyPhysicsMotor | RememberGroundY | PhysicsRememberGroundY | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyPhysicsMotor | TryGetGroundY | PhysicsTryGetGroundY | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyPhysicsMotor | IsOwnCollider | PhysicsIsOwnCollider | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyPhysicsMotor | IsGroundCollider | PhysicsIsGroundCollider | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyPhysicsMotor | IsBlockingEnvironmentCollider | PhysicsIsBlockingEnvironmentCollider | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyPhysicsMotor | ResolveReferences | PhysicsResolveReferences | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyPhysicsMotor | ConfigureBody | PhysicsConfigureBody | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyPhysicsMotor | AuditPose | AuditPose | Integrer puis centraliser les appels de cycle de vie | 
| CombatEnemyPhysicsMotor | SetState | PhysicsSetState | Integrer puis centraliser les appels de cycle de vie | 

## Arbitrages fonctionnels

| Responsabilités en concurrence | Décision et consommateurs |
|---|---|
| RealTimeCombatEnemyBehaviour et EnemyCombatBrain | Supprimer la boucle historique. Les consommateurs du gestionnaire de combat, des paliers et des cinématiques lisent désormais les états du contrôleur par profils. |
| NavMeshAgent, locomotion, physique et root motion | Conserver une autorité de déplacement par état. Les deltas Animator passent une seule fois par EnemyController.OnAnimatorMove; les clients réseau ne pilotent pas la physique autoritaire. |
| Plusieurs Awake/OnEnable/OnDisable/Update | Orchestration centrale dans EnemyController.cs; retrait de 13 méthodes devenues inutilisées, dont les anciens Reset/OnValidate et le rafraîchissement visuel redondant. |
| EndEnemyAttack et watchdog | Procédure CompleteAction commune, identifiant d'action contrôlé, fin normale idempotente. La sécurité peut terminer une action déjà en attente d'atterrissage. |
| WaitForSeconds et horloge locale | Le watchdog utilise le temps local et attend pendant la suspension. Les délais de riposte utilisent la même horloge que le cerveau. |
| Santé initiale vide et sauvegarde à zéro | État d'initialisation explicite dans CharacterInfo. SetHealth est une restauration et ne ressuscite pas le personnage. |
| Skill sélectionné et attaque engagée | Conserver les deux états distincts : SelectedSkill sert à préparer une compétence; ActiveSkill identifie l'action effectivement engagée. |
| Animation commune joueur/ennemi | Contrat abstrait partagé; implémentation ennemie dans EnemyController, implémentations du joueur dans ses composants dédiés. |
| Trois activations du scientifique | Un seul changement de CombatEnabled; le contrôleur reste disponible pour son cycle de vie et sa physique. |
| Réglages de prefab et fiche partagée | Transfert dans EnemySettings sur CharacterData; fiche séparée pour GiantJuggernaut afin de préserver les différences. |

## Ancien exécuteur historique

Toutes les méthodes privées de RealTimeCombatEnemyBehaviour sont supprimées avec sa boucle de décision. Les équivalents utiles sont la sélection de patterns, RegisterThreat, SetSuspended/Place, le retour au point initial et la fin d'action dans EnemyController. Les accesseurs de combat/cinématique encore consommés délèguent à ces états. Aucun second MonoBehaviour ne reste pour cette compatibilité d'API.
