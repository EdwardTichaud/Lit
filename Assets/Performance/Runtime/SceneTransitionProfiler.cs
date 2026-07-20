using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

namespace Lit.Performance
{
    /// <summary>
    /// Telemetrie legere pour comparer les transitions de scene. Les donnees
    /// sont visibles dans le Profiler via les marqueurs et resumees dans la
    /// Console a la fin de chaque transition, uniquement dans l'editeur et
    /// les builds de developpement.
    /// </summary>
    public static class SceneTransitionProfiler
    {
        public static readonly ProfilerMarker TransitionPulse =
            new ProfilerMarker(ProfilerCategory.Scripts, "Lit.SceneFlow.Transition.Pulse");
        public static readonly ProfilerMarker OverlayPresentation =
            new ProfilerMarker(ProfilerCategory.Scripts, "Lit.SceneFlow.Overlay.Presentation");
        public static readonly ProfilerMarker SceneLoadRequest =
            new ProfilerMarker(ProfilerCategory.Loading, "Lit.SceneFlow.Scene.LoadRequest");
        public static readonly ProfilerMarker SceneActivation =
            new ProfilerMarker(ProfilerCategory.Loading, "Lit.SceneFlow.Scene.Activation");
        public static readonly ProfilerMarker SceneUnloadRequest =
            new ProfilerMarker(ProfilerCategory.Loading, "Lit.SceneFlow.Scene.UnloadRequest");
        public static readonly ProfilerMarker SquadPlacement =
            new ProfilerMarker(ProfilerCategory.Scripts, "Lit.SceneFlow.Squad.PlaceAtSpawn");

        private static readonly List<Phase> phases = new List<Phase>();
        private static string transitionName;
        private static double transitionStartedAt;
        private static double lastPulseAt;
        private static double longestFrameGap;
        private static bool isRunning;

        public static bool IsEnabled
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return true;
#else
                return false;
#endif
            }
        }

        public static void Begin(string name)
        {
            if (!IsEnabled)
            {
                return;
            }

            transitionName = string.IsNullOrWhiteSpace(name) ? "Unknown" : name;
            transitionStartedAt = Time.realtimeSinceStartupAsDouble;
            lastPulseAt = transitionStartedAt;
            longestFrameGap = 0d;
            phases.Clear();
            isRunning = true;
            Mark("Debut");
        }

        public static void Mark(string phaseName)
        {
            if (!isRunning)
            {
                return;
            }

            phases.Add(new Phase
            {
                name = phaseName,
                timestamp = Time.realtimeSinceStartupAsDouble
            });
        }

        /// <summary>
        /// A appeler une fois par reprise de coroutine. Un ecart anormalement
        /// eleve correspond a une frame bloquee, meme si Unity etait occupe
        /// dans son pipeline de chargement entre deux reprises.
        /// </summary>
        public static void Pulse()
        {
            if (!isRunning)
            {
                return;
            }

            using (TransitionPulse.Auto())
            {
                double now = Time.realtimeSinceStartupAsDouble;
                longestFrameGap = Math.Max(longestFrameGap, now - lastPulseAt);
                lastPulseAt = now;
            }
        }

        /// <summary>
        /// Reprend la mesure des frames apres une attente volontaire (fondu,
        /// delai de confort de l'ecran de chargement). Cette attente ne doit
        /// pas etre comptabilisee comme un freeze.
        /// </summary>
        public static void ResetFrameGapMeasurement()
        {
            if (!isRunning)
            {
                return;
            }

            lastPulseAt = Time.realtimeSinceStartupAsDouble;
        }

        public static void End(string finalPhaseName = "Joueur pret")
        {
            if (!isRunning)
            {
                return;
            }

            Mark(finalPhaseName);
            Pulse();

            double endedAt = Time.realtimeSinceStartupAsDouble;
            string phaseSummary = BuildPhaseSummary(endedAt);
            Debug.Log(
                $"[SceneTransition] {transitionName} | total {endedAt - transitionStartedAt:0.000}s | " +
                $"plus longue frame {longestFrameGap * 1000d:0.0} ms | {phaseSummary}");

            phases.Clear();
            isRunning = false;
            transitionName = null;
        }

        public static void Cancel()
        {
            phases.Clear();
            isRunning = false;
            transitionName = null;
        }

        private static string BuildPhaseSummary(double endedAt)
        {
            if (phases.Count <= 1)
            {
                return "phases indisponibles";
            }

            List<string> entries = new List<string>(phases.Count - 1);
            for (int i = 1; i < phases.Count; i++)
            {
                double previous = phases[i - 1].timestamp;
                entries.Add($"{phases[i].name} {phases[i].timestamp - previous:0.000}s");
            }

            if (phases[phases.Count - 1].timestamp < endedAt)
            {
                entries.Add($"fin {endedAt - phases[phases.Count - 1].timestamp:0.000}s");
            }

            return string.Join(" | ", entries);
        }

        private struct Phase
        {
            public string name;
            public double timestamp;
        }
    }
}
