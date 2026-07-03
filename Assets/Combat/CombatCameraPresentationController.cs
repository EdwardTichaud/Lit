using UnityEngine;
using UccCameraController = Opsive.UltimateCharacterController.Camera.CameraController;
using UccCameraControllerHandler = Opsive.UltimateCharacterController.Camera.CameraControllerHandler;

// Role: applique une presentation camera locale pendant les combats sans toucher au timeScale global.
// Usage: cree par CombatSessionManager a l'entree en combat; lit uniquement le contexte local expose par le manager.
// Responsibilities: piloter directement la camera par phase de combat, puis restaurer la camera gameplay precedente.
// Dependencies: CombatSessionManager, LocalPlayerContext via le manager, Opsive CameraController.
// Precautions: presentation locale uniquement; ne pilote aucune logique serveur.
[DefaultExecutionOrder(600)]
public sealed class CombatCameraPresentationController : MonoBehaviour
{
    public static CombatCameraPresentationController Instance { get; private set; }

    [Header("References")]
    [SerializeField] private Camera controlledCamera;
    [SerializeField] private UccCameraController cameraController;
    [SerializeField] private UccCameraControllerHandler cameraControllerHandler;
    [SerializeField] private LitUccCameraCharacterBinder cameraBinder;
    [SerializeField, Tooltip("Autres pilotes camera gameplay a suspendre pendant le combat.")]
    private Behaviour[] additionalGameplayCameraBehaviours;

    [Header("Phase Shots")]
    [SerializeField] private Vector3 playerDecisionCameraOffset = new Vector3(0.75f, 1.55f, -3f);
    [SerializeField] private Vector3 playerActionCameraOffset = new Vector3(0.45f, 1.35f, -2.45f);
    [SerializeField] private Vector3 enemyDecisionCameraOffset = new Vector3(-0.95f, 1.25f, -2.7f);
    [SerializeField] private Vector3 enemyActionCameraOffset = new Vector3(-1.25f, 1.08f, -2.1f);
    [SerializeField] private Vector3 resolvingCameraOffset = new Vector3(0f, 1.65f, -3.2f);
    [SerializeField, Range(0f, 1f)] private float playerDecisionLookBias = 0.62f;
    [SerializeField, Range(0f, 1f)] private float playerActionLookBias = 0.78f;
    [SerializeField, Range(0f, 1f)] private float enemyReactionLookBias = 0.74f;
    [SerializeField, Range(0f, 1f)] private float enemyActionLookBias = 0.92f;
    [SerializeField, Range(15f, 100f)] private float playerDecisionFieldOfView = 50f;
    [SerializeField, Range(15f, 100f)] private float playerActionFieldOfView = 54f;
    [SerializeField, Range(15f, 100f)] private float enemyReactionFieldOfView = 60f;
    [SerializeField, Range(15f, 100f)] private float enemyActionFieldOfView = 68f;
    [SerializeField, Range(15f, 100f)] private float resolvingFieldOfView = 52f;
    [SerializeField, Min(0f)] private float targetLookHeight = 1.25f;
    [SerializeField, Min(0f)] private float cinematicLookHeight = 1.1f;
    [SerializeField, Min(0.01f)] private float minLookDistance = 0.25f;

    [Header("Cinematic Motion")]
    [SerializeField, Min(0.05f)] private float decisionBlendSeconds = 2f;
    [SerializeField, Min(0.05f)] private float actionBlendSeconds = 0.35f;
    [SerializeField, Min(0.05f)] private float enemyActionBlendSeconds = 0.75f;
    [SerializeField, Min(0f)] private float cameraFollowSharpness = 7f;
    [SerializeField, Min(0f)] private float enemyActionFollowSharpness = 9f;
    [SerializeField] private Vector3 subtleBreathingAmplitude = new Vector3(0.08f, 0.03f, 0.06f);
    [SerializeField] private Vector3 enemyActionBreathingAmplitude = new Vector3(0.32f, 0.12f, 0.26f);
    [SerializeField, Min(0f)] private float subtleBreathingFrequency = 0.05f;
    [SerializeField, Min(0f)] private float enemyActionBreathingFrequency = 0.08f;
    [SerializeField, Range(-10f, 10f)] private float enemyReactionRollDegrees = -2.5f;
    [SerializeField, Range(-10f, 10f)] private float enemyActionRollDegrees = -1.5f;

