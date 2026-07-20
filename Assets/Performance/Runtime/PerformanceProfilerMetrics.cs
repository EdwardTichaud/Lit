using System.Diagnostics;
using Unity.Profiling;
using UnityEngine;

namespace Lit.Performance
{
    public static class PerformanceProfilerMetrics
    {
        public const string VisibilityObjectsExaminedCounterName = "Lit Visibility Objects Examined";
        public const string VisibilityObjectsModifiedCounterName = "Lit Visibility Objects Modified";
        public const string VisibilityRaycastsCounterName = "Lit Visibility Raycasts";
        public const string LightCandidatesCounterName = "Lit Light Candidates";
        public const string LightRetainedCounterName = "Lit Lights Retained";
        public const string LightShadowsRetainedCounterName = "Lit Shadowed Lights Retained";
        public const string PortalCamerasEvaluatedCounterName = "Lit Portal Cameras Evaluated";
        public const string PortalCamerasRenderedCounterName = "Lit Portal Cameras Rendered";
        public const string IceRenderersExaminedCounterName = "Lit Ice Renderers Examined";
        public const string IceRenderersModifiedCounterName = "Lit Ice Renderers Modified";
        public const string IceInfluencesActiveCounterName = "Lit Ice Influences Active";
        public const string IceInfluencesSelectedCounterName = "Lit Ice Influences Selected";
        public const string IceInfluencesDiscardedCounterName = "Lit Ice Influences Discarded";

        public static readonly ProfilerMarker VisibilityUpdate =
            new ProfilerMarker(ProfilerCategory.Scripts, "Lit.Performance.Visibility.Update");
        public static readonly ProfilerMarker OptimizableObjectProcessing =
            new ProfilerMarker(ProfilerCategory.Scripts, "Lit.Performance.Visibility.OptimizableObject");
        public static readonly ProfilerMarker ObstructionRaycasts =
            new ProfilerMarker(ProfilerCategory.Physics, "Lit.Performance.Visibility.ObstructionRaycasts");
        public static readonly ProfilerMarker LightBudget =
            new ProfilerMarker(ProfilerCategory.Scripts, "Lit.Performance.Lighting.Budget");
        public static readonly ProfilerMarker PortalSelection =
            new ProfilerMarker(ProfilerCategory.Scripts, "Lit.Performance.Portals.Selection");
        public static readonly ProfilerMarker PortalScheduling =
            new ProfilerMarker(ProfilerCategory.Scripts, "Lit.Performance.Portals.RenderScheduling");
        public static readonly ProfilerMarker IceInfluenceScan =
            new ProfilerMarker(ProfilerCategory.Scripts, "Lit.Performance.Ice.InfluenceScan");
        public static readonly ProfilerMarker IceInfluenceTransitions =
            new ProfilerMarker(ProfilerCategory.Scripts, "Lit.Performance.Ice.InfluenceTransitions");

