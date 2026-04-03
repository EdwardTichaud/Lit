using UnityEngine;

[DisallowMultipleComponent]
public class HiddenRoomPortalRenderer : MonoBehaviour
{
    private static readonly string[] TexturePropertyCandidates =
    {
        "_BaseColorMap",
        "_BaseMap",
        "_MainTex",
        "_UnlitColorMap",
        "_EmissiveColorMap"
    };

    [SerializeField] private HiddenRoomBootstrap bootstrap;
    [SerializeField] private Camera portalCamera;
    [SerializeField] private Camera referenceCamera;
    [SerializeField] private Transform sourceAnchor;
    [SerializeField] private Transform targetAnchor;
    [SerializeField] private MeshRenderer targetRenderer;
    [SerializeField] private Renderer[] hiddenRenderers = new Renderer[0];
    [SerializeField, Min(128)] private int renderTextureWidth = 1280;
    [SerializeField, Min(128)] private int renderTextureHeight = 720;
    [SerializeField, Min(0.01f)] private float nearClipPlane = 0.03f;

    private RenderTexture renderTexture;
    private Material runtimeMaterial;
    private bool renderingActive;
    private bool missingReferenceLogged;

    public MeshRenderer TargetRenderer => targetRenderer;

    public void Configure(
        HiddenRoomBootstrap bootstrap,
        Camera portalCamera,
        Transform sourceAnchor,
        Transform targetAnchor,
        MeshRenderer targetRenderer,
        int renderTextureWidth,
        int renderTextureHeight)
    {
        this.bootstrap = bootstrap;
        this.portalCamera = portalCamera;
        this.sourceAnchor = sourceAnchor;
        this.targetAnchor = targetAnchor;
        this.targetRenderer = targetRenderer;
        this.renderTextureWidth = Mathf.Max(128, renderTextureWidth);
        this.renderTextureHeight = Mathf.Max(128, renderTextureHeight);
        runtimeMaterial = targetRenderer != null ? targetRenderer.sharedMaterial : null;
        ApplyTextureToMaterial(null);
    }

    public void SetPortalCamera(Camera camera)
    {
        portalCamera = camera;
    }

    public void SetReferenceCamera(Camera camera)
    {
        referenceCamera = camera;
    }

    public void SetTargetAnchor(Transform anchor)
    {
        targetAnchor = anchor;
    }

    public void SetHiddenRenderers(Renderer[] renderers)
    {
        hiddenRenderers = renderers ?? new Renderer[0];
    }

    public void SetRenderingActive(bool active)
    {
        renderingActive = active;
        if (targetRenderer != null)
        {
            targetRenderer.enabled = active;
        }
    }

    private void Reset()
    {
        targetRenderer = GetComponent<MeshRenderer>();
    }

    private void LateUpdate()
    {
        if (!renderingActive)
        {
            return;
        }

        Camera resolvedReferenceCamera = referenceCamera != null ? referenceCamera : bootstrap != null ? bootstrap.CurrentPlayerCamera : null;
        if (portalCamera == null || resolvedReferenceCamera == null || sourceAnchor == null || targetAnchor == null || targetRenderer == null)
        {
            if (!missingReferenceLogged)
            {
                ReportMissingReference();
                missingReferenceLogged = true;
            }

            return;
        }

        missingReferenceLogged = false;
        EnsureRenderTexture();
        RenderPortal(resolvedReferenceCamera);
    }

    private void EnsureRenderTexture()
    {
        int width = Mathf.Max(128, renderTextureWidth);
        int height = Mathf.Max(128, renderTextureHeight);

        if (renderTexture != null && renderTexture.width == width && renderTexture.height == height)
        {
            return;
        }

        ReleaseRenderTexture();

        renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
        {
            name = $"{name}_RenderTexture",
            antiAliasing = 1
        };
        renderTexture.Create();

        if (runtimeMaterial == null && targetRenderer != null)
        {
            runtimeMaterial = targetRenderer.sharedMaterial;
        }
        ApplyTextureToMaterial(renderTexture);
    }

    private void RenderPortal(Camera resolvedReferenceCamera)
    {
        Vector3 localPosition = sourceAnchor.InverseTransformPoint(resolvedReferenceCamera.transform.position);
        Quaternion localRotation = Quaternion.Inverse(sourceAnchor.rotation) * resolvedReferenceCamera.transform.rotation;

        portalCamera.transform.SetPositionAndRotation(
            targetAnchor.TransformPoint(localPosition),
            targetAnchor.rotation * localRotation);

        portalCamera.fieldOfView = resolvedReferenceCamera.fieldOfView;
        portalCamera.orthographic = resolvedReferenceCamera.orthographic;
        portalCamera.orthographicSize = resolvedReferenceCamera.orthographicSize;
        portalCamera.nearClipPlane = nearClipPlane;
        portalCamera.farClipPlane = resolvedReferenceCamera.farClipPlane;
        portalCamera.clearFlags = resolvedReferenceCamera.clearFlags;
        portalCamera.backgroundColor = resolvedReferenceCamera.backgroundColor;
        portalCamera.cullingMask = resolvedReferenceCamera.cullingMask;
        portalCamera.targetTexture = renderTexture;

        int hiddenCount = hiddenRenderers != null ? hiddenRenderers.Length : 0;
        bool[] previousEnabledStates = hiddenCount > 0 ? new bool[hiddenCount] : null;
        for (int i = 0; i < hiddenCount; i++)
        {
            Renderer current = hiddenRenderers[i];
            if (current == null)
            {
                continue;
            }

            previousEnabledStates[i] = current.enabled;
            current.enabled = false;
        }

        try
        {
            portalCamera.Render();
        }
        finally
        {
            for (int i = 0; i < hiddenCount; i++)
            {
                Renderer current = hiddenRenderers[i];
                if (current != null)
                {
                    current.enabled = previousEnabledStates[i];
                }
            }
        }
    }

    private void ApplyTextureToMaterial(Texture texture)
    {
        Material material = runtimeMaterial != null ? runtimeMaterial : targetRenderer != null ? targetRenderer.sharedMaterial : null;
        if (material == null)
        {
            return;
        }

        for (int i = 0; i < TexturePropertyCandidates.Length; i++)
        {
            string candidate = TexturePropertyCandidates[i];
            if (material.HasProperty(candidate))
            {
                material.SetTexture(candidate, texture);
            }
        }

        material.mainTexture = texture;
    }

    private void ReportMissingReference()
    {
        string message = $"HiddenRoomPortalRenderer '{name}': reference manquante. "
            + "Verifier portalCamera, sourceAnchor, targetAnchor, targetRenderer et la camera joueur.";

        if (bootstrap != null)
        {
            bootstrap.ReportMissingReference(message);
        }
        else
        {
            Debug.LogWarning(message, this);
        }
    }

    private void ReleaseRenderTexture()
    {
        if (renderTexture == null)
        {
            return;
        }

        renderTexture.Release();
        Destroy(renderTexture);
        renderTexture = null;
    }

    private void OnDisable()
    {
        if (targetRenderer != null)
        {
            targetRenderer.enabled = false;
        }
    }

    private void OnDestroy()
    {
        ReleaseRenderTexture();
    }
}
