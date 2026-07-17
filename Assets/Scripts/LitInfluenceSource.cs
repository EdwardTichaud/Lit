using System;
using System.Collections.Generic;
using UnityEngine;

public enum LitInfluenceSourceKind
{
    Flame,
    AncientFlame
}

public struct LitInfluenceInfo
{
    public LitInfluenceInfo(
        MonoBehaviour source,
        LitInfluenceSourceKind sourceKind,
        Vector3 center,
        float radius,
        float transitionDuration = 5f)
    {
        Source = source;
        SourceKind = sourceKind;
        Center = center;
        Radius = radius;
        TransitionDuration = Mathf.Max(0f, transitionDuration);
        SourceId = source != null ? source.GetInstanceID() : 0;
    }

    public MonoBehaviour Source { get; }
    public LitInfluenceSourceKind SourceKind { get; }
    public Vector3 Center { get; }
    public float Radius { get; }
    public float TransitionDuration { get; }
    public int SourceId { get; }
    public GameObject SourceObject => Source != null ? Source.gameObject : null;
}

public interface ILitInfluenceReceiver
{
    void OnLitInfluenceEnter(LitInfluenceInfo info);
    void OnLitInfluenceStay(LitInfluenceInfo info);
    void OnLitInfluenceExit(LitInfluenceInfo info);
}

[Serializable]
public class LitInfluenceSource
{
    private const int InitialHitCapacity = 128;
    private const int MaximumHitCapacity = 16384;

    [SerializeField, Tooltip("Active la zone d'influence quand la source est allumee.")]
    private bool enabled = true;
    [SerializeField, Min(0f), Tooltip("Rayon monde de l'influence lumineuse.")]
    private float radius = 5f;
    [SerializeField, Min(0f), Tooltip("Duree en secondes du fondu entre la glace et l'apparence revelee.")]
    private float transitionDuration = 5f;
    [SerializeField, Tooltip("Centre local de la zone d'influence.")]
    private Vector3 center = Vector3.zero;
    [SerializeField, Tooltip("Layers scannes pour trouver les objets qui peuvent reagir a la lumiere.")]
    private LayerMask layerMask = ~0;
    [SerializeField, Tooltip("Prise en compte des triggers pendant le scan d'influence.")]
    private QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.Collide;
    [SerializeField, Min(0.02f), Tooltip("Intervalle entre deux scans de la zone.")]
    private float refreshInterval = 0.25f;
    [SerializeField, Tooltip("Envoie aussi OnLitInfluenceStay aux receivers deja presents dans la zone.")]
    private bool notifyStay = true;
    [SerializeField, Tooltip("Dessine le rayon d'influence dans la Scene view.")]
    private bool drawDebugGizmos = true;

    private Collider[] hits = new Collider[InitialHitCapacity];
    private readonly HashSet<ILitInfluenceReceiver> activeReceivers = new HashSet<ILitInfluenceReceiver>();
    private readonly HashSet<ILitInfluenceReceiver> scannedReceivers = new HashSet<ILitInfluenceReceiver>();
    private readonly List<ILitInfluenceReceiver> removalBuffer = new List<ILitInfluenceReceiver>();
    private readonly HashSet<Renderer> activeMaterialRenderers = new HashSet<Renderer>();
    private readonly HashSet<Renderer> scannedMaterialRenderers = new HashSet<Renderer>();
    private readonly List<Renderer> materialRendererRemovalBuffer = new List<Renderer>();
    private float nextScanTime;

    public LitInfluenceSource()
    {
    }

    public LitInfluenceSource(float radius)
    {
        this.radius = Mathf.Max(0f, radius);
    }

    public bool Enabled => enabled;
    public float Radius => radius;
    public float TransitionDuration => transitionDuration;
    public Vector3 Center => center;
    public bool DrawDebugGizmos => drawDebugGizmos;

    public Vector3 GetWorldCenter(Transform owner)
    {
        return owner != null ? owner.TransformPoint(center) : center;
    }

