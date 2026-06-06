using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

[DisallowMultipleComponent]
[DefaultExecutionOrder(1020)]
[RequireComponent(typeof(RectTransform))]
public sealed class XRayMaskFollower : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Transform target;
    [SerializeField] private Canvas canvas;
    [SerializeField] private RectTransform maskRoot;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Settings")]
    [SerializeField, Tooltip("Taille du cercle en unites UI du Canvas. Ne pas modifier Size Delta directement: ce script le pilote.")]
    private float maskSize = 260f;
    [SerializeField, Tooltip("Offset monde applique a la cible avant projection ecran. Utile pour viser le torse ou la tete au lieu du pivot aux pieds.")]
    private Vector3 worldOffset = new(0f, 1.2f, 0f);
    [SerializeField, Tooltip("Offset final en unites UI du Canvas apres projection ecran.")]
    private Vector2 screenOffset = Vector2.zero;
    [SerializeField] private float fadeSpeed = 8f;

    [Header("HDRP Vignette")]
    [SerializeField, Tooltip("Volume global HDRP qui contient la Vignette a piloter pendant le XRay.")]
    private Volume globalVolume;
    [SerializeField] private bool driveVignette = true;
    [SerializeField, Range(0f, 1f)] private float activeVignetteIntensity = 1f;
    [SerializeField, Range(0f, 1f)] private float inactiveVignetteIntensity = 0f;
    [SerializeField, Tooltip("Donne temporairement la priorite au GlobalVolume XRay face aux volumes camera runtime.")]
    private bool raiseVignettePriorityWhileVisible = true;
    [SerializeField] private float activeVignettePriority = 1000f;

    private RectTransform canvasRect;
    private Vignette vignette;
    private Vector2 lastVignetteCenter = new(0.5f, 0.5f);
    private float originalVolumePriority;
    private bool warnedMissingReferences;
    private bool warnedMissingVignette;
    private bool hasOriginalVolumePriority;

    public bool Visible { get; set; }
    public float MaskSize
    {
        get => maskSize;
        set
        {
            maskSize = Mathf.Max(1f, value);
            ApplyMaskSize();
        }
    }

    public Vector3 WorldOffset
    {
        get => worldOffset;
        set => worldOffset = value;
    }

    public Vector2 ScreenOffset
    {
        get => screenOffset;
        set => screenOffset = value;
    }

    private void Reset()
    {
        ResolveReferences();
    }

    private void Awake()
    {
        ResolveReferences();
        HideInstantly();
    }

    private void LateUpdate()
    {
        ResolveReferences();

        if (!HasRequiredReferences())
        {
            ApplyVignette(lastVignetteCenter, 0f);
            return;
        }

        Vector3 screenPosition = mainCamera.WorldToScreenPoint(target.position + worldOffset);
        bool shouldShow = Visible && screenPosition.z > 0f;

        canvasGroup.alpha = Mathf.MoveTowards(
            canvasGroup.alpha,
            shouldShow ? 1f : 0f,
            fadeSpeed * Time.unscaledDeltaTime);

        if (canvasGroup.alpha <= 0.001f && !shouldShow)
        {
            ApplyVignette(lastVignetteCenter, 0f);
            return;
        }

        Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenPosition,
            eventCamera,
            out Vector2 localPoint);

        maskRoot.anchoredPosition = localPoint + screenOffset;
        ApplyMaskSize();
        ApplyVignette(GetMaskViewportCenter(eventCamera), canvasGroup.alpha);
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    public void SetMainCamera(Camera newCamera)
    {
        mainCamera = newCamera;
    }

    public void SetOffsets(Vector3 newWorldOffset, Vector2 newScreenOffset)
    {
        worldOffset = newWorldOffset;
        screenOffset = newScreenOffset;
    }

    public void HideInstantly()
    {
        Visible = false;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }

        ApplyVignette(lastVignetteCenter, 0f);
    }

    private void OnDisable()
    {
        ApplyVignette(lastVignetteCenter, 0f);
    }

    private void ResolveReferences()
    {
        if (maskRoot == null)
        {
            maskRoot = GetComponent<RectTransform>();
        }

        if (canvas == null)
        {
            canvas = GetComponentInParent<Canvas>(true);
        }

        if (canvas != null && canvasRect == null)
        {
            canvasRect = canvas.GetComponent<RectTransform>();
        }

        if (canvasGroup == null && maskRoot != null)
        {
            canvasGroup = maskRoot.GetComponent<CanvasGroup>();
        }

        if (canvasGroup == null && maskRoot != null)
        {
            canvasGroup = maskRoot.gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        ResolveVignette();
    }

    private bool HasRequiredReferences()
    {
        if (mainCamera != null && target != null && canvas != null && canvasRect != null && maskRoot != null && canvasGroup != null)
        {
            warnedMissingReferences = false;
            return true;
        }

        if (!warnedMissingReferences)
        {
            Debug.LogWarning(
                $"[XRay] References de masque manquantes sur {name}. " +
                $"mainCamera={(mainCamera ? mainCamera.name : "null")}, " +
                $"target={(target ? target.name : "null")}, " +
                $"canvas={(canvas ? canvas.name : "null")}, " +
                $"maskRoot={(maskRoot ? maskRoot.name : "null")}, " +
                $"canvasGroup={(canvasGroup ? canvasGroup.name : "null")}.");
            warnedMissingReferences = true;
        }

        return false;
    }

    private void OnValidate()
    {
        maskSize = Mathf.Max(1f, maskSize);
        fadeSpeed = Mathf.Max(0f, fadeSpeed);
        activeVignetteIntensity = Mathf.Clamp01(activeVignetteIntensity);
        inactiveVignetteIntensity = Mathf.Clamp01(inactiveVignetteIntensity);
        activeVignettePriority = Mathf.Max(0f, activeVignettePriority);
        ResolveReferences();
        ApplyMaskSize();
    }

    private void ApplyMaskSize()
    {
        if (maskRoot != null)
        {
            maskRoot.sizeDelta = new Vector2(maskSize, maskSize);
        }
    }

    private void ResolveVignette()
    {
        if (!driveVignette)
        {
            return;
        }

        if (globalVolume == null)
        {
            Volume[] volumes = FindObjectsByType<Volume>(FindObjectsInactive.Include);
            for (int i = 0; i < volumes.Length; i++)
            {
                if (volumes[i] != null && volumes[i].name == "GlobalVolume")
                {
                    globalVolume = volumes[i];
                    break;
                }
            }

            if (globalVolume == null)
            {
                for (int i = 0; i < volumes.Length; i++)
                {
                    if (volumes[i] != null && volumes[i].isGlobal)
                    {
                        globalVolume = volumes[i];
                        break;
                    }
                }
            }
        }

        if (globalVolume == null)
        {
            WarnMissingVignette("GlobalVolume introuvable.");
            return;
        }

        CaptureOriginalVolumePriority();

        VolumeProfile profile = Application.isPlaying ? globalVolume.profile : globalVolume.sharedProfile;
        if (profile == null)
        {
            WarnMissingVignette($"Aucun VolumeProfile sur {globalVolume.name}.");
            return;
        }

        if (!profile.TryGet(out vignette))
        {
            if (!Application.isPlaying)
            {
                WarnMissingVignette($"Aucune Vignette HDRP dans le profile de {globalVolume.name}.");
                return;
            }

            vignette = profile.Add<Vignette>(true);
            vignette.mode.overrideState = true;
            vignette.mode.value = VignetteMode.Procedural;
            vignette.color.overrideState = true;
            vignette.color.value = Color.black;
        }

        vignette.center.overrideState = true;
        vignette.intensity.overrideState = true;
        warnedMissingVignette = false;
    }

    private void ApplyVignette(Vector2 center, float maskAlpha)
    {
        if (!driveVignette)
        {
            return;
        }

        if (vignette == null)
        {
            ResolveVignette();
        }

        if (vignette == null)
        {
            return;
        }

        lastVignetteCenter = new Vector2(Mathf.Clamp01(center.x), Mathf.Clamp01(center.y));
        float intensity = Mathf.Lerp(inactiveVignetteIntensity, activeVignetteIntensity, Mathf.Clamp01(maskAlpha));
        vignette.center.value = lastVignetteCenter;
        vignette.intensity.value = intensity;
        ApplyVignetteVolumePriority(maskAlpha);
    }

    private Vector2 GetMaskViewportCenter(Camera eventCamera)
    {
        Vector3 worldCenter = maskRoot.TransformPoint(maskRoot.rect.center);
        Vector2 screenCenter = RectTransformUtility.WorldToScreenPoint(eventCamera, worldCenter);

        float width = Mathf.Max(1f, Screen.width);
        float height = Mathf.Max(1f, Screen.height);
        return new Vector2(screenCenter.x / width, screenCenter.y / height);
    }

    private void WarnMissingVignette(string reason)
    {
        if (warnedMissingVignette)
        {
            return;
        }

        Debug.LogWarning($"[XRay] Vignette non pilotee sur {name}: {reason}");
        warnedMissingVignette = true;
    }

    private void CaptureOriginalVolumePriority()
    {
        if (hasOriginalVolumePriority || globalVolume == null)
        {
            return;
        }

        originalVolumePriority = globalVolume.priority;
        hasOriginalVolumePriority = true;
    }

    private void ApplyVignetteVolumePriority(float maskAlpha)
    {
        if (!raiseVignettePriorityWhileVisible || globalVolume == null)
        {
            return;
        }

        CaptureOriginalVolumePriority();
        globalVolume.priority = maskAlpha > 0.001f
            ? activeVignettePriority
            : originalVolumePriority;
    }
}
