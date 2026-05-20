// Role:
// Lightweight editor tooling for rebuilding readable Item pages from DistrictRegistry data.
// Usage:
// Select a DistrictRegistry and use the inspector button, or run
// Lit/Narrative/Rebuild District Registry Readables for all registry assets.
// Responsibilities:
// Keep generated Item book pages synchronized with resident data and report the
// temporal page counts used by runtime filtering.
// Dependencies:
// UnityEditor AssetDatabase, DistrictRegistry, Item.
// Precautions:
// The tool only writes the assigned readable Item assets; resident data remains
// the source of truth.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DistrictRegistry))]
public class DistrictRegistryEditorTools : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        DistrictRegistry registry = (DistrictRegistry)target;
        EditorGUILayout.Space();
        if (GUILayout.Button("Rebuild Readable Item Pages (Age666)"))
        {
            RebuildRegistry(registry);
        }

        if (GUILayout.Button("Log Registry Validation"))
        {
            LogRegistryValidation(registry);
        }
    }

    [MenuItem("Lit/Narrative/Rebuild District Registry Readables")]
    private static void RebuildAllRegistries()
    {
        string[] guids = AssetDatabase.FindAssets("t:DistrictRegistry");
        int rebuilt = 0;
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            DistrictRegistry registry = AssetDatabase.LoadAssetAtPath<DistrictRegistry>(path);
            if (registry == null)
            {
                continue;
            }

            RebuildRegistry(registry);
            rebuilt++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"DistrictRegistry: rebuilt {rebuilt} readable item(s).");
    }

    private static void RebuildRegistry(DistrictRegistry registry)
    {
        if (registry == null)
        {
            return;
        }

        if (registry.readableItem == null)
        {
            Debug.LogWarning($"DistrictRegistry '{registry.name}' has no readable Item assigned.", registry);
            return;
        }

        Undo.RecordObject(registry.readableItem, "Rebuild District Registry Readable");
        registry.ApplyToReadableItem();
        EditorUtility.SetDirty(registry);
        EditorUtility.SetDirty(registry.readableItem);
        AssetDatabase.SaveAssets();
        LogRegistryValidation(registry);
    }

    private static void LogRegistryValidation(DistrictRegistry registry)
    {
        if (registry == null)
        {
            return;
        }

        registry.GetEventGroupCounts(out int ordinary, out int relocations, out int anomalies);
        List<string> duplicates = registry.FindDuplicateResidentIds();
        int pageCount = registry.BuildReadablePages().Count;
        int pages111 = registry.BuildReadablePagesForAge(TemporalAge.Age111).Count;
        int pages333 = registry.BuildReadablePagesForAge(TemporalAge.Age333).Count;
        int pages555 = registry.BuildReadablePagesForAge(TemporalAge.Age555).Count;
        string duplicateText = duplicates.Count == 0 ? "none" : string.Join(", ", duplicates);
        Debug.Log(
            $"DistrictRegistry '{registry.name}': residents={registry.Residents.Count}, pages666={pageCount}, pages111={pages111}, pages333={pages333}, pages555={pages555}, ordinary={ordinary}, relocations={relocations}, anomalies={anomalies}, duplicateIds={duplicateText}",
            registry);
    }
}
