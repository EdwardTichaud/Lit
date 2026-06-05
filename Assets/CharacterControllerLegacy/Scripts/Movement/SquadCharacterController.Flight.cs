using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

public partial class SquadCharacterController
{
    private const float FlightCharacterControllerMinimumStepOffset = 0.001f;
    private const string FlightRootName = "Flight";
    private const string FlightAudioName = "Flight Audio";
    private const string FlightTrailNamePrefix = "Flight_Trail";
    private const string PreviousFlightTrailNamePrefix = "Flight Trail";
    private const string FlightSpeedLinesName = "Flight Speed Lines";
    private const float FlightVfxMoveStartSpeed = 0.35f;
    private const float FlightVfxMoveStopSpeed = 0.12f;

    [Header("Flight")]
    [SerializeField, Tooltip("Autorise le controle local du vol.")]
    private bool allowFlightControl = true;
    [SerializeField, Tooltip("Desactive le moteur de vol pendant une session multijoueur.")]
    private bool blockFlightDuringMultiplayer = true;
    [SerializeField, Tooltip("Moteur qui simule le vol. Auto-resolu si vide.")]
    private StarterInspiredThirdPersonMotor flightMotor;
    [SerializeField, Tooltip("Driver Animator associe au moteur de vol. Auto-resolu si vide.")]
    private StarterMotorAnimatorDriver flightAnimatorDriver;
    [SerializeField, Tooltip("CharacterController utilise par le moteur de vol. Auto-resolu/cree si vide.")]
    private CharacterController flightCharacterController;
    [SerializeField, Tooltip("Input bridge de test local. Desactive pendant le pilotage SquadManager.")]
    private StarterMotorLocalInputBridge flightLocalInputBridge;
    [SerializeField, Tooltip("Configure le CharacterController de vol depuis la capsule source.")]
    private bool configureFlightCharacterControllerFromCapsule = true;
    [FormerlySerializedAs("disableLegacyCapsuleWhileFlightActive")]
    [SerializeField, Tooltip("Desactive la capsule source pendant que le moteur de vol est actif.")]
    private bool disableSourceCapsuleWhileFlightActive = true;
    [SerializeField, Tooltip("Applique les reglages ci-dessous au moteur de vol quand il est actif.")]
    private bool applyFlightProfile = true;
    [SerializeField, Tooltip("Reglages de gameplay du vol appliques au moteur runtime.")]
    private StarterInspiredThirdPersonMotor.FlightProfile flightProfile =
        StarterInspiredThirdPersonMotor.FlightProfile.Default;

    [Header("Flight Feedback References")]
    [SerializeField, Tooltip("Source audio loop du vol. Auto-resolue sous 'Flight Audio' si vide.")]
    private AudioSource flightLoopAudioSource;
    [SerializeField, Tooltip("Source audio one-shot du takeoff/boost. Auto-resolue sous 'Flight Audio' si vide.")]
    private AudioSource flightBurstAudioSource;
    [SerializeField, Tooltip("Particules de vitesse en vol. Pas utilise pour le burst de boost.")]
    private ParticleSystem flightSpeedLineParticles;
    [SerializeField, Tooltip("Trails de vol. Auto-resolus depuis les enfants nommes 'Flight_Trail*' sous 'Flight' si vide.")]
    private TrailRenderer[] flightTrails = new TrailRenderer[0];
    [SerializeField, HideInInspector]
    private TrailRenderer flightLeftTrail;
    [SerializeField, HideInInspector]
    private TrailRenderer flightRightTrail;
    [SerializeField, Tooltip("Point d'instanciation du prefab de boost. Transform du personnage si vide.")]
    private Transform flightBoostPrefabSpawnPoint;

    [Header("Flight Audio")]
    [SerializeField, Tooltip("Clip loop du vol. Si vide, un clip procedural de test est utilise.")]
    private AudioClip flightLoopClip;
    [SerializeField, Tooltip("Clip joue au takeoff et au debut du boost. Si vide, un clip procedural de test est utilise.")]
    private AudioClip flightBurstClip;
    [SerializeField, Range(0f, 1f), Tooltip("Volume minimal du loop de vol.")]
    private float flightLoopMinVolume = 0.08f;
    [SerializeField, Range(0f, 1f), Tooltip("Volume maximal du loop de vol.")]
    private float flightLoopMaxVolume = 0.34f;
    [SerializeField, Range(0.1f, 3f), Tooltip("Pitch minimal du loop de vol.")]
    private float flightLoopMinPitch = 0.8f;
    [SerializeField, Range(0.1f, 3f), Tooltip("Pitch maximal du loop de vol.")]
    private float flightLoopMaxPitch = 1.65f;
    [SerializeField, Range(0f, 1f), Tooltip("Volume du one-shot takeoff/boost.")]
    private float flightBurstVolume = 0.55f;

