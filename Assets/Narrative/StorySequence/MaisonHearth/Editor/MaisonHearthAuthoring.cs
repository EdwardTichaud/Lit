using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Lit.Story;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class MaisonHearthAuthoring
{
    public const string Folder = "Assets/Narrative/StorySequence/MaisonHearth";
    public const string SequencePath = Folder + "/Maison_Hearth_Vigil.asset";
    public const string ScenePath = "Assets/Scenes/Maison/Maison_Sequences.unity";
    private const string Output = "Library/MaisonHearthVigil";
    static MaisonHearthAuthoring() => EditorApplication.update += Poll;
    private static void Poll()
    {
        if (!EditorApplication.isCompiling && !EditorApplication.isUpdating && !EditorApplication.isPlayingOrWillChangePlaymode &&
            File.Exists(Output + "/RepairSpawnBinding.request"))
        {
            File.Delete(Output + "/RepairSpawnBinding.request");
            try { RepairOpenHearthSpawnBindings(); }
            catch (Exception exception) { File.WriteAllText(Output + "/RepairSpawnBinding.result", "FAIL: " + exception); }
            return;
        }
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode ||
            AssetDatabase.IsAssetImportWorkerProcess() || !File.Exists(Output + "/Install.request")) return;
        File.Delete(Output + "/Install.request");
        try { Install(); File.WriteAllText(Output + "/Install.result", "PASS: hearth vigil installed in Maison_Sequences."); }
        catch (Exception exception) { File.WriteAllText(Output + "/Install.result", "FAIL: " + exception); Debug.LogException(exception); }
    }
    [MenuItem("Lit/Narrative/Maison/Repair Open Hearth Portal Binding")]
    public static void RepairOpenHearthSpawnBindings()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before repairing scene bindings.");
        var report = new System.Text.StringBuilder();
        // Scene import may have preceded compilation of the new serialized spawn binding type.
        AssetDatabase.ImportAsset(ScenePath, ImportAssetOptions.ForceUpdate);
        report.AppendLine("Forced scene reimport with the compiled spawn binding schema.");
        foreach (var vigil in Object.FindObjectsByType<MaisonHearthVigil>(FindObjectsInactive.Include))
        {
            if (!vigil.gameObject.scene.IsValid() || EditorSceneManager.IsPreviewScene(vigil.gameObject.scene) || vigil.bindings == null) continue;
            var binding = vigil.bindings.ResolveSpawnPoint(MaisonHearthVigil.PortalSpawnPointId);
            report.AppendLine(vigil.gameObject.scene.path + ": before=" + (binding?.point != null ? "assigned" : "missing"));
            if (binding?.point != null) continue;
            var anchor = vigil.transform.Find("Hearth_Portal_Appearance");
            if (anchor == null) throw new InvalidOperationException("Hearth_Portal_Appearance anchor is missing in " + vigil.gameObject.scene.path);
            var list = (List<StorySequenceSpawnBinding>)typeof(StorySequenceSceneBindings)
                .GetField("spawnPoints", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(vigil.bindings);
            Undo.RecordObject(vigil.bindings, "Repair hearth portal binding");
            if (binding == null)
            {
                binding = new StorySequenceSpawnBinding { spawnPointId = MaisonHearthVigil.PortalSpawnPointId };
                list.Add(binding);
            }
            binding.point = anchor;
            bool hasCallback = Enumerable.Range(0, binding.onSpawned.GetPersistentEventCount()).Any(index =>
                binding.onSpawned.GetPersistentTarget(index) == vigil && binding.onSpawned.GetPersistentMethodName(index) == nameof(vigil.OnPortalSpawned));
            if (!hasCallback) UnityEventTools.AddPersistentListener(binding.onSpawned, vigil.OnPortalSpawned);
            EditorUtility.SetDirty(vigil.bindings);
            EditorSceneManager.MarkSceneDirty(vigil.gameObject.scene);
            report.AppendLine("Repaired Transform and dynamic OnPortalSpawned callback; no scene saved.");
        }
        Directory.CreateDirectory(Output);
        File.WriteAllText(Output + "/RepairSpawnBinding.result", report.ToString());
    }
    [MenuItem("Lit/Narrative/Maison/Install Hearth Vigil")]
    public static void Install()
    {
        if (File.Exists(SequencePath)) throw new InvalidOperationException("Hearth content already installed; refusing to overwrite authoring.");
        var scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (!opened && scene.isDirty) throw new InvalidOperationException("Save Maison_Sequences explicitly before installing.");
        Directory.CreateDirectory(Output);
        File.Copy(ScenePath, Output + "/Maison_Sequences.unity.native-before", true);
        EnsureFolder(Folder + "/Dialogues");
        var sequence = ScriptableObject.CreateInstance<StorySequenceAsset>();
        sequence.sequenceId = "maison.hearth.first_vigil";
        sequence.displayName = "Ceux qui r\u00e9pondent";
        sequence.description = "Une veill\u00e9e entre de vieux amis qui portent des mondes. La lumi\u00e8re de Lucian vacille ; l'appel reprend.";
        sequence.playOnce = true;
        sequence.startFromBlack = sequence.fadeToBlackBeforeGameplay = sequence.fadeFromBlackAfterGameplayRestore = false;
        var close = Profile("Seated", new Vector3(.7f, 1.15f, 2.3f), 43);
        var reverse = Profile("Lucian_Seated", new Vector3(-.7f, 1.15f, 2.3f), 43);
        var final = Profile("Lucian_Wavering", new Vector3(-.38f, 1.1f, 1.9f), 38);
        final.transitionDuration = 2.5f;
        final.shotTravelLocalOffset = new Vector3(0, 0, .08f);
        final.shotTravelDuration = 6;
        var controller = AnimatorController.CreateAnimatorControllerAtPath(Folder + "/Hearth_Seated.controller");
        var state = controller.layers[0].stateMachine.AddState("Sitting_Idle");
        state.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Autres/Interaction/Lucian/Lucian_Sitting_Idle_ClosedMouth.anim");
        if (state.motion == null) throw new InvalidOperationException("Sitting clip missing.");
        controller.layers[0].stateMachine.defaultState = state;
        void Event(string id) => sequence.steps.Add(new StorySequenceStep { type = StorySequenceStepType.SceneEvent, eventId = id, label = id, skippable = false });
        void Wait(float seconds) => sequence.steps.Add(new StorySequenceStep { type = StorySequenceStepType.Wait, duration = seconds, skippable = false });
        void Shot(string id, float hold, float transition = .3f) => sequence.steps.Add(new StorySequenceStep { type = StorySequenceStepType.CameraShot,
            cameraPointId = id, cameraHoldDuration = hold, cameraTransitionDuration = transition, skippable = false });
        void Fade(float alpha, float duration) => sequence.steps.Add(new StorySequenceStep { type = StorySequenceStepType.Fade, fadeAlpha = alpha, fadeDuration = duration, skippable = false });
        Shot("hearth.wide", .2f);
        Event("hearth.seat_lucian"); Wait(2.1f);
        Fade(1, .65f);
        Event("hearth.gather"); Wait(.2f);
        Shot("hearth.wide", 0, 0); Fade(0, .85f); Wait(.45f);
        var lines = new (string speaker, string text)[] {
            ("Soren", "Tu t'assieds toujours le dos \u00e0 la porte."),
            ("Lucian", "Et toi, tu la regardes toujours comme si elle allait te r\u00e9pondre."),
            ("Aurell", "Apr\u00e8s ce qui en est sorti la derni\u00e8re fois\u2026"),
            ("Vesta", "Je vous avais dit de ne pas frapper."),
            ("Lucian", "Vous \u00e9tiez o\u00f9, cette fois ?"),
            ("Vesta", "Dans une ville b\u00e2tie sur le dos d'un g\u00e9ant. Il avait cess\u00e9 de r\u00eaver. Les maisons tombaient une \u00e0 une."),
            ("Soren", "Tu l'as r\u00e9veill\u00e9 ?"),
            ("Vesta", "Je lui ai rappel\u00e9 le nom de sa fille. \u00c7a a suffi."),
            ("Aurell", "J'ai tenu un passage. Trois jours pour eux. Un hiver, peut-\u00eatre, pour moi."),
            ("Lucian", "Et tu dis \u00e7a comme si tu avais attendu sous la pluie."),
            ("Aurell", "Ils sont tous pass\u00e9s. Le reste\u2026"),
            ("Soren", "Deux royaumes avaient effac\u00e9 leur fronti\u00e8re. Plus personne ne se souvenait que c'\u00e9tait une guerre."),
            ("Vesta", "Tu leur as rendu la m\u00e9moire ?"),
            ("Soren", "Seulement ce qu'ils \u00e9taient pr\u00eats \u00e0 porter."),
            ("Lucian", "J'ai suivi un dernier appel. Quand j'ai regard\u00e9 derri\u00e8re moi, le ciel s'\u00e9tait referm\u00e9."),
            ("Aurell", "Tu es revenu. C'est ce qui compte."),
            ("Vesta", "Pas tout \u00e0 fait pareil. Tu ris moins."),
            ("Lucian", "Vous avez compt\u00e9 ?"),
            ("Soren", "On te conna\u00eet depuis assez longtemps pour ne plus avoir besoin de compter."),
            ("Vesta", "Des chemins diff\u00e9rents, toujours. Et pourtant, nous revoil\u00e0 autour du m\u00eame feu."),
            ("Lucian", "Parce que quand l'un de nous appelle\u2026"),
            ("Aurell", "\u2026les autres r\u00e9pondent."),
            ("Aurell", "On peut rester ce soir. Les mondes attendront."),
            ("Lucian", "Ce n'est pas eux qui m'inqui\u00e8tent."),
            ("Vesta", "Alors dis-le."),
            ("Lucian", "Je sens que ma lumi\u00e8re vacille.")
        };
        for (int i = 0; i < lines.Length; i++)
        {
            AddLine(sequence, i + 1, lines[i].speaker, lines[i].text, i == lines.Length - 1 ? final : null);
        }
        Wait(1.1f);
        sequence.steps.Add(new StorySequenceStep { type = StorySequenceStepType.InstantiateGameObject, label = "Create departure portal",
            spawnId = MaisonHearthVigil.PortalSpawnId, spawnPointId = MaisonHearthVigil.PortalSpawnPointId,
            spawnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Hearth_DeparturePortal.prefab"),
            keepSpawnAfterCompletion = true, skippable = false });
        Shot("hearth.portal", 1.25f, 3f);
        AddLine(sequence, 27, "Soren", "C'est l'heure.", null);
        Wait(.5f);
        AssetDatabase.CreateAsset(sequence, SequencePath);
        var previousActive = SceneManager.GetActiveScene();
        if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(scene);
            var template = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Narrative/StorySequence/Prefabs/StorySequenceRig.prefab");
            var root = (GameObject)PrefabUtility.InstantiatePrefab(template, scene);
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            root.name = "Maison_Hearth_FirstVigil";
            var vigil = root.AddComponent<MaisonHearthVigil>();
            vigil.runner = root.GetComponent<StorySequenceRunner>();
            vigil.bindings = root.GetComponent<StorySequenceSceneBindings>();
            vigil.prerequisite = AssetDatabase.LoadAssetAtPath<StorySequenceAsset>(MaisonCompanionReunionAuthoring.SequencePath);
            vigil.seatedController = controller;
            vigil.prompt = root.AddComponent<WorldInteractionPrompt>();
            vigil.prompt.worldOffset = new Vector3(0, 1.5f, 0);
            var runner = new SerializedObject(vigil.runner);
            runner.FindProperty("sequence").objectReferenceValue = sequence;
            runner.FindProperty("playOnStart").boolValue = false;
            // The lower room around the hearth has authored floor markers; validate them before shipping.
            var seatPositions = new[] { new Vector3(-.8f, -.579f, 2.6f), new Vector3(1.15f, -.502f, 3.9f), new Vector3(-1.6f, -.466f, 3.8f), new Vector3(.35f, -.706f, 2.1f) };
            Vector3 fire = new Vector3(-.23f, -.6f, 4.7f);
            var seatNames = new[] { "Lucian", "Vesta", "Soren", "Aurell" };
            var anchors = new Transform[4];
            for (int i = 0; i < anchors.Length; i++)
            {
                var point = new GameObject(seatNames[i] + "_HearthSeat").transform;
                point.SetParent(root.transform, false);
                point.position = seatPositions[i];
                point.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(fire - point.position, Vector3.up));
                anchors[i] = point;
            }
            runner.FindProperty("playerTransform").objectReferenceValue = anchors[0];
            runner.FindProperty("playerApproachTimeout").floatValue = 15;
            runner.ApplyModifiedPropertiesWithoutUndo();
            vigil.companionSeats = Enumerable.Range(1, 3).Select(i => new MaisonHearthVigil.Seat { actorId = seatNames[i], point = anchors[i] }).ToArray();
            var portalPoint = new GameObject("Hearth_Portal_Appearance").transform;
            portalPoint.SetParent(root.transform, false);
            portalPoint.position = new Vector3(3f, -.95f, 1.5f);
            portalPoint.rotation = Quaternion.LookRotation(new Vector3(-1, 0, -.3f));
            var spawnBinding = new StorySequenceSpawnBinding { spawnPointId = MaisonHearthVigil.PortalSpawnPointId, point = portalPoint };
            UnityEventTools.AddPersistentListener(spawnBinding.onSpawned, vigil.OnPortalSpawned);
            typeof(StorySequenceSceneBindings).GetField("spawnPoints", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(vigil.bindings, new List<StorySequenceSpawnBinding> { spawnBinding });
            CreateCamera(root.transform, "hearth.wide", new Vector3(.2f, 3.4f, 1.1f), new Vector3(-.2f, .15f, 3.15f), 55);
            CreateCamera(root.transform, "hearth.portal", new Vector3(-.2f, 1.4f, .4f), new Vector3(3f, .4f, 1.5f), 46);
            var bindingProperties = new SerializedObject(vigil.bindings);
            var cameras = bindingProperties.FindProperty("cameraPoints");
            var points = root.GetComponentsInChildren<StorySequenceCameraPoint>();
            cameras.arraySize = points.Length;
            for (int i = 0; i < points.Length; i++) cameras.GetArrayElementAtIndex(i).objectReferenceValue = points[i];
            var events = new[] { ("hearth.seat_lucian", (UnityAction)vigil.SeatLucian), ("hearth.gather", (UnityAction)vigil.GatherAtHearth) };
            var eventBindings = new List<StorySequenceEventBinding>();
            foreach (var entry in events) { var binding = new StorySequenceEventBinding { eventId = entry.Item1, callback = new UnityEvent() }; UnityEventTools.AddPersistentListener(binding.callback, entry.Item2); eventBindings.Add(binding); }
            // Serialized lists are filled through their actual typed fields, preserving persistent event arguments.
            bindingProperties.ApplyModifiedPropertiesWithoutUndo();
            typeof(StorySequenceSceneBindings).GetField("events", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(vigil.bindings, eventBindings);
            AddCallback(vigil.runner, "onSequenceCompleted", vigil.CompleteVigil);
            AddCallback(vigil.runner, "onSequenceAborted", vigil.AbortVigil);
            EditorUtility.SetDirty(vigil.bindings); EditorUtility.SetDirty(vigil.runner);
            if (!sequence.TryValidate(out string issue)) throw new InvalidOperationException(issue);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save Maison_Sequences.");
        }
        finally
        {
            if (previousActive.IsValid() && previousActive.isLoaded) SceneManager.SetActiveScene(previousActive);
            if (opened) EditorSceneManager.CloseScene(scene, true);
        }
        foreach (var guid in AssetDatabase.FindAssets("", new[] { Folder }))
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null) AssetDatabase.SaveAssetIfDirty(asset);
        }
    }
    private static void AddCallback(StorySequenceRunner runner, string field, UnityAction callback)
    {
        var unityEvent = (UnityEvent)typeof(StorySequenceRunner).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(runner);
        UnityEventTools.AddPersistentListener(unityEvent, callback);
    }
    private static StorySequenceCameraProfile Profile(string name, Vector3 offset, float fov)
    {
        var profile = ScriptableObject.CreateInstance<StorySequenceCameraProfile>();
        profile.localCameraOffset = offset; profile.fieldOfView = fov; profile.transitionDuration = .3f; profile.useConversationAxis = true;
        AssetDatabase.CreateAsset(profile, Folder + "/" + name + ".asset"); return profile;
    }
    private static void AddLine(StorySequenceAsset sequence, int index, string actor, string text, StorySequenceCameraProfile profile)
    {
        var line = ScriptableObject.CreateInstance<VoiceLineData>(); line.voiceLineText = text;
        AssetDatabase.CreateAsset(line, Folder + "/Dialogues/Hearth_" + index.ToString("00") + "_" + actor + ".asset");
        sequence.steps.Add(new StorySequenceStep { label = "Hearth " + index + " " + actor, type = StorySequenceStepType.Dialogue, actorId = actor,
            listenerId = actor == "Lucian" ? "Vesta" : "Lucian", voiceLine = line, speakerNameOverride = actor,
            speakerCharacterData = AssetDatabase.LoadAssetAtPath<CharacterData>($"Assets/Characters/1_Squad/{actor}/{actor}.asset"),
            focusCameraOnSpeaker = profile != null, dialogueCameraProfile = profile,
            cameraTransitionDuration = profile != null ? profile.transitionDuration : .3f, skippable = true });
    }
    private static void CreateCamera(Transform parent, string id, Vector3 position, Vector3 target, float fov)
    {
        var point = new GameObject(id).AddComponent<StorySequenceCameraPoint>(); point.transform.SetParent(parent, false);
        point.transform.position = position; point.transform.rotation = Quaternion.LookRotation(target - position);
        var properties = new SerializedObject(point); properties.FindProperty("pointId").stringValue = id;
        properties.FindProperty("useAuthoredRotation").boolValue = true; properties.FindProperty("fieldOfView").floatValue = fov;
        if (id == "hearth.wide")
        {
            properties.FindProperty("lookTarget").objectReferenceValue = parent;
            properties.FindProperty("lookAtOffset").vector3Value = target - parent.position;
            properties.FindProperty("useAuthoredRotation").boolValue = false;
            properties.FindProperty("orbitArcDegrees").floatValue = 18;
            properties.FindProperty("orbitPeriod").floatValue = 180;
        }
        properties.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
        AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
    }
}
