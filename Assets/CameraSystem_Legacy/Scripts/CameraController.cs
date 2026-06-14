using UnityEngine;

[DisallowMultipleComponent]
public class CameraController : MonoBehaviour
{
    [Header("Rig References")]
    [Tooltip("Camera principale utilisee par le rig.")]
    public Camera mainCam;
    [Tooltip("Pivot monde libre du rig.")]
    [SerializeField] private Transform cameraAnchor;
    [Tooltip("Pivot de rotation horizontale.")]
    [SerializeField] private Transform yawPivot;
    [Tooltip("Pivot de rotation verticale.")]
    [SerializeField] private Transform pitchPivot;

    [Header("Startup Reset")]
    [SerializeField, Tooltip("Reinitialise le Transform local de CameraSystem au lancement du jeu.")]
    private bool resetTransformOnPlay = true;
    [SerializeField] private Vector3 launchLocalPosition = Vector3.zero;
    [SerializeField] private Vector3 launchLocalEulerAngles = Vector3.zero;
    [SerializeField] private Vector3 launchLocalScale = Vector3.one;

    [Header("Compatibility")]
    [Tooltip("Cible logique courante resolue par la camera.")]
    public Transform mainCamCurrentTarget;
    [Tooltip("Sommet actuel de la pile d'override de focus.")]
    public Transform followOverrideTarget;
    [Tooltip("Offset applique a la cible courante.")]
    public Vector3 targetOffset = new Vector3(0f, 1.8f, 0f);
    [Tooltip("Offset applique a une cible temporaire d'override.")]
    public Vector3 overrideTargetOffset = Vector3.zero;
    [Tooltip("Utilise le targetOffset standard pour les overrides.")]
    public bool useTargetOffsetForOverride = false;

    [Header("Zoom Profile")]
    [SerializeField, Range(0f, 1f), Tooltip("0 = zoom tactique proche, 1 = zoom tactique lointain.")]
    private float zoomNormalized = 0.28f;
    [SerializeField] private float zoomInSpeed = 2f;
    [SerializeField] private float zoomOutSpeed = 2.2f;
    [SerializeField] private float zoomSharpness = 10f;
    [SerializeField] private float minDistance = 3.4f;
    [SerializeField] private float maxDistance = 18f;
    [SerializeField] private float minPivotHeight = 0.95f;
    [SerializeField] private float maxPivotHeight = 8.5f;
    [SerializeField] private float zoomedInPitch = 42f;
    [SerializeField] private float zoomedOutPitch = 63f;
    [SerializeField] private float minPanSpeed = 8f;
    [SerializeField] private float maxPanSpeed = 24f;

    [Header("Rotation")]
    [SerializeField] private float rotationSharpness = 12f;
    [SerializeField] private float minPitch = 0f;
    [SerializeField] private float maxPitch = 89f;
    [SerializeField] private float pitchOffsetMin = -45f;
    [SerializeField] private float pitchOffsetMax = 45f;

    [Header("Pan")]
    [SerializeField, Tooltip("Conversion pixels drag -> metres monde, modulee par la distance de camera.")]
    private float dragPanDistanceFactor = 0.012f;

    [Header("Collision Smoothing")]
    [SerializeField] private float anchorSharpness = 14f;
    [SerializeField] private float obstructionSharpness = 18f;
    [SerializeField] private float releaseSharpness = 10f;

    [Header("Legacy Collision")]
    [SerializeField, Tooltip("Ancien comportement : rapproche/repositionne physiquement la camera pour eviter les murs. Desactive par defaut pour conserver une camera type BG3.")]
    private bool allowLegacyObstacleRepositioning = false;

    [Header("Trigger-Driven Zoom Test")]
    [SerializeField, Tooltip("Desactive le focus auto et le clamp collision pour tester un zoom pilote manuellement.")]
    private bool triggerDrivenZoomTestMode = true;

    [Header("Subsystems")]
    [SerializeField] private CrpgCameraInput cameraInput = new CrpgCameraInput();
    [SerializeField] private CrpgCameraFocus cameraFocus = new CrpgCameraFocus();
    [SerializeField] private CrpgCameraCollision cameraCollision = new CrpgCameraCollision();

    [Header("Run Speed Effect")]
    [SerializeField] private RunSpeedCameraEffect runSpeedEffect = new RunSpeedCameraEffect();

    [Header("Fall Speed Effect")]
    [SerializeField] private FallSpeedCameraEffect fallSpeedEffect = new FallSpeedCameraEffect();

