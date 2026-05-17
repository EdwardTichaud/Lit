using UnityEngine;

[System.Serializable]
public class CrpgCameraInput
{
    [System.Serializable]
    public struct FrameState
    {
        public Vector2 panAxes;
        public Vector2 panDragDelta;
        public Vector2 orbitDelta;
        public float zoomDelta;
        public bool recenterRequested;
        public bool toggleFreeCameraRequested;
    }

    [Header("Keyboard Pan")]
    [SerializeField] private float keyboardPanWeight = 1f;
    [SerializeField] private bool enableEdgeScrolling = true;
    [SerializeField] private float edgeScrollMargin = 20f;
    [SerializeField] private float edgeScrollWeight = 1f;

    [Header("Mouse")]
    [SerializeField] private float mouseOrbitSensitivity = 0.12f;
    [SerializeField] private float mousePanDragSensitivity = 1f;
    [SerializeField] private float mouseScrollZoomSensitivity = 0.0015f;

    [Header("Gamepad")]
    [SerializeField] private float gamepadPanWeight = 1f;
    [SerializeField] private float gamepadOrbitSensitivity = 150f;
    [SerializeField] private float gamepadPitchSensitivity = 150f;
    [SerializeField] private bool gamepadStickUpRaisesCamera = true;
    [SerializeField] private float gamepadZoomSensitivity = 0.9f;

    private bool recenterRequested;
    private bool toggleFreeCameraRequested;

    public void Bind()
    {
        LocalInputRouter.CameraRecenter -= OnCameraRecenter;
        LocalInputRouter.CameraRecenter += OnCameraRecenter;
        LocalInputRouter.CameraToggleFreeMode -= OnToggleFreeCamera;
        LocalInputRouter.CameraToggleFreeMode += OnToggleFreeCamera;
    }

    public void Unbind()
    {
        LocalInputRouter.CameraRecenter -= OnCameraRecenter;
        LocalInputRouter.CameraToggleFreeMode -= OnToggleFreeCamera;
        recenterRequested = false;
        toggleFreeCameraRequested = false;
    }

    public void Validate()
    {
        keyboardPanWeight = Mathf.Max(0f, keyboardPanWeight);
        edgeScrollMargin = Mathf.Max(0f, edgeScrollMargin);
        edgeScrollWeight = Mathf.Max(0f, edgeScrollWeight);
        mouseOrbitSensitivity = Mathf.Max(0f, mouseOrbitSensitivity);
        mousePanDragSensitivity = Mathf.Max(0f, mousePanDragSensitivity);
        mouseScrollZoomSensitivity = Mathf.Max(0f, mouseScrollZoomSensitivity);
        gamepadPanWeight = Mathf.Max(0f, gamepadPanWeight);
        gamepadOrbitSensitivity = Mathf.Max(0f, gamepadOrbitSensitivity);
        gamepadPitchSensitivity = Mathf.Max(0f, gamepadPitchSensitivity);
        gamepadZoomSensitivity = Mathf.Max(0f, gamepadZoomSensitivity);
    }

    public FrameState Collect(bool inputBlocked, float deltaTime)
    {
        FrameState state = default;

        if (inputBlocked)
        {
            recenterRequested = false;
            toggleFreeCameraRequested = false;
            return state;
        }

        if (MainMenuInputSettings.AllowsKeyboardMouse())
        {
            state.panAxes += LocalInputRouter.CameraPanValue * keyboardPanWeight;

            if (enableEdgeScrolling && !LocalInputRouter.CameraPanModifierPressed && !LocalInputRouter.CameraOrbitModifierPressed)
            {
                state.panAxes += ResolveEdgeScroll(LocalInputRouter.CameraPointerPosition) * edgeScrollWeight;
            }

            Vector2 pointerDelta = LocalInputRouter.ConsumeCameraPointerDelta();
            if (LocalInputRouter.CameraPanModifierPressed)
            {
                state.panDragDelta += pointerDelta * mousePanDragSensitivity;
            }

            if (LocalInputRouter.CameraOrbitModifierPressed)
            {
                state.orbitDelta += new Vector2(pointerDelta.x, -pointerDelta.y) * mouseOrbitSensitivity;
            }

            state.zoomDelta -= LocalInputRouter.ConsumeCameraPointerScrollValue() * mouseScrollZoomSensitivity;
        }

        if (MainMenuInputSettings.AllowsGamepad())
        {
            state.panAxes += LocalInputRouter.CameraPanValue * gamepadPanWeight;
            Vector2 orbitInput = LocalInputRouter.CameraOrbitValue;
            float pitchDirection = gamepadStickUpRaisesCamera ? -1f : 1f;
            state.orbitDelta += new Vector2(
                orbitInput.x * gamepadOrbitSensitivity * deltaTime,
                orbitInput.y * gamepadPitchSensitivity * deltaTime * pitchDirection);
            if (!ShouldSuppressGamepadZoomForFlight())
            {
                state.zoomDelta -= LocalInputRouter.CameraZoomValue * (gamepadZoomSensitivity * deltaTime);
            }
        }

        state.recenterRequested = recenterRequested;
        state.toggleFreeCameraRequested = toggleFreeCameraRequested;
        recenterRequested = false;
        toggleFreeCameraRequested = false;
        return state;
    }

    private static bool ShouldSuppressGamepadZoomForFlight()
    {
        if (Mathf.Abs(LocalInputRouter.FlightVerticalValue) <= 0.05f)
        {
            return false;
        }

        Transform localCharacter = LocalPlayerContext.LocalCharacterRoot;
        if (localCharacter == null)
        {
            return false;
        }

        SquadCharacterController controller = localCharacter.GetComponent<SquadCharacterController>();
        return controller != null && controller.FlightActive;
    }

    private void OnCameraRecenter()
    {
        recenterRequested = true;
    }

    private void OnToggleFreeCamera()
    {
        toggleFreeCameraRequested = true;
    }

    private Vector2 ResolveEdgeScroll(Vector2 pointerPosition)
    {
        if (Screen.width <= 0 || Screen.height <= 0 || pointerPosition == Vector2.zero)
        {
            return Vector2.zero;
        }

        Vector2 edge = Vector2.zero;
        float marginX = Mathf.Max(1f, edgeScrollMargin);
        float marginY = Mathf.Max(1f, edgeScrollMargin);

        if (pointerPosition.x <= marginX)
        {
            edge.x = -1f + Mathf.Clamp01(pointerPosition.x / marginX);
        }
        else if (pointerPosition.x >= Screen.width - marginX)
        {
            edge.x = Mathf.Clamp01((pointerPosition.x - (Screen.width - marginX)) / marginX);
        }

        if (pointerPosition.y <= marginY)
        {
            edge.y = -1f + Mathf.Clamp01(pointerPosition.y / marginY);
        }
        else if (pointerPosition.y >= Screen.height - marginY)
        {
            edge.y = Mathf.Clamp01((pointerPosition.y - (Screen.height - marginY)) / marginY);
        }

        return Vector2.ClampMagnitude(edge, 1f);
    }
}
