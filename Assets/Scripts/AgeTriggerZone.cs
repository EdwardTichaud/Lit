using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
public class LocalRuntimeAgeTrigger : MonoBehaviour
{
    internal struct MaterialAgeState
    {
        public Material Material;
        public bool HasAgeCenter;
        public Vector4 AgeCenter;
        public bool HasAgeRadius;
        public float AgeRadius;
        public bool HasAgeAmount;
        public float AgeAmount;
    }

    private sealed class RendererAgeState
    {
        public readonly HashSet<Collider> Colliders = new HashSet<Collider>();
        public MaterialAgeState[] Materials;
    }

    [Header("Age")]
    [Range(0f, 666f)]
    public float ageAmount = 666f;
    [SerializeField, Tooltip("Pont optionnel vers la grille temporelle canonique 0/111/222/333/444/555/666.")]
    private bool useTemporalAgePreset;
    [SerializeField]
    private TemporalAge temporalAge = TemporalAge.Age666;

    [Header("Shader Property Names")]
    public string ageCenterProperty = "_AgeCenter";
    public string ageRadiusProperty = "_AgeRadius";
    public string ageAmountProperty = "_AgeAmount";

    [Header("Torch Owner")]
    [SerializeField] private bool requireEquippedTorchOwner = true;
    [SerializeField] private SquadCharacterController owner;

    [Header("Restore")]
    [SerializeField, Range(1f, 2f)] private float restoreDuration = 1.5f;
    [SerializeField] private AnimationCurve restoreCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private SphereCollider sphereCollider;

    private int ageCenterID;
    private int ageRadiusID;
    private int ageAmountID;
    private readonly Dictionary<Renderer, RendererAgeState> agedRenderers = new Dictionary<Renderer, RendererAgeState>();
    private readonly Dictionary<Collider, Renderer> colliderRendererLookup = new Dictionary<Collider, Renderer>();
    private readonly HashSet<Collider> deferredReleaseColliders = new HashSet<Collider>();
    private readonly List<Renderer> rendererRemovalBuffer = new List<Renderer>();
    private readonly List<Collider> colliderReleaseBuffer = new List<Collider>();
    private readonly Dictionary<ITemporalReactable, int> localReactableCounts = new Dictionary<ITemporalReactable, int>();
    private readonly List<ITemporalReactable> temporalReactableBuffer = new List<ITemporalReactable>();

    public float AgeAmount => ageAmount;

    private void Awake()
    {
        sphereCollider = GetComponent<SphereCollider>();
        sphereCollider.isTrigger = true;
        ResolveOwner();

        ageCenterID = Shader.PropertyToID(ageCenterProperty);
        ageRadiusID = Shader.PropertyToID(ageRadiusProperty);
        ageAmountID = Shader.PropertyToID(ageAmountProperty);
    }

    private void OnValidate()
    {
        if (useTemporalAgePreset)
        {
            ageAmount = TemporalAgeUtility.AgeToInt(temporalAge);
        }

        restoreDuration = Mathf.Clamp(restoreDuration, 1f, 2f);
        if (restoreCurve == null)
        {
            restoreCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }
    }

    public void SetTemporalAge(TemporalAge age)
    {
        temporalAge = TemporalAgeUtility.ClampAge(age);
        ageAmount = TemporalAgeUtility.AgeToInt(temporalAge);
    }

    public void SetAgeAmount(float amount)
    {
        ageAmount = Mathf.Clamp(amount, TemporalAgeUtility.MinYear, TemporalAgeUtility.MaxYear);
        temporalAge = TemporalAgeUtility.IntToAge(Mathf.RoundToInt(ageAmount));
        useTemporalAgePreset = false;
    }

    public void SetOwner(SquadCharacterController targetOwner, bool requireEquippedTorch)
    {
        owner = targetOwner;
        requireEquippedTorchOwner = requireEquippedTorch;
    }

    private void OnTriggerEnter(Collider other)
    {
        ApplyAgeToCollider(other);
    }

