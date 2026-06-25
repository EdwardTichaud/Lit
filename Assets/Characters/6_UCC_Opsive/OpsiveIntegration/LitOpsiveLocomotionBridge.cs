using System.Collections;
using Opsive.Shared.Events;
using Opsive.UltimateCharacterController.Character;
using Opsive.UltimateCharacterController.Character.Abilities;
using Opsive.UltimateCharacterController.Character.Abilities.Items;
using UnityEngine;

// Runtime bridge between Lit's gameplay facade and Opsive UCC locomotion.
[RequireComponent(typeof(UltimateCharacterLocomotion))]
[RequireComponent(typeof(UltimateCharacterLocomotionHandler))]
[RequireComponent(typeof(LitOpsivePlayerInput))]
public partial class LitOpsiveLocomotionBridge : MonoBehaviour
{
    private const string JumpInputName = "Jump";
    private const string SpeedChangeInputName = "Change Speeds";
    private const string CrouchInputName = "Crouch";

    [SerializeField] private SquadCharacterController squadController;
    [SerializeField] private UltimateCharacterLocomotion locomotion;
    [SerializeField] private UltimateCharacterLocomotionHandler locomotionHandler;
    [SerializeField] private LitOpsivePlayerInput playerInput;
    [SerializeField] private LitOpsiveLookSource lookSource;
    [SerializeField] private Animator animator;
    [SerializeField] private AnimatorMonitor animatorMonitor;

    [Header("Bridge")]
    [SerializeField, Tooltip("When enabled, SquadCharacterController forwards locomotion commands to UCC and keeps Lit simulation disabled.")]
    private bool driveFromSquadFacade = true;
    [SerializeField, Tooltip("Feed movement directly into UltimateCharacterLocomotionHandler.OverrideInput.")]
    private bool overrideOpsiveHandlerInput = true;
    [SerializeField, Tooltip("Rotate the local look source toward world-space movement. Does not modify the project camera.")]
    private bool orientLookSourceFromMovement = true;
    [SerializeField, Tooltip("Configure the Rigidbody as kinematic/no-gravity while UCC is active.")]
    private bool configureRigidbodyForOpsive = true;
    [SerializeField, Tooltip("Add Lit/UCC companion bridges at runtime so interaction, damage and follower systems can respect UCC state without prefab edits.")]
    private bool autoInstallCompanionBridges = true;
    [SerializeField, Range(0f, 0.5f)] private float movementDeadZone = 0.08f;
    [SerializeField, Min(0f), Tooltip("Keeps a rejected jump request alive briefly while UCC updates grounded/ability state.")]
    private float jumpRetryWindow = 0.15f;
    [SerializeField, Tooltip("Log a diagnostic warning when UCC keeps rejecting a jump request.")]
    private bool warnWhenJumpRejected = true;
    [SerializeField, Tooltip("Apply a direct UCC force if the migrated Jump ability is missing or refuses a grounded jump request.")]
    private bool useJumpForceFallback = true;
    [SerializeField, Min(0f), Tooltip("Velocity-style upward force used by the Lit fallback jump path.")]
    private float jumpFallbackVelocity = 7f;

    [Header("Flight")]
    [SerializeField, Tooltip("Restores the pre-UCC LocomotionMode flight toggle through a lightweight UCC ability.")]
    private bool enableUccFlight = true;
    [SerializeField, Min(0f)] private float flightTakeoffVerticalSpeed = 6.5f;
    [SerializeField, Min(0f)] private float flightTakeoffDuration = 0.45f;
    [SerializeField, Min(0f)] private float flightTakeoffDamping = 16f;
    [SerializeField, Min(0f)] private float flightCruiseSpeed = 33f;
    [SerializeField, Min(0f)] private float flightBoostSpeed = 81f;
    [SerializeField, Min(0f)] private float flightAcceleration = 54f;
    [SerializeField, Min(0f)] private float flightBoostAcceleration = 126f;
    [SerializeField, Min(0f)] private float flightDeceleration = 36f;
    [SerializeField, Min(0f)] private float flightVerticalSpeed = 24f;
    [SerializeField, Min(0f)] private float flightVerticalAcceleration = 66f;
    [SerializeField, Min(0f)] private float flightVerticalDeceleration = 54f;
    [SerializeField, Range(0f, 0.4f)] private float flightVerticalDeadZone = 0.05f;
    [SerializeField, Min(0f)] private float flightIdleSpeedThreshold = 0.08f;
    [SerializeField, Min(0f)] private float flightTurnRate = 760f;
    [SerializeField, Min(0f)] private float flightBoostTurnRate = 460f;
    [SerializeField, Min(0f)] private float flightLandingSpeed = 12f;
    [SerializeField, Min(0f)] private float flightLandingAcceleration = 36f;
    [SerializeField, Tooltip("Utilise un pilote autonome si la capacite de vol UCC est absente ou refuse de demarrer.")]
    private bool allowStandaloneFlightFallback = true;
    [SerializeField, Min(0f)] private float fallbackFlightCollisionSkin = 0.03f;
    [SerializeField, Min(0f)] private float fallbackFlightGroundProbeDistance = 0.2f;

