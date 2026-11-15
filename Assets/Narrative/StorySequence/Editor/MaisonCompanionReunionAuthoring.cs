using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lit.Story;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// One-off, explicitly requested content installation. Never runs on Play or without its request file.
[InitializeOnLoad]
public static class MaisonCompanionReunionAuthoring
{
    public const string Folder = "Assets/Narrative/StorySequence/MaisonReunion";
    public const string ScenePath = "Assets/Scenes/Maison/Maison_Sequences.unity";
    public const string SequencePath = Folder + "/Maison_Companions_Reunion.asset";
    private const string Request = "Library/MaisonCompanionReunion/Install.request";
    static MaisonCompanionReunionAuthoring() => EditorApplication.update += Poll;

    private static void Poll()
    {
        if (AssetDatabase.IsAssetImportWorkerProcess() || EditorApplication.isCompiling ||
            EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Request)) return;
        File.Delete(Request);
        try
        {
            Install();
            File.WriteAllText("Library/MaisonCompanionReunion/Install.result", "PASS: content installed in Maison_Sequences.");
        }
        catch (Exception exception)
        {
            File.WriteAllText("Library/MaisonCompanionReunion/Install.result", "FAIL: " + exception);
            Debug.LogException(exception);
        }
    }

    private static void Install()
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (!opened && scene.isDirty) throw new InvalidOperationException("Maison_Sequences is dirty; save it before installation.");
        if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            if (scene.GetRootGameObjects().Any(root => root.GetComponent<MaisonCompanionReunion>() != null))
                throw new InvalidOperationException("Reunion already installed; refusing to overwrite authored content.");
            if (AssetDatabase.IsValidFolder(Folder))
                throw new InvalidOperationException("Content folder already exists; refusing to overwrite it.");
            AssetDatabase.CreateFolder("Assets/Narrative/StorySequence", "MaisonReunion");
            AssetDatabase.CreateFolder(Folder, "Presentations");
            AssetDatabase.CreateFolder(Folder, "Dialogues");
            string[] names = { "Vesta", "Soren", "Aurell" };
            var prefabs = new StorySequenceActor[3];
            for (int i = 0; i < names.Length; i++) prefabs[i] = CreatePresentation(names[i]);
            var sequence = CreateSequence();
            var template = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Narrative/StorySequence/Prefabs/StorySequenceRig.prefab");
            if (template == null) throw new InvalidOperationException("StorySequenceRig missing.");
            var root = (GameObject)PrefabUtility.InstantiatePrefab(template, scene);
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            root.name = "Maison_Companions_Reunion";
            var runner = root.GetComponent<StorySequenceRunner>();
            var bindings = root.GetComponent<StorySequenceSceneBindings>();
            SetObject(runner, "sequence", sequence);
            var runnerProperties = new SerializedObject(runner);
            runnerProperties.FindProperty("playOnStart").boolValue = false;
            runnerProperties.ApplyModifiedPropertiesWithoutUndo();
            var reunion = root.AddComponent<MaisonCompanionReunion>();
            var properties = new SerializedObject(reunion);
            properties.FindProperty("runner").objectReferenceValue = runner;
            properties.FindProperty("bindings").objectReferenceValue = bindings;
            var entries = properties.FindProperty("companions");
            entries.arraySize = names.Length;
            for (int i = 0; i < names.Length; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("actorId").stringValue = names[i];
                entry.FindPropertyRelative("presentationPrefab").objectReferenceValue = prefabs[i];
                entry.FindPropertyRelative("waitingPointIndex").intValue = i + 2;
            }
            properties.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save Maison_Sequences.");
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }

    private static StorySequenceActor CreatePresentation(string characterName)
    {
        var data = AssetDatabase.LoadAssetAtPath<CharacterData>($"Assets/Characters/1_Squad/{characterName}/{characterName}.asset");
        if (data == null || data.worldPrefab == null) throw new InvalidOperationException("Missing character: " + characterName);
        // Copy only native visual components, never a live player, its UCC, network, AI, inputs or persistence.
        var mapping = new Dictionary<Transform, Transform>();
        Transform CopyHierarchy(Transform source, Transform parent)
        {
            var target = new GameObject(source.name).transform;
            target.SetParent(parent, false);
            target.localPosition = source.localPosition;
            target.localRotation = source.localRotation;
            target.localScale = source.localScale;
            target.gameObject.layer = source.gameObject.layer;
            target.gameObject.SetActive(source.gameObject.activeSelf);
            mapping.Add(source, target);
            foreach (Transform child in source) CopyHierarchy(child, target);
            return target;
        }
        var root = CopyHierarchy(data.worldPrefab.transform, null).gameObject;
        try
        {
            root.name = characterName + "_Maison_Presentation";
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            foreach (var pair in mapping)
            {
                var filter = pair.Key.GetComponent<MeshFilter>();
                if (filter != null) pair.Value.gameObject.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                foreach (var source in pair.Key.GetComponents<Renderer>())
                {
                    Renderer target;
                    if (source is SkinnedMeshRenderer skin)
                    {
                        var copy = pair.Value.gameObject.AddComponent<SkinnedMeshRenderer>();
                        copy.sharedMesh = skin.sharedMesh;
                        copy.rootBone = skin.rootBone != null && mapping.TryGetValue(skin.rootBone, out var bone) ? bone : null;
                        copy.bones = skin.bones.Select(b => b != null && mapping.TryGetValue(b, out var mapped) ? mapped : null).ToArray();
                        copy.localBounds = skin.localBounds;
                        copy.quality = skin.quality;
                        if (skin.sharedMesh != null)
                            for (int i = 0; i < skin.sharedMesh.blendShapeCount; i++) copy.SetBlendShapeWeight(i, skin.GetBlendShapeWeight(i));
                        target = copy;
                    }
                    else if (source is MeshRenderer) target = pair.Value.gameObject.AddComponent<MeshRenderer>();
                    else continue;
                    target.sharedMaterials = source.sharedMaterials;
                    target.enabled = source.enabled;
                    target.shadowCastingMode = source.shadowCastingMode;
                    target.receiveShadows = source.receiveShadows;
                }
                var animator = pair.Key.GetComponent<Animator>();
                if (animator != null)
                {
                    var copy = pair.Value.gameObject.AddComponent<Animator>();
                    copy.avatar = animator.avatar;
                    copy.runtimeAnimatorController = animator.runtimeAnimatorController;
                    copy.applyRootMotion = false;
                }
            }
            var actor = root.AddComponent<StorySequenceActor>();
            var properties = new SerializedObject(actor);
            properties.FindProperty("actorId").stringValue = characterName;
            properties.FindProperty("displayName").stringValue = characterName;
            properties.FindProperty("characterData").objectReferenceValue = data;
            properties.ApplyModifiedPropertiesWithoutUndo();
            return PrefabUtility.SaveAsPrefabAsset(root, Folder + "/Presentations/" + root.name + ".prefab").GetComponent<StorySequenceActor>();
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static StorySequenceAsset CreateSequence()
    {
        var sequence = ScriptableObject.CreateInstance<StorySequenceAsset>();
        sequence.sequenceId = "maison.companions.first_reunion";
        sequence.displayName = "Comme au bon vieux temps";
        sequence.description = "Retrouvailles de Lucian, Vesta, Soren et Aurell : une vieille amitie, quatre rencontres recentes, une attirance inexpliquee.";
        sequence.playOnce = true;
        sequence.startFromBlack = false;
        sequence.fadeToBlackBeforeGameplay = false;
        sequence.fadeFromBlackAfterGameplayRestore = false;
        var lines = new (string speaker, string text)[]
        {
            ("Vesta", "Te voilà enfin ! J'ai gardé ta place. Soren voulait déjà s'y étaler."),
            ("Soren", "La meilleure place pour le dernier arrivé. Comme d'habitude."),
            ("Lucian", "Tu me l'as quand même gardée."),
            ("Aurell", "Laisse-le souffler, Soren. Lucian ? Ça va ?"),
            ("Lucian", "J'ai rencontré mon ange gardien. [Munin]. Enfin… c'est lui qui m'a trouvé."),
            ("Vesta", "Attends, toi aussi ? La mienne est venue me voir il y a trois jours."),
            ("Soren", "Le mien, hier. J'ai cru qu'on avait avancé le départ."),
            ("Aurell", "Le mien m'a trouvé il y a deux nuits. J'allais vous en parler."),
            ("Lucian", "Et personne ne vous les a attribués ?"),
            ("Vesta", "Non. Elle dit que quelque chose l'attirait vers moi. Elle sait pas quoi."),
            ("Soren", "Le mien m'a sorti : « C'était toi. » Merci, ça m'aide."),
            ("Aurell", "Et moi qui pensais que vous sauriez m'expliquer le mien."),
            ("Lucian", "J'avais peur que vous me preniez pour un fou."),
            ("Soren", "Pour un fou, non. Pour un retardataire, le dossier est solide."),
            ("Vesta", "La prochaine fois, viens nous voir, d'accord ?"),
            ("Aurell", "Allez, assieds-toi. On va en parler."),
            ("Lucian", "Ça fait du bien de vous revoir."),
        };
        for (int i = 0; i < lines.Length; i++)
        {
            var line = ScriptableObject.CreateInstance<VoiceLineData>();
            line.voiceLineText = lines[i].text;
            string name = $"Reunion_{i + 1:00}_{lines[i].speaker}";
            AssetDatabase.CreateAsset(line, Folder + "/Dialogues/" + name + ".asset");
            sequence.steps.Add(new StorySequenceStep
            {
                label = name, type = StorySequenceStepType.Dialogue, voiceLine = line,
                actorId = lines[i].speaker, speakerNameOverride = lines[i].speaker,
                listenerId = lines[i].speaker == "Lucian" ? "Vesta" : "Lucian",
                skippable = true, dialogueMaxDisplayDuration = 0f, focusCameraOnSpeaker = true
            });
        }
        if (!sequence.TryValidate(out string issue)) throw new InvalidOperationException(issue);
        AssetDatabase.CreateAsset(sequence, SequencePath);
        return sequence;
    }

    private static void SetObject(Object target, string property, Object value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(property).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