    private void OnTriggerStay(Collider other)
    {
        ApplyAgeToCollider(other);
    }

    private void Update()
    {
        if (!CanApplyAge())
        {
            BeginRestoreAllAgedRenderers();
            deferredReleaseColliders.Clear();
            return;
        }

        ReleaseDeferredCollidersIfAllowed();
    }

    private void OnTriggerExit(Collider other)
    {
        ReleaseAgeFromCollider(other);
    }

    private void OnDisable()
    {
        if (Application.isPlaying)
        {
            BeginRestoreAllAgedRenderers();
        }
        else
        {
            RestoreAllAgedRenderersImmediate();
        }
    }

    private void ApplyAgeToCollider(Collider other)
    {
        if (!CanApplyAge())
        {
            BeginRestoreAllAgedRenderers();
            return;
        }

        if (other == null)
            return;

        deferredReleaseColliders.Remove(other);

        Renderer renderer = other.GetComponentInParent<Renderer>();
        if (renderer == null)
        {
            renderer = other.GetComponentInChildren<Renderer>();
        }

        if (renderer == null)
            return;

        Vector3 worldCenter = transform.TransformPoint(sphereCollider.center);
        float worldRadius = GetWorldSphereRadius();

        // renderer.materials cree des instances runtime,
        // donc ca ne modifie pas le material asset du projet.
        Material[] runtimeMaterials = renderer.materials;
        LocalRuntimeAgeRestoreRunner.Cancel(runtimeMaterials);
        RendererAgeState state = GetOrCreateRendererAgeState(renderer, runtimeMaterials);
        bool wasEmpty = state.Colliders.Count == 0;
        state.Colliders.Add(other);
        if (wasEmpty && state.Colliders.Count > 0)
        {
            NotifyLocalReveal(renderer, 1);
        }

        colliderRendererLookup[other] = renderer;

        foreach (Material mat in runtimeMaterials)
        {
            if (mat == null)
                continue;

            if (mat.HasProperty(ageCenterID))
                mat.SetVector(ageCenterID, worldCenter);

            if (mat.HasProperty(ageRadiusID))
                mat.SetFloat(ageRadiusID, worldRadius);

            if (mat.HasProperty(ageAmountID))
                mat.SetFloat(ageAmountID, ageAmount);
        }

        renderer.materials = runtimeMaterials;
        RefreshTemporalReactables(renderer);
    }

    private bool CanApplyAge()
    {
        if (!requireEquippedTorchOwner)
        {
            return true;
        }

        SquadCharacterController resolvedOwner = ResolveOwner();
        return resolvedOwner == null || resolvedOwner.IsTorchAgingEffectActive;
    }

    private SquadCharacterController ResolveOwner()
    {
        if (owner == null)
        {
            owner = GetComponentInParent<SquadCharacterController>(true);
        }

        return owner;
    }

    private RendererAgeState GetOrCreateRendererAgeState(Renderer renderer, Material[] runtimeMaterials)
    {
        if (agedRenderers.TryGetValue(renderer, out RendererAgeState state)
            && HasSameMaterials(state.Materials, runtimeMaterials))
        {
            return state;
        }

        state = new RendererAgeState
        {
            Materials = CaptureMaterialAgeStates(runtimeMaterials)
        };
        agedRenderers[renderer] = state;
        return state;
    }

    private MaterialAgeState[] CaptureMaterialAgeStates(Material[] materials)
    {
        if (materials == null || materials.Length == 0)
        {
            return System.Array.Empty<MaterialAgeState>();
        }

        MaterialAgeState[] states = new MaterialAgeState[materials.Length];
        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];
            MaterialAgeState state = new MaterialAgeState
            {
                Material = material
            };

            if (material != null)
            {
                state.HasAgeCenter = material.HasProperty(ageCenterID);
                if (state.HasAgeCenter)
                {
                    state.AgeCenter = material.GetVector(ageCenterID);
                }

                state.HasAgeRadius = material.HasProperty(ageRadiusID);
                if (state.HasAgeRadius)
                {
                    state.AgeRadius = material.GetFloat(ageRadiusID);
                }

                state.HasAgeAmount = material.HasProperty(ageAmountID);
                if (state.HasAgeAmount)
                {
                    state.AgeAmount = material.GetFloat(ageAmountID);
                }
            }

