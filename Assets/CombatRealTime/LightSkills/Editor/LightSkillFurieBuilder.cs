#if UNITY_EDITOR
using System;
using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public static class LightSkillFurieBuilder
{
    private const string Root = "Assets/CombatRealTime/LightSkills";
    private const string TimelinePath = Root + "/LightSkill_1_Furie.playable";
    private const string PrefabPath = "Assets/Core/System/GameplaySessionRoot.prefab";
    private const string ControllerPath = "Assets/Characters/4_Animations/Player_Model.controller";
    private const string LightSkillPath = Root + "/LightSkill_Devastation.asset";

    [InitializeOnLoadMethod]
    private static void BuildMissingAssetsAfterReload()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath) != null)
            {
                return;
            }

            Build();
        };
    }

    [MenuItem("Lit/Combat/Build LightSkill 1 Furie")]
    public static void Build()
    {
        EnsureFolder(Root + "/Animations");
        EnsureFolder(Root + "/Signals");

        AnimationClip startClip = CreateClipCopy("LightSkill_1_Furie_Start_Temp", "Twinblades_attack02_Inplace.FBX", false);
        AnimationClip impulseClip = CreateClipCopy("LightSkill_1_Furie_Impulse_Temp", "Twinblades_dash01_Inplace.FBX", false);
        AnimationClip attackClip = CreateClipCopy("LightSkill_1_Furie_Attack_Temp", "Twinblades_attack05_Inplace.FBX", true);
        ConfigureAnimator(startClip, impulseClip, attackClip);

        SignalAsset startSignal = GetOrCreateSignal("LightSkill_1_Furie_Start");
        SignalAsset rearSignal = GetOrCreateSignal("LightSkill_1_Furie_RearShot");
        SignalAsset impulseSignal = GetOrCreateSignal("LightSkill_1_Furie_Impulse");
        TimelineAsset timeline = CreateTimeline(startSignal, rearSignal, impulseSignal);
        ConfigureGameplaySession(timeline, startSignal, rearSignal, impulseSignal);
        ConfigureLightSkill(timeline);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[LightSkill] Furie cinematic built.");
    }

    private static TimelineAsset CreateTimeline(SignalAsset start, SignalAsset rear, SignalAsset impulse)
    {
        TimelineAsset timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath);
        if (timeline != null)
        {
            return timeline;
        }

        timeline = ScriptableObject.CreateInstance<TimelineAsset>();
        timeline.name = "LightSkill_1_Furie";
        AssetDatabase.CreateAsset(timeline, TimelinePath);

        timeline.CreateTrack<AnimationTrack>(null, "Player.Animator");
        timeline.CreateTrack<AnimationTrack>(null, "Enemy.Animator");
        timeline.CreateTrack<AudioTrack>(null, "Audio.Start");
        timeline.CreateTrack<AudioTrack>(null, "Audio.Impulse");
        timeline.CreateTrack<AudioTrack>(null, "Audio.Impact");

        CinemachineTrack cameraTrack = timeline.CreateTrack<CinemachineTrack>(null, "Cinemachine");
        TimelineClip cameraClip = cameraTrack.CreateClip<CinemachineShot>();
        cameraClip.start = 0d;
        cameraClip.duration = 5d;
        ((CinemachineShot)cameraClip.asset).VirtualCamera.exposedName = new PropertyName("LightSkill_1_Furie.VirtualCamera");

        SignalTrack signalTrack = timeline.CreateTrack<SignalTrack>(null, "Signals");
        AddSignal(signalTrack, 0d, start);
        AddSignal(signalTrack, 2d, rear);
        AddSignal(signalTrack, 4d, impulse);
        EditorUtility.SetDirty(timeline);
        return timeline;
    }

    private static void ConfigureGameplaySession(
        TimelineAsset timeline,
        SignalAsset startSignal,
        SignalAsset rearSignal,
        SignalAsset impulseSignal)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform battleManager = root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(transform => transform.name == "BattleManager");
            if (battleManager == null)
            {
                throw new InvalidOperationException("BattleManager introuvable dans GameplaySessionRoot.");
            }

            PlayableDirector director = GetOrAdd<PlayableDirector>(battleManager.gameObject);
            GetOrAdd<LitTimelineCinemachineBridge>(battleManager.gameObject);
            SignalReceiver receiver = GetOrAdd<SignalReceiver>(battleManager.gameObject);
            LightSkillFurieSequenceDriver sequence = GetOrAdd<LightSkillFurieSequenceDriver>(battleManager.gameObject);
            LightSkillCombatController lightSkillController = battleManager.GetComponent<LightSkillCombatController>();
            if (lightSkillController == null)
            {
                throw new InvalidOperationException("LightSkillCombatController introuvable sur BattleManager.");
            }

            Transform cameraTransform = battleManager.Find("LightSkill_1_Furie_VirtualCamera");
            if (cameraTransform == null)
            {
                cameraTransform = new GameObject("LightSkill_1_Furie_VirtualCamera").transform;
                cameraTransform.SetParent(battleManager, false);
            }

            CinemachineCamera virtualCamera = GetOrAdd<CinemachineCamera>(cameraTransform.gameObject);
            PrioritySettings priority = virtualCamera.Priority;
            priority.Value = 0;
            virtualCamera.Priority = priority;
            LightSkillFurieCameraRig cameraRig = GetOrAdd<LightSkillFurieCameraRig>(cameraTransform.gameObject);

            Assign(sequence, "virtualCamera", virtualCamera);
            Assign(sequence, "cameraRig", cameraRig);
            Assign(sequence, "signalReceiver", receiver);
            Assign(lightSkillController, "furieSequence", sequence);
            director.playableAsset = timeline;

            ConfigureReaction(receiver, startSignal, sequence, sequence.BeginFurieStart);
            ConfigureReaction(receiver, rearSignal, sequence, sequence.SetFurieRearShot);
            ConfigureReaction(receiver, impulseSignal, sequence, sequence.BeginFurieImpulse);

            SignalTrack signalTrack = timeline.GetOutputTracks().OfType<SignalTrack>().FirstOrDefault();
            if (signalTrack != null)
            {
                director.SetGenericBinding(signalTrack, receiver);
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigureLightSkill(TimelineAsset timeline)
    {
        LightSkillSO skill = AssetDatabase.LoadAssetAtPath<LightSkillSO>(LightSkillPath);
        if (skill == null)
        {
            throw new InvalidOperationException("LightSkill_Devastation introuvable.");
        }

        SerializedObject serialized = new SerializedObject(skill);
        serialized.FindProperty("timeline").objectReferenceValue = timeline;
        serialized.FindProperty("cinemachineTrackName").stringValue = "Cinemachine";
        serialized.FindProperty("maximumCinematicStartDistance").floatValue = 18f;
        serialized.FindProperty("resolveDamageWhenTimelineStops").boolValue = false;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(skill);
    }

    private static void ConfigureAnimator(AnimationClip start, AnimationClip impulse, AnimationClip attack)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            throw new InvalidOperationException("Player_Model.controller introuvable.");
        }

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        UpsertState(stateMachine, "LightSkill_1_Furie_Start_Temp", start);
        UpsertState(stateMachine, "LightSkill_1_Furie_Impulse_Temp", impulse);
        UpsertState(stateMachine, "LightSkill_1_Furie_Attack_Temp", attack);
        EditorUtility.SetDirty(controller);
    }

    private static AnimationClip CreateClipCopy(string destinationName, string sourceFileName, bool addImpactEvent)
    {
        string destinationPath = Root + "/Animations/" + destinationName + ".anim";
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(destinationPath);
        if (clip == null)
        {
            AnimationClip source = FindClip(sourceFileName);
            if (source == null)
            {
                throw new InvalidOperationException("Clip source introuvable: " + sourceFileName);
            }

            clip = UnityEngine.Object.Instantiate(source);
            clip.name = destinationName;
            AssetDatabase.CreateAsset(clip, destinationPath);
        }

        if (addImpactEvent)
        {
            AnimationUtility.SetAnimationEvents(clip, new[]
            {
                new AnimationEvent
                {
                    functionName = "ResolveLightSkillImpact",
                    time = Mathf.Max(0.01f, clip.length * 0.55f)
                }
            });
        }

        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static AnimationClip FindClip(string sourceFileName)
    {
        string path = AssetDatabase.FindAssets(System.IO.Path.GetFileNameWithoutExtension(sourceFileName))
            .Select(AssetDatabase.GUIDToAssetPath)
            .FirstOrDefault(candidate => candidate.EndsWith(sourceFileName, StringComparison.OrdinalIgnoreCase));
        return string.IsNullOrEmpty(path)
            ? null
            : AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(clip => !clip.name.StartsWith("__preview__"));
    }

    private static SignalAsset GetOrCreateSignal(string name)
    {
        string path = Root + "/Signals/" + name + ".asset";
        SignalAsset signal = AssetDatabase.LoadAssetAtPath<SignalAsset>(path);
        if (signal == null)
        {
            signal = ScriptableObject.CreateInstance<SignalAsset>();
            signal.name = name;
            AssetDatabase.CreateAsset(signal, path);
        }

        return signal;
    }

    private static void AddSignal(SignalTrack track, double time, SignalAsset signal)
    {
        SignalEmitter marker = track.CreateMarker<SignalEmitter>(time);
        marker.asset = signal;
        marker.emitOnce = true;
    }

    private static void ConfigureReaction(SignalReceiver receiver, SignalAsset signal, LightSkillFurieSequenceDriver target, UnityAction action)
    {
        UnityEvent reaction = receiver.GetReaction(signal);
        if (reaction == null)
        {
            reaction = new UnityEvent();
            receiver.AddReaction(signal, reaction);
        }

        for (int index = reaction.GetPersistentEventCount() - 1; index >= 0; index--)
        {
            UnityEventTools.RemovePersistentListener(reaction, index);
        }
        UnityEventTools.AddPersistentListener(reaction, action);
        EditorUtility.SetDirty(receiver);
    }

    private static void UpsertState(AnimatorStateMachine stateMachine, string name, AnimationClip clip)
    {
        ChildAnimatorState child = stateMachine.states.FirstOrDefault(state => state.state.name == name);
        AnimatorState state = child.state;
        if (state == null)
        {
            state = stateMachine.AddState(name, new Vector3(700f, 220f + stateMachine.states.Length * 45f));
        }

        state.motion = clip;
        state.tag = "RealTimeCombatRootMotion";
    }

    private static T GetOrAdd<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }

    private static void Assign(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            throw new InvalidOperationException("Propriete introuvable: " + propertyName);
        }

        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
        {
            return;
        }

        string parent = System.IO.Path.GetDirectoryName(folder)?.Replace('\\', '/');
        string name = System.IO.Path.GetFileName(folder);
        if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(name))
        {
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
