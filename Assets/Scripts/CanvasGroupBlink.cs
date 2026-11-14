using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasGroup))]
public sealed class CanvasGroupBlink : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float cycleDuration = 3f;
    [SerializeField, Range(0f, 1f)] private float minimumOpacity = 0.35f;

    private CanvasGroup group;
    private float baseAlpha;
    private float startedAt;

    private void OnEnable()
    {
        group = GetComponent<CanvasGroup>();
        baseAlpha = group.alpha;
        startedAt = Time.unscaledTime;
    }

    private void Update()
    {
        float phase = Mathf.Repeat((Time.unscaledTime - startedAt) / Mathf.Max(0.1f, cycleDuration), 1f);
        float opacity = (1f + Mathf.Cos(phase * 2f * Mathf.PI)) * 0.5f;
        group.alpha = baseAlpha * Mathf.Lerp(minimumOpacity, 1f, opacity);
    }

    private void OnDisable()
    {
        if (group != null) group.alpha = baseAlpha;
    }
}