    [Header("Combat Camera")]
    [SerializeField, Tooltip("Active le cadrage special pendant les combats tour par tour.")]
    private bool combatCameraEnabled = true;
    [SerializeField, Tooltip("Offset lateral applique quand c'est le tour du joueur. Negatif = epaule gauche.")]
    private float combatPlayerTurnSideOffset = -1.1f;
    [SerializeField, Tooltip("Offset lateral applique quand c'est le tour de l'ennemi.")]
    private float combatEnemyTurnSideOffset = 1.1f;
    [SerializeField, Min(0.1f), Tooltip("Recul du plan combat par rapport au joueur.")]
    private float combatDistance = 10f;
    [SerializeField, Tooltip("Hauteur du point camera au-dessus du joueur.")]
    private float combatShoulderHeight = 2.05f;
    [SerializeField, Tooltip("Offset vertical du point vise sur l'ennemi pour compenser son pivot aux pieds.")]
    private float combatLookAtYOffset = 1f;
    [SerializeField, Tooltip("Lissage du repositionnement entre deux plans de combat.")]
    private float combatCameraSharpness = 10f;
    [SerializeField, Tooltip("Oscillation verticale du mouvement de respiration.")]
    private float combatBreathVerticalAmplitude = 0.05f;
    [SerializeField, Tooltip("Oscillation laterale du mouvement de respiration.")]
    private float combatBreathHorizontalAmplitude = 0.025f;
    [SerializeField, Tooltip("Oscillation avant/arriere du mouvement de respiration.")]
    private float combatBreathDepthAmplitude = 0.06f;
    [SerializeField, Tooltip("Frequence du mouvement de respiration.")]
    private float combatBreathFrequency = 0.85f;

    private bool runtimeInitialized;
    private float desiredYaw;
    private float currentYaw;
    private float manualPitchOffset;
    private float currentPitch;
    private float desiredZoomNormalized;
    private float currentZoomNormalized;
    private float currentDistance;
    private Vector3 currentAnchorPosition;
    private bool combatCameraRuntimeInitialized;
    private bool combatCameraWasActive;
    private Vector3 currentCombatCameraPosition;
    private Quaternion currentCombatCameraRotation;
    private Component fixedCameraSource;
    private Transform fixedCameraPoint;
    private Transform fixedCameraTarget;
    private Vector3 fixedCameraLookAtOffset;
    private int fixedCameraPriority = int.MinValue;
    private float fixedCameraTransitionSharpness = 8f;
    private bool fixedCameraPoseInitialized;
    private Vector3 currentFixedCameraPosition;
    private Quaternion currentFixedCameraRotation = Quaternion.identity;
    private bool launchTransformResetApplied;

    public bool FixedCameraActive => fixedCameraSource != null && fixedCameraPoint != null && fixedCameraTarget != null;
    public Camera MainCamera => mainCam;

