using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>One-shot authoring setup for the Juggernaut combat reaction contract.</summary>
[InitializeOnLoad]
public static class JuggernautCombatSetupUtility
{
    private const string ControllerPath = "Assets/Characters/3_Enemy/Juggernaut/Juggernaut.controller";
    private const string PrefabPath = "Assets/Characters/3_Enemy/Juggernaut/Juggernaut_Combat.prefab";
    private const string CharacterDataPath = "Assets/Characters/3_Enemy/Juggernaut/Juggernaut.asset";
    private const string GuardClipPath = "Assets/0 - UnityPackages/Fab/Raise Creation/Super_Fast_Fighting Pack/Animations/Style_One/Anim_SF_Block.fbx";
    private const string DodgeClipPath = "Assets/0 - UnityPackages/Fab/Raise Creation/Super_Fast_Fighting Pack/Animations/Style_One/Anim_SF_Dodge.fbx";

    static JuggernautCombatSetupUtility()
    {
        EditorApplication.delayCall += InstallMissingContractAfterReload;
    }

    private static void InstallMissingContractAfterReload()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (controller == null || prefab == null)
        {
            return;
        }

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        Animator animator = prefab.GetComponent<Animator>();
        bool missing = FindState(machine, "CombatIdle") == null ||
                       FindState(machine, "Guard") == null ||
                       FindState(machine, "Dodge") == null ||
                       animator == null || animator.cullingMode != AnimatorCullingMode.AlwaysAnimate ||
                       prefab.GetComponent<EnemyTacticalResponseController>() == null ||
                       prefab.GetComponent<EnemyAttackRecoverySafety>() == null ||
                       prefab.GetComponent<CombatEnemyRuntimeContract>() == null;
        if (missing)
        {
            Configure();
        }
    }

    [MenuItem("Lit/Combat/Configure Juggernaut Combat AI")]
    public static void Configure()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError("[Juggernaut Combat] Controller introuvable : " + ControllerPath);
            return;
        }

        try
        {
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState idle = FindState(machine, "Idle");
            EnsureState(machine, "CombatIdle", idle != null ? idle.motion : null);
            EnsureState(machine, "Guard", LoadClip(GuardClipPath));
            EnsureState(machine, "Dodge", LoadClip(DodgeClipPath));
            EditorUtility.SetDirty(controller);

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Animator animator = prefabRoot.GetComponent<Animator>();
                if (animator == null)
                {
                    throw new InvalidOperationException("Animator absent du root Juggernaut_Combat.");
                }

                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                if (prefabRoot.GetComponent<EnemyTacticalResponseController>() == null)
                {
                    prefabRoot.AddComponent<EnemyTacticalResponseController>();
                }
                if (prefabRoot.GetComponent<EnemyAttackRecoverySafety>() == null)
                {
                    prefabRoot.AddComponent<EnemyAttackRecoverySafety>();
                }
                if (prefabRoot.GetComponent<CombatEnemyRuntimeContract>() == null)
                {
                    prefabRoot.AddComponent<CombatEnemyRuntimeContract>();
                }
                EnsurePhysicsEnvelope(prefabRoot);

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            AssignWorldPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceUpdate);
            NetcodePrefabRegistry.InvalidateSceneMarkerCharacterCache();
            Debug.Log("[Juggernaut Combat] Etats, enveloppe physique et contrat runtime configures.");
        }
        catch (Exception exception)
        {
            Debug.LogError("[Juggernaut Combat] Configuration annulee : " + exception.Message);
        }
    }

    [MenuItem("Lit/Combat/Validate Juggernaut Combat AI")]
    public static void Validate()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        bool valid = controller != null && prefab != null;
        if (controller != null)
        {
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            valid &= FindState(machine, "Guard") != null;
            valid &= FindState(machine, "Dodge") != null;
            valid &= FindState(machine, "CombatIdle") != null;
        }

        Animator animator = prefab != null ? prefab.GetComponent<Animator>() : null;
        valid &= animator != null && animator.cullingMode == AnimatorCullingMode.AlwaysAnimate;
        valid &= prefab != null && prefab.GetComponent<EnemyTacticalResponseController>() != null;
        valid &= prefab != null && prefab.GetComponent<EnemyAttackRecoverySafety>() != null;
        valid &= prefab != null && prefab.GetComponent<CombatEnemyRuntimeContract>() != null;
        valid &= prefab != null && prefab.GetComponent<Rigidbody>() != null;
        valid &= prefab != null && prefab.GetComponent<CapsuleCollider>() != null;
        valid &= prefab != null && prefab.GetComponent<UnityEngine.AI.NavMeshAgent>() != null;
        CharacterData data = AssetDatabase.LoadAssetAtPath<CharacterData>(CharacterDataPath);
        valid &= data != null && data.worldPrefab == prefab;
        string prefabPath = prefab != null ? AssetDatabase.GetAssetPath(prefab) : "absent";
        string prefabGuid = prefab != null ? AssetDatabase.AssetPathToGUID(prefabPath) : "absent";
        Debug.Log("[Juggernaut Combat] Validation=" + valid +
                  " | worldPrefab=" + (data != null && data.worldPrefab != null ? data.worldPrefab.name : "absent") +
                  " | prefabPath=" + prefabPath + " | guid=" + prefabGuid +
                  " | contract={" + CombatEnemyRuntimeContract.DescribeRequiredComponents(prefab) + "}.");
    }

    [MenuItem("Lit/Combat/Repair Juggernaut Runtime Contract")]
    private static void RepairRuntimeContract()
    {
        Configure();
        Validate();
    }

    private static void EnsurePhysicsEnvelope(GameObject root)
    {
        Rigidbody body = root.GetComponent<Rigidbody>() ?? root.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        CapsuleCollider capsule = root.GetComponent<CapsuleCollider>() ?? root.AddComponent<CapsuleCollider>();
        capsule.isTrigger = false;
        if (root.GetComponent<UnityEngine.AI.NavMeshAgent>() == null)
        {
            root.AddComponent<UnityEngine.AI.NavMeshAgent>();
        }
    }

    private static void AssignWorldPrefab()
    {
        CharacterData data = AssetDatabase.LoadAssetAtPath<CharacterData>(CharacterDataPath);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (data == null || prefab == null)
        {
            throw new InvalidOperationException("CharacterData ou prefab Juggernaut introuvable.");
        }

        SerializedObject serializedData = new SerializedObject(data);
        SerializedProperty worldPrefab = serializedData.FindProperty("worldPrefab");
        if (worldPrefab == null)
        {
            throw new InvalidOperationException("Champ worldPrefab absent de Juggernaut.asset.");
        }

        worldPrefab.objectReferenceValue = prefab;
        serializedData.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(data);
    }

    private static void EnsureState(AnimatorStateMachine machine, string stateName, Motion motion)
    {
        if (motion == null)
        {
            throw new InvalidOperationException("Clip requis introuvable pour l'etat " + stateName + ".");
        }

        AnimatorState state = FindState(machine, stateName) ?? machine.AddState(stateName, new Vector3(900f, machine.states.Length * 95f));
        state.motion = motion;
        state.writeDefaultValues = true;
    }

    private static AnimatorState FindState(AnimatorStateMachine machine, string stateName)
    {
        foreach (ChildAnimatorState child in machine.states)
        {
            if (child.state != null && child.state.name == stateName)
            {
                return child.state;
            }
        }
        return null;
    }

    private static AnimationClip LoadClip(string path)
    {
        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
            {
                return clip;
            }
        }
        return null;
    }
}