    [Header("Flight VFX")]
    [SerializeField, Min(0f), Tooltip("Emission minimale des particules de vitesse.")]
    private float flightMinParticleRate = 10f;
    [SerializeField, Min(0f), Tooltip("Emission maximale des particules de vitesse.")]
    private float flightMaxParticleRate = 115f;
    [SerializeField, Min(0f), Tooltip("Duree minimale des trails.")]
    private float flightMinTrailTime = 0.08f;
    [SerializeField, Min(0f), Tooltip("Duree maximale des trails.")]
    private float flightMaxTrailTime = 0.34f;
    [SerializeField, Min(0f), Tooltip("Largeur minimale des trails.")]
    private float flightMinTrailWidth = 0.015f;
    [SerializeField, Min(0f), Tooltip("Largeur maximale des trails.")]
    private float flightMaxTrailWidth = 0.115f;
    [SerializeField, Tooltip("Oriente les particules de vitesse dans la meme direction que l'inclinaison montee/descente.")]
    private bool orientFlightParticlesWithVerticalTilt = true;

    [Header("Flight Boost Prefab")]
    [SerializeField, Tooltip("Prefab instancie au moment du boost. Remplace les anciennes particules de boost.")]
    private GameObject flightBoostPrefab;
    [SerializeField, Tooltip("Offset local applique depuis le point de spawn.")]
    private Vector3 flightBoostPrefabLocalOffset;
    [SerializeField, Tooltip("Rotation locale ajoutee au point de spawn.")]
    private Vector3 flightBoostPrefabEulerOffset;
    [SerializeField, Tooltip("Parent le prefab au point de spawn apres instanciation.")]
    private bool parentFlightBoostPrefabToSpawnPoint;
    [SerializeField, Min(0f), Tooltip("Duree de vie du prefab instancie. 0 = pas de destruction automatique.")]
    private float flightBoostPrefabLifetime = 2f;

    private AudioClip flightGeneratedLoopClip;
    private AudioClip flightGeneratedBurstClip;
    private Transform flightSpeedLineParticleRotationTransform;
    private Quaternion flightSpeedLineParticleInitialLocalRotation = Quaternion.identity;
    private bool wasFlightActive;
    private bool flightVfxMoving;
    private bool flightMotorActive;
    private bool previousFlightCharacterControllerEnabled;
    private bool previousFlightMotorEnabled;
    private bool previousFlightAnimatorDriverEnabled;
    private AnimatorUpdateMode previousFlightAnimatorUpdateMode;
    private bool previousFlightAnimatorApplyRootMotion;
    private bool previousFlightLocalInputBridgeEnabled;
    private bool previousFlightRigidbodyIsKinematic;
    private bool previousFlightRigidbodyUseGravity;
    private bool previousFlightRigidbodyDetectCollisions;
    private CollisionDetectionMode previousFlightRigidbodyCollisionDetectionMode;
    private RigidbodyInterpolation previousFlightRigidbodyInterpolation;
    private bool previousSourceCapsuleEnabled;
    private bool flightSuppressedSourceController;
    [SerializeField, Tooltip("Debug: vrai quand le moteur de vol est actif via SquadCharacterController.")]
    private bool debugFlightMotorActive;

    public bool IsFlightMotorActive => flightMotorActive;
    public bool DebugFlightMotorActive => debugFlightMotorActive;

    public bool FlightActive
    {
        get
        {
            ResolveFlightMotorReferences();
            return flightMotorActive && flightMotor != null && flightMotor.FlightActive;
        }
    }

    public bool CanUseFlightMotor => allowFlightControl && !HasUccLocomotionBridge && !IsFlightBlockedByMultiplayer();

    public void ConfigureFlightMotorRuntime(
        StarterInspiredThirdPersonMotor motor,
        StarterMotorAnimatorDriver animatorDriver)
    {
        if (motor != null)
        {
            flightMotor = motor;
            motor.ConfigureGroundSpeedProfile(WalkMoveSpeed, MoveSpeed);
            if (applyFlightProfile)
            {
                StarterInspiredThirdPersonMotor.FlightProfile profile = flightProfile;
                StarterInspiredThirdPersonMotor.ClampFlightProfile(ref profile);
                flightProfile = profile;
                motor.ConfigureFlightProfile(flightProfile);
            }
        }

        if (animatorDriver != null)
        {
            animatorDriver.ConfigureGroundSpeedReference(MoveSpeed);
        }

        ResolveFlightFeedbackReferences();
        EnsureFlightFeedbackObjects();
    }

    public bool SetFlightMotorActive(bool shouldBeActive, bool createMissingComponents = true)
    {
        if (shouldBeActive && !CanUseFlightMotor)
        {
            shouldBeActive = false;
        }

        if (shouldBeActive)
        {
            if (!EnsureFlightMotorComponents(createMissingComponents))
            {
                ApplyFlightMotorInactiveState();
                return false;
            }

            ApplyFlightMotorActiveState();
            return true;
        }

        ApplyFlightMotorInactiveState();
        return false;
    }

