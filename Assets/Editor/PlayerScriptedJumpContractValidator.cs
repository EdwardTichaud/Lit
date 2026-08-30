using System.Collections.Generic;
using System.Linq;
using Opsive.UltimateCharacterController.Character;
using Opsive.UltimateCharacterController.Character.Abilities;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>Non-mutating audit for the shared in-place player jump contract.</summary>
public static class PlayerScriptedJumpContractValidator
{
    private const string ControllerPath = "Assets/Characters/4_Animations/Player_Model.controller";
    private static readonly string[] PrefabPaths = {
        "Assets/Characters/1_Squad/Lucian/Player_Model_Lucian.prefab",
        "Assets/Characters/1_Squad/Link/Player_Model_Link.prefab",
        "Assets/Characters/1_Squad/Luna/Player_Model_Luna.prefab",
        "Assets/Characters/1_Squad/Mia/Player_Model_Mia.prefab"
    };

    [MenuItem("Lit/Animation/Validate Player Scripted Jump Contract")]
    public static void Validate()
    {
        List<string> failures = new List<string>();
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null) failures.Add("Player_Model.controller is missing.");
        else ValidateController(controller, failures);
        foreach (string path in PrefabPaths) ValidatePrefab(path, controller, failures);

        if (failures.Count == 0)
            Debug.Log("[Player Scripted Jump] Contract valid: 4 heroes, in-place chain, no UCC Jump ability or legacy references.");
        else
            Debug.LogError("[Player Scripted Jump] Contract invalid (" + failures.Count + "):\n- " + string.Join("\n- ", failures));
    }

    private static void ValidatePrefab(string path, RuntimeAnimatorController controller, List<string> failures)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            PlayerScriptedJumpController jumpController = root.GetComponent<PlayerScriptedJumpController>();
            if (jumpController == null) failures.Add(path + ": PlayerScriptedJumpController missing.");
            else if (!Mathf.Approximately(jumpController.TargetJumpHeight, 5f)) failures.Add(path + ": target jump height must be 5.");
            UltimateCharacterLocomotion locomotion = root.GetComponent<UltimateCharacterLocomotion>();
            if (locomotion == null) failures.Add(path + ": UltimateCharacterLocomotion missing.");
            else if ((locomotion.Abilities ?? new Ability[0]).Any(ability => ability is Jump)) failures.Add(path + ": legacy UCC Jump ability is still configured.");
            Animator animator = root.GetComponentInChildren<Animator>();
            if (animator == null || animator.runtimeAnimatorController != controller) failures.Add(path + ": shared Player_Model.controller is not the active animator controller.");
            if (AssetDatabase.GetDependencies(path, true).Any(dependency => dependency.Contains("FallingPhase_Legacy"))) failures.Add(path + ": references FallingPhase_Legacy.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void ValidateController(AnimatorController controller, List<string> failures)
    {
        AnimatorStateMachine root = controller.layers[0].stateMachine;
        ValidateMotion(root, "Jump_Start", "jump_start", failures);
        ValidateMotion(root, "Jump_Loop", "jump", failures);
        ValidateMotion(root, "Falling", "jump_falling_loop", failures);
        ValidateMotion(root, "Jump_End", "jump_landing", failures);
        ValidateMotion(root, "Landing_Hard", "Mixamo_Landing_Hard_Inplace", failures);
        if (FindState(root, "Jump_Start_Back") == null) failures.Add("Player_Model.controller is missing Jump_Start_Back.");
        if (FindState(root, "Jump_Roll") != null) failures.Add("Player_Model.controller still contains Jump_Roll.");
        if (controller.parameters.Any(parameter => parameter.name == "JumpRollTrigger")) failures.Add("Player_Model.controller still contains JumpRollTrigger.");
        if (AssetDatabase.GetDependencies(ControllerPath, true).Any(dependency => dependency.Contains("FallingPhase_Legacy"))) failures.Add("Player_Model.controller references FallingPhase_Legacy.");
        if (EnumerateTransitions(root).Any(transition => transition.destinationState != null && transition.destinationState.name == "Jump_Roll" || transition.conditions.Any(condition => condition.parameter == "JumpRollTrigger")))
            failures.Add("Player_Model.controller still exposes a Jump_Roll transition.");
    }

    private static void ValidateMotion(AnimatorStateMachine root, string stateName, string clipName, List<string> failures)
    {
        AnimatorState state = FindState(root, stateName);
        if (state == null || state.motion == null || state.motion.name != clipName) failures.Add(stateName + " must use " + clipName + ".");
    }

    private static AnimatorState FindState(AnimatorStateMachine machine, string name)
    {
        foreach (var child in machine.states) if (child.state != null && child.state.name == name) return child.state;
        foreach (var child in machine.stateMachines)
        {
            AnimatorState state = FindState(child.stateMachine, name);
            if (state != null) return state;
        }
        return null;
    }

    private static IEnumerable<AnimatorStateTransition> EnumerateTransitions(AnimatorStateMachine machine)
    {
        foreach (var transition in machine.anyStateTransitions) yield return transition;
        foreach (var state in machine.states.Select(child => child.state).Where(state => state != null))
            foreach (var transition in state.transitions) yield return transition;
        foreach (var child in machine.stateMachines)
            foreach (var transition in EnumerateTransitions(child.stateMachine)) yield return transition;
    }
}