    [Header("Animator Compatibility")]
    [SerializeField, Tooltip("Only enable for legacy Lit animator controllers. UCC animator controllers should be driven by AnimatorMonitor parameters.")]
    private bool driveLitLocomotionAnimatorParameters = false;
    [SerializeField] private string speedParam = "Speed";
    [SerializeField] private string isMovingParam = "IsMoving";
    [SerializeField] private string locomotionTierParam = "LocomotionTier";
    [SerializeField] private string turnParam = "Turn";
    [SerializeField] private string flightStateParam = "FlightState";
    [SerializeField] private string flightSpeedParam = "FlightSpeed";
    [SerializeField] private string flightVerticalParam = "FlightVertical";
    [SerializeField] private string flightBoostParam = "FlightBoost";
    [SerializeField] private string flightStartTriggerParam = "FlightStartTrigger";
    [SerializeField] private float walkPresentationSpeed = 1.35f;
    [SerializeField] private float runPresentationSpeed = 3.25f;

    private Rigidbody rb;
    private bool externalDriverRegistered;
    private bool previousUseGravity;
    private bool previousIsKinematic;
    private CollisionDetectionMode previousCollisionMode;
    private Vector2 currentWorldMoveInput;
    private bool sprintPressed;
    private bool warnedMissingJump;
    private bool warnedMissingSpeedChange;
    private bool warnedMissingHeightChange;
    private bool warnedJumpRejected;
    private bool warnedFlightStartRejected;
    private bool hasPendingJump;
    private float pendingJumpUntil;
    private Vector2 pendingJumpWorldInput;
    private bool pendingJumpHasWorldInput;
    private LitUccFlightAbility flightAbility;
    private Vector3 lastPlanarDirection = Vector3.forward;
    private Vector3 lastPosition;
    private bool hasLastPosition;
    private int scriptedTraversalLockCount;
    private bool scriptedTraversalInputDisabled;
    private Coroutine externalImpulseLockRoutine;
    private Ability[] scriptedTraversalAbilities;
    private bool[] scriptedTraversalAbilityEnabledStates;
    private ItemAbility[] scriptedTraversalItemAbilities;
    private bool[] scriptedTraversalItemAbilityEnabledStates;
    private bool previousLocomotionUseGravity;
    private bool scriptedTraversalGravityPrepared;
    private int externalLockCount;
    private bool externalLockInputDisabled;

    public bool IsDriving => isActiveAndEnabled && driveFromSquadFacade && locomotion != null && locomotionHandler != null;
    public bool IsScriptedTraversalActive => scriptedTraversalLockCount > 0;
    public bool IsExternalLockActive => externalLockCount > 0;
    public bool IsInputSuppressedByUcc => IsScriptedTraversalActive || IsExternalLockActive;
    public bool IsFlightActive => IsFlightModeActive;
    public bool Grounded => locomotion != null && locomotion.Grounded;
    public Vector3 Velocity => locomotion != null ? locomotion.Velocity : Vector3.zero;
    public Vector3 PlanarVelocity => Vector3.ProjectOnPlane(Velocity, transform.up);
    public Vector3 WorldPosition => locomotion != null ? locomotion.transform.position : Vector3.zero;
    public bool CanDriveScriptedTraversal
    {
        get
        {
            ResolveReferences();
            return isActiveAndEnabled && locomotion != null;
        }
    }

    public void SetMoveInput(Vector2 input, bool isWorldSpace)
    {
        ResolveReferences();

        if (IsInputSuppressedByUcc)
        {
            ApplyWorldMoveInput(Vector2.zero);
            return;
        }

        Vector2 worldInput = isWorldSpace || squadController == null ? input : squadController.GetWorldSpaceInput(input);
        ApplyWorldMoveInput(worldInput);
    }

    public void SetSprintModifier(bool pressed)
    {
        ResolveReferences();

        if (IsInputSuppressedByUcc)
        {
            pressed = false;
        }

        sprintPressed = pressed;
        if (playerInput != null)
        {
            playerInput.SetSprintOverride(pressed, IsDriving);
        }

        if (!IsFlightModeActive)
        {
            SyncSpeedChangeAbility();
        }
    }