    public bool TryGetActiveFlightCharacterController(out CharacterController activeCharacterController)
    {
        activeCharacterController = null;
        if (!flightMotorActive)
        {
            return false;
        }

        ResolveFlightMotorReferences();
        if (flightCharacterController == null || !flightCharacterController.enabled)
        {
            return false;
        }

        activeCharacterController = flightCharacterController;
        return true;
    }

    public void ApplyFlightMotorControlInput(
        Vector2 move,
        bool boostOrSprint,
        float verticalInput,
        bool requestJump,
        bool toggleFlightMode)
    {
        ResolveFlightMotorReferences();
        if (!flightMotorActive || flightMotor == null)
        {
            return;
        }

        if (toggleFlightMode)
        {
            flightMotor.ToggleFlightMode();
        }

        flightMotor.SetBoostInput(boostOrSprint);
        flightMotor.SetSprintInput(boostOrSprint);
        flightMotor.SetFlightVerticalInput(verticalInput);
        flightMotor.SetMoveInput(move);

        if (requestJump)
        {
            flightMotor.RequestJump();
        }
    }

    public void StopFlightMotorControl()
    {
        ResolveFlightMotorReferences();
        if (flightMotorActive && flightMotor != null)
        {
            flightMotor.Stop();
        }
    }

    private void ResolveFlightMotorReferences()
    {
        if (flightMotor == null)
        {
            flightMotor = GetComponent<StarterInspiredThirdPersonMotor>();
        }

        if (flightAnimatorDriver == null)
        {
            flightAnimatorDriver = GetComponent<StarterMotorAnimatorDriver>();
        }

        if (flightCharacterController == null)
        {
            flightCharacterController = GetComponent<CharacterController>();
        }

        if (flightLocalInputBridge == null)
        {
            flightLocalInputBridge = GetComponent<StarterMotorLocalInputBridge>();
        }
    }

    private bool EnsureFlightMotorComponents(bool createMissingComponents)
    {
        ResolveFlightMotorReferences();

        if (flightCharacterController == null && createMissingComponents)
        {
            flightCharacterController = gameObject.AddComponent<CharacterController>();
            flightCharacterController.enabled = false;
        }

        ConfigureFlightCharacterController();

        if (flightMotor == null && createMissingComponents)
        {
            flightMotor = gameObject.AddComponent<StarterInspiredThirdPersonMotor>();
            flightMotor.enabled = false;
        }

        if (flightAnimatorDriver == null && createMissingComponents)
        {
            flightAnimatorDriver = gameObject.AddComponent<StarterMotorAnimatorDriver>();
            flightAnimatorDriver.enabled = false;
        }

        return flightCharacterController != null &&
               flightMotor != null &&
               flightAnimatorDriver != null;
    }

    private void ApplyFlightMotorActiveState()
    {
        if (flightMotorActive)
        {
            return;
        }

        ResolveFlightMotorReferences();
        CaptureFlightMotorPreviousState();
        ConfigureFlightMotorRuntime(flightMotor, flightAnimatorDriver);

        PushExternalLocomotionDriver();
        flightSuppressedSourceController = true;

        if (rigidbodyTarget != null)
        {
            if (!rigidbodyTarget.isKinematic)
            {
                rigidbodyTarget.linearVelocity = Vector3.zero;
                rigidbodyTarget.angularVelocity = Vector3.zero;
            }

            rigidbodyTarget.useGravity = false;
            rigidbodyTarget.isKinematic = true;
            rigidbodyTarget.detectCollisions = true;
            rigidbodyTarget.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rigidbodyTarget.interpolation = RigidbodyInterpolation.None;
        }

        if (disableSourceCapsuleWhileFlightActive && locomotionCapsule != null)
        {
            locomotionCapsule.enabled = false;
        }

        if (flightLocalInputBridge != null)
        {
            flightLocalInputBridge.enabled = false;
        }

        if (animator != null)
        {
            animator.updateMode = AnimatorUpdateMode.Normal;
            animator.applyRootMotion = false;
        }

        flightCharacterController.enabled = true;
        if (!flightCharacterController.enabled)
        {
            Debug.LogError(
                $"Flight motor activation failed on '{name}': CharacterController did not enable.",
                this);
            flightMotorActive = true;
            ApplyFlightMotorInactiveState();
            return;
        }

        flightMotor.enabled = true;
        flightAnimatorDriver.enabled = true;
        flightMotor.ResetMotionState();

        flightMotorActive = true;
        debugFlightMotorActive = true;
    }

