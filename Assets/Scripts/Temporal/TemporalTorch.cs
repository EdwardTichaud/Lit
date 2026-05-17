using System;
using UnityEngine;

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

    [Header("References")]
    [SerializeField] private TemporalZone currentZone;
    [SerializeField] private bool autoFindZoneInParents = true;
    [SerializeField, Tooltip("Pont optionnel vers le shader d'age local existant.")]
    private LocalRuntimeAgeTrigger localAgeTrigger;
    [SerializeField] private bool autoFindLocalAgeTrigger = true;

    [Header("Reveal")]
    [SerializeField] private RevealMode revealMode = RevealMode.CurrentAge;
    [SerializeField] private TemporalAge explicitAge = TemporalAge.Age666;

    [Header("Shader Globals")]
    [SerializeField, Tooltip("Desactive par defaut pour eviter de concurrencer GlobalAgeZone.")]
    private bool setShaderGlobals;
    [SerializeField] private string globalAgeAmountProperty = "_AgeAmount";
    [SerializeField] private string globalAgeCenterProperty = "_AgeCenter";

    public TemporalZone CurrentZone => currentZone;
    public RevealMode CurrentRevealMode => revealMode;
    public TemporalAge TargetAge { get; private set; } = TemporalAge.Age666;
    public int TargetYear => TemporalAgeUtility.AgeToInt(TargetAge);

    public event Action<TemporalTorch, TemporalAge> TargetAgeChanged;

    private void OnEnable()
    {
        ResolveReferences();
        Subscribe();
        RefreshTargetAge();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnValidate()
    {
        ResolveReferences();
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

    public void SetRevealMode(RevealMode mode)
    {
        revealMode = mode;
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
        explicitAge = TemporalAgeUtility.ClampAge(age);
        revealMode = RevealMode.ExplicitAge;
        RefreshTargetAge();
    }

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
            localAgeTrigger.SetTemporalAge(TargetAge);
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
