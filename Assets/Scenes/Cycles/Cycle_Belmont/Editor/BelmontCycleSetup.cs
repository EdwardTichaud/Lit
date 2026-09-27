using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;

/// <summary>
/// Creates the authored Belmont cycle without relying on hand-edited scene YAML.
/// It is deliberately repeatable: assets retain their GUIDs and the additive scene
/// is rebuilt only when this explicit menu command is run.
/// </summary>
[InitializeOnLoad]
public static class BelmontCycleSetup
{
    private const string Root = "Assets/Scenes/Cycles/Cycle_Belmont";
    private const string Data = Root + "/Data";
    private const string Prefabs = Root + "/Prefabs";
    private const string ScenePath = Root + "/District_1_Cycle_Belmont.unity";
    private const string CorridorScenePath = "Assets/Scenes/District_1/District_1_Corridor_Environment.unity";
    private const string DefinitionPath = "Assets/Resources/Narrative/BelmontCycle.asset";
    private const string ActivationId = "district1.belmont.crypt_access";
    private const string AncientFlameActivationId = "district1.first_ancient_flame.lit";

    static BelmontCycleSetup() => EditorApplication.update += Poll;

    private static void Poll()
    {
        const string request = "Library/BelmontCycle.request";
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(request)) return;
        File.Delete(request);
        try { Create(); File.WriteAllText("Library/BelmontCycle.result", "Created and validated " + ScenePath); }
        catch (Exception exception) { File.WriteAllText("Library/BelmontCycle.result", exception.ToString()); Debug.LogException(exception); }
    }

    [MenuItem("Lit/Narrative/Create Belmont Cycle")]
    public static void Create()
    {
        // The encounter always uses the shared runtime enemy contract.  This
        // prevents a scene rebuild from bringing back the legacy controller
        // that lacked the locomotion parameters required by EnemyController.
        EnemyAnimationContractMigration.Migrate();
        EnsureFolder(Data); EnsureFolder(Prefabs); EnsureFolder("Assets/Resources/Narrative");
        GatePose legacyGatePose = CaptureLegacyGatePose();

        Item roster = Readable("Item_Belmont_ShiftRegister", "Registre de relève", "registre_belmont_releve",
            "REGISTRE DE RELÈVE — VEILLÉE DE LA LUNE\n\nTour 4 : Éloïse Belmont, absente pour faiblesse de voix.\nÉtienne Belmont prend le tour entier.\n\nLa flamme ne doit pas demeurer seule.");
        Item note = Readable("Item_Belmont_FamilyNote", "Note d’Étienne", "note_belmont_etienne",
            "Éloïse,\n\nNe force pas ta voix cette nuit. Je prendrai la Veillée, comme tu l’as fait pour moi l’hiver dernier. Garde la lanterne près de toi ; je reviendrai avant le changement de tour.\n\nÉtienne");
        Item maintenance = Readable("Item_Belmont_FlameMaintenance", "Relevé des conduits", "releve_belmont_conduits",
            "RELEVÉ D’ENTRETIEN DES FLAMES\n\nÉtienne Belmont affecté aux conduits inférieurs après la relève.\nLa pression baisse sous l’escalier muré.\n\nAucun retour enregistré.");

        KnowledgeSO shift = Knowledge("shift_register", "La relève d’Étienne", "Étienne Belmont a pris le tour de Veillée d’Éloïse.", roster, "etienne_belmont");
        KnowledgeSO choice = Knowledge("family_note", "Le choix d’Étienne", "Étienne a pris cette Veillée volontairement afin qu’Éloïse puisse se reposer.", note, "etienne_belmont");
        KnowledgeSO conduits = Knowledge("flame_maintenance", "Les conduits inférieurs", "Après la relève, Étienne a été affecté aux conduits sous l’escalier muré.", maintenance, "etienne_belmont");
        KnowledgeSO understood = Knowledge("duty_understood", "Le tour de la Veillée", "Étienne n’a pas abandonné Éloïse : il a choisi de prendre son tour de Veillée.", null, "eloise_belmont");
        KnowledgeSO flameNetwork = Knowledge("lower_flame_network", "Le réseau de Veillée", "Les Flames des conduits forment un réseau qui tient la fracture à distance.", null, "etienne_belmont");
        KnowledgeSO firstAncient = Knowledge("first_ancient_flame", "La première Ancient Flame", "Une Ancient Flame ne se contente pas d’éclairer : elle fixe une part du temps du district.", null, "district_1");
        roster.knowledgeUnlockedOnRead = new List<KnowledgeSO> { shift };
        note.knowledgeUnlockedOnRead = new List<KnowledgeSO> { choice };
        maintenance.knowledgeUnlockedOnRead = new List<KnowledgeSO> { conduits };

        FamilyRecord eloise = Family("FamilyRecord_Eloise_Belmont", "eloise_belmont", "Éloïse Belmont", "belmont", "etienne_belmont", "district_1.corridor.vigil_alcove");
        FamilyRecord etienne = Family("FamilyRecord_Etienne_Belmont", "etienne_belmont", "Étienne Belmont", "belmont", "eloise_belmont", "district_1.crypt.conduits");
        TransgenerationalObjectRecord lantern = Asset<TransgenerationalObjectRecord>(Data + "/TransgenerationalObject_Belmont_Lantern.asset");
        lantern.objectId = "belmont_lantern"; lantern.displayName = "Lanterne de Veillée Belmont";
        lantern.description = "Une petite lanterne cabossée, transmise entre les gardiens de Veillée.";
        lantern.associatedLineageId = "belmont"; lantern.successiveOwnerIds = new List<string> { "etienne_belmont", "eloise_belmont" };
        lantern.appearsAtAges = new List<TemporalAge> { TemporalAge.Age666 }; lantern.foundLocations = new List<string> { "district_1.corridor.vigil_alcove" };
        lantern.notes = "Objet domestique : il relie la note d’Étienne à la garde d’Éloïse.";
        RegistryEntry entry = Asset<RegistryEntry>(Data + "/RegistryEntry_Belmont_ShiftTransfer.asset");
        entry.entryId = "reg_belmont_shift_transfer"; entry.personName = "Étienne Belmont"; entry.entryType = RegistryEntryType.Vigil;
        entry.age = TemporalAge.Age666; entry.district = "district_1"; entry.room = "couloir_de_veillee";
        entry.cause = "Relève d’Éloïse Belmont et affectation aux conduits inférieurs."; entry.note = "Aucun retour enregistré.";
        entry.readableReferences = new List<Item> { roster, maintenance }; entry.associatedObjectIds = new List<string> { "belmont_lantern" };

        GameObject ghostPrefab = CreateGhostPrefab();
        GhostData ghostData = Asset<GhostData>(Data + "/GhostData_Eloise_Belmont.asset");
        ghostData.ghostId = "ghost_eloise_belmont"; ghostData.displayName = "Éloïse Belmont";
        ghostData.worldPrefab = ghostPrefab;
        ghostData.apparitionAge = TemporalAge.Age666; ghostData.apparitionLocationId = "district_1.corridor.vigil_alcove";
        ghostData.targetId = "etienne_belmont"; ghostData.targetDisplayName = "Étienne Belmont"; ghostData.expectedAnswerType = GhostAnswerType.Event;
        ghostData.apparitionLine = "La flamme baisse… Étienne devait revenir avant le changement de tour.";
        ghostData.question = "As-tu vu Étienne ?";
        ghostData.missingKnowledgeLine = "Il a pris ma Veillée. Pourquoi n’est-il pas revenu ?";
        ghostData.reactions = new List<GhostKnowledgeReaction> {
            new GhostKnowledgeReaction {
                reactionId = "etienne_took_the_watch", priority = 100, marksGhostUnderstood = true,
                optionText = "Étienne a pris ton tour pour te laisser te reposer. Il a ensuite été envoyé aux conduits.",
                responseLine = "Alors il ne m’a pas abandonnée… Il a gardé la flamme jusqu’au bout. Je peux enfin laisser son tour se terminer.",
                requirement = AllKnowledge(shift, choice, conduits), unlockKnowledge = new List<KnowledgeSO> { understood },
                notes = "Résolution humaine : le cycle ne révèle ni la mort exacte d’Étienne ni le rituel."
            }
        };
        ghostData.evidence = new List<GhostEvidenceReference> {
            Evidence(GhostEvidenceType.RegistryEntry, entry, null, null, roster, "district_1.corridor.archive_table", "Le registre confirme la relève."),
            Evidence(GhostEvidenceType.ReadableItem, null, eloise, lantern, note, "district_1.corridor.vigil_alcove", "La note établit le choix d’Étienne."),
            Evidence(GhostEvidenceType.ReadableItem, entry, etienne, null, maintenance, "district_1.corridor.dark_niche", "Le relevé suit Étienne jusqu’aux conduits.")
        };

        CycleDefinition cycle = Asset<CycleDefinition>(DefinitionPath);
        cycle.cycleId = "district1.belmont"; cycle.title = "Le tour de la Veillée";
        cycle.description = "Suivre la Veillée d’Étienne, restaurer les conduits et rallumer la première Ancient Flame."; cycle.category = CycleCategory.Main;
        cycle.prerequisites = new CycleRequirements { mode = CycleRequirementMode.All, conditions = Array.Empty<CycleRequirement>() };
        cycle.completionPresentationTimeout = 15f; cycle.cycleSceneName = "District_1_Cycle_Belmont"; cycle.dialogueSeconds = 4f;
        cycle.steps = new[] {
            KnowledgeStep("shift_register_read", "Lire le registre de relève", "Le registre établit qu’Étienne a pris le tour d’Éloïse.", shift),
            KnowledgeStep("family_note_read", "Lire la note d’Étienne", "La note montre qu’il a choisi cette Veillée.", choice),
            KnowledgeStep("flame_maintenance_read", "Lire le relevé des conduits", "Étienne a été envoyé dans les salles inférieures.", conduits),
            new CycleStep { id = "eloise_spoken", title = "Apaiser Éloïse", description = "Lui dire ce qu’Étienne a choisi.", kind = CycleStepKind.DialogueCompleted, sourceId = "eloise", prerequisites = Steps("shift_register_read", "family_note_read", "flame_maintenance_read"), rewards = new[] { new CycleReward { kind = CycleRewardKind.Knowledge, knowledge = understood } } },
            new CycleStep { id = "shadow_warden_defeated", title = "Vaincre le Veilleur d’Ombre", description = "La créature des zones obscures bloque la descente vers la crypte.", kind = CycleStepKind.EnemyDefeated, sourceId = "shadow_warden",
                prerequisites = Steps("eloise_spoken"), rewards = new[] { new CycleReward { kind = CycleRewardKind.Activation, key = ActivationId, boolValue = true } } },
            new CycleStep { id = "lucian_after_shadow", title = "Écouter Lucian", description = "Quelque chose se dissipe dans les conduits.", kind = CycleStepKind.DialogueCompleted, sourceId = "lucian_after_shadow", prerequisites = Steps("shadow_warden_defeated") },
            new CycleStep { id = "lower_flame_one_lit", title = "Rallumer la première Flame", description = "Rétablir la lumière de Veillée dans les conduits.", kind = CycleStepKind.Interaction, sourceId = "lower_flame_one", prerequisites = Steps("lucian_after_shadow") },
            new CycleStep { id = "lower_flame_two_lit", title = "Rallumer la deuxième Flame", description = "La lumière révèle le chemin suivant.", kind = CycleStepKind.Interaction, sourceId = "lower_flame_two", prerequisites = Steps("lower_flame_one_lit") },
            new CycleStep { id = "lower_flame_three_lit", title = "Rallumer la troisième Flame", description = "Le réseau de Veillée se reforme.", kind = CycleStepKind.Interaction, sourceId = "lower_flame_three", prerequisites = Steps("lower_flame_two_lit") },
            new CycleStep { id = "lower_flames_restored", title = "Comprendre le réseau de Veillée", description = "Les Flames maintenaient la fracture sous l’escalier.", kind = CycleStepKind.DialogueCompleted, sourceId = "lucian_flame_network", prerequisites = Steps("lower_flame_three_lit"), rewards = new[] { new CycleReward { kind = CycleRewardKind.Knowledge, knowledge = flameNetwork } } },
            new CycleStep { id = "broken_anchor_defeated", title = "Vaincre l’Ancre brisée", description = "Une masse de fracture retient l’Ancient Flame dans l’obscurité.", kind = CycleStepKind.EnemyDefeated, sourceId = "broken_anchor", prerequisites = Steps("lower_flames_restored") },
            new CycleStep { id = "first_ancient_flame_lit", title = "Rallumer la première Ancient Flame", description = "Rendre au district sa première Veillée ancienne.", kind = CycleStepKind.Interaction, sourceId = "first_ancient_flame", terminal = true, prerequisites = Steps("broken_anchor_defeated"),
                rewards = new[] { new CycleReward { kind = CycleRewardKind.Knowledge, knowledge = firstAncient }, new CycleReward { kind = CycleRewardKind.Activation, key = AncientFlameActivationId, boolValue = true } } }
        };
        cycle.dialogues = new[] {
            new CycleDialogue { id = "eloise", durationSeconds = 5f, condition = new CycleCondition { requirements = Steps("shift_register_read", "family_note_read", "flame_maintenance_read") },
                line = "Alors il ne m’a pas abandonnée… Il a gardé la flamme jusqu’au bout. Je peux enfin laisser son tour se terminer.",
                unavailableLine = "Il a pris ma Veillée. Pourquoi n’est-il pas revenu ?", disappearAfterCompletion = true, disappearanceDelay = 0f }
        };

        GameObject enemyPrefab = CreateEnemyPrefab();
        CharacterData enemyData = Asset<CharacterData>(Data + "/CharacterData_VeilleurDOmbre.asset");
        enemyData.characterId = "shadow_warden_belmont"; enemyData.characterName = "Veilleur d’Ombre"; enemyData.isEnemy = true;
        enemyData.hp = 90; enemyData.worldPrefab = enemyPrefab;
        // Combat profile and skills deliberately inherit the stable Juggernaut setup for this first pass.
        CharacterData juggernaut = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/Characters/3_Enemy/Juggernaut/Juggernaut.asset");
        if (juggernaut != null) { enemyData.enemyCombatProfile = juggernaut.enemyCombatProfile; enemyData.combatSkills = new List<SkillSO>(juggernaut.combatSkills); enemyData.enemySettings = juggernaut.enemySettings; enemyData.vision = juggernaut.vision; }

        BuildScene(cycle, ghostData, roster, note, maintenance, enemyPrefab, enemyData);
        ConfigurePersistentCryptGate(legacyGatePose);
        Register(ScenePath);
        foreach (UnityEngine.Object asset in new UnityEngine.Object[] { roster, note, maintenance, shift, choice, conduits, understood, flameNetwork, firstAncient, eloise, etienne, lantern, entry, ghostData, ghostPrefab, cycle, enemyData, enemyPrefab }) if (asset != null) EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log("[BelmontCycle] Cycle créé et enregistré.");
    }

    private static void BuildScene(CycleDefinition cycle, GhostData ghostData, Item roster, Item note, Item maintenance, GameObject enemyPrefab, CharacterData enemyData)
    {
        Scene existing = SceneManager.GetSceneByPath(ScenePath); if (existing.IsValid() && existing.isLoaded) EditorSceneManager.CloseScene(existing, true);
        var previous = SceneManager.GetActiveScene();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); SceneManager.SetActiveScene(scene);
        try {
            var root = new GameObject("BelmontCycle"); root.transform.position = new Vector3(50.06f, -97.4f, 95.57f);
            root.AddComponent<NetworkObject>(); var controller = root.AddComponent<CycleController>(); controller.definition = cycle;
            var alcove = Child(root, "Alcôve_de_Veillée"); alcove.transform.localPosition = new Vector3(0, 0, 0);
            var ghost = CreateGhost(alcove, ghostData, controller); ghost.transform.localPosition = new Vector3(0, 0, 0);
            CreateReadable(root, "Registre_de_relève", roster, new Vector3(3, 0, 1));
            CreateReadable(root, "Note_d’Étienne", note, new Vector3(-3, 0, 1));
            CreateReadable(root, "Relevé_des_conduits", maintenance, new Vector3(0, 0, 6));
            var bossRoot = new GameObject("ShadowGuardian_Spawn"); bossRoot.transform.SetParent(root.transform, false); bossRoot.transform.localPosition = new Vector3(0, 0, 10); bossRoot.SetActive(false);
            var boss = PrefabUtility.InstantiatePrefab(enemyPrefab, bossRoot.transform) as GameObject; boss.name = "ShadowGuardian"; boss.transform.localPosition = Vector3.zero;
            var enemy = boss.GetComponent<EnemyController>();
            var conduitOne = CreateCycleFlame(root, "Flame_Conduit_1", new Vector3(0, 0, 17), "district1.belmont.conduit_1", false);
            var conduitTwo = CreateCycleFlame(root, "Flame_Conduit_2", new Vector3(0, 0, 24), "district1.belmont.conduit_2", false);
            var conduitThree = CreateCycleFlame(root, "Flame_Conduit_3", new Vector3(0, 0, 31), "district1.belmont.conduit_3", false);
            conduitOne.gameObject.SetActive(false); conduitTwo.gameObject.SetActive(false); conduitThree.gameObject.SetActive(false);
            var anchorRoot = new GameObject("BrokenAnchor_Spawn"); anchorRoot.transform.SetParent(root.transform, false); anchorRoot.transform.localPosition = new Vector3(0, 0, 40); anchorRoot.SetActive(false);
            var giantPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Characters/3_Enemy/GiantJuggernaut/GiantJuggernaut.prefab");
            if (giantPrefab == null) throw new InvalidOperationException("Prefab GiantJuggernaut introuvable.");
            var anchor = PrefabUtility.InstantiatePrefab(giantPrefab, anchorRoot.transform) as GameObject; anchor.name = "BrokenAnchor"; anchor.transform.localPosition = Vector3.zero;
            var anchorEnemy = anchor.GetComponent<EnemyController>();
            var ancientRoot = CreateCycleFlame(root, "AncientFlame_PremiereVeille", new Vector3(0, 0, 49), "district1.belmont.first_ancient", true);
            ancientRoot.gameObject.SetActive(false);
            controller.interactions = new[] { ghost.GetComponent<CycleInteraction>() };
            controller.encounters = new[] { new CycleEncounterBinding { id = "shadow_warden", enemy = enemy }, new CycleEncounterBinding { id = "broken_anchor", enemy = anchorEnemy } };
            controller.flames = new[] { new CycleFlameBinding { id = "lower_flame_one", flame = conduitOne }, new CycleFlameBinding { id = "lower_flame_two", flame = conduitTwo }, new CycleFlameBinding { id = "lower_flame_three", flame = conduitThree }, new CycleFlameBinding { id = "first_ancient_flame", flame = ancientRoot } };
            controller.autoDialogues = new[] {
                new CycleAutoDialogueBinding { id = "lucian_after_shadow", condition = new CycleCondition { requirements = Steps("shadow_warden_defeated") }, durationSeconds = 5f, line = "Lucian : Mais qu’est-ce qu’il se passe ici ? J’ai une impression… de rage… dissipée. Mais les conduits restent plongés dans le noir." },
                new CycleAutoDialogueBinding { id = "lucian_flame_network", condition = new CycleCondition { requirements = Steps("lower_flame_three_lit") }, durationSeconds = 5f, line = "Lucian : Ces Flames ne servaient pas seulement à éclairer. Elles tenaient quelque chose à distance. Là-bas… cette lumière ancienne attend encore." }
            };
            controller.activations = new[] {
                new CycleActivationBinding { target = bossRoot, condition = new CycleCondition { requirements = Steps("eloise_spoken") } },
                new CycleActivationBinding { target = conduitOne.gameObject, condition = new CycleCondition { requirements = Steps("lucian_after_shadow") } },
                new CycleActivationBinding { target = conduitTwo.gameObject, condition = new CycleCondition { requirements = Steps("lower_flame_one_lit") } },
                new CycleActivationBinding { target = conduitThree.gameObject, condition = new CycleCondition { requirements = Steps("lower_flame_two_lit") } },
                new CycleActivationBinding { target = anchorRoot, condition = new CycleCondition { requirements = Steps("lower_flames_restored") } },
                new CycleActivationBinding { target = ancientRoot.gameObject, condition = new CycleCondition { requirements = Steps("broken_anchor_defeated") } }
            };
            EditorSceneManager.SaveScene(scene, ScenePath);
        } finally { EditorSceneManager.CloseScene(scene, true); if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous); }
    }

    private static GameObject CreateEnemyPrefab()
    {
        GameObject shadowGuardian = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/ShadowGuardian.prefab");
        if (shadowGuardian == null) throw new InvalidOperationException("Prefab ShadowGuardian introuvable.");
        return shadowGuardian;
    }

    private static GameObject CreateGhostPrefab()
    {
        const string path = Prefabs + "/Prefab_Ghost_Eloise_Belmont.prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Characters/9_Ghosts/Nina/Ghost_Model_Nina.prefab");
        if (source == null) throw new InvalidOperationException("Prefab de fantôme source introuvable.");
        GameObject instance = PrefabUtility.InstantiatePrefab(source) as GameObject;
        instance.name = "Prefab_Ghost_Eloise_Belmont";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, path);
        UnityEngine.Object.DestroyImmediate(instance);
        return prefab;
    }

    private static GameObject CreateGhost(GameObject parent, GhostData data, CycleController cycle)
    {
        var actor = new GameObject("Ghost_Eloise_Belmont"); actor.transform.SetParent(parent.transform, false); actor.AddComponent<SphereCollider>().isTrigger = true;
        if (data.worldPrefab != null)
        {
            var visual = PrefabUtility.InstantiatePrefab(data.worldPrefab, actor.transform) as GameObject;
            visual.name = "Eloise_Belmont_Visual";
            visual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        }
        var ghost = actor.AddComponent<GhostController>(); ghost.SetGhostData(data);
        var interaction = actor.AddComponent<CycleInteraction>(); interaction.cycle = cycle; interaction.dialogueId = "eloise";
        interaction.useGhostPuzzleResolution = true;
        return actor;
    }

    private readonly struct GatePose
    {
        public readonly bool valid;
        public readonly Vector3 position;
        public readonly Quaternion rotation;
        public readonly Vector3 scale;
        public GatePose(Transform transform) { valid = transform != null; position = transform != null ? transform.position : default; rotation = transform != null ? transform.rotation : Quaternion.identity; scale = transform != null ? transform.lossyScale : Vector3.one; }
    }

    // Same spatial threshold as the former Crypt_Wall, expressed in District_1 world space.
    private static GatePose DefaultGatePose()
    {
        var anchor = new GameObject("BelmontGatePoseAnchor");
        anchor.transform.SetPositionAndRotation(new Vector3(38.29143f, -90.9777f, 102.2475f), Quaternion.Euler(0f, 90f, 0f));
        anchor.transform.localScale = new Vector3(.7125001f, 10.645983f, 8.919549f);
        GatePose pose = new GatePose(anchor.transform);
        UnityEngine.Object.DestroyImmediate(anchor);
        return pose;
    }

    private static GatePose CaptureLegacyGatePose()
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool alreadyLoaded = scene.IsValid() && scene.isLoaded;
        if (!alreadyLoaded && File.Exists(ScenePath)) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        Transform wall = scene.IsValid() && scene.isLoaded
            ? scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).FirstOrDefault(transform => transform.name == "Crypt_Wall")
            : null;
        GatePose pose = new GatePose(wall);
        if (!alreadyLoaded && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
        return pose;
    }

    private static void ConfigurePersistentCryptGate(GatePose legacyPose)
    {
        if (!File.Exists(CorridorScenePath)) throw new InvalidOperationException("Scène de corridor introuvable.");
        Scene scene = SceneManager.GetSceneByPath(CorridorScenePath);
        bool alreadyLoaded = scene.IsValid() && scene.isLoaded;
        if (!alreadyLoaded) scene = EditorSceneManager.OpenScene(CorridorScenePath, OpenSceneMode.Additive);
        try
        {
            GameObject existing = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "BelmontCryptGatePersistent");
            GatePose preservedPose = existing != null ? new GatePose(existing.transform.childCount > 0 ? existing.transform.GetChild(0) : existing.transform) : default;
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing);
            GatePose pose = legacyPose.valid ? legacyPose : preservedPose.valid ? preservedPose : DefaultGatePose();
            var root = new GameObject("BelmontCryptGatePersistent");
            SceneManager.MoveGameObjectToScene(root, scene);
            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.name = "Crypt_Wall_Belmont";
            SceneManager.MoveGameObjectToScene(blocker, scene);
            blocker.transform.SetParent(root.transform, true);
            blocker.transform.SetPositionAndRotation(pose.position, pose.rotation);
            blocker.transform.localScale = pose.scale;
            var activation = root.AddComponent<CycleActivationId>();
            activation.activationId = ActivationId;
            activation.target = blocker;
            activation.activeWhenSet = false;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        finally { if (!alreadyLoaded && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true); }
    }

    private static Flame CreateCycleFlame(GameObject parent, string name, Vector3 position, string id, bool ancient)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent.transform, false);
        root.transform.localPosition = position;
        root.AddComponent<NetworkObject>();
        var trigger = root.AddComponent<SphereCollider>(); trigger.isTrigger = true; trigger.radius = 2f;
        var flame = root.AddComponent<Flame>();
        root.AddComponent<PersistentFlameState>();
        flame.flameLight = root.AddComponent<Light>();
        flame.flameLight.type = LightType.Point;
        flame.flameLight.range = ancient ? 12f : 7f;
        flame.flameLight.intensity = ancient ? 3.5f : 2f;
        flame.flameLight.color = ancient ? new Color(.35f, .72f, 1f) : new Color(1f, .55f, .14f);
        var brazier = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        brazier.name = "Brazier"; brazier.transform.SetParent(root.transform, false);
        brazier.transform.localPosition = new Vector3(0, .35f, 0); brazier.transform.localScale = new Vector3(.55f, .35f, .55f);
        var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        visual.name = ancient ? "AncientFlameVisual" : "FlameVisual";
        visual.transform.SetParent(root.transform, false); visual.transform.localPosition = new Vector3(0, 1.05f, 0);
        visual.transform.localScale = ancient ? new Vector3(.8f, 1.35f, .8f) : new Vector3(.45f, .8f, .45f);
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        material.color = flame.flameLight.color; material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", flame.flameLight.color * (ancient ? 3f : 2f));
        visual.GetComponent<Renderer>().sharedMaterial = material;
        flame.activateWhenLitTargets = new[] { visual };
        flame.ConfigureFromSceneMarker(id, ancient, false, 2f, ancient ? 12f : 7f, ancient ? 2 : 1);
        return flame;
    }

    private static void CreateReadable(GameObject parent, string name, Item item, Vector3 position)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Environment/2_Props/Props_Note_1.prefab");
        GameObject go = source != null ? PrefabUtility.InstantiatePrefab(source, parent.transform) as GameObject : GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name; go.transform.SetParent(parent.transform, false); go.transform.localPosition = position; go.AddComponent<NetworkObject>();
        var interactable = go.GetComponent<InteractableItem>() ?? go.AddComponent<InteractableItem>();
        interactable.interactableCategory = InteractableItem.InteractableCategory.RecoverableItem;
        interactable.representedItem = item;
        interactable.allowTake = true;
    }
    private static Item Readable(string file, string name, string id, string text) { var item = Asset<Item>(Data + "/" + file + ".asset"); item.itemId = id; item.itemName = name; item.description = name; item.readableKind = Item.ReadableKind.Parchment; item.parchmentText = text; item.canUse = true; item.readableContentId = id; return item; }
    private static KnowledgeSO Knowledge(string suffix, string title, string description, Item readable, string person) { var value = Asset<KnowledgeSO>(Data + "/Knowledge_Belmont_" + suffix + ".asset"); value.knowledgeId = "belmont." + suffix; value.title = title; value.description = description; value.category = KnowledgeCategory.Truth; value.districtId = "district_1"; value.personId = person; value.lineageId = "belmont"; value.readableItem = readable; value.tags = new List<string> { "belmont", "veillee" }; return value; }
    private static FamilyRecord Family(string file, string id, string name, string lineage, string relation, string location) { var value = Asset<FamilyRecord>(Data + "/" + file + ".asset"); value.recordId = id; value.displayName = name; value.lineageId = lineage; value.associatedObjectIds = new List<string> { "belmont_lantern" }; value.occupiedDistrictsOrRooms = new List<string> { location }; value.status = FamilyRecordStatus.Missing; value.notes = "Personnage du cycle Belmont."; value.spouseId = string.Empty; value.parentIds = new List<string>(); value.childIds = new List<string> { relation }; return value; }
    private static GhostEvidenceReference Evidence(GhostEvidenceType type, RegistryEntry entry, FamilyRecord family, TransgenerationalObjectRecord obj, Item item, string location, string note) => new GhostEvidenceReference { evidenceType = type, registryEntry = entry, familyRecord = family, transgenerationalObject = obj, readableItem = item, locationId = location, note = note };
    private static CycleStep KnowledgeStep(string id, string title, string description, KnowledgeSO knowledge) => new CycleStep { id = id, title = title, description = description, kind = CycleStepKind.Knowledge, knowledge = knowledge, prerequisites = new CycleRequirements { mode = CycleRequirementMode.All, conditions = Array.Empty<CycleRequirement>() } };
    private static CycleRequirements Steps(params string[] ids) => new CycleRequirements { mode = CycleRequirementMode.All, conditions = ids.Select(id => new CycleRequirement { kind = CycleRequirementKind.Step, stepId = id }).ToArray() };
    private static KnowledgeRequirement AllKnowledge(params KnowledgeSO[] values) => new KnowledgeRequirement { requiredKnowledge = values.ToList() };
    private static T Asset<T>(string path) where T : ScriptableObject { var value = AssetDatabase.LoadAssetAtPath<T>(path); if (value != null) return value; value = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(value, path); return value; }
    private static void EnsureFolder(string path) { if (AssetDatabase.IsValidFolder(path)) return; string parent = path[..path.LastIndexOf('/')]; EnsureFolder(parent); AssetDatabase.CreateFolder(parent, path[(path.LastIndexOf('/') + 1)..]); }
    private static GameObject Child(GameObject parent, string name) { var child = new GameObject(name); child.transform.SetParent(parent.transform, false); return child; }
    private static void Register(string scenePath)
    {
        var manifest = AssetDatabase.LoadAssetAtPath<ZoneManifest>("Assets/Scenes/Maison/ZoneManifest_District_1.asset");
        var asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
        if (manifest != null && asset != null)
        {
            if (!manifest.loadingScenes.Contains(asset)) manifest.loadingScenes.Add(asset);
            var serialized = new SerializedObject(manifest);
            var names = serialized.FindProperty("loadingSceneNames");
            bool hasName = Enumerable.Range(0, names.arraySize)
                .Any(index => names.GetArrayElementAtIndex(index).stringValue == "District_1_Cycle_Belmont");
            if (!hasName)
            {
                names.InsertArrayElementAtIndex(names.arraySize);
                names.GetArrayElementAtIndex(names.arraySize - 1).stringValue = "District_1_Cycle_Belmont";
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(manifest);
        }
        if (!EditorBuildSettings.scenes.Any(scene => scene.path == scenePath))
            EditorBuildSettings.scenes = EditorBuildSettings.scenes.Concat(new[] { new EditorBuildSettingsScene(scenePath, true) }).ToArray();
    }
}
