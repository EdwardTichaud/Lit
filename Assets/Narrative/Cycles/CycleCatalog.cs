using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Explicit runtime references for definitions kept alongside their authored scenes.</summary>
[CreateAssetMenu(menuName = "Lit/Narrative/Cycle Catalog", fileName = "CycleCatalog")]
public sealed class CycleCatalog : ScriptableObject
{
    public CycleDefinition[] definitions = Array.Empty<CycleDefinition>();

    public static CycleDefinition[] LoadDefinitions()
    {
        var result = new List<CycleDefinition>();
        var seen = new HashSet<CycleDefinition>();
        foreach (var definition in Resources.LoadAll<CycleDefinition>("Narrative"))
            if (definition != null && seen.Add(definition)) result.Add(definition);
        foreach (var catalog in Resources.LoadAll<CycleCatalog>("Narrative"))
            foreach (var definition in catalog.definitions ?? Array.Empty<CycleDefinition>())
                if (definition != null && seen.Add(definition)) result.Add(definition);
        return result.ToArray();
    }
}
