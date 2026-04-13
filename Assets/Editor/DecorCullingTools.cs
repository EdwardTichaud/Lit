using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class DecorCullingTools
{
    private const string MenuRoot = "Tools/Lit/Decor Culling/";
    private const string FantasticDungeonPackRoot = "Assets/Fantastic Dungeon Pack/prefabs";

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
