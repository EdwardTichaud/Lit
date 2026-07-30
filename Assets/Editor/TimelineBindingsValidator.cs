#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Lit.Timeline;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

namespace Lit.Editor
{
    public static class TimelineBindingsValidator
    {
        [MenuItem("Lit/Timeline/Validate Loaded Scene Bindings", priority = 100)]
        public static void ValidateLoadedSceneBindings()
        {
            List<string> errors = new List<string>();
            Dictionary<string, TimelineBindingTarget> targets =
                new Dictionary<string, TimelineBindingTarget>(StringComparer.OrdinalIgnoreCase);
            TimelineBindingTarget[] sceneTargets = UnityEngine.Object.FindObjectsByType<TimelineBindingTarget>(
                FindObjectsInactive.Include);

            for (int i = 0; i < sceneTargets.Length; i++)
            {
                TimelineBindingTarget target = sceneTargets[i];
                foreach (TimelineBindingTargetEntry entry in target.Targets)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.bindingId))
                    {
                        errors.Add($"{target.name}: cle de binding vide.");
                        continue;
                    }

                    if (entry.target == null)
                    {
                        errors.Add($"{target.name}: cible manquante pour '{entry.bindingId}'.");
                        continue;
                    }

                    if (!targets.TryAdd(entry.bindingId.Trim(), target))
                    {
                        errors.Add($"Cle de binding dupliquee '{entry.bindingId}'.");
                    }
                }
            }

            TimelineBindingProfile[] profiles = AssetDatabase.FindAssets("t:TimelineBindingProfile")
                .Select(guid => AssetDatabase.LoadAssetAtPath<TimelineBindingProfile>(AssetDatabase.GUIDToAssetPath(guid)))
                .ToArray();
            for (int i = 0; i < profiles.Length; i++)
            {
                ValidateProfile(profiles[i], targets, errors);
            }

            Report("Timeline bindings", errors);
        }

        [MenuItem("Lit/Timeline/Validate Bootstrap Configuration", priority = 101)]
        public static void ValidateBootstrapConfiguration()
        {
            List<string> errors = new List<string>();
            Lit.Timeline.TimelineManager[] managers = UnityEngine.Object.FindObjectsByType<Lit.Timeline.TimelineManager>(FindObjectsInactive.Include);
            if (managers.Length != 1)
            {
                errors.Add($"Un seul TimelineManager Bootstrap est requis (trouve : {managers.Length}).");
            }
            else if (managers[0].GetComponentInParent<ApplicationRoot>() == null)
            {
                errors.Add("TimelineManager doit etre enfant de ApplicationRoot.");
            }

            if (!SceneManager.GetSceneByName("Bootstrap").isLoaded)
            {
                errors.Add("Ouvrir la scene Bootstrap avant cette validation.");
            }

            Report("Timeline Bootstrap", errors);
        }

        private static void ValidateProfile(
            TimelineBindingProfile profile,
            Dictionary<string, TimelineBindingTarget> targets,
            List<string> errors)
        {
            if (profile == null || profile.Timeline == null)
            {
                errors.Add($"Profile '{(profile != null ? profile.name : "<null>")}' sans Timeline.");
                return;
            }

            TimelineAsset timeline = profile.Timeline as TimelineAsset;
            if (timeline == null)
            {
                errors.Add($"Profile '{profile.name}' ne reference pas un TimelineAsset.");
                return;
            }

            HashSet<TrackAsset> outputTracks = new HashSet<TrackAsset>();
            Dictionary<TrackAsset, Type> outputTypes = new Dictionary<TrackAsset, Type>();
            foreach (PlayableBinding output in timeline.outputs)
            {
                if (output.sourceObject is TrackAsset track)
                {
                    outputTracks.Add(track);
                    outputTypes[track] = output.outputTargetType;
                }
            }

            HashSet<TrackAsset> configuredTracks = new HashSet<TrackAsset>();
            foreach (TimelineBindingDefinition binding in profile.Bindings)
            {
                if (binding == null || binding.track == null)
                {
                    errors.Add($"Profile '{profile.name}': piste manquante.");
                    continue;
                }

                if (!configuredTracks.Add(binding.track))
                {
                    errors.Add($"Profile '{profile.name}': piste '{binding.track.name}' declaree plusieurs fois.");
                }

                if (!outputTracks.Contains(binding.track) && binding.track != timeline.markerTrack)
                {
                    errors.Add($"Profile '{profile.name}': piste '{binding.track.name}' absente de '{timeline.name}'.");
                }

                if (binding.required && (string.IsNullOrWhiteSpace(binding.bindingId) || !targets.ContainsKey(binding.bindingId.Trim())))
                {
                    errors.Add($"Profile '{profile.name}': cible requise '{binding.bindingId}' absente des scenes chargees.");
                }
                else if (!string.IsNullOrWhiteSpace(binding.bindingId) &&
                         targets.TryGetValue(binding.bindingId.Trim(), out TimelineBindingTarget target) &&
                         outputTypes.TryGetValue(binding.track, out Type expectedType))
                {
                    TimelineBindingTargetEntry targetEntry = target.Targets.FirstOrDefault(entry =>
                        entry != null && string.Equals(entry.bindingId, binding.bindingId.Trim(), StringComparison.OrdinalIgnoreCase));
                    if (targetEntry != null && targetEntry.target != null &&
                        expectedType != null && !expectedType.IsInstanceOfType(targetEntry.target))
                    {
                        errors.Add($"Profile '{profile.name}': '{binding.bindingId}' ({targetEntry.target.GetType().Name}) est incompatible avec '{binding.track.name}' ({expectedType.Name}).");
                    }
                }
            }
        }

        private static void Report(string title, List<string> errors)
        {
            if (errors.Count == 0)
            {
                Debug.Log($"{title}: validation reussie.");
                return;
            }

            for (int i = 0; i < errors.Count; i++)
            {
                Debug.LogError($"{title}: {errors[i]}");
            }

            throw new InvalidOperationException($"{title}: {errors.Count} erreur(s).");
        }
    }
}
#endif
