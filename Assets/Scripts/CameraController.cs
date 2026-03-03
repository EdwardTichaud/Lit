using UnityEngine;
using UnityEngine.InputSystem;

[ExecuteAlways]
// Camera de suivi orbitale (zoom, collisions, visibility).
public class CameraController : MonoBehaviour
{
    [Header("MainCamera")]
    [Tooltip("Camera principale (fallback sur Camera.main).")]
    public Camera mainCam;
    [Tooltip("Cible actuelle de la camera.")]
    public Transform mainCamCurrentTarget;
    [Tooltip("Cible temporaire a suivre (placement, cutscene, etc.).")]
    public Transform followOverrideTarget;
    [Tooltip("Offset initial par rapport a la cible.")]
    public Vector3 mainCamOffset;
    [Tooltip("Offset applique au pivot de la cible.")]
    public Vector3 targetOffset = new Vector3(0f, 1.5f, 0f);
    [Tooltip("Offset applique quand un override est actif.")]
    public Vector3 overrideTargetOffset = Vector3.zero;
    [Tooltip("Utilise le targetOffset meme en override.")]
    public bool useTargetOffsetForOverride = false;
    [Tooltip("Vitesse de lerp de position.")]
    public float positionLerpSpeed = 5f;
    [Tooltip("Vitesse de lerp de rotation.")]
    public float rotationLerpSpeed = 8f;
    [Tooltip("Vitesse de lerp lors d'obstruction.")]
    public float obstructionLerpSpeed = 14f;

    [Header("Orbit")]
    [Tooltip("Autorise l'orbite autour de la cible.")]
    public bool allowOrbit = true;
    [Tooltip("Vitesse d'orbite.")]
    public float orbitSpeed = 90f;
    [Tooltip("Deadzone pour l'orbite.")]
    public float orbitDeadzone = 0.1f;
    [Tooltip("Autorise l'orbite a la souris.")]
    public bool allowMouseOrbit = true;
    [Tooltip("Sensibilite d'orbite souris.")]
    public float mouseOrbitSensitivity = 0.15f;

    [Header("Placement Override")]
    [Tooltip("Duree du lerp du regard avant de suivre la cible.")]
    public float placementLookLerpDuration = 0.5f;
    [Tooltip("Rayon d'orbite autour de la cible (0 = conserver la distance initiale).")]
    public float placementOrbitRadius = 5f;
    [Tooltip("Autorise l'orbite pendant l'override.")]
    public bool placementAllowOrbit = true;
    [Tooltip("Autorise l'orbite a la souris pendant l'override.")]
    public bool placementAllowMouseOrbit = true;
    [Tooltip("Vitesse d'orbite pendant l'override (0 = utiliser orbitSpeed).")]
    public float placementOrbitSpeed = 0f;

    [Header("Zoom")]
    [Tooltip("Autorise le zoom.")]
    public bool allowZoom = true;
    [Tooltip("Distance min au pivot.")]
    public float minDistance = 2f;
    [Tooltip("Distance max au pivot.")]
    public float maxDistance = 10f;
    [Tooltip("Vitesse de zoom avant.")]
    public float zoomInSpeed = 6f;
    [Tooltip("Vitesse de zoom arriere.")]
    public float zoomOutSpeed = 6f;
    [Tooltip("Vitesse de lerp du zoom.")]
    public float zoomLerpSpeed = 12f;
    [Tooltip("Deadzone du zoom.")]
    public float zoomDeadzone = 0.1f;
    [Tooltip("Sensibilite zoom souris.")]
    public float mouseZoomSensitivity = 0.02f;
    [Tooltip("Sensibilite zoom gamepad.")]
    public float gamepadZoomSensitivity = 3f;
    [Tooltip("Autorise zoom via stick droit.")]
    public bool allowRightStickZoom = true;
    [Tooltip("Sensibilite zoom stick droit.")]
    public float rightStickZoomSensitivity = 4f;
    [Tooltip("Favorise l'axe horizontal pour l'orbite.")]
    public float rightStickHorizontalDominance = 1.2f;

