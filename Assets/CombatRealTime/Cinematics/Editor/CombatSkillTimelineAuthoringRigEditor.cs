using System.Collections.Generic;
using System.IO;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[CustomEditor(typeof(CombatSkillTimelineAuthoringRig))]
public sealed class CombatSkillTimelineAuthoringRigEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        CombatSkillTimelineAuthoringRig rig = (CombatSkillTimelineAuthoringRig)target;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Runtime Bake", EditorStyles.boldLabel);

        if (GUILayout.Button("Validate Runtime Contract"))
        {
            if (CombatSkillRuntimeRigBaker.Validate(rig, out string report)) Debug.Log("[Combat Skill Bake] " + report, rig);
            else EditorGUILayout.HelpBox(report, MessageType.Error);
        }

        if (GUILayout.Button("Bake Combat Skill"))
        {
            if (CombatSkillRuntimeRigBaker.Bake(rig, out CombatCinematicRig baked, out string report))
            {
                Selection.activeObject = baked;
                if (report.Contains("Avertissements")) Debug.LogWarning("[Combat Skill Bake] " + report, baked);
                else Debug.Log("[Combat Skill Bake] " + report, baked);
            }
            else EditorUtility.DisplayDialog("Bake Combat Skill", report, "OK");
        }
    }
}

public static class CombatSkillRuntimeRigBaker
{
    public static bool Validate(CombatSkillTimelineAuthoringRig authoring, out string report)
    {
        report = null;
        if (authoring == null || authoring.Skill == null || authoring.Director == null ||
            authoring.PreviewPlayerAnimator == null || authoring.PreviewEnemyAnimator == null ||
            authoring.PreviewCameraBrain == null || authoring.PreviewSignalReceiver == null)
        {
            report = "Le rig d'auteur doit referencer SkillSO, Director, Animators Player/Enemy, Brain et SignalReceiver.";
            return false;
        }

        if (!authoring.ApplyPreviewBindings(out report)) return false;
        if (!CombatCinematicAuthoringActorResolver.ValidateRootAnimator(
                authoring.PreviewPlayerActorRoot,
                null,
                authoring.PreviewPlayerAnimator,
                "Player",
                out string playerRootError))
        {
            report = playerRootError;
            return false;
        }

        if (!CombatCinematicAuthoringActorResolver.ValidateRootAnimator(
                authoring.PreviewEnemyActorRoot,
                null,
                authoring.PreviewEnemyAnimator,
                "Enemy",
                out string enemyRootError))
        {
            report = enemyRootError;
            return false;
        }
        TimelineAsset timeline = authoring.Skill.Cinematic.Timeline as TimelineAsset;
        if (timeline == null) { report = "La Timeline du SkillSO est introuvable."; return false; }

        foreach (PlayableBinding output in timeline.outputs)
        {
            if (output.sourceObject is not CinemachineTrack track) continue;
            foreach (TimelineClip clip in track.GetClips())
            {
                if (clip.asset is not CinemachineShot shot) continue;
                bool valid;
                CinemachineCamera camera = authoring.Director.GetReferenceValue(shot.VirtualCamera.exposedName, out valid) as CinemachineCamera;
                if (!valid || camera == null || !camera.transform.IsChildOf(authoring.transform))
                {
                    report = "Camera de preview non resolue pour '" + shot.VirtualCamera.exposedName + "'.";
                    return false;
                }
            }
        }

        report = "Contrat valide : package in-place exportable.";
        return true;
    }

