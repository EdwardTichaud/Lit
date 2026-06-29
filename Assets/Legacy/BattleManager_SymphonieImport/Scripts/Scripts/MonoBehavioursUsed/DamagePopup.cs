using UnityEngine;
using TMPro;

public class DamagePopup : MonoBehaviour
{
    [Header("Animation")]
    public float floatSpeed = 1f;
    public float duration = 1f;
    [SerializeField] private float bounceHeight = 0.35f;
    [SerializeField] private float bounceFrequency = 2f;
    [SerializeField] private float bounceScale = 0.2f;

    [Header("Références")]
    public TextMeshProUGUI textMesh; // Référence au texte affichant le montant

    private float elapsed = 0f;        // Temps écoulé depuis l'initialisation pour piloter le fondu
    private float floatOffset = 0f;    // Décalage vertical cumulé appliqué au-dessus de la cible
    private float defaultDuration = -1f;
    private CanvasGroup canvasGroup;   // Permet de faire disparaître progressivement le popup
    private DamagePopupManager owner;
    private Transform target;
    private CharacterUnit targetUnit;
    private Camera battleCamera;
    private Canvas popupCanvas;
    private Canvas localCanvas;
    private RectTransform rectTransform;
    private RectTransform textRect;
    private TMP_FontAsset defaultFont;
    private float baseTextScale = 1f;
    private Vector3 styleOffset;
    private bool useCameraRelativeOffset;
    [SerializeField] private float worldSpaceScale = 0.0033f;
    private Vector3 baseRectScale = Vector3.one;
    private bool baseScaleInitialized;
    private const float MinWorldSpaceScale = 0.0001f;
    private const float MaxWorldSpaceScale = 0.02f;
    private const float MinParentScale = 0.0001f;

    public void SetOwner(DamagePopupManager manager)
    {
        owner = manager;
    }

    public void ApplyStyle(TMP_FontAsset font, float fontSize, float textScale, Vector3 offsetOverride, bool offsetUsesCameraAxes)
    {
        if (textMesh == null)
            return;

        if (defaultFont == null)
            defaultFont = textMesh.font;

        textMesh.font = font != null ? font : defaultFont;
        if (fontSize > 0f)
            textMesh.fontSize = fontSize;

        baseTextScale = Mathf.Max(0.001f, textScale);
        styleOffset = offsetOverride;
        useCameraRelativeOffset = offsetUsesCameraAxes;
    }

    private void RefreshStyle()
    {
        if (owner != null)
            owner.ApplyStyle(this);
    }

    /// <summary>
    /// Initialise le popup avec un texte et la cible à suivre.
    /// </summary>
    /// <param name="text">Texte à afficher (dégâts, soins, buff...).</param>
    /// <param name="followTarget">Transform de l'unité concernée.</param>
    /// <param name="cameraOverride">Caméra utilisée pour la conversion Monde/Ecran.</param>
    /// <param name="textColor">Couleur du texte.</param>
    public void Initialize(string text, Transform followTarget, Camera cameraOverride, Color textColor, float durationOverride = -1f)
    {
        if (defaultDuration <= 0f)
            defaultDuration = duration;
        duration = durationOverride > 0f ? durationOverride : defaultDuration;

        if (textMesh == null)
        {
            textMesh = GetComponentInChildren<TextMeshProUGUI>();
            if (textMesh == null)
            {
                Debug.LogError("[DamagePopup] Aucun TextMeshProUGUI trouvé sur le prefab.");
                Release();
                return;
            }
        }

        textMesh.text = text;
        textMesh.color = textColor;
        textRect ??= textMesh.rectTransform;
        textRect.localScale = Vector3.one * Mathf.Max(0.001f, baseTextScale);

        target = followTarget;
        if (target == null)
        {
            Debug.LogWarning("[DamagePopup] Cible manquante, affichage annulé.");
            Release();
            return;
        }

        targetUnit = target.GetComponent<CharacterUnit>() ?? target.GetComponentInParent<CharacterUnit>();

        battleCamera = cameraOverride != null ? cameraOverride : ResolveBattleCamera();
        if (battleCamera == null)
        {
            Debug.LogWarning("[DamagePopup] Aucune caméra disponible pour positionner le popup.");
            Release();
            return;
        }

        rectTransform ??= GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            Debug.LogError("[DamagePopup] Aucun RectTransform disponible pour positionner le popup.");
            Release();
            return;
        }

        if (!baseScaleInitialized)
        {
            baseRectScale = rectTransform.localScale;
            baseScaleInitialized = true;
        }

        localCanvas ??= GetComponent<Canvas>();
        popupCanvas = ResolvePopupCanvas(localCanvas);

        if (popupCanvas == null)
        {
            Debug.LogWarning("[DamagePopup] Aucun Canvas trouvé pour positionner le popup.");
            Release();
            return;
        }

        if (localCanvas != null)
            localCanvas.enabled = popupCanvas == localCanvas;

