using TMPro;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// UI partagee pour choisir une quantite via Move gauche/droite.
[DisallowMultipleComponent]
public class QuantityBox : MonoBehaviour
{
    public static QuantityBox Instance { get; private set; }

    [Header("UI")]
    [Tooltip("Texte affiche dans le panel de quantite.")]
    public TextMeshProUGUI quantityText;
    [Tooltip("Image de la fleche gauche.")]
    public Image arrowLeft;
    [Tooltip("Image de la fleche droite.")]
    public Image arrowRight;

    [Header("Display")]
    [Tooltip("Duree du fade du panel de quantite.")]
    public float fadeDuration = 0.15f;
    [Tooltip("Met l'alpha a 0 au demarrage.")]
    public bool setAlphaToZeroOnStart = true;
    [Tooltip("Ajoute un CanvasGroup si manquant.")]
    public bool addCanvasGroupIfMissing = true;
    [Tooltip("Desactive les raycasts quand cache.")]
    public bool disableRaycastsWhenHidden = true;
    [Tooltip("Format d'affichage (quantite/total).")]
    public string defaultFormat = "{0}/{1}";

    [Header("Arrow Pump")]
    [Tooltip("Scale du pump des fleches.")]
    public float arrowPumpScale = 1.12f;
    [Tooltip("Duree du pump des fleches.")]
    public float arrowPumpDuration = 0.1f;

    private RectTransform anchor;
    private Vector2 offset;
    private int currentQuantity;
    private int maxQuantity;
    private string format;
    private int lastDirection;
    private float nextMoveTime;
    private bool active;

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Coroutine fadeRoutine;
    private Coroutine pumpLeftRoutine;
    private Coroutine pumpRightRoutine;

    public bool IsActive => active;

    public int CurrentQuantity
    {
        get
        {
            int max = Mathf.Max(1, maxQuantity);
            return Mathf.Clamp(currentQuantity, 1, max);
        }
    }

