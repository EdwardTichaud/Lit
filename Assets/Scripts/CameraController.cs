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

    [Header("Compatibility")]
    [Tooltip("Cible logique courante resolue par la camera.")]
    public Transform mainCamCurrentTarget;
    [Tooltip("Sommet actuel de la pile d'override de focus.")]
    public Transform followOverrideTarget;
    [Tooltip("Offset applique a la cible courante.")]
    public Vector3 targetOffset = new Vector3(0f, 1.5f, 0f);
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

    [Header("Subsystems")]
    [SerializeField] private CrpgCameraInput cameraInput = new CrpgCameraInput();
    [SerializeField] private CrpgCameraFocus cameraFocus = new CrpgCameraFocus();
    [SerializeField] private CrpgCameraCollision cameraCollision = new CrpgCameraCollision();

    private bool runtimeInitialized;
    private float desiredYaw;
    private float currentYaw;
    private float manualPitchOffset;
    private float currentPitch;
    private float desiredZoomNormalized;
    private float currentZoomNormalized;
    private float currentDistance;
    private Vector3 currentAnchorPosition;

    private void Awake()
    {
        TryResolveRigReferences();
        ValidateFields();
    }

    private void OnEnable()
    {
        LocalInputRouter.EnsureInitialized();
        LocalInputRouter.SetCameraFreeModeActive(false, suppressImmediateCharacterMove: true);
        cameraInput.Bind();
        cameraFocus.Reset();
        mainCamCurrentTarget = null;
        followOverrideTarget = null;
        runtimeInitialized = false;
    }

    private void OnDisable()
    {
        cameraInput.Unbind();
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

        Transform gameplayTarget = ResolveGameplayTarget();
        followOverrideTarget = cameraFocus.GetTopOverrideTarget();
        Transform logicalTarget = followOverrideTarget != null ? followOverrideTarget : gameplayTarget;
        mainCamCurrentTarget = logicalTarget;

        Vector3 logicalFocusPoint = ResolveFocusPoint(logicalTarget, followOverrideTarget != null);
        InitializeRuntimeState(logicalFocusPoint);

        bool inputBlocked = InputFocusStack.HasAnyFocusBlockingCamera();
        CrpgCameraInput.FrameState inputState = cameraInput.Collect(inputBlocked, deltaTime);

        UpdateZoom(inputState.zoomDelta, deltaTime);
        UpdateRotation(inputState.orbitDelta, deltaTime);

        Vector3 panDelta = ResolveWorldPanDelta(inputState, deltaTime);
        Vector3 focusPoint = cameraFocus.Update(
            logicalFocusPoint,
            panDelta,
            inputState.recenterRequested,
            inputState.toggleFreeCameraRequested,
            deltaTime);
        UpdateRig(focusPoint, logicalTarget, deltaTime);

        zoomNormalized = currentZoomNormalized;
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

    private void UpdateRig(Vector3 focusPoint, Transform ignoredTarget, float deltaTime)
    {
        float desiredDistance = EvaluateDistance(currentZoomNormalized);
        float desiredPivotHeight = EvaluatePivotHeight(currentZoomNormalized);
        Vector3 desiredAnchorPosition = focusPoint + Vector3.up * desiredPivotHeight;

        Quaternion rigRotation = Quaternion.Euler(0f, currentYaw, 0f) * Quaternion.Euler(currentPitch, 0f, 0f);
        CrpgCameraCollision.SolveResult solve = cameraCollision.Solve(desiredAnchorPosition, rigRotation, desiredDistance, ignoredTarget);

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

    private Transform ResolveGameplayTarget()
    {
        if (SquadManager.Instance != null && SquadManager.Instance.currentCharacter != null)
        {
            return SquadManager.Instance.currentCharacter.transform;
        }

        return LocalPlayerContext.LocalCharacterRoot;
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

        cameraInput?.Validate();
        cameraFocus?.Validate();
        cameraCollision?.Validate();
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
