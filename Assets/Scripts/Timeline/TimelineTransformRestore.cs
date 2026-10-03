using UnityEngine;
using UnityEngine.Playables;

namespace Lit.Timeline
{
    /// <summary>Restores an animated scene prop after playback, including cancellation.</summary>
    [DisallowMultipleComponent]
    public sealed class TimelineTransformRestore : MonoBehaviour, ITimelinePlaybackParticipant
    {
        private Vector3 position, scale;
        private Quaternion rotation;
        private bool captured;

        public void OnTimelinePlaybackStarted(PlayableDirector director)
        {
            if (captured) return;
            position = transform.localPosition;
            rotation = transform.localRotation;
            scale = transform.localScale;
            captured = true;
        }

        public void OnTimelinePlaybackFinished(PlayableDirector director) => Restore();
        private void OnDisable() => Restore();
        private void Restore()
        {
            if (!captured) return;
            transform.localPosition = position;
            transform.localRotation = rotation;
            transform.localScale = scale;
            captured = false;
        }
    }
}
