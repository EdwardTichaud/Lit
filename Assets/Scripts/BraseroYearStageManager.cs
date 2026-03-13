using System;
using System.Collections.Generic;
using UnityEngine;

// Active une racine de scene en fonction de l'annee courante.
[DisallowMultipleComponent]
public class BraseroYearStageManager : MonoBehaviour
{
    [Serializable]
    public struct YearStage
    {
        [Tooltip("Annee minimale pour activer cette racine.")]
        public int minYear;
        [Tooltip("Racine de scene a activer pour cette tranche.")]
        public GameObject root;
    }

    [Header("References")]
    [Tooltip("Manager des braseros.")]
    public BraseroTimeManager timeManager;
    [Tooltip("Cherche automatiquement un manager si non assigne.")]
    public bool autoFindManager = true;

    [Header("Stages")]
    [Tooltip("Racine par defaut si aucune regle ne matche.")]
    public GameObject defaultRoot;
    [Tooltip("Regles de selection par annee.")]
    public List<YearStage> stages = new List<YearStage>();

    private GameObject currentRoot;

    public GameObject CurrentRoot => currentRoot;

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
            return;
        }

        ApplyForYear(timeManager.CurrentYear);
    }

    public void ApplyForYear(int year)
    {
        GameObject selected = SelectRoot(year);
        ApplySelection(selected);
        currentRoot = selected;
    }

    private GameObject SelectRoot(int year)
    {
        GameObject bestRoot = null;
        int bestMinYear = int.MinValue;

        for (int i = 0; i < stages.Count; i++)
        {
            YearStage stage = stages[i];
            if (stage.root == null)
            {
                continue;
            }

            if (year >= stage.minYear && stage.minYear >= bestMinYear)
            {
                bestMinYear = stage.minYear;
                bestRoot = stage.root;
            }
        }

        return bestRoot;
    }

    private void ApplySelection(GameObject selected)
    {
        if (defaultRoot != null)
        {
            defaultRoot.SetActive(true);
        }

        HashSet<GameObject> roots = new HashSet<GameObject>();
        for (int i = 0; i < stages.Count; i++)
        {
            if (stages[i].root != null)
            {
                roots.Add(stages[i].root);
            }
        }

        foreach (GameObject root in roots)
        {
            if (root != null && !ReferenceEquals(root, defaultRoot))
            {
                root.SetActive(root == selected);
            }
        }
    }
}
