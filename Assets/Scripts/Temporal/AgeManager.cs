using System;
using System.Collections.Generic;
using UnityEngine;

// Source canonique de l'age de scene.
// La logique gameplay doit lire ce manager pour eviter les calculs divergents.
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public class AgeManager : MonoBehaviour
{
    public const int DefaultStartYear = TemporalAgeUtility.MaxYear;
    public const int DefaultYearsPerBrasero = TemporalAgeUtility.StepYears;
    public const int DefaultTorchRevealForwardYears = TemporalAgeUtility.StepYears - 1;

    public static AgeManager ActiveInstance { get; private set; }

    [Header("Braseros")]
    [SerializeField, Tooltip("Collecte automatiquement les braseros de la scene chargee.")]
    private bool autoCollectSceneBraseros = true;
    [SerializeField, Tooltip("Inclut les braseros inactifs dans le calcul, utile pour les objets caches au demarrage.")]
    private bool includeInactiveBraseros = true;
    [SerializeField, Tooltip("Liste de braseros pilotes par l'AgeManager.")]
    private List<Brasero> braseros = new List<Brasero>();

    [Header("Age")]
    [SerializeField, Tooltip("Annee de depart du joueur.")]
    private int startYear = DefaultStartYear;
    [SerializeField, Tooltip("Annees reculees par brasero allume.")]
    private int yearsPerBrasero = DefaultYearsPerBrasero;
    [SerializeField, Tooltip("Nombre d'annees revelees devant l'age courant par la torche.")]
    private int torchRevealForwardYears = DefaultTorchRevealForwardYears;
    [SerializeField, Tooltip("Ecrit un log quand l'age change.")]
    private bool logAgeChanges;

    [Header("State")]
    [SerializeField, Tooltip("Nombre de braseros allumes.")]
    private int litBrazierCount;
    [SerializeField, Tooltip("Annee courante canonique.")]
    private int currentYear = DefaultStartYear;

    private readonly HashSet<Brasero> subscribedBraseros = new HashSet<Brasero>();

    public IReadOnlyList<Brasero> Braseros => braseros;
    public int LitBrazierCount => litBrazierCount;
    public int CurrentYear => currentYear;
    public int CurrentYearOffsetFromStart => Mathf.Max(0, startYear - currentYear);
    public int StartYear => startYear;
    public int YearsPerBrasero => yearsPerBrasero;
    public int TorchRevealForwardYears => torchRevealForwardYears;
    public int TorchRevealStartYear => CurrentYear;
    public int TorchRevealEndYear => GetTorchRevealEndYear(torchRevealForwardYears);
    public TemporalAge CurrentTemporalAge => TemporalAgeUtility.IntToAge(currentYear);
    public int CurrentTemporalAgeStep => TemporalAgeUtility.AgeToStep(CurrentTemporalAge);

    public event Action<AgeManager, int, int> AgeChanged;

    public static AgeManager GetOrCreate()
    {
        if (ActiveInstance != null)
        {
            return ActiveInstance;
        }

#if UNITY_2023_1_OR_NEWER
        AgeManager existing = FindFirstObjectByType<AgeManager>();
#else
        AgeManager existing = FindObjectOfType<AgeManager>();
#endif
        if (existing != null)
        {
            ActiveInstance = existing;
            return existing;
        }

        if (!Application.isPlaying)
        {
            return null;
        }

        GameObject host = new GameObject("AgeManager");
        return host.AddComponent<AgeManager>();
    }

    private void OnEnable()
    {
        if (ActiveInstance != null && ActiveInstance != this)
        {
            Debug.LogWarning("Plusieurs AgeManager actifs. Le premier reste la source canonique.", this);
        }
        else
        {
            ActiveInstance = this;
        }

        RefreshAndResubscribe();
    }

    private void OnDisable()
    {
        UnsubscribeFromBraseros();

        if (ReferenceEquals(ActiveInstance, this))
        {
            ActiveInstance = null;
        }
    }

    private void OnValidate()
    {
        startYear = Mathf.Clamp(startYear, TemporalAgeUtility.MinYear, TemporalAgeUtility.MaxYear);
        yearsPerBrasero = Mathf.Max(1, yearsPerBrasero);
        torchRevealForwardYears = Mathf.Clamp(torchRevealForwardYears, 0, TemporalAgeUtility.MaxYear);
        currentYear = ClampYear(currentYear);
    }

    public void RefreshAndResubscribe()
    {
        UnsubscribeFromBraseros();
        RefreshBraseros();
        SubscribeToBraseros();
        RecalculateAge(rescanTimePeriodVisibility: true);
    }

    public void RefreshBraseros()
    {
        RemoveMissingBraseros();

        if (!autoCollectSceneBraseros)
        {
            return;
        }

#if UNITY_2023_1_OR_NEWER
        Brasero[] sceneBraseros = includeInactiveBraseros
            ? FindObjectsByType<Brasero>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            : FindObjectsByType<Brasero>(FindObjectsSortMode.None);
#else
        Brasero[] sceneBraseros = FindObjectsOfType<Brasero>(includeInactiveBraseros);
#endif
        AddBraseros(sceneBraseros);
    }

    public void RecalculateAge(bool rescanTimePeriodVisibility = true)
    {
        int previousYear = currentYear;
        int previousLitCount = litBrazierCount;

        litBrazierCount = CountLitBraseros();
        currentYear = CalculateYearForLitCount(litBrazierCount);

        bool changed = previousYear != currentYear || previousLitCount != litBrazierCount;
        if (logAgeChanges && changed)
        {
            Debug.Log(
                $"[AgeManager] litBraseros={litBrazierCount}/{(braseros != null ? braseros.Count : 0)} currentYear={currentYear} torchWindow={TorchRevealStartYear}-{TorchRevealEndYear}",
                this);
        }

        if (changed)
        {
            AgeChanged?.Invoke(this, previousYear, currentYear);
        }

        TimePeriodVisibility.RefreshAllForAgeManager(this, rescanTimePeriodVisibility);
        TemporalTorch.RefreshAllManagedTorches();
        BraseroDisplayManager.RefreshAllDisplays();
    }

    public int CalculateYearForLitCount(int litCount)
    {
        int count = Mathf.Max(0, litCount);
        return ClampYear(startYear - count * yearsPerBrasero);
    }

    public int GetTorchRevealEndYear(int forwardYears)
    {
        return ClampYear(currentYear + Mathf.Max(0, forwardYears));
    }

    public bool IsYearInCurrentAge(int year)
    {
        return ClampYear(year) == currentYear;
    }

    public bool IsYearInTorchRevealWindow(int year)
    {
        int value = ClampYear(year);
        return value >= TorchRevealStartYear && value <= TorchRevealEndYear;
    }

    public bool DoesYearRangeOverlapTorchRevealWindow(int minYear, int maxYear)
    {
        int min = Mathf.Min(minYear, maxYear);
        int max = Mathf.Max(minYear, maxYear);
        return min <= TorchRevealEndYear && max >= TorchRevealStartYear;
    }

    public int GetComparisonValue(TimePeriodValueMode valueMode)
    {
        switch (valueMode)
        {
            case TimePeriodValueMode.YearOffsetFromBase:
                return CurrentYearOffsetFromStart;

            case TimePeriodValueMode.LitBrazierCount:
                return LitBrazierCount;

            case TimePeriodValueMode.TemporalAgeYear:
                return TemporalAgeUtility.AgeToInt(CurrentTemporalAge);

            case TimePeriodValueMode.TemporalAgeStep:
                return CurrentTemporalAgeStep;

            case TimePeriodValueMode.AbsoluteYear:
            default:
                return CurrentYear;
        }
    }

    public void GetTorchRevealComparisonWindow(TimePeriodValueMode valueMode, out int minValue, out int maxValue)
    {
        int start = TorchRevealStartYear;
        int end = TorchRevealEndYear;

        switch (valueMode)
        {
            case TimePeriodValueMode.YearOffsetFromBase:
                minValue = Mathf.Min(startYear - start, startYear - end);
                maxValue = Mathf.Max(startYear - start, startYear - end);
                return;

            case TimePeriodValueMode.TemporalAgeStep:
                minValue = Mathf.Min(
                    TemporalAgeUtility.AgeToStep(TemporalAgeUtility.IntToAge(start)),
                    TemporalAgeUtility.AgeToStep(TemporalAgeUtility.IntToAge(end)));
                maxValue = Mathf.Max(
                    TemporalAgeUtility.AgeToStep(TemporalAgeUtility.IntToAge(start)),
                    TemporalAgeUtility.AgeToStep(TemporalAgeUtility.IntToAge(end)));
                return;

            case TimePeriodValueMode.LitBrazierCount:
                minValue = maxValue = LitBrazierCount;
                return;

            case TimePeriodValueMode.TemporalAgeYear:
            case TimePeriodValueMode.AbsoluteYear:
            default:
                minValue = Mathf.Min(start, end);
                maxValue = Mathf.Max(start, end);
                return;
        }
    }

    private int CountLitBraseros()
    {
        int count = 0;
        if (braseros == null)
        {
            return count;
        }

        for (int i = 0; i < braseros.Count; i++)
        {
            Brasero brasero = braseros[i];
            if (brasero != null && brasero.IsLit)
            {
                count++;
            }
        }

        return count;
    }

    private void SubscribeToBraseros()
    {
        if (braseros == null)
        {
            return;
        }

        for (int i = 0; i < braseros.Count; i++)
        {
            Brasero brasero = braseros[i];
            if (brasero == null || subscribedBraseros.Contains(brasero))
            {
                continue;
            }

            brasero.StateChanged += OnBraseroStateChanged;
            subscribedBraseros.Add(brasero);
        }
    }

    private void UnsubscribeFromBraseros()
    {
        foreach (Brasero brasero in subscribedBraseros)
        {
            if (brasero != null)
            {
                brasero.StateChanged -= OnBraseroStateChanged;
            }
        }

        subscribedBraseros.Clear();
    }

    private void OnBraseroStateChanged(Brasero brasero, bool lit)
    {
        RecalculateAge();
    }

    private void AddBraseros(IList<Brasero> source)
    {
        if (source == null)
        {
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            AddBrasero(source[i]);
        }
    }

    private void AddBraseros(Brasero[] source)
    {
        if (source == null)
        {
            return;
        }

        for (int i = 0; i < source.Length; i++)
        {
            AddBrasero(source[i]);
        }
    }

    private void AddBrasero(Brasero brasero)
    {
        if (brasero == null)
        {
            return;
        }

        if (braseros == null)
        {
            braseros = new List<Brasero>();
        }

        if (!braseros.Contains(brasero))
        {
            braseros.Add(brasero);
        }
    }

    private void RemoveMissingBraseros()
    {
        if (braseros == null || braseros.Count == 0)
        {
            return;
        }

        for (int i = braseros.Count - 1; i >= 0; i--)
        {
            if (braseros[i] == null)
            {
                braseros.RemoveAt(i);
            }
        }
    }

    private static int ClampYear(int year)
    {
        return Mathf.Clamp(year, TemporalAgeUtility.MinYear, TemporalAgeUtility.MaxYear);
    }
}
