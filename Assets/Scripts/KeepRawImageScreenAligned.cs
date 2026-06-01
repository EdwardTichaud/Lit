using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(1030)]
[RequireComponent(typeof(RectTransform))]
public sealed class KeepRawImageScreenAligned : MonoBehaviour
{
    [SerializeField] private RectTransform canvasRect;
    [SerializeField] private RectTransform maskRoot;

    private RectTransform rect;

    private void Reset()
    {
        ResolveReferences();
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void LateUpdate()
    {
        ResolveReferences();

        if (canvasRect == null || rect == null)
        {
            return;
        }

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = canvasRect.rect.size;

        // The RawImage lives inside the moving circular mask, so offset it in
        // the opposite direction to keep the RenderTexture locked to screen space.
        rect.anchoredPosition = maskRoot != null ? -maskRoot.anchoredPosition : Vector2.zero;
    }

    private void ResolveReferences()
    {
        if (rect == null)
        {
            rect = GetComponent<RectTransform>();
        }

        if (maskRoot == null && rect != null && rect.parent is RectTransform parentRect)
        {
            maskRoot = parentRect;
        }

        if (canvasRect == null)
        {
            Canvas canvas = GetComponentInParent<Canvas>(true);
            if (canvas != null)
            {
                canvasRect = canvas.GetComponent<RectTransform>();
            }
        }
    }
}
