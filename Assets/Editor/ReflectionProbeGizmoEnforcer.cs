using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal static class ReflectionProbeGizmoEnforcer
{
    private const double CheckIntervalSeconds = 1d;
    private static double nextCheckTime;

    static ReflectionProbeGizmoEnforcer()
    {
        EditorApplication.delayCall += Enforce;
        EditorApplication.update += Update;
    }

    private static void Update()
    {
        if (EditorApplication.timeSinceStartup < nextCheckTime)
        {
            return;
        }

        nextCheckTime = EditorApplication.timeSinceStartup + CheckIntervalSeconds;
        Enforce();
    }

    private static void Enforce()
    {
        if (!GizmoUtility.TryGetGizmoInfo(typeof(ReflectionProbe), out GizmoInfo info))
        {
            return;
        }

        bool changed = false;
        if (info.hasGizmo && !info.gizmoEnabled)
        {
            info.gizmoEnabled = true;
            changed = true;
        }

        if (info.hasIcon && !info.iconEnabled)
        {
            info.iconEnabled = true;
            changed = true;
        }

        if (changed)
        {
            GizmoUtility.ApplyGizmoInfo(info, false);
            SceneView.RepaintAll();
        }
    }
}
