using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class WorldRulesStateManager : MonoBehaviour
{
    public const string BrazierLitCountKey = "world.braziers.lit_count";
    public const string BrazierTotalCountKey = "world.braziers.total_count";
    public const string CurrentYearKey = "world.time.current_year";
    public const string CurrentTemporalAgeKey = "world.time.temporal_age";
    public const string CurrentTemporalAgeStepKey = "world.time.temporal_age_step";
    public const string ActiveVolumeProfilesKey = "world.environment.active_volume_profiles";

    [SerializeField] private AgeManager ageManager;
    [SerializeField] private bool autoResolveAgeManagers = true;

    private readonly Dictionary<string, WorldVariableSnapshot> variables = new Dictionary<string, WorldVariableSnapshot>();

    public event Action VariablesChanged;

    private void OnEnable()
    {
        ResolveReferences();
        Subscribe();
        RebuildDerivedBrazierVariables();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    public void ResetRuntimeState(string reason = null)
    {
        variables.Clear();
        VariablesChanged?.Invoke();
    }

    public void SetInt(string key, int value)
    {
        Upsert(new WorldVariableSnapshot
        {
            Key = key,
            ValueType = WorldVariableValueType.Int,
            IntValue = value
        });
    }

    public void SetFloat(string key, float value)
    {
        Upsert(new WorldVariableSnapshot
        {
            Key = key,
            ValueType = WorldVariableValueType.Float,
            FloatValue = value
        });
    }

    public void SetBool(string key, bool value)
    {
        Upsert(new WorldVariableSnapshot
        {
            Key = key,
            ValueType = WorldVariableValueType.Bool,
            BoolValue = value
        });
    }

    public void SetString(string key, string value)
    {
        Upsert(new WorldVariableSnapshot
        {
            Key = key,
            ValueType = WorldVariableValueType.String,
            StringValue = value ?? string.Empty
        });
    }

    public bool TryGetInt(string key, out int value)
    {
        if (variables.TryGetValue(key ?? string.Empty, out WorldVariableSnapshot snapshot) &&
            snapshot != null &&
            snapshot.ValueType == WorldVariableValueType.Int)
        {
            value = snapshot.IntValue;
            return true;
        }

        value = default;
        return false;
    }

    public bool TryGetString(string key, out string value)
    {
        if (variables.TryGetValue(key ?? string.Empty, out WorldVariableSnapshot snapshot) &&
            snapshot != null &&
            snapshot.ValueType == WorldVariableValueType.String)
        {
            value = snapshot.StringValue ?? string.Empty;
            return true;
        }

        value = string.Empty;
        return false;
    }

    public List<WorldVariableSnapshot> CaptureVariables()
    {
        List<WorldVariableSnapshot> results = new List<WorldVariableSnapshot>(variables.Count);
        foreach (KeyValuePair<string, WorldVariableSnapshot> pair in variables)
        {
            WorldVariableSnapshot source = pair.Value;
            if (source == null)
            {
                continue;
            }

            results.Add(new WorldVariableSnapshot
            {
                Key = source.Key,
                ValueType = source.ValueType,
                IntValue = source.IntValue,
                FloatValue = source.FloatValue,
                BoolValue = source.BoolValue,
                StringValue = source.StringValue
            });
        }

        results.Sort((left, right) => string.CompareOrdinal(left.Key, right.Key));
        return results;
    }

    public void ApplyVariables(IReadOnlyList<WorldVariableSnapshot> incoming)
    {
        variables.Clear();
        if (incoming == null)
        {
            VariablesChanged?.Invoke();
            return;
        }

        for (int i = 0; i < incoming.Count; i++)
        {
            WorldVariableSnapshot snapshot = incoming[i];
            if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.Key))
            {
                continue;
            }

            variables[snapshot.Key] = new WorldVariableSnapshot
            {
                Key = snapshot.Key,
                ValueType = snapshot.ValueType,
                IntValue = snapshot.IntValue,
                FloatValue = snapshot.FloatValue,
                BoolValue = snapshot.BoolValue,
                StringValue = snapshot.StringValue
            };
        }

        VariablesChanged?.Invoke();
    }

    public void RebuildDerivedBrazierVariables()
    {
        ResolveReferences();
        if (ageManager == null)
        {
            return;
        }

        int litCount = ageManager.LitBrazierCount;
        int totalCount = ageManager.TotalAgeDrivingBraseroCount;
        int currentYear = ageManager.CurrentYear;
        TemporalAge currentAge = ageManager.CurrentTemporalAge;

        SetInt(BrazierLitCountKey, litCount);
        SetInt(BrazierTotalCountKey, totalCount);
        SetInt(CurrentYearKey, currentYear);
        SetInt(CurrentTemporalAgeKey, TemporalAgeUtility.AgeToInt(currentAge));
        SetInt(CurrentTemporalAgeStepKey, TemporalAgeUtility.AgeToStep(currentAge));
        SetString(ActiveVolumeProfilesKey, DescribeActiveVolumeProfiles());
    }

    public string DescribeActiveVolumeProfiles()
    {
        List<string> values = new List<string>();
#if UNITY_2023_1_OR_NEWER
        BraseroVolumeByYear[] managers = FindObjectsByType<BraseroVolumeByYear>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        BraseroVolumeByYear[] managers = FindObjectsOfType<BraseroVolumeByYear>(true);
#endif
        if (managers == null)
        {
            return string.Empty;
        }

        for (int i = 0; i < managers.Length; i++)
        {
            BraseroVolumeByYear manager = managers[i];
            if (manager == null)
            {
                continue;
            }

            string managerPath = PersistentWorldDebug.DescribeTransform(manager.transform);
            string profileName = manager.CurrentProfile != null ? manager.CurrentProfile.name : "<none>";
            values.Add($"{managerPath}=>{profileName}");
        }

        values.Sort(StringComparer.Ordinal);
        return string.Join("|", values);
    }

    private void ResolveReferences()
    {
        if (!autoResolveAgeManagers)
        {
            return;
        }

        if (ageManager == null)
        {
            ageManager = AgeManager.ActiveInstance;
            if (ageManager == null)
            {
#if UNITY_2023_1_OR_NEWER
                ageManager = FindFirstObjectByType<AgeManager>();
#else
                ageManager = FindObjectOfType<AgeManager>();
#endif
            }
        }
    }

    private void Subscribe()
    {
        if (ageManager != null)
        {
            ageManager.AgeChanged += OnAgeChanged;
        }
    }

    private void Unsubscribe()
    {
        if (ageManager != null)
        {
            ageManager.AgeChanged -= OnAgeChanged;
        }
    }

    private void OnAgeChanged(AgeManager manager, int previousYear, int currentYear)
    {
        RebuildDerivedBrazierVariables();
    }

    private void Upsert(WorldVariableSnapshot snapshot)
    {
        if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.Key))
        {
            return;
        }

        variables[snapshot.Key] = snapshot;
        VariablesChanged?.Invoke();
    }
}
