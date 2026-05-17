using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Computes the local age revealed by a temporal torch.
/// AgeManager is the canonical source; the older zone modes remain available for
/// isolated test setups and scene migration.
/// </summary>
[DisallowMultipleComponent]
public class TemporalTorch : MonoBehaviour
{
    public enum RevealMode
    {
        PreviousAge = 0,
        CurrentAge = 1,
        NextAge = 2,
        ExplicitAge = 3
    }

    private static readonly HashSet<TemporalTorch> ManagedTorches = new HashSet<TemporalTorch>();
    private static readonly List<TemporalTorch> RefreshBuffer = new List<TemporalTorch>();

    [Header("Age Manager")]
    [SerializeField, Tooltip("Utilise AgeManager comme source canonique de l'age revele par la torche.")]
    private bool useAgeManager = true;
    [SerializeField, Tooltip("Annees revelees devant l'age courant. La valeur canonique est 110.")]
    private int ageManagerForwardRevealYears = AgeManager.DefaultTorchRevealForwardYears;

    [Header("References")]
    [SerializeField] private TemporalZone currentZone;
    [SerializeField] private bool autoFindZoneInParents = true;
    [SerializeField, Tooltip("Pont vers le shader d'age local existant.")]
    private LocalRuntimeAgeTrigger localAgeTrigger;
    [SerializeField] private bool autoFindLocalAgeTrigger = true;
    [SerializeField, Tooltip("Cree LocalRuntimeAgeTrigger si la torche n'en a pas encore.")]
    private bool autoCreateLocalAgeTrigger = true;
    [SerializeField, Tooltip("Owner de torche utilise pour verifier que la torche est equipee.")]
    private SquadCharacterController owner;
    [SerializeField, Min(0.1f), Tooltip("Rayon par defaut cree pour la revelation locale si aucun trigger n'existe.")]
    private float revealRadius = 5f;

    [Header("Fallback Reveal")]
    [SerializeField] private RevealMode revealMode = RevealMode.CurrentAge;
    [SerializeField] private TemporalAge explicitAge = TemporalAge.Age666;

    [Header("Shader Globals")]
    [SerializeField, Tooltip("Desactive par defaut pour eviter de concurrencer AgeManager et LocalRuntimeAgeTrigger.")]
    private bool setShaderGlobals;
    [SerializeField] private string globalAgeAmountProperty = "_AgeAmount";
    [SerializeField] private string globalAgeCenterProperty = "_AgeCenter";

    public TemporalZone CurrentZone => currentZone;
    public RevealMode CurrentRevealMode => revealMode;
    public bool UsesAgeManager => useAgeManager;
    public TemporalAge TargetAge { get; private set; } = TemporalAge.Age666;
    public int TargetYear { get; private set; } = TemporalAgeUtility.MaxYear;

    public event Action<TemporalTorch, TemporalAge> TargetAgeChanged;
    public event Action<TemporalTorch, int> TargetYearChanged;

    public static void RefreshAllManagedTorches()
    {
        RefreshBuffer.Clear();
        foreach (TemporalTorch torch in ManagedTorches)
        {
            if (torch != null)
            {
                RefreshBuffer.Add(torch);
            }
        }

        for (int i = 0; i < RefreshBuffer.Count; i++)
        {
            TemporalTorch torch = RefreshBuffer[i];
            if (torch != null && torch.isActiveAndEnabled)
            {
                torch.RefreshTargetAge();
            }
        }

        RefreshBuffer.Clear();
    }

