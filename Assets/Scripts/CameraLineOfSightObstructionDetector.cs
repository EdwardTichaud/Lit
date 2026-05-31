using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public sealed class CameraLineOfSightObstructionDetector : MonoBehaviour
{
    [Header("References")]
    [SerializeField, Tooltip("Camera qui regarde le joueur. Si vide, le composant utilise CameraController ou Camera.main.")]
    private Camera sourceCamera;
    [SerializeField, Tooltip("Point cible a garder visible. Si vide, le composant utilise le personnage controle local.")]
    private Transform playerTargetTransform;
    [SerializeField, Tooltip("Offset vise sur le personnage pour eviter de caster vers les pieds.")]
    private Vector3 targetOffset = new Vector3(0f, 1.6f, 0f);
    [SerializeField, Tooltip("Utilise les bounds des renderers du personnage pour placer la vignette a hauteur du corps plutot qu'au pivot.")]
    private bool useTargetRendererBounds = true;
    [SerializeField, Range(0f, 1f), Tooltip("Hauteur normalisee dans les bounds du personnage. 0.5 = centre, 0.6 = torse/haut du corps.")]
    private float targetBoundsHeightFactor = 0.58f;

    [Header("Detection")]
    [SerializeField] private LayerMask obstacleLayerMask = ~0;
    [SerializeField, Min(0f), Tooltip("0 = Raycast. > 0 = SphereCast plus stable dans les couloirs.")]
    private float detectionRadius = 0.18f;
    [SerializeField, Min(0f), Tooltip("Secondes entre deux checks. 0 = chaque frame.")]
    private float checkInterval = 0.03f;
    [SerializeField, Min(0f), Tooltip("Garde la vignette un court instant pour eviter les clignotements.")]
    private float obstacleGraceTime = 0.12f;
    [SerializeField] private bool ignoreTargetColliders = true;
    [SerializeField] private bool ignoreTriggers = true;
    [SerializeField, Tooltip("Tag optionnel pour des objets a ignorer sans changer leur layer.")]
    private string nonObstructingTag = "CameraNonObstructing";
    [SerializeField, Min(0f), Tooltip("Ignore les renderers tres petits si leur plus grande dimension est inferieure a cette valeur.")]
    private float minimumRendererBoundsSize = 0.2f;

    [Header("Outputs")]
    [SerializeField, Tooltip("Compatibilite legacy uniquement. Laisser desactive pour ne pas modifier les renderers des obstacles.")]
    private bool driveLegacyObstacleFader;
    [SerializeField, Tooltip("Compatibilite legacy uniquement. Laisser desactive: le nouveau masque HDRP remplace cette vignette.")]
    private bool driveLegacyVignette;
    [SerializeField] private CameraObstacleFader obstacleFader;
    [SerializeField] private CameraObstructionVignetteController vignetteController;

    [Header("Debug")]
    [SerializeField] private bool drawDebugRay = false;
    [SerializeField] private bool showCurrentObstructions = false;
    [SerializeField] private bool logObstructionChanges = false;

    private readonly RaycastHit[] hitBuffer = new RaycastHit[32];
    private readonly List<Renderer> obstructingRenderers = new List<Renderer>(32);
    private readonly List<Renderer> targetRendererBuffer = new List<Renderer>(16);
    private readonly HashSet<Renderer> obstructingRendererSet = new HashSet<Renderer>();
    private CameraController cameraController;
    private float nextCheckTime;
    private float lastObstructedTime = float.NegativeInfinity;
    private bool rawObstructed;
    private bool obstructionActive;

    public bool IsObstructed => obstructionActive;
    public int CurrentObstructionCount => obstructingRenderers.Count;

    private void Awake()
    {
        ResolveReferences();
        ValidateFields();
    }

    private void OnEnable()
    {
        nextCheckTime = 0f;
        rawObstructed = false;
        obstructionActive = false;
    }

    private void OnDisable()
    {
        obstructingRenderers.Clear();
        obstructingRendererSet.Clear();

        if (obstacleFader != null)
        {
            obstacleFader.RestoreAllImmediate();
        }

        if (vignetteController != null)
        {
            vignetteController.ClearImmediate();
        }
    }

    private void LateUpdate()
    {
        ResolveReferences();

        float deltaTime = Time.unscaledDeltaTime > 0f ? Time.unscaledDeltaTime : 1f / 60f;
        bool checkedThisFrame = false;
        if (checkInterval <= 0f || Time.unscaledTime >= nextCheckTime)
        {
            PerformObstructionCheck();
            nextCheckTime = Time.unscaledTime + checkInterval;
            checkedThisFrame = true;
        }

        bool nextActive = rawObstructed || Time.unscaledTime <= lastObstructedTime + obstacleGraceTime;
        if (nextActive != obstructionActive)
        {
            obstructionActive = nextActive;
            if (logObstructionChanges)
            {
                Debug.Log($"[CameraObstruction] active={obstructionActive} raw={rawObstructed} count={obstructingRenderers.Count}", this);
            }
        }
        else
        {
            obstructionActive = nextActive;
        }

        if (driveLegacyObstacleFader && obstacleFader != null)
        {
            obstacleFader.ApplyObstructions(rawObstructed ? obstructingRenderers : null, deltaTime);
        }

        if (driveLegacyVignette && vignetteController != null)
        {
            if (TryResolveTargetViewportCenter(out Vector2 viewportCenter))
            {
                vignetteController.SetObstructionWeight(obstructionActive ? 1f : 0f, viewportCenter);
            }
            else
            {
                vignetteController.SetObstructionWeight(obstructionActive ? 1f : 0f);
            }
        }

        if (drawDebugRay && (checkedThisFrame || checkInterval > 0f) && TryResolveLineOfSight(out Vector3 from, out Vector3 to, out _))
        {
            Debug.DrawLine(from, to, obstructionActive ? Color.red : Color.green);
        }
    }

    private void OnValidate()
    {
        ValidateFields();
    }

    public void Configure(Camera camera, CameraController controller)
    {
        if (sourceCamera == null)
        {
            sourceCamera = camera;
        }

        if (cameraController == null)
        {
            cameraController = controller;
        }

        ResolveReferences();
    }

    public void ApplyVisibilityMaskSettings(VisibilityMaskSettings maskSettings)
    {
        if (maskSettings == null)
        {
            return;
        }

        obstacleLayerMask = maskSettings.ObstacleLayers;
    }

    public bool TryGetTargetViewportCenter(out Vector2 viewportCenter)
    {
        return TryResolveTargetViewportCenter(out viewportCenter);
    }

    public bool TryGetLineOfSight(out Vector3 from, out Vector3 to, out Transform targetTransform)
    {
        return TryResolveLineOfSight(out from, out to, out targetTransform);
    }

    private void PerformObstructionCheck()
    {
        // This check only reports line-of-sight blockers; it never changes the camera pose.
        obstructingRenderers.Clear();
        obstructingRendererSet.Clear();
        rawObstructed = false;

        if (!TryResolveLineOfSight(out Vector3 from, out Vector3 to, out Transform targetTransform))
        {
            return;
        }

        Vector3 delta = to - from;
        float distance = delta.magnitude;
        if (distance <= 0.001f)
        {
            return;
        }

        Vector3 direction = delta / distance;
        QueryTriggerInteraction triggerMode = ignoreTriggers ? QueryTriggerInteraction.Ignore : QueryTriggerInteraction.Collide;
        int hitCount = detectionRadius > 0f
            ? Physics.SphereCastNonAlloc(from, detectionRadius, direction, hitBuffer, distance, obstacleLayerMask, triggerMode)
            : Physics.RaycastNonAlloc(from, direction, hitBuffer, distance, obstacleLayerMask, triggerMode);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = hitBuffer[i].collider;
            if (ShouldIgnoreCollider(hitCollider, targetTransform))
            {
                continue;
            }

            Renderer renderer = ResolveRenderer(hitCollider);
            if (renderer == null || ShouldIgnoreRenderer(renderer))
            {
                continue;
            }

            if (obstructingRendererSet.Add(renderer))
            {
                obstructingRenderers.Add(renderer);
            }
        }

        rawObstructed = obstructingRenderers.Count > 0;
        if (rawObstructed)
        {
            lastObstructedTime = Time.unscaledTime;
        }

        if (showCurrentObstructions && rawObstructed)
        {
            Debug.Log($"[CameraObstruction] renderers={DescribeRenderers()}", this);
        }
    }

    private bool TryResolveLineOfSight(out Vector3 from, out Vector3 to, out Transform targetTransform)
    {
        from = Vector3.zero;
        to = Vector3.zero;
        targetTransform = ResolveTargetTransform();

        Camera camera = ResolveCamera();
        if (camera == null || targetTransform == null)
        {
            return false;
        }

        from = camera.transform.position;
        to = ResolveTargetPoint(targetTransform);
        return true;
    }

    private bool TryResolveTargetViewportCenter(out Vector2 viewportCenter)
    {
        viewportCenter = new Vector2(0.5f, 0.5f);
        Camera camera = ResolveCamera();
        Transform targetTransform = ResolveTargetTransform();
        if (camera == null || targetTransform == null)
        {
            return false;
        }

        Vector3 viewportPoint = camera.WorldToViewportPoint(ResolveTargetPoint(targetTransform));
        if (viewportPoint.z <= 0f)
        {
            return false;
        }

        viewportCenter = new Vector2(viewportPoint.x, viewportPoint.y);
        return true;
    }

    private Vector3 ResolveTargetPoint(Transform targetTransform)
    {
        if (targetTransform == null)
        {
            return Vector3.zero;
        }

        if (useTargetRendererBounds && TryResolveTargetRendererPoint(targetTransform, out Vector3 rendererPoint))
        {
            return rendererPoint;
        }

        return targetTransform.position + targetOffset;
    }

    private bool TryResolveTargetRendererPoint(Transform targetTransform, out Vector3 point)
    {
        point = Vector3.zero;
        targetRendererBuffer.Clear();
        targetTransform.GetComponentsInChildren(includeInactive: false, targetRendererBuffer);

        bool hasBounds = false;
        Bounds combinedBounds = default;
        for (int i = 0; i < targetRendererBuffer.Count; i++)
        {
            Renderer renderer = targetRendererBuffer[i];
            if (renderer == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                combinedBounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                combinedBounds.Encapsulate(renderer.bounds);
            }
        }

        targetRendererBuffer.Clear();
        if (!hasBounds)
        {
            return false;
        }

        point = new Vector3(
            combinedBounds.center.x,
            Mathf.Lerp(combinedBounds.min.y, combinedBounds.max.y, targetBoundsHeightFactor),
            combinedBounds.center.z);
        return true;
    }

    private Transform ResolveTargetTransform()
    {
        if (playerTargetTransform != null)
        {
            return playerTargetTransform;
        }

        if (cameraController != null && cameraController.TryGetGameplayTarget(out Transform cameraTarget))
        {
            return cameraTarget;
        }

        if (SquadManager.Instance != null && SquadManager.Instance.currentCharacter != null)
        {
            return SquadManager.Instance.currentCharacter.transform;
        }

        return LocalPlayerContext.LocalCharacterRoot;
    }

    private Camera ResolveCamera()
    {
        if (sourceCamera != null)
        {
            return sourceCamera;
        }

        if (cameraController != null && cameraController.MainCamera != null)
        {
            sourceCamera = cameraController.MainCamera;
            return sourceCamera;
        }

        sourceCamera = Camera.main;
        return sourceCamera;
    }

    private void ResolveReferences()
    {
        if (cameraController == null)
        {
            cameraController = GetComponent<CameraController>();
            if (cameraController == null)
            {
                cameraController = GetComponentInParent<CameraController>();
            }
        }

        if (sourceCamera == null && cameraController != null)
        {
            sourceCamera = cameraController.MainCamera;
        }

        if (obstacleFader == null)
        {
            obstacleFader = GetComponent<CameraObstacleFader>();
        }

        if (vignetteController == null)
        {
            vignetteController = GetComponent<CameraObstructionVignetteController>();
        }
    }

    private bool ShouldIgnoreCollider(Collider collider, Transform targetTransform)
    {
        if (collider == null || !collider.enabled)
        {
            return true;
        }

        if (ignoreTargetColliders && targetTransform != null && collider.transform.IsChildOf(targetTransform))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(nonObstructingTag) && collider.gameObject.tag == nonObstructingTag)
        {
            return true;
        }

        return false;
    }

    private Renderer ResolveRenderer(Collider hitCollider)
    {
        if (hitCollider == null)
        {
            return null;
        }

        Renderer renderer = hitCollider.GetComponent<Renderer>();
        if (renderer != null)
        {
            return renderer;
        }

        return hitCollider.GetComponentInParent<Renderer>();
    }

    private bool ShouldIgnoreRenderer(Renderer renderer)
    {
        if (renderer == null)
        {
            return true;
        }

        if (minimumRendererBoundsSize <= 0f)
        {
            return false;
        }

        Vector3 size = renderer.bounds.size;
        float maxAxis = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
        return maxAxis < minimumRendererBoundsSize;
    }

    private string DescribeRenderers()
    {
        if (obstructingRenderers.Count == 0)
        {
            return "(none)";
        }

        string description = obstructingRenderers[0] != null ? obstructingRenderers[0].name : "(null)";
        for (int i = 1; i < obstructingRenderers.Count; i++)
        {
            description += ", ";
            description += obstructingRenderers[i] != null ? obstructingRenderers[i].name : "(null)";
        }

        return description;
    }

    private void ValidateFields()
    {
        detectionRadius = Mathf.Max(0f, detectionRadius);
        checkInterval = Mathf.Max(0f, checkInterval);
        obstacleGraceTime = Mathf.Max(0f, obstacleGraceTime);
        targetBoundsHeightFactor = Mathf.Clamp01(targetBoundsHeightFactor);
        minimumRendererBoundsSize = Mathf.Max(0f, minimumRendererBoundsSize);
    }
}
