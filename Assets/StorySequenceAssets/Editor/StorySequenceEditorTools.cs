#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lit.Story.Editor
{
    public static class StorySequenceEditorTools
    {
        private const string Root = "Assets/StorySequenceAssets";
        private const string SequenceFolder = Root + "/Sequences";
        private const string ProfileFolder = Root + "/CameraProfiles";
        private const string PrefabFolder = Root + "/Prefabs";

        [MenuItem("Lit/Story Sequences/Create Runtime Rig In Scene", priority = 10)]
        public static void CreateRuntimeRig()
        {
            StorySequenceRunner existing = UnityEngine.Object.FindAnyObjectByType<StorySequenceRunner>(
                FindObjectsInactive.Include);
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                Debug.Log("StorySequence: le rig existe deja dans la scene.", existing);
                return;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                PrefabFolder + "/StorySequenceRig.prefab");
            GameObject rig = prefab != null
                ? PrefabUtility.InstantiatePrefab(prefab) as GameObject
                : new GameObject("StorySequenceRig");
            if (rig == null)
            {
                throw new InvalidOperationException("Impossible de creer StorySequenceRig.");
            }

            Undo.RegisterCreatedObjectUndo(rig, "Create Story Sequence Rig");
            if (prefab == null)
            {
                rig.AddComponent<StorySequenceSceneBindings>();
                rig.AddComponent<StorySequenceCameraDriver>();
                rig.AddComponent<StorySequenceDialoguePresenter>();
                rig.AddComponent<StorySequenceFadeController>();
                rig.AddComponent<StorySequenceRunner>();
            }
            Selection.activeGameObject = rig;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        [MenuItem("Lit/Story Sequences/Create Starter Assets", priority = 20)]
        public static void CreateStarterAssets()
        {
            EnsureFolder(SequenceFolder);
            EnsureFolder(ProfileFolder);
            EnsureFolder(PrefabFolder);

            StorySequenceCameraProfile closeUp = CreateOrLoadProfile(
                "Dialogue_CloseUp",
                new Vector3(0.85f, 1.65f, 2.4f),
                42f,
                false);
            StorySequenceCameraProfile twoShot = CreateOrLoadProfile(
                "Dialogue_TwoShot",
                new Vector3(1.9f, 1.85f, 3.8f),
                52f,
                true);
            CreateOrLoadProfile(
                "Dialogue_Wide",
                new Vector3(3.2f, 2.5f, 6.5f),
                60f,
                true);

            string sequencePath = SequenceFolder + "/Intro_Template.asset";
            StorySequenceAsset sequence = AssetDatabase.LoadAssetAtPath<StorySequenceAsset>(sequencePath);
            if (sequence == null)
            {
                sequence = ScriptableObject.CreateInstance<StorySequenceAsset>();
                sequence.sequenceId = "intro_template";
                sequence.displayName = "Intro Template";
                sequence.description = "Dupliquer cet asset pour creer une nouvelle sequence.";
                sequence.playOnce = false;
                sequence.steps = new List<StorySequenceStep>
                {
                    new StorySequenceStep
                    {
                        label = "Plan large d'ouverture",
                        type = StorySequenceStepType.CameraShot,
                        actorId = "lucian",
                        cameraProfile = twoShot,
                        cameraTransitionDuration = 0.8f,
                        cameraHoldDuration = 0.8f,
                        skippable = true
                    },
                    new StorySequenceStep
                    {
                        label = "Premiere replique",
                        type = StorySequenceStepType.Dialogue,
                        actorId = "lucian",
                        dialogueCameraProfile = closeUp,
                        cameraTransitionDuration = 0.55f,
                        skippable = true
                    }
                };
                AssetDatabase.CreateAsset(sequence, sequencePath);
            }

            CreateRuntimeRigPrefabIfMissing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = sequence;
            EditorGUIUtility.PingObject(sequence);
            Debug.Log("StorySequence: assets de depart crees dans Assets/StorySequenceAssets.");
        }

        [MenuItem("Lit/Story Sequences/Validate Open Scene", priority = 30)]
        public static void ValidateOpenScene()
        {
            List<string> errors = new List<string>();
            List<string> warnings = new List<string>();

            StorySequenceRunner[] runners = UnityEngine.Object.FindObjectsByType<StorySequenceRunner>(
                FindObjectsInactive.Include);
            StorySequenceActor[] actors = UnityEngine.Object.FindObjectsByType<StorySequenceActor>(
                FindObjectsInactive.Include);
            StorySequenceCameraPoint[] points = UnityEngine.Object.FindObjectsByType<StorySequenceCameraPoint>(
                FindObjectsInactive.Include);

            if (runners.Length == 0)
            {
                warnings.Add("Aucun StorySequenceRunner dans la scene.");
            }

            ValidateDuplicateIds(
                actors.Select(actor => actor != null ? actor.ActorId : null),
                "acteur",
                errors);
            ValidateDuplicateIds(
                points.Select(point => point != null ? point.PointId : null),
                "point camera",
                errors);

            for (int i = 0; i < runners.Length; i++)
            {
                ValidateRunner(runners[i], errors, warnings);
            }

            string sceneName = SceneManager.GetActiveScene().name;
            string summary =
                $"StorySequence validation '{sceneName}': errors={errors.Count}, warnings={warnings.Count}";
            for (int i = 0; i < errors.Count; i++)
            {
                Debug.LogError($"StorySequence: {errors[i]}");
            }

            for (int i = 0; i < warnings.Count; i++)
            {
                Debug.LogWarning($"StorySequence: {warnings[i]}");
            }

            if (errors.Count == 0)
            {
                Debug.Log(summary);
            }
            else
            {
                throw new InvalidOperationException(summary);
            }
        }

        [MenuItem("Lit/Story Sequences/Reset Selected Sequence Completion", priority = 40)]
        public static void ResetSelectedSequenceCompletion()
        {
            StorySequenceAsset sequence = Selection.activeObject as StorySequenceAsset;
            if (sequence == null)
            {
                EditorUtility.DisplayDialog(
                    "Reset Sequence Completion",
                    "Selectionnez un StorySequenceAsset dans le Project.",
                    "OK");
                return;
            }

            ResetCompletion(sequence);
        }

        [MenuItem("Lit/Story Sequences/Reset Selected Sequence Completion", true)]
        private static bool CanResetSelectedSequenceCompletion()
        {
            return Selection.activeObject is StorySequenceAsset;
        }

        [MenuItem("Lit/Story Sequences/Reset All Sequence Completions", priority = 41)]
        public static void ResetAllSequenceCompletions()
        {
            bool hasActiveSave = StorySequenceCompletionStore.HasActiveSave;
            string scope = hasActiveSave
                ? "la sauvegarde active"
                : "toutes les sauvegardes présentes sur cette machine";
            if (!EditorUtility.DisplayDialog(
                    "Reset All Sequence Completions",
                    $"Réinitialiser l'état Play Once de {scope} ?\n\n" +
                    "Les sauvegardes de partie ne seront pas supprimées.",
                    "Réinitialiser",
                    "Annuler"))
            {
                return;
            }

            int resetCount;
            if (hasActiveSave)
            {
                resetCount = StorySequenceCompletionStore.ResetAllForActiveSave();
            }
            else
            {
                resetCount = ResetSavedCompletionFiles(null);
            }

            Debug.Log(
                $"StorySequence: {resetCount} entrée(s) Play Once réinitialisée(s) dans {scope}.");
        }

        public static void ResetCompletion(StorySequenceAsset sequence)
        {
            if (sequence == null)
            {
                return;
            }

            string id = StorySequenceCompletionStore.ResolveSequenceId(sequence);
            if (StorySequenceCompletionStore.HasActiveSave)
            {
                StorySequenceCompletionStore.ResetCompletion(sequence);
                SaveSessionManager session = SaveSessionManager.Instance;
                Debug.Log(
                    $"StorySequence: '{id}' réinitialisée dans la sauvegarde active " +
                    $"'{session.CurrentSaveName}' ({session.CurrentSaveId}).",
                    sequence);
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "Reset Sequence Completion",
                    "Aucune sauvegarde n'est active dans l'éditeur.\n\n" +
                    $"Réinitialiser '{id}' dans tous les slots de sauvegarde ?",
                    "Réinitialiser partout",
                    "Annuler"))
            {
                return;
            }

            StorySequenceCompletionStore.ResetCompletion(sequence);
            int changedFiles = ResetSavedCompletionFiles(id);
            Debug.Log(
                $"StorySequence: '{id}' réinitialisée dans {changedFiles} slot(s) de sauvegarde.",
                sequence);
        }

        private static int ResetSavedCompletionFiles(string sequenceId)
        {
            string root = Path.Combine(
                Application.persistentDataPath,
                SaveSessionManager.DefaultSavesRootFolder);
            if (!Directory.Exists(root))
            {
                return 0;
            }

            string[] metaPaths = Directory.GetFiles(
                root,
                "meta.json",
                SearchOption.AllDirectories);
            int changedFiles = 0;
            for (int i = 0; i < metaPaths.Length; i++)
            {
                string path = metaPaths[i];
                try
                {
                    SaveMeta meta = JsonUtility.FromJson<SaveMeta>(File.ReadAllText(path));
                    if (meta == null ||
                        meta.completedStorySequenceIds == null ||
                        meta.completedStorySequenceIds.Count == 0)
                    {
                        continue;
                    }

                    int removed;
                    if (string.IsNullOrWhiteSpace(sequenceId))
                    {
                        removed = meta.completedStorySequenceIds.Count;
                        meta.completedStorySequenceIds.Clear();
                    }
                    else
                    {
                        removed = meta.completedStorySequenceIds.RemoveAll(
                            candidate => string.Equals(
                                candidate,
                                sequenceId,
                                StringComparison.OrdinalIgnoreCase));
                    }

                    if (removed <= 0)
                    {
                        continue;
                    }

                    File.WriteAllText(path, JsonUtility.ToJson(meta, true));
                    changedFiles++;
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        $"StorySequence: impossible de réinitialiser '{path}'. {exception.Message}");
                }
            }

            return changedFiles;
        }

        private static void ValidateRunner(
            StorySequenceRunner runner,
            List<string> errors,
            List<string> warnings)
        {
            if (runner == null)
            {
                return;
            }

            StorySequenceAsset sequence = runner.Sequence;
            if (sequence == null)
            {
                warnings.Add($"Runner '{runner.name}' sans sequence.");
                return;
            }

            if (string.IsNullOrWhiteSpace(sequence.sequenceId))
            {
                errors.Add($"Sequence '{sequence.name}' sans sequenceId.");
            }

            if (sequence.steps == null)
            {
                return;
            }

            for (int i = 0; i < sequence.steps.Count; i++)
            {
                StorySequenceStep step = sequence.steps[i];
                if (step == null)
                {
                    warnings.Add($"{sequence.name}: etape {i} nulle.");
                    continue;
                }

                string prefix = $"{sequence.name} etape {i} ({step.label})";
                if (step.type == StorySequenceStepType.Dialogue)
                {
                    if (string.IsNullOrWhiteSpace(step.actorId))
                    {
                        errors.Add($"{prefix}: actorId manquant.");
                    }

                    if (step.voiceLine == null)
                    {
                        warnings.Add($"{prefix}: VoiceLineData manquante.");
                    }

                    if (!step.skippable && step.dialogueMaxDisplayDuration <= 0f)
                    {
                        errors.Add(
                            $"{prefix}: dialogue non skippable sans duree maximale; l'etape ne peut pas se terminer.");
                    }
                }
                else if (step.type == StorySequenceStepType.AnimatorTrigger &&
                         (string.IsNullOrWhiteSpace(step.actorId) ||
                          string.IsNullOrWhiteSpace(step.animatorTrigger)))
                {
                    errors.Add($"{prefix}: acteur ou trigger Animator manquant.");
                }
                else if (step.type == StorySequenceStepType.Sitting &&
                         !step.applyToWholeSquad &&
                         string.IsNullOrWhiteSpace(step.actorId))
                {
                    errors.Add($"{prefix}: actorId manquant pour l'etat assis individuel.");
                }
                else if (step.type == StorySequenceStepType.Timeline && step.timeline == null)
                {
                    errors.Add($"{prefix}: Timeline manquante.");
                }
                else if (step.type == StorySequenceStepType.SceneEvent &&
                         string.IsNullOrWhiteSpace(step.eventId))
                {
                    errors.Add($"{prefix}: eventId manquant.");
                }
            }
        }

        private static void ValidateDuplicateIds(
            IEnumerable<string> ids,
            string label,
            List<string> errors)
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string rawId in ids)
            {
                if (string.IsNullOrWhiteSpace(rawId))
                {
                    continue;
                }

                string id = rawId.Trim();
                if (!seen.Add(id))
                {
                    errors.Add($"Identifiant {label} duplique: '{id}'.");
                }
            }
        }

        private static StorySequenceCameraProfile CreateOrLoadProfile(
            string name,
            Vector3 offset,
            float fov,
            bool frameBoth)
        {
            string path = $"{ProfileFolder}/{name}.asset";
            StorySequenceCameraProfile profile =
                AssetDatabase.LoadAssetAtPath<StorySequenceCameraProfile>(path);
            if (profile != null)
            {
                return profile;
            }

            profile = ScriptableObject.CreateInstance<StorySequenceCameraProfile>();
            profile.localCameraOffset = offset;
            profile.fieldOfView = fov;
            profile.frameSpeakerAndListener = frameBoth;
            AssetDatabase.CreateAsset(profile, path);
            return profile;
        }

        private static void CreateRuntimeRigPrefabIfMissing()
        {
            string path = PrefabFolder + "/StorySequenceRig.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                return;
            }

            GameObject rig = new GameObject("StorySequenceRig");
            rig.AddComponent<StorySequenceSceneBindings>();
            rig.AddComponent<StorySequenceCameraDriver>();
            rig.AddComponent<StorySequenceDialoguePresenter>();
            rig.AddComponent<StorySequenceFadeController>();
            rig.AddComponent<StorySequenceRunner>();
            PrefabUtility.SaveAsPrefabAsset(rig, path);
            UnityEngine.Object.DestroyImmediate(rig);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (!string.IsNullOrWhiteSpace(parent))
            {
                EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent, name);
            }
        }
    }

    [CustomEditor(typeof(StorySequenceRunner))]
    public sealed class StorySequenceRunnerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();

            if (GUILayout.Button("Validate Open Scene"))
            {
                StorySequenceEditorTools.ValidateOpenScene();
            }

            StorySequenceRunner runner = (StorySequenceRunner)target;
            StorySequenceAsset sequence = runner.Sequence;
            using (new EditorGUI.DisabledScope(sequence == null))
            {
                if (GUILayout.Button("Reset Sequence Completion"))
                {
                    StorySequenceEditorTools.ResetCompletion(sequence);
                }
            }

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (!runner.IsPlaying && GUILayout.Button("Play Sequence"))
                {
                    runner.Play();
                }
                else if (runner.IsPlaying && GUILayout.Button("Abort Sequence"))
                {
                    runner.Abort();
                }
            }
        }
    }

    [CustomEditor(typeof(StorySequenceAsset))]
    public sealed class StorySequenceAssetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();

            StorySequenceAsset sequence = (StorySequenceAsset)target;
            bool completed = StorySequenceCompletionStore.IsCompleted(sequence);
            string storage = StorySequenceCompletionStore.HasActiveSave
                ? $"Sauvegarde active : {SaveSessionManager.Instance.CurrentSaveName}"
                : "Aucune sauvegarde active : état transitoire de test";
            EditorGUILayout.HelpBox(
                $"Play Once : {(completed ? "déjà terminé" : "prêt à jouer")}\n" +
                $"ID : {StorySequenceCompletionStore.ResolveSequenceId(sequence)}\n" +
                storage,
                completed ? MessageType.Warning : MessageType.Info);

            if (GUILayout.Button("Reset Play Once Completion"))
            {
                StorySequenceEditorTools.ResetCompletion(sequence);
            }
        }
    }
}
#endif
