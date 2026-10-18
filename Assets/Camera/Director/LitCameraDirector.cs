using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;
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
    private int cameraSession;
    private float timelineExitBlendSeconds;
    private CameraPose blendSource, rawRenderPose, lastDisplayedPose;
    private bool renderPoseOverridden, hasDisplayedPose;
    private double blendStartedAt;
    private float outputBlendSeconds;
    private bool renderingSubscribed;
    private Transform chargeActor;
    private int chargeFramingGeneration;
    private bool chargeFramingActive;
    private float chargeFramingWeight, chargeFovReduction, chargeBehindOffset, chargeBlendSeconds;
    private double chargeLastRenderAt;

    public bool IsChargeFramingActive => chargeFramingActive;

    public int BeginChargeFraming(Transform actor, float fovReduction, float behindOffset, float blendSeconds)
    {
        ResolveDependencies();
        if (actor == null || controlledCamera == null || hasUccState) return 0;
        chargeActor = actor;
        chargeFramingActive = true;
        chargeFovReduction = Mathf.Clamp(fovReduction, 0f, 30f);
        chargeBehindOffset = Mathf.Max(0f, behindOffset);
        chargeBlendSeconds = Mathf.Max(.01f, blendSeconds);
        chargeLastRenderAt = Time.realtimeSinceStartupAsDouble;
        SubscribeRendering();
        return ++chargeFramingGeneration;
    }

    public void EndChargeFraming(int generation)
    {
        if (generation != 0 && generation == chargeFramingGeneration) chargeFramingActive = false;
    }

    private struct CameraPose
    {
        public Vector3 position;
        public Quaternion rotation;
        public float fov;
        public static CameraPose Capture(Camera camera) => new CameraPose
        { position = camera.transform.position, rotation = camera.transform.rotation, fov = camera.fieldOfView };
        public void Apply(Camera camera)
        {
            camera.transform.SetPositionAndRotation(position, rotation);
            camera.fieldOfView = fov;
        }
    }

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
        UnityEngine.SceneManagement.SceneManager.sceneUnloaded -= OnSceneUnloaded;
        cameraSession++;
        chargeFramingActive = false;
        chargeFramingWeight = 0f;
        CancelOutputBlend();
        ReleaseCinemachine();
        UnsubscribeRendering();
    }

    private void OnEnable() => UnityEngine.SceneManagement.SceneManager.sceneUnloaded += OnSceneUnloaded;
    private void OnSceneUnloaded(UnityEngine.SceneManagement.Scene scene)
    { chargeFramingActive = false; chargeFramingWeight = 0f; CancelOutputBlend(); }

    private void OnDestroy()
    {
        UnsubscribeRendering();
        UnityEngine.SceneManagement.SceneManager.sceneUnloaded -= OnSceneUnloaded;
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
        => BeginTimelineCinemachineControl(0f, 0f, out _);

    public bool BeginTimelineCinemachineControl(float entrySeconds, float exitSeconds, out int generation)
    {
        generation = cameraSession;
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

        StartOutputBlend(entrySeconds);
        timelineExitBlendSeconds = Mathf.Max(0f, exitSeconds);
        generation = ++cameraSession;
        SuspendUccCameraControl();
        StoreTimelineBlend();
        cinemachineBrain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
        cinemachineBrain.enabled = true;
        timelineCinemachineControlActive = true;
        return true;
    }

    /// <summary>Returns authority to UCC after a Timeline Cinemachine track ends.</summary>
    public void EndTimelineCinemachineControl()
        => EndTimelineCinemachineControl(cameraSession);

    public void EndTimelineCinemachineControl(int generation)
    {
        if (!timelineCinemachineControlActive || generation != cameraSession)
        {
            return;
        }

        StartOutputBlend(timelineExitBlendSeconds);
        timelineCinemachineControlActive = false;
        ReleaseCinemachine();
    }

    /// <summary>Also used explicitly by the editor preview; no gameplay authority is changed.</summary>
    public void StartOutputBlend(float seconds)
    {
        ResolveDependencies();
        if (controlledCamera == null) return;
        blendSource = hasDisplayedPose ? lastDisplayedPose : CameraPose.Capture(controlledCamera);
        RestoreRenderPose();
        outputBlendSeconds = Mathf.Max(0f, seconds);
        // Start on the first displayed frame, not during a potentially costly pre-roll.
        blendStartedAt = double.NaN;
        if (outputBlendSeconds > 0f) SubscribeRendering();
    }

    public void CancelOutputBlend()
    {
        outputBlendSeconds = 0f;
        RestoreRenderPose();
        hasDisplayedPose = false;
    }

    public void PrepareOutputPreview()
    {
        ResolveDependencies();
        SubscribeRendering();
    }

    public void EndOutputPreview()
    {
        CancelOutputBlend();
        UnsubscribeRendering();
    }

    private void SubscribeRendering()
    {
        if (renderingSubscribed) return;
        renderingSubscribed = true;
        // HDRP snapshots camera matrices before beginCameraRendering.
        // Compose before the entire render context so culling and image agree.
        RenderPipelineManager.beginContextRendering += BeginContextRender;
        RenderPipelineManager.endContextRendering += EndContextRender;
        Camera.onPreCull += BeginBuiltinRender;
        Camera.onPostRender += EndBuiltinRender;
    }

    private void UnsubscribeRendering()
    {
        if (!renderingSubscribed) return;
        renderingSubscribed = false;
        RenderPipelineManager.beginContextRendering -= BeginContextRender;
        RenderPipelineManager.endContextRendering -= EndContextRender;
        Camera.onPreCull -= BeginBuiltinRender;
        Camera.onPostRender -= EndBuiltinRender;
    }

    private void BeginBuiltinRender(Camera camera)
    { if (GraphicsSettings.currentRenderPipeline == null) BeginRender(default, camera); }
    private void EndBuiltinRender(Camera camera)
    { if (GraphicsSettings.currentRenderPipeline == null) EndRender(default, camera); }

    private void BeginContextRender(ScriptableRenderContext context, System.Collections.Generic.List<Camera> cameras)
    { if (cameras.Contains(controlledCamera)) BeginRender(context, controlledCamera); }
    private void EndContextRender(ScriptableRenderContext context, System.Collections.Generic.List<Camera> cameras)
    { if (cameras.Contains(controlledCamera)) EndRender(context, controlledCamera); }

    private void BeginRender(ScriptableRenderContext _, Camera camera)
    {
        if (camera != controlledCamera || renderPoseOverridden) return;
        rawRenderPose = CameraPose.Capture(camera);
        CameraPose destination = rawRenderPose;
        double now = Time.realtimeSinceStartupAsDouble;
        if (chargeActor == null || !chargeActor.gameObject.activeInHierarchy || hasUccState) chargeFramingActive = false;
        chargeFramingWeight = Mathf.MoveTowards(chargeFramingWeight, chargeFramingActive ? 1f : 0f,
            (float)(now - chargeLastRenderAt) / Mathf.Max(.01f, chargeBlendSeconds));
        chargeLastRenderAt = now;
        float chargeWeight = Mathf.SmoothStep(0f, 1f, chargeFramingWeight);
        if (chargeActor != null && chargeWeight > 0f && !hasUccState)
        {
            Vector3 offset = -chargeActor.forward * (chargeBehindOffset * chargeWeight);
            if (offset.sqrMagnitude > .0001f && Physics.SphereCast(destination.position, .15f, offset.normalized,
                out RaycastHit obstruction, offset.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                offset = offset.normalized * Mathf.Max(0f, obstruction.distance - .05f);
            destination.position += offset;
            destination.fov = Mathf.Clamp(destination.fov - chargeFovReduction * chargeWeight, 20f, 100f);
        }
        if (double.IsNaN(blendStartedAt)) blendStartedAt = Time.realtimeSinceStartupAsDouble;
        float elapsed01 = outputBlendSeconds > 0f
            ? Mathf.Clamp01((float)(Time.realtimeSinceStartupAsDouble - blendStartedAt) / outputBlendSeconds) : 1f;
        float weight = Mathf.SmoothStep(0f, 1f, elapsed01);
        lastDisplayedPose = new CameraPose
        {
            position = Vector3.Lerp(blendSource.position, destination.position, weight),
            rotation = Quaternion.Slerp(blendSource.rotation, destination.rotation, weight),
            fov = Mathf.Lerp(blendSource.fov, destination.fov, weight)
        };
        hasDisplayedPose = true;
        if (elapsed01 < 1f || chargeWeight > 0f)
        {
            lastDisplayedPose.Apply(camera);
            renderPoseOverridden = true;
        }
        else outputBlendSeconds = 0f;
    }

    private void EndRender(ScriptableRenderContext _, Camera camera)
    { if (camera == controlledCamera) RestoreRenderPose(); }

    private void RestoreRenderPose()
    {
        if (!renderPoseOverridden) return;
        if (controlledCamera != null) rawRenderPose.Apply(controlledCamera);
        renderPoseOverridden = false;
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

        uccCameraController?.GetComponent<LitGameplayCameraModeController>()?.EndExternalControl();

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
        chargeFramingActive = false;
        chargeFramingWeight = 0f;
        if (uccCameraController != null)
            uccCameraController.GetComponent<LitGameplayCameraModeController>()?.BeginExternalControl();
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
