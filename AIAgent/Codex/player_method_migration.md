# Arbitrages des modules joueur

| Responsabilite | Proprietaire retenu | Decision |
|---|---|---|
| Physique et collision | UCC et LitOpsiveLocomotionBridge | Conserver; reservations identifiees |
| Saut / esquive / trajectoires | Modules specialises existants | Conserver independants |
| Root motion | PlayerAnimationController et relay | Retirer les branches ennemies |
| Evenements clips | PlayerCombatAnimationEvents | Garder les signatures; deleguer execution |
| Sante | CharacterHealth / LitUccDamageBridge / squad | Conserver autorite configuree |
| Visage diagnostic | Editeur FacialExpressionController | Supprimer le composant debugger |

## Inventaire avant migration

### Assets/Characters/0_Script/SquadCharacterController.AnimatorParameters.cs

Methodes : SetAnimatorBoolIfValid, SetAnimatorFloatIfValid, HasAnimatorParameter

Champs serialises : 

### Assets/Characters/0_Script/SquadCharacterController.cs

Methodes : TryGetHeadWorldY, Reset, Update, LateUpdate, Awake, OnEnable, OnDisable, OnDestroy, OnTransformChildrenChanged, OnTransformParentChanged, CacheAudioListener, CacheNetworkObject, RefreshAnimationReferences, UpdateAudioListenerState, RefreshAudioListenerStateForExternalLocomotion, SetAudioListenerActive, BindCharacterData, ShouldForceStarterItems, SyncCharacterInfo, InitializeHealthFromCharacterData, SetHealth, SetCurrentHp, SetMaxHp, SetHealthFromAuthority, SetHealthLocal, SetCurrentHpLocal, SetMaxHpLocal, RestoreHealthToMax, NotifyHealthChanged, AddItem, EnsureDynamicMeshCollidersSafe, ResetFlameToMax, FindFlameItemInCharacterData, ApplyInventoryState, TryUseItem, IsInteractionItemEquipped, HasEquippedInteractionCapability, GetEquippedInteractionCapabilities, TryToggleEquippedInteractionItem, IsCombatItemEnabled, TryToggleEnabledCombatItem, TryEnableCombatItem, TryDisableCombatItem, GetEnabledCombatItemsSnapshot, GetEnabledCombatDefensiveItems, TryGetCombatDefenseItemHitPoints, GetCombatDefenseItemRemainingHitPoints, SetCombatDefenseItemRemainingHitPoints, ApplyCombatDefenseItemHitPointChange, GetCombatDefenseItemHitPointStacks, GetCombatDefenseItemHitPointsSnapshot, TryEquipInteractionItem, TryUnequipInteractionItem, TryBreakItem, HasMatchingKey, TryUseMatchingKey, LearnSkill, ForgetSkill, TryRemoveItem, TryRemoveItemQuantity, ApplyStarterItems, EnsureInventoryList, EnsureEquippedInteractionList, EnsureEnabledCombatList, EnsureCombatDefenseItemHitPointsList, ApplyEquippedInteractionItems, ApplyEnabledCombatItems, ApplyCombatDefenseItemHitPoints, SanitizeEquippedInteractionItems, SanitizeEnabledCombatItems, SanitizeCombatDefenseItemHitPoints, FindMostDamagedHitPointEntryIndex, ResolveInventoryItemById, CountInventoryItemById, AddCombatDefenseItemHitPointUnits, RemoveCombatDefenseItemHitPointUnits, NormalizeReactiveInventoryItems, NormalizeWetClayPreservation, TryFindMatchingKey, MarkInventoryInitialized, LoadInventoryFromCharacterData, SyncFlameStateToCharacterData, SyncInteractionEquipmentToCharacterData, SyncCombatEquipmentToCharacterData, SyncCombatDefenseItemHitPointsToCharacterData, BindMuninChargeController, ApplyMuninChargeStateFromCharacterData, OnMuninChargesChanged, SyncMuninChargesToCharacterData, SyncFlameStateToCharacterDataIfChanged, OnValidate, ToggleFlame, TriggerMunin, ApplyFlameState, Move, MoveWorld, SetSprintModifier, Jump, Stop, TraceDistrictMoveIntent, TraceDistrictStopWhileInputHeld, PushScriptedMovementSuppression, PopScriptedMovementSuppression, ClearTransientMovementLocksForSceneTransition, PushExternalLocomotionDriver, PopExternalLocomotionDriver, ApplyMovementFacialNeutral, ResolveFacialExpressionController, TryGetLocomotionCapsule, IsSelfCollider, InitializeFlameState, TickFlameLifetimeForExternalLocomotion, UpdateFlameLifetime, AddFlameSeconds, ConsumeItem, IsFlameItem, DisableCharacterFlameState, SetFlameEquipped, ApplyFlameVisualState, QueueFlameVisualTransition, UpdateFlameVisualTransition, CanDelayFlameVisualTransition, SetFlameAnimatorBool, IsFlameAnimationStateActive, IsFlameVisualTransitionAnimationComplete, GetFlameAnimationLayerIndex, SyncFlameAnimationStateImmediate, UpdateFlameAnimationLayerWeight, ResolveFlameAnimationLayerWeightTarget, MatchesFlameAnimationState, ClearPendingFlameVisualTransition, GetFlameItem, RemoveFlameItem, EnsureFlameCached, ConfigureFlamePhysics, FindFlameTransform, CreateFallbackCarriedTorchVisual, FindChildByName, ResolveMovementInputMagnitude, GetCurrentHorizontalVelocity, GetMoveDirection, GetWorldSpaceInput, ShouldUseCameraRelativeInput, TryResolveStoredMovementBasis, ResolveMovementReferenceInput, TryStoreMovementReference, BlendStoredMovementReference, TryNormalizeMovementBasis, ClearStoredMovementReference, TryResolveMovementBasis, ResolveMovementCamera, IsValidMovementCamera, ApplyAnimatorSettings, HasCinematicMotionAuthority, EnsureRigidbodyCollisionSafety, ApplyLowFrictionLocomotionMaterial, GetLowFrictionLocomotionMaterial, AddImpulse, RegisterCharacter, UnregisterCharacter, RefreshCharacterCollisionsIfNeeded, MarkCollidersDirty, CacheColliders, SetIgnoreCollisionsWith, IsSceneCollider

