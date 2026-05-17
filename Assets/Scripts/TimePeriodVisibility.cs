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

    public enum VisibilityApplicationMode
    {
        RenderersOnly = 0,
        GameObjectActive = 1,
        RenderersCollidersAndBehaviours = 2
    }

    private static readonly HashSet<TimePeriodVisibility> RegisteredInstances = new HashSet<TimePeriodVisibility>();
    private static readonly List<TimePeriodVisibility> RefreshBuffer = new List<TimePeriodVisibility>();
    private static readonly List<TimePeriodVisibility> RemovalBuffer = new List<TimePeriodVisibility>();

    [Header("References")]
    [SerializeField, Tooltip("AgeManager canonique a ecouter. Laisse vide pour utiliser celui de la scene.")]
    private AgeManager ageManager;
    [SerializeField, Tooltip("Ancien manager garde comme fallback pendant la migration.")]
    private BraseroTimeManager timeManager;
    [SerializeField, Tooltip("Cherche automatiquement un manager actif si aucun n'est assigne.")]
    private bool autoFindManager = true;
    [SerializeField, Tooltip("Objet a piloter. Si vide, le composant pilote son propre GameObject.")]
    private GameObject targetObject;

    [Header("Rule")]
    [SerializeField, Tooltip("Compare l'annee absolue, l'offset depuis le depart, le nombre de braseros ou l'age temporel.")]
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

    [Header("Torch Reveal")]
    [SerializeField, Tooltip("Autorise une torche locale a reveler cet objet si sa periode croise la fenetre Age courant -> +110 ans.")]
    private bool includeTorchRevealWindow = true;

    [Header("Application")]
    [SerializeField, Tooltip("RenderersOnly garde l'objet actif pour les triggers de torche; GameObjectActive conserve l'ancien comportement.")]
    private VisibilityApplicationMode applicationMode = VisibilityApplicationMode.RenderersOnly;

    [Header("Diagnostics")]
    [SerializeField, Tooltip("Ecrit un log quand ce composant applique un changement de visibilite.")]
    private bool logStateChanges = false;

    private readonly Dictionary<LocalRuntimeAgeTrigger, int> localRevealSourceCounts = new Dictionary<LocalRuntimeAgeTrigger, int>();
    private readonly List<LocalRuntimeAgeTrigger> staleRevealSources = new List<LocalRuntimeAgeTrigger>();

    private bool hasAppliedVisibility;
    private bool lastVisibleState = true;
    private int lastAppliedValue = int.MinValue;
    private int lastRevealSourceCount = -1;
    private bool warnedMissingManager;
    private bool warnedMissingTarget;

    public GameObject TargetObject => targetObject != null ? targetObject : gameObject;
    public bool IsVisibleNow => lastVisibleState;

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

    public static bool IsVisibleFor(Component component)
    {
        if (component == null)
        {
            return true;
        }

        TimePeriodVisibility[] visibilities = component.GetComponentsInParent<TimePeriodVisibility>(true);
        for (int i = 0; i < visibilities.Length; i++)
        {
            TimePeriodVisibility visibility = visibilities[i];
            if (visibility != null && !visibility.IsVisibleNow)
            {
                return false;
            }
        }

        return true;
    }

    public static void RefreshAllForAgeManager(AgeManager manager, bool rescanScene)
    {
        if (manager == null)
        {
            return;
        }

        RefreshRegisteredInstances(rescanScene);
        for (int i = 0; i < RefreshBuffer.Count; i++)
        {
            TimePeriodVisibility instance = RefreshBuffer[i];
            if (instance != null)
            {
                instance.RefreshForAgeManager(manager);
            }
        }
    }

    public static void RefreshAllForManager(BraseroTimeManager manager, bool rescanScene)
    {
        if (manager == null)
        {
            return;
        }

        AgeManager activeAgeManager = AgeManager.ActiveInstance;
        if (activeAgeManager != null)
        {
            RefreshAllForAgeManager(activeAgeManager, rescanScene);
            return;
        }

        RefreshRegisteredInstances(rescanScene);
        for (int i = 0; i < RefreshBuffer.Count; i++)
        {
            TimePeriodVisibility instance = RefreshBuffer[i];
            if (instance != null)
            {
                instance.RefreshForLegacyManager(manager);
            }
        }
    }

    public void AddLocalRevealSource(LocalRuntimeAgeTrigger source, int delta)
    {
        if (source == null || delta == 0)
        {
            return;
        }

        localRevealSourceCounts.TryGetValue(source, out int count);
        count += delta;
        if (count <= 0)
        {
            localRevealSourceCounts.Remove(source);
        }
        else
        {
            localRevealSourceCounts[source] = count;
        }

        RefreshForCurrentPeriod();
    }

    [ContextMenu("Refresh For Current Period")]
    public void RefreshForCurrentPeriod()
    {
        ResolveReferences();
        RemoveStaleRevealSources();

        AgeManager resolvedAgeManager = ResolveAgeManager();
        if (resolvedAgeManager != null)
        {
            warnedMissingManager = false;
            ApplyForAgeManager(resolvedAgeManager, force: true);
            return;
        }

        BraseroTimeManager legacyManager = ResolveLegacyTimeManager();
        if (legacyManager == null)
        {
            if (!warnedMissingManager && (!autoFindManager || logStateChanges))
            {
                Debug.LogWarning($"[TimePeriod] No AgeManager or BraseroTimeManager found for '{name}'.", this);
                warnedMissingManager = true;
            }

            return;
        }

        warnedMissingManager = false;
        ApplyForLegacyManager(legacyManager, force: true);
    }

    private void RefreshForAgeManager(AgeManager manager)
    {
        if (!ShouldReactTo(manager))
        {
            return;
        }

        warnedMissingManager = false;
        ApplyForAgeManager(manager, force: false);
    }

    private void RefreshForLegacyManager(BraseroTimeManager manager)
    {
        if (!ShouldReactTo(manager))
        {
            return;
        }

        warnedMissingManager = false;
        ApplyForLegacyManager(manager, force: false);
    }

    private void ApplyForAgeManager(AgeManager manager, bool force)
    {
        int currentValue = manager.GetComparisonValue(valueMode);
        bool matches = IsRuleMatched(currentValue);
        bool localRevealMatches = HasLocalRevealSources() && MatchesTorchRevealWindow(manager);
        ApplyVisibility(currentValue, matches || localRevealMatches, force, "AgeManager");
    }

    private void ApplyForLegacyManager(BraseroTimeManager manager, bool force)
    {
        int currentValue = manager.GetComparisonValue(valueMode);
        ApplyVisibility(currentValue, IsRuleMatched(currentValue), force, "BraseroTimeManager");
    }

    private void ApplyVisibility(int currentValue, bool matches, bool force, string source)
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

        bool shouldBeVisible = matchBehavior == MatchBehavior.VisibleWhenMatched ? matches : !matches;
        int revealSourceCount = GetLocalRevealSourceCount();
        bool targetAlreadyMatches = IsTargetAlreadyInState(target, shouldBeVisible);

        if (!force
            && hasAppliedVisibility
            && lastAppliedValue == currentValue
            && lastVisibleState == shouldBeVisible
            && lastRevealSourceCount == revealSourceCount
            && targetAlreadyMatches)
        {
            return;
        }

        ApplyTargetVisibility(target, shouldBeVisible);

        if (logStateChanges)
        {
            Debug.Log(
                $"[TimePeriod] source={source} target='{target.name}' valueMode={valueMode} currentValue={currentValue} ruleMode={ruleMode} visible={shouldBeVisible} localRevealSources={revealSourceCount}",
                this);
        }

        hasAppliedVisibility = true;
        lastAppliedValue = currentValue;
        lastVisibleState = shouldBeVisible;
        lastRevealSourceCount = revealSourceCount;
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

    private bool MatchesTorchRevealWindow(AgeManager manager)
    {
        if (!includeTorchRevealWindow || manager == null)
        {
            return false;
        }

        if (valueMode == TimePeriodValueMode.LitBrazierCount)
        {
            return false;
        }

        manager.GetTorchRevealComparisonWindow(valueMode, out int windowMin, out int windowMax);

        switch (ruleMode)
        {
            case RuleMode.AllowedValues:
                return AnyAllowedValueInRange(windowMin, windowMax);

            case RuleMode.Range:
            default:
                return ConfiguredRangeOverlaps(windowMin, windowMax);
        }
    }

    private bool AnyAllowedValueInRange(int windowMin, int windowMax)
    {
        if (allowedValues == null || allowedValues.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < allowedValues.Count; i++)
        {
            int value = allowedValues[i];
            if (value >= windowMin && value <= windowMax)
            {
                return true;
            }
        }

        return false;
    }

    private bool ConfiguredRangeOverlaps(int windowMin, int windowMax)
    {
        int configuredMin = useMinValue ? minValue : int.MinValue;
        int configuredMax = useMaxValue ? maxValue : int.MaxValue;
        if (configuredMax < configuredMin)
        {
            int swap = configuredMin;
            configuredMin = configuredMax;
            configuredMax = swap;
        }

        return configuredMin <= windowMax && configuredMax >= windowMin;
    }

    private bool HasLocalRevealSources()
    {
        RemoveStaleRevealSources();
        return localRevealSourceCounts.Count > 0;
    }

    private int GetLocalRevealSourceCount()
    {
        RemoveStaleRevealSources();
        int count = 0;
        foreach (KeyValuePair<LocalRuntimeAgeTrigger, int> entry in localRevealSourceCounts)
        {
            count += Mathf.Max(0, entry.Value);
        }

        return count;
    }

    private void RemoveStaleRevealSources()
    {
        if (localRevealSourceCounts.Count == 0)
        {
            return;
        }

        staleRevealSources.Clear();
        foreach (KeyValuePair<LocalRuntimeAgeTrigger, int> entry in localRevealSourceCounts)
        {
            LocalRuntimeAgeTrigger source = entry.Key;
            if (source == null || !source.isActiveAndEnabled)
            {
                staleRevealSources.Add(source);
            }
        }

        for (int i = 0; i < staleRevealSources.Count; i++)
        {
            localRevealSourceCounts.Remove(staleRevealSources[i]);
        }

        staleRevealSources.Clear();
    }

    private bool IsTargetAlreadyInState(GameObject target, bool visible)
    {
        if (applicationMode == VisibilityApplicationMode.GameObjectActive)
        {
            return target.activeSelf == visible;
        }

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return true;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer != null && renderer.enabled != visible)
            {
                return false;
            }
        }

        return true;
    }

    private void ApplyTargetVisibility(GameObject target, bool visible)
    {
        if (applicationMode == VisibilityApplicationMode.GameObjectActive)
        {
            if (target.activeSelf != visible)
            {
                target.SetActive(visible);
            }

            return;
        }

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer != null)
            {
                renderer.enabled = visible;
            }
        }

        if (applicationMode != VisibilityApplicationMode.RenderersCollidersAndBehaviours)
        {
            return;
        }

        Collider[] colliders = target.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider != null)
            {
                collider.enabled = visible;
            }
        }

        Behaviour[] behaviours = target.GetComponentsInChildren<Behaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            Behaviour behaviour = behaviours[i];
            if (CanDriveBehaviour(behaviour))
            {
                behaviour.enabled = visible;
            }
        }
    }

    private bool CanDriveBehaviour(Behaviour behaviour)
    {
        return behaviour != null
            && behaviour != this
            && !(behaviour is TimePeriodVisibility)
            && !(behaviour is AgeManager)
            && !(behaviour is BraseroTimeManager)
            && !(behaviour is LocalRuntimeAgeTrigger);
    }

    private bool ShouldReactTo(AgeManager manager)
    {
        if (manager == null)
        {
            return false;
        }

        if (ageManager != null)
        {
            return ReferenceEquals(ageManager, manager);
        }

        if (!autoFindManager)
        {
            return false;
        }

        ageManager = manager;
        return true;
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

    private AgeManager ResolveAgeManager()
    {
        if (ageManager != null)
        {
            return ageManager;
        }

        if (!autoFindManager)
        {
            return null;
        }

        ageManager = AgeManager.ActiveInstance;
        if (ageManager != null)
        {
            return ageManager;
        }

#if UNITY_2023_1_OR_NEWER
        ageManager = FindFirstObjectByType<AgeManager>();
#else
        ageManager = FindObjectOfType<AgeManager>();
#endif
        return ageManager;
    }

    private BraseroTimeManager ResolveLegacyTimeManager()
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
        ResolveAgeManager();
        ResolveLegacyTimeManager();
    }

    private static void RefreshRegisteredInstances(bool rescanScene)
    {
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

        RemovalBuffer.Clear();
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
