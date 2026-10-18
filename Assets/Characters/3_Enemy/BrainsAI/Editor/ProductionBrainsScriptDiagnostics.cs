using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>Read-only evidence for unresolved prefab component slots, not an asset-repair heuristic.</summary>
public static class ProductionBrainsScriptDiagnostics
{
    [Serializable]
    public sealed class Slot
    {
        public string gameObjectPath, serializedAssetPath, scriptGuid, guidToAssetPath, className, status;
        public int componentSlot;
        public long gameObjectFileId, componentFileId;
        public bool monoScriptResolved;
    }

    public static List<Slot> Inspect(GameObject prefab)
    {
        var result = new List<Slot>();
        if (prefab == null) return result;
        foreach (var transform in prefab.GetComponentsInChildren<Transform>(true))
        {
            var owner = transform.gameObject;
            var nativeComponents = owner.GetComponents<Component>();
            var components = new SerializedObject(owner).FindProperty("m_Component");
            if (components == null || !components.isArray) continue;
            for (int i = 0; i < components.arraySize; i++)
            {
                var reference = components.GetArrayElementAtIndex(i).FindPropertyRelative("component");
                if (reference == null || i < nativeComponents.Length && nativeComponents[i] != null) continue;
                var slot = new Slot { gameObjectPath = HierarchyPath(transform), componentSlot = i };
                var rawComponent = reference.objectReferenceValue;
                if (rawComponent != null)
                {
                    var scriptReference = new SerializedObject(rawComponent).FindProperty("m_Script")?.objectReferenceValue;
                    if (scriptReference != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(scriptReference, out string guid, out long _))
                    {
                        slot.scriptGuid = guid;
                        slot.serializedAssetPath = AssetDatabase.GetAssetPath(rawComponent);
                        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(rawComponent, out string _, out slot.componentFileId);
                        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(owner, out string _, out slot.gameObjectFileId);
                    }
                }
                if (string.IsNullOrEmpty(slot.scriptGuid) && !ReadReference(owner, i, slot))
                {
                    // Nested prefab objects can be stripped records in the containing asset.
                    var original = PrefabUtility.GetCorrespondingObjectFromOriginalSource(owner);
                    if (original != null && original != owner) ReadReference(original, i, slot);
                }
                DescribeScript(slot);
                result.Add(slot);
            }
        }
        return result;
    }

    public static void DescribeScript(Slot slot)
    {
        if (string.IsNullOrEmpty(slot.scriptGuid))
        { slot.status = "Serialized script reference unavailable; no source-absence conclusion."; return; }
        slot.guidToAssetPath = AssetDatabase.GUIDToAssetPath(slot.scriptGuid);
        if (string.IsNullOrEmpty(slot.guidToAssetPath))
        { slot.status = "GUID unresolved in AssetDatabase; source metadata/package import must be checked separately."; return; }
        var script = AssetDatabase.LoadAssetAtPath<MonoScript>(slot.guidToAssetPath);
        slot.monoScriptResolved = script != null;
        if (script == null)
        { slot.status = "Asset path resolves, but MonoScript import does not."; return; }
        var type = script.GetClass();
        slot.className = type != null ? type.FullName : "<null>";
        slot.status = type != null ? "Class resolves; component slot remains unresolved in the imported prefab." :
            "MonoScript resolves, but MonoScript.GetClass() is null; inspect compilation/import/type availability.";
    }

    private static string HierarchyPath(Transform transform)
    {
        var names = new List<string>();
        for (var current = transform; current != null; current = current.parent)
            names.Add(current.name + "[" + current.GetSiblingIndex() + "]");
        names.Reverse();
        return string.Join("/", names);
    }

    private static bool ReadReference(GameObject owner, int slotIndex, Slot slot)
    {
        if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(owner, out string assetGuid, out long localId)) return false;
        string path = AssetDatabase.GUIDToAssetPath(assetGuid);
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
        if (!TryReadSerializedReference(File.ReadAllText(path), localId, slotIndex, out long componentId, out string scriptGuid)) return false;
        slot.serializedAssetPath = path;
        slot.gameObjectFileId = localId;
        slot.componentFileId = componentId;
        slot.scriptGuid = scriptGuid;
        return true;
    }

    // Unity does not expose m_Script for a null Component. This bounded reader only
    // extracts native YAML document IDs and object-reference fields; it never rewrites YAML.
    public static bool TryReadSerializedReference(string text, long ownerId, int slotIndex, out long componentId, out string scriptGuid)
    {
        componentId = 0; scriptGuid = null;
        if (string.IsNullOrEmpty(text) || slotIndex < 0) return false;
        var headers = Regex.Matches(text, @"(?m)^--- !u!(\d+) &(-?\d+)(?: stripped)?\r?$");
        var documents = new Dictionary<long, string>();
        for (int i = 0; i < headers.Count; i++)
        {
            int start = headers[i].Index + headers[i].Length;
            int end = i + 1 < headers.Count ? headers[i + 1].Index : text.Length;
            documents[long.Parse(headers[i].Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture)] = text.Substring(start, end - start);
        }
        if (!documents.TryGetValue(ownerId, out string owner)) return false;
        var references = Regex.Matches(owner, @"(?m)^\s+- component: \{fileID: (-?\d+)\}\r?$");
        if (slotIndex >= references.Count) return false;
        componentId = long.Parse(references[slotIndex].Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        if (!documents.TryGetValue(componentId, out string component)) return false;
        var script = Regex.Match(component, @"(?m)^\s+m_Script: \{fileID: -?\d+, guid: ([0-9a-fA-F]{32}), type: \d+\}\r?$");
        if (!script.Success) return false;
        scriptGuid = script.Groups[1].Value;
        return true;
    }
}