Champs serialises : int startingFlameSeconds, int flameSecondsRemaining, bool logInventoryInitialization, CharacterData characterData, int maxHp, int currentHp, bool resetHpOnBind, bool clampHpToMax, Animator animator, Rigidbody rigidbodyTarget, Transform motionRoot, FacialExpressionController facialExpressionController, bool forceIdleFacialExpressionWhileMoving, float facialMovementInputThreshold, float facialMovementIdleFadeDuration, float moveSpeed, float movementInputDeadZone, bool useCameraRelative, Camera referenceCamera, bool preserveFixedCameraMovementContinuity, float fixedCameraMovementInputRefreshAngle, float fixedCameraMovementReferenceBlendSharpness, bool animatePhysics, bool allowFlameToggle, string flameParentName, string flameChildName, GameObject carriedTorchVisualPrefab, string carriedTorchAttachBoneName, string flameBoolParam, bool flameStartsActive, bool initializeFlameFromHierarchy, bool useFlameAnimationLayer, float flameUpperBodyIdleLayerWeight, float flameUpperBodyMovingLayerWeight, float flameUpperBodyLayerWeightResponsiveness, float inputLockTime, ForceMode knockbackForceMode, bool ignoreCharacterCollisions, bool ignoreCharacterTriggerColliders, float collisionRefreshInterval, bool useLowFrictionLocomotionMaterial, bool overrideExistingLocomotionMaterial, AudioListener audioListener, bool searchAudioListenerInChildren, bool logDistrictInputDiagnostics

### Assets/Characters/0_Script/SquadCharacterController.Health.cs

Methodes : ApplyDamage, RecordDamageApplied, PlayActionAudio

Champs serialises : 

### Assets/Characters/0_Script/SquadCharacterController.Interactions.cs

Methodes : GetInteractionOriginWorldPosition, RefreshLocalInteractionDetectionForExternalLocomotion, HideLocalInteractionPresentation, UpdateLocalInteractionDetection, RefreshInteractionDetectionCandidates, SelectBestInteractionTarget, TryEvaluateInteractionCandidate, IsInteractionCandidateVisibleFromCamera, ResolveInteractionForward, ResolveInteractionDetectionRadius, ResolveInteractionMaxDistance, IsMuninLightInteractionTarget, GetInteractionCandidateTieBreaker, ApplyLocalInteractionTarget, ClearLocalInteractionTarget, UpdateMuninInteractionReaction, ResolveMuninReactionIntensity, ResolveMuninReactionController, IsLocalControlledCharacter

Champs serialises : bool enableCharacterInteractionDetection, LayerMask interactionDetectionMask, float interactionDetectionRadius, float interactionMinimumForwardDot, float interactionDetectionHeightOffset, bool requireInteractionTargetVisibleByCamera, float interactionCameraVisibilityGraceSeconds, bool enableMuninInteractionReaction, float muninFlameProximityReactionIntensity

### Assets/Characters/0_Script/SquadCharacterController.Lockpick.cs

Methodes : GetStatValue, GetDexterityValue, GetDexterityModifier, CountItemById, HasItemById, CountItem, TryFindInventoryItemById, TryConsumeItemById

Champs serialises : 

### Assets/Characters/0_Script/SquadCharacterController.Sitting.cs

Methodes : TryToggleSitting, TrySetSitting, TrySetSittingImmediate, InitializeSittingState, ResetSittingIdleTimer, UpdateSittingState, CanEnterSitting, CanEnterScriptedSittingImmediate, UpdateStandUpCompletion, IsSittingState, IsState, ReleaseSittingMovementSuppression, CancelSittingState, ValidateSittingSettings

Champs serialises : bool enableIdleSitting, float idleSecondsBeforeSitting, string sittingParam, float standUpMovementUnlockFallback

### Assets/Characters/0_Script/SquadCharacterController.UccLocomotion.cs

