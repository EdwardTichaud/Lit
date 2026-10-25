using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static partial class ReveLimbesAuthoringSession
{
    private static void PollQuestCreation()
    {
        string request = Output + "/CreateQuests.request";
        if (running || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(request)) return;
        File.Delete(request); running = true;
        try { CreateQuests(); }
        catch (Exception error) { File.WriteAllText(Output + "/CreateQuests.result", "FAILED " + error); }
        finally { running = false; }
    }

    private static void CreateQuests()
    {
        if (!File.Exists(ZoneFolder + "/Reve_Limbes_Environment.unity")) throw new InvalidOperationException("Create the environment first.");
        string[] names = { "Seuil", "Chambre" };
        foreach (string name in names)
            if (File.Exists("Assets/Resources/Narrative/Limbes" + name + "Cycle.asset") || File.Exists(ZoneFolder + "/Reve_Limbes_Cycle_" + name + ".unity"))
                throw new InvalidOperationException("Never regenerate an existing quest: " + name);
        Scene original = SceneManager.GetActiveScene(); var owned = new List<Scene>();
        try
        {
            for (int quest = 0; quest < 2; quest++)
            {
                string label = names[quest], id = "reve.limbes." + label.ToLowerInvariant();
                string folder = ZoneFolder + "/Cycles/" + label;
                EnsureFolder(folder); EnsureFolder(folder + "/Data"); EnsureFolder(folder + "/Prefabs");
                var scene = NewSavedScene("Reve_Limbes_Cycle_" + label, owned);
                var root = Root(scene, "Cycle_" + label, Origin);
                var controller = root.AddComponent<CycleController>();
                var definition = ScriptableObject.CreateInstance<CycleDefinition>();
                definition.cycleId = id; definition.title = quest == 0 ? "Le seuil éparpillé" : "La chambre à l'envers";
                definition.description = quest == 0 ? "Réunir les fragments d'un passage dispersé, puis reconstituer le seuil depuis sa stèle." : "Explorer une chambre renversée et comprendre ce qu'il reste de son point de vue.";
                definition.category = quest == 0 ? CycleCategory.Main : CycleCategory.Side;
                definition.cycleSceneName = scene.name; definition.playCinematicAfterDefeat = false;
                controller.definition = definition;
                AssetDatabase.CreateAsset(definition, "Assets/Resources/Narrative/Limbes" + label + "Cycle.asset");
                var entry = Child(root.transform, "Entree_du_cycle", quest == 0 ? new Vector3(0, 15, 40) : Islands[2] + Vector3.up);
                var entryBox = entry.AddComponent<BoxCollider>(); entryBox.isTrigger = true; entryBox.size = quest == 0 ? new Vector3(170, 100, 200) : new Vector3(10, 4, 10);
                var trigger = entry.AddComponent<CycleZoneTrigger>(); trigger.cycle = controller; trigger.sourceId = "entry";
                var steps = new List<CycleStep> { new CycleStep { id = "entered", title = quest == 0 ? "Entrer dans les Limbes" : "Entrer dans la chambre renversée", kind = CycleStepKind.ZoneEntered, sourceId = "entry" } };
                int count = quest == 0 ? 3 : 1;
                var knowledge = new KnowledgeSO[count];
                string[] titles = quest == 0 ? new[] { "Un seuil sans mur", "La même entaille", "Des pierres qui se reconnaissent" } : new[] { "La fenêtre n'a pas bougé" };
                string[] texts = quest == 0 ? new[]
                {
                    "Un passage est gravé dans la pierre. Le mur s'arrête au milieu du dessin ; le seuil, lui, se poursuit sur le bord cassé.",
                    "Une entaille traverse le bas de cette pierre. Le fragment du seuil portait la même marque, au même endroit, comme si les deux morceaux avaient oublié leur séparation.",
                    "Les lignes interrompues reprennent sur ce dernier morceau. Ces fragments ne décrivent pas trois portes : ils appartiennent au même passage."
                } : new[] { "Le plafond est sous les pieds, les piliers pointent vers le vide. Pourtant, la lumière tombe toujours du même côté de cette fenêtre. Le souvenir a conservé un regard, pas une pièce entière." };
                int[] locations = quest == 0 ? new[] { 1, 3, 5 } : new[] { 2 };
                for (int index = 0; index < count; index++)
                {
                    var fact = ScriptableObject.CreateInstance<KnowledgeSO>();
                    fact.knowledgeId = id + ".fragment." + index; fact.title = titles[index]; fact.description = texts[index]; fact.districtId = "reve.limbes";
                    AssetDatabase.CreateAsset(fact, folder + "/Data/Knowledge_" + index + ".asset");
                    Set(fact, "uniqueId", AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(fact)));
                    var item = Object.Instantiate(AssetDatabase.LoadAssetAtPath<Item>("Assets/Scenes/Cycles/Cycle_Clairmont/Data/Readable_Clairmont_old_route.asset"));
                    item.name = "Fragment_" + label + "_" + index; item.itemId = id + ".fragment." + index; item.readableContentId = item.itemId; item.itemName = titles[index]; item.description = texts[index]; item.parchmentText = texts[index];
                    item.knowledgeUnlockedOnRead = new List<KnowledgeSO> { fact }; item.knowledgeUnlockedOnPickup = new List<KnowledgeSO> { fact };
                    item.worldPrefab = null; item.allowTakeFromContainers = true;
                    AssetDatabase.CreateAsset(item, folder + "/Data/Item_" + index + ".asset");
                    fact.readableItem = item; EditorUtility.SetDirty(fact); AssetDatabase.SaveAssetIfDirty(fact);
                    var source = CreateReadablePrefab(scene, folder, index, item);
                    item.worldPrefab = source; EditorUtility.SetDirty(item); AssetDatabase.SaveAssetIfDirty(item);
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(source, scene); instance.transform.SetParent(root.transform, false);
                    instance.transform.localPosition = Islands[locations[index]] + new Vector3(0, .35f, 1);
                    knowledge[index] = fact;
                    steps.Add(new CycleStep { id = "fragment_" + index, title = titles[index], kind = CycleStepKind.Knowledge, knowledge = fact, prerequisites = Requires("entered") });
                }
                var interactionRoot = Child(root.transform, quest == 0 ? "Stele_du_seuil" : "Miroir_sans_chambre", (quest == 0 ? Islands[4] : Islands[2]) + new Vector3(2, 0, 1));
                Module(quest == 0 ? "Pillar_01_Base Variant" : "Doorway_Smooth_01 Variant", "Pierre_du_souvenir", interactionRoot.transform,
                    new Vector3(0, quest == 0 ? 1 : 2.8f, 0), new Vector3(0, 90, 0), 1, false);
                var collider = interactionRoot.AddComponent<BoxCollider>(); collider.isTrigger = true; collider.center = Vector3.up * .7f; collider.size = new Vector3(1.5f, 1.8f, 1.5f);
                interactionRoot.AddComponent<RuntimeOutlineTarget>();
                var interaction = interactionRoot.AddComponent<CycleInteraction>(); interaction.cycle = controller; interaction.dialogueId = "resolve"; interaction.requireLitInfluence = true;
                Set(interaction, "interactionDistance", 2f); controller.interactions = new[] { interaction };
                definition.dialogues = new[] { new CycleDialogue
                {
                    id = "resolve", durationSeconds = 5, condition = new CycleCondition { knowledge = knowledge },
                    line = quest == 0 ? "Ces fragments ne décrivent qu'un seul passage. Il suffit de leur rendre un seuil... même sans mur autour." : "Ce lieu a gardé un regard, pas une chambre. Je n'ai pas besoin de retourner le monde pour retrouver sa lumière.",
                    unavailableLine = quest == 0 ? "Il manque des morceaux. Les pierres ne dessinent pas encore un passage." : "Cette chambre est renversée... mais quelque chose n'a pas bougé."
                } };
                var rewards = new List<CycleReward> { new CycleReward { kind = CycleRewardKind.Activation, key = quest == 0 ? "reve.limbes.seuil_recompose" : "reve.limbes.chambre_apaisee" } };
                if (quest == 1) rewards.Add(new CycleReward { kind = CycleRewardKind.Item, item = AssetDatabase.LoadAssetAtPath<Item>("Assets/Gameplay/Items/Item_Pickable/Item_Chiffon sale.asset"), quantity = 3 });
                steps.Add(new CycleStep { id = "resolved", title = quest == 0 ? "Reconstituer le seuil" : "Retrouver le regard de la chambre", kind = CycleStepKind.DialogueCompleted, sourceId = "resolve", terminal = true,
                    prerequisites = Requires(Enumerable.Range(0, count).Select(index => "fragment_" + index).ToArray()), rewards = rewards.ToArray() });
                definition.steps = steps.ToArray();
                var issues = definition.ValidateConfiguration(); if (issues.Count > 0) throw new InvalidOperationException(string.Join("; ", issues));
                EditorUtility.SetDirty(definition); AssetDatabase.SaveAssetIfDirty(definition); Save(scene);
            }
            var manifest = AssetDatabase.LoadAssetAtPath<ZoneManifest>(ZoneFolder + "/ZoneManifest_Reve_Limbes.asset");
            foreach (string label in names)
            {
                string path = ZoneFolder + "/Reve_Limbes_Cycle_" + label + ".unity";
                manifest.loadingScenes.Add(AssetDatabase.LoadAssetAtPath<SceneAsset>(path));
                if (!EditorBuildSettings.scenes.Any(entry => entry.path == path)) EditorBuildSettings.scenes = EditorBuildSettings.scenes.Concat(new[] { new EditorBuildSettingsScene(path, true) }).ToArray();
            }
            Set(manifest, "loadingSceneNames", manifest.loadingScenes.Select(asset => asset.name).ToArray()); EditorUtility.SetDirty(manifest); AssetDatabase.SaveAssetIfDirty(manifest);
            File.WriteAllText(Output + "/CreateQuests.result", "FINISHED cycles=2; recoverable/readable proofs=4; independent prerequisites; persistent bridge and optional home-chest fuel reward.");
        }
        finally
        {
            if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
            foreach (var scene in owned) if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
        }
    }
    private static CycleRequirements Requires(params string[] ids) => new() { conditions = ids.Select(id => new CycleRequirement { kind = CycleRequirementKind.Step, stepId = id }).ToArray() };
    private static GameObject CreateReadablePrefab(Scene scene, string folder, int index, Item item)
    {
        var root = Root(scene, item.name, Vector3.zero);
        try
        {
            Module("Pillar_01_Top Variant", "Fragment_de_pierre", root.transform, new Vector3(0, .35f, 0), Vector3.zero, .65f, false);
            var box = root.AddComponent<BoxCollider>(); box.isTrigger = true; box.center = Vector3.up * .3f; box.size = new Vector3(.9f, .7f, .9f);
            var readable = root.AddComponent<InteractableItem>(); readable.interactableCategory = InteractableItem.InteractableCategory.RecoverableItem;
            readable.representedItem = item; readable.storedItems = new List<InteractableItem.LootItemEntry> { new() { item = item, quantity = 1 } };
            readable.interactionTrigger = box; readable.interactionMaxDistance = 2; readable.allowTake = true;
            root.AddComponent<RuntimeOutlineTarget>();
            return PrefabUtility.SaveAsPrefabAsset(root, folder + "/Prefabs/Fragment_" + index + ".prefab");
        }
        finally { Object.DestroyImmediate(root); }
    }
}
