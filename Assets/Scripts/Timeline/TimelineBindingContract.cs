using System.Collections.Generic;
using System.Linq;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Lit.Timeline
{
    /// <summary>Shared authoring/runtime coverage check. Stable optional bindings
    /// remain explicit; compatible Director bindings need no redundant profile entry.</summary>
    public static class TimelineBindingContract
    {
        public static List<string> CollectCoverageIssues(PlayableDirector director, TimelineBindingProfile profile)
        {
            var errors = new List<string>();
            if (director == null || profile == null || !profile.Matches(director.playableAsset) ||
                !(director.playableAsset is TimelineAsset timeline))
            {
                errors.Add("Director, Timeline ou profil absent/incompatible.");
                return errors;
            }
            foreach (var output in timeline.outputs)
            {
                if (!(output.sourceObject is TrackAsset track) || track.mutedInHierarchy ||
                    output.outputTargetType == null || output.outputTargetType == typeof(void)) continue;
                bool hasContent = track.GetClips().Any() || track.GetMarkerCount() > 0 ||
                    track is AnimationTrack animation && animation.infiniteClip != null;
                if (!hasContent) continue;
                if (profile.TryGetBinding(track, out var binding))
                {
                    if (string.IsNullOrWhiteSpace(binding.bindingId))
                        errors.Add($"{director.name}: cle de binding vide pour '{track.name}'.");
                    continue;
                }
                var nativeTarget = director.GetGenericBinding(track);
                if (nativeTarget == null || !output.outputTargetType.IsInstanceOfType(nativeTarget))
                    errors.Add($"{director.name}: piste active '{track.name}' sans cible compatible ni binding declare dans '{profile.name}'.");
            }
            return errors;
        }
    }
}
