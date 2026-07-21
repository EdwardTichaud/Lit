using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

// Pointeur visible du MainMenu. La souris et la manette pilotent la meme position.
[DisallowMultipleComponent]
public class MainMenuPointerCursor : MonoBehaviour
{
    private enum PointerSource
    {
        Mouse,
        Gamepad
    }

    [Header("References")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private RectTransform cursorVisual;
    [SerializeField] private Camera decorCamera;
    [SerializeField] private Light flameLight;

    [Header("Gamepad Pointer")]
    [SerializeField] private float gamepadSpeed = 1150f;
    [SerializeField] private float gamepadDeadzone = 0.15f;
    [SerializeField] private bool warpHardwareMouseForGamepad = true;
    [SerializeField] private bool synthesizeGamepadUiEvents = true;

    [Header("Flame")]
    [SerializeField] private float flameCameraOffset = 0.2f;
    [SerializeField] private float worldRayDistance = 50f;
    [SerializeField] private LayerMask interactionLayers = ~0;

    [Header("Flame Collision")]
    [SerializeField, Tooltip("Garde la lumiere du curseur en dehors des colliders du decor.")]
    private bool keepFlameOutsideGeometry = true;
    [SerializeField] private LayerMask flameCollisionLayers = ~0;
    [SerializeField, Min(0f), Tooltip("Rayon physique autour de la lumiere.")]
    private float flameCollisionRadius = 0.12f;
    [SerializeField, Min(0f), Tooltip("Distance minimale gardee entre la lumiere et la surface touchee.")]
    private float flameSurfaceOffset = 0.035f;
    [SerializeField, Range(1, 6), Tooltip("Nombre de corrections si la lumiere commence dans un collider.")]
    private int flameCollisionIterations = 3;
    [SerializeField] private QueryTriggerInteraction flameCollisionTriggers = QueryTriggerInteraction.Ignore;

    [Header("Flame Projection Plane")]
    [SerializeField, Tooltip("Place la lumiere sur un plan parallele a la camera/UI borne par le decor 3D.")]
    private bool useFlameBoundsPlane = true;
    [SerializeField, Tooltip("Racine des objets 3D qui bornent la navigation de la lumiere. Vide = parent de la lumiere.")]
    private Transform flameBoundsRoot;
    [SerializeField, Tooltip("Inclut les etats de decor inactifs dans les bounds du plan pour eviter un changement de zone selon la sauvegarde.")]
    private bool includeInactiveFlameBounds = true;
    [SerializeField, Tooltip("Limite la projection de la lumiere a la zone ecran occupee par le decor.")]
    private bool clampFlameToDecorViewportBounds = true;
    [SerializeField, Range(0f, 0.25f), Tooltip("Padding en coordonnees viewport autour des bounds projetes du decor.")]
    private float flameViewportBoundsPadding = 0.015f;
    [SerializeField, Min(0f), Tooltip("Padding monde ajoute aux bounds du decor pour les corrections de placement.")]
    private float flameBoundsPadding = 0.08f;
    [SerializeField, Min(0.05f), Tooltip("Intervalle de recalcul des bounds du decor.")]
    private float flameBoundsRefreshInterval = 0.25f;
    [SerializeField, Tooltip("Utilise les bounds des renderers actifs pour garder la lumiere hors des meshes sans collider.")]
    private bool keepFlameOutsideRendererBounds = true;

    [Header("Flame Projection Rail Fallback")]
    [SerializeField, Tooltip("Construit au lancement une grille de profondeurs devant la camera pour guider la lumiere.")]
    private bool useFlameProjectionRail = true;
    [SerializeField, Range(4, 96)] private int flameRailColumns = 32;
    [SerializeField, Range(4, 96)] private int flameRailRows = 18;
    [SerializeField, Min(0.1f), Tooltip("Distance maximale des raycasts du rail depuis le plan de projection.")]
    private float flameRailRayDistance = 80f;
    [SerializeField, Min(0f), Tooltip("Distance gardee entre la lumiere et le relief du rail.")]
    private float flameRailSurfaceStandOff = 0.32f;
    [SerializeField, Tooltip("Reconstruit le rail si la taille d'ecran change.")]
    private bool rebuildFlameRailOnScreenChange = true;
    [SerializeField, Min(0f), Tooltip("Lissage de la position de la lumiere. 0 = instantane.")]
    private float flamePositionSharpness = 18f;
    [SerializeField, Min(0f), Tooltip("Lissage de l'orientation de la lumiere. 0 = instantane.")]
    private float flameRotationSharpness = 22f;

    [Header("System Cursor")]
    [SerializeField] private bool hideSystemCursor = true;

    private readonly Collider[] flameOverlapBuffer = new Collider[16];
    private readonly List<Bounds> flameActiveRendererBounds = new List<Bounds>();
    private readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();
    private PointerEventData pointerEventData;
    private EventSystem pointerEventSystem;
    private Bounds flameDecorCameraBounds;
    private Rect flameDecorViewportBounds;
    private bool hasFlameDecorBounds;
    private Transform cachedFlameBoundsRoot;
    private Camera cachedFlameBoundsCamera;
    private float nextFlameBoundsRefreshTime;
    private float[] flameRailDepths;
    private bool[] flameRailSampleValid;
    private int builtFlameRailColumns;
    private int builtFlameRailRows;
    private int builtFlameRailScreenWidth;
    private int builtFlameRailScreenHeight;
    private Camera builtFlameRailCamera;
    private bool flameRailReady;
    private Vector2 screenPosition;
    private bool hasScreenPosition;
    private PointerSource activeSource = PointerSource.Mouse;
    private bool cachedCursorVisible;
    private CursorLockMode cachedCursorLockMode;
    private GameObject syntheticUiHover;
    private CursorIntercation worldHover;
    private Vector3 currentFlamePosition;
    private Quaternion currentFlameRotation = Quaternion.identity;
    private bool hasFlamePose;
    private bool inputLocked;
    private bool cursorVisible = true;
    private Graphic cursorGraphic;

    public bool InputLocked => inputLocked;

    public void SetCursorVisible(bool visible)
    {
        if (cursorVisible == visible)
        {
            return;
        }

        cursorVisible = visible;
        ResolveReferences();
        if (cursorGraphic != null)
        {
            cursorGraphic.enabled = cursorVisible;
        }

        if (flameLight != null)
        {
            flameLight.enabled = cursorVisible;
        }

        if (!cursorVisible)
        {
            ClearSyntheticUiHover();
            SetWorldHover(null);
        }
    }

    public void SetInputLocked(bool locked)
    {
        if (inputLocked == locked)
        {
            return;
        }

        inputLocked = locked;
        if (inputLocked)
        {
            InitializeScreenPositionFromCursorTransform();
            ClearSyntheticUiHover();
            SetWorldHover(null);
            return;
        }

        SyncHardwareMouseToScreenPosition();
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        CacheAndApplySystemCursor();
        if (inputLocked)
        {
            InitializeScreenPositionFromCursorTransform();
        }
        else
        {
            InitializeScreenPosition();
        }

        RebuildFlameProjectionRail();
        if (flameLight != null)
        {
            flameLight.enabled = cursorVisible;
        }
    }

    private void OnDisable()
    {
        ClearSyntheticUiHover();
        SetWorldHover(null);
        RestoreSystemCursor();
        hasFlamePose = false;
        flameRailReady = false;
        hasFlameDecorBounds = false;
        flameActiveRendererBounds.Clear();
        if (flameLight != null)
        {
            flameLight.enabled = false;
        }
    }

    private void Update()
    {
        ResolveReferences();
        if (!cursorVisible)
        {
            ClearSyntheticUiHover();
            SetWorldHover(null);
            return;
        }

        if (inputLocked)
        {
            if (!hasScreenPosition)
            {
                InitializeScreenPositionFromCursorTransform();
            }

            SyncHardwareMouseToScreenPosition();
        }
        else
        {
            UpdateScreenPosition();
        }

        UpdateCursorVisual();
        UpdateFlame();
        if (inputLocked)
        {
            ClearSyntheticUiHover();
            SetWorldHover(null);
            return;
        }

        UpdateWorldHover();
        UpdateSyntheticUiHoverAndClick();
        HandleWorldClick();
    }

    private void ResolveReferences()
    {
        if (canvas == null)
        {
            canvas = GetComponentInParent<Canvas>();
        }

        if (cursorVisual == null)
        {
            cursorVisual = transform as RectTransform;
        }

        if (cursorGraphic == null && cursorVisual != null)
        {
            cursorGraphic = cursorVisual.GetComponent<Graphic>();
        }

        if (decorCamera == null)
        {
            decorCamera = canvas != null && canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
        }

        if (flameLight != null && flameLight.type != LightType.Point)
        {
            flameLight.type = LightType.Point;
        }
    }

    private void CacheAndApplySystemCursor()
    {
        cachedCursorVisible = Cursor.visible;
        cachedCursorLockMode = Cursor.lockState;

        if (!hideSystemCursor)
        {
            return;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = false;
    }

    private void RestoreSystemCursor()
    {
        if (!hideSystemCursor)
        {
            return;
        }

        Cursor.visible = cachedCursorVisible;
        Cursor.lockState = cachedCursorLockMode;
    }

    private void InitializeScreenPosition()
    {
        if (Mouse.current != null)
        {
            screenPosition = ClampToScreen(Mouse.current.position.ReadValue());
            hasScreenPosition = true;
            activeSource = PointerSource.Mouse;
            return;
        }

        screenPosition = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        hasScreenPosition = true;
        activeSource = PointerSource.Gamepad;
    }

    private void InitializeScreenPositionFromCursorTransform()
    {
        RectTransform sourceTransform = transform as RectTransform;
        if (sourceTransform == null)
        {
            sourceTransform = cursorVisual;
        }

        if (sourceTransform == null)
        {
            InitializeScreenPosition();
            return;
        }

        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera != null ? canvas.worldCamera : Camera.main
            : null;
        screenPosition = ClampToScreen(RectTransformUtility.WorldToScreenPoint(uiCamera, sourceTransform.position));
        hasScreenPosition = true;
        activeSource = Mouse.current != null ? PointerSource.Mouse : PointerSource.Gamepad;
        WarpHardwareMouseIfNeeded();
    }

    private void UpdateScreenPosition()
    {
        if (!hasScreenPosition)
        {
            InitializeScreenPosition();
        }

        bool mouseActive = TryReadMouseActivity(out Vector2 mousePosition);
        if (mouseActive)
        {
            screenPosition = ClampToScreen(mousePosition);
            activeSource = PointerSource.Mouse;
            return;
        }

        Vector2 gamepadMove = ReadGamepadPointerMove();
        if (gamepadMove.sqrMagnitude > 0f)
        {
            float deltaTime = Time.unscaledDeltaTime > 0f ? Time.unscaledDeltaTime : Time.deltaTime;
            screenPosition += gamepadMove * (gamepadSpeed * Mathf.Max(0f, deltaTime));
            screenPosition = ClampToScreen(screenPosition);
            activeSource = PointerSource.Gamepad;
            WarpHardwareMouseIfNeeded();
            return;
        }

        if (activeSource == PointerSource.Mouse && Mouse.current != null)
        {
            screenPosition = ClampToScreen(Mouse.current.position.ReadValue());
        }
    }

    private static bool TryReadMouseActivity(out Vector2 mousePosition)
    {
        mousePosition = Vector2.zero;
        if (Mouse.current == null)
        {
            return false;
        }

        mousePosition = Mouse.current.position.ReadValue();
        Vector2 delta = Mouse.current.delta.ReadValue();
        return delta.sqrMagnitude > 0.01f
            || Mouse.current.leftButton.wasPressedThisFrame
            || Mouse.current.rightButton.wasPressedThisFrame
            || Mouse.current.middleButton.wasPressedThisFrame;
    }

    private Vector2 ReadGamepadPointerMove()
    {
        Gamepad gamepad = Gamepad.current;
        if (gamepad == null)
        {
            return Vector2.zero;
        }

        Vector2 move = gamepad.leftStick.ReadValue();
        if (move.sqrMagnitude < gamepadDeadzone * gamepadDeadzone)
        {
            move = gamepad.rightStick.ReadValue();
        }

        if (move.sqrMagnitude < gamepadDeadzone * gamepadDeadzone)
        {
            move = gamepad.dpad.ReadValue();
        }

        if (move.sqrMagnitude < gamepadDeadzone * gamepadDeadzone)
        {
            return Vector2.zero;
        }

        return Vector2.ClampMagnitude(move, 1f);
    }

    private void WarpHardwareMouseIfNeeded()
    {
        if (!warpHardwareMouseForGamepad || Mouse.current == null)
        {
            return;
        }

        SyncHardwareMouseToScreenPosition();
    }

    private void SyncHardwareMouseToScreenPosition()
    {
        if (Mouse.current == null || !hasScreenPosition)
        {
            return;
        }

        Mouse.current.WarpCursorPosition(screenPosition);
        InputState.Change(Mouse.current.position, screenPosition);
    }

    private Vector2 ClampToScreen(Vector2 position)
    {
        float width = Mathf.Max(1f, Screen.width);
        float height = Mathf.Max(1f, Screen.height);
        return new Vector2(Mathf.Clamp(position.x, 0f, width), Mathf.Clamp(position.y, 0f, height));
    }

    private void UpdateCursorVisual()
    {
        if (cursorVisual == null)
        {
            return;
        }

        cursorVisual.SetAsLastSibling();

        RectTransform canvasRect = canvas != null ? canvas.transform as RectTransform : cursorVisual.parent as RectTransform;
        if (canvasRect == null)
        {
            cursorVisual.position = screenPosition;
            return;
        }

        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, uiCamera, out Vector2 localPoint))
        {
            cursorVisual.anchoredPosition = localPoint;
        }
    }