    public static TemporalTorch EnsureOnTorch(Transform torchTransform, SquadCharacterController torchOwner, float radius)
    {
        if (torchTransform == null)
        {
            return null;
        }

        GameObject torchObject = torchTransform.gameObject;
        LocalRuntimeAgeTrigger ageTrigger = torchObject.GetComponent<LocalRuntimeAgeTrigger>();
        if (ageTrigger == null)
        {
            ageTrigger = torchObject.AddComponent<LocalRuntimeAgeTrigger>();
        }

        SphereCollider sphere = torchObject.GetComponent<SphereCollider>();
        if (sphere != null)
        {
            sphere.isTrigger = true;
            sphere.radius = Mathf.Max(0.1f, radius);
        }

        ageTrigger.SetOwner(torchOwner, requireEquippedTorch: true);

        TemporalTorch temporalTorch = torchObject.GetComponent<TemporalTorch>();
        if (temporalTorch == null)
        {
            temporalTorch = torchObject.AddComponent<TemporalTorch>();
        }

        temporalTorch.ConfigureManagedTorch(torchOwner, ageTrigger, radius);
        return temporalTorch;
    }

    private void OnEnable()
    {
        ManagedTorches.Add(this);
        ResolveReferences();
        Subscribe();
        RefreshTargetAge();
    }

    private void OnDisable()
    {
        Unsubscribe();
        ManagedTorches.Remove(this);
    }

    private void OnValidate()
    {
        ageManagerForwardRevealYears = Mathf.Clamp(ageManagerForwardRevealYears, 0, TemporalAgeUtility.MaxYear);
        revealRadius = Mathf.Max(0.1f, revealRadius);
        ResolveReferences();
    }

    public void ConfigureManagedTorch(SquadCharacterController torchOwner, LocalRuntimeAgeTrigger trigger, float radius)
    {
        owner = torchOwner;
        localAgeTrigger = trigger != null ? trigger : localAgeTrigger;
        revealRadius = Mathf.Max(0.1f, radius);
        useAgeManager = true;
        ageManagerForwardRevealYears = AgeManager.DefaultTorchRevealForwardYears;

        if (localAgeTrigger != null)
        {
            localAgeTrigger.SetOwner(owner, requireEquippedTorch: true);
            SphereCollider sphere = localAgeTrigger.GetComponent<SphereCollider>();
            if (sphere != null)
            {
                sphere.isTrigger = true;
                sphere.radius = revealRadius;
            }
        }

        RefreshTargetAge();
    }

    public void SetZone(TemporalZone zone)
    {
        if (currentZone == zone)
        {
            return;
        }

        Unsubscribe();
        currentZone = zone;
        Subscribe();
        RefreshTargetAge();
    }

    public void SetUseAgeManager(bool value)
    {
        if (useAgeManager == value)
        {
            return;
        }

        Unsubscribe();
        useAgeManager = value;
        Subscribe();
        RefreshTargetAge();
    }

    public void SetRevealMode(RevealMode mode)
    {
        bool wasUsingAgeManager = useAgeManager;
        if (wasUsingAgeManager)
        {
            Unsubscribe();
        }

        useAgeManager = false;
        revealMode = mode;
        if (wasUsingAgeManager)
        {
            Subscribe();
        }

        RefreshTargetAge();
    }

    public void RevealPreviousAge()
    {
        SetRevealMode(RevealMode.PreviousAge);
    }

    public void RevealCurrentAge()
    {
        SetRevealMode(RevealMode.CurrentAge);
    }

    public void RevealNextAge()
    {
        SetRevealMode(RevealMode.NextAge);
    }

    public void SetExplicitAge(TemporalAge age)
    {
        bool wasUsingAgeManager = useAgeManager;
        if (wasUsingAgeManager)
        {
            Unsubscribe();
        }

        useAgeManager = false;
        explicitAge = TemporalAgeUtility.ClampAge(age);
        revealMode = RevealMode.ExplicitAge;
        if (wasUsingAgeManager)
        {
            Subscribe();
        }

        RefreshTargetAge();
    }

    [ContextMenu("Refresh Target Age")]
    public void RefreshTargetAge()
    {
        TemporalAge previousAge = TargetAge;
        int previousYear = TargetYear;

        TargetYear = ResolveTargetYear();
        TargetAge = TemporalAgeUtility.IntToAge(TargetYear);
        ApplyTargetAge();

        if (previousYear != TargetYear)
        {
            TargetYearChanged?.Invoke(this, TargetYear);
        }

        if (previousAge != TargetAge)
        {
            TargetAgeChanged?.Invoke(this, TargetAge);
        }
    }