    public bool TryGetFixedCameraMovementBasis(out Vector3 forward, out Vector3 right)
    {
        forward = Vector3.zero;
        right = Vector3.zero;

        if (!FixedCameraActive)
        {
            return false;
        }

        Quaternion referenceRotation = fixedCameraPoseInitialized
            ? currentFixedCameraRotation
            : mainCam != null ? mainCam.transform.rotation : Quaternion.identity;
        forward = Vector3.ProjectOnPlane(referenceRotation * Vector3.forward, Vector3.up);
        right = Vector3.ProjectOnPlane(referenceRotation * Vector3.right, Vector3.up);

        if (forward.sqrMagnitude <= 0.0001f || right.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        forward.Normalize();
        right.Normalize();
        return true;
    }

    private void Awake()
    {
        ApplyLaunchTransformReset();
        TryResolveRigReferences();
        ValidateFields();
        runSpeedEffect?.Initialize(mainCam);
    }

    private void OnEnable()
    {
        ApplyLaunchTransformReset();
        LocalInputRouter.EnsureInitialized();
        LocalInputRouter.SetCameraFreeModeActive(false, suppressImmediateCharacterMove: true);
        cameraInput.Bind();
        cameraFocus.Reset();
        mainCamCurrentTarget = null;
        followOverrideTarget = null;
        runtimeInitialized = false;
        combatCameraRuntimeInitialized = false;
        combatCameraWasActive = false;
    }

    private void OnDisable()
    {
        ClearFixedCameraRuntime();
        cameraInput.Unbind();
        runSpeedEffect?.Cleanup(mainCam);
    }

    private void OnDestroy()
    {
        runSpeedEffect?.Cleanup(mainCam);
    }

    private void LateUpdate()
    {
        if (!TryResolveRigReferences())
        {
            return;
        }

        float deltaTime = Application.isPlaying ? Time.unscaledDeltaTime : 1f / 60f;
        if (deltaTime <= 0f)
        {
            deltaTime = 1f / 60f;
        }

        if (TryApplyCombatCamera(deltaTime))
        {
            return;
        }

        Transform gameplayTarget = ResolveGameplayTarget();
        followOverrideTarget = cameraFocus.GetTopOverrideTarget();
        Transform logicalTarget = followOverrideTarget != null ? followOverrideTarget : gameplayTarget;
        mainCamCurrentTarget = logicalTarget;

        Vector3 logicalFocusPoint = ResolveFocusPoint(logicalTarget, followOverrideTarget != null);
        InitializeRuntimeState(logicalFocusPoint);

        if (triggerDrivenZoomTestMode)
        {
            if (LocalInputRouter.CameraFreeModeActive)
            {
                LocalInputRouter.SetCameraFreeModeActive(false, suppressImmediateCharacterMove: false);
            }

            cameraFocus.SetFreeCameraMode(false);
        }

        bool inputBlocked = InputFocusStack.HasAnyFocusBlockingCamera();
        CrpgCameraInput.FrameState inputState = cameraInput.Collect(inputBlocked, deltaTime);

        if (TryApplyFixedCamera(deltaTime))
        {
            zoomNormalized = currentZoomNormalized;
            return;
        }

        SyncExternalZoomRequest();
        UpdateZoom(inputState.zoomDelta, deltaTime);
        UpdateRotation(inputState.orbitDelta, deltaTime);

        Vector3 focusPoint;
        if (triggerDrivenZoomTestMode)
        {
            focusPoint = cameraFocus.Update(
                logicalFocusPoint,
                Vector3.zero,
                recenterRequested: false,
                toggleFreeCameraRequested: false,
                deltaTime);
        }
        else
        {
            Vector3 panDelta = ResolveWorldPanDelta(inputState, deltaTime);
            focusPoint = cameraFocus.Update(
                logicalFocusPoint,
                panDelta,
                inputState.recenterRequested,
                inputState.toggleFreeCameraRequested,
                deltaTime);
        }

        UpdateRig(focusPoint, logicalTarget, deltaTime);
        runSpeedEffect?.UpdateEffect(mainCam, gameplayTarget, deltaTime, fallSpeedEffect);

        zoomNormalized = currentZoomNormalized;
    }

    public void SetTriggerDrivenZoomTestMode(bool active)
    {
        triggerDrivenZoomTestMode = active;
        if (triggerDrivenZoomTestMode && LocalInputRouter.CameraFreeModeActive)
        {
            LocalInputRouter.SetCameraFreeModeActive(false, suppressImmediateCharacterMove: false);
            cameraFocus.SetFreeCameraMode(false);
        }
    }

    public void SetZoomNormalized(float normalized)
    {
        float clamped = Mathf.Clamp01(normalized);
        desiredZoomNormalized = clamped;
        zoomNormalized = clamped;

        if (!runtimeInitialized)
        {
            currentZoomNormalized = clamped;
        }
    }

    public void SnapZoomNormalized(float normalized)
    {
        float clamped = Mathf.Clamp01(normalized);
        zoomNormalized = clamped;
        desiredZoomNormalized = clamped;
        currentZoomNormalized = clamped;

        if (!runtimeInitialized)
        {
            return;
        }

        currentDistance = EvaluateDistance(clamped);
        currentPitch = Mathf.Clamp(EvaluateProfilePitch(clamped) + manualPitchOffset, minPitch, maxPitch);
    }

    private void SyncExternalZoomRequest()
    {
        float clampedInspectorZoom = Mathf.Clamp01(zoomNormalized);
        if (!runtimeInitialized)
        {
            desiredZoomNormalized = clampedInspectorZoom;
            currentZoomNormalized = clampedInspectorZoom;
            return;
        }

        bool inspectorValueChanged = Mathf.Abs(clampedInspectorZoom - desiredZoomNormalized) > 0.0001f;
        bool valueCameFromThisController = Mathf.Abs(clampedInspectorZoom - currentZoomNormalized) <= 0.0001f;
        if (inspectorValueChanged && !valueCameFromThisController)
        {
            desiredZoomNormalized = clampedInspectorZoom;
        }
    }

    public void SetFollowOverride(Transform target)
    {
        cameraFocus.PushOverride(target);
        followOverrideTarget = cameraFocus.GetTopOverrideTarget();
    }

    public void ClearFollowOverride(Transform target)
    {
        cameraFocus.ClearOverride(target);
        followOverrideTarget = cameraFocus.GetTopOverrideTarget();
    }

    public bool TrySetFixedCamera(
        Component source,
        Transform cameraPoint,
        Transform target,
        Vector3 lookAtOffset,
        int priority = 0,
        float transitionSharpness = 8f)
    {
        if (source == null || cameraPoint == null || target == null)
        {
            return false;
        }

        if (fixedCameraSource != null && fixedCameraSource != source && priority < fixedCameraPriority)
        {
            return false;
        }

        fixedCameraSource = source;
        fixedCameraPoint = cameraPoint;
        fixedCameraTarget = target;
        fixedCameraLookAtOffset = lookAtOffset;
        fixedCameraPriority = priority;
        fixedCameraTransitionSharpness = Mathf.Max(0f, transitionSharpness);
        return true;
    }

    public void ReleaseFixedCamera(Component source)
    {
        if (source != null && fixedCameraSource != source)
        {
            return;
        }

        ClearFixedCameraRuntime();
    }

    public void RecenterOnCurrentTarget(bool immediate = false)
    {
        Transform gameplayTarget = ResolveGameplayTarget();
        followOverrideTarget = cameraFocus.GetTopOverrideTarget();
        Transform logicalTarget = followOverrideTarget != null ? followOverrideTarget : gameplayTarget;
        Vector3 logicalFocusPoint = ResolveFocusPoint(logicalTarget, followOverrideTarget != null);

        if (immediate)
        {
            LocalInputRouter.SetCameraFreeModeActive(false, suppressImmediateCharacterMove: true);
            cameraFocus.SetFreeCameraMode(false);
            cameraFocus.SnapTo(logicalFocusPoint);
            currentAnchorPosition = logicalFocusPoint + Vector3.up * EvaluatePivotHeight(currentZoomNormalized);
        }
        else
        {
            LocalInputRouter.SetCameraFreeModeActive(false, suppressImmediateCharacterMove: true);
            cameraFocus.Update(
                logicalFocusPoint,
                Vector3.zero,
                recenterRequested: true,
                toggleFreeCameraRequested: false,
                Time.unscaledDeltaTime > 0f ? Time.unscaledDeltaTime : 1f / 60f);
        }
    }

    private void UpdateZoom(float zoomDelta, float deltaTime)
    {
        float next = desiredZoomNormalized;
        if (Mathf.Abs(zoomDelta) > 0.0001f)
        {
            float speed = zoomDelta < 0f ? zoomInSpeed : zoomOutSpeed;
            next = Mathf.Clamp01(next + zoomDelta * speed);
        }

        desiredZoomNormalized = next;

        if (zoomSharpness <= 0f)
        {
            currentZoomNormalized = desiredZoomNormalized;
            return;
        }

        float t = 1f - Mathf.Exp(-zoomSharpness * deltaTime);
        currentZoomNormalized = Mathf.Lerp(currentZoomNormalized, desiredZoomNormalized, t);
    }

    private bool TryApplyCombatCamera(float deltaTime)
    {
        if (!combatCameraEnabled)
        {
            ResetCombatCameraRuntimeIfNeeded();
            return false;
        }

        CombatSessionManager combatManager = CombatSessionManager.Instance;
        if (combatManager == null ||
            !combatManager.TryGetLocalCombatCameraContext(out Transform player, out Transform enemy, out bool playerTurn))
        {
            ResetCombatCameraRuntimeIfNeeded();
            return false;
        }

        if (LocalInputRouter.CameraFreeModeActive)
        {
            LocalInputRouter.SetCameraFreeModeActive(false, suppressImmediateCharacterMove: false);
        }

        cameraFocus.SetFreeCameraMode(false);
        followOverrideTarget = enemy;
        mainCamCurrentTarget = enemy;
        runSpeedEffect?.ResetEffect(mainCam);

        Vector3 desiredPosition = ResolveCombatCameraPosition(player, enemy, playerTurn);
        Vector3 lookTarget = ResolveCombatLookTarget(enemy);
        Quaternion desiredRotation = Quaternion.LookRotation((lookTarget - desiredPosition).normalized, Vector3.up);

        if (!combatCameraRuntimeInitialized)
        {
            currentCombatCameraPosition = desiredPosition;
            currentCombatCameraRotation = desiredRotation;
            combatCameraRuntimeInitialized = true;
        }
        else
        {
            float combatT = combatCameraSharpness <= 0f ? 1f : 1f - Mathf.Exp(-combatCameraSharpness * deltaTime);
            currentCombatCameraPosition = Vector3.Lerp(currentCombatCameraPosition, desiredPosition, combatT);
            currentCombatCameraRotation = Quaternion.Slerp(currentCombatCameraRotation, desiredRotation, combatT);
        }

        ApplyDirectCameraPose(currentCombatCameraPosition, currentCombatCameraRotation);
        combatCameraWasActive = true;
        return true;
    }

    private bool TryApplyFixedCamera(float deltaTime)
    {
        if (fixedCameraSource == null && fixedCameraPoint == null && fixedCameraTarget == null)
        {
            return false;
        }

        if (fixedCameraSource == null || fixedCameraPoint == null || fixedCameraTarget == null)
        {
            ClearFixedCameraRuntime();
            return false;
        }

        if (!fixedCameraTarget.gameObject.activeInHierarchy)
        {
            ClearFixedCameraRuntime();
            return false;
        }

        if (LocalInputRouter.CameraFreeModeActive)
        {
            LocalInputRouter.SetCameraFreeModeActive(false, suppressImmediateCharacterMove: false);
        }

        cameraFocus.SetFreeCameraMode(false);
        followOverrideTarget = fixedCameraTarget;
        mainCamCurrentTarget = fixedCameraTarget;
        runSpeedEffect?.ResetEffect(mainCam);

        Vector3 desiredCameraPosition = fixedCameraPoint.position;
        Vector3 lookTarget = fixedCameraTarget.position + fixedCameraLookAtOffset;
        Vector3 lookDirection = lookTarget - desiredCameraPosition;
        if (lookDirection.sqrMagnitude <= 0.0001f)
        {
            lookDirection = fixedCameraPoint.forward;
        }

        if (lookDirection.sqrMagnitude <= 0.0001f)
        {
            lookDirection = Vector3.forward;
        }

        Quaternion desiredCameraRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        if (!fixedCameraPoseInitialized)
        {
            InitializeFixedCameraPose(desiredCameraPosition, desiredCameraRotation);
        }

        float t = fixedCameraTransitionSharpness <= 0f ? 1f : 1f - Mathf.Exp(-fixedCameraTransitionSharpness * deltaTime);
        currentFixedCameraPosition = Vector3.Lerp(currentFixedCameraPosition, desiredCameraPosition, t);
        currentFixedCameraRotation = Quaternion.Slerp(currentFixedCameraRotation, desiredCameraRotation, t);

        ApplyDirectCameraPose(currentFixedCameraPosition, currentFixedCameraRotation);
        return true;
    }

    private void InitializeFixedCameraPose(Vector3 fallbackPosition, Quaternion fallbackRotation)
    {
        if (mainCam != null)
        {
            Transform camTransform = mainCam.transform;
            currentFixedCameraPosition = camTransform.position;
            currentFixedCameraRotation = camTransform.rotation;
        }
        else
        {
            currentFixedCameraPosition = fallbackPosition;
            currentFixedCameraRotation = fallbackRotation;
        }

        fixedCameraPoseInitialized = true;
    }

    private void ResetCombatCameraRuntimeIfNeeded()
    {
        if (!combatCameraWasActive)
        {
            return;
        }

        combatCameraWasActive = false;
        combatCameraRuntimeInitialized = false;
        runtimeInitialized = false;
    }

    private void UpdateRotation(Vector2 orbitDelta, float deltaTime)
    {
        desiredYaw += orbitDelta.x;
        manualPitchOffset = Mathf.Clamp(manualPitchOffset + orbitDelta.y, pitchOffsetMin, pitchOffsetMax);

        float desiredPitch = Mathf.Clamp(EvaluateProfilePitch(currentZoomNormalized) + manualPitchOffset, minPitch, maxPitch);
        float rotationT = rotationSharpness <= 0f ? 1f : 1f - Mathf.Exp(-rotationSharpness * deltaTime);
        currentYaw = Mathf.LerpAngle(currentYaw, desiredYaw, rotationT);
        currentPitch = Mathf.Lerp(currentPitch, desiredPitch, rotationT);
    }

    private Vector3 ResolveWorldPanDelta(CrpgCameraInput.FrameState inputState, float deltaTime)
    {
        Vector2 panAxes = Vector2.ClampMagnitude(inputState.panAxes, 1f);
        float panSpeed = EvaluatePanSpeed(currentZoomNormalized);
        Quaternion yawRotation = Quaternion.Euler(0f, currentYaw, 0f);
        Vector3 flatForward = Vector3.ProjectOnPlane(yawRotation * Vector3.forward, Vector3.up).normalized;
        Vector3 flatRight = Vector3.ProjectOnPlane(yawRotation * Vector3.right, Vector3.up).normalized;

        Vector3 worldPan = (flatRight * panAxes.x + flatForward * panAxes.y) * panSpeed * deltaTime;

        if (inputState.panDragDelta.sqrMagnitude > 0.0001f)
        {
            float dragScale = Mathf.Max(0.001f, currentDistance) * dragPanDistanceFactor;
            worldPan += (-flatRight * inputState.panDragDelta.x - flatForward * inputState.panDragDelta.y) * dragScale;
        }

        return worldPan;
    }

    private Vector3 ResolveCombatCameraPosition(Transform player, Transform enemy, bool playerTurn)
    {
        Vector3 playerPosition = player != null ? player.position : Vector3.zero;
        Vector3 enemyPosition = enemy != null ? enemy.position : playerPosition + Vector3.forward;
        Vector3 toEnemy = Vector3.ProjectOnPlane(enemyPosition - playerPosition, Vector3.up);
        if (toEnemy.sqrMagnitude <= 0.0001f)
        {
            toEnemy = Vector3.ProjectOnPlane(player != null ? player.forward : Vector3.forward, Vector3.up);
        }

        if (toEnemy.sqrMagnitude <= 0.0001f)
        {
            toEnemy = Vector3.forward;
        }

        Vector3 forward = toEnemy.normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        float sideOffset = playerTurn ? combatPlayerTurnSideOffset : combatEnemyTurnSideOffset;
        Vector3 basePosition = playerPosition
            + Vector3.up * combatShoulderHeight
            - forward * combatDistance
            + right * sideOffset;

        Quaternion baseRotation = Quaternion.LookRotation((ResolveCombatLookTarget(enemy) - basePosition).normalized, Vector3.up);
        return basePosition + ResolveCombatBreathOffset(baseRotation);
    }

    private Vector3 ResolveCombatLookTarget(Transform enemy)
    {
        return (enemy != null ? enemy.position : Vector3.zero) + Vector3.up * combatLookAtYOffset;
    }

    private Vector3 ResolveCombatBreathOffset(Quaternion baseRotation)
    {
        float phase = Time.unscaledTime * Mathf.Max(0.01f, combatBreathFrequency) * Mathf.PI * 2f;
        Vector3 localOffset = new Vector3(
            Mathf.Sin(phase * 0.5f) * combatBreathHorizontalAmplitude,
            Mathf.Sin(phase) * combatBreathVerticalAmplitude,
            Mathf.Cos(phase * 0.75f) * combatBreathDepthAmplitude);
        return baseRotation * localOffset;
    }

    private void ApplyDirectCameraPose(Vector3 worldPosition, Quaternion worldRotation)
    {
        if (cameraAnchor != null)
        {
            cameraAnchor.position = worldPosition;
        }

        Vector3 euler = worldRotation.eulerAngles;
        float yaw = euler.y;
        float pitch = NormalizePitchAngle(euler.x);

        currentAnchorPosition = worldPosition;
        desiredYaw = yaw;
        currentYaw = yaw;
        manualPitchOffset = 0f;
        currentPitch = pitch;
        currentDistance = 0f;

        if (yawPivot != null)
        {
            yawPivot.localPosition = Vector3.zero;
            yawPivot.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }

        if (pitchPivot != null)
        {
            pitchPivot.localPosition = Vector3.zero;
            pitchPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        if (mainCam != null)
        {
            Transform camTransform = mainCam.transform;
            camTransform.localPosition = Vector3.zero;
            camTransform.localRotation = Quaternion.identity;
        }
    }

    private void UpdateRig(Vector3 focusPoint, Transform ignoredTarget, float deltaTime)
    {
        float desiredDistance = EvaluateDistance(currentZoomNormalized);
        float desiredPivotHeight = EvaluatePivotHeight(currentZoomNormalized);
        Vector3 desiredAnchorPosition = focusPoint + Vector3.up * desiredPivotHeight;

        Quaternion rigRotation = Quaternion.Euler(0f, currentYaw, 0f) * Quaternion.Euler(currentPitch, 0f, 0f);
        // Obstructions are handled visually by default; the legacy solver is kept only as an opt-in.
        bool useLegacyCollisionSolver = !triggerDrivenZoomTestMode && allowLegacyObstacleRepositioning;
        CrpgCameraCollision.SolveResult solve = !useLegacyCollisionSolver
            ? new CrpgCameraCollision.SolveResult
            {
                anchorPosition = desiredAnchorPosition,
                allowedDistance = desiredDistance,
                obstructed = false
            }
            : cameraCollision.Solve(desiredAnchorPosition, rigRotation, desiredDistance, ignoredTarget);

        float anchorT = anchorSharpness <= 0f ? 1f : 1f - Mathf.Exp(-anchorSharpness * deltaTime);
        currentAnchorPosition = Vector3.Lerp(currentAnchorPosition, solve.anchorPosition, anchorT);

        float distanceSharpness = solve.obstructed || solve.allowedDistance < currentDistance
            ? obstructionSharpness
            : releaseSharpness;
        float distanceT = distanceSharpness <= 0f ? 1f : 1f - Mathf.Exp(-distanceSharpness * deltaTime);
        currentDistance = Mathf.Lerp(currentDistance, solve.allowedDistance, distanceT);

        if (cameraAnchor != null)
        {
            cameraAnchor.position = currentAnchorPosition;
        }

        if (yawPivot != null)
        {
            yawPivot.localPosition = Vector3.zero;
            yawPivot.localRotation = Quaternion.Euler(0f, currentYaw, 0f);
        }

        if (pitchPivot != null)
        {
            pitchPivot.localPosition = Vector3.zero;
            pitchPivot.localRotation = Quaternion.Euler(currentPitch, 0f, 0f);
        }

        if (mainCam != null)
        {
            Transform camTransform = mainCam.transform;
            camTransform.localPosition = new Vector3(0f, 0f, -currentDistance);
            camTransform.localRotation = Quaternion.identity;
        }
    }

    private void InitializeRuntimeState(Vector3 focusPoint)
    {
        if (runtimeInitialized)
        {
            return;
        }

        desiredZoomNormalized = Mathf.Clamp01(zoomNormalized);
        currentZoomNormalized = desiredZoomNormalized;
        desiredYaw = yawPivot != null ? yawPivot.localEulerAngles.y : 0f;
        currentYaw = desiredYaw;
        manualPitchOffset = 0f;
        currentPitch = Mathf.Clamp(EvaluateProfilePitch(currentZoomNormalized), minPitch, maxPitch);
        currentDistance = EvaluateDistance(currentZoomNormalized);
        currentAnchorPosition = focusPoint + Vector3.up * EvaluatePivotHeight(currentZoomNormalized);
        cameraFocus.SnapTo(focusPoint);

        if (cameraAnchor != null)
        {
            cameraAnchor.position = currentAnchorPosition;
        }

        runtimeInitialized = true;
    }

    private void ClearFixedCameraRuntime()
    {
        fixedCameraSource = null;
        fixedCameraPoint = null;
        fixedCameraTarget = null;
        fixedCameraLookAtOffset = Vector3.zero;
        fixedCameraPriority = int.MinValue;
        fixedCameraTransitionSharpness = 8f;
        fixedCameraPoseInitialized = false;
    }

    private Transform ResolveGameplayTarget()
    {
        if (SquadManager.Instance != null && SquadManager.Instance.currentCharacter != null)
        {
            return SquadManager.Instance.currentCharacter.transform;
        }

        return LocalPlayerContext.LocalCharacterRoot;
    }

    public bool TryGetGameplayTarget(out Transform gameplayTarget)
    {
        gameplayTarget = ResolveGameplayTarget();
        return gameplayTarget != null;
    }

    private Vector3 ResolveFocusPoint(Transform logicalTarget, bool usingOverride)
    {
        if (logicalTarget == null)
        {
            if (runtimeInitialized)
            {
                return cameraFocus.CurrentFocusPoint;
            }

            return transform.position;
        }

        Vector3 offset = usingOverride && !useTargetOffsetForOverride ? overrideTargetOffset : targetOffset;
        return logicalTarget.position + offset;
    }

    private float EvaluateDistance(float normalizedZoom)
    {
        return Mathf.Lerp(minDistance, maxDistance, normalizedZoom);
    }

    private float EvaluatePivotHeight(float normalizedZoom)
    {
        return Mathf.Lerp(minPivotHeight, maxPivotHeight, normalizedZoom);
    }

    private float EvaluateProfilePitch(float normalizedZoom)
    {
        return Mathf.Lerp(zoomedInPitch, zoomedOutPitch, normalizedZoom);
    }

    private float EvaluatePanSpeed(float normalizedZoom)
    {
        return Mathf.Lerp(minPanSpeed, maxPanSpeed, normalizedZoom);
    }

    private bool TryResolveRigReferences()
    {
        if (cameraAnchor == null)
        {
            cameraAnchor = FindChildRecursive(transform, "CameraAnchor");
        }

        if (yawPivot == null)
        {
            yawPivot = FindChildRecursive(transform, "YawPivot");
        }

        if (pitchPivot == null)
        {
            pitchPivot = FindChildRecursive(transform, "PitchPivot");
        }

        if (mainCam == null)
        {
            mainCam = GetComponentInChildren<Camera>(true);
        }

        return mainCam != null && cameraAnchor != null && yawPivot != null && pitchPivot != null;
    }

    private void OnValidate()
    {
        ValidateFields();
    }

    private void ValidateFields()
    {
        if (launchLocalScale == Vector3.zero)
        {
            launchLocalScale = Vector3.one;
        }

        zoomNormalized = Mathf.Clamp01(zoomNormalized);
        zoomInSpeed = Mathf.Max(0f, zoomInSpeed);
        zoomOutSpeed = Mathf.Max(0f, zoomOutSpeed);
        zoomSharpness = Mathf.Max(0f, zoomSharpness);
        minDistance = Mathf.Max(0.1f, minDistance);
        maxDistance = Mathf.Max(minDistance, maxDistance);
        minPivotHeight = Mathf.Max(0f, minPivotHeight);
        maxPivotHeight = Mathf.Max(minPivotHeight, maxPivotHeight);
        minPanSpeed = Mathf.Max(0f, minPanSpeed);
        maxPanSpeed = Mathf.Max(minPanSpeed, maxPanSpeed);
        rotationSharpness = Mathf.Max(0f, rotationSharpness);
        minPitch = Mathf.Clamp(minPitch, 0f, 89f);
        maxPitch = Mathf.Clamp(maxPitch, minPitch, 89f);
        zoomedInPitch = Mathf.Clamp(zoomedInPitch, minPitch, maxPitch);
        zoomedOutPitch = Mathf.Clamp(zoomedOutPitch, minPitch, maxPitch);
        pitchOffsetMin = Mathf.Min(pitchOffsetMin, pitchOffsetMax);
        dragPanDistanceFactor = Mathf.Max(0f, dragPanDistanceFactor);
        anchorSharpness = Mathf.Max(0f, anchorSharpness);
        obstructionSharpness = Mathf.Max(0f, obstructionSharpness);
        releaseSharpness = Mathf.Max(0f, releaseSharpness);
        combatDistance = Mathf.Max(0.1f, combatDistance);
        combatCameraSharpness = Mathf.Max(0f, combatCameraSharpness);
        combatBreathVerticalAmplitude = Mathf.Max(0f, combatBreathVerticalAmplitude);
        combatBreathHorizontalAmplitude = Mathf.Max(0f, combatBreathHorizontalAmplitude);
        combatBreathDepthAmplitude = Mathf.Max(0f, combatBreathDepthAmplitude);
        combatBreathFrequency = Mathf.Max(0.01f, combatBreathFrequency);

        cameraInput?.Validate();
        cameraFocus?.Validate();
        cameraCollision?.Validate();
        runSpeedEffect?.Validate();
        fallSpeedEffect?.Validate();
    }

    private void ApplyLaunchTransformReset()
    {
        if (!Application.isPlaying || !resetTransformOnPlay || launchTransformResetApplied)
        {
            return;
        }

        transform.localPosition = launchLocalPosition;
        transform.localRotation = Quaternion.Euler(launchLocalEulerAngles);
        transform.localScale = launchLocalScale == Vector3.zero ? Vector3.one : launchLocalScale;
        launchTransformResetApplied = true;
    }

    private static float NormalizePitchAngle(float angle)
    {
        angle %= 360f;
        if (angle > 180f)
        {
            angle -= 360f;
        }

        return angle;
    }

    private static Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == childName)
            {
                return child;
            }

            Transform nested = FindChildRecursive(child, childName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }
}
