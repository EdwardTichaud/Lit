#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[CustomEditor(typeof(LightSkillTimelineAuthoringRig))]
public sealed class LightSkillTimelineAuthoringRigEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        LightSkillTimelineAuthoringRig rig = (LightSkillTimelineAuthoringRig)target;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Runtime Bake", EditorStyles.boldLabel);

        if (GUILayout.Button("Validate Runtime Contract"))
        {
            if (LightSkillRuntimeRigBaker.Validate(rig, out string report)) Debug.Log("[LightSkill Bake] " + report, rig);
            else EditorGUILayout.HelpBox(report, MessageType.Error);
        }

        if (GUILayout.Button("Bake LightSkill"))
        {
            if (LightSkillRuntimeRigBaker.Bake(rig, out CombatCinematicRig bakedRig, out string report))
            {
                Selection.activeObject = bakedRig;
                Debug.Log("[LightSkill Bake] " + report, bakedRig);
            }
            else EditorUtility.DisplayDialog("Bake LightSkill", report, "OK");
        }
    }
}

public static class LightSkillRuntimeRigBaker
{
    public static bool Validate(LightSkillTimelineAuthoringRig authoringRig, out string report)
    {
        List<string> issues = new List<string>();
        if (authoringRig == null) issues.Add("LightSkillTimelineAuthoringRig manquant.");
        if (authoringRig != null && authoringRig.LightSkill == null) issues.Add("LightSkillSO manquant.");
        if (authoringRig != null && authoringRig.Director == null) issues.Add("PlayableDirector manquant.");

        if (authoringRig != null && !authoringRig.ApplyPreviewBindings(out string bindingError)) issues.Add(bindingError);
        TimelineAsset timeline = authoringRig != null && authoringRig.LightSkill != null
            ? authoringRig.LightSkill.Timeline as TimelineAsset : null;
        if (timeline == null) issues.Add("TimelineAsset manquant.");
        if (timeline != null) issues.AddRange(LightSkillTimelineContract.GetIssues(timeline, authoringRig.LightSkill));

        if (authoringRig != null && timeline != null)
        {
            ValidateCameras(authoringRig, timeline, issues);
            ValidateExtraTrackBindings(authoringRig, timeline, issues);
            ValidateRuntimeExportDependencies(authoringRig, issues);
        }

        report = issues.Count == 0
            ? "Contrat valide : package runtime exportable."
            : string.Join("\n", issues);
        return issues.Count == 0;
    }

