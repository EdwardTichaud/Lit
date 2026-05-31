using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

[DisallowMultipleComponent]
[DefaultExecutionOrder(120)]
public sealed class PlayerVisibilityMaskController : MonoBehaviour
{
    private const string LogPrefix = "[PlayerVisibilityMask]";

    private static readonly int MaskCenterId = Shader.PropertyToID(VisibilityMaskSettings.MaskCenterPropertyName);
    private static readonly int MaskParamsId = Shader.PropertyToID(VisibilityMaskSettings.MaskParamsPropertyName);
    private static readonly int MaskDebugId = Shader.PropertyToID(VisibilityMaskSettings.MaskDebugPropertyName);

    [Header("References")]
    [SerializeField, Tooltip("Camera qui rend le masque. Si vide, utilise CameraController ou Camera.main.")]
    private Camera sourceCamera;
    [SerializeField, Tooltip("Cible joueur optionnelle. Si vide, utilise CameraController ou le personnage local.")]
    private Transform playerTargetTransform;
    [SerializeField] private CameraController cameraController;
    [SerializeField] private CameraLineOfSightObstructionDetector obstacleDetector;

    [Header("Settings")]
    [SerializeField] private VisibilityMaskSettings settings = new VisibilityMaskSettings();

    [Header("HDRP Custom Pass")]
    [SerializeField, Tooltip("Cree et configure un CustomPassVolume HDRP au runtime si aucun volume n'est assigne.")]
    private bool installCustomPassAtRuntime = true;
    [SerializeField, Tooltip("Volume HDRP optionnel a reutiliser. Si vide, un volume global runtime est cree.")]
    private CustomPassVolume customPassVolume;
    [SerializeField, Tooltip("Injection HDRP du composite. AfterOpaqueAndSky garde les murs opaques rendus avant le masque.")]
    private CustomPassInjectionPoint customPassInjectionPoint = CustomPassInjectionPoint.AfterOpaqueAndSky;
    [SerializeField] private float customPassPriority;
    [SerializeField, Tooltip("Override optionnel du shader Hidden/HDRP/PlayerVisibilityMaskComposite.")]
    private Shader customPassCompositeShader;
    [SerializeField, Tooltip("Active un override runtime sur la camera si les Frame Settings HDRP desactivent les Custom Passes.")]
    private bool autoEnableCustomPassFrameSettings = true;

    private float currentActivation;
    private Vector2 currentViewportCenter = new Vector2(0.5f, 0.5f);
    private bool hasValidViewportCenter;
    private bool configured;
    private PlayerVisibilityMaskCustomPass visibilityCustomPass;
    private GameObject runtimeCustomPassVolumeObject;
    private bool addedCustomPassAtRuntime;
    private bool ownsRuntimeCustomPassVolume;
    private bool loggedRuntimeVolumeCreation;
    private bool loggedPassCreation;
    private bool loggedFrameSettingsFallback;
    private bool loggedPipelineCustomPassUnsupported;
    private bool loggedNonHdrpPipeline;
    private bool loggedAddedHDAdditionalCameraData;
    private bool loggedMissingPlayerRenderers;
    private bool loggedFoundPlayerRenderers;
    private Transform lastRendererCheckTarget;
    private int lastRendererCheckLayerMask;
    private readonly System.Collections.Generic.List<Renderer> playerRendererBuffer = new System.Collections.Generic.List<Renderer>(16);

    public VisibilityMaskSettings Settings => settings;
    public bool IsMaskActive => currentActivation > 0.001f;
    public Vector2 CurrentViewportCenter => currentViewportCenter;
    public float CurrentActivation => currentActivation;

    private void Awake()
    {
        ResolveReferences();
        ValidateSettings();
        ApplySettingsToDetector();
        EnsureCustomPass();
    }

    private void OnEnable()
    {
        EnsureCustomPass();
        SetCustomPassEnabled(true);
        PublishMaskGlobals(active: false, currentViewportCenter);
    }