    public static QuantityBox Resolve()
    {
        if (Instance != null)
        {
            return Instance;
        }

        GameObject tagged = GameObject.FindWithTag("QuantityBox");
        if (tagged != null)
        {
            QuantityBox box = tagged.GetComponent<QuantityBox>();
            if (box == null)
            {
                box = tagged.AddComponent<QuantityBox>();
            }
            Instance = box;
            return box;
        }

        Transform[] allTransforms = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform t = allTransforms[i];
            if (t == null || !t.gameObject.scene.IsValid())
            {
                continue;
            }

            if (!t.CompareTag("QuantityBox"))
            {
                continue;
            }

            QuantityBox box = t.GetComponent<QuantityBox>();
            if (box == null)
            {
                box = t.gameObject.AddComponent<QuantityBox>();
            }
            Instance = box;
            return box;
        }

#if UNITY_2023_1_OR_NEWER
        Instance = FindAnyObjectByType<QuantityBox>(FindObjectsInactive.Include);
#else
        Instance = FindAnyObjectByType<QuantityBox>();
#endif
        return Instance;
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }

        rectTransform = GetComponent<RectTransform>();
        ResolveReferences();
        canvasGroup = GetCanvasGroup();
        if (canvasGroup != null && setAlphaToZeroOnStart)
        {
            SetAlpha(0f);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void LateUpdate()
    {
        if (!active)
        {
            return;
        }

        PositionPanel();
    }

    public void Open(RectTransform anchorRect, Vector2 panelOffset, int startQuantity, int max, string textFormat)
    {
        ResolveReferences();
        canvasGroup = GetCanvasGroup();
        anchor = anchorRect;
        offset = panelOffset;
        maxQuantity = Mathf.Max(1, max);
        currentQuantity = Mathf.Clamp(startQuantity, 1, maxQuantity);
        format = string.IsNullOrWhiteSpace(textFormat) ? defaultFormat : textFormat;
        lastDirection = 0;
        nextMoveTime = 0f;
        active = true;
        gameObject.SetActive(true);
        UpdateText();
        PositionPanel();
        FadeTo(1f, fadeDuration);
    }

    public void Close()
    {
        active = false;
        anchor = null;
        FadeTo(0f, fadeDuration);
    }

    public void HandleInput(Vector2 input, float deadzone, float initialRepeatDelay, float repeatInterval)
    {
        if (!active)
        {
            return;
        }

        int direction = GetHorizontalDirection(input, deadzone);
        if (direction == 0)
        {
            lastDirection = 0;
            nextMoveTime = 0f;
            return;
        }

        float now = Time.unscaledTime;
        if (direction != lastDirection)
        {
            AdjustQuantity(direction);
            lastDirection = direction;
            nextMoveTime = now + Mathf.Max(0.02f, initialRepeatDelay);
            return;
        }

        if (now >= nextMoveTime)
        {
            AdjustQuantity(direction);
            nextMoveTime = now + Mathf.Max(0.02f, repeatInterval);
        }
    }

    private int GetHorizontalDirection(Vector2 input, float deadzone)
    {
        float absX = Mathf.Abs(input.x);
        float absY = Mathf.Abs(input.y);
        if (absX < deadzone || absX < absY)
        {
            return 0;
        }

        return input.x > 0f ? 1 : -1;
    }

    private void AdjustQuantity(int direction)
    {
        if (direction < 0)
        {
            currentQuantity = Mathf.Max(1, currentQuantity - 1);
            PumpArrow(arrowLeft, ref pumpLeftRoutine);
        }
        else if (direction > 0)
        {
            currentQuantity = Mathf.Min(maxQuantity, currentQuantity + 1);
            PumpArrow(arrowRight, ref pumpRightRoutine);
        }

        UpdateText();
    }

    private void UpdateText()
    {
        if (quantityText == null)
        {
            return;
        }

        int current = CurrentQuantity;
        int max = Mathf.Max(1, maxQuantity);
        string text = $"{current}/{max}";
        if (!string.IsNullOrWhiteSpace(format) && format.Contains("{0"))
        {
            text = string.Format(format, current, max);
        }

        quantityText.text = text;
        quantityText.gameObject.SetActive(true);
    }

    private void PositionPanel()
    {
        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
        }

        Vector2 localOffset = offset;

        if (anchor == null)
        {
            rectTransform.anchoredPosition = localOffset;
            return;
        }

        RectTransform parentRect = rectTransform.parent as RectTransform;
        if (parentRect == null)
        {
            rectTransform.anchoredPosition = localOffset;
            return;
        }

        Canvas canvas = GetComponentInParent<Canvas>();
        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, anchor.position);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPoint, uiCamera, out Vector2 localPoint))
        {
            rectTransform.anchoredPosition = localPoint + localOffset;
            return;
        }

        rectTransform.position = anchor.position + (Vector3)localOffset;
    }

    private void ResolveReferences()
    {
        if (quantityText == null)
        {
            TextMeshProUGUI[] texts = GetComponentsInChildren<TextMeshProUGUI>(true);
            if (texts != null && texts.Length > 0)
            {
                for (int i = 0; i < texts.Length; i++)
                {
                    TextMeshProUGUI tmp = texts[i];
                    if (tmp == null)
                    {
                        continue;
                    }

                    string name = tmp.name;
                    if (!string.IsNullOrEmpty(name)
                        && (name.IndexOf("quantity", System.StringComparison.OrdinalIgnoreCase) >= 0
                            || name.IndexOf("count", System.StringComparison.OrdinalIgnoreCase) >= 0
                            || name.IndexOf("qty", System.StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        quantityText = tmp;
                        break;
                    }
                }

                if (quantityText == null)
                {
                    quantityText = texts[0];
                }
            }
        }

        if (arrowLeft == null)
        {
            arrowLeft = FindArrowImage("left", "gauche", "arrow_left", "arrowleft", "flechegauche", "fleche_gauche");
        }

        if (arrowRight == null)
        {
            arrowRight = FindArrowImage("right", "droite", "arrow_right", "arrowright", "flechedroite", "fleche_droite");
        }

        if (arrowLeft == null)
        {
            arrowLeft = FindArrowImage("down", "bas", "arrow_down", "arrowdown", "flechebas", "fleche_bas");
        }

        if (arrowRight == null)
        {
            arrowRight = FindArrowImage("up", "haut", "arrow_up", "arrowup", "flechehaut", "fleche_haut");
        }
    }

    private Image FindArrowImage(params string[] keywords)
    {
        if (keywords == null || keywords.Length == 0)
        {
            return null;
        }

        Image[] images = GetComponentsInChildren<Image>(true);
        if (images == null || images.Length == 0)
        {
            return null;
        }

        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null)
            {
                continue;
            }

            string name = image.name;
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            for (int k = 0; k < keywords.Length; k++)
            {
                string keyword = keywords[k];
                if (string.IsNullOrWhiteSpace(keyword))
                {
                    continue;
                }

                if (name.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return image;
                }
            }
        }

        return null;
    }

    private void PumpArrow(Image arrow, ref Coroutine routine)
    {
        if (arrow == null)
        {
            return;
        }

        if (routine != null)
        {
            StopCoroutine(routine);
        }

        routine = StartCoroutine(PumpArrowRoutine(arrow.rectTransform, arrowPumpScale, arrowPumpDuration));
    }

    private IEnumerator PumpArrowRoutine(RectTransform rect, float scaleMultiplier, float duration)
    {
        if (rect == null)
        {
            yield break;
        }

        Vector3 baseScale = rect.localScale;
        float half = Mathf.Max(0.01f, duration * 0.5f);
        float time = 0f;
        while (time < half)
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / half);
            rect.localScale = Vector3.Lerp(baseScale, baseScale * scaleMultiplier, t);
            yield return null;
        }

        time = 0f;
        while (time < half)
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / half);
            rect.localScale = Vector3.Lerp(baseScale * scaleMultiplier, baseScale, t);
            yield return null;
        }

        rect.localScale = baseScale;
    }

    private CanvasGroup GetCanvasGroup()
    {
        CanvasGroup group = GetComponent<CanvasGroup>();
        if (group == null && addCanvasGroupIfMissing)
        {
            group = gameObject.AddComponent<CanvasGroup>();
        }

        return group;
    }

    private void FadeTo(float targetAlpha, float duration)
    {
        if (canvasGroup == null)
        {
            return;
        }

        if (!CanRunCoroutines() || !gameObject.activeInHierarchy)
        {
            SetAlpha(targetAlpha);
            return;
        }

        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
        }

        float startAlpha = canvasGroup.alpha;
        if (duration <= 0f)
        {
            SetAlpha(targetAlpha);
            return;
        }

        fadeRoutine = StartCoroutine(FadeRoutine(startAlpha, targetAlpha, duration));
    }

    private IEnumerator FadeRoutine(float startAlpha, float targetAlpha, float duration)
    {
        float time = 0f;
        if (disableRaycastsWhenHidden)
        {
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / duration);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            yield return null;
        }

        SetAlpha(targetAlpha);
    }

    private void SetAlpha(float alpha)
    {
        if (canvasGroup == null)
        {
            return;
        }

        canvasGroup.alpha = alpha;
        if (disableRaycastsWhenHidden)
        {
            bool visible = alpha > 0.001f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }
    }

    private bool CanRunCoroutines()
    {
        return isActiveAndEnabled && gameObject.activeInHierarchy;
    }
}
