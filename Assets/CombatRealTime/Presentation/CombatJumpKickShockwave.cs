using UnityEngine;

[DisallowMultipleComponent]
public sealed class CombatJumpKickShockwave : MonoBehaviour
{
    [SerializeField, Min(0.05f)] private float durationSeconds = 0.45f;
    [SerializeField] private float startScale = 0.35f;
    [SerializeField] private float endScale = 5.5f;
    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField] private Camera targetCamera;

    private SpriteRenderer[] spriteRenderers;
    private Color[] initialColors;
    private float elapsed;

    private void Awake()
    {
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        initialColors = new Color[spriteRenderers.Length];
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            initialColors[i] = spriteRenderers[i].color;
        }
    }

    private void Update()
    {
        elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float progress = Mathf.Clamp01(elapsed / Mathf.Max(0.05f, durationSeconds));
        float scale = Mathf.Lerp(startScale, endScale, 1f - Mathf.Pow(1f - progress, 3f));
        transform.localScale = Vector3.one * scale;

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] == null)
            {
                continue;
            }

            Color color = initialColors[i];
            color.a *= 1f - progress;
            spriteRenderers[i].color = color;
        }

        if (progress >= 1f)
        {
            Destroy(gameObject);
        }
    }

    private void LateUpdate()
    {
        targetCamera ??= Camera.main;
        if (targetCamera != null)
        {
            transform.forward = targetCamera.transform.forward;
        }
    }
}