Methodes : QueueUccJumpInput, TryForwardMoveToUcc, TryForwardSprintToUcc, TryForwardJumpToUcc, TryForwardStopToUcc, TryToggleUccHeightChange, TryToggleUccFlightMode, TrySetUccFlightInput, TryBeginUccExternalLock, TryBeginUccProgressiveStop, IsUccProgressiveStopComplete, CompleteUccProgressiveStop, EndUccExternalLock, TrySetUccExternalPositionAndRotation, TryTeleportForSceneTransition, ResetUccLocomotionIntent, ClearQueuedUccJumpInput, TryAddImpulseToUcc, TryGetUccGrounded, TryGetUccPlanarVelocity, CanUseLitInteractionsWithUcc, CanUseLitInteractableWithUcc, TryGetActiveUccLocomotionBridge, GetUccLocomotionBridge, GetUccInteractionBridge

Champs serialises : 

### Assets/Characters/6_UCC_Opsive/OpsiveIntegration/LitCombatLockMovementType.cs

Methodes : GetDeltaYawRotation, GetInputVector

Champs serialises : 

### Assets/Characters/6_UCC_Opsive/OpsiveIntegration/LitOpsiveLocomotionBridge.cs

Methodes : ApplyStateMotionVerticalImpulse, HasRecentExplicitWorldMoveInput, OnValidate, SetMoveInput, RefreshLocomotionPresentation, SetSprintModifier, Jump, StopBridgeInput, ToggleHeightChange, ToggleFlightMode, SetFlightMode, SetFlightInput, BeginExternalLock, BeginExternalLockWithProgressiveStop, RequestCombatSkillLanding, ApplyPlanarHandoffDamping, IsMotionHandoffSettled, ShouldBeginMotionHandoff, BeginCombatAirborneHold, EndCombatAirborneHold, MaintainCombatAirborneHold, ClearCombatAirborneHolds, IsProgressiveStopComplete, CompleteProgressiveStop, EndExternalLock, BeginScriptedTraversal, TryBeginScriptedTraversal, EndScriptedTraversal, ReleaseScriptedTraversalAfterFixedStep, CompleteScriptedTraversalRelease, ClearTransientLocksForSceneTransition, RequestRunStartResponse, ApplyScriptedTraversalPose, ApplyStoredScriptedTraversalPose, SetExternalPositionAndRotation, SetCinematicPositionAndRotation, ApplyCinematicRootMotion, SetActionFacingDirection, SetCombatFacingDirection, BeginScriptedPlanarMotion, DriveScriptedPlanarMotion, ApplyScriptedPlanarImpulse, EndScriptedPlanarMotion, TeleportForSceneTransition, AddExternalImpulse, AddExternalImpulseUntilGrounded, Awake, OnEnable, OnDisable, Update, LateUpdate, SetCombatLockTarget, ClearCombatLockTarget, ApplyCombatMovementType, RestoreExplorationMovementType, TryResolveCombatLockMove, BeginDodgeDirectionFacing, EndDodgeDirectionFacing, MaintainCombatLockFacing, SetCombatAnimatorInput, ResetCombatOrbitRadius, LogCombatLockMotion, EnterCombatIdleFromLocomotion, ExitCombatIdleForMovement, RefreshSquadFacadeSystems, EndExternalImpulseLockAfter, EndExternalImpulseLockWhenGrounded, MaintainAirborneImpulseInertia, ForceZeroInput, SuppressGravityForScriptedTraversal, RestoreGravityAfterScriptedTraversal, SuppressGroundingForScriptedTraversal, RestoreGroundingAfterScriptedTraversal, TraceExternalTraversalCorrection, TraceAppliedTraversalPose, DisableAbilitiesForScriptedTraversal, RestoreAbilitiesAfterScriptedTraversal, CaptureAndDisableAbilities, RestoreAbilityEnabledStates, ResolveReferences, EnsureCompanionBridges, EnsureLookSource, AttachLookSourceIfNeeded, ApplyWorldMoveInput, TryApplyRunStartResponse, TraceLocomotionResponse, ResolveOpsiveMoveInput, ResolveLocalMoveInput, SyncSpeedChangeAbility, EnsureFlightAbility, ConfigureFlightAbility, StopFlightAbilityIfActive, ResolveActiveAbilityLabel, UpdateAnimatorParameters, ResolvePlanarVelocity, ResolveLocomotionTier, ResolveSignedTurn, SetGroundedDirectionalAnimatorParameters, ResolveGroundedLocalMoveDirection, RegisterExternalDriver, UnregisterExternalDriver, CacheRigidbodyState, ConfigureRigidbody, EnforceGameplayMotionAuthority, ResolveCurrentAnimationPhase, ResolveAnimationPhase, ConfigureGroundReliefTolerance, RefreshGroundReliefTolerance, ResolveGroundReliefTargets, ResolveMovingGroundReliefBlend, MoveReliefValue, ResolveGroundReliefDeltaTime, RestoreGroundReliefTolerance, RestoreRigidbody, SetAnimatorFloat, SetAnimatorBool, SetAnimatorInteger, SetAnimatorTrigger, HasAnimatorParameter, WarnOnce