    private struct CombatCameraShot
    {
        public Vector3 LocalOffset;
        public float LookBias;
        public float LookHeight;
        public float FieldOfView;
        public float RollDegrees;
        public float FollowSharpness;
        public Vector3 BreathingAmplitude;
        public float BreathingFrequency;

        public CombatCameraShot(
            Vector3 localOffset,
            float lookBias,
            float lookHeight,
            float fieldOfView,
            float rollDegrees,
            float followSharpness,
            Vector3 breathingAmplitude,
            float breathingFrequency)
        {
            LocalOffset = localOffset;
            LookBias = lookBias;
            LookHeight = lookHeight;
            FieldOfView = fieldOfView;
            RollDegrees = rollDegrees;
            FollowSharpness = followSharpness;
            BreathingAmplitude = breathingAmplitude;
            BreathingFrequency = breathingFrequency;
        }
    }

    private Vector3 originalCameraPosition;
    private Quaternion originalCameraRotation;
    private float originalFieldOfView;
    private bool originalCameraStateStored;
    private Vector3 originalAnchorOffset;
    private bool originalAnchorOffsetStored;
    private bool previousUccControllerEnabled;
    private bool previousUccHandlerEnabled;
    private bool previousUccBinderEnabled;
    private bool[] previousAdditionalEnabled;
    private bool cameraControlActive;
    private bool presentationLogged;
    private bool phaseStateStored;
    private bool activePlayerTurn;
    private CombatSessionPhase activePhase = CombatSessionPhase.Finished;
    private Vector3 transitionStartPosition;
    private Quaternion transitionStartRotation;
    private float transitionStartFieldOfView;
    private float transitionDuration;
    private float transitionElapsed;
    private bool transitioning;
    private float localPauseWeight;

    public static CombatCameraPresentationController EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

#if UNITY_2023_1_OR_NEWER
        Instance = FindAnyObjectByType<CombatCameraPresentationController>();
#else
        Instance = FindObjectOfType<CombatCameraPresentationController>();
#endif
        if (Instance != null)
        {
            return Instance;
        }

        GameObject host = new GameObject("CombatCameraPresentationController");
        DontDestroyOnLoad(host);
        Instance = host.AddComponent<CombatCameraPresentationController>();
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDisable()
    {
        RestoreCameraPresentation();
    }

