using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>One-shot authoring tool for the reusable combat locomotion graphs.</summary>
public static class CombatLocomotionSetupUtility
{
    private const string PlayerControllerPath = "Assets/Characters/4_Animations/Player_Model.controller";
    private const string JuggernautControllerPath = "Assets/Characters/3_Enemy/Juggernaut/Juggernaut.controller";
    private const string GiantJuggernautControllerPath = "Assets/Characters/3_Enemy/GiantJuggernaut/GiantJuggernaut.controller";
    private const string StrafeFolder = "Assets/0 - UnityPackages/Fab/TwinBladesBundle/Twinblades_Expansion_V2/Animation/Root/Movement/";
    private static readonly string[] EnemyPrefabs =
    {
        "Assets/Characters/3_Enemy/Juggernaut/Juggernaut_Combat.prefab",
        "Assets/Characters/3_Enemy/GiantJuggernaut/GiantJuggernaut.prefab"
    };

    [MenuItem("Lit/Combat/Configure Combat Locomotion")]
    public static void ConfigureCombatLocomotion()
    {
        try
        {
            ConfigurePlayerController(LoadController(PlayerControllerPath));
            ConfigureEnemyController(LoadController(JuggernautControllerPath));
            ConfigureEnemyController(LoadController(GiantJuggernautControllerPath));
            foreach (string prefabPath in EnemyPrefabs)
            {
                EnsureEnemyLocomotionComponent(prefabPath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Combat Locomotion] Player_Model, Juggernaut et les prefabs ennemis sont configures.");
        }
        catch (Exception exception)
        {
            Debug.LogError("[Combat Locomotion] Configuration annulee : " + exception.Message);
        }
    }

    [MenuItem("Lit/Combat/Validate Combat Locomotion")]
    public static void ValidateCombatLocomotion()
    {
        AnimatorController player = LoadController(PlayerControllerPath);
        AnimatorController enemy = LoadController(JuggernautControllerPath);
        AnimatorController giant = LoadController(GiantJuggernautControllerPath);
        ValidateController(player, "CombatLocomotion", "CombatStrafeActive", "HorizontalMovement", "ForwardMovement");
        ValidateController(enemy, "CombatLocomotion", "CombatMoveX", "CombatMoveZ", "CombatMoveSpeed");
        ValidateController(giant, "CombatLocomotion", "CombatMoveX", "CombatMoveZ", "CombatMoveSpeed");
    }

    private static AnimatorController LoadController(string path)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (controller == null)
        {
            throw new InvalidOperationException("AnimatorController introuvable : " + path);
        }

        return controller;
    }

    private static void ConfigurePlayerController(AnimatorController controller)
    {
        AddParameterIfMissing(controller, "CombatStrafeActive", AnimatorControllerParameterType.Bool);
        AddParameterIfMissing(controller, "HorizontalMovement", AnimatorControllerParameterType.Float);
        AddParameterIfMissing(controller, "ForwardMovement", AnimatorControllerParameterType.Float);
        AddParameterIfMissing(controller, "LocomotionTier", AnimatorControllerParameterType.Float);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState state = FindOrCreateState(machine, "CombatLocomotion");
        DeleteGeneratedBlendTrees(controller, "PlayerCombatStrafe");
        state.motion = CreateSpeedStrafeBlendTree(controller, "PlayerCombatStrafe", "LocomotionTier", "HorizontalMovement", "ForwardMovement");
        state.writeDefaultValues = true;
        state.tag = "RealTimeCombatRootMotion";

        AnimatorState locomotion = FindState(machine, "Locomotion");
        if (locomotion == null)
        {
            throw new InvalidOperationException("State Locomotion manquante dans Player_Model.");
        }

        EnsureTransition(locomotion, state, "CombatStrafeActive", true);
        EnsureTransition(state, locomotion, "CombatStrafeActive", false);
        EditorUtility.SetDirty(controller);
    }

    private static void ConfigureEnemyController(AnimatorController controller)
    {
        AddParameterIfMissing(controller, "CombatMoveX", AnimatorControllerParameterType.Float);
        AddParameterIfMissing(controller, "CombatMoveZ", AnimatorControllerParameterType.Float);
        AddParameterIfMissing(controller, "CombatMoveSpeed", AnimatorControllerParameterType.Float);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState state = FindOrCreateState(machine, "CombatLocomotion");
        DeleteGeneratedBlendTrees(controller, "EnemyCombatLocomotion");
        state.motion = CreateSpeedStrafeBlendTree(controller, "EnemyCombatLocomotion", "CombatMoveSpeed", "CombatMoveX", "CombatMoveZ");
        state.writeDefaultValues = true;
        EditorUtility.SetDirty(controller);
    }