Champs serialises : SquadCharacterController squadController, UltimateCharacterLocomotion locomotion, UltimateCharacterLocomotionHandler locomotionHandler, LitOpsivePlayerInput playerInput, LitOpsiveLookSource lookSource, Animator animator, AnimatorMonitor animatorMonitor, PlayerScriptedJumpController scriptedJumpController, CombatTimeDomain timeDomain, bool logScriptedTraversalDiagnostics, int scriptedTraversalDiagnosticTickInterval, float scriptedTraversalExternalCorrectionDistance, float scriptedTraversalExternalCorrectionDegrees, bool driveFromSquadFacade, bool overrideOpsiveHandlerInput, bool orientLookSourceFromMovement, bool configureRigidbodyForOpsive, bool enableRunStartResponse, float runStartVelocityBonus, float runStartResponseCooldown, float runStartResponseMaximumPlanarSpeed, bool logLocomotionResponseDiagnostics, bool autoInstallCompanionBridges, float movementDeadZone, bool relaxGroundReliefTolerance, float groundReliefMinStepHeight, float groundReliefMinSlopeLimit, float groundReliefMinStickToGroundDistance, bool adaptMovingGroundRelief, float movingStepHeight, float movingSlopeLimit, float movingStickToGroundDistance, float idleStickToGroundDistance, float groundReliefAdaptationSpeed, bool enableUccFlight, float flightTakeoffVerticalSpeed, float flightTakeoffDuration, float flightTakeoffDamping, float flightCruiseSpeed, float flightBoostSpeed, float flightAcceleration, float flightBoostAcceleration, float flightDeceleration, float flightVerticalSpeed, float flightVerticalAcceleration, float flightVerticalDeceleration, float flightVerticalDeadZone, float flightIdleSpeedThreshold, float flightTurnRate, float flightBoostTurnRate, float flightLandingSpeed, float flightLandingAcceleration, float combatSkillLandingSpeed, bool allowStandaloneFlightFallback, float fallbackFlightCollisionSkin, float fallbackFlightGroundProbeDistance, bool driveLitLocomotionAnimatorParameters, string speedParam, string horizontalMovementParam, string forwardMovementParam, string isMovingParam, string locomotionTierParam, string combatMoveMagnitudeParam, string turnParam, string flightStateParam, string flightSpeedParam, string flightVerticalParam, string flightBoostParam, string flightStartTriggerParam, float walkPresentationSpeed, float runPresentationSpeed, float combatFacingSpeedDegreesPerSecond, float combatOrbitPureLateralThreshold, float combatOrbitRadiusDeadZone, float combatOrbitRadiusCorrectionGain, float combatOrbitMaximumCorrection, bool logCombatLockMotionDiagnostics

### Assets/Characters/6_UCC_Opsive/OpsiveIntegration/LitOpsiveLocomotionBridge.FlightState.cs

Methodes : RequestFlightMode, ApplyFlightInput, TryStartUccFlight, StartStandaloneFlight, BeginFlightLanding, UpdateFlightMode, UpdateUccFlightMode, FixedUpdate, TickStandaloneFlight, TickStandaloneCruise, RotateStandaloneFlight, MoveStandaloneFlight, IsStandaloneGrounded, TryCapsuleCastStandalone, GetStandaloneCapsuleWorldPoints, ProcessStandaloneVerticalInput, ResolveFlightUp, CompleteFlightMode, RestoreUccAfterStandaloneFlight, ShutdownFlightMode, SetFlightPresentationState, UpdateFlightAnimatorParameters

Champs serialises : 

### Assets/Characters/6_UCC_Opsive/OpsiveIntegration/LitOpsiveLocomotionBridge.GroundBlockDiagnostics.cs

Methodes : SetGroundBlockDiagnosticsEnabled, TickGroundBlockDiagnostics, SetGroundBlockDiagnosticSample, ResetGroundBlockDiagnosticSample, LogGroundBlockDiagnostic, TryFindGroundBlockDiagnosticHit, TryFindGroundBlockDiagnosticOverlap, TryResolveGroundBlockDiagnosticCapsule, ResolveGroundBlockDiagnosticCapsule, IsGroundBlockDiagnosticCollider, FormatGroundBlockDiagnosticHit, FormatGroundBlockDiagnosticCollider, ResolveGroundBlockDiagnosticPath, FormatVector

Champs serialises : bool debugGroundBlockDiagnostics, float groundBlockDiagnosticInterval, float groundBlockDiagnosticMinProgress

### Assets/Characters/6_UCC_Opsive/OpsiveIntegration/LitOpsiveLocomotionBridge.GroundedFeel.cs

