using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class StarterMotorPlayerIntegration : MonoBehaviour
{
    [Header("Activation")]
    [SerializeField] private bool allowStarterMotorControl = true;
    [SerializeField] private bool blockDuringMultiplayer = true;

    [Header("References")]
    [SerializeField] private StarterInspiredThirdPersonMotor motor;
    [SerializeField] private StarterMotorAnimatorDriver animatorDriver;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private SquadCharacterController legacyController;
    [SerializeField] private Rigidbody legacyRigidbody;
    [SerializeField] private CapsuleCollider legacyCapsule;
    [SerializeField] private StarterMotorLocalInputBridge localInputBridge;

    [Header("Compatibility")]
    [SerializeField] private bool configureCharacterControllerFromCapsule = true;
    [SerializeField] private bool disableLegacyCapsuleWhileActive = true;

    [Header("Debug")]
    [SerializeField] private bool debugStarterMotorActive;

    private bool previousCharacterControllerEnabled;
    private bool previousMotorEnabled;
    private bool previousAnimatorDriverEnabled;
    private bool previousLocalInputBridgeEnabled;
    private bool previousRigidbodyIsKinematic;
    private bool previousRigidbodyUseGravity;
    private bool previousRigidbodyDetectCollisions;
    private CollisionDetectionMode previousRigidbodyCollisionDetectionMode;
    private RigidbodyInterpolation previousRigidbodyInterpolation;
    private bool previousLegacyCapsuleEnabled;
    private bool legacyControllerSuppressed;
    private bool active;

    public bool IsStarterMotorActive => active;
    public bool CanUseStarterMotor => allowStarterMotorControl && !IsBlockedByMultiplayer();

    public bool TryGetActiveCharacterController(out CharacterController activeCharacterController)
    {
        activeCharacterController = null;
        if (!active)
        {
            return false;
        }

        ResolveReferences();
        if (characterController == null || !characterController.enabled)
        {
            return false;
        }

        activeCharacterController = characterController;
        return true;
    }

    private void Reset()
    {
        ResolveReferences();
    }

    private void Awake()
    {
        ResolveReferences();
        debugStarterMotorActive = active;
    }

    private void Update()
    {
        if (!active || legacyController == null)
        {
            return;
        }

        legacyController.TickTorchLifetimeForExternalLocomotion(Time.deltaTime);
        legacyController.RefreshAudioListenerStateForExternalLocomotion();
    }

    private void LateUpdate()
    {
        if (!active || legacyController == null)
        {
            return;
        }

        legacyController.RefreshLocalInteractionDetectionForExternalLocomotion();
    }

    private void OnDisable()
    {
        SetStarterMotorActive(false);
    }

    public bool SetStarterMotorActive(bool shouldBeActive)
    {
        if (shouldBeActive && !CanUseStarterMotor)
        {
            shouldBeActive = false;
        }

        if (shouldBeActive)
        {
            if (!EnsureStarterMotorComponents())
            {
                ApplyInactiveState();
                return false;
            }

            ApplyActiveState();
            return true;
        }

        ApplyInactiveState();
        return false;
    }

    public void SetMoveInput(Vector2 input)
    {
        if (!active || motor == null)
        {
            return;
        }

        motor.SetMoveInput(input);
    }

    public void RequestJump()
    {
        if (!active || motor == null)
        {
            return;
        }

        motor.RequestJump();
    }

    public void Stop()
    {
        if (motor != null)
        {
            motor.Stop();
        }
    }

    private bool EnsureStarterMotorComponents()
    {
        ResolveReferences();

        if (characterController == null)
        {
            characterController = gameObject.AddComponent<CharacterController>();
            characterController.enabled = false;
        }

        ConfigureCharacterController();

        if (motor == null)
        {
            motor = gameObject.AddComponent<StarterInspiredThirdPersonMotor>();
            motor.enabled = false;
        }

        if (animatorDriver == null)
        {
            animatorDriver = gameObject.AddComponent<StarterMotorAnimatorDriver>();
            animatorDriver.enabled = false;
        }

        return characterController != null && motor != null && animatorDriver != null;
    }

    private void ApplyActiveState()
    {
        if (active)
        {
            return;
        }

        ResolveReferences();
        CapturePreviousState();

        if (legacyController != null)
        {
            legacyController.PushExternalLocomotionDriver();
            legacyControllerSuppressed = true;
        }

        if (legacyRigidbody != null)
        {
            legacyRigidbody.linearVelocity = Vector3.zero;
            legacyRigidbody.angularVelocity = Vector3.zero;
            legacyRigidbody.useGravity = false;
            legacyRigidbody.isKinematic = true;
            legacyRigidbody.detectCollisions = true;
            legacyRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        if (disableLegacyCapsuleWhileActive && legacyCapsule != null)
        {
            legacyCapsule.enabled = false;
        }

        if (localInputBridge != null)
        {
            localInputBridge.enabled = false;
        }

        characterController.enabled = true;
        motor.enabled = true;
        animatorDriver.enabled = true;
        motor.ResetMotionState();

        active = true;
        debugStarterMotorActive = true;
    }

    private void ApplyInactiveState()
    {
        if (!active)
        {
            debugStarterMotorActive = false;
            return;
        }

        if (motor != null)
        {
            motor.ResetMotionState();
            motor.enabled = previousMotorEnabled;
        }

        if (animatorDriver != null)
        {
            animatorDriver.enabled = previousAnimatorDriverEnabled;
        }

        if (characterController != null)
        {
            characterController.enabled = previousCharacterControllerEnabled;
        }

        if (localInputBridge != null)
        {
            localInputBridge.enabled = previousLocalInputBridgeEnabled;
        }

        if (disableLegacyCapsuleWhileActive && legacyCapsule != null)
        {
            legacyCapsule.enabled = previousLegacyCapsuleEnabled;
        }

        if (legacyRigidbody != null)
        {
            legacyRigidbody.isKinematic = previousRigidbodyIsKinematic;
            legacyRigidbody.useGravity = previousRigidbodyUseGravity;
            legacyRigidbody.detectCollisions = previousRigidbodyDetectCollisions;
            legacyRigidbody.collisionDetectionMode = previousRigidbodyCollisionDetectionMode;
            legacyRigidbody.interpolation = previousRigidbodyInterpolation;
            if (!legacyRigidbody.isKinematic)
            {
                legacyRigidbody.linearVelocity = Vector3.zero;
                legacyRigidbody.angularVelocity = Vector3.zero;
            }
        }

        if (legacyControllerSuppressed && legacyController != null)
        {
            legacyController.PopExternalLocomotionDriver();
            legacyControllerSuppressed = false;
        }

        active = false;
        debugStarterMotorActive = false;
    }

    private void CapturePreviousState()
    {
        previousCharacterControllerEnabled = characterController != null && characterController.enabled;
        previousMotorEnabled = motor != null && motor.enabled;
        previousAnimatorDriverEnabled = animatorDriver != null && animatorDriver.enabled;
        previousLocalInputBridgeEnabled = localInputBridge != null && localInputBridge.enabled;
        previousLegacyCapsuleEnabled = legacyCapsule != null && legacyCapsule.enabled;

        if (legacyRigidbody == null)
        {
            previousRigidbodyIsKinematic = false;
            previousRigidbodyUseGravity = false;
            previousRigidbodyDetectCollisions = false;
            previousRigidbodyCollisionDetectionMode = CollisionDetectionMode.Discrete;
            previousRigidbodyInterpolation = RigidbodyInterpolation.None;
            return;
        }

        previousRigidbodyIsKinematic = legacyRigidbody.isKinematic;
        previousRigidbodyUseGravity = legacyRigidbody.useGravity;
        previousRigidbodyDetectCollisions = legacyRigidbody.detectCollisions;
        previousRigidbodyCollisionDetectionMode = legacyRigidbody.collisionDetectionMode;
        previousRigidbodyInterpolation = legacyRigidbody.interpolation;
    }

    private void ResolveReferences()
    {
        if (motor == null)
        {
            motor = GetComponent<StarterInspiredThirdPersonMotor>();
        }

        if (animatorDriver == null)
        {
            animatorDriver = GetComponent<StarterMotorAnimatorDriver>();
        }

        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
        }

        if (legacyController == null)
        {
            legacyController = GetComponent<SquadCharacterController>();
        }

        if (legacyRigidbody == null)
        {
            legacyRigidbody = GetComponent<Rigidbody>();
        }

        if (legacyCapsule == null)
        {
            legacyCapsule = GetComponent<CapsuleCollider>();
        }

        if (localInputBridge == null)
        {
            localInputBridge = GetComponent<StarterMotorLocalInputBridge>();
        }
    }

    private void ConfigureCharacterController()
    {
        if (characterController == null)
        {
            return;
        }

        if (configureCharacterControllerFromCapsule && legacyCapsule != null)
        {
            characterController.center = legacyCapsule.center;
            characterController.height = Mathf.Max(legacyCapsule.height, legacyCapsule.radius * 2f);
            characterController.radius = Mathf.Max(0.01f, legacyCapsule.radius);
        }
        else
        {
            characterController.center = new Vector3(0f, 0.9f, 0f);
            characterController.height = 1.8f;
            characterController.radius = 0.45f;
        }

        characterController.slopeLimit = 50f;
        characterController.stepOffset = 0.35f;
        characterController.skinWidth = 0.06f;
        characterController.minMoveDistance = 0f;
    }

    private bool IsBlockedByMultiplayer()
    {
        return blockDuringMultiplayer &&
               NetworkManager.Singleton != null &&
               NetworkManager.Singleton.IsListening;
    }
}
