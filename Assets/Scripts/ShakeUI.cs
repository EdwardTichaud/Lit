using UnityEngine;

[DisallowMultipleComponent]
public class ShakeUI : MonoBehaviour
{
    [Header("Target")]
    [SerializeField, Tooltip("RectTransform a secouer. Laisse vide pour utiliser ce GameObject.")]
    private RectTransform target;
    [SerializeField, Tooltip("Utilise le temps non scale pour fonctionner pendant les pauses/UI.")]
    private bool useUnscaledTime = true;
    [SerializeField, Tooltip("Reinitialise la position de base quand l'objet est reactive.")]
    private bool recaptureBaseOnEnable = true;

    [Header("Shake")]
    [SerializeField, Min(0.01f), Tooltip("Duree du tremblement.")]
    private float duration = 0.28f;
    [SerializeField, Min(0f), Tooltip("Amplitude maximale en pixels UI.")]
    private float amplitude = 18f;
    [SerializeField, Min(0.01f), Tooltip("Frequence du tremblement.")]
    private float frequency = 34f;
    [SerializeField, Tooltip("Axes utilises par le tremblement.")]
    private Vector2 axisMultiplier = Vector2.one;
    [SerializeField, Min(0f), Tooltip("Force de l'attenuation. 1 = lineaire, plus haut = fin plus rapide.")]
    private float dampingPower = 1.4f;

    private RectTransform resolvedTarget;
    private Vector2 baseAnchoredPosition;
    private float elapsed;
    private float phase;
    private bool isShaking;

    public bool IsShaking => isShaking;

    private void Awake()
    {
        ResolveTarget();
        CaptureBasePosition();
    }

    private void OnEnable()
    {
        ResolveTarget();
        if (recaptureBaseOnEnable)
        {
            CaptureBasePosition();
        }
    }

    private void OnDisable()
    {
        ResetPosition();
        isShaking = false;
        elapsed = 0f;
    }

    private void Update()
    {
        if (!isShaking || resolvedTarget == null)
        {
            return;
        }

        float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        elapsed += Mathf.Max(0f, deltaTime);

        float safeDuration = Mathf.Max(0.01f, duration);
        float progress = Mathf.Clamp01(elapsed / safeDuration);
        float envelope = Mathf.Pow(1f - progress, Mathf.Max(0f, dampingPower));

        float time = (useUnscaledTime ? Time.unscaledTime : Time.time) + phase;
        float offsetX = Mathf.Sin(time * frequency) * amplitude * envelope * axisMultiplier.x;
        float offsetY = Mathf.Cos(time * frequency * 1.37f) * amplitude * envelope * axisMultiplier.y;
        resolvedTarget.anchoredPosition = baseAnchoredPosition + new Vector2(offsetX, offsetY);

        if (progress >= 1f)
        {
            StopShake();
        }
    }

    public void Shake()
    {
        Play();
    }

    public void Play()
    {
        ResolveTarget();
        if (resolvedTarget == null)
        {
            return;
        }

        if (!isShaking)
        {
            CaptureBasePosition();
        }

        elapsed = 0f;
        phase = Random.value * 100f;
        isShaking = true;
    }

    public void StopShake()
    {
        ResetPosition();
        isShaking = false;
        elapsed = 0f;
    }

    private void ResolveTarget()
    {
        resolvedTarget = target != null ? target : transform as RectTransform;
    }

    private void CaptureBasePosition()
    {
        if (resolvedTarget != null)
        {
            baseAnchoredPosition = resolvedTarget.anchoredPosition;
        }
    }

    private void ResetPosition()
    {
        if (resolvedTarget != null)
        {
            resolvedTarget.anchoredPosition = baseAnchoredPosition;
        }
    }
}
