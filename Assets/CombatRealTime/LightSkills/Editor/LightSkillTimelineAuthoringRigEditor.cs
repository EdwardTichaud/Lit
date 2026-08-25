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

        if (GUILayout.Button("Log Framing Audit"))
        {
            if (LightSkillRuntimeRigBaker.LogFramingAudit(rig, out string report)) Debug.Log("[LightSkill Framing Authoring] " + report, rig);
            else EditorGUILayout.HelpBox(report, MessageType.Error);
        }

        if (GUILayout.Button("Attach Framing Audit To Active Rig"))
        {
            if (LightSkillRuntimeRigBaker.AttachFramingAuditToActiveRig(rig, out string report))
                Debug.Log("[LightSkill Framing Audit] " + report, rig);
            else EditorUtility.DisplayDialog("LightSkill Framing Audit", report, "OK");
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
        return Validate(authoringRig, validateExistingPackage: true, out report);
    }

    private static bool Validate(
        LightSkillTimelineAuthoringRig authoringRig,
        bool validateExistingPackage,
        out string report)
    {
        List<string> issues = new List<string>();
        if (authoringRig == null) issues.Add("LightSkillTimelineAuthoringRig manquant.");
        if (authoringRig != null && authoringRig.LightSkill == null) issues.Add("LightSkillSO manquant.");
        if (authoringRig != null && authoringRig.Director == null) issues.Add("PlayableDirector manquant.");
        if (authoringRig != null && (authoringRig.PreviewPlayerAnimator == null || authoringRig.PreviewEnemyAnimator == null))
            issues.Add("Les Animators preview Player et Enemy sont requis.");
        if (authoringRig != null && (authoringRig.PreviewPlayerAnchor == null || authoringRig.PreviewEnemyAnchor == null))
            issues.Add("Lucian_Anchor et Enemy_Anchor sont requis comme reperes des roots du plateau runtime.");

        if (authoringRig != null && !authoringRig.ApplyPreviewBindings(out string bindingError)) issues.Add(bindingError);
        TimelineAsset timeline = authoringRig != null && authoringRig.LightSkill != null
            ? authoringRig.LightSkill.Timeline as TimelineAsset : null;
        if (timeline == null) issues.Add("TimelineAsset manquant.");
        if (timeline != null) issues.AddRange(LightSkillTimelineContract.GetIssues(timeline, authoringRig.LightSkill));

        if (authoringRig != null && timeline != null)
        {
            ValidateCameras(authoringRig, timeline, issues);
            ValidateStageAnchors(authoringRig, issues);
            ValidatePostTimelineStates(authoringRig, issues);
            ValidateExtraTrackBindings(authoringRig, timeline, issues);
            ValidateRuntimeExportDependencies(authoringRig, issues);

            if (validateExistingPackage && issues.Count == 0 && authoringRig.LightSkill.CombatCinematicRigPrefab != null)
            {
                CombatCinematicRig existingRig = authoringRig.LightSkill.CombatCinematicRigPrefab.GetComponent<CombatCinematicRig>();
                string expectedRuntimeTimelinePath = existingRig != null && existingRig.Director != null
                    ? AssetDatabase.GetAssetPath(existingRig.Director.playableAsset)
                    : null;
                ValidateBakedPackage(
                    authoringRig.LightSkill.CombatCinematicRigPrefab,
                    CaptureAuthorCameraSnapshots(authoringRig, timeline),
                    expectedRuntimeTimelinePath,
                    issues,
                    out _);
            }
        }

        report = issues.Count == 0
            ? "Contrat valide : package runtime exportable."
            : string.Join("\n", issues);
        return issues.Count == 0;
    }

    public static bool Bake(LightSkillTimelineAuthoringRig authoringRig, out CombatCinematicRig bakedRig, out string report)
    {
        bakedRig = null;
        report = null;
        if (authoringRig == null || authoringRig.LightSkill == null)
        {
            report = "Rig d'auteur ou LightSkillSO manquant.";
            return false;
        }

        if (!EnsureCameraAuthoring(authoringRig, out string setupError))
        {
            report = setupError;
            return false;
        }

        if (!Validate(authoringRig, validateExistingPackage: false, out report))
        {
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
        string temporaryPrefabPath = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + skill.name + "_CinematicRig_BakeTmp.prefab");
        GameObject root = null;
        bool bakeCompleted = false;
        try
        {
            if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(sourceTimeline), temporaryTimelinePath))
            {
                report = "Impossible de cloner la Timeline runtime.";
                return false;
            }

            TimelineAsset runtimeTimeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(temporaryTimelinePath);
            if (runtimeTimeline == null)
            {
                report = "La copie temporaire n'est pas un TimelineAsset valide : " + temporaryTimelinePath;
                return false;
            }

            // AnimationLab can use absolute preview actor transforms. Runtime actors
            // must instead keep the stage transform chosen at playback time.
            ConfigureRuntimeActorTracks(runtimeTimeline);
            EditorUtility.SetDirty(runtimeTimeline);
            AssetDatabase.SaveAssets();

            root = new GameObject(skill.name + "_CinematicRig");
            PlayableDirector director = root.AddComponent<PlayableDirector>();
            director.playableAsset = runtimeTimeline;
            director.playOnAwake = false;
            director.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
            director.extrapolationMode = DirectorWrapMode.None;
            root.AddComponent<SignalReceiver>();
            root.AddComponent<LitTimelineCinemachineBridge>();
            root.AddComponent<LightSkillCinematicSequenceController>();
            CombatCinematicRig rig = root.AddComponent<CombatCinematicRig>();

            List<CombatCinematicCameraBinding> cameras = CopyReferencedCameras(authoringRig, runtimeTimeline, root.transform);
            List<CameraSnapshot> authorCameras = CaptureAuthorCameraSnapshots(authoringRig, sourceTimeline);
            List<CombatCinematicFramingReference> framingReferences = CaptureAuthorFramingReferences(authoringRig, sourceTimeline, out string framingError);
            if (!string.IsNullOrEmpty(framingError))
            {
                report = framingError;
                return false;
            }
            Dictionary<LightSkillRuntimeExport, LightSkillRuntimeExport> exports = CopyRuntimeExports(authoringRig, root.transform);
            List<CombatCinematicTrackBinding> tracks = CopyExtraTrackBindings(
                authoringRig,
                sourceTimeline,
                runtimeTimeline,
                cameras,
                exports);
            ApplyRigBindings(rig, cameras, tracks);
            rig.ConfigureAuthoringStageLayout(
                authoringRig.transform,
                authoringRig.PreviewPlayerAnchor,
                authoringRig.PreviewEnemyAnchor);
            rig.ConfigureFramingReferences(framingReferences);
            if (CountMissingScripts(root) > 0)
            {
                report = "Le package contient un script manquant avant sauvegarde. Corrigez AnimationLab, puis rebakez.";
                return false;
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, temporaryPrefabPath);
            if (prefab == null)
            {
                report = "Unity n'a pas pu enregistrer le prefab runtime.";
                return false;
            }
            if (CountMissingScripts(temporaryPrefabPath) > 0)
            {
                report = "Le prefab runtime contient encore un script manquant. Corrigez le rig d'auteur avant de rebaker.";
                return false;
            }

            if (HasAuthoringDependency(temporaryPrefabPath))
            {
                report = "Le package runtime reference encore AnimationLab. Corrigez les objets exportes avant de rebaker.";
                return false;
            }

            List<string> packageIssues = new List<string>();
            ValidateBakedPackage(
                AssetDatabase.LoadAssetAtPath<GameObject>(temporaryPrefabPath).GetComponent<CombatCinematicRig>(),
                authorCameras,
                temporaryTimelinePath,
                packageIssues,
                out string packageReport);
            if (packageIssues.Count > 0)
            {
                report = "Package runtime invalide :\n" + string.Join("\n", packageIssues);
                return false;
            }

            if (!TryFinalizePackage(root, prefabPath, temporaryTimelinePath, runtimeTimelinePath, out string finalizeError))
            {
                report = "Le bake temporaire est valide, mais le remplacement du package existant a echoue : " + finalizeError;
                return false;
            }

            bakeCompleted = true;
            GameObject bakedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            bakedRig = bakedPrefab != null ? bakedPrefab.GetComponent<CombatCinematicRig>() : null;
            if (bakedRig == null)
            {
                report = "Le prefab runtime a ete enregistre mais son CombatCinematicRig est introuvable.";
                return false;
            }

            SerializedObject skillSerialized = new SerializedObject(skill);
            skillSerialized.FindProperty("combatCinematicRigPrefab").objectReferenceValue = bakedRig;
            skillSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(skill);
            AssetDatabase.SaveAssets();
            CleanupObsoleteBakedTimelines(folder, skill.name, runtimeTimelinePath);

            report = "Package runtime bake : " + prefabPath + "\nTimeline runtime : " + runtimeTimelinePath + "\n" +
                packageReport + "\nPlacement : midpoint direct sur les anchors bakes." +
                "\nExports : " + exports.Count + ". Exclus : preview actors, Preview_MainCamera, Brain et AudioListener.";
            return true;
        }
        finally
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            if (!bakeCompleted && AssetDatabase.LoadMainAssetAtPath(temporaryTimelinePath) != null)
                AssetDatabase.DeleteAsset(temporaryTimelinePath);
            if (AssetDatabase.LoadMainAssetAtPath(temporaryPrefabPath) != null)
                AssetDatabase.DeleteAsset(temporaryPrefabPath);
        }
    }

    public static bool LogFramingAudit(LightSkillTimelineAuthoringRig authoringRig, out string report)
    {
        report = null;
        if (authoringRig == null || authoringRig.LightSkill == null)
        {
            report = "Rig d'auteur ou LightSkillSO manquant.";
            return false;
        }

        TimelineAsset timeline = authoringRig.LightSkill.Timeline as TimelineAsset;
        if (timeline == null)
        {
            report = "Timeline d'auteur introuvable.";
            return false;
        }

        if (!authoringRig.ApplyPreviewBindings(out string bindingError))
        {
            report = bindingError;
            return false;
        }

        List<CombatCinematicFramingReference> references = CaptureAuthorFramingReferences(authoringRig, timeline, out report);
        if (references == null)
        {
            return false;
        }

        List<string> lines = new List<string>();
        for (int i = 0; i < references.Count; i++)
        {
            CombatCinematicFramingReference reference = references[i];
            lines.Add("t=" + reference.timelineTime.ToString("0.000") +
                      " camera='" + reference.cameraKey + "'" +
                      " playerStage=" + reference.playerAnimatorStagePosition.ToString("F4") +
                      " enemyStage=" + reference.enemyAnimatorStagePosition.ToString("F4") +
                      " cameraStage=" + reference.cameraStagePosition.ToString("F4") +
                      " cameraRot=" + reference.cameraStageRotation.eulerAngles.ToString("F4") +
                      " fov=" + reference.cameraFieldOfView.ToString("0.000"));
        }

        report = "Timeline='" + timeline.name + "'\n" + string.Join("\n", lines);
        return true;
    }

    public static bool AttachFramingAuditToActiveRig(LightSkillTimelineAuthoringRig authoringRig, out string report)
    {
        report = null;
        if (authoringRig == null || authoringRig.LightSkill == null || authoringRig.LightSkill.CombatCinematicRigPrefab == null)
        {
            report = "Le prefab runtime actif du LightSkill est requis.";
            return false;
        }

        TimelineAsset timeline = authoringRig.LightSkill.Timeline as TimelineAsset;
        if (timeline == null)
        {
            report = "Timeline d'auteur introuvable.";
            return false;
        }

        List<CombatCinematicFramingReference> references = CaptureAuthorFramingReferences(authoringRig, timeline, out string captureError);
        if (references == null)
        {
            report = captureError;
            return false;
        }

        string prefabPath = AssetDatabase.GetAssetPath(authoringRig.LightSkill.CombatCinematicRigPrefab);
        GameObject prefabContents = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            CombatCinematicRig runtimeRig = prefabContents.GetComponent<CombatCinematicRig>();
            if (runtimeRig == null)
            {
                report = "CombatCinematicRig est absent du prefab runtime actif.";
                return false;
            }

            for (int i = 0; i < references.Count; i++)
            {
                if (!runtimeRig.HasCameraBinding(references[i].cameraKey))
                {
                    report = "La camera d'auteur '" + references[i].cameraKey + "' n'existe pas dans le prefab runtime actif. Rebakerez le package avant de comparer le cadrage.";
                    return false;
                }
            }

            runtimeRig.ConfigureFramingReferences(references);
            PrefabUtility.SaveAsPrefabAsset(prefabContents, prefabPath);
            report = "References de cadrage attachees au prefab actif : " + prefabPath + ". " + references.Count + " releve(s) seront compares en runtime.";
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabContents);
        }
    }

    private static bool TryFinalizePackage(
        GameObject runtimeRigRoot,
        string prefabPath,
        string temporaryTimelinePath,
        string runtimeTimelinePath,
        out string error)
    {
        error = null;
        if (runtimeRigRoot == null)
        {
            error = "Rig runtime temporaire introuvable.";
            return false;
        }

        string previousTimelinePath = null;
        bool runtimeTimelineMoved = false;
        try
        {
            AssetDatabase.SaveAssets();
            if (AssetDatabase.LoadMainAssetAtPath(runtimeTimelinePath) != null)
            {
                previousTimelinePath = AssetDatabase.GenerateUniqueAssetPath(
                    Path.GetDirectoryName(runtimeTimelinePath)?.Replace('\\', '/') + "/Runtime_Previous.playable");
                string movePreviousError = AssetDatabase.MoveAsset(runtimeTimelinePath, previousTimelinePath);
                if (!string.IsNullOrEmpty(movePreviousError))
                {
                    error = movePreviousError;
                    return false;
                }
            }

            string moveRuntimeError = AssetDatabase.MoveAsset(temporaryTimelinePath, runtimeTimelinePath);
            if (!string.IsNullOrEmpty(moveRuntimeError))
            {
                error = moveRuntimeError;
                RestorePreviousRuntimeTimeline(runtimeTimelinePath, temporaryTimelinePath, previousTimelinePath);
                return false;
            }
            runtimeTimelineMoved = true;

            if (PrefabUtility.SaveAsPrefabAsset(runtimeRigRoot, prefabPath) == null)
            {
                error = "Unity n'a pas pu enregistrer le prefab runtime final.";
                RestorePreviousRuntimeTimeline(runtimeTimelinePath, temporaryTimelinePath, previousTimelinePath);
                return false;
            }

            AssetDatabase.SaveAssets();
            if (!string.IsNullOrEmpty(previousTimelinePath))
            {
                AssetDatabase.DeleteAsset(previousTimelinePath);
            }
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            if (runtimeTimelineMoved)
            {
                RestorePreviousRuntimeTimeline(runtimeTimelinePath, temporaryTimelinePath, previousTimelinePath);
            }
            return false;
        }
    }

    private static void RestorePreviousRuntimeTimeline(
        string runtimeTimelinePath,
        string temporaryTimelinePath,
        string previousTimelinePath)
    {
        if (AssetDatabase.LoadMainAssetAtPath(runtimeTimelinePath) != null)
        {
            AssetDatabase.MoveAsset(runtimeTimelinePath, temporaryTimelinePath);
        }

        if (!string.IsNullOrEmpty(previousTimelinePath) && AssetDatabase.LoadMainAssetAtPath(previousTimelinePath) != null)
        {
            AssetDatabase.MoveAsset(previousTimelinePath, runtimeTimelinePath);
        }
    }

    private static void CleanupObsoleteBakedTimelines(string folder, string skillName, string runtimeTimelinePath)
    {
        string obsoletePrefix = folder + "/" + skillName + "_Runtime_Baked";
        string[] guids = AssetDatabase.FindAssets("t:TimelineAsset", new[] { folder });
        for (int i = 0; i < guids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (string.Equals(assetPath, runtimeTimelinePath, StringComparison.Ordinal) ||
                !assetPath.StartsWith(obsoletePrefix, StringComparison.Ordinal))
            {
                continue;
            }

            AssetDatabase.DeleteAsset(assetPath);
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
            config.Configure(shot.VirtualCamera.exposedName.ToString(), configureDefaults: false);
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
            {
                issues.Add("Camera non resolue pour '" + key + "'.");
                continue;
            }

            LightSkillCinematicCameraAuthoring config = camera.GetComponent<LightSkillCinematicCameraAuthoring>();
            if (config == null)
            {
                issues.Add("Camera sans configuration runtime : '" + key + "'.");
                continue;
            }

            if (config.FollowTarget != LightSkillRuntimeAnchor.None && camera.GetComponent<CinemachineFollow>() == null)
                issues.Add("Camera '" + key + "' sans CinemachineFollow pour sa cible de suivi.");
            if (config.LookAtTarget != LightSkillRuntimeAnchor.None &&
                camera.GetComponent<CinemachineHardLookAt>() == null &&
                camera.GetComponent<CinemachineRotationComposer>() == null)
            {
                issues.Add("Camera '" + key + "' sans module de visee Cinemachine.");
            }
        }
    }

    private static void ValidateStageAnchors(LightSkillTimelineAuthoringRig rig, List<string> issues)
    {
        if (rig.PreviewPlayerAnchor == null || rig.PreviewEnemyAnchor == null ||
            rig.PreviewPlayerAnimator == null || rig.PreviewEnemyAnimator == null)
        {
            return;
        }

        if (!rig.PreviewPlayerAnchor.IsChildOf(rig.transform) || !rig.PreviewEnemyAnchor.IsChildOf(rig.transform))
        {
            issues.Add("Les anchors de plateau doivent etre des enfants de AnimationLab.");
        }

        if (!CombatCinematicAuthoringActorResolver.ValidateRootAnimator(
                rig.PreviewPlayerActorRoot,
                rig.PreviewPlayerAnchor,
                rig.PreviewPlayerAnimator,
                "Player",
                out string playerError))
        {
            issues.Add(playerError);
        }

        if (!CombatCinematicAuthoringActorResolver.ValidateRootAnimator(
                rig.PreviewEnemyActorRoot,
                rig.PreviewEnemyAnchor,
                rig.PreviewEnemyAnimator,
                "Enemy",
                out string enemyError))
        {
            issues.Add(enemyError);
        }

        if (Vector3.Distance(rig.PreviewPlayerAnchor.localPosition, rig.PreviewEnemyAnchor.localPosition) <= 0.001f)
        {
            issues.Add("Lucian_Anchor et Enemy_Anchor ne peuvent pas partager la meme pose.");
        }
    }

    private static void ValidatePostTimelineStates(LightSkillTimelineAuthoringRig rig, List<string> issues)
    {
        if (rig == null || rig.LightSkill == null)
        {
            return;
        }

        ValidatePostTimelineState(
            rig.LightSkill.PostTimelinePlayerState,
            rig.PreviewPlayerAnimator,
            "Player",
            issues);
        ValidatePostTimelineState(
            rig.LightSkill.PostTimelineEnemyState,
            rig.PreviewEnemyAnimator,
            "Enemy",
            issues);
    }

    private static void ValidatePostTimelineState(
        LightSkillPostTimelineState state,
        Animator animator,
        string actorLabel,
        List<string> issues)
    {
        if (state == null || !state.IsConfigured)
        {
            return;
        }

        if (animator == null || animator.runtimeAnimatorController == null)
        {
            issues.Add("State de sortie " + actorLabel + " configuree mais Animator preview invalide.");
            return;
        }

        if (!animator.HasState(0, Animator.StringToHash(state.AnimatorStateName)))
        {
            issues.Add("State de sortie " + actorLabel + " introuvable : '" + state.AnimatorStateName + "'.");
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

            if (TryResolveAuthorCameraTarget(target, rig, timeline, out _))
            {
                if (!names.Add(output.streamName))
                    issues.Add("Nom de piste runtime duplique : '" + output.streamName + "'.");
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
        TimelineAsset sourceTimeline,
        TimelineAsset runtimeTimeline,
        List<CombatCinematicCameraBinding> cameras,
        Dictionary<LightSkillRuntimeExport, LightSkillRuntimeExport> exports)
    {
        List<CombatCinematicTrackBinding> result = new List<CombatCinematicTrackBinding>();
        foreach (PlayableBinding output in runtimeTimeline.outputs)
        {
            if (IsStandardOutput(output, rig.LightSkill) || !TrackHasContent(output.sourceObject as TrackAsset)) continue;

            // The runtime Timeline is a clone. Its TrackAsset instances are different from
            // the authoring Timeline instances held by the preview director, so resolve the
            // equivalent source track by name before reading its preview binding.
            PlayableBinding sourceOutput = FindMatchingOutput(sourceTimeline, output);
            if (sourceOutput.sourceObject == null) continue;
            UnityEngine.Object sourceTarget = rig.Director.GetGenericBinding(sourceOutput.sourceObject);
            if (TryResolveAuthorCameraTarget(sourceTarget, rig, runtimeTimeline, out string cameraKey))
            {
                CinemachineCamera copiedCamera = FindCopiedCamera(cameras, cameraKey);
                UnityEngine.Object copiedTarget = ResolveCopiedCameraTarget(sourceTarget, copiedCamera);
                if (copiedTarget != null)
                {
                    result.Add(new CombatCinematicTrackBinding
                    {
                        trackName = output.streamName,
                        target = copiedTarget
                    });
                }
                continue;
            }

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

    private static PlayableBinding FindMatchingOutput(TimelineAsset sourceTimeline, PlayableBinding runtimeOutput)
    {
        if (sourceTimeline == null || runtimeOutput.sourceObject == null) return default;
        foreach (PlayableBinding candidate in sourceTimeline.outputs)
        {
            if (candidate.sourceObject == null ||
                candidate.sourceObject.GetType() != runtimeOutput.sourceObject.GetType() ||
                !string.Equals(candidate.streamName, runtimeOutput.streamName, StringComparison.Ordinal))
            {
                continue;
            }

            return candidate;
        }

        return default;
    }

    private static bool TryResolveAuthorCameraTarget(
        UnityEngine.Object target,
        LightSkillTimelineAuthoringRig rig,
        TimelineAsset timeline,
        out string cameraKey)
    {
        cameraKey = null;
        if (target == null || rig == null || timeline == null) return false;

        Transform targetTransform = target switch
        {
            GameObject gameObject => gameObject.transform,
            Component component => component.transform,
            _ => null
        };
        if (targetTransform == null) return false;

        foreach (CinemachineShot shot in GetShots(timeline))
        {
            bool valid;
            CinemachineCamera camera = rig.Director.GetReferenceValue(shot.VirtualCamera.exposedName, out valid) as CinemachineCamera;
            if (!valid || camera == null || targetTransform != camera.transform) continue;
            cameraKey = shot.VirtualCamera.exposedName.ToString();
            return true;
        }
        return false;
    }

    private static CinemachineCamera FindCopiedCamera(List<CombatCinematicCameraBinding> cameras, string key)
    {
        for (int i = 0; i < cameras.Count; i++)
        {
            CombatCinematicCameraBinding binding = cameras[i];
            if (binding != null && binding.camera != null && string.Equals(binding.timelineCameraKey, key, StringComparison.Ordinal))
                return binding.camera;
        }
        return null;
    }

    private static UnityEngine.Object ResolveCopiedCameraTarget(UnityEngine.Object source, CinemachineCamera copiedCamera)
    {
        if (copiedCamera == null) return null;
        if (source is GameObject) return copiedCamera.gameObject;
        if (source is Component component) return copiedCamera.GetComponent(component.GetType());
        return null;
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

    private static List<CameraSnapshot> CaptureAuthorCameraSnapshots(
        LightSkillTimelineAuthoringRig rig,
        TimelineAsset timeline)
    {
        List<CameraSnapshot> snapshots = new List<CameraSnapshot>();
        foreach (CinemachineShot shot in GetShots(timeline))
        {
            string key = shot.VirtualCamera.exposedName.ToString();
            bool valid;
            CinemachineCamera camera = rig.Director.GetReferenceValue(shot.VirtualCamera.exposedName, out valid) as CinemachineCamera;
            if (valid && camera != null) snapshots.Add(CameraSnapshot.Create(key, camera));
        }
        return snapshots;
    }

    private static void ValidateBakedPackage(
        CombatCinematicRig bakedRig,
        List<CameraSnapshot> authorCameras,
        string expectedRuntimeTimelinePath,
        List<string> issues,
        out string report)
    {
        List<string> details = new List<string>();
        if (bakedRig == null)
        {
            issues.Add("CombatCinematicRig manquant dans le prefab baked.");
            report = string.Empty;
            return;
        }

        if (!bakedRig.HasAuthoringStageLayout)
            issues.Add("Le prefab baked ne contient pas PlayerStageAnchor et EnemyStageAnchor.");
        string prefabPath = AssetDatabase.GetAssetPath(bakedRig.gameObject);
        if (CountMissingScripts(prefabPath) > 0)
            issues.Add("Le prefab baked contient un ou plusieurs scripts manquants.");

        PlayableDirector bakedDirector = bakedRig.Director;
        if (bakedDirector == null)
        {
            issues.Add("PlayableDirector manquant dans le prefab baked.");
        }
        else if (bakedDirector.playableAsset == null ||
                 !string.Equals(AssetDatabase.GetAssetPath(bakedDirector.playableAsset), expectedRuntimeTimelinePath,
                     StringComparison.OrdinalIgnoreCase))
        {
            issues.Add("La Timeline runtime du prefab ne correspond pas au package attendu.");
        }
        else
        {
            ValidateBakedCameraAnimationOffsets(bakedRig, bakedDirector.playableAsset as TimelineAsset, issues);
            if (bakedDirector.playOnAwake)
                issues.Add("Le PlayableDirector runtime ne doit jamais avoir Play On Awake active.");
            if (bakedDirector.timeUpdateMode != DirectorUpdateMode.UnscaledGameTime)
                issues.Add("Le PlayableDirector runtime doit utiliser UnscaledGameTime.");
            if (bakedDirector.extrapolationMode != DirectorWrapMode.None)
                issues.Add("Le PlayableDirector runtime doit utiliser DirectorWrapMode.None.");
        }

        if (bakedRig.CameraBindings.Count != authorCameras.Count)
            issues.Add("Nombre de cameras baked incorrect : " + bakedRig.CameraBindings.Count + "/" + authorCameras.Count + ".");

        for (int i = 0; i < authorCameras.Count; i++)
        {
            CameraSnapshot source = authorCameras[i];
            CombatCinematicCameraBinding binding = null;
            for (int j = 0; j < bakedRig.CameraBindings.Count; j++)
            {
                CombatCinematicCameraBinding candidate = bakedRig.CameraBindings[j];
                if (candidate != null && string.Equals(candidate.timelineCameraKey, source.Key, StringComparison.Ordinal))
                {
                    binding = candidate;
                    break;
                }
            }

            if (binding == null || binding.camera == null)
            {
                issues.Add("Camera baked introuvable pour la cle '" + source.Key + "'.");
                continue;
            }

            CameraSnapshot baked = CameraSnapshot.Create(binding.timelineCameraKey, binding.camera);
            string difference = source.GetDifference(baked);
            if (!string.IsNullOrEmpty(difference))
                issues.Add("Camera '" + source.Key + "' differente du rig d'auteur : " + difference);
            else
                details.Add(source.Describe());
        }

        report = details.Count == 0
            ? "Aucune camera valide exportee."
            : "Cameras exportees :\n" + string.Join("\n", details);
    }

    private static void ValidateBakedCameraAnimationOffsets(
        CombatCinematicRig rig,
        TimelineAsset timeline,
        List<string> issues)
    {
        if (timeline == null) return;

        foreach (CombatCinematicTrackBinding binding in rig.TrackBindings)
        {
            if (binding?.target is not Animator animator || animator.GetComponent<CinemachineCamera>() == null)
                continue;

            if (FindAnimationTrack(timeline, binding.trackName) == null)
                issues.Add("La piste camera '" + binding.trackName + "' est absente de la Timeline runtime.");
        }
    }

    private static AnimationTrack FindAnimationTrack(TimelineAsset timeline, string trackName)
    {
        if (timeline == null || string.IsNullOrWhiteSpace(trackName)) return null;
        foreach (PlayableBinding output in timeline.outputs)
        {
            if (output.sourceObject is AnimationTrack track && string.Equals(output.streamName, trackName, StringComparison.Ordinal))
                return track;
        }
        return null;
    }

    private static void ConfigureRuntimeActorTracks(TimelineAsset timeline)
    {
        if (timeline == null) return;

        foreach (PlayableBinding output in timeline.outputs)
        {
            if (output.sourceObject is not AnimationTrack track ||
                (output.streamName != LightSkillTimelineContract.PlayerAnimatorTrack &&
                 output.streamName != LightSkillTimelineContract.EnemyAnimatorTrack))
            {
                continue;
            }

            track.trackOffset = TrackOffset.ApplySceneOffsets;
        }
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
        if (track is AnimationTrack animationTrack && animationTrack.infiniteClip != null) return true;
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
        return transform != null &&
               ((rig.PreviewPlayerActorRoot != null &&
                 (transform == rig.PreviewPlayerActorRoot || transform.IsChildOf(rig.PreviewPlayerActorRoot))) ||
                (rig.PreviewEnemyActorRoot != null &&
                 (transform == rig.PreviewEnemyActorRoot || transform.IsChildOf(rig.PreviewEnemyActorRoot))));
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

    private static int RemoveMissingScripts(GameObject root)
    {
        int removed = 0;
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);
        return removed;
    }

    private static int CountMissingScripts(GameObject root)
    {
        int count = 0;
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            MonoBehaviour[] behaviours = child.GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
                if (behaviours[i] == null) count++;
        }
        return count;
    }

    private static int CountMissingScripts(string prefabPath)
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            int count = 0;
            foreach (Transform child in contents.GetComponentsInChildren<Transform>(true))
            {
                MonoBehaviour[] behaviours = child.GetComponents<MonoBehaviour>();
                for (int i = 0; i < behaviours.Length; i++)
                    if (behaviours[i] == null) count++;
            }
            return count;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private static List<CombatCinematicFramingReference> CaptureAuthorFramingReferences(
        LightSkillTimelineAuthoringRig rig,
        TimelineAsset timeline,
        out string error)
    {
        error = null;
        if (rig == null || rig.Director == null || rig.PreviewPlayerAnimator == null ||
            rig.PreviewEnemyAnimator == null || timeline == null)
        {
            error = "Impossible de capturer le cadrage : rig, Timeline ou acteurs preview manquants.";
            return null;
        }

        if (!rig.ApplyPreviewBindings(out error))
        {
            return null;
        }

        PlayableDirector director = rig.Director;
        double previousTime = director.time;
        List<CombatCinematicFramingReference> references = new List<CombatCinematicFramingReference>();
        try
        {
            foreach (double time in GetFramingAuditTimes(timeline))
            {
                director.time = time;
                director.Evaluate();
                CinemachineCamera camera = ResolveAuthorCameraAtTime(rig, timeline, time, out string cameraKey);
                if (camera == null)
                {
                    error = "Camera d'auteur introuvable a t=" + time.ToString("0.000") + ".";
                    return null;
                }

                references.Add(new CombatCinematicFramingReference
                {
                    timelineTime = time,
                    cameraKey = cameraKey,
                    playerAnimatorStagePosition = rig.transform.InverseTransformPoint(rig.PreviewPlayerActorRoot.position),
                    playerAnimatorStageRotation = Quaternion.Inverse(rig.transform.rotation) * rig.PreviewPlayerActorRoot.rotation,
                    enemyAnimatorStagePosition = rig.transform.InverseTransformPoint(rig.PreviewEnemyActorRoot.position),
                    enemyAnimatorStageRotation = Quaternion.Inverse(rig.transform.rotation) * rig.PreviewEnemyActorRoot.rotation,
                    cameraStagePosition = rig.transform.InverseTransformPoint(camera.transform.position),
                    cameraStageRotation = Quaternion.Inverse(rig.transform.rotation) * camera.transform.rotation,
                    cameraFieldOfView = camera.Lens.FieldOfView
                });
            }
        }
        finally
        {
            director.time = previousTime;
            director.Evaluate();
        }

        return references;
    }

    private static List<double> GetFramingAuditTimes(TimelineAsset timeline)
    {
        SortedSet<double> times = new SortedSet<double> { 0d };
        if (timeline == null) return new List<double>(times);

        if (timeline.duration >= 1d)
        {
            times.Add(1d);
        }

        foreach (PlayableBinding output in timeline.outputs)
        {
            if (output.sourceObject is not CinemachineTrack track) continue;
            foreach (TimelineClip clip in track.GetClips())
            {
                if (clip.start > 0d && clip.start < timeline.duration)
                {
                    times.Add(clip.start);
                }
            }
        }

        return new List<double>(times);
    }

    private static CinemachineCamera ResolveAuthorCameraAtTime(
        LightSkillTimelineAuthoringRig rig,
        TimelineAsset timeline,
        double time,
        out string cameraKey)
    {
        cameraKey = null;
        foreach (PlayableBinding output in timeline.outputs)
        {
            if (output.sourceObject is not CinemachineTrack track) continue;
            foreach (TimelineClip clip in track.GetClips())
            {
                if (time < clip.start || time > clip.end || clip.asset is not CinemachineShot shot) continue;
                cameraKey = shot.VirtualCamera.exposedName.ToString();
                bool assigned;
                return rig.Director.GetReferenceValue(shot.VirtualCamera.exposedName, out assigned) as CinemachineCamera;
            }
        }

        return null;
    }

    private sealed class CameraSnapshot
    {
        private const float PositionTolerance = 0.001f;
        private const float RotationTolerance = 0.1f;
        private const float FovTolerance = 0.01f;

        public string Key { get; private set; }
        public Vector3 LocalPosition { get; private set; }
        public Quaternion LocalRotation { get; private set; }
        public float FieldOfView { get; private set; }
        public string Priority { get; private set; }
        public string OutputChannel { get; private set; }
        public string Pipeline { get; private set; }
        public Vector3 FollowOffset { get; private set; }
        public LightSkillRuntimeAnchor FollowTarget { get; private set; }
        public LightSkillRuntimeAnchor LookAtTarget { get; private set; }

        public static CameraSnapshot Create(string key, CinemachineCamera camera)
        {
            LightSkillCinematicCameraAuthoring authoring = camera.GetComponent<LightSkillCinematicCameraAuthoring>();
            CinemachineFollow follow = camera.GetComponent<CinemachineFollow>();
            List<string> components = new List<string>();
            foreach (Component component in camera.GetComponents<Component>())
            {
                if (component == null || component is Transform || component is Animator) continue;
                components.Add(component.GetType().FullName);
            }
            components.Sort(StringComparer.Ordinal);
            return new CameraSnapshot
            {
                Key = key,
                LocalPosition = camera.transform.localPosition,
                LocalRotation = camera.transform.localRotation,
                FieldOfView = camera.Lens.FieldOfView,
                Priority = camera.Priority.Value.ToString(),
                OutputChannel = camera.OutputChannel.ToString(),
                Pipeline = string.Join(", ", components),
                FollowOffset = follow != null ? follow.FollowOffset : Vector3.zero,
                FollowTarget = authoring != null ? authoring.FollowTarget : LightSkillRuntimeAnchor.None,
                LookAtTarget = authoring != null ? authoring.LookAtTarget : LightSkillRuntimeAnchor.None
            };
        }

        public string GetDifference(CameraSnapshot other)
        {
            if (other == null) return "snapshot camera manquant";
            List<string> differences = new List<string>();
            if (!string.Equals(Key, other.Key, StringComparison.Ordinal)) differences.Add("cle Timeline");
            if (Vector3.Distance(LocalPosition, other.LocalPosition) > PositionTolerance) differences.Add("position locale");
            if (Quaternion.Angle(LocalRotation, other.LocalRotation) > RotationTolerance) differences.Add("rotation locale");
            if (Mathf.Abs(FieldOfView - other.FieldOfView) > FovTolerance) differences.Add("FOV");
            if (!string.Equals(Priority, other.Priority, StringComparison.Ordinal)) differences.Add("priorite");
            if (!string.Equals(OutputChannel, other.OutputChannel, StringComparison.Ordinal)) differences.Add("Output Channel");
            if (!string.Equals(Pipeline, other.Pipeline, StringComparison.Ordinal)) differences.Add("pipeline Cinemachine");
            if (Vector3.Distance(FollowOffset, other.FollowOffset) > PositionTolerance) differences.Add("offset Follow");
            if (FollowTarget != other.FollowTarget) differences.Add("Follow cible");
            if (LookAtTarget != other.LookAtTarget) differences.Add("Look At cible");
            return string.Join(", ", differences);
        }

        public string Describe()
        {
            return "- " + Key + " | FOV " + FieldOfView.ToString("0.##") + " | Follow " + FollowTarget +
                   " " + FollowOffset.ToString("F2") + " | LookAt " + LookAtTarget + " | " + Pipeline;
        }
    }
}
#endif
