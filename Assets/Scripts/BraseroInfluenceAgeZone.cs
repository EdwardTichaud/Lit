using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(TemporalZone))]
public class BraseroInfluenceAgeZone : MonoBehaviour, ILitInfluenceReceiver
{
    private static readonly HashSet<BraseroInfluenceAgeZone> ActiveZones = new HashSet<BraseroInfluenceAgeZone>();
    private static readonly Dictionary<int, int> ActiveBraseroSourceRefs = new Dictionary<int, int>();
    private static readonly List<int> SourceRemovalBuffer = new List<int>();

    [SerializeField, Tooltip("Zone temporelle pilotee par les braseros allumes. Laisse vide pour utiliser ce GameObject.")]
    private TemporalZone temporalZone;
    [SerializeField, Tooltip("Remet la zone a l'age de depart quand aucun brasero allume ne l'influence.")]
    private bool resetWhenNoBraseroInfluence = true;
    [SerializeField, Tooltip("Ecrit un log quand cette zone applique un age de brasero.")]
    private bool logAgeChanges;

    private readonly HashSet<int> localBraseroSources = new HashSet<int>();

    private void Reset()
    {
        temporalZone = GetComponent<TemporalZone>();
    }

    private void Awake()
    {
        ResolveTemporalZone();
    }

    private void OnDisable()
    {
        ClearLocalSources();
    }

    public void OnLitInfluenceEnter(LitInfluenceInfo info)
    {
        if (info.SourceKind != LitInfluenceSourceKind.Brasero || info.SourceId == 0)
        {
            return;
        }

        if (!localBraseroSources.Add(info.SourceId))
        {
            return;
        }

        AddSourceRef(info.SourceId);
        ActiveZones.Add(this);
        ApplySharedAgeToActiveZones();
    }

    public void OnLitInfluenceStay(LitInfluenceInfo info)
    {
    }

    public void OnLitInfluenceExit(LitInfluenceInfo info)
    {
        if (info.SourceKind != LitInfluenceSourceKind.Brasero || info.SourceId == 0)
        {
            return;
        }

        if (!localBraseroSources.Remove(info.SourceId))
        {
            return;
        }

        RemoveSourceRef(info.SourceId);
        if (localBraseroSources.Count == 0)
        {
            ActiveZones.Remove(this);
            ResetZoneAgeIfNeeded();
        }

        ApplySharedAgeToActiveZones();
    }

    private void ClearLocalSources()
    {
        if (localBraseroSources.Count == 0)
        {
            return;
        }

        SourceRemovalBuffer.Clear();
        foreach (int sourceId in localBraseroSources)
        {
            SourceRemovalBuffer.Add(sourceId);
        }

        for (int i = 0; i < SourceRemovalBuffer.Count; i++)
        {
            RemoveSourceRef(SourceRemovalBuffer[i]);
        }

        SourceRemovalBuffer.Clear();
        localBraseroSources.Clear();
        ActiveZones.Remove(this);
        ResetZoneAgeIfNeeded();
        ApplySharedAgeToActiveZones();
    }

    private void ApplyAgeFromYear(int year)
    {
        ResolveTemporalZone();
        if (temporalZone == null)
        {
            return;
        }

        temporalZone.SetAgeFromInt(year);

        if (logAgeChanges)
        {
            Debug.Log(
                $"[BraseroInfluenceAgeZone] zone='{name}' year={year} activeBraseros={ActiveBraseroSourceRefs.Count}",
                this);
        }
    }

    private void ResetZoneAgeIfNeeded()
    {
        if (!resetWhenNoBraseroInfluence)
        {
            return;
        }

        ApplyAgeFromYear(ResolveStartYear());
    }

    private void ResolveTemporalZone()
    {
        if (temporalZone == null)
        {
            temporalZone = GetComponent<TemporalZone>();
        }
    }

    private static void ApplySharedAgeToActiveZones()
    {
        int year = ResolveSharedYear();

        foreach (BraseroInfluenceAgeZone zone in ActiveZones)
        {
            if (zone != null)
            {
                zone.ApplyAgeFromYear(year);
            }
        }
    }

    private static int ResolveSharedYear()
    {
        int litBraseroCount = Mathf.Max(0, ActiveBraseroSourceRefs.Count);
        return Mathf.Clamp(
            ResolveStartYear() - litBraseroCount * ResolveYearsPerBrasero(),
            TemporalAgeUtility.MinYear,
            TemporalAgeUtility.MaxYear);
    }

    private static int ResolveStartYear()
    {
        AgeManager manager = AgeManager.ActiveInstance;
        return manager != null ? manager.StartYear : AgeManager.DefaultStartYear;
    }

    private static int ResolveYearsPerBrasero()
    {
        AgeManager manager = AgeManager.ActiveInstance;
        return manager != null ? manager.YearsPerBrasero : AgeManager.DefaultYearsPerBrasero;
    }

    private static void AddSourceRef(int sourceId)
    {
        if (ActiveBraseroSourceRefs.TryGetValue(sourceId, out int count))
        {
            ActiveBraseroSourceRefs[sourceId] = count + 1;
            return;
        }

        ActiveBraseroSourceRefs.Add(sourceId, 1);
    }

    private static void RemoveSourceRef(int sourceId)
    {
        if (!ActiveBraseroSourceRefs.TryGetValue(sourceId, out int count))
        {
            return;
        }

        count--;
        if (count <= 0)
        {
            ActiveBraseroSourceRefs.Remove(sourceId);
            return;
        }

        ActiveBraseroSourceRefs[sourceId] = count;
    }
}
