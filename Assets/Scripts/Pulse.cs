using UnityEngine;

[DisallowMultipleComponent]
[ExecuteAlways]
public class Pulse : MonoBehaviour
{
    [Header("Pulse Settings")]
    [Tooltip("Oscillations per second.")]
    [SerializeField] private float frequency = 0.3f;
    [Tooltip("Scale amplitude (0.1 = 10% of base scale).")]
    [SerializeField] private float amplitude = 0.03f;
    [SerializeField] private bool useUnscaledTime = false;
    [SerializeField] private bool useInitialScale = true;

    private Vector3 baseScale = Vector3.one;

    private void Awake()
    {
        if (useInitialScale)
        {
            baseScale = transform.localScale;
        }
    }

    private void OnEnable()
    {
        if (useInitialScale)
        {
            baseScale = transform.localScale;
        }
    }

    private void Update()
    {
        float time = useUnscaledTime ? Time.unscaledTime : Time.time;
        float omega = Mathf.Max(0f, frequency) * Mathf.PI * 2f;
        float pulse = 1f + Mathf.Sin(time * omega) * Mathf.Max(0f, amplitude);
        transform.localScale = baseScale * pulse;
    }

    private void OnValidate()
    {
        frequency = Mathf.Max(0f, frequency);
        amplitude = Mathf.Max(0f, amplitude);
        if (!Application.isPlaying && useInitialScale)
        {
            baseScale = transform.localScale;
        }
    }
}
