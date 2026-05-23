using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.UI;

[DisallowMultipleComponent]
[DefaultExecutionOrder(110)]
public sealed class CameraObstructionVignetteController : MonoBehaviour
{
    [Header("Vignette")]
    [SerializeField] private bool vignetteEnabled = true;
    [SerializeField, Range(0f, 1f)] private float vignetteMaxIntensity = 0.55f;
    [SerializeField, Min(0f)] private float vignetteSmoothSpeed = 7f;
    [SerializeField, Range(0.05f, 0.95f), Tooltip("Taille approximative du centre lisible. HDRP expose surtout la douceur; cette valeur est convertie en smoothness.")]
    private float vignetteCenterSize = 0.48f;
    [SerializeField] private Color vignetteColor = Color.black;
    [SerializeField, Range(0f, 1f)] private float obstacleVignetteThreshold = 0.05f;
    [SerializeField, Tooltip("Centre la zone lisible sur la position ecran du personnage au lieu du centre de l'ecran.")]
    private bool centerOnTarget = true;
    [SerializeField, Min(0f)] private float centerFollowSharpness = 16f;
    [SerializeField, Range(0f, 0.45f), Tooltip("Garde le centre de la vignette dans l'ecran si le personnage approche du bord.")]
    private float viewportCenterPadding = 0.02f;

    [Header("Runtime Volume")]
    [SerializeField] private bool createRuntimeVolume = true;
    [SerializeField] private float volumePriority = 950f;
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Screen Overlay Fallback")]
    [SerializeField, Tooltip("Fallback fiable si le Volume HDRP n'est pas visible sur la camera active.")]
    private bool createScreenOverlayFallback = true;
    [SerializeField] private int overlaySortingOrder = 32760;
    [SerializeField, Range(64, 512)] private int overlayTextureSize = 256;

    private GameObject runtimeVolumeObject;
    private Volume runtimeVolume;
    private VolumeProfile runtimeProfile;
    private Vignette vignette;
    private GameObject overlayObject;
    private CanvasGroup overlayCanvasGroup;
    private RawImage overlayImage;
    private Texture2D overlayTexture;
    private float generatedOverlayCenterSize = -1f;
    private int generatedOverlayTextureSize = -1;
    private float targetWeight;
    private float currentWeight;
    private Vector2 targetViewportCenter = new Vector2(0.5f, 0.5f);
    private Vector2 currentViewportCenter = new Vector2(0.5f, 0.5f);
    private bool viewportCenterInitialized;

    private void OnDisable()
    {
        targetWeight = 0f;
        currentWeight = 0f;
        if (runtimeVolume != null)
        {
            ApplyVignetteSettings();
        }

        if (overlayCanvasGroup != null)
        {
            overlayCanvasGroup.alpha = 0f;
        }
    }

    private void OnDestroy()
    {
        DestroyRuntimeObject(runtimeVolumeObject);
        DestroyRuntimeObject(runtimeProfile);
        DestroyRuntimeObject(overlayObject);
        DestroyRuntimeObject(overlayTexture);
        runtimeVolumeObject = null;
        runtimeVolume = null;
        runtimeProfile = null;
        vignette = null;
        overlayObject = null;
        overlayCanvasGroup = null;
        overlayImage = null;
        overlayTexture = null;
    }

    private void LateUpdate()
    {
        float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        if (deltaTime <= 0f)
        {
            deltaTime = 1f / 60f;
        }

        float t = vignetteSmoothSpeed <= 0f ? 1f : 1f - Mathf.Exp(-vignetteSmoothSpeed * deltaTime);
        currentWeight = Mathf.Lerp(currentWeight, targetWeight, t);
        if (Mathf.Abs(currentWeight - targetWeight) <= 0.001f)
        {
            currentWeight = targetWeight;
        }

        UpdateViewportCenter(deltaTime);
        ApplyVignetteSettings();
    }

