using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

[InitializeOnLoad]
public static class LadderAnimatorSetup
{
    private const string PlayerControllerPath = "Assets/Animations/Player_Model.controller";
    private const string BuilderControllerPath = "Assets/Animations/Builder_Model_Trooper.controller";
    private const string StartClipPath = "Assets/Animations/Mixamo_Ladder_Start_1.fbx";
    private const string LoopClipPath = "Assets/Animations/Mixamo_Ladder_Loop.fbx";
    private const string EndClipPath = "Assets/Animations/Mixamo_Ladder_End.fbx";
    private const string StartClipName = "Mixamo_Ladder_Start_1";
    private const string LoopClipName = "Mixamo_Ladder_Loop";
    private const string EndClipName = "Mixamo_Ladder_End";
    private const string SessionAppliedKey = "Lit.LadderAnimatorSetup.Applied";

    static LadderAnimatorSetup()
    {
        EditorApplication.delayCall -= SetupAutomatically;
        EditorApplication.delayCall += SetupAutomatically;
    }

    private static void SetupAutomatically()
    {
        if (SessionState.GetBool(SessionAppliedKey, false))
        {
            return;
        }

        SessionState.SetBool(SessionAppliedKey, true);
        Setup();
    }

    [MenuItem("Tools/Lit/Setup Ladder Animator States")]
    public static void Setup()
    {
        ConfigureImportedClipLooping(StartClipPath, StartClipName, false);
        ConfigureImportedClipLooping(LoopClipPath, LoopClipName, true);
        ConfigureImportedClipLooping(EndClipPath, EndClipName, false);

        AnimationClip startClip = LoadClip(StartClipPath, StartClipName);
        AnimationClip loopClip = LoadClip(LoopClipPath, LoopClipName);
        AnimationClip endClip = LoadClip(EndClipPath, EndClipName);

        SetupController(PlayerControllerPath, startClip, loopClip, endClip);
        SetupController(BuilderControllerPath, startClip, loopClip, endClip);

        AssetDatabase.SaveAssets();
        Debug.Log("[LadderAnimatorSetup] Animator ladder states configured.");
    }

    private static void SetupController(string controllerPath, AnimationClip startClip, AnimationClip loopClip, AnimationClip endClip)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
        {
            Debug.LogWarning($"[LadderAnimatorSetup] AnimatorController introuvable: {controllerPath}");
            return;
        }

        EnsureParameter(controller, "LadderStartTrigger", AnimatorControllerParameterType.Trigger, 0f, 0, false);
        EnsureParameter(controller, "LadderEndTrigger", AnimatorControllerParameterType.Trigger, 0f, 0, false);
        EnsureParameter(controller, "IsClimbingLadder", AnimatorControllerParameterType.Bool, 0f, 0, false);
        EnsureParameter(controller, "LadderDirection", AnimatorControllerParameterType.Float, 1f, 0, false);
        EnsureParameter(controller, "LadderPhase", AnimatorControllerParameterType.Int, 0f, 0, false);
        EnsureParameter(controller, "LadderProgress", AnimatorControllerParameterType.Float, 0f, 0, false);

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        AnimatorState startState = EnsureState(stateMachine, "Ladder_Start", new Vector3(960f, 80f, 0f));
        AnimatorState loopState = EnsureState(stateMachine, "Ladder_Loop", new Vector3(1220f, 80f, 0f));
        AnimatorState endState = EnsureState(stateMachine, "Ladder_End", new Vector3(1480f, 80f, 0f));

        ConfigureState(startState, startClip);
        ConfigureState(loopState, loopClip);
        ConfigureState(endState, endClip);

        EnsureAnyStateTransition(stateMachine, startState, "LadderStartTrigger");
        EnsureConditionTransition(startState, loopState, "LadderPhase", AnimatorConditionMode.Equals, 3f, 0.05f);
        EnsureConditionTransition(loopState, endState, "LadderEndTrigger", AnimatorConditionMode.If, 0f, 0.05f);
        EnsureConditionTransition(endState, ResolveRecoveryState(stateMachine), "IsClimbingLadder", AnimatorConditionMode.IfNot, 0f, 0.08f);

