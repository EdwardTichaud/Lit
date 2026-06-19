using System.Collections.Generic;
using UnityEngine;

public interface IAncientFlameDisplayTarget
{
    void ApplyAncientFlameDisplay(AncientFlameDisplaySnapshot snapshot);
}

public struct AncientFlameDisplaySnapshot
{
    public bool HasSource;
    public int CurrentYear;
    public int LitCount;
    public int TotalCount;
    public int StartYear;
    public int YearsPerAncientFlame;
    public TemporalAge CurrentTemporalAge;
    public int CurrentTemporalAgeStep;

    public int CurrentYearOffsetFromStart => Mathf.Max(0, StartYear - CurrentYear);

    public AncientFlameDisplaySnapshot(
        bool hasSource,
        int currentYear,
        int litCount,
        int totalCount,
        int startYear,
        int yearsPerAncientFlame,
        TemporalAge currentTemporalAge,
        int currentTemporalAgeStep)
    {
        HasSource = hasSource;
        CurrentYear = currentYear;
        LitCount = litCount;
        TotalCount = totalCount;
        StartYear = startYear;
        YearsPerAncientFlame = yearsPerAncientFlame;
        CurrentTemporalAge = currentTemporalAge;
        CurrentTemporalAgeStep = currentTemporalAgeStep;
    }

    public int GetComparisonValue(TimePeriodValueMode valueMode)
    {
        switch (valueMode)
        {
            case TimePeriodValueMode.YearOffsetFromBase:
                return CurrentYearOffsetFromStart;

            case TimePeriodValueMode.LitAncientFlameCount:
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

    public static AncientFlameDisplaySnapshot Default()
    {
        return new AncientFlameDisplaySnapshot(
            false,
            AgeManager.DefaultStartYear,
            0,
            0,
            AgeManager.DefaultStartYear,
            AgeManager.DefaultYearsPerAncientFlame,
            TemporalAgeUtility.IntToAge(AgeManager.DefaultStartYear),
            TemporalAgeUtility.AgeToStep(TemporalAgeUtility.IntToAge(AgeManager.DefaultStartYear)));
    }
}

// Centralise les affichages dependants des flammes. AgeManager calcule l'etat;
// ce manager ne fait que diffuser un snapshot aux cibles UI/anim/volume.
[DefaultExecutionOrder(-80)]
[DisallowMultipleComponent]
public class AncientFlameDisplayManager : MonoBehaviour
{
    public static AncientFlameDisplayManager ActiveInstance { get; private set; }

    private static readonly HashSet<IAncientFlameDisplayTarget> RegisteredTargets = new HashSet<IAncientFlameDisplayTarget>();
    private static readonly List<IAncientFlameDisplayTarget> RefreshBuffer = new List<IAncientFlameDisplayTarget>();
    private static readonly List<IAncientFlameDisplayTarget> RemovalBuffer = new List<IAncientFlameDisplayTarget>();
    private static bool isApplicationQuitting;

    [SerializeField, Tooltip("Ecrit un log quand les affichages de flames sont rafraichis.")]
    private bool logRefreshes;

    public static void Register(IAncientFlameDisplayTarget target)
    {
        if (IsNullTarget(target))
        {
            return;
        }

        RegisteredTargets.Add(target);
        GetOrCreate();
        ApplyToTarget(target, GetCurrentSnapshot());
    }

    public static void Unregister(IAncientFlameDisplayTarget target)
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
        AncientFlameDisplaySnapshot snapshot = GetCurrentSnapshot();
        RefreshBuffer.Clear();
        RemovalBuffer.Clear();

        foreach (IAncientFlameDisplayTarget target in RegisteredTargets)
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
                $"[AncientFlameDisplay] year={snapshot.CurrentYear} lit={snapshot.LitCount}/{snapshot.TotalCount} targets={RegisteredTargets.Count}",
                ActiveInstance);
        }
    }

    public static AncientFlameDisplaySnapshot GetCurrentSnapshot()
    {
        AgeManager ageManager = ResolveAgeManager();
        return ageManager != null ? FromAgeManager(ageManager) : AncientFlameDisplaySnapshot.Default();
    }

    public static AncientFlameDisplayManager GetOrCreate()
    {
        if (isApplicationQuitting)
        {
            return null;
        }

        if (ActiveInstance != null)
        {
            return ActiveInstance;
        }

#if UNITY_2023_1_OR_NEWER
        AncientFlameDisplayManager existing = FindAnyObjectByType<AncientFlameDisplayManager>();
#else
        AncientFlameDisplayManager existing = FindAnyObjectByType<AncientFlameDisplayManager>();
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

        GameObject host = new GameObject("AncientFlameDisplayManager");
        return host.AddComponent<AncientFlameDisplayManager>();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        ActiveInstance = null;
        RegisteredTargets.Clear();
        RefreshBuffer.Clear();
        RemovalBuffer.Clear();
        isApplicationQuitting = false;
    }

    private void OnEnable()
    {
        if (ActiveInstance != null && ActiveInstance != this)
        {
            Debug.LogWarning("Plusieurs AncientFlameDisplayManager actifs. Le premier reste le diffuseur d'affichage.", this);
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

    private void OnApplicationQuit()
    {
        isApplicationQuitting = true;
    }

    private static AncientFlameDisplaySnapshot FromAgeManager(AgeManager manager)
    {
        return new AncientFlameDisplaySnapshot(
            true,
            manager.CurrentYear,
            manager.LitAncientFlameCount,
            manager.TotalAncientFlameCount,
            manager.StartYear,
            manager.YearsPerAncientFlame,
            manager.CurrentTemporalAge,
            manager.CurrentTemporalAgeStep);
    }

    private static void ApplyToTarget(IAncientFlameDisplayTarget target, AncientFlameDisplaySnapshot snapshot)
    {
        if (!IsNullTarget(target))
        {
            target.ApplyAncientFlameDisplay(snapshot);
        }
    }

    private static AgeManager ResolveAgeManager()
    {
        if (AgeManager.ActiveInstance != null)
        {
            return AgeManager.ActiveInstance;
        }

#if UNITY_2023_1_OR_NEWER
        return FindAnyObjectByType<AgeManager>();
#else
        return FindAnyObjectByType<AgeManager>();
#endif
    }

    private static bool IsNullTarget(IAncientFlameDisplayTarget target)
    {
        if (target == null)
        {
            return true;
        }

        Object unityObject = target as Object;
        return !ReferenceEquals(unityObject, null) && unityObject == null;
    }
}
