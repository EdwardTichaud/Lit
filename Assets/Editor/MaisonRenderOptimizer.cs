using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

public static class MaisonRenderOptimizer
{
    private const string MenuRoot = "Tools/Lit/Maison Render Optimizer/";

    [MenuItem(MenuRoot + "Audit Active Scene")]
    public static void AuditActiveScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogWarning("[MaisonRenderOptimizer] No active loaded scene.");
            return;
        }

        RenderStats stats = CollectStats(scene);
        Debug.Log(stats.Format(scene.name));
    }

    [MenuItem(MenuRoot + "Apply Fluidity Preset")]
    public static void ApplyFluidityPreset()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogWarning("[MaisonRenderOptimizer] No active loaded scene.");
            return;
        }

        int rendererChanges = OptimizeRenderers(scene);
        int lightChanges = OptimizeLights(scene);
        int managerChanges = ConfigureRuntimeManagers();

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log(
            $"[MaisonRenderOptimizer] Fluidity preset applied to '{scene.name}'. renderers={rendererChanges}, lights={lightChanges}, managers={managerChanges}.");
    }

    private static int OptimizeRenderers(Scene scene)
    {
        int changes = 0;
        Renderer[] renderers = FindSceneObjects<Renderer>(scene, includeInactive: true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            Undo.RecordObject(renderer, "Optimize Maison Renderer");

            if (!(renderer is SkinnedMeshRenderer) &&
                renderer.motionVectorGenerationMode != MotionVectorGenerationMode.ForceNoMotion)
            {
                renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                changes++;
            }

            if (!(renderer is SkinnedMeshRenderer) &&
                renderer.reflectionProbeUsage != ReflectionProbeUsage.Off &&
                !IsReflectionImportant(renderer))
            {
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                changes++;
            }

            if (!(renderer is SkinnedMeshRenderer) && IsSmallDecoration(renderer) && !IsLikelyStructural(renderer))
            {
                if (renderer.shadowCastingMode != ShadowCastingMode.Off)
                {
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    changes++;
                }

                if (renderer.receiveShadows)
                {
                    renderer.receiveShadows = false;
                    changes++;
                }
            }

            DisableRayTracing(renderer);
            EditorUtility.SetDirty(renderer);
        }

        return changes;
    }

    private static int OptimizeLights(Scene scene)
    {
        int changes = 0;
        Light[] lights = FindSceneObjects<Light>(scene, includeInactive: true);
        Array.Sort(lights, CompareLightsForBudget);

        int realtimeBudget = 24;
        int shadowBudget = 8;
        int realtimeCount = 0;
        int shadowCount = 0;

        for (int i = 0; i < lights.Length; i++)
        {
            Light light = lights[i];
            if (light == null || light.lightmapBakeType == LightmapBakeType.Baked || !IsRuntimeLightType(light))
            {
                continue;
            }

            Undo.RecordObject(light, "Optimize Maison Light");
            bool critical = IsCriticalLight(light);
            bool allowRealtime = critical || realtimeCount < realtimeBudget;
            bool allowShadow = allowRealtime && (critical || shadowCount < shadowBudget) && light.shadows != LightShadows.None;

            if (allowRealtime)
            {
                realtimeCount++;
            }

            if (light.enabled != allowRealtime)
            {
                light.enabled = allowRealtime;
                changes++;
            }

            if (!allowShadow && light.shadows != LightShadows.None)
            {
                light.shadows = LightShadows.None;
                changes++;
            }
            else if (allowShadow)
            {
                shadowCount++;
            }

            if (light.renderMode != LightRenderMode.Auto)
            {
                light.renderMode = LightRenderMode.Auto;
                changes++;
            }

            HDAdditionalLightData hdLight = light.GetComponent<HDAdditionalLightData>();
            if (hdLight != null)
            {
                Undo.RecordObject(hdLight, "Optimize Maison HDRP Light");
                hdLight.SetShadowResolution(512);
                hdLight.useContactShadow.useOverride = true;
                hdLight.useContactShadow.@override = false;
                EditorUtility.SetDirty(hdLight);
                changes++;
            }

            EditorUtility.SetDirty(light);
        }

        return changes;
    }

    private static int ConfigureRuntimeManagers()
    {
        int changes = 0;

        VisibilityOptimizationManager visibility = UnityEngine.Object.FindFirstObjectByType<VisibilityOptimizationManager>();
        if (visibility != null)
        {
            SerializedObject serialized = new SerializedObject(visibility);
            SetBool(serialized, "discoverObjectsOnEnable", true, ref changes);
            SetBool(serialized, "autoDiscoverObjects", false, ref changes);
            SetFloat(serialized, "rescanInterval", 8f, ref changes);
            SetFloat(serialized, "evaluationInterval", 0.15f, ref changes);
            SetInt(serialized, "maxEvaluationsPerFrame", 128, ref changes);
            SetFloat(serialized, "obstructionCheckInterval", 0.35f, ref changes);
            SetInt(serialized, "maxObstructionChecksPerFrame", 12, ref changes);
            SetInt(serialized, "obstructionSampleCount", 1, ref changes);
            SetLayerMask(serialized, "obstructionLayers", LayerMask.GetMask("Ground", "Stairs", "CameraObstruction", "Obstacle"), ref changes);
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(visibility);
        }

        SceneLightOcclusionEnforcer enforcer = UnityEngine.Object.FindFirstObjectByType<SceneLightOcclusionEnforcer>();
        if (enforcer != null)
        {
            SerializedObject serialized = new SerializedObject(enforcer);
            SetBool(serialized, "enforceOnEnable", false, ref changes);
            SetBool(serialized, "enforceContinuously", false, ref changes);
            SetBool(serialized, "enableHdrpContactShadows", false, ref changes);
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(enforcer);
        }

        GameObject host = visibility != null ? visibility.gameObject : new GameObject("RenderOptimizationManagers");
        if (visibility == null)
        {
            Undo.RegisterCreatedObjectUndo(host, "Create Render Optimization Managers");
        }

        EnsureComponent<PortalRenderScheduler>(host, ref changes);
        EnsureComponent<LightRenderBudgetManager>(host, ref changes);

        return changes;
    }

    private static T EnsureComponent<T>(GameObject host, ref int changes) where T : Component
    {
        T component = UnityEngine.Object.FindFirstObjectByType<T>();
        if (component != null)
        {
            return component;
        }

        changes++;
        return Undo.AddComponent<T>(host);
    }

    private static RenderStats CollectStats(Scene scene)
    {
        RenderStats stats = new RenderStats();

        Renderer[] renderers = FindSceneObjects<Renderer>(scene, includeInactive: true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            stats.Renderers++;
            if (renderer.enabled)
            {
                stats.EnabledRenderers++;
            }

            if (renderer.shadowCastingMode != ShadowCastingMode.Off)
            {
                stats.ShadowCasters++;
            }

            if (renderer.receiveShadows)
            {
                stats.ShadowReceivers++;
            }

            if (renderer.motionVectorGenerationMode != MotionVectorGenerationMode.ForceNoMotion)
            {
                stats.MotionVectors++;
            }

            if (renderer.reflectionProbeUsage != ReflectionProbeUsage.Off)
            {
                stats.ReflectionProbeUsers++;
            }
        }

        Light[] lights = FindSceneObjects<Light>(scene, includeInactive: true);
        for (int i = 0; i < lights.Length; i++)
        {
            Light light = lights[i];
            if (light == null)
            {
                continue;
            }

            stats.Lights++;
            if (light.enabled)
            {
                stats.EnabledLights++;
            }

            if (light.lightmapBakeType == LightmapBakeType.Realtime)
            {
                stats.RealtimeLights++;
            }

            if (light.shadows != LightShadows.None)
            {
                stats.ShadowedLights++;
            }
        }

        stats.Cameras = FindSceneObjects<Camera>(scene, includeInactive: true).Length;
        stats.PortalSchedulers = UnityEngine.Object.FindObjectsByType<PortalRenderScheduler>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        stats.LightBudgetManagers = UnityEngine.Object.FindObjectsByType<LightRenderBudgetManager>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        return stats;
    }

    private static T[] FindSceneObjects<T>(Scene scene, bool includeInactive) where T : Component
    {
        List<T> results = new List<T>();
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            results.AddRange(roots[i].GetComponentsInChildren<T>(includeInactive));
        }

        return results.ToArray();
    }

    private static int CompareLightsForBudget(Light left, Light right)
    {
        bool leftCritical = IsCriticalLight(left);
        bool rightCritical = IsCriticalLight(right);
        if (leftCritical != rightCritical)
        {
            return leftCritical ? -1 : 1;
        }

        bool leftShadowed = left != null && left.shadows != LightShadows.None;
        bool rightShadowed = right != null && right.shadows != LightShadows.None;
        if (leftShadowed != rightShadowed)
        {
            return leftShadowed ? -1 : 1;
        }

        return 0;
    }

    private static bool IsRuntimeLightType(Light light)
    {
        if (light == null)
        {
            return false;
        }

        return light.type == LightType.Point || light.type == LightType.Spot;
    }

    private static bool IsCriticalLight(Light light)
    {
        if (light == null)
        {
            return false;
        }

        LightRenderPriority priority = light.GetComponent<LightRenderPriority>();
        return priority != null && priority.Critical;
    }

    private static bool IsSmallDecoration(Renderer renderer)
    {
        if (renderer == null)
        {
            return false;
        }

        return renderer.bounds.extents.magnitude < 0.8f;
    }

    private static bool IsLikelyStructural(Renderer renderer)
    {
        if (renderer == null)
        {
            return false;
        }

        string name = renderer.name.ToLowerInvariant();
        return name.Contains("wall") ||
               name.Contains("floor") ||
               name.Contains("ceiling") ||
               name.Contains("roof") ||
               name.Contains("stairs") ||
               name.Contains("door") ||
               name.Contains("ground");
    }

    private static bool IsReflectionImportant(Renderer renderer)
    {
        if (renderer == null)
        {
            return false;
        }

        string name = renderer.name.ToLowerInvariant();
        return name.Contains("mirror") || name.Contains("glass") || name.Contains("water");
    }

    private static void DisableRayTracing(Renderer renderer)
    {
        SerializedObject serialized = new SerializedObject(renderer);
        SerializedProperty rayTracingMode = serialized.FindProperty("m_RayTracingMode");
        if (rayTracingMode != null && rayTracingMode.intValue != 0)
        {
            rayTracingMode.intValue = 0;
            serialized.ApplyModifiedProperties();
        }
    }

    private static void SetBool(SerializedObject serialized, string propertyName, bool value, ref int changes)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null && property.boolValue != value)
        {
            property.boolValue = value;
            changes++;
        }
    }

    private static void SetFloat(SerializedObject serialized, string propertyName, float value, ref int changes)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null && !Mathf.Approximately(property.floatValue, value))
        {
            property.floatValue = value;
            changes++;
        }
    }

    private static void SetInt(SerializedObject serialized, string propertyName, int value, ref int changes)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null && property.intValue != value)
        {
            property.intValue = value;
            changes++;
        }
    }

    private static void SetLayerMask(SerializedObject serialized, string propertyName, int value, ref int changes)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null && property.intValue != value)
        {
            property.intValue = value;
            changes++;
        }
    }

    private struct RenderStats
    {
        public int Renderers;
        public int EnabledRenderers;
        public int ShadowCasters;
        public int ShadowReceivers;
        public int MotionVectors;
        public int ReflectionProbeUsers;
        public int Lights;
        public int EnabledLights;
        public int RealtimeLights;
        public int ShadowedLights;
        public int Cameras;
        public int PortalSchedulers;
        public int LightBudgetManagers;

        public string Format(string sceneName)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine($"[MaisonRenderOptimizer] Audit '{sceneName}'");
            builder.AppendLine($"Renderers: {EnabledRenderers}/{Renderers} enabled, shadows cast={ShadowCasters}, receive={ShadowReceivers}");
            builder.AppendLine($"Motion vectors={MotionVectors}, reflection probes={ReflectionProbeUsers}");
            builder.AppendLine($"Lights: {EnabledLights}/{Lights} enabled, realtime={RealtimeLights}, shadowed={ShadowedLights}");
            builder.AppendLine($"Cameras={Cameras}, PortalRenderScheduler={PortalSchedulers}, LightRenderBudgetManager={LightBudgetManagers}");
            return builder.ToString();
        }
    }
}
