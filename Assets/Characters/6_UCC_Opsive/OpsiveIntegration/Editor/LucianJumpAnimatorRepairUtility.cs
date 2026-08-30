using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

[InitializeOnLoad]
public static class LucianJumpAnimatorRepairUtility
{
    private const string ControllerPath = "Assets/Characters/4_Animations/Player_Model.controller";
    private const string LegacyControllerPath = "Assets/FallingPhase_Legacy/Animations/Player_Model_Falling.controller";
    private const string SessionRepairKey = "Lit.LucianJumpAnimatorRepair.Completed.DescentFeelV4";

    static LucianJumpAnimatorRepairUtility()
    {
        if (!SessionState.GetBool(SessionRepairKey, false))
        {
            EditorApplication.delayCall += RepairOnceAfterCompilation;
        }
    }

    private static void RepairOnceAfterCompilation()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        Repair();
        SessionState.SetBool(SessionRepairKey, true);
    }

    [MenuItem("Lit/Animation/Repair Lucian Jump Animator")]
    public static void Repair()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError("Lucian jump repair could not load " + ControllerPath + ".");
            return;
        }

        AnimatorStateMachine baseLayer = controller.layers[0].stateMachine;
        AnimatorState locomotion = FindState(baseLayer, "Locomotion");
        AnimatorState jumpStart = FindState(baseLayer, "Jump_Start");
        AnimatorState jumpLoop = FindState(baseLayer, "Jump_Loop");
        AnimatorState falling = FindState(baseLayer, "Falling");
        AnimatorState jumpEnd = FindState(baseLayer, "Jump_End");
        AnimatorState hardLanding = FindState(baseLayer, "Landing_Hard");
        AnimatorState jumpRoll = FindState(baseLayer, "Jump_Roll");
        AnimatorController legacyController = AssetDatabase.LoadAssetAtPath<AnimatorController>(LegacyControllerPath);
        AnimatorState legacyFallingLoop = legacyController != null
            ? FindState(legacyController.layers[0].stateMachine, "Falling_Loop")
            : null;
        if (locomotion == null || jumpStart == null || jumpLoop == null || falling == null || jumpEnd == null || hardLanding == null || jumpRoll == null || legacyFallingLoop == null || legacyFallingLoop.motion == null)
        {
            Debug.LogError("Lucian jump repair could not resolve the active or archived Falling_Loop animation.");
            return;
        }

        EnsureParameter(controller, "JumpStartTrigger", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "LandingTrigger", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "JumpRollTrigger", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "JumpPresentationActive", AnimatorControllerParameterType.Bool);
        EnsureParameter(controller, "IsAirborne", AnimatorControllerParameterType.Bool);
        EnsureParameter(controller, "JumpPhase", AnimatorControllerParameterType.Int);
        EnsureParameter(controller, "LandingType", AnimatorControllerParameterType.Int);

        ClearTransitions(jumpStart);
        ClearTransitions(jumpLoop);
        ClearTransitions(falling);
        ClearTransitions(jumpEnd);
        ClearTransitions(hardLanding);
        ClearTransitions(jumpRoll);
        ClearJumpAnyStateTransitions(baseLayer);

        AddAnyStateTransition(baseLayer, jumpStart, "JumpStartTrigger", "JumpPresentationActive", 0f);
        AnimatorStateTransition startToLoop = jumpStart.AddTransition(jumpLoop);
        // Keep a visible anticipation phase even though UCC has already
        // accepted the input. The jump stays responsive physically while the
        // presentation carries the weight of the takeoff into Jump_Loop.
        startToLoop.hasExitTime = true;
        startToLoop.exitTime = 0.38f;
        startToLoop.hasFixedDuration = true;
        startToLoop.duration = 0.08f;
        startToLoop.AddCondition(AnimatorConditionMode.If, 0f, "IsAirborne");
        // The legacy falling loop is only an animation resource. Its former
        // falling-mode scripts remain archived and are never installed here.
        falling.motion = legacyFallingLoop.motion;
        // The two clips have different airborne silhouettes. Give the pose
        // enough time to blend at the physical descent threshold instead of
        // snapping from the apex into the fall.
        AddPhaseTransition(jumpLoop, falling, 3, 0.18f);

        AddLandingTransition(baseLayer, jumpEnd, AnimatorConditionMode.Less, 0.5f);
        AddLandingTransition(baseLayer, hardLanding, AnimatorConditionMode.Greater, 0.5f);
        AddRollTransition(baseLayer, jumpRoll);
        AddExitTransition(jumpEnd, locomotion, 0.82f, 0.12f);
        AddExitTransition(hardLanding, locomotion, 0.82f, 0.12f);
        AddExitTransition(jumpRoll, locomotion, 0.82f, 0.1f);

        AddTraceBehaviour(jumpStart);
        AddTraceBehaviour(jumpLoop);
        AddTraceBehaviour(falling);
        AddTraceBehaviour(jumpEnd);
        AddTraceBehaviour(hardLanding);
        AddTraceBehaviour(jumpRoll);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(ControllerPath, ImportAssetOptions.ForceUpdate);
        Validate();
        Debug.Log("Lucian jump Animator repaired through the Unity AnimatorController API.");
    }

    [MenuItem("Lit/Animation/Validate Lucian Jump Animator")]
    public static void Validate()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        AnimatorStateMachine baseLayer = controller != null ? controller.layers[0].stateMachine : null;
        bool parametersValid = controller != null && new[] { "JumpStartTrigger", "LandingTrigger", "JumpRollTrigger", "JumpPresentationActive", "IsAirborne", "JumpPhase", "LandingType" }
            .All(name => controller.parameters.Any(parameter => parameter.name == name));
        bool statesValid = baseLayer != null && new[] { "Jump_Start", "Jump_Loop", "Falling", "Jump_End", "Landing_Hard", "Jump_Roll", "Locomotion" }
            .All(name => FindState(baseLayer, name) != null);
        AnimatorState jumpLoop = baseLayer != null ? FindState(baseLayer, "Jump_Loop") : null;
        AnimatorState falling = baseLayer != null ? FindState(baseLayer, "Falling") : null;
        bool fallingRouteValid = jumpLoop != null && falling != null && jumpLoop.transitions.Any(transition =>
            transition.destinationState == falling &&
            transition.conditions.Any(condition => condition.parameter == "JumpPhase" &&
                                                 condition.mode == AnimatorConditionMode.Equals &&
                                                 Mathf.Approximately(condition.threshold, 3f)));
        bool landingRouteValid = baseLayer != null && baseLayer.anyStateTransitions.Any(transition =>
            transition.destinationState != null && transition.destinationState.name == "Jump_End" &&
            transition.conditions.Any(condition => condition.parameter == "LandingTrigger"));
        bool rollRouteValid = baseLayer != null && baseLayer.anyStateTransitions.Any(transition =>
            transition.destinationState != null && transition.destinationState.name == "Jump_Roll" &&
            transition.conditions.Any(condition => condition.parameter == "JumpRollTrigger"));

        if (!parametersValid || !statesValid || !fallingRouteValid || !landingRouteValid || !rollRouteValid)
        {
            Debug.LogError("Lucian jump Animator validation failed. Run Lit/Animation/Repair Lucian Jump Animator.");
            return;
        }

        Debug.Log("Lucian jump Animator validation passed.");
    }

    private static AnimatorState FindState(AnimatorStateMachine stateMachine, string name)
    {
        return stateMachine.states.Select(child => child.state).FirstOrDefault(state => state != null && state.name == name);
    }

    private static void EnsureParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
    {
        if (!controller.parameters.Any(parameter => parameter.name == name))
        {
            controller.AddParameter(name, type);
        }
    }

    private static void ClearTransitions(AnimatorState state)
    {
        foreach (AnimatorStateTransition transition in state.transitions.ToArray())
        {
            state.RemoveTransition(transition);
        }
    }

    private static void ClearJumpAnyStateTransitions(AnimatorStateMachine stateMachine)
    {
        foreach (AnimatorStateTransition transition in stateMachine.anyStateTransitions.ToArray())
        {
            if (transition.conditions.Any(condition =>
                    condition.parameter == "JumpTrigger" ||
                    condition.parameter == "JumpStartTrigger" ||
                    condition.parameter == "LandingTrigger" ||
                    condition.parameter == "JumpRollTrigger"))
            {
                stateMachine.RemoveAnyStateTransition(transition);
            }
        }
    }

    private static void AddAnyStateTransition(AnimatorStateMachine stateMachine, AnimatorState destination, string trigger, string extraParameter, float extraThreshold)
    {
        AnimatorStateTransition transition = stateMachine.AddAnyStateTransition(destination);
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = 0.05f;
        transition.AddCondition(AnimatorConditionMode.If, 0f, trigger);
        if (!string.IsNullOrEmpty(extraParameter))
        {
            transition.AddCondition(AnimatorConditionMode.If, extraThreshold, extraParameter);
        }
    }

    private static void AddLandingTransition(AnimatorStateMachine stateMachine, AnimatorState destination, AnimatorConditionMode landingTypeMode, float landingTypeThreshold)
    {
        AnimatorStateTransition transition = stateMachine.AddAnyStateTransition(destination);
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = 0.05f;
        transition.AddCondition(AnimatorConditionMode.If, 0f, "LandingTrigger");
        transition.AddCondition(AnimatorConditionMode.If, 0f, "JumpPresentationActive");
        transition.AddCondition(landingTypeMode, landingTypeThreshold, "LandingType");
    }

    private static void AddRollTransition(AnimatorStateMachine stateMachine, AnimatorState destination)
    {
        AnimatorStateTransition transition = stateMachine.AddAnyStateTransition(destination);
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = 0.05f;
        transition.AddCondition(AnimatorConditionMode.If, 0f, "JumpRollTrigger");
        transition.AddCondition(AnimatorConditionMode.If, 0f, "JumpPresentationActive");
        transition.AddCondition(AnimatorConditionMode.Less, 0.5f, "LandingType");
    }

    private static void AddPhaseTransition(AnimatorState source, AnimatorState destination, int phase, float duration)
    {
        AnimatorStateTransition transition = source.AddTransition(destination);
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = duration;
        transition.AddCondition(AnimatorConditionMode.Equals, phase, "JumpPhase");
    }

    private static void AddExitTransition(AnimatorState source, AnimatorState destination, float exitTime, float duration)
    {
        AnimatorStateTransition transition = source.AddTransition(destination);
        transition.hasExitTime = true;
        transition.exitTime = exitTime;
        transition.hasFixedDuration = true;
        transition.duration = duration;
    }

    private static void AddTraceBehaviour(AnimatorState state)
    {
        if (!state.behaviours.Any(behaviour => behaviour is LucianJumpStateTraceBehaviour))
        {
            state.AddStateMachineBehaviour<LucianJumpStateTraceBehaviour>();
        }
    }
}
