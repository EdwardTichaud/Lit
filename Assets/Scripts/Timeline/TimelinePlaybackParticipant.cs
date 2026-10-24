using UnityEngine.Playables;

namespace Lit.Timeline
{
    /// <summary>Permet a une cible de binding de suspendre puis restaurer son comportement pendant une Timeline.</summary>
    public interface ITimelinePlaybackParticipant
    {
        void OnTimelinePlaybackStarted(PlayableDirector director);
        void OnTimelinePlaybackFinished(PlayableDirector director);
    }

    /// <summary>Optional result-aware cleanup. Existing participants keep their
    /// original callback; presentation-only effects can retain a successful
    /// final pose while restoring themselves on cancellation or failure.</summary>
    public interface ITimelinePlaybackResultParticipant : ITimelinePlaybackParticipant
    {
        void OnTimelinePlaybackFinished(PlayableDirector director, TimelinePlaybackState result);
    }
}
