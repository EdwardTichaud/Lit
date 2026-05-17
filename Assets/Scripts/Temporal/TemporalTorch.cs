// Role:
// Represents a local temporal reveal source, usually the player's temporal torch.
// Usage:
// Attach to a torch object or reveal volume that should expose a neighboring age.
// Responsibilities:
// Resolve the target age from the current zone, notify listeners, and optionally
// feed an existing LocalRuntimeAgeTrigger or shader globals.
// Dependencies:
// TemporalZone, TemporalAgeUtility, optional LocalRuntimeAgeTrigger.
// Precautions:
// Shader globals are disabled by default because older global age systems may
// already drive the same materials.
using System;
using UnityEngine;

/// <summary>
/// Computes the local age revealed by a temporal torch.
/// </summary>
[DisallowMultipleComponent]
public class TemporalTorch : MonoBehaviour
{
    /// <summary>
    /// Which age the torch should reveal relative to its current zone.
    /// </summary>
    public enum RevealMode
    {
        PreviousAge = 0,
        CurrentAge = 1,
        NextAge = 2,
        ExplicitAge = 3
    }

    [Header("References")]
    /// <summary>Zone used as the dominant age reference.</summary>
    [SerializeField] private TemporalZone currentZone;
    /// <summary>If true, searches parent objects for a TemporalZone.</summary>
    [SerializeField] private bool autoFindZoneInParents = true;
    /// <summary>Optional bridge to the older local shader age trigger.</summary>
    [SerializeField, Tooltip("Pont optionnel vers le shader d'age local existant.")]
    private LocalRuntimeAgeTrigger localAgeTrigger;
    /// <summary>If true, searches children for LocalRuntimeAgeTrigger.</summary>
    [SerializeField] private bool autoFindLocalAgeTrigger = true;

    [Header("Reveal")]
    /// <summary>Current local reveal mode.</summary>
    [SerializeField] private RevealMode revealMode = RevealMode.CurrentAge;
    /// <summary>Age used when RevealMode is ExplicitAge.</summary>
    [SerializeField] private TemporalAge explicitAge = TemporalAge.Age666;

    [Header("Shader Globals")]
    /// <summary>If true, writes the target age and center to global shader properties.</summary>
    [SerializeField, Tooltip("Desactive par defaut pour eviter de concurrencer GlobalAgeZone.")]
    private bool setShaderGlobals;
    /// <summary>Global shader float receiving the target year.</summary>
    [SerializeField] private string globalAgeAmountProperty = "_AgeAmount";
    /// <summary>Global shader vector receiving this torch position.</summary>
    [SerializeField] private string globalAgeCenterProperty = "_AgeCenter";

    /// <summary>Zone currently used by this torch.</summary>
    public TemporalZone CurrentZone => currentZone;
    /// <summary>Reveal mode currently used by this torch.</summary>
    public RevealMode CurrentRevealMode => revealMode;
    /// <summary>Resolved temporal age currently revealed by the torch.</summary>
    public TemporalAge TargetAge { get; private set; } = TemporalAge.Age666;
    /// <summary>Resolved target year, useful for shaders and debug UI.</summary>
    public int TargetYear => TemporalAgeUtility.AgeToInt(TargetAge);

    /// <summary>Event fired when the resolved target age changes.</summary>
    public event Action<TemporalTorch, TemporalAge> TargetAgeChanged;

    private void OnEnable()
    {
        // Unity calls OnEnable when the torch becomes active.
        // Subscribe after resolving references so zone age changes refresh the reveal.
        ResolveReferences();
        Subscribe();
        RefreshTargetAge();
    }

    private void OnDisable()
    {
        // Prevent dangling event subscriptions when the torch is disabled or destroyed.
        Unsubscribe();
    }

    private void OnValidate()
    {
        // Editor-only convenience: keep auto references visible in the inspector.
        ResolveReferences();
    }

    /// <summary>
    /// Changes the zone used as the dominant age source.
    /// </summary>
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

    /// <summary>
    /// Changes how the torch resolves its local target age.
    /// </summary>
    public void SetRevealMode(RevealMode mode)
    {
        revealMode = mode;
        RefreshTargetAge();
    }

    /// <summary>Reveals the previous age relative to the current zone.</summary>
    public void RevealPreviousAge()
    {
        SetRevealMode(RevealMode.PreviousAge);
    }

    /// <summary>Reveals the current dominant zone age.</summary>
    public void RevealCurrentAge()
    {
        SetRevealMode(RevealMode.CurrentAge);
    }

    /// <summary>Reveals the next age relative to the current zone.</summary>
    public void RevealNextAge()
    {
        SetRevealMode(RevealMode.NextAge);
    }

    /// <summary>
    /// Reveals a specific age, clamped to the current zone if one exists.
    /// </summary>
    public void SetExplicitAge(TemporalAge age)
    {
        explicitAge = TemporalAgeUtility.ClampAge(age);
        revealMode = RevealMode.ExplicitAge;
        RefreshTargetAge();
    }

    /// <summary>
    /// Recomputes and applies the age currently revealed by this torch.
    /// </summary>
    [ContextMenu("Refresh Target Age")]
    public void RefreshTargetAge()
    {
        TemporalAge previous = TargetAge;
        TargetAge = ResolveTargetAge();
        ApplyTargetAge();

        if (previous != TargetAge)
        {
            TargetAgeChanged?.Invoke(this, TargetAge);
        }
    }

    private TemporalAge ResolveTargetAge()
    {
        TemporalAge zoneAge = currentZone != null ? currentZone.CurrentAge : explicitAge;

        // The torch reads a neighboring stratum; it does not move the whole zone.
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
        // Preferred integration path for the existing local shader bridge.
        if (localAgeTrigger != null)
        {
            localAgeTrigger.SetTemporalAge(TargetAge);
        }

        if (!setShaderGlobals)
        {
            return;
        }

        // Optional global shader writes are kept explicit to avoid fighting older systems.
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

        if (localAgeTrigger == null && autoFindLocalAgeTrigger)
        {
            localAgeTrigger = GetComponentInChildren<LocalRuntimeAgeTrigger>(true);
        }
    }

    private void Subscribe()
    {
        if (currentZone != null)
        {
            currentZone.AgeChanged += OnZoneAgeChanged;
        }
    }

    private void Unsubscribe()
    {
        if (currentZone != null)
        {
            currentZone.AgeChanged -= OnZoneAgeChanged;
        }
    }

    private void OnZoneAgeChanged(TemporalZone zone, TemporalAge previous, TemporalAge current)
    {
        RefreshTargetAge();
    }
}