    private void ApplyFlightMotorInactiveState()
    {
        if (!flightMotorActive)
        {
            debugFlightMotorActive = false;
            return;
        }

        if (flightMotor != null)
        {
            flightMotor.ResetMotionState();
            flightMotor.enabled = previousFlightMotorEnabled;
        }

        if (flightAnimatorDriver != null)
        {
            flightAnimatorDriver.enabled = previousFlightAnimatorDriverEnabled;
        }

        if (animator != null)
        {
            animator.updateMode = previousFlightAnimatorUpdateMode;
            animator.applyRootMotion = previousFlightAnimatorApplyRootMotion;
        }

        if (flightCharacterController != null)
        {
            flightCharacterController.enabled = previousFlightCharacterControllerEnabled;
        }

        if (flightLocalInputBridge != null)
        {
            flightLocalInputBridge.enabled = previousFlightLocalInputBridgeEnabled;
        }

        if (disableSourceCapsuleWhileFlightActive && locomotionCapsule != null)
        {
            locomotionCapsule.enabled = previousSourceCapsuleEnabled;
        }

        if (rigidbodyTarget != null)
        {
            rigidbodyTarget.isKinematic = previousFlightRigidbodyIsKinematic;
            rigidbodyTarget.useGravity = previousFlightRigidbodyUseGravity;
            rigidbodyTarget.detectCollisions = previousFlightRigidbodyDetectCollisions;
            rigidbodyTarget.collisionDetectionMode = previousFlightRigidbodyCollisionDetectionMode;
            rigidbodyTarget.interpolation = previousFlightRigidbodyInterpolation;
            if (!rigidbodyTarget.isKinematic)
            {
                rigidbodyTarget.linearVelocity = Vector3.zero;
                rigidbodyTarget.angularVelocity = Vector3.zero;
            }
        }

        if (flightSuppressedSourceController)
        {
            PopExternalLocomotionDriver();
            flightSuppressedSourceController = false;
        }

        flightMotorActive = false;
        debugFlightMotorActive = false;
    }

    private void CaptureFlightMotorPreviousState()
    {
        previousFlightCharacterControllerEnabled = flightCharacterController != null && flightCharacterController.enabled;
        previousFlightMotorEnabled = flightMotor != null && flightMotor.enabled;
        previousFlightAnimatorDriverEnabled = flightAnimatorDriver != null && flightAnimatorDriver.enabled;
        previousFlightAnimatorUpdateMode = animator != null ? animator.updateMode : AnimatorUpdateMode.Normal;
        previousFlightAnimatorApplyRootMotion = animator != null && animator.applyRootMotion;
        previousFlightLocalInputBridgeEnabled = flightLocalInputBridge != null && flightLocalInputBridge.enabled;
        previousSourceCapsuleEnabled = locomotionCapsule != null && locomotionCapsule.enabled;

        if (rigidbodyTarget == null)
        {
            previousFlightRigidbodyIsKinematic = false;
            previousFlightRigidbodyUseGravity = false;
            previousFlightRigidbodyDetectCollisions = false;
            previousFlightRigidbodyCollisionDetectionMode = CollisionDetectionMode.Discrete;
            previousFlightRigidbodyInterpolation = RigidbodyInterpolation.None;
            return;
        }

        previousFlightRigidbodyIsKinematic = rigidbodyTarget.isKinematic;
        previousFlightRigidbodyUseGravity = rigidbodyTarget.useGravity;
        previousFlightRigidbodyDetectCollisions = rigidbodyTarget.detectCollisions;
        previousFlightRigidbodyCollisionDetectionMode = rigidbodyTarget.collisionDetectionMode;
        previousFlightRigidbodyInterpolation = rigidbodyTarget.interpolation;
    }

    private void ConfigureFlightCharacterController()
    {
        if (flightCharacterController == null)
        {
            return;
        }

        if (configureFlightCharacterControllerFromCapsule && locomotionCapsule != null)
        {
            flightCharacterController.center = locomotionCapsule.center;
            flightCharacterController.height = Mathf.Max(locomotionCapsule.height, locomotionCapsule.radius * 2f);
            flightCharacterController.radius = Mathf.Max(0.01f, locomotionCapsule.radius);
        }
        else
        {
            flightCharacterController.center = new Vector3(0f, 0.9f, 0f);
            flightCharacterController.height = 1.8f;
            flightCharacterController.radius = 0.45f;
        }

        flightCharacterController.slopeLimit = 50f;
        flightCharacterController.stepOffset = FlightCharacterControllerMinimumStepOffset;
        flightCharacterController.skinWidth = 0.06f;
        flightCharacterController.minMoveDistance = 0f;
    }

    private void TickFlightExternalLocomotion(float deltaTime)
    {
        if (!flightMotorActive)
        {
            return;
        }

        TickTorchLifetimeForExternalLocomotion(deltaTime);
        RefreshAudioListenerStateForExternalLocomotion();
    }

    private void RefreshFlightExternalLocomotionInteractions()
    {
        if (flightMotorActive)
        {
            RefreshLocalInteractionDetectionForExternalLocomotion();
        }
    }

    private bool IsFlightBlockedByMultiplayer()
    {
        return blockFlightDuringMultiplayer &&
               NetworkManager.Singleton != null &&
               NetworkManager.Singleton.IsListening;
    }