    private void UpdateFlame()
    {
        if (flameLight == null)
        {
            return;
        }

        Camera cam = decorCamera != null ? decorCamera : Camera.main;
        if (cam == null)
        {
            return;
        }

        EnsureFlameDecorBounds(cam);
        EnsureFlameProjectionRail(cam);

        Vector3 direction = cam.transform.forward.sqrMagnitude > 0.0001f ? cam.transform.forward.normalized : Vector3.forward;
        Vector2 viewportPosition = ScreenToViewport(screenPosition);
        Vector3 desiredAimPoint;
        Vector3 desiredPosition = ResolveFlameDesiredPosition(cam, viewportPosition, direction, out desiredAimPoint);
        if (keepFlameOutsideGeometry)
        {
            desiredPosition = ResolveFlameClearance(desiredPosition, direction);
        }

        float deltaTime = Time.unscaledDeltaTime > 0f ? Time.unscaledDeltaTime : Time.deltaTime;
        Vector3 flamePosition = SmoothFlamePosition(desiredPosition, deltaTime);
        if (keepFlameOutsideGeometry)
        {
            flamePosition = ResolveFlameClearance(flamePosition, direction);
        }

        Quaternion desiredRotation = ResolveFlameRotation(flamePosition, desiredAimPoint, direction, cam.transform.up);
        Quaternion flameRotation = SmoothFlameRotation(desiredRotation, deltaTime);

        flameLight.transform.position = flamePosition;
        flameLight.transform.rotation = flameRotation;
        currentFlamePosition = flamePosition;
        currentFlameRotation = flameRotation;
        hasFlamePose = true;
    }

