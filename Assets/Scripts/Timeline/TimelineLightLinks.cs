using System;
using UnityEngine;
using UnityEngine.Playables;

namespace Lit.Timeline
{
    /// <summary>Editable light links driven exclusively by an Animation track.
    /// Endpoints follow authored props, not baked world coordinates.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class TimelineLightLinks : MonoBehaviour, ITimelinePlaybackParticipant
    {
        [SerializeField] private Transform[] sources = Array.Empty<Transform>();
        [SerializeField] private Transform target;
        [SerializeField] private LineRenderer[] lines = Array.Empty<LineRenderer>();
        [SerializeField] private Vector3 sourceOffset = Vector3.up;
        [SerializeField] private Vector3 targetOffset = Vector3.up;
        [SerializeField, Min(.001f)] private float width = .08f;
        [Range(0, 1)] public float strength;
        public void OnTimelinePlaybackStarted(PlayableDirector director) { strength = 0; Apply(); }
        public void OnTimelinePlaybackFinished(PlayableDirector director) { strength = 0; Apply(); }
        private void OnDisable() { strength = 0; Apply(); }
        private void OnDidApplyAnimationProperties() => Apply();
        private void LateUpdate() => Apply();
        private void Apply()
        {
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line == null) continue;
                bool visible = isActiveAndEnabled && strength > .001f && target != null && i < sources.Length && sources[i] != null;
                line.enabled = visible;
                if (!visible) continue;
                line.useWorldSpace = true; line.positionCount = 2;
                line.widthMultiplier = width * Mathf.Clamp01(strength);
                line.SetPosition(0, sources[i].position + sourceOffset);
                line.SetPosition(1, target.position + targetOffset);
            }
        }
    }
}
