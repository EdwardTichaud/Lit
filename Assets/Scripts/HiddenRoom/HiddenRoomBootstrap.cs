using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class HiddenRoomBootstrap : MonoBehaviour
{
    private const string TargetSceneName = "Maison";
    private const string HiddenRoomName = "HiddenRoom";
    private const string RootName = "Root";
    private const float SharedTeleportCooldown = 0.35f;
    private const float PortalSurfaceOffset = 0.015f;

    [Header("Optional Explicit References")]
    [Tooltip("Camera joueur explicite. Laisse vide pour utiliser Camera.main puis CameraController.")]
    [SerializeField] private Camera playerCamera;
    [Tooltip("Racine joueur explicite. Laisse vide pour utiliser LocalPlayerUtils puis SquadManager.")]
    [SerializeField] private Transform playerRoot;

    [Header("Maison Door Placement")]
    [Tooltip("Position approximee du centre de la grande porte de Maison. Ajuster si necessaire.")]
    [SerializeField] private Vector3 exteriorPortalPosition = new Vector3(13.65f, 0f, -4.55f);
    [Tooltip("Rotation de la porte exterieure. Par defaut, l'exterieur regarde vers +X.")]
    [SerializeField] private Vector3 exteriorPortalEuler = new Vector3(0f, 90f, 0f);

    [Header("Portal Dimensions")]
    [SerializeField, Min(0.5f)] private float portalWidth = 2.6f;
    [SerializeField, Min(1.8f)] private float portalHeight = 3.4f;
    [SerializeField, Min(0.25f)] private float triggerDepth = 1.1f;
    [SerializeField, Min(0f)] private float triggerSidePadding = 0.35f;
    [SerializeField, Min(0.25f)] private float arrivalOffset = 0.9f;
    [SerializeField, Min(128)] private int renderTextureWidth = 1280;
    [SerializeField, Min(128)] private int renderTextureHeight = 720;

    [Header("Hidden Room")]
    [SerializeField] private Vector3 hiddenRoomPosition = new Vector3(0f, -250f, 0f);
    [SerializeField, Min(3f)] private float roomWidth = 7f;
    [SerializeField, Min(3f)] private float roomDepth = 8f;
    [SerializeField, Min(2f)] private float roomHeight = 4f;
    [SerializeField, Min(0.05f)] private float wallThickness = 0.2f;
    [SerializeField] private Color roomColor = new Color(0.55f, 0.62f, 0.67f, 1f);
    [SerializeField] private Color portalColor = Color.white;
    [SerializeField] private Color roomLightColor = new Color(1f, 0.95f, 0.84f, 1f);
    [SerializeField, Min(0f)] private float roomLightIntensity = 18f;
    [SerializeField, Min(0.5f)] private float roomLightRange = 12f;

    [Header("Diagnostics")]
    [SerializeField] private bool logMissingReferences = true;

    [Header("Runtime References")]
    [SerializeField] private Camera sharedPortalCamera;
    [SerializeField] private HiddenRoomPortalRenderer exteriorPortalRenderer;
    [SerializeField] private HiddenRoomPortalRenderer interiorPortalRenderer;
    [SerializeField] private HiddenRoomPortalTeleporter exteriorPortalTeleporter;
    [SerializeField] private HiddenRoomPortalTeleporter interiorPortalTeleporter;
    [SerializeField] private BoxCollider roomBoundsCollider;

    private Transform exteriorPortalRoot;
    private Transform interiorPortalRoot;
    private Transform exteriorInboundAnchor;
    private Transform exteriorOutboundAnchor;
    private Transform exteriorArrivalAnchor;
    private Transform interiorInboundAnchor;
    private Transform interiorOutboundAnchor;
    private Transform interiorArrivalAnchor;
    private MeshRenderer exteriorPortalSurfaceRenderer;
    private MeshRenderer interiorPortalSurfaceRenderer;

    private Material runtimeRoomMaterial;
    private Material runtimePortalMaterial;
    private Camera resolvedPlayerCamera;
    private Transform resolvedPlayerRoot;
    private bool configured;
    private bool lastKnownPlayerInside;
    private float lastTeleportTime = float.NegativeInfinity;
    private Transform lastTeleportedRoot;
    private bool missingCameraLogged;
    private bool missingPlayerLogged;

    public Camera CurrentPlayerCamera => resolvedPlayerCamera;
    public Transform CurrentPlayerRoot => resolvedPlayerRoot;
    public Camera SharedPortalCamera => sharedPortalCamera;
    public bool LogMissingReferences => logMissingReferences;

    public void EnsureSceneSetup()
    {
        EnsureConfigured();
    }

    private void Awake()
    {
        if (Application.isPlaying && IsTargetScene(gameObject.scene))
        {
            EnsureConfigured();
        }
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying || !IsTargetScene(gameObject.scene))
        {
            return;
        }

        EnsureConfigured();
        ResolveRuntimeReferences();
        UpdateRendererState();
    }

    public bool TryTeleport(Transform travelerRoot, Transform destinationAnchor, HiddenRoomPortalTeleporter sourceTeleporter)
    {
        if (travelerRoot == null || destinationAnchor == null)
        {
            return false;
        }

        if (IsTravelerOnCooldown(travelerRoot))
        {
            return false;
        }

        Rigidbody rigidbodyTarget = travelerRoot.GetComponent<Rigidbody>();
        if (rigidbodyTarget == null)
        {
            rigidbodyTarget = travelerRoot.GetComponentInChildren<Rigidbody>(true);
        }

        CharacterController characterController = travelerRoot.GetComponent<CharacterController>();
        if (characterController == null)
        {
            characterController = travelerRoot.GetComponentInChildren<CharacterController>(true);
        }

        bool controllerWasEnabled = characterController != null && characterController.enabled;
        if (controllerWasEnabled)
        {
            characterController.enabled = false;
        }

        if (rigidbodyTarget != null)
        {
            rigidbodyTarget.position = destinationAnchor.position;
            rigidbodyTarget.rotation = destinationAnchor.rotation;
#if UNITY_6000_0_OR_NEWER
            rigidbodyTarget.linearVelocity = Vector3.zero;
#else
            rigidbodyTarget.velocity = Vector3.zero;
#endif
            rigidbodyTarget.angularVelocity = Vector3.zero;
        }

        travelerRoot.SetPositionAndRotation(destinationAnchor.position, destinationAnchor.rotation);

        if (controllerWasEnabled)
        {
            characterController.enabled = true;
        }

        SquadCharacterController squadController = travelerRoot.GetComponent<SquadCharacterController>();
        if (squadController == null)
        {
            squadController = travelerRoot.GetComponentInChildren<SquadCharacterController>(true);
        }

        if (squadController != null)
        {
            squadController.Stop();
        }

        lastTeleportedRoot = travelerRoot;
        lastTeleportTime = Time.unscaledTime;
        lastKnownPlayerInside = sourceTeleporter == exteriorPortalTeleporter;

        if (resolvedPlayerRoot == null && IsControlledTraveler(travelerRoot))
        {
            resolvedPlayerRoot = travelerRoot;
        }

        return true;
    }

    public bool IsControlledTraveler(Transform travelerRoot)
    {
        if (travelerRoot == null)
        {
            return false;
        }

        Transform controlled = ResolvePlayerRoot();
        if (controlled == null)
        {
            return travelerRoot.GetComponentInParent<SquadCharacterController>() != null
                || travelerRoot.GetComponentInChildren<SquadCharacterController>(true) != null;
        }

        return SharesHierarchy(travelerRoot, controlled);
    }

    public bool IsTravelerOnCooldown(Transform travelerRoot)
    {
        if (travelerRoot == null || lastTeleportedRoot == null)
        {
            return false;
        }

        if (Time.unscaledTime - lastTeleportTime > SharedTeleportCooldown)
        {
            return false;
        }

        return SharesHierarchy(travelerRoot, lastTeleportedRoot);
    }

    public void ReportMissingReference(string message)
    {
        if (!logMissingReferences || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        Debug.LogWarning(message, this);
    }

    private void EnsureConfigured()
    {
        if (configured)
        {
            return;
        }

        ValidateParameters();
        BindSceneObjects();
        ResolveRuntimeReferences();
        ConfigureExistingSceneObjects();

        configured = true;
    }

    private void BindSceneObjects()
    {
        BindPortalCamera();
        BindExteriorPortal();
        BindInteriorPortalAndRoom();
    }

    private void BindPortalCamera()
    {
        Transform portalCameraTransform = transform.Find("PortalCamera");
        sharedPortalCamera = portalCameraTransform != null ? portalCameraTransform.GetComponent<Camera>() : null;

        if (sharedPortalCamera == null)
        {
            return;
        }

        sharedPortalCamera.enabled = false;
        sharedPortalCamera.clearFlags = CameraClearFlags.Skybox;
        sharedPortalCamera.backgroundColor = Color.black;
        sharedPortalCamera.nearClipPlane = 0.03f;
        sharedPortalCamera.farClipPlane = 1000f;
        sharedPortalCamera.allowHDR = true;
        sharedPortalCamera.allowMSAA = false;
        sharedPortalCamera.targetTexture = null;

        AudioListener audioListener = portalCameraTransform.GetComponent<AudioListener>();
        if (audioListener != null)
        {
            DestroySafely(audioListener);
        }
    }

    private void BindExteriorPortal()
    {
        exteriorPortalRoot = transform.Find("ExteriorPortal");
        exteriorInboundAnchor = transform.Find("ExteriorPortal/Anchors/View_Inbound");
        exteriorOutboundAnchor = transform.Find("ExteriorPortal/Anchors/View_Outbound");
        exteriorArrivalAnchor = transform.Find("ExteriorPortal/Anchors/Arrival_FromRoom");

        Transform surfaceTransform = transform.Find("ExteriorPortal/PortalSurface");
        exteriorPortalSurfaceRenderer = surfaceTransform != null ? surfaceTransform.GetComponent<MeshRenderer>() : null;
        exteriorPortalRenderer = surfaceTransform != null ? surfaceTransform.GetComponent<HiddenRoomPortalRenderer>() : null;

        Transform triggerTransform = transform.Find("ExteriorPortal/Trigger_FromOutside");
        exteriorPortalTeleporter = triggerTransform != null ? triggerTransform.GetComponent<HiddenRoomPortalTeleporter>() : null;
    }

    private void BindInteriorPortalAndRoom()
    {
        interiorPortalRoot = transform.Find("InteriorPortal");
        interiorInboundAnchor = transform.Find("InteriorPortal/Anchors/View_Inbound");
        interiorOutboundAnchor = transform.Find("InteriorPortal/Anchors/View_Outbound");
        interiorArrivalAnchor = transform.Find("InteriorPortal/Anchors/Arrival_FromOutside");

        Transform surfaceTransform = transform.Find("InteriorPortal/PortalSurface");
        interiorPortalSurfaceRenderer = surfaceTransform != null ? surfaceTransform.GetComponent<MeshRenderer>() : null;
        interiorPortalRenderer = surfaceTransform != null ? surfaceTransform.GetComponent<HiddenRoomPortalRenderer>() : null;

        Transform triggerTransform = transform.Find("InteriorPortal/Trigger_FromRoom");
        interiorPortalTeleporter = triggerTransform != null ? triggerTransform.GetComponent<HiddenRoomPortalTeleporter>() : null;

        Transform boundsTransform = transform.Find("InteriorPortal/HiddenRoomInterior/RoomBounds");
        roomBoundsCollider = boundsTransform != null ? boundsTransform.GetComponent<BoxCollider>() : null;
    }

    private void ConfigureExistingSceneObjects()
    {
        if (exteriorPortalRenderer != null)
        {
            exteriorPortalRenderer.Configure(
                this,
                sharedPortalCamera,
                exteriorInboundAnchor,
                interiorInboundAnchor,
                exteriorPortalSurfaceRenderer,
                renderTextureWidth,
                renderTextureHeight);
        }

        if (interiorPortalRenderer != null)
        {
            interiorPortalRenderer.Configure(
                this,
                sharedPortalCamera,
                interiorOutboundAnchor,
                exteriorOutboundAnchor,
                interiorPortalSurfaceRenderer,
                renderTextureWidth,
                renderTextureHeight);
        }

        if (exteriorPortalRenderer != null && interiorPortalRenderer != null)
        {
            Renderer[] hiddenRenderers =
            {
                exteriorPortalRenderer.TargetRenderer,
                interiorPortalRenderer.TargetRenderer
            };

            exteriorPortalRenderer.SetHiddenRenderers(hiddenRenderers);
            interiorPortalRenderer.SetHiddenRenderers(hiddenRenderers);
        }

        if (exteriorPortalTeleporter != null)
        {
            exteriorPortalTeleporter.Configure(this, interiorArrivalAnchor, true, "ExteriorToInterior", SharedTeleportCooldown);
        }

        if (interiorPortalTeleporter != null)
        {
            interiorPortalTeleporter.Configure(this, exteriorArrivalAnchor, true, "InteriorToExterior", SharedTeleportCooldown);
        }
    }

    private void ResolveRuntimeReferences()
    {
        resolvedPlayerCamera = ResolvePlayerCamera();
        resolvedPlayerRoot = ResolvePlayerRoot();

        if (resolvedPlayerCamera == null)
        {
            if (!missingCameraLogged)
            {
                ReportMissingReference("HiddenRoomBootstrap: aucune camera joueur resolue. Assigne playerCamera si Camera.main ne suffit pas.");
                missingCameraLogged = true;
            }
        }
        else
        {
            missingCameraLogged = false;
        }

        if (resolvedPlayerRoot == null && resolvedPlayerCamera == null)
        {
            if (!missingPlayerLogged)
            {
                ReportMissingReference("HiddenRoomBootstrap: aucun joueur controle resolu. Assigne playerRoot si LocalPlayerUtils ne suffit pas.");
                missingPlayerLogged = true;
            }
        }
        else
        {
            missingPlayerLogged = false;
        }

        if (exteriorPortalRenderer != null)
        {
            exteriorPortalRenderer.SetPortalCamera(sharedPortalCamera);
            exteriorPortalRenderer.SetReferenceCamera(resolvedPlayerCamera);
        }

        if (interiorPortalRenderer != null)
        {
            interiorPortalRenderer.SetPortalCamera(sharedPortalCamera);
            interiorPortalRenderer.SetReferenceCamera(resolvedPlayerCamera);
        }
    }

    private void UpdateRendererState()
    {
        bool hasCamera = resolvedPlayerCamera != null;
        if (!hasCamera)
        {
            if (exteriorPortalRenderer != null)
            {
                exteriorPortalRenderer.SetRenderingActive(false);
            }

            if (interiorPortalRenderer != null)
            {
                interiorPortalRenderer.SetRenderingActive(false);
            }

            return;
        }

        Vector3 probePosition = resolvedPlayerRoot != null
            ? resolvedPlayerRoot.position
            : resolvedPlayerCamera.transform.position;

        if (roomBoundsCollider != null)
        {
            lastKnownPlayerInside = roomBoundsCollider.bounds.Contains(probePosition);
        }

        if (exteriorPortalRenderer != null)
        {
            exteriorPortalRenderer.SetRenderingActive(!lastKnownPlayerInside);
        }

        if (interiorPortalRenderer != null)
        {
            interiorPortalRenderer.SetRenderingActive(lastKnownPlayerInside);
        }
    }

    private Camera ResolvePlayerCamera()
    {
        if (playerCamera != null && playerCamera.isActiveAndEnabled && playerCamera != sharedPortalCamera)
        {
            return playerCamera;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null && mainCamera.isActiveAndEnabled && mainCamera != sharedPortalCamera)
        {
            return mainCamera;
        }

        CameraController controller = FindCameraController();
        if (controller != null && controller.mainCam != null && controller.mainCam.isActiveAndEnabled && controller.mainCam != sharedPortalCamera)
        {
            return controller.mainCam;
        }

#if UNITY_2023_1_OR_NEWER
        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
#else
        Camera[] cameras = FindObjectsOfType<Camera>();
#endif
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera candidate = cameras[i];
            if (candidate != null && candidate.isActiveAndEnabled && candidate != sharedPortalCamera)
            {
                return candidate;
            }
        }

        return null;
    }

    private Transform ResolvePlayerRoot()
    {
        if (playerRoot != null)
        {
            return playerRoot;
        }

        GameObject controlledCharacter = LocalPlayerUtils.GetControlledCharacter();
        if (controlledCharacter != null)
        {
            return controlledCharacter.transform;
        }

        if (SquadManager.Instance != null && SquadManager.Instance.currentCharacter != null)
        {
            return SquadManager.Instance.currentCharacter.transform;
        }

        CameraController controller = FindCameraController();
        if (controller != null && controller.mainCamCurrentTarget != null)
        {
            return controller.mainCamCurrentTarget;
        }

        return null;
    }

    private CameraController FindCameraController()
    {
#if UNITY_2023_1_OR_NEWER
        return FindFirstObjectByType<CameraController>();
#else
        return FindObjectOfType<CameraController>();
#endif
    }

    private MeshRenderer EnsurePortalSurface(Transform parent, string name, Vector3 localPosition, Quaternion localRotation)
    {
        Transform existing = parent.Find(name);
        GameObject surfaceObject;
        bool created = existing == null;
        if (existing == null)
        {
            surfaceObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            surfaceObject.name = name;
            surfaceObject.transform.SetParent(parent, false);
        }
        else
        {
            surfaceObject = existing.gameObject;
        }

        if (created)
        {
            ConfigureLocalTransform(surfaceObject.transform, localPosition, localRotation, new Vector3(portalWidth, portalHeight, 1f));
        }

        Collider collider = surfaceObject.GetComponent<Collider>();
        if (collider != null)
        {
            DestroySafely(collider);
        }

        MeshRenderer renderer = GetOrAdd<MeshRenderer>(surfaceObject);
        MeshFilter filter = GetOrAdd<MeshFilter>(surfaceObject);
        if (filter.sharedMesh == null)
        {
            GameObject template = GameObject.CreatePrimitive(PrimitiveType.Quad);
            MeshFilter templateFilter = template.GetComponent<MeshFilter>();
            filter.sharedMesh = templateFilter != null ? templateFilter.sharedMesh : null;
            DestroySafely(template);
        }

        EnsureUniquePortalSurfaceMaterial(renderer, name);
        return renderer;
    }

    private void EnsureRoomPrimitive(Transform parent, string name, PrimitiveType primitiveType, Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
    {
        Transform existing = parent.Find(name);
        GameObject primitiveObject;
        bool created = existing == null;
        if (existing == null)
        {
            primitiveObject = GameObject.CreatePrimitive(primitiveType);
            primitiveObject.name = name;
            primitiveObject.transform.SetParent(parent, false);
        }
        else
        {
            primitiveObject = existing.gameObject;
        }

        if (created)
        {
            ConfigureLocalTransform(primitiveObject.transform, localPosition, localRotation, localScale);
        }

        MeshRenderer renderer = primitiveObject.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = GetOrCreateRoomMaterial();
        }
    }

    private Material GetOrCreateRoomMaterial()
    {
        if (runtimeRoomMaterial != null)
        {
            return runtimeRoomMaterial;
        }

        runtimeRoomMaterial = CreateRuntimeMaterial(
            new string[] { "HDRP/Lit", "Standard", "Universal Render Pipeline/Lit" },
            roomColor);
        return runtimeRoomMaterial;
    }

    private Material GetOrCreatePortalMaterial()
    {
        if (runtimePortalMaterial != null)
        {
            return runtimePortalMaterial;
        }

        runtimePortalMaterial = CreateRuntimeMaterial(
            new string[] { "HDRP/Unlit", "Unlit/Texture", "Universal Render Pipeline/Unlit", "Standard" },
            portalColor);
        return runtimePortalMaterial;
    }

    private void EnsureUniquePortalSurfaceMaterial(MeshRenderer renderer, string surfaceName)
    {
        if (renderer == null)
        {
            return;
        }

        Material assignedMaterial = renderer.sharedMaterial;
        if (assignedMaterial != null && assignedMaterial != runtimePortalMaterial)
        {
            return;
        }

        Material instance = new Material(GetOrCreatePortalMaterial())
        {
            name = $"{surfaceName}_PortalMaterial"
        };
        ApplyColor(instance, portalColor);
        renderer.sharedMaterial = instance;
    }

    private static Material CreateRuntimeMaterial(string[] shaderNames, Color color)
    {
        Shader resolvedShader = null;
        for (int i = 0; i < shaderNames.Length; i++)
        {
            resolvedShader = Shader.Find(shaderNames[i]);
            if (resolvedShader != null)
            {
                break;
            }
        }

        if (resolvedShader == null)
        {
            resolvedShader = Shader.Find("Standard");
        }

        Material material = new Material(resolvedShader);
        ApplyColor(material, color);
        return material;
    }

    private static void ApplyColor(Material material, Color color)
    {
        if (material == null)
        {
            return;
        }

        string[] candidates = { "_BaseColor", "_Color", "_UnlitColor", "_TintColor" };
        for (int i = 0; i < candidates.Length; i++)
        {
            if (material.HasProperty(candidates[i]))
            {
                material.SetColor(candidates[i], color);
            }
        }
    }

    private static Transform EnsureChild(Transform parent, string name)
    {
        return EnsureChild(parent, name, out _);
    }

    private static Transform EnsureChild(Transform parent, string name, out bool created)
    {
        Transform child = parent.Find(name);
        if (child != null)
        {
            created = false;
            return child;
        }

        GameObject childObject = new GameObject(name);
        child = childObject.transform;
        child.SetParent(parent, false);
        created = true;
        return child;
    }

    private static Transform EnsureAnchor(Transform parent, string name, Vector3 localPosition, Quaternion localRotation)
    {
        Transform anchor = EnsureChild(parent, name, out bool created);
        if (created)
        {
            ConfigureLocalTransform(anchor, localPosition, localRotation, Vector3.one);
        }
        return anchor;
    }

    private static void ConfigureWorldTransform(Transform target, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        if (target == null)
        {
            return;
        }

        target.SetPositionAndRotation(position, rotation);
        target.localScale = scale;
    }

    private static void ConfigureLocalTransform(Transform target, Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
    {
        if (target == null)
        {
            return;
        }

        target.localPosition = localPosition;
        target.localRotation = localRotation;
        target.localScale = localScale;
    }

    private void ValidateParameters()
    {
        portalWidth = Mathf.Max(0.5f, portalWidth);
        portalHeight = Mathf.Max(1.8f, portalHeight);
        triggerDepth = Mathf.Max(0.25f, triggerDepth);
        arrivalOffset = Mathf.Max(0.25f, arrivalOffset);
        renderTextureWidth = Mathf.Max(128, renderTextureWidth);
        renderTextureHeight = Mathf.Max(128, renderTextureHeight);
        roomWidth = Mathf.Max(portalWidth + wallThickness * 2f + 0.5f, roomWidth);
        roomDepth = Mathf.Max(3f, roomDepth);
        roomHeight = Mathf.Max(portalHeight + wallThickness + 0.25f, roomHeight);
        wallThickness = Mathf.Max(0.05f, wallThickness);
        roomLightRange = Mathf.Max(0.5f, roomLightRange);
    }

    private bool SceneObjectsStillValid()
    {
        return sharedPortalCamera != null
            && exteriorPortalRenderer != null
            && interiorPortalRenderer != null
            && exteriorPortalTeleporter != null
            && interiorPortalTeleporter != null
            && exteriorInboundAnchor != null
            && exteriorOutboundAnchor != null
            && exteriorArrivalAnchor != null
            && interiorInboundAnchor != null
            && interiorOutboundAnchor != null
            && interiorArrivalAnchor != null
            && roomBoundsCollider != null;
    }

    private static bool SharesHierarchy(Transform a, Transform b)
    {
        if (a == null || b == null)
        {
            return false;
        }

        return a == b || a.IsChildOf(b) || b.IsChildOf(a);
    }

    private static bool IsTargetScene(Scene scene)
    {
        return scene.IsValid() && scene.isLoaded && string.Equals(scene.name, TargetSceneName, System.StringComparison.Ordinal);
    }

    private static T GetOrAdd<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        if (component == null)
        {
            component = gameObject.AddComponent<T>();
        }

        return component;
    }

    private static void DestroySafely(Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

    private void OnDestroy()
    {
        DestroySafely(runtimePortalMaterial);
        DestroySafely(runtimeRoomMaterial);
    }
}
