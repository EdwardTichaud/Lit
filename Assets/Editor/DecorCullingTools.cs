using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class DecorCullingTools
{
    private const string MenuRoot = "Tools/Lit/Decor Culling/";
    private const string FantasticDungeonPackRoot = "Assets/Fantastic Dungeon Pack/prefabs";
    private const float SplitMaxBoundsRadius = 20f;

    private static readonly HashSet<string> ExcludedBehaviourNames = new HashSet<string>
    {
        "BeaconMarker",
        "Brasero",
        "BuildingInfoInteractable",
        "BuilderController",
        "DestructibleObject",
        "HubCompanionSwapTrigger",
        "InteractableItem",
        "ItemSceneMarker",
        "LabyrinthStartTrigger",
        "Lever",
        "PersistentNetworkObject",
        "ReturnHomeTrigger",
        "TorchVisionSensitive",
        "TreasureFinder",
        "TrouEtroit",
        "Zone"
    };

    private static readonly HashSet<string> ExcludedRootNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "lever",
        "levers",
        "trap",
        "traps"
    };

    [MenuItem(MenuRoot + "Create Manager In Scene")]
    public static void CreateManagerInScene()
    {
#if UNITY_2023_1_OR_NEWER
        DecorCullingManager existing = UnityEngine.Object.FindFirstObjectByType<DecorCullingManager>();
#else
        DecorCullingManager existing = UnityEngine.Object.FindObjectOfType<DecorCullingManager>();
#endif
        if (existing != null)
        {
            Selection.activeGameObject = existing.gameObject;
            return;
        }

        GameObject host = new GameObject("DecorCullingManager");
        Undo.RegisterCreatedObjectUndo(host, "Create Decor Culling Manager");
        host.AddComponent<DecorCullingManager>();
        Selection.activeGameObject = host;
    }

    [MenuItem(MenuRoot + "Add Cullable To Selection")]
    public static void AddCullableToSelection()
    {
        GameObject[] selected = Selection.gameObjects;
        int added = 0;
        int skipped = 0;

        for (int i = 0; i < selected.Length; i++)
        {
            GameObject target = selected[i];
            if (target == null)
            {
                continue;
            }

            string assetPath = AssetDatabase.GetAssetPath(target);
            if (!string.IsNullOrEmpty(assetPath) && assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                if (TryAddCullableToPrefab(assetPath))
                {
                    added++;
                }
                else
                {
                    skipped++;
                }

                continue;
            }

            if (TryAddCullableToSceneObject(target))
            {
                added++;
            }
            else
            {
                skipped++;
            }
        }

        Debug.Log($"Decor culling: {added} objet(s) prepares, {skipped} ignore(s).");
    }

    [MenuItem(MenuRoot + "Add Cullable To Selection", true)]
    private static bool ValidateAddCullableToSelection()
    {
        return Selection.gameObjects != null && Selection.gameObjects.Length > 0;
    }

    [MenuItem(MenuRoot + "Split Selection Into Smaller Cullables")]
    public static void SplitSelectionIntoSmallerCullables()
    {
        GameObject[] selected = Selection.gameObjects;
        int added = 0;
        int removed = 0;
        int skipped = 0;
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Split Decor Cullables");

        for (int i = 0; i < selected.Length; i++)
        {
            GameObject target = selected[i];
            if (target == null)
            {
                continue;
            }

            if (EditorUtility.IsPersistent(target))
            {
                skipped++;
                Debug.Log($"Decor culling split: '{target.name}' ignore (selectionne une instance de scene, pas un prefab asset).", target);
                continue;
            }

            if (!HasCullableTargets(target))
            {
                skipped++;
                Debug.Log($"Decor culling split: '{target.name}' ignore (aucun Renderer, Light ou ParticleSystem).", target);
                continue;
            }

            DecorCullable rootCullable = target.GetComponent<DecorCullable>();
            int addedBefore = added;
            Transform targetTransform = target.transform;
            for (int childIndex = 0; childIndex < targetTransform.childCount; childIndex++)
            {
                AddCullablesRecursively(targetTransform.GetChild(childIndex).gameObject, ref added, ref skipped);
            }

            if (added > addedBefore)
            {
                if (rootCullable != null)
                {
                    Undo.DestroyObjectImmediate(rootCullable);
                    removed++;
                }
            }
            else
            {
                skipped++;
                Debug.Log($"Decor culling split: '{target.name}' n'a pas de sous-racine decor eligible.", target);
            }

            EditorUtility.SetDirty(target);
        }

        Undo.CollapseUndoOperations(undoGroup);
        Debug.Log($"Decor culling split: {added} sous-racine(s) preparee(s), {removed} DecorCullable racine retire(s), {skipped} element(s) ignore(s).");
    }

    [MenuItem(MenuRoot + "Split Selection Into Smaller Cullables", true)]
    private static bool ValidateSplitSelectionIntoSmallerCullables()
    {
        return Selection.gameObjects != null && Selection.gameObjects.Length > 0;
    }

    [MenuItem(MenuRoot + "Batch Fantastic Dungeon Pack Props And FX")]
    public static void BatchFantasticDungeonPackPropsAndFx()
    {
        string[] prefabGuids = AssetDatabase.FindAssets(
            "t:Prefab",
            new[] { $"{FantasticDungeonPackRoot}/PROPS", $"{FantasticDungeonPackRoot}/FX" });
        int added = 0;
        int skipped = 0;

        for (int i = 0; i < prefabGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
            if (ShouldSkipFantasticDungeonPrefab(path))
            {
                skipped++;
                continue;
            }

            if (TryAddCullableToPrefab(path))
            {
                added++;
            }
            else
            {
                skipped++;
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Decor culling batch: {added} prefab(s) prepares, {skipped} ignore(s).");
    }

    private static bool TryAddCullableToSceneObject(GameObject root)
    {
        if (!CanAddCullable(root, out string reason))
        {
            Debug.Log($"Decor culling: '{root.name}' ignore ({reason}).", root);
            return false;
        }

        DecorCullable cullable = Undo.AddComponent<DecorCullable>(root);
        cullable.RefreshCachedTargets();
        EditorUtility.SetDirty(root);
        return true;
    }

    private static bool TryAddCullableToPrefab(string prefabPath)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            if (root == null)
            {
                return false;
            }

            if (!CanAddCullable(root, out _))
            {
                return false;
            }

            DecorCullable cullable = root.AddComponent<DecorCullable>();
            cullable.RefreshCachedTargets();
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            return true;
        }
        finally
        {
            if (root != null)
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }

    private static bool CanAddCullable(GameObject root, out string reason)
    {
        reason = string.Empty;
        if (root == null)
        {
            reason = "racine nulle";
            return false;
        }

        if (root.GetComponent<DecorCullable>() != null)
        {
            reason = "DecorCullable deja present";
            return false;
        }

        if (ExcludedRootNames.Contains(root.name))
        {
            reason = $"groupe exclu: {root.name}";
            return false;
        }

        if (HasExcludedBehaviour(root, out string behaviourName))
        {
            reason = $"script gameplay detecte: {behaviourName}";
            return false;
        }

        if (!HasCullableTargets(root))
        {
            reason = "aucun Renderer, Light ou ParticleSystem";
            return false;
        }

        return true;
    }

    private static void AddCullablesRecursively(GameObject root, ref int added, ref int skipped)
    {
        if (root == null)
        {
            return;
        }

        if (ExcludedRootNames.Contains(root.name))
        {
            skipped++;
            Debug.Log($"Decor culling split: '{root.name}' ignore (groupe gameplay probable).", root);
            return;
        }

        if (!HasCullableTargets(root))
        {
            skipped++;
            return;
        }

        if (root.GetComponent<DecorCullable>() != null)
        {
            skipped++;
            return;
        }

        if (ShouldSplitFurther(root))
        {
            int addedBefore = added;
            Transform rootTransform = root.transform;
            for (int childIndex = 0; childIndex < rootTransform.childCount; childIndex++)
            {
                AddCullablesRecursively(rootTransform.GetChild(childIndex).gameObject, ref added, ref skipped);
            }

            if (added > addedBefore)
            {
                return;
            }
        }

        if (TryAddCullableToSceneObject(root))
        {
            added++;
        }
        else
        {
            skipped++;
        }
    }

    private static bool ShouldSplitFurther(GameObject root)
    {
        if (root.transform.childCount == 0)
        {
            return false;
        }

        if (!TryCalculateRendererBounds(root, out Bounds bounds))
        {
            return false;
        }

        if (bounds.extents.magnitude <= SplitMaxBoundsRadius)
        {
            return false;
        }

        Transform rootTransform = root.transform;
        for (int childIndex = 0; childIndex < rootTransform.childCount; childIndex++)
        {
            if (HasCullableTargets(rootTransform.GetChild(childIndex).gameObject))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryCalculateRendererBounds(GameObject root, out Bounds bounds)
    {
        bounds = default;
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    private static bool HasCullableTargets(GameObject root)
    {
        return root.GetComponentInChildren<Renderer>(true) != null
            || root.GetComponentInChildren<Light>(true) != null
            || root.GetComponentInChildren<ParticleSystem>(true) != null;
    }

    private static bool HasExcludedBehaviour(GameObject root, out string behaviourName)
    {
        MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null)
            {
                continue;
            }

            Type type = behaviour.GetType();
            if (type == typeof(DecorCullable) || type == typeof(DecorCullingManager))
            {
                continue;
            }

            string namespaceName = type.Namespace ?? string.Empty;
            if (namespaceName.StartsWith("Unity.Netcode", StringComparison.Ordinal))
            {
                behaviourName = type.Name;
                return true;
            }

            if (ExcludedBehaviourNames.Contains(type.Name))
            {
                behaviourName = type.Name;
                return true;
            }
        }

        behaviourName = string.Empty;
        return false;
    }

    private static bool ShouldSkipFantasticDungeonPrefab(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return true;
        }

        string normalized = path.Replace('\\', '/');
        return normalized.IndexOf("/levers/", StringComparison.OrdinalIgnoreCase) >= 0
            || normalized.IndexOf("/traps/", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
