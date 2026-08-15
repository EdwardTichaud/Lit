#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class CombatActorAnimationContractEditor
{
    private static readonly string[] PrefabPaths =
    {
        "Assets/Characters/1_Squad/Lucian/Player_Model_Lucian.prefab",
        "Assets/Characters/3_Enemy/Juggernaut/Juggernaut_Combat.prefab",
        "Assets/Characters/3_Enemy/GiantJuggernaut/GiantJuggernaut.prefab"
    };

    [MenuItem("Lit/Combat/Validate Actor Animation Contract")]
    private static void ValidateAll()
    {
        string report = BuildValidationReport();
        Debug.Log("[Combat Actor Contract]\n" + report);
        EditorUtility.DisplayDialog("Actor Animation Contract", report, "OK");
    }

    [MenuItem("Lit/Combat/Normalize Actor Animation Hierarchies")]
    private static void NormalizeAll()
    {
        if (EditorUtility.DisplayDialog(
                "Normalize Actor Animation Hierarchies",
                "Migrer Lucian, Juggernaut et GiantJuggernaut vers ActorRoot > AnimationRoot ?\n\n" +
                "La prevalidation doit etre entierement valide. Les skeletons, clips et references sont conserves.",
                "Migrer", "Annuler"))
            NormalizeAllInternal(true);
    }

    // Entry point available to Unity batchmode after scripts compile.
    public static void NormalizeAllBatch()
    {
        NormalizeAllInternal(false);
    }

    private static void NormalizeAllInternal(bool showDialog)
    {
        List<ActorMigrationPlan> plans = new List<ActorMigrationPlan>();
        List<string> issues = new List<string>();
        for (int i = 0; i < PrefabPaths.Length; i++)
        {
            if (TryCreatePlan(PrefabPaths[i], out ActorMigrationPlan plan, out string error)) plans.Add(plan);
            else issues.Add(PrefabPaths[i] + ": " + error);
        }

        if (issues.Count > 0)
        {
            string rejected = "Migration annulee. Aucun prefab n'a ete modifie :\n" + string.Join("\n", issues);
            Debug.LogError("[Combat Actor Contract]\n" + rejected);
            if (showDialog) EditorUtility.DisplayDialog("Actor Animation Contract", rejected, "OK");
            throw new InvalidOperationException(rejected);
        }

        List<string> report = new List<string>();
        for (int i = 0; i < plans.Count; i++) ApplyPlan(plans[i], report);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        string result = string.Join("\n", report) + "\n\n" + BuildValidationReport();
        Debug.Log("[Combat Actor Contract]\n" + result);
        if (showDialog) EditorUtility.DisplayDialog("Actor Animation Contract", result, "OK");
    }

    private static string BuildValidationReport()
    {
        List<string> report = new List<string>();
        for (int i = 0; i < PrefabPaths.Length; i++)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPaths[i]);
            try
            {
                CombatActorAnimationRoot contract = root.GetComponent<CombatActorAnimationRoot>();
                string error = null;
                bool valid = contract != null && contract.ValidateContract(out error);
                report.Add(valid
                    ? PrefabPaths[i] + ": valide | AnimationRoot=" + contract.AnimationRoot.name + " | Animator=" + contract.Animator.name + "."
                    : PrefabPaths[i] + ": invalide | " + (contract == null ? "contrat absent." : error));
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        return string.Join("\n", report);
    }

    private static bool TryCreatePlan(string prefabPath, out ActorMigrationPlan plan, out string error)
    {
        plan = null;
        error = null;
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            Animator animator = FindGameplayAnimator(root);
            if (animator == null) { error = "aucun Animator avec RuntimeAnimatorController."; return false; }

            Animator[] animators = root.GetComponentsInChildren<Animator>(true);
            int controllerCount = 0;
            for (int i = 0; i < animators.Length; i++) if (animators[i].runtimeAnimatorController != null) controllerCount++;
            if (controllerCount != 1) { error = "plusieurs Animators de gameplay detectes (" + controllerCount + ")."; return false; }

            List<Transform> visualRoots = new List<Transform>();
            if (animator.transform == root.transform)
            {
                CollectVisualRoots(root.transform, visualRoots);
                if (visualRoots.Count == 0) { error = "Animator racine sans hierarchy visuelle identifiable; migration refusee."; return false; }
            }

            plan = new ActorMigrationPlan(prefabPath, animator.transform == root.transform, visualRoots);
            return true;
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void ApplyPlan(ActorMigrationPlan plan, List<string> report)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(plan.prefabPath);
        try
        {
            Animator oldAnimator = FindGameplayAnimator(root);
            Transform animationRoot;
            Animator animator;
            if (plan.moveRootAnimator)
            {
                animationRoot = CreateIdentityAnimationRoot(root.transform);
                for (int i = 0; i < plan.visualRootNames.Count; i++)
                {
                    Transform visualRoot = root.transform.Find(plan.visualRootNames[i]);
                    if (visualRoot != null) visualRoot.SetParent(animationRoot, true);
                }
                animator = animationRoot.gameObject.AddComponent<Animator>();
                EditorUtility.CopySerialized(oldAnimator, animator);
                ReplaceAnimatorReferences(root, oldAnimator, animator);
                UnityEngine.Object.DestroyImmediate(oldAnimator);
            }
            else
            {
                animationRoot = EnsureIdentityAnimationRoot(root.transform, oldAnimator.transform);
                animator = oldAnimator;
            }

            Animator[] rootAnimators = root.GetComponents<Animator>();
            for (int i = 0; i < rootAnimators.Length; i++)
                if (rootAnimators[i] != animator && rootAnimators[i].runtimeAnimatorController == null)
                    UnityEngine.Object.DestroyImmediate(rootAnimators[i]);

            CombatActorAnimationRoot contract = root.GetComponent<CombatActorAnimationRoot>();
            if (contract == null) contract = root.AddComponent<CombatActorAnimationRoot>();
            contract.Configure(animationRoot, animator, root.transform.Find("EnemyLockPoint"));

            CombatActorRootMotionRelay relay = animator.GetComponent<CombatActorRootMotionRelay>();
            if (relay == null) relay = animator.gameObject.AddComponent<CombatActorRootMotionRelay>();
            SetObjectReference(relay, "actor", contract);
            AssignAnimatorReferences(root, contract, animator);
            if (!contract.ValidateContract(out string validationError)) throw new InvalidOperationException(validationError);

            PrefabUtility.SaveAsPrefabAsset(root, plan.prefabPath);
            report.Add(plan.prefabPath + ": migre | AnimationRoot=" + animationRoot.name + " | Animator=" + animator.name + ".");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static Transform CreateIdentityAnimationRoot(Transform actorRoot)
    {
        GameObject rootObject = new GameObject("AnimationRoot");
        Transform animationRoot = rootObject.transform;
        animationRoot.SetParent(actorRoot, false);
        animationRoot.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        animationRoot.localScale = Vector3.one;
        return animationRoot;
    }

    private static Transform EnsureIdentityAnimationRoot(Transform actorRoot, Transform animatorTransform)
    {
        if (animatorTransform.parent != null && animatorTransform.parent.name == "AnimationRoot" &&
            animatorTransform.parent.parent == actorRoot && IsIdentity(animatorTransform.parent)) return animatorTransform.parent;
        Transform animationRoot = CreateIdentityAnimationRoot(actorRoot);
        animatorTransform.SetParent(animationRoot, true);
        return animationRoot;
    }

    private static void CollectVisualRoots(Transform actorRoot, List<Transform> roots)
    {
        HashSet<Transform> unique = new HashSet<Transform>();
        SkinnedMeshRenderer[] renderers = actorRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Transform current = renderers[i].transform;
            while (current.parent != null && current.parent != actorRoot) current = current.parent;
            if (current.parent == actorRoot) unique.Add(current);
        }
        roots.AddRange(unique);
    }

    private static Animator FindGameplayAnimator(GameObject root)
    {
        EnemySkills skills = root.GetComponent<EnemySkills>();
        if (skills != null && skills.Animator != null && skills.Animator.runtimeAnimatorController != null) return skills.Animator;
        Animator[] animators = root.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++) if (animators[i].runtimeAnimatorController != null) return animators[i];
        return null;
    }

    private static void ReplaceAnimatorReferences(GameObject root, Animator oldAnimator, Animator newAnimator)
    {
        MonoBehaviour[] components = root.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] == null) continue;
            SerializedObject serialized = new SerializedObject(components[i]);
            SerializedProperty property = serialized.GetIterator();
            bool changed = false;
            while (property.NextVisible(true))
            {
                if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == oldAnimator)
                {
                    property.objectReferenceValue = newAnimator;
                    changed = true;
                }
            }
            if (changed) serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void AssignAnimatorReferences(GameObject root, CombatActorAnimationRoot contract, Animator animator)
    {
        SetObjectReference(root.GetComponent<RealTimeCombatEnemy>(), "animationContract", contract);
        SetObjectReference(root.GetComponent<RealTimeCombatEnemy>(), "animator", animator);
        SetObjectReference(root.GetComponent<EnemySkills>(), "animationContract", contract);
        SetObjectReference(root.GetComponent<EnemySkills>(), "animator", animator);
        SetObjectReference(root.GetComponent<LitOpsiveLocomotionBridge>(), "animator", animator);
        SetObjectReference(root.GetComponent<PlayerActionPresentationController>(), "animator", animator);
        SetObjectReference(root.GetComponent<AnimationGroundRecovery>(), "animator", animator);
    }

    private static void SetObjectReference(UnityEngine.Object component, string propertyName, UnityEngine.Object value)
    {
        if (component == null) return;
        SerializedObject serialized = new SerializedObject(component);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null || property.propertyType != SerializedPropertyType.ObjectReference) return;
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(component);
    }

    private static bool IsIdentity(Transform transform)
    {
        return transform.localPosition.sqrMagnitude <= 0.000001f &&
               Quaternion.Angle(transform.localRotation, Quaternion.identity) <= 0.01f &&
               (transform.localScale - Vector3.one).sqrMagnitude <= 0.000001f;
    }

    private sealed class ActorMigrationPlan
    {
        public readonly string prefabPath;
        public readonly bool moveRootAnimator;
        public readonly List<string> visualRootNames = new List<string>();
        public ActorMigrationPlan(string path, bool moveAnimator, List<Transform> visualRoots)
        {
            prefabPath = path;
            moveRootAnimator = moveAnimator;
            for (int i = 0; i < visualRoots.Count; i++) visualRootNames.Add(visualRoots[i].name);
        }
    }
}
#endif
