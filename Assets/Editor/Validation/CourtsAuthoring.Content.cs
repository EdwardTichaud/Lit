using System;
using System.Collections.Generic;
using System.Linq;
using Lit.Story;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static partial class CourtsAuthoring
{
    private static CycleDefinition Cycle(Transform root, string id, string title, CycleCategory category, out CycleController controller)
    {
        var definition = ScriptableObject.CreateInstance<CycleDefinition>(); definition.cycleId = "district2.courts." + id;
        definition.title = title; definition.category = category; definition.playCinematicAfterDefeat = false;
        // Content remains in the Core: props, shortcuts and memory places must survive cycle completion.
        definition.cycleSceneName = string.Empty;
        AssetDatabase.CreateAsset(definition, "Assets/Resources/Narrative/Courts_" + id + "Cycle.asset");
        controller = Child(root, "Cycle_" + id, Vector3.zero).AddComponent<CycleController>(); controller.definition = definition;
        return definition;
    }
    private static CycleDefinition BuildFamily(Transform root)
    {
        var cycle = Cycle(root, "family", "Les portes qu'on laisse ouvertes", CycleCategory.Main, out var controller);
        cycle.description = "Reconstituer trois aspects de la vie des Mirage, puis rétablir au présent un accès utilisable par tous.";
        var curiosity = Proof(root, cycle, "curiosity", "Le carnet de Alix", new Vector3(-23, .5f, 23),
            "J'aime avoir un coin à moi. Derrière les caisses, j'ai retrouvé une vieille ouverture. Plus haut, j'en ai dégagé une autre. " +
            "Je note ce que je trouve et ce que j'aménage : ce n'est pas la même chose. Ma liste ne sera jamais complète. — Alix");
        var ties = Proof(root, cycle, "ties", "Le billet d'Agathe", new Vector3(-17, .5f, 31),
            "Alix, je ne veux pas t'empêcher de sortir. Dis-moi seulement par où tu pars : certaines pierres ne tiennent plus. " +
            "Tu as besoin de ton espace, je le comprends. Et la porte reste ouverte quand tu veux rentrer. — Agathe");
        var help = Proof(root, cycle, "help", "Les allers-retours", new Vector3(23, .5f, 29),
            "Trois paniers portés cette semaine. Une visite à Abel, puis une autre avec Agathe. " +
            "Je ne passe plus seulement pour aller où je veux : ces détours raccourcis me laissent aussi du temps avec eux. — Alix");
        cycle.steps = new[] {
            Know("curiosity", curiosity), Know("ties", ties), Know("help", help),
            Fact("memory_curiosity", "Comprendre les explorations de Alix", "curiosity_memory", "curiosity"),
            Fact("memory_ties", "Retrouver les liens de la maison", "ties_memory", "ties"),
            Fact("memory_help", "Suivre les gestes d'entraide", "help_memory", "help"),
            new CycleStep { id = "restored", title = "Rétablir l'accès commun", kind = CycleStepKind.Interaction, sourceId = "restore",
                prerequisites = Require("memory_curiosity", "memory_ties", "memory_help"), terminal = true }
        };
        Story(root, "Nell", "Une porte pour soi", new Vector3(-24, 0, 28), new[] {
            "Alix cherchait d'abord un endroit à elle. Elle n'avait pas besoin de renier les siens pour le vouloir.",
            "Certaines ouvertures étaient déjà là. Elle en a dégagé d'autres, à sa mesure."
        }, cycle, "curiosity_memory");
        Story(root, "Alma", "La porte reste ouverte", new Vector3(-16, 0, 34), new[] {
            "Agathe s'inquiétait des pierres qui cédaient, pas de la liberté de sa fille.",
            "Partir un moment… et pouvoir revenir. Cette maison lui laissait les deux."
        }, cycle, "ties_memory");
        Story(root, "Silas", "Des chemins pour les autres", new Vector3(22, 0, 34), new[] {
            "Des paniers, des visites, des trajets répétés. Ce n'était pas un seul grand geste, mais une manière de vivre.",
            "Ses petits raccourcis ne convenaient pas à tous. Elle cherchait aussi une voie assez large pour son père.",
            "Nous pouvons remettre cet accès en service aujourd'hui. Cela ne changera pas ce qu'ils ont vécu."
        }, cycle, "help_memory");
        Action(root, controller, "restore", "Manivelle_de_l_acces_commun", new Vector3(0, 6.6f, 51));
        Finish(cycle); return cycle;
    }
    private static CycleDefinition BuildVisits(Transform root)
    {
        var cycle = Cycle(root, "visits", "Le trajet des visites", CycleCategory.Side, out var controller);
        cycle.description = "Comprendre pourquoi un raccourci étroit ne remplace pas un chemin commun et rouvrir un retour entre les deux maisons.";
        var a = Proof(root, cycle, "silas", "Le trajet de Abel", new Vector3(-25, .5f, 34),
            "Le passage de Alix est trop étroit pour moi. Elle m'a proposé de porter mon panier ; je préfère que nous marchions ensemble, " +
            "même si le tour est plus long. Elle cherche maintenant comment rouvrir la traversante. — Abel");
        var b = Proof(root, cycle, "visits", "Deux visites ordinaires", new Vector3(17, .5f, 21),
            "Agathe est passée le matin. Alix est revenue en fin de journée, sans panier cette fois : simplement pour rester un peu. — Abel");
        cycle.steps = new[] { Know("silas", a), Know("visits", b), new CycleStep {
            id = "return", title = "Rouvrir la traversante des visites", kind = CycleStepKind.DialogueCompleted, sourceId = "visits_return",
            prerequisites = Require("silas", "visits"), terminal = true } };
        cycle.dialogues = new[] { new CycleDialogue { id = "visits_return", condition = new CycleCondition { requirements = Require("silas", "visits") },
            line = "Lucian — Ce retour n'est pas réservé à ceux qui peuvent se glisser entre les pierres. Ouvrons la traversante.",
            unavailableLine = "Deux maisons se font face. Des traces de visites semblent relier les lieux." } };
        Action(root, controller, "visits_return", "Loquet_de_la_traversante", new Vector3(-11, .6f, 24));
        Finish(cycle); return cycle;
    }
    private static void BuildReserve(Transform root)
    {
        var cycle = Cycle(root, "reserve", "La réserve commune", CycleCategory.Side, out var controller);
        cycle.description = "Lire les usages de la réserve dans ses objets et ses comptes : laisser à disposition plutôt que posséder pour soi.";
        var a = Proof(root, cycle, "baskets", "Les paniers de la réserve", new Vector3(24, .5f, 22),
            "Un panier pour la maison, un autre pour ceux qui ne peuvent pas venir. Rapporter les paniers vides, pas les provisions données. — Agathe");
        var b = Proof(root, cycle, "ledger", "Le registre des parts", new Vector3(16, .5f, 32),
            "Alix : deux trajets. Abel : réparation des anses. Agathe : répartition du matin. " +
            "Les contributions n'ont pas toutes le même poids ni la même forme. Elles gardent la réserve ouverte.");
        cycle.steps = new[] { Know("baskets", a), Know("ledger", b), new CycleStep { id = "shared", title = "Comprendre le partage des tâches",
            kind = CycleStepKind.DialogueCompleted, sourceId = "shared", prerequisites = Require("baskets", "ledger"), terminal = true } };
        cycle.dialogues = new[] { new CycleDialogue { id = "shared", condition = new CycleCondition { requirements = Require("baskets", "ledger") },
            line = "Lucian — Porter, réparer, répartir… Chacun aidait à sa manière. La liberté de Alix lui permettait aussi de choisir d'aider.",
            unavailableLine = "Les paniers et le registre racontent peut-être les mêmes trajets." } };
        Action(root, controller, "shared", "Table_de_partage", new Vector3(20, .6f, 27)); Finish(cycle);
    }
    private static void BuildMargins(Transform root)
    {
        var cycle = Cycle(root, "margins", "Les marges du plan", CycleCategory.Side, out var controller);
        cycle.description = "Comparer les annotations de Alix aux lieux : une ouverture trouvée n'est pas toujours une ouverture aménagée.";
        var a = Proof(root, cycle, "old", "L'ouverture ancienne", new Vector3(-17, 6.5f, 49),
            "Les bords étaient déjà usés quand je l'ai trouvée. Je n'ai pas creusé ce passage. J'ai seulement déplacé ce qui le cachait. — Alix");
        var b = Proof(root, cycle, "made", "Les pierres dégagées", new Vector3(17, 6.5f, 49),
            "Ici, j'ai retiré les petites pierres qui barraient le jour. C'est une ouverture aménagée, pas un ancien chemin retrouvé. " +
            "Mes notes ne recensent sûrement pas tout ce qui existe dans le château. — Alix");
        cycle.steps = new[] { Know("old", a), Know("made", b), new CycleStep { id = "compare", title = "Comparer le plan et les marques",
            kind = CycleStepKind.DialogueCompleted, sourceId = "compare", prerequisites = Require("old", "made"), terminal = true } };
        cycle.dialogues = new[] { new CycleDialogue { id = "compare", condition = new CycleCondition { requirements = Require("old", "made") },
            line = "Lucian — Elle distinguait les chemins hérités des ouvertures qu'elle avait dégagées. Ce plan est un regard sur le château, pas sa carte complète.",
            unavailableLine = "Deux annotations se répondent de part et d'autre de la galerie." } };
        Action(root, controller, "compare", "Plan_sur_la_balustrade", new Vector3(0, 6.6f, 46)); Finish(cycle);
    }
    private static CycleDefinition BuildFinale(Transform root)
    {
        var cycle = Cycle(root, "finale", "Le passage commun", CycleCategory.Main, out var controller);
        cycle.description = "Franchir l'accès rétabli, affronter la Gargouille et conclure l'enquête familiale.";
        cycle.steps = new[] { Fact("introduced", "Atteindre la cour finale", "introduction"), new CycleStep {
            id = "defeated", title = "Vaincre la Gargouille", kind = CycleStepKind.EnemyDefeated, sourceId = "gargoyle", prerequisites = Require("introduced") },
            Fact("concluded", "Ce que les portes relient", "conclusion", "defeated") };
        cycle.steps[2].terminal = true;
        Story(root, "Introduction", "La cour occupée", new Vector3(0, 0, 73), new[] {
            "Une Gargouille dans la cour. La Flame lui donne le mouvement.",
            "L'accès est rouvert, mais nous devons encore nous frayer un chemin."
        }, cycle, "introduction");
        Story(root, "Conclusion", "Ce que les portes relient", new Vector3(0, 0, 82), new[] {
            "Alix voulait pouvoir partir. Ses chemins lui ont aussi permis de revenir, de rendre visite et d'aider.",
            "Les passages étroits lui donnaient une liberté à elle. La voie commune laissait de la place aux autres.",
            "Nous n'avons pas changé leur histoire. Nous avons retrouvé un peu de leur vie, et remis ce chemin en service."
        }, cycle, "conclusion");
        var source = Load<CharacterData>("Assets/Characters/3_Enemy/Juggernaut/Juggernaut.asset");
        var data = Object.Instantiate(source); data.name = "CourtsGargoyle"; data.characterId = "district2.courts.gargoyle";
        data.characterName = "La Gargouille des cours"; data.hp = 200; data.enableCombatHealthThresholds = false;
        AssetDatabase.CreateAsset(data, Folder + "/Data/CourtsGargoyle.asset"); Set(data, "uniqueId", AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(data)));
        var markerRoot = Child(root, "Marker_Gargouille_des_cours", new Vector3(0, 0, 81));
        var actor = (GameObject)PrefabUtility.InstantiatePrefab(source.worldPrefab, root.gameObject.scene);
        PrefabUtility.UnpackPrefabInstance(actor, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        actor.name = "Gargouille_des_cours"; actor.transform.SetParent(markerRoot.transform, false); actor.transform.localPosition = Vector3.zero;
        actor.GetComponent<CharacterInfo>().SetCharacterData(data); actor.GetComponent<LitBrainsEnemy>().ConfigureCharacterData(data);
        foreach (var r in actor.GetComponentsInChildren<Renderer>(true)) r.sharedMaterials = r.sharedMaterials.Select(_ => stone).ToArray();
        actor.AddComponent<CourtsGargoyleGate>().cycle = cycle;
        data.worldPrefab = PrefabUtility.SaveAsPrefabAsset(actor, Folder + "/Prefabs/CourtsGargoyle.prefab");
        var marker = markerRoot.AddComponent<SceneMarker>(); marker.SetCharacterData(data); marker.SetBakedCharacterInstance(actor);
        controller.encounters = new[] { new CycleEncounterBinding { id = "gargoyle", marker = marker } };
        EditorSceneManager.SaveScene(root.gameObject.scene); marker.SendMessage("OnValidate", SendMessageOptions.RequireReceiver);
        Finish(cycle); return cycle;
    }
    private static KnowledgeSO Proof(Transform root, CycleDefinition cycle, string suffix, string title, Vector3 point, string text)
    {
        string id = cycle.cycleId + "." + suffix;
        var knowledge = ScriptableObject.CreateInstance<KnowledgeSO>(); knowledge.knowledgeId = id; knowledge.title = title;
        knowledge.description = text; knowledge.districtId = "district2.courts"; knowledge.cycles = new List<CycleDefinition> { cycle };
        AssetDatabase.CreateAsset(knowledge, Folder + "/Data/Knowledge_" + cycle.name + "_" + suffix + ".asset");
        Set(knowledge, "uniqueId", AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(knowledge)));
        var item = Object.Instantiate(Load<Item>("Assets/Scenes/Reve/Cycles/Seuil/Data/Item_0.asset"));
        item.itemId = id; item.readableContentId = id; item.itemName = title; item.description = item.parchmentText = text;
        item.knowledgeUnlockedOnPickup = new List<KnowledgeSO>(); item.knowledgeUnlockedOnRead = new List<KnowledgeSO> { knowledge }; item.worldPrefab = null;
        AssetDatabase.CreateAsset(item, Folder + "/Data/Note_" + cycle.name + "_" + suffix + ".asset"); knowledge.readableItem = item; Items.Add(item);
        var go = Child(root, "Note_" + suffix, point);
        var visible = Block(go.transform, "Document", Vector3.zero, new Vector3(.6f, .1f, .4f)); Object.DestroyImmediate(visible.GetComponent<Collider>()); visible.GetComponent<Renderer>().sharedMaterial = glow;
        var box = go.AddComponent<BoxCollider>(); box.isTrigger = true; box.size = new Vector3(1, .8f, 1);
        var readable = go.AddComponent<InteractableItem>(); readable.interactableCategory = InteractableItem.InteractableCategory.RecoverableItem;
        readable.representedItem = item; readable.storedItems = new List<InteractableItem.LootItemEntry> { new() { item = item, quantity = 1 } };
        readable.interactionTrigger = box; readable.interactionMaxDistance = 2; readable.allowTake = true; go.AddComponent<RuntimeOutlineTarget>();
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, Folder + "/Prefabs/Note_" + cycle.name + "_" + suffix + ".prefab");
        var contents = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(prefab));
        try { contents.transform.position = Vector3.zero; PrefabUtility.SaveAsPrefabAsset(contents, AssetDatabase.GetAssetPath(prefab)); }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
        item.worldPrefab = prefab; EditorUtility.SetDirty(item); EditorUtility.SetDirty(knowledge); return knowledge;
    }
    private static void Action(Transform root, CycleController controller, string id, string name, Vector3 point)
    {
        var go = Child(root, name, point); var visible = Block(go.transform, "Objet", Vector3.zero, new Vector3(1, .8f, .6f));
        Object.DestroyImmediate(visible.GetComponent<Collider>());
        var box = go.AddComponent<BoxCollider>(); box.isTrigger = true; box.size = new Vector3(1.5f, 1.5f, 1.5f);
        var action = go.AddComponent<CycleInteraction>(); action.cycle = controller; action.dialogueId = id; action.requireLitInfluence = true;
        controller.interactions = controller.interactions.Concat(new[] { action }).ToArray(); go.AddComponent<RuntimeOutlineTarget>();
    }
    private static StorySequenceRunner Story(Transform root, string id, string title, Vector3 point, string[] lines, CycleDefinition cycle, string source)
    {
        var asset = ScriptableObject.CreateInstance<StorySequenceAsset>(); asset.sequenceId = "district2.courts." + id.ToLowerInvariant();
        asset.displayName = title; asset.playOnce = true; asset.startFromBlack = false; asset.fadeToBlackBeforeGameplay = false; asset.fadeFromBlackAfterGameplayRestore = false;
        AssetDatabase.CreateAsset(asset, Folder + "/Sequences/" + id + ".asset");
        var rig = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>("Assets/Narrative/StorySequence/Prefabs/StorySequenceRig.prefab"), root.gameObject.scene);
        PrefabUtility.UnpackPrefabInstance(rig, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        rig.name = "Sequence_" + id; rig.transform.SetParent(root, false); rig.transform.localPosition = point;
        var runner = rig.GetComponent<StorySequenceRunner>(); Set(runner, "sequence", asset); Set(runner, "playOnStart", false);
        var camera = Child(rig.transform, "Point_camera", new Vector3(5, 5, -7)).AddComponent<StorySequenceCameraPoint>();
        Set(camera, "pointId", id); Set(camera, "lookTarget", Child(rig.transform, "Cible_camera", Vector3.up).transform);
        var bindings = rig.GetComponent<StorySequenceSceneBindings>(); var so = new SerializedObject(bindings);
        so.FindProperty("cameraPoints").arraySize = 1; so.FindProperty("cameraPoints").GetArrayElementAtIndex(0).objectReferenceValue = camera; so.ApplyModifiedPropertiesWithoutUndo();
        var callback = new UnityEngine.Events.UnityEvent(); UnityEventTools.AddBoolPersistentListener(callback, rig.GetComponent<StorySequenceCameraDriver>().EndCinematic, true);
        typeof(StorySequenceSceneBindings).GetField("events", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .SetValue(bindings, new List<StorySequenceEventBinding> { new() { eventId = "gameplay", callback = callback } });
        asset.steps.Add(new StorySequenceStep { type = StorySequenceStepType.CameraShot, label = title, cameraPointId = id, cameraHoldDuration = 3, cameraTransitionDuration = .8f });
        asset.steps.Add(new StorySequenceStep { type = StorySequenceStepType.SceneEvent, eventId = "gameplay" });
        for (int i = 0; i < lines.Length; i++)
        {
            var line = ScriptableObject.CreateInstance<VoiceLineData>(); line.voiceLineText = lines[i]; AssetDatabase.CreateAsset(line, Folder + "/Sequences/" + id + "_Line_" + i + ".asset");
            asset.steps.Add(new StorySequenceStep { type = StorySequenceStepType.Dialogue, actorId = "Lucian", speakerNameOverride = "Lucian", voiceLine = line, skippable = true, focusCameraOnSpeaker = false });
        }
        if (cycle != null)
        {
            var gate = rig.AddComponent<LimbesMemorySequence>(); gate.runner = runner; gate.cycle = cycle; gate.completionSource = source;
            gate.primarySceneName = "District_2_Courts"; gate.radius = id == "Introduction" ? 5 : 3;
        }
        else
        {
            var arrival = rig.AddComponent<StorySequenceOnZoneArrival>(); Set(arrival, "runner", runner); Set(arrival, "bindings", bindings);
        }
        if (!asset.TryValidate(out string issue)) throw new InvalidOperationException(issue);
        EditorUtility.SetDirty(asset); return runner;
    }
    private static CycleStep Know(string id, KnowledgeSO knowledge) => new() { id = id, title = knowledge.title, kind = CycleStepKind.Knowledge, knowledge = knowledge };
    private static CycleStep Fact(string id, string title, string source, params string[] requirements) => new() { id = id, title = title, kind = CycleStepKind.NamedFact, sourceId = source, prerequisites = Require(requirements) };
    private static CycleRequirements Require(params string[] ids) => new() { conditions = ids.Select(i => new CycleRequirement { kind = CycleRequirementKind.Step, stepId = i }).ToArray() };
    private static void Finish(CycleDefinition cycle)
    {
        var errors = cycle.ValidateConfiguration(); if (errors.Count > 0) throw new InvalidOperationException(cycle.name + ": " + string.Join("; ", errors)); EditorUtility.SetDirty(cycle);
    }
}
