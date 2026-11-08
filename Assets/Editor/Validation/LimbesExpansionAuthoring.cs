using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lit.Story;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Explicit, one-time scene authoring. No generation on Play or editor startup.</summary>
public static class LimbesExpansionAuthoring
{
    private const string Folder = "Assets/Scenes/Reve/Completion";
    private const string SceneFolder = "Assets/Scenes/Reve";
    private const string ScenePath = SceneFolder + "/Reve_Limbes_Progression.unity";
    private static readonly Vector3 Origin = new(600, 120, 0);
    private static readonly Vector3[] Peaks = { new(-43, 10, 36), new(45, 16, 57), new(-26, 22, 85) };
    private static readonly Vector3[] Starts = { new(-20, 3, 27), new(22, 5, 46), new(-12, 7, 62) };
    private static readonly string[] Labels = { "Creation", "Captivite", "Invocation" };
    private static readonly string[] Titles = { "Le monde inachevé", "La lignée captive", "Le visage de la statue" };
    private static readonly string[][] Proofs = {
        new[] { "Les deux faces d'une arche", "Le nom d'Azazel", "Une création imparfaite" },
        new[] { "Des cellules sous les chapelles", "La lignée avant les noms", "Des âmes venues des Enfers" },
        new[] { "Le visage sous la protection", "Les voix réunies", "Le passage interrompu" }
    };
    private static readonly string[][] Texts = {
        new[] {
            "Les deux faces de cette arche se contredisent. La pierre tient ensemble des formes incompatibles : les incohérences appartiennent au lieu lui-même.",
            "Azazel : ange déchu, créateur du Monde des Rêves. Il souhaitait créer des mondes sans en connaître la perfection.",
            "Le Rêve est un monde qui existe, avec ses propres êtres. Son imperfection ne rend pas les vies qui s'y déroulent moins réelles."
        },
        new[] {
            "Sous les usages religieux, les pierres conservent des aménagements d'enfermement. Le château a d'abord été une prison.",
            "Les lignées étaient préservées pour atteindre l'an 666. Les générations ont transmis les gestes sans en conserver l'intention.",
            "Les fondateurs étaient une secte. Ils avaient arraché des âmes aux Enfers et organisé leur descendance dans le Monde des Rêves."
        },
        new[] {
            "La statue représente une déesse des Enfers. La secte fondatrice voulait l'amener dans le Monde des Rêves ; la statue devait servir d'ancre.",
            "La présence, la lignée et le chant des Chanteurs étaient nécessaires au rite. Les rassemblements de Veillée en ont conservé les procédures.",
            "Le rituel fut interrompu. Le château demeure bloqué entre les Rêves et les Enfers : les strates et les mémoires incomplètes partagent cette origine."
        }
    };
    private static readonly string[][] Lines = {
        new[] {
            "Azazel… Un créateur qui ne connaissait pas la perfection.",
            "Ces ruines ne sont pas seulement détruites. Certaines n'ont jamais pu tenir ensemble de cette façon.",
            "Nous marchons dans un monde imparfait. Mais les vies qu'il abrite comptent autant que les nôtres."
        },
        new[] {
            "Avant les quartiers et les chapelles, il y avait des cellules.",
            "Des âmes arrachées aux Enfers… puis des enfants, des familles, des générations entières.",
            "Ils vivaient vraiment. Ils ignoraient seulement ce que les fondateurs attendaient de leur descendance.",
            "Les registres gardaient les noms. Les traditions gardaient le projet. Personne n'avait besoin de comprendre pour le poursuivre."
        },
        new[] {
            "Les Espérants attendaient son secours. Les Fatalistes priaient pour échapper aux Enfers.",
            "Et cette statue représentait justement une déesse que la secte voulait faire sortir des Enfers.",
            "Ils n'étaient pas complices. Ils avaient hérité de gestes dont le sens avait disparu.",
            "Le rite a commencé sans eux pour le comprendre. Son interruption a laissé le château entre deux mondes."
        }
    };
    private static Material stone, glow;