    private void RebuildFlameProjectionRail()
    {
        Camera cam = decorCamera != null ? decorCamera : Camera.main;
        if (cam == null)
        {
            hasFlameDecorBounds = false;
            flameRailReady = false;
            return;
        }

        RefreshFlameDecorBounds(cam, ResolveFlameBoundsRoot());
        if (useFlameBoundsPlane && hasFlameDecorBounds)
        {
            flameRailReady = false;
            return;
        }

        BuildFlameProjectionRail(cam);
    }

    private void EnsureFlameDecorBounds(Camera cam)
    {
        if (cam == null || !useFlameBoundsPlane && !keepFlameOutsideRendererBounds)
        {
            return;
        }

        Transform root = ResolveFlameBoundsRoot();
        if (root == null)
        {
            hasFlameDecorBounds = false;
            flameActiveRendererBounds.Clear();
            return;
        }

        float now = Application.isPlaying ? Time.unscaledTime : 0f;
        if (hasFlameDecorBounds &&
            cachedFlameBoundsRoot == root &&
            cachedFlameBoundsCamera == cam &&
            now < nextFlameBoundsRefreshTime)
        {
            return;
        }

        RefreshFlameDecorBounds(cam, root);
    }

    private Transform ResolveFlameBoundsRoot()
    {
        if (flameBoundsRoot != null)
        {
            return flameBoundsRoot;
        }

        if (flameLight != null && flameLight.transform.parent != null)
        {
            return flameLight.transform.parent;
        }

        return null;
    }

    private void RefreshFlameDecorBounds(Camera cam, Transform root)
    {
        flameActiveRendererBounds.Clear();
        cachedFlameBoundsRoot = root;
        cachedFlameBoundsCamera = cam;
        nextFlameBoundsRefreshTime = (Application.isPlaying ? Time.unscaledTime : 0f) + Mathf.Max(0.05f, flameBoundsRefreshInterval);

        if (cam == null || root == null)
        {
            hasFlameDecorBounds = false;
            return;
        }

        Bounds cameraBounds = new Bounds();
        bool hasCameraBounds = false;
        Vector2 viewportMin = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 viewportMax = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        bool hasViewportBounds = false;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(includeInactiveFlameBounds);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || IsFlameRenderer(renderer))
            {
                continue;
            }

            bool activeRenderer = renderer.enabled && renderer.gameObject.activeInHierarchy;
            if (!includeInactiveFlameBounds && !activeRenderer)
            {
                continue;
            }

            Bounds bounds = renderer.bounds;
            if (!IsUsableBounds(bounds))
            {
                continue;
            }

            EncapsulateFlameProjectionBounds(cam, bounds, ref cameraBounds, ref hasCameraBounds, ref viewportMin, ref viewportMax, ref hasViewportBounds);

