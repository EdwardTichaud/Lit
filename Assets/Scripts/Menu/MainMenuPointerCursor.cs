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

    [Header("System Cursor")]
    [SerializeField] private bool hideSystemCursor = true;

    private readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();
    private PointerEventData pointerEventData;
    private EventSystem pointerEventSystem;
    private Vector2 screenPosition;
    private bool hasScreenPosition;
    private PointerSource activeSource = PointerSource.Mouse;
    private bool cachedCursorVisible;
    private CursorLockMode cachedCursorLockMode;
    private GameObject syntheticUiHover;
    private CursorIntercation worldHover;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        CacheAndApplySystemCursor();
        InitializeScreenPosition();
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

        Ray ray = cam.ScreenPointToRay(screenPosition);
        Vector3 direction = ray.direction.sqrMagnitude > 0.0001f ? ray.direction.normalized : cam.transform.forward;
        torchLight.transform.position = cam.transform.position + direction * Mathf.Max(0f, torchCameraOffset);
        torchLight.transform.rotation = Quaternion.LookRotation(direction, cam.transform.up);
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
}