Methodes : ConfigureGroundedFeelProfile, RestoreGroundedFeelProfile, ConfigureGroundedSprintSpeedChange, RestoreGroundedSprintSpeedChange, ResolveGroundedFeelWorldMoveInput, ResolveGroundedInputRate, TryUpdateGroundedFeelAnimatorParameters, ResolveGroundedPresentationSpeed, ResolveGroundedPresentationTurn, ShouldGroundedTurnInPlace, TickGroundedPivotTurn, TryStartGroundedPivotTurn, ShouldSnapGroundedStationaryTurn, SnapGroundedStationaryTurn, ResolveGroundedPivotMoveInput, ApplyGroundedPivotRotationCommit, CommitGroundedPivotTargetDirection, UpdateGroundedPresentationState, EnterGroundedStart, EnterGroundedStop, SetGroundedPresentationState, TrackGroundedStopExit, IsGroundedStopStateReadyToSettle, ArmGroundedMoveStartTierOverride, ResolveGroundedLocomotionTier, IsGroundedStartStateActive, IsGroundedStartState, TickGroundedMoveIntentAge, TickGroundedMoveTransitionDirection, LatchGroundedMoveTransitionDirection, TryGetGroundedMoveTransitionLocalDirection, ResolveGroundedMoveTransitionLocalDirection, ResetGroundedFeelState, ResetGroundedFeelInput, ResetGroundedMoveIntentAge, ResetGroundedPivotTurn, ResetGroundedMoveTransitionDirection, ResetGroundedMoveStartTierOverride, ResolveGroundedFeelDeltaTime, RecordLocomotionSample, DumpLocomotionPresentationDiagnostics, ResetAnimatorTrigger, SetLitAnimatorSpeedParameterOverride

Champs serialises : bool enableCinematicGroundedFeel, bool tuneGroundedUccPhysics, Vector3 groundedMotorAcceleration, float groundedMotorDamping, float groundedPreviousAccelerationInfluence, float groundedBackwardsMultiplier, float groundedGravityAmount, float groundedStickToGroundDistance, float groundedSlopeLimit, float groundedMaxStepHeight, float groundedMovingPlatformSeparationVelocity, float groundedMovingPlatformDisconnectMultiplier, float groundedMovingPlatformForceDamping, float groundedInputAcceleration, float groundedSprintInputAcceleration, float groundedInputDeceleration, float groundedDirectionChangeAcceleration, float groundedDirectionChangeDot, bool tuneGroundedSprintSpeedChange, float groundedSprintSpeedMultiplier, float groundedSprintSpeedParameterValue, bool suppressSpeedChangeAnimatorParameter, string moveStartTriggerParam, string moveStopTriggerParam, string turnInPlaceParam, float groundedAnimationSpeedToBlend, float groundedAnimatorSpeedRiseRate, float groundedAnimatorSpeedFallRate, float groundedAnimatorTurnRate, float groundedTurnInPlaceThreshold, float groundedTurnInPlaceMaxSpeed, bool enableGroundedTurnInPlaceClips, float groundedStopTriggerMinSpeed, float groundedStopRequestDelay, float groundedStopSettledSpeed, float groundedStopSettledDuration, float groundedStopExitNormalizedTime, float groundedMoveTransitionDirectionHoldTime, float groundedMoveTransitionParameterSpeed, bool useForwardOnlyGroundedLocomotion, bool enableScriptedPivotTurns, float groundedPivotMinAngle, float groundedPivot180Angle, bool groundedSnapStationaryTurn, float groundedSnapStationaryTurnMinAngle, float groundedSnapStationaryTurnMaxSpeed, float groundedSnapStationaryTurnMaxSmoothedInput, float groundedPivotMaxSpeed, float groundedPivotMaxSmoothedInput, float groundedPivotHoldTime, float groundedPivotCooldown, float groundedPivotStartGraceTime, float groundedPivotStartGraceMinAngle, float groundedPivotMovementReleaseStart, float groundedPivotMovementReleaseMaxAngle, float groundedPivotMovementReleaseScale, bool commitBodyRotationDuringPivot, float groundedPivotRotationCommitRate, bool recordLocomotionDiagnostics

### Assets/Characters/6_UCC_Opsive/OpsiveIntegration/LitOpsiveLocomotionBridge.LocomotionDiagnostics.cs

Methodes : SetLocomotionDiagnosticsEnabled, TickLocomotionDiagnostics, LogLocomotionDiagnostic, ResolveAnimatorClipName, ResolveNextAnimatorClipName, ReadAnimatorFloat, ReadAnimatorBool

Champs serialises : bool debugLocomotionDiagnostics, float locomotionDiagnosticInterval

### Assets/Characters/6_UCC_Opsive/OpsiveIntegration/LitOpsiveLocomotionBridge.ObstacleTraversal.cs

Methodes : ValidateObstacleTraversalSettings, TryStartObstacleTraversal, TryResolveObstacleTraversal, TryFindClosestTraversalHit, TryResolveTraversalSurfaceHeight, HasTraversalLandingClearance, HasTraversalHeadClearance, ResolveObstacleTraversalLandingFootY, ResolveObstacleTraversalFootY, ResolveObstacleTraversalMask, ResolveObstacleTraversalIgnoreHeight, IsValidObstacleTraversalCollider, IsObstacleTraversalInteractiveBlocker, IsOwnObstacleTraversalCollider, ObstacleTraversalRoutine, ResolveObstacleTraversalPosition, ResolveObstacleTraversalDuration, ResolveObstacleTraversalArcHeight, ResolveObstacleTraversalRotation, EaseObstacleTraversalTime, CancelObstacleTraversal

