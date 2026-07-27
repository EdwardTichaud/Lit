using UnityEngine;

[DisallowMultipleComponent]
public sealed class AttackLightAlert : MonoBehaviour
{
    [SerializeField, Min(0f)] private float durationSeconds = 0.25f;
    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField] private Camera targetCamera;

    private Transform visual;
    private Vector3 restingScale;
    private float elapsed;
    private bool isPlaying;

    private void Awake()
    {
        ResolveVisual();
    }

    private void OnEnable()
    {
        Play();
    }

    private void Update()
    {
        if (!isPlaying || visual == null)
        {
            return;
        }

        elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float progress = durationSeconds <= 0f ? 1f : Mathf.Clamp01(elapsed / durationSeconds);
        visual.localScale = restingScale * Mathf.Lerp(5f, 1f, progress);
        if (progress >= 1f)
        {
            isPlaying = false;
            Destroy(gameObject);
        }
    }

    private void LateUpdate()
    {
        if (visual == null)
        {
            return;
        }

        Camera cameraToFace = targetCamera != null ? targetCamera : Camera.main;
        if (cameraToFace == null)
        {
            return;
        }

        Vector3 directionToCamera = cameraToFace.transform.position - visual.position;
        if (directionToCamera.sqrMagnitude > 0.0001f)
        {
            visual.rotation = Quaternion.LookRotation(directionToCamera.normalized, cameraToFace.transform.up);
        }
    }

    /// <summary>
    /// Relance l'alerte depuis l'echelle x5 de son enfant direct.
    /// </summary>
    public void Play()
    {
        ResolveVisual();
        if (visual == null)
        {
            return;
        }

        elapsed = 0f;
        isPlaying = true;
        visual.localScale = restingScale * 5f;
    }

    private void ResolveVisual()
    {
        if (visual != null || transform.childCount == 0)
        {
            return;
        }

        visual = transform.GetChild(0);
        restingScale = visual.localScale;
    }
}
