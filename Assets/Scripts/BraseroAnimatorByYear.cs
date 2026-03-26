using System;
using System.Collections.Generic;
using UnityEngine;

// Met a jour des Animator bools "An_XXXX" en fonction de l'annee courante.
[DisallowMultipleComponent]
public class BraseroAnimatorByYear : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Manager des braseros.")]
    public BraseroTimeManager timeManager;
    [Tooltip("Cherche automatiquement un manager si non assigne.")]
    public bool autoFindManager = true;
    [Tooltip("Valeur comparee pour choisir le bool d'Animator.")]
    public TimePeriodValueMode valueMode = TimePeriodValueMode.YearOffsetFromBase;

    [Header("Animators")]
    [Tooltip("Liste des animators a piloter (si vide, auto-collect).")]
    public List<Animator> animators = new List<Animator>();
    [Tooltip("Inclut les enfants lors de l'auto-collect.")]
    public bool includeChildren = true;
    [Tooltip("Inclut les animators inactifs lors de l'auto-collect.")]
    public bool includeInactive = true;

    [Header("Parameters")]
    [Tooltip("Prefix des bools d'annee (ex: An_500).")]
    public string parameterPrefix = "An_";

    private void OnEnable()
    {
        ResolveReferences();
        CacheAnimatorsIfNeeded();
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
            timeManager = BraseroTimeManager.ActiveInstance;
            if (timeManager == null)
            {
#if UNITY_2023_1_OR_NEWER
                timeManager = FindFirstObjectByType<BraseroTimeManager>();
#else
                timeManager = FindObjectOfType<BraseroTimeManager>();
#endif
            }
        }
    }

    private void CacheAnimatorsIfNeeded()
    {
        if (animators != null && animators.Count > 0)
        {
            return;
        }

        if (animators == null)
        {
            animators = new List<Animator>();
        }

        animators.Clear();

        if (includeChildren)
        {
            GetComponentsInChildren(includeInactive, animators);
            return;
        }

        Animator animator = GetComponent<Animator>();
        if (animator != null)
        {
            animators.Add(animator);
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
        int currentValue = timeManager != null ? timeManager.GetComparisonValue(valueMode) : year;
        ApplyForYear(currentValue);
    }

    private void ApplyForCurrentYear()
    {
        if (timeManager == null)
        {
            return;
        }

        ApplyForYear(timeManager.GetComparisonValue(valueMode));
    }

    public void ApplyForYear(int year)
    {
        if (animators == null || animators.Count == 0)
        {
            CacheAnimatorsIfNeeded();
        }

        for (int i = 0; i < animators.Count; i++)
        {
            ApplyForAnimator(animators[i], year);
        }
    }

    private void ApplyForAnimator(Animator animator, int year)
    {
        if (animator == null)
        {
            return;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        if (parameters == null || parameters.Length == 0)
        {
            return;
        }

        string prefix = string.IsNullOrEmpty(parameterPrefix) ? "An_" : parameterPrefix;
        int bestYear = int.MinValue;
        string bestParam = null;

        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter param = parameters[i];
            if (param == null || param.type != AnimatorControllerParameterType.Bool)
            {
                continue;
            }

            string name = param.name;
            if (string.IsNullOrEmpty(name) || !name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string suffix = name.Substring(prefix.Length);
            if (!int.TryParse(suffix, out int paramYear))
            {
                continue;
            }

            if (paramYear <= year && paramYear >= bestYear)
            {
                bestYear = paramYear;
                bestParam = name;
            }
        }

        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter param = parameters[i];
            if (param == null || param.type != AnimatorControllerParameterType.Bool)
            {
                continue;
            }

            string name = param.name;
            if (string.IsNullOrEmpty(name) || !name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            animator.SetBool(name, name == bestParam);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(parameterPrefix))
        {
            parameterPrefix = "An_";
        }
    }
#endif
}