    [Header("Collision")]
    [Tooltip("Masque des obstacles pour la camera.")]
    public LayerMask collisionMask = ~0;
    [Tooltip("Rayon du spherecast de collision.")]
    public float collisionRadius = 0.25f;
    [Tooltip("Distance de marge avant l'obstacle.")]
    public float collisionBuffer = 0.1f;
    [Tooltip("Distance minimale au pivot.")]
    public float minCollisionDistance = 0.3f;
    [Tooltip("Ignore les colliders de la cible.")]
    public bool ignoreTargetColliders = true;

    [Header("Character Visibility")]
    [Tooltip("Masque le personnage quand la camera est trop proche.")]
    public bool hideCharacterWhenClose = true;
    [Tooltip("Distance a partir de laquelle on masque.")]
    public float hideCharacterDistance = 1.5f;
    [Tooltip("Distance a partir de laquelle on re-affiche.")]
    public float showCharacterDistance = 2f;

    [Header("Focus")]
    [Tooltip("Calcule un look-at dynamique selon la distance.")]
    public bool useDynamicLookAt = true;
    [Tooltip("Utilise le forward du personnage pour le focus.")]
    public bool focusAlongCharacterForward = false;
    [Tooltip("Distance de focus proche.")]
    public float closeFocusDistance = 1.5f;
    [Tooltip("Distance ou le blend commence.")]
    public float focusBlendStartDistance = 6f;
    [Tooltip("Distance ou le blend se termine.")]
    public float focusBlendEndDistance = 2.5f;

    private float orbitYaw;
    private float desiredDistance;
    private float currentDistance;
    private bool distanceInitialized;
    private Vector3 currentOrbitDirection;
    private readonly RaycastHit[] collisionHits = new RaycastHit[8];
    private Transform cachedTarget;
    private Renderer[] cachedTargetRenderers;
    private bool[] cachedTargetRendererStates;
    private bool targetHidden;
    private bool lastUsingOverride;
    private bool overrideInitialized;
    private float overrideStartTime;
    private Vector3 overrideStartCamPosition;
    private Quaternion overrideStartCamRotation;
    private float overrideHeightOffset;
    private float overrideBaseRadius;
    private float overrideOrbitYaw;

    private void Start()
    {
        orbitYaw = GetInitialYaw();
    }

