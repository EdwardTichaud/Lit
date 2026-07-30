using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Lit.Timeline
{
    /// <summary>Timeline clip that moves its bound character between two scene points.</summary>
    [System.Serializable]
    public sealed class TimelinePlayerMoveClip : PlayableAsset, ITimelineClipAsset
    {
        public ExposedReference<Transform> startPoint;
        public ExposedReference<Transform> endPoint;
        public bool matchEndRotation = true;
        public bool disableUccRootMotion = true;
        public AnimationCurve progress = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<TimelinePlayerMoveBehaviour>.Create(graph);
            TimelinePlayerMoveBehaviour behaviour = playable.GetBehaviour();
            IExposedPropertyTable resolver = graph.GetResolver();
            behaviour.startPoint = startPoint.Resolve(resolver);
            behaviour.endPoint = endPoint.Resolve(resolver);
            behaviour.matchEndRotation = matchEndRotation;
            behaviour.disableUccRootMotion = disableUccRootMotion;
            behaviour.progress = progress;
            return playable;
        }
    }

    public sealed class TimelinePlayerMoveBehaviour : PlayableBehaviour
    {
        internal Transform startPoint;
        internal Transform endPoint;
        internal bool matchEndRotation;
        internal bool disableUccRootMotion;
        internal AnimationCurve progress;

        internal bool TryEvaluate(double time, double duration, out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = Quaternion.identity;
            if (startPoint == null || endPoint == null || duration <= 0d)
            {
                return false;
            }

            float normalizedTime = Mathf.Clamp01((float)(time / duration));
            float t = progress != null ? Mathf.Clamp01(progress.Evaluate(normalizedTime)) : normalizedTime;
            position = Vector3.LerpUnclamped(startPoint.position, endPoint.position, t);
            rotation = Quaternion.Slerp(startPoint.rotation, endPoint.rotation, t);
            return true;
        }
    }
}
