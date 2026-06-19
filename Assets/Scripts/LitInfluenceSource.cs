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
    public LitInfluenceInfo(MonoBehaviour source, LitInfluenceSourceKind sourceKind, Vector3 center, float radius)
    {
        Source = source;
        SourceKind = sourceKind;
        Center = center;
        Radius = radius;
        SourceId = source != null ? source.GetInstanceID() : 0;
    }

    public MonoBehaviour Source { get; }
    public LitInfluenceSourceKind SourceKind { get; }
    public Vector3 Center { get; }
    public float Radius { get; }
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
    [SerializeField, Tooltip("Active la zone d'influence quand la source est allumee.")]
    private bool enabled = true;
    [SerializeField, Min(0f), Tooltip("Rayon monde de l'influence lumineuse.")]
    private float radius = 5f;
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

    private readonly Collider[] hits = new Collider[96];
    private readonly HashSet<ILitInfluenceReceiver> activeReceivers = new HashSet<ILitInfluenceReceiver>();
    private readonly HashSet<ILitInfluenceReceiver> scannedReceivers = new HashSet<ILitInfluenceReceiver>();
    private readonly List<ILitInfluenceReceiver> removalBuffer = new List<ILitInfluenceReceiver>();
    private readonly HashSet<Renderer> activeAgeRenderers = new HashSet<Renderer>();
    private readonly HashSet<Renderer> scannedAgeRenderers = new HashSet<Renderer>();
    private readonly List<Renderer> ageRendererRemovalBuffer = new List<Renderer>();
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
        if (activeReceivers.Count == 0 && activeAgeRenderers.Count == 0)
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

        ClearMaterialAgeInfluence(info);
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
        scannedAgeRenderers.Clear();

        Transform ownerTransform = owner.transform;
        Vector3 worldCenter = GetWorldCenter(ownerTransform);
        int hitCount = Physics.OverlapSphereNonAlloc(
            worldCenter,
            radius,
            hits,
            layerMask,
            queryTriggerInteraction);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = hits[i];
            hits[i] = null;

            if (hit == null || hit.transform == null || hit.transform.IsChildOf(ownerTransform))
            {
                continue;
            }

            AddReceivers(hit);
            if (sourceKind == LitInfluenceSourceKind.AncientFlame)
            {
                AddAgeRenderers(hit);
            }
        }

        LitInfluenceInfo info = new LitInfluenceInfo(owner, sourceKind, worldCenter, radius);

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

        UpdateMaterialAgeInfluence(info);
        removalBuffer.Clear();
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

    private void AddAgeRenderers(Collider hit)
    {
        if (hit == null)
        {
            return;
        }

        AddAgeRenderers(hit.GetComponentsInParent<Renderer>(true));
        AddAgeRenderers(hit.GetComponentsInChildren<Renderer>(true));
    }

    private void AddAgeRenderers(Renderer[] renderers)
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
                scannedAgeRenderers.Add(renderer);
            }
        }
    }

    private void UpdateMaterialAgeInfluence(LitInfluenceInfo info)
    {
        if (info.SourceKind != LitInfluenceSourceKind.AncientFlame || info.SourceId == 0)
        {
            return;
        }

        foreach (Renderer renderer in scannedAgeRenderers)
        {
            if (!IsRendererAlive(renderer))
            {
                continue;
            }

            activeAgeRenderers.Add(renderer);
            FlameInfluenceMaterialRuntime.RegisterOrUpdate(info.SourceId, info.Center, renderer);
        }

        ageRendererRemovalBuffer.Clear();
        foreach (Renderer renderer in activeAgeRenderers)
        {
            if (!scannedAgeRenderers.Contains(renderer))
            {
                ageRendererRemovalBuffer.Add(renderer);
            }
        }

        for (int i = 0; i < ageRendererRemovalBuffer.Count; i++)
        {
            Renderer renderer = ageRendererRemovalBuffer[i];
            activeAgeRenderers.Remove(renderer);
            FlameInfluenceMaterialRuntime.Unregister(info.SourceId, renderer);
        }

        ageRendererRemovalBuffer.Clear();
        scannedAgeRenderers.Clear();
    }

    private void ClearMaterialAgeInfluence(LitInfluenceInfo info)
    {
        if (info.SourceKind != LitInfluenceSourceKind.AncientFlame || info.SourceId == 0)
        {
            activeAgeRenderers.Clear();
            scannedAgeRenderers.Clear();
            ageRendererRemovalBuffer.Clear();
            return;
        }

        foreach (Renderer renderer in activeAgeRenderers)
        {
            FlameInfluenceMaterialRuntime.Unregister(info.SourceId, renderer);
        }

        activeAgeRenderers.Clear();
        scannedAgeRenderers.Clear();
        ageRendererRemovalBuffer.Clear();
    }

    private LitInfluenceInfo CreateInfo(MonoBehaviour owner, LitInfluenceSourceKind sourceKind)
    {
        Transform ownerTransform = owner != null ? owner.transform : null;
        Vector3 worldCenter = ownerTransform != null ? GetWorldCenter(ownerTransform) : center;
        return new LitInfluenceInfo(owner, sourceKind, worldCenter, radius);
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
    private struct SourceInfluence
    {
        public int SourceId;
        public Vector3 Center;
    }

    private static readonly Dictionary<Renderer, List<SourceInfluence>> rendererInfluences = new Dictionary<Renderer, List<SourceInfluence>>();
    private static readonly List<Renderer> staleRenderers = new List<Renderer>();
    private static readonly int ageCenterPropertyId = Shader.PropertyToID("_AgeCenter");
    private static readonly int ageAmountPropertyId = Shader.PropertyToID("_AgeAmount");
    private static MaterialPropertyBlock propertyBlock;

    public static void RegisterOrUpdate(int sourceId, Vector3 sourceCenter, Renderer renderer)
    {
        if (sourceId == 0 || renderer == null)
        {
            return;
        }

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

            influence.Center = sourceCenter;
            influences[i] = influence;
            updated = true;
            break;
        }

        if (!updated)
        {
            influences.Add(new SourceInfluence
            {
                SourceId = sourceId,
                Center = sourceCenter
            });
        }

        ApplyBestInfluence(renderer, influences);
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

        for (int i = influences.Count - 1; i >= 0; i--)
        {
            if (influences[i].SourceId == sourceId)
            {
                influences.RemoveAt(i);
            }
        }

        if (influences.Count == 0)
        {
            rendererInfluences.Remove(renderer);
            ClearInfluence(renderer);
            return;
        }

        ApplyBestInfluence(renderer, influences);
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

    private static void ApplyBestInfluence(Renderer renderer, List<SourceInfluence> influences)
    {
        if (renderer == null || influences == null || influences.Count == 0)
        {
            return;
        }

        if (!TryResolveAgePropertySupport(renderer, out bool hasAgeCenter, out bool hasAgeAmount))
        {
            return;
        }

        Vector3 reference = renderer.bounds.center;
        SourceInfluence best = influences[0];
        float bestDistanceSqr = (best.Center - reference).sqrMagnitude;
        for (int i = 1; i < influences.Count; i++)
        {
            SourceInfluence candidate = influences[i];
            float distanceSqr = (candidate.Center - reference).sqrMagnitude;
            if (distanceSqr < bestDistanceSqr)
            {
                best = candidate;
                bestDistanceSqr = distanceSqr;
            }
        }

        ApplyAgeProperties(renderer, hasAgeCenter, hasAgeAmount, best.Center, ResolveCurrentAgeAmount());
    }

    private static void ClearInfluence(Renderer renderer)
    {
        if (renderer == null || !TryResolveAgePropertySupport(renderer, out bool hasAgeCenter, out bool hasAgeAmount))
        {
            return;
        }

        ApplyAgeProperties(renderer, hasAgeCenter, hasAgeAmount, Vector3.zero, ResolveCurrentAgeAmount());
    }

    private static void ApplyAgeProperties(
        Renderer renderer,
        bool hasAgeCenter,
        bool hasAgeAmount,
        Vector3 ageCenter,
        float ageAmount)
    {
        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        renderer.GetPropertyBlock(propertyBlock);
        if (hasAgeCenter)
        {
            propertyBlock.SetVector(ageCenterPropertyId, ageCenter);
        }

        if (hasAgeAmount)
        {
            propertyBlock.SetFloat(ageAmountPropertyId, ageAmount);
        }

        renderer.SetPropertyBlock(propertyBlock);
    }

    private static float ResolveCurrentAgeAmount()
    {
        AgeManager manager = AgeManager.ActiveInstance;
        return manager != null ? manager.CurrentYear : AgeManager.DefaultStartYear;
    }

    private static bool TryResolveAgePropertySupport(Renderer renderer, out bool hasAgeCenter, out bool hasAgeAmount)
    {
        hasAgeCenter = false;
        hasAgeAmount = false;

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
            if (hasAgeCenter && hasAgeAmount)
            {
                return true;
            }
        }

        return hasAgeCenter || hasAgeAmount;
    }
}
