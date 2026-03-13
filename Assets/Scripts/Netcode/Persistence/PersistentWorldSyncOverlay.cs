using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PersistentWorldSyncOverlay : MonoBehaviour
{
    [SerializeField] private bool createRuntimeOverlayIfMissing = true;
    [SerializeField] private string defaultStatusMessage = "Synchronisation du monde...";
    [SerializeField] private Color overlayColor = Color.black;
    [SerializeField] private Color failureOverlayColor = new Color(0.22f, 0.03f, 0.03f, 1f);
    [SerializeField, Range(0f, 1f)] private float overlayAlpha = 1f;
    [SerializeField] private Color statusTextColor = Color.white;
    [SerializeField] private Color failureTextColor = new Color(1f, 0.85f, 0.85f, 1f);

    private Canvas overlayCanvas;
    private CanvasGroup overlayGroup;
    private Image overlayImage;
    private Text statusText;

    private void Awake()
    {
        EnsureOverlay();
        SetVisible(false);
    }

    public void SetVisible(bool visible, string message = null, bool failed = false)
    {
        EnsureOverlay();
        if (overlayCanvas == null || overlayGroup == null)
        {
            return;
        }

        string resolvedMessage = string.IsNullOrWhiteSpace(message) ? defaultStatusMessage : message;
        if (statusText != null)
        {
            statusText.text = resolvedMessage;
            statusText.color = failed ? failureTextColor : statusTextColor;
        }

        if (overlayImage != null)
        {
            overlayImage.color = failed ? failureOverlayColor : overlayColor;
        }

        overlayGroup.alpha = visible ? overlayAlpha : 0f;
        overlayGroup.blocksRaycasts = visible;
        overlayGroup.interactable = visible;
        overlayCanvas.enabled = visible;
    }

    private void EnsureOverlay()
    {
        if (!createRuntimeOverlayIfMissing || overlayCanvas != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject("PersistentWorldSyncOverlay");
        canvasObject.transform.SetParent(transform, false);

        overlayCanvas = canvasObject.AddComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = short.MaxValue;

        canvasObject.AddComponent<CanvasScaler>();
        canvasObject.AddComponent<GraphicRaycaster>();

        overlayGroup = canvasObject.AddComponent<CanvasGroup>();
        overlayGroup.alpha = 0f;
        overlayGroup.blocksRaycasts = false;
        overlayGroup.interactable = false;

        GameObject backgroundObject = new GameObject("Background");
        backgroundObject.transform.SetParent(canvasObject.transform, false);
        RectTransform backgroundRect = backgroundObject.AddComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;

        overlayImage = backgroundObject.AddComponent<Image>();
        overlayImage.color = overlayColor;

        GameObject textObject = new GameObject("Status");
        textObject.transform.SetParent(canvasObject.transform, false);
        RectTransform textRect = textObject.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.sizeDelta = new Vector2(720f, 120f);
        textRect.anchoredPosition = Vector2.zero;

        statusText = textObject.AddComponent<Text>();
        statusText.alignment = TextAnchor.MiddleCenter;
        statusText.color = statusTextColor;
        statusText.fontSize = 28;
        Font builtInFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (builtInFont == null)
        {
            builtInFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        statusText.font = builtInFont;
        statusText.text = defaultStatusMessage;
    }
}