    public bool TouchesCollider(Transform owner, Collider targetCollider, Vector3 fallbackPoint)
    {
        if (!enabled || radius <= 0f)
        {
            return false;
        }

        Vector3 worldCenter = GetWorldCenter(owner);
        float sqrRadius = radius * radius;
        if (targetCollider != null)
        {
            Vector3 closestPoint = targetCollider.ClosestPoint(worldCenter);
            if ((closestPoint - worldCenter).sqrMagnitude <= sqrRadius)
            {
                return true;
            }
        }

        return (fallbackPoint - worldCenter).sqrMagnitude <= sqrRadius;
    }

    public void Tick(MonoBehaviour owner, LitInfluenceSourceKind sourceKind, bool isLit, bool force = false)
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (owner == null || !enabled || !isLit || radius <= 0f)
        {
            Clear(owner, sourceKind);
            return;
        }

        if (!force && Time.time < nextScanTime)
        {
            return;
        }

        nextScanTime = Time.time + Mathf.Max(0.02f, refreshInterval);
        Scan(owner, sourceKind);
    }

    public void Clear(MonoBehaviour owner, LitInfluenceSourceKind sourceKind)
    {
        if (activeReceivers.Count == 0 && activeMaterialRenderers.Count == 0)
        {
            return;
        }

        LitInfluenceInfo info = CreateInfo(owner, sourceKind);
        removalBuffer.Clear();
        foreach (ILitInfluenceReceiver receiver in activeReceivers)
        {
            removalBuffer.Add(receiver);
        }

        for (int i = 0; i < removalBuffer.Count; i++)
        {
            ILitInfluenceReceiver receiver = removalBuffer[i];
            if (!IsReceiverAlive(receiver))
            {
                continue;
            }

            receiver.OnLitInfluenceExit(info);
        }

        ClearMaterialInfluence(info);
        activeReceivers.Clear();
        scannedReceivers.Clear();
        removalBuffer.Clear();
    }

    public void DrawGizmos(Transform owner, bool isLit)
    {
        if (!drawDebugGizmos || owner == null || radius <= 0f)
        {
            return;
        }

        Gizmos.color = isLit
            ? new Color(1f, 0.72f, 0.18f, 0.28f)
            : new Color(0.6f, 0.6f, 0.6f, 0.16f);
        Gizmos.DrawWireSphere(GetWorldCenter(owner), radius);
    }

    private void Scan(MonoBehaviour owner, LitInfluenceSourceKind sourceKind)
    {
        scannedReceivers.Clear();
        scannedMaterialRenderers.Clear();

        Transform ownerTransform = owner.transform;
        Vector3 worldCenter = GetWorldCenter(ownerTransform);
        int hitCount = CollectOverlappingColliders(worldCenter);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = hits[i];
            hits[i] = null;

            if (hit == null || hit.transform == null || hit.transform.IsChildOf(ownerTransform))
            {
                continue;
            }

            AddReceivers(hit);
            AddMaterialRenderers(hit);
        }

        LitInfluenceInfo info = new LitInfluenceInfo(owner, sourceKind, worldCenter, radius, transitionDuration);

        foreach (ILitInfluenceReceiver receiver in scannedReceivers)
        {
            if (!IsReceiverAlive(receiver))
            {
                continue;
            }

            if (activeReceivers.Add(receiver))
            {
                receiver.OnLitInfluenceEnter(info);
            }
            else if (notifyStay)
            {
                receiver.OnLitInfluenceStay(info);
            }
        }

        removalBuffer.Clear();
        foreach (ILitInfluenceReceiver receiver in activeReceivers)
        {
            if (!scannedReceivers.Contains(receiver))
            {
                removalBuffer.Add(receiver);
            }
        }

        for (int i = 0; i < removalBuffer.Count; i++)
        {
            ILitInfluenceReceiver receiver = removalBuffer[i];
            activeReceivers.Remove(receiver);
            if (IsReceiverAlive(receiver))
            {
                receiver.OnLitInfluenceExit(info);
            }
        }

        UpdateMaterialInfluence(info);
        removalBuffer.Clear();
    }

    private int CollectOverlappingColliders(Vector3 worldCenter)
    {
        while (true)
        {
            int hitCount = Physics.OverlapSphereNonAlloc(
                worldCenter,
                radius,
                hits,
                layerMask,
                queryTriggerInteraction);

            if (hitCount < hits.Length || hits.Length >= MaximumHitCapacity)
            {
                return hitCount;
            }

            int expandedCapacity = Mathf.Min(hits.Length * 2, MaximumHitCapacity);
            Array.Resize(ref hits, expandedCapacity);
        }
    }

    private void AddReceivers(Collider hit)
    {
        AddReceivers(hit.GetComponentsInParent<MonoBehaviour>(true));
        AddReceivers(hit.GetComponentsInChildren<MonoBehaviour>(true));
    }

    private void AddReceivers(MonoBehaviour[] behaviours)
    {
        if (behaviours == null)
        {
            return;
        }

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is ILitInfluenceReceiver receiver)
            {
                scannedReceivers.Add(receiver);
            }
        }
    }

    private void AddMaterialRenderers(Collider hit)
    {
        if (hit == null)
        {
            return;
        }

        AddMaterialRenderers(hit.GetComponentsInParent<Renderer>(true));
        AddMaterialRenderers(hit.GetComponentsInChildren<Renderer>(true));
    }

    private void AddMaterialRenderers(Renderer[] renderers)
    {
        if (renderers == null)
        {
            return;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer != null)
            {
                scannedMaterialRenderers.Add(renderer);
            }
        }
    }

    private void UpdateMaterialInfluence(LitInfluenceInfo info)
    {
        if (info.SourceId == 0)
        {
            return;
        }

        foreach (Renderer renderer in scannedMaterialRenderers)
        {
            if (!IsRendererAlive(renderer))
            {
                continue;
            }

            activeMaterialRenderers.Add(renderer);
            FlameInfluenceMaterialRuntime.RegisterOrUpdate(info, renderer);
        }

        materialRendererRemovalBuffer.Clear();
        foreach (Renderer renderer in activeMaterialRenderers)
        {
            if (!scannedMaterialRenderers.Contains(renderer))
            {
                materialRendererRemovalBuffer.Add(renderer);
            }
        }

        for (int i = 0; i < materialRendererRemovalBuffer.Count; i++)
        {
            Renderer renderer = materialRendererRemovalBuffer[i];
            activeMaterialRenderers.Remove(renderer);
            FlameInfluenceMaterialRuntime.Unregister(info.SourceId, renderer);
        }

        materialRendererRemovalBuffer.Clear();
        scannedMaterialRenderers.Clear();
    }

    private void ClearMaterialInfluence(LitInfluenceInfo info)
    {
        if (info.SourceId == 0)
        {
            activeMaterialRenderers.Clear();
            scannedMaterialRenderers.Clear();
            materialRendererRemovalBuffer.Clear();
            return;
        }

        foreach (Renderer renderer in activeMaterialRenderers)
        {
            FlameInfluenceMaterialRuntime.Unregister(info.SourceId, renderer);
        }

        activeMaterialRenderers.Clear();
        scannedMaterialRenderers.Clear();
        materialRendererRemovalBuffer.Clear();
    }

    private LitInfluenceInfo CreateInfo(MonoBehaviour owner, LitInfluenceSourceKind sourceKind)
    {
        Transform ownerTransform = owner != null ? owner.transform : null;
        Vector3 worldCenter = ownerTransform != null ? GetWorldCenter(ownerTransform) : center;
        return new LitInfluenceInfo(owner, sourceKind, worldCenter, radius, transitionDuration);
    }

    private static bool IsReceiverAlive(ILitInfluenceReceiver receiver)
    {
        if (receiver == null)
        {
            return false;
        }

        if (receiver is UnityEngine.Object unityObject)
        {
            return unityObject != null;
        }

        return true;
    }

    private static bool IsRendererAlive(Renderer renderer)
    {
        return renderer != null;
    }
}

