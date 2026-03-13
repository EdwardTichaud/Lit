using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class WorldRulesStateManager : MonoBehaviour
{
    public const string BrazierLitCountKey = "world.braziers.lit_count";
    public const string BrazierTotalCountKey = "world.braziers.total_count";
    public const string CurrentYearKey = "world.time.current_year";
    public const string ActiveStageRootsKey = "world.environment.active_stage_roots";
    public const string ActiveVolumeProfilesKey = "world.environment.active_volume_profiles";

    [SerializeField] private BraseroTimeManager braseroTimeManager;
    [SerializeField] private bool autoResolveBraseroTimeManager = true;

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
        if (braseroTimeManager == null)
        {
            return;
        }

        SetInt(BrazierLitCountKey, braseroTimeManager.LitCount);
        SetInt(BrazierTotalCountKey, braseroTimeManager.braseros != null ? braseroTimeManager.braseros.Count : 0);
        SetInt(CurrentYearKey, braseroTimeManager.CurrentYear);
        SetString(ActiveStageRootsKey, DescribeActiveStageRoots());
        SetString(ActiveVolumeProfilesKey, DescribeActiveVolumeProfiles());
    }

    public string DescribeActiveStageRoots()
    {
        List<string> values = new List<string>();
#if UNITY_2023_1_OR_NEWER
        BraseroYearStageManager[] managers = FindObjectsByType<BraseroYearStageManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        BraseroYearStageManager[] managers = FindObjectsOfType<BraseroYearStageManager>(true);
#endif
        if (managers == null)
        {
            return string.Empty;
        }

        for (int i = 0; i < managers.Length; i++)
        {
            BraseroYearStageManager manager = managers[i];
            if (manager == null)
            {
                continue;
            }

            string managerPath = PersistentWorldDebug.DescribeTransform(manager.transform);
            string currentRoot = manager.CurrentRoot != null
                ? PersistentWorldDebug.DescribeTransform(manager.CurrentRoot.transform)
                : "<none>";
            values.Add($"{managerPath}=>{currentRoot}");
        }

        values.Sort(StringComparer.Ordinal);
        return string.Join("|", values);
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
        if (braseroTimeManager == null && autoResolveBraseroTimeManager)
        {
#if UNITY_2023_1_OR_NEWER
            braseroTimeManager = FindFirstObjectByType<BraseroTimeManager>();
#else
            braseroTimeManager = FindObjectOfType<BraseroTimeManager>();
#endif
        }
    }

    private void Subscribe()
    {
        if (braseroTimeManager == null)
        {
            return;
        }

        braseroTimeManager.TimeChanged += OnBraseroTimeChanged;
    }

    private void Unsubscribe()
    {
        if (braseroTimeManager == null)
        {
            return;
        }

        braseroTimeManager.TimeChanged -= OnBraseroTimeChanged;
    }

    private void OnBraseroTimeChanged(int currentYear, int litCount)
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