    public static bool Bake(LightSkillTimelineAuthoringRig authoringRig, out CombatCinematicRig bakedRig, out string report)
    {
        bakedRig = null;
        if (authoringRig == null || authoringRig.LightSkill == null)
        {
            report = "Rig d'auteur ou LightSkillSO manquant.";
            return false;
        }

        if (!EnsureCameraAuthoring(authoringRig, out string setupError) || !Validate(authoringRig, out report))
        {
            report = string.IsNullOrWhiteSpace(setupError) ? report : setupError;
            return false;
        }

        LightSkillSO skill = authoringRig.LightSkill;
        TimelineAsset sourceTimeline = skill.Timeline as TimelineAsset;
        string folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(skill))?.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(folder))
        {
            report = "Dossier du LightSkillSO introuvable.";
            return false;
        }

        string prefabPath = folder + "/" + skill.name + "_CinematicRig.prefab";
        string runtimeTimelinePath = folder + "/" + skill.name + "_Runtime.playable";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null &&
            !EditorUtility.DisplayDialog("Bake LightSkill", "Remplacer le package runtime existant ?\n" + prefabPath,
                "Remplacer", "Annuler"))
        {
            report = "Bake annule.";
            return false;
        }

        string temporaryTimelinePath = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + skill.name + "_Runtime_BakeTmp.playable");
        GameObject root = null;
        try
        {
            if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(sourceTimeline), temporaryTimelinePath))
            {
                report = "Impossible de cloner la Timeline runtime.";
                return false;
            }

            TimelineAsset runtimeTimeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(temporaryTimelinePath);
            root = new GameObject(skill.name + "_CinematicRig");
            PlayableDirector director = root.AddComponent<PlayableDirector>();
            director.playableAsset = runtimeTimeline;
            director.extrapolationMode = DirectorWrapMode.None;
            root.AddComponent<SignalReceiver>();
            root.AddComponent<LitTimelineCinemachineBridge>();
            root.AddComponent<LightSkillCinematicSequenceController>();
            CombatCinematicRig rig = root.AddComponent<CombatCinematicRig>();

            List<CombatCinematicCameraBinding> cameras = CopyReferencedCameras(authoringRig, runtimeTimeline, root.transform);
            Dictionary<LightSkillRuntimeExport, LightSkillRuntimeExport> exports = CopyRuntimeExports(authoringRig, root.transform);
            List<CombatCinematicTrackBinding> tracks = CopyExtraTrackBindings(authoringRig, sourceTimeline, exports);
            ApplyRigBindings(rig, cameras, tracks);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            if (prefab == null)
            {
                report = "Unity n'a pas pu enregistrer le prefab runtime.";
                return false;
            }

            if (AssetDatabase.LoadAssetAtPath<TimelineAsset>(runtimeTimelinePath) != null)
                AssetDatabase.DeleteAsset(runtimeTimelinePath);
            string moveError = AssetDatabase.MoveAsset(temporaryTimelinePath, runtimeTimelinePath);
            if (!string.IsNullOrEmpty(moveError))
            {
                report = "Prefab cree mais Timeline runtime non finalisee : " + moveError;
                return false;
            }

            TimelineAsset finalTimeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(runtimeTimelinePath);
            GameObject prefabContents = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                prefabContents.GetComponent<PlayableDirector>().playableAsset = finalTimeline;
                PrefabUtility.SaveAsPrefabAsset(prefabContents, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabContents);
            }

            if (HasAuthoringDependency(prefabPath))
            {
                report = "Le package runtime reference encore AnimationLab. Corrigez les objets exportes avant de rebaker.";
                return false;
            }

            bakedRig = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath).GetComponent<CombatCinematicRig>();
            SerializedObject skillSerialized = new SerializedObject(skill);
            skillSerialized.FindProperty("combatCinematicRigPrefab").objectReferenceValue = bakedRig;
            skillSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(skill);
            AssetDatabase.SaveAssets();

            report = "Package runtime bake : " + prefabPath + " (" + cameras.Count + " camera(s), " + exports.Count + " export(s)).";
            return true;
        }
        finally
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            if (AssetDatabase.LoadAssetAtPath<TimelineAsset>(temporaryTimelinePath) != null)
                AssetDatabase.DeleteAsset(temporaryTimelinePath);
        }
    }

    private static bool EnsureCameraAuthoring(LightSkillTimelineAuthoringRig rig, out string error)
    {
        error = null;
        TimelineAsset timeline = rig.LightSkill.Timeline as TimelineAsset;
        foreach (CinemachineShot shot in GetShots(timeline))
        {
            bool valid;
            CinemachineCamera camera = rig.Director.GetReferenceValue(shot.VirtualCamera.exposedName, out valid) as CinemachineCamera;
            if (!valid || camera == null || !camera.transform.IsChildOf(rig.transform))
            {
                error = "Camera d'auteur introuvable pour '" + shot.VirtualCamera.exposedName + "'.";
                return false;
            }

            LightSkillCinematicCameraAuthoring config = camera.GetComponent<LightSkillCinematicCameraAuthoring>();
            if (config == null) config = Undo.AddComponent<LightSkillCinematicCameraAuthoring>(camera.gameObject);
            config.Configure(shot.VirtualCamera.exposedName.ToString());
            EditorUtility.SetDirty(config);
        }
        return true;
    }

    private static void ValidateCameras(LightSkillTimelineAuthoringRig rig, TimelineAsset timeline, List<string> issues)
    {
        HashSet<string> keys = new HashSet<string>();
        foreach (CinemachineShot shot in GetShots(timeline))
        {
            string key = shot.VirtualCamera.exposedName.ToString();
            if (!keys.Add(key))
            {
                issues.Add("Cle Cinemachine dupliquee : '" + key + "'.");
                continue;
            }

            bool valid;
            CinemachineCamera camera = rig.Director.GetReferenceValue(shot.VirtualCamera.exposedName, out valid) as CinemachineCamera;
            if (!valid || camera == null || !camera.transform.IsChildOf(rig.transform))
                issues.Add("Camera non resolue pour '" + key + "'.");
        }
    }

    private static void ValidateExtraTrackBindings(LightSkillTimelineAuthoringRig rig, TimelineAsset timeline, List<string> issues)
    {
        HashSet<string> names = new HashSet<string>();
        foreach (PlayableBinding output in timeline.outputs)
        {
            if (IsStandardOutput(output, rig.LightSkill) || !TrackHasContent(output.sourceObject as TrackAsset)) continue;
            UnityEngine.Object target = rig.Director.GetGenericBinding(output.sourceObject);
            if (target == null)
            {
                issues.Add("La piste '" + output.streamName + "' contient du contenu mais aucun binding runtime.");
                continue;
            }

            if (IsPreviewActorTarget(target, rig))
            {
                issues.Add("La piste '" + output.streamName + "' vise un preview actor. Utilisez Player.Animator ou Enemy.Animator.");
                continue;
            }

            if (FindRuntimeExport(target) == null)
                issues.Add("La piste '" + output.streamName + "' doit viser un objet marque LightSkillRuntimeExport.");
            else if (!names.Add(output.streamName))
                issues.Add("Nom de piste runtime duplique : '" + output.streamName + "'.");
        }
    }

    private static List<CombatCinematicCameraBinding> CopyReferencedCameras(
        LightSkillTimelineAuthoringRig rig, TimelineAsset runtimeTimeline, Transform destination)
    {
        List<CombatCinematicCameraBinding> bindings = new List<CombatCinematicCameraBinding>();
        foreach (CinemachineShot shot in GetShots(runtimeTimeline))
        {
            string key = shot.VirtualCamera.exposedName.ToString();
            bool valid;
            CinemachineCamera source = rig.Director.GetReferenceValue(shot.VirtualCamera.exposedName, out valid) as CinemachineCamera;
            if (!valid || source == null) continue;

            GameObject copy = UnityEngine.Object.Instantiate(source.gameObject, destination);
            copy.name = "Camera_" + (bindings.Count + 1);
            bindings.Add(new CombatCinematicCameraBinding
            {
                timelineCameraKey = key,
                camera = copy.GetComponent<CinemachineCamera>()
            });
        }
        return bindings;
    }

    private static Dictionary<LightSkillRuntimeExport, LightSkillRuntimeExport> CopyRuntimeExports(
        LightSkillTimelineAuthoringRig rig, Transform destination)
    {
        Dictionary<LightSkillRuntimeExport, LightSkillRuntimeExport> result = new Dictionary<LightSkillRuntimeExport, LightSkillRuntimeExport>();
        foreach (LightSkillRuntimeExport source in rig.GetComponentsInChildren<LightSkillRuntimeExport>(true))
        {
            if (source.transform.parent != null && source.transform.parent.GetComponentInParent<LightSkillRuntimeExport>() != null)
                continue;

            GameObject copy = UnityEngine.Object.Instantiate(source.gameObject, destination);
            copy.name = source.name;
            result.Add(source, copy.GetComponent<LightSkillRuntimeExport>());
        }
        return result;
    }

    private static List<CombatCinematicTrackBinding> CopyExtraTrackBindings(
        LightSkillTimelineAuthoringRig rig,
        TimelineAsset runtimeTimeline,
        Dictionary<LightSkillRuntimeExport, LightSkillRuntimeExport> exports)
    {
        List<CombatCinematicTrackBinding> result = new List<CombatCinematicTrackBinding>();
        foreach (PlayableBinding output in runtimeTimeline.outputs)
        {
            if (IsStandardOutput(output, rig.LightSkill) || !TrackHasContent(output.sourceObject as TrackAsset)) continue;
            UnityEngine.Object sourceTarget = rig.Director.GetGenericBinding(output.sourceObject);
            LightSkillRuntimeExport sourceExport = FindRuntimeExport(sourceTarget);
            if (sourceExport == null || !exports.TryGetValue(sourceExport, out LightSkillRuntimeExport copiedExport)) continue;
            result.Add(new CombatCinematicTrackBinding
            {
                trackName = output.streamName,
                target = ResolveCopiedTarget(sourceTarget, copiedExport)
            });
        }
        return result;
    }

    private static UnityEngine.Object ResolveCopiedTarget(UnityEngine.Object source, LightSkillRuntimeExport copy)
    {
        if (source is GameObject) return copy.gameObject;
        if (source is Component component) return copy.GetComponent(component.GetType());
        return copy.gameObject;
    }

    private static void ApplyRigBindings(
        CombatCinematicRig rig,
        List<CombatCinematicCameraBinding> cameras,
        List<CombatCinematicTrackBinding> tracks)
    {
        SerializedObject serialized = new SerializedObject(rig);
        CopyBindings(serialized.FindProperty("cameraBindings"), cameras, (property, binding) =>
        {
            property.FindPropertyRelative("timelineCameraKey").stringValue = binding.timelineCameraKey;
            property.FindPropertyRelative("camera").objectReferenceValue = binding.camera;
        });
        CopyBindings(serialized.FindProperty("trackBindings"), tracks, (property, binding) =>
        {
            property.FindPropertyRelative("trackName").stringValue = binding.trackName;
            property.FindPropertyRelative("target").objectReferenceValue = binding.target;
        });
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CopyBindings<T>(SerializedProperty property, List<T> values, Action<SerializedProperty, T> apply)
    {
        property.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++) apply(property.GetArrayElementAtIndex(i), values[i]);
    }

    private static IEnumerable<CinemachineShot> GetShots(TimelineAsset timeline)
    {
        foreach (PlayableBinding output in timeline.outputs)
        {
            if (output.sourceObject is not CinemachineTrack track) continue;
            foreach (TimelineClip clip in track.GetClips())
                if (clip.asset is CinemachineShot shot) yield return shot;
        }
    }

    private static bool IsStandardOutput(PlayableBinding output, LightSkillSO skill)
    {
        return output.sourceObject is CinemachineTrack || output.sourceObject is SignalTrack ||
               output.streamName == skill.PlayerAnimatorTrackName || output.streamName == skill.EnemyAnimatorTrackName;
    }

    private static bool TrackHasContent(TrackAsset track)
    {
        if (track == null) return false;
        foreach (TimelineClip ignored in track.GetClips()) return true;
        foreach (IMarker ignored in track.GetMarkers()) return true;
        return false;
    }

    private static bool IsPreviewActorTarget(UnityEngine.Object target, LightSkillTimelineAuthoringRig rig)
    {
        Transform transform = target switch
        {
            GameObject gameObject => gameObject.transform,
            Component component => component.transform,
            _ => null
        };
        return transform != null && ((rig.PreviewPlayerAnimator != null && transform.IsChildOf(rig.PreviewPlayerAnimator.transform)) ||
            (rig.PreviewEnemyAnimator != null && transform.IsChildOf(rig.PreviewEnemyAnimator.transform)));
    }

    private static LightSkillRuntimeExport FindRuntimeExport(UnityEngine.Object target)
    {
        return target switch
        {
            GameObject gameObject => gameObject.GetComponentInParent<LightSkillRuntimeExport>(),
            Component component => component.GetComponentInParent<LightSkillRuntimeExport>(),
            _ => null
        };
    }

    private static void ValidateRuntimeExportDependencies(LightSkillTimelineAuthoringRig rig, List<string> issues)
    {
        foreach (LightSkillRuntimeExport export in rig.GetComponentsInChildren<LightSkillRuntimeExport>(true))
        {
            MonoBehaviour[] components = export.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (MonoBehaviour component in components)
            {
                if (component == null || component is LightSkillRuntimeExport) continue;
                SerializedObject serialized = new SerializedObject(component);
                SerializedProperty property = serialized.GetIterator();
                while (property.NextVisible(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference ||
                        property.propertyPath == "m_Script") continue;

                    UnityEngine.Object reference = property.objectReferenceValue;
                    Transform referencedTransform = reference switch
                    {
                        GameObject gameObject => gameObject.transform,
                        Component referencedComponent => referencedComponent.transform,
                        _ => null
                    };
                    if (referencedTransform == null || !referencedTransform.IsChildOf(rig.transform) ||
                        referencedTransform.IsChildOf(export.transform)) continue;

                    issues.Add("L'export '" + export.name + "' reference l'objet d'auteur '" +
                        referencedTransform.name + "' via " + component.GetType().Name + "." + property.propertyPath + ".");
                    break;
                }
            }
        }
    }

    private static bool HasAuthoringDependency(string prefabPath)
    {
        foreach (string dependency in AssetDatabase.GetDependencies(prefabPath, true))
        {
            string normalized = dependency.Replace('\\', '/');
            if (normalized.EndsWith("/AnimationLab.prefab", StringComparison.OrdinalIgnoreCase) ||
                normalized.EndsWith("/AnimationLab.unity", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
#endif