internal static class FlameInfluenceMaterialRuntime
{
    // ShaderGraph properties cannot expose an arbitrary-length list. V3 therefore
    // receives the most relevant influences through two fixed-size MPB arrays.
    // The original single-center properties remain populated for V2 and legacy
    // materials, and for manual preview in the material inspector.
    private const int MaxShaderInfluenceCount = 32;

    private struct SourceInfluence
    {
        public int SourceId;
        public LitInfluenceSourceKind SourceKind;
        public Vector3 Center;
        public float Radius;
        public float TransitionDuration;
        public float TransitionProgress;
        public bool Active;
    }

    private static readonly Dictionary<Renderer, List<SourceInfluence>> rendererInfluences = new Dictionary<Renderer, List<SourceInfluence>>();
    private static readonly List<Renderer> staleRenderers = new List<Renderer>();
    private static readonly int ageCenterPropertyId = Shader.PropertyToID("_AgeCenter");
    private static readonly int ageAmountPropertyId = Shader.PropertyToID("_AgeAmount");
    private static readonly int flameCenterPropertyId = Shader.PropertyToID("_FlameCenter");
    private static readonly int flameRadiusPropertyId = Shader.PropertyToID("_FlameInfluenceRadius");
    private static readonly int transitionProgressPropertyId = Shader.PropertyToID("_TransitionProgress");
    private static readonly int flameInfluenceCountPropertyId = Shader.PropertyToID("_LitIceFlameInfluenceCount");
    private static readonly int flameCentersAndRadiiPropertyId = Shader.PropertyToID("_LitIceFlameCentersAndRadii");
    private static readonly int flameTransitionDataPropertyId = Shader.PropertyToID("_LitIceFlameTransitionData");
    private static readonly Vector4[] flameCentersAndRadii = new Vector4[MaxShaderInfluenceCount];
    private static readonly Vector4[] flameTransitionData = new Vector4[MaxShaderInfluenceCount];
    private static readonly float[] flameInfluenceDistances = new float[MaxShaderInfluenceCount];
    private static MaterialPropertyBlock propertyBlock;
    private static FlameInfluenceMaterialUpdater updater;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        rendererInfluences.Clear();
        staleRenderers.Clear();
        propertyBlock = null;
        updater = null;
    }

    public static void RegisterOrUpdate(LitInfluenceInfo info, Renderer renderer)
    {
        int sourceId = info.SourceId;
        if (sourceId == 0 || renderer == null)
        {
            return;
        }

        EnsureUpdater();

        if (!rendererInfluences.TryGetValue(renderer, out List<SourceInfluence> influences))
        {
            influences = new List<SourceInfluence>();
            rendererInfluences.Add(renderer, influences);
        }

        bool updated = false;
        for (int i = 0; i < influences.Count; i++)
        {
            SourceInfluence influence = influences[i];
            if (influence.SourceId != sourceId)
            {
                continue;
            }

            influence.SourceKind = info.SourceKind;
            influence.Center = info.Center;
            influence.Radius = Mathf.Max(0f, info.Radius);
            influence.TransitionDuration = Mathf.Max(0f, info.TransitionDuration);
            influence.Active = true;
            if (influence.TransitionDuration <= 0f)
            {
                influence.TransitionProgress = 1f;
            }
            influences[i] = influence;
            updated = true;
            break;
        }

        if (!updated)
        {
            influences.Add(new SourceInfluence
            {
                SourceId = sourceId,
                SourceKind = info.SourceKind,
                Center = info.Center,
                Radius = Mathf.Max(0f, info.Radius),
                TransitionDuration = Mathf.Max(0f, info.TransitionDuration),
                TransitionProgress = info.TransitionDuration <= 0f ? 1f : 0f,
                Active = true
            });
        }

        ApplyBestInfluence(renderer, influences, info.SourceKind == LitInfluenceSourceKind.AncientFlame);
    }

    public static void Unregister(int sourceId, Renderer renderer)
    {
        if (sourceId == 0 || renderer == null)
        {
            return;
        }

        if (!rendererInfluences.TryGetValue(renderer, out List<SourceInfluence> influences))
        {
            return;
        }

        bool removedAncientFlame = false;
        for (int i = influences.Count - 1; i >= 0; i--)
        {
            SourceInfluence influence = influences[i];
            if (influence.SourceId == sourceId)
            {
                removedAncientFlame |= influence.SourceKind == LitInfluenceSourceKind.AncientFlame;
                influence.Active = false;
                if (influence.TransitionDuration <= 0f || influence.TransitionProgress <= 0f)
                {
                    influences.RemoveAt(i);
                }
                else
                {
                    influences[i] = influence;
                }
            }
        }

        if (influences.Count == 0)
        {
            rendererInfluences.Remove(renderer);
            ClearInfluence(renderer, removedAncientFlame);
            return;
        }

        ApplyBestInfluence(renderer, influences, removedAncientFlame);
    }

    public static void UpdateTransitions(float deltaTime)
    {
        if (rendererInfluences.Count == 0)
        {
            return;
        }

        float safeDeltaTime = Mathf.Max(0f, deltaTime);
        staleRenderers.Clear();
        foreach (KeyValuePair<Renderer, List<SourceInfluence>> entry in rendererInfluences)
        {
            Renderer renderer = entry.Key;
            List<SourceInfluence> influences = entry.Value;
            if (renderer == null)
            {
                staleRenderers.Add(renderer);
                continue;
            }

            bool changed = false;
            for (int i = influences.Count - 1; i >= 0; i--)
            {
                SourceInfluence influence = influences[i];
                float target = influence.Active ? 1f : 0f;
                float step = influence.TransitionDuration <= 0f
                    ? 1f
                    : safeDeltaTime / influence.TransitionDuration;
                float progress = Mathf.MoveTowards(influence.TransitionProgress, target, step);
                if (!Mathf.Approximately(progress, influence.TransitionProgress))
                {
                    influence.TransitionProgress = progress;
                    influences[i] = influence;
                    changed = true;
                }

                if (!influence.Active && influence.TransitionProgress <= 0f)
                {
                    influences.RemoveAt(i);
                    changed = true;
                }
            }

            if (influences.Count == 0)
            {
                ClearInfluence(renderer, false);
                staleRenderers.Add(renderer);
            }
            else if (changed)
            {
                ApplyBestInfluence(renderer, influences, false);
            }
        }

        for (int i = 0; i < staleRenderers.Count; i++)
        {
            rendererInfluences.Remove(staleRenderers[i]);
        }

        staleRenderers.Clear();
    }

    public static void CleanupStaleRenderers()
    {
        staleRenderers.Clear();
        foreach (KeyValuePair<Renderer, List<SourceInfluence>> entry in rendererInfluences)
        {
            if (entry.Key == null)
            {
                staleRenderers.Add(entry.Key);
            }
        }

        for (int i = 0; i < staleRenderers.Count; i++)
        {
            rendererInfluences.Remove(staleRenderers[i]);
        }

        staleRenderers.Clear();
    }

    private static void ApplyBestInfluence(
        Renderer renderer,
        List<SourceInfluence> influences,
        bool refreshAgeProperties)
    {
        if (renderer == null || influences == null || influences.Count == 0)
        {
            return;
        }

        if (!TryResolvePropertySupport(
                renderer,
                out bool hasAgeCenter,
                out bool hasAgeAmount,
                out bool hasFlameCenter,
                out bool hasFlameRadius,
                out bool hasTransitionProgress))
        {
            return;
        }

        Bounds rendererBounds = renderer.bounds;
        if (!TryGetClosestInfluence(influences, rendererBounds, false, true, out SourceInfluence bestFlame))
        {
            TryGetClosestInfluence(influences, rendererBounds, false, false, out bestFlame);
        }

        int shaderInfluenceCount = BuildShaderInfluenceArrays(influences, rendererBounds);

        Vector3 ageCenter = Vector3.zero;
        if (refreshAgeProperties
            && TryGetClosestInfluence(influences, rendererBounds, true, true, out SourceInfluence bestAncientFlame))
        {
            ageCenter = bestAncientFlame.Center;
        }

        ApplyProperties(
            renderer,
            refreshAgeProperties && hasAgeCenter,
            refreshAgeProperties && hasAgeAmount,
            ageCenter,
            ResolveCurrentAgeAmount(),
            hasFlameCenter,
            hasFlameRadius,
            bestFlame.Center,
            bestFlame.Radius,
            hasTransitionProgress,
            bestFlame.TransitionProgress,
            hasFlameCenter || hasFlameRadius,
            shaderInfluenceCount);
    }

    private static void ClearInfluence(Renderer renderer, bool clearAgeProperties)
    {
        if (renderer == null
            || !TryResolvePropertySupport(
                renderer,
                out bool hasAgeCenter,
                out bool hasAgeAmount,
                out bool hasFlameCenter,
                out bool hasFlameRadius,
                out bool hasTransitionProgress))
        {
            return;
        }

        ApplyProperties(
            renderer,
            clearAgeProperties && hasAgeCenter,
            clearAgeProperties && hasAgeAmount,
            Vector3.zero,
            ResolveCurrentAgeAmount(),
            hasFlameCenter,
            hasFlameRadius,
            Vector3.zero,
            0f,
            hasTransitionProgress,
            0f,
            hasFlameCenter || hasFlameRadius,
            0);
    }

    private static bool TryGetClosestInfluence(
        List<SourceInfluence> influences,
        Bounds rendererBounds,
        bool ancientFlamesOnly,
        bool activeOnly,
        out SourceInfluence best)
    {
        best = default;
        bool found = false;
        float bestDistanceSqr = float.PositiveInfinity;
        for (int i = 0; i < influences.Count; i++)
        {
            SourceInfluence candidate = influences[i];
            if (ancientFlamesOnly && candidate.SourceKind != LitInfluenceSourceKind.AncientFlame)
            {
                continue;
            }

            if (activeOnly && !candidate.Active)
            {
                continue;
            }

            if (!activeOnly && !candidate.Active && candidate.TransitionProgress <= 0f)
            {
                continue;
            }

            // A large floor or wall can extend far away from its bounds center.
            // Distance to the bounds is therefore a better representative than
            // distance to the renderer pivot/center.
            float distanceSqr = rendererBounds.SqrDistance(candidate.Center);
            if (!found || distanceSqr < bestDistanceSqr)
            {
                best = candidate;
                bestDistanceSqr = distanceSqr;
                found = true;
            }
        }

        return found;
    }

    private static int BuildShaderInfluenceArrays(
        List<SourceInfluence> influences,
        Bounds rendererBounds)
    {
        int count = 0;
        for (int i = 0; i < influences.Count; i++)
        {
            SourceInfluence candidate = influences[i];
            if (!candidate.Active && candidate.TransitionProgress <= 0f)
            {
                continue;
            }

            float distance = rendererBounds.SqrDistance(candidate.Center);
            int targetIndex;
            if (count < MaxShaderInfluenceCount)
            {
                targetIndex = count;
                count++;
            }
            else
            {
                targetIndex = FindFarthestShaderInfluenceIndex(count);
                if (distance >= flameInfluenceDistances[targetIndex])
                {
                    continue;
                }
            }

            flameCentersAndRadii[targetIndex] = new Vector4(
                candidate.Center.x,
                candidate.Center.y,
                candidate.Center.z,
                Mathf.Max(0f, candidate.Radius));
            flameTransitionData[targetIndex] = new Vector4(
                Mathf.Clamp01(candidate.TransitionProgress),
                candidate.Active ? 1f : 0f,
                candidate.SourceKind == LitInfluenceSourceKind.AncientFlame ? 1f : 0f,
                0f);
            flameInfluenceDistances[targetIndex] = distance;
        }

        return count;
    }

    private static int FindFarthestShaderInfluenceIndex(int count)
    {
        int farthestIndex = 0;
        float farthestDistance = flameInfluenceDistances[0];
        for (int i = 1; i < count; i++)
        {
            if (flameInfluenceDistances[i] > farthestDistance)
            {
                farthestIndex = i;
                farthestDistance = flameInfluenceDistances[i];
            }
        }

        return farthestIndex;
    }

    private static void ApplyProperties(
        Renderer renderer,
        bool writeAgeCenter,
        bool writeAgeAmount,
        Vector3 ageCenter,
        float ageAmount,
        bool writeFlameCenter,
        bool writeFlameRadius,
        Vector3 flameCenter,
        float flameRadius,
        bool writeTransitionProgress,
        float transitionProgress,
        bool writeFlameInfluenceArray,
        int flameInfluenceCount)
    {
        if (!writeAgeCenter
            && !writeAgeAmount
            && !writeFlameCenter
            && !writeFlameRadius
            && !writeTransitionProgress
            && !writeFlameInfluenceArray)
        {
            return;
        }

        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        renderer.GetPropertyBlock(propertyBlock);
        if (writeAgeCenter)
        {
            propertyBlock.SetVector(ageCenterPropertyId, ageCenter);
        }

        if (writeAgeAmount)
        {
            propertyBlock.SetFloat(ageAmountPropertyId, ageAmount);
        }

        if (writeFlameCenter)
        {
            propertyBlock.SetVector(flameCenterPropertyId, flameCenter);
        }

        if (writeFlameRadius)
        {
            propertyBlock.SetFloat(flameRadiusPropertyId, Mathf.Max(0f, flameRadius));
        }

        if (writeTransitionProgress)
        {
            propertyBlock.SetFloat(transitionProgressPropertyId, Mathf.Clamp01(transitionProgress));
        }

        if (writeFlameInfluenceArray)
        {
            propertyBlock.SetInt(
                flameInfluenceCountPropertyId,
                Mathf.Clamp(flameInfluenceCount, 0, MaxShaderInfluenceCount));
            propertyBlock.SetVectorArray(flameCentersAndRadiiPropertyId, flameCentersAndRadii);
            propertyBlock.SetVectorArray(flameTransitionDataPropertyId, flameTransitionData);
        }

        renderer.SetPropertyBlock(propertyBlock);
    }

    private static float ResolveCurrentAgeAmount()
    {
        AgeManager manager = AgeManager.ActiveInstance;
        return manager != null ? manager.CurrentYear : AgeManager.DefaultStartYear;
    }

    private static bool TryResolvePropertySupport(
        Renderer renderer,
        out bool hasAgeCenter,
        out bool hasAgeAmount,
        out bool hasFlameCenter,
        out bool hasFlameRadius,
        out bool hasTransitionProgress)
    {
        hasAgeCenter = false;
        hasAgeAmount = false;
        hasFlameCenter = false;
        hasFlameRadius = false;
        hasTransitionProgress = false;

        Material[] materials = renderer != null ? renderer.sharedMaterials : null;
        if (materials == null)
        {
            return false;
        }

        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];
            if (material == null)
            {
                continue;
            }

            hasAgeCenter |= material.HasProperty(ageCenterPropertyId);
            hasAgeAmount |= material.HasProperty(ageAmountPropertyId);
            hasFlameCenter |= material.HasProperty(flameCenterPropertyId);
            hasFlameRadius |= material.HasProperty(flameRadiusPropertyId);
            hasTransitionProgress |= material.HasProperty(transitionProgressPropertyId);
            if (hasAgeCenter
                && hasAgeAmount
                && hasFlameCenter
                && hasFlameRadius
                && hasTransitionProgress)
            {
                return true;
            }
        }

        return hasAgeCenter
            || hasAgeAmount
            || hasFlameCenter
            || hasFlameRadius
            || hasTransitionProgress;
    }

    private static void EnsureUpdater()
    {
        if (!Application.isPlaying || updater != null)
        {
            return;
        }

        var updaterObject = new GameObject("Lit Flame Material Transition Updater")
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        UnityEngine.Object.DontDestroyOnLoad(updaterObject);
        updater = updaterObject.AddComponent<FlameInfluenceMaterialUpdater>();
    }
}

internal sealed class FlameInfluenceMaterialUpdater : MonoBehaviour
{
    private void Update()
    {
        FlameInfluenceMaterialRuntime.UpdateTransitions(Time.deltaTime);
    }
}
