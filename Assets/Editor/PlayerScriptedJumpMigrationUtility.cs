using System;
using System.Linq;
using Opsive.UltimateCharacterController.Character;
using Opsive.UltimateCharacterController.Character.Abilities;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>One-shot, explicit asset migration for the scripted in-place jump contract.</summary>
public static class PlayerScriptedJumpMigrationUtility
{
    private const string ControllerPath = "Assets/Characters/4_Animations/Player_Model.controller";
    private static readonly string[] PrefabPaths = {
        "Assets/Characters/1_Squad/Lucian/Player_Model_Lucian.prefab",
        "Assets/Characters/1_Squad/Link/Player_Model_Link.prefab",
        "Assets/Characters/1_Squad/Luna/Player_Model_Luna.prefab",
        "Assets/Characters/1_Squad/Mia/Player_Model_Mia.prefab"
    };

    [MenuItem("Lit/Animation/Migrate Player Scripted Jump")]
    public static void Migrate()
    {
        MigrateController();
        foreach (string prefabPath in PrefabPaths) MigratePrefab(prefabPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Player Scripted Jump] Migration complete: four prefabs and Player_Model.controller.");
    }

    private static void MigrateController()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null) throw new InvalidOperationException("Player_Model.controller is missing.");
        AnimatorStateMachine root = controller.layers[0].stateMachine;
        SetMotion(root, "Jump_Start", "Assets/Characters/4_Animations/Dynamic_Archer_Set/Animation/Humanoid/inplace/jump_start_inplace.fbx");
        SetMotion(root, "Jump_Loop", "Assets/Characters/4_Animations/Dynamic_Archer_Set/Animation/Humanoid/inplace/jump_inplace.fbx");
        SetMotion(root, "Falling", "Assets/Characters/1_Squad/Lucian/Animation/jump_falling_loop_inplace.fbx");
        SetMotion(root, "Jump_End", "Assets/Characters/1_Squad/Lucian/Animation/jump_landing_inplace.fbx");
        RemoveJumpRoll(root);
        RemoveMissingBehaviours(root);
        int jumpRollParameterIndex = System.Array.FindIndex(controller.parameters, parameter => parameter.name == "JumpRollTrigger");
        if (jumpRollParameterIndex >= 0) controller.RemoveParameter(jumpRollParameterIndex);
        EditorUtility.SetDirty(controller);
    }

    private static void SetMotion(AnimatorStateMachine root, string stateName, string clipPath)
    {
        AnimatorState state = FindState(root, stateName);
        AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(clipPath).OfType<AnimationClip>().FirstOrDefault();
        if (state == null || clip == null) throw new InvalidOperationException("Missing scripted jump state or in-place clip: " + stateName);
        state.motion = clip;
    }

    private static void RemoveJumpRoll(AnimatorStateMachine stateMachine)
    {
        foreach (var child in stateMachine.stateMachines) RemoveJumpRoll(child.stateMachine);
        AnimatorState roll = stateMachine.states.Select(child => child.state).FirstOrDefault(state => state != null && state.name == "Jump_Roll");
        if (roll != null) stateMachine.RemoveState(roll);
        foreach (AnimatorState state in stateMachine.states.Select(child => child.state).Where(state => state != null))
            foreach (AnimatorStateTransition transition in state.transitions.Where(UsesJumpRoll).ToArray()) state.RemoveTransition(transition);
        foreach (AnimatorStateTransition transition in stateMachine.anyStateTransitions.Where(UsesJumpRoll).ToArray()) stateMachine.RemoveAnyStateTransition(transition);
    }

    private static void RemoveMissingBehaviours(AnimatorStateMachine stateMachine)
    {
        foreach (var child in stateMachine.stateMachines) RemoveMissingBehaviours(child.stateMachine);
        foreach (AnimatorState state in stateMachine.states.Select(child => child.state).Where(state => state != null))
            state.behaviours = state.behaviours.Where(behaviour => behaviour != null).ToArray();
    }

    private static bool UsesJumpRoll(AnimatorStateTransition transition)
    {
        return transition.destinationState != null && transition.destinationState.name == "Jump_Roll" ||
               transition.conditions.Any(condition => condition.parameter == "JumpRollTrigger");
    }

    private static AnimatorState FindState(AnimatorStateMachine machine, string name)
    {
        foreach (var child in machine.states) if (child.state != null && child.state.name == name) return child.state;
        foreach (var child in machine.stateMachines)
        {
            AnimatorState result = FindState(child.stateMachine, name);
            if (result != null) return result;
        }
        return null;
    }

    private static void MigratePrefab(string prefabPath)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            UltimateCharacterLocomotion locomotion = root.GetComponent<UltimateCharacterLocomotion>();
            if (locomotion == null) throw new InvalidOperationException("Missing UCC locomotion: " + prefabPath);
            PlayerScriptedJumpController jumpController = root.GetComponent<PlayerScriptedJumpController>();
            if (jumpController == null) jumpController = root.AddComponent<PlayerScriptedJumpController>();
            jumpController.SetTargetJumpHeight(5f);
            RemoveMissingComponents(root);
            Ability[] abilities = locomotion.Abilities ?? Array.Empty<Ability>();
            locomotion.Abilities = abilities.Where(ability => !(ability is Jump)).ToArray();
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void RemoveMissingComponents(GameObject root)
    {
        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(root);
    }
}
