using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class TimePeriodVisibility : MonoBehaviour
{
    public enum RuleMode
    {
        Range,
        AllowedValues
    }

    public enum MatchBehavior
    {
        VisibleWhenMatched,
        HiddenWhenMatched
    }

    private static readonly HashSet<TimePeriodVisibility> RegisteredInstances = new HashSet<TimePeriodVisibility>();
    private static readonly List<TimePeriodVisibility> RefreshBuffer = new List<TimePeriodVisibility>();
    private static readonly List<TimePeriodVisibility> RemovalBuffer = new List<TimePeriodVisibility>();

    [Header("References")]
    [SerializeField, Tooltip("Manager de temps a ecouter. Laisse vide pour utiliser celui de la scene.")]
    private BraseroTimeManager timeManager;
    [SerializeField, Tooltip("Cherche automatiquement le BraseroTimeManager actif si aucun n'est assigne.")]
    private bool autoFindManager = true;
    [SerializeField, Tooltip("Objet a activer ou desactiver. Si vide, le composant pilote son propre GameObject.")]
    private GameObject targetObject;

    [Header("Rule")]
    [SerializeField, Tooltip("Compare soit l'annee absolue, soit l'offset depuis l'annee de base, soit le nombre de braseros allumes.")]
    private TimePeriodValueMode valueMode = TimePeriodValueMode.AbsoluteYear;
    [SerializeField, Tooltip("Choisit entre une plage continue et une liste de valeurs exactes.")]
    private RuleMode ruleMode = RuleMode.Range;
    [SerializeField, Tooltip("Visible seulement quand la regle matche, ou cache seulement quand elle matche.")]
    private MatchBehavior matchBehavior = MatchBehavior.VisibleWhenMatched;
    [SerializeField, Tooltip("Active la borne minimale quand RuleMode = Range.")]
    private bool useMinValue = true;
    [SerializeField, Tooltip("Valeur minimale incluse.")]
    private int minValue;
    [SerializeField, Tooltip("Active la borne maximale quand RuleMode = Range.")]
    private bool useMaxValue = false;
    [SerializeField, Tooltip("Valeur maximale incluse.")]
    private int maxValue;
    [SerializeField, Tooltip("Valeurs exactes autorisees quand RuleMode = AllowedValues.")]
    private List<int> allowedValues = new List<int>();

    [Header("Diagnostics")]
    [SerializeField, Tooltip("Ecrit un log quand ce composant applique un changement de visibilite.")]
    private bool logStateChanges = false;

    private bool hasAppliedVisibility;
    private bool lastVisibleState = true;
    private int lastAppliedValue = int.MinValue;
    private bool warnedMissingManager;
    private bool warnedMissingTarget;

    public GameObject TargetObject => targetObject != null ? targetObject : gameObject;
    public bool IsVisibleNow => TargetObject != null && TargetObject.activeSelf;

    private void Awake()
    {
        RegisterSelf();
    }

    private void OnEnable()
    {
        RegisterSelf();
        ResolveReferences();
        RefreshForCurrentPeriod();
    }

    private void OnDestroy()
    {
        UnregisterSelf();
    }

    public static void RefreshAllForManager(BraseroTimeManager manager, bool rescanScene)
    {
        if (manager == null)
        {
            return;
        }

        if (rescanScene)
        {
            RegisterSceneInstances();
        }

        RefreshBuffer.Clear();
        RemovalBuffer.Clear();

        foreach (TimePeriodVisibility instance in RegisteredInstances)
        {
            if (instance == null)
            {
                RemovalBuffer.Add(instance);
                continue;
            }

            RefreshBuffer.Add(instance);
        }

        for (int i = 0; i < RemovalBuffer.Count; i++)
        {
            RegisteredInstances.Remove(RemovalBuffer[i]);
        }

        for (int i = 0; i < RefreshBuffer.Count; i++)
        {
            TimePeriodVisibility instance = RefreshBuffer[i];
            if (instance != null)
            {
                instance.RefreshForManager(manager);
            }
        }
    }

    [ContextMenu("Refresh For Current Period")]
    public void RefreshForCurrentPeriod()
    {
        ResolveReferences();
        BraseroTimeManager manager = ResolveTimeManager();
        if (manager == null)
        {
            if (!warnedMissingManager && (!autoFindManager || logStateChanges))
            {
                Debug.LogWarning($"[TimePeriod] No BraseroTimeManager found for '{name}'.", this);
                warnedMissingManager = true;
            }

            return;
        }

        warnedMissingManager = false;
        ApplyForManager(manager, force: true);
    }

    private void RefreshForManager(BraseroTimeManager manager)
    {
        if (!ShouldReactTo(manager))
        {
            return;
        }

        warnedMissingManager = false;
        ApplyForManager(manager, force: false);
    }

    private void ApplyForManager(BraseroTimeManager manager, bool force)
    {
        GameObject target = TargetObject;
        if (target == null)
        {
            if (!warnedMissingTarget)
            {
                Debug.LogWarning($"[TimePeriod] No target object configured for '{name}'.", this);
                warnedMissingTarget = true;
            }

            return;
        }

        warnedMissingTarget = false;

        int currentValue = manager.GetComparisonValue(valueMode);
        bool matches = IsRuleMatched(currentValue);
        bool shouldBeVisible = matchBehavior == MatchBehavior.VisibleWhenMatched ? matches : !matches;
        bool targetAlreadyMatches = target.activeSelf == shouldBeVisible;

        if (!force && hasAppliedVisibility && lastAppliedValue == currentValue && lastVisibleState == shouldBeVisible && targetAlreadyMatches)
        {
            return;
        }

        if (!targetAlreadyMatches)
        {
            target.SetActive(shouldBeVisible);
        }

        if (logStateChanges)
        {
            Debug.Log(
                $"[TimePeriod] target='{target.name}' valueMode={valueMode} currentValue={currentValue} ruleMode={ruleMode} visible={shouldBeVisible}",
                this);
        }

        hasAppliedVisibility = true;
        lastAppliedValue = currentValue;
        lastVisibleState = shouldBeVisible;
    }

    private bool IsRuleMatched(int currentValue)
    {
        switch (ruleMode)
        {
            case RuleMode.AllowedValues:
                return MatchesAllowedValues(currentValue);

            case RuleMode.Range:
            default:
                return MatchesRange(currentValue);
        }
    }

    private bool MatchesRange(int currentValue)
    {
        if (useMinValue && currentValue < minValue)
        {
            return false;
        }

        if (useMaxValue && currentValue > maxValue)
        {
            return false;
        }

        return true;
    }

    private bool MatchesAllowedValues(int currentValue)
    {
        if (allowedValues == null || allowedValues.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < allowedValues.Count; i++)
        {
            if (allowedValues[i] == currentValue)
            {
                return true;
            }
        }

        return false;
    }

    private bool ShouldReactTo(BraseroTimeManager manager)
    {
        if (manager == null)
        {
            return false;
        }

        if (timeManager != null)
        {
            return ReferenceEquals(timeManager, manager);
        }

        if (!autoFindManager)
        {
            return false;
        }

        timeManager = manager;
        return true;
    }

    private BraseroTimeManager ResolveTimeManager()
    {
        if (timeManager != null)
        {
            return timeManager;
        }

        if (!autoFindManager)
        {
            return null;
        }

        timeManager = BraseroTimeManager.ActiveInstance;
        if (timeManager != null)
        {
            return timeManager;
        }

#if UNITY_2023_1_OR_NEWER
        timeManager = FindFirstObjectByType<BraseroTimeManager>();
#else
        timeManager = FindObjectOfType<BraseroTimeManager>();
#endif
        return timeManager;
    }

    private void ResolveReferences()
    {
        ResolveTimeManager();
    }

    private static void RegisterSceneInstances()
    {
#if UNITY_2023_1_OR_NEWER
        TimePeriodVisibility[] sceneInstances = FindObjectsByType<TimePeriodVisibility>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        TimePeriodVisibility[] sceneInstances = FindObjectsOfType<TimePeriodVisibility>(true);
#endif
        if (sceneInstances == null)
        {
            return;
        }

        for (int i = 0; i < sceneInstances.Length; i++)
        {
            if (sceneInstances[i] != null)
            {
                RegisteredInstances.Add(sceneInstances[i]);
            }
        }
    }

    private void RegisterSelf()
    {
        RegisteredInstances.Add(this);
    }

    private void UnregisterSelf()
    {
        RegisteredInstances.Remove(this);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (useMinValue && useMaxValue && maxValue < minValue)
        {
            maxValue = minValue;
        }
    }
#endif
}
