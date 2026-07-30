using UnityEngine.Playables;

namespace Lit.Timeline
{
    /// <summary>Permet a une cible de binding de suspendre puis restaurer son comportement pendant une Timeline.</summary>
    public interface ITimelinePlaybackParticipant
    {
        void OnTimelinePlaybackStarted(PlayableDirector director);
        void OnTimelinePlaybackFinished(PlayableDirector director);
    }
}
