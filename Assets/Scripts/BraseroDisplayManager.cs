using System.Collections.Generic;
using UnityEngine;

public interface IBraseroDisplayTarget
{
    void ApplyBraseroDisplay(BraseroDisplaySnapshot snapshot);
}

public struct BraseroDisplaySnapshot
{
    public bool HasSource;
    public int CurrentYear;
    public int LitCount;
    public int TotalCount;
    public int StartYear;
    public int YearsPerBrasero;
    public TemporalAge CurrentTemporalAge;
    public int CurrentTemporalAgeStep;

    public int CurrentYearOffsetFromStart => Mathf.Max(0, StartYear - CurrentYear);

    public BraseroDisplaySnapshot(
        bool hasSource,
        int currentYear,
        int litCount,
        int totalCount,
        int startYear,
        int yearsPerBrasero,
        TemporalAge currentTemporalAge,
        int currentTemporalAgeStep)
    {
        HasSource = hasSource;
        CurrentYear = currentYear;
        LitCount = litCount;
        TotalCount = totalCount;
        StartYear = startYear;
        YearsPerBrasero = yearsPerBrasero;
        CurrentTemporalAge = currentTemporalAge;
        CurrentTemporalAgeStep = currentTemporalAgeStep;
    }

    public int GetComparisonValue(TimePeriodValueMode valueMode)
    {
        switch (valueMode)
        {
            case TimePeriodValueMode.YearOffsetFromBase:
                return CurrentYearOffsetFromStart;

            case TimePeriodValueMode.LitBrazierCount:
                return LitCount;

            case TimePeriodValueMode.TemporalAgeYear:
                return TemporalAgeUtility.AgeToInt(CurrentTemporalAge);

            case TimePeriodValueMode.TemporalAgeStep:
                return CurrentTemporalAgeStep;

            case TimePeriodValueMode.AbsoluteYear:
            default:
                return CurrentYear;
        }
    }

    public static BraseroDisplaySnapshot Default()
    {
        return new BraseroDisplaySnapshot(
            false,
            AgeManager.DefaultStartYear,
            0,
            0,
            AgeManager.DefaultStartYear,
            AgeManager.DefaultYearsPerBrasero,
            TemporalAgeUtility.IntToAge(AgeManager.DefaultStartYear),
            TemporalAgeUtility.AgeToStep(TemporalAgeUtility.IntToAge(AgeManager.DefaultStartYear)));
    }
}

// Centralise les affichages dependants des braseros. AgeManager calcule l'etat;
// ce manager ne fait que diffuser un snapshot aux cibles UI/anim/volume.
[DefaultExecutionOrder(-80)]
[DisallowMultipleComponent]
public class BraseroDisplayManager : MonoBehaviour
{
    public static BraseroDisplayManager ActiveInstance { get; private set; }

    private static readonly HashSet<IBraseroDisplayTarget> RegisteredTargets = new HashSet<IBraseroDisplayTarget>();
    private static readonly List<IBraseroDisplayTarget> RefreshBuffer = new List<IBraseroDisplayTarget>();
    private static readonly List<IBraseroDisplayTarget> RemovalBuffer = new List<IBraseroDisplayTarget>();

    [SerializeField, Tooltip("Ecrit un log quand les affichages de braseros sont rafraichis.")]
    private bool logRefreshes;

    public static void Register(IBraseroDisplayTarget target)
    {
        if (IsNullTarget(target))
        {
            return;
        }

        RegisteredTargets.Add(target);
        GetOrCreate();
        ApplyToTarget(target, GetCurrentSnapshot());
    }

    public static void Unregister(IBraseroDisplayTarget target)
    {
        if (target == null)
        {
            return;
        }

        RegisteredTargets.Remove(target);
    }