    private void OnDestroy()
    {
        RestoreCameraPresentation();
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void LateUpdate()
    {
        CombatSessionManager manager = CombatSessionManager.Instance;
        Transform player = null;
        Transform enemy = null;
        bool playerTurn = false;
        CombatSessionPhase phase = CombatSessionPhase.Finished;
        bool hasContext = manager != null &&
            manager.TryGetLocalCombatCameraContext(
                out player,
                out enemy,
                out playerTurn,
                out phase);

        if (!hasContext)
        {
            UpdateLocalPauseWeight(holding: false);
            TimeManager.Instance?.SetCombatTimeTargets(null, null, defensiveReactionActive: false);
            if (localPauseWeight <= 0f)
            {
                RestoreCameraPresentation();
            }

            return;
        }

        UpdateLocalPauseWeight(ShouldHoldLocalPause(playerTurn, phase));
        TimeManager.EnsureInstance().SetCombatTimeTargets(player, enemy, ShouldSlowDefensiveReaction(playerTurn, phase));
        if (!EnsureCameraRig())
        {
            return;
        }

        if (!BeginCameraControl())
        {
            return;
        }

        if (!phaseStateStored || activePlayerTurn != playerTurn || activePhase != phase)
        {
            activePlayerTurn = playerTurn;
            activePhase = phase;
            phaseStateStored = true;
            StartCameraTransition(ResolveBlendSeconds(playerTurn, phase));
        }

        ApplyCameraPose(player, enemy, playerTurn, phase);
    }

    private bool EnsureCameraRig()
    {
        if (controlledCamera == null || !controlledCamera.isActiveAndEnabled)
        {
            controlledCamera = Camera.main;
        }

        if (controlledCamera == null)
        {
#if UNITY_2023_1_OR_NEWER
            controlledCamera = FindAnyObjectByType<Camera>();
#else
            controlledCamera = FindObjectOfType<Camera>();
#endif
        }

        if (cameraController == null && controlledCamera != null)
        {
            cameraController = controlledCamera.GetComponentInParent<UccCameraController>();
        }

        if (cameraController == null)
        {
#if UNITY_2023_1_OR_NEWER
            cameraController = FindAnyObjectByType<UccCameraController>();
#else
            cameraController = FindObjectOfType<UccCameraController>();
#endif
        }

        if (controlledCamera == null && cameraController != null)
        {
            controlledCamera = cameraController.GetComponentInChildren<Camera>();
        }

        if (cameraController != null)
        {
            if (cameraControllerHandler == null)
            {
                cameraControllerHandler = cameraController.GetComponent<UccCameraControllerHandler>();
            }

            if (cameraBinder == null)
            {
                cameraBinder = cameraController.GetComponent<LitUccCameraCharacterBinder>();
            }
        }

        return controlledCamera != null && controlledCamera.isActiveAndEnabled;
    }

    private bool BeginCameraControl()
    {
        if (cameraControlActive)
        {
            return true;
        }

        if (controlledCamera == null)
        {
            return false;
        }

        StoreOriginalCameraState();
        SuspendGameplayCameraDrivers();
        cameraControlActive = true;
        phaseStateStored = false;
        transitioning = false;

        if (!presentationLogged)
        {
            Debug.Log("[CombatCamera] Cinematic combat camera active.");
            presentationLogged = true;
        }

        return true;
    }

    private void StoreOriginalCameraState()
    {
        if (controlledCamera != null && !originalCameraStateStored)
        {
            Transform cameraTransform = controlledCamera.transform;
            originalCameraPosition = cameraTransform.position;
            originalCameraRotation = cameraTransform.rotation;
            originalFieldOfView = controlledCamera.fieldOfView;
            originalCameraStateStored = true;
        }

        if (cameraController != null && !originalAnchorOffsetStored)
        {
            originalAnchorOffset = cameraController.AnchorOffset;
            originalAnchorOffsetStored = true;
        }
    }

    private void SuspendGameplayCameraDrivers()
    {
        if (additionalGameplayCameraBehaviours != null)
        {
            previousAdditionalEnabled = new bool[additionalGameplayCameraBehaviours.Length];
            for (int i = 0; i < additionalGameplayCameraBehaviours.Length; i++)
            {
                Behaviour behaviour = additionalGameplayCameraBehaviours[i];
                previousAdditionalEnabled[i] = behaviour != null && behaviour.enabled;
                if (behaviour != null && behaviour != this)
                {
                    behaviour.enabled = false;
                }
            }
        }

        previousUccControllerEnabled = cameraController != null && cameraController.enabled;
        previousUccHandlerEnabled = cameraControllerHandler != null && cameraControllerHandler.enabled;
        previousUccBinderEnabled = cameraBinder != null && cameraBinder.enabled;

        if (cameraBinder != null)
        {
            cameraBinder.enabled = false;
        }

        if (cameraControllerHandler != null)
        {
            cameraControllerHandler.enabled = false;
        }

        if (cameraController != null)
        {
            cameraController.enabled = false;
        }
    }

    private void UpdateLocalPauseWeight(bool holding)
    {
        float target = holding ? 1f : 0f;
        float duration = target > localPauseWeight ? decisionBlendSeconds : actionBlendSeconds;
        localPauseWeight = Mathf.MoveTowards(
            localPauseWeight,
            target,
            Time.unscaledDeltaTime / Mathf.Max(0.05f, duration));
    }

    private static bool ShouldHoldLocalPause(bool playerTurn, CombatSessionPhase phase)
    {
        if (phase == CombatSessionPhase.Decision)
        {
            return true;
        }

        if (!playerTurn && phase == CombatSessionPhase.EnemyAction)
        {
            return true;
        }

        return playerTurn && phase == CombatSessionPhase.TurnActive;
    }

    private static bool ShouldSlowDefensiveReaction(bool playerTurn, CombatSessionPhase phase)
    {
        return !playerTurn && phase == CombatSessionPhase.Decision;
    }

    private float ResolveBlendSeconds(bool playerTurn, CombatSessionPhase phase)
    {
        if (phase == CombatSessionPhase.Decision)
        {
            return decisionBlendSeconds;
        }

        if (!playerTurn && phase == CombatSessionPhase.EnemyAction)
        {
            return enemyActionBlendSeconds;
        }

        return actionBlendSeconds;
    }

    private void StartCameraTransition(float duration)
    {
        if (controlledCamera == null)
        {
            return;
        }

        transitionStartPosition = controlledCamera.transform.position;
        transitionStartRotation = controlledCamera.transform.rotation;
        transitionStartFieldOfView = controlledCamera.fieldOfView;
        transitionDuration = Mathf.Max(0f, duration);
        transitionElapsed = 0f;
        transitioning = transitionDuration > 0f;
    }

    private void ApplyCameraPose(Transform player, Transform enemy, bool playerTurn, CombatSessionPhase phase)
    {
        if (controlledCamera == null)
        {
            return;
        }

        CombatCameraShot shot = ResolveShot(playerTurn, phase);
        ResolveDesiredPose(
            player,
            enemy,
            shot,
            out Vector3 targetPosition,
            out Quaternion targetRotation,
            out float targetFov);

        if (transitioning)
        {
            transitionElapsed += Time.unscaledDeltaTime;
            float t = transitionDuration <= 0f
                ? 1f
                : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(transitionElapsed / transitionDuration));

            controlledCamera.transform.SetPositionAndRotation(
                Vector3.Lerp(transitionStartPosition, targetPosition, t),
                Quaternion.Slerp(transitionStartRotation, targetRotation, t));
            controlledCamera.fieldOfView = Mathf.Lerp(transitionStartFieldOfView, targetFov, t);

            if (t >= 1f)
            {
                transitioning = false;
            }

            return;
        }

        float followT = shot.FollowSharpness <= 0f
            ? 1f
            : 1f - Mathf.Exp(-shot.FollowSharpness * Time.unscaledDeltaTime);
        controlledCamera.transform.SetPositionAndRotation(
            Vector3.Lerp(controlledCamera.transform.position, targetPosition, followT),
            Quaternion.Slerp(controlledCamera.transform.rotation, targetRotation, followT));
        controlledCamera.fieldOfView = Mathf.Lerp(controlledCamera.fieldOfView, targetFov, followT);
    }

