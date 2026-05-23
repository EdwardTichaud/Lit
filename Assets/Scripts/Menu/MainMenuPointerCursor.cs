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
    [SerializeField] private Light torchLight;

    [Header("Gamepad Pointer")]
    [SerializeField] private float gamepadSpeed = 1150f;
    [SerializeField] private float gamepadDeadzone = 0.15f;
    [SerializeField] private bool warpHardwareMouseForGamepad = true;
    [SerializeField] private bool synthesizeGamepadUiEvents = true;

    [Header("Torch")]
    [SerializeField] private float torchCameraOffset = 0.2f;
    [SerializeField] private float worldRayDistance = 50f;
    [SerializeField] private LayerMask interactionLayers = ~0;

    [Header("Torch Collision")]
    [SerializeField, Tooltip("Garde la lumiere du curseur en dehors des colliders du decor.")]
    private bool keepTorchOutsideGeometry = true;
    [SerializeField] private LayerMask torchCollisionLayers = ~0;
    [SerializeField, Min(0f), Tooltip("Rayon physique autour de la lumiere.")]
    private float torchCollisionRadius = 0.12f;
    [SerializeField, Min(0f), Tooltip("Distance minimale gardee entre la lumiere et la surface touchee.")]
    private float torchSurfaceOffset = 0.035f;
    [SerializeField, Range(1, 6), Tooltip("Nombre de corrections si la lumiere commence dans un collider.")]
    private int torchCollisionIterations = 3;
    [SerializeField] private QueryTriggerInteraction torchCollisionTriggers = QueryTriggerInteraction.Ignore;

    [Header("Torch Projection Plane")]
    [SerializeField, Tooltip("Place la lumiere sur un plan parallele a la camera/UI borne par le decor 3D.")]
    private bool useTorchBoundsPlane = true;
    [SerializeField, Tooltip("Racine des objets 3D qui bornent la navigation de la lumiere. Vide = parent de la lumiere.")]
    private Transform torchBoundsRoot;
    [SerializeField, Tooltip("Inclut les etats de decor inactifs dans les bounds du plan pour eviter un changement de zone selon la sauvegarde.")]
    private bool includeInactiveTorchBounds = true;
    [SerializeField, Tooltip("Limite la projection de la lumiere a la zone ecran occupee par le decor.")]
    private bool clampTorchToDecorViewportBounds = true;
    [SerializeField, Range(0f, 0.25f), Tooltip("Padding en coordonnees viewport autour des bounds projetes du decor.")]
    private float torchViewportBoundsPadding = 0.015f;
    [SerializeField, Min(0f), Tooltip("Padding monde ajoute aux bounds du decor pour les corrections de placement.")]
    private float torchBoundsPadding = 0.08f;
    [SerializeField, Min(0.05f), Tooltip("Intervalle de recalcul des bounds du decor.")]
    private float torchBoundsRefreshInterval = 0.25f;
    [SerializeField, Tooltip("Utilise les bounds des renderers actifs pour garder la lumiere hors des meshes sans collider.")]
    private bool keepTorchOutsideRendererBounds = true;

    [Header("Torch Projection Rail Fallback")]
    [SerializeField, Tooltip("Construit au lancement une grille de profondeurs devant la camera pour guider la lumiere.")]
    private bool useTorchProjectionRail = true;
    [SerializeField, Range(4, 96)] private int torchRailColumns = 32;
    [SerializeField, Range(4, 96)] private int torchRailRows = 18;
    [SerializeField, Min(0.1f), Tooltip("Distance maximale des raycasts du rail depuis le plan de projection.")]
    private float torchRailRayDistance = 80f;
    [SerializeField, Min(0f), Tooltip("Distance gardee entre la lumiere et le relief du rail.")]
    private float torchRailSurfaceStandOff = 0.32f;
    [SerializeField, Tooltip("Reconstruit le rail si la taille d'ecran change.")]
    private bool rebuildTorchRailOnScreenChange = true;
    [SerializeField, Min(0f), Tooltip("Lissage de la position de la lumiere. 0 = instantane.")]
    private float torchPositionSharpness = 18f;
    [SerializeField, Min(0f), Tooltip("Lissage de l'orientation de la lumiere. 0 = instantane.")]
    private float torchRotationSharpness = 22f;

    [Header("System Cursor")]
    [SerializeField] private bool hideSystemCursor = true;

    private readonly Collider[] torchOverlapBuffer = new Collider[16];
    private readonly List<Bounds> torchActiveRendererBounds = new List<Bounds>();
    private readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();
    private PointerEventData pointerEventData;
    private EventSystem pointerEventSystem;
    private Bounds torchDecorCameraBounds;
    private Rect torchDecorViewportBounds;
    private bool hasTorchDecorBounds;
    private Transform cachedTorchBoundsRoot;
    private Camera cachedTorchBoundsCamera;
    private float nextTorchBoundsRefreshTime;
    private float[] torchRailDepths;
    private bool[] torchRailSampleValid;
    private int builtTorchRailColumns;
    private int builtTorchRailRows;
    private int builtTorchRailScreenWidth;
    private int builtTorchRailScreenHeight;
    private Camera builtTorchRailCamera;
    private bool torchRailReady;
    private Vector2 screenPosition;
    private bool hasScreenPosition;
    private PointerSource activeSource = PointerSource.Mouse;
    private bool cachedCursorVisible;
    private CursorLockMode cachedCursorLockMode;
    private GameObject syntheticUiHover;
    private CursorIntercation worldHover;
    private Vector3 currentTorchPosition;
    private Quaternion currentTorchRotation = Quaternion.identity;
    private bool hasTorchPose;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        CacheAndApplySystemCursor();
        InitializeScreenPosition();
        RebuildTorchProjectionRail();
        if (torchLight != null)
        {
            torchLight.enabled = true;
        }
    }

    private void OnDisable()
    {
        ClearSyntheticUiHover();
        SetWorldHover(null);
        RestoreSystemCursor();
        hasTorchPose = false;
        torchRailReady = false;
        hasTorchDecorBounds = false;
        torchActiveRendererBounds.Clear();
        if (torchLight != null)
        {
            torchLight.enabled = false;
        }
    }

    private void Update()
    {
        ResolveReferences();
        UpdateScreenPosition();
        UpdateCursorVisual();
        UpdateTorch();
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

        if (decorCamera == null)
        {
            decorCamera = canvas != null && canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
        }

        if (torchLight != null && torchLight.type != LightType.Point)
        {
            torchLight.type = LightType.Point;
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

        cursorVisual.gameObject.SetActive(true);
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

    private void UpdateTorch()
    {
        if (torchLight == null)
        {
            return;
        }

        Camera cam = decorCamera != null ? decorCamera : Camera.main;
        if (cam == null)
        {
            return;
        }

        EnsureTorchDecorBounds(cam);
        EnsureTorchProjectionRail(cam);

        Vector3 direction = cam.transform.forward.sqrMagnitude > 0.0001f ? cam.transform.forward.normalized : Vector3.forward;
        Vector2 viewportPosition = ScreenToViewport(screenPosition);
        Vector3 desiredAimPoint;
        Vector3 desiredPosition = ResolveTorchDesiredPosition(cam, viewportPosition, direction, out desiredAimPoint);
        if (keepTorchOutsideGeometry)
        {
            desiredPosition = ResolveTorchClearance(desiredPosition, direction);
        }

        float deltaTime = Time.unscaledDeltaTime > 0f ? Time.unscaledDeltaTime : Time.deltaTime;
        Vector3 torchPosition = SmoothTorchPosition(desiredPosition, deltaTime);
        if (keepTorchOutsideGeometry)
        {
            torchPosition = ResolveTorchClearance(torchPosition, direction);
        }

        Quaternion desiredRotation = ResolveTorchRotation(torchPosition, desiredAimPoint, direction, cam.transform.up);
        Quaternion torchRotation = SmoothTorchRotation(desiredRotation, deltaTime);

        torchLight.transform.position = torchPosition;
        torchLight.transform.rotation = torchRotation;
        currentTorchPosition = torchPosition;
        currentTorchRotation = torchRotation;
        hasTorchPose = true;
    }

    private void RebuildTorchProjectionRail()
    {
        Camera cam = decorCamera != null ? decorCamera : Camera.main;
        if (cam == null)
        {
            hasTorchDecorBounds = false;
            torchRailReady = false;
            return;
        }

        RefreshTorchDecorBounds(cam, ResolveTorchBoundsRoot());
        if (useTorchBoundsPlane && hasTorchDecorBounds)
        {
            torchRailReady = false;
            return;
        }

        BuildTorchProjectionRail(cam);
    }

    private void EnsureTorchDecorBounds(Camera cam)
    {
        if (cam == null || !useTorchBoundsPlane && !keepTorchOutsideRendererBounds)
        {
            return;
        }

        Transform root = ResolveTorchBoundsRoot();
        if (root == null)
        {
            hasTorchDecorBounds = false;
            torchActiveRendererBounds.Clear();
            return;
        }

        float now = Application.isPlaying ? Time.unscaledTime : 0f;
        if (hasTorchDecorBounds &&
            cachedTorchBoundsRoot == root &&
            cachedTorchBoundsCamera == cam &&
            now < nextTorchBoundsRefreshTime)
        {
            return;
        }

        RefreshTorchDecorBounds(cam, root);
    }

    private Transform ResolveTorchBoundsRoot()
    {
        if (torchBoundsRoot != null)
        {
            return torchBoundsRoot;
        }

        if (torchLight != null && torchLight.transform.parent != null)
        {
            return torchLight.transform.parent;
        }

        return null;
    }

    private void RefreshTorchDecorBounds(Camera cam, Transform root)
    {
        torchActiveRendererBounds.Clear();
        cachedTorchBoundsRoot = root;
        cachedTorchBoundsCamera = cam;
        nextTorchBoundsRefreshTime = (Application.isPlaying ? Time.unscaledTime : 0f) + Mathf.Max(0.05f, torchBoundsRefreshInterval);

        if (cam == null || root == null)
        {
            hasTorchDecorBounds = false;
            return;
        }

        Bounds cameraBounds = new Bounds();
        bool hasCameraBounds = false;
        Vector2 viewportMin = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 viewportMax = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        bool hasViewportBounds = false;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(includeInactiveTorchBounds);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || IsTorchRenderer(renderer))
            {
                continue;
            }

            bool activeRenderer = renderer.enabled && renderer.gameObject.activeInHierarchy;
            if (!includeInactiveTorchBounds && !activeRenderer)
            {
                continue;
            }

            Bounds bounds = renderer.bounds;
            if (!IsUsableBounds(bounds))
            {
                continue;
            }

            EncapsulateTorchProjectionBounds(cam, bounds, ref cameraBounds, ref hasCameraBounds, ref viewportMin, ref viewportMax, ref hasViewportBounds);

            if (activeRenderer)
            {
                Bounds paddedBounds = bounds;
                paddedBounds.Expand(Mathf.Max(0f, torchBoundsPadding) * 2f);
                torchActiveRendererBounds.Add(paddedBounds);
            }
        }

        Collider[] colliders = root.GetComponentsInChildren<Collider>(includeInactiveTorchBounds);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || IsTorchCollider(collider))
            {
                continue;
            }

            if (!includeInactiveTorchBounds && (!collider.enabled || !collider.gameObject.activeInHierarchy))
            {
                continue;
            }

            Bounds bounds = collider.bounds;
            if (!IsUsableBounds(bounds))
            {
                continue;
            }

            EncapsulateTorchProjectionBounds(cam, bounds, ref cameraBounds, ref hasCameraBounds, ref viewportMin, ref viewportMax, ref hasViewportBounds);
        }

        hasTorchDecorBounds = hasCameraBounds && hasViewportBounds;
        if (!hasTorchDecorBounds)
        {
            return;
        }

        torchDecorCameraBounds = cameraBounds;
        torchDecorViewportBounds = Rect.MinMaxRect(viewportMin.x, viewportMin.y, viewportMax.x, viewportMax.y);
    }

    private void EncapsulateTorchProjectionBounds(
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

    private void EnsureTorchProjectionRail(Camera cam)
    {
        if (!useTorchProjectionRail || cam == null)
        {
            return;
        }

        if (useTorchBoundsPlane && hasTorchDecorBounds)
        {
            return;
        }

        int columns = Mathf.Clamp(torchRailColumns, 4, 96);
        int rows = Mathf.Clamp(torchRailRows, 4, 96);
        bool screenChanged = builtTorchRailScreenWidth != Screen.width ||
            builtTorchRailScreenHeight != Screen.height;

        if (!torchRailReady ||
            builtTorchRailCamera != cam ||
            builtTorchRailColumns != columns ||
            builtTorchRailRows != rows ||
            rebuildTorchRailOnScreenChange && screenChanged)
        {
            BuildTorchProjectionRail(cam);
        }
    }

    private void BuildTorchProjectionRail(Camera cam)
    {
        int columns = Mathf.Clamp(torchRailColumns, 4, 96);
        int rows = Mathf.Clamp(torchRailRows, 4, 96);
        int sampleCount = columns * rows;
        if (torchRailDepths == null || torchRailDepths.Length != sampleCount)
        {
            torchRailDepths = new float[sampleCount];
            torchRailSampleValid = new bool[sampleCount];
        }

        Vector3 forward = cam.transform.forward.sqrMagnitude > 0.0001f ? cam.transform.forward.normalized : Vector3.forward;
        float projectionDistance = ResolveTorchProjectionDistance(cam);
        float maxRayDistance = Mathf.Max(0.1f, torchRailRayDistance);
        int validCount = 0;

        for (int y = 0; y < rows; y++)
        {
            float viewportY = rows <= 1 ? 0.5f : y / (float)(rows - 1);
            for (int x = 0; x < columns; x++)
            {
                float viewportX = columns <= 1 ? 0.5f : x / (float)(columns - 1);
                Vector3 origin = cam.ViewportToWorldPoint(new Vector3(viewportX, viewportY, projectionDistance));
                int index = ResolveTorchRailIndex(x, y, columns);
                if (Physics.Raycast(origin, forward, out RaycastHit hit, maxRayDistance, torchCollisionLayers, torchCollisionTriggers) &&
                    !IsTorchCollider(hit.collider))
                {
                    torchRailDepths[index] = Mathf.Max(0f, hit.distance);
                    torchRailSampleValid[index] = true;
                    validCount++;
                }
                else
                {
                    torchRailDepths[index] = 0f;
                    torchRailSampleValid[index] = false;
                }
            }
        }

        FillMissingTorchRailDepths(columns, rows, validCount > 0 ? ResolveAverageValidTorchRailDepth(validCount) : 0f);

        builtTorchRailColumns = columns;
        builtTorchRailRows = rows;
        builtTorchRailScreenWidth = Screen.width;
        builtTorchRailScreenHeight = Screen.height;
        builtTorchRailCamera = cam;
        torchRailReady = true;
    }

    private float ResolveAverageValidTorchRailDepth(int validCount)
    {
        if (validCount <= 0 || torchRailDepths == null || torchRailSampleValid == null)
        {
            return 0f;
        }

        float sum = 0f;
        for (int i = 0; i < torchRailDepths.Length; i++)
        {
            if (torchRailSampleValid[i])
            {
                sum += torchRailDepths[i];
            }
        }

        return sum / validCount;
    }

    private void FillMissingTorchRailDepths(int columns, int rows, float fallbackDepth)
    {
        if (torchRailDepths == null || torchRailSampleValid == null)
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
                    int index = ResolveTorchRailIndex(x, y, columns);
                    if (torchRailSampleValid[index])
                    {
                        continue;
                    }

                    float sum = 0f;
                    int count = 0;
                    AccumulateTorchRailNeighbor(x - 1, y, columns, rows, ref sum, ref count);
                    AccumulateTorchRailNeighbor(x + 1, y, columns, rows, ref sum, ref count);
                    AccumulateTorchRailNeighbor(x, y - 1, columns, rows, ref sum, ref count);
                    AccumulateTorchRailNeighbor(x, y + 1, columns, rows, ref sum, ref count);

                    if (count <= 0)
                    {
                        continue;
                    }

                    torchRailDepths[index] = sum / count;
                    torchRailSampleValid[index] = true;
                    changed = true;
                }
            }

            if (!changed)
            {
                break;
            }
        }

        for (int i = 0; i < torchRailDepths.Length; i++)
        {
            if (!torchRailSampleValid[i])
            {
                torchRailDepths[i] = fallbackDepth;
                torchRailSampleValid[i] = true;
            }
        }
    }

    private void AccumulateTorchRailNeighbor(int x, int y, int columns, int rows, ref float sum, ref int count)
    {
        if (x < 0 || y < 0 || x >= columns || y >= rows)
        {
            return;
        }

        int index = ResolveTorchRailIndex(x, y, columns);
        if (!torchRailSampleValid[index])
        {
            return;
        }

        sum += torchRailDepths[index];
        count++;
    }

    private Vector3 ResolveTorchDesiredPosition(Camera cam, Vector2 viewportPosition, Vector3 direction, out Vector3 aimPoint)
    {
        if (useTorchBoundsPlane && hasTorchDecorBounds)
        {
            return ResolveTorchBoundsPlanePosition(cam, viewportPosition, direction, out aimPoint);
        }

        float projectionDistance = ResolveTorchProjectionDistance(cam);
        Vector3 origin = cam.ViewportToWorldPoint(new Vector3(viewportPosition.x, viewportPosition.y, projectionDistance));

        if (useTorchProjectionRail && torchRailReady && torchRailDepths != null && torchRailDepths.Length > 0)
        {
            float railDepth = SampleTorchRailDepth(viewportPosition);
            float standOff = Mathf.Max(0f, torchRailSurfaceStandOff) + Mathf.Max(0f, torchSurfaceOffset);
            float lightDepth = Mathf.Max(0f, railDepth - standOff - Mathf.Max(0f, torchCollisionRadius));
            aimPoint = origin + direction * railDepth;
            return origin + direction * lightDepth;
        }

        aimPoint = origin + direction * ResolveTorchAimExtraDistance();
        if (!keepTorchOutsideGeometry)
        {
            return origin;
        }

        return ResolveTorchBlockedPosition(new Ray(origin, direction), origin, 0f);
    }

    private Vector3 ResolveTorchBoundsPlanePosition(Camera cam, Vector2 viewportPosition, Vector3 direction, out Vector3 aimPoint)
    {
        Vector2 clampedViewport = ClampTorchViewportToDecorBounds(viewportPosition);
        float planeDistance = ResolveTorchBoundsPlaneDistance(cam);
        Vector3 position = ResolvePointOnCameraParallelPlane(cam, clampedViewport, planeDistance, direction);

        float aimDistance = Mathf.Min(
            Mathf.Max(planeDistance, torchDecorCameraBounds.max.z + Mathf.Max(0f, torchBoundsPadding)),
            planeDistance + ResolveTorchAimExtraDistance());
        if (aimDistance <= planeDistance + 0.001f)
        {
            aimDistance = planeDistance + ResolveTorchAimExtraDistance();
        }

        aimPoint = ResolvePointOnCameraParallelPlane(cam, clampedViewport, aimDistance, direction);
        return position;
    }

    private Vector2 ClampTorchViewportToDecorBounds(Vector2 viewportPosition)
    {
        if (!clampTorchToDecorViewportBounds || !hasTorchDecorBounds)
        {
            return viewportPosition;
        }

        float padding = Mathf.Clamp(torchViewportBoundsPadding, 0f, 0.25f);
        float minX = Mathf.Clamp01(torchDecorViewportBounds.xMin - padding);
        float maxX = Mathf.Clamp01(torchDecorViewportBounds.xMax + padding);
        float minY = Mathf.Clamp01(torchDecorViewportBounds.yMin - padding);
        float maxY = Mathf.Clamp01(torchDecorViewportBounds.yMax + padding);

        if (minX > maxX || minY > maxY)
        {
            return viewportPosition;
        }

        return new Vector2(
            Mathf.Clamp(viewportPosition.x, minX, maxX),
            Mathf.Clamp(viewportPosition.y, minY, maxY));
    }

    private float ResolveTorchBoundsPlaneDistance(Camera cam)
    {
        float nearPlane = cam != null ? cam.nearClipPlane + 0.001f : 0.001f;
        float padding = Mathf.Max(0f, torchBoundsPadding);
        float minDecorDepth = Mathf.Max(nearPlane, torchDecorCameraBounds.min.z - padding);
        float maxDecorDepth = Mathf.Max(minDecorDepth, torchDecorCameraBounds.max.z + padding);
        float standOff = Mathf.Max(0f, torchRailSurfaceStandOff) +
            Mathf.Max(0f, torchSurfaceOffset) +
            Mathf.Max(0f, torchCollisionRadius);

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

    private float ResolveTorchProjectionDistance(Camera cam)
    {
        float nearPlane = cam != null ? cam.nearClipPlane : 0.01f;
        return Mathf.Max(nearPlane + 0.001f, torchCameraOffset);
    }

    private float ResolveTorchAimExtraDistance()
    {
        if (torchLight == null)
        {
            return 0.5f;
        }

        return Mathf.Max(0.1f, torchLight.range);
    }

    private float SampleTorchRailDepth(Vector2 viewportPosition)
    {
        int columns = Mathf.Max(1, builtTorchRailColumns);
        int rows = Mathf.Max(1, builtTorchRailRows);
        if (torchRailDepths == null || torchRailDepths.Length < columns * rows)
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

        float d00 = torchRailDepths[ResolveTorchRailIndex(x0, y0, columns)];
        float d10 = torchRailDepths[ResolveTorchRailIndex(x1, y0, columns)];
        float d01 = torchRailDepths[ResolveTorchRailIndex(x0, y1, columns)];
        float d11 = torchRailDepths[ResolveTorchRailIndex(x1, y1, columns)];

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

    private static int ResolveTorchRailIndex(int x, int y, int columns)
    {
        return y * columns + x;
    }

    private Vector3 ResolveTorchBlockedPosition(Ray ray, Vector3 desiredPosition, float distanceFromCamera)
    {
        Vector3 resolvedPosition = desiredPosition;
        float radius = Mathf.Max(0f, torchCollisionRadius);
        float surfaceOffset = Mathf.Max(0f, torchSurfaceOffset);
        float castDistance = distanceFromCamera + surfaceOffset + radius;

        if (castDistance > 0.0001f && TryFindTorchObstacle(ray, castDistance, radius, out RaycastHit hit))
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

        return ResolveTorchOverlaps(resolvedPosition, ray.direction);
    }

    private Vector3 ResolveTorchClearance(Vector3 position, Vector3 fallbackDirection)
    {
        Vector3 resolvedPosition = ResolveTorchOverlaps(position, fallbackDirection);
        if (keepTorchOutsideRendererBounds)
        {
            resolvedPosition = ResolveTorchRendererBoundsOverlaps(resolvedPosition);
            resolvedPosition = ResolveTorchOverlaps(resolvedPosition, fallbackDirection);
        }

        return resolvedPosition;
    }

    private Vector3 SmoothTorchPosition(Vector3 desiredPosition, float deltaTime)
    {
        if (!hasTorchPose || torchPositionSharpness <= 0f || deltaTime <= 0f)
        {
            return desiredPosition;
        }

        float t = 1f - Mathf.Exp(-torchPositionSharpness * deltaTime);
        return Vector3.Lerp(currentTorchPosition, desiredPosition, t);
    }

    private Quaternion SmoothTorchRotation(Quaternion desiredRotation, float deltaTime)
    {
        if (!hasTorchPose || torchRotationSharpness <= 0f || deltaTime <= 0f)
        {
            return desiredRotation;
        }

        float t = 1f - Mathf.Exp(-torchRotationSharpness * deltaTime);
        return Quaternion.Slerp(currentTorchRotation, desiredRotation, t);
    }

    private static Quaternion ResolveTorchRotation(Vector3 position, Vector3 aimPoint, Vector3 fallbackDirection, Vector3 up)
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

    private bool TryFindTorchObstacle(Ray ray, float castDistance, float radius, out RaycastHit hit)
    {
        if (radius > 0.0001f)
        {
            return Physics.SphereCast(
                ray,
                radius,
                out hit,
                castDistance,
                torchCollisionLayers,
                torchCollisionTriggers) &&
                !IsTorchCollider(hit.collider);
        }

        return Physics.Raycast(
            ray,
            out hit,
            castDistance,
            torchCollisionLayers,
            torchCollisionTriggers) &&
            !IsTorchCollider(hit.collider);
    }

    private Vector3 ResolveTorchOverlaps(Vector3 position, Vector3 fallbackDirection)
    {
        float radius = Mathf.Max(0f, torchCollisionRadius);
        if (radius <= 0.0001f)
        {
            return position;
        }

        Vector3 resolvedPosition = position;
        float clearance = radius + Mathf.Max(0f, torchSurfaceOffset);
        float clearanceSqr = clearance * clearance;
        int iterations = Mathf.Clamp(torchCollisionIterations, 1, 6);

        for (int iteration = 0; iteration < iterations; iteration++)
        {
            int hitCount = Physics.OverlapSphereNonAlloc(
                resolvedPosition,
                clearance,
                torchOverlapBuffer,
                torchCollisionLayers,
                torchCollisionTriggers);

            bool adjusted = false;
            int usableHitCount = Mathf.Min(hitCount, torchOverlapBuffer.Length);
            for (int i = 0; i < usableHitCount; i++)
            {
                Collider hitCollider = torchOverlapBuffer[i];
                if (hitCollider == null || IsTorchCollider(hitCollider))
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

    private Vector3 ResolveTorchRendererBoundsOverlaps(Vector3 position)
    {
        if (torchActiveRendererBounds.Count == 0)
        {
            return position;
        }

        Vector3 resolvedPosition = position;
        float clearance = Mathf.Max(0f, torchCollisionRadius) + Mathf.Max(0f, torchSurfaceOffset);
        int iterations = Mathf.Clamp(torchCollisionIterations, 1, 6);

        for (int iteration = 0; iteration < iterations; iteration++)
        {
            bool adjusted = false;
            for (int i = 0; i < torchActiveRendererBounds.Count; i++)
            {
                Bounds solidBounds = torchActiveRendererBounds[i];
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

    private bool IsTorchCollider(Collider candidate)
    {
        return torchLight != null &&
            candidate != null &&
            candidate.transform.IsChildOf(torchLight.transform);
    }

    private bool IsTorchRenderer(Renderer candidate)
    {
        return torchLight != null &&
            candidate != null &&
            candidate.transform.IsChildOf(torchLight.transform);
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
        torchCameraOffset = Mathf.Max(0f, torchCameraOffset);
        worldRayDistance = Mathf.Max(0.1f, worldRayDistance);
        torchCollisionRadius = Mathf.Max(0f, torchCollisionRadius);
        torchSurfaceOffset = Mathf.Max(0f, torchSurfaceOffset);
        torchCollisionIterations = Mathf.Clamp(torchCollisionIterations, 1, 6);
        torchViewportBoundsPadding = Mathf.Clamp(torchViewportBoundsPadding, 0f, 0.25f);
        torchBoundsPadding = Mathf.Max(0f, torchBoundsPadding);
        torchBoundsRefreshInterval = Mathf.Max(0.05f, torchBoundsRefreshInterval);
        torchRailColumns = Mathf.Clamp(torchRailColumns, 4, 96);
        torchRailRows = Mathf.Clamp(torchRailRows, 4, 96);
        torchRailSurfaceStandOff = Mathf.Max(0f, torchRailSurfaceStandOff);
        torchRailRayDistance = Mathf.Max(0.1f, torchRailRayDistance);
        torchPositionSharpness = Mathf.Max(0f, torchPositionSharpness);
        torchRotationSharpness = Mathf.Max(0f, torchRotationSharpness);
    }
}
