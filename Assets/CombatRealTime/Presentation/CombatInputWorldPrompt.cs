using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Prompt world-space temporaire pilote par les Animation Events ennemis.
/// </summary>
public sealed class CombatInputWorldPrompt : MonoBehaviour
{
    private const float WorldScale = 0.01f;

    private Transform anchor;
    private Camera targetCamera;
    private Vector3 offset;

    public static CombatInputWorldPrompt Show(Transform anchor, Sprite inputSprite, Vector3 offset)
    {
        if (anchor == null || inputSprite == null)
        {
            return null;
        }

        GameObject root = new GameObject(
            "CombatInputWorldUI",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasRenderer),
            typeof(CombatInputWorldPrompt));
        root.transform.SetParent(anchor, false);
        root.transform.localPosition = offset;
        root.transform.localScale = Vector3.one * WorldScale;

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 1001;
        canvas.worldCamera = Camera.main;

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(260f, 88f);

        GameObject icon = new GameObject("InputIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        icon.transform.SetParent(root.transform, false);
        RectTransform iconRect = icon.GetComponent<RectTransform>();
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.offsetMin = Vector2.zero;
        iconRect.offsetMax = Vector2.zero;

        Image image = icon.GetComponent<Image>();
        image.sprite = inputSprite;
        image.preserveAspect = true;
        image.raycastTarget = false;

        CombatInputWorldPrompt prompt = root.GetComponent<CombatInputWorldPrompt>();
        prompt.anchor = anchor;
        prompt.offset = offset;
        prompt.targetCamera = canvas.worldCamera;
        return prompt;
    }

    public void Hide()
    {
        Destroy(gameObject);
    }

    private void LateUpdate()
    {
        if (anchor == null)
        {
            Hide();
            return;
        }

        transform.localPosition = offset;
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera != null)
        {
            transform.rotation = Quaternion.LookRotation(targetCamera.transform.forward, targetCamera.transform.up);
        }
    }
}