    private CombatCameraShot ResolveShot(bool playerTurn, CombatSessionPhase phase)
    {
        if (phase == CombatSessionPhase.Resolving)
        {
            return new CombatCameraShot(
                resolvingCameraOffset,
                0.68f,
                targetLookHeight,
                resolvingFieldOfView,
                0f,
                cameraFollowSharpness,
                subtleBreathingAmplitude,
                subtleBreathingFrequency);
        }

        if (!playerTurn)
        {
            if (phase == CombatSessionPhase.EnemyAction)
            {
                return new CombatCameraShot(
                    enemyActionCameraOffset,
                    enemyActionLookBias,
                    cinematicLookHeight,
                    enemyActionFieldOfView,
                    enemyActionRollDegrees,
                    enemyActionFollowSharpness,
                    enemyActionBreathingAmplitude,
                    enemyActionBreathingFrequency);
            }

            return new CombatCameraShot(
                enemyDecisionCameraOffset,
                enemyReactionLookBias,
                cinematicLookHeight,
                enemyReactionFieldOfView,
                enemyReactionRollDegrees,
                cameraFollowSharpness,
                subtleBreathingAmplitude,
                subtleBreathingFrequency);
        }

        if (phase == CombatSessionPhase.PlayerAction)
        {
            return new CombatCameraShot(
                playerActionCameraOffset,
                playerActionLookBias,
                targetLookHeight,
                playerActionFieldOfView,
                0f,
                cameraFollowSharpness,
                subtleBreathingAmplitude,
                subtleBreathingFrequency);
        }

        return new CombatCameraShot(
            playerDecisionCameraOffset,
            playerDecisionLookBias,
            targetLookHeight,
            playerDecisionFieldOfView,
            0f,
            cameraFollowSharpness,
            subtleBreathingAmplitude,
            subtleBreathingFrequency);
    }

    private void ResolveDesiredPose(
        Transform player,
        Transform enemy,
        CombatCameraShot shot,
        out Vector3 position,
        out Quaternion rotation,
        out float fieldOfView)
    {
        ResolveCombatAxes(player, enemy, out Vector3 origin, out Vector3 forward, out Vector3 right);

        position = origin +
                   right * shot.LocalOffset.x +
                   Vector3.up * shot.LocalOffset.y +
                   forward * shot.LocalOffset.z;
        position += ResolveBreathingOffset(forward, right, shot);

        Vector3 lookPosition = ResolveLookPosition(player, enemy, shot);
        Vector3 direction = lookPosition - position;
        if (direction.sqrMagnitude < minLookDistance * minLookDistance)
        {
            direction = forward;
        }

        rotation = direction.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(direction.normalized, Vector3.up)
            : controlledCamera.transform.rotation;
        if (!Mathf.Approximately(shot.RollDegrees, 0f))
        {
            rotation *= Quaternion.Euler(0f, 0f, shot.RollDegrees);
        }

        fieldOfView = shot.FieldOfView;
    }

