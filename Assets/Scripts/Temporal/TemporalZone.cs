// Role:
// Represents a gameplay area whose dominant temporal age can change.
// Usage:
// Attach to a zone root in a scene. BraseroTemporalController can drive it, and
// TemporalObject instances can register to react to age changes.
// Responsibilities:
// Store current/min/max ages, notify listeners, and apply the age to affected objects.
// Dependencies:
// TemporalAgeUtility, TemporalObject, UnityEvent for inspector wiring.
// Precautions:
// Keep age changes explicit and small; scene objects may depend on the event order.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Inspector-friendly event fired with the new temporal age.
/// </summary>
[Serializable]
public class TemporalAgeChangedEvent : UnityEvent<TemporalAge>
{
}

/// <summary>
/// Controls the dominant temporal age for a scene zone.
/// </summary>
[DisallowMultipleComponent]
public class TemporalZone : MonoBehaviour
{
    [Header("Identity")]
    /// <summary>Readable identifier used by designers and debug tools.</summary>
    [SerializeField, Tooltip("Identifiant lisible de la zone narrative/temporelle.")]
    private string zoneId;

    [Header("Age")]
    /// <summary>Current dominant age for this zone.</summary>
    [SerializeField] private TemporalAge currentAge = TemporalAge.Age666;
    /// <summary>Lowest age this zone is allowed to reach.</summary>
    [SerializeField] private TemporalAge minimumAge = TemporalAge.Age000;
    /// <summary>Highest age this zone is allowed to reach.</summary>
    [SerializeField] private TemporalAge maximumAge = TemporalAge.Age666;
    /// <summary>If true, applies the current age when Unity enables this component.</summary>
    [SerializeField, Tooltip("Applique l'age aux objets references quand la zone s'active.")]
    private bool applyOnEnable = true;

    [Header("Affected Objects")]
    /// <summary>Objects explicitly driven by this zone.</summary>
    [SerializeField, Tooltip("Objets explicitement pilotes par cette zone.")]
    private List<TemporalObject> affectedObjects = new List<TemporalObject>();

    [Header("Events")]
    /// <summary>Inspector event for doors, VFX, sounds or other scene reactions.</summary>
    [SerializeField] private TemporalAgeChangedEvent onAgeChanged = new TemporalAgeChangedEvent();

    /// <summary>Readable identifier for this temporal zone.</summary>
    public string ZoneId => zoneId;
    /// <summary>Current dominant temporal age.</summary>
    public TemporalAge CurrentAge => currentAge;
    /// <summary>Minimum age allowed by this zone.</summary>
    public TemporalAge MinimumAge => minimumAge;
    /// <summary>Maximum age allowed by this zone.</summary>
    public TemporalAge MaximumAge => maximumAge;
    /// <summary>Objects currently registered or assigned as affected by this zone.</summary>
    public IReadOnlyList<TemporalObject> AffectedObjects => affectedObjects;

    /// <summary>
    /// Runtime event fired with this zone, the previous age, and the new age.
    /// </summary>
    public event Action<TemporalZone, TemporalAge, TemporalAge> AgeChanged;

    private void OnEnable()
    {
        // Unity calls OnEnable when the component becomes active.
        // Re-clamp here because serialized scene values may have changed in the editor.
        NormalizeBounds();
        currentAge = ClampToZone(currentAge);

        if (applyOnEnable)
        {
            ApplyCurrentAgeToObjects();
            onAgeChanged.Invoke(currentAge);
        }
    }

    private void OnValidate()
    {
        // Unity calls OnValidate in the editor after inspector changes.
        // Keep designer-edited min/max/current values coherent before Play Mode.
        NormalizeBounds();
        currentAge = ClampToZone(currentAge);
    }

    /// <summary>
    /// Sets the dominant age for the zone and updates all affected objects.
    /// </summary>
    public void SetAge(TemporalAge age)
    {
        TemporalAge previous = currentAge;
        TemporalAge next = ClampToZone(age);
        currentAge = next;

        ApplyCurrentAgeToObjects();

        if (previous != next)
        {
            AgeChanged?.Invoke(this, previous, next);
            onAgeChanged.Invoke(next);
        }
    }

    /// <summary>
    /// Converts a year from older systems to a temporal age and applies it.
    /// </summary>
    public void SetAgeFromInt(int year)
    {
        SetAge(TemporalAgeUtility.IntToAge(year));
    }

    /// <summary>Moves this zone one 111-year step forward.</summary>
    public void StepForward()
    {
        Step(1);
    }

    /// <summary>Moves this zone one 111-year step backward.</summary>
    public void StepBackward()
    {
        Step(-1);
    }

    /// <summary>
    /// Moves this zone by a signed number of temporal steps.
    /// </summary>
    public void Step(int steps)
    {
        int nextStep = TemporalAgeUtility.AgeToStep(currentAge) + steps;
        SetAge(TemporalAgeUtility.StepToAge(nextStep));
    }

    /// <summary>
    /// Returns the previous age allowed by this zone.
    /// </summary>
    public TemporalAge GetPreviousAge()
    {
        return ClampToZone(TemporalAgeUtility.GetPreviousAge(currentAge));
    }

    /// <summary>
    /// Returns the next age allowed by this zone.
    /// </summary>
    public TemporalAge GetNextAge()
    {
        return ClampToZone(TemporalAgeUtility.GetNextAge(currentAge));
    }

    /// <summary>
    /// Clamps an age to this zone's min/max bounds.
    /// </summary>
    public TemporalAge ClampToZone(TemporalAge age)
    {
        int min = TemporalAgeUtility.AgeToInt(minimumAge);
        int max = TemporalAgeUtility.AgeToInt(maximumAge);
        int value = Mathf.Clamp(TemporalAgeUtility.AgeToInt(age), min, max);
        return TemporalAgeUtility.IntToAge(value);
    }

    /// <summary>
    /// Adds a temporal object to the list driven by this zone.
    /// </summary>
    public void RegisterObject(TemporalObject temporalObject)
    {
        if (temporalObject == null)
        {
            return;
        }

        if (!affectedObjects.Contains(temporalObject))
        {
            affectedObjects.Add(temporalObject);
        }
    }

    /// <summary>
    /// Removes a temporal object from this zone.
    /// </summary>
    public void UnregisterObject(TemporalObject temporalObject)
    {
        if (temporalObject == null)
        {
            return;
        }

        affectedObjects.Remove(temporalObject);
    }

    /// <summary>
    /// Applies the current age to all affected objects.
    /// </summary>
    [ContextMenu("Apply Current Age")]
    public void ApplyCurrentAgeToObjects()
    {
        if (affectedObjects == null)
        {
            return;
        }

        for (int i = affectedObjects.Count - 1; i >= 0; i--)
        {
            // Remove missing references so designers do not carry broken scene links.
            TemporalObject temporalObject = affectedObjects[i];
            if (temporalObject == null)
            {
                affectedObjects.RemoveAt(i);
                continue;
            }

            temporalObject.ApplyAge(currentAge);
        }
    }

    private void NormalizeBounds()
    {
        // Avoid an impossible range that would make ClampToZone ambiguous.
        if (TemporalAgeUtility.AgeToInt(maximumAge) < TemporalAgeUtility.AgeToInt(minimumAge))
        {
            maximumAge = minimumAge;
        }
    }
}
