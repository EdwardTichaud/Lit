using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

[InitializeOnLoad]
public static class LucianJumpAnimatorRepairUtility
{
    private const string ControllerPath = "Assets/Characters/4_Animations/Player_Model.controller";
    private const string SessionRepairKey = "Lit.LucianJumpAnimatorRepair.Completed";

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
        AnimatorState landing = FindState(baseLayer, "Landing");
        AnimatorState hardLanding = FindState(baseLayer, "Landing_Hard");
        if (locomotion == null || jumpStart == null || jumpLoop == null || falling == null || landing == null || hardLanding == null)
        {
            Debug.LogError("Lucian jump repair could not resolve every required Animator state.");
            return;
        }

        EnsureParameter(controller, "JumpStartTrigger", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "LandingTrigger", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "JumpPresentationActive", AnimatorControllerParameterType.Bool);
        EnsureParameter(controller, "IsAirborne", AnimatorControllerParameterType.Bool);
        EnsureParameter(controller, "JumpPhase", AnimatorControllerParameterType.Int);
        EnsureParameter(controller, "LandingType", AnimatorControllerParameterType.Int);

        ClearTransitions(jumpStart);
        ClearTransitions(jumpLoop);
        ClearTransitions(falling);
        ClearTransitions(landing);
        ClearTransitions(hardLanding);
        ClearJumpAnyStateTransitions(baseLayer);

        AddAnyStateTransition(baseLayer, jumpStart, "JumpStartTrigger", "JumpPresentationActive", 0f);
        AnimatorStateTransition startToLoop = jumpStart.AddTransition(jumpLoop);
        startToLoop.hasExitTime = false;
        startToLoop.hasFixedDuration = true;
        startToLoop.duration = 0.05f;
        startToLoop.AddCondition(AnimatorConditionMode.If, 0f, "IsAirborne");

        AnimatorStateTransition startToFalling = jumpStart.AddTransition(falling);
        startToFalling.hasExitTime = false;
        startToFalling.hasFixedDuration = true;
        startToFalling.duration = 0.05f;
        startToFalling.AddCondition(AnimatorConditionMode.Equals, 3f, "JumpPhase");

        AnimatorStateTransition loopToFalling = jumpLoop.AddTransition(falling);
        loopToFalling.hasExitTime = false;
        loopToFalling.hasFixedDuration = true;
        loopToFalling.duration = 0.05f;
        loopToFalling.AddCondition(AnimatorConditionMode.Equals, 3f, "JumpPhase");

        AddLandingTransition(baseLayer, landing, AnimatorConditionMode.Less, 0.5f);
        AddLandingTransition(baseLayer, hardLanding, AnimatorConditionMode.Greater, 0.5f);
        AddExitTransition(landing, locomotion, 0.28f, 0.08f);
        AddExitTransition(hardLanding, locomotion, 0.65f, 0.1f);

        AddTraceBehaviour(jumpStart);
        AddTraceBehaviour(jumpLoop);
        AddTraceBehaviour(falling);
        AddTraceBehaviour(landing);
        AddTraceBehaviour(hardLanding);

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
        bool parametersValid = controller != null && new[] { "JumpStartTrigger", "LandingTrigger", "JumpPresentationActive", "IsAirborne", "JumpPhase", "LandingType" }
            .All(name => controller.parameters.Any(parameter => parameter.name == name));
        bool statesValid = baseLayer != null && new[] { "Jump_Start", "Jump_Loop", "Falling", "Landing", "Landing_Hard", "Locomotion" }
            .All(name => FindState(baseLayer, name) != null);
        bool landingRouteValid = baseLayer != null && baseLayer.anyStateTransitions.Any(transition =>
            transition.destinationState != null && transition.destinationState.name == "Landing" &&
            transition.conditions.Any(condition => condition.parameter == "LandingTrigger"));

        if (!parametersValid || !statesValid || !landingRouteValid)
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
                    condition.parameter == "LandingTrigger"))
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
