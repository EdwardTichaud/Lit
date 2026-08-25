#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

/// <summary>
/// Keeps the AnimationLab preview enemy on the same root-Animator topology as
/// Juggernaut_Combat. This is deliberately an editor-only migration: no bake
/// ever has to guess whether an Animator belongs to a mesh child or ActorRoot.
/// </summary>
public static class CombatAnimationLabRootAnimatorEditor
{
    private const string AnimationLabScenePath = "Assets/Scenes/Workshop/AnimationLab.unity";
    private const string AnimationLabPrefabPath = "Assets/CombatRealTime/LightSkills/AnimationLab.prefab";
    private const string JuggernautPrefabPath = "Assets/Characters/3_Enemy/Juggernaut/Juggernaut_Combat.prefab";

    [MenuItem("Lit/Combat/Update AnimationLab Root Animators")]
    private static void SynchronizeAnimationLab()
    {
        if (!EditorUtility.DisplayDialog(
                "Update AnimationLab",
                "Aligner les previews Player/Enemy et les bindings Timeline sur le contrat Animator racine de Juggernaut_Combat ?",
                "Mettre a jour", "Annuler"))
        {
            return;
        }

        GameObject juggernaut = PrefabUtility.LoadPrefabContents(JuggernautPrefabPath);
        try
        {
            Animator sourceAnimator = juggernaut.GetComponent<CombatActorAnimationRoot>()?.Animator;
            if (sourceAnimator == null || sourceAnimator.runtimeAnimatorController == null)
            {
                EditorUtility.DisplayDialog("Update AnimationLab", "Juggernaut_Combat ne possede pas d'Animator racine valide.", "OK");
                return;
            }

            List<string> report = new List<string>();
            SynchronizeScene(sourceAnimator, report);
            SynchronizePrefab(sourceAnimator, report);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            string message = string.Join("\n", report);
            Debug.Log("[Combat AnimationLab] " + message);
            EditorUtility.DisplayDialog("Update AnimationLab", message, "OK");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(juggernaut);
        }
    }

