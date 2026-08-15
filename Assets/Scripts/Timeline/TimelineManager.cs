using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

namespace Lit.Timeline
{
    [DefaultExecutionOrder(-9000)]
    [DisallowMultipleComponent]
    public sealed class TimelineManager : MonoBehaviour
    {
        private sealed class ActivePlayback
        {
            public PlayableDirector director;
            public TimelineBindingProfile profile;
            public TimelinePlaybackHandle handle;
            public Coroutine waitRoutine;
            public readonly List<ITimelinePlaybackParticipant> participants = new List<ITimelinePlaybackParticipant>();
        }

        private readonly Dictionary<string, UnityEngine.Object> registeredTargets =
            new Dictionary<string, UnityEngine.Object>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<TimelineBindingTarget, List<string>> targetKeys =
            new Dictionary<TimelineBindingTarget, List<string>>();
        private readonly Dictionary<PlayableDirector, ActivePlayback> activePlaybacks =
            new Dictionary<PlayableDirector, ActivePlayback>();

        public static TimelineManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("Un seul Lit.Timeline.TimelineManager est autorise. Le manager duplique est desactive.", this);
                enabled = false;
                return;
            }

            if (GetComponentInParent<ApplicationRoot>() == null)
            {
                Debug.LogError("TimelineManager doit etre place sous ApplicationRoot dans la scene Bootstrap.", this);
                enabled = false;
                return;
            }

            Instance = this;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void Register(TimelineBindingTarget target)
        {
            if (target == null || targetKeys.ContainsKey(target))
            {
                return;
            }

            List<string> keys = new List<string>();
            IReadOnlyList<TimelineBindingTargetEntry> entries = target.Targets;
            for (int i = 0; i < entries.Count; i++)
            {
                TimelineBindingTargetEntry entry = entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.bindingId) || entry.target == null)
                {
                    continue;
                }

                string key = entry.bindingId.Trim();
                if (registeredTargets.ContainsKey(key))
                {
                    Debug.LogError($"Timeline binding duplique pour la cle '{key}'. Cible '{target.name}' ignoree.", target);
                    continue;
                }