            states[i] = state;
        }

        return states;
    }

    private static bool HasSameMaterials(MaterialAgeState[] states, Material[] materials)
    {
        if (states == null || materials == null || states.Length != materials.Length)
        {
            return false;
        }

        for (int i = 0; i < states.Length; i++)
        {
            if (states[i].Material != materials[i])
            {
                return false;
            }
        }

        return true;
    }

    private void ReleaseAgeFromCollider(Collider other)
    {
        if (other == null)
        {
            return;
        }

        if (ShouldDeferRestoreUntilTorchStored())
        {
            deferredReleaseColliders.Add(other);
            return;
        }

        ReleaseAgeFromColliderNow(other);
    }

    private void ReleaseDeferredCollidersIfAllowed()
    {
        if (deferredReleaseColliders.Count == 0 || ShouldDeferRestoreUntilTorchStored())
        {
            return;
        }

        colliderReleaseBuffer.Clear();
        foreach (Collider collider in deferredReleaseColliders)
        {
            colliderReleaseBuffer.Add(collider);
        }

        deferredReleaseColliders.Clear();
        for (int i = 0; i < colliderReleaseBuffer.Count; i++)
        {
            ReleaseAgeFromColliderNow(colliderReleaseBuffer[i]);
        }

        colliderReleaseBuffer.Clear();
    }

    private bool ShouldDeferRestoreUntilTorchStored()
    {
        if (!requireEquippedTorchOwner)
        {
            return false;
        }

        SquadCharacterController resolvedOwner = ResolveOwner();
        return resolvedOwner != null &&
               !resolvedOwner.IsTorchEquipped &&
               resolvedOwner.IsTorchAgingEffectActive;
    }

    private void ReleaseAgeFromColliderNow(Collider other)
    {
        if (other == null)
        {
            return;
        }

        Renderer renderer = null;
        if (!colliderRendererLookup.TryGetValue(other, out renderer))
        {
            renderer = other.GetComponentInParent<Renderer>();
            if (renderer == null)
            {
                renderer = other.GetComponentInChildren<Renderer>();
            }
        }

        colliderRendererLookup.Remove(other);
        if (renderer == null || !agedRenderers.TryGetValue(renderer, out RendererAgeState state))
        {
            return;
        }

        state.Colliders.Remove(other);
        state.Colliders.RemoveWhere(collider => collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy);
        if (state.Colliders.Count > 0)
        {
            return;
        }

        NotifyLocalReveal(renderer, -1);
        BeginRestoreRendererAgeState(state);
        agedRenderers.Remove(renderer);
    }

    private void BeginRestoreAllAgedRenderers()
    {
        if (agedRenderers.Count == 0)
        {
            colliderRendererLookup.Clear();
            deferredReleaseColliders.Clear();
            return;
        }

        rendererRemovalBuffer.Clear();
        foreach (KeyValuePair<Renderer, RendererAgeState> pair in agedRenderers)
        {
            NotifyLocalReveal(pair.Key, -1);
            BeginRestoreRendererAgeState(pair.Value);
            rendererRemovalBuffer.Add(pair.Key);
        }

        for (int i = 0; i < rendererRemovalBuffer.Count; i++)
        {
            agedRenderers.Remove(rendererRemovalBuffer[i]);
        }

        rendererRemovalBuffer.Clear();
        colliderRendererLookup.Clear();
        deferredReleaseColliders.Clear();
    }

    private void BeginRestoreRendererAgeState(RendererAgeState state)
    {
        if (state == null || state.Materials == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            LocalRuntimeAgeRestoreRunner.FadeTo(
                state.Materials,
                Mathf.Max(0.05f, restoreDuration),
                restoreCurve,
                ageCenterID,
                ageRadiusID,
                ageAmountID);
            return;
        }

        RestoreRendererAgeState(state);
    }

    private void RestoreAllAgedRenderersImmediate()
    {
        if (agedRenderers.Count == 0)
        {
            colliderRendererLookup.Clear();
            deferredReleaseColliders.Clear();
            return;
        }

        rendererRemovalBuffer.Clear();
        foreach (KeyValuePair<Renderer, RendererAgeState> pair in agedRenderers)
        {
            NotifyLocalReveal(pair.Key, -1);
            RestoreRendererAgeState(pair.Value);
            rendererRemovalBuffer.Add(pair.Key);
        }

        for (int i = 0; i < rendererRemovalBuffer.Count; i++)
        {
            agedRenderers.Remove(rendererRemovalBuffer[i]);
        }

        rendererRemovalBuffer.Clear();
        colliderRendererLookup.Clear();
        deferredReleaseColliders.Clear();
    }

    private void RestoreRendererAgeState(RendererAgeState state)
    {
        if (state == null || state.Materials == null)
        {
            return;
        }

        for (int i = 0; i < state.Materials.Length; i++)
        {
            MaterialAgeState materialState = state.Materials[i];
            Material material = materialState.Material;
            if (material == null)
            {
                continue;
            }

            if (materialState.HasAgeCenter && material.HasProperty(ageCenterID))
            {
                material.SetVector(ageCenterID, materialState.AgeCenter);
            }

            if (materialState.HasAgeRadius && material.HasProperty(ageRadiusID))
            {
                material.SetFloat(ageRadiusID, materialState.AgeRadius);
            }

            if (materialState.HasAgeAmount && material.HasProperty(ageAmountID))
            {
                material.SetFloat(ageAmountID, materialState.AgeAmount);
            }
        }
    }

    private float GetWorldSphereRadius()
    {
        Vector3 scale = transform.lossyScale;

        float maxScale = Mathf.Max(
            Mathf.Abs(scale.x),
            Mathf.Abs(scale.y),
            Mathf.Abs(scale.z)
        );

        return sphereCollider.radius * maxScale;
    }

    private void NotifyLocalReveal(Renderer renderer, int delta)
    {
        if (renderer == null || delta == 0)
        {
            return;
        }

        TimePeriodVisibility visibility = renderer.GetComponentInParent<TimePeriodVisibility>(true);
        if (visibility != null)
        {
            visibility.AddLocalRevealSource(this, delta);
        }

        NotifyTemporalReactables(renderer, delta);
    }

    private void NotifyTemporalReactables(Renderer renderer, int delta)
    {
        CollectTemporalReactables(renderer);
        int snappedYear = GetSnappedTemporalYear();

        for (int i = 0; i < temporalReactableBuffer.Count; i++)
        {
            ITemporalReactable reactable = temporalReactableBuffer[i];
            localReactableCounts.TryGetValue(reactable, out int count);

            if (delta > 0)
            {
                count += delta;
                localReactableCounts[reactable] = count;
                if (count == delta)
                {
                    reactable.ApplyLocalTemporalAge(this, snappedYear);
                }
                else
                {
                    reactable.UpdateLocalTemporalAge(this, snappedYear);
                }

                continue;
            }

            count += delta;
            if (count <= 0)
            {
                localReactableCounts.Remove(reactable);
                reactable.ClearLocalTemporalAge(this);
            }
            else
            {
                localReactableCounts[reactable] = count;
                reactable.UpdateLocalTemporalAge(this, snappedYear);
            }
        }

        temporalReactableBuffer.Clear();
    }

    private void RefreshTemporalReactables(Renderer renderer)
    {
        CollectTemporalReactables(renderer);
        int snappedYear = GetSnappedTemporalYear();

        for (int i = 0; i < temporalReactableBuffer.Count; i++)
        {
            ITemporalReactable reactable = temporalReactableBuffer[i];
            if (localReactableCounts.ContainsKey(reactable))
            {
                reactable.UpdateLocalTemporalAge(this, snappedYear);
            }
        }

        temporalReactableBuffer.Clear();
    }

    private void CollectTemporalReactables(Renderer renderer)
    {
        temporalReactableBuffer.Clear();
        if (renderer == null)
        {
            return;
        }

        MonoBehaviour[] behaviours = renderer.GetComponentsInParent<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is ITemporalReactable reactable && !temporalReactableBuffer.Contains(reactable))
            {
                temporalReactableBuffer.Add(reactable);
            }
        }
    }

    private int GetSnappedTemporalYear()
    {
        TemporalAge snappedAge = TemporalAgeUtility.IntToAge(Mathf.RoundToInt(ageAmount));
        return TemporalAgeUtility.AgeToInt(snappedAge);
    }
}

