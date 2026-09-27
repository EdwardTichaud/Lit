#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;

/// <summary>
/// Keeps authored enemy models independent while enforcing the single combat
/// runtime contract used by EnemyController. The Juggernaut controller is a
/// read-only source template and is never edited here.
/// </summary>
public sealed class EnemyAnimationContractMigration : IPreprocessBuildWithReport
{
    private const string RequestPath = "Library/EnemyAnimationContract.request";
    private const string JuggernautPrefabPath = "Assets/Characters/3_Enemy/Juggernaut/Juggernaut_Combat.prefab";
    private const string JuggernautControllerPath = "Assets/Characters/3_Enemy/Juggernaut/Juggernaut_Model.controller";
    private const string JuggernautDataPath = "Assets/Characters/3_Enemy/Juggernaut/Juggernaut.asset";
    private const string ShadowPrefabPath = "Assets/Scenes/Cycles/Cycle_Belmont/Prefabs/ShadowGuardian.prefab";
    private const string ShadowControllerPath = "Assets/Scenes/Cycles/Cycle_Belmont/Prefabs/ShadowGuardian.controller";
    private const string ShadowDataPath = "Assets/Scenes/Cycles/Cycle_Belmont/Data/CharacterData_VeilleurDOmbre.asset";
    private const string GiantPrefabPath = "Assets/Characters/3_Enemy/GiantJuggernaut/GiantJuggernaut.prefab";
    private const string GiantControllerPath = "Assets/Characters/3_Enemy/GiantJuggernaut/GiantJuggernaut_Contract.controller";
    private const string GiantDataPath = "Assets/Characters/3_Enemy/GiantJuggernaut/GiantJuggernaut_CharacterData.asset";
    private const string GiantSkillPath = "Assets/Characters/3_Enemy/Juggernaut/Skill_GiantJuggernaut_Jump.asset";
    private const string ScientistPrefabPath = "Assets/Characters/9_Ghosts/Luc/Enemy_Model_MadScientist.prefab";
    private const string ScientistDataPath = "Assets/Narrative/NinaCycle/Data/Enemy_ScientifiqueFou.asset";

    private static readonly string[] PrefabPaths =
    {
        JuggernautPrefabPath,
        ShadowPrefabPath,
        GiantPrefabPath,
        ScientistPrefabPath
    };

    public int callbackOrder => 100;

    [InitializeOnLoadMethod]
    private static void InstallRequestHandler()
    {
        EditorApplication.update -= ConsumeRequest;
        EditorApplication.update += ConsumeRequest;
    }

