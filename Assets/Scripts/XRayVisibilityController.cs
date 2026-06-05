using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(1010)]
public sealed class XRayVisibilityController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private XRayMaskFollower maskFollower;

    [Header("Player Auto Detection")]
    [SerializeField] private string playerLayerName = "Player";
    [SerializeField] private Transform playerTarget;
    [SerializeField] private SkinnedMeshRenderer playerRenderer;
    [SerializeField] private string preferredSkinnedMeshRendererName = "CC_Base_Body";
    [SerializeField] private Vector3 targetOffset = new(0f, 1.2f, 0f);

    [Header("Detection")]
    [SerializeField] private LayerMask obstacleDetectionMask;
    [SerializeField] private string obstacleLayerName = "Obstacle";
    [SerializeField, Tooltip("Layers supplementaires conserves pour les anciens objets de scene qui ont ete serialises sur un layer maintenant non nomme.")]
    private int[] legacyObstacleLayerIndices = { 19 };
    [SerializeField] private bool useSphereCast = true;
    [SerializeField] private float sphereCastRadius = 0.25f;
    [SerializeField] private int maxHits = 32;
    [SerializeField, Tooltip("Ignore les objets qui participent deja aux interactions ou a l'Outline runtime, afin de ne pas casser leur layer temporaire.")]
    private bool ignoreInteractableAndOutlineObstacles = true;

    [Header("Renderer Occlusion")]
    [SerializeField, Tooltip("Si actif, le masque XRay utilise plusieurs points du SkinnedMeshRenderer au lieu du seul pivot joueur.")]
    private bool requireFullRendererOcclusion = true;
    [SerializeField, Range(1, 13)] private int rendererOcclusionSampleCount = 9;
    [SerializeField, Range(0.1f, 1f), Tooltip("Part des points du renderer qui doivent etre masques avant d'activer le XRay.")]
    private float rendererOcclusionRequiredSampleRatio = 0.5f;
    [SerializeField, Range(0.25f, 1f)] private float boundsSampleScale = 0.85f;
    [SerializeField, Min(0f), Tooltip("Rayon optionnel pour les tests de visibilite du renderer. 0 = raycast fin.")]
    private float visibilityProbeRadius = 0f;

    [Header("Layer Change")]
    [SerializeField] private bool applyToChildren = false;

    [Header("Debug")]
    [SerializeField] private bool debugDraw = true;
    [SerializeField, Tooltip("Log si aucune cible joueur n'est trouvee. Desactive par defaut car la squad peut etre instanciee apres XRayCam.")]
    private bool warnWhenPlayerTargetMissing;

    private const QueryTriggerInteraction TriggerInteraction = QueryTriggerInteraction.Ignore;

    private readonly HashSet<GameObject> currentObstacles = new();
    private readonly HashSet<GameObject> previousObstacles = new();
    private readonly Dictionary<GameObject, int> originalLayers = new();
    private readonly Dictionary<GameObject, int> rendererOcclusionCounts = new();
    private readonly HashSet<GameObject> sampleObstacles = new();
    private readonly List<Transform> transformBuffer = new(64);
    private readonly List<SkinnedMeshRenderer> rendererBuffer = new(16);

    private RaycastHit[] hitBuffer;
    private int obstacleLayer = -1;
    private int playerLayer = -1;
    private int cachedMaxHits;
    private bool warnedMissingReferences;
    private bool warnedMissingRenderer;
    private bool warnedMissingPlayer;
    private Transform ignoredPlayerRoot;

    private static readonly Vector2[] RendererSampleOffsets =
    {
        new(0f, 0f),
        new(0f, 1f),
        new(0f, -1f),
        new(-1f, 0f),
        new(1f, 0f),
        new(-0.8f, 0.8f),
        new(0.8f, 0.8f),
        new(-0.8f, -0.8f),
        new(0.8f, -0.8f),
        new(-0.45f, 0.45f),
        new(0.45f, 0.45f),
        new(-0.45f, -0.45f),
        new(0.45f, -0.45f),
    };

    private void Awake()
    {
        ResolveReferences();
        EnsureHitBuffer();
        ResolveLayers();
        ResolvePlayerRuntimeReferences();
        ConfigureMaskFollower();
    }

    private void OnEnable()
    {
        ResolveReferences();
        EnsureHitBuffer();
        ResolveLayers();
        ResolvePlayerRuntimeReferences();
        ConfigureMaskFollower();
    }

    private void LateUpdate()
    {
        ResolveReferences();

        if (!HasRequiredReferences())
        {
            HideMask();
            return;
        }

        if (!ResolveLayers())
        {
            HideMask();
            return;
        }

        EnsureHitBuffer();
        ResolvePlayerRuntimeReferences();
        ConfigureMaskFollower();
        DetectObstacles();
        ApplyAndRestoreLayers();

        maskFollower.Visible = currentObstacles.Count > 0;
    }

    private void ResolveReferences()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (maskFollower == null)
        {
            maskFollower = GetComponentInChildren<XRayMaskFollower>(true);
        }

        RefreshPlayerTarget();
    }

    private bool HasRequiredReferences()
    {
        if (mainCamera != null && maskFollower != null && IsUsablePlayerTarget(playerTarget))
        {
            warnedMissingReferences = false;
            return true;
        }

        if (!warnedMissingReferences)
        {
            Debug.LogWarning(
                $"[XRay] References manquantes sur {name}. " +
                $"mainCamera={(mainCamera ? mainCamera.name : "null")}, " +
                $"maskFollower={(maskFollower ? maskFollower.name : "null")}, " +
                $"playerTarget={(playerTarget ? playerTarget.name : "null")}.");
            warnedMissingReferences = true;
        }

        return false;
    }

    private bool ResolveLayers()
    {
        playerLayer = LayerMask.NameToLayer(playerLayerName);
        obstacleLayer = LayerMask.NameToLayer(obstacleLayerName);

        if (playerLayer < 0)
        {
            Debug.LogError($"[XRay] Layer joueur introuvable: '{playerLayerName}'.");
            return false;
        }

        if (obstacleLayer < 0)
        {
            Debug.LogError($"[XRay] Layer obstacle introuvable: '{obstacleLayerName}'.");
            return false;
        }

        int playerBit = 1 << playerLayer;
        if ((obstacleDetectionMask.value & playerBit) != 0)
        {
            obstacleDetectionMask.value &= ~playerBit;
            Debug.LogWarning($"[XRay] Le layer '{playerLayerName}' a ete retire de obstacleDetectionMask.");
        }

        return true;
    }

    private void EnsureHitBuffer()
    {
        maxHits = Mathf.Clamp(maxHits, 1, 256);
        if (hitBuffer != null && cachedMaxHits == maxHits)
        {
            return;
        }

        cachedMaxHits = maxHits;
        hitBuffer = new RaycastHit[cachedMaxHits];
    }

    private void RefreshPlayerTarget()
    {
        Transform controlledTarget = ResolveControlledCharacterTarget();
        if (controlledTarget != null)
        {
            AssignPlayerTarget(controlledTarget, "personnage local controle");
            return;
        }

        if (IsUsablePlayerTarget(playerTarget))
        {
            warnedMissingPlayer = false;
            return;
        }

        AssignPlayerTarget(FindPlayerTargetInScene(), "detection scene");
    }

    private Transform ResolveControlledCharacterTarget()
    {
        GameObject controlled = LocalPlayerUtils.GetControlledCharacter();
        if (controlled == null || !controlled.activeInHierarchy)
        {
            return null;
        }

        SquadCharacterController controller = controlled.GetComponent<SquadCharacterController>();
        if (controller == null)
        {
            controller = controlled.GetComponentInParent<SquadCharacterController>();
        }

        return controller != null ? controller.transform : controlled.transform;
    }

    private void AssignPlayerTarget(Transform newTarget, string source)
    {
        if (newTarget == null)
        {
            if (!IsUsablePlayerTarget(playerTarget))
            {
                RestoreAllLayers();
                playerTarget = null;
                playerRenderer = null;
                ignoredPlayerRoot = null;
            }

            if (warnWhenPlayerTargetMissing && !warnedMissingPlayer)
            {
                Debug.LogWarning($"[XRay] Aucun player target actif trouve pour {name}.");
                warnedMissingPlayer = true;
            }

            return;
        }

        warnedMissingPlayer = false;

        if (playerTarget == newTarget)
        {
            return;
        }

        RestoreAllLayers();
        playerTarget = newTarget;
        playerRenderer = null;
        ignoredPlayerRoot = null;
        warnedMissingRenderer = false;

        Debug.Log($"[XRay] Player target detecte automatiquement ({source}): {newTarget.name}.");
    }

    private bool IsUsablePlayerTarget(Transform candidate)
    {
        return candidate != null && candidate.gameObject.activeInHierarchy;
    }

    private Transform FindPlayerTargetInScene()
    {
        Transform taggedPlayer = FindTaggedPlayerTarget();
        if (taggedPlayer != null)
        {
            return taggedPlayer;
        }

        SquadCharacterController[] controllers = FindObjectsByType<SquadCharacterController>(FindObjectsInactive.Exclude);
        for (int i = 0; i < controllers.Length; i++)
        {
            SquadCharacterController controller = controllers[i];
            if (controller != null && controller.gameObject.activeInHierarchy)
            {
                return controller.transform;
            }
        }

        return FindPlayerTargetByLayer();
    }

    private Transform FindTaggedPlayerTarget()
    {
        GameObject[] taggedPlayers = GameObject.FindGameObjectsWithTag("Player");
        Transform fallback = null;

        for (int i = 0; i < taggedPlayers.Length; i++)
        {
            GameObject candidate = taggedPlayers[i];
            if (candidate == null || !candidate.activeInHierarchy)
            {
                continue;
            }

            if (candidate.TryGetComponent(out SquadCharacterController _))
            {
                return candidate.transform;
            }

            fallback ??= candidate.transform;
        }

        return fallback;
    }

    private Transform FindPlayerTargetByLayer()
    {
        int layer = LayerMask.NameToLayer(playerLayerName);
        if (layer < 0)
        {
            return null;
        }

        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Exclude);
        Transform fallback = null;

        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate == null || candidate.gameObject.layer != layer)
            {
                continue;
            }

            fallback ??= candidate;
            Transform parent = candidate.parent;
            if (parent == null || parent.gameObject.layer != layer)
            {
                return candidate;
            }
        }

        return fallback;
    }

    private void ConfigureMaskFollower()
    {
        if (maskFollower == null)
        {
            return;
        }

        maskFollower.SetMainCamera(mainCamera);
        maskFollower.SetTarget(playerTarget);
        maskFollower.WorldOffset = targetOffset;
    }

    private void DetectObstacles()
    {
        previousObstacles.Clear();
        foreach (GameObject obstacle in currentObstacles)
        {
            if (obstacle != null)
            {
                previousObstacles.Add(obstacle);
            }
        }

        currentObstacles.Clear();

        if (requireFullRendererOcclusion)
        {
            DetectFullRendererOcclusion();
            return;
        }

        DetectCenterLineOcclusion();
    }

    private void DetectFullRendererOcclusion()
    {
        if (playerRenderer == null)
        {
            if (!warnedMissingRenderer)
            {
                Debug.LogWarning(
                    $"[XRay] Aucun SkinnedMeshRenderer cible trouve pour {name}. " +
                    $"Assigne playerRenderer ou verifie le nom '{preferredSkinnedMeshRendererName}'. Fallback sur le pivot joueur.");
                warnedMissingRenderer = true;
            }

            DetectCenterLineOcclusion();
            return;
        }

        warnedMissingRenderer = false;

        Bounds bounds = playerRenderer.bounds;
        if (bounds.size.sqrMagnitude <= 0.0001f)
        {
            DetectCenterLineOcclusion();
            return;
        }

        Vector3 origin = mainCamera.transform.position;
        int sampleCount = Mathf.Clamp(rendererOcclusionSampleCount, 1, RendererSampleOffsets.Length);
        int requiredOccludedSamples = Mathf.Clamp(
            Mathf.CeilToInt(sampleCount * rendererOcclusionRequiredSampleRatio),
            1,
            sampleCount);
        int occludedSampleCount = 0;

        rendererOcclusionCounts.Clear();
        sampleObstacles.Clear();

        for (int i = 0; i < sampleCount; i++)
        {
            Vector3 samplePoint = GetRendererSamplePoint(bounds, i);
            sampleObstacles.Clear();
            bool occluded = TryCollectOccludingHits(origin, samplePoint, visibilityProbeRadius, sampleObstacles, out Color debugColor);

            if (debugDraw)
            {
                Debug.DrawLine(origin, samplePoint, debugColor);
            }

            if (!occluded)
            {
                continue;
            }

            occludedSampleCount++;
            foreach (GameObject obstacle in sampleObstacles)
            {
                if (rendererOcclusionCounts.TryGetValue(obstacle, out int count))
                {
                    rendererOcclusionCounts[obstacle] = count + 1;
                }
                else
                {
                    rendererOcclusionCounts.Add(obstacle, 1);
                }
            }
        }

        if (occludedSampleCount < requiredOccludedSamples)
        {
            currentObstacles.Clear();
            return;
        }

        foreach (KeyValuePair<GameObject, int> pair in rendererOcclusionCounts)
        {
            if (pair.Key != null && pair.Value > 0)
            {
                currentObstacles.Add(pair.Key);
            }
        }
    }

    private void DetectCenterLineOcclusion()
    {
        Vector3 origin = mainCamera.transform.position;
        Vector3 target = playerTarget.position + targetOffset;
        TryCollectOccludingHits(origin, target, useSphereCast ? sphereCastRadius : 0f, currentObstacles, out _);
    }

    private bool TryCollectOccludingHits(
        Vector3 origin,
        Vector3 target,
        float radius,
        HashSet<GameObject> destination,
        out Color debugColor)
    {
        debugColor = Color.green;

        Vector3 direction = target - origin;
        float distance = direction.magnitude;

        if (distance <= 0.01f)
        {
            return false;
        }

        direction /= distance;

        int effectiveMask = BuildEffectiveObstacleMask();

        int hitCount = radius > 0f
            ? Physics.SphereCastNonAlloc(origin, radius, direction, hitBuffer, distance, effectiveMask, TriggerInteraction)
            : Physics.RaycastNonAlloc(origin, direction, hitBuffer, distance, effectiveMask, TriggerInteraction);

        bool occluded = false;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = hitBuffer[i].collider;
            if (hitCollider == null || IsColliderIgnored(hitCollider))
            {
                continue;
            }

            GameObject obstacle = ResolveLayerChangeTarget(hitCollider);
            if (obstacle == null ||
                obstacle.layer == playerLayer ||
                IsTransformUnderIgnoredRoot(obstacle.transform) ||
                ShouldIgnoreObstacle(hitCollider, obstacle))
            {
                continue;
            }

            destination.Add(obstacle);
            occluded = true;
        }

        if (debugDraw)
        {
            debugColor = occluded ? Color.red : Color.green;
        }

        return occluded;
    }

    private int BuildEffectiveObstacleMask()
    {
        int effectiveMask = obstacleDetectionMask.value;

        if (obstacleLayer >= 0)
        {
            effectiveMask |= 1 << obstacleLayer;
        }

        if (legacyObstacleLayerIndices != null)
        {
            for (int i = 0; i < legacyObstacleLayerIndices.Length; i++)
            {
                int layer = legacyObstacleLayerIndices[i];
                if (layer >= 0 && layer <= 31)
                {
                    effectiveMask |= 1 << layer;
                }
            }
        }

        if (playerLayer >= 0)
        {
            effectiveMask &= ~(1 << playerLayer);
        }

        return effectiveMask;
    }

    private bool ShouldIgnoreObstacle(Collider hitCollider, GameObject obstacle)
    {
        if (!ignoreInteractableAndOutlineObstacles)
        {
            return false;
        }

        if (CharacterInteractionDetection.ResolveTarget(hitCollider) != null)
        {
            return true;
        }

        return HasRuntimeOutlineTarget(hitCollider, obstacle);
    }

    private static bool HasRuntimeOutlineTarget(Collider hitCollider, GameObject obstacle)
    {
        if (hitCollider != null && hitCollider.GetComponentInParent<RuntimeOutlineTarget>() != null)
        {
            return true;
        }

        return obstacle != null && obstacle.GetComponentInChildren<RuntimeOutlineTarget>(true) != null;
    }

    private GameObject ResolveLayerChangeTarget(Collider hitCollider)
    {
        Renderer renderer = hitCollider.GetComponentInParent<Renderer>();
        if (renderer != null && !IsTransformUnderIgnoredRoot(renderer.transform))
        {
            return renderer.gameObject;
        }

        if (hitCollider.attachedRigidbody != null &&
            !IsTransformUnderIgnoredRoot(hitCollider.attachedRigidbody.transform))
        {
            return hitCollider.attachedRigidbody.gameObject;
        }

        return hitCollider.gameObject;
    }

    private Vector3 GetRendererSamplePoint(Bounds bounds, int sampleIndex)
    {
        Vector2 offset = RendererSampleOffsets[Mathf.Clamp(sampleIndex, 0, RendererSampleOffsets.Length - 1)];
        Vector3 cameraRight = mainCamera.transform.right;
        Vector3 cameraUp = mainCamera.transform.up;
        Vector3 extents = bounds.extents;

        float rightRadius = ProjectBoundsExtents(extents, cameraRight) * boundsSampleScale;
        float upRadius = ProjectBoundsExtents(extents, cameraUp) * boundsSampleScale;

        return bounds.center
            + cameraRight * (offset.x * rightRadius)
            + cameraUp * (offset.y * upRadius);
    }

    private static float ProjectBoundsExtents(Vector3 extents, Vector3 axis)
    {
        return Mathf.Abs(axis.x) * extents.x
            + Mathf.Abs(axis.y) * extents.y
            + Mathf.Abs(axis.z) * extents.z;
    }

    private bool IsColliderIgnored(Collider hitCollider)
    {
        if (ignoredPlayerRoot == null)
        {
            return false;
        }

        Transform hitTransform = hitCollider.attachedRigidbody != null
            ? hitCollider.attachedRigidbody.transform
            : hitCollider.transform;

        return hitTransform != null && hitTransform.IsChildOf(ignoredPlayerRoot);
    }

    private void ApplyAndRestoreLayers()
    {
        foreach (GameObject obstacle in currentObstacles)
        {
            if (obstacle == null)
            {
                continue;
            }

            SaveOriginalLayers(obstacle);
            SetLayer(obstacle, obstacleLayer);
        }

        foreach (GameObject obstacle in previousObstacles)
        {
            if (obstacle != null && !currentObstacles.Contains(obstacle))
            {
                RestoreLayer(obstacle);
            }
        }
    }

    private void SaveOriginalLayers(GameObject root)
    {
        if (!applyToChildren)
        {
            if (!originalLayers.ContainsKey(root))
            {
                originalLayers.Add(root, root.layer);
            }

            return;
        }

        transformBuffer.Clear();
        root.GetComponentsInChildren(true, transformBuffer);

        for (int i = 0; i < transformBuffer.Count; i++)
        {
            GameObject child = transformBuffer[i].gameObject;
            if (!originalLayers.ContainsKey(child))
            {
                originalLayers.Add(child, child.layer);
            }
        }

        transformBuffer.Clear();
    }

    private void SetLayer(GameObject root, int layer)
    {
        if (!applyToChildren)
        {
            root.layer = layer;
            return;
        }

        transformBuffer.Clear();
        root.GetComponentsInChildren(true, transformBuffer);

        for (int i = 0; i < transformBuffer.Count; i++)
        {
            transformBuffer[i].gameObject.layer = layer;
        }

        transformBuffer.Clear();
    }

    private void RestoreLayer(GameObject root)
    {
        if (!applyToChildren)
        {
            if (originalLayers.TryGetValue(root, out int originalLayer))
            {
                root.layer = originalLayer;
                originalLayers.Remove(root);
            }

            return;
        }

        transformBuffer.Clear();
        root.GetComponentsInChildren(true, transformBuffer);

        for (int i = 0; i < transformBuffer.Count; i++)
        {
            GameObject child = transformBuffer[i].gameObject;
            if (originalLayers.TryGetValue(child, out int originalLayer))
            {
                child.layer = originalLayer;
                originalLayers.Remove(child);
            }
        }

        transformBuffer.Clear();
    }

    private void RestoreAllLayers()
    {
        foreach (KeyValuePair<GameObject, int> pair in originalLayers)
        {
            if (pair.Key != null)
            {
                pair.Key.layer = pair.Value;
            }
        }

        originalLayers.Clear();
        currentObstacles.Clear();
        previousObstacles.Clear();
    }

    private void HideMask()
    {
        if (maskFollower != null)
        {
            maskFollower.Visible = false;
        }
    }

    private void OnDisable()
    {
        RestoreAllLayers();

        if (maskFollower != null)
        {
            maskFollower.HideInstantly();
        }
    }

    private void OnDestroy()
    {
        RestoreAllLayers();
    }

    private void OnValidate()
    {
        maxHits = Mathf.Clamp(maxHits, 1, 256);
        sphereCastRadius = Mathf.Max(0.01f, sphereCastRadius);
        rendererOcclusionSampleCount = Mathf.Clamp(rendererOcclusionSampleCount, 1, RendererSampleOffsets.Length);
        rendererOcclusionRequiredSampleRatio = Mathf.Clamp(rendererOcclusionRequiredSampleRatio, 0.1f, 1f);
        boundsSampleScale = Mathf.Clamp(boundsSampleScale, 0.25f, 1f);
        visibilityProbeRadius = Mathf.Max(0f, visibilityProbeRadius);
    }

    private void ResolvePlayerRuntimeReferences()
    {
        UpdateIgnoredPlayerRoot();

        if (playerRenderer == null || !IsTransformUnderIgnoredRoot(playerRenderer.transform))
        {
            playerRenderer = FindPlayerRenderer();
        }
    }

    private void UpdateIgnoredPlayerRoot()
    {
        ignoredPlayerRoot = ResolvePlayerRoot(playerTarget);
    }

    private Transform ResolvePlayerRoot(Transform candidate)
    {
        if (candidate == null)
        {
            return null;
        }

        SquadCharacterController controller = candidate.GetComponentInParent<SquadCharacterController>();
        if (controller != null)
        {
            return controller.transform;
        }

        Transform root = candidate;
        Transform current = candidate;
        while (current != null)
        {
            if (IsPlayerMarker(current))
            {
                root = current;
            }

            current = current.parent;
        }

        return root;
    }

    private bool IsPlayerMarker(Transform candidate)
    {
        return candidate != null &&
               (candidate.CompareTag("Player") ||
                candidate.gameObject.layer == playerLayer ||
                candidate.GetComponent<SquadCharacterController>() != null);
    }

    private SkinnedMeshRenderer FindPlayerRenderer()
    {
        Transform searchRoot = ignoredPlayerRoot != null ? ignoredPlayerRoot : playerTarget;
        if (searchRoot == null)
        {
            return null;
        }

        rendererBuffer.Clear();
        searchRoot.GetComponentsInChildren(true, rendererBuffer);

        SkinnedMeshRenderer bestRenderer = null;
        float bestVolume = -1f;

        for (int i = 0; i < rendererBuffer.Count; i++)
        {
            SkinnedMeshRenderer candidate = rendererBuffer[i];
            if (candidate == null)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(preferredSkinnedMeshRendererName) &&
                candidate.name == preferredSkinnedMeshRendererName)
            {
                rendererBuffer.Clear();
                return candidate;
            }

            Bounds bounds = candidate.bounds;
            float volume = bounds.size.x * bounds.size.y * bounds.size.z;
            if (volume > bestVolume)
            {
                bestRenderer = candidate;
                bestVolume = volume;
            }
        }

        rendererBuffer.Clear();
        return bestRenderer;
    }

    private bool IsTransformUnderIgnoredRoot(Transform candidate)
    {
        return candidate != null && ignoredPlayerRoot != null && candidate.IsChildOf(ignoredPlayerRoot);
    }
}
