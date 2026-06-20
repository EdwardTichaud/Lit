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
                }
                else if (step.type == StorySequenceStepType.AnimatorTrigger &&
                         (string.IsNullOrWhiteSpace(step.actorId) ||
                          string.IsNullOrWhiteSpace(step.animatorTrigger)))
                {
                    errors.Add($"{prefix}: acteur ou trigger Animator manquant.");
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

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                StorySequenceRunner runner = (StorySequenceRunner)target;
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
}
#endif