internal sealed class LocalRuntimeAgeRestoreRunner : MonoBehaviour
{
    private sealed class MaterialRestoreState
    {
        public Material Material;
        public int AgeCenterID;
        public int AgeRadiusID;
        public int AgeAmountID;
        public bool HasAgeCenter;
        public Vector4 StartAgeCenter;
        public Vector4 TargetAgeCenter;
        public bool HasAgeRadius;
        public float StartAgeRadius;
        public float TargetAgeRadius;
        public bool HasAgeAmount;
        public float StartAgeAmount;
        public float TargetAgeAmount;
        public float Duration;
        public float Elapsed;
        public AnimationCurve Curve;
    }

    private static LocalRuntimeAgeRestoreRunner instance;

    private readonly Dictionary<Material, MaterialRestoreState> activeRestores = new Dictionary<Material, MaterialRestoreState>();
    private readonly List<Material> restoreRemovalBuffer = new List<Material>();

    public static void Cancel(Material[] materials)
    {
        if (instance == null || materials == null || materials.Length == 0)
        {
            return;
        }

        instance.CancelInternal(materials);
    }

    public static void FadeTo(
        LocalRuntimeAgeTrigger.MaterialAgeState[] states,
        float duration,
        AnimationCurve curve,
        int ageCenterID,
        int ageRadiusID,
        int ageAmountID)
    {
        if (states == null || states.Length == 0)
        {
            return;
        }

        if (!Application.isPlaying)
        {
            RestoreImmediate(states, ageCenterID, ageRadiusID, ageAmountID);
            return;
        }

        LocalRuntimeAgeRestoreRunner runner = EnsureInstance();
        if (runner == null)
        {
            RestoreImmediate(states, ageCenterID, ageRadiusID, ageAmountID);
            return;
        }

        runner.FadeToInternal(
            states,
            Mathf.Max(0.05f, duration),
            CopyCurve(curve),
            ageCenterID,
            ageRadiusID,
            ageAmountID);
    }