    public bool Jump(Vector2 worldInput, bool hasWorldInput)
    {
        ResolveReferences();

        if (!IsDriving)
        {
            return false;
        }

        if (IsInputSuppressedByUcc)
        {
            ClearPendingJump();
            return false;
        }

        if (IsFlightActive)
        {
            return false;
        }

        if (hasWorldInput && worldInput.sqrMagnitude > movementDeadZone * movementDeadZone)
        {
            ApplyWorldMoveInput(worldInput);
        }

        if (playerInput != null)
        {
            playerInput.PulseButton(JumpInputName);
        }

        Jump jump = locomotion != null ? locomotion.GetAbility<Jump>() : null;
        if (jump == null)
        {
            if (TryApplyJumpForceFallback(worldInput, hasWorldInput))
            {
                return true;
            }

            WarnOnce(ref warnedMissingJump, "UCC Jump ability is missing on this character. Add standard movement abilities with the Lit/Opsive UCC migration tool or Opsive Character Manager.");
            ClearPendingJump();
            return false;
        }

        if (TryStartJumpAbility(jump))
        {
            return true;
        }

        if (TryApplyJumpForceFallback(worldInput, hasWorldInput))
        {
            return true;
        }

        QueuePendingJump(worldInput, hasWorldInput);
        return true;
    }

    public void StopBridgeInput()
    {
        ApplyWorldMoveInput(Vector2.zero);
        SetSprintModifier(false);
        ShutdownFlightMode();
    }

    public bool ToggleHeightChange()
    {
        ResolveReferences();

        if (!IsDriving || IsInputSuppressedByUcc)
        {
            return false;
        }

        HeightChange heightChange = locomotion != null ? locomotion.GetAbility<HeightChange>() : null;
        if (heightChange == null)
        {
            WarnOnce(ref warnedMissingHeightChange, "UCC HeightChange ability is missing on this character. LocomotionMode/Crouch input will be ignored until the ability is added.");
            return false;
        }

        if (heightChange.IsActive)
        {
            return locomotion.TryStopAbility(heightChange);
        }

        if (playerInput != null)
        {
            playerInput.PulseButton(CrouchInputName);
        }

        return locomotion.TryStartAbility(heightChange);
    }

    public bool ToggleFlightMode(float verticalInput)
    {
        bool shouldActivate = !IsFlightModeActive ||
                              flightPresentationState == FlightPresentationState.Landing;
        return SetFlightMode(shouldActivate, verticalInput);
    }

    public bool SetFlightMode(bool active, float verticalInput)
    {
        return RequestFlightMode(active, verticalInput);
    }

    public bool SetFlightInput(Vector2 worldInput, bool boost, float verticalInput)
    {
        return ApplyFlightInput(worldInput, boost, verticalInput);
    }

    public bool BeginExternalLock(bool disableGameplayInput = true, bool stopActiveAbilities = false)
    {
        ResolveReferences();
        if (!CanDriveScriptedTraversal)
        {
            return false;
        }

        externalLockCount = Mathf.Max(0, externalLockCount) + 1;
        if (externalLockCount > 1)
        {
            ForceZeroInput();
            return true;
        }

        ClearPendingJump();
        if (stopActiveAbilities && locomotion != null)
        {
            locomotion.StopAllAbilities(false);
        }

        ForceZeroInput();
        if (disableGameplayInput)
        {
            EventHandler.ExecuteEvent<bool>(gameObject, "OnEnableGameplayInput", false);
            externalLockInputDisabled = true;
        }

        return true;
    }

    public void EndExternalLock()
    {
        if (externalLockCount <= 0)
        {
            externalLockCount = 0;
            return;
        }

        externalLockCount--;
        if (externalLockCount > 0)
        {
            ForceZeroInput();
            return;
        }

        if (externalLockInputDisabled)
        {
            if (!scriptedTraversalInputDisabled)
            {
                EventHandler.ExecuteEvent<bool>(gameObject, "OnEnableGameplayInput", true);
            }

            externalLockInputDisabled = false;
        }

        ForceZeroInput();
        AttachLookSourceIfNeeded(true);
    }

    public bool BeginScriptedTraversal()
    {
        ResolveReferences();
        if (!CanDriveScriptedTraversal)
        {
            return false;
        }

        scriptedTraversalLockCount = Mathf.Max(0, scriptedTraversalLockCount) + 1;
        if (scriptedTraversalLockCount > 1)
        {
            ForceZeroInput();
            return true;
        }

        ClearPendingJump();
        if (locomotion != null)
        {
            locomotion.StopAllAbilities(false);
        }

        SuppressGravityForScriptedTraversal();
        DisableAbilitiesForScriptedTraversal();
        ForceZeroInput();
        EventHandler.ExecuteEvent<bool>(gameObject, "OnEnableGameplayInput", false);
        scriptedTraversalInputDisabled = true;
        return true;
    }

