using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Change le Volume profile en fonction de l'annee courante.
[DisallowMultipleComponent]
public class BraseroVolumeByYear : MonoBehaviour
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
    [Tooltip("Manager des braseros.")]
    public BraseroTimeManager timeManager;
    [Tooltip("Volume cible a piloter.")]
    public Volume targetVolume;
    [Tooltip("Cherche automatiquement un manager si non assigne.")]
    public bool autoFindManager = true;
    [Tooltip("Cherche automatiquement un Volume si non assigne.")]
    public bool autoFindVolume = true;

    [Header("Profiles")]
    [Tooltip("Profil par defaut si aucune regle ne matche.")]
    public VolumeProfile defaultProfile;
    [Tooltip("Regles de selection par annee.")]
    public List<YearProfile> profiles = new List<YearProfile>();

    private VolumeProfile currentProfile;

    private void OnEnable()
    {
        ResolveReferences();
        Subscribe();
        ApplyForCurrentYear();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void ResolveReferences()
    {
        if (targetVolume == null && autoFindVolume)
        {
            targetVolume = GetComponent<Volume>();
            if (targetVolume == null)
            {
                targetVolume = FindObjectOfType<Volume>();
            }
        }

        if (timeManager == null && autoFindManager)
        {
            timeManager = FindObjectOfType<BraseroTimeManager>();
        }
    }

    private void Subscribe()
    {
        if (timeManager == null)
        {
            return;
        }

        timeManager.TimeChanged += OnTimeChanged;
    }

    private void Unsubscribe()
    {
        if (timeManager == null)
        {
            return;
        }

        timeManager.TimeChanged -= OnTimeChanged;
    }

    private void OnTimeChanged(int year, int litCount)
    {
        ApplyForYear(year);
    }

    private void ApplyForCurrentYear()
    {
        if (timeManager == null)
        {
            ApplyForYear(0);
            return;
        }

        ApplyForYear(timeManager.CurrentYear);
    }

    public void ApplyForYear(int year)
    {
        if (targetVolume == null)
        {
            return;
        }

        VolumeProfile selected = SelectProfile(year);
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

    private VolumeProfile SelectProfile(int year)
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

            if (year >= entry.minYear && entry.minYear >= bestMinYear)
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
            ResolveReferences();
            ApplyForCurrentYear();
        }
    }
#endif
}