Champs serialises : bool enableObstacleTraversal, float ignoredObstacleMaxHeight, float traversableObstacleMaxHeight, float obstacleProbeDistance, float obstacleProbeRadius, float obstacleProbeBaseHeight, float obstacleTraversalMaxSurfaceUpDot, float obstacleLandingDistance, float obstacleTraversalDuration, float obstacleTraversalArcHeight, float obstacleTraversalTopClearance, float obstacleTraversalHeightArcMultiplier, float obstacleTraversalRotationLead, float obstacleTraversalMinInputMagnitude, float obstacleTraversalCooldown, LayerMask obstacleTraversalMask, string obstacleTraversalTriggerParam

### Assets/Characters/6_UCC_Opsive/OpsiveIntegration/LitOpsiveLocomotionBridge.OrientationFeel.cs

Methodes : ValidateOrientationFeelSettings, ResetOrientationFeelState, ForceOrientationLookDirection, ResolveOrientationLookDirection, ResolveVelocityAssistedOrientationTarget, ResolveOrientationTurnRate, ResolveFallbackOrientationDirection, ResolveOrientationDeltaTime

Champs serialises : bool enableCinematicOrientationFeel, float orientationInputDeadZone, float orientationWalkTurnRate, float orientationSprintTurnRate, float orientationSharpTurnRate, float orientationSharpTurnAngle, float orientationVelocityBlend

### Assets/Characters/6_UCC_Opsive/OpsiveIntegration/LitOpsiveLookSource.cs

Methodes : AttachToCharacter, IsAttachedToCharacter, QueueAttachRetries, SetPlanarLookDirection, LookPosition, LookDirection, Awake, OnEnable, Start, FixedUpdate, Update, LateUpdate, OnApplicationFocus, OnTransformParentChanged, OnDisable, ResolveDefaults, ResolveReportedLookDirection, EnsureStableWorldPlanarDirection, ApplyYawOffset, RefreshStableWorldRotation, HasGameplayCameraLookSource

Champs serialises : GameObject eventTarget, Transform lookTransform, float lookDirectionDistance, float fallbackLookHeight, bool useStableWorldPlanarDirection, float planarDirectionYawOffset

### Assets/Characters/6_UCC_Opsive/OpsiveIntegration/LitOpsivePlayerInput.cs

Methodes : SetMovementOverride, SetSprintOverride, PulseButton, OnEnable, OnDisable, LateUpdate, GetAxisInternal, GetAxisRawInternal, GetButtonInternal, GetButtonDownInternal, GetButtonUpInternal, GetLookVector, ResolveMovement, OnJump, OnLocomotionMode, ShouldLetFacadeHandleLocomotionMode, SetHeld, Matches, IsCurrentFrame, ClearOldButtonFrames, Normalize

Champs serialises : bool fallbackToLocalInputRouter

### Assets/Characters/6_UCC_Opsive/OpsiveIntegration/LitSimpleUccNpcPatrol.cs

Methodes : Reset, Awake, OnEnable, OnDisable, OnValidate, Update, ResumePatrol, PausePatrol, RestartPatrol, SkipToNextPatrolPoint, FinishInteractionAndResume, FinishInteractionWithoutResume, TryHandleLocalInteract, CanBeDetectedBy, GetInteractionDetectionCollider, GetInteractionAnchor, GetInteractionMaxDistance, GetInteractionPriority, SetDetectedCharacter, TickPatrol, BeginWait, AdvancePatrolPoint, TryGetCurrentPoint, StartInteraction, FinishInteractionAfterDelay, FinishInteraction, ResumePatrolAfterInteraction, UpdateInteractionFacing, IsDetectedCharacterInRange, SetPatrolCommand, ClearPatrolCommand, StartPatrolAbility, EnsurePatrolAbility, ResolveReferences, EnsureInteractionTrigger, ConfigureInteractionTrigger, PlayAnimatorCue, SetAnimatorMoving, OnDrawGizmosSelected, SetCommand, AbilityStopped, ResolveLocalInputVector, ResolveDeltaRotation, ResolveUp

Champs serialises : bool startOnEnable, bool loop, float moveInput, float arrivalDistance, float turnSpeed, Animator animator, float animatorCrossFadeDuration, string movingBoolParameter, string stoppedBoolParameter, bool canInteract, float interactionMaxDistance, int interactionPriority, bool faceInteractor, bool resumePatrolAfterInteraction, bool autoFinishInteraction, float interactionDuration, string interactionAnimatorState, string interactionAnimatorTrigger, UnityEvent onInteractionStarted, UnityEvent onInteractionFinished, SphereCollider interactionTrigger, float interactionRadius, Vector3 interactionCenter

### Assets/Characters/6_UCC_Opsive/OpsiveIntegration/LitUccCameraCharacterBinder.cs

Methodes : BeginTimelineControl, BeginExternalCameraControl, EndTimelineControl, EndExternalCameraControl, Reset, Awake, OnEnable, Start, OnDisable, OnLocalCharacterChanged, OnCameraRecenter, QueueBind, BindWhenCharacterAvailable, ResolveCharacter, TryBind, SetCameraCharacter, SetInitCharacterOnAwake, IsCameraBoundAndInitialized, SnapCameraToBoundCharacter, IsValidCharacter, ResolveCameraController, FindTaggedPlayer

Champs serialises : UccCameraController cameraController, bool bindOnEnable, bool subscribeToLocalPlayerContext, bool subscribeToCameraRecenter, bool snapCameraOnBind, float retryInterval

