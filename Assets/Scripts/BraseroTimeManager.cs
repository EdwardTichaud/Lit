using System;
using System.Collections.Generic;
using UnityEngine;

// Manager des braseros qui pilote le temps global du chateau.
[DisallowMultipleComponent]
public class BraseroTimeManager : MonoBehaviour
{
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

    [Header("State")]
    [SerializeField, Tooltip("Nombre de braseros allumes.")]
    private int litCount;
    [SerializeField, Tooltip("Annee courante calculee.")]
    private int currentYear;

    public int LitCount => litCount;
    public int CurrentYear => currentYear;

    public event Action<int, int> TimeChanged;

    private void OnEnable()
    {
        if (autoCollectChildren)
        {
            RefreshBraseros();
        }

        Subscribe();
        RecalculateTime();
    }

    private void OnDisable()
    {
        Unsubscribe();
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

    public void RecalculateTime()
    {
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
        TimeChanged?.Invoke(currentYear, litCount);
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
