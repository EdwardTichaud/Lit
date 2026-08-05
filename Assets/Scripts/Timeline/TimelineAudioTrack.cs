using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Lit.Timeline
{
    [TrackColor(0.35f, 0.55f, 1f)]
    [TrackClipType(typeof(TimelineAudioClip))]
    public sealed class TimelineAudioTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            return ScriptPlayable<TimelineAudioMixer>.Create(graph, inputCount);
        }
    }

    public sealed class TimelineAudioMixer : PlayableBehaviour
    {
        private readonly Dictionary<TimelineAudioBehaviour, AudioSource> sources =
            new Dictionary<TimelineAudioBehaviour, AudioSource>();
        private readonly HashSet<TimelineAudioBehaviour> activeBehaviours =
            new HashSet<TimelineAudioBehaviour>();
        private bool musicOverridden;
        private bool ambienceOverridden;

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            AudioManager manager = AudioManager.Instance;
            if (manager == null)
            {
                return;
            }

            activeBehaviours.Clear();
            bool wantsMusicOverride = false;
            bool wantsAmbienceOverride = false;

            for (int i = 0; i < playable.GetInputCount(); i++)
            {
                float weight = playable.GetInputWeight(i);
                if (weight <= 0f)
                {
                    continue;
                }

                ScriptPlayable<TimelineAudioBehaviour> input =
                    (ScriptPlayable<TimelineAudioBehaviour>)playable.GetInput(i);
                TimelineAudioBehaviour behaviour = input.GetBehaviour();
                if (!behaviour.IsValid)
                {
                    continue;
                }

                AudioSource source = GetOrCreateSource(manager, behaviour, (float)input.GetTime());
                if (source == null)
                {
                    continue;
                }

                activeBehaviours.Add(behaviour);
                AudioManager.ApplyClipPitch(source, behaviour.audioClip);
                source.volume = GetChannelVolume(manager, behaviour) * Mathf.Clamp01(behaviour.volumeMultiplier) * weight;
                wantsMusicOverride |= behaviour.channel == TimelineAudioChannel.Music;
                wantsAmbienceOverride |= behaviour.channel == TimelineAudioChannel.Ambience;
            }

            SetChannelOverride(manager, TimelineAudioChannel.Music, wantsMusicOverride);
            SetChannelOverride(manager, TimelineAudioChannel.Ambience, wantsAmbienceOverride);
            StopInactiveSources(manager);
        }

        public override void OnPlayableDestroy(Playable playable)
        {
            AudioManager manager = AudioManager.Instance;
            if (manager != null)
            {
                foreach (AudioSource source in sources.Values)
                {
                    manager.StopTimelineClip(source);
                }

                SetChannelOverride(manager, TimelineAudioChannel.Music, false);
                SetChannelOverride(manager, TimelineAudioChannel.Ambience, false);
            }

            sources.Clear();
            activeBehaviours.Clear();
        }

        private AudioSource GetOrCreateSource(AudioManager manager, TimelineAudioBehaviour behaviour, float time)
        {
            if (sources.TryGetValue(behaviour, out AudioSource source) && source != null)
            {
                return source;
            }

            source = manager.PlayTimelineClip(behaviour.audioClip, time, behaviour.loop);
            if (source != null)
            {
                sources[behaviour] = source;
            }

            return source;
        }

        private void StopInactiveSources(AudioManager manager)
        {
            List<TimelineAudioBehaviour> toRemove = null;
            foreach (KeyValuePair<TimelineAudioBehaviour, AudioSource> pair in sources)
            {
                if (activeBehaviours.Contains(pair.Key))
                {
                    continue;
                }

                manager.StopTimelineClip(pair.Value);
                (toRemove ??= new List<TimelineAudioBehaviour>()).Add(pair.Key);
            }

            if (toRemove == null)
            {
                return;
            }

            for (int i = 0; i < toRemove.Count; i++)
            {
                sources.Remove(toRemove[i]);
            }
        }

        private float GetChannelVolume(AudioManager manager, TimelineAudioBehaviour behaviour)
        {
            switch (behaviour.channel)
            {
                case TimelineAudioChannel.Music:
                    return manager.GetTimelineMusicVolume(behaviour.audioClip);
                case TimelineAudioChannel.Ambience:
                    return manager.GetTimelineAmbienceVolume(behaviour.audioClip);
                default:
                    return manager.GetTimelineSfxVolume(behaviour.audioClip);
            }
        }

        private void SetChannelOverride(AudioManager manager, TimelineAudioChannel channel, bool active)
        {
            if (channel == TimelineAudioChannel.Music)
            {
                if (active && !musicOverridden)
                {
                    manager.BeginMusicDucking(0f);
                    musicOverridden = true;
                }
                else if (!active && musicOverridden)
                {
                    manager.EndMusicDucking();
                    musicOverridden = false;
                }

                return;
            }

            if (active && !ambienceOverridden)
            {
                manager.BeginAmbienceDucking(0f);
                ambienceOverridden = true;
            }
            else if (!active && ambienceOverridden)
            {
                manager.EndAmbienceDucking();
                ambienceOverridden = false;
            }
        }
    }
}