                registeredTargets.Add(key, entry.target);
                keys.Add(key);
            }

            targetKeys.Add(target, keys);
        }

        public void Unregister(TimelineBindingTarget target)
        {
            if (target == null || !targetKeys.TryGetValue(target, out List<string> keys))
            {
                return;
            }

            for (int i = 0; i < keys.Count; i++)
            {
                registeredTargets.Remove(keys[i]);
            }

            targetKeys.Remove(target);
        }

        public TimelinePlaybackHandle Play(
            PlayableDirector director,
            TimelineBindingProfile profile,
            TimelineBindingContext context = null,
            TimelinePlaybackOptions options = default)
        {
            TimelinePlaybackHandle handle = new TimelinePlaybackHandle(this);
            if (!enabled || director == null || profile == null)
            {
                handle.Finish(TimelinePlaybackState.Failed, "PlayableDirector ou TimelineBindingProfile manquant.");
                return handle;
            }

            if (director.gameObject.scene == gameObject.scene)
            {
                handle.Finish(TimelinePlaybackState.Failed, "Le PlayableDirector doit appartenir a une scene de contenu, pas a Bootstrap.");
                return handle;
            }

            if (!profile.Matches(director.playableAsset))
            {
                handle.Finish(TimelinePlaybackState.Failed, "Le profile ne correspond pas a la Timeline assignee au PlayableDirector.");
                return handle;
            }

            if (activePlaybacks.ContainsKey(director))
            {
                handle.Finish(TimelinePlaybackState.Failed, "Ce PlayableDirector est deja pilote par TimelineManager.");
                return handle;
            }

            ActivePlayback playback = new ActivePlayback { director = director, profile = profile, handle = handle };
            activePlaybacks.Add(director, playback);
            if (options.waitForRequiredBindings)
            {
                playback.waitRoutine = StartCoroutine(WaitAndPlay(playback, context, options.requiredBindingsTimeout));
            }
            else
            {
                if (!TryPrepare(playback, context, logFailures: true, startPlayback: true) && !handle.IsDone)
                {
                    FinishPlayback(playback, TimelinePlaybackState.Failed, "Bindings requis introuvables ou incompatibles.");
                }
            }

            return handle;
        }

        /// <summary>Valide et applique les bindings sans lancer le PlayableDirector.</summary>
        public bool Prepare(
            PlayableDirector director,
            TimelineBindingProfile profile,
            TimelineBindingContext context = null)
        {
            if (!enabled || director == null || profile == null || !profile.Matches(director.playableAsset))
            {
                return false;
            }

            ActivePlayback preparation = new ActivePlayback
            {
                director = director,
                profile = profile,
                handle = new TimelinePlaybackHandle(this)
            };
            return TryPrepare(preparation, context, logFailures: true, startPlayback: false);
        }

        public void Stop(TimelinePlaybackHandle handle, bool skip)
        {
            foreach (KeyValuePair<PlayableDirector, ActivePlayback> pair in activePlaybacks)
            {
                if (pair.Value.handle != handle)
                {
                    continue;
                }

                PlayableDirector director = pair.Key;
                if (skip && director != null && director.duration > 0d && !double.IsInfinity(director.duration))
                {
                    director.time = director.duration;
                    director.Evaluate();
                }

                FinishPlayback(pair.Value, TimelinePlaybackState.Stopped);
                return;
            }
        }

        private IEnumerator WaitAndPlay(ActivePlayback playback, TimelineBindingContext context, float timeout)
        {
            float startedAt = Time.unscaledTime;
            while (!TryPrepare(playback, context, logFailures: false, startPlayback: true))
            {
                if (playback.handle.IsDone)
                {
                    yield break;
                }

                if (timeout > 0f && Time.unscaledTime - startedAt >= timeout)
                {
                    TryPrepare(playback, context, logFailures: true, startPlayback: true);
                    if (!playback.handle.IsDone)
                    {
                        FinishPlayback(playback, TimelinePlaybackState.Failed, "Delai d'attente des bindings requis depasse.");
                    }
                    yield break;
                }

                yield return null;
            }
        }

        private bool TryPrepare(
            ActivePlayback playback,
            TimelineBindingContext context,
            bool logFailures,
            bool startPlayback)
        {
            if (playback.director == null || !playback.director.gameObject.scene.isLoaded)
            {
                FinishPlayback(playback, TimelinePlaybackState.Failed, "PlayableDirector detruit ou scene dechargee.");
                return false;
            }

            TimelineAsset timeline = playback.director.playableAsset as TimelineAsset;
            if (timeline == null)
            {
                FinishPlayback(playback, TimelinePlaybackState.Failed, "La Timeline doit etre un TimelineAsset.");
                return false;
            }

            HashSet<TrackAsset> outputTracks = new HashSet<TrackAsset>();
            foreach (PlayableBinding output in timeline.outputs)
            {
                if (output.sourceObject is TrackAsset track)
                {
                    outputTracks.Add(track);
                }
            }

            for (int i = 0; i < playback.profile.Bindings.Count; i++)
            {
                TimelineBindingDefinition definition = playback.profile.Bindings[i];
                if (definition == null || definition.track == null ||
                    (!outputTracks.Contains(definition.track) && definition.track != timeline.markerTrack))
                {
                    if (logFailures)
                    {
                        Debug.LogError($"Timeline '{timeline.name}': le profile '{playback.profile.name}' reference une piste absente.", playback.director);
                    }
                    return false;
                }
            }

            foreach (PlayableBinding output in timeline.outputs)
            {
                if (!playback.profile.TryGetBinding(output.sourceObject, out TimelineBindingDefinition definition))
                {
                    continue;
                }

                if (TryResolve(definition.bindingId, context, out UnityEngine.Object target) &&
                    IsCompatible(output.outputTargetType, target))
                {
                    playback.director.SetGenericBinding(output.sourceObject, target);
                    RegisterParticipant(playback, target);
                    continue;
                }

                if (definition.required)
                {
                    if (logFailures)
                    {
                        string targetName = target != null ? target.GetType().Name : "absente";
                        Debug.LogError($"Timeline '{timeline.name}': binding requis '{definition.bindingId}' invalide pour la piste '{output.streamName}' ({targetName}).", playback.director);
                    }
                    return false;
                }

                if (logFailures)
                {
                    Debug.LogWarning($"Timeline '{timeline.name}': binding facultatif '{definition.bindingId}' absent pour '{output.streamName}'.", playback.director);
                }

                playback.director.ClearGenericBinding(output.sourceObject);
            }

            if (timeline.markerTrack != null &&
                playback.profile.TryGetBinding(timeline.markerTrack, out TimelineBindingDefinition markerDefinition))
            {
                if (TryResolve(markerDefinition.bindingId, context, out UnityEngine.Object markerTarget))
                {
                    playback.director.SetGenericBinding(timeline.markerTrack, markerTarget);
                    RegisterParticipant(playback, markerTarget);
                }
                else if (markerDefinition.required)
                {
                    if (logFailures)
                    {
                        Debug.LogError($"Timeline '{timeline.name}': binding requis '{markerDefinition.bindingId}' absent pour les marqueurs.", playback.director);
                    }
                    return false;
                }
                else
                {
                    playback.director.ClearGenericBinding(timeline.markerTrack);
                }
            }

            if (!startPlayback)
            {
                return true;
            }

            for (int i = 0; i < playback.participants.Count; i++)
            {
                playback.participants[i].OnTimelinePlaybackStarted(playback.director);
            }

            playback.director.stopped += OnDirectorStopped;
            playback.director.time = 0d;
            playback.director.Play();
            playback.handle.State = TimelinePlaybackState.Playing;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log(
                $"TimelineManager: lecture de '{timeline.name}' demarree " +
                $"(duree {playback.director.duration:0.###} s, director '{playback.director.name}').",
                playback.director);
#endif
            return true;
        }

        private bool TryResolve(string bindingId, TimelineBindingContext context, out UnityEngine.Object target)
        {
            target = null;
            if (!string.IsNullOrWhiteSpace(bindingId) && context != null && context.TryResolve(bindingId.Trim(), out target))
            {
                return target != null;
            }

            return !string.IsNullOrWhiteSpace(bindingId) && registeredTargets.TryGetValue(bindingId.Trim(), out target) && target != null;
        }

        private static bool IsCompatible(Type expectedType, UnityEngine.Object target)
        {
            return target != null && (expectedType == null || expectedType.IsInstanceOfType(target));
        }

        private static void RegisterParticipant(ActivePlayback playback, UnityEngine.Object target)
        {
            Component component = target as Component;
            if (component == null)
            {
                return;
            }

            MonoBehaviour[] behaviours = component.GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is ITimelinePlaybackParticipant participant &&
                    !playback.participants.Contains(participant))
                {
                    playback.participants.Add(participant);
                }
            }
        }

        private void OnDirectorStopped(PlayableDirector director)
        {
            if (director != null && activePlaybacks.TryGetValue(director, out ActivePlayback playback))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log(
                    $"TimelineManager: lecture de '{director.playableAsset?.name}' terminee a {director.time:0.###} s.",
                    director);
#endif
                FinishPlayback(playback, TimelinePlaybackState.Completed);
            }
        }

        private void OnSceneUnloaded(Scene scene)
        {
            List<ActivePlayback> interrupted = new List<ActivePlayback>();
            foreach (ActivePlayback playback in activePlaybacks.Values)
            {
                if (playback.director == null || playback.director.gameObject.scene == scene)
                {
                    interrupted.Add(playback);
                }
            }

            for (int i = 0; i < interrupted.Count; i++)
            {
                FinishPlayback(interrupted[i], TimelinePlaybackState.Failed, "Scene du PlayableDirector dechargee.");
            }
        }

        private void FinishPlayback(ActivePlayback playback, TimelinePlaybackState state, string reason = null)
        {
            if (playback == null || playback.handle.IsDone)
            {
                return;
            }

            if (playback.waitRoutine != null)
            {
                StopCoroutine(playback.waitRoutine);
            }

            if (playback.director != null)
            {
                playback.director.stopped -= OnDirectorStopped;
                // A director which reached DirectorWrapMode.None is already
                // stopped, but its graph may still retain Timeline playables.
                // Stopping explicitly releases custom tracks so they can
                // restore and commit their final gameplay pose before input
                // is returned to UCC.
                playback.director.Stop();
            }

            for (int i = playback.participants.Count - 1; i >= 0; i--)
            {
                playback.participants[i].OnTimelinePlaybackFinished(playback.director);
            }
            playback.participants.Clear();

            activePlaybacks.Remove(playback.director);
            playback.handle.Finish(state, reason);
        }
    }
}
