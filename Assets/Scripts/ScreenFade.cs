using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ScreenFade : MonoBehaviour
{
    public enum FadeMode
    {
        FadeIn,
        FadeOut
    }

    private const string CanvasObjectName = "ScreenFadeCanvas";
    private const string PanelObjectName = "ScreenPanel";

    [Header("Fade")]
    [SerializeField] private Color fadeColor = Color.black;
    [SerializeField, Min(0f)] private float fadeDuration = 1f;
    [SerializeField] private FadeMode startupFade = FadeMode.FadeIn;

    [Header("Screen Panel")]
    [SerializeField] private Canvas overlayCanvas;
    [SerializeField] private RectTransform screenPanel;
    [SerializeField] private CanvasGroup screenPanelCanvasGroup;
    [SerializeField] private Image screenPanelImage;
    [SerializeField] private int sortingOrder = 32767;
    [SerializeField] private bool blockRaycastsWhileVisible = true;

    private Coroutine fadeRoutine;

    private void Awake()
    {
        PrepareConfiguredFade();
    }

    public void PrepareConfiguredFade()
    {
        if (!EnsureScreenPanel())
        {
            return;
        }

        screenPanel.gameObject.SetActive(true);
        BringPanelToFront();
        ApplyPanelColor();
        SetPanelAlpha(startupFade == FadeMode.FadeIn ? 1f : 0f);
    }

    public void PlayConfiguredFade()
    {
        switch (startupFade)
        {
            case FadeMode.FadeOut:
                FadeOut();
                break;
            default:
                FadeIn();
                break;
        }
    }

    public void FadeIn()
    {
        PlayFade(fromAlpha: 1f, toAlpha: 0f, disableWhenTransparent: true);
    }

    public void FadeOut()
    {
        PlayFade(fromAlpha: 0f, toAlpha: 1f, disableWhenTransparent: false);
    }

    private void PlayFade(float fromAlpha, float toAlpha, bool disableWhenTransparent)
    {
        if (!EnsureScreenPanel())
        {
            Debug.LogWarning("[ScreenFade] Impossible de jouer le fondu: le Canvas, le ScreenPanel ou son CanvasGroup n'a pas pu etre cree.", this);
            return;
        }

        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
        }

        screenPanel.gameObject.SetActive(true);
        BringPanelToFront();
        ApplyPanelColor();
        SetPanelAlpha(fromAlpha);

        float safeDuration = Mathf.Max(0f, fadeDuration);
        if (safeDuration <= 0f)
        {
            SetPanelAlpha(toAlpha);
            if (disableWhenTransparent && Mathf.Approximately(toAlpha, 0f))
            {
                screenPanel.gameObject.SetActive(false);
            }

            fadeRoutine = null;
            return;
        }

        fadeRoutine = StartCoroutine(FadeRoutine(fromAlpha, toAlpha, safeDuration, disableWhenTransparent));
    }

    private IEnumerator FadeRoutine(float fromAlpha, float toAlpha, float duration, bool disableWhenTransparent)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            SetPanelAlpha(Mathf.Lerp(fromAlpha, toAlpha, progress));
            yield return null;
        }

        SetPanelAlpha(toAlpha);
        if (disableWhenTransparent && Mathf.Approximately(toAlpha, 0f))
        {
            screenPanel.gameObject.SetActive(false);
        }

        fadeRoutine = null;
    }

    private bool EnsureScreenPanel()
    {
        EnsureCanvas();
        if (overlayCanvas == null)
        {
            return false;
        }

        EnsurePanel();
        EnsureCanvasGroup();
        EnsureImage();
        return screenPanel != null && screenPanelCanvasGroup != null;
    }

    private void EnsureCanvas()
    {
        if (overlayCanvas == null)
        {
            Transform existingCanvas = transform.Find(CanvasObjectName);
            if (existingCanvas != null)
            {
                overlayCanvas = existingCanvas.GetComponent<Canvas>();
            }
        }

        if (overlayCanvas == null)
        {
            GameObject canvasObject = new GameObject(CanvasObjectName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            overlayCanvas = canvasObject.GetComponent<Canvas>();

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = sortingOrder;
    }

    private void EnsurePanel()
    {
        if (screenPanel == null)
        {
            Transform existingPanel = overlayCanvas.transform.Find(PanelObjectName);
            if (existingPanel != null)
            {
                screenPanel = existingPanel as RectTransform;
            }
        }

        if (screenPanel == null)
        {
            GameObject panelObject = new GameObject(PanelObjectName, typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            panelObject.transform.SetParent(overlayCanvas.transform, false);
            screenPanel = panelObject.GetComponent<RectTransform>();
        }

        StretchToFullScreen(screenPanel);
    }

    private void EnsureCanvasGroup()
    {
        if (screenPanelCanvasGroup == null && screenPanel != null)
        {
            screenPanelCanvasGroup = screenPanel.GetComponent<CanvasGroup>();
        }

        if (screenPanelCanvasGroup == null && screenPanel != null)
        {
            screenPanelCanvasGroup = screenPanel.gameObject.AddComponent<CanvasGroup>();
        }

        if (screenPanelCanvasGroup != null)
        {
            screenPanelCanvasGroup.ignoreParentGroups = true;
            screenPanelCanvasGroup.interactable = false;
        }
    }

    private void EnsureImage()
    {
        if (screenPanelImage == null && screenPanel != null)
        {
            screenPanelImage = screenPanel.GetComponent<Image>();
        }

        if (screenPanelImage == null && screenPanel != null)
        {
            screenPanelImage = screenPanel.GetComponentInChildren<Image>(true);
        }

        if (screenPanelImage == null && screenPanel != null)
        {
            screenPanelImage = screenPanel.gameObject.AddComponent<Image>();
        }

        if (screenPanelImage != null)
        {
            screenPanelImage.raycastTarget = true;
            ApplyPanelColor();
        }
    }

    private void SetPanelAlpha(float alpha)
    {
        if (screenPanelCanvasGroup == null)
        {
            return;
        }

        float clampedAlpha = Mathf.Clamp01(alpha);
        screenPanelCanvasGroup.ignoreParentGroups = true;
        screenPanelCanvasGroup.alpha = clampedAlpha;
        screenPanelCanvasGroup.blocksRaycasts = blockRaycastsWhileVisible && clampedAlpha > 0f;
        screenPanelCanvasGroup.interactable = false;
    }

    private void ApplyPanelColor()
    {
        if (screenPanelImage == null)
        {
            return;
        }

        Color color = fadeColor;
        color.a = 1f;
        screenPanelImage.color = color;
    }

    private void BringPanelToFront()
    {
        if (screenPanel == null)
        {
            return;
        }

        screenPanel.SetAsLastSibling();
    }

    private static void StretchToFullScreen(RectTransform rectTransform)
    {
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.localScale = Vector3.one;
    }

    private void OnDisable()
    {
        fadeRoutine = null;
    }

    private void OnValidate()
    {
        fadeDuration = Mathf.Max(0f, fadeDuration);

        if (overlayCanvas != null)
        {
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.sortingOrder = sortingOrder;
        }

        if (screenPanel != null)
        {
            StretchToFullScreen(screenPanel);
            BringPanelToFront();
        }

        if (screenPanelCanvasGroup != null)
        {
            screenPanelCanvasGroup.ignoreParentGroups = true;
            screenPanelCanvasGroup.interactable = false;
            screenPanelCanvasGroup.blocksRaycasts = blockRaycastsWhileVisible && screenPanelCanvasGroup.alpha > 0f;
        }

        if (screenPanelImage != null)
        {
            screenPanelImage.raycastTarget = true;
            ApplyPanelColor();
        }
    }
}
