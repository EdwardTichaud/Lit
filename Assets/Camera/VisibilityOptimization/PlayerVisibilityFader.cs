using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(1005)]
[DisallowMultipleComponent]
public sealed class PlayerVisibilityFader : MonoBehaviour
{
    private sealed class FadeState
    {
        public readonly List<Renderer> Renderers = new List<Renderer>();
        public float Alpha = 1f;
        public bool DetectedThisFrame;
    }

    [Header("References")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Transform playerTarget;
    [SerializeField] private bool fallbackToMainCamera = true;
    [SerializeField] private bool fallbackToControlledPlayer = true;

    [Header("Detection")]
    [SerializeField] private LayerMask obstacleMask = ~0;
    [SerializeField, Min(0.01f)] private float sphereCastRadius = 0.35f;
    [SerializeField, Min(1)] private int maxHits = 48;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;
    [SerializeField] private bool includeChildRenderers = true;
    [SerializeField] private Vector3 playerOffset = new Vector3(0f, 1.2f, 0f);

    [Header("Fade")]
    [SerializeField, Range(0.05f, 1f)] private float fadedAlpha = 0.28f;
    [SerializeField, Min(0f)] private float fadeInSpeed = 6f;
    [SerializeField, Min(0f)] private float fadeOutSpeed = 10f;
    [SerializeField] private bool writeBaseColor = true;
    [SerializeField] private bool writeColor = true;
    [SerializeField] private bool writeAlphaProperty = true;
    [SerializeField] private bool writeVisibilityFadeProperty = true;

    [Header("Debug")]
    [SerializeField] private bool debugDraw;
    [SerializeField] private bool logDetectedObstacles;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int AlphaId = Shader.PropertyToID("_Alpha");
    private static readonly int VisibilityFadeId = Shader.PropertyToID("_VisibilityFade");

    private readonly Dictionary<GameObject, FadeState> fadeStates = new Dictionary<GameObject, FadeState>();
    private readonly List<GameObject> removalBuffer = new List<GameObject>();
    private readonly List<Renderer> rendererBuffer = new List<Renderer>(32);
    private MaterialPropertyBlock propertyBlock;
    private RaycastHit[] hitBuffer;
    private int cachedMaxHits;

    private void Awake()
    {
        EnsureBuffers();
        ResolveReferences();
    }

    private void OnEnable()
    {
        EnsureBuffers();
        ResolveReferences();
    }

    private void OnDisable()
    {
        RestoreAll();
    }

    private void OnDestroy()
    {
        RestoreAll();
    }

    private void OnValidate()
    {
        sphereCastRadius = Mathf.Max(0.01f, sphereCastRadius);
        maxHits = Mathf.Clamp(maxHits, 1, 256);
        fadedAlpha = Mathf.Clamp(fadedAlpha, 0.05f, 1f);
        fadeInSpeed = Mathf.Max(0f, fadeInSpeed);
        fadeOutSpeed = Mathf.Max(0f, fadeOutSpeed);
    }

    private void LateUpdate()
    {
        EnsureBuffers();
        ResolveReferences();
        if (targetCamera == null || playerTarget == null)
        {
            FadeAllTowardVisible();
            return;
        }

        MarkAllUndetected();
        DetectObstacles();
        UpdateFadeStates();
    }

    private void ResolveReferences()
    {
        if (targetCamera == null && fallbackToMainCamera)
        {
            targetCamera = Camera.main;
        }

        if (playerTarget == null && fallbackToControlledPlayer)
        {
            GameObject controlled = LocalPlayerUtils.GetControlledCharacter();
            if (controlled != null && controlled.activeInHierarchy)
            {
                playerTarget = controlled.transform;
            }
        }
    }

    private void DetectObstacles()
    {
        Vector3 origin = targetCamera.transform.position;
        Vector3 target = playerTarget.position + playerOffset;
        Vector3 direction = target - origin;
        float distance = direction.magnitude;
        if (distance <= 0.01f)
        {
            return;
        }

        direction /= distance;
        int hitCount = Physics.SphereCastNonAlloc(
            origin,
            sphereCastRadius,
            direction,
            hitBuffer,
            distance,
            obstacleMask,
            triggerInteraction);

        if (debugDraw)
        {
            Debug.DrawLine(origin, target, hitCount > 0 ? Color.red : Color.green);
        }

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = hitBuffer[i].collider;
            if (hitCollider == null || IsPlayerCollider(hitCollider))
            {
                continue;
            }

            CameraVisibilityObstacle marker = hitCollider.GetComponentInParent<CameraVisibilityObstacle>();
            if (marker != null && !marker.UsableByCameraFade)
            {
                continue;
            }

            GameObject obstacleRoot = ResolveObstacleRoot(hitCollider, marker);
            if (obstacleRoot == null)
            {
                continue;
            }

            RegisterDetectedObstacle(obstacleRoot, marker);
        }
    }

    private GameObject ResolveObstacleRoot(Collider hitCollider, CameraVisibilityObstacle marker)
    {
        if (marker != null)
        {
            return marker.gameObject;
        }

        Renderer renderer = hitCollider.GetComponentInParent<Renderer>();
        if (renderer != null)
        {
            return renderer.gameObject;
        }

        return hitCollider.attachedRigidbody != null
            ? hitCollider.attachedRigidbody.gameObject
            : hitCollider.gameObject;
    }

