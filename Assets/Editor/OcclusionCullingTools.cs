using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class OcclusionCullingTools
{
    private const string MenuRoot = "Tools/Lit/Occlusion Culling/";
    private const string MaisonScenePath = "Assets/Scenes/Maison/Maison.unity";
    private const string WorldRootName = "World";
    private const float MinOccluderLargestAxis = 3f;
    private const float MinOccluderSecondAxis = 1.15f;
    private const int TransparentRenderQueue = 3000;

    private static readonly StaticEditorFlags OccludeeFlags = StaticEditorFlags.OccludeeStatic;
    private static readonly StaticEditorFlags OccluderFlags = StaticEditorFlags.OccludeeStatic | StaticEditorFlags.OccluderStatic;

    private static readonly HashSet<string> ExcludedBehaviourNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "BeaconMarker",
        "Flame",
        "BuilderController",
        "BuildingInfoInteractable",
        "CharacterController",
        "DestructibleObject",
        "HubCompanionSwapTrigger",
        "InteractableItem",
        "ItemSceneMarker",
        "LabyrinthStartTrigger",
        "Lever",
        "NavMeshAgent",
        "PersistentNetworkObject",
        "ReturnHomeTrigger",
        "SquadCharacterController",
        "DissolveRevealTarget",
        "TrouEtroit",
        "Zone"
    };

    [MenuItem(MenuRoot + "Configure Maison Scene")]
    public static void ConfigureMaisonScene()
    {
        ConfigureSceneAsset(MaisonScenePath, bakeAfterConfigure: false);
    }

    [MenuItem(MenuRoot + "Configure And Bake Maison Scene")]
    public static void ConfigureAndBakeMaisonScene()
    {
        ConfigureSceneAsset(MaisonScenePath, bakeAfterConfigure: true);
    }

    [MenuItem(MenuRoot + "Configure And Bake Maison World")]
    public static void ConfigureAndBakeMaisonWorld()
    {
        ConfigureWorldAsset(MaisonScenePath, bakeAfterConfigure: true);
    }

    [MenuItem(MenuRoot + "Configure Active Scene")]
    public static void ConfigureActiveScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            throw new InvalidOperationException("No active loaded scene to configure.");
        }

        OcclusionStats stats = ApplyToScene(scene, useUndo: true);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"Occlusion culling: active scene configured. {stats}");
    }

    [MenuItem(MenuRoot + "Configure And Bake Active Scene")]
    public static void ConfigureAndBakeActiveScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            throw new InvalidOperationException("No active loaded scene to configure.");
        }

        OcclusionStats stats = ApplyToScene(scene, useUndo: false);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("Occlusion culling: starting active scene bake.");
        StaticOcclusionCulling.Compute();

        if (!EditorSceneManager.SaveScene(scene))
        {
            throw new InvalidOperationException($"Could not save scene '{scene.path}'.");
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Occlusion culling: active scene configured and baked. {stats}");
    }

    [MenuItem(MenuRoot + "Configure Selected Hierarchies")]
    [MenuItem("GameObject/Lit/Configure Occlusion Culling", false, 49)]
    [MenuItem("Assets/Lit/Configure Occlusion Culling", false, 2000)]
    public static void ConfigureSelectedHierarchies()
    {
        GameObject[] selected = Selection.gameObjects;
        if (selected == null || selected.Length == 0)
        {
            Debug.LogWarning("Occlusion culling: select one or more scene objects or prefab assets first.");
            return;
        }

        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Configure Occlusion Culling");

        OcclusionStats total = default;
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
                total.Add(ApplyToPrefabAsset(assetPath));
                continue;
            }

            total.Add(ApplyToRoot(target, useUndo: true));
            EditorSceneManager.MarkSceneDirty(target.scene);
        }

        Undo.CollapseUndoOperations(undoGroup);
        AssetDatabase.SaveAssets();
        Debug.Log($"Occlusion culling: selected hierarchies configured. {total}");
    }

    [MenuItem(MenuRoot + "Configure Selected Hierarchies", true)]
    [MenuItem("GameObject/Lit/Configure Occlusion Culling", true)]
    [MenuItem("Assets/Lit/Configure Occlusion Culling", true)]
    private static bool ValidateConfigureSelectedHierarchies()
    {
        return Selection.gameObjects != null && Selection.gameObjects.Length > 0;
    }

    public static void ConfigureMaisonSceneFromCommandLine()
    {
        ConfigureSceneAsset(MaisonScenePath, bakeAfterConfigure: false);
    }

    public static void ConfigureAndBakeMaisonSceneFromCommandLine()
    {
        ConfigureSceneAsset(MaisonScenePath, bakeAfterConfigure: true);
    }

    public static void ConfigureAndBakeMaisonWorldFromCommandLine()
    {
        ConfigureWorldAsset(MaisonScenePath, bakeAfterConfigure: true);
    }

    private static void ConfigureSceneAsset(string scenePath, bool bakeAfterConfigure)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            throw new InvalidOperationException($"Could not open scene '{scenePath}'.");
        }

        OcclusionStats stats = ApplyToScene(scene, useUndo: false);
        EditorSceneManager.MarkSceneDirty(scene);

        if (bakeAfterConfigure)
        {
            Debug.Log("Occlusion culling: starting bake.");
            StaticOcclusionCulling.Compute();
            Debug.Log("Occlusion culling: bake completed.");
        }

        if (!EditorSceneManager.SaveScene(scene))
        {
            throw new InvalidOperationException($"Could not save scene '{scenePath}'.");
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Occlusion culling: scene '{scenePath}' configured. {stats}");
    }

    private static void ConfigureWorldAsset(string scenePath, bool bakeAfterConfigure)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            throw new InvalidOperationException($"Could not open scene '{scenePath}'.");
        }

        GameObject world = FindRootGameObject(scene, WorldRootName);
        if (world == null)
        {
            throw new InvalidOperationException($"Could not find root '{WorldRootName}' in '{scenePath}'.");
        }

        OcclusionStats stats = ApplyToRoot(world, useUndo: false);
        EditorSceneManager.MarkSceneDirty(scene);

        if (bakeAfterConfigure)
        {
            Debug.Log("Occlusion culling: starting Maison World bake.");
            StaticOcclusionCulling.Compute();
            Debug.Log("Occlusion culling: Maison World bake completed.");
        }

        if (!EditorSceneManager.SaveScene(scene))
        {
            throw new InvalidOperationException($"Could not save scene '{scenePath}'.");
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Occlusion culling: Maison World configured. {stats}");
    }

    private static GameObject FindRootGameObject(Scene scene, string name)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i] != null && string.Equals(roots[i].name, name, StringComparison.Ordinal))
            {
                return roots[i];
            }
        }

        return null;
    }

    private static OcclusionStats ApplyToScene(Scene scene, bool useUndo)
    {
        OcclusionStats stats = default;
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            stats.Add(ApplyToRoot(roots[i], useUndo));
        }

        return stats;
    }

    private static OcclusionStats ApplyToPrefabAsset(string prefabPath)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            if (root == null)
            {
                return default;
            }

            OcclusionStats stats = ApplyToRoot(root, useUndo: false);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            return stats;
        }
        finally
        {
            if (root != null)
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }

    private static OcclusionStats ApplyToRoot(GameObject root, bool useUndo)
    {
        OcclusionStats stats = default;
        if (root == null)
        {
            return stats;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            ApplyToRenderer(renderers[i], root.transform, useUndo, ref stats);
        }

        return stats;
    }

    private static void ApplyToRenderer(Renderer renderer, Transform scanRoot, bool useUndo, ref OcclusionStats stats)
    {
        if (renderer == null)
        {
            return;
        }

        stats.renderersScanned++;

        if (!IsSupportedRenderer(renderer))
        {
            stats.unsupportedRendererSkipped++;
            return;
        }

        if (!renderer.enabled || !renderer.gameObject.activeInHierarchy)
        {
            stats.inactiveSkipped++;
            return;
        }

        if (HasDynamicOrGameplayParent(renderer.transform, scanRoot))
        {
            stats.dynamicOrGameplaySkipped++;
            return;
        }

        bool opaque = IsOpaque(renderer);
        StaticEditorFlags flags = IsLargeEnoughOccluder(renderer) && opaque ? OccluderFlags : OccludeeFlags;

        if (AddStaticFlags(renderer.gameObject, flags, useUndo))
        {
            stats.gameObjectsChanged++;
        }

        stats.occludeesConfigured++;
        if ((flags & StaticEditorFlags.OccluderStatic) != 0)
        {
            stats.occludersConfigured++;
        }
        else if (!opaque)
        {
            stats.transparentOccluderSkipped++;
        }
    }

    private static bool IsSupportedRenderer(Renderer renderer)
    {
        return renderer is MeshRenderer && renderer.GetComponent<MeshFilter>() != null;
    }

    private static bool HasDynamicOrGameplayParent(Transform transform, Transform scanRoot)
    {
        Transform current = transform;
        while (current != null)
        {
            GameObject gameObject = current.gameObject;
            if (gameObject.GetComponent<Rigidbody>() != null ||
                gameObject.GetComponent<Animator>() != null)
            {
                return true;
            }

            MonoBehaviour[] behaviours = gameObject.GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null)
                {
                    continue;
                }

                Type type = behaviour.GetType();
                string namespaceName = type.Namespace ?? string.Empty;
                if (namespaceName.StartsWith("Unity.Netcode", StringComparison.Ordinal) ||
                    ExcludedBehaviourNames.Contains(type.Name))
                {
                    return true;
                }
            }

            if (current == scanRoot)
            {
                break;
            }

            current = current.parent;
        }

        return false;
    }

    private static bool IsOpaque(Renderer renderer)
    {
        Material[] materials = renderer.sharedMaterials;
        if (materials == null || materials.Length == 0)
        {
            return true;
        }

        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];
            if (material == null)
            {
                continue;
            }

            if (IsTransparent(material))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsTransparent(Material material)
    {
        int renderQueue = material.renderQueue;
        if (renderQueue < 0 && material.shader != null)
        {
            renderQueue = material.shader.renderQueue;
        }

        if (renderQueue >= TransparentRenderQueue)
        {
            return true;
        }

        string renderType = material.GetTag("RenderType", false, string.Empty);
        if (renderType.IndexOf("Transparent", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        return material.HasProperty("_SurfaceType") && material.GetFloat("_SurfaceType") > 0.5f;
    }

    private static bool IsLargeEnoughOccluder(Renderer renderer)
    {
        Vector3 size = renderer.bounds.size;
        float[] axes =
        {
            Mathf.Abs(size.x),
            Mathf.Abs(size.y),
            Mathf.Abs(size.z)
        };
        Array.Sort(axes);

        return axes[2] >= MinOccluderLargestAxis && axes[1] >= MinOccluderSecondAxis;
    }

    private static bool AddStaticFlags(GameObject gameObject, StaticEditorFlags flags, bool useUndo)
    {
        StaticEditorFlags current = GameObjectUtility.GetStaticEditorFlags(gameObject);
        StaticEditorFlags updated = current | flags;
        if (updated == current)
        {
            return false;
        }

        if (useUndo)
        {
            Undo.RecordObject(gameObject, "Configure Occlusion Culling");
        }

        GameObjectUtility.SetStaticEditorFlags(gameObject, updated);
        EditorUtility.SetDirty(gameObject);
        return true;
    }

    private struct OcclusionStats
    {
        public int renderersScanned;
        public int occludeesConfigured;
        public int occludersConfigured;
        public int gameObjectsChanged;
        public int unsupportedRendererSkipped;
        public int inactiveSkipped;
        public int dynamicOrGameplaySkipped;
        public int transparentOccluderSkipped;

        public void Add(OcclusionStats other)
        {
            renderersScanned += other.renderersScanned;
            occludeesConfigured += other.occludeesConfigured;
            occludersConfigured += other.occludersConfigured;
            gameObjectsChanged += other.gameObjectsChanged;
            unsupportedRendererSkipped += other.unsupportedRendererSkipped;
            inactiveSkipped += other.inactiveSkipped;
            dynamicOrGameplaySkipped += other.dynamicOrGameplaySkipped;
            transparentOccluderSkipped += other.transparentOccluderSkipped;
        }

        public override string ToString()
        {
            return $"scanned={renderersScanned}, occludees={occludeesConfigured}, occluders={occludersConfigured}, changed={gameObjectsChanged}, skippedUnsupported={unsupportedRendererSkipped}, skippedInactive={inactiveSkipped}, skippedDynamicOrGameplay={dynamicOrGameplaySkipped}, skippedTransparentOccluder={transparentOccluderSkipped}";
        }
    }
}