    public static void RefreshAllDisplays()
    {
        if (RegisteredTargets.Count == 0)
        {
            return;
        }

        GetOrCreate();
        BraseroDisplaySnapshot snapshot = GetCurrentSnapshot();
        RefreshBuffer.Clear();
        RemovalBuffer.Clear();

        foreach (IBraseroDisplayTarget target in RegisteredTargets)
        {
            if (IsNullTarget(target))
            {
                RemovalBuffer.Add(target);
                continue;
            }

            RefreshBuffer.Add(target);
        }

        for (int i = 0; i < RemovalBuffer.Count; i++)
        {
            RegisteredTargets.Remove(RemovalBuffer[i]);
        }

        RemovalBuffer.Clear();

        for (int i = 0; i < RefreshBuffer.Count; i++)
        {
            ApplyToTarget(RefreshBuffer[i], snapshot);
        }

        RefreshBuffer.Clear();

        if (ActiveInstance != null && ActiveInstance.logRefreshes)
        {
            Debug.Log(
                $"[BraseroDisplay] year={snapshot.CurrentYear} lit={snapshot.LitCount}/{snapshot.TotalCount} targets={RegisteredTargets.Count}",
                ActiveInstance);
        }
    }

    public static BraseroDisplaySnapshot GetCurrentSnapshot()
    {
        AgeManager ageManager = ResolveAgeManager();
        if (ageManager != null)
        {
            return FromAgeManager(ageManager);
        }

        BraseroTimeManager timeManager = ResolveLegacyTimeManager();
        return timeManager != null ? FromTimeManager(timeManager) : BraseroDisplaySnapshot.Default();
    }

    public static BraseroDisplayManager GetOrCreate()
    {
        if (ActiveInstance != null)
        {
            return ActiveInstance;
        }

#if UNITY_2023_1_OR_NEWER
        BraseroDisplayManager existing = FindFirstObjectByType<BraseroDisplayManager>();
#else
        BraseroDisplayManager existing = FindObjectOfType<BraseroDisplayManager>();
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

        GameObject host = new GameObject("BraseroDisplayManager");
        return host.AddComponent<BraseroDisplayManager>();
    }

    private void OnEnable()
    {
        if (ActiveInstance != null && ActiveInstance != this)
        {
            Debug.LogWarning("Plusieurs BraseroDisplayManager actifs. Le premier reste le diffuseur d'affichage.", this);
            return;
        }

        ActiveInstance = this;
        RefreshAllDisplays();
    }

    private void OnDisable()
    {
        if (ReferenceEquals(ActiveInstance, this))
        {
            ActiveInstance = null;
        }
    }

    private static BraseroDisplaySnapshot FromAgeManager(AgeManager manager)
    {
        int totalCount = manager.Braseros != null ? manager.Braseros.Count : 0;
        return new BraseroDisplaySnapshot(
            true,
            manager.CurrentYear,
            manager.LitBrazierCount,
            totalCount,
            manager.StartYear,
            manager.YearsPerBrasero,
            manager.CurrentTemporalAge,
            manager.CurrentTemporalAgeStep);
    }

    private static BraseroDisplaySnapshot FromTimeManager(BraseroTimeManager manager)
    {
        return new BraseroDisplaySnapshot(
            true,
            manager.CurrentYear,
            manager.LitCount,
            manager.TotalCount,
            manager.baseYear,
            manager.yearsPerBrasero,
            manager.CurrentTemporalAge,
            TemporalAgeUtility.AgeToStep(manager.CurrentTemporalAge));
    }

    private static void ApplyToTarget(IBraseroDisplayTarget target, BraseroDisplaySnapshot snapshot)
    {
        if (!IsNullTarget(target))
        {
            target.ApplyBraseroDisplay(snapshot);
        }
    }

    private static AgeManager ResolveAgeManager()
    {
        if (AgeManager.ActiveInstance != null)
        {
            return AgeManager.ActiveInstance;
        }

#if UNITY_2023_1_OR_NEWER
        return FindFirstObjectByType<AgeManager>();
#else
        return FindObjectOfType<AgeManager>();
#endif
    }

    private static BraseroTimeManager ResolveLegacyTimeManager()
    {
        if (BraseroTimeManager.ActiveInstance != null)
        {
            return BraseroTimeManager.ActiveInstance;
        }

#if UNITY_2023_1_OR_NEWER
        return FindFirstObjectByType<BraseroTimeManager>();
#else
        return FindObjectOfType<BraseroTimeManager>();
#endif
    }

    private static bool IsNullTarget(IBraseroDisplayTarget target)
    {
        if (target == null)
        {
            return true;
        }

        Object unityObject = target as Object;
        return !ReferenceEquals(unityObject, null) && unityObject == null;
    }
}