    private void OnValidate()
    {
        vignetteMaxIntensity = Mathf.Clamp01(vignetteMaxIntensity);
        vignetteSmoothSpeed = Mathf.Max(0f, vignetteSmoothSpeed);
        vignetteCenterSize = Mathf.Clamp(vignetteCenterSize, 0.05f, 0.95f);
        obstacleVignetteThreshold = Mathf.Clamp01(obstacleVignetteThreshold);
        centerFollowSharpness = Mathf.Max(0f, centerFollowSharpness);
        viewportCenterPadding = Mathf.Clamp(viewportCenterPadding, 0f, 0.45f);
        volumePriority = Mathf.Max(-1000f, volumePriority);
        overlayTextureSize = Mathf.Clamp(overlayTextureSize, 64, 512);
        if (Application.isPlaying)
        {
            ApplyVignetteSettings();
        }
    }

    public void SetObstructionWeight(float obstructionWeight)
    {
        SetObstructionWeight(obstructionWeight, targetViewportCenter);
    }

    public void SetObstructionWeight(float obstructionWeight, Vector2 viewportCenter)
    {
        if (!vignetteEnabled)
        {
            targetWeight = 0f;
            return;
        }

        targetViewportCenter = ClampViewportCenter(viewportCenter);
        float clamped = Mathf.Clamp01(obstructionWeight);
        targetWeight = clamped >= obstacleVignetteThreshold ? clamped : 0f;
    }

    private void UpdateViewportCenter(float deltaTime)
    {
        Vector2 desiredCenter = centerOnTarget ? targetViewportCenter : new Vector2(0.5f, 0.5f);
        if (!viewportCenterInitialized)
        {
            currentViewportCenter = desiredCenter;
            viewportCenterInitialized = true;
            return;
        }

        float t = centerFollowSharpness <= 0f ? 1f : 1f - Mathf.Exp(-centerFollowSharpness * deltaTime);
        currentViewportCenter = Vector2.Lerp(currentViewportCenter, desiredCenter, t);
    }

    public void ClearImmediate()
    {
        targetWeight = 0f;
        currentWeight = 0f;
        if (runtimeVolume != null)
        {
            ApplyVignetteSettings();
        }

        if (overlayCanvasGroup != null)
        {
            overlayCanvasGroup.alpha = 0f;
        }
    }

    private void EnsureRuntimeVolume()
    {
        if (!createRuntimeVolume || runtimeVolume != null && runtimeProfile != null)
        {
            return;
        }

        // Separate volume: weight stays at 0 when unobstructed, so speed/fall camera effects keep control.
        runtimeVolumeObject = new GameObject("Camera Obstruction Vignette Volume");
        runtimeVolumeObject.hideFlags = HideFlags.HideAndDontSave;
        runtimeVolume = runtimeVolumeObject.AddComponent<Volume>();
        runtimeVolume.isGlobal = true;
        runtimeVolume.priority = volumePriority;
        runtimeVolume.weight = 0f;

        runtimeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        runtimeProfile.hideFlags = HideFlags.HideAndDontSave;
        runtimeVolume.sharedProfile = runtimeProfile;

        vignette = runtimeProfile.Add<Vignette>(true);
        vignette.mode.overrideState = true;
        vignette.mode.value = VignetteMode.Procedural;
        vignette.color.overrideState = true;
        vignette.center.overrideState = true;
        vignette.center.value = new Vector2(0.5f, 0.5f);
        vignette.intensity.overrideState = true;
        vignette.smoothness.overrideState = true;
        vignette.roundness.overrideState = true;
        vignette.rounded.overrideState = true;
    }

    private void ApplyVignetteSettings()
    {
        float weight = vignetteEnabled ? Mathf.Clamp01(currentWeight) : 0f;
        ApplyRuntimeVolume(weight);
        ApplyScreenOverlay(weight);
    }

    private void ApplyRuntimeVolume(float weight)
    {
        if (!createRuntimeVolume)
        {
            if (runtimeVolume != null)
            {
                runtimeVolume.weight = 0f;
            }

            return;
        }

        EnsureRuntimeVolume();

        if (runtimeVolume != null)
        {
            runtimeVolume.priority = volumePriority;
            runtimeVolume.weight = weight;
        }

        if (vignette == null)
        {
            return;
        }

        vignette.color.value = vignetteColor;
        vignette.center.value = currentViewportCenter;
        vignette.intensity.value = vignetteMaxIntensity;
        vignette.smoothness.value = Mathf.Clamp01(1f - vignetteCenterSize);
        vignette.roundness.value = 1f;
        vignette.rounded.value = false;
    }

