using System;
using System.Collections.Generic;
using UnityEngine;

// Manager des braseros qui pilote le temps global du chateau.
[DisallowMultipleComponent]
public class BraseroTimeManager : MonoBehaviour
{
    public static BraseroTimeManager ActiveInstance { get; private set; }

    [Header("Braseros")]
    [Tooltip("Si true, collecte automatiquement les braseros dans les enfants.")]
    public bool autoCollectChildren = true;
    [Tooltip("Liste des braseros geres par ce manager.")]
    public List<Brasero> braseros = new List<Brasero>();

    [Header("Time")]
    [Tooltip("Annee de reference (0 par defaut).")]
    public int baseYear = 0;
    [Tooltip("Nombre d'annees gagnees par brasero allume.")]
    public int yearsPerBrasero = 100;
    [Tooltip("Ecrit un log quand la periode change.")]
    public bool logTimeChanges = false;

    [Header("State")]
    [SerializeField, Tooltip("Nombre de braseros allumes.")]
    private int litCount;
    [SerializeField, Tooltip("Annee courante calculee.")]
    private int currentYear;

    public int LitCount => litCount;
    public int CurrentYear => currentYear;
    public int CurrentYearOffset => litCount * yearsPerBrasero;

    public event Action<int, int> TimeChanged;

    private void OnEnable()
    {
        ActiveInstance = this;

        if (autoCollectChildren)
        {
            RefreshBraseros();
        }

        Subscribe();
        RecalculateTime(rescanTimePeriodVisibility: true);
    }

    private void OnDisable()
    {
        Unsubscribe();

        if (ReferenceEquals(ActiveInstance, this))
        {
            ActiveInstance = null;
        }
    }

    private void OnTransformChildrenChanged()
    {
        if (!autoCollectChildren)
        {
            return;
        }

        RefreshAndResubscribe();
    }

    public void RefreshBraseros()
    {
        if (!autoCollectChildren)
        {
            return;
        }

        braseros.Clear();
        GetComponentsInChildren(true, braseros);
    }

    public void RefreshAndResubscribe()
    {
        Unsubscribe();
        RefreshBraseros();
        Subscribe();
        RecalculateTime();
    }

    public void RecalculateTime(bool rescanTimePeriodVisibility = true)
    {
        int previousLitCount = litCount;
        int previousYear = currentYear;
        int count = 0;
        for (int i = 0; i < braseros.Count; i++)
        {
            Brasero brasero = braseros[i];
            if (brasero == null)
            {
                continue;
            }

            if (brasero.IsLit)
            {
                count++;
            }
        }

        litCount = count;
        currentYear = baseYear + litCount * yearsPerBrasero;
        if (logTimeChanges && (previousLitCount != litCount || previousYear != currentYear))
        {
            Debug.Log(
                $"[TimePeriod] litCount={litCount}/{(braseros != null ? braseros.Count : 0)} currentYear={currentYear} yearOffset={CurrentYearOffset}",
                this);
        }

        TimeChanged?.Invoke(currentYear, litCount);
        TimePeriodVisibility.RefreshAllForManager(this, rescanTimePeriodVisibility);
    }

    public void SetAllLit(bool lit)
    {
        for (int i = 0; i < braseros.Count; i++)
        {
            Brasero brasero = braseros[i];
            if (brasero == null)
            {
                continue;
            }

            brasero.SetLit(lit);
        }
    }

    private void Subscribe()
    {
        for (int i = 0; i < braseros.Count; i++)
        {
            Brasero brasero = braseros[i];
            if (brasero == null)
            {
                continue;
            }

            brasero.StateChanged += OnBraseroStateChanged;
        }
    }

    private void Unsubscribe()
    {
        for (int i = 0; i < braseros.Count; i++)
        {
            Brasero brasero = braseros[i];
            if (brasero == null)
            {
                continue;
            }

            brasero.StateChanged -= OnBraseroStateChanged;
        }
    }

    private void OnBraseroStateChanged(Brasero brasero, bool lit)
    {
        RecalculateTime();
    }

    public int GetComparisonValue(TimePeriodValueMode valueMode)
    {
        switch (valueMode)
        {
            case TimePeriodValueMode.YearOffsetFromBase:
                return CurrentYearOffset;

            case TimePeriodValueMode.LitBrazierCount:
                return LitCount;

            case TimePeriodValueMode.AbsoluteYear:
            default:
                return CurrentYear;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying && yearsPerBrasero < 0)
        {
            yearsPerBrasero = 0;
        }
    }
#endif
}