    private void InitializeFlightFeedback()
    {
        ValidateFlightSettings();
        ResolveFlightMotorReferences();
        ResolveFlightFeedbackReferences();
        EnsureFlightFeedbackObjects();
        SetFlightFeedbackActive(false, clearTrails: true);
    }

    private void TickFlightFeedback()
    {
        ResolveFlightMotorReferences();
        if (flightMotor == null)
        {
            SetFlightFeedbackActive(false, clearTrails: true);
            wasFlightActive = false;
            return;
        }

        ResolveFlightFeedbackReferences();
        EnsureFlightFeedbackObjects();

        bool flightActive = flightMotor.FlightActive;
        bool boostStarted = flightMotor.FlightBoostStarted;
        float speed01 = flightMotor.FlightNormalizedSpeed;
        float boost01 = Mathf.Clamp01(flightMotor.FlightBoostAmount);

        UpdateFlightAudio(flightActive, boostStarted, speed01, boost01);
        UpdateFlightVfx(flightActive, speed01, boost01);

        if (flightActive && boostStarted)
        {
            InstantiateFlightBoostPrefab();
        }

        wasFlightActive = flightActive;
    }

    private void ShutdownFlightFeedback()
    {
        SetFlightFeedbackActive(false, clearTrails: true);
        wasFlightActive = false;
    }

    private void DisposeFlightFeedbackRuntimeObjects()
    {
        DestroyRuntimeObject(flightGeneratedLoopClip);
        DestroyRuntimeObject(flightGeneratedBurstClip);
    }

    private void ValidateFlightSettings()
    {
        StarterInspiredThirdPersonMotor.FlightProfile profile = flightProfile;
        StarterInspiredThirdPersonMotor.ClampFlightProfile(ref profile);
        flightProfile = profile;

        flightLoopMinVolume = Mathf.Clamp01(flightLoopMinVolume);
        flightLoopMaxVolume = Mathf.Clamp01(flightLoopMaxVolume);
        if (flightLoopMaxVolume < flightLoopMinVolume)
        {
            flightLoopMaxVolume = flightLoopMinVolume;
        }

        flightLoopMinPitch = Mathf.Clamp(flightLoopMinPitch, 0.1f, 3f);
        flightLoopMaxPitch = Mathf.Clamp(flightLoopMaxPitch, 0.1f, 3f);
        if (flightLoopMaxPitch < flightLoopMinPitch)
        {
            flightLoopMaxPitch = flightLoopMinPitch;
        }

        flightBurstVolume = Mathf.Clamp01(flightBurstVolume);
        flightMinParticleRate = Mathf.Max(0f, flightMinParticleRate);
        flightMaxParticleRate = Mathf.Max(flightMinParticleRate, flightMaxParticleRate);
        flightMinTrailTime = Mathf.Max(0f, flightMinTrailTime);
        flightMaxTrailTime = Mathf.Max(flightMinTrailTime, flightMaxTrailTime);
        flightMinTrailWidth = Mathf.Max(0f, flightMinTrailWidth);
        flightMaxTrailWidth = Mathf.Max(flightMinTrailWidth, flightMaxTrailWidth);
        flightBoostPrefabLifetime = Mathf.Max(0f, flightBoostPrefabLifetime);
    }

    private void ResolveFlightFeedbackReferences()
    {
        Transform feedbackRoot = ResolveFlightFeedbackRoot();

        if (flightLoopAudioSource == null || flightBurstAudioSource == null)
        {
            ResolveFlightAudioSources(feedbackRoot);
        }

        if (!HasFlightTrails())
        {
            flightTrails = FindFlightTrails(feedbackRoot);
        }

        if (flightSpeedLineParticles == null)
        {
            flightSpeedLineParticles = FindNamedFlightComponent<ParticleSystem>(
                feedbackRoot,
                FlightSpeedLinesName);
        }
    }

    private Transform ResolveFlightFeedbackRoot()
    {
        Transform feedbackRoot = FindFlightDescendant(transform, FlightRootName);
        return feedbackRoot != null ? feedbackRoot : transform;
    }

    private void ResolveFlightAudioSources(Transform feedbackRoot)
    {
        Transform audioTransform = FindFlightDescendant(feedbackRoot, FlightAudioName);
        if (audioTransform == null && feedbackRoot != transform)
        {
            audioTransform = FindFlightDescendant(transform, FlightAudioName);
        }

        if (audioTransform == null)
        {
            return;
        }

        AudioSource[] audioSources = audioTransform.GetComponentsInChildren<AudioSource>(true);
        if (audioSources.Length == 0)
        {
            return;
        }

        if (flightLoopAudioSource == null)
        {
            flightLoopAudioSource = FindFlightLoopAudioSource(audioSources);
        }

        if (flightBurstAudioSource == null)
        {
            flightBurstAudioSource = FindFlightBurstAudioSource(
                audioSources,
                flightLoopAudioSource);
        }
    }

