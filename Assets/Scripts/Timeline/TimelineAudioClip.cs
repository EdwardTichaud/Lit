using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Lit.Timeline
{
    public enum TimelineAudioChannel
    {
        Sfx,
        Music,
        Ambience
    }

    /// <summary>
    /// Plays one AudioClipSO on a TimelineAudioTrack. Timeline blend handles
    /// the fades: overlap two clips directly in Timeline for a crossfade.
    /// </summary>
    [System.Serializable]
    public sealed class TimelineAudioClip : PlayableAsset, ITimelineClipAsset
    {
        public AudioClipSO audioClip;
        public TimelineAudioChannel channel = TimelineAudioChannel.Sfx;
        [Range(0f, 1f)] public float volumeMultiplier = 1f;
        [Tooltip("Force une lecture en boucle, independamment du reglage de l'asset.")]
        public bool overrideLoop;
        public bool loop;

        public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.ClipIn | ClipCaps.SpeedMultiplier;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            ScriptPlayable<TimelineAudioBehaviour> playable = ScriptPlayable<TimelineAudioBehaviour>.Create(graph);
            TimelineAudioBehaviour behaviour = playable.GetBehaviour();
            behaviour.audioClip = audioClip;
            behaviour.channel = channel;
            behaviour.volumeMultiplier = volumeMultiplier;
            behaviour.loop = overrideLoop ? loop : audioClip != null && audioClip.loop;
            return playable;
        }
    }

    public sealed class TimelineAudioBehaviour : PlayableBehaviour
    {
        internal AudioClipSO audioClip;
        internal TimelineAudioChannel channel;
        internal float volumeMultiplier;
        internal bool loop;

        internal bool IsValid => audioClip != null && audioClip.audioClip != null;
    }
}