    private static void SynchronizeScene(Animator sourceAnimator, List<string> report)
    {
        Scene scene = SceneManager.GetSceneByPath(AnimationLabScenePath);
        bool closeWhenDone = false;
        if (!scene.IsValid() || !scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(AnimationLabScenePath, OpenSceneMode.Additive);
            closeWhenDone = true;
        }

        try
        {
            if (SynchronizeSceneObjects(scene, sourceAnimator, report))
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }
        finally
        {
            if (closeWhenDone)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    private static void SynchronizePrefab(Animator sourceAnimator, List<string> report)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(AnimationLabPrefabPath);
        try
        {
            if (SynchronizeHierarchy(root, sourceAnimator, report))
            {
                PrefabUtility.SaveAsPrefabAsset(root, AnimationLabPrefabPath);
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static bool SynchronizeSceneObjects(Scene scene, Animator sourceAnimator, List<string> report)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        bool changed = false;
        for (int i = 0; i < roots.Length; i++)
        {
            changed |= SynchronizeHierarchy(roots[i], sourceAnimator, report);
        }
        return changed;
    }

    private static bool SynchronizeHierarchy(GameObject root, Animator sourceAnimator, List<string> report)
    {
        Transform enemyRoot = FindDescendant(root.transform, "Enemy_Preview");
        Transform playerRoot = FindDescendant(root.transform, "Lucian_Preview");
        if (enemyRoot == null && playerRoot == null)
        {
            report.Add(root.name + ": aucune preview acteur trouvee.");
            return false;
        }

        Animator previousEnemyAnimator = enemyRoot != null
            ? FindGameplayAnimator(enemyRoot, excludeRoot: false)
            : null;
        Animator enemyAnimator = enemyRoot != null
            ? EnsureRootAnimator(enemyRoot, previousEnemyAnimator, sourceAnimator)
            : null;
        Animator playerAnimator = playerRoot != null ? playerRoot.GetComponent<Animator>() : null;
        bool changed = enemyRoot != null;

        if (enemyRoot != null)
        {
            EnsureActorContract(enemyRoot, enemyAnimator);
            RebindTimelineActors(root, playerAnimator, previousEnemyAnimator, enemyAnimator);
            UpdateAuthoringReferences(root, playerRoot, enemyRoot, playerAnimator, enemyAnimator);
            report.Add(root.name + ": Enemy_Preview synchronise (Animator racine '" + enemyAnimator.name + "').");
        }
        return changed;
    }

    private static Animator EnsureRootAnimator(Transform actorRoot, Animator previousAnimator, Animator sourceAnimator)
    {
        Animator rootAnimator = actorRoot.GetComponent<Animator>();
        if (rootAnimator == null)
        {
            rootAnimator = actorRoot.gameObject.AddComponent<Animator>();
        }

        // The combat prefab is authoritative for controller, avatar and root
        // motion settings. Copying the component avoids a hidden authoring
        // mismatch between AnimationLab and the real enemy.
        EditorUtility.CopySerialized(sourceAnimator, rootAnimator);

        if (previousAnimator != null && previousAnimator != rootAnimator)
        {
            Object.DestroyImmediate(previousAnimator);
        }

        return rootAnimator;
    }

    private static void EnsureActorContract(Transform actorRoot, Animator animator)
    {
        CombatActorAnimationRoot contract = actorRoot.GetComponent<CombatActorAnimationRoot>();
        if (contract == null)
        {
            contract = actorRoot.gameObject.AddComponent<CombatActorAnimationRoot>();
        }

        contract.Configure(actorRoot, animator, actorRoot.Find("EnemyLockPoint"));
        if (actorRoot.GetComponent<CombatActorRootMotionRelay>() == null)
        {
            actorRoot.gameObject.AddComponent<CombatActorRootMotionRelay>();
        }
    }

    private static void RebindTimelineActors(GameObject root, Animator playerAnimator, Animator previousEnemyAnimator, Animator enemyAnimator)
    {
        PlayableDirector[] directors = root.GetComponentsInChildren<PlayableDirector>(true);
        for (int i = 0; i < directors.Length; i++)
        {
            if (directors[i].playableAsset is not TimelineAsset timeline) continue;
            foreach (PlayableBinding output in timeline.outputs)
            {
                if (output.sourceObject is not AnimationTrack track) continue;
                if (track.name == LightSkillTimelineContract.PlayerAnimatorTrack && playerAnimator != null)
                {
                    directors[i].SetGenericBinding(track, playerAnimator);
                }
                else if (track.name == LightSkillTimelineContract.EnemyAnimatorTrack &&
                         (previousEnemyAnimator == null || directors[i].GetGenericBinding(track) == previousEnemyAnimator))
                {
                    directors[i].SetGenericBinding(track, enemyAnimator);
                }
            }
        }
    }

    private static void UpdateAuthoringReferences(
        GameObject root,
        Transform playerRoot,
        Transform enemyRoot,
        Animator playerAnimator,
        Animator enemyAnimator)
    {
        LightSkillTimelineAuthoringRig[] lightRigs = root.GetComponentsInChildren<LightSkillTimelineAuthoringRig>(true);
        for (int i = 0; i < lightRigs.Length; i++)
        {
            SerializedObject serialized = new SerializedObject(lightRigs[i]);
            serialized.FindProperty("previewPlayerAnimator").objectReferenceValue = playerAnimator;
            serialized.FindProperty("previewEnemyAnimator").objectReferenceValue = enemyAnimator;
            serialized.FindProperty("previewPlayerActorRoot").objectReferenceValue = playerRoot;
            serialized.FindProperty("previewEnemyActorRoot").objectReferenceValue = enemyRoot;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        CombatSkillTimelineAuthoringRig[] skillRigs = root.GetComponentsInChildren<CombatSkillTimelineAuthoringRig>(true);
        for (int i = 0; i < skillRigs.Length; i++)
        {
            SerializedObject serialized = new SerializedObject(skillRigs[i]);
            serialized.FindProperty("previewPlayerAnimator").objectReferenceValue = playerAnimator;
            serialized.FindProperty("previewEnemyAnimator").objectReferenceValue = enemyAnimator;
            serialized.FindProperty("previewPlayerActorRoot").objectReferenceValue = playerRoot;
            serialized.FindProperty("previewEnemyActorRoot").objectReferenceValue = enemyRoot;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static Animator FindGameplayAnimator(Transform root, bool excludeRoot)
    {
        Animator[] animators = root.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            if ((!excludeRoot || animators[i].transform != root) && animators[i].runtimeAnimatorController != null)
            {
                return animators[i];
            }
        }
        return null;
    }

    private static Transform FindDescendant(Transform root, string name)
    {
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i].name == name) return transforms[i];
        }
        return null;
    }
}
#endif
