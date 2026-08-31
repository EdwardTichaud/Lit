using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>Non-destructive audit for the player in-place dodge contract.</summary>
public static class PlayerDodgeInPlaceContractValidator
{
    private const string ControllerPath = "Assets/Characters/4_Animations/Player_Model.controller";
    private const string SessionPrefabPath = "Assets/Core/System/GameplaySessionRoot.prefab";
    private const string InPlaceTag = "RealTimeCombatInPlace";

    private static readonly string[] DodgeStates =
    {
        "TwinSword_Dodge_F_Root", "TwinSword_Dodge_B_Root", "TwinSword_Dodge_L_Root", "TwinSword_Dodge_R_Root",
        "Twinblades_Dodge_F_Root", "Twinblades_Dodge_B_Root", "Twinblades_Dodge_L_Root", "Twinblades_Dodge_R_Root"
    };

    [MenuItem("Lit/Animation/Validate Player Dodge InPlace Contract")]
    public static void Validate()
    {
        var failures = new List<string>();
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError("[Player Dodge InPlace] Player_Model.controller is missing.");
            return;
        }

        CombatMobilityController mobility = LoadMobilityController(failures);
        foreach (string stateName in DodgeStates)
        {
            AnimatorState state = FindState(controller.layers[0].stateMachine, stateName);
            if (state == null)
            {
                failures.Add("Missing state: " + stateName);
                continue;
            }

            if (!string.Equals(state.tag, InPlaceTag, StringComparison.Ordinal))
            {
                failures.Add(stateName + " must use tag " + InPlaceTag + ".");
            }

            AnimationClip clip = state.motion as AnimationClip;
            if (clip == null || clip.name.IndexOf("Inplace", StringComparison.OrdinalIgnoreCase) < 0)
            {
                failures.Add(stateName + " must reference an *_Inplace clip.");
            }

            string statePath = "Base Layer.RealTimeCombat_RootMotion." + stateName;
            if (mobility == null || !mobility.HasDodgeDashProfile(statePath))
            {
                failures.Add("Missing dash profile: " + statePath);
            }
        }

        if (failures.Count == 0)
        {
            Debug.Log("[Player Dodge InPlace] Contract valid: 8 in-place states and 8 UCC dash profiles.");
            return;
        }

        Debug.LogError("[Player Dodge InPlace] Contract invalid (" + failures.Count + "):\n- " + string.Join("\n- ", failures));
    }

    private static CombatMobilityController LoadMobilityController(List<string> failures)
    {
        GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(SessionPrefabPath);
        if (root == null)
        {
            failures.Add("GameplaySessionRoot is missing.");
            return null;
        }

        CombatMobilityController mobility = root.GetComponentInChildren<CombatMobilityController>(true);
        if (mobility == null) failures.Add("GameplaySessionRoot has no CombatMobilityController.");
        return mobility;
    }

    private static AnimatorState FindState(AnimatorStateMachine machine, string name)
    {
        foreach (ChildAnimatorState child in machine.states)
        {
            if (child.state != null && child.state.name == name) return child.state;
        }

        foreach (ChildAnimatorStateMachine child in machine.stateMachines)
        {
            AnimatorState result = FindState(child.stateMachine, name);
            if (result != null) return result;
        }

        return null;
    }
}
