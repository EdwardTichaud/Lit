using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>Explicit, repeatable migration for player dodge clips and UCC dash profiles.</summary>
public static class PlayerDodgeInPlaceMigrationUtility
{
    private const string ControllerPath = "Assets/Characters/4_Animations/Player_Model.controller";
    private const string SessionPrefabPath = "Assets/Core/System/GameplaySessionRoot.prefab";
    private const string Root = "Assets/0 - UnityPackages/Fab/TwinBladesBundle/";
    private const string InPlaceTag = "RealTimeCombatInPlace";

    private readonly struct DodgeClipMapping
    {
        public readonly string State;
        public readonly string RootClip;
        public readonly string InPlaceClip;

        public DodgeClipMapping(string state, string rootClip, string inPlaceClip)
        {
            State = state;
            RootClip = rootClip;
            InPlaceClip = inPlaceClip;
        }
    }

    private static readonly DodgeClipMapping[] Mappings =
    {
        new DodgeClipMapping("TwinSword_Dodge_F_Root", Root + "TwinSword_Expansion_V2/Animation/Root/Dodge/TwinSword_Dodge_F_Root.FBX", Root + "TwinSword_Expansion_V2/Animation/Inplace/Dodge/TwinSword_Dodge_F_Inplace.FBX"),
        new DodgeClipMapping("TwinSword_Dodge_B_Root", Root + "TwinSword_Expansion_V2/Animation/Root/Dodge/TwinSword_Dodge_B_Root.FBX", Root + "TwinSword_Expansion_V2/Animation/Inplace/Dodge/TwinSword_Dodge_B_Inplace.FBX"),
        new DodgeClipMapping("TwinSword_Dodge_L_Root", Root + "TwinSword_Expansion_V2/Animation/Root/Dodge/TwinSword_Dodge_L_Root.FBX", Root + "TwinSword_Expansion_V2/Animation/Inplace/Dodge/TwinSword_Dodge_L_Inplace.FBX"),
        new DodgeClipMapping("TwinSword_Dodge_R_Root", Root + "TwinSword_Expansion_V2/Animation/Root/Dodge/TwinSword_Dodge_R_Root.FBX", Root + "TwinSword_Expansion_V2/Animation/Inplace/Dodge/TwinSword_Dodge_R_Inplace.FBX"),
        new DodgeClipMapping("Twinblades_Dodge_F_Root", Root + "Twinblades_Expansion_V2/Animation/Root/Dodge/Twinblades_Dodge_F_Root.FBX", Root + "Twinblades_Expansion_V2/Animation/Inplace/Dodge/Twinblades_Dodge_F_Inplace.FBX"),
        new DodgeClipMapping("Twinblades_Dodge_B_Root", Root + "Twinblades_Expansion_V2/Animation/Root/Dodge/Twinblades_Dodge_B_Root.FBX", Root + "Twinblades_Expansion_V2/Animation/Inplace/Dodge/Twinblades_Dodge_B_Inplace.FBX"),
        new DodgeClipMapping("Twinblades_Dodge_L_Root", Root + "Twinblades_Expansion_V2/Animation/Root/Dodge/Twinblades_Dodge_L_Root.FBX", Root + "Twinblades_Expansion_V2/Animation/Inplace/Dodge/Twinblades_Dodge_L_Inplace.FBX"),
        new DodgeClipMapping("Twinblades_Dodge_R_Root", Root + "Twinblades_Expansion_V2/Animation/Root/Dodge/Twinblades_Dodge_R_Root.FBX", Root + "Twinblades_Expansion_V2/Animation/Inplace/Dodge/Twinblades_Dodge_R_Inplace.FBX")
    };

    [MenuItem("Lit/Animation/Migrate Player Dodges To InPlace")]
    public static void Migrate()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null) throw new InvalidOperationException("Player_Model.controller is missing.");

        foreach (DodgeClipMapping mapping in Mappings)
        {
            AnimatorState state = FindState(controller.layers[0].stateMachine, mapping.State);
            AnimationClip inPlace = LoadClip(mapping.InPlaceClip);
            if (state == null || inPlace == null) throw new InvalidOperationException("Missing dodge state or in-place clip: " + mapping.State);
            state.motion = inPlace;
            state.tag = InPlaceTag;
        }

        EditorUtility.SetDirty(controller);
        ConfigureSessionDashProfiles();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Player Dodge InPlace] Migration complete: 8 clips and UCC dash profiles.");
    }

    private static void ConfigureSessionDashProfiles()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(SessionPrefabPath);
        try
        {
            CombatMobilityController mobility = root.GetComponentInChildren<CombatMobilityController>(true);
            if (mobility == null) throw new InvalidOperationException("GameplaySessionRoot has no CombatMobilityController.");

            foreach (DodgeClipMapping mapping in Mappings)
            {
                AnimationClip source = LoadClip(mapping.RootClip);
                if (source == null) throw new InvalidOperationException("Missing root dodge clip: " + mapping.RootClip);
                float duration = Mathf.Max(0.01f, source.length);
                float distance = Mathf.Max(0.01f, source.averageSpeed.magnitude * duration);
                mobility.ConfigureDodgeDashProfile("Base Layer.RealTimeCombat_RootMotion." + mapping.State, distance, duration,
                    AnimationCurve.Linear(0f, 0f, 1f, 1f));
            }

            PrefabUtility.SaveAsPrefabAsset(root, SessionPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static AnimationClip LoadClip(string path)
    {
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault();
    }

    private static AnimatorState FindState(AnimatorStateMachine machine, string name)
    {
        foreach (ChildAnimatorState child in machine.states)
            if (child.state != null && child.state.name == name) return child.state;
        foreach (ChildAnimatorStateMachine child in machine.stateMachines)
        {
            AnimatorState result = FindState(child.stateMachine, name);
            if (result != null) return result;
        }

        return null;
    }
}
