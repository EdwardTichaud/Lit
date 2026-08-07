using UnityEngine;
using UnityEngine.UI;

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

    public void Configure(Color color, float duration, Camera camera)
    {
        durationSeconds = Mathf.Max(0.01f, duration);
        targetCamera = camera;
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].material.HasProperty("_BaseColor"))
            {
                renderers[i].material.SetColor("_BaseColor", color);
            }
            else if (renderers[i] != null && renderers[i].material.HasProperty("_Color"))
            {
                renderers[i].material.color = color;
            }
        }

        Image[] images = GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++) images[i].color = color;
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
