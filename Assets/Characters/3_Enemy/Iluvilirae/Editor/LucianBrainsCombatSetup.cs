using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Unity.Cinemachine;

/// <summary>Explicit authoring upgrade. Never runs automatically on import or at runtime.</summary>
public static class LucianBrainsCombatSetup
{
    [MenuItem("Lit/Brains AI/Upgrade Lucian Combat Laboratory")]
    public static void Upgrade()
    {
        UpgradeEnemy();
        var previous = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            var scene = EditorSceneManager.OpenScene(IluviliraeSetup.ScenePath);
            var arena = UnityEngine.Object.FindAnyObjectByType<LucianBrainsCombatArena>();
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Core/System/GameplaySessionRoot.prefab");
            var sourceCamera = source.GetComponentInChildren<LitGameplayCameraModeController>(true);
            if (sourceCamera == null) throw new InvalidOperationException("Gameplay camera source missing.");
            if (arena.arenaCamera.GetComponent<LitGameplayCameraModeController>() == null)
            {
                var cameraObject = UnityEngine.Object.Instantiate(sourceCamera.gameObject);
                cameraObject.name = "CombatLab_GameplayCamera";
                cameraObject.SetActive(true);
                var ucc = cameraObject.GetComponent<Opsive.UltimateCharacterController.Camera.CameraController>();
                ucc.InitCharacterOnAwake = false;
                ucc.Character = null;
                var selector = new SerializedObject(cameraObject.GetComponent<LitGameplayCameraModeController>());
                selector.FindProperty("initialMode").enumValueIndex = (int)GameplayCameraMode.Tactical;
                selector.ApplyModifiedPropertiesWithoutUndo();
                UnityEngine.Object.DestroyImmediate(arena.arenaCamera.gameObject);
                arena.arenaCamera = cameraObject.GetComponent<Camera>();
                if (cameraObject.GetComponent<AudioListener>() == null) cameraObject.AddComponent<AudioListener>();
            }
            var root = arena.gameObject;
            Ensure<TimeManager>(root);
            var manager = Ensure<RealTimeCombatManager>(root);
            var input = Ensure<RealTimeCombatInput>(root);
            var mobility = Ensure<CombatMobilityController>(root);
            var mobilitySource = source.GetComponentInChildren<CombatMobilityController>(true);
            if (mobilitySource != null)
            {
                var from = new SerializedObject(mobilitySource);
                var to = new SerializedObject(mobility);
                to.CopyFromSerializedProperty(from.FindProperty("dodge"));
                to.ApplyModifiedPropertiesWithoutUndo();
            }
            Ensure<CounterSkillCombatController>(root);
            Ensure<CombatCinematicPlaybackService>(root);
            Ensure<LightSkillCombatController>(root);
            Ensure<CombatLockOnCameraController>(root);
            Ensure<CombatImpactFeedbackController>(root);
            var skills = Ensure<SkillsManager>(root);
            var counterSource = source.GetComponentInChildren<CounterSkillCombatController>(true);
            if (counterSource != null)
            {
                var from = new SerializedObject(counterSource);
                var to = new SerializedObject(root.GetComponent<CounterSkillCombatController>());
                to.CopyFromSerializedProperty(from.FindProperty("defaultCounterSkill"));
                to.CopyFromSerializedProperty(from.FindProperty("availableSkills"));
                to.ApplyModifiedPropertiesWithoutUndo();
            }
            ConfigureCounter(root.GetComponent<CounterSkillCombatController>(), source);
            ConfigureFluidLaboratory(arena, root);
            arena.enemyAttackSkills = new[] { "Strike", "Sweep", "Followup" }
                .Select(name => AssetDatabase.LoadAssetAtPath<SkillSO>("Assets/Characters/3_Enemy/Juggernaut/Skill_Juggernaut_" + name + ".asset")).ToArray();
            if (arena.enemyAttackSkills.Any(skill => skill == null)) throw new InvalidOperationException("Juggernaut authored attack definitions missing.");
            var wheel = UnityEngine.Object.FindAnyObjectByType<SkillWheel>(FindObjectsInactive.Include);
            if (wheel == null) wheel = CreateWheel(skills);
            var data = new SerializedObject(input);
            data.FindProperty("skillWheel").objectReferenceValue = wheel;
            data.FindProperty("skillWheelCanvasGroup").objectReferenceValue = wheel.GetComponent<CanvasGroup>();
            data.FindProperty("skillWheelVisibleAlpha").floatValue = 1f;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(arena);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }
        finally { if (previous.Any(s => s.isLoaded && s.isActive)) EditorSceneManager.RestoreSceneManagerSetup(previous); }
    }

    private static T Ensure<T>(GameObject root) where T : Component => root.GetComponent<T>() ?? root.AddComponent<T>();

    private static void ConfigureCounter(CounterSkillCombatController counter, GameObject sessionRoot)
    {
        // The shipped prototype uses a scene Director. Wrap its unchanged clips
        // in an owned pooled rig for this independent laboratory.
        string folder = IluviliraeSetup.Folder;
        string skillPath = folder + "/CombatLab_CounterSkill.asset";
        string timelinePath = folder + "/CombatLab_CounterSkill.playable";
        string rigPath = folder + "/CombatLab_CounterRig.prefab";
        if (AssetDatabase.LoadAssetAtPath<CounterSkillSO>(skillPath) == null)
            AssetDatabase.CopyAsset("Assets/CombatRealTime/Counters/CounterSkill_TemporalRiposte.asset", skillPath);
        if (AssetDatabase.LoadAssetAtPath<TimelineAsset>(timelinePath) == null)
            AssetDatabase.CopyAsset("Assets/CombatRealTime/Counters/CounterSkill_TemporalRiposte.playable", timelinePath);
        var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(timelinePath);
        const string cameraKey = "CombatLab.CounterCamera";
        foreach (var track in timeline.GetOutputTracks().OfType<CinemachineTrack>())
            foreach (var clip in track.GetClips())
                if (clip.asset is CinemachineShot shot)
                {
                    shot.VirtualCamera.exposedName = new PropertyName(cameraKey);
                    EditorUtility.SetDirty(shot);
                }
        var rig = AssetDatabase.LoadAssetAtPath<CombatCinematicRig>(rigPath);
        if (rig == null)
        {
            var root = new GameObject("CombatLab_CounterRig", typeof(PlayableDirector), typeof(SignalReceiver), typeof(CombatCinematicRig));
            try
            {
                var director = root.GetComponent<PlayableDirector>();
                director.playOnAwake = false; director.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
                director.playableAsset = timeline;
                var cameraSource = sessionRoot.GetComponentInChildren<CounterSkillCameraRig>(true);
                if (cameraSource == null) throw new InvalidOperationException("Authored CounterSkill camera missing.");
                var camera = UnityEngine.Object.Instantiate(cameraSource.gameObject, root.transform);
                camera.SetActive(true);
                var data = new SerializedObject(root.GetComponent<CombatCinematicRig>());
                data.FindProperty("director").objectReferenceValue = director;
                data.FindProperty("signalReceiver").objectReferenceValue = root.GetComponent<SignalReceiver>();
                var bindings = data.FindProperty("cameraBindings"); bindings.arraySize = 1;
                bindings.GetArrayElementAtIndex(0).FindPropertyRelative("timelineCameraKey").stringValue = new PropertyName(cameraKey).ToString();
                bindings.GetArrayElementAtIndex(0).FindPropertyRelative("camera").objectReferenceValue = camera.GetComponent<CinemachineCamera>();
                data.ApplyModifiedPropertiesWithoutUndo();
                rig = PrefabUtility.SaveAsPrefabAsset(root, rigPath).GetComponent<CombatCinematicRig>();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        ConfigureRelativeCounterRig(rigPath, timeline);
        var skill = AssetDatabase.LoadAssetAtPath<CounterSkillSO>(skillPath);
        var settings = new SerializedObject(skill);
        settings.FindProperty("timeline").objectReferenceValue = timeline;
        settings.FindProperty("combatCinematicRigPrefab").objectReferenceValue = rig;
        settings.ApplyModifiedPropertiesWithoutUndo();
        var controller = new SerializedObject(counter);
        controller.FindProperty("defaultCounterSkill").objectReferenceValue = skill;
        var available = controller.FindProperty("availableSkills"); available.arraySize = 1;
        available.GetArrayElementAtIndex(0).objectReferenceValue = skill;
        controller.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(timeline);
    }

    private static void ConfigureRelativeCounterRig(string rigPath, TimelineAsset timeline)
    {
        var root = PrefabUtility.LoadPrefabContents(rigPath);
        try
        {
            var rig = root.GetComponent<CombatCinematicRig>();
            if (!rig.HasAuthoringStageLayout)
            {
                var player = new GameObject("AuthorPlayerReference").transform;
                var enemy = new GameObject("AuthorEnemyReference").transform;
                player.SetParent(root.transform, false); enemy.SetParent(root.transform, false);
                player.localPosition = Vector3.back * .85f;
                enemy.localPosition = Vector3.forward * .85f;
                enemy.localRotation = Quaternion.Euler(0, 180, 0);
                rig.ConfigureAuthoringStageLayout(root.transform, player, enemy);
                var camera = new SerializedObject(root.GetComponentInChildren<CounterSkillCameraRig>(true));
                camera.FindProperty("fixedStageFraming").boolValue = true;
                camera.ApplyModifiedPropertiesWithoutUndo();
                // ConfigureAuthoringStageLayout creates the runtime anchors.
                UnityEngine.Object.DestroyImmediate(player.gameObject);
                UnityEngine.Object.DestroyImmediate(enemy.gameObject);
            }
            PrefabUtility.SaveAsPrefabAsset(root, rigPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }

        var tracks = timeline.GetOutputTracks().OfType<AnimationTrack>().ToArray();
        var playerTrack = tracks.Single(t => t.name == "Player.Animator");
        var enemyTrack = tracks.Single(t => t.name == "Enemy.Animator");
        foreach (var track in new[] { playerTrack, enemyTrack })
        {
            track.trackOffset = TrackOffset.ApplySceneOffsets;
            var data = new SerializedObject(track);
            var clips = data.FindProperty("m_Clips");
            for (int i = 0; i < clips.arraySize; i++)
            {
                clips.GetArrayElementAtIndex(i).FindPropertyRelative("m_PreExtrapolationMode").intValue = (int)TimelineClip.ClipExtrapolation.Hold;
                clips.GetArrayElementAtIndex(i).FindPropertyRelative("m_PostExtrapolationMode").intValue = (int)TimelineClip.ClipExtrapolation.Hold;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(track);
        }
        var playerClip = playerTrack.GetClips().First();
        var animation = (AnimationPlayableAsset)playerClip.asset;
        var impact = AnimationUtility.GetAnimationEvents(animation.clip).Single(e => e.functionName == "ResolveCounterSkillImpact");
        double contact = playerClip.start + (impact.time - playerClip.clipIn) / playerClip.timeScale;
        foreach (var clip in enemyTrack.GetClips())
            if (clip.start == 0) clip.start = contact;
        double end = System.Math.Max(playerClip.end, enemyTrack.GetClips().Max(c => c.end));
        foreach (var track in timeline.GetOutputTracks().OfType<CinemachineTrack>())
        {
            foreach (var shot in track.GetClips()) shot.duration = end - shot.start;
            EditorUtility.SetDirty(track);
        }
        EditorUtility.SetDirty(timeline);
    }

    private static SkillWheel CreateWheel(SkillsManager skills)
    {
        var canvasObject = new GameObject("CombatLab_UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        var wheelObject = new GameObject("SkillWheel", typeof(RectTransform), typeof(CanvasGroup), typeof(SkillWheel));
        wheelObject.transform.SetParent(canvasObject.transform, false);
        var rect = wheelObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.85f, 0.25f);
        rect.anchoredPosition = Vector2.zero; rect.sizeDelta = new Vector2(360, 360);
        var group = wheelObject.GetComponent<CanvasGroup>();
        group.alpha = 0; group.interactable = group.blocksRaycasts = false;
        var wheel = wheelObject.GetComponent<SkillWheel>();
        var data = new SerializedObject(wheel);
        data.FindProperty("skillsManager").objectReferenceValue = skills;
        var slots = data.FindProperty("slots"); slots.arraySize = 8;
        for (int i = 0; i < 8; i++)
        {
            var slot = new GameObject("SkillSlot_" + i, typeof(RectTransform), typeof(CanvasGroup), typeof(SkillWheelSlot));
            slot.transform.SetParent(wheelObject.transform, false);
            float angle = Mathf.PI * 0.5f - i * Mathf.PI * 0.25f;
            var position = slot.GetComponent<RectTransform>();
            position.sizeDelta = new Vector2(100, 65);
            position.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 120;
            var text = new GameObject("SkillName", typeof(RectTransform), typeof(TextMeshProUGUI));
            text.transform.SetParent(slot.transform, false);
            var label = text.GetComponent<TextMeshProUGUI>();
            label.fontSize = 16; label.alignment = TextAlignmentOptions.Center;
            var tr = text.GetComponent<RectTransform>(); tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = tr.offsetMax = Vector2.zero;
            slots.GetArrayElementAtIndex(i).objectReferenceValue = slot.GetComponent<SkillWheelSlot>();
        }
        data.ApplyModifiedPropertiesWithoutUndo();
        return wheel;
    }

    public static void UpgradeEnemy()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(JuggernautV2Setup.ControllerPath);
        var machine = controller.layers[0].stateMachine;
        if (!machine.states.Any(s => s.state.name == "Knocked Out"))
        {
            var knockedOut = machine.AddState("Knocked Out", new Vector3(750, 140));
            knockedOut.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(CounterKnockoutSignalSetup.AnimationPath)
                ?? machine.states.First(s => s.state.name == "Hurt").state.motion;
            if (knockedOut.motion is AnimationClip clip) knockedOut.speed = clip.length / 2f;
            // The stun timer owns the exit; no automatic transition can end it early.
            EditorUtility.SetDirty(controller);
        }
        var locomotion = controller.layers[0].stateMachine.states.First(s => s.state.name == "Locomotion").state;
        var root = locomotion.motion as BlendTree;
        if (root.children.All(c => c.motion is AnimationClip))
        {
            var children = root.children;
            children[1].motion = Directional(controller, "Walk", (AnimationClip)children[1].motion);
            children[2].motion = Directional(controller, "Run", (AnimationClip)children[2].motion);
            root.children = children;
            EditorUtility.SetDirty(root);
        }
        // Authored events belong to the combat profile. Never replace them with
        // guessed percentages when this assistant is run a second time.
        EnsureFluidProfile();
        AssetDatabase.SaveAssets();
    }

    public const string FluidProfilePath = "Assets/Characters/3_Enemy/Juggernaut_v2/Juggernaut_v2_CombatProfile.asset";

    private static void EnsureFluidProfile()
    {
        var profile = AssetDatabase.LoadAssetAtPath<LitBrainsCombatProfile>(FluidProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<LitBrainsCombatProfile>();
            profile.attackStartDistance = 3.6f;
            profile.approachStopDistance = 1.7f;
            profile.attackFacingDegrees = 55f;
            profile.attackInterval = new Vector2(1.5f, 1.9f);
            string[] names = { "Strike", "Sweep", "Followup" };
            // Explicit clip frame markers, independently adjustable by the author.
            int[] strike = { 14, 17, 15 }, impact = { 17, 21, 18 }, recovery = { 22, 27, 23 };
            profile.attacks = new LitBrainsAttackProfile[3];
            for (int i = 0; i < 3; i++)
                profile.attacks[i] = new LitBrainsAttackProfile {
                    stateName = i == 0 ? "Attack" : "Attack_" + names[i],
                    clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(JuggernautV2Setup.Folder + "/Animations/Juggernaut_v2_" + names[i] + ".anim"),
                    reactionSeconds = (strike[i] - 7) / 30f, strikeSeconds = strike[i] / 30f,
                    impactSeconds = impact[i] / 30f, recoverySeconds = recovery[i] / 30f,
                    preparationAdvanceDistance = 1f, advanceDistance = .9f,
                    advanceSeconds = .2f, recoveryDurationSeconds = .2f };
            AssetDatabase.CreateAsset(profile, FluidProfilePath);
        }
        foreach (var attack in profile.attacks)
        {
            if (attack.clip == null) throw new InvalidOperationException("Missing authored Brains attack clip.");
            var old = AnimationUtility.GetAnimationEvents(attack.clip).Where(e => !new[] {
                "OpenBrainsReactionOpportunity", "BeginBrainsStrike", "ResolveBrainsAttackImpact", "BeginBrainsRecovery" }.Contains(e.functionName));
            AnimationUtility.SetAnimationEvents(attack.clip, old.Concat(new[] {
                new AnimationEvent { functionName = "OpenBrainsReactionOpportunity", time = attack.reactionSeconds },
                new AnimationEvent { functionName = "BeginBrainsStrike", time = attack.strikeSeconds },
                new AnimationEvent { functionName = "ResolveBrainsAttackImpact", time = attack.impactSeconds },
                new AnimationEvent { functionName = "BeginBrainsRecovery", time = attack.recoverySeconds }
            }).OrderBy(e => e.time).ToArray());
            EditorUtility.SetDirty(attack.clip);
        }
        var enemy = PrefabUtility.LoadPrefabContents(JuggernautV2Setup.PrefabPath);
        try
        {
            var brain = enemy.GetComponent<LitBrainsEnemy>();
            brain.combatProfile = profile;
            if (brain.VFX_KnockedOut == null)
                brain.VFX_KnockedOut = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/VFX_KnockedOut.prefab");
            PrefabUtility.SaveAsPrefabAsset(enemy, JuggernautV2Setup.PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(enemy); }
    }

    private static void ConfigureFluidLaboratory(LucianBrainsCombatArena arena, GameObject root)
    {
        if (arena.basicSkills == null) throw new InvalidOperationException("Missing basic skills.");
        for (int i = 0; i < arena.basicSkills.Length; i++)
        {
            string path = IluviliraeSetup.Folder + "/CombatLab_BasicSkill_" + (i + 1) + ".asset";
            var copy = AssetDatabase.LoadAssetAtPath<BasicSkillsSO>(path);
            if (copy == null)
            {
                AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(arena.basicSkills[i]), path);
                copy = AssetDatabase.LoadAssetAtPath<BasicSkillsSO>(path);
                copy.interruptionForce = i == 0 ? 20 : i == 1 ? 30 : 50;
                copy.maximumHitDistance = 2.6f;
                copy.impactFeedback.hitStopSeconds = i == 0 ? .025f : i == 1 ? .035f : .05f;
                var events = AnimationUtility.GetAnimationEvents(copy.AnimationClip);
                float impact = events.Where(e => e.functionName.Contains("Impact") || e.functionName.Contains("Damage"))
                    .Select(e => e.time / copy.AnimationClip.length).DefaultIfEmpty(.35f).Min();
                copy.presentation.chainNormalizedTime = Mathf.Max(copy.presentation.chainNormalizedTime, impact + .04f);
                copy.presentation.chainTransitionNormalizedTime = Mathf.Max(copy.presentation.chainTransitionNormalizedTime, impact + .08f);
                copy.presentation.mobilityCancelNormalizedTime = Mathf.Max(copy.presentation.mobilityCancelNormalizedTime, impact + .04f);
                EditorUtility.SetDirty(copy);
            }
            arena.basicSkills[i] = copy;
        }
        var mobility = new SerializedObject(root.GetComponent<CombatMobilityController>());
        mobility.FindProperty("mobilityInputBufferSeconds").floatValue = .15f;
        mobility.ApplyModifiedPropertiesWithoutUndo();
        var threshold = root.GetComponent<CombatHealthThresholdController>() ?? root.AddComponent<CombatHealthThresholdController>();
        var reactions = new SerializedObject(threshold);
        reactions.FindProperty("reactionTimeScale").floatValue = 1;
        reactions.ApplyModifiedPropertiesWithoutUndo();
        string rigPath = IluviliraeSetup.Folder + "/CombatLab_CounterRig.prefab";
        var rig = PrefabUtility.LoadPrefabContents(rigPath);
        try
        {
            var camera = new SerializedObject(rig.GetComponentInChildren<CounterSkillCameraRig>());
            if (camera.FindProperty("actorClearance").floatValue <= 0)
            {
                camera.FindProperty("actorClearance").floatValue = 1.8f;
                camera.FindProperty("openingOffset").vector3Value = new Vector3(2.2f, 1.7f, -4f);
                camera.FindProperty("impactOffset").vector3Value = new Vector3(-1.8f, 1.5f, -3.2f);
            }
            camera.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(rig, rigPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(rig); }
    }

    private static BlendTree Directional(AnimatorController controller, string gait, AnimationClip forward)
    {
        var tree = new BlendTree { name = "Juggernaut_v2_" + gait + "Directional", blendType = BlendTreeType.FreeformDirectional2D, blendParameter = "MoveX", blendParameterY = "MoveY" };
        AssetDatabase.AddObjectToAsset(tree, controller);
        tree.AddChild(forward, Vector2.up);
        string[] directions = { "FR", "R", "BR", "B", "BL", "L", "FL" };
        for (int i = 0; i < directions.Length; i++)
        {
            string path = JuggernautV2Setup.Folder + "/Animations/Juggernaut_v2_" + gait + "_" + directions[i] + ".anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                var source = ActorAnimationAudit.Source(ActorAnimationAudit.EnemySource(gait + "_" + directions[i]));
                clip = UnityEngine.Object.Instantiate(source);
                clip.name = "Juggernaut_v2_" + gait + "_" + directions[i];
                AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                AssetDatabase.CreateAsset(clip, path);
            }
            float angle = (i + 1) * Mathf.PI * 0.25f;
            tree.AddChild(clip, new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)));
        }
        EditorUtility.SetDirty(tree);
        return tree;
    }
}