    private void OnDisable()
    {
        currentActivation = 0f;
        PublishMaskGlobals(active: false, currentViewportCenter);
        SetCustomPassEnabled(false);
    }

    private void OnDestroy()
    {
        CleanupRuntimeCustomPass();
    }

    private void LateUpdate()
    {
        ResolveReferences();
        ValidateSettings();
        ApplySettingsToDetector();
        EnsureCustomPass();
        SyncCustomPassSettings();
        ValidatePlayerRenderers(ResolveTargetTransform());
        UpdateMaskState();
    }

    private void OnValidate()
    {
        ValidateSettings();
    }

    public void Configure(Camera camera, CameraController controller, CameraLineOfSightObstructionDetector detector)
    {
        if (sourceCamera == null)
        {
            sourceCamera = camera;
        }

        if (cameraController == null)
        {
            cameraController = controller;
        }

        if (obstacleDetector == null)
        {
            obstacleDetector = detector;
        }

        configured = true;
        ResolveReferences();
        ApplySettingsToDetector();
    }

    private void UpdateMaskState()
    {
        bool hasCenter = TryResolveTargetViewportCenter(out Vector2 targetViewportCenter);
        if (hasCenter)
        {
            currentViewportCenter = settings.ResolveViewportCenter(targetViewportCenter, ResolveCamera());
        }

        if (!hasCenter && settings.ForceMaskVisibleForDebug)
        {
            currentViewportCenter = new Vector2(0.5f, 0.5f);
        }

        hasValidViewportCenter = hasCenter || settings.ForceMaskVisibleForDebug;
        bool obstructed = obstacleDetector != null && obstacleDetector.IsObstructed;
        bool shouldActivate = hasValidViewportCenter && (settings.ForceMaskVisibleForDebug || !settings.OnlyShowWhenObstructed || obstructed);

        float targetActivation = shouldActivate ? settings.Intensity : 0f;
        float deltaTime = Time.unscaledDeltaTime > 0f ? Time.unscaledDeltaTime : 1f / 60f;
        float sharpness = settings.ActivationSharpness;
        float t = sharpness <= 0f ? 1f : 1f - Mathf.Exp(-sharpness * deltaTime);
        currentActivation = Mathf.Lerp(currentActivation, targetActivation, t);
        if (Mathf.Abs(currentActivation - targetActivation) <= 0.001f)
        {
            currentActivation = targetActivation;
        }

        PublishMaskGlobals(currentActivation > 0.001f, currentViewportCenter);
    }

    private bool TryResolveTargetViewportCenter(out Vector2 viewportCenter)
    {
        if (obstacleDetector != null && obstacleDetector.TryGetTargetViewportCenter(out viewportCenter))
        {
            return true;
        }

        viewportCenter = new Vector2(0.5f, 0.5f);
        Camera camera = ResolveCamera();
        Transform target = ResolveTargetTransform();
        if (camera == null || target == null)
        {
            return false;
        }

        Vector3 viewportPoint = camera.WorldToViewportPoint(target.position);
        if (viewportPoint.z <= 0f)
        {
            return false;
        }

        viewportCenter = new Vector2(viewportPoint.x, viewportPoint.y);
        return true;
    }

    private Transform ResolveTargetTransform()
    {
        if (playerTargetTransform != null)
        {
            return playerTargetTransform;
        }

        if (cameraController != null && cameraController.TryGetGameplayTarget(out Transform gameplayTarget))
        {
            return gameplayTarget;
        }

        if (SquadManager.Instance != null && SquadManager.Instance.currentCharacter != null)
        {
            return SquadManager.Instance.currentCharacter.transform;
        }

        return LocalPlayerContext.LocalCharacterRoot;
    }

    private Camera ResolveCamera()
    {
        if (sourceCamera != null)
        {
            return sourceCamera;
        }

        if (cameraController != null && cameraController.MainCamera != null)
        {
            sourceCamera = cameraController.MainCamera;
            return sourceCamera;
        }

        sourceCamera = Camera.main;
        return sourceCamera;
    }

