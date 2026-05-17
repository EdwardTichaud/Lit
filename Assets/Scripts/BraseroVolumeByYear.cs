using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Cible d'affichage: change le Volume profile depuis BraseroDisplayManager.
[DisallowMultipleComponent]
public class BraseroVolumeByYear : MonoBehaviour, IBraseroDisplayTarget
{
    [Serializable]
    public struct YearProfile
    {
        [Tooltip("Annee minimale pour activer ce profil.")]
        public int minYear;
        [Tooltip("Profile a appliquer pour cette tranche.")]
        public VolumeProfile profile;
    }

    [Header("References")]
    [Tooltip("Volume cible a piloter.")]
    public Volume targetVolume;
    [Tooltip("Cherche automatiquement un Volume si non assigne.")]
    public bool autoFindVolume = true;

    [Header("Display")]
    [Tooltip("Valeur comparee pour choisir le profil.")]
    public TimePeriodValueMode valueMode = TimePeriodValueMode.YearOffsetFromBase;

    [Header("Profiles")]
    [Tooltip("Profil par defaut si aucune regle ne matche.")]
    public VolumeProfile defaultProfile;
    [Tooltip("Regles de selection par annee.")]
    public List<YearProfile> profiles = new List<YearProfile>();

    private VolumeProfile currentProfile;

    public VolumeProfile CurrentProfile => currentProfile;

    private void OnEnable()
    {
        ResolveTargetVolume();
        BraseroDisplayManager.Register(this);
    }

    private void OnDisable()
    {
        BraseroDisplayManager.Unregister(this);
    }

    public void ApplyBraseroDisplay(BraseroDisplaySnapshot snapshot)
    {
        ApplyForValue(snapshot.GetComparisonValue(valueMode));
    }

    public void ApplyForCurrentYear()
    {
        ApplyBraseroDisplay(BraseroDisplayManager.GetCurrentSnapshot());
    }

    public void ApplyForYear(int year)
    {
        ApplyForValue(year);
    }

    public void ApplyForValue(int currentValue)
    {
        ResolveTargetVolume();
        if (targetVolume == null)
        {
            return;
        }

        VolumeProfile selected = SelectProfile(currentValue);
        if (selected == null)
        {
            selected = defaultProfile;
        }

        if (selected == null || ReferenceEquals(selected, currentProfile))
        {
            return;
        }

        targetVolume.sharedProfile = selected;
        currentProfile = selected;
    }

    private void ResolveTargetVolume()
    {
        if (targetVolume != null || !autoFindVolume)
        {
            return;
        }

        targetVolume = GetComponent<Volume>();
        if (targetVolume != null)
        {
            return;
        }

#if UNITY_2023_1_OR_NEWER
        targetVolume = FindFirstObjectByType<Volume>();
#else
        targetVolume = FindObjectOfType<Volume>();
#endif
    }

    private VolumeProfile SelectProfile(int currentValue)
    {
        VolumeProfile bestProfile = null;
        int bestMinYear = int.MinValue;

        for (int i = 0; i < profiles.Count; i++)
        {
            YearProfile entry = profiles[i];
            if (entry.profile == null)
            {
                continue;
            }

            if (currentValue >= entry.minYear && entry.minYear >= bestMinYear)
            {
                bestMinYear = entry.minYear;
                bestProfile = entry.profile;
            }
        }

        return bestProfile;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            ResolveTargetVolume();
            ApplyForCurrentYear();
        }
    }
#endif
}