    private static void ConsumeRequest()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(RequestPath))
            return;

        File.Delete(RequestPath);
        try
        {
            Migrate();
            File.WriteAllText("Library/EnemyAnimationContract.result", "PASS");
        }
        catch (Exception exception)
        {
            File.WriteAllText("Library/EnemyAnimationContract.result", exception.ToString());
            Debug.LogException(exception);
        }
    }

    [MenuItem("Lit/Combat/Migrate Enemy Animation Contracts")]
    public static void Migrate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Quitter le mode Play avant la migration des ennemis.");

        AnimatorController baseline = Require<AnimatorController>(JuggernautControllerPath);
        CharacterData juggernautData = Require<CharacterData>(JuggernautDataPath);
        AnimatorController shadowController = CopyControllerIfMissing(JuggernautControllerPath, ShadowControllerPath, "ShadowGuardian");
        AnimatorController giantController = CopyControllerIfMissing(JuggernautControllerPath, GiantControllerPath, "GiantJuggernaut_Contract");
        SkillSO giantJump = Require<SkillSO>(GiantSkillPath);
        EnsureRootState(giantController, giantJump.AnimatorState, giantJump.AnimationClip);

        CharacterData shadowData = Require<CharacterData>(ShadowDataPath);
        shadowData.characterId = "shadow_warden_belmont";
        shadowData.characterName = "Veilleur d’Ombre";
        shadowData.isEnemy = true;
        shadowData.enemyCombatProfile = juggernautData.enemyCombatProfile;
        shadowData.combatSkills = new List<SkillSO>(juggernautData.combatSkills);
        shadowData.enemySettings = juggernautData.enemySettings;
        shadowData.vision = juggernautData.vision;
        SynchronizeIdentity(shadowData);
        EditorUtility.SetDirty(shadowData);

        CharacterData giantData = Require<CharacterData>(GiantDataPath);
        giantData.characterId = "giant_juggernaut";
        giantData.characterName = "Giant Juggernaut";
        giantData.combatSkills = new List<SkillSO> { giantJump };
        giantData.enemySettings.ActorIdleAnimatorState = EnemyAnimatorContract.CombatIdle;
        giantData.enemySettings.ActorHitAnimatorState = EnemyAnimatorContract.Hit;
        giantData.enemySettings.ActorDeathAnimatorState = EnemyAnimatorContract.Death;
        SynchronizeIdentity(giantData);
        EditorUtility.SetDirty(giantData);

        CharacterData scientistData = Require<CharacterData>(ScientistDataPath);
        scientistData.characterId = "mad_scientist_nina";
        scientistData.enemySettings.ActorIdleAnimatorState = EnemyAnimatorContract.CombatIdle;
        scientistData.enemySettings.ActorHitAnimatorState = EnemyAnimatorContract.Hit;
        scientistData.enemySettings.ActorDeathAnimatorState = EnemyAnimatorContract.Death;
        SynchronizeIdentity(scientistData);
        EditorUtility.SetDirty(scientistData);

        ConfigurePrefab(ShadowPrefabPath, shadowController, shadowData);
        ConfigurePrefab(GiantPrefabPath, giantController, giantData);
        ConfigurePrefab(ScientistPrefabPath, null, scientistData);

        EditorUtility.SetDirty(baseline);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        ValidateOrThrow();
        Debug.Log("[EnemyAnimationContract] Migration terminée : ShadowGuardian, GiantJuggernaut et Scientifique fou sont conformes au contrat Juggernaut.");
    }

    [MenuItem("Lit/Combat/Validate Enemy Animation Contracts")]
    public static void ValidateMenu()
    {
        ValidateOrThrow();
        Debug.Log("[EnemyAnimationContract] Les prefabs ennemis sont valides.");
    }

    public void OnPreprocessBuild(BuildReport report) => ValidateOrThrow();

    public static void ValidateOrThrow()
    {
        List<string> failures = new List<string>();
        foreach (string path in PrefabPaths)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try { ValidatePrefab(path, root, failures); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        if (failures.Count > 0)
            throw new BuildFailedException("[EnemyAnimationContract]\n" + string.Join("\n", failures));
    }

    private static void ConfigurePrefab(string path, RuntimeAnimatorController controller, CharacterData data)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            EnemyController enemy = root.GetComponent<EnemyController>() ?? root.AddComponent<EnemyController>();
            CharacterInfo info = root.GetComponent<CharacterInfo>() ?? root.AddComponent<CharacterInfo>();
            Rigidbody body = root.GetComponent<Rigidbody>() ?? root.AddComponent<Rigidbody>();
            CapsuleCollider capsule = root.GetComponent<CapsuleCollider>() ?? root.AddComponent<CapsuleCollider>();
            NavMeshAgent agent = root.GetComponent<NavMeshAgent>() ?? root.AddComponent<NavMeshAgent>();
            if (root.GetComponent<NetworkObject>() == null) root.AddComponent<NetworkObject>();

            Animator animator = ResolveGameplayAnimator(root, enemy);
            if (animator == null)
                throw new InvalidOperationException(path + " ne possède pas d’Animator utilisable.");
            if (controller != null) animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            Transform lockPoint = root.transform.Find("EnemyLockPoint");
            if (lockPoint == null)
            {
                lockPoint = new GameObject("EnemyLockPoint").transform;
                lockPoint.SetParent(root.transform, false);
                lockPoint.localPosition = Vector3.up * 1.2f;
            }

            info.SetCharacterData(data);
            data.worldPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            body.isKinematic = true;
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            capsule.enabled = true;
            capsule.isTrigger = false;
            capsule.radius = Mathf.Max(.3f, capsule.radius);
            capsule.height = Mathf.Max(capsule.radius * 2f, capsule.height);
            agent.enabled = false; // Enabled by EnemyController once a NavMesh world is ready.

            enemy.Configure(animator.transform, animator, lockPoint);
            SetReference(enemy, "ActorAnimator", animator);
            SetReference(enemy, "SkillsAnimator", animator);
            SetReference(enemy, "ContractAnimator", animator);
            SetReference(enemy, "ActorEnemyLockPoint", lockPoint);
            SetReference(enemy, "SkillsCasterVfxPoint", root.transform);

            EditorUtility.SetDirty(data);
            EditorUtility.SetDirty(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static AnimatorController CopyControllerIfMissing(string sourcePath, string targetPath, string name)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(targetPath);
        if (controller == null)
        {
            if (!AssetDatabase.CopyAsset(sourcePath, targetPath))
                throw new InvalidOperationException("Impossible de créer le controller '" + targetPath + "'.");
            controller = Require<AnimatorController>(targetPath);
        }
        controller.name = name;
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void EnsureRootState(AnimatorController controller, string stateName, Motion motion)
    {
        if (controller == null || string.IsNullOrWhiteSpace(stateName) || motion == null)
            throw new InvalidOperationException("Etat ou animation de compétence Géant invalide.");
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState state = machine.states.Select(child => child.state).FirstOrDefault(candidate => candidate != null && candidate.name == stateName);
        if (state == null) state = machine.AddState(stateName);
        state.motion = motion;
        state.writeDefaultValues = true;
        EditorUtility.SetDirty(controller);
    }

    private static Animator ResolveGameplayAnimator(GameObject root, EnemyController enemy)
    {
        if (enemy != null && enemy.Animator != null && enemy.Animator.runtimeAnimatorController != null)
            return enemy.Animator;
        Animator rootAnimator = root.GetComponent<Animator>();
        if (rootAnimator != null && rootAnimator.runtimeAnimatorController != null) return rootAnimator;
        return root.GetComponentsInChildren<Animator>(true).FirstOrDefault(candidate => candidate.runtimeAnimatorController != null);
    }

    private static void ValidatePrefab(string path, GameObject root, ICollection<string> failures)
    {
        EnemyController enemy = root.GetComponent<EnemyController>();
        CharacterInfo info = root.GetComponent<CharacterInfo>();
        Animator animator = ResolveGameplayAnimator(root, enemy);
        if (enemy == null) failures.Add(path + " : EnemyController absent.");
        if (info == null || info.SourceData == null) failures.Add(path + " : CharacterData absent.");
        if (root.GetComponent<NetworkObject>() == null) failures.Add(path + " : NetworkObject absent sur la racine.");
        Rigidbody body = root.GetComponent<Rigidbody>();
        if (body == null || !body.isKinematic) failures.Add(path + " : Rigidbody cinématique requis.");
        CapsuleCollider capsule = root.GetComponent<CapsuleCollider>();
        if (capsule == null || !capsule.enabled || capsule.isTrigger) failures.Add(path + " : CapsuleCollider actif non-trigger requis.");
        if (root.GetComponent<NavMeshAgent>() == null) failures.Add(path + " : NavMeshAgent absent.");
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            failures.Add(path + " : Animator ou AnimatorController absent.");
            return;
        }

        if (info != null && info.SourceData != null && info.SourceData.worldPrefab != AssetDatabase.LoadAssetAtPath<GameObject>(path))
            failures.Add(path + " : CharacterData.worldPrefab ne référence pas ce prefab.");

        foreach (string parameter in EnemyAnimatorContract.RequiredParameters)
            if (!animator.parameters.Any(candidate => candidate.name == parameter))
                failures.Add(path + " : paramètre Animator requis manquant '" + parameter + "'.");

        foreach (string state in RequiredStates(info != null ? info.SourceData : null))
            if (!HasState(animator, state))
                failures.Add(path + " : état Animator requis manquant '" + state + "'.");
    }

    private static IEnumerable<string> RequiredStates(CharacterData data)
    {
        yield return EnemyAnimatorContract.CombatIdle;
        yield return EnemyAnimatorContract.Hit;
        yield return EnemyAnimatorContract.Death;
        if (data == null || data.combatSkills == null) yield break;
        foreach (SkillSO skill in data.combatSkills)
        {
            if (skill == null) continue;
            string state = string.IsNullOrWhiteSpace(skill.AnimatorState) ? skill.AnimationClip != null ? skill.AnimationClip.name : null : skill.AnimatorState;
            if (!string.IsNullOrWhiteSpace(state)) yield return state;
        }
    }

    private static bool HasState(Animator animator, string stateName)
    {
        return animator.HasState(0, Animator.StringToHash(stateName)) ||
               animator.HasState(0, Animator.StringToHash("Base Layer." + stateName));
    }

    private static void SynchronizeIdentity(CharacterData data)
    {
        SerializedObject serialized = new SerializedObject(data);
        SerializedProperty uniqueId = serialized.FindProperty("uniqueId");
        if (uniqueId != null) uniqueId.stringValue = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(data));
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetReference(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null || property.propertyType != SerializedPropertyType.ObjectReference) return;
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static T Require<T>(string path) where T : UnityEngine.Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) throw new InvalidOperationException("Asset requis introuvable : " + path);
        return asset;
    }
}
#endif