        private static int visibilityFrame = -1;
        private static int lightFrame = -1;
        private static int portalFrame = -1;
        private static int iceFrame = -1;
        private static int visibilityObjectsExamined;
        private static int visibilityObjectsModified;
        private static int visibilityRaycasts;
        private static int lightCandidates;
        private static int lightsRetained;
        private static int shadowedLightsRetained;
        private static int portalCamerasEvaluated;
        private static int portalCamerasRendered;
        private static int iceRenderersExamined;
        private static int iceRenderersModified;
        private static int iceInfluencesActive;
        private static int iceInfluencesSelected;
        private static int iceInfluencesDiscarded;
        private static int iceTransitioningRenderers;

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Begin(ProfilerMarker marker)
        {
            marker.Begin();
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void End(ProfilerMarker marker)
        {
            marker.End();
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void PublishVisibility(int examined, int modified, int raycasts)
        {
            visibilityFrame = Time.frameCount;
            visibilityObjectsExamined = examined;
            visibilityObjectsModified = modified;
            visibilityRaycasts = raycasts;
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void PublishLightBudget(int candidates, int retained, int shadowedRetained)
        {
            lightFrame = Time.frameCount;
            lightCandidates = candidates;
            lightsRetained = retained;
            shadowedLightsRetained = shadowedRetained;
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void PublishPortalBudget(int evaluated, int rendered)
        {
            portalFrame = Time.frameCount;
            portalCamerasEvaluated = evaluated;
            portalCamerasRendered = rendered;
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void AddIceInfluences(int examined, int modified)
        {
            EnsureIceFrame();

            iceRenderersExamined += examined;
            iceRenderersModified += modified;
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void AddIceInfluenceSelection(
            int active,
            int selected,
            int transitioningRenderers)
        {
            EnsureIceFrame();
            iceInfluencesActive += active;
            iceInfluencesSelected += selected;
            iceInfluencesDiscarded += Mathf.Max(0, active - selected);
            iceTransitioningRenderers = Mathf.Max(
                iceTransitioningRenderers,
                transitioningRenderers);
        }

        public static PerformanceProfilerSnapshot GetSnapshot()
        {
            int frame = Time.frameCount;
            return new PerformanceProfilerSnapshot
            {
                VisibilityObjectsExamined = visibilityFrame == frame ? visibilityObjectsExamined : 0,
                VisibilityObjectsModified = visibilityFrame == frame ? visibilityObjectsModified : 0,
                VisibilityRaycasts = visibilityFrame == frame ? visibilityRaycasts : 0,
                LightCandidates = lightFrame == frame ? lightCandidates : 0,
                LightsRetained = lightFrame == frame ? lightsRetained : 0,
                ShadowedLightsRetained = lightFrame == frame ? shadowedLightsRetained : 0,
                PortalCamerasEvaluated = portalFrame == frame ? portalCamerasEvaluated : 0,
                PortalCamerasRendered = portalFrame == frame ? portalCamerasRendered : 0,
                IceRenderersExamined = iceFrame == frame ? iceRenderersExamined : 0,
                IceRenderersModified = iceFrame == frame ? iceRenderersModified : 0,
                IceInfluencesActive = iceFrame == frame ? iceInfluencesActive : 0,
                IceInfluencesSelected = iceFrame == frame ? iceInfluencesSelected : 0,
                IceInfluencesDiscarded = iceFrame == frame ? iceInfluencesDiscarded : 0,
                IceTransitioningRenderers = iceFrame == frame ? iceTransitioningRenderers : 0
            };
        }

        private static void EnsureIceFrame()
        {
            int frame = Time.frameCount;
            if (iceFrame == frame)
                return;

            iceFrame = frame;
            iceRenderersExamined = 0;
            iceRenderersModified = 0;
            iceInfluencesActive = 0;
            iceInfluencesSelected = 0;
            iceInfluencesDiscarded = 0;
            iceTransitioningRenderers = 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            visibilityFrame = -1;
            lightFrame = -1;
            portalFrame = -1;
            iceFrame = -1;
            visibilityObjectsExamined = 0;
            visibilityObjectsModified = 0;
            visibilityRaycasts = 0;
            lightCandidates = 0;
            lightsRetained = 0;
            shadowedLightsRetained = 0;
            portalCamerasEvaluated = 0;
            portalCamerasRendered = 0;
            iceRenderersExamined = 0;
            iceRenderersModified = 0;
            iceInfluencesActive = 0;
            iceInfluencesSelected = 0;
            iceInfluencesDiscarded = 0;
            iceTransitioningRenderers = 0;
        }
    }

    public struct PerformanceProfilerSnapshot
    {
        public int VisibilityObjectsExamined;
        public int VisibilityObjectsModified;
        public int VisibilityRaycasts;
        public int LightCandidates;
        public int LightsRetained;
        public int ShadowedLightsRetained;
        public int PortalCamerasEvaluated;
        public int PortalCamerasRendered;
        public int IceRenderersExamined;
        public int IceRenderersModified;
        public int IceInfluencesActive;
        public int IceInfluencesSelected;
        public int IceInfluencesDiscarded;
        public int IceTransitioningRenderers;
    }
}