    private void EnsureFlightFeedbackObjects()
    {
        ConfigureFlightFeedbackObjects();

        if (flightGeneratedLoopClip == null)
        {
            flightGeneratedLoopClip = CreateProceduralFlightLoopClip();
        }

        if (flightGeneratedBurstClip == null)
        {
            flightGeneratedBurstClip = CreateProceduralFlightBurstClip();
        }
    }

    private void ConfigureFlightFeedbackObjects()
    {
        ConfigureFlightAudioSource(flightLoopAudioSource, true, 28f);

        if (flightBurstAudioSource != flightLoopAudioSource)
        {
            ConfigureFlightAudioSource(flightBurstAudioSource, false, 34f);
        }

        ConfigureFlightTrails();
        ConfigureFlightSpeedLineParticles();
    }

    private void ConfigureFlightAudioSource(AudioSource source, bool loop, float maxDistance)
    {
        if (source == null)
        {
            return;
        }

        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 2f;
        source.maxDistance = maxDistance;
    }

    private void ConfigureFlightTrail(TrailRenderer trail)
    {
        if (trail == null)
        {
            return;
        }

        trail.emitting = false;
    }

    private void ConfigureFlightTrails()
    {
        if (flightTrails == null)
        {
            return;
        }

        for (int i = 0; i < flightTrails.Length; i++)
        {
            ConfigureFlightTrail(flightTrails[i]);
        }
    }

    private void ConfigureFlightSpeedLineParticles()
    {
        if (flightSpeedLineParticles == null)
        {
            return;
        }

        ParticleSystem.MainModule main = flightSpeedLineParticles.main;
        main.loop = true;
        main.playOnAwake = false;
        CaptureFlightParticleInitialRotation();

        ParticleSystem.EmissionModule emission = flightSpeedLineParticles.emission;
        emission.enabled = true;
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(0f);
    }

    private void UpdateFlightAudio(bool flightActive, bool boostStarted, float speed01, float boost01)
    {
        if (flightLoopAudioSource == null && flightBurstAudioSource == null)
        {
            return;
        }

        if (!flightActive)
        {
            if (flightLoopAudioSource != null && flightLoopAudioSource.isPlaying)
            {
                flightLoopAudioSource.Stop();
            }

            return;
        }

        if (flightLoopAudioSource != null)
        {
            AudioClip loopClip = flightLoopClip != null
                ? flightLoopClip
                : flightGeneratedLoopClip;
            if (flightLoopAudioSource.clip != loopClip)
            {
                flightLoopAudioSource.clip = loopClip;
            }

            float intensity = Mathf.Max(speed01, boost01);
            flightLoopAudioSource.volume = Mathf.Lerp(
                flightLoopMinVolume,
                flightLoopMaxVolume,
                intensity);
            flightLoopAudioSource.pitch = Mathf.Lerp(
                flightLoopMinPitch,
                flightLoopMaxPitch,
                intensity);

            if (!flightLoopAudioSource.isPlaying && loopClip != null)
            {
                flightLoopAudioSource.Play();
            }
        }

        if (flightBurstAudioSource != null && (boostStarted || (flightActive && !wasFlightActive)))
        {
            AudioClip burstClip = flightBurstClip != null
                ? flightBurstClip
                : flightGeneratedBurstClip;
            if (burstClip != null)
            {
                flightBurstAudioSource.pitch = boostStarted ? 1.12f : 0.92f;
                flightBurstAudioSource.PlayOneShot(burstClip, flightBurstVolume);
            }
        }
    }

    private void UpdateFlightVfx(bool flightActive, float speed01, float boost01)
    {
        float movementSpeed = flightMotor != null ? flightMotor.FlightVelocity.magnitude : 0f;
        if (!flightActive)
        {
            flightVfxMoving = false;
        }
        else if (movementSpeed >= FlightVfxMoveStartSpeed)
        {
            flightVfxMoving = true;
        }
        else if (movementSpeed <= FlightVfxMoveStopSpeed)
        {
            flightVfxMoving = false;
        }

        float movementIntensity = flightVfxMoving ? Mathf.Clamp01(speed01) : 0f;
        float trailIntensity = flightVfxMoving ? Mathf.Clamp01(Mathf.Max(movementIntensity, boost01)) : 0f;

        UpdateFlightTrails(flightVfxMoving, trailIntensity);
        UpdateFlightParticleOrientation(flightActive);
        UpdateFlightParticles(flightVfxMoving, movementIntensity);
    }

    private void UpdateFlightTrails(bool moving, float intensity)
    {
        if (flightTrails == null)
        {
            return;
        }

        for (int i = 0; i < flightTrails.Length; i++)
        {
            UpdateFlightTrail(flightTrails[i], moving, intensity);
        }
    }