            if (activeRenderer)
            {
                Bounds paddedBounds = bounds;
                paddedBounds.Expand(Mathf.Max(0f, flameBoundsPadding) * 2f);
                flameActiveRendererBounds.Add(paddedBounds);
            }
        }

        Collider[] colliders = root.GetComponentsInChildren<Collider>(includeInactiveFlameBounds);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || IsFlameCollider(collider))
            {
                continue;
            }

            if (!includeInactiveFlameBounds && (!collider.enabled || !collider.gameObject.activeInHierarchy))
            {
                continue;
            }

            Bounds bounds = collider.bounds;
            if (!IsUsableBounds(bounds))
            {
                continue;
            }

            EncapsulateFlameProjectionBounds(cam, bounds, ref cameraBounds, ref hasCameraBounds, ref viewportMin, ref viewportMax, ref hasViewportBounds);
        }

        hasFlameDecorBounds = hasCameraBounds && hasViewportBounds;
        if (!hasFlameDecorBounds)
        {
            return;
        }

        flameDecorCameraBounds = cameraBounds;
        flameDecorViewportBounds = Rect.MinMaxRect(viewportMin.x, viewportMin.y, viewportMax.x, viewportMax.y);
    }

    private void EncapsulateFlameProjectionBounds(
        Camera cam,
        Bounds bounds,
        ref Bounds cameraBounds,
        ref bool hasCameraBounds,
        ref Vector2 viewportMin,
        ref Vector2 viewportMax,
        ref bool hasViewportBounds)
    {
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;
        float nearDepth = cam.nearClipPlane + 0.001f;

        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 corner = center + Vector3.Scale(extents, new Vector3(x, y, z));
                    Vector3 cameraLocal = cam.transform.InverseTransformPoint(corner);
                    if (cameraLocal.z <= nearDepth)
                    {
                        continue;
                    }

                    if (!hasCameraBounds)
                    {
                        cameraBounds = new Bounds(cameraLocal, Vector3.zero);
                        hasCameraBounds = true;
                    }
                    else
                    {
                        cameraBounds.Encapsulate(cameraLocal);
                    }

                    Vector3 viewport = cam.WorldToViewportPoint(corner);
                    if (viewport.z <= nearDepth)
                    {
                        continue;
                    }

                    Vector2 viewportPoint = new Vector2(viewport.x, viewport.y);
                    viewportMin = Vector2.Min(viewportMin, viewportPoint);
                    viewportMax = Vector2.Max(viewportMax, viewportPoint);
                    hasViewportBounds = true;
                }
            }
        }
    }

    private static bool IsUsableBounds(Bounds bounds)
    {
        Vector3 size = bounds.size;
        Vector3 center = bounds.center;
        return IsFinite(size.x) &&
            IsFinite(size.y) &&
            IsFinite(size.z) &&
            IsFinite(center.x) &&
            IsFinite(center.y) &&
            IsFinite(center.z) &&
            size.sqrMagnitude > 0.000001f;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private void EnsureFlameProjectionRail(Camera cam)
    {
        if (!useFlameProjectionRail || cam == null)
        {
            return;
        }

        if (useFlameBoundsPlane && hasFlameDecorBounds)
        {
            return;
        }

        int columns = Mathf.Clamp(flameRailColumns, 4, 96);
        int rows = Mathf.Clamp(flameRailRows, 4, 96);
        bool screenChanged = builtFlameRailScreenWidth != Screen.width ||
            builtFlameRailScreenHeight != Screen.height;

        if (!flameRailReady ||
            builtFlameRailCamera != cam ||
            builtFlameRailColumns != columns ||
            builtFlameRailRows != rows ||
            rebuildFlameRailOnScreenChange && screenChanged)
        {
            BuildFlameProjectionRail(cam);
        }
    }

    private void BuildFlameProjectionRail(Camera cam)
    {
        int columns = Mathf.Clamp(flameRailColumns, 4, 96);
        int rows = Mathf.Clamp(flameRailRows, 4, 96);
        int sampleCount = columns * rows;
        if (flameRailDepths == null || flameRailDepths.Length != sampleCount)
        {
            flameRailDepths = new float[sampleCount];
            flameRailSampleValid = new bool[sampleCount];
        }

        Vector3 forward = cam.transform.forward.sqrMagnitude > 0.0001f ? cam.transform.forward.normalized : Vector3.forward;
        float projectionDistance = ResolveFlameProjectionDistance(cam);
        float maxRayDistance = Mathf.Max(0.1f, flameRailRayDistance);
        int validCount = 0;

        for (int y = 0; y < rows; y++)
        {
            float viewportY = rows <= 1 ? 0.5f : y / (float)(rows - 1);
            for (int x = 0; x < columns; x++)
            {
                float viewportX = columns <= 1 ? 0.5f : x / (float)(columns - 1);
                Vector3 origin = cam.ViewportToWorldPoint(new Vector3(viewportX, viewportY, projectionDistance));
                int index = ResolveFlameRailIndex(x, y, columns);
                if (Physics.Raycast(origin, forward, out RaycastHit hit, maxRayDistance, flameCollisionLayers, flameCollisionTriggers) &&
                    !IsFlameCollider(hit.collider))
                {
                    flameRailDepths[index] = Mathf.Max(0f, hit.distance);
                    flameRailSampleValid[index] = true;
                    validCount++;
                }
                else
                {
                    flameRailDepths[index] = 0f;
                    flameRailSampleValid[index] = false;
                }
            }
        }

        FillMissingFlameRailDepths(columns, rows, validCount > 0 ? ResolveAverageValidFlameRailDepth(validCount) : 0f);

        builtFlameRailColumns = columns;
        builtFlameRailRows = rows;
        builtFlameRailScreenWidth = Screen.width;
        builtFlameRailScreenHeight = Screen.height;
        builtFlameRailCamera = cam;
        flameRailReady = true;
    }

    private float ResolveAverageValidFlameRailDepth(int validCount)
    {
        if (validCount <= 0 || flameRailDepths == null || flameRailSampleValid == null)
        {
            return 0f;
        }

        float sum = 0f;
        for (int i = 0; i < flameRailDepths.Length; i++)
        {
            if (flameRailSampleValid[i])
            {
                sum += flameRailDepths[i];
            }
        }

        return sum / validCount;
    }

    private void FillMissingFlameRailDepths(int columns, int rows, float fallbackDepth)
    {
        if (flameRailDepths == null || flameRailSampleValid == null)
        {
            return;
        }

        int maxPasses = columns + rows;
        for (int pass = 0; pass < maxPasses; pass++)
        {
            bool changed = false;
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < columns; x++)
                {
                    int index = ResolveFlameRailIndex(x, y, columns);
                    if (flameRailSampleValid[index])
                    {
                        continue;
                    }

                    float sum = 0f;
                    int count = 0;
                    AccumulateFlameRailNeighbor(x - 1, y, columns, rows, ref sum, ref count);
                    AccumulateFlameRailNeighbor(x + 1, y, columns, rows, ref sum, ref count);
                    AccumulateFlameRailNeighbor(x, y - 1, columns, rows, ref sum, ref count);
                    AccumulateFlameRailNeighbor(x, y + 1, columns, rows, ref sum, ref count);

                    if (count <= 0)
                    {
                        continue;
                    }

                    flameRailDepths[index] = sum / count;
                    flameRailSampleValid[index] = true;
                    changed = true;
                }
            }

            if (!changed)
            {
                break;
            }
        }

        for (int i = 0; i < flameRailDepths.Length; i++)
        {
            if (!flameRailSampleValid[i])
            {
                flameRailDepths[i] = fallbackDepth;
                flameRailSampleValid[i] = true;
            }
        }
    }

    private void AccumulateFlameRailNeighbor(int x, int y, int columns, int rows, ref float sum, ref int count)
    {
        if (x < 0 || y < 0 || x >= columns || y >= rows)
        {
            return;
        }

        int index = ResolveFlameRailIndex(x, y, columns);
        if (!flameRailSampleValid[index])
        {
            return;
        }

        sum += flameRailDepths[index];
        count++;
    }

    private Vector3 ResolveFlameDesiredPosition(Camera cam, Vector2 viewportPosition, Vector3 direction, out Vector3 aimPoint)
    {
        if (useFlameBoundsPlane && hasFlameDecorBounds)
        {
            return ResolveFlameBoundsPlanePosition(cam, viewportPosition, direction, out aimPoint);
        }

        float projectionDistance = ResolveFlameProjectionDistance(cam);
        Vector3 origin = cam.ViewportToWorldPoint(new Vector3(viewportPosition.x, viewportPosition.y, projectionDistance));

        if (useFlameProjectionRail && flameRailReady && flameRailDepths != null && flameRailDepths.Length > 0)
        {
            float railDepth = SampleFlameRailDepth(viewportPosition);
            float standOff = Mathf.Max(0f, flameRailSurfaceStandOff) + Mathf.Max(0f, flameSurfaceOffset);
            float lightDepth = Mathf.Max(0f, railDepth - standOff - Mathf.Max(0f, flameCollisionRadius));
            aimPoint = origin + direction * railDepth;
            return origin + direction * lightDepth;
        }

        aimPoint = origin + direction * ResolveFlameAimExtraDistance();
        if (!keepFlameOutsideGeometry)
        {
            return origin;
        }

        return ResolveFlameBlockedPosition(new Ray(origin, direction), origin, 0f);
    }

    private Vector3 ResolveFlameBoundsPlanePosition(Camera cam, Vector2 viewportPosition, Vector3 direction, out Vector3 aimPoint)
    {
        Vector2 clampedViewport = ClampFlameViewportToDecorBounds(viewportPosition);
        float planeDistance = ResolveFlameBoundsPlaneDistance(cam);
        Vector3 position = ResolvePointOnCameraParallelPlane(cam, clampedViewport, planeDistance, direction);

        float aimDistance = Mathf.Min(
            Mathf.Max(planeDistance, flameDecorCameraBounds.max.z + Mathf.Max(0f, flameBoundsPadding)),
            planeDistance + ResolveFlameAimExtraDistance());
        if (aimDistance <= planeDistance + 0.001f)
        {
            aimDistance = planeDistance + ResolveFlameAimExtraDistance();
        }

        aimPoint = ResolvePointOnCameraParallelPlane(cam, clampedViewport, aimDistance, direction);
        return position;
    }

    private Vector2 ClampFlameViewportToDecorBounds(Vector2 viewportPosition)
    {
        if (!clampFlameToDecorViewportBounds || !hasFlameDecorBounds)
        {
            return viewportPosition;
        }

        float padding = Mathf.Clamp(flameViewportBoundsPadding, 0f, 0.25f);
        float minX = Mathf.Clamp01(flameDecorViewportBounds.xMin - padding);
        float maxX = Mathf.Clamp01(flameDecorViewportBounds.xMax + padding);
        float minY = Mathf.Clamp01(flameDecorViewportBounds.yMin - padding);
        float maxY = Mathf.Clamp01(flameDecorViewportBounds.yMax + padding);

        if (minX > maxX || minY > maxY)
        {
            return viewportPosition;
        }

        return new Vector2(
            Mathf.Clamp(viewportPosition.x, minX, maxX),
            Mathf.Clamp(viewportPosition.y, minY, maxY));
    }

    private float ResolveFlameBoundsPlaneDistance(Camera cam)
    {
        float nearPlane = cam != null ? cam.nearClipPlane + 0.001f : 0.001f;
        float padding = Mathf.Max(0f, flameBoundsPadding);
        float minDecorDepth = Mathf.Max(nearPlane, flameDecorCameraBounds.min.z - padding);
        float maxDecorDepth = Mathf.Max(minDecorDepth, flameDecorCameraBounds.max.z + padding);
        float standOff = Mathf.Max(0f, flameRailSurfaceStandOff) +
            Mathf.Max(0f, flameSurfaceOffset) +
            Mathf.Max(0f, flameCollisionRadius);

        return Mathf.Clamp(minDecorDepth - standOff, nearPlane, maxDecorDepth);
    }

    private Vector3 ResolvePointOnCameraParallelPlane(Camera cam, Vector2 viewportPosition, float planeDistance, Vector3 planeNormal)
    {
        float distance = Mathf.Max(cam.nearClipPlane + 0.001f, planeDistance);
        Ray ray = cam.ViewportPointToRay(new Vector3(viewportPosition.x, viewportPosition.y, 0f));
        Plane plane = new Plane(planeNormal, cam.transform.position + planeNormal * distance);
        if (plane.Raycast(ray, out float enter))
        {
            return ray.GetPoint(enter);
        }

        return cam.ViewportToWorldPoint(new Vector3(viewportPosition.x, viewportPosition.y, distance));
    }

    private float ResolveFlameProjectionDistance(Camera cam)
    {
        float nearPlane = cam != null ? cam.nearClipPlane : 0.01f;
        return Mathf.Max(nearPlane + 0.001f, flameCameraOffset);
    }

    private float ResolveFlameAimExtraDistance()
    {
        if (flameLight == null)
        {
            return 0.5f;
        }

        return Mathf.Max(0.1f, flameLight.range);
    }

    private float SampleFlameRailDepth(Vector2 viewportPosition)
    {
        int columns = Mathf.Max(1, builtFlameRailColumns);
        int rows = Mathf.Max(1, builtFlameRailRows);
        if (flameRailDepths == null || flameRailDepths.Length < columns * rows)
        {
            return 0f;
        }

        float x = Mathf.Clamp01(viewportPosition.x) * (columns - 1);
        float y = Mathf.Clamp01(viewportPosition.y) * (rows - 1);
        int x0 = Mathf.FloorToInt(x);
        int y0 = Mathf.FloorToInt(y);
        int x1 = Mathf.Min(x0 + 1, columns - 1);
        int y1 = Mathf.Min(y0 + 1, rows - 1);
        float tx = x - x0;
        float ty = y - y0;

        float d00 = flameRailDepths[ResolveFlameRailIndex(x0, y0, columns)];
        float d10 = flameRailDepths[ResolveFlameRailIndex(x1, y0, columns)];
        float d01 = flameRailDepths[ResolveFlameRailIndex(x0, y1, columns)];
        float d11 = flameRailDepths[ResolveFlameRailIndex(x1, y1, columns)];

        float bottom = Mathf.Lerp(d00, d10, tx);
        float top = Mathf.Lerp(d01, d11, tx);
        return Mathf.Lerp(bottom, top, ty);
    }

    private Vector2 ScreenToViewport(Vector2 position)
    {
        float width = Mathf.Max(1f, Screen.width);
        float height = Mathf.Max(1f, Screen.height);
        return new Vector2(Mathf.Clamp01(position.x / width), Mathf.Clamp01(position.y / height));
    }

    private static int ResolveFlameRailIndex(int x, int y, int columns)
    {
        return y * columns + x;
    }

    private Vector3 ResolveFlameBlockedPosition(Ray ray, Vector3 desiredPosition, float distanceFromCamera)
    {
        Vector3 resolvedPosition = desiredPosition;
        float radius = Mathf.Max(0f, flameCollisionRadius);
        float surfaceOffset = Mathf.Max(0f, flameSurfaceOffset);
        float castDistance = distanceFromCamera + surfaceOffset + radius;

        if (castDistance > 0.0001f && TryFindFlameObstacle(ray, castDistance, radius, out RaycastHit hit))
        {
            if (radius > 0.0001f)
            {
                resolvedPosition = hit.point + hit.normal * (radius + surfaceOffset);
            }
            else
            {
                resolvedPosition = hit.point + hit.normal * surfaceOffset;
            }
        }

        return ResolveFlameOverlaps(resolvedPosition, ray.direction);
    }

    private Vector3 ResolveFlameClearance(Vector3 position, Vector3 fallbackDirection)
    {
        Vector3 resolvedPosition = ResolveFlameOverlaps(position, fallbackDirection);
        if (keepFlameOutsideRendererBounds)
        {
            resolvedPosition = ResolveFlameRendererBoundsOverlaps(resolvedPosition);
            resolvedPosition = ResolveFlameOverlaps(resolvedPosition, fallbackDirection);
        }

        return resolvedPosition;
    }

    private Vector3 SmoothFlamePosition(Vector3 desiredPosition, float deltaTime)
    {
        if (!hasFlamePose || flamePositionSharpness <= 0f || deltaTime <= 0f)
        {
            return desiredPosition;
        }

        float t = 1f - Mathf.Exp(-flamePositionSharpness * deltaTime);
        return Vector3.Lerp(currentFlamePosition, desiredPosition, t);
    }

    private Quaternion SmoothFlameRotation(Quaternion desiredRotation, float deltaTime)
    {
        if (!hasFlamePose || flameRotationSharpness <= 0f || deltaTime <= 0f)
        {
            return desiredRotation;
        }

        float t = 1f - Mathf.Exp(-flameRotationSharpness * deltaTime);
        return Quaternion.Slerp(currentFlameRotation, desiredRotation, t);
    }

    private static Quaternion ResolveFlameRotation(Vector3 position, Vector3 aimPoint, Vector3 fallbackDirection, Vector3 up)
    {
        Vector3 lookDirection = aimPoint - position;
        if (lookDirection.sqrMagnitude <= 0.0001f)
        {
            lookDirection = fallbackDirection;
        }

        if (lookDirection.sqrMagnitude <= 0.0001f)
        {
            lookDirection = Vector3.forward;
        }

        return Quaternion.LookRotation(lookDirection.normalized, up);
    }

    private bool TryFindFlameObstacle(Ray ray, float castDistance, float radius, out RaycastHit hit)
    {
        if (radius > 0.0001f)
        {
            return Physics.SphereCast(
                ray,
                radius,
                out hit,
                castDistance,
                flameCollisionLayers,
                flameCollisionTriggers) &&
                !IsFlameCollider(hit.collider);
        }

        return Physics.Raycast(
            ray,
            out hit,
            castDistance,
            flameCollisionLayers,
            flameCollisionTriggers) &&
            !IsFlameCollider(hit.collider);
    }

    private Vector3 ResolveFlameOverlaps(Vector3 position, Vector3 fallbackDirection)
    {
        float radius = Mathf.Max(0f, flameCollisionRadius);
        if (radius <= 0.0001f)
        {
            return position;
        }

        Vector3 resolvedPosition = position;
        float clearance = radius + Mathf.Max(0f, flameSurfaceOffset);
        float clearanceSqr = clearance * clearance;
        int iterations = Mathf.Clamp(flameCollisionIterations, 1, 6);

        for (int iteration = 0; iteration < iterations; iteration++)
        {
            int hitCount = Physics.OverlapSphereNonAlloc(
                resolvedPosition,
                clearance,
                flameOverlapBuffer,
                flameCollisionLayers,
                flameCollisionTriggers);

            bool adjusted = false;
            int usableHitCount = Mathf.Min(hitCount, flameOverlapBuffer.Length);
            for (int i = 0; i < usableHitCount; i++)
            {
                Collider hitCollider = flameOverlapBuffer[i];
                if (hitCollider == null || IsFlameCollider(hitCollider))
                {
                    continue;
                }

                Vector3 closestPoint = hitCollider.ClosestPoint(resolvedPosition);
                Vector3 pushDirection = resolvedPosition - closestPoint;
                float pushSqrMagnitude = pushDirection.sqrMagnitude;
                if (pushSqrMagnitude > clearanceSqr)
                {
                    continue;
                }

                if (pushSqrMagnitude > 0.000001f)
                {
                    float distance = Mathf.Sqrt(pushSqrMagnitude);
                    resolvedPosition += pushDirection / distance * (clearance - distance + 0.001f);
                    adjusted = true;
                    continue;
                }

                pushDirection = resolvedPosition - hitCollider.bounds.center;
                if (pushDirection.sqrMagnitude <= 0.000001f)
                {
                    pushDirection = -fallbackDirection;
                }

                if (pushDirection.sqrMagnitude <= 0.000001f)
                {
                    pushDirection = Vector3.up;
                }

                resolvedPosition += pushDirection.normalized * (clearance + 0.001f);
                adjusted = true;
            }

            if (!adjusted)
            {
                break;
            }
        }

        return resolvedPosition;
    }

    private Vector3 ResolveFlameRendererBoundsOverlaps(Vector3 position)
    {
        if (flameActiveRendererBounds.Count == 0)
        {
            return position;
        }

        Vector3 resolvedPosition = position;
        float clearance = Mathf.Max(0f, flameCollisionRadius) + Mathf.Max(0f, flameSurfaceOffset);
        int iterations = Mathf.Clamp(flameCollisionIterations, 1, 6);

        for (int iteration = 0; iteration < iterations; iteration++)
        {
            bool adjusted = false;
            for (int i = 0; i < flameActiveRendererBounds.Count; i++)
            {
                Bounds solidBounds = flameActiveRendererBounds[i];
                Bounds clearanceBounds = solidBounds;
                clearanceBounds.Expand(clearance * 2f);
                if (!clearanceBounds.Contains(resolvedPosition))
                {
                    continue;
                }

                Vector3 closestPoint = solidBounds.ClosestPoint(resolvedPosition);
                Vector3 pushDirection = resolvedPosition - closestPoint;
                float pushSqrMagnitude = pushDirection.sqrMagnitude;

                if (pushSqrMagnitude > 0.000001f)
                {
                    float distance = Mathf.Sqrt(pushSqrMagnitude);
                    float correction = clearance - distance + 0.001f;
                    if (correction > 0f)
                    {
                        resolvedPosition += pushDirection / distance * correction;
                        adjusted = true;
                    }

                    continue;
                }

                resolvedPosition = PushPointOutsideBounds(resolvedPosition, solidBounds, clearance);
                adjusted = true;
            }

            if (!adjusted)
            {
                break;
            }
        }

        return resolvedPosition;
    }

    private static Vector3 PushPointOutsideBounds(Vector3 point, Bounds bounds, float clearance)
    {
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        float epsilon = clearance + 0.001f;

        float bestDistance = Mathf.Abs(max.x - point.x);
        float targetValue = max.x + epsilon;
        int axis = 0;

        ConsiderBoundsFace(point.x - min.x, min.x - epsilon, 0, ref bestDistance, ref targetValue, ref axis);
        ConsiderBoundsFace(max.x - point.x, max.x + epsilon, 0, ref bestDistance, ref targetValue, ref axis);
        ConsiderBoundsFace(point.y - min.y, min.y - epsilon, 1, ref bestDistance, ref targetValue, ref axis);
        ConsiderBoundsFace(max.y - point.y, max.y + epsilon, 1, ref bestDistance, ref targetValue, ref axis);
        ConsiderBoundsFace(point.z - min.z, min.z - epsilon, 2, ref bestDistance, ref targetValue, ref axis);
        ConsiderBoundsFace(max.z - point.z, max.z + epsilon, 2, ref bestDistance, ref targetValue, ref axis);

        switch (axis)
        {
            case 0:
                point.x = targetValue;
                break;
            case 1:
                point.y = targetValue;
                break;
            default:
                point.z = targetValue;
                break;
        }

        return point;
    }

    private static void ConsiderBoundsFace(
        float distance,
        float targetValue,
        int axis,
        ref float bestDistance,
        ref float bestTargetValue,
        ref int bestAxis)
    {
        if (distance < bestDistance)
        {
            bestDistance = distance;
            bestTargetValue = targetValue;
            bestAxis = axis;
        }
    }

    private bool IsFlameCollider(Collider candidate)
    {
        return flameLight != null &&
            candidate != null &&
            candidate.transform.IsChildOf(flameLight.transform);
    }

    private bool IsFlameRenderer(Renderer candidate)
    {
        return flameLight != null &&
            candidate != null &&
            candidate.transform.IsChildOf(flameLight.transform);
    }

    private void UpdateWorldHover()
    {
        Camera cam = decorCamera != null ? decorCamera : Camera.main;
        if (cam == null)
        {
            SetWorldHover(null);
            return;
        }

        Ray ray = cam.ScreenPointToRay(screenPosition);
        CursorIntercation nextHover = null;
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Max(0.1f, worldRayDistance), interactionLayers, QueryTriggerInteraction.Collide))
        {
            nextHover = hit.collider.GetComponentInParent<CursorIntercation>();
        }

        SetWorldHover(nextHover);
    }

    private void SetWorldHover(CursorIntercation nextHover)
    {
        if (worldHover == nextHover)
        {
            return;
        }

        if (worldHover != null)
        {
            worldHover.SetCursorHovered(false);
        }

        worldHover = nextHover;

        if (worldHover != null)
        {
            worldHover.SetCursorHovered(true);
        }
    }

    private void UpdateSyntheticUiHoverAndClick()
    {
        if (!synthesizeGamepadUiEvents || activeSource != PointerSource.Gamepad)
        {
            ClearSyntheticUiHover();
            return;
        }

        GameObject hit = RaycastUi(screenPosition);
        SetSyntheticUiHover(hit);

        if (hit != null && WasGamepadSubmitPressed())
        {
            DispatchSyntheticClick(hit);
        }
    }

    private void HandleWorldClick()
    {
        if (worldHover == null)
        {
            return;
        }

        bool gamepadClick = activeSource == PointerSource.Gamepad && WasGamepadSubmitPressed() && RaycastUi(screenPosition) == null;
        bool mouseClick = activeSource == PointerSource.Mouse
            && Mouse.current != null
            && Mouse.current.leftButton.wasPressedThisFrame
            && RaycastUi(screenPosition) == null;

        if (gamepadClick || mouseClick)
        {
            worldHover.NotifyCursorClick();
        }
    }

    private static bool WasGamepadSubmitPressed()
    {
        Gamepad gamepad = Gamepad.current;
        return gamepad != null && gamepad.buttonSouth.wasPressedThisFrame;
    }

    private GameObject RaycastUi(Vector2 position)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            return null;
        }

        if (pointerEventData == null || pointerEventSystem != eventSystem)
        {
            pointerEventData = new PointerEventData(eventSystem);
            pointerEventSystem = eventSystem;
        }

        pointerEventData.Reset();
        pointerEventData.position = position;
        pointerEventData.button = PointerEventData.InputButton.Left;
        uiRaycastResults.Clear();
        eventSystem.RaycastAll(pointerEventData, uiRaycastResults);

        for (int i = 0; i < uiRaycastResults.Count; i++)
        {
            GameObject candidate = uiRaycastResults[i].gameObject;
            if (candidate == null || cursorVisual != null && candidate.transform.IsChildOf(cursorVisual))
            {
                continue;
            }

            if (candidate.GetComponentInParent<Selectable>() != null ||
                HasPointerClickHandler(candidate.transform) ||
                HasMenuCursorHandler(candidate.transform))
            {
                return candidate;
            }
        }

        return null;
    }

    private void SetSyntheticUiHover(GameObject nextHover)
    {
        if (syntheticUiHover == nextHover)
        {
            return;
        }

        if (syntheticUiHover != null && pointerEventData != null)
        {
            ExecuteEvents.ExecuteHierarchy(syntheticUiHover, pointerEventData, ExecuteEvents.pointerExitHandler);
        }

        syntheticUiHover = nextHover;

        if (syntheticUiHover != null && pointerEventData != null)
        {
            ExecuteEvents.ExecuteHierarchy(syntheticUiHover, pointerEventData, ExecuteEvents.pointerEnterHandler);
        }
    }

    private void ClearSyntheticUiHover()
    {
        SetSyntheticUiHover(null);
    }

    private void DispatchSyntheticClick(GameObject target)
    {
        EventSystem eventSystem = EventSystem.current;
        if (target == null || eventSystem == null)
        {
            return;
        }

        if (pointerEventData == null || pointerEventSystem != eventSystem)
        {
            pointerEventData = new PointerEventData(eventSystem);
            pointerEventSystem = eventSystem;
        }

        pointerEventData.Reset();
        pointerEventData.position = screenPosition;
        pointerEventData.button = PointerEventData.InputButton.Left;
        pointerEventData.clickCount = 1;
        pointerEventData.eligibleForClick = true;

        ExecuteEvents.ExecuteHierarchy(target, pointerEventData, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.ExecuteHierarchy(target, pointerEventData, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.ExecuteHierarchy(target, pointerEventData, ExecuteEvents.pointerClickHandler);
    }

    private static bool HasPointerClickHandler(Transform transform)
    {
        for (Transform current = transform; current != null; current = current.parent)
        {
            MonoBehaviour[] behaviours = current.GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IPointerClickHandler)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool HasMenuCursorHandler(Transform transform)
    {
        for (Transform current = transform; current != null; current = current.parent)
        {
            MonoBehaviour[] behaviours = current.GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IMenuCursorHandler)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void OnValidate()
    {
        gamepadSpeed = Mathf.Max(0f, gamepadSpeed);
        gamepadDeadzone = Mathf.Clamp01(gamepadDeadzone);
        flameCameraOffset = Mathf.Max(0f, flameCameraOffset);
        worldRayDistance = Mathf.Max(0.1f, worldRayDistance);
        flameCollisionRadius = Mathf.Max(0f, flameCollisionRadius);
        flameSurfaceOffset = Mathf.Max(0f, flameSurfaceOffset);
        flameCollisionIterations = Mathf.Clamp(flameCollisionIterations, 1, 6);
        flameViewportBoundsPadding = Mathf.Clamp(flameViewportBoundsPadding, 0f, 0.25f);
        flameBoundsPadding = Mathf.Max(0f, flameBoundsPadding);
        flameBoundsRefreshInterval = Mathf.Max(0.05f, flameBoundsRefreshInterval);
        flameRailColumns = Mathf.Clamp(flameRailColumns, 4, 96);
        flameRailRows = Mathf.Clamp(flameRailRows, 4, 96);
        flameRailSurfaceStandOff = Mathf.Max(0f, flameRailSurfaceStandOff);
        flameRailRayDistance = Mathf.Max(0.1f, flameRailRayDistance);
        flamePositionSharpness = Mathf.Max(0f, flamePositionSharpness);
        flameRotationSharpness = Mathf.Max(0f, flameRotationSharpness);
    }
}