### Assets/Characters/6_UCC_Opsive/OpsiveIntegration/LitUccDamageBridge.cs

Methodes : Awake, OnEnable, Start, OnDisable, TryApplyDamageToAuthority, TrySetAuthorityHealth, TrySetAuthorityCurrentHealth, TrySetAuthorityMaxHealth, NotifyDamageApplied, ResolveReferences, ConfigureCharacterHealthAuthority, CanUseCharacterHealthAuthority, ResolveHealthAttribute, CacheCombatHealth, CacheCharacterHealth, OnCombatHealthChanged, SubscribeCharacterHealthEvents, UnsubscribeCharacterHealthEvents, OnCharacterHealthDamage, OnCharacterHealthHeal, OnCharacterHealthDeath, SubscribeSquadHealth, UnsubscribeSquadHealth, OnSquadHealthChanged, SyncSquadHealthFromCharacterHealth, MirrorLitHealthToOpsiveAttributes, ResolveDamageSourceLabel, ToLitHealthValue, ResolveDamagePosition, ResolveDamageForce

Champs serialises : UltimateCharacterLocomotion locomotion, SquadCharacterController squadController, CharacterInfo combatHealth, CharacterAttributeManager attributeManager, CharacterHealth characterHealth, string healthAttributeName, bool characterHealthIsAuthority, bool syncSquadHealthFromCharacterHealth, bool mirrorLitHealthToOpsiveAttributes, bool raiseOpsiveDamageEvent, bool raiseOpsiveDeathEvent, bool startImpactKnockBackOnDamage, int impactKnockBackResponseId, float defaultForceMagnitude

### Assets/Characters/6_UCC_Opsive/OpsiveIntegration/LitUccFlightAbility.cs

Methodes : Configure, SetInput, RequestLanding, CancelLanding, ShouldStopActiveAbility, ShouldBlockAbilityStart, AbilityStarted, Update, UpdateRotation, UpdateDesiredMovement, AbilityStopped, TickFlight, TickLanding, ProcessVerticalInput, ResolveUp

Champs serialises : 

### Assets/Characters/6_UCC_Opsive/OpsiveIntegration/LitUccFollowerBridge.cs

Methodes : TryTeleport, Awake, ResolveReferences

Champs serialises : LitOpsiveLocomotionBridge locomotionBridge

### Assets/Characters/6_UCC_Opsive/OpsiveIntegration/LitUccInteractionBridge.cs

Methodes : CanUseLitInteractable, Awake, ResolveReferences, HasBlockingActiveAbility, HasBlockingActiveItemAbility, IsAllowedConcurrentAbility

Champs serialises : LitOpsiveLocomotionBridge locomotionBridge, UltimateCharacterLocomotion locomotion, bool requireGroundedForLitInteractions, bool allowWhileHeightChange, bool allowWhileSpeedChange, bool allowWhileFall, bool allowWhileJump, bool blockWhileItemAbilityActive

### Assets/Characters/6_UCC_Opsive/OpsiveIntegration/MotionHandoffProfile.cs

Methodes : CreateActionDefault

Champs serialises : 

### Assets/Characters/6_UCC_Opsive/OpsiveIntegration/PlayerScriptedJumpController.cs

Methodes : SetTargetJumpHeight, Awake, OnEnable, OnDisable, TryStartJump, Update, FixedUpdate, OnGroundedChanged, OnLanded, RequestLanding, IsLandingComplete, HasLandingAnimationReachedExit, IsLandingExitStateReady, FinishLanding, ResetPresentation, RestoreGravity, CalculateTakeoffSpeed, HasReachedJumpStartTakeoffTime, HasReachedTakeoffTime, ApplyTakeoffImpulse, ResolveReferences, SetPhase, HasParameter, SetBool, SetInteger, SetTrigger, ResetTrigger

Champs serialises : UltimateCharacterLocomotion locomotion, LitOpsiveLocomotionBridge locomotionBridge, Animator animator, float jumpHeight, float apexGravityEntryVelocity, float apexGravityMultiplier, float descentGravityMultiplier, float fallingAnimationEntryVelocity, float hardLandingHeight, MotionHandoffProfile landingHandoff

### Assets/CombatRealTime/Actors/PlayerAnimationController.cs

Methodes : OnDisable, Reset, Awake, LateUpdate, OnValidate, Configure, ValidateContract, SetActorPose, ResetAnimationRootPose, BeginCinematicMotion, SetCinematicRootMotionRelayEnabled, EnableRootMotionRelay, EndCinematicMotion, ApplyAnimationDelta, ResolveReferences, LogDevelopmentContractDiagnostic, ValidateRequiredEnemyStates, ValidateAnimatorState

Champs serialises : Transform animationRoot, Animator animator, Transform lockPoint

### Assets/CombatRealTime/Actors/PlayerRootMotionRelay.cs

Methodes : Awake, OnValidate, OnAnimatorMove, LogDevelopmentContractDiagnostic

Champs serialises : PlayerAnimationController actor

### Assets/CombatRealTime/AnimationEvents/PlayerCombatAnimationEvents.cs

