using System;
using System.Collections.Generic;
using Opsive.UltimateCharacterController.Character;
using Opsive.UltimateCharacterController.Character.Abilities;
using Opsive.UltimateCharacterController.Utility.Builders;
using UnityEditor;
using UnityEngine;

public static class LitOpsiveUccMigrationUtility
{
    private const string MenuRoot = "Lit/Opsive UCC/";
    private const string UccSuffix = "_UCC";
    private const string AdventureMovementType = "Opsive.UltimateCharacterController.ThirdPersonController.Character.MovementTypes.Adventure";
    private const string UccDemoAnimatorControllerPath = "Assets/Opsive/UltimateCharacterController/RuntimeAnimator/Characters/Demo.controller";
    private const string LucianAnimatorControllerPath = "Assets/Animations/Player_Model.controller";
    private const float UccAnimatorSpeed = 0.8f;
    private const string LucianCharacterDataPath = "Assets/ScriptableObjects/CharacterData/Lucian.asset";
    private const string LucianPrefabPath = "Assets/Prefabs/Character/Player_Model_Lucian.prefab";
    private static readonly string[] KnownPlayerCharacterPrefabPaths =
    {
        "Assets/Prefabs/Character/Player_Model_Trooper.prefab",
        "Assets/Prefabs/Character/Player_Model_MechanicGirl.prefab",
        "Assets/Prefabs/Character/Player_Model_Lucian.prefab"
    };

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

    [MenuItem(MenuRoot + "Configure Lucian Prefab In Place")]
    public static void ConfigureLucianPrefabInPlace()
    {
        ConfigurePrefabInPlace(LucianPrefabPath);
    }

    [MenuItem(MenuRoot + "Validate Lucian UCC Setup")]
    public static void ValidateLucianUccSetup()
    {
        List<string> errors = ValidateLucianUccSetupInternal();
        if (errors.Count == 0)
        {
            Debug.Log("Lucian UCC setup validation passed.");
            return;
        }

        string message = "Lucian UCC setup validation failed:\n- " + string.Join("\n- ", errors);
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

        string directory = System.IO.Path.GetDirectoryName(sourcePath);
        string filename = System.IO.Path.GetFileNameWithoutExtension(sourcePath);
        string targetPath = $"{directory}/{filename}{UccSuffix}.prefab";
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

#if ENABLE_INPUT_SYSTEM
        CharacterBuilder.BuildCharacter(character, new[] { character }, true, new[] { animatorController }, string.Empty, AdventureMovementType, false, null, null, false, false, null);
#else
        CharacterBuilder.BuildCharacter(character, new[] { character }, true, new[] { animatorController }, string.Empty, AdventureMovementType, false, null, null, false, false);
#endif
        CharacterBuilder.BuildCharacterComponents(character, false, false, null, null, false, false, false, false, true, false);
        CharacterBuilder.RemoveUnityInput(character);

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
            useRootMotionPosition: !useLucianAnimatorController);
        ConfigureAnimatorMonitor(character.GetComponent<AnimatorMonitor>());
        EnsureLookSource(character);

        LitOpsiveLocomotionBridge bridge = character.GetComponent<LitOpsiveLocomotionBridge>();
        if (bridge == null)
        {
            bridge = character.AddComponent<LitOpsiveLocomotionBridge>();
        }

