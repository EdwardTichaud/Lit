using System.Collections;
using System.Linq;
using Lit.Timeline;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using TimelineManager = Lit.Timeline.TimelineManager;

public sealed partial class CycleRuntimeSmoke
{
    private IEnumerator RunTimelineFixture()
    {
        var source = Resources.Load<GameObject>("CycleTimelineFixture");
        var profile = Resources.Load<TimelineBindingProfile>("CycleTimelineFixtureProfile");
        if (source == null || profile == null) { Finish(false, "Missing standalone Timeline fixture assets."); yield break; }
        var services = new GameObject("Timeline fixture application root"); services.AddComponent<ApplicationRoot>();
        var timelineManager = services.AddComponent<TimelineManager>();
        var actor = Instantiate(source);
        var director = actor.GetComponent<PlayableDirector>();
        var effect = actor.GetComponentInChildren<TimelineDissolveTarget>();
        var visual = actor.GetComponentInChildren<MeshRenderer>();
        if (scenario == "timeline_missing")
            director.ClearGenericBinding(((TimelineAsset)director.playableAsset).GetOutputTracks().OfType<AnimationTrack>().Single());
        var playback = timelineManager.Play(director, profile);
        evidence.Add("Real player Timeline started: " + playback.State);
        if (scenario == "timeline_missing")
        {
            Finish(playback.State == TimelinePlaybackState.Failed && visual.enabled,
                "An unbound active track must fail before any presentation or completion.");
            yield break;
        }
        if (playback.State != TimelinePlaybackState.Playing) { Finish(false, "PlayableDirector did not start."); yield break; }
        bool animated = false, interrupted = false, paused = false;
        float begin = Time.realtimeSinceStartup;
        int frames = 0, notifications = 0;
        playback.Finished += _ => notifications++;
        while (!playback.IsDone && Time.realtimeSinceStartup - begin < 5f)
        {
            frames++;
            if (effect.strength > .1f && effect.strength < .9f)
            {
                animated = true;
                if (!interrupted && scenario == "timeline_stop") { interrupted = true; playback.Stop(); }
                if (!interrupted && scenario == "timeline_service") { interrupted = true; timelineManager.enabled = false; }
                if (!interrupted && scenario == "timeline_external_stop") { interrupted = true; director.Stop(); }
                if (!paused && scenario == "timeline_pause")
                {
                    paused = true; director.Pause();
                    float heldStrength = effect.strength;
                    yield return new WaitForSecondsRealtime(1.2f);
                    if (playback.IsDone || Mathf.Abs(effect.strength - heldStrength) > .001f)
                    { Finish(false, "A paused graph completed from elapsed wall time or kept animating."); yield break; }
                    evidence.Add("Pause longer than the authored duration: no completion and no advancing animation.");
                    director.Resume();
                }
            }
            yield return null;
        }
        evidence.Add("Frames=" + frames + " animated=" + animated + " final=" + playback.State + " notifications=" + notifications);
        if (!animated || frames < 2 || notifications != 1) { Finish(false, "Native animation did not advance or completion was not unique."); yield break; }
        if (director.extrapolationMode != DirectorWrapMode.None) { Finish(false, "Authored wrap mode was not restored."); yield break; }
        if (scenario == "timeline" || scenario == "timeline_pause")
        {
            Finish(playback.State == TimelinePlaybackState.Completed && !visual.enabled,
                "Natural Director end completes once and retains the final invisible presentation.");
        }
        else
        {
            yield return new WaitForSecondsRealtime(.1f);
            var expected = scenario == "timeline_service" ? TimelinePlaybackState.Failed : TimelinePlaybackState.Stopped;
            Finish(interrupted && playback.State == expected && visual.enabled && effect.strength == 0,
                "Interrupted Director returns its visual state and cannot validate natural completion.");
        }
    }
}