    public static bool Bake(CombatSkillTimelineAuthoringRig authoring, out CombatCinematicRig bakedRig, out string report)
    {
        bakedRig = null;
        report = null;
        if (authoring == null || authoring.Skill == null)
        {
            report = "Rig d'auteur ou SkillSO manquant.";
            return false;
        }

        SkillSO skill = authoring.Skill;
        TimelineAsset source = skill.Cinematic.Timeline as TimelineAsset;
        if (source == null)
        {
            report = "La Timeline source du SkillSO est introuvable.";
            return false;
        }

        List<string> warnings = new List<string>();
        if (!Validate(authoring, out string validationReport))
        {
            warnings.Add(validationReport);
        }

        string folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(skill))?.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(folder)) { report = "Dossier du SkillSO introuvable."; return false; }

        string timelinePath = folder + "/" + skill.name + "_Runtime.playable";
        string prefabPath = folder + "/" + skill.name + "_CinematicRig.prefab";
        if ((AssetDatabase.LoadMainAssetAtPath(timelinePath) != null || AssetDatabase.LoadMainAssetAtPath(prefabPath) != null) &&
            !EditorUtility.DisplayDialog("Bake Combat Skill", "Remplacer le package runtime existant ?", "Remplacer", "Annuler"))
        {
            report = "Bake annule.";
            return false;
        }

        string tempTimelinePath = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + skill.name + "_Runtime_Tmp.playable");
        GameObject root = null;
        try
        {
            if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source), tempTimelinePath))
            {
                report = "Impossible de copier la Timeline runtime.";
                return false;
            }

            TimelineAsset runtime = AssetDatabase.LoadAssetAtPath<TimelineAsset>(tempTimelinePath);
            ConfigureActorTracks(runtime, skill.Cinematic);
            root = new GameObject(skill.name + "_CinematicRig");
            PlayableDirector director = root.AddComponent<PlayableDirector>();
            director.playableAsset = runtime;
            director.playOnAwake = false;
            director.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
            director.extrapolationMode = DirectorWrapMode.None;
            root.AddComponent<SignalReceiver>();
            root.AddComponent<LitTimelineCinemachineBridge>();
            CombatCinematicRig rig = root.AddComponent<CombatCinematicRig>();
            ApplyCameraBindings(authoring, runtime, root.transform, rig, warnings);

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            AssetDatabase.DeleteAsset(timelinePath);
            string moveError = AssetDatabase.MoveAsset(tempTimelinePath, timelinePath);
            if (!string.IsNullOrEmpty(moveError)) { report = moveError; return false; }

            bakedRig = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath)?.GetComponent<CombatCinematicRig>();
            if (bakedRig == null) { report = "CombatCinematicRig absent du prefab bake."; return false; }
            SerializedObject serializedSkill = new SerializedObject(skill);
            serializedSkill.FindProperty("cinematic").FindPropertyRelative("combatCinematicRigPrefab").objectReferenceValue = bakedRig;
            serializedSkill.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(skill);
            AssetDatabase.SaveAssets();
            report = "Package runtime bake : " + prefabPath + "\nTimeline runtime : " + timelinePath;
            if (warnings.Count > 0)
            {
                report += "\n\nAvertissements : le package a ete exporte partiellement.\n- " +
                          string.Join("\n- ", warnings);
            }
            return true;
        }
        finally
        {
            if (root != null) Object.DestroyImmediate(root);
            if (AssetDatabase.LoadMainAssetAtPath(tempTimelinePath) != null) AssetDatabase.DeleteAsset(tempTimelinePath);
        }
    }

    private static void ConfigureActorTracks(TimelineAsset timeline, CombatSkillCinematicDefinition cinematic)
    {
        foreach (PlayableBinding output in timeline.outputs)
        {
            if (output.sourceObject is AnimationTrack track &&
                (output.streamName == cinematic.PlayerAnimatorTrackName || output.streamName == cinematic.EnemyAnimatorTrackName))
            {
                track.trackOffset = TrackOffset.ApplySceneOffsets;
            }
        }
    }

    private static void ApplyCameraBindings(
        CombatSkillTimelineAuthoringRig authoring,
        TimelineAsset timeline,
        Transform parent,
        CombatCinematicRig rig,
        List<string> warnings)
    {
        SerializedObject serializedRig = new SerializedObject(rig);
        SerializedProperty bindings = serializedRig.FindProperty("cameraBindings");
        List<(string key, CinemachineCamera camera)> copied = new List<(string, CinemachineCamera)>();
        if (authoring.Director == null)
        {
            warnings.Add("PlayableDirector auteur manquant : aucune camera Cinemachine n'a ete exportee.");
            bindings.arraySize = 0;
            serializedRig.ApplyModifiedPropertiesWithoutUndo();
            return;
        }

        foreach (PlayableBinding output in timeline.outputs)
        {
            if (output.sourceObject is not CinemachineTrack track) continue;
            foreach (TimelineClip clip in track.GetClips())
            {
                if (clip.asset is not CinemachineShot shot) continue;
                string key = shot.VirtualCamera.exposedName.ToString();
                if (copied.Exists(item => item.key == key)) continue;
                bool valid;
                CinemachineCamera source = authoring.Director.GetReferenceValue(shot.VirtualCamera.exposedName, out valid) as CinemachineCamera;
                if (!valid || source == null)
                {
                    warnings.Add("Camera Cinemachine non resolue pour la cle '" + key + "' : fallback gameplay au runtime.");
                    continue;
                }
                CinemachineCamera copy = Object.Instantiate(source.gameObject, parent).GetComponent<CinemachineCamera>();
                copy.name = "Camera_" + (copied.Count + 1);
                copied.Add((key, copy));
            }
        }

        bindings.arraySize = copied.Count;
        for (int i = 0; i < copied.Count; i++)
        {
            SerializedProperty binding = bindings.GetArrayElementAtIndex(i);
            binding.FindPropertyRelative("timelineCameraKey").stringValue = copied[i].key;
            binding.FindPropertyRelative("camera").objectReferenceValue = copied[i].camera;
        }
        serializedRig.ApplyModifiedPropertiesWithoutUndo();
    }
}