        ConfigureBridgeAnimatorMode(bridge, driveLitAnimatorParameters: useLucianAnimatorController);
    }

    private static void ConfigureLocomotionAnimationMode(UltimateCharacterLocomotion locomotion, bool useRootMotionPosition)
    {
        if (locomotion == null)
        {
            return;
        }

        SerializedObject serializedObject = new SerializedObject(locomotion);
        SetSerializedBool(serializedObject, "m_UseRootMotionPosition", useRootMotionPosition);
        SetSerializedFloat(serializedObject, "m_MotorRotationSpeed", 0.14f);
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureAnimatorMonitor(AnimatorMonitor animatorMonitor)
    {
        if (animatorMonitor == null)
        {
            return;
        }

        SerializedObject serializedObject = new SerializedObject(animatorMonitor);
        SetSerializedFloat(serializedObject, "m_AnimatorSpeed", UccAnimatorSpeed);
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

        return controller;
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
        EditorUtility.SetDirty(lookSource);
    }

    private static List<string> ValidateLucianUccSetupInternal()
    {
        List<string> errors = new List<string>();

        CharacterData lucianData = AssetDatabase.LoadAssetAtPath<CharacterData>(LucianCharacterDataPath);
        if (lucianData == null)
        {
            errors.Add($"Missing CharacterData: {LucianCharacterDataPath}");
            return errors;
        }

        string modelPath = lucianData.model != null ? AssetDatabase.GetAssetPath(lucianData.model) : null;
        if (string.IsNullOrEmpty(modelPath))
        {
            errors.Add("Lucian CharacterData model is null.");
        }
        else if (!string.Equals(modelPath, LucianPrefabPath, StringComparison.Ordinal))
        {
            errors.Add($"Lucian CharacterData model should be '{LucianPrefabPath}' but is '{modelPath}'.");
        }

        ValidateUccPrefab(LucianPrefabPath, "Player_Model_Lucian prefab", errors);

        return errors;
    }

    private static void ValidateUccPrefab(string prefabPath, string label, List<string> errors)
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
            ValidateComponent<SquadCharacterController>(contents, errors);
            ValidateComponent<UltimateCharacterLocomotion>(contents, errors);
            ValidateComponent<UltimateCharacterLocomotionHandler>(contents, errors);
            ValidateComponent<LitOpsivePlayerInput>(contents, errors);
            ValidateComponent<LitOpsiveLocomotionBridge>(contents, errors);
            ValidateAnimatorController(contents, label, errors, LucianAnimatorControllerPath);

            LitOpsiveLookSource lookSource = contents.GetComponentInChildren<LitOpsiveLookSource>(true);
            if (lookSource == null)
            {
                errors.Add($"Missing LitOpsiveLookSource in {label} children.");
            }
            else if (lookSource.EventTarget != contents)
            {
                errors.Add($"LitOpsiveLookSource EventTarget does not point to the {label} root.");
            }

            UltimateCharacterLocomotion locomotion = contents.GetComponent<UltimateCharacterLocomotion>();
            if (locomotion != null)
            {
                ValidateLocomotionAnimationMode(locomotion, label, errors, expectedRootMotionPosition: false);
                ValidateAbility<Jump>(locomotion, errors);
                ValidateAbility<Fall>(locomotion, errors);
                ValidateAbility<MoveTowards>(locomotion, errors);
                ValidateAbility<SpeedChange>(locomotion, errors);
                ValidateAbility<HeightChange>(locomotion, errors);
            }

            AnimatorMonitor animatorMonitor = contents.GetComponent<AnimatorMonitor>();
            if (animatorMonitor == null)
            {
                errors.Add($"Missing AnimatorMonitor in {label}.");
            }
            else
            {
                ValidateAnimatorMonitor(animatorMonitor, label, errors);
            }

            LitOpsiveLocomotionBridge bridge = contents.GetComponent<LitOpsiveLocomotionBridge>();
            if (bridge != null)
            {
                ValidateBridgeBoolean(bridge, "driveFromSquadFacade", true, errors);
                ValidateBridgeBoolean(bridge, "overrideOpsiveHandlerInput", true, errors);
                ValidateBridgeBoolean(bridge, "orientLookSourceFromMovement", true, errors);
                ValidateBridgeBoolean(bridge, "configureRigidbodyForOpsive", true, errors);
                ValidateBridgeBoolean(bridge, "driveLitLocomotionAnimatorParameters", true, errors);
            }

            Component[] components = contents.GetComponentsInChildren<Component>(true);
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
                    errors.Add($"Unexpected Opsive camera controller component found in {label}: {type.FullName}.");
                }
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private static void ValidateComponent<T>(GameObject character, List<string> errors) where T : Component
    {
        if (character.GetComponent<T>() == null)
        {
            errors.Add($"Missing required component: {typeof(T).Name}.");
        }
    }

    private static void ValidateAbility<T>(UltimateCharacterLocomotion locomotion, List<string> errors) where T : Ability
    {
        if (locomotion.GetAbility<T>() == null)
        {
            errors.Add($"Missing required UCC ability: {typeof(T).Name}.");
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
        bool expectedRootMotionPosition)
    {
        SerializedObject serializedObject = new SerializedObject(locomotion);
        SerializedProperty rootMotionPosition = serializedObject.FindProperty("m_UseRootMotionPosition");
        if (rootMotionPosition != null && rootMotionPosition.boolValue != expectedRootMotionPosition)
        {
            errors.Add($"{label} m_UseRootMotionPosition should be {expectedRootMotionPosition}.");
        }

        SerializedProperty motorRotationSpeed = serializedObject.FindProperty("m_MotorRotationSpeed");
        if (motorRotationSpeed != null && Mathf.Abs(motorRotationSpeed.floatValue - 0.14f) > 0.001f)
        {
            errors.Add($"{label} m_MotorRotationSpeed should match the UCC sample value 0.14 but is {motorRotationSpeed.floatValue}.");
        }
    }

    private static void ValidateAnimatorMonitor(AnimatorMonitor animatorMonitor, string label, List<string> errors)
    {
        SerializedObject serializedObject = new SerializedObject(animatorMonitor);
        SerializedProperty animatorSpeed = serializedObject.FindProperty("m_AnimatorSpeed");
        if (animatorSpeed != null && Mathf.Abs(animatorSpeed.floatValue - UccAnimatorSpeed) > 0.001f)
        {
            errors.Add($"{label} AnimatorMonitor speed should be {UccAnimatorSpeed} but is {animatorSpeed.floatValue}.");
        }
    }

    private static void ValidateBridgeBoolean(UnityEngine.Object target, string propertyName, bool expected, List<string> errors)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            errors.Add($"Bridge serialized property missing: {propertyName}.");
            return;
        }

        if (property.boolValue != expected)
        {
            errors.Add($"Bridge property '{propertyName}' should be {expected}.");
        }
    }
}
