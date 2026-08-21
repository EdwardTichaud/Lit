using System;
using System.Collections.Generic;
using Opsive.UltimateCharacterController.Character;
using Opsive.UltimateCharacterController.Character.Abilities;
using Opsive.UltimateCharacterController.Character.Identifiers;
using Opsive.UltimateCharacterController.Traits;
using Opsive.UltimateCharacterController.Utility.Builders;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class LitOpsiveUccMigrationUtility
{
    private const string MenuRoot = "Lit/Opsive UCC/";
    private const string UccSuffix = "_UCC";
    private const string AdventureMovementType = "Opsive.UltimateCharacterController.ThirdPersonController.Character.MovementTypes.Adventure";
    private const string UccDemoAnimatorControllerPath = "Assets/Opsive/UltimateCharacterController/RuntimeAnimator/Characters/Demo.controller";
    private const string LucianAnimatorControllerPath = "Assets/Characters/4_Animations/Player_Model.controller";
    private const float UccAnimatorSpeed = 0.8f;
    private const float LucianRootMotionAnimatorSpeed = 1f;
    private const float LucianRootMotionSpeedMultiplier = 1.04f;
    private const float LucianRootMotionRotationMultiplier = 1.08f;
    private const bool LucianPreferLookSourceRotationForRootMotionLocomotion = true;
    private const bool LucianAllowRootMotionRotationDuringStartStop = false;
    private const bool LucianUseLookSourceForwardInputForRootMotion = true;
    private const bool LucianUseStableWorldPlanarLookSource = true;
    private const float LucianLookSourcePlanarYawOffset = 0f;
    private const bool LucianSuppressIdleRootMotionPosition = true;
    private const float LucianIdleRootMotionVelocityThreshold = 0.06f;
    private const float LucianRootMotionLoopSpeedScale = 1.02f;
    private const float LucianRootMotionLoopRotationScale = 1f;
    private const float LucianRootMotionStartSpeedScale = 1.18f;
    private const float LucianRootMotionStartRotationScale = 1f;
    private const float LucianRootMotionStopSpeedScale = 0.7f;
    private const float LucianRootMotionStopRotationScale = 0.94f;
    private const float LucianRootMotionPivotSpeedScale = 0.88f;
    private const float LucianRootMotionPivotRotationScale = 1.12f;
    private const float LucianGroundReliefMinStepHeight = 0.5f;
    private const float LucianGroundReliefMinSlopeLimit = 60f;
    private const float LucianGroundReliefMinStickToGroundDistance = 0.72f;
    private const float LucianRootMotionMovingStepHeight = 0.58f;
    private const float LucianRootMotionMovingSlopeLimit = 62f;
    private const float LucianRootMotionMovingStickToGroundDistance = 0.86f;
    private const float LucianRootMotionIdleStickToGroundDistance = 0.64f;
    private const float LucianRootMotionGroundReliefAdaptationSpeed = 7.5f;
    private const float LucianGroundedInputAcceleration = 6.2f;
    private const float LucianGroundedSprintInputAcceleration = 5f;
    private const float LucianGroundedInputDeceleration = 6.2f;
    private const float LucianGroundedDirectionChangeAcceleration = 11.2f;
    private const float LucianGroundedAnimatorSpeedRiseRate = 13.5f;
    private const float LucianGroundedAnimatorSpeedFallRate = 5.4f;
    private const float LucianGroundedAnimatorTurnRate = 5.4f;
    private const float LucianGroundedStopTriggerMinSpeed = 0.48f;
    private const float LucianGroundedRootMotionSpeedToBlend = 0.22f;
    private const float LucianGroundedMoveTransitionDirectionHoldTime = 0.18f;
    private const float LucianGroundedMoveTransitionParameterSpeed = 1.22f;
    private const bool LucianUseForwardOnlyGroundedLocomotion = true;
    private const float LucianGroundedPivotMinAngle = 85f;
    private const float LucianGroundedPivot180Angle = 135f;
    private const bool LucianGroundedSnapStationaryTurn = true;
    private const float LucianGroundedSnapStationaryTurnMinAngle = 25f;
    private const float LucianGroundedSnapStationaryTurnMaxSpeed = 0.22f;
    private const float LucianGroundedSnapStationaryTurnMaxSmoothedInput = 0.08f;
    private const float LucianGroundedPivotMaxSpeed = 0.45f;
    private const float LucianGroundedPivotMaxSmoothedInput = 0.14f;
    private const float LucianGroundedPivotHoldTime = 0.32f;
    private const float LucianGroundedPivotCooldown = 0.34f;
    private const float LucianGroundedPivotStartGraceTime = 0.12f;
    private const float LucianGroundedPivotStartGraceMinAngle = 128f;
    private const float LucianGroundedPivotMovementReleaseStart = 0.38f;
    private const float LucianGroundedPivotMovementReleaseMaxAngle = 72f;
    private const float LucianGroundedPivotMovementReleaseScale = 0.58f;
    private const bool LucianCommitRootRotationDuringPivot = true;
    private const float LucianGroundedPivotRotationCommitRate = 960f;
    private const float LucianOrientationInputDeadZone = 0.14f;
    private const float LucianOrientationWalkTurnRate = 360f;
    private const float LucianOrientationSprintTurnRate = 300f;
    private const float LucianOrientationSharpTurnRate = 540f;
    private const float LucianOrientationSharpTurnAngle = 92f;
    private const float LucianOrientationVelocityBlend = 0.1f;
    private const float LucianIgnoredObstacleMaxHeight = 0.22f;
    private const float LucianTraversableObstacleMaxHeight = 1.05f;
    private const float LucianObstacleProbeDistance = 0.75f;
    private const float LucianObstacleProbeRadius = 0.22f;
    private const float LucianObstacleProbeBaseHeight = 0.25f;
    private const float LucianObstacleTraversalMaxSurfaceUpDot = 0.35f;
    private const float LucianObstacleLandingDistance = 0.55f;
    private const float LucianObstacleTraversalDuration = 0.46f;
    private const float LucianObstacleTraversalArcHeight = 0.22f;
    private const float LucianObstacleTraversalTopClearance = 0.16f;
    private const float LucianObstacleTraversalHeightArcMultiplier = 0.38f;
    private const float LucianObstacleTraversalRotationLead = 0.68f;
    private const float LucianObstacleTraversalMinInputMagnitude = 0.34f;
    private const float LucianObstacleTraversalCooldown = 0.28f;
    private const float LucianCharacterIkHipsPositionAdjustmentSpeed = 7f;
    private const float LucianCharacterIkFootOffsetAdjustment = 0.012f;
    private const float LucianCharacterIkFootWeightActiveAdjustmentSpeed = 14f;
    private const float LucianCharacterIkFootWeightInactiveAdjustmentSpeed = 4f;
    private const string UccHealthAttributeName = "Health";
    private const string LucianCharacterDataPath = "Assets/Characters/1_Squad/Lucian/Lucian.asset";
    private const string LucianPrefabPath = "Assets/Characters/1_Squad/Lucian/Player_Model_Lucian.prefab";
    private static readonly string[] KnownPlayerCharacterPrefabPaths =
    {
        LucianPrefabPath
    };
    private const string AutoSimplifyLucianAnimatorAfterReloadKey =
        "Lit.Opsive.AutoSimplifyLucianAnimatorAfterReload";
    private static bool AutoSimplifyLucianAnimatorAfterReload =>
        EditorPrefs.GetBool(AutoSimplifyLucianAnimatorAfterReloadKey, false);

    [InitializeOnLoadMethod]
    private static void ApplyLucianLocomotionSimplificationAfterReload()
    {
        if (!AutoSimplifyLucianAnimatorAfterReload)
            return;

        EditorApplication.delayCall += () =>
        {
            RuntimeAnimatorController controller = ResolveAnimatorController(LucianAnimatorControllerPath);
            if (controller != null)
            {
                SimplifyLucianLocomotionController(controller);
            }
        };
    }

    [MenuItem(MenuRoot + "Create Selected Character UCC Variant", true)]
    private static bool CanCreateSelectedCharacterVariant()
    {
        return Selection.activeObject is GameObject selected &&
               !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(selected));
    }

    [MenuItem(MenuRoot + "Create Selected Character UCC Variant")]
    private static void CreateSelectedCharacterVariant()
    {
        GameObject selected = Selection.activeObject as GameObject;
        string sourcePath = AssetDatabase.GetAssetPath(selected);
        if (string.IsNullOrEmpty(sourcePath))
        {
            Debug.LogError("Select a character prefab first.");
            return;
        }

        string targetPath = CreateOrRefreshVariant(sourcePath);
        if (string.IsNullOrEmpty(targetPath))
        {
            return;
        }

        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(targetPath);
        Debug.Log(string.Equals(targetPath, sourcePath, StringComparison.Ordinal)
            ? $"Configured UCC locomotion prefab: {targetPath}"
            : $"Created UCC locomotion variant: {targetPath}");
    }

    [MenuItem(MenuRoot + "Create/Refresh Known Player UCC Variants")]
    public static void CreateKnownPlayerCharacterVariants()
    {
        for (int i = 0; i < KnownPlayerCharacterPrefabPaths.Length; i++)
        {
            CreateOrRefreshVariant(KnownPlayerCharacterPrefabPaths[i]);
        }
    }

    [MenuItem(MenuRoot + "Print Known Player UCC Adoption Audit")]
    public static void PrintKnownPlayerUccAdoptionAudit()
    {
        List<string> lines = new List<string>
        {
            "[LitOpsiveUCC] Known player UCC adoption audit"
        };

        for (int i = 0; i < KnownPlayerCharacterPrefabPaths.Length; i++)
        {
            string sourcePath = KnownPlayerCharacterPrefabPaths[i];
            AppendPrefabUccAdoption(lines, sourcePath, IsLucianPrefabPath(sourcePath) ? "in-place target" : "source prefab");

            if (!IsLucianPrefabPath(sourcePath))
            {
                AppendPrefabUccAdoption(lines, GetUccVariantPath(sourcePath), "UCC variant");
            }
        }

        Debug.Log(string.Join("\n", lines));
    }

    [MenuItem(MenuRoot + "Configure Lucian Prefab In Place")]
    public static void ConfigureLucianPrefabInPlace()
    {
        ConfigurePrefabInPlace(LucianPrefabPath);
    }

    [MenuItem(MenuRoot + "Validate Lucian UCC Setup")]
    public static void ValidateLucianUccSetup()
    {
        List<string> warnings = new List<string>();
        List<string> errors = ValidateLucianUccSetupInternal(warnings);
        errors.AddRange(ValidateAllSquadUccSetupInternal(warnings, skipPrefabPath: LucianPrefabPath));
        if (warnings.Count > 0)
        {
            Debug.LogWarning("UCC setup validation warnings:\n- " + string.Join("\n- ", warnings));
        }

        if (errors.Count == 0)
        {
            Debug.Log("Lucian and Squad UCC setup validation passed.");
            return;
        }

        string message = "UCC setup validation failed:\n- " + string.Join("\n- ", errors);
        Debug.LogError(message);
        throw new InvalidOperationException(message);
    }

    [MenuItem(MenuRoot + "Validate Squad UCC Setup")]
    public static void ValidateSquadUccSetup()
    {
        List<string> warnings = new List<string>();
        List<string> errors = ValidateAllSquadUccSetupInternal(warnings);
        if (warnings.Count > 0)
        {
            Debug.LogWarning("Squad UCC setup validation warnings:\n- " + string.Join("\n- ", warnings));
        }

        if (errors.Count == 0)
        {
            Debug.Log("Squad UCC setup validation passed.");
            return;
        }

        string message = "Squad UCC setup validation failed:\n- " + string.Join("\n- ", errors);
        Debug.LogError(message);
        throw new InvalidOperationException(message);
    }

    private static string CreateOrRefreshVariant(string sourcePath)
    {
        if (IsLucianPrefabPath(sourcePath))
        {
            ConfigurePrefabInPlace(LucianPrefabPath);
            return LucianPrefabPath;
        }

        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
        if (source == null)
        {
            Debug.LogWarning($"Skipping missing character prefab: {sourcePath}");
            return null;
        }

        string targetPath = GetUccVariantPath(sourcePath);
        if (!AssetDatabase.LoadAssetAtPath<GameObject>(targetPath))
        {
            if (!AssetDatabase.CopyAsset(sourcePath, targetPath))
            {
                Debug.LogError($"Unable to copy prefab: {sourcePath}");
                return null;
            }
        }

        GameObject contents = PrefabUtility.LoadPrefabContents(targetPath);
        try
        {
            ConfigureCharacter(contents, useLucianAnimatorController: false);
            PrefabUtility.SaveAsPrefabAsset(contents, targetPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }

        AssetDatabase.ImportAsset(targetPath);
        Debug.Log($"Created/refreshed UCC locomotion variant: {targetPath}");
        return targetPath;
    }

    private static string GetUccVariantPath(string sourcePath)
    {
        string directory = System.IO.Path.GetDirectoryName(sourcePath);
        string filename = System.IO.Path.GetFileNameWithoutExtension(sourcePath);
        return $"{directory}/{filename}{UccSuffix}.prefab";
    }

    private static bool IsLucianPrefabPath(string path)
    {
        return string.Equals(path, LucianPrefabPath, StringComparison.Ordinal);
    }

    private static void ConfigurePrefabInPlace(string prefabPath)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            throw new InvalidOperationException($"Missing character prefab: {prefabPath}");
        }

        GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            ConfigureCharacter(contents, IsLucianPrefabPath(prefabPath));
            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }

        AssetDatabase.ImportAsset(prefabPath);
        Debug.Log($"Configured UCC locomotion on prefab: {prefabPath}");
    }

    [MenuItem(MenuRoot + "Configure Selected Character In Place", true)]
    private static bool CanConfigureSelectedCharacterInPlace()
    {
        return Selection.activeGameObject != null;
    }

    [MenuItem(MenuRoot + "Configure Selected Character In Place")]
    private static void ConfigureSelectedCharacterInPlace()
    {
        GameObject selected = Selection.activeGameObject;
        Undo.RegisterFullObjectHierarchyUndo(selected, "Configure Lit UCC Locomotion");
        string selectedPath = AssetDatabase.GetAssetPath(selected);
        ConfigureCharacter(selected, IsLucianPrefabPath(selectedPath));
        EditorUtility.SetDirty(selected);
    }

    public static void ConfigureCharacter(GameObject character)
    {
        ConfigureCharacter(character, useLucianAnimatorController: false);
    }

    private static void ConfigureCharacter(GameObject character, bool useLucianAnimatorController)
    {
        if (character == null)
        {
            return;
        }

        Animator animator = character.GetComponent<Animator>();
        RuntimeAnimatorController animatorController = useLucianAnimatorController
            ? ResolveLucianAnimatorController()
            : ResolveUccAnimatorController();
        if (animator != null && animatorController != null)
        {
            animator.runtimeAnimatorController = animatorController;
            EditorUtility.SetDirty(animator);
        }

        bool hasExistingUccLocomotion = character.GetComponent<UltimateCharacterLocomotion>() != null;
        if (!hasExistingUccLocomotion)
        {
#if ENABLE_INPUT_SYSTEM
            CharacterBuilder.BuildCharacter(character, new[] { character }, true, new[] { animatorController }, string.Empty, AdventureMovementType, false, null, null, false, false, null);
#else
            CharacterBuilder.BuildCharacter(character, new[] { character }, true, new[] { animatorController }, string.Empty, AdventureMovementType, false, null, null, false, false);
#endif
            CharacterBuilder.BuildCharacterComponents(character, false, false, null, null, false, false, false, false, true, false);
        }

        CharacterBuilder.RemoveUnityInput(character);
        RemoveDuplicateUccColliderGroups(character);

        if (character.GetComponent<LitOpsivePlayerInput>() == null)
        {
            character.AddComponent<LitOpsivePlayerInput>();
        }

        if (character.GetComponent<UltimateCharacterLocomotionHandler>() == null)
        {
            character.AddComponent<UltimateCharacterLocomotionHandler>();
        }

        ConfigureLocomotionAnimationMode(
            character.GetComponent<UltimateCharacterLocomotion>(),
            useRootMotionPosition: useLucianAnimatorController,
            useRootMotionRotation: useLucianAnimatorController,
            rootMotionSpeedMultiplier: useLucianAnimatorController ? LucianRootMotionSpeedMultiplier : 1f,
            rootMotionRotationMultiplier: useLucianAnimatorController ? LucianRootMotionRotationMultiplier : 1f);
        EnsureSingleStandardAbilities(character.GetComponent<UltimateCharacterLocomotion>());
        ConfigureAnimatorMonitor(
            character.GetComponent<AnimatorMonitor>(),
            useLucianAnimatorController ? LucianRootMotionAnimatorSpeed : UccAnimatorSpeed);
        EnsureLookSource(character);
        ConfigureCharacterIk(character);
        ConfigureCharacterHealthAuthority(character);

        LitOpsiveLocomotionBridge bridge = character.GetComponent<LitOpsiveLocomotionBridge>();
        if (bridge == null)
        {
            bridge = character.AddComponent<LitOpsiveLocomotionBridge>();
        }

        ConfigureBridgeAnimatorMode(bridge, driveLitAnimatorParameters: useLucianAnimatorController);
        ConfigureBridgeRootMotionMode(bridge, useRootMotionLocomotion: useLucianAnimatorController);
        ConfigureBridgeCompanionMode(bridge, autoInstallCompanionBridges: false);
        EnsureExplicitCompanionBridges(character);
        ConfigureDamageBridgeHealthAuthority(character);
    }

    private static void ConfigureLocomotionAnimationMode(
        UltimateCharacterLocomotion locomotion,
        bool useRootMotionPosition,
        bool useRootMotionRotation,
        float rootMotionSpeedMultiplier,
        float rootMotionRotationMultiplier)
    {
        if (locomotion == null)
        {
            return;
        }

        SerializedObject serializedObject = new SerializedObject(locomotion);
        SetSerializedBool(serializedObject, "m_UseRootMotionPosition", useRootMotionPosition);
        SetSerializedFloat(serializedObject, "m_RootMotionSpeedMultiplier", rootMotionSpeedMultiplier);
        SetSerializedBool(serializedObject, "m_UseRootMotionRotation", useRootMotionRotation);
        SetSerializedFloat(serializedObject, "m_RootMotionRotationMultiplier", rootMotionRotationMultiplier);
        SetSerializedFloat(serializedObject, "m_MotorRotationSpeed", 0.14f);
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureAnimatorMonitor(AnimatorMonitor animatorMonitor, float animatorSpeed)
    {
        if (animatorMonitor == null)
        {
            return;
        }

        SerializedObject serializedObject = new SerializedObject(animatorMonitor);
        SetSerializedFloat(serializedObject, "m_AnimatorSpeed", animatorSpeed);
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static RuntimeAnimatorController ResolveUccAnimatorController()
    {
        RuntimeAnimatorController controller = ResolveAnimatorController(UccDemoAnimatorControllerPath);
        if (controller == null)
        {
            Debug.LogError($"Missing UCC demo animator controller: {UccDemoAnimatorControllerPath}");
        }

        return controller;
    }

    private static RuntimeAnimatorController ResolveLucianAnimatorController()
    {
        RuntimeAnimatorController controller = ResolveAnimatorController(LucianAnimatorControllerPath);
        if (controller == null)
        {
            Debug.LogError($"Missing Lucian animator controller: {LucianAnimatorControllerPath}");
        }
        else
        {
            SimplifyLucianLocomotionController(controller);
        }

        return controller;
    }

    private static void SimplifyLucianLocomotionController(RuntimeAnimatorController runtimeController)
    {
        AnimatorController controller = runtimeController as AnimatorController;
        if (controller == null || controller.layers == null || controller.layers.Length == 0)
        {
            return;
        }

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        HashSet<AnimatorState> statesToRemove = new HashSet<AnimatorState>();
        bool changed = false;
        AnimatorState jogtrotStart = FindAnimatorState(stateMachine, "Jogtrot_Start");
        AnimatorState jogtrotStop = FindAnimatorState(stateMachine, "Jogtrot_Stop");
        if (jogtrotStart != null)
        {
            statesToRemove.Add(jogtrotStart);
        }

        if (jogtrotStop != null)
        {
            statesToRemove.Add(jogtrotStop);
        }

        ChildAnimatorState[] states = stateMachine.states;
        List<ChildAnimatorState> validStates = new List<ChildAnimatorState>(states.Length);
        for (int i = 0; i < states.Length; i++)
        {
            if (states[i].state != null)
            {
                validStates.Add(states[i]);
            }
            else
            {
                changed = true;
            }
        }

        if (validStates.Count != states.Length)
        {
            stateMachine.states = validStates.ToArray();
            states = stateMachine.states;
        }

        for (int i = 0; i < states.Length; i++)
        {
            AnimatorState state = states[i].state;
            AnimatorStateTransition[] transitions = state.transitions;
            for (int j = transitions.Length - 1; j >= 0; j--)
            {
                if (transitions[j].destinationState == null || statesToRemove.Contains(transitions[j].destinationState))
                {
                    state.RemoveTransition(transitions[j]);
                    changed = true;
                }
            }
        }

        foreach (AnimatorState state in statesToRemove)
        {
            stateMachine.RemoveState(state);
            changed = true;
        }

        if (changed)
        {
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
        }
    }

    private static AnimatorState FindAnimatorState(AnimatorStateMachine stateMachine, string stateName)
    {
        ChildAnimatorState[] states = stateMachine.states;
        for (int i = 0; i < states.Length; i++)
        {
            if (string.Equals(states[i].state.name, stateName, StringComparison.Ordinal))
            {
                return states[i].state;
            }
        }

        return null;
    }

    private static RuntimeAnimatorController ResolveAnimatorController(string path)
    {
        return AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(path);
    }

    private static void ConfigureBridgeAnimatorMode(LitOpsiveLocomotionBridge bridge, bool driveLitAnimatorParameters)
    {
        if (bridge == null)
        {
            return;
        }

        SerializedObject serializedObject = new SerializedObject(bridge);
        SerializedProperty driveLitProperty = serializedObject.FindProperty("driveLitLocomotionAnimatorParameters");
        if (driveLitProperty != null)
        {
            driveLitProperty.boolValue = driveLitAnimatorParameters;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void ConfigureBridgeRootMotionMode(LitOpsiveLocomotionBridge bridge, bool useRootMotionLocomotion)
    {
        if (bridge == null)
        {
            return;
        }

        SerializedObject serializedObject = new SerializedObject(bridge);
        SetSerializedBool(serializedObject, "useRootMotionLocomotion", useRootMotionLocomotion);
        SetSerializedFloat(
            serializedObject,
            "rootMotionSpeedMultiplier",
            useRootMotionLocomotion ? LucianRootMotionSpeedMultiplier : 1f);
        SetSerializedFloat(
            serializedObject,
            "rootMotionRotationMultiplier",
            useRootMotionLocomotion ? LucianRootMotionRotationMultiplier : 1f);
        SetSerializedBool(
            serializedObject,
            "preferLookSourceRotationForRootMotionLocomotion",
            useRootMotionLocomotion && LucianPreferLookSourceRotationForRootMotionLocomotion);
        SetSerializedBool(
            serializedObject,
            "allowRootMotionRotationDuringStartStop",
            useRootMotionLocomotion && LucianAllowRootMotionRotationDuringStartStop);
        SetSerializedBool(
            serializedObject,
            "suppressIdleRootMotionPosition",
            useRootMotionLocomotion && LucianSuppressIdleRootMotionPosition);
        SetSerializedFloat(
            serializedObject,
            "idleRootMotionVelocityThreshold",
            LucianIdleRootMotionVelocityThreshold);
        SetSerializedBool(serializedObject, "useRootMotionPhaseMultipliers", useRootMotionLocomotion);
        SetSerializedFloat(
            serializedObject,
            "rootMotionLoopSpeedScale",
            useRootMotionLocomotion ? LucianRootMotionLoopSpeedScale : 1f);
        SetSerializedFloat(
            serializedObject,
            "rootMotionLoopRotationScale",
            useRootMotionLocomotion ? LucianRootMotionLoopRotationScale : 1f);
        SetSerializedFloat(
            serializedObject,
            "rootMotionStartSpeedScale",
            useRootMotionLocomotion ? LucianRootMotionStartSpeedScale : 1f);
        SetSerializedFloat(
            serializedObject,
            "rootMotionStartRotationScale",
            useRootMotionLocomotion ? LucianRootMotionStartRotationScale : 1f);
        SetSerializedFloat(
            serializedObject,
            "rootMotionStopSpeedScale",
            useRootMotionLocomotion ? LucianRootMotionStopSpeedScale : 1f);
        SetSerializedFloat(
            serializedObject,
            "rootMotionStopRotationScale",
            useRootMotionLocomotion ? LucianRootMotionStopRotationScale : 1f);
        SetSerializedFloat(
            serializedObject,
            "rootMotionPivotSpeedScale",
            useRootMotionLocomotion ? LucianRootMotionPivotSpeedScale : 1f);
        SetSerializedFloat(
            serializedObject,
            "rootMotionPivotRotationScale",
            useRootMotionLocomotion ? LucianRootMotionPivotRotationScale : 1f);
        SetSerializedBool(serializedObject, "relaxGroundReliefTolerance", true);
        SetSerializedFloat(serializedObject, "groundReliefMinStepHeight", LucianGroundReliefMinStepHeight);
        SetSerializedFloat(serializedObject, "groundReliefMinSlopeLimit", LucianGroundReliefMinSlopeLimit);
        SetSerializedFloat(
            serializedObject,
            "groundReliefMinStickToGroundDistance",
            LucianGroundReliefMinStickToGroundDistance);
        SetSerializedBool(serializedObject, "adaptRootMotionGroundRelief", useRootMotionLocomotion);
        SetSerializedFloat(serializedObject, "rootMotionMovingStepHeight", LucianRootMotionMovingStepHeight);
        SetSerializedFloat(serializedObject, "rootMotionMovingSlopeLimit", LucianRootMotionMovingSlopeLimit);
        SetSerializedFloat(
            serializedObject,
            "rootMotionMovingStickToGroundDistance",
            LucianRootMotionMovingStickToGroundDistance);
        SetSerializedFloat(
            serializedObject,
            "rootMotionIdleStickToGroundDistance",
            LucianRootMotionIdleStickToGroundDistance);
        SetSerializedFloat(
            serializedObject,
            "rootMotionGroundReliefAdaptationSpeed",
            LucianRootMotionGroundReliefAdaptationSpeed);
        SetSerializedBool(serializedObject, "preserveAnimatorRootMotion", true);
        SetSerializedBool(serializedObject, "restoreRootMotionSettingsOnDisable", true);
        SetSerializedBool(serializedObject, "refreshRootMotionSettingsEveryFrame", true);
        SetSerializedBool(serializedObject, "driveDirectionalRootMotionInput", false);
        SetSerializedBool(
            serializedObject,
            "useLookSourceForwardInputForRootMotion",
            useRootMotionLocomotion && LucianUseLookSourceForwardInputForRootMotion);
        SetSerializedString(serializedObject, "horizontalMovementParam", "HorizontalMovement");
        SetSerializedString(serializedObject, "forwardMovementParam", "ForwardMovement");
        SetSerializedFloat(serializedObject, "groundedInputAcceleration", LucianGroundedInputAcceleration);
        SetSerializedFloat(serializedObject, "groundedSprintInputAcceleration", LucianGroundedSprintInputAcceleration);
        SetSerializedFloat(serializedObject, "groundedInputDeceleration", LucianGroundedInputDeceleration);
        SetSerializedFloat(
            serializedObject,
            "groundedDirectionChangeAcceleration",
            LucianGroundedDirectionChangeAcceleration);
        SetSerializedFloat(serializedObject, "groundedAnimatorSpeedRiseRate", LucianGroundedAnimatorSpeedRiseRate);
        SetSerializedFloat(serializedObject, "groundedAnimatorSpeedFallRate", LucianGroundedAnimatorSpeedFallRate);
        SetSerializedFloat(serializedObject, "groundedAnimatorTurnRate", LucianGroundedAnimatorTurnRate);
        SetSerializedFloat(serializedObject, "groundedStopTriggerMinSpeed", LucianGroundedStopTriggerMinSpeed);
        SetSerializedFloat(serializedObject, "groundedRootMotionSpeedToBlend", LucianGroundedRootMotionSpeedToBlend);
        SetSerializedFloat(
            serializedObject,
            "groundedMoveTransitionDirectionHoldTime",
            LucianGroundedMoveTransitionDirectionHoldTime);
        SetSerializedFloat(
            serializedObject,
            "groundedMoveTransitionParameterSpeed",
            LucianGroundedMoveTransitionParameterSpeed);
        SetSerializedBool(
            serializedObject,
            "useForwardOnlyGroundedLocomotion",
            LucianUseForwardOnlyGroundedLocomotion);
        SetSerializedBool(serializedObject, "enableRootMotionPivotTurns", useRootMotionLocomotion);
        SetSerializedFloat(serializedObject, "groundedPivotMinAngle", LucianGroundedPivotMinAngle);
        SetSerializedFloat(serializedObject, "groundedPivot180Angle", LucianGroundedPivot180Angle);
        SetSerializedBool(serializedObject, "groundedSnapStationaryTurn", LucianGroundedSnapStationaryTurn);
        SetSerializedFloat(
            serializedObject,
            "groundedSnapStationaryTurnMinAngle",
            LucianGroundedSnapStationaryTurnMinAngle);
        SetSerializedFloat(
            serializedObject,
            "groundedSnapStationaryTurnMaxSpeed",
            LucianGroundedSnapStationaryTurnMaxSpeed);
        SetSerializedFloat(
            serializedObject,
            "groundedSnapStationaryTurnMaxSmoothedInput",
            LucianGroundedSnapStationaryTurnMaxSmoothedInput);
        SetSerializedFloat(serializedObject, "groundedPivotMaxSpeed", LucianGroundedPivotMaxSpeed);
        SetSerializedFloat(serializedObject, "groundedPivotMaxSmoothedInput", LucianGroundedPivotMaxSmoothedInput);
        SetSerializedFloat(serializedObject, "groundedPivotHoldTime", LucianGroundedPivotHoldTime);
        SetSerializedFloat(serializedObject, "groundedPivotCooldown", LucianGroundedPivotCooldown);
        SetSerializedFloat(serializedObject, "groundedPivotStartGraceTime", LucianGroundedPivotStartGraceTime);
        SetSerializedFloat(serializedObject, "groundedPivotStartGraceMinAngle", LucianGroundedPivotStartGraceMinAngle);
        SetSerializedFloat(
            serializedObject,
            "groundedPivotMovementReleaseStart",
            LucianGroundedPivotMovementReleaseStart);
        SetSerializedFloat(
            serializedObject,
            "groundedPivotMovementReleaseMaxAngle",
            LucianGroundedPivotMovementReleaseMaxAngle);
        SetSerializedFloat(
            serializedObject,
            "groundedPivotMovementReleaseScale",
            LucianGroundedPivotMovementReleaseScale);
        SetSerializedBool(serializedObject, "commitRootRotationDuringPivot", LucianCommitRootRotationDuringPivot);
        SetSerializedFloat(
            serializedObject,
            "groundedPivotRotationCommitRate",
            LucianGroundedPivotRotationCommitRate);
        SetSerializedBool(serializedObject, "enableCinematicOrientationFeel", useRootMotionLocomotion);
        SetSerializedFloat(serializedObject, "orientationInputDeadZone", LucianOrientationInputDeadZone);
        SetSerializedFloat(serializedObject, "orientationWalkTurnRate", LucianOrientationWalkTurnRate);
        SetSerializedFloat(serializedObject, "orientationSprintTurnRate", LucianOrientationSprintTurnRate);
        SetSerializedFloat(serializedObject, "orientationSharpTurnRate", LucianOrientationSharpTurnRate);
        SetSerializedFloat(serializedObject, "orientationSharpTurnAngle", LucianOrientationSharpTurnAngle);
        SetSerializedFloat(serializedObject, "orientationVelocityBlend", LucianOrientationVelocityBlend);
        SetSerializedBool(serializedObject, "driveJumpLandingAnimatorParameters", useRootMotionLocomotion);
        SetSerializedString(serializedObject, "jumpTriggerParam", "JumpTrigger");
        SetSerializedString(serializedObject, "isAirborneParam", "IsAirborne");
        SetSerializedBool(serializedObject, "enableObstacleTraversal", useRootMotionLocomotion);
        SetSerializedFloat(serializedObject, "ignoredObstacleMaxHeight", LucianIgnoredObstacleMaxHeight);
        SetSerializedFloat(serializedObject, "traversableObstacleMaxHeight", LucianTraversableObstacleMaxHeight);
        SetSerializedFloat(serializedObject, "obstacleProbeDistance", LucianObstacleProbeDistance);
        SetSerializedFloat(serializedObject, "obstacleProbeRadius", LucianObstacleProbeRadius);
        SetSerializedFloat(serializedObject, "obstacleProbeBaseHeight", LucianObstacleProbeBaseHeight);
        SetSerializedFloat(
            serializedObject,
            "obstacleTraversalMaxSurfaceUpDot",
            LucianObstacleTraversalMaxSurfaceUpDot);
        SetSerializedFloat(serializedObject, "obstacleLandingDistance", LucianObstacleLandingDistance);
        SetSerializedFloat(serializedObject, "obstacleTraversalDuration", LucianObstacleTraversalDuration);
        SetSerializedFloat(serializedObject, "obstacleTraversalArcHeight", LucianObstacleTraversalArcHeight);
        SetSerializedFloat(serializedObject, "obstacleTraversalTopClearance", LucianObstacleTraversalTopClearance);
        SetSerializedFloat(
            serializedObject,
            "obstacleTraversalHeightArcMultiplier",
            LucianObstacleTraversalHeightArcMultiplier);
        SetSerializedFloat(serializedObject, "obstacleTraversalRotationLead", LucianObstacleTraversalRotationLead);
        SetSerializedFloat(
            serializedObject,
            "obstacleTraversalMinInputMagnitude",
            LucianObstacleTraversalMinInputMagnitude);
        SetSerializedFloat(serializedObject, "obstacleTraversalCooldown", LucianObstacleTraversalCooldown);
        SetSerializedString(serializedObject, "obstacleTraversalTriggerParam", "ObstacleTraversal");
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureBridgeCompanionMode(LitOpsiveLocomotionBridge bridge, bool autoInstallCompanionBridges)
    {
        if (bridge == null)
        {
            return;
        }

        SerializedObject serializedObject = new SerializedObject(bridge);
        SerializedProperty autoInstallProperty = serializedObject.FindProperty("autoInstallCompanionBridges");
        if (autoInstallProperty != null)
        {
            autoInstallProperty.boolValue = autoInstallCompanionBridges;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void EnsureExplicitCompanionBridges(GameObject character)
    {
        EnsureComponent<LitUccInteractionBridge>(character);
        EnsureComponent<LitUccDamageBridge>(character);
        EnsureComponent<LitUccFollowerBridge>(character);
    }

    private static T EnsureComponent<T>(GameObject character) where T : Component
    {
        T component = character.GetComponent<T>();
        if (component == null)
        {
            component = character.AddComponent<T>();
        }

        EditorUtility.SetDirty(component);
        return component;
    }

    private static void EnsureSingleStandardAbilities(UltimateCharacterLocomotion locomotion)
    {
        if (locomotion == null)
        {
            return;
        }

        EnsureSingleAbility(locomotion, typeof(Jump));
        EnsureSingleAbility(locomotion, typeof(Fall));
        EnsureSingleAbility(locomotion, typeof(MoveTowards));
        EnsureSingleAbility(locomotion, typeof(SpeedChange));
        EnsureSingleAbility(locomotion, typeof(HeightChange));
    }

    private static void EnsureSingleAbility(UltimateCharacterLocomotion locomotion, Type abilityType)
    {
        Ability[] abilities = locomotion.Abilities;
        if (abilities == null || abilities.Length == 0)
        {
            AbilityBuilder.AddAbility(locomotion, abilityType);
            EditorUtility.SetDirty(locomotion);
            return;
        }

        bool found = false;
        bool changed = false;
        List<Ability> cleanedAbilities = new List<Ability>(abilities.Length);
        for (int i = 0; i < abilities.Length; i++)
        {
            Ability ability = abilities[i];
            if (ability == null)
            {
                changed = true;
                continue;
            }

            if (ability.GetType() == abilityType)
            {
                if (found)
                {
                    changed = true;
                    continue;
                }

                found = true;
            }

            cleanedAbilities.Add(ability);
        }

        if (changed)
        {
            locomotion.Abilities = cleanedAbilities.ToArray();
        }

        if (!found)
        {
            AbilityBuilder.AddAbility(locomotion, abilityType);
        }

        if (changed || !found)
        {
            EditorUtility.SetDirty(locomotion);
        }
    }

    private static void RemoveDuplicateUccColliderGroups(GameObject character)
    {
        List<GameObject> colliderGroups = new List<GameObject>();
        for (int i = 0; i < character.transform.childCount; i++)
        {
            Transform child = character.transform.GetChild(i);
            if (child.name == "Colliders" && child.GetComponent<CharacterColliderBaseIdentifier>() != null)
            {
                colliderGroups.Add(child.gameObject);
            }
        }

        for (int i = 1; i < colliderGroups.Count; i++)
        {
            UnityEngine.Object.DestroyImmediate(colliderGroups[i], true);
        }
    }

    private static void ConfigureCharacterIk(GameObject character)
    {
        Animator animator = character.GetComponent<Animator>();
        if (animator == null || !animator.isHuman)
        {
            return;
        }

        CharacterIK characterIk = character.GetComponent<CharacterIK>();
        if (characterIk == null)
        {
            characterIk = character.AddComponent<CharacterIK>();
        }

        SerializedObject serializedObject = new SerializedObject(characterIk);
        SetSerializedFloat(serializedObject, "m_LookAtBodyWeight", 0f);
        SetSerializedFloat(serializedObject, "m_LookAtHeadWeight", 0f);
        SetSerializedFloat(serializedObject, "m_LookAtEyesWeight", 0f);
        SetSerializedFloat(serializedObject, "m_UpperArmWeight", 0f);
        SetSerializedFloat(serializedObject, "m_HandWeight", 0f);
        SetSerializedFloat(serializedObject, "m_LeftHandWeight", 0f);
        SetSerializedFloat(serializedObject, "m_RightHandWeight", 0f);
        SetSerializedFloat(serializedObject, "m_LeftElbowWeight", 0f);
        SetSerializedFloat(serializedObject, "m_RightElbowWeight", 0f);
        SetSerializedBool(serializedObject, "m_IndividualHandWeightsInitialized", true);
        SetSerializedFloat(serializedObject, "m_OverrideFootIKWeight", -1f);
        SetSerializedFloat(
            serializedObject,
            "m_HipsPositionAdjustmentSpeed",
            LucianCharacterIkHipsPositionAdjustmentSpeed);
        SetSerializedFloat(
            serializedObject,
            "m_FootOffsetAdjustment",
            LucianCharacterIkFootOffsetAdjustment);
        SetSerializedFloat(
            serializedObject,
            "m_FootWeightActiveAdjustmentSpeed",
            LucianCharacterIkFootWeightActiveAdjustmentSpeed);
        SetSerializedFloat(
            serializedObject,
            "m_FootWeightInactiveAdjustmentSpeed",
            LucianCharacterIkFootWeightInactiveAdjustmentSpeed);
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(characterIk);
    }

    private static void ConfigureCharacterHealthAuthority(GameObject character)
    {
        CharacterAttributeManager attributeManager = EnsureComponent<CharacterAttributeManager>(character);
        int maxHealth = ResolveCharacterMaxHealth(character);
        ConfigureHealthAttribute(attributeManager, maxHealth);

        CharacterHealth characterHealth = EnsureComponent<CharacterHealth>(character);
        characterHealth.HealthAttributeName = UccHealthAttributeName;
        characterHealth.ShieldAttributeName = string.Empty;
        characterHealth.ApplyFallDamage = false;
        characterHealth.Invincible = false;
        characterHealth.TimeInvincibleAfterSpawn = 0f;
        characterHealth.DeactivateOnDeath = false;
        EditorUtility.SetDirty(characterHealth);
    }

    private static int ResolveCharacterMaxHealth(GameObject character)
    {
        SquadCharacterController squadController = character.GetComponent<SquadCharacterController>();
        if (squadController != null)
        {
            CharacterData data = squadController.CharacterData;
            if (data != null)
            {
                return Mathf.Max(1, data.hp);
            }

            return Mathf.Max(1, squadController.MaxHp);
        }

        return 100;
    }

    private static void ConfigureHealthAttribute(CharacterAttributeManager attributeManager, int maxHealth)
    {
        Opsive.UltimateCharacterController.Traits.Attribute[] attributes = attributeManager.Attributes;
        List<Opsive.UltimateCharacterController.Traits.Attribute> cleanedAttributes =
            new List<Opsive.UltimateCharacterController.Traits.Attribute>();
        bool healthAttributeInserted = false;

        if (attributes != null)
        {
            for (int i = 0; i < attributes.Length; i++)
            {
                Opsive.UltimateCharacterController.Traits.Attribute attribute = attributes[i];
                if (attribute == null)
                {
                    continue;
                }

                if (string.Equals(attribute.Name, UccHealthAttributeName, StringComparison.Ordinal))
                {
                    if (!healthAttributeInserted)
                    {
                        cleanedAttributes.Add(new Opsive.UltimateCharacterController.Traits.Attribute(
                            UccHealthAttributeName,
                            Mathf.Max(1, maxHealth)));
                        healthAttributeInserted = true;
                    }

                    continue;
                }

                cleanedAttributes.Add(attribute);
            }
        }

        if (!healthAttributeInserted)
        {
            cleanedAttributes.Insert(0, new Opsive.UltimateCharacterController.Traits.Attribute(
                UccHealthAttributeName,
                Mathf.Max(1, maxHealth)));
        }

        attributeManager.Attributes = cleanedAttributes.ToArray();
        EditorUtility.SetDirty(attributeManager);
    }

    private static void ConfigureDamageBridgeHealthAuthority(GameObject character)
    {
        LitUccDamageBridge damageBridge = character.GetComponent<LitUccDamageBridge>();
        if (damageBridge == null)
        {
            return;
        }

        SerializedObject serializedObject = new SerializedObject(damageBridge);
        SetSerializedObjectReference(serializedObject, "squadController", character.GetComponent<SquadCharacterController>());
        SetSerializedObjectReference(serializedObject, "attributeManager", character.GetComponent<CharacterAttributeManager>());
        SetSerializedObjectReference(serializedObject, "characterHealth", character.GetComponent<CharacterHealth>());
        SetSerializedString(serializedObject, "healthAttributeName", UccHealthAttributeName);
        SetSerializedBool(serializedObject, "characterHealthIsAuthority", true);
        SetSerializedBool(serializedObject, "syncSquadHealthFromCharacterHealth", true);
        SetSerializedBool(serializedObject, "mirrorLitHealthToOpsiveAttributes", false);
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(damageBridge);
    }

    private static void SetSerializedBool(SerializedObject serializedObject, string propertyName, bool value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.boolValue = value;
        }
    }

    private static void SetSerializedFloat(SerializedObject serializedObject, string propertyName, float value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.floatValue = value;
        }
    }

    private static void SetSerializedString(SerializedObject serializedObject, string propertyName, string value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.stringValue = value;
        }
    }

    private static void SetSerializedObjectReference(SerializedObject serializedObject, string propertyName, UnityEngine.Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.objectReferenceValue = value;
        }
    }

    private static void EnsureLookSource(GameObject character)
    {
        LitOpsiveLookSource lookSource = character.GetComponentInChildren<LitOpsiveLookSource>(true);
        if (lookSource == null)
        {
            GameObject lookSourceObject = new GameObject("LitUccLookSource");
            lookSourceObject.transform.SetParent(character.transform, false);
            lookSource = lookSourceObject.AddComponent<LitOpsiveLookSource>();
        }

        lookSource.EventTarget = character;
        SerializedObject serializedObject = new SerializedObject(lookSource);
        SetSerializedBool(serializedObject, "useStableWorldPlanarDirection", LucianUseStableWorldPlanarLookSource);
        SetSerializedFloat(serializedObject, "planarDirectionYawOffset", LucianLookSourcePlanarYawOffset);
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(lookSource);
    }

    private static void AppendPrefabUccAdoption(List<string> lines, string prefabPath, string label)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            lines.Add($"- {label}: missing prefab at {prefabPath}");
            return;
        }

        GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            int missingScripts = CountMissingScripts(contents);
            bool hasSquadController = contents.GetComponent<SquadCharacterController>() != null;
            bool hasLocomotion = contents.GetComponent<UltimateCharacterLocomotion>() != null;
            bool hasHandler = contents.GetComponent<UltimateCharacterLocomotionHandler>() != null;
            bool hasPlayerInput = contents.GetComponent<LitOpsivePlayerInput>() != null;
            bool hasBridge = contents.GetComponent<LitOpsiveLocomotionBridge>() != null;
            bool hasLookSource = contents.GetComponentInChildren<LitOpsiveLookSource>(true) != null;
            bool hasInteractionBridge = contents.GetComponent<LitUccInteractionBridge>() != null;
            bool hasDamageBridge = contents.GetComponent<LitUccDamageBridge>() != null;
            bool hasFollowerBridge = contents.GetComponent<LitUccFollowerBridge>() != null;
            bool hasCharacterIk = contents.GetComponent<CharacterIK>() != null;
            bool hasAttributeManager = contents.GetComponent<CharacterAttributeManager>() != null;
            bool hasCharacterHealth = contents.GetComponent<CharacterHealth>() != null;

            string status = hasLocomotion && hasHandler && hasPlayerInput && hasBridge && hasLookSource &&
                            hasInteractionBridge && hasDamageBridge && hasFollowerBridge && hasCharacterIk &&
                            hasAttributeManager && hasCharacterHealth
                ? "UCC-ready"
                : "needs migration";
            lines.Add(
                $"- {label}: {prefabPath}: {status}; " +
                $"SquadController={FormatBool(hasSquadController)}, " +
                $"UCCLocomotion={FormatBool(hasLocomotion)}, " +
                $"UCCHandler={FormatBool(hasHandler)}, " +
                $"LitInputBridge={FormatBool(hasPlayerInput)}, " +
                $"LocomotionBridge={FormatBool(hasBridge)}, " +
                $"InteractionBridge={FormatBool(hasInteractionBridge)}, " +
                $"DamageBridge={FormatBool(hasDamageBridge)}, " +
                $"FollowerBridge={FormatBool(hasFollowerBridge)}, " +
                $"LookSource={FormatBool(hasLookSource)}, " +
                $"CharacterIK={FormatBool(hasCharacterIk)}, " +
                $"CharacterHealth={FormatBool(hasCharacterHealth)}, " +
                $"CharacterAttributeManager={FormatBool(hasAttributeManager)}, " +
                $"MissingScripts={missingScripts}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private static int CountMissingScripts(GameObject root)
    {
        int missingScripts = 0;
        Component[] components = root.GetComponentsInChildren<Component>(true);
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] == null)
            {
                missingScripts++;
            }
        }

        return missingScripts;
    }

    private static string FormatBool(bool value)
    {
        return value ? "yes" : "no";
    }

    private static List<string> ValidateLucianUccSetupInternal(List<string> warnings)
    {
        List<string> errors = new List<string>();

        CharacterData lucianData = AssetDatabase.LoadAssetAtPath<CharacterData>(LucianCharacterDataPath);
        if (lucianData == null)
        {
            errors.Add($"Missing CharacterData: {LucianCharacterDataPath}");
            return errors;
        }

        string modelPath = lucianData.worldPrefab != null ? AssetDatabase.GetAssetPath(lucianData.worldPrefab) : null;
        if (string.IsNullOrEmpty(modelPath))
        {
            errors.Add("Lucian CharacterData WorldPrefab is null.");
        }
        else if (!string.Equals(modelPath, LucianPrefabPath, StringComparison.Ordinal))
        {
            errors.Add($"Lucian CharacterData model should be '{LucianPrefabPath}' but is '{modelPath}'.");
        }

        ValidateUccPrefab(LucianPrefabPath, "Player_Model_Lucian prefab", errors, warnings, LucianAnimatorControllerPath);

        return errors;
    }

    private static List<string> ValidateAllSquadUccSetupInternal(List<string> warnings, string skipPrefabPath = null)
    {
        List<string> errors = new List<string>();
        ValidateSquadUccPrefabs(errors, warnings, skipPrefabPath);
        ValidateSquadUccScenes(errors, warnings);
        return errors;
    }

    private static void ValidateSquadUccPrefabs(List<string> errors, List<string> warnings, string skipPrefabPath)
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
        for (int i = 0; i < prefabGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
            if (string.IsNullOrEmpty(path) ||
                string.Equals(path, skipPrefabPath, StringComparison.Ordinal))
            {
                continue;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                continue;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                SquadCharacterController[] controllers = contents.GetComponentsInChildren<SquadCharacterController>(true);
                for (int j = 0; j < controllers.Length; j++)
                {
                    SquadCharacterController controller = controllers[j];
                    if (controller == null)
                    {
                        continue;
                    }

                    string label = $"Prefab {path}/{GetHierarchyPath(controller.transform)}";
                    ValidateUccCharacter(controller.gameObject, label, errors, warnings, expectedAnimatorControllerPath: null);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }
    }

    private static void ValidateSquadUccScenes(List<string> errors, List<string> warnings)
    {
        string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
        for (int i = 0; i < sceneGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(sceneGuids[i]);
            if (string.IsNullOrEmpty(path))
            {
                continue;
            }

            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                GameObject[] roots = scene.GetRootGameObjects();
                for (int j = 0; j < roots.Length; j++)
                {
                    SquadCharacterController[] controllers = roots[j].GetComponentsInChildren<SquadCharacterController>(true);
                    for (int k = 0; k < controllers.Length; k++)
                    {
                        SquadCharacterController controller = controllers[k];
                        if (controller == null)
                        {
                            continue;
                        }

                        string label = $"Scene {path}/{GetHierarchyPath(controller.transform)}";
                        ValidateUccCharacter(controller.gameObject, label, errors, warnings, expectedAnimatorControllerPath: null);
                    }
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, removeScene: true);
            }
        }
    }

    private static string GetHierarchyPath(Transform transform)
    {
        if (transform == null)
        {
            return "<null>";
        }

        List<string> parts = new List<string>();
        Transform current = transform;
        while (current != null)
        {
            parts.Add(current.name);
            current = current.parent;
        }

        parts.Reverse();
        return string.Join("/", parts);
    }

    private static bool ShouldExpectRootMotionLocomotion(
        GameObject character,
        string expectedAnimatorControllerPath,
        LitOpsiveLocomotionBridge bridge)
    {
        if (string.Equals(expectedAnimatorControllerPath, LucianAnimatorControllerPath, StringComparison.Ordinal))
        {
            return true;
        }

        return bridge != null &&
               TryGetSerializedBool(bridge, "useRootMotionLocomotion", out bool useRootMotionLocomotion) &&
               useRootMotionLocomotion;
    }

    private static bool TryGetSerializedBool(UnityEngine.Object target, string propertyName, out bool value)
    {
        value = false;
        if (target == null)
        {
            return false;
        }

        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            return false;
        }

        value = property.boolValue;
        return true;
    }

    private static void ValidateUccPrefab(
        string prefabPath,
        string label,
        List<string> errors,
        List<string> warnings,
        string expectedAnimatorControllerPath)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            errors.Add($"Missing {label}: {prefabPath}");
            return;
        }

        GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            ValidateUccCharacter(contents, label, errors, warnings, expectedAnimatorControllerPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private static void ValidateUccCharacter(
        GameObject character,
        string label,
        List<string> errors,
        List<string> warnings,
        string expectedAnimatorControllerPath)
    {
        ValidateComponent<SquadCharacterController>(character, label, errors);
        ValidateComponent<UltimateCharacterLocomotion>(character, label, errors);
        ValidateComponent<UltimateCharacterLocomotionHandler>(character, label, errors);
        ValidateComponent<LitOpsivePlayerInput>(character, label, errors);
        ValidateComponent<LitOpsiveLocomotionBridge>(character, label, errors);
        ValidateComponent<LitUccInteractionBridge>(character, label, errors);
        ValidateComponent<LitUccDamageBridge>(character, label, errors);
        ValidateComponent<LitUccFollowerBridge>(character, label, errors);
        ValidateComponent<CharacterAttributeManager>(character, label, errors);
        ValidateComponent<CharacterHealth>(character, label, errors);
        ValidateNoComponentByTypeName(character, label, "StarterInspiredThirdPersonMotor", errors);
        ValidateNoComponentByTypeName(character, label, "StarterMotorAnimatorDriver", errors);
        ValidateNoComponentByTypeName(character, label, "StarterMotorLocalInputBridge", errors);
        if (!string.IsNullOrEmpty(expectedAnimatorControllerPath))
        {
            ValidateAnimatorController(character, label, errors, expectedAnimatorControllerPath);
        }

        ValidateCharacterIkMigration(character, label, errors);
        ValidateCharacterHealthMigration(character, label, errors);
        ValidateSingleUccColliderGroup(character, label, errors);

        LitOpsiveLookSource lookSource = character.GetComponentInChildren<LitOpsiveLookSource>(true);
        if (lookSource == null)
        {
            errors.Add($"{label} is missing LitOpsiveLookSource in children.");
        }
        else if (lookSource.EventTarget != character)
        {
            errors.Add($"{label} LitOpsiveLookSource EventTarget does not point to the character root.");
        }
        else
        {
            ValidateSerializedBool(lookSource, "useStableWorldPlanarDirection", LucianUseStableWorldPlanarLookSource, errors);
            ValidateSerializedFloat(lookSource, "planarDirectionYawOffset", LucianLookSourcePlanarYawOffset, errors);
        }

        LitOpsiveLocomotionBridge bridge = character.GetComponent<LitOpsiveLocomotionBridge>();
        bool expectedRootMotionLocomotion = ShouldExpectRootMotionLocomotion(character, expectedAnimatorControllerPath, bridge);

        UltimateCharacterLocomotion locomotion = character.GetComponent<UltimateCharacterLocomotion>();
        if (locomotion != null)
        {
            ValidateLocomotionAnimationMode(
                locomotion,
                label,
                errors,
                expectedRootMotionPosition: expectedRootMotionLocomotion,
                expectedRootMotionRotation: expectedRootMotionLocomotion,
                expectedRootMotionSpeedMultiplier: expectedRootMotionLocomotion ? LucianRootMotionSpeedMultiplier : 1f,
                expectedRootMotionRotationMultiplier: expectedRootMotionLocomotion ? LucianRootMotionRotationMultiplier : 1f);
            ValidateSingleAbility<Jump>(locomotion, label, errors);
            ValidateSingleAbility<Fall>(locomotion, label, errors);
            ValidateSingleAbility<MoveTowards>(locomotion, label, errors);
            ValidateSingleAbility<SpeedChange>(locomotion, label, errors);
            ValidateSingleAbility<HeightChange>(locomotion, label, errors);
        }

        AnimatorMonitor animatorMonitor = character.GetComponent<AnimatorMonitor>();
        if (animatorMonitor == null)
        {
            errors.Add($"{label} is missing AnimatorMonitor.");
        }
        else
        {
            ValidateAnimatorMonitor(
                animatorMonitor,
                label,
                errors,
                expectedRootMotionLocomotion ? LucianRootMotionAnimatorSpeed : UccAnimatorSpeed);
        }

        if (bridge != null)
        {
            ValidateBridgeBoolean(bridge, label, "driveFromSquadFacade", true, errors);
            ValidateBridgeBoolean(bridge, label, "overrideOpsiveHandlerInput", true, errors);
            ValidateBridgeBoolean(bridge, label, "orientLookSourceFromMovement", true, errors);
            ValidateBridgeBoolean(bridge, label, "configureRigidbodyForOpsive", true, errors);
            ValidateBridgeBoolean(bridge, label, "useRootMotionLocomotion", expectedRootMotionLocomotion, errors);
            ValidateBridgeBoolean(bridge, label, "refreshRootMotionSettingsEveryFrame", true, errors);
            ValidateBridgeBoolean(bridge, label, "driveDirectionalRootMotionInput", false, errors);
            ValidateBridgeBoolean(
                bridge,
                label,
                "useLookSourceForwardInputForRootMotion",
                expectedRootMotionLocomotion && LucianUseLookSourceForwardInputForRootMotion,
                errors);
            ValidateBridgeBoolean(bridge, label, "enableRootMotionPivotTurns", expectedRootMotionLocomotion, errors);
            ValidateBridgeString(bridge, label, "horizontalMovementParam", "HorizontalMovement", errors);
            ValidateBridgeString(bridge, label, "forwardMovementParam", "ForwardMovement", errors);
            ValidateSerializedFloat(
                bridge,
                "rootMotionSpeedMultiplier",
                expectedRootMotionLocomotion ? LucianRootMotionSpeedMultiplier : 1f,
                errors);
            ValidateSerializedFloat(
                bridge,
                "rootMotionRotationMultiplier",
                expectedRootMotionLocomotion ? LucianRootMotionRotationMultiplier : 1f,
                errors);
            ValidateBridgeBoolean(
                bridge,
                label,
                "preferLookSourceRotationForRootMotionLocomotion",
                expectedRootMotionLocomotion && LucianPreferLookSourceRotationForRootMotionLocomotion,
                errors);
            ValidateBridgeBoolean(
                bridge,
                label,
                "allowRootMotionRotationDuringStartStop",
                expectedRootMotionLocomotion && LucianAllowRootMotionRotationDuringStartStop,
                errors);
            ValidateBridgeBoolean(
                bridge,
                label,
                "suppressIdleRootMotionPosition",
                expectedRootMotionLocomotion && LucianSuppressIdleRootMotionPosition,
                errors);
            ValidateSerializedFloat(bridge, "idleRootMotionVelocityThreshold", LucianIdleRootMotionVelocityThreshold, errors);
            ValidateBridgeBoolean(bridge, label, "useRootMotionPhaseMultipliers", expectedRootMotionLocomotion, errors);
            ValidateSerializedFloat(
                bridge,
                "rootMotionLoopSpeedScale",
                expectedRootMotionLocomotion ? LucianRootMotionLoopSpeedScale : 1f,
                errors);
            ValidateSerializedFloat(
                bridge,
                "rootMotionLoopRotationScale",
                expectedRootMotionLocomotion ? LucianRootMotionLoopRotationScale : 1f,
                errors);
            ValidateSerializedFloat(
                bridge,
                "rootMotionStartSpeedScale",
                expectedRootMotionLocomotion ? LucianRootMotionStartSpeedScale : 1f,
                errors);
            ValidateSerializedFloat(
                bridge,
                "rootMotionStartRotationScale",
                expectedRootMotionLocomotion ? LucianRootMotionStartRotationScale : 1f,
                errors);
            ValidateSerializedFloat(
                bridge,
                "rootMotionStopSpeedScale",
                expectedRootMotionLocomotion ? LucianRootMotionStopSpeedScale : 1f,
                errors);
            ValidateSerializedFloat(
                bridge,
                "rootMotionStopRotationScale",
                expectedRootMotionLocomotion ? LucianRootMotionStopRotationScale : 1f,
                errors);
            ValidateSerializedFloat(
                bridge,
                "rootMotionPivotSpeedScale",
                expectedRootMotionLocomotion ? LucianRootMotionPivotSpeedScale : 1f,
                errors);
            ValidateSerializedFloat(
                bridge,
                "rootMotionPivotRotationScale",
                expectedRootMotionLocomotion ? LucianRootMotionPivotRotationScale : 1f,
                errors);
            ValidateBridgeBoolean(bridge, label, "relaxGroundReliefTolerance", true, errors);
            ValidateSerializedFloat(bridge, "groundReliefMinStepHeight", LucianGroundReliefMinStepHeight, errors);
            ValidateSerializedFloat(bridge, "groundReliefMinSlopeLimit", LucianGroundReliefMinSlopeLimit, errors);
            ValidateSerializedFloat(
                bridge,
                "groundReliefMinStickToGroundDistance",
                LucianGroundReliefMinStickToGroundDistance,
                errors);
            ValidateBridgeBoolean(
                bridge,
                label,
                "adaptRootMotionGroundRelief",
                expectedRootMotionLocomotion,
                errors);
            ValidateSerializedFloat(bridge, "rootMotionMovingStepHeight", LucianRootMotionMovingStepHeight, errors);
            ValidateSerializedFloat(bridge, "rootMotionMovingSlopeLimit", LucianRootMotionMovingSlopeLimit, errors);
            ValidateSerializedFloat(
                bridge,
                "rootMotionMovingStickToGroundDistance",
                LucianRootMotionMovingStickToGroundDistance,
                errors);
            ValidateSerializedFloat(
                bridge,
                "rootMotionIdleStickToGroundDistance",
                LucianRootMotionIdleStickToGroundDistance,
                errors);
            ValidateSerializedFloat(
                bridge,
                "rootMotionGroundReliefAdaptationSpeed",
                LucianRootMotionGroundReliefAdaptationSpeed,
                errors);
            ValidateSerializedFloat(bridge, "groundedInputAcceleration", LucianGroundedInputAcceleration, errors);
            ValidateSerializedFloat(
                bridge,
                "groundedSprintInputAcceleration",
                LucianGroundedSprintInputAcceleration,
                errors);
            ValidateSerializedFloat(bridge, "groundedInputDeceleration", LucianGroundedInputDeceleration, errors);
            ValidateSerializedFloat(
                bridge,
                "groundedDirectionChangeAcceleration",
                LucianGroundedDirectionChangeAcceleration,
                errors);
            ValidateSerializedFloat(
                bridge,
                "groundedAnimatorSpeedRiseRate",
                LucianGroundedAnimatorSpeedRiseRate,
                errors);
            ValidateSerializedFloat(
                bridge,
                "groundedAnimatorSpeedFallRate",
                LucianGroundedAnimatorSpeedFallRate,
                errors);
            ValidateSerializedFloat(bridge, "groundedAnimatorTurnRate", LucianGroundedAnimatorTurnRate, errors);
            ValidateSerializedFloat(bridge, "groundedStopTriggerMinSpeed", LucianGroundedStopTriggerMinSpeed, errors);
            ValidateSerializedFloat(
                bridge,
                "groundedMoveTransitionDirectionHoldTime",
                LucianGroundedMoveTransitionDirectionHoldTime,
                errors);
            ValidateSerializedFloat(
                bridge,
                "groundedMoveTransitionParameterSpeed",
                LucianGroundedMoveTransitionParameterSpeed,
                errors);
            ValidateBridgeBoolean(
                bridge,
                label,
                "useForwardOnlyGroundedLocomotion",
                LucianUseForwardOnlyGroundedLocomotion,
                errors);
            ValidateSerializedFloat(bridge, "groundedPivotMinAngle", LucianGroundedPivotMinAngle, errors);
            ValidateSerializedFloat(bridge, "groundedPivot180Angle", LucianGroundedPivot180Angle, errors);
            ValidateBridgeBoolean(
                bridge,
                label,
                "groundedSnapStationaryTurn",
                LucianGroundedSnapStationaryTurn,
                errors);
            ValidateSerializedFloat(
                bridge,
                "groundedSnapStationaryTurnMinAngle",
                LucianGroundedSnapStationaryTurnMinAngle,
                errors);
            ValidateSerializedFloat(
                bridge,
                "groundedSnapStationaryTurnMaxSpeed",
                LucianGroundedSnapStationaryTurnMaxSpeed,
                errors);
            ValidateSerializedFloat(
                bridge,
                "groundedSnapStationaryTurnMaxSmoothedInput",
                LucianGroundedSnapStationaryTurnMaxSmoothedInput,
                errors);
            ValidateSerializedFloat(bridge, "groundedPivotMaxSpeed", LucianGroundedPivotMaxSpeed, errors);
            ValidateSerializedFloat(bridge, "groundedPivotMaxSmoothedInput", LucianGroundedPivotMaxSmoothedInput, errors);
            ValidateSerializedFloat(bridge, "groundedPivotHoldTime", LucianGroundedPivotHoldTime, errors);
            ValidateSerializedFloat(bridge, "groundedPivotCooldown", LucianGroundedPivotCooldown, errors);
            ValidateSerializedFloat(bridge, "groundedPivotStartGraceTime", LucianGroundedPivotStartGraceTime, errors);
            ValidateSerializedFloat(bridge, "groundedPivotStartGraceMinAngle", LucianGroundedPivotStartGraceMinAngle, errors);
            ValidateSerializedFloat(
                bridge,
                "groundedPivotMovementReleaseStart",
                LucianGroundedPivotMovementReleaseStart,
                errors);
            ValidateSerializedFloat(
                bridge,
                "groundedPivotMovementReleaseMaxAngle",
                LucianGroundedPivotMovementReleaseMaxAngle,
                errors);
            ValidateSerializedFloat(
                bridge,
                "groundedPivotMovementReleaseScale",
                LucianGroundedPivotMovementReleaseScale,
                errors);
            ValidateBridgeBoolean(
                bridge,
                label,
                "commitRootRotationDuringPivot",
                LucianCommitRootRotationDuringPivot,
                errors);
            ValidateSerializedFloat(
                bridge,
                "groundedPivotRotationCommitRate",
                LucianGroundedPivotRotationCommitRate,
                errors);
            ValidateBridgeBoolean(
                bridge,
                label,
                "enableCinematicOrientationFeel",
                expectedRootMotionLocomotion,
                errors);
            ValidateSerializedFloat(bridge, "orientationInputDeadZone", LucianOrientationInputDeadZone, errors);
            ValidateSerializedFloat(bridge, "orientationWalkTurnRate", LucianOrientationWalkTurnRate, errors);
            ValidateSerializedFloat(bridge, "orientationSprintTurnRate", LucianOrientationSprintTurnRate, errors);
            ValidateSerializedFloat(bridge, "orientationSharpTurnRate", LucianOrientationSharpTurnRate, errors);
            ValidateSerializedFloat(bridge, "orientationSharpTurnAngle", LucianOrientationSharpTurnAngle, errors);
            ValidateSerializedFloat(bridge, "orientationVelocityBlend", LucianOrientationVelocityBlend, errors);
            ValidateBridgeBoolean(
                bridge,
                label,
                "driveJumpLandingAnimatorParameters",
                expectedRootMotionLocomotion,
                errors);
            ValidateBridgeString(bridge, label, "jumpTriggerParam", "JumpTrigger", errors);
            ValidateBridgeString(bridge, label, "isAirborneParam", "IsAirborne", errors);
            ValidateBridgeBoolean(bridge, label, "enableObstacleTraversal", expectedRootMotionLocomotion, errors);
            ValidateSerializedFloat(bridge, "ignoredObstacleMaxHeight", LucianIgnoredObstacleMaxHeight, errors);
            ValidateSerializedFloat(bridge, "traversableObstacleMaxHeight", LucianTraversableObstacleMaxHeight, errors);
            ValidateSerializedFloat(bridge, "obstacleProbeDistance", LucianObstacleProbeDistance, errors);
            ValidateSerializedFloat(bridge, "obstacleProbeRadius", LucianObstacleProbeRadius, errors);
            ValidateSerializedFloat(bridge, "obstacleProbeBaseHeight", LucianObstacleProbeBaseHeight, errors);
            ValidateSerializedFloat(
                bridge,
                "obstacleTraversalMaxSurfaceUpDot",
                LucianObstacleTraversalMaxSurfaceUpDot,
                errors);
            ValidateSerializedFloat(bridge, "obstacleLandingDistance", LucianObstacleLandingDistance, errors);
            ValidateSerializedFloat(bridge, "obstacleTraversalDuration", LucianObstacleTraversalDuration, errors);
            ValidateSerializedFloat(bridge, "obstacleTraversalArcHeight", LucianObstacleTraversalArcHeight, errors);
            ValidateSerializedFloat(bridge, "obstacleTraversalTopClearance", LucianObstacleTraversalTopClearance, errors);
            ValidateSerializedFloat(
                bridge,
                "obstacleTraversalHeightArcMultiplier",
                LucianObstacleTraversalHeightArcMultiplier,
                errors);
            ValidateSerializedFloat(bridge, "obstacleTraversalRotationLead", LucianObstacleTraversalRotationLead, errors);
            ValidateSerializedFloat(
                bridge,
                "obstacleTraversalMinInputMagnitude",
                LucianObstacleTraversalMinInputMagnitude,
                errors);
            ValidateSerializedFloat(bridge, "obstacleTraversalCooldown", LucianObstacleTraversalCooldown, errors);
            ValidateBridgeString(bridge, label, "obstacleTraversalTriggerParam", "ObstacleTraversal", errors);
            ValidateBridgeBoolean(bridge, label, "autoInstallCompanionBridges", false, errors);
            ValidateBridgeBoolean(bridge, label, "driveLitLocomotionAnimatorParameters", true, errors);
        }

        LitUccDamageBridge damageBridge = character.GetComponent<LitUccDamageBridge>();
        if (damageBridge != null)
        {
            ValidateBridgeBoolean(damageBridge, label, "characterHealthIsAuthority", true, errors);
            ValidateBridgeBoolean(damageBridge, label, "syncSquadHealthFromCharacterHealth", true, errors);
            ValidateBridgeBoolean(damageBridge, label, "mirrorLitHealthToOpsiveAttributes", false, errors);
            ValidateBridgeObjectReference(damageBridge, label, "squadController", character.GetComponent<SquadCharacterController>(), errors);
            ValidateBridgeObjectReference(damageBridge, label, "attributeManager", character.GetComponent<CharacterAttributeManager>(), errors);
            ValidateBridgeObjectReference(damageBridge, label, "characterHealth", character.GetComponent<CharacterHealth>(), errors);
            ValidateBridgeString(damageBridge, label, "healthAttributeName", UccHealthAttributeName, errors);
        }

        Component[] components = character.GetComponentsInChildren<Component>(true);
        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];
            if (component == null)
            {
                errors.Add($"{label} contains a missing script reference.");
                continue;
            }

            Type type = component.GetType();
            if (type.Name == "CameraController" &&
                type.FullName != null &&
                type.FullName.StartsWith("Opsive.", StringComparison.Ordinal))
            {
                errors.Add($"{label} contains an unexpected Opsive camera controller component: {type.FullName}.");
            }
        }
    }

    private static void ValidateComponent<T>(GameObject character, string label, List<string> errors) where T : Component
    {
        if (character.GetComponent<T>() == null)
        {
            errors.Add($"{label} is missing required component: {typeof(T).Name}.");
        }
    }

    private static void ValidateNoComponentByTypeName(GameObject character, string label, string typeName, List<string> errors)
    {
        Component[] components = character.GetComponentsInChildren<Component>(true);
        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];
            if (component == null)
            {
                continue;
            }

            Type type = component.GetType();
            if (string.Equals(type.Name, typeName, StringComparison.Ordinal))
            {
                errors.Add($"{label} should not carry legacy Starter motor component: {typeName}.");
            }
        }
    }

    private static void ValidateCharacterIkMigration(GameObject character, string label, List<string> errors)
    {
        Animator animator = character.GetComponent<Animator>();
        if (animator == null)
        {
            return;
        }

        if (!animator.isHuman)
        {
            errors.Add($"{label} Animator must be humanoid for UCC CharacterIK.");
            return;
        }

        CharacterIK characterIk = character.GetComponent<CharacterIK>();
        if (characterIk == null)
        {
            errors.Add($"Missing required component: {nameof(CharacterIK)}.");
        }
        else
        {
            ValidateSerializedFloat(characterIk, "m_LookAtBodyWeight", 0f, errors);
            ValidateSerializedFloat(characterIk, "m_LookAtHeadWeight", 0f, errors);
            ValidateSerializedFloat(characterIk, "m_LookAtEyesWeight", 0f, errors);
            ValidateSerializedFloat(characterIk, "m_UpperArmWeight", 0f, errors);
            ValidateSerializedFloat(characterIk, "m_LeftHandWeight", 0f, errors);
            ValidateSerializedFloat(characterIk, "m_RightHandWeight", 0f, errors);
            ValidateSerializedFloat(characterIk, "m_LeftElbowWeight", 0f, errors);
            ValidateSerializedFloat(characterIk, "m_RightElbowWeight", 0f, errors);
            ValidateSerializedFloat(characterIk, "m_OverrideFootIKWeight", -1f, errors);
            ValidateSerializedFloat(
                characterIk,
                "m_HipsPositionAdjustmentSpeed",
                LucianCharacterIkHipsPositionAdjustmentSpeed,
                errors);
            ValidateSerializedFloat(
                characterIk,
                "m_FootOffsetAdjustment",
                LucianCharacterIkFootOffsetAdjustment,
                errors);
            ValidateSerializedFloat(
                characterIk,
                "m_FootWeightActiveAdjustmentSpeed",
                LucianCharacterIkFootWeightActiveAdjustmentSpeed,
                errors);
            ValidateSerializedFloat(
                characterIk,
                "m_FootWeightInactiveAdjustmentSpeed",
                LucianCharacterIkFootWeightInactiveAdjustmentSpeed,
                errors);
        }
    }

    private static void ValidateCharacterHealthMigration(GameObject character, string label, List<string> errors)
    {
        CharacterAttributeManager attributeManager = character.GetComponent<CharacterAttributeManager>();
        if (attributeManager == null)
        {
            errors.Add($"Missing required component: {nameof(CharacterAttributeManager)}.");
            return;
        }

        int healthAttributeCount = 0;
        Opsive.UltimateCharacterController.Traits.Attribute healthAttribute = null;
        Opsive.UltimateCharacterController.Traits.Attribute[] attributes = attributeManager.Attributes;
        if (attributes != null)
        {
            for (int i = 0; i < attributes.Length; i++)
            {
                Opsive.UltimateCharacterController.Traits.Attribute attribute = attributes[i];
                if (attribute == null || !string.Equals(attribute.Name, UccHealthAttributeName, StringComparison.Ordinal))
                {
                    continue;
                }

                healthAttributeCount++;
                healthAttribute = attribute;
            }
        }

        if (healthAttributeCount == 0)
        {
            errors.Add($"{label} CharacterAttributeManager is missing the '{UccHealthAttributeName}' attribute.");
        }
        else if (healthAttributeCount > 1)
        {
            errors.Add($"{label} CharacterAttributeManager has duplicate '{UccHealthAttributeName}' attributes.");
        }
        else if (healthAttribute.MaxValue <= 0f)
        {
            errors.Add($"{label} UCC health attribute must have a positive MaxValue.");
        }

        CharacterHealth characterHealth = character.GetComponent<CharacterHealth>();
        if (characterHealth == null)
        {
            errors.Add($"Missing required component: {nameof(CharacterHealth)}.");
            return;
        }

        if (!string.Equals(characterHealth.HealthAttributeName, UccHealthAttributeName, StringComparison.Ordinal))
        {
            errors.Add($"{label} CharacterHealth should use '{UccHealthAttributeName}' as its health attribute.");
        }

        if (!string.IsNullOrEmpty(characterHealth.ShieldAttributeName))
        {
            errors.Add($"{label} CharacterHealth shield attribute should be empty while CharacterHealth owns squad health.");
        }

        if (characterHealth.ApplyFallDamage)
        {
            errors.Add($"{label} CharacterHealth fall damage should stay disabled until fall damage has been routed through the squad health policy.");
        }

        if (characterHealth.Invincible)
        {
            errors.Add($"{label} CharacterHealth should not be invincible when it is the health authority.");
        }

        if (characterHealth.DeactivateOnDeath)
        {
            errors.Add($"{label} CharacterHealth should not deactivate the character while Lit still owns character lifetime.");
        }
    }

    private static void ValidateSerializedFloat(UnityEngine.Object target, string propertyName, float expected, List<string> errors)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            errors.Add($"{target.GetType().Name} serialized property missing: {propertyName}.");
            return;
        }

        if (Mathf.Abs(property.floatValue - expected) > 0.001f)
        {
            errors.Add($"{target.GetType().Name} property '{propertyName}' should be {expected} but is {property.floatValue}.");
        }
    }

    private static void ValidateSerializedBool(UnityEngine.Object target, string propertyName, bool expected, List<string> errors)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            errors.Add($"{target.GetType().Name} serialized property missing: {propertyName}.");
            return;
        }

        if (property.boolValue != expected)
        {
            errors.Add($"{target.GetType().Name} property '{propertyName}' should be {expected}.");
        }
    }

    private static void ValidateSingleUccColliderGroup(GameObject character, string label, List<string> errors)
    {
        int colliderGroupCount = 0;
        for (int i = 0; i < character.transform.childCount; i++)
        {
            Transform child = character.transform.GetChild(i);
            if (child.name == "Colliders" && child.GetComponent<CharacterColliderBaseIdentifier>() != null)
            {
                colliderGroupCount++;
            }
        }

        if (colliderGroupCount == 0)
        {
            errors.Add($"{label} is missing the UCC Colliders child with CharacterColliderBaseIdentifier.");
        }
        else if (colliderGroupCount > 1)
        {
            errors.Add($"{label} contains {colliderGroupCount} UCC Colliders children; expected exactly one.");
        }
    }

    private static void ValidateSingleAbility<T>(UltimateCharacterLocomotion locomotion, string label, List<string> errors) where T : Ability
    {
        T[] abilities = locomotion.GetAbilities<T>();
        if (abilities == null || abilities.Length == 0)
        {
            errors.Add($"{label} is missing required UCC ability: {typeof(T).Name}.");
        }
        else if (abilities.Length > 1)
        {
            errors.Add($"{label} has duplicate required UCC ability: {typeof(T).Name} appears {abilities.Length} times.");
        }
    }

    private static void ValidateAnimatorController(GameObject character, string label, List<string> errors, string expectedControllerPath)
    {
        Animator animator = character.GetComponent<Animator>();
        if (animator == null)
        {
            errors.Add($"Missing Animator in {label}.");
            return;
        }

        RuntimeAnimatorController controller = ResolveAnimatorController(expectedControllerPath);
        if (controller == null)
        {
            errors.Add($"Missing animator controller asset: {expectedControllerPath}");
            return;
        }

        if (animator.runtimeAnimatorController != controller)
        {
            string currentPath = animator.runtimeAnimatorController != null
                ? AssetDatabase.GetAssetPath(animator.runtimeAnimatorController)
                : "<none>";
            errors.Add($"{label} Animator Controller should be '{expectedControllerPath}' but is '{currentPath}'.");
        }
    }

    private static void ValidateLocomotionAnimationMode(
        UltimateCharacterLocomotion locomotion,
        string label,
        List<string> errors,
        bool expectedRootMotionPosition,
        bool expectedRootMotionRotation,
        float expectedRootMotionSpeedMultiplier,
        float expectedRootMotionRotationMultiplier)
    {
        SerializedObject serializedObject = new SerializedObject(locomotion);
        SerializedProperty rootMotionPosition = serializedObject.FindProperty("m_UseRootMotionPosition");
        if (rootMotionPosition != null && rootMotionPosition.boolValue != expectedRootMotionPosition)
        {
            errors.Add($"{label} m_UseRootMotionPosition should be {expectedRootMotionPosition}.");
        }

        SerializedProperty rootMotionSpeedMultiplier = serializedObject.FindProperty("m_RootMotionSpeedMultiplier");
        if (rootMotionSpeedMultiplier != null && Mathf.Abs(rootMotionSpeedMultiplier.floatValue - expectedRootMotionSpeedMultiplier) > 0.001f)
        {
            errors.Add($"{label} m_RootMotionSpeedMultiplier should be {expectedRootMotionSpeedMultiplier} but is {rootMotionSpeedMultiplier.floatValue}.");
        }

        SerializedProperty rootMotionRotation = serializedObject.FindProperty("m_UseRootMotionRotation");
        if (rootMotionRotation != null && rootMotionRotation.boolValue != expectedRootMotionRotation)
        {
            errors.Add($"{label} m_UseRootMotionRotation should be {expectedRootMotionRotation}.");
        }

        SerializedProperty rootMotionRotationMultiplier = serializedObject.FindProperty("m_RootMotionRotationMultiplier");
        if (rootMotionRotationMultiplier != null && Mathf.Abs(rootMotionRotationMultiplier.floatValue - expectedRootMotionRotationMultiplier) > 0.001f)
        {
            errors.Add($"{label} m_RootMotionRotationMultiplier should be {expectedRootMotionRotationMultiplier} but is {rootMotionRotationMultiplier.floatValue}.");
        }

        SerializedProperty motorRotationSpeed = serializedObject.FindProperty("m_MotorRotationSpeed");
        if (motorRotationSpeed != null && Mathf.Abs(motorRotationSpeed.floatValue - 0.14f) > 0.001f)
        {
            errors.Add($"{label} m_MotorRotationSpeed should match the UCC sample value 0.14 but is {motorRotationSpeed.floatValue}.");
        }
    }

    private static void ValidateAnimatorMonitor(
        AnimatorMonitor animatorMonitor,
        string label,
        List<string> errors,
        float expectedAnimatorSpeed)
    {
        SerializedObject serializedObject = new SerializedObject(animatorMonitor);
        SerializedProperty animatorSpeed = serializedObject.FindProperty("m_AnimatorSpeed");
        if (animatorSpeed != null && Mathf.Abs(animatorSpeed.floatValue - expectedAnimatorSpeed) > 0.001f)
        {
            errors.Add($"{label} AnimatorMonitor speed should be {expectedAnimatorSpeed} but is {animatorSpeed.floatValue}.");
        }
    }

    private static void ValidateBridgeBoolean(UnityEngine.Object target, string label, string propertyName, bool expected, List<string> errors)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            errors.Add($"{label} bridge serialized property missing: {propertyName}.");
            return;
        }

        if (property.boolValue != expected)
        {
            errors.Add($"{label} bridge property '{propertyName}' should be {expected}.");
        }
    }

    private static void ValidateBridgeString(UnityEngine.Object target, string label, string propertyName, string expected, List<string> errors)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            errors.Add($"{label} bridge serialized property missing: {propertyName}.");
            return;
        }

        if (!string.Equals(property.stringValue, expected, StringComparison.Ordinal))
        {
            errors.Add($"{label} bridge property '{propertyName}' should be '{expected}' but is '{property.stringValue}'.");
        }
    }

    private static void ValidateBridgeObjectReference(
        UnityEngine.Object target,
        string label,
        string propertyName,
        UnityEngine.Object expected,
        List<string> errors)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            errors.Add($"{label} bridge serialized property missing: {propertyName}.");
            return;
        }

        if (property.objectReferenceValue != expected)
        {
            string expectedName = expected != null ? expected.GetType().Name : "<null>";
            string currentName = property.objectReferenceValue != null
                ? property.objectReferenceValue.GetType().Name
                : "<null>";
            errors.Add($"{label} bridge property '{propertyName}' should reference {expectedName} but references {currentName}.");
        }
    }
}
