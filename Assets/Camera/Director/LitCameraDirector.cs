using Unity.Cinemachine;
using UnityEngine;
using UccCameraController = Opsive.UltimateCharacterController.Camera.CameraController;
using UccCameraControllerHandler = Opsive.UltimateCharacterController.Camera.CameraControllerHandler;

/// <summary>
/// The sole authority switch for the rendered gameplay camera.
/// UCC drives exploration and combat lock; Cinemachine only drives explicitly
/// requested scene shots. Both systems are never enabled as camera drivers together.
/// </summary>
[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
public sealed class LitCameraDirector : MonoBehaviour
{
    public static LitCameraDirector Instance { get; private set; }

    [SerializeField] private Camera controlledCamera;
    [SerializeField] private UccCameraController uccCameraController;
    [SerializeField] private UccCameraControllerHandler uccCameraHandler;
    [SerializeField] private LitUccCameraCharacterBinder uccCameraBinder;
    [SerializeField] private CinemachineBrain cinemachineBrain;
    [SerializeField, Min(0f)] private float defaultBlendSeconds = 0.35f;
    [SerializeField] private int activeCinemachinePriority = 1000;

    private CinemachineCamera activeCinemachineCamera;
    private PrioritySettings activeCameraOriginalPriority;
    private bool activeCameraPriorityStored;
    private bool uccControllerWasEnabled;
    private bool uccHandlerWasEnabled;
    private bool hasUccState;
    private bool timelineCinemachineControlActive;
    private CinemachineBlendDefinition timelineOriginalBlend;
    private bool timelineBlendStored;

    public bool IsCinemachineDriving => activeCinemachineCamera != null && cinemachineBrain != null && cinemachineBrain.enabled;
    public Camera ControlledCamera => controlledCamera;
    public CinemachineBrain CinemachineBrain => cinemachineBrain;
    public CinemachineCamera ActiveCinemachineCamera => activeCinemachineCamera;

    public static LitCameraDirector EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return null;
        }

        LitCameraDirector director = mainCamera.GetComponent<LitCameraDirector>();
        return director != null ? director : mainCamera.gameObject.AddComponent<LitCameraDirector>();
    }

    /// <summary>Returns the director attached to this exact gameplay camera.</summary>
    public static LitCameraDirector EnsureInstance(Camera camera)
    {
        if (camera == null) return null;
        LitCameraDirector director = camera.GetComponent<LitCameraDirector>();
        return director != null ? director : camera.gameObject.AddComponent<LitCameraDirector>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        ResolveDependencies();
        // UCC is the default owner. A Brain left enabled in a scene would otherwise
        // overwrite UCC during LateUpdate even when no cinematic shot is requested.
        if (cinemachineBrain != null)
        {
            cinemachineBrain.enabled = false;
        }
    }

    private void OnDisable()
    {
        ReleaseCinemachine();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>Lets a Cinemachine scene shot become the only camera driver.</summary>
    public bool ActivateCinemachine(CinemachineCamera camera, float blendSeconds = -1f)
    {
        if (camera == null)
        {
            return false;
        }

        ResolveDependencies();
        if (controlledCamera == null)
        {
            return false;
        }

        EnsureBrain();
        if (cinemachineBrain == null)
        {
            return false;
        }

        SuspendUccCameraControl();

        float duration = blendSeconds >= 0f ? blendSeconds : defaultBlendSeconds;
        cinemachineBrain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, duration);
        cinemachineBrain.enabled = true;

        if (activeCinemachineCamera != null && activeCinemachineCamera != camera)
        {
            RestoreActiveCameraPriority();
        }

        activeCinemachineCamera = camera;
        activeCameraOriginalPriority = camera.Priority;
        activeCameraPriorityStored = true;
        PrioritySettings priority = camera.Priority;
        priority.Value = activeCinemachinePriority;
        camera.Priority = priority;
        camera.Prioritize();
        return true;
    }

    /// <summary>
    /// Gives authority to a Timeline Cinemachine track. The track itself keeps
    /// ownership of virtual-camera selection and blending.
    /// </summary>
    public bool BeginTimelineCinemachineControl()
    {
        ResolveDependencies();
        if (controlledCamera == null)
        {
            return false;
        }

        EnsureBrain();
        if (cinemachineBrain == null)
        {
            return false;
        }

        SuspendUccCameraControl();
        StoreTimelineBlend();
        cinemachineBrain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
        cinemachineBrain.enabled = true;
        timelineCinemachineControlActive = true;
        return true;
    }

    /// <summary>Returns authority to UCC after a Timeline Cinemachine track ends.</summary>
    public void EndTimelineCinemachineControl()
    {
        if (!timelineCinemachineControlActive)
        {
            return;
        }

        timelineCinemachineControlActive = false;
        ReleaseCinemachine();
    }

    /// <summary>Returns authority to UCC and rebinds it to the local character.</summary>
    public void ReleaseCinemachine()
    {
        if (!hasUccState && activeCinemachineCamera == null && !timelineBlendStored)
        {
            return;
        }

        if (cinemachineBrain != null)
        {
            cinemachineBrain.enabled = false;
        }

        RestoreTimelineBlend();

        RestoreActiveCameraPriority();

        if (uccCameraBinder != null)
        {
            uccCameraBinder.EndExternalCameraControl(uccControllerWasEnabled);
        }
        else if (uccCameraController != null)
        {
            uccCameraController.enabled = uccControllerWasEnabled;
        }

        if (uccCameraHandler != null)
        {
            uccCameraHandler.enabled = uccHandlerWasEnabled;
        }

        if (uccCameraController != null && uccCameraController.enabled && uccCameraController.Character != null)
        {
            LitSmoothUccCameraViewAdapter smoothAdapter = uccCameraController.GetComponent<LitSmoothUccCameraViewAdapter>();
            if (smoothAdapter != null)
            {
                smoothAdapter.RequestImmediatePose(CameraSnapReason.ExternalCameraReturn);
            }
            else
            {
                uccCameraController.PositionImmediately(true);
            }
        }

        activeCinemachineCamera = null;
        hasUccState = false;
        timelineCinemachineControlActive = false;
    }

    private void StoreTimelineBlend()
    {
        if (timelineBlendStored || cinemachineBrain == null) return;
        timelineOriginalBlend = cinemachineBrain.DefaultBlend;
        timelineBlendStored = true;
    }

    private void RestoreTimelineBlend()
    {
        if (!timelineBlendStored || cinemachineBrain == null) return;
        cinemachineBrain.DefaultBlend = timelineOriginalBlend;
        timelineBlendStored = false;
    }

    private void RestoreActiveCameraPriority()
    {
        if (activeCinemachineCamera != null && activeCameraPriorityStored)
        {
            activeCinemachineCamera.Priority = activeCameraOriginalPriority;
        }

        activeCameraPriorityStored = false;
    }

    private void SuspendUccCameraControl()
    {
        if (!hasUccState)
        {
            uccControllerWasEnabled = uccCameraController != null && uccCameraController.enabled;
            uccHandlerWasEnabled = uccCameraHandler != null && uccCameraHandler.enabled;
            hasUccState = true;
        }

        if (uccCameraHandler != null)
        {
            uccCameraHandler.enabled = false;
        }

        if (uccCameraBinder != null)
        {
            uccCameraBinder.BeginExternalCameraControl();
        }
        else if (uccCameraController != null)
        {
            uccCameraController.enabled = false;
        }
    }

    private void ResolveDependencies()
    {
        if (controlledCamera == null)
        {
            controlledCamera = GetComponent<Camera>();
            if (controlledCamera == null)
            {
                controlledCamera = Camera.main;
            }
        }

        if (controlledCamera == null)
        {
            return;
        }

        if (uccCameraController == null)
        {
            uccCameraController = controlledCamera.GetComponent<UccCameraController>();
        }
        if (uccCameraHandler == null && uccCameraController != null)
        {
            uccCameraHandler = uccCameraController.GetComponent<UccCameraControllerHandler>();
        }
        if (uccCameraBinder == null && uccCameraController != null)
        {
            uccCameraBinder = uccCameraController.GetComponent<LitUccCameraCharacterBinder>();
        }
        if (cinemachineBrain == null)
        {
            cinemachineBrain = controlledCamera.GetComponent<CinemachineBrain>();
        }
    }

    private void EnsureBrain()
    {
        if (cinemachineBrain == null && controlledCamera != null)
        {
            cinemachineBrain = controlledCamera.gameObject.AddComponent<CinemachineBrain>();
            cinemachineBrain.enabled = false;
        }
    }
}