        if (popupCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            popupCanvas.worldCamera = battleCamera;

        if (popupCanvas.renderMode == RenderMode.WorldSpace)
        {
            float safeScale = GetSafeWorldSpaceScale();
            rectTransform.localScale = baseRectScale * safeScale;
        }
        else
        {
            rectTransform.localScale = baseRectScale;
        }

        canvasGroup ??= GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        elapsed = 0f;
        floatOffset = 0f;
        canvasGroup.alpha = 1f;

        RefreshStyle();

        // Position initiale avant la première mise à jour (indispensable pour appliquer l'offset).
        UpdatePosition();
    }

    void Update()
    {
        if (duration <= 0f)
        {
            Release();
            return;
        }

        RefreshStyle();

        if (popupCanvas != null
            && popupCanvas.renderMode == RenderMode.WorldSpace
            && rectTransform != null
            && baseScaleInitialized)
        {
            float safeScale = GetSafeWorldSpaceScale();
            rectTransform.localScale = baseRectScale * safeScale;
        }

        // Les effets d'interface utilisent le temps non-scalé pour rester visibles en pause.
        elapsed += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(elapsed / duration);

        float rise = floatSpeed * duration * t;
        float bounce = Mathf.Sin(t * Mathf.PI * bounceFrequency) * bounceHeight * (1f - t);
        floatOffset = rise + bounce;

        if (textRect != null)
        {
            float pulseScale = baseTextScale * (1f + bounceScale * Mathf.Sin(t * Mathf.PI));
            textRect.localScale = Vector3.one * pulseScale;
        }

        // Met à jour la position à chaque frame.
        UpdatePosition();

        if (canvasGroup != null)
            canvasGroup.alpha = Mathf.Clamp01(1f - t);

        if (t >= 1f)
            Release();
    }

    private float GetSafeWorldSpaceScale()
    {
        float safeScale = Mathf.Clamp(worldSpaceScale, MinWorldSpaceScale, MaxWorldSpaceScale);

        Transform scaleRoot = rectTransform != null ? rectTransform.parent : null;
        if (scaleRoot != null)
        {
            Vector3 lossyScale = scaleRoot.lossyScale;
            float parentScale = Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.y), Mathf.Abs(lossyScale.z));
            if (parentScale > MinParentScale)
                safeScale /= parentScale;
        }

        return safeScale;
    }

    /// <summary>
    /// Calcule la position et l'orientation du popup d'écran.
    /// </summary>
    private void UpdatePosition()
    {
        if (battleCamera == null || target == null || popupCanvas == null)
            return;

        Vector3 anchor = target.position;
        if (targetUnit != null)
        {
            Bounds bounds = targetUnit.GetVisualBounds();
            anchor = bounds.center + Vector3.up * bounds.extents.y;
        }

        Vector3 finalOffset = styleOffset;
        if (useCameraRelativeOffset && battleCamera != null)
        {
            Transform camTransform = battleCamera.transform;
            finalOffset = camTransform.right * styleOffset.x
                          + camTransform.up * styleOffset.y
                          + camTransform.forward * styleOffset.z;
        }

        Vector3 worldPos = anchor + finalOffset + Vector3.up * floatOffset;

        if (popupCanvas != null && popupCanvas.renderMode == RenderMode.WorldSpace)
        {
            rectTransform.position = worldPos;

            if (battleCamera != null)
            {
                Vector3 toCamera = rectTransform.position - battleCamera.transform.position;
                if (toCamera.sqrMagnitude > 0.0001f)
                    rectTransform.rotation = Quaternion.LookRotation(toCamera, Vector3.up);
            }

            return;
        }

        Vector3 screenPos = battleCamera.WorldToScreenPoint(worldPos);
        if (screenPos.z < 0f)
            return;

        screenPos.x = Mathf.Clamp(screenPos.x, 0f, Screen.width);
        screenPos.y = Mathf.Clamp(screenPos.y, 0f, Screen.height);
        RectTransform canvasRect = popupCanvas != null ? popupCanvas.transform as RectTransform : null;
        if (canvasRect == null)
            return;

        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(canvasRect, screenPos, popupCanvas.worldCamera, out Vector3 worldPoint))
            rectTransform.position = worldPoint;
    }

    private Canvas ResolvePopupCanvas(Canvas local)
    {
        Canvas parentCanvas = null;
        Canvas fallbackCanvas = null;
        Canvas[] canvases = GetComponentsInParent<Canvas>(true);
        foreach (Canvas canvas in canvases)
        {
            if (canvas == null || canvas == local)
                continue;
            if (!canvas.enabled)
                continue;
            if (canvas.renderMode == RenderMode.WorldSpace)
            {
                parentCanvas = canvas;
                break;
            }

            fallbackCanvas ??= canvas;
        }

        return parentCanvas ?? fallbackCanvas ?? local;
    }


    private void Release()
    {
        if (owner != null)
            owner.ReleasePopup(this);
        else
            Destroy(gameObject);
    }

    private Camera ResolveBattleCamera()
    {
        GameObject battleCameraGO = GameObject.FindGameObjectWithTag("BattleCamera");
        if (battleCameraGO != null)
            return battleCameraGO.GetComponent<Camera>();

        return Camera.main;
    }
}