        EditorUtility.SetDirty(controller);
        Debug.Log($"[LadderAnimatorSetup] Configured {controllerPath}");
    }

    private static void ConfigureImportedClipLooping(string clipPath, string clipName, bool loopTime)
    {
        ModelImporter importer = AssetImporter.GetAtPath(clipPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogWarning($"[LadderAnimatorSetup] ModelImporter introuvable: {clipPath}");
            return;
        }

        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
        {
            clips = importer.defaultClipAnimations;
        }

        bool changed = false;
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] == null || clips[i].name != clipName)
            {
                continue;
            }

            if (clips[i].loopTime != loopTime || clips[i].wrapMode != (loopTime ? WrapMode.Loop : WrapMode.Default))
            {
                clips[i].loopTime = loopTime;
                clips[i].wrapMode = loopTime ? WrapMode.Loop : WrapMode.Default;
                changed = true;
            }
        }

        if (!changed)
        {
            return;
        }

        importer.clipAnimations = clips;
        importer.SaveAndReimport();
    }

    private static AnimationClip LoadClip(string path, string clipName)
    {
        AnimationClip clip = AssetDatabase
            .LoadAllAssetsAtPath(path)
            .OfType<AnimationClip>()
            .FirstOrDefault(candidate => candidate != null && candidate.name == clipName);

        if (clip == null)
        {
            Debug.LogWarning($"[LadderAnimatorSetup] AnimationClip introuvable: {path} / {clipName}");
        }

        return clip;
    }

    private static void EnsureParameter(
        AnimatorController controller,
        string name,
        AnimatorControllerParameterType type,
        float defaultFloat,
        int defaultInt,
        bool defaultBool)
    {
        AnimatorControllerParameter parameter = controller.parameters.FirstOrDefault(p => p.name == name);
        if (parameter != null)
        {
            if (parameter.type != type)
            {
                Debug.LogWarning($"[LadderAnimatorSetup] Parametre '{name}' existe avec le type {parameter.type}; type attendu {type}.");
            }
            return;
        }

        controller.AddParameter(new AnimatorControllerParameter
        {
            name = name,
            type = type,
            defaultFloat = defaultFloat,
            defaultInt = defaultInt,
            defaultBool = defaultBool,
        });
    }

    private static AnimatorState EnsureState(AnimatorStateMachine stateMachine, string name, Vector3 position)
    {
        ChildAnimatorState existing = stateMachine.states.FirstOrDefault(state => state.state != null && state.state.name == name);
        if (existing.state != null)
        {
            return existing.state;
        }

        return stateMachine.AddState(name, position);
    }

    private static void ConfigureState(AnimatorState state, Motion motion)
    {
        if (state == null)
        {
            return;
        }

        state.motion = motion;
        state.speed = 1f;
        state.writeDefaultValues = true;
        state.iKOnFeet = false;
    }

    private static void EnsureAnyStateTransition(AnimatorStateMachine stateMachine, AnimatorState destination, string triggerName)
    {
        if (stateMachine == null || destination == null)
        {
            return;
        }

        AnimatorStateTransition transition = stateMachine.anyStateTransitions.FirstOrDefault(candidate =>
            candidate != null &&
            candidate.destinationState == destination &&
            HasCondition(candidate, triggerName, AnimatorConditionMode.If));

        if (transition == null)
        {
            transition = stateMachine.AddAnyStateTransition(destination);
            transition.AddCondition(AnimatorConditionMode.If, 0f, triggerName);
        }

        ConfigureTransition(transition, hasExitTime: false, duration: 0.05f);
        transition.canTransitionToSelf = false;
    }

    private static void EnsureConditionTransition(
        AnimatorState source,
        AnimatorState destination,
        string parameterName,
        AnimatorConditionMode mode,
        float threshold,
        float duration)
    {
        if (source == null || destination == null)
        {
            return;
        }

        AnimatorStateTransition transition = source.transitions.FirstOrDefault(candidate =>
            candidate != null &&
            candidate.destinationState == destination &&
            HasCondition(candidate, parameterName, mode));

        if (transition == null)
        {
            transition = source.AddTransition(destination);
            transition.AddCondition(mode, threshold, parameterName);
        }

        ConfigureTransition(transition, hasExitTime: false, duration: duration);
        transition.canTransitionToSelf = false;
    }

    private static void ConfigureTransition(AnimatorStateTransition transition, bool hasExitTime, float duration)
    {
        if (transition == null)
        {
            return;
        }

        transition.hasExitTime = hasExitTime;
        transition.exitTime = hasExitTime ? 0.95f : 0f;
        transition.duration = duration;
        transition.offset = 0f;
        transition.hasFixedDuration = true;
        transition.interruptionSource = TransitionInterruptionSource.None;
        transition.orderedInterruption = true;
    }

    private static bool HasCondition(AnimatorStateTransition transition, string parameterName, AnimatorConditionMode mode)
    {
        return transition.conditions.Any(condition =>
            condition.parameter == parameterName &&
            condition.mode == mode);
    }

    private static AnimatorState ResolveRecoveryState(AnimatorStateMachine stateMachine)
    {
        AnimatorState state = FindState(stateMachine, "Locomotion");
        if (state != null)
        {
            return state;
        }

        state = FindState(stateMachine, "Idle");
        if (state != null)
        {
            return state;
        }

        return stateMachine.defaultState;
    }

    private static AnimatorState FindState(AnimatorStateMachine stateMachine, string name)
    {
        return stateMachine.states
            .Select(state => state.state)
            .FirstOrDefault(state => state != null && state.name == name);
    }
}
