using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class VisibilityOptimizationTools
{
    private const string MenuRoot = "Tools/Lit/Visibility Optimization/";

    private static readonly HashSet<string> CriticalBehaviourNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "AudioManager",
        "KnowledgeManager",
        "SquadManager",
        "NetcodeBootstrap",
        "NetworkObject",
        "PersistentNetworkObject",
        "WorldInteractionService",
        "Zone",
        "EnvironmentZone",
        "TemporalZone",
        "ReturnHomeTrigger",
        "LabyrinthStartTrigger",
        "HubCompanionSwapTrigger",
        "BuildingInfoInteractable",
        "InteractableItem",
        "Door",
        "Flame",
        "Flame",
        "DestructibleObject",
        "SquadCharacterController",
        "GhostController",
        "CharacterController",
        "NavMeshAgent"
    };

    [MenuItem(MenuRoot + "Audit Active Scene")]
    public static void AuditActiveScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogWarning("[VisibilityOptimization] No active loaded scene.");
            return;
        }

        AuditStats stats = new AuditStats();
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            ScanRoot(roots[i], stats);
        }

        Debug.Log(stats.Format(scene.name));
    }

    [MenuItem(MenuRoot + "Install Manager In Active Scene")]
    public static void InstallManagerInActiveScene()
    {
#if UNITY_2023_1_OR_NEWER
        VisibilityOptimizationManager existing = UnityEngine.Object.FindAnyObjectByType<VisibilityOptimizationManager>();
#else
        VisibilityOptimizationManager existing = UnityEngine.Object.FindAnyObjectByType<VisibilityOptimizationManager>();
#endif
        if (existing != null)
        {
            Selection.activeGameObject = existing.gameObject;
            return;
        }

        GameObject host = new GameObject("VisibilityOptimizationManager");
        Undo.RegisterCreatedObjectUndo(host, "Create Visibility Optimization Manager");
        host.AddComponent<VisibilityOptimizationManager>();
        Selection.activeGameObject = host;
        Debug.Log("[VisibilityOptimization] Manager installed in active scene.", host);
    }

    [MenuItem(MenuRoot + "Add OptimizableObject To Selection")]
    public static void AddOptimizableObjectToSelection()
    {
        GameObject[] selection = Selection.gameObjects;
        int added = 0;
        int skipped = 0;
        for (int i = 0; i < selection.Length; i++)
        {
            GameObject target = selection[i];
            if (target == null)
            {
                continue;
            }

            if (!CanOptimizeRoot(target, out string reason))
            {
                skipped++;
                Debug.Log($"[VisibilityOptimization] Skip '{target.name}': {reason}", target);
                continue;
            }

            OptimizableObject optimizableObject = target.GetComponent<OptimizableObject>();
            if (optimizableObject == null)
            {
                optimizableObject = Undo.AddComponent<OptimizableObject>(target);
                added++;
            }

            optimizableObject.RefreshCachedTargets();
            EditorUtility.SetDirty(target);
        }

        Debug.Log($"[VisibilityOptimization] OptimizableObject added={added}, skipped={skipped}.");
    }

    [MenuItem(MenuRoot + "Add OptimizableObject To Selection", true)]
    private static bool ValidateAddOptimizableObjectToSelection()
    {
        return Selection.gameObjects != null && Selection.gameObjects.Length > 0;
    }

    [MenuItem(MenuRoot + "Mark Selection As Camera Visibility Obstacles")]
    public static void MarkSelectionAsCameraVisibilityObstacles()
    {
        GameObject[] selection = Selection.gameObjects;
        int added = 0;
        for (int i = 0; i < selection.Length; i++)
        {
            GameObject target = selection[i];
            if (target == null)
            {
                continue;
            }

            if (target.GetComponent<CameraVisibilityObstacle>() == null)
            {
                Undo.AddComponent<CameraVisibilityObstacle>(target);
                added++;
                EditorUtility.SetDirty(target);
            }
        }

        Debug.Log($"[VisibilityOptimization] CameraVisibilityObstacle added={added}.");
    }

    [MenuItem(MenuRoot + "Mark Selection As Camera Visibility Obstacles", true)]
    private static bool ValidateMarkSelectionAsCameraVisibilityObstacles()
    {
        return Selection.gameObjects != null && Selection.gameObjects.Length > 0;
    }

    private static void ScanRoot(GameObject root, AuditStats stats)
    {
        if (root == null)
        {
            return;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            stats.Renderers++;
            if (renderer is SkinnedMeshRenderer)
            {
                stats.SkinnedRenderers++;
            }

            if (renderer.GetComponentInParent<LODGroup>() == null)
            {
                stats.RenderersWithoutLod++;
            }

            if (renderer.enabled && renderer.gameObject.isStatic && !HasCriticalBehaviourParent(renderer.transform))
            {
                stats.StaticBatchingCandidates++;
            }
        }

        LODGroup[] lodGroups = root.GetComponentsInChildren<LODGroup>(true);
        stats.LodGroups += lodGroups.Length;

        Light[] lights = root.GetComponentsInChildren<Light>(true);
        for (int i = 0; i < lights.Length; i++)
        {
            Light light = lights[i];
            if (light == null)
            {
                continue;
            }

            stats.Lights++;
            if (light.lightmapBakeType == LightmapBakeType.Realtime)
            {
                stats.RealtimeLights++;
            }
            else if (light.lightmapBakeType == LightmapBakeType.Mixed)
            {
                stats.MixedLights++;
            }
            else if (light.lightmapBakeType == LightmapBakeType.Baked)
            {
                stats.BakedLights++;
            }

            if (light.shadows != LightShadows.None)
            {
                stats.ShadowCastingLights++;
            }

            if (light.type != LightType.Directional && light.range > 20f)
            {
                stats.LargeRangeLights++;
            }
        }

        MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null)
            {
                stats.MissingScripts++;
                continue;
            }

            Type type = behaviour.GetType();
            bool hasUpdate = DeclaresMethod(type, "Update");
            bool hasLateUpdate = DeclaresMethod(type, "LateUpdate");
            bool hasFixedUpdate = DeclaresMethod(type, "FixedUpdate");
            if (!hasUpdate && !hasLateUpdate && !hasFixedUpdate)
            {
                continue;
            }

            stats.UpdateBehaviours++;
            stats.AddUpdateType(type.Name, hasUpdate, hasLateUpdate, hasFixedUpdate);
        }

        OptimizableObject[] optimizables = root.GetComponentsInChildren<OptimizableObject>(true);
        stats.OptimizableObjects += optimizables.Length;
    }

    private static bool CanOptimizeRoot(GameObject root, out string reason)
    {
        reason = string.Empty;
        if (root == null)
        {
            reason = "null root";
            return false;
        }

        if (root.GetComponent<OptimizableObject>() != null)
        {
            reason = "OptimizableObject already present";
            return false;
        }

        if (HasCriticalBehaviourParent(root.transform) || HasCriticalBehaviour(root))
        {
            reason = "critical gameplay behaviour detected";
            return false;
        }

        if (root.GetComponentInChildren<Renderer>(true) == null &&
            root.GetComponentInChildren<Light>(true) == null)
        {
            reason = "no Renderer or Light";
            return false;
        }

        return true;
    }

    private static bool HasCriticalBehaviourParent(Transform transform)
    {
        Transform current = transform;
        while (current != null)
        {
            if (HasCriticalBehaviour(current.gameObject))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private static bool HasCriticalBehaviour(GameObject target)
    {
        if (target == null)
        {
            return false;
        }

        MonoBehaviour[] behaviours = target.GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null)
            {
                continue;
            }

            Type type = behaviour.GetType();
            if (CriticalBehaviourNames.Contains(type.Name) ||
                behaviour is ICharacterDetectedInteractable)
            {
                return true;
            }
        }

        return false;
    }

    private static bool DeclaresMethod(Type type, string methodName)
    {
        if (type == null)
        {
            return false;
        }

        MethodInfo method = type.GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        return method != null;
    }

    private sealed class AuditStats
    {
        public int Renderers;
        public int SkinnedRenderers;
        public int RenderersWithoutLod;
        public int LodGroups;
        public int Lights;
        public int RealtimeLights;
        public int MixedLights;
        public int BakedLights;
        public int ShadowCastingLights;
        public int LargeRangeLights;
        public int StaticBatchingCandidates;
        public int UpdateBehaviours;
        public int MissingScripts;
        public int OptimizableObjects;
        private readonly Dictionary<string, int> updateTypes = new Dictionary<string, int>();

        public void AddUpdateType(string typeName, bool update, bool lateUpdate, bool fixedUpdate)
        {
            string key = typeName;
            if (update)
            {
                Increment($"{key}.Update");
            }

            if (lateUpdate)
            {
                Increment($"{key}.LateUpdate");
            }

            if (fixedUpdate)
            {
                Increment($"{key}.FixedUpdate");
            }
        }

        public string Format(string sceneName)
        {
            StringBuilder builder = new StringBuilder(2048);
            builder.AppendLine($"[VisibilityOptimization] Audit scene='{sceneName}'");
            builder.AppendLine($"Renderers={Renderers}, Skinned={SkinnedRenderers}, withoutLOD={RenderersWithoutLod}, LODGroups={LodGroups}, optimizableObjects={OptimizableObjects}");
            builder.AppendLine($"Lights={Lights}, realtime={RealtimeLights}, mixed={MixedLights}, baked={BakedLights}, shadowCasting={ShadowCastingLights}, largeRange>20={LargeRangeLights}");
            builder.AppendLine($"Static batching candidates={StaticBatchingCandidates}, behavioursWithUpdate={UpdateBehaviours}, missingScripts={MissingScripts}");

            if (updateTypes.Count > 0)
            {
                builder.AppendLine("Top Update/LateUpdate/FixedUpdate declarations:");
                int emitted = 0;
                foreach (KeyValuePair<string, int> pair in SortDescending(updateTypes))
                {
                    builder.AppendLine($"- {pair.Key}: {pair.Value}");
                    emitted++;
                    if (emitted >= 30)
                    {
                        break;
                    }
                }
            }

            return builder.ToString();
        }

        private void Increment(string key)
        {
            updateTypes.TryGetValue(key, out int count);
            updateTypes[key] = count + 1;
        }

        private static List<KeyValuePair<string, int>> SortDescending(Dictionary<string, int> values)
        {
            List<KeyValuePair<string, int>> list = new List<KeyValuePair<string, int>>(values);
            list.Sort((left, right) => right.Value.CompareTo(left.Value));
            return list;
        }
    }
}