    public void EndScriptedTraversal()
    {
        if (scriptedTraversalLockCount <= 0)
        {
            scriptedTraversalLockCount = 0;
            return;
        }

        scriptedTraversalLockCount--;
        if (scriptedTraversalLockCount > 0)
        {
            ForceZeroInput();
            return;
        }

        RestoreAbilitiesAfterScriptedTraversal();
        RestoreGravityAfterScriptedTraversal();
        if (scriptedTraversalInputDisabled)
        {
            if (!externalLockInputDisabled)
            {
                EventHandler.ExecuteEvent<bool>(gameObject, "OnEnableGameplayInput", true);
            }

            scriptedTraversalInputDisabled = false;
        }

        ForceZeroInput();
        AttachLookSourceIfNeeded(true);
    }

    public void ApplyScriptedTraversalPose(Vector3 position, Quaternion rotation)
    {
        ResolveReferences();
        if (locomotion == null)
        {
            return;
        }

        locomotion.SetPositionAndRotation(position, rotation, false, false);
        lastPosition = position;
        hasLastPosition = true;
    }

    public bool SetExternalPositionAndRotation(Vector3 position, Quaternion rotation, bool stopActiveAbilities)
    {
        ResolveReferences();
        if (!IsDriving || locomotion == null)
        {
            return false;
        }

        locomotion.SetPositionAndRotation(position, rotation, false, stopActiveAbilities);
        lastPosition = position;
        hasLastPosition = true;
        ForceZeroInput();
        return true;
    }

    public bool AddExternalImpulse(Vector3 worldImpulse, ForceMode forceMode, float lockInputForSeconds)
    {
        ResolveReferences();
        if (!IsDriving || locomotion == null || worldImpulse.sqrMagnitude <= 0f)
        {
            return false;
        }

        bool scaleByMass = forceMode != ForceMode.VelocityChange && forceMode != ForceMode.Acceleration;
        locomotion.AddForce(worldImpulse, 1, scaleByMass);

        if (lockInputForSeconds > 0f)
        {
            BeginExternalLock(disableGameplayInput: false, stopActiveAbilities: false);
            if (externalImpulseLockRoutine != null)
            {
                StopCoroutine(externalImpulseLockRoutine);
                EndExternalLock();
            }

            externalImpulseLockRoutine = StartCoroutine(EndExternalImpulseLockAfter(lockInputForSeconds));
        }

        return true;
    }

    private void Awake()
    {
        ResolveReferences();
        EnsureCompanionBridges();
        CacheRigidbodyState();
        lastPosition = transform.position;
        hasLastPosition = true;
    }

    private void OnEnable()
    {
        ResolveReferences();
        EnsureCompanionBridges();
        CacheRigidbodyState();
        ConfigureRigidbody();
        RegisterExternalDriver();
        AttachLookSourceIfNeeded(true);
        ApplyWorldMoveInput(currentWorldMoveInput);
    }

    private void OnDisable()
    {
        CancelObstacleTraversal();
        RestoreAbilitiesAfterScriptedTraversal();
        RestoreGravityAfterScriptedTraversal();
        if (externalLockInputDisabled || scriptedTraversalInputDisabled)
        {
            EventHandler.ExecuteEvent<bool>(gameObject, "OnEnableGameplayInput", true);
            externalLockInputDisabled = false;
            scriptedTraversalInputDisabled = false;
        }

        if (externalImpulseLockRoutine != null)
        {
            StopCoroutine(externalImpulseLockRoutine);
            externalImpulseLockRoutine = null;
        }

        externalLockCount = 0;
        scriptedTraversalLockCount = 0;
        StopBridgeInput();
        UnregisterExternalDriver();
        RestoreRigidbody();
    }

    private void Update()
    {
        if (!IsDriving && !IsInputSuppressedByUcc)
        {
            return;
        }

        if (IsInputSuppressedByUcc)
        {
            ForceZeroInput();
            RefreshSquadFacadeSystems();
            return;
        }

        if (TryStartObstacleTraversal())
        {
            RefreshSquadFacadeSystems();
            return;
        }

        if (!IsFlightModeActive)
        {
            SyncSpeedChangeAbility();
        }
        AttachLookSourceIfNeeded(false);
        RetryPendingJump();
        UpdateFlightMode();
        UpdateAnimatorParameters();
        RefreshSquadFacadeSystems();
    }

    private void RefreshSquadFacadeSystems()
    {
        if (squadController != null)
        {
            squadController.RefreshAudioListenerStateForExternalLocomotion();
            if (squadController.IsCharacterFlameSystemEnabled)
            {
                squadController.TickFlameLifetimeForExternalLocomotion(Time.deltaTime);
            }
            squadController.RefreshLocalInteractionDetectionForExternalLocomotion();
        }
    }

