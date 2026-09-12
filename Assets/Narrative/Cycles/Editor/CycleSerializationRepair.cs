using System;
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class CycleSerializationRepair
{
    [Serializable] private sealed class Capture { public Entry[] entries; }
    [Serializable] private sealed class Entry { public string path, kind, guid, text; public long fileId, integer; public double number; }
    static CycleSerializationRepair() => EditorApplication.update += Poll;
    private static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode ||
            !File.Exists("Library/CycleScientistRepair.request")) return;
        File.Delete("Library/CycleScientistRepair.request");
        try { Repair(); File.WriteAllText("Library/CycleScientistRepair.result", "PASS: authored values restored with Unity serialization"); }
        catch (Exception error) { File.WriteAllText("Library/CycleScientistRepair.result", error.ToString()); }
    }
    private static void Repair()
    {
        const string path = "Assets/Narrative/NinaCycle/Data/Enemy_ScientifiqueFou.asset";
        var data = AssetDatabase.LoadAssetAtPath<CharacterData>(path);
        var capture = JsonUtility.FromJson<Capture>(File.ReadAllText("Library/CycleScientistAuthored.json"));
        var serialized = new SerializedObject(data);
        foreach (var entry in capture.entries)
        {
            var property = serialized.FindProperty(entry.path);
            if (property == null && entry.path.EndsWith(".m_Bits")) property = serialized.FindProperty(entry.path.Substring(0, entry.path.Length - 7));
            if (property == null) throw new InvalidOperationException("Unresolved author field: " + entry.path);
            if (entry.kind == "array") { property.arraySize = (int)entry.integer; continue; }
            if (entry.kind == "reference")
            {
                UnityEngine.Object reference = null;
                if (entry.fileId != 0)
                {
                    foreach (var candidate in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(entry.guid)))
                        if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(candidate, out string guid, out long id) && id == entry.fileId) { reference = candidate; break; }
                    if (reference == null) throw new InvalidOperationException("Unresolved reference: " + entry.path + " / " + entry.guid);
                }
                property.objectReferenceValue = reference;
                continue;
            }
            switch (property.propertyType)
            {
                case SerializedPropertyType.Boolean: property.boolValue = entry.integer != 0; break;
                case SerializedPropertyType.Float: property.doubleValue = entry.kind == "integer" ? entry.integer : entry.number; break;
                case SerializedPropertyType.Integer: property.longValue = entry.integer; break;
                case SerializedPropertyType.Enum: case SerializedPropertyType.LayerMask: property.intValue = (int)entry.integer; break;
                case SerializedPropertyType.String: property.stringValue = entry.text ?? ""; break;
                default: throw new InvalidOperationException("Unsupported field: " + entry.path + " / " + property.propertyType);
            }
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssetIfDirty(data);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        if (!data.enemyEncounterOptions.startAsGhost || !data.enemyDeathOptions.enabled || data.enemyEncounterOptions.requiredKnowledge == null)
            throw new InvalidOperationException("Encounter options did not survive Unity serialization.");
    }
}
