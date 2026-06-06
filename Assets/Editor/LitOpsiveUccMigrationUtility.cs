using System;
using System.Collections.Generic;
using Opsive.UltimateCharacterController.Character;
using Opsive.UltimateCharacterController.Character.Abilities;
using Opsive.UltimateCharacterController.Character.Identifiers;
using Opsive.UltimateCharacterController.Traits;
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
    private const string UccHealthAttributeName = "Health";
    private const string LucianCharacterDataPath = "Assets/ScriptableObjects/CharacterData/Lucian.asset";
    private const string LucianPrefabPath = "Assets/Prefabs/Character/Player_Model_Lucian.prefab";
    private static readonly string[] KnownPlayerCharacterPrefabPaths =
    {
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
        if (warnings.Count > 0)
        {
            Debug.LogWarning("Lucian UCC setup validation warnings:\n- " + string.Join("\n- ", warnings));
        }

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
            useRootMotionPosition: !useLucianAnimatorController);
        EnsureSingleStandardAbilities(character.GetComponent<UltimateCharacterLocomotion>());
        ConfigureAnimatorMonitor(character.GetComponent<AnimatorMonitor>());
        EnsureLookSource(character);
        ConfigureCharacterIk(character);
        ConfigureCharacterHealthMirror(character);

        LitOpsiveLocomotionBridge bridge = character.GetComponent<LitOpsiveLocomotionBridge>();
        if (bridge == null)
        {
            bridge = character.AddComponent<LitOpsiveLocomotionBridge>();
        }

        ConfigureBridgeAnimatorMode(bridge, driveLitAnimatorParameters: useLucianAnimatorController);
        ConfigureBridgeCompanionMode(bridge, autoInstallCompanionBridges: false);
        EnsureExplicitCompanionBridges(character);
        ConfigureDamageBridgeHealthMirror(character);
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
        SetSerializedFloat(serializedObject, "m_FootOffsetAdjustment", 0.005f);
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(characterIk);
    }

    private static void ConfigureCharacterHealthMirror(GameObject character)
    {
        CharacterAttributeManager attributeManager = EnsureComponent<CharacterAttributeManager>(character);
        int maxHealth = ResolveCharacterMaxHealth(character);
        ConfigureHealthAttribute(attributeManager, maxHealth);

        CharacterHealth characterHealth = EnsureComponent<CharacterHealth>(character);
        characterHealth.HealthAttributeName = UccHealthAttributeName;
        characterHealth.ShieldAttributeName = string.Empty;
        characterHealth.ApplyFallDamage = false;
        characterHealth.Invincible = true;
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

    private static void ConfigureDamageBridgeHealthMirror(GameObject character)
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
        SetSerializedBool(serializedObject, "mirrorLitHealthToOpsiveAttributes", true);
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

        string modelPath = lucianData.model != null ? AssetDatabase.GetAssetPath(lucianData.model) : null;
        if (string.IsNullOrEmpty(modelPath))
        {
            errors.Add("Lucian CharacterData model is null.");
        }
        else if (!string.Equals(modelPath, LucianPrefabPath, StringComparison.Ordinal))
        {
            errors.Add($"Lucian CharacterData model should be '{LucianPrefabPath}' but is '{modelPath}'.");
        }

        ValidateUccPrefab(LucianPrefabPath, "Player_Model_Lucian prefab", errors, warnings);

        return errors;
    }

    private static void ValidateUccPrefab(string prefabPath, string label, List<string> errors, List<string> warnings)
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
            ValidateComponent<LitUccInteractionBridge>(contents, errors);
            ValidateComponent<LitUccDamageBridge>(contents, errors);
            ValidateComponent<LitUccFollowerBridge>(contents, errors);
            ValidateComponent<CharacterAttributeManager>(contents, errors);
            ValidateComponent<CharacterHealth>(contents, errors);
            ValidateNoComponent<StarterInspiredThirdPersonMotor>(contents, label, errors);
            ValidateNoComponent<StarterMotorAnimatorDriver>(contents, label, errors);
            ValidateNoComponent<StarterMotorLocalInputBridge>(contents, label, errors);
            ValidateAnimatorController(contents, label, errors, LucianAnimatorControllerPath);
            ValidateCharacterIkMigration(contents, label, errors);
            ValidateCharacterHealthMigration(contents, label, errors);
            ValidateSingleUccColliderGroup(contents, label, errors);

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
                ValidateSingleAbility<Jump>(locomotion, errors);
                ValidateSingleAbility<Fall>(locomotion, errors);
                ValidateSingleAbility<MoveTowards>(locomotion, errors);
                ValidateSingleAbility<SpeedChange>(locomotion, errors);
                ValidateSingleAbility<HeightChange>(locomotion, errors);
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
                ValidateBridgeBoolean(bridge, "autoInstallCompanionBridges", false, errors);
                ValidateBridgeBoolean(bridge, "driveLitLocomotionAnimatorParameters", true, errors);
            }

            LitUccDamageBridge damageBridge = contents.GetComponent<LitUccDamageBridge>();
            if (damageBridge != null)
            {
                ValidateBridgeBoolean(damageBridge, "mirrorLitHealthToOpsiveAttributes", true, errors);
                ValidateBridgeObjectReference(damageBridge, "squadController", contents.GetComponent<SquadCharacterController>(), errors);
                ValidateBridgeObjectReference(damageBridge, "attributeManager", contents.GetComponent<CharacterAttributeManager>(), errors);
                ValidateBridgeObjectReference(damageBridge, "characterHealth", contents.GetComponent<CharacterHealth>(), errors);
                ValidateBridgeString(damageBridge, "healthAttributeName", UccHealthAttributeName, errors);
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

    private static void ValidateNoComponent<T>(GameObject character, string label, List<string> errors) where T : Component
    {
        if (character.GetComponent<T>() != null)
        {
            errors.Add($"{label} should not carry legacy Starter motor component: {typeof(T).Name}.");
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
            ValidateSerializedFloat(characterIk, "m_FootOffsetAdjustment", 0.005f, errors);
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
            errors.Add($"{label} CharacterHealth shield attribute should be empty during Lit-owned health mirroring.");
        }

        if (characterHealth.ApplyFallDamage)
        {
            errors.Add($"{label} CharacterHealth fall damage should stay disabled while Lit owns health.");
        }

        if (!characterHealth.Invincible)
        {
            errors.Add($"{label} CharacterHealth should be invincible while it is mirror-only.");
        }

        if (characterHealth.DeactivateOnDeath)
        {
            errors.Add($"{label} CharacterHealth should not deactivate the character while Lit owns death flow.");
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

    private static void ValidateSingleAbility<T>(UltimateCharacterLocomotion locomotion, List<string> errors) where T : Ability
    {
        T[] abilities = locomotion.GetAbilities<T>();
        if (abilities == null || abilities.Length == 0)
        {
            errors.Add($"Missing required UCC ability: {typeof(T).Name}.");
        }
        else if (abilities.Length > 1)
        {
            errors.Add($"Duplicate required UCC ability: {typeof(T).Name} appears {abilities.Length} times.");
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

    private static void ValidateBridgeString(UnityEngine.Object target, string propertyName, string expected, List<string> errors)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            errors.Add($"Bridge serialized property missing: {propertyName}.");
            return;
        }

        if (!string.Equals(property.stringValue, expected, StringComparison.Ordinal))
        {
            errors.Add($"Bridge property '{propertyName}' should be '{expected}' but is '{property.stringValue}'.");
        }
    }

    private static void ValidateBridgeObjectReference(
        UnityEngine.Object target,
        string propertyName,
        UnityEngine.Object expected,
        List<string> errors)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            errors.Add($"Bridge serialized property missing: {propertyName}.");
            return;
        }

        if (property.objectReferenceValue != expected)
        {
            string expectedName = expected != null ? expected.GetType().Name : "<null>";
            string currentName = property.objectReferenceValue != null
                ? property.objectReferenceValue.GetType().Name
                : "<null>";
            errors.Add($"Bridge property '{propertyName}' should reference {expectedName} but references {currentName}.");
        }
    }
}