    [MenuItem("Tools/Lit/Limbes/Install approved zone expansion")]
    public static void Install()
    {
        if (File.Exists(ScenePath) || Directory.Exists(Folder) && Directory.GetFiles(Folder, "*.asset", SearchOption.AllDirectories).Length > 0)
            throw new InvalidOperationException("Expansion exists; edit authored content instead of regenerating it.");
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode required.");
        foreach (var scene in Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt))
            if (scene.isDirty) throw new InvalidOperationException("Preserve unsaved scene: " + scene.path);
        if (string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
            EditorSceneManager.OpenScene(SceneFolder + "/Reve_Limbes.unity", OpenSceneMode.Single);
        EnsureFolder(Folder); EnsureFolder(Folder + "/Data"); EnsureFolder(Folder + "/Sequences");
        EnsureFolder(Folder + "/Prefabs");
        stone = AssetDatabase.LoadAssetAtPath<Material>("Assets/Scenes/Reve/Materials/Limbes_8508bf557d417f64ea03e22431806255.mat");
        glow = AssetDatabase.LoadAssetAtPath<Material>("Assets/Scenes/Reve/Lighting/Limbes_Lueur.mat");
        if (stone == null || glow == null) throw new InvalidOperationException("Existing Limbes materials required.");
        Scene active = SceneManager.GetActiveScene();
        var owned = new List<Scene>();
        try
        {
            Scene world = NewScene(ScenePath, owned);
            var root = new GameObject("Limbes_Completion"); SceneManager.MoveGameObjectToScene(root, world);
            root.transform.position = Origin;
            var routes = Child(root.transform, "Parcours_3D", Vector3.zero);
            var permanent = Child(root.transform, "Souvenirs_et_progression", Vector3.zero);
            var definitions = new List<CycleDefinition> {
                AssetDatabase.LoadAssetAtPath<CycleDefinition>("Assets/Resources/Narrative/LimbesSeuilCycle.asset"),
                AssetDatabase.LoadAssetAtPath<CycleDefinition>("Assets/Resources/Narrative/LimbesChambreCycle.asset")
            };
            for (int index = 0; index < 3; index++)
            {
                var points = BuildBranch(routes.transform, index);
                var cycle = CreateExplorationCycle(index, points, permanent.transform, owned);
                definitions.Add(cycle);
            }
            // The arena is physically separate from the last exploration landing.
            var arena = Child(root.transform, "Arene_de_la_Gargouille_Ange", new Vector3(4, 9, 123));
            Floor(arena.transform, "Sol_arene", Vector3.zero, new Vector3(28, .8f, 28));
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI / 4;
                var pillar = Primitive(arena.transform, "Pilier_" + i, PrimitiveType.Cylinder,
                    new Vector3(Mathf.Cos(angle) * 13, 3, Mathf.Sin(angle) * 13), new Vector3(1, 3, 1), stone, true);
            }
            var center = Child(arena.transform, "Centre", new Vector3(0, .05f, 0));
            AddFlame(world, arena.transform, new Vector3(-8, .6f, -5), "angel", 40);
            var bridge = Child(root.transform, "Acces_arene_apres_cinq_cycles", Vector3.zero);
            Walkway(bridge.transform, new Vector3(4, 9, 89), new Vector3(4, 9, 111), "Pont_final");
            bridge.SetActive(false);

            var boss = CreateBoss(world, arena.transform, center.transform);
            var final = CreateConclusion(world, permanent.transform, arena.transform, definitions.ToArray(), boss);
            var progression = root.AddComponent<LimbesZoneProgression>();
            progression.explorationCycles = definitions.ToArray(); progression.conclusionCycle = final;
            progression.arenaAccess = bridge; progression.boss = boss;
            progression.bossIntroduction = AssetDatabase.LoadAssetAtPath<StorySequenceAsset>(Folder + "/Sequences/AngelIntroduction.asset");
            // The pre-existing retreat portals remain available; this final one denotes zone completion.
            var sourceScene = EditorSceneManager.OpenScene(SceneFolder + "/Reve_Limbes_Interactives.unity", OpenSceneMode.Additive);
            owned.Add(sourceScene);
            var source = sourceScene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<PortalController>(true)).First();
            var exit = Object.Instantiate(source.gameObject, arena.transform);
            exit.name = "Sortie_victoire_Maison"; exit.transform.localPosition = new Vector3(0, 0, 9);
            // Cloned portal destinations are world-space authoring anchors, not offsets from the arena.
            var sourceData = new SerializedObject(source); var exitData = new SerializedObject(exit.GetComponent<PortalController>());
            var sourcePoints = sourceData.FindProperty("destinationPoints"); var exitPoints = exitData.FindProperty("destinationPoints");
            for (int i = 0; i < sourcePoints.arraySize; i++)
            {
                var a = sourcePoints.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
                var b = exitPoints.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
                if (a != null && b != null) b.SetPositionAndRotation(a.position, a.rotation);
            }
            exit.SetActive(false); progression.victoryExit = exit;
            EditorSceneManager.SaveScene(world);
            RegisterScenes(owned.Where(s => s.path.StartsWith(SceneFolder + "/Reve_Limbes_")), world);
            RegisterItems();
            BakeNavigation(owned, world);
            File.WriteAllText("Library/ReveLimbesCompletion/Install.result", "Installed: five exploration cycles, one boss cycle, six StorySequences including existing arrival. Native playability verification pending.");
        }
        finally
        {
            if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
            foreach (var scene in owned.AsEnumerable().Reverse()) if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static Vector3[] BuildBranch(Transform parent, int index)
    {
        var root = Child(parent, "Branche_" + Labels[index], Vector3.zero);
        Vector3 start = Starts[index], end = Peaks[index];
        // A low landing, a lateral return and the high landing form an explorable three-dimensional branch.
        Vector3 turn = (start + end) * .5f + new Vector3(index == 1 ? 5 : -5, 0, -8);
        Walkway(root.transform, start, turn, "Montee_basse");
        Walkway(root.transform, turn, end, "Retour_en_surplomb");
        Floor(root.transform, "Palier_intermediaire", turn, new Vector3(6, .6f, 6));
        Floor(root.transform, "Terrasse_du_souvenir", end, new Vector3(9, .8f, 9));
        var archPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/0 - UnityPackages/Fab/ModularDungeonCollection/Prefabs/Doorway_Pillar_01 Variant.prefab");
        if (archPrefab != null)
        {
            var arch = (GameObject)PrefabUtility.InstantiatePrefab(archPrefab, parent.gameObject.scene);
            arch.transform.SetParent(root.transform, false); arch.transform.localPosition = end + Vector3.up * 3;
            foreach (var collider in arch.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            foreach (var renderer in arch.GetComponentsInChildren<Renderer>(true)) renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => stone).ToArray();
        }
        AddFlame(parent.gameObject.scene, root.transform, turn + (index == 1 ? new Vector3(-2.2f, .5f, 2.2f) : new Vector3(-2, .5f, 0)), Labels[index] + ".mid", 20);
        AddFlame(parent.gameObject.scene, root.transform, end + new Vector3(-3, .5f, 0), Labels[index] + ".peak", 20);
        return new[] { Vector3.Lerp(start, turn, .55f), turn + Vector3.right, end };
    }

    private static CycleDefinition CreateExplorationCycle(int index, Vector3[] points, Transform permanent, List<Scene> owned)
    {
        string label = Labels[index], id = "reve.limbes." + label.ToLowerInvariant();
        Scene scene = NewScene(SceneFolder + "/Reve_Limbes_Cycle_" + label + ".unity", owned);
        var root = new GameObject("Cycle_" + label); SceneManager.MoveGameObjectToScene(root, scene); root.transform.position = Origin;
        var controller = root.AddComponent<CycleController>();
        var definition = ScriptableObject.CreateInstance<CycleDefinition>(); definition.cycleId = id;
        definition.title = Titles[index]; definition.category = CycleCategory.Main;
        definition.description = "Explorer les trois niveaux de la branche et confronter leurs preuves au souvenir du sommet.";
        definition.cycleSceneName = scene.name; definition.playCinematicAfterDefeat = false;
        AssetDatabase.CreateAsset(definition, "Assets/Resources/Narrative/Limbes" + label + "Cycle.asset");
        controller.definition = definition;
        var steps = new List<CycleStep>();
        for (int proof = 0; proof < 3; proof++)
        {
            KnowledgeSO knowledge = CreateProof(scene, root.transform, id, index, proof, points[proof]);
            steps.Add(new CycleStep { id = "proof_" + proof, title = Proofs[index][proof], kind = CycleStepKind.Knowledge, knowledge = knowledge });
        }
        steps.Add(new CycleStep { id = "understood", title = "Comprendre le souvenir", kind = CycleStepKind.NamedFact, sourceId = "memory", terminal = true,
            prerequisites = RequireSteps("proof_0", "proof_1", "proof_2") });
        definition.steps = steps.ToArray();
        CreateStory(permanent, label, Titles[index], Peaks[index], Lines[index], definition, "memory", Array.Empty<CycleDefinition>());
        RequireValid(definition); EditorUtility.SetDirty(definition); AssetDatabase.SaveAssetIfDirty(definition);
        EditorSceneManager.SaveScene(scene);
        return definition;
    }

    private static KnowledgeSO CreateProof(Scene scene, Transform parent, string cycleId, int index, int proof, Vector3 point)
    {
        string id = cycleId + ".proof." + proof;
        var knowledge = ScriptableObject.CreateInstance<KnowledgeSO>(); knowledge.knowledgeId = id;
        knowledge.title = Proofs[index][proof]; knowledge.description = Texts[index][proof]; knowledge.districtId = "reve.limbes";
        AssetDatabase.CreateAsset(knowledge, Folder + "/Data/Knowledge_" + Labels[index] + "_" + proof + ".asset");
        Set(knowledge, "uniqueId", AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(knowledge)));
        var template = AssetDatabase.LoadAssetAtPath<Item>("Assets/Scenes/Reve/Cycles/Seuil/Data/Item_0.asset");
        if (template == null) throw new InvalidOperationException("Existing Limbes readable required.");
        var item = Object.Instantiate(template); item.name = "Preuve_" + Labels[index] + "_" + proof;
        item.itemId = id; item.readableContentId = id; item.itemName = knowledge.title;
        item.description = item.parchmentText = knowledge.description;
        item.knowledgeUnlockedOnPickup = new List<KnowledgeSO> { knowledge };
        item.knowledgeUnlockedOnRead = new List<KnowledgeSO> { knowledge };
        item.worldPrefab = null;
        AssetDatabase.CreateAsset(item, Folder + "/Data/" + item.name + ".asset");
        knowledge.readableItem = item;
        var go = Child(parent, item.name, point + Vector3.up * .35f);
        Primitive(go.transform, "Pierre_inscrite", PrimitiveType.Cube, Vector3.zero, new Vector3(.9f, .4f, .65f), glow, false);
        var box = go.AddComponent<BoxCollider>(); box.isTrigger = true; box.size = new Vector3(1, .8f, 1);
        var readable = go.AddComponent<InteractableItem>();
        readable.interactableCategory = InteractableItem.InteractableCategory.RecoverableItem;
        readable.representedItem = item; readable.storedItems = new List<InteractableItem.LootItemEntry> { new() { item = item, quantity = 1 } };
        readable.interactionTrigger = box; readable.interactionMaxDistance = 2; readable.allowTake = true;
        go.AddComponent<RuntimeOutlineTarget>();
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, Folder + "/Prefabs/" + item.name + ".prefab");
        // A world prefab has no scene-placement offset.
        var contents = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(prefab));
        try { contents.transform.position = Vector3.zero; PrefabUtility.SaveAsPrefabAsset(contents, AssetDatabase.GetAssetPath(prefab)); }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
        item.worldPrefab = prefab;
        EditorUtility.SetDirty(item); AssetDatabase.SaveAssetIfDirty(item);
        EditorUtility.SetDirty(knowledge); AssetDatabase.SaveAssetIfDirty(knowledge);
        return knowledge;
    }

    private static StorySequenceAsset CreateStory(Transform parent, string id, string title, Vector3 point, string[] lines,
        CycleDefinition cycle, string source, CycleDefinition[] required)
    {
        var asset = ScriptableObject.CreateInstance<StorySequenceAsset>(); asset.sequenceId = "limbes." + id.ToLowerInvariant();
        asset.displayName = title; asset.playOnce = true; asset.startFromBlack = false;
        asset.fadeToBlackBeforeGameplay = false; asset.fadeFromBlackAfterGameplayRestore = false;
        AssetDatabase.CreateAsset(asset, Folder + "/Sequences/" + id + ".asset");
        var template = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Narrative/StorySequence/Prefabs/StorySequenceRig.prefab");
        var rig = (GameObject)PrefabUtility.InstantiatePrefab(template, parent.gameObject.scene);
        PrefabUtility.UnpackPrefabInstance(rig, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        rig.name = "Souvenir_" + id; rig.transform.SetParent(parent, false); rig.transform.localPosition = point;
        var runner = rig.GetComponent<StorySequenceRunner>(); Set(runner, "sequence", asset); Set(runner, "playOnStart", false);
        var camera = Child(rig.transform, "Point_camera", new Vector3(7, 6, -8)).AddComponent<StorySequenceCameraPoint>();
        var target = Child(rig.transform, "Cible_camera", Vector3.up);
        Set(camera, "pointId", id); Set(camera, "lookTarget", target.transform);
        var bindings = rig.GetComponent<StorySequenceSceneBindings>(); var props = new SerializedObject(bindings);
        props.FindProperty("cameraPoints").arraySize = 1; props.FindProperty("cameraPoints").GetArrayElementAtIndex(0).objectReferenceValue = camera;
        props.FindProperty("events").arraySize = 1;
        var entry = props.FindProperty("events").GetArrayElementAtIndex(0); entry.FindPropertyRelative("eventId").stringValue = "gameplay";
        props.ApplyModifiedPropertiesWithoutUndo();
        var ev = new UnityEngine.Events.UnityEvent(); UnityEventTools.AddBoolPersistentListener(ev, rig.GetComponent<StorySequenceCameraDriver>().EndCinematic, true);
        // Copy the native serialized event from a small temporary bridge object through the bindings' actual field.
        var eventField = typeof(StorySequenceSceneBindings).GetField("events", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        eventField.SetValue(bindings, new List<StorySequenceEventBinding> { new() { eventId = "gameplay", callback = ev } });
        asset.steps.Add(new StorySequenceStep { type = StorySequenceStepType.CameraShot, label = title, cameraPointId = id, cameraHoldDuration = 3.5f, cameraTransitionDuration = .8f, skippable = false });
        asset.steps.Add(new StorySequenceStep { type = StorySequenceStepType.SceneEvent, eventId = "gameplay" });
        for (int i = 0; i < lines.Length; i++)
        {
            var line = ScriptableObject.CreateInstance<VoiceLineData>(); line.voiceLineText = lines[i];
            AssetDatabase.CreateAsset(line, Folder + "/Sequences/" + id + "_Line_" + i + ".asset");
            asset.steps.Add(new StorySequenceStep { type = StorySequenceStepType.Dialogue, label = "Lucian " + (i + 1), actorId = "Lucian", speakerNameOverride = "Lucian", voiceLine = line, skippable = true, focusCameraOnSpeaker = false });
        }
        var gate = rig.AddComponent<LimbesMemorySequence>(); gate.runner = runner; gate.cycle = cycle;
        gate.completionSource = source; gate.requiredCycles = required; gate.radius = id == "AngelIntroduction" ? 10 : 4;
        if (!asset.TryValidate(out var issue)) throw new InvalidOperationException(issue);
        EditorUtility.SetDirty(asset); AssetDatabase.SaveAssetIfDirty(asset);
        return asset;
    }

    private static LimbesAngelBoss CreateBoss(Scene scene, Transform arena, Transform center)
    {
        var source = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/Characters/3_Enemy/Juggernaut/Juggernaut.asset");
        var data = Object.Instantiate(source); data.name = "Limbes_StoneAngel"; data.characterId = "limbes.stone_angel";
        data.characterName = "La Gargouille des Limbes"; data.hp = 300; data.enableCombatHealthThresholds = false;
        var profile = Object.Instantiate(source.enemyCombatProfile); profile.name = "Limbes_StoneAngel_Profile";
        profile.pursuitRadius = 24; profile.guardChance = 0;
        AssetDatabase.CreateAsset(profile, Folder + "/Data/AngelCombatProfile.asset"); data.enemyCombatProfile = profile;
        AssetDatabase.CreateAsset(data, Folder + "/Data/AngelCharacter.asset");
        Set(data, "uniqueId", AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(data)));
        var root = (GameObject)PrefabUtility.InstantiatePrefab(source.worldPrefab, scene);
        PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        root.name = "Gargouille_Ange_des_Limbes"; root.transform.SetParent(arena, false); root.transform.localPosition = Vector3.zero;
        root.GetComponent<CharacterInfo>().SetCharacterData(data);
        root.GetComponent<LitBrainsEnemy>().ConfigureCharacterData(data);
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
        var statue = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Environment/Statue/AngelOfJustice/Import/AngelOfJustice.prefab"), scene);
        statue.name = "Corps_de_pierre_ange"; statue.transform.SetParent(root.transform, false);
        statue.transform.localPosition = Vector3.zero;
        foreach (var collider in statue.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
        foreach (var animator in statue.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(animator);
        var renderers = statue.GetComponentsInChildren<Renderer>(true); Bounds bounds = renderers[0].bounds;
        foreach (var renderer in renderers) { bounds.Encapsulate(renderer.bounds); renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => stone).ToArray(); }
        float scale = 3.5f / Mathf.Max(.1f, bounds.size.y); statue.transform.localScale *= scale;
        statue.transform.localPosition -= new Vector3(bounds.center.x - root.transform.position.x, bounds.min.y - root.transform.position.y, bounds.center.z - root.transform.position.z) * scale;
        var boss = root.AddComponent<LimbesAngelBoss>(); boss.arenaCenter = center; boss.visual = statue.transform;
        var definition = ScriptableObject.CreateInstance<BossDefinitionSO>();
        Set(definition, "bossId", "limbes.stone_angel"); Set(definition, "displayName", "La Gargouille des Limbes");
        Set(definition, "damagePolicy", 0); Set(definition, "segmentCount", 300); Set(definition, "engagementDistance", 12f);
        AssetDatabase.CreateAsset(definition, Folder + "/Data/AngelBoss.asset"); Set(boss, "definition", definition);
        var bolt = Primitive(null, "Limbes_Angel_Bolt", PrimitiveType.Sphere, Vector3.zero, Vector3.one * .45f, glow, false);
        bolt.AddComponent<LimbesAngelBolt>();
        boss.boltPrefab = PrefabUtility.SaveAsPrefabAsset(bolt, Folder + "/Prefabs/AngelBolt.prefab").GetComponent<LimbesAngelBolt>();
        Object.DestroyImmediate(bolt);
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, Folder + "/Prefabs/StoneAngel.prefab");
        data.worldPrefab = prefab; EditorUtility.SetDirty(data); AssetDatabase.SaveAssetIfDirty(data);
        return boss;
    }

    private static CycleDefinition CreateConclusion(Scene scene, Transform permanent, Transform arena, CycleDefinition[] previous, LimbesAngelBoss enemy)
    {
        var definition = ScriptableObject.CreateInstance<CycleDefinition>(); definition.cycleId = "reve.limbes.ange";
        definition.title = "Le dernier passage"; definition.category = CycleCategory.Main; definition.playCinematicAfterDefeat = false;
        definition.steps = new[] {
            new CycleStep { id = "introduced", title = "Atteindre la Gargouille", kind = CycleStepKind.NamedFact, sourceId = "introduction" },
            new CycleStep { id = "defeated", title = "Vaincre la Gargouille des Limbes", kind = CycleStepKind.NamedFact, sourceId = "angel_defeat", prerequisites = RequireSteps("introduced") },
            new CycleStep { id = "departed", title = "Comprendre ce que le château a conservé", kind = CycleStepKind.NamedFact, sourceId = "departure", prerequisites = RequireSteps("defeated"), terminal = true }
        };
        AssetDatabase.CreateAsset(definition, "Assets/Resources/Narrative/LimbesAngelCycle.asset");
        var root = Child(permanent, "Cycle_Ange", Vector3.zero); var controller = root.AddComponent<CycleController>(); controller.definition = definition;
        // The zone adapter reports the production actor health through the trusted named-fact bridge.
        CreateStory(permanent, "AngelIntroduction", "La pierre ailée", new Vector3(4, 9, 113), new[] {
            "Une silhouette d'ange… mais c'est une Gargouille.", "La Flame lui rend le mouvement. Cette lumière peut ouvrir le passage et réveiller ce qui le défend.",
            "Je dois attendre qu'elle redescende, éviter ses projectiles, puis trouver une ouverture."
        }, definition, "introduction", previous);
        CreateStory(permanent, "Departure", "Ce que les traditions ont gardé", new Vector3(4, 9, 123), new[] {
            "La pierre peut enfin se taire.", "Nous avons retrouvé la prison sous les maisons, et l'invocation sous les prières.",
            "L'espoir des uns et la culpabilité des autres ont conduit au même rassemblement. Aucun d'eux ne savait ce qu'il préparait.",
            "Je ne veux pas réduire leurs vies au projet de leurs fondateurs. Il nous reste des noms à retrouver."
        }, definition, "departure", Array.Empty<CycleDefinition>());
        RequireValid(definition); EditorUtility.SetDirty(definition); AssetDatabase.SaveAssetIfDirty(definition);
        return definition;
    }

    private static void RegisterScenes(IEnumerable<Scene> scenes, Scene world)
    {
        var manifest = AssetDatabase.LoadAssetAtPath<ZoneManifest>(SceneFolder + "/ZoneManifest_Reve_Limbes.asset");
        foreach (var scene in scenes)
        {
            if (!manifest.loadingScenes.Any(a => AssetDatabase.GetAssetPath(a) == scene.path)) manifest.loadingScenes.Add(AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path));
            if (!EditorBuildSettings.scenes.Any(s => s.path == scene.path)) EditorBuildSettings.scenes = EditorBuildSettings.scenes.Concat(new[] { new EditorBuildSettingsScene(scene.path, true) }).ToArray();
        }
        var props = new SerializedObject(manifest); var names = props.FindProperty("loadingSceneNames"); names.arraySize = manifest.loadingScenes.Count;
        for (int i = 0; i < names.arraySize; i++) names.GetArrayElementAtIndex(i).stringValue = manifest.loadingScenes[i].name;
        props.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(manifest); AssetDatabase.SaveAssetIfDirty(manifest);
    }
    private static void BakeNavigation(List<Scene> owned, Scene world)
    {
        foreach (string path in new[] { SceneFolder + "/Reve_Limbes_Environment.unity" })
            if (!SceneManager.GetSceneByPath(path).isLoaded) owned.Add(EditorSceneManager.OpenScene(path, OpenSceneMode.Additive));
        var host = new GameObject("__Limbes_Expansion_Navigation"); SceneManager.MoveGameObjectToScene(host, world);
        try
        {
            var surface = host.AddComponent<NavMeshSurface>(); surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.collectObjects = CollectObjects.Volume; surface.center = Origin + new Vector3(0, 20, 65); surface.size = new Vector3(200, 140, 220);
            surface.BuildNavMesh();
            if (surface.navMeshData == null) throw new InvalidOperationException("Navigation build failed.");
            var data = Object.Instantiate(surface.navMeshData);
            string navPath = Folder + "/LimbesExpandedNavigation.asset";
            var existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(navPath);
            if (existing == null) AssetDatabase.CreateAsset(data, navPath);
            else
            {
                EditorUtility.CopySerialized(data, existing); Object.DestroyImmediate(data);
                data = existing; EditorUtility.SetDirty(data); AssetDatabase.SaveAssetIfDirty(data);
            }
            var manifest = AssetDatabase.LoadAssetAtPath<ZoneManifest>(SceneFolder + "/ZoneManifest_Reve_Limbes.asset");
            Set(manifest, "bakedNavMeshData", data); Set(manifest, "bakedNavMeshVersion", DateTime.UtcNow.ToString("yyyyMMddHHmmss"));
            Set(manifest, "bakedNavMeshAgentTypeId", surface.agentTypeID); AssetDatabase.SaveAssetIfDirty(manifest);
        }
        finally { Object.DestroyImmediate(host); }
    }
    private static void RegisterItems()
    {
        const string path = "Assets/Core/System/GameplaySessionRoot.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var items = AssetDatabase.FindAssets("t:Item", new[] { Folder + "/Data" })
                .Select(id => AssetDatabase.LoadAssetAtPath<Item>(AssetDatabase.GUIDToAssetPath(id))).Where(i => i != null).ToArray();
            foreach (var registry in root.GetComponentsInChildren<ItemRegistry>(true))
                foreach (var item in items) if (!registry.items.Any(i => i != null && i.itemId == item.itemId)) registry.items.Add(item);
            foreach (var store in root.GetComponentsInChildren<CharacterStateStore>(true))
                foreach (var item in items) if (!store.allItems.Any(i => i != null && i.itemId == item.itemId)) store.allItems.Add(item);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    public static void RepairProductionBoss()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var boss = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<LimbesAngelBoss>(true)).Single();
        var progression = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<LimbesZoneProgression>(true)).Single();
        var data = AssetDatabase.LoadAssetAtPath<CharacterData>(Folder + "/Data/AngelCharacter.asset");
        boss.GetComponent<LitBrainsEnemy>().ConfigureCharacterData(data);
        var body = boss.GetComponent<Rigidbody>();
        if (body == null) body = boss.gameObject.AddComponent<Rigidbody>();
        body.isKinematic = true; body.useGravity = false;
        progression.boss = boss;
        var controller = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CycleController>(true)).Single();
        controller.encounters = Array.Empty<CycleEncounterBinding>();
        var definition = controller.definition;
        var defeated = definition.FindStep("defeated"); defeated.kind = CycleStepKind.NamedFact; defeated.sourceId = "angel_defeat";
        EditorUtility.SetDirty(definition); AssetDatabase.SaveAssetIfDirty(definition);
        EditorSceneManager.SaveScene(scene);
        PrefabUtility.SaveAsPrefabAsset(boss.gameObject, Folder + "/Prefabs/StoneAngel.prefab");
        Debug.Log("Limbes boss bound to the existing Brains encounter actor.");
        LimbesExpansionVerification.Run();
    }

    public static void SmoothBranches()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var owned = new List<Scene>();
        stone = AssetDatabase.LoadAssetAtPath<Material>("Assets/Scenes/Reve/Materials/Limbes_8508bf557d417f64ea03e22431806255.mat");
        EnsureFolder(Folder + "/Geometry");
        foreach (int index in Enumerable.Range(0, 3))
        {
            var branch = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Single(t => t.name == "Branche_" + Labels[index]);
            foreach (string part in new[] { "Montee_basse", "Retour_en_surplomb" })
            {
                if (branch.Find("Rampe_" + part) != null) continue;
                var nodes = branch.Cast<Transform>().Where(t => t.name.StartsWith(part + "_")).OrderBy(t => t.name, StringComparer.Ordinal).ToArray();
                Vector3 a = branch.InverseTransformPoint(nodes[0].position + Vector3.up * .25f);
                Vector3 b = branch.InverseTransformPoint(nodes[nodes.Length - 1].position + Vector3.up * .25f);
                Vector3 axis = b - a;
                Vector3 side = Vector3.Cross(Vector3.up, new Vector3(axis.x, 0, axis.z)).normalized * 1.1f;
                var mesh = new Mesh { name = "LimbesRamp_" + Labels[index] + "_" + part };
                mesh.vertices = new[] { a-side, a+side, b+side, b-side, a-side-Vector3.up*.5f, a+side-Vector3.up*.5f, b+side-Vector3.up*.5f, b-side-Vector3.up*.5f };
                mesh.triangles = new[] { 0,3,2,0,2,1, 4,5,6,4,6,7, 0,1,5,0,5,4, 1,2,6,1,6,5, 2,3,7,2,7,6, 3,0,4,3,4,7 };
                mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up, Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
                mesh.RecalculateNormals(); mesh.RecalculateBounds();
                AssetDatabase.CreateAsset(mesh, Folder + "/Geometry/" + mesh.name + ".asset");
                var ramp = Child(branch, "Rampe_" + part, Vector3.zero);
                ramp.AddComponent<MeshFilter>().sharedMesh = mesh; ramp.AddComponent<MeshRenderer>().sharedMaterial = stone;
                ramp.AddComponent<MeshCollider>().sharedMesh = mesh;
                foreach (Transform node in nodes)
                {
                    foreach (var collider in node.GetComponents<Collider>()) Object.DestroyImmediate(collider);
                    foreach (var renderer in node.GetComponents<Renderer>()) Object.DestroyImmediate(renderer);
                    foreach (var filter in node.GetComponents<MeshFilter>()) Object.DestroyImmediate(filter);
                }
            }
        }
        EditorSceneManager.SaveScene(scene);
        try { BakeNavigation(owned, scene); }
        finally { foreach (var other in owned) if (other.IsValid() && other.isLoaded) EditorSceneManager.CloseScene(other, true); }
        LimbesExpansionVerification.Run();
    }

    public static void FinalizeLandingAndPersistence()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var owned = new List<Scene>();
        foreach (int index in Enumerable.Range(0, 3))
        {
            var branch = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Single(t => t.name == "Branche_" + Labels[index]);
            foreach (string part in new[] { "Montee_basse", "Retour_en_surplomb" })
            {
                var nodes = branch.Cast<Transform>().Where(t => t.name.StartsWith(part + "_")).OrderBy(t => t.name, StringComparer.Ordinal).ToArray();
                Vector3 a = branch.InverseTransformPoint(nodes[0].position + Vector3.up * .25f);
                Vector3 b = branch.InverseTransformPoint(nodes[nodes.Length-1].position + Vector3.up * .25f);
                Vector3 direction = Vector3.ProjectOnPlane(b-a, Vector3.up).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, direction) * 1.1f;
                Vector3 before = b - direction * (part == "Montee_basse" ? 3.3f : 4.8f);
                var mesh = branch.Find("Rampe_" + part).GetComponent<MeshFilter>().sharedMesh;
                var top = new[] { a-side, a+side, before-side, before+side, b-side, b+side };
                var vertices = top.Concat(top.Select(v => v-Vector3.up*.5f)).ToArray();
                var triangles = new List<int>();
                for (int segment=0; segment<2; segment++)
                {
                    int i=segment*2;
                    triangles.AddRange(new[] { i,i+2,i+3, i,i+3,i+1, i+6,i+7,i+9, i+6,i+9,i+8,
                        i,i+6,i+8, i,i+8,i+2, i+1,i+3,i+9, i+1,i+9,i+7 });
                }
                triangles.AddRange(new[] { 0,1,7,0,7,6, 4,10,11,4,11,5 });
                mesh.Clear(); mesh.vertices=vertices; mesh.triangles=triangles.ToArray();
                mesh.uv=Enumerable.Range(0,12).Select(i=>new Vector2(i%2,(i%6)/2f)).ToArray();
                mesh.RecalculateNormals(); mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh); AssetDatabase.SaveAssetIfDirty(mesh);
                var collider=branch.Find("Rampe_"+part).GetComponent<MeshCollider>(); collider.sharedMesh=null; collider.sharedMesh=mesh;
            }
        }
        var boss=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<LimbesAngelBoss>(true)).Single();
        if (boss.GetComponentInParent<SceneMarker>() == null)
        {
            var markerRoot=Child(boss.transform.parent,"Marker_Gargouille_Ange",boss.transform.localPosition);
            markerRoot.transform.rotation=boss.transform.rotation;
            boss.transform.SetParent(markerRoot.transform,true);
            var marker=markerRoot.AddComponent<SceneMarker>();
            marker.SetCharacterData(boss.GetComponent<CharacterInfo>().SourceData); marker.SetBakedCharacterInstance(boss.gameObject);
            EditorSceneManager.SaveScene(scene);
            marker.SendMessage("OnValidate", SendMessageOptions.RequireReceiver);
        }
        EditorSceneManager.SaveScene(scene);
        try { BakeNavigation(owned,scene); }
        finally { foreach(var other in owned) if(other.IsValid()&&other.isLoaded) EditorSceneManager.CloseScene(other,true); }
        FixProofLandings();
    }
    public static void RepairPresentation()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var boss = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<LimbesAngelBoss>(true)).Single();
        var visual = boss.visual;
        visual.localPosition = Vector3.zero; visual.localRotation = Quaternion.identity; visual.localScale = Vector3.one;
        var renderers = visual.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        visual.localScale *= 3.5f / bounds.size.y;
        bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        visual.position += new Vector3(boss.transform.position.x - bounds.center.x, boss.transform.position.y - bounds.min.y, boss.transform.position.z - bounds.center.z);
        const string materialPath = Folder + "/Data/LimbesExpansionStone.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("HDRP/Lit")) { name = "LimbesExpansionStone" };
            material.SetTexture("_BaseColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/0 - UnityPackages/Fab/ModularDungeonCollection/Textures/T_Stone_Wall_01_Master_BaseColor.png"));
            material.SetTextureScale("_BaseColorMap", Vector2.one * 4);
            material.SetColor("_BaseColor", Color.white); material.SetFloat("_Smoothness", .2f);
            AssetDatabase.CreateAsset(material, materialPath);
        }
        foreach (var renderer in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MeshRenderer>(true)))
            if (renderer.GetComponentInParent<LimbesAngelBoss>() == null && renderer.GetComponentInParent<Flame>() == null)
                renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => material).ToArray();
        const string angelMaterialPath = Folder + "/Data/LimbesAngelStone.mat";
        var angelMaterial = AssetDatabase.LoadAssetAtPath<Material>(angelMaterialPath);
        if (angelMaterial == null)
        {
            angelMaterial = new Material(Shader.Find("HDRP/Lit")) { name = "LimbesAngelStone" };
            angelMaterial.SetColor("_BaseColor", new Color(.67f, .65f, .57f)); angelMaterial.SetFloat("_Smoothness", .3f);
            AssetDatabase.CreateAsset(angelMaterial, angelMaterialPath);
        }
        foreach (var renderer in renderers) renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => angelMaterial).ToArray();
        foreach (string label in Labels)
        {
            var branch = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Single(t => t.name == "Branche_" + label);
            foreach (Transform child in branch.Cast<Transform>().ToArray())
            {
                string sourcePath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(child.gameObject);
                if (sourcePath == "Assets/0 - UnityPackages/Fab/ModularDungeonCollection/Prefabs/Doorway_Pillar_01 Variant.prefab")
                    Object.DestroyImmediate(child.gameObject);
            }
        }
        EditorSceneManager.SaveScene(scene);
        PrefabUtility.SaveAsPrefabAsset(boss.gameObject, Folder + "/Prefabs/StoneAngel.prefab");
        LimbesExpansionVerification.Run();
    }

    public static void ClearCaptiviteLanding()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var flame = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Single(t => t.name == "Flame_Captivite.mid");
        flame.localPosition = (Starts[1] + Peaks[1]) * .5f + new Vector3(5, 0, -8) + new Vector3(-2.2f, .5f, 2.2f);
        EditorSceneManager.SaveScene(scene);
        var owned = new List<Scene>();
        try { BakeNavigation(owned, scene); }
        finally { foreach (var other in owned) if (other.IsValid() && other.isLoaded) EditorSceneManager.CloseScene(other, true); }
        LimbesExpansionVerification.Run();
    }

    public static void FixProofLandings()
    {
        var world=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        Physics.SyncTransforms();
        foreach(string label in Labels)
        {
            var scene=EditorSceneManager.OpenScene(SceneFolder+"/Reve_Limbes_Cycle_"+label+".unity",OpenSceneMode.Additive);
            try
            {
                foreach(var item in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<InteractableItem>(true)))
                {
                    var hits=Physics.RaycastAll(item.transform.position+Vector3.up*30,Vector3.down,60,~0,QueryTriggerInteraction.Ignore)
                        .Where(h=>h.collider.gameObject.scene==world&&h.normal.y>.6f).OrderBy(h=>h.distance).ToArray();
                    if(hits.Length==0) throw new InvalidOperationException("No physical floor under "+item.name);
                    Vector3 point=item.transform.position;point.y=hits[0].point.y+.35f;item.transform.position=point;
                }
                EditorSceneManager.SaveScene(scene);
            }
            finally {EditorSceneManager.CloseScene(scene,true);}
        }
        LimbesExpansionVerification.Run();
    }
    private static CycleRequirements RequireSteps(params string[] ids) => new() { conditions = ids.Select(id => new CycleRequirement { kind = CycleRequirementKind.Step, stepId = id }).ToArray() };
    private static void RequireValid(CycleDefinition definition)
    {
        var issues = definition.ValidateConfiguration(); if (issues.Count != 0) throw new InvalidOperationException(string.Join("; ", issues));
    }
    private static void AddFlame(Scene scene, Transform parent, Vector3 position, string id, float radius)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Model_Flame_WallTorch.prefab");
        var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene); root.transform.SetParent(parent, false); root.transform.localPosition = position;
        root.name = "Flame_" + id; root.GetComponent<Flame>().ConfigureFromSceneMarker("reve.limbes.expansion." + id, false, true, 2, radius, 0);
    }
    private static void Walkway(Transform parent, Vector3 a, Vector3 b, string name)
    {
        int count = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(a, b) / .65f));
        for (int i = 0; i <= count; i++) Floor(parent, name + "_" + i.ToString("00"), Vector3.Lerp(a, b, (float)i / count), new Vector3(2.2f, .5f, 2.2f));
    }
    private static void Floor(Transform parent, string name, Vector3 top, Vector3 size) => Primitive(parent, name, PrimitiveType.Cube, top - Vector3.up * size.y * .5f, size, stone, true);
    private static GameObject Primitive(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material, bool collision)
    {
        var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = material; if (!collision) Object.DestroyImmediate(go.GetComponent<Collider>()); return go;
    }
    private static GameObject Child(Transform parent, string name, Vector3 position)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = position; return go;
    }
    private static Scene NewScene(string path, List<Scene> owned)
    {
        if (File.Exists(path)) throw new InvalidOperationException("Refusing to overwrite " + path);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        EditorSceneManager.SaveScene(scene, path); owned.Add(scene); return scene;
    }
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/')); AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
    }
    private static void Set(Object target, string field, Object value) { var so = new SerializedObject(target); so.FindProperty(field).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(target); }
    private static void Set(Object target, string field, string value) { var so = new SerializedObject(target); so.FindProperty(field).stringValue = value; so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(target); }
    private static void Set(Object target, string field, bool value) { var so = new SerializedObject(target); so.FindProperty(field).boolValue = value; so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(target); }
    private static void Set(Object target, string field, int value) { var so = new SerializedObject(target); var p = so.FindProperty(field); if (p.propertyType == SerializedPropertyType.Enum) p.enumValueIndex = value; else p.intValue = value; so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(target); }
    private static void Set(Object target, string field, float value) { var so = new SerializedObject(target); so.FindProperty(field).floatValue = value; so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(target); }
}