    private IEnumerator EndExternalImpulseLockAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        externalImpulseLockRoutine = null;
        EndExternalLock();
    }

    private void ForceZeroInput()
    {
        currentWorldMoveInput = Vector2.zero;
        sprintPressed = false;

        if (playerInput != null)
        {
            playerInput.SetMovementOverride(Vector2.zero, IsDriving || IsInputSuppressedByUcc);
            playerInput.SetSprintOverride(false, IsDriving || IsInputSuppressedByUcc);
        }

        if (flightAbility != null)
        {
            flightAbility.SetInput(Vector2.zero, false, 0f);
        }

        if (overrideOpsiveHandlerInput && locomotionHandler != null)
        {
            locomotionHandler.OverrideInput = IsDriving || IsInputSuppressedByUcc;
            locomotionHandler.OverriddenHorizontalMovement = 0f;
            locomotionHandler.OverriddenForwardMovement = 0f;
            locomotionHandler.OverriddenLookVector = Vector2.zero;
        }

        SpeedChange speedChange = locomotion != null ? locomotion.GetAbility<SpeedChange>() : null;
        if (speedChange != null && speedChange.IsActive)
        {
            locomotion.TryStopAbility(speedChange);
        }
    }

    private void SuppressGravityForScriptedTraversal()
    {
        if (locomotion == null)
        {
            return;
        }

        previousLocomotionUseGravity = locomotion.UseGravity;
        locomotion.UseGravity = false;
        locomotion.GravityAccumulation = 0f;
        scriptedTraversalGravityPrepared = true;
    }

    private void RestoreGravityAfterScriptedTraversal()
    {
        if (!scriptedTraversalGravityPrepared)
        {
            return;
        }

        if (locomotion != null)
        {
            locomotion.UseGravity = previousLocomotionUseGravity;
            locomotion.GravityAccumulation = 0f;
        }

        scriptedTraversalGravityPrepared = false;
    }

    private void DisableAbilitiesForScriptedTraversal()
    {
        scriptedTraversalAbilities = locomotion != null ? locomotion.Abilities : null;
        scriptedTraversalAbilityEnabledStates = CaptureAndDisableAbilities(scriptedTraversalAbilities);
        scriptedTraversalItemAbilities = locomotion != null ? locomotion.ItemAbilities : null;
        scriptedTraversalItemAbilityEnabledStates = CaptureAndDisableAbilities(scriptedTraversalItemAbilities);
    }

    private void RestoreAbilitiesAfterScriptedTraversal()
    {
        RestoreAbilityEnabledStates(scriptedTraversalAbilities, scriptedTraversalAbilityEnabledStates);
        RestoreAbilityEnabledStates(scriptedTraversalItemAbilities, scriptedTraversalItemAbilityEnabledStates);
        scriptedTraversalAbilities = null;
        scriptedTraversalAbilityEnabledStates = null;
        scriptedTraversalItemAbilities = null;
        scriptedTraversalItemAbilityEnabledStates = null;
    }

    private static bool[] CaptureAndDisableAbilities(Ability[] abilities)
    {
        if (abilities == null || abilities.Length == 0)
        {
            return null;
        }

        bool[] enabledStates = new bool[abilities.Length];
        for (int i = 0; i < abilities.Length; i++)
        {
            Ability ability = abilities[i];
            if (ability == null)
            {
                continue;
            }

            enabledStates[i] = ability.Enabled;
            ability.Enabled = false;
        }

        return enabledStates;
    }

    private static void RestoreAbilityEnabledStates(Ability[] abilities, bool[] enabledStates)
    {
        if (abilities == null || enabledStates == null)
        {
            return;
        }

        int count = Mathf.Min(abilities.Length, enabledStates.Length);
        for (int i = 0; i < count; i++)
        {
            Ability ability = abilities[i];
            if (ability != null)
            {
                ability.Enabled = enabledStates[i];
            }
        }
    }

    private void ResolveReferences()
    {
        if (squadController == null)
        {
            squadController = GetComponent<SquadCharacterController>();
        }

        if (locomotion == null)
        {
            locomotion = GetComponent<UltimateCharacterLocomotion>();
        }

        if (locomotionHandler == null)
        {
            locomotionHandler = GetComponent<UltimateCharacterLocomotionHandler>();
        }

        if (playerInput == null)
        {
            playerInput = GetComponent<LitOpsivePlayerInput>();
        }

        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (animatorMonitor == null)
        {
            animatorMonitor = GetComponent<AnimatorMonitor>();
        }

        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        EnsureLookSource();
    }

    private void EnsureCompanionBridges()
    {
        if (!autoInstallCompanionBridges || !Application.isPlaying)
        {
            return;
        }

        EnsureComponent<LitUccInteractionBridge>();
        EnsureComponent<LitUccDamageBridge>();
        EnsureComponent<LitUccFollowerBridge>();
    }

    private T EnsureComponent<T>() where T : Component
    {
        T component = GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }

    private void EnsureLookSource()
    {
        if (lookSource != null)
        {
            lookSource.EventTarget = gameObject;
            if (Application.isPlaying && isActiveAndEnabled && (locomotion == null || !ReferenceEquals(locomotion.LookSource, lookSource)))
            {
                lookSource.AttachToCharacter();
            }
            return;
        }

        lookSource = GetComponentInChildren<LitOpsiveLookSource>(true);
        if (lookSource == null && Application.isPlaying)
        {
            GameObject host = new GameObject("LitUccLookSource");
            host.transform.SetParent(transform, false);
            lookSource = host.AddComponent<LitOpsiveLookSource>();
        }

        if (lookSource != null)
        {
            lookSource.EventTarget = gameObject;
            if (Application.isPlaying && isActiveAndEnabled && (locomotion == null || !ReferenceEquals(locomotion.LookSource, lookSource)))
            {
                lookSource.AttachToCharacter();
            }
        }
    }

    private void AttachLookSourceIfNeeded(bool force)
    {
        if (lookSource == null)
        {
            EnsureLookSource();
        }

        if (lookSource == null)
        {
            return;
        }

        lookSource.EventTarget = gameObject;
        if (force)
        {
            lookSource.QueueAttachRetries();
            lookSource.AttachToCharacter();
            return;
        }

        if (lookSource.IsAttachedToCharacter() == false)
        {
            lookSource.AttachToCharacter();
        }
    }

    private void ApplyWorldMoveInput(Vector2 worldInput)
    {
        currentWorldMoveInput = Vector2.ClampMagnitude(worldInput, 1f);
        float magnitude = currentWorldMoveInput.magnitude;
        if (magnitude <= movementDeadZone)
        {
            currentWorldMoveInput = Vector2.zero;
            magnitude = 0f;
        }

        Vector2 opsiveInput = Vector2.zero;
        if (magnitude > 0f)
        {
            Vector3 direction = new Vector3(currentWorldMoveInput.x, 0f, currentWorldMoveInput.y);
            direction.Normalize();
            lastPlanarDirection = direction;
            opsiveInput = new Vector2(0f, magnitude);
            if (orientLookSourceFromMovement && lookSource != null)
            {
                lookSource.SetPlanarLookDirection(direction);
            }
        }

        if (playerInput != null)
        {
            playerInput.SetMovementOverride(opsiveInput, IsDriving);
            playerInput.SetSprintOverride(sprintPressed, IsDriving);
        }

        if (overrideOpsiveHandlerInput && locomotionHandler != null)
        {
            locomotionHandler.OverrideInput = IsDriving;
            locomotionHandler.OverriddenHorizontalMovement = opsiveInput.x;
            locomotionHandler.OverriddenForwardMovement = opsiveInput.y;
            locomotionHandler.OverriddenLookVector = Vector2.zero;
        }
    }

    private void SyncSpeedChangeAbility()
    {
        if (locomotion == null)
        {
            return;
        }

        SpeedChange speedChange = locomotion.GetAbility<SpeedChange>();
        if (speedChange == null)
        {
            if (sprintPressed)
            {
                WarnOnce(ref warnedMissingSpeedChange, "UCC SpeedChange ability is missing on this character. Sprint input will be ignored by UCC until the ability is added.");
            }

            return;
        }

        if (sprintPressed)
        {
            locomotion.TryStartAbility(speedChange);
        }
        else if (speedChange.IsActive)
        {
            locomotion.TryStopAbility(speedChange);
        }
    }

    private bool TryStartJumpAbility(Jump jump)
    {
        jump.ImmediateJump = true;
        bool started = locomotion != null && locomotion.TryStartAbility(jump);
        if (started)
        {
            ClearPendingJump();
            warnedJumpRejected = false;
            return true;
        }

        jump.ImmediateJump = false;
        return false;
    }

    private void QueuePendingJump(Vector2 worldInput, bool hasWorldInput)
    {
        if (jumpRetryWindow <= 0f)
        {
            WarnJumpRejected();
            return;
        }

        hasPendingJump = true;
        pendingJumpUntil = Time.time + jumpRetryWindow;
        pendingJumpWorldInput = worldInput;
        pendingJumpHasWorldInput = hasWorldInput;
    }

    private void RetryPendingJump()
    {
        if (!hasPendingJump)
        {
            return;
        }

        if (Time.time > pendingJumpUntil)
        {
            if (TryApplyJumpForceFallback(pendingJumpWorldInput, pendingJumpHasWorldInput))
            {
                return;
            }

            ClearPendingJump();
            WarnJumpRejected();
            return;
        }

        if (pendingJumpHasWorldInput && pendingJumpWorldInput.sqrMagnitude > movementDeadZone * movementDeadZone)
        {
            ApplyWorldMoveInput(pendingJumpWorldInput);
        }

        Jump jump = locomotion != null ? locomotion.GetAbility<Jump>() : null;
        if (jump == null)
        {
            if (TryApplyJumpForceFallback(pendingJumpWorldInput, pendingJumpHasWorldInput))
            {
                return;
            }

            WarnOnce(ref warnedMissingJump, "UCC Jump ability is missing on this character. Add standard movement abilities with the Lit/Opsive UCC migration tool or Opsive Character Manager.");
            ClearPendingJump();
            return;
        }

        if (!TryStartJumpAbility(jump))
        {
            TryApplyJumpForceFallback(pendingJumpWorldInput, pendingJumpHasWorldInput);
        }
    }

    private bool TryApplyJumpForceFallback(Vector2 worldInput, bool hasWorldInput)
    {
        if (!useJumpForceFallback ||
            jumpFallbackVelocity <= 0f ||
            locomotion == null ||
            !locomotion.Grounded ||
            IsFlightActive)
        {
            return false;
        }

        if (hasWorldInput && worldInput.sqrMagnitude > movementDeadZone * movementDeadZone)
        {
            ApplyWorldMoveInput(worldInput);
        }

        locomotion.GravityAccumulation = 0f;
        locomotion.AddForce(transform.up * jumpFallbackVelocity, 1, false);
        warnedJumpRejected = false;
        ClearPendingJump();
        return true;
    }

    private bool EnsureFlightAbility()
    {
        if (!enableUccFlight || locomotion == null)
        {
            return false;
        }

        flightAbility = locomotion.GetAbility<LitUccFlightAbility>();
        if (flightAbility != null)
        {
            ConfigureFlightAbility();
            return true;
        }

        Ability[] abilities = locomotion.Abilities;
        int length = abilities != null ? abilities.Length : 0;
        Ability[] nextAbilities = new Ability[length + 1];
        if (length > 0)
        {
            System.Array.Copy(abilities, nextAbilities, length);
        }

        flightAbility = new LitUccFlightAbility();
        nextAbilities[length] = flightAbility;
        locomotion.Abilities = nextAbilities;
        ConfigureFlightAbility();
        return flightAbility != null;
    }

    private void ConfigureFlightAbility()
    {
        if (flightAbility == null)
        {
            return;
        }

        flightAbility.Configure(
            flightTakeoffVerticalSpeed,
            flightTakeoffDuration,
            flightTakeoffDamping,
            flightCruiseSpeed,
            flightBoostSpeed,
            flightAcceleration,
            flightBoostAcceleration,
            flightDeceleration,
            flightVerticalSpeed,
            flightVerticalAcceleration,
            flightVerticalDeceleration,
            flightVerticalDeadZone,
            flightIdleSpeedThreshold,
            flightTurnRate,
            flightBoostTurnRate,
            flightLandingSpeed,
            flightLandingAcceleration);
    }

    private bool StopFlightAbilityIfActive()
    {
        if (flightAbility == null || !flightAbility.IsActive)
        {
            return true;
        }

        return locomotion != null && locomotion.TryStopAbility(flightAbility, true);
    }

    private void ClearPendingJump()
    {
        hasPendingJump = false;
        pendingJumpUntil = 0f;
        pendingJumpWorldInput = Vector2.zero;
        pendingJumpHasWorldInput = false;
    }

    private void WarnJumpRejected()
    {
        if (!warnWhenJumpRejected)
        {
            return;
        }

        WarnOnce(
            ref warnedJumpRejected,
            $"UCC Jump was requested on '{name}' but UltimateCharacterLocomotion rejected it. Grounded={ResolveGroundedLabel()}, ActiveAbilities={ResolveActiveAbilityLabel()}. Check UCC ground detection, slope/ceiling rules, and any ability blocking Jump.");
    }

    private string ResolveGroundedLabel()
    {
        return locomotion != null ? locomotion.Grounded.ToString() : "unknown";
    }

    private string ResolveActiveAbilityLabel()
    {
        if (locomotion == null || locomotion.ActiveAbilityCount <= 0 || locomotion.ActiveAbilities == null)
        {
            return "none";
        }

        string label = string.Empty;
        int count = Mathf.Min(locomotion.ActiveAbilityCount, locomotion.ActiveAbilities.Length);
        for (int i = 0; i < count; i++)
        {
            Ability ability = locomotion.ActiveAbilities[i];
            if (ability == null)
            {
                continue;
            }

            if (label.Length > 0)
            {
                label += ", ";
            }

            label += $"{ability.GetType().Name}(index {ability.Index})";
        }

        return label.Length > 0 ? label : "none";
    }

    private void UpdateAnimatorParameters()
    {
        UpdateFlightAnimatorParameters();

        if (!driveLitLocomotionAnimatorParameters || animator == null || IsFlightModeActive)
        {
            return;
        }

        Vector3 velocity = ResolvePlanarVelocity();
        float speed = velocity.magnitude;
        bool moving = currentWorldMoveInput.sqrMagnitude > movementDeadZone * movementDeadZone || speed > 0.05f;

        SetAnimatorFloat(speedParam, speed);
        SetAnimatorBool(isMovingParam, moving);
        SetAnimatorFloat(locomotionTierParam, ResolveLocomotionTier(speed));
        SetAnimatorFloat(turnParam, ResolveSignedTurn(velocity));
    }

    private Vector3 ResolvePlanarVelocity()
    {
        Vector3 velocity = locomotion != null ? locomotion.Velocity : Vector3.zero;
        velocity.y = 0f;
        if (velocity.sqrMagnitude > 0.0001f)
        {
            lastPosition = transform.position;
            hasLastPosition = true;
            return velocity;
        }

        if (!hasLastPosition)
        {
            lastPosition = transform.position;
            hasLastPosition = true;
            return Vector3.zero;
        }

        float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
        Vector3 delta = transform.position - lastPosition;
        lastPosition = transform.position;
        delta.y = 0f;
        return delta / deltaTime;
    }

    private float ResolveLocomotionTier(float speed)
    {
        if (speed <= 0.05f)
        {
            return 1f;
        }

        float jogThreshold = Mathf.Lerp(walkPresentationSpeed, runPresentationSpeed, 0.5f);
        return speed >= jogThreshold ? 3f : speed >= walkPresentationSpeed * 0.5f ? 2f : 1f;
    }

    private float ResolveSignedTurn(Vector3 velocity)
    {
        Vector3 direction = velocity.sqrMagnitude > 0.0001f ? velocity.normalized : lastPlanarDirection;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return 0f;
        }

        return Mathf.Clamp(Vector3.SignedAngle(transform.forward, direction, Vector3.up) / 90f, -1f, 1f);
    }

    private void RegisterExternalDriver()
    {
        if (!driveFromSquadFacade || externalDriverRegistered || squadController == null)
        {
            return;
        }

        squadController.PushExternalLocomotionDriver();
        externalDriverRegistered = true;
    }

    private void UnregisterExternalDriver()
    {
        if (!externalDriverRegistered || squadController == null)
        {
            externalDriverRegistered = false;
            return;
        }

        squadController.PopExternalLocomotionDriver();
        externalDriverRegistered = false;
    }

    private void CacheRigidbodyState()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        if (rb == null)
        {
            return;
        }

        previousUseGravity = rb.useGravity;
        previousIsKinematic = rb.isKinematic;
        previousCollisionMode = rb.collisionDetectionMode;
    }

    private void ConfigureRigidbody()
    {
        if (!configureRigidbodyForOpsive || rb == null)
        {
            return;
        }

        rb.useGravity = false;
        rb.isKinematic = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }

    private void RestoreRigidbody()
    {
        if (!configureRigidbodyForOpsive || rb == null)
        {
            return;
        }

        rb.useGravity = previousUseGravity;
        rb.isKinematic = previousIsKinematic;
        rb.collisionDetectionMode = previousCollisionMode;
    }

    private void SetAnimatorFloat(string parameter, float value)
    {
        if (HasAnimatorParameter(parameter, AnimatorControllerParameterType.Float))
        {
            animator.SetFloat(parameter, value);
        }
    }

    private void SetAnimatorBool(string parameter, bool value)
    {
        if (HasAnimatorParameter(parameter, AnimatorControllerParameterType.Bool))
        {
            animator.SetBool(parameter, value);
        }
    }

    private void SetAnimatorInteger(string parameter, int value)
    {
        if (HasAnimatorParameter(parameter, AnimatorControllerParameterType.Int))
        {
            animator.SetInteger(parameter, value);
        }
    }

    private void SetAnimatorTrigger(string parameter)
    {
        if (HasAnimatorParameter(parameter, AnimatorControllerParameterType.Trigger))
        {
            animator.SetTrigger(parameter);
        }
    }

    private bool HasAnimatorParameter(string parameter, AnimatorControllerParameterType type)
    {
        if (animator == null || string.IsNullOrWhiteSpace(parameter))
        {
            return false;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].type == type && parameters[i].name == parameter)
            {
                return true;
            }
        }

        return false;
    }

    private void WarnOnce(ref bool flag, string message)
    {
        if (flag)
        {
            return;
        }

        flag = true;
        Debug.LogWarning(message, this);
    }
}
