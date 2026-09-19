using UnityEngine;

/// <summary>
/// Centralizes the Main Menu button VFX through scene-owned UI-world objects.
/// </summary>
[DisallowMultipleComponent]
public sealed class MainMenuGameOptionsHoverVfx : MonoBehaviour
{
    [System.Serializable]
    private struct ActionPlacement
    {
        public MenuCursorAction action;
        [Tooltip("Point de l'action utilise comme ancrage : (0,0) bas-gauche, (0.5,0.5) centre, (1,1) haut-droite.")]
        public Vector2 normalizedAnchor;
        [Tooltip("Decalage final en pixels ecran.")]
        public Vector2 screenOffset;
    }

    [SerializeField] private Canvas menuCanvas;
    [SerializeField] private Camera decorCamera;
    [SerializeField] private Transform uiWorldVfxCameraRoot;
    [SerializeField] private Transform uiWorldVfxRoot;
    [SerializeField, Tooltip("Layer unique rendu par la camera des VFX ecran.")] private LayerMask uiWorldVfxLayer = 1 << 27;
    [SerializeField, Min(0.01f)] private float projectionDistance = 1.5f;
    [SerializeField] private ActionPlacement[] actionPlacements;

    private static MainMenuGameOptionsHoverVfx instance;

    private Camera uiWorldVfxCamera;
    private GameObject activeEffectInstance;
    private ParticleSystem activeEffect;
    private Component hoveredSource;
    private int visualEffectLayer = -1;
    private int decorCameraCullingMask;
    private bool decorCameraMaskCaptured;

    public static void SetHovered(Component source, bool hovered)
    {
        if (instance != null)
        {
            instance.SetHoveredInternal(source, hovered);
        }
    }

    private void Awake()
    {
        instance = this;
        ResolveSceneReferences();
        HideEffect();
    }

    private void OnEnable()
    {
        instance = this;
    }

    private void OnDisable()
    {
        hoveredSource = null;
        HideEffect();
        if (instance == this)
        {
            instance = null;
        }
    }

    private void OnDestroy()
    {
        if (decorCamera != null && decorCameraMaskCaptured)
        {
            decorCamera.cullingMask = decorCameraCullingMask;
        }
    }

    private void OnValidate()
    {
        projectionDistance = Mathf.Max(0.01f, projectionDistance);
    }

    private void LateUpdate()
    {
        if (hoveredSource != null && activeEffectInstance != null && activeEffectInstance.activeSelf)
        {
            UpdateEffectPosition();
        }
    }

    private void SetHoveredInternal(Component source, bool hovered)
    {
        if (source == null || activeEffect == null)
        {
            return;
        }

        if (!hovered)
        {
            if (hoveredSource == source)
            {
                hoveredSource = null;
                HideEffect();
            }

            return;
        }

        hoveredSource = source;
        UpdateEffectPosition();
        activeEffectInstance.SetActive(true);
        activeEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        activeEffect.Play(true);
    }

    private void ResolveSceneReferences()
    {
        ResolveNavigationChildren();
        uiWorldVfxCamera = UIWorldVfxCameraService.Instance != null && UIWorldVfxCameraService.Instance.Camera != null
            ? UIWorldVfxCameraService.Instance.Camera
            : uiWorldVfxCameraRoot != null ? uiWorldVfxCameraRoot.GetComponent<Camera>() : null;
        activeEffectInstance = uiWorldVfxRoot != null ? uiWorldVfxRoot.gameObject : null;
        activeEffect = activeEffectInstance != null
            ? activeEffectInstance.GetComponentInChildren<ParticleSystem>(true)
            : null;

        if (uiWorldVfxCamera == null || activeEffectInstance == null || activeEffect == null)
        {
            Debug.LogError("MainMenu_Navigation requires a UI-world camera and a VFX_UI_Button particle system.", this);
            return;
        }

        int layer = GetVisualEffectLayer();
        SetLayerRecursively(activeEffectInstance, layer);
        uiWorldVfxCamera.cullingMask = 1 << layer;
        ExcludeVisualEffectsFromDecorCamera();
    }

    private void ResolveNavigationChildren()
    {
        if (uiWorldVfxCameraRoot != null && uiWorldVfxRoot != null)
        {
            return;
        }

        GameObject navigation = GameObject.Find("MainMenu_Navigation");
        if (navigation == null)
        {
            return;
        }

        if (uiWorldVfxCameraRoot == null)
        {
            uiWorldVfxCameraRoot = navigation.transform.Find("MainMenu_UIWorldVfxCamera");
        }

        if (uiWorldVfxCameraRoot == null && UIWorldVfxCameraService.Instance != null)
        {
            uiWorldVfxCameraRoot = UIWorldVfxCameraService.Instance.Camera != null
                ? UIWorldVfxCameraService.Instance.Camera.transform
                : null;
        }

        if (uiWorldVfxRoot == null)
        {
            uiWorldVfxRoot = navigation.transform.Find("VFX_UI_Button");
        }
    }

    private void UpdateEffectPosition()
    {
        if (hoveredSource == null || uiWorldVfxCamera == null || activeEffectInstance == null)
        {
            return;
        }

        RectTransform target = hoveredSource.transform as RectTransform;
        if (target == null)
        {
            return;
        }

        ActionPlacement placement = GetPlacement(hoveredSource as MenuCursorAction);
        Camera uiCamera = menuCanvas != null && menuCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? menuCanvas.worldCamera
            : null;
        Vector2 normalizedAnchor = placement.normalizedAnchor == Vector2.zero ? new Vector2(0.5f, 0.5f) : placement.normalizedAnchor;
        Vector2 localAnchor = new Vector2(
            Mathf.Lerp(target.rect.xMin, target.rect.xMax, normalizedAnchor.x),
            Mathf.Lerp(target.rect.yMin, target.rect.yMax, normalizedAnchor.y));
        Vector2 screenPosition = RectTransformUtility.WorldToScreenPoint(uiCamera, target.TransformPoint(localAnchor)) + placement.screenOffset;
        activeEffectInstance.transform.position = uiWorldVfxCamera.ScreenPointToRay(screenPosition).GetPoint(projectionDistance);
    }

    private ActionPlacement GetPlacement(MenuCursorAction source)
    {
        if (actionPlacements != null)
        {
            foreach (ActionPlacement placement in actionPlacements)
            {
                if (placement.action == source)
                {
                    return placement;
                }
            }
        }

        return default;
    }

    private int GetVisualEffectLayer()
    {
        if (visualEffectLayer >= 0)
        {
            return visualEffectLayer;
        }

        int layerMask = uiWorldVfxLayer.value;
        if (layerMask != 0 && (layerMask & (layerMask - 1)) == 0)
        {
            for (int layer = 0; layer < 32; layer++)
            {
                if (layerMask == (1 << layer))
                {
                    visualEffectLayer = layer;
                    return visualEffectLayer;
                }
            }
        }

        Debug.LogError("Select exactly one existing layer in 'UI World Vfx Layer'.", this);
        return visualEffectLayer;
    }

    private void ExcludeVisualEffectsFromDecorCamera()
    {
        if (decorCamera == null || decorCameraMaskCaptured)
        {
            return;
        }

        decorCameraCullingMask = decorCamera.cullingMask;
        decorCamera.cullingMask &= ~(1 << GetVisualEffectLayer());
        decorCameraMaskCaptured = true;
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        root.layer = layer;
        foreach (Transform child in root.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    private void HideEffect()
    {
        if (activeEffect == null || activeEffectInstance == null)
        {
            return;
        }

        activeEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        activeEffectInstance.SetActive(false);
    }
}
