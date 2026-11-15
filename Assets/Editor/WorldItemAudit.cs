using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using System.Text.RegularExpressions;

internal static class WorldItemAudit
{
    [MenuItem("Lit/Objets monde/Auditer les assets et les scènes")]
    private static void Audit()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        WriteReport(true);
    }

    internal static void WriteReport(bool showProgress)
    {
        string report = BuildReport(showProgress);
        Directory.CreateDirectory("Library/WorldItemAudit");
        File.WriteAllText("Library/WorldItemAudit/Report.txt", report);
        Debug.Log("Audit en lecture seule : Library/WorldItemAudit/Report.txt");
    }

    public static void RunAuditBatch() => WriteReport(false);

    internal static string BuildReport(bool showProgress)
    {
        var report = new StringBuilder("Audit des objets monde — aucune correction automatique\n");
        var items = WorldItemAuthoring.AllItems();
        var paths = AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/", StringComparison.Ordinal))
            .Where(p => p.EndsWith(".asset") || p.EndsWith(".prefab") || p.EndsWith(".unity") ||
                p.EndsWith(".controller") || p.EndsWith(".overrideController")).OrderBy(p => p, StringComparer.Ordinal).ToArray();
        var referrers = items.Select(AssetDatabase.GetAssetPath).Distinct().ToDictionary(p => p, _ => new List<string>());
        var diagnostics = new StringBuilder();
        var registries = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
        var itemsByGuid = items.GroupBy(item => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(item)))
            .ToDictionary(group => group.Key, group => group.ToArray());
        int scanned = 0;
        try
        {
            foreach (string path in paths)
            {
                if (showProgress && scanned % 16 == 0 && EditorUtility.DisplayCancelableProgressBar(
                        "Audit des objets monde", path, (float)scanned / Mathf.Max(paths.Length, 1))) break;
                scanned++;
                foreach (string dependency in AssetDatabase.GetDependencies(path, false))
                    if (dependency != path && referrers.TryGetValue(dependency, out var usages)) usages.Add(path);
                try
                {
                    if (path.EndsWith(".prefab"))
                    {
                        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) is GameObject prefab)
                            InspectRoot(prefab, path, diagnostics, registries);
                    }
                    else if (path.EndsWith(".unity"))
                        InspectSceneText(path, diagnostics, itemsByGuid);
                }
                catch (Exception ex)
                {
                    diagnostics.AppendLine($"LECTURE INCOMPLÈTE {path}: {ex.Message}");
                }
            }
        }
        finally { if (showProgress) EditorUtility.ClearProgressBar(); }

        report.AppendLine($"Définitions : {items.Length}. Fichiers parcourus : {scanned}/{paths.Length}.");
        if (scanned != paths.Length) report.AppendLine("Audit interrompu : les références listées sont partielles.");
        report.AppendLine("Une absence de référence directe n’autorise pas une suppression : sauvegardes et accès par ID restent possibles.");
        report.AppendLine("Scènes : lecture de la sérialisation, sans exécuter les composants. Les overrides de prefab qui demandent une résolution sont signalés pour revue.");
        report.AppendLine("\nIDENTIFIANTS ET RÉFÉRENCES");
        foreach (var group in items.GroupBy(ItemIdUtils.GetItemId).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            bool collision = group.Count() > 1;
            if (collision)
            {
                report.AppendLine($"ID PARTAGÉ : {group.Key} (la première définition enregistrée gagne dans chaque registre)");
                if (registries.TryGetValue(group.Key, out var entries))
                    foreach (string entry in entries) report.AppendLine("  Première entrée auteur : " + entry);
            }
            foreach (Item item in group)
            {
                string path = AssetDatabase.GetAssetPath(item);
                var usages = referrers[path];
                report.AppendLine($"  {group.Key} | {path} | {item.name}" +
                    (string.IsNullOrWhiteSpace(item.itemId) ? " | ID IMPLICITE" : "") +
                    (item.ResolveWorldPrefab() == null ? " | sans prefab monde" : ""));
                if (usages.Count == 0) report.AppendLine("    Aucune dépendance directe détectée");
                else foreach (string usage in usages) report.AppendLine("    " + usage);
            }
        }
        report.AppendLine("\nPLACEMENTS ET INSTANCES");
        report.Append(diagnostics);
        return report.ToString();
    }

    private static void InspectRoot(GameObject root, string path, StringBuilder report, IDictionary<string, List<string>> registries)
    {
        foreach (var registry in root.GetComponentsInChildren<ItemRegistry>(true))
        {
            if (registry.items == null) continue;
            var seen = new HashSet<string>();
            foreach (Item item in registry.items)
            {
                string id = ItemIdUtils.GetItemId(item);
                if (string.IsNullOrEmpty(id) || !seen.Add(id)) continue;
                if (!registries.TryGetValue(id, out var entries)) registries.Add(id, entries = new List<string>());
                entries.Add(path + " → " + AssetDatabase.GetAssetPath(item));
            }
        }
        foreach (var marker in root.GetComponentsInChildren<SceneMarker>(true))
        {
            if (!marker.UsesItem) continue;
            string place = Location(path, marker.transform);
            if (!marker.HasBakedItemLink) report.AppendLine($"MARKER NON LIÉ {place}");
            else if (marker.BakedItemInteraction == null ||
                !marker.BakedItemInteraction.transform.IsChildOf(marker.transform))
                report.AppendLine($"LIEN CASSÉ {place}");
            else if (marker.BakedItemInteraction.representedItem != marker.Item ||
                marker.BakedItemSourcePrefab != null && marker.BakedItemSourcePrefab != marker.ResolvePreviewPrefab())
                report.AppendLine($"DIVERGENCE MARKER/INSTANCE {place}");
            if (!WorldItemAuthoring.TryCategory(marker.Item, marker.Placement, out var role, out string reason))
                report.AppendLine($"USAGE NON RÉSOLU {place}: {reason}");
            else if (marker.BakedItemInteraction != null && role != marker.BakedItemInteraction.interactableCategory)
                report.AppendLine($"USAGE À ACTUALISER {place}");
        }
        foreach (var interaction in root.GetComponentsInChildren<InteractableItem>(true))
        {
            string place = Location(path, interaction.transform);
            if (interaction.GetComponentsInParent<InteractableItem>(true).Length > 1 ||
                interaction.GetComponentsInChildren<InteractableItem>(true).Length > 1)
                report.AppendLine($"INTERACTIONS IMBRIQUÉES {place}");
            if (interaction.representedItem == null && interaction.interactableCategory != InteractableItem.InteractableCategory.Container)
                report.AppendLine($"ITEM REPRÉSENTÉ MANQUANT {place}");
            if (interaction.IsFixedWorldDocument &&
                (interaction.representedItem == null || !interaction.representedItem.IsReadable() ||
                    interaction.storedItems != null && interaction.storedItems.Count != 0))
                report.AppendLine($"DOCUMENT FIXE INCOHÉRENT {place} (données conservées)");
            if (!interaction.IsFixedWorldDocument && interaction.representedItem != null && interaction.representedItem.IsReadableStab())
                report.AppendLine($"STÈLE EN MODE NON FIXE {place} (à vérifier)");
        }
#pragma warning disable CS0618
        foreach (var legacy in root.GetComponentsInChildren<ItemSceneMarker>(true))
            report.AppendLine($"MARKER HISTORIQUE {Location(path, legacy.transform)} (données conservées)");
#pragma warning restore CS0618
    }

    private static string Location(string path, Transform target)
    {
        string name = target.name;
        for (var parent = target.parent; parent != null; parent = parent.parent) name = parent.name + "/" + name;
        return path + " :: " + name;
    }

    private const string MarkerScript = "1f8c4db6b1a9475e9e5b9d80b20ee401";
    private const string InteractionScript = "ccd7488eb2c54b14fb0812256450a344";
    private const string LegacyScript = "d95b8452a73aad75214a186711867545";
    private static readonly Regex Header = new Regex(@"^--- !u!(\d+) &(-?\d+)", RegexOptions.Multiline);
    private static readonly Regex GuidValue = new Regex(@"guid:\s*([a-fA-F0-9]{32})");
    private static readonly Regex FileId = new Regex(@"fileID:\s*(-?\d+)");

    private static string Field(string block, string key)
    {
        int start = block.IndexOf("\n  " + key + ":", StringComparison.Ordinal);
        if (start < 0) return null;
        start += key.Length + 4;
        int end = block.IndexOf('\n', start);
        return (end < 0 ? block.Substring(start) : block.Substring(start, end - start)).Trim();
    }

    private static string RefId(string value) => FileId.Match(value ?? "").Groups[1].Value;

    private static void InspectSceneText(string path, StringBuilder report, IDictionary<string, Item[]> itemsByGuid)
    {
        string text = File.ReadAllText(path);
        if (!text.StartsWith("%YAML", StringComparison.Ordinal))
        { report.AppendLine($"REVUE MANUELLE {path}: scène non sérialisée en texte."); return; }
        if (text.IndexOf(MarkerScript, StringComparison.Ordinal) < 0 &&
            text.IndexOf(InteractionScript, StringComparison.Ordinal) < 0 &&
            text.IndexOf(LegacyScript, StringComparison.Ordinal) < 0) return;

        var blocks = Header.Matches(text);
        var names = new Dictionary<string, string>();
        var interactions = new Dictionary<string, string>();
        var markers = new List<(string id, string block)>();
        var owners = new Dictionary<string, int>();
        for (int i = 0; i < blocks.Count; i++)
        {
            var header = blocks[i];
            string kind = header.Groups[1].Value;
            string id = header.Groups[2].Value;
            if (kind != "1" && kind != "114" && kind != "1001") continue;
            int end = i + 1 < blocks.Count ? blocks[i + 1].Index : text.Length;
            string block = text.Substring(header.Index, end - header.Index);
            if (kind == "1") { names[id] = Field(block, "m_Name") ?? ""; continue; }
            if (kind == "1001")
            {
                if (Regex.IsMatch(block, @"propertyPath:\s*(assetType|itemPlacement|bakedItemInteraction|interactableCategory|representedItem|storedItems)(\s|\.)"))
                    report.AppendLine($"OVERRIDE À VÉRIFIER {path} :: prefab #{id}");
                continue;
            }
            string script = GuidValue.Match(Field(block, "m_Script") ?? "").Groups[1].Value;
            if (script == MarkerScript || script == LegacyScript) markers.Add((id, block));
            if (script != InteractionScript) continue;
            interactions[id] = block;
            string owner = RefId(Field(block, "m_GameObject"));
            owners.TryGetValue(owner, out int count);
            owners[owner] = count + 1;
        }

        foreach (var marker in markers)
        {
            string place = Describe(marker.id, marker.block);
            string script = GuidValue.Match(Field(marker.block, "m_Script") ?? "").Groups[1].Value;
            if (script == LegacyScript) { report.AppendLine($"MARKER HISTORIQUE {place}"); continue; }
            if (Field(marker.block, "assetType") != "1") continue;
            if (Field(marker.block, "hasBakedItemLink") != "1") report.AppendLine($"MARKER NON LIÉ {place}");
            else
            {
                string link = Field(marker.block, "bakedItemInteraction");
                if (!interactions.TryGetValue(RefId(link), out string interaction))
                    report.AppendLine($"LIEN À VÉRIFIER {place} (absent de la scène brute ou hérité)");
                else if (Field(marker.block, "item") != Field(interaction, "representedItem"))
                    report.AppendLine($"DIVERGENCE MARKER/INSTANCE {place}");
            }
            string placement = Field(marker.block, "itemPlacement");
            if (placement == null || placement == "0") report.AppendLine($"USAGE NON RÉSOLU {place}");
        }
        foreach (var pair in interactions)
        {
            string block = pair.Value;
            string place = Describe(pair.Key, block);
            string role = Field(block, "interactableCategory") ?? "0";
            string represented = Field(block, "representedItem");
            string guid = GuidValue.Match(represented ?? "").Groups[1].Value;
            if (owners.TryGetValue(RefId(Field(block, "m_GameObject")), out int count) && count > 1)
                report.AppendLine($"PLUSIEURS INTERACTIONS SUR LA MÊME RACINE {place}");
            if (role != "0" && (RefId(represented) == "" || RefId(represented) == "0"))
                report.AppendLine($"ITEM REPRÉSENTÉ MANQUANT {place}");
            if (role == "2" && ((Field(block, "storedItems") ?? "[]") != "[]" ||
                itemsByGuid.TryGetValue(guid, out var definitions) && definitions.All(item => !item.IsReadable())))
                report.AppendLine($"DOCUMENT FIXE INCOHÉRENT {place} (données conservées)");
            if (role != "2" && itemsByGuid.TryGetValue(guid, out var possibleSteles) && possibleSteles.All(item => item.IsReadableStab()))
                report.AppendLine($"STÈLE EN MODE NON FIXE {place} (à vérifier)");
        }

        string Describe(string componentId, string block)
        {
            var ownerId = RefId(Field(block, "m_GameObject"));
            names.TryGetValue(ownerId, out string name);
            return $"{path} :: {name ?? "(racine héritée)"} [composant #{componentId}]";
        }
    }
}