    private void ApplyScreenOverlay(float weight)
    {
        if (!createScreenOverlayFallback)
        {
            if (overlayCanvasGroup != null)
            {
                overlayCanvasGroup.alpha = 0f;
            }

            return;
        }

        EnsureScreenOverlay();
        if (overlayCanvasGroup == null || overlayImage == null)
        {
            return;
        }

        overlayCanvasGroup.alpha = vignetteMaxIntensity * weight;
        overlayImage.color = new Color(vignetteColor.r, vignetteColor.g, vignetteColor.b, vignetteColor.a);
        overlayImage.uvRect = new Rect(0.5f - currentViewportCenter.x, 0.5f - currentViewportCenter.y, 1f, 1f);
    }

    private void EnsureScreenOverlay()
    {
        if (overlayObject != null && overlayCanvasGroup != null && overlayImage != null)
        {
            EnsureOverlayTexture();
            return;
        }

        if (overlayObject == null)
        {
            overlayObject = new GameObject("Camera Obstruction Vignette Overlay");
            overlayObject.hideFlags = HideFlags.HideAndDontSave;

            Canvas canvas = overlayObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = overlaySortingOrder;
        }
        else
        {
            Canvas canvas = overlayObject.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = overlayObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }

            canvas.sortingOrder = overlaySortingOrder;
        }

        if (overlayCanvasGroup == null)
        {
            overlayCanvasGroup = overlayObject.GetComponent<CanvasGroup>();
            if (overlayCanvasGroup == null)
            {
                overlayCanvasGroup = overlayObject.AddComponent<CanvasGroup>();
            }
        }

        overlayCanvasGroup.alpha = 0f;
        overlayCanvasGroup.interactable = false;
        overlayCanvasGroup.blocksRaycasts = false;

        if (overlayImage == null)
        {
            overlayImage = CreateOverlayImage("Vignette");
        }

        EnsureOverlayTexture();
    }

    private RawImage CreateOverlayImage(string imageName)
    {
        GameObject imageObject = new GameObject(imageName);
        imageObject.hideFlags = HideFlags.HideAndDontSave;
        imageObject.transform.SetParent(overlayObject.transform, false);

        RectTransform rect = imageObject.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        RawImage image = imageObject.AddComponent<RawImage>();
        image.raycastTarget = false;
        return image;
    }

    private void EnsureOverlayTexture()
    {
        int size = Mathf.Clamp(overlayTextureSize, 64, 512);
        if (overlayTexture != null &&
            generatedOverlayTextureSize == size &&
            Mathf.Abs(generatedOverlayCenterSize - vignetteCenterSize) <= 0.001f)
        {
            return;
        }

        DestroyRuntimeObject(overlayTexture);
        overlayTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        float innerRadius = Mathf.Clamp01(vignetteCenterSize) * 0.5f;
        float outerRadius = 0.7071f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size - 0.5f;
                float v = (y + 0.5f) / size - 0.5f;
                float distance = Mathf.Sqrt(u * u + v * v);
                float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(innerRadius, outerRadius, distance));
                overlayTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        overlayTexture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
        generatedOverlayTextureSize = size;
        generatedOverlayCenterSize = vignetteCenterSize;

        if (overlayImage != null)
        {
            overlayImage.texture = overlayTexture;
        }
    }

    private Vector2 ClampViewportCenter(Vector2 viewportCenter)
    {
        float padding = Mathf.Clamp(viewportCenterPadding, 0f, 0.45f);
        return new Vector2(
            Mathf.Clamp(viewportCenter.x, padding, 1f - padding),
            Mathf.Clamp(viewportCenter.y, padding, 1f - padding));
    }

    private static void DestroyRuntimeObject(Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }
}
