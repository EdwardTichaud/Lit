using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

[InitializeOnLoad]
public static class RealTimeCombatAnimatorInstaller
{
    private const string ControllerPath = "Assets/Characters/4_Animations/Player_Model.controller";
    private const string StateMachineName = "RealTimeCombat_RootMotion";
    private const string StateTag = "RealTimeCombatRootMotion";
    private const string Root = "Assets/0 - UnityPackages/Fab/TwinBladesBundle/";

    private static readonly string[] ClipPaths =
    {
        Root + "TwinSwordAnimsetBase_V2/Animation/RootMotion/TwinSword_attack01_Root.FBX",
        Root + "TwinSwordAnimsetBase_V2/Animation/RootMotion/TwinSword_attack02_Root.FBX",
        Root + "TwinSwordAnimsetBase_V2/Animation/RootMotion/TwinSword_attack03_Root.FBX",
        Root + "TwinSwordAnimsetBase_V2/Animation/RootMotion/TwinSword_attack04_Root.FBX",
        Root + "TwinSword_Expansion_V2/Animation/Root/Battle/TwinSword_Attack13_Root.FBX",
        Root + "TwinSword_Expansion_V2/Animation/Root/Battle/TwinSword_Attack14_Root.FBX",
        Root + "TwinSword_Expansion_V2/Animation/Root/Battle/TwinSword_Attack15_Root.FBX",
        Root + "TwinSword_Expansion_V2/Animation/Root/Battle/TwinSword_Attack16_Root.FBX",
        Root + "TwinSword_Expansion_V2/Animation/Root/Dodge/TwinSword_Dodge_F_Root.FBX",
        Root + "TwinSword_Expansion_V2/Animation/Root/Dodge/TwinSword_Dodge_B_Root.FBX",
        Root + "TwinSword_Expansion_V2/Animation/Root/Dodge/TwinSword_Dodge_L_Root.FBX",
        Root + "TwinSword_Expansion_V2/Animation/Root/Dodge/TwinSword_Dodge_R_Root.FBX",
        Root + "TwinSword_Expansion_V2/Animation/Root/Hit/TwinSword_Defense_Hit_Root.FBX",
        Root + "TwinSword_Expansion_V2/Animation/Root/Hit/TwinSword_Large_Hit_Root.FBX",
        Root + "TwinSword_Expansion_V2/Animation/Root/Hit/TwinSword_Die_1_Root.FBX",
        Root + "TwinSword_Expansion_V2/Animation/Root/Battle/TwinSword_Idle_Root.FBX",
        Root + "TwinBladesAnimsetBase_V2/Animation/RootMotion/Twinblades_attack02_Root.FBX",
        Root + "TwinBladesAnimsetBase_V2/Animation/RootMotion/Twinblades_attack03_Root.FBX",
        Root + "TwinBladesAnimsetBase_V2/Animation/RootMotion/Twinblades_attack04_Root.FBX",
        Root + "TwinBladesAnimsetBase_V2/Animation/RootMotion/Twinblades_attack05_Root.FBX",
        Root + "Twinblades_Expansion_V2/Animation/Root/Battle/Twinblades_Attack13_Root.FBX",
        Root + "Twinblades_Expansion_V2/Animation/Root/Battle/Twinblades_Attack14_Root.FBX",
        Root + "Twinblades_Expansion_V2/Animation/Root/Battle/Twinblades_Attack15_Root.FBX",
        Root + "Twinblades_Expansion_V2/Animation/Root/Battle/Twinblades_Attack16_Root.FBX",
        Root + "Twinblades_Expansion_V2/Animation/Root/Dodge/Twinblades_Dodge_F_Root.FBX",
        Root + "Twinblades_Expansion_V2/Animation/Root/Dodge/Twinblades_Dodge_B_Root.FBX",
        Root + "Twinblades_Expansion_V2/Animation/Root/Dodge/Twinblades_Dodge_L_Root.FBX",
        Root + "Twinblades_Expansion_V2/Animation/Root/Dodge/Twinblades_Dodge_R_Root.FBX",
        Root + "Twinblades_Expansion_V2/Animation/Root/Hit/Twinblades_Defense_Hit_Root.FBX",
        Root + "Twinblades_Expansion_V2/Animation/Root/Hit/Twinblades_Large_Hit_Root.FBX",
        Root + "Twinblades_Expansion_V2/Animation/Root/Hit/Twinblades_Die_1_Root.FBX",
        Root + "Twinblades_Expansion_V2/Animation/Root/Battle/Twinblades_Idle_Root.FBX",
    };

    static RealTimeCombatAnimatorInstaller()
    {
        EditorApplication.delayCall += Install;
    }

    [MenuItem("Lit/Combat/Install Selected Root Motion States")]
    public static void Install()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null) return;

        AnimatorStateMachine machine = FindStateMachine(controller.layers[0].stateMachine, StateMachineName)
            ?? controller.layers[0].stateMachine.AddStateMachine(StateMachineName, new Vector3(2300f, 500f, 0f));

        for (int i = 0; i < ClipPaths.Length; i++)
        {
            AnimationClip clip = LoadClip(ClipPaths[i]);
            if (clip == null) continue;

            AnimatorState state = FindState(machine, clip.name) ?? machine.AddState(clip.name, new Vector3((i % 4) * 260f, (i / 4) * 85f, 0f));
            state.motion = clip;
            state.tag = StateTag;
            state.writeDefaultValues = true;
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
    }

    private static AnimatorStateMachine FindStateMachine(AnimatorStateMachine parent, string name)
    {
        foreach (ChildAnimatorStateMachine child in parent.stateMachines)
            if (child.stateMachine != null && child.stateMachine.name == name) return child.stateMachine;
        return null;
    }

    private static AnimatorState FindState(AnimatorStateMachine machine, string name)
    {
        foreach (ChildAnimatorState child in machine.states)
            if (child.state != null && child.state.name == name) return child.state;
        return null;
    }

    private static AnimationClip LoadClip(string path)
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__")) return clip;
        return null;
    }
}