    private void UpdateFlightTrail(TrailRenderer trail, bool moving, float intensity)
    {
        if (trail == null)
        {
            return;
        }

        bool shouldEmit = moving && intensity > 0.08f;
        if (shouldEmit)
        {
            SetFlightGameObjectActive(trail.gameObject, true);
        }

        trail.emitting = shouldEmit;
        trail.time = Mathf.Lerp(flightMinTrailTime, flightMaxTrailTime, intensity);
        trail.widthMultiplier = Mathf.Lerp(flightMinTrailWidth, flightMaxTrailWidth, intensity);

        if (!shouldEmit)
        {
            trail.Clear();
            SetFlightGameObjectActive(trail.gameObject, false);
        }
    }

    private void UpdateFlightParticles(bool moving, float intensity)
    {
        if (flightSpeedLineParticles == null)
        {
            return;
        }

        bool shouldPlay = moving && intensity > 0.005f;
        if (shouldPlay)
        {
            SetFlightGameObjectActive(flightSpeedLineParticles.gameObject, true);
        }

        ParticleSystem.EmissionModule emission = flightSpeedLineParticles.emission;
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(shouldPlay
            ? Mathf.Lerp(flightMinParticleRate, flightMaxParticleRate, intensity)
            : 0f);

        if (shouldPlay && !flightSpeedLineParticles.isPlaying)
        {
            flightSpeedLineParticles.Play();
        }
        else if (!shouldPlay && flightSpeedLineParticles.isPlaying)
        {
            flightSpeedLineParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        if (!shouldPlay)
        {
            SetFlightGameObjectActive(flightSpeedLineParticles.gameObject, false);
        }
    }

    private void UpdateFlightParticleOrientation(bool flightActive)
    {
        if (!orientFlightParticlesWithVerticalTilt ||
            !flightActive ||
            flightMotor == null ||
            flightSpeedLineParticles == null)
        {
            return;
        }

        CaptureFlightParticleInitialRotation();
        flightSpeedLineParticles.transform.rotation =
            flightMotor.FlightVisualRotation * flightSpeedLineParticleInitialLocalRotation;
    }

    private void CaptureFlightParticleInitialRotation()
    {
        if (flightSpeedLineParticles == null)
        {
            flightSpeedLineParticleRotationTransform = null;
            flightSpeedLineParticleInitialLocalRotation = Quaternion.identity;
            return;
        }

        Transform particleTransform = flightSpeedLineParticles.transform;
        if (flightSpeedLineParticleRotationTransform == particleTransform)
        {
            return;
        }

        flightSpeedLineParticleRotationTransform = particleTransform;
        flightSpeedLineParticleInitialLocalRotation = particleTransform.localRotation;
    }

    private void ResetFlightParticleOrientation()
    {
        if (flightSpeedLineParticleRotationTransform != null)
        {
            flightSpeedLineParticleRotationTransform.localRotation = flightSpeedLineParticleInitialLocalRotation;
        }
    }

    private void InstantiateFlightBoostPrefab()
    {
        if (flightBoostPrefab == null)
        {
            return;
        }

        Transform spawnPoint = flightBoostPrefabSpawnPoint != null
            ? flightBoostPrefabSpawnPoint
            : transform;
        Vector3 position = spawnPoint.TransformPoint(flightBoostPrefabLocalOffset);
        Quaternion rotation = spawnPoint.rotation * Quaternion.Euler(flightBoostPrefabEulerOffset);
        Transform parent = parentFlightBoostPrefabToSpawnPoint ? spawnPoint : null;

        GameObject instance = Instantiate(flightBoostPrefab, position, rotation, parent);
        if (flightBoostPrefabLifetime > 0f)
        {
            Destroy(instance, flightBoostPrefabLifetime);
        }
    }

    private void SetFlightFeedbackActive(bool active, bool clearTrails)
    {
        if (!active)
        {
            flightVfxMoving = false;
            ResetFlightParticleOrientation();
        }

        if (flightLoopAudioSource != null && !active)
        {
            flightLoopAudioSource.Stop();
        }

        if (flightSpeedLineParticles != null && !active)
        {
            flightSpeedLineParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            SetFlightGameObjectActive(flightSpeedLineParticles.gameObject, false);
        }

        SetFlightTrailsActive(active, clearTrails);
    }

    private bool HasFlightTrails()
    {
        if (flightTrails == null)
        {
            return false;
        }

        for (int i = 0; i < flightTrails.Length; i++)
        {
            if (flightTrails[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    private TrailRenderer[] FindFlightTrails(Transform feedbackRoot)
    {
        List<TrailRenderer> trails = new List<TrailRenderer>();
        AddNamedFlightTrails(feedbackRoot, trails);

        if (feedbackRoot != transform)
        {
            AddNamedFlightTrails(transform, trails);
        }

        if (trails.Count == 0)
        {
            AddFlightTrailIfValid(flightLeftTrail, trails);
            AddFlightTrailIfValid(flightRightTrail, trails);
        }

        return trails.ToArray();
    }

    private void SetFlightTrailsActive(bool active, bool clearTrails)
    {
        if (flightTrails == null)
        {
            return;
        }

        for (int i = 0; i < flightTrails.Length; i++)
        {
            TrailRenderer trail = flightTrails[i];
            if (trail == null)
            {
                continue;
            }

            trail.emitting = active;
            if (clearTrails)
            {
                trail.Clear();
            }

            if (!active)
            {
                SetFlightGameObjectActive(trail.gameObject, false);
            }
        }
    }

    private static void AddNamedFlightTrails(Transform searchRoot, List<TrailRenderer> trails)
    {
        if (searchRoot == null)
        {
            return;
        }

        TrailRenderer[] candidates = searchRoot.GetComponentsInChildren<TrailRenderer>(true);
        for (int i = 0; i < candidates.Length; i++)
        {
            TrailRenderer candidate = candidates[i];
            if (candidate == null || !IsFlightTrailName(candidate.name))
            {
                continue;
            }

            AddFlightTrailIfValid(candidate, trails);
        }
    }

    private static bool IsFlightTrailName(string objectName)
    {
        return !string.IsNullOrWhiteSpace(objectName) &&
               (objectName.StartsWith(FlightTrailNamePrefix, System.StringComparison.OrdinalIgnoreCase) ||
                objectName.StartsWith(PreviousFlightTrailNamePrefix, System.StringComparison.OrdinalIgnoreCase));
    }

    private static void AddFlightTrailIfValid(TrailRenderer trail, List<TrailRenderer> trails)
    {
        if (trail == null || trails.Contains(trail))
        {
            return;
        }

        trails.Add(trail);
    }

    private T FindNamedFlightComponent<T>(Transform feedbackRoot, string objectName) where T : Component
    {
        Transform target = FindFlightDescendant(feedbackRoot, objectName);
        if (target == null && feedbackRoot != transform)
        {
            target = FindFlightDescendant(transform, objectName);
        }

        return target != null ? target.GetComponent<T>() : null;
    }

    private static Transform FindFlightDescendant(Transform searchRoot, string objectName)
    {
        if (searchRoot == null)
        {
            return null;
        }

        Transform[] descendants = searchRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < descendants.Length; i++)
        {
            if (descendants[i].name == objectName)
            {
                return descendants[i];
            }
        }

        return null;
    }

    private static AudioSource FindFlightLoopAudioSource(AudioSource[] audioSources)
    {
        for (int i = 0; i < audioSources.Length; i++)
        {
            if (audioSources[i] != null && audioSources[i].loop)
            {
                return audioSources[i];
            }
        }

        return audioSources[0];
    }

    private static AudioSource FindFlightBurstAudioSource(
        AudioSource[] audioSources,
        AudioSource loopSource)
    {
        for (int i = 0; i < audioSources.Length; i++)
        {
            if (audioSources[i] != null && audioSources[i] != loopSource && !audioSources[i].loop)
            {
                return audioSources[i];
            }
        }

        for (int i = 0; i < audioSources.Length; i++)
        {
            if (audioSources[i] != null && audioSources[i] != loopSource)
            {
                return audioSources[i];
            }
        }

        return null;
    }

    private static void SetFlightGameObjectActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
        {
            target.SetActive(active);
        }
    }

    private static AudioClip CreateProceduralFlightLoopClip()
    {
        const int sampleRate = 44100;
        const float duration = 1.25f;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleRate;
            float rumble = Mathf.Sin(Mathf.PI * 2f * 72f * t) * 0.16f;
            float air = Mathf.Sin(Mathf.PI * 2f * 311f * t + Mathf.Sin(t * 29f)) * 0.045f;
            float hiss = Mathf.Sin(t * 2197.17f) * Mathf.Sin(t * 467.31f) * 0.035f;
            samples[i] = (rumble + air + hiss) * 0.45f;
        }

        AudioClip clip = AudioClip.Create("Procedural Flight Wind Loop", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static AudioClip CreateProceduralFlightBurstClip()
    {
        const int sampleRate = 44100;
        const float duration = 0.42f;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleRate;
            float normalizedTime = t / duration;
            float envelope = Mathf.Sin(normalizedTime * Mathf.PI) * Mathf.Exp(-normalizedTime * 1.8f);
            float sweep = Mathf.Sin(Mathf.PI * 2f * Mathf.Lerp(120f, 760f, normalizedTime) * t);
            float air = Mathf.Sin(t * 3721.13f) * Mathf.Sin(t * 911.7f);
            samples[i] = (sweep * 0.24f + air * 0.08f) * envelope;
        }

        AudioClip clip = AudioClip.Create("Procedural Flight Burst", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static void DestroyRuntimeObject(Object runtimeObject)
    {
        if (runtimeObject == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(runtimeObject);
            return;
        }

        DestroyImmediate(runtimeObject);
    }
}