    private static LocalRuntimeAgeRestoreRunner EnsureInstance()
    {
        if (instance != null)
        {
            return instance;
        }

        GameObject host = new GameObject("LocalRuntimeAgeRestoreRunner");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<LocalRuntimeAgeRestoreRunner>();
        return instance;
    }

    private static AnimationCurve CopyCurve(AnimationCurve curve)
    {
        if (curve == null)
        {
            return null;
        }

        AnimationCurve copy = new AnimationCurve(curve.keys)
        {
            preWrapMode = curve.preWrapMode,
            postWrapMode = curve.postWrapMode
        };
        return copy;
    }

    private static void RestoreImmediate(
        LocalRuntimeAgeTrigger.MaterialAgeState[] states,
        int ageCenterID,
        int ageRadiusID,
        int ageAmountID)
    {
        for (int i = 0; i < states.Length; i++)
        {
            LocalRuntimeAgeTrigger.MaterialAgeState state = states[i];
            Material material = state.Material;
            if (material == null)
            {
                continue;
            }

            if (state.HasAgeCenter && material.HasProperty(ageCenterID))
            {
                material.SetVector(ageCenterID, state.AgeCenter);
            }

            if (state.HasAgeRadius && material.HasProperty(ageRadiusID))
            {
                material.SetFloat(ageRadiusID, state.AgeRadius);
            }

            if (state.HasAgeAmount && material.HasProperty(ageAmountID))
            {
                material.SetFloat(ageAmountID, state.AgeAmount);
            }
        }
    }