Methodes : QTE, Reset, Awake, OnDisable, OnEnable, ResolveLightSkillImpact, ResolveCounterSkillImpact, ResolveCinematicSkillImpact, InstantiateSkillVFX, InstantiateSkillVFXAtIndex, ShowBow, HideBow, ShowSword, HideSword, HideSwordWhenComboEnds, Dash, StopDash, PlaySkillVfxCue, PlaySkillVfxCueAudio, PlayProjectileSkillVfx, HitEnemy, ResolveSkillImpact, ResolveSkillImpactAndRetreat, ResolveEnemy, ResolveEnemySkills, ResolveSelectedSkill, ResolvePlayerBow, ResolvePlayerSword, BindPlayerActionPresentation, UnbindPlayerActionPresentation, HideEquippedWeapons, TryResolveSelectedSkillImpact, HideSwordAfterComboEnds, StopDashRoutine

Champs serialises : EnemyController enemy, EnemyController enemySkills, SkillsManager skillsManager, RealTimeCombatInput combatInput, PlayerBow playerBow, PlayerSword playerSword, Transform inputPromptAnchor, Vector3 inputPromptOffset, float dashOvershootDistance, float dashImpulsePerMeter, float minimumDashImpulse, float maximumDashImpulse, float dashInputLockSeconds, float stopDashDuration, float stopDashDeceleration

### Assets/CombatRealTime/Mobility/PlayerScriptedDodgeController.cs

Methodes : TryStartDodge, CancelDodge, OnDisable, RunDodge, EndDodge, ShouldAlignToTravel

Champs serialises : 

### Assets/CombatRealTime/Mobility/PlayerStateMotionController.cs

Methodes : RefreshLandingLock, IsLandingState, SetActionPolicy, Awake, TryEvaluateMotion, Cancel, OnDisable

Champs serialises : PlayerStateMotionLibrary library

### Assets/CombatRealTime/Mobility/PlayerStateMotionLibrary.cs

Methodes : Position, Find

Champs serialises : 

### Assets/CombatRealTime/Presentation/PlayerActionPresentationController.cs

Methodes : ToggleDiagnostics, ResolveReferences, SetActionFacingTarget, ClearActionFacingTarget, CancelActionForMobility, TryPlaySkill, TryPlayCombatState, WaitForChainWindow, CancelAction, LockDeathAnimation, ClearDeathAnimationLock, ResumeLocomotionFromCinematic, Awake, OnDisable, BeginTargetLunge, CancelTargetLunge, RunTargetLunge, LateUpdate, TryPlay, StartAction, TrackAction, IsActiveState, FaceActionTarget, FaceVisualRig, ResumeLocomotion, WaitForMotionHandoff, FinishUnexpectedActionExit, StartBufferedAction, FinishWithoutTransition, RequestLocomotionHandoff, RequestAirborneLandingIfDue, ReleaseAirborneHold, ResolveLocomotionDestination, ResolveCurrentLocomotionDestination, KeepDeathAnimationActive, Trace

Champs serialises : Animator animator, LitOpsiveLocomotionBridge locomotionBridge, bool debugTransitions

## Resultat de la migration

| Methodes / donnees | Destination | Arbitrage |
|---|---|---|
| Begin/Drive/ApplyImpulse/EndScriptedPlanarMotion | LitOpsiveLocomotionBridge | Proprietaire obligatoire; consommateurs manager, lunge, esquive et trajectoires migres |
| ApplyAnimationDelta, SetActorPose, ShouldConsumeAnimatorRootMotion | PlayerAnimationController | Conserver chemin joueur; retirer branches EnemyController et validation des etats ennemis |
| 16 signatures publiques d evenements de clips | PlayerCombatAnimationEvents | Conserver comme routeur; aucune application directe d impact ou de mouvement |
| Armes, effets, impacts et nettoyage des evenements | PlayerActionPresentationController.SkillEvents | Fusionner dans le service existant; handlers renommes pour eviter la double reception Unity |
| Dash, StopDash, StopDashRoutine | CombatMobilityController.AnimationDash | Deplacer dans la mobilite; capturer le personnage du dash et annuler seulement son operation |
| TestFear/Anger/Laugh/Surprise/Smirk/Suspicious/HalfSmile, ResetIdle, PrintAvailableBlendShapes, ValidatePresets | FacialExpressionControllerEditor | Retirer le composant de debug et garder les commandes dans l editeur |
| Cinq options de rotation instantanee/arret sans consommateur | Supprimees | Champs abandones confirmes dans le code; controles editeur correspondants retires |
| Parametres de saut, esquive, locomotion, interactions, equipement, visage, pas, trajectoires | CharacterData.playerSettings | Copies runtime par module; aucune modification de l asset source |
| Reglages de sante et synchronisation | LitUccDamageBridge | Autorite conservee; abonnement HealthChanged rendu idempotent |

Les services de narration, de voix, de SpiritBond et les composants Opsive restent independants. Les donnees deja portees par leurs ressources ne sont pas dupliquees. La calibration locale de machoire reste sur le composant avec sa pose osseuse.

Validation : compilation C# runtime/editeur reussie; tests ajoutes et compiles, execution Unity encore requise. Voir Assets/Characters/PlayerModules/README.md.
