using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Presentation locale et ephemere des degats a proximite d'un combattant.
/// </summary>
public sealed class CombatDamageWorldFeedback : MonoBehaviour
{
    private const float Duration = 0.72f;
    private const float RiseDistance = 0.45f;
    private static int popupSequence;

    private CanvasGroup canvasGroup;
    private Vector3 startLocalPosition;
    private Vector3 baseScale;
    private float elapsed;
    private Camera targetCamera;

    public static void Show(Transform target, int amount, Color color, float height)
    {
        if (target == null || amount <= 0)
        {
            return;
        }

        ShowText(target, "-" + amount, color, height, 46f);
    }

    public static void ShowMessage(Transform target, string message, Color color, float height)
    {
        if (target == null || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        ShowText(target, message, color, height, 30f);
    }

    private static void ShowText(Transform target, string message, Color color, float height, float fontSize)
    {

        GameObject root = new GameObject(
            "CombatDamageWorldUI",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasGroup),
            typeof(CombatDamageWorldFeedback));
        root.transform.SetParent(target, false);

        int sequence = popupSequence++;
        float lateralOffset = (Mathf.Repeat(sequence * 0.6180339f, 1f) - 0.5f) * 0.45f;
        root.transform.localPosition = new Vector3(lateralOffset, height, 0f);
        root.transform.localScale = Vector3.one * 0.01f;

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 1000;
        canvas.worldCamera = Camera.main;

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(240f, 90f);

        GameObject label = new GameObject("Damage", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        label.transform.SetParent(root.transform, false);
        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = label.GetComponent<TextMeshProUGUI>();
        text.text = message;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        text.outlineColor = new Color(0f, 0f, 0f, 0.85f);
        text.outlineWidth = 0.18f;
        text.raycastTarget = false;

        CombatDamageWorldFeedback feedback = root.GetComponent<CombatDamageWorldFeedback>();
        feedback.canvasGroup = root.GetComponent<CanvasGroup>();
        feedback.startLocalPosition = root.transform.localPosition;
        feedback.baseScale = root.transform.localScale;
        feedback.targetCamera = canvas.worldCamera;
    }

    private void LateUpdate()
    {
        elapsed += Time.unscaledDeltaTime;
        float normalizedTime = Mathf.Clamp01(elapsed / Duration);
        float rise = Mathf.SmoothStep(0f, RiseDistance, normalizedTime);
        float punch = 1f + Mathf.Sin(Mathf.Clamp01(normalizedTime * 5f) * Mathf.PI) * 0.28f;

        transform.localPosition = startLocalPosition + Vector3.up * rise;
        transform.localScale = baseScale * punch;
        canvasGroup.alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.34f, 1f, normalizedTime));

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera != null)
        {
            transform.rotation = Quaternion.LookRotation(targetCamera.transform.forward, targetCamera.transform.up);
        }

        if (normalizedTime >= 1f)
        {
            Destroy(gameObject);
        }
    }
}
