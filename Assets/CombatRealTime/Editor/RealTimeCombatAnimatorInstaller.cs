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
    private const string CombatInPlaceTag = "RealTimeCombatInPlace";
    private const string CombatLocomotionStateName = "CombatLocomotion";
    private const string CombatIdleStateName = "CombatIdle";
    private const string Root = "Assets/0 - UnityPackages/Fab/TwinBladesBundle/";
    private const string InPlaceRoot = Root + "Twinblades_Expansion_V2/Animation/Inplace/";
    private const string GuardClipPath = "Assets/Raise Creation/Super_Fast_Fighting Pack/Animations/Style_Two/Anim_SF_Block_v2.fbx";
    private const string GuardStateName = "Guard_Block";

    private static readonly string[] CombatWalkInPlaceClipPaths =
    {
        InPlaceRoot + "Movement/Twinblades_Strafe_Walk_F_Inplace.FBX",
        InPlaceRoot + "Movement/Twinblades_Strafe_Walk_B_Inplace.FBX",
        InPlaceRoot + "Movement/Twinblades_Strafe_Walk_FL_Inplace.FBX",
        InPlaceRoot + "Movement/Twinblades_Strafe_Walk_FR_Inplace.FBX",
        InPlaceRoot + "Movement/Twinblades_Strafe_Walk_BL_Inplace.FBX",
        InPlaceRoot + "Movement/Twinblades_Strafe_Walk_BR_Inplace.FBX",
        InPlaceRoot + "Movement/Twinblades_Strafe_Walk_L_Inplace.FBX",
        InPlaceRoot + "Movement/Twinblades_Strafe_Walk_R_Inplace.FBX",
    };

    private static readonly string[] CombatRunInPlaceClipPaths =
    {
        InPlaceRoot + "Movement/Twinblades_Strafe_Run_F_Inplace.FBX",
        InPlaceRoot + "Movement/Twinblades_Strafe_Run_B_Inplace.FBX",
        InPlaceRoot + "Movement/Twinblades_Strafe_Run_FL_Inplace.FBX",
        InPlaceRoot + "Movement/Twinblades_Strafe_Run_FR_Inplace.FBX",
        InPlaceRoot + "Movement/Twinblades_Strafe_Run_BL_Inplace.FBX",
        InPlaceRoot + "Movement/Twinblades_Strafe_Run_BR_Inplace.FBX",
        InPlaceRoot + "Movement/Twinblades_Strafe_Run_L_Inplace.FBX",
        InPlaceRoot + "Movement/Twinblades_Strafe_Run_R_Inplace.FBX",
    };

    private const string CombatIdleInPlaceClipPath = InPlaceRoot + "Battle/Twinblades_Idle_Inplace.FBX";

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

    [MenuItem("Lit/Combat/Install Root Motion States And Combat InPlace Locomotion")]
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

        AnimationClip guardClip = LoadClip(GuardClipPath);
        if (guardClip != null)
        {
            AnimatorState guardState = FindState(machine, GuardStateName)
                ?? machine.AddState(GuardStateName, new Vector3(1040f, 640f, 0f));
            guardState.motion = guardClip;
            guardState.writeDefaultValues = true;
        }

        EnsureCombatLocomotionInPlace(controller);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Lit/Combat/Migrate Combat Locomotion To InPlace")]
    private static void MigrateCombatLocomotionToInPlace()
    {
        EnsureCombatLocomotionInPlace(AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath));
    }

    private static void EnsureCombatLocomotionInPlace(AnimatorController controller)
    {
        if (controller == null || controller.layers == null || controller.layers.Length == 0)
        {
            return;
        }

        AnimationClip[] walkClips = LoadClips(CombatWalkInPlaceClipPaths);
        AnimationClip[] runClips = LoadClips(CombatRunInPlaceClipPaths);
        AnimationClip idleClip = LoadClip(CombatIdleInPlaceClipPath);
        if (walkClips == null || runClips == null || idleClip == null)
        {
            Debug.LogError("[Combat Animator] Migration InPlace annulee : un ou plusieurs clips Twinblades InPlace sont introuvables.");
            return;
        }

        AnimatorStateMachine rootMachine = controller.layers[0].stateMachine;
        AnimatorState combatLocomotion = FindState(rootMachine, CombatLocomotionStateName);
        if (combatLocomotion == null)
        {
            Debug.LogError("[Combat Animator] Etat CombatLocomotion introuvable dans Player_Model.controller.");
            return;
        }

        EnsureFloatParameter(controller, "CombatMoveMagnitude");

        BlendTree outerTree = combatLocomotion.motion as BlendTree;
        if (outerTree == null)
        {
            outerTree = CreateBlendTree(controller, "PlayerCombatStrafe");
            combatLocomotion.motion = outerTree;
        }

        BlendTree walkTree = FindOrCreateBlendTree(controller, "PlayerCombatStrafe_Walk");
        BlendTree runTree = FindOrCreateBlendTree(controller, "PlayerCombatStrafe_Run");
        ConfigureDirectionalInPlaceTree(walkTree, walkClips, idleClip);
        ConfigureDirectionalInPlaceTree(runTree, runClips, idleClip);

        outerTree.name = "PlayerCombatStrafe";
        outerTree.blendType = BlendTreeType.Simple1D;
        outerTree.blendParameter = "LocomotionTier";
        outerTree.children = new[]
        {
            CreateChild(walkTree, 1.1f, Vector2.zero),
            CreateChild(runTree, 3.2f, Vector2.zero),
        };

        combatLocomotion.tag = CombatInPlaceTag;
        combatLocomotion.writeDefaultValues = true;

        AnimatorState combatIdle = FindState(rootMachine, CombatIdleStateName)
            ?? rootMachine.AddState(CombatIdleStateName, new Vector3(1330f, 850f, 0f));
        combatIdle.motion = idleClip;
        combatIdle.tag = CombatInPlaceTag;
        combatIdle.writeDefaultValues = true;

        RewireCombatEntry(rootMachine, combatLocomotion, combatIdle);
        EnsureTransition(combatIdle, combatLocomotion, AnimatorConditionMode.Greater, "CombatMoveMagnitude", 0.05f, 0.03f);
        EnsureTransition(combatLocomotion, combatIdle, AnimatorConditionMode.Less, "CombatMoveMagnitude", 0.05f, 0.04f);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log("[Combat Animator] CombatLocomotion migre vers les clips Twinblades InPlace.", controller);
    }

    private static AnimationClip[] LoadClips(string[] paths)
    {
        AnimationClip[] clips = new AnimationClip[paths.Length];
        for (int i = 0; i < paths.Length; i++)
        {
            clips[i] = LoadClip(paths[i]);
            if (clips[i] == null)
            {
                return null;
            }
        }

        return clips;
    }

    private static BlendTree FindOrCreateBlendTree(AnimatorController controller, string name)
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(ControllerPath))
        {
            if (asset is BlendTree tree && tree.name == name)
            {
                return tree;
            }
        }

        return CreateBlendTree(controller, name);
    }

    private static BlendTree CreateBlendTree(AnimatorController controller, string name)
    {
        BlendTree tree = new BlendTree { name = name };
        AssetDatabase.AddObjectToAsset(tree, controller);
        return tree;
    }

    private static void ConfigureDirectionalInPlaceTree(BlendTree tree, AnimationClip[] clips, AnimationClip idleClip)
    {
        tree.blendType = BlendTreeType.SimpleDirectional2D;
        tree.blendParameter = "HorizontalMovement";
        tree.blendParameterY = "ForwardMovement";
        tree.children = new[]
        {
            CreateChild(clips[0], 0f, new Vector2(0f, 1f)),
            CreateChild(clips[1], 0f, new Vector2(0f, -1f)),
            CreateChild(clips[2], 0f, new Vector2(-1f, 1f)),
            CreateChild(clips[3], 0f, new Vector2(1f, 1f)),
            CreateChild(clips[4], 0f, new Vector2(-1f, -1f)),
            CreateChild(clips[5], 0f, new Vector2(1f, -1f)),
            CreateChild(clips[6], 0f, new Vector2(-1f, 0f)),
            CreateChild(clips[7], 0f, new Vector2(1f, 0f)),
            CreateChild(idleClip, 0f, Vector2.zero),
        };
    }

    private static ChildMotion CreateChild(Motion motion, float threshold, Vector2 position)
    {
        return new ChildMotion
        {
            motion = motion,
            threshold = threshold,
            position = position,
            timeScale = 1f,
        };
    }

    private static void RewireCombatEntry(AnimatorStateMachine rootMachine, AnimatorState combatLocomotion, AnimatorState combatIdle)
    {
        foreach (ChildAnimatorState child in rootMachine.states)
        {
            AnimatorState state = child.state;
            if (state == null || state == combatLocomotion || state == combatIdle)
            {
                continue;
            }

            foreach (AnimatorStateTransition transition in state.transitions)
            {
                if (transition.destinationState == combatLocomotion && HasCondition(transition, "CombatStrafeActive"))
                {
                    transition.destinationState = combatIdle;
                }
            }
        }
    }

    private static void EnsureTransition(AnimatorState source, AnimatorState destination, AnimatorConditionMode mode, string parameter, float threshold, float duration)
    {
        foreach (AnimatorStateTransition transition in source.transitions)
        {
            if (transition.destinationState == destination && HasCondition(transition, parameter))
            {
                transition.hasExitTime = false;
                transition.hasFixedDuration = true;
                transition.duration = duration;
                return;
            }
        }

        AnimatorStateTransition created = source.AddTransition(destination);
        created.hasExitTime = false;
        created.hasFixedDuration = true;
        created.duration = duration;
        created.AddCondition(mode, threshold, parameter);
    }

    private static bool HasCondition(AnimatorStateTransition transition, string parameter)
    {
        foreach (AnimatorCondition condition in transition.conditions)
        {
            if (condition.parameter == parameter)
            {
                return true;
            }
        }

        return false;
    }

    private static void EnsureFloatParameter(AnimatorController controller, string parameterName)
    {
        foreach (AnimatorControllerParameter parameter in controller.parameters)
        {
            if (parameter.name == parameterName)
            {
                return;
            }
        }

        controller.AddParameter(parameterName, AnimatorControllerParameterType.Float);
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