    private void RegisterDetectedObstacle(GameObject obstacleRoot, CameraVisibilityObstacle marker)
    {
        if (!fadeStates.TryGetValue(obstacleRoot, out FadeState state))
        {
            state = new FadeState();
            fadeStates.Add(obstacleRoot, state);
            CollectRenderers(obstacleRoot, marker, state.Renderers);
        }

        state.DetectedThisFrame = true;
        bool protectRenderers = marker == null || marker.NeverCullWhenBetweenCameraAndPlayer;
        for (int i = 0; protectRenderers && i < state.Renderers.Count; i++)
        {
            CameraVisibilityProtection.RegisterRenderer(state.Renderers[i], this);
        }

        if (logDetectedObstacles)
        {
            Debug.Log($"[PlayerVisibilityFader] detected '{obstacleRoot.name}' renderers={state.Renderers.Count}", obstacleRoot);
        }
    }

    private void CollectRenderers(GameObject obstacleRoot, CameraVisibilityObstacle marker, List<Renderer> destination)
    {
        destination.Clear();
        bool collectChildren = marker != null ? marker.IncludeChildRenderers : includeChildRenderers;
        rendererBuffer.Clear();
        obstacleRoot.GetComponentsInChildren(includeInactive: true, rendererBuffer);
        for (int i = 0; i < rendererBuffer.Count; i++)
        {
            Renderer renderer = rendererBuffer[i];
            if (renderer != null)
            {
                destination.Add(renderer);
            }
        }

        if (!collectChildren && destination.Count > 1)
        {
            Renderer rootRenderer = obstacleRoot.GetComponent<Renderer>();
            destination.Clear();
            if (rootRenderer != null)
            {
                destination.Add(rootRenderer);
            }
        }

        rendererBuffer.Clear();
    }

    private void MarkAllUndetected()
    {
        foreach (FadeState state in fadeStates.Values)
        {
            state.DetectedThisFrame = false;
        }
    }

    private void UpdateFadeStates()
    {
        float deltaTime = Time.unscaledDeltaTime;
        removalBuffer.Clear();
        foreach (KeyValuePair<GameObject, FadeState> pair in fadeStates)
        {
            FadeState state = pair.Value;
            float targetAlpha = state.DetectedThisFrame ? fadedAlpha : 1f;
            float speed = state.DetectedThisFrame ? fadeOutSpeed : fadeInSpeed;
            state.Alpha = Mathf.MoveTowards(state.Alpha, targetAlpha, speed * deltaTime);
            ApplyAlpha(state.Renderers, state.Alpha);

            if (!state.DetectedThisFrame && state.Alpha >= 0.999f)
            {
                UnprotectRenderers(state.Renderers);
                removalBuffer.Add(pair.Key);
            }
        }

        for (int i = 0; i < removalBuffer.Count; i++)
        {
            fadeStates.Remove(removalBuffer[i]);
        }

        removalBuffer.Clear();
    }

    private void FadeAllTowardVisible()
    {
        MarkAllUndetected();
        UpdateFadeStates();
    }

    private void ApplyAlpha(List<Renderer> renderers, float alpha)
    {
        for (int i = 0; renderers != null && i < renderers.Count; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            renderer.GetPropertyBlock(propertyBlock);
            if (writeBaseColor && HasMaterialProperty(renderer, BaseColorId))
            {
                Color color = ResolveMaterialColor(renderer, BaseColorId, Color.white);
                color.a = alpha;
                propertyBlock.SetColor(BaseColorId, color);
            }

            if (writeColor && HasMaterialProperty(renderer, ColorId))
            {
                Color color = ResolveMaterialColor(renderer, ColorId, Color.white);
                color.a = alpha;
                propertyBlock.SetColor(ColorId, color);
            }

            if (writeAlphaProperty)
            {
                propertyBlock.SetFloat(AlphaId, alpha);
            }

            if (writeVisibilityFadeProperty)
            {
                propertyBlock.SetFloat(VisibilityFadeId, alpha);
            }

            renderer.SetPropertyBlock(propertyBlock);
        }
    }

    private static bool HasMaterialProperty(Renderer renderer, int propertyId)
    {
        if (renderer == null || renderer.sharedMaterial == null)
        {
            return false;
        }

        return renderer.sharedMaterial.HasProperty(propertyId);
    }

    private static Color ResolveMaterialColor(Renderer renderer, int propertyId, Color fallback)
    {
        if (renderer == null || renderer.sharedMaterial == null || !renderer.sharedMaterial.HasProperty(propertyId))
        {
            return fallback;
        }

        return renderer.sharedMaterial.GetColor(propertyId);
    }

    private bool IsPlayerCollider(Collider hitCollider)
    {
        if (playerTarget == null || hitCollider == null)
        {
            return false;
        }

        Transform hitTransform = hitCollider.attachedRigidbody != null
            ? hitCollider.attachedRigidbody.transform
            : hitCollider.transform;
        return hitTransform != null && hitTransform.IsChildOf(playerTarget);
    }

    private void RestoreAll()
    {
        foreach (FadeState state in fadeStates.Values)
        {
            if (state == null)
            {
                continue;
            }

            ApplyAlpha(state.Renderers, 1f);
            UnprotectRenderers(state.Renderers);
        }

        fadeStates.Clear();
        CameraVisibilityProtection.ClearOwner(this);
    }

    private void UnprotectRenderers(List<Renderer> renderers)
    {
        for (int i = 0; renderers != null && i < renderers.Count; i++)
        {
            CameraVisibilityProtection.UnregisterRenderer(renderers[i], this);
        }
    }

    private void EnsureBuffers()
    {
        maxHits = Mathf.Clamp(maxHits, 1, 256);
        if (hitBuffer == null || cachedMaxHits != maxHits)
        {
            cachedMaxHits = maxHits;
            hitBuffer = new RaycastHit[cachedMaxHits];
        }

        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }
    }
}