    private void FadeToInternal(
        LocalRuntimeAgeTrigger.MaterialAgeState[] states,
        float duration,
        AnimationCurve curve,
        int ageCenterID,
        int ageRadiusID,
        int ageAmountID)
    {
        for (int i = 0; i < states.Length; i++)
        {
            LocalRuntimeAgeTrigger.MaterialAgeState state = states[i];
            Material material = state.Material;
            if (material == null)
            {
                continue;
            }

            MaterialRestoreState restore = new MaterialRestoreState
            {
                Material = material,
                AgeCenterID = ageCenterID,
                AgeRadiusID = ageRadiusID,
                AgeAmountID = ageAmountID,
                Duration = Mathf.Max(0.05f, duration),
                Curve = curve
            };

            if (state.HasAgeCenter && material.HasProperty(ageCenterID))
            {
                restore.HasAgeCenter = true;
                restore.StartAgeCenter = material.GetVector(ageCenterID);
                restore.TargetAgeCenter = state.AgeCenter;
            }

            if (state.HasAgeRadius && material.HasProperty(ageRadiusID))
            {
                restore.HasAgeRadius = true;
                restore.StartAgeRadius = material.GetFloat(ageRadiusID);
                restore.TargetAgeRadius = state.AgeRadius;
            }

            if (state.HasAgeAmount && material.HasProperty(ageAmountID))
            {
                restore.HasAgeAmount = true;
                restore.StartAgeAmount = material.GetFloat(ageAmountID);
                restore.TargetAgeAmount = state.AgeAmount;
            }

            if (!restore.HasAgeCenter && !restore.HasAgeRadius && !restore.HasAgeAmount)
            {
                continue;
            }

            activeRestores[material] = restore;
        }
    }

    private void CancelInternal(Material[] materials)
    {
        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];
            if (material == null)
            {
                continue;
            }

            activeRestores.Remove(material);
        }
    }

    private void Update()
    {
        if (activeRestores.Count == 0)
        {
            return;
        }

        restoreRemovalBuffer.Clear();
        foreach (KeyValuePair<Material, MaterialRestoreState> pair in activeRestores)
        {
            MaterialRestoreState restore = pair.Value;
            if (restore == null || restore.Material == null)
            {
                restoreRemovalBuffer.Add(pair.Key);
                continue;
            }

            restore.Elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(restore.Elapsed / Mathf.Max(0.05f, restore.Duration));
            float eased = restore.Curve != null ? Mathf.Clamp01(restore.Curve.Evaluate(t)) : t;
            ApplyRestoreStep(restore, eased);

            if (t >= 1f)
            {
                ApplyRestoreStep(restore, 1f);
                restoreRemovalBuffer.Add(pair.Key);
            }
        }

        for (int i = 0; i < restoreRemovalBuffer.Count; i++)
        {
            activeRestores.Remove(restoreRemovalBuffer[i]);
        }

        restoreRemovalBuffer.Clear();
    }

    private static void ApplyRestoreStep(MaterialRestoreState restore, float t)
    {
        Material material = restore.Material;
        if (material == null)
        {
            return;
        }

        if (restore.HasAgeCenter)
        {
            material.SetVector(restore.AgeCenterID, Vector4.Lerp(restore.StartAgeCenter, restore.TargetAgeCenter, t));
        }

        if (restore.HasAgeRadius)
        {
            material.SetFloat(restore.AgeRadiusID, Mathf.Lerp(restore.StartAgeRadius, restore.TargetAgeRadius, t));
        }

        if (restore.HasAgeAmount)
        {
            material.SetFloat(restore.AgeAmountID, Mathf.Lerp(restore.StartAgeAmount, restore.TargetAgeAmount, t));
        }
    }
}