    private void ResolveReferences()
    {
        if (cameraController == null)
        {
            cameraController = GetComponent<CameraController>();
            if (cameraController == null)
            {
                cameraController = GetComponentInParent<CameraController>();
            }
        }

        if (sourceCamera == null && cameraController != null)
        {
            sourceCamera = cameraController.MainCamera;
        }

        if (obstacleDetector == null)
        {
            obstacleDetector = GetComponent<CameraLineOfSightObstructionDetector>();
        }
    }

    private void ApplySettingsToDetector()
    {
        if (obstacleDetector != null)
        {
            obstacleDetector.ApplyVisibilityMaskSettings(settings);
        }
    }

    private void ValidateSettings()
    {
        if (settings == null)
        {
            settings = new VisibilityMaskSettings();
        }

        settings.Validate();
    }

    private void PublishMaskGlobals(bool active, Vector2 viewportCenter)
    {
        float activation = active ? Mathf.Clamp01(currentActivation) : 0f;
        Shader.SetGlobalVector(MaskCenterId, new Vector4(viewportCenter.x, viewportCenter.y, hasValidViewportCenter ? 1f : 0f, 0f));
        Shader.SetGlobalVector(MaskParamsId, new Vector4(settings.MaskRadius, settings.EdgeSoftness, activation, active ? 1f : 0f));
        Shader.SetGlobalVector(MaskDebugId, new Vector4(settings.DebugMode ? 1f : 0f, configured ? 1f : 0f, settings.ForceMaskVisibleForDebug ? 1f : 0f, 0f));
    }

    private void EnsureCustomPass()
    {
        if (!installCustomPassAtRuntime || !Application.isPlaying)
        {
            return;
        }

        CustomPassVolume volume = ResolveCustomPassVolume();
        if (volume == null)
        {
            return;
        }

        volume.isGlobal = true;
        volume.injectionPoint = customPassInjectionPoint;
        volume.priority = customPassPriority;

        Camera camera = ResolveCamera();
        if (camera != null && volume.targetCamera != camera)
        {
            volume.targetCamera = camera;
        }

        EnsureCustomPassFrameSettings(camera);

        if (visibilityCustomPass == null)
        {
            visibilityCustomPass = FindCustomPass(volume);
            if (visibilityCustomPass == null)
            {
                visibilityCustomPass = new PlayerVisibilityMaskCustomPass();
                volume.customPasses.Add(visibilityCustomPass);
                addedCustomPassAtRuntime = true;
                if (!loggedPassCreation)
                {
                    Debug.Log($"{LogPrefix} Custom Pass ajoute au volume '{volume.name}' injection={volume.injectionPoint} targetCamera={(camera != null ? camera.name : "(none)")} playerLayer={DescribeLayerMask(settings.PlayerLayer)}.", this);
                    loggedPassCreation = true;
                }
            }
        }

        visibilityCustomPass.name = "Player Visibility Mask";
        visibilityCustomPass.enabled = isActiveAndEnabled;
        SyncCustomPassSettings();
    }

    private CustomPassVolume ResolveCustomPassVolume()
    {
        if (customPassVolume != null)
        {
            return customPassVolume;
        }

        if (runtimeCustomPassVolumeObject == null)
        {
            runtimeCustomPassVolumeObject = new GameObject("Player Visibility Mask Custom Pass Volume");
            runtimeCustomPassVolumeObject.hideFlags = HideFlags.DontSave;
            runtimeCustomPassVolumeObject.transform.SetParent(transform, false);
            customPassVolume = runtimeCustomPassVolumeObject.AddComponent<CustomPassVolume>();
            ownsRuntimeCustomPassVolume = true;
            if (!loggedRuntimeVolumeCreation)
            {
                Debug.Log($"{LogPrefix} CustomPassVolume runtime cree: '{runtimeCustomPassVolumeObject.name}'.", this);
                loggedRuntimeVolumeCreation = true;
            }
        }
        else
        {
            customPassVolume = runtimeCustomPassVolumeObject.GetComponent<CustomPassVolume>();
        }

        return customPassVolume;
    }

