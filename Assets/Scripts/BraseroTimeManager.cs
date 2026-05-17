using System;
using UnityEngine;

// Pont de compatibilite pour les scenes qui referencent encore l'ancien manager.
// La source canonique de gameplay est AgeManager.
[DisallowMultipleComponent]
public class BraseroTimeManager : MonoBehaviour
{
    public static BraseroTimeManager ActiveInstance { get; private set; }

    [Header("Time")]
    [Tooltip("Annee de depart. Gardee pour compatibilite, AgeManager utilise 666 par defaut.")]
    public int baseYear = AgeManager.DefaultStartYear;
    [Tooltip("Nombre d'annees reculees par brasero allume.")]
    public int yearsPerBrasero = AgeManager.DefaultYearsPerBrasero;
    [Tooltip("Ecrit un log quand la periode change.")]
    public bool logTimeChanges = false;

    [Header("State")]
    [SerializeField, Tooltip("Nombre de braseros allumes.")]
    private int litCount;
    [SerializeField, Tooltip("Annee courante calculee.")]
    private int currentYear;
    private AgeManager activeAgeManager;

    public int LitCount => litCount;
    public int CurrentYear => currentYear;
    public int CurrentYearOffset => litCount * yearsPerBrasero;
    public int TotalCount => GetBraseroCount();
    public TemporalAge CurrentTemporalAge => TemporalAgeUtility.IntToAge(currentYear);
    public TemporalAge CurrentTemporalAgeOffset => TemporalAgeUtility.IntToAge(CurrentYearOffset);

    public event Action<int, int> TimeChanged;

    private void OnEnable()
    {
        ActiveInstance = this;
        BindToAgeManager();
        RecalculateTime(rescanTimePeriodVisibility: true);
    }

    private void OnDisable()
    {
        UnsubscribeFromAgeManager();

        if (ReferenceEquals(ActiveInstance, this))
        {
            ActiveInstance = null;
        }
    }

    private void OnTransformChildrenChanged()
    {
        RefreshAndResubscribe();
    }

    public void RefreshBraseros()
    {
        AgeManager manager = ResolveAgeManager();
        if (manager != null)
        {
            manager.RefreshBraseros();
        }
    }

    public void RefreshAndResubscribe()
    {
        AgeManager manager = ResolveAgeManager();
        if (manager != null)
        {
            manager.RefreshAndResubscribe();
            SyncFromAgeManager(manager, notifyChanged: true);
            return;
        }

        RecalculateTime();
    }

    public void RecalculateTime(bool rescanTimePeriodVisibility = true)
    {
        if (BindToAgeManager())
        {
            activeAgeManager.RecalculateAge(rescanTimePeriodVisibility);
            SyncFromAgeManager(activeAgeManager, notifyChanged: true);
            return;
        }

        SyncDefaultState(notifyChanged: true);
        BraseroDisplayManager.RefreshAllDisplays();
    }

    public void SetAllLit(bool lit)
    {
        AgeManager manager = ResolveAgeManager();
        if (manager == null || manager.Braseros == null)
        {
            return;
        }

        for (int i = 0; i < manager.Braseros.Count; i++)
        {
            Brasero brasero = manager.Braseros[i];
            if (brasero == null)
            {
                continue;
            }

            brasero.SetLit(lit);
        }
    }

    private bool BindToAgeManager()
    {
        AgeManager manager = ResolveAgeManager();
        if (manager == null)
        {
            return false;
        }

        if (!ReferenceEquals(activeAgeManager, manager))
        {
            UnsubscribeFromAgeManager();
            activeAgeManager = manager;
            activeAgeManager.AgeChanged += OnAgeManagerChanged;
        }

        return true;
    }

    private void UnsubscribeFromAgeManager()
    {
        if (activeAgeManager != null)
        {
            activeAgeManager.AgeChanged -= OnAgeManagerChanged;
            activeAgeManager = null;
        }
    }

    private void OnAgeManagerChanged(AgeManager manager, int previousYear, int currentYearValue)
    {
        SyncFromAgeManager(manager, notifyChanged: true);
    }

    private void SyncFromAgeManager(AgeManager manager, bool notifyChanged)
    {
        if (manager == null)
        {
            return;
        }

        int previousLitCount = litCount;
        int previousYear = currentYear;

        baseYear = manager.StartYear;
        yearsPerBrasero = manager.YearsPerBrasero;
        litCount = manager.LitBrazierCount;
        currentYear = manager.CurrentYear;

        bool changed = previousLitCount != litCount || previousYear != currentYear;
        if (logTimeChanges && changed)
        {
            Debug.Log(
                $"[TimePeriod:LegacyBridge] litCount={litCount}/{TotalCount} currentYear={currentYear} yearOffset={CurrentYearOffset}",
                this);
        }

        if (notifyChanged && changed)
        {
            TimeChanged?.Invoke(currentYear, litCount);
        }
    }

    private void SyncDefaultState(bool notifyChanged)
    {
        int previousLitCount = litCount;
        int previousYear = currentYear;

        baseYear = AgeManager.DefaultStartYear;
        yearsPerBrasero = AgeManager.DefaultYearsPerBrasero;
        litCount = 0;
        currentYear = AgeManager.DefaultStartYear;

        if (notifyChanged && (previousLitCount != litCount || previousYear != currentYear))
        {
            TimeChanged?.Invoke(currentYear, litCount);
        }
    }

    public int GetComparisonValue(TimePeriodValueMode valueMode)
    {
        AgeManager manager = activeAgeManager != null ? activeAgeManager : AgeManager.ActiveInstance;
        if (manager != null)
        {
            return manager.GetComparisonValue(valueMode);
        }

        switch (valueMode)
        {
            case TimePeriodValueMode.YearOffsetFromBase:
                return CurrentYearOffset;

            case TimePeriodValueMode.LitBrazierCount:
                return LitCount;

            case TimePeriodValueMode.TemporalAgeYear:
                return TemporalAgeUtility.AgeToInt(CurrentTemporalAge);

            case TimePeriodValueMode.TemporalAgeStep:
                return TemporalAgeUtility.AgeToStep(CurrentTemporalAge);

            case TimePeriodValueMode.AbsoluteYear:
            default:
                return CurrentYear;
        }
    }

    private int GetBraseroCount()
    {
        AgeManager manager = activeAgeManager != null ? activeAgeManager : AgeManager.ActiveInstance;
        return manager != null && manager.Braseros != null ? manager.Braseros.Count : 0;
    }

    private static AgeManager ResolveAgeManager()
    {
        if (AgeManager.ActiveInstance != null)
        {
            return AgeManager.ActiveInstance;
        }

        return AgeManager.GetOrCreate();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying && yearsPerBrasero < 1)
        {
            yearsPerBrasero = AgeManager.DefaultYearsPerBrasero;
        }

        if (!Application.isPlaying)
        {
            baseYear = Mathf.Clamp(baseYear, TemporalAgeUtility.MinYear, TemporalAgeUtility.MaxYear);
        }
    }
#endif
}