    private static void ResolveCombatAxes(
        Transform player,
        Transform enemy,
        out Vector3 origin,
        out Vector3 forward,
        out Vector3 right)
    {
        origin = player != null
            ? player.position
            : enemy != null ? enemy.position : Vector3.zero;

        forward = enemy != null && player != null
            ? enemy.position - player.position
            : player != null ? player.forward : enemy != null ? -enemy.forward : Vector3.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = player != null ? player.forward : Vector3.forward;
            forward.y = 0f;
        }

        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.forward;
        }

        forward.Normalize();
        right = Vector3.Cross(Vector3.up, forward);
        if (right.sqrMagnitude < 0.0001f)
        {
            right = Vector3.right;
        }
        else
        {
            right.Normalize();
        }
    }

    private Vector3 ResolveLookPosition(Transform player, Transform enemy, CombatCameraShot shot)
    {
        if (enemy != null && player != null)
        {
            Vector3 playerPoint = player.position + Vector3.up * targetLookHeight;
            Vector3 enemyPoint = enemy.position + Vector3.up * shot.LookHeight;
            return Vector3.Lerp(playerPoint, enemyPoint, Mathf.Clamp01(shot.LookBias));
        }

        Transform target = enemy != null ? enemy : player;
        return target != null
            ? target.position + Vector3.up * shot.LookHeight
            : Vector3.up * shot.LookHeight;
    }

    private static Vector3 ResolveBreathingOffset(Vector3 forward, Vector3 right, CombatCameraShot shot)
    {
        if (shot.BreathingFrequency <= 0f || shot.BreathingAmplitude == Vector3.zero)
        {
            return Vector3.zero;
        }

        float phase = Time.unscaledTime * shot.BreathingFrequency * Mathf.PI * 2f;
        return right * (Mathf.Sin(phase) * shot.BreathingAmplitude.x) +
               Vector3.up * (Mathf.Sin(phase * 0.7f + 1.1f) * shot.BreathingAmplitude.y) +
               forward * (Mathf.Cos(phase * 0.55f + 0.4f) * shot.BreathingAmplitude.z);
    }

    private void RestoreCameraPresentation()
    {
        TimeManager.Instance?.RestoreCombatTime();
        RestoreCameraState();
    }

    private void RestoreCameraState()
    {
        if (!cameraControlActive &&
            !originalCameraStateStored &&
            !originalAnchorOffsetStored &&
            previousAdditionalEnabled == null)
        {
            return;
        }

        if (additionalGameplayCameraBehaviours != null && previousAdditionalEnabled != null)
        {
            int count = Mathf.Min(additionalGameplayCameraBehaviours.Length, previousAdditionalEnabled.Length);
            for (int i = 0; i < count; i++)
            {
                if (additionalGameplayCameraBehaviours[i] != null)
                {
                    additionalGameplayCameraBehaviours[i].enabled = previousAdditionalEnabled[i];
                }
            }
        }

        if (cameraController != null)
        {
            if (originalAnchorOffsetStored)
            {
                cameraController.AnchorOffset = originalAnchorOffset;
            }

            cameraController.enabled = previousUccControllerEnabled;
        }

        if (cameraControllerHandler != null)
        {
            cameraControllerHandler.enabled = previousUccHandlerEnabled;
        }

        if (cameraBinder != null)
        {
            cameraBinder.enabled = previousUccBinderEnabled;
        }

        bool restoredByOpsive = false;
        if (cameraController != null &&
            cameraController.enabled &&
            cameraController.Character != null &&
            cameraController.ActiveViewType != null)
        {
            cameraController.PositionImmediately(true);
            restoredByOpsive = true;
        }

        if (controlledCamera != null && originalCameraStateStored)
        {
            if (!restoredByOpsive)
            {
                controlledCamera.transform.SetPositionAndRotation(originalCameraPosition, originalCameraRotation);
            }

            controlledCamera.fieldOfView = originalFieldOfView;
        }

        originalCameraStateStored = false;
        originalAnchorOffsetStored = false;
        previousAdditionalEnabled = null;
        cameraControlActive = false;
        presentationLogged = false;
        phaseStateStored = false;
        transitioning = false;
        activePhase = CombatSessionPhase.Finished;
        activePlayerTurn = false;
        localPauseWeight = 0f;
    }
}