    private void EnsureCustomPassFrameSettings(Camera camera)
    {
        if (camera == null)
        {
            return;
        }

        if (GraphicsSettings.currentRenderPipeline is HDRenderPipelineAsset hdrpAsset)
        {
            if (!hdrpAsset.currentPlatformRenderPipelineSettings.supportCustomPass)
            {
                if (!loggedPipelineCustomPassUnsupported)
                {
                    Debug.LogWarning($"{LogPrefix} Le HDRP Asset actif a supportCustomPass=false. Active 'Custom Pass' dans le HDRP Asset pour que le masque puisse s'executer.", this);
                    loggedPipelineCustomPassUnsupported = true;
                }

                return;
            }
        }
        else if (!loggedNonHdrpPipeline)
        {
            Debug.LogWarning($"{LogPrefix} Le render pipeline actif n'est pas HDRP. Le masque joueur requiert HDRP Custom Pass.", this);
            loggedNonHdrpPipeline = true;
        }

        HDAdditionalCameraData hdCameraData = camera.GetComponent<HDAdditionalCameraData>();
        if (hdCameraData == null)
        {
            if (!autoEnableCustomPassFrameSettings)
            {
                return;
            }

            hdCameraData = camera.gameObject.AddComponent<HDAdditionalCameraData>();
            if (!loggedAddedHDAdditionalCameraData)
            {
                Debug.Log($"{LogPrefix} HDAdditionalCameraData ajoute a la camera '{camera.name}' pour pouvoir forcer les Frame Settings Custom Pass.", this);
                loggedAddedHDAdditionalCameraData = true;
            }
        }

        bool overridesCustomPass = hdCameraData.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)FrameSettingsField.CustomPass];
        bool customPassEnabled = hdCameraData.renderingPathCustomFrameSettings.IsEnabled(FrameSettingsField.CustomPass);
        bool explicitlyDisabled = hdCameraData.customRenderingSettings && overridesCustomPass && !customPassEnabled;
        if (!autoEnableCustomPassFrameSettings)
        {
            if (explicitlyDisabled && !loggedFrameSettingsFallback)
            {
                Debug.LogWarning($"{LogPrefix} Les Frame Settings HDRP de la camera '{camera.name}' desactivent Custom Pass. Active Custom Pass sur la camera ou autoEnableCustomPassFrameSettings.", this);
                loggedFrameSettingsFallback = true;
            }

            return;
        }

        if (hdCameraData.customRenderingSettings && overridesCustomPass && customPassEnabled)
        {
            return;
        }

        hdCameraData.customRenderingSettings = true;
        hdCameraData.renderingPathCustomFrameSettings.SetEnabled(FrameSettingsField.CustomPass, true);
        hdCameraData.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)FrameSettingsField.CustomPass] = true;

        if (!loggedFrameSettingsFallback)
        {
            string reason = explicitlyDisabled ? "override camera desactive" : "fallback runtime";
            Debug.LogWarning($"{LogPrefix} Custom Pass force dans les Frame Settings HDRP de la camera '{camera.name}' ({reason}).", this);
            loggedFrameSettingsFallback = true;
        }
    }

    private void ValidatePlayerRenderers(Transform targetRoot)
    {
        int playerLayerMask = settings != null ? settings.PlayerLayer.value : 0;
        if (targetRoot != lastRendererCheckTarget || playerLayerMask != lastRendererCheckLayerMask)
        {
            lastRendererCheckTarget = targetRoot;
            lastRendererCheckLayerMask = playerLayerMask;
            loggedMissingPlayerRenderers = false;
            loggedFoundPlayerRenderers = false;
        }

        if (targetRoot == null)
        {
            return;
        }

        playerRendererBuffer.Clear();
        targetRoot.GetComponentsInChildren(includeInactive: false, playerRendererBuffer);

        int totalRendererCount = 0;
        int matchingLayerRendererCount = 0;
        for (int i = 0; i < playerRendererBuffer.Count; i++)
        {
            Renderer renderer = playerRendererBuffer[i];
            if (renderer == null)
            {
                continue;
            }

            totalRendererCount++;
            if ((playerLayerMask & (1 << renderer.gameObject.layer)) != 0)
            {
                matchingLayerRendererCount++;
            }
        }

        playerRendererBuffer.Clear();

        if (matchingLayerRendererCount == 0)
        {
            if (!loggedMissingPlayerRenderers)
            {
                Debug.LogWarning($"{LogPrefix} Aucun renderer joueur trouve sous '{targetRoot.name}' pour playerLayer={DescribeLayerMask(settings.PlayerLayer)}. Renderers sous la cible={totalRendererCount}. Verifie que les meshes du joueur sont sur le layer Character/player configure.", this);
                loggedMissingPlayerRenderers = true;
            }

            return;
        }

        if (settings.DebugMode && !loggedFoundPlayerRenderers)
        {
            Debug.Log($"{LogPrefix} Renderers joueur detectes: {matchingLayerRendererCount}/{totalRendererCount} sous '{targetRoot.name}' pour playerLayer={DescribeLayerMask(settings.PlayerLayer)}.", this);
            loggedFoundPlayerRenderers = true;
        }
    }

    private static string DescribeLayerMask(LayerMask layerMask)
    {
        if (layerMask.value == 0)
        {
            return "(none)";
        }

        string description = string.Empty;
        for (int i = 0; i < 32; i++)
        {
            if ((layerMask.value & (1 << i)) == 0)
            {
                continue;
            }

            string layerName = LayerMask.LayerToName(i);
            if (string.IsNullOrEmpty(layerName))
            {
                layerName = $"Layer {i}";
            }

            if (!string.IsNullOrEmpty(description))
            {
                description += ", ";
            }

            description += $"{i}:{layerName}";
        }

        return description;
    }

    private PlayerVisibilityMaskCustomPass FindCustomPass(CustomPassVolume volume)
    {
        for (int i = 0; i < volume.customPasses.Count; i++)
        {
            if (volume.customPasses[i] is PlayerVisibilityMaskCustomPass pass)
            {
                return pass;
            }
        }

        return null;
    }

    private void SyncCustomPassSettings()
    {
        if (visibilityCustomPass == null || settings == null)
        {
            return;
        }

        visibilityCustomPass.Configure(settings.PlayerLayer, settings.DebugMode, customPassCompositeShader);
    }

    private void SetCustomPassEnabled(bool enabled)
    {
        if (visibilityCustomPass != null)
        {
            visibilityCustomPass.enabled = enabled;
        }
    }

    private void CleanupRuntimeCustomPass()
    {
        if (!ownsRuntimeCustomPassVolume && customPassVolume != null && visibilityCustomPass != null && addedCustomPassAtRuntime)
        {
            customPassVolume.customPasses.Remove(visibilityCustomPass);
        }

        visibilityCustomPass = null;
        addedCustomPassAtRuntime = false;

        if (runtimeCustomPassVolumeObject != null)
        {
            if (Application.isPlaying)
            {
                Destroy(runtimeCustomPassVolumeObject);
            }
            else
            {
                DestroyImmediate(runtimeCustomPassVolumeObject);
            }

            runtimeCustomPassVolumeObject = null;
        }

        ownsRuntimeCustomPassVolume = false;
    }

    private void OnDrawGizmosSelected()
    {
        if (settings == null || !settings.DebugMode)
        {
            return;
        }

        Camera camera = ResolveCamera();
        Transform target = ResolveTargetTransform();
        if (camera == null || target == null)
        {
            return;
        }

        Gizmos.color = obstacleDetector != null && obstacleDetector.IsObstructed ? Color.red : Color.green;
        Gizmos.DrawLine(camera.transform.position, target.position);
        Gizmos.DrawWireSphere(target.position, 0.25f);
    }
}