    private int ResolveTargetYear()
    {
        if (useAgeManager)
        {
            AgeManager manager = AgeManager.GetOrCreate();
            if (manager != null)
            {
                return manager.GetTorchRevealEndYear(ageManagerForwardRevealYears);
            }
        }

        return TemporalAgeUtility.AgeToInt(ResolveFallbackTargetAge());
    }

    private TemporalAge ResolveFallbackTargetAge()
    {
        TemporalAge zoneAge = currentZone != null ? currentZone.CurrentAge : explicitAge;

        switch (revealMode)
        {
            case RevealMode.PreviousAge:
                return currentZone != null ? currentZone.GetPreviousAge() : TemporalAgeUtility.GetPreviousAge(zoneAge);
            case RevealMode.NextAge:
                return currentZone != null ? currentZone.GetNextAge() : TemporalAgeUtility.GetNextAge(zoneAge);
            case RevealMode.ExplicitAge:
                return currentZone != null ? currentZone.ClampToZone(explicitAge) : TemporalAgeUtility.ClampAge(explicitAge);
            case RevealMode.CurrentAge:
            default:
                return currentZone != null ? currentZone.ClampToZone(zoneAge) : TemporalAgeUtility.ClampAge(zoneAge);
        }
    }

    private void ApplyTargetAge()
    {
        if (localAgeTrigger != null)
        {
            localAgeTrigger.SetAgeAmount(TargetYear);
        }

        if (!setShaderGlobals)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(globalAgeAmountProperty))
        {
            Shader.SetGlobalFloat(globalAgeAmountProperty, TargetYear);
        }

        if (!string.IsNullOrWhiteSpace(globalAgeCenterProperty))
        {
            Shader.SetGlobalVector(globalAgeCenterProperty, transform.position);
        }
    }

    private void ResolveReferences()
    {
        if (currentZone == null && autoFindZoneInParents)
        {
            currentZone = GetComponentInParent<TemporalZone>(true);
        }

        if (owner == null)
        {
            owner = GetComponentInParent<SquadCharacterController>(true);
        }

        if (localAgeTrigger == null && autoFindLocalAgeTrigger)
        {
            localAgeTrigger = GetComponentInChildren<LocalRuntimeAgeTrigger>(true);
        }

        if (localAgeTrigger == null && autoCreateLocalAgeTrigger && Application.isPlaying)
        {
            localAgeTrigger = gameObject.AddComponent<LocalRuntimeAgeTrigger>();
        }

        if (localAgeTrigger != null)
        {
            localAgeTrigger.SetOwner(owner, requireEquippedTorch: true);
            SphereCollider sphere = localAgeTrigger.GetComponent<SphereCollider>();
            if (sphere != null)
            {
                sphere.isTrigger = true;
                sphere.radius = Mathf.Max(0.1f, revealRadius);
            }
        }
    }

    private void Subscribe()
    {
        if (currentZone != null)
        {
            currentZone.AgeChanged += OnZoneAgeChanged;
        }

        if (!useAgeManager)
        {
            return;
        }

        AgeManager manager = AgeManager.GetOrCreate();
        if (manager != null)
        {
            manager.AgeChanged += OnAgeManagerChanged;
        }
    }

    private void Unsubscribe()
    {
        if (currentZone != null)
        {
            currentZone.AgeChanged -= OnZoneAgeChanged;
        }

        AgeManager manager = AgeManager.ActiveInstance;
        if (manager != null)
        {
            manager.AgeChanged -= OnAgeManagerChanged;
        }
    }

    private void OnZoneAgeChanged(TemporalZone zone, TemporalAge previous, TemporalAge current)
    {
        if (!useAgeManager)
        {
            RefreshTargetAge();
        }
    }

    private void OnAgeManagerChanged(AgeManager manager, int previousYear, int currentYear)
    {
        if (useAgeManager)
        {
            RefreshTargetAge();
        }
    }
}
