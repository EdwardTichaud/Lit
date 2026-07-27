using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(FallingPlayerController))]
public sealed class FallingGrappleController : MonoBehaviour
{
    [Header("Availability")]
    [SerializeField, Min(0f)] private float minimumDistance = 18f;
    [SerializeField, Min(0f)] private float maximumDistance = 65f;
    [SerializeField, Range(0.01f, 0.5f)] private float maximumScreenCenterDistance = 0.12f;
    [SerializeField] private string promptFormat = "[{0}]";

    [Header("Tether")]
    [SerializeField] private LineRenderer tetherRenderer;
    [SerializeField] private Vector3 originOffset = new Vector3(0f, 1.1f, 0.2f);
    [SerializeField, Min(0.05f)] private float tetherDurationSeconds = 0.32f;
    [SerializeField, Range(0.005f, 0.2f)] private float tetherWidth = 0.035f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClipSO grappleSfx;
    [SerializeField] private AudioClipSO grappleVoice;

    private FallingPlayerController player;
    private Camera targetCamera;
    private FallingGrapplePoint selectedPoint;
    private Transform tetherTarget;
    private float tetherEndsAt;
    private string bindingDisplay = "E";

    private void Awake()
    {
        player = GetComponent<FallingPlayerController>();
        targetCamera = Camera.main;
        bindingDisplay = player != null ? player.GetGrappleBindingDisplayString() : bindingDisplay;
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f;
            }
        }

        EnsureTetherRenderer();
    }

    private void OnEnable()
    {
        if (player != null)
        {
            player.GrappleRequested += TryUseSelectedPoint;
        }
    }

    private void OnDisable()
    {
        if (player != null)
        {
            player.GrappleRequested -= TryUseSelectedPoint;
        }

        ClearSelection();
        SetTetherVisible(false);
    }

    private void Update()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            if (targetCamera == null)
            {
                return;
            }
        }

        SelectBestPoint();
        UpdateTether();
    }

    private void SelectBestPoint()
    {
        if (player != null)
        {
            bindingDisplay = player.GetGrappleBindingDisplayString();
        }

        FallingGrapplePoint bestPoint = null;
        float bestScore = float.MaxValue;
        for (int i = 0; i < FallingGrapplePoint.Points.Count; i++)
        {
            FallingGrapplePoint point = FallingGrapplePoint.Points[i];
            if (point == null || !point.IsReady)
            {
                continue;
            }

            Vector3 delta = point.Anchor.position - transform.position;
            float distance = delta.magnitude;
            if (distance < minimumDistance || distance > maximumDistance)
            {
                continue;
            }

            Vector3 viewport = targetCamera.WorldToViewportPoint(point.Anchor.position);
            if (viewport.z <= 0f)
            {
                continue;
            }

            float screenDistance = Vector2.Distance(new Vector2(viewport.x, viewport.y), Vector2.one * 0.5f);
            if (screenDistance > maximumScreenCenterDistance)
            {
                continue;
            }

            float score = screenDistance * 100f + distance * 0.01f;
            if (score < bestScore)
            {
                bestScore = score;
                bestPoint = point;
            }
        }

        if (selectedPoint == bestPoint)
        {
            return;
        }

        ClearSelection();
        selectedPoint = bestPoint;
        if (selectedPoint != null)
        {
            selectedPoint.SetAvailable(true, string.Format(promptFormat, bindingDisplay));
        }
    }

    private void TryUseSelectedPoint()
    {
        if (selectedPoint == null || player == null || !player.TriggerGrappleImpulse())
        {
            return;
        }

        if (!selectedPoint.TryConsume())
        {
            return;
        }

        tetherTarget = selectedPoint.Anchor;
        tetherEndsAt = Time.time + tetherDurationSeconds;
        SetTetherVisible(true);
        PlayAudio(grappleSfx);
        PlayAudio(grappleVoice);
        selectedPoint = null;
    }

    private void ClearSelection()
    {
        if (selectedPoint != null)
        {
            selectedPoint.SetAvailable(false, string.Empty);
            selectedPoint = null;
        }
    }

    private void EnsureTetherRenderer()
    {
        if (tetherRenderer == null)
        {
            GameObject tetherObject = new GameObject("GrappleTether", typeof(LineRenderer));
            tetherObject.transform.SetParent(transform, false);
            tetherRenderer = tetherObject.GetComponent<LineRenderer>();
            Shader tetherShader = Shader.Find("HDRP/Unlit");
            if (tetherShader == null)
            {
                tetherShader = Shader.Find("Sprites/Default");
            }

            if (tetherShader != null)
            {
                tetherRenderer.material = new Material(tetherShader);
            }
            tetherRenderer.startColor = new Color(0.45f, 0.9f, 1f, 0.95f);
            tetherRenderer.endColor = new Color(0.7f, 0.98f, 1f, 0.15f);
        }

        tetherRenderer.positionCount = 2;
        tetherRenderer.startWidth = tetherWidth;
        tetherRenderer.endWidth = tetherWidth * 0.5f;
        SetTetherVisible(false);
    }

    private void UpdateTether()
    {
        if (tetherRenderer == null || tetherTarget == null || Time.time >= tetherEndsAt)
        {
            SetTetherVisible(false);
            tetherTarget = null;
            return;
        }

        tetherRenderer.SetPosition(0, transform.TransformPoint(originOffset));
        tetherRenderer.SetPosition(1, tetherTarget.position);
    }

    private void SetTetherVisible(bool visible)
    {
        if (tetherRenderer != null)
        {
            tetherRenderer.enabled = visible;
        }
    }

    private void PlayAudio(AudioClipSO clip)
    {
        if (audioSource != null && clip != null && clip.audioClip != null)
        {
            audioSource.PlayOneShot(clip.audioClip, clip.volume);
        }
    }
}
