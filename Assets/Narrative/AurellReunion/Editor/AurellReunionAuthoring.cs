using System;
using System.IO;
using System.Linq;
using Lit.Story;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class AurellReunionAuthoring
{
    public const string Folder = "Assets/Narrative/AurellReunion";
    public const string DefinitionPath = Folder + "/AurellReunion.asset";
    public const string ScenePath = "Assets/Scenes/Cycles/Cycle_Aurell/District_1_Cycle_Aurell.unity";
    private const string Output = "Library/AurellFirstEncounter";
    static AurellReunionAuthoring() => EditorApplication.update += Poll;
    private static void Poll()
    {
        if (AssetDatabase.IsAssetImportWorkerProcess() || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Output + "/Install.request")) return;
        File.Delete(Output + "/Install.request");
        try { Install(); File.WriteAllText(Output + "/Install.result", "PASS: cycle installed; encounter anchor awaits author placement."); }
        catch (Exception e) { File.WriteAllText(Output + "/Install.result", "FAIL: " + e); Debug.LogException(e); }
    }

    [MenuItem("Lit/Narrative/Aurell/Install First Encounter")]
    public static void Install()
    {
        if (File.Exists(DefinitionPath) || File.Exists(ScenePath))
            throw new InvalidOperationException("Aurell content already exists; refusing to overwrite authored content.");
        Directory.CreateDirectory(Output);
        const string manifestPath = "Assets/Scenes/Maison/ZoneManifest_District_1.asset";
        const string catalogPath = "Assets/Resources/Narrative/District1CycleCatalog.asset";
        foreach (string path in new[] { manifestPath, catalogPath, "ProjectSettings/EditorBuildSettings.asset" })
            File.Copy(path, Output + "/" + Path.GetFileName(path) + ".before", true);
        EnsureFolder(Folder + "/Dialogues");
        EnsureFolder("Assets/Scenes/Cycles/Cycle_Aurell");
        var lucian = Character("Lucian");
        var aurell = Character("Aurell");
        string[] introTexts = {
            "AttendsÃ¢â‚¬Â¦ OÃƒÂ¹ sont les autres ? Ils ÃƒÂ©taient juste derriÃƒÂ¨re moi.",
            "Non, je ne les vois pas non plusÃ¢â‚¬Â¦",
            "Tu crois quÃ¢â‚¬â„¢ils sont dÃƒÂ©jÃƒÂ  entrÃƒÂ©s ? Sans nous attendre ?",
            "DÃ¢â‚¬â„¢accordÃ¢â‚¬Â¦ On avance. Mais on les retrouve."
        };
        float[] durations = { 5, 4, 5, 4 };
        var definition = ScriptableObject.CreateInstance<CycleDefinition>();
        definition.cycleId = CompanionReunionState.CycleId;
        definition.title = "Une ÃƒÂ©ternitÃƒÂ© dÃ¢â‚¬â„¢avance";
        definition.description = "Lucian dÃƒÂ©couvre que ses compagnons ne lÃ¢â‚¬â„¢ont pas suivi et retrouve Aurell blessÃƒÂ© ÃƒÂ  Moonrise.";
        definition.category = CycleCategory.Main;
        definition.audience = CycleAudience.SoloOnly;
        definition.enemyDefeatedFlags = 0;
        definition.cinematicCompletedFlags = 0;
        definition.cycleSceneName = Path.GetFileNameWithoutExtension(ScenePath);
        definition.completionUnloadDelay = 3;
        definition.steps = new CycleStep[6];
        definition.steps[0] = new CycleStep { id = "arrival", sourceId = "arrival", title = "Le groupe sÃ¢â‚¬â„¢est sÃƒÂ©parÃƒÂ©", kind = CycleStepKind.ZoneEntered };
        for (int i = 0; i < 4; i++)
            definition.steps[i + 1] = new CycleStep { id = "absence_" + (i + 1), sourceId = "absence_" + (i + 1),
                title = "LÃ¢â‚¬â„¢inquiÃƒÂ©tude de Lucian " + (i + 1), kind = CycleStepKind.NamedFact,
                prerequisites = Requires(i == 0 ? "arrival" : "absence_" + i) };
        definition.steps[5] = new CycleStep { id = CompanionReunionState.ReunionStep, sourceId = CompanionReunionState.ReunionStep,
            title = "Retrouver Aurell", kind = CycleStepKind.SequenceCompleted, terminal = true, prerequisites = Requires("absence_4"),
            rewards = new[] { new CycleReward { kind = CycleRewardKind.RecruitCompanion, companion = CombatSkillKit.Aurell } } };
        AssetDatabase.CreateAsset(definition, DefinitionPath);

        var sequence = ScriptableObject.CreateInstance<StorySequenceAsset>();
        sequence.sequenceId = "district1.aurell.reunion";
        sequence.displayName = "Une ÃƒÂ©ternitÃƒÂ© dÃ¢â‚¬â„¢avance";
        // The cycle step is the source of truth; an aborted conversation always remains retryable.
        sequence.playOnce = false;
        sequence.startFromBlack = sequence.fadeToBlackBeforeGameplay = sequence.fadeFromBlackAfterGameplayRestore = false;
        var camera = ScriptableObject.CreateInstance<StorySequenceCameraProfile>();
        camera.localCameraOffset = new Vector3(.65f, 1.05f, 2.3f);
        camera.useConversationAxis = true;
        camera.transitionDuration = .2f;
        camera.fieldOfView = 45;
        AssetDatabase.CreateAsset(camera, Folder + "/ReunionCamera.asset");
        var reverseCamera = Object.Instantiate(camera);
        reverseCamera.localCameraOffset = new Vector3(-.65f, 1.6f, 2.3f);
        AssetDatabase.CreateAsset(reverseCamera, Folder + "/LucianReunionCamera.asset");
        var lines = new (string actor, string text)[] {
            ("Lucian", "Aurell ! QuÃ¢â‚¬â„¢est-ce qui tÃ¢â‚¬â„¢est arrivÃƒÂ© ?"),
            ("Aurell", "LucianÃ¢â‚¬Â¦ CÃ¢â‚¬â„¢est vraiment toi ?"),
            ("Lucian", "On vient ÃƒÂ  peine de quitter la Maison. Je pensais que tu nous suivais."),
            ("Aurell", "Ãƒâ‚¬ peineÃ¢â‚¬Â¦ ? JÃ¢â‚¬â„¢ai lÃ¢â‚¬â„¢impression dÃ¢â‚¬â„¢ÃƒÂªtre ici depuis une ÃƒÂ©ternitÃƒÂ©."),
            ("Lucian", "Tu es blessÃƒÂ©. Retourne ÃƒÂ  la Maison. Je vais chercher les autres."),
            ("Aurell", "DÃ¢â‚¬â„¢accordÃ¢â‚¬Â¦ Je vais essayer de rentrer. Tu pourras compter sur moi.")
        };
        for (int i = 0; i < lines.Length; i++)
        {
            var voice = ScriptableObject.CreateInstance<VoiceLineData>();
            voice.voiceLineText = lines[i].text;
            AssetDatabase.CreateAsset(voice, Folder + "/Dialogues/Reunion_" + (i + 1).ToString("00") + ".asset");
            sequence.steps.Add(new StorySequenceStep { type = StorySequenceStepType.Dialogue, actorId = lines[i].actor,
                listenerId = lines[i].actor == "Lucian" ? "Aurell" : "Lucian", voiceLine = voice,
                speakerCharacterData = lines[i].actor == "Lucian" ? lucian : aurell,
                dialogueCameraProfile = lines[i].actor == "Lucian" ? reverseCamera : camera, skippable = true });
        }
        AssetDatabase.CreateAsset(sequence, Folder + "/AurellReunionDialogue.asset");
        var controller = AnimatorController.CreateAnimatorControllerAtPath(Folder + "/AurellInjured.controller");
        var state = controller.layers[0].stateMachine.AddState("Injured_Sitting");
        state.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Autres/Interaction/Lucian/Lucian_Sitting_Idle_ClosedMouth.anim");
        if (state.motion == null) throw new InvalidOperationException("Sitting clip missing.");
        controller.layers[0].stateMachine.defaultState = state;

        Scene previousActive = SceneManager.GetActiveScene();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            var template = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Narrative/StorySequence/Prefabs/StorySequenceRig.prefab");
            var root = (GameObject)PrefabUtility.InstantiatePrefab(template, scene);
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            root.name = "Cycle_Aurell_UneEterniteDAvance";
            var cycle = root.AddComponent<CycleController>();
            cycle.definition = definition;
            var adapter = root.AddComponent<AurellReunionCycle>();
            adapter.cycle = cycle;
            adapter.reunion = root.GetComponent<StorySequenceRunner>();
            adapter.bindings = root.GetComponent<StorySequenceSceneBindings>();
            Set(adapter.reunion, "sequence", sequence);
            var runnerProperties = new SerializedObject(adapter.reunion);
            runnerProperties.FindProperty("playOnStart").boolValue = false;
            runnerProperties.ApplyModifiedPropertiesWithoutUndo();
            var anchor = new GameObject("Aurell_MeetingAnchor_PLACE_ME");
            SceneManager.MoveGameObjectToScene(anchor, scene);
            anchor.transform.SetParent(root.transform, false);
            var collider = anchor.AddComponent<SphereCollider>();
            collider.radius = .6f;
            collider.center = new Vector3(0, .7f, 0);
            collider.isTrigger = true;
            adapter.meeting = anchor.AddComponent<AurellReunionAnchor>();
            adapter.meeting.owner = adapter;
            var prompt = anchor.AddComponent<WorldInteractionPrompt>();
            prompt.target = adapter.meeting;
            prompt.label = "Parler ÃƒÂ  Aurell";
            prompt.worldOffset = new Vector3(0, 1.4f, 0);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Narrative/StorySequence/MaisonReunion/Presentations/Aurell_Maison_Presentation.prefab"), scene);
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            model.name = "Aurell_Injured_Presentation";
            var animator = model.GetComponentInChildren<Animator>(true);
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (renderer.name == "CC_Base_Body" || renderer.name == "Pants" || renderer.name == "Dress" || renderer.name == "cape")
                    renderer.gameObject.AddComponent<RuntimeOutlineTarget>();
            PrefabUtility.SaveAsPrefabAsset(model, Folder + "/Aurell_Injured_Presentation.prefab");
            model.transform.SetParent(anchor.transform, false);
            adapter.presentation = model;
            adapter.aurell = model.GetComponent<StorySequenceActor>();
            adapter.introduction = introTexts.Select((text, i) => new CycleAutoDialogueBinding {
                id = "absence_" + (i + 1), line = text, durationSeconds = durations[i], speakerCharacterData = lucian,
                blocksGameplayInput = false }).ToArray();
            // Unity's event helper targets the real serialized UnityEvent instance.
            var completion = (UnityEngine.Events.UnityEvent)typeof(StorySequenceRunner).GetField("onSequenceCompleted",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(adapter.reunion);
            UnityEventTools.AddPersistentListener(completion, adapter.CompleteReunion);
            EditorUtility.SetDirty(adapter.reunion);
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Could not save Aurell scene.");
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
            if (previousActive.IsValid() && previousActive.isLoaded) SceneManager.SetActiveScene(previousActive);
        }
        var catalog = AssetDatabase.LoadAssetAtPath<CycleCatalog>(catalogPath);
        catalog.definitions = catalog.definitions.Concat(new[] { definition }).ToArray();
        EditorUtility.SetDirty(catalog);
        var manifest = AssetDatabase.LoadAssetAtPath<ZoneManifest>(manifestPath);
        manifest.loadingScenes.Add(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath));
        // The runtime names are serialized separately from the editor scene references.
        var manifestProperties = new SerializedObject(manifest);
        var names = manifestProperties.FindProperty("loadingSceneNames");
        int last = names.arraySize++;
        names.GetArrayElementAtIndex(last).stringValue = definition.cycleSceneName;
        manifestProperties.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(manifest);
        EditorBuildSettings.scenes = EditorBuildSettings.scenes.Concat(new[] { new EditorBuildSettingsScene(ScenePath, true) }).ToArray();
        AssetDatabase.SaveAssetIfDirty(catalog);
        AssetDatabase.SaveAssetIfDirty(manifest);
        foreach (var guid in AssetDatabase.FindAssets("", new[] { Folder }))
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null) AssetDatabase.SaveAssetIfDirty(asset);
        }
    }
    private static CharacterData Character(string name) => AssetDatabase.LoadAssetAtPath<CharacterData>($"Assets/Characters/1_Squad/{name}/{name}.asset");
    private static CycleRequirements Requires(string step) => new CycleRequirements { conditions = new[] { new CycleRequirement { stepId = step } } };
    private static void Set(Object target, string field, Object value)
    {
        var properties = new SerializedObject(target);
        properties.FindProperty(field).objectReferenceValue = value;
        properties.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
        AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
    }
}