    private void LateUpdate()
    {
        if (mainCam == null)
        {
            mainCam = Camera.main;
        }

        if (mainCam == null)
        {
            return;
        }

        Transform desiredTarget = followOverrideTarget;
        bool usingOverride = desiredTarget != null;
        if (usingOverride)
        {
            mainCamCurrentTarget = desiredTarget;
            UpdatePlacementOverride(desiredTarget);
            if (!lastUsingOverride)
            {
                ClearTargetVisibility();
            }
            lastUsingOverride = true;
            return;
        }

        Transform localTarget = LocalPlayerContext.LocalCharacterRoot;
        if (localTarget != null)
        {
            desiredTarget = localTarget;
        }
        else if (SquadManager.Instance != null && SquadManager.Instance.currentCharacter != null)
        {
            desiredTarget = SquadManager.Instance.currentCharacter.transform;
        }

        if (desiredTarget != null)
        {
            // Suivi de la cible courante.
            mainCamCurrentTarget = desiredTarget;
            bool inputLocked = InputFocusStack.HasAnyFocusBlockingCamera();
            if (!inputLocked)
            {
                UpdateOrbitYaw();
                UpdateZoom(Time.deltaTime);
            }

            Vector3 offset = mainCamOffset.sqrMagnitude > 0.0001f
                ? mainCamOffset
                : new Vector3(0f, 3f, -6f);

            float baseDistance = offset.magnitude;
            InitializeDistance(baseDistance);

            Vector3 desiredOrbitDirection = Quaternion.Euler(0f, orbitYaw, 0f) * offset.normalized;
            if (currentOrbitDirection.sqrMagnitude < 0.0001f)
            {
                currentOrbitDirection = desiredOrbitDirection;
            }

            float orbitT = 1f - Mathf.Exp(-positionLerpSpeed * Time.deltaTime);
            currentOrbitDirection = Vector3.Slerp(currentOrbitDirection, desiredOrbitDirection, orbitT);

            Vector3 pivot = mainCamCurrentTarget.position + targetOffset;
            float collisionDistance = ResolveCollisionDistance(pivot, currentOrbitDirection, desiredDistance);
            float targetDistance = Mathf.Min(desiredDistance, collisionDistance);
            float lerpSpeed = collisionDistance < desiredDistance ? obstructionLerpSpeed : zoomLerpSpeed;

            float t = 1f - Mathf.Exp(-lerpSpeed * Time.deltaTime);
            currentDistance = Mathf.Lerp(currentDistance, targetDistance, t);
            currentDistance = Mathf.Min(currentDistance, collisionDistance);

            Vector3 desiredPosition = pivot + currentOrbitDirection * currentDistance;
            mainCam.transform.position = desiredPosition;

            Vector3 lookTarget = GetLookTarget(pivot);
            Vector3 lookDirection = lookTarget - mainCam.transform.position;
            if (lookDirection.sqrMagnitude > 0.0001f)
            {
                Quaternion desiredRotation = Quaternion.LookRotation(lookDirection);
                mainCam.transform.rotation = Quaternion.Slerp(
                    mainCam.transform.rotation,
                    desiredRotation,
                    rotationLerpSpeed * Time.deltaTime);
            }

            UpdateTargetVisibility(pivot);
            lastUsingOverride = false;
        }
        else
        {
            mainCamCurrentTarget = null;
            ClearTargetVisibility();
            lastUsingOverride = false;
        }
    }

    public void SetFollowOverride(Transform target)
    {
        if (followOverrideTarget == target)
        {
            return;
        }

        followOverrideTarget = target;
        overrideInitialized = false;
    }

    public void ClearFollowOverride(Transform target)
    {
        if (followOverrideTarget == target)
        {
            followOverrideTarget = null;
            overrideInitialized = false;
        }
    }

    private void UpdateOrbitYaw()
    {
        if (!allowOrbit)
        {
            return;
        }

        float orbitDelta = 0f;

        if (Gamepad.current != null)
        {
            float stickX = Gamepad.current.rightStick.ReadValue().x;
            if (Mathf.Abs(stickX) > orbitDeadzone)
            {
                orbitDelta += stickX * orbitSpeed * Time.deltaTime;
            }
        }

        if (Keyboard.current != null)
        {
            if (Keyboard.current.qKey.isPressed)
            {
                orbitDelta -= orbitSpeed * Time.deltaTime;
            }
            if (Keyboard.current.eKey.isPressed)
            {
                orbitDelta += orbitSpeed * Time.deltaTime;
            }
        }

        if (allowMouseOrbit && Mouse.current != null)
        {
            orbitDelta += Mouse.current.delta.ReadValue().x * mouseOrbitSensitivity;
        }

        if (Mathf.Abs(orbitDelta) > 0.0001f)
        {
            orbitYaw += orbitDelta;
        }
    }

    private void UpdatePlacementOverride(Transform target)
    {
        if (mainCam == null || target == null)
        {
            return;
        }

        if (!overrideInitialized)
        {
            InitializePlacementOverrideState(target);
        }

        float deltaTime = Time.unscaledDeltaTime;
        float elapsed = Time.unscaledTime - overrideStartTime;
        Vector3 pivotOffset = useTargetOffsetForOverride ? targetOffset : overrideTargetOffset;
        Vector3 pivot = target.position + pivotOffset;

        UpdatePlacementOrbitYaw(deltaTime);

        float lookDuration = Mathf.Max(0f, placementLookLerpDuration);
        if (elapsed < lookDuration)
        {
            mainCam.transform.position = overrideStartCamPosition;
            Vector3 lookDirection = pivot - overrideStartCamPosition;
            if (lookDirection.sqrMagnitude > 0.0001f)
            {
                Quaternion desiredRotation = Quaternion.LookRotation(lookDirection);
                float t = lookDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / lookDuration);
                mainCam.transform.rotation = Quaternion.Slerp(overrideStartCamRotation, desiredRotation, t);
            }
            return;
        }

