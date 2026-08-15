#if UNITY_EDITOR
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
        List<string> report = new List<string>();
        for (int i = 0; i < PrefabPaths.Length; i++)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPaths[i]);
            try
            {
                CombatActorAnimationRoot contract = root.GetComponent<CombatActorAnimationRoot>();
                if (contract == null)
                {
                    report.Add(PrefabPaths[i] + ": contrat absent.");
                    continue;
                }

                report.Add(contract.ValidateContract(out string error)
                    ? PrefabPaths[i] + ": valide | Animator=" + contract.Animator.name + "."
                    : PrefabPaths[i] + ": invalide | " + error);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        string message = string.Join("\n", report);
        Debug.Log("[Combat Actor Contract]\n" + message);
        EditorUtility.DisplayDialog("Actor Animation Contract", message, "OK");
    }

    [MenuItem("Lit/Combat/Normalize Actor Animation Hierarchies")]
    private static void NormalizeAll()
    {
        if (!EditorUtility.DisplayDialog(
                "Normalize Actor Animation Hierarchies",
                "Normaliser Lucian, Juggernaut et GiantJuggernaut ?\n\n" +
                "Les skeletons importes restent intacts. Les ennemis recoivent un AnimationRoot; " +
                "Lucian conserve temporairement son Animator racine afin de ne pas casser ses clips generiques.",
                "Normaliser", "Annuler"))
        {
            return;
        }

        List<string> report = new List<string>();
        for (int i = 0; i < PrefabPaths.Length; i++)
        {
            NormalizePrefab(PrefabPaths[i], report);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        string message = string.Join("\n", report);
        Debug.Log("[Combat Actor Contract]\n" + message);
        EditorUtility.DisplayDialog("Actor Animation Contract", message, "OK");
    }

    private static void NormalizePrefab(string prefabPath, List<string> report)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            Animator animator = FindGameplayAnimator(root);
            if (animator == null)
            {
                report.Add(prefabPath + ": ignore, aucun Animator avec Controller.");
                return;
            }

            Transform animationRoot = animator.transform;
            if (animator.transform != root.transform)
            {
                animationRoot = EnsureAnimationRoot(root.transform, animator.transform);
            }

            Animator[] rootAnimators = root.GetComponents<Animator>();
            for (int i = 0; i < rootAnimators.Length; i++)
            {
                if (rootAnimators[i] != animator && rootAnimators[i].runtimeAnimatorController == null)
                {
                    Object.DestroyImmediate(rootAnimators[i]);
                }
            }

            CombatActorAnimationRoot contract = root.GetComponent<CombatActorAnimationRoot>();
            if (contract == null)
            {
                contract = root.AddComponent<CombatActorAnimationRoot>();
            }

            Transform lockPoint = root.transform.Find("EnemyLockPoint");
            contract.Configure(animationRoot, animator, lockPoint);

            if (animator.GetComponent<CombatActorRootMotionRelay>() == null)
            {
                animator.gameObject.AddComponent<CombatActorRootMotionRelay>();
            }

            AssignAnimatorReferences(root, contract, animator);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            report.Add(prefabPath + ": normalise | AnimationRoot=" + animationRoot.name + " | Animator=" + animator.name + ".");
        }
        catch (System.Exception exception)
        {
            report.Add(prefabPath + ": echec | " + exception.Message);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Transform EnsureAnimationRoot(Transform actorRoot, Transform animatorTransform)
    {
        Transform currentParent = animatorTransform.parent;
        if (currentParent != null && currentParent.name == "AnimationRoot" && currentParent.parent == actorRoot &&
            IsIdentity(currentParent))
        {
            return currentParent;
        }

        GameObject rootObject = new GameObject("AnimationRoot");
        Transform animationRoot = rootObject.transform;
        animationRoot.SetParent(actorRoot, false);
        animationRoot.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        animationRoot.localScale = Vector3.one;
        animatorTransform.SetParent(animationRoot, true);
        return animationRoot;
    }

    private static Animator FindGameplayAnimator(GameObject root)
    {
        EnemySkills skills = root.GetComponent<EnemySkills>();
        if (skills != null && skills.Animator != null && skills.Animator.runtimeAnimatorController != null)
        {
            return skills.Animator;
        }

        Animator[] animators = root.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            if (animators[i].runtimeAnimatorController != null)
            {
                return animators[i];
            }
        }

        return null;
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

    private static void SetObjectReference(Object component, string propertyName, Object value)
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
}
#endif
