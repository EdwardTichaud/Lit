using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public class TemporalAgeChangedEvent : UnityEvent<TemporalAge>
{
}

[DisallowMultipleComponent]
public class TemporalZone : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField, Tooltip("Identifiant lisible de la zone narrative/temporelle.")]
    private string zoneId;

    [Header("Age")]
    [SerializeField] private TemporalAge currentAge = TemporalAge.Age666;
    [SerializeField] private TemporalAge minimumAge = TemporalAge.Age000;
    [SerializeField] private TemporalAge maximumAge = TemporalAge.Age666;
    [SerializeField, Tooltip("Applique l'age aux objets references quand la zone s'active.")]
    private bool applyOnEnable = true;

    [Header("Affected Objects")]
    [SerializeField, Tooltip("Objets explicitement pilotes par cette zone.")]
    private List<TemporalObject> affectedObjects = new List<TemporalObject>();

    [Header("Events")]
    [SerializeField] private TemporalAgeChangedEvent onAgeChanged = new TemporalAgeChangedEvent();

    public string ZoneId => zoneId;
    public TemporalAge CurrentAge => currentAge;
    public TemporalAge MinimumAge => minimumAge;
    public TemporalAge MaximumAge => maximumAge;
    public IReadOnlyList<TemporalObject> AffectedObjects => affectedObjects;

    public event Action<TemporalZone, TemporalAge, TemporalAge> AgeChanged;

    private void OnEnable()
    {
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
        NormalizeBounds();
        currentAge = ClampToZone(currentAge);
    }

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

    public void SetAgeFromInt(int year)
    {
        SetAge(TemporalAgeUtility.IntToAge(year));
    }

    public void StepForward()
    {
        Step(1);
    }

    public void StepBackward()
    {
        Step(-1);
    }

    public void Step(int steps)
    {
        int nextStep = TemporalAgeUtility.AgeToStep(currentAge) + steps;
        SetAge(TemporalAgeUtility.StepToAge(nextStep));
    }

    public TemporalAge GetPreviousAge()
    {
        return ClampToZone(TemporalAgeUtility.GetPreviousAge(currentAge));
    }

    public TemporalAge GetNextAge()
    {
        return ClampToZone(TemporalAgeUtility.GetNextAge(currentAge));
    }

    public TemporalAge ClampToZone(TemporalAge age)
    {
        int min = TemporalAgeUtility.AgeToInt(minimumAge);
        int max = TemporalAgeUtility.AgeToInt(maximumAge);
        int value = Mathf.Clamp(TemporalAgeUtility.AgeToInt(age), min, max);
        return TemporalAgeUtility.IntToAge(value);
    }

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

    public void UnregisterObject(TemporalObject temporalObject)
    {
        if (temporalObject == null)
        {
            return;
        }

        affectedObjects.Remove(temporalObject);
    }

    [ContextMenu("Apply Current Age")]
    public void ApplyCurrentAgeToObjects()
    {
        if (affectedObjects == null)
        {
            return;
        }

        for (int i = affectedObjects.Count - 1; i >= 0; i--)
        {
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
        if (TemporalAgeUtility.AgeToInt(maximumAge) < TemporalAgeUtility.AgeToInt(minimumAge))
        {
            maximumAge = minimumAge;
        }
    }
}