        float radius = placementOrbitRadius > 0f ? placementOrbitRadius : overrideBaseRadius;
        Vector3 flatDir = Quaternion.Euler(0f, overrideOrbitYaw, 0f) * Vector3.forward;
        Vector3 desiredOffset = new Vector3(flatDir.x * radius, overrideHeightOffset, flatDir.z * radius);
        Vector3 desiredPosition = pivot + desiredOffset;
        mainCam.transform.position = desiredPosition;

        Vector3 lookDir = pivot - desiredPosition;
        if (lookDir.sqrMagnitude > 0.0001f)
        {
            Quaternion desiredRotation = Quaternion.LookRotation(lookDir);
            mainCam.transform.rotation = Quaternion.Slerp(
                mainCam.transform.rotation,
                desiredRotation,
                rotationLerpSpeed * deltaTime);
        }
    }

    private void InitializePlacementOverrideState(Transform target)
    {
        if (mainCam == null || target == null)
        {
            overrideInitialized = false;
            return;
        }

        overrideInitialized = true;
        overrideStartTime = Time.unscaledTime;
        overrideStartCamPosition = mainCam.transform.position;
        overrideStartCamRotation = mainCam.transform.rotation;
        Vector3 offset = overrideStartCamPosition - target.position;
        overrideHeightOffset = offset.y;
        Vector3 flatOffset = new Vector3(offset.x, 0f, offset.z);
        overrideBaseRadius = flatOffset.magnitude;
        if (overrideBaseRadius < 0.01f)
        {
            overrideBaseRadius = placementOrbitRadius > 0f ? placementOrbitRadius : 5f;
        }

        overrideOrbitYaw = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
    }

    private void UpdatePlacementOrbitYaw(float deltaTime)
    {
        if (!placementAllowOrbit)
        {
            return;
        }

        float orbitDelta = 0f;
        float speed = placementOrbitSpeed > 0f ? placementOrbitSpeed : orbitSpeed;

        if (Gamepad.current != null)
        {
            float stickX = Gamepad.current.rightStick.ReadValue().x;
            if (Mathf.Abs(stickX) > orbitDeadzone)
            {
                orbitDelta += stickX * speed * deltaTime;
            }
        }

        if (Keyboard.current != null)
        {
            if (Keyboard.current.qKey.isPressed)
            {
                orbitDelta -= speed * deltaTime;
            }
            if (Keyboard.current.eKey.isPressed)
            {
                orbitDelta += speed * deltaTime;
            }
        }

        if (placementAllowMouseOrbit && Mouse.current != null)
        {
            orbitDelta += Mouse.current.delta.ReadValue().x * mouseOrbitSensitivity;
        }

        if (Mathf.Abs(orbitDelta) > 0.0001f)
        {
            overrideOrbitYaw += orbitDelta;
        }
    }

    private void UpdateZoom(float deltaTime)
    {
        if (!allowZoom)
        {
            return;
        }

        float zoomDelta = 0f;

        if (Mouse.current != null)
        {
            float scroll = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                zoomDelta -= scroll * mouseZoomSensitivity;
            }
        }

        if (Gamepad.current != null)
        {
            float triggerDelta = Gamepad.current.rightTrigger.ReadValue() - Gamepad.current.leftTrigger.ReadValue();
            if (Mathf.Abs(triggerDelta) > zoomDeadzone)
            {
                zoomDelta -= triggerDelta * gamepadZoomSensitivity * deltaTime;
            }

            if (allowRightStickZoom)
            {
                Vector2 stick = Gamepad.current.rightStick.ReadValue();
                float absX = Mathf.Abs(stick.x);
                float absY = Mathf.Abs(stick.y);
                bool isMostlyHorizontal = absX >= absY * rightStickHorizontalDominance;

                if (!isMostlyHorizontal && absY > zoomDeadzone)
                {
                    zoomDelta -= stick.y * rightStickZoomSensitivity * deltaTime;
                }
            }
        }

        if (Mathf.Abs(zoomDelta) > 0.0001f)
        {
            float speed = zoomDelta < 0f ? zoomInSpeed : zoomOutSpeed;
            desiredDistance = Mathf.Clamp(desiredDistance + zoomDelta * speed, minDistance, maxDistance);
        }
    }

    private void InitializeDistance(float baseDistance)
    {
        if (distanceInitialized)
        {
            return;
        }

        minDistance = Mathf.Max(0.1f, minDistance);
        maxDistance = Mathf.Max(minDistance, maxDistance);

        float startDistance = Mathf.Clamp(baseDistance, minDistance, maxDistance);
        desiredDistance = startDistance;
        currentDistance = startDistance;
        currentOrbitDirection = mainCamOffset.sqrMagnitude > 0.0001f
            ? mainCamOffset.normalized
            : new Vector3(0f, 0.5f, -1f).normalized;
        distanceInitialized = true;
    }

    private float ResolveCollisionDistance(Vector3 pivot, Vector3 direction, float desired)
    {
        if (desired <= 0.01f)
        {
            return desired;
        }

        int hitCount = Physics.SphereCastNonAlloc(
            pivot,
            collisionRadius,
            direction,
            collisionHits,
            desired,
            collisionMask,
            QueryTriggerInteraction.Ignore);

        if (hitCount <= 0)
        {
            return desired;
        }

        float closest = desired;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = collisionHits[i];
            if (hit.collider == null)
            {
                continue;
            }

            if (ignoreTargetColliders && mainCamCurrentTarget != null &&
                hit.collider.transform.IsChildOf(mainCamCurrentTarget))
            {
                continue;
            }

            if (hit.distance < closest)
            {
                closest = hit.distance;
            }
        }

        if (closest < desired)
        {
            return Mathf.Max(minCollisionDistance, closest - collisionBuffer);
        }

        return desired;
    }

    private float GetInitialYaw()
    {
        Vector3 flatOffset = new Vector3(mainCamOffset.x, 0f, mainCamOffset.z);
        if (flatOffset.sqrMagnitude > 0.0001f)
        {
            return Mathf.Atan2(flatOffset.x, flatOffset.z) * Mathf.Rad2Deg;
        }

        return 0f;
    }

    private Vector3 GetLookTarget(Vector3 pivot)
    {
        if (!useDynamicLookAt)
        {
            return pivot;
        }

        float start = Mathf.Max(0.01f, focusBlendStartDistance);
        float end = Mathf.Max(0.01f, focusBlendEndDistance);
        float t;
        if (start > end)
        {
            t = Mathf.InverseLerp(start, end, currentDistance);
        }
        else
        {
            t = Mathf.InverseLerp(end, start, currentDistance);
        }

        if (t <= 0f || closeFocusDistance <= 0f)
        {
            return pivot;
        }

        Vector3 direction = Vector3.zero;
        if (focusAlongCharacterForward && mainCamCurrentTarget != null)
        {
            direction = Vector3.ProjectOnPlane(mainCamCurrentTarget.forward, Vector3.up);
        }
        else
        {
            direction = Vector3.ProjectOnPlane(-currentOrbitDirection, Vector3.up);
        }

        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = Vector3.ProjectOnPlane(mainCam.transform.forward, Vector3.up);
        }

        if (direction.sqrMagnitude > 0.0001f)
        {
            direction = direction.normalized;
        }

        return pivot + direction * (closeFocusDistance * t);
    }

    private void OnValidate()
    {
        minDistance = Mathf.Max(0.1f, minDistance);
        maxDistance = Mathf.Max(minDistance, maxDistance);
        zoomInSpeed = Mathf.Max(0f, zoomInSpeed);
        zoomOutSpeed = Mathf.Max(0f, zoomOutSpeed);
        zoomLerpSpeed = Mathf.Max(0f, zoomLerpSpeed);
        obstructionLerpSpeed = Mathf.Max(0f, obstructionLerpSpeed);
        collisionRadius = Mathf.Max(0.01f, collisionRadius);
        collisionBuffer = Mathf.Max(0f, collisionBuffer);
        minCollisionDistance = Mathf.Max(0.05f, minCollisionDistance);
        closeFocusDistance = Mathf.Max(0f, closeFocusDistance);
        focusBlendStartDistance = Mathf.Max(0.01f, focusBlendStartDistance);
        focusBlendEndDistance = Mathf.Max(0.01f, focusBlendEndDistance);
        rightStickHorizontalDominance = Mathf.Max(0.01f, rightStickHorizontalDominance);
        hideCharacterDistance = Mathf.Max(0.05f, hideCharacterDistance);
        showCharacterDistance = Mathf.Max(hideCharacterDistance, showCharacterDistance);
        placementLookLerpDuration = Mathf.Max(0f, placementLookLerpDuration);
        placementOrbitRadius = Mathf.Max(0f, placementOrbitRadius);
        placementOrbitSpeed = Mathf.Max(0f, placementOrbitSpeed);
    }

    private void UpdateTargetVisibility(Vector3 pivot)
    {
        CacheTargetRenderers();

        if (!hideCharacterWhenClose || cachedTargetRenderers == null || cachedTargetRenderers.Length == 0)
        {
            if (targetHidden)
            {
                SetRenderersVisible(cachedTargetRenderers, cachedTargetRendererStates, true);
                targetHidden = false;
            }
            return;
        }

        float distance = Vector3.Distance(mainCam.transform.position, pivot);
        bool shouldHide = targetHidden
            ? distance < showCharacterDistance
            : distance < hideCharacterDistance;

        if (shouldHide != targetHidden)
        {
            SetRenderersVisible(cachedTargetRenderers, cachedTargetRendererStates, !shouldHide);
            targetHidden = shouldHide;
        }
    }

    private void CacheTargetRenderers()
    {
        if (mainCamCurrentTarget == cachedTarget)
        {
            return;
        }

        if (cachedTargetRenderers != null && cachedTargetRenderers.Length > 0)
        {
            SetRenderersVisible(cachedTargetRenderers, cachedTargetRendererStates, true);
        }

        cachedTarget = mainCamCurrentTarget;
        targetHidden = false;

        if (cachedTarget == null)
        {
            cachedTargetRenderers = null;
            cachedTargetRendererStates = null;
            return;
        }

        cachedTargetRenderers = cachedTarget.GetComponentsInChildren<Renderer>(true);
        if (cachedTargetRenderers == null || cachedTargetRenderers.Length == 0)
        {
            cachedTargetRendererStates = null;
            return;
        }

        cachedTargetRendererStates = new bool[cachedTargetRenderers.Length];
        for (int i = 0; i < cachedTargetRenderers.Length; i++)
        {
            Renderer renderer = cachedTargetRenderers[i];
            cachedTargetRendererStates[i] = renderer != null && renderer.enabled;
        }
    }

    private void ClearTargetVisibility()
    {
        if (cachedTargetRenderers != null && cachedTargetRenderers.Length > 0)
        {
            SetRenderersVisible(cachedTargetRenderers, cachedTargetRendererStates, true);
        }

        cachedTarget = null;
        cachedTargetRenderers = null;
        cachedTargetRendererStates = null;
        targetHidden = false;
    }

    private static void SetRenderersVisible(Renderer[] renderers, bool[] states, bool visible)
    {
        if (renderers == null)
        {
            return;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (visible)
            {
                bool state = states != null && i < states.Length ? states[i] : true;
                renderer.enabled = state;
            }
            else
            {
                renderer.enabled = false;
            }
        }
    }
}
