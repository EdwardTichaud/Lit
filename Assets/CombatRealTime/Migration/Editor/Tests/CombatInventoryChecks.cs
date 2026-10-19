using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public static class CombatInventoryChecks
{
    public static IEnumerable<KeyValuePair<string, Action>> Cases()
    {
        yield return Case("Only exact builtin GUID namespaces are exempt", () => {
            Check(CombatInventoryReference.ClassifyGuid("0000000000000000f000000000000000", "", false) == "builtin-resource");
            Check(CombatInventoryReference.ClassifyGuid("0000000000000000e000000000000000", "", false) == "builtin-resource");
            Check(CombatInventoryReference.ClassifyGuid("00000000000000001234567890123456", "", false) == "missing-guid");
        });
        yield return Case("Editor resource namespace needs the exact engine-resolved library path", () => {
            const string guid = "0000000000000000d000000000000000";
            Check(CombatInventoryReference.ClassifyGuid(guid, "Library/unity editor resources", false) == "builtin-resource");
            Check(CombatInventoryReference.ClassifyGuid(guid, "", false) == "missing-guid");
            Check(CombatInventoryReference.ClassifyGuid(guid, "Assets/FakeEditorResource.asset", false) == "missing-guid-payload");
            Check(CombatInventoryReference.ClassifyGuid("0000000000000000a000000000000000", "Library/unity editor resources", false) == "missing-guid-payload");
        });
        yield return Case("Export-generated GUID prefixes need actual payload proof", () => {
            const string guid = "abc00000000001921638038075592800";
            Check(CombatInventoryReference.ClassifyGuid(guid, "", false) == "missing-guid");
            Check(CombatInventoryReference.ClassifyGuid(guid, "Assets/Stone.mat", false) == "missing-guid-payload");
            Check(CombatInventoryReference.ClassifyGuid(guid, "Assets/Stone.mat", true) == "asset-guid");
        });
        yield return Case("Comment stripping preserves strings offsets and call-site line numbers", () => {
            const string source = "/// Resources.Load(\"example\")\n/* Resources.Load(\"demo\") */\nvar x = Resources.Load(\"url//path\"); // tail\nvar y = @\"/*text*/\";";
            string code = CombatInventoryReference.WithoutComments(source);
            Check(code.Length == source.Length && code.Count(c => c == '\n') == source.Count(c => c == '\n'));
            Check(!code.Contains("example") && !code.Contains("demo") && code.Contains("url//path") && code.Contains("/*text*/"));
        });
        yield return Case("Constants resolve but mutable locals stay candidate-only", () => {
            const string source = "const string A = \"UI/A\"; const string B = \"UI/B\"; string resourcePath = flag ? A : B;";
            Check(CombatInventoryReference.TryConstant(source, "A", out string key) && key == "UI/A");
            Check(!CombatInventoryReference.TryConstant(source, "resourcePath", out _));
            Check(CombatInventoryReference.ConstantChoices(source, "resourcePath").SequenceEqual(new[] { "UI/A", "UI/B" }));
        });
        yield return Case("Resource namespaces include descendants without sibling expansion", () => {
            Check(CombatInventoryReference.TryResourcePrefix("\"CombatSupportSkills/\" + skill.name", out string prefix));
            var keys = new[] { "CombatSupportSkills/Heal", "CombatSupportSkills/Sub/Heal", "CombatSupportSkillsOther/Heal", "Vendor/Demo" };
            Check(CombatInventoryReference.ResourceCandidates(keys, prefix, true, false).SequenceEqual(keys.Take(2)));
            Check(!CombatInventoryReference.TryResourcePrefix("prefix + name", out _));
            Check(!CombatInventoryReference.TryResourcePrefix("\"CombatSupportSkills/\" + GetName()", out _));
        });
        yield return Case("Unused material texture slot needs every occurrence and a verified shader property check", () => {
            string guid = new string('a', 32);
            string material = "  m_SavedProperties:\n    m_TexEnvs:\n    - _OldTexture:\n        m_Texture: {fileID: 2800000, guid: " + guid + ", type: 3}\n    m_Floats: []\n";
            Check(CombatInventoryReference.UnusedTextureSlots(material, guid, _ => false).SequenceEqual(new[] { "_OldTexture" }));
            Check(CombatInventoryReference.UnusedTextureSlots(material, guid, _ => true).Length == 0);
            Check(CombatInventoryReference.UnusedTextureSlots("  m_Shader: {guid: " + guid + "}\n" + material, guid, _ => false).Length == 0);
        });
        yield return Case("Routes preserve separate identities sharing a prefab", () => {
            var graph = new CombatReachabilityGraph(path => path == "giant_juggernaut" || path == "giantjuggernaut" ? new[] { "shared-prefab" } : Array.Empty<string>());
            graph.AddRoot("giant_juggernaut", "identity-1"); graph.AddRoot("giantjuggernaut", "identity-2");
            Check(graph.Roots.Count == 2 && graph.Reachable.Count == 3);
            Check(graph.RouteTo("giantjuggernaut").SequenceEqual(new[] { "giantjuggernaut" }));
            Check(graph.RouteTo("shared-prefab").Length == 2 && graph.RouteTo("absent").Length == 0);
            graph.AddRoot("new-root", "late-runtime-load");
            Check(graph.RouteTo("new-root").SequenceEqual(new[] { "new-root" }));
        });
        yield return Case("Historical dynamic findings cover five namespace calls two constants three mutable calls and two comments", () => {
            int namespaces = 0, constants = 0, mutable = 0;
            foreach (string path in new[] { "Assets/Scripts/AudioManager.cs", "Assets/Scripts/BuildingInfoInteractable.cs", "Assets/Scripts/LegacyBuildingSystemSettings.cs",
                "Assets/CombatRealTime/Skills/Healing/NetworkCharacterInput.Healing.cs", "Assets/CombatRealTime/Skills/Protection/NetworkCharacterInput.Protection.cs",
                "Assets/CombatRealTime/Skills/Control/NetworkCharacterInput.Control.cs", "Assets/CombatRealTime/Enemies/EnemyDeathPresentation.cs" })
            {
                string source = CombatInventoryReference.WithoutComments(File.ReadAllText(path));
                foreach (System.Text.RegularExpressions.Match call in CombatInventoryReference.Loads.Matches(source))
                {
                    string expression = call.Groups[2].Value.Trim();
                    if (CombatInventoryReference.TryConstant(source, expression, out _)) constants++;
                    else if (CombatInventoryReference.TryResourcePrefix(expression, out _)) namespaces++;
                    else mutable++;
                }
            }
            Check(namespaces == 5 && constants == 2 && mutable == 3);
            // The package location is versioned; validate both doc examples without pinning its cache hash.
            string image = Directory.GetFiles("Library/PackageCache", "Image.cs", SearchOption.AllDirectories)
                .Single(path => path.Replace('\\', '/').Contains("/Runtime/UGUI/UI/Core/Image.cs"));
            string packageSource = File.ReadAllText(image);
            Check(CombatInventoryReference.Loads.Matches(packageSource).Count == 2);
            Check(CombatInventoryReference.Loads.Matches(CombatInventoryReference.WithoutComments(packageSource)).Count == 0);
        });
        yield return Case("Current District marker has verified direct-child runtime fallback", () => {
            if (!File.Exists("Assets/Scenes/District_1/District_1_Enemies.unity")) throw new InvalidOperationException("Run from project root; fixture coverage must not silently skip.");
            string scene = File.ReadAllText("Assets/Scenes/District_1/District_1_Enemies.unity");
            string marker = System.Text.RegularExpressions.Regex.Split(scene, @"(?m)^--- ").Single(part => part.Contains("markerId: GlobalObjectId_V1-2-6f707f864cc6ecd40a182bedabf7a0ee"));
            Func<string, string> prefab = guid => guid == "f5bf933f572371045aef154a22c977f5" ? File.ReadAllText("Assets/Characters/3_Enemy/Juggernaut/Juggernaut_Combat.prefab") : "";
            Func<string, string> script = guid => guid == "6d482e64f4c70ee4fae5f379165d46a4" ? "Assets/Scripts/CharacterInfo.cs" : "";
            Check(CombatMarkerInventory.DirectChildFallback(scene, marker, prefab, script) != null);
            Check(CombatMarkerInventory.DirectChildFallback(scene, marker.Replace("c54e94b14c7826c4482529b6d4d739ec", new string('a', 32)), prefab, script) == null);
            Check(CombatMarkerInventory.DirectChildFallback(scene.Replace("m_TransformParent: {fileID: 900000000000000101}", "m_TransformParent: {fileID: 123}"), marker, prefab, script) == null);
            Check(CombatMarkerInventory.DirectChildFallback(scene.Replace("propertyPath: m_Name", "propertyPath: characterData"), marker, prefab, script) == null);
            Check(CombatMarkerInventory.DirectChildFallback(scene, marker, _ => "", script) == null);
        });
        yield return Case("Authored Giant GUID and character IDs remain distinct", () => {
            string first = File.ReadAllText("Assets/Characters/3_Enemy/GiantJuggernaut/GiantJuggernaut_CharacterData.asset");
            string second = File.ReadAllText("Assets/Characters/3_Enemy/Juggernaut/GiantJuggernaut.asset");
            Check(CombatMarkerInventory.Field(first, "characterId") == "giant_juggernaut");
            Check(CombatMarkerInventory.Field(second, "characterId") == "giantjuggernaut");
            Check(CombatMarkerInventory.Field(first, "uniqueId") != CombatMarkerInventory.Field(second, "uniqueId"));
            Check(CombatMarkerInventory.Field(first, "worldPrefab") == CombatMarkerInventory.Field(second, "worldPrefab"));
        });
    }
    private static KeyValuePair<string, Action> Case(string name, Action test) => new KeyValuePair<string, Action>(name, test);
    private static void Check(bool value) { if (!value) throw new InvalidOperationException("Inventory classification/coverage assertion failed."); }
}