    private static BlendTree CreateSpeedStrafeBlendTree(AnimatorController controller, string name, string speedParameter, string xParameter, string zParameter)
    {
        BlendTree root = new BlendTree { name = name, blendType = BlendTreeType.Simple1D, blendParameter = speedParameter, useAutomaticThresholds = false };
        AssetDatabase.AddObjectToAsset(root, controller);
        BlendTree walk = CreateStrafeBlendTree(controller, name + "_Walk", xParameter, zParameter, false);
        BlendTree run = CreateStrafeBlendTree(controller, name + "_Run", xParameter, zParameter, true);
        root.children = new[]
        {
            new ChildMotion { motion = walk, threshold = 1.1f, timeScale = 1f },
            new ChildMotion { motion = run, threshold = 3.2f, timeScale = 1f }
        };
        return root;
    }

    private static BlendTree CreateStrafeBlendTree(AnimatorController controller, string name, string xParameter, string zParameter, bool run)
    {
        BlendTree tree = new BlendTree
        {
            name = name,
            blendType = BlendTreeType.FreeformCartesian2D,
            blendParameter = xParameter,
            blendParameterY = zParameter,
            useAutomaticThresholds = false
        };
        AssetDatabase.AddObjectToAsset(tree, controller);
        tree.children = new[]
        {
            Child("F", new Vector2(0f, 1f), run),
            Child("B", new Vector2(0f, -1f), run),
            Child("FL", new Vector2(-1f, 1f), run),
            Child("FR", new Vector2(1f, 1f), run),
            Child("BL", new Vector2(-1f, -1f), run),
            Child("BR", new Vector2(1f, -1f), run),
            Child("L", new Vector2(-1f, 0f), run),
            Child("R", new Vector2(1f, 0f), run)
        };
        return tree;
    }

    private static ChildMotion Child(string direction, Vector2 position, bool run)
    {
        string file = "Twinblades_Strafe_" + (run ? "Run" : "Walk") + "_" + direction + "_Root.FBX";
        AnimationClip clip = Array.Find(AssetDatabase.LoadAllAssetsAtPath(StrafeFolder + file), candidate => candidate is AnimationClip) as AnimationClip;
        if (clip == null)
        {
            throw new InvalidOperationException("Clip de strafe introuvable : " + file);
        }

        return new ChildMotion { motion = clip, position = position, threshold = 0f, timeScale = 1f };
    }

    private static void EnsureEnemyLocomotionComponent(string prefabPath)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            if (root.GetComponent<CombatEnemyLocomotionController>() == null)
            {
                root.AddComponent<CombatEnemyLocomotionController>();
            }
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void DeleteGeneratedBlendTrees(AnimatorController controller, string prefix)
    {
        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(controller)))
        {
            if (asset is BlendTree tree && tree.name.StartsWith(prefix, StringComparison.Ordinal))
            {
                UnityEngine.Object.DestroyImmediate(tree, true);
            }
        }
    }

    private static AnimatorState FindOrCreateState(AnimatorStateMachine machine, string name)
    {
        AnimatorState existing = FindState(machine, name);
        return existing != null ? existing : machine.AddState(name, new Vector3(550f, 100f, 0f));
    }

    private static AnimatorState FindState(AnimatorStateMachine machine, string name)
    {
        foreach (ChildAnimatorState child in machine.states)
        {
            if (child.state != null && child.state.name == name)
            {
                return child.state;
            }
        }
        return null;
    }

    private static void EnsureTransition(AnimatorState source, AnimatorState destination, string parameter, bool value)
    {
        foreach (AnimatorStateTransition transition in source.transitions)
        {
            if (transition.destinationState == destination && transition.conditions.Length == 1 && transition.conditions[0].parameter == parameter)
            {
                return;
            }
        }

        AnimatorStateTransition created = source.AddTransition(destination);
        created.hasExitTime = false;
        created.hasFixedDuration = true;
        created.duration = 0.08f;
        created.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, parameter);
    }

    private static void AddParameterIfMissing(AnimatorController controller, string name, AnimatorControllerParameterType type)
    {
        foreach (AnimatorControllerParameter parameter in controller.parameters)
        {
            if (parameter.name == name)
            {
                return;
            }
        }
        controller.AddParameter(name, type);
    }

    private static void ValidateController(AnimatorController controller, string stateName, params string[] parameters)
    {
        bool stateFound = FindState(controller.layers[0].stateMachine, stateName) != null;
        foreach (string parameter in parameters)
        {
            bool found = Array.Exists(controller.parameters, candidate => candidate.name == parameter);
            if (!found)
            {
                Debug.LogError("[Combat Locomotion] Parametre manquant dans " + controller.name + " : " + parameter);
            }
        }
        Debug.Log("[Combat Locomotion] " + controller.name + " | state=" + stateName + " present=" + stateFound + ".");
    }
}
