using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Read-only authored checks; never rewrites assets, scenes or real saves.</summary>
public static class AbilitySystemValidation
{
    [MenuItem("Lit/Abilities/Validate Ability System")]
    public static void Run()
    {
        string report = Validate();
        Directory.CreateDirectory("Library/AbilityValidation");
        File.WriteAllText("Library/AbilityValidation/UnityChecks.txt", report);
        Debug.Log(report);
    }
    public static string Validate()
    {
        int passed = 0;
        var lines = new List<string>();
        void Check(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException("[Abilities] FAIL: " + label);
            passed++; lines.Add("PASS " + label);
        }
        var model = new AbilitySaveData();
        Check(!model.Equip("solo", "locked"), "Cannot equip a locked ability");
        Check(model.Unlock("observateur") && !model.Unlock("observateur"), "Unlocks are idempotent");
        model.Unlock("sauf_conduit");
        model.Equip("solo", "observateur"); model.Equip("solo", "sauf_conduit");
        Check(model.slots.Count == 1 && model.Equipped("solo") == "sauf_conduit", "Solo has exactly one slot");
        model.Equip("alice", "observateur"); model.Equip("bob", "sauf_conduit");
        model.Equip("alice", "sauf_conduit");
        Check(model.Equipped("bob") == "sauf_conduit" && model.slots.Count == 3, "Players have independent slots");
        Check(!model.Equip(null, "observateur") && !model.Unlock(""), "Empty identities and IDs rejected");
        var restored = JsonUtility.FromJson<AbilitySaveData>(JsonUtility.ToJson(model));
        Check(restored.Equipped("solo") == "sauf_conduit" && restored.IsUnlocked("observateur"), "Ability JSON round trip");
        var serializer = new SnapshotSerializer();
        var world = serializer.Deserialize(serializer.Serialize(new WorldSnapshot { Abilities = model }));
        Check(world.Abilities != null && world.Abilities.Equipped("alice") == "sauf_conduit", "World binary v2 retains abilities");
        var oldWorld = serializer.Deserialize(serializer.Serialize(new WorldSnapshot { SchemaVersion = 1 }));
        Check(oldWorld.Abilities == null && oldWorld.SchemaVersion == 1, "World binary v1 remains readable");
        var observer = AssetDatabase.LoadAssetAtPath<AbilitySO>("Assets/Resources/Abilities/Ability_Observateur.asset");
        var passage = AssetDatabase.LoadAssetAtPath<AbilitySO>("Assets/Resources/Abilities/Ability_SaufConduit.asset");
        Check(observer != null && observer.availableAtStart && observer.abilityId == "observateur", "Observateur available at start");
        Check(passage != null && !passage.availableAtStart && passage.unlockCycle != null &&
            passage.unlockCycle.cycleId == "district1.belmont" && string.IsNullOrEmpty(passage.unlockStepId), "Sauf-Conduit unlocks on Belmont completion");
        Check(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(observer)) == "a88b20f6f707d1544aa3a09caf466336" &&
            AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(passage)) == "870300d0851943b4a89f1d7eb0da1849", "Original ability asset GUIDs preserved");
        Check(typeof(CharacterData).GetField("stats") == null && typeof(CharacterData).GetField("skills") == null,
            "CharacterData no longer owns stats or exploration abilities");
        Check(typeof(CharacterData).GetField("combatSkills") != null && typeof(CharacterData).GetField("groundBasicSkills") != null &&
            typeof(CharacterData).GetField("hp") != null, "Combat skills, basic skills and HP preserved");
        var ids = new HashSet<string>();
        foreach (var ability in Resources.LoadAll<AbilitySO>("Abilities"))
        {
            Check(!string.IsNullOrWhiteSpace(ability.abilityId) && ids.Add(ability.abilityId), "Unique ability ID: " + ability.abilityId);
            if (ability.unlockCycle != null && !string.IsNullOrWhiteSpace(ability.unlockStepId))
                Check(ability.unlockCycle.FindStep(ability.unlockStepId) != null, "Unlock step exists: " + ability.abilityId);
        }
        lines.Add("[Abilities] " + passed + " checks passed. Play Mode solo/host/client checks are separate.");
        return string.Join("\n", lines);
    }
}
