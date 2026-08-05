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
        [Tooltip("Conserve la position reelle du joueur au debut. A→B devient un deplacement relatif et ne le teleporte jamais sur A.")]
        public bool preserveRuntimeStartPosition = true;
        [Tooltip("Conserve l'orientation reelle du joueur au debut, puis applique la rotation relative de A vers B.")]
        public bool preserveRuntimeStartRotation = true;
        public bool matchEndRotation = true;
        public bool disableUccRootMotion = true;
        [Tooltip("Projette le trajet sur le sol. La hauteur des points A/B est alors ignoree.")]
        public bool conformToGround = true;
        [Tooltip("Calques consideres comme sol pour la projection verticale.")]
        public LayerMask groundLayers = ~0;
        [Min(0.01f), Tooltip("Hauteur de depart du rayon de sol.")]
        public float groundRaycastHeight = 10f;
        [Min(0.01f), Tooltip("Portee maximale du rayon de sol.")]
        public float groundRaycastDistance = 50f;
        public AnimationCurve progress = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<TimelinePlayerMoveBehaviour>.Create(graph);
            TimelinePlayerMoveBehaviour behaviour = playable.GetBehaviour();
            IExposedPropertyTable resolver = graph.GetResolver();
            behaviour.startPoint = startPoint.Resolve(resolver);
            behaviour.endPoint = endPoint.Resolve(resolver);
            behaviour.preserveRuntimeStartPosition = preserveRuntimeStartPosition;
            behaviour.preserveRuntimeStartRotation = preserveRuntimeStartRotation;
            behaviour.matchEndRotation = matchEndRotation;
            behaviour.disableUccRootMotion = disableUccRootMotion;
            behaviour.conformToGround = conformToGround;
            behaviour.groundLayers = groundLayers;
            behaviour.groundRaycastHeight = groundRaycastHeight;
            behaviour.groundRaycastDistance = groundRaycastDistance;
            behaviour.progress = progress;
            return playable;
        }
    }

    public sealed class TimelinePlayerMoveBehaviour : PlayableBehaviour
    {
        internal Transform startPoint;
        internal Transform endPoint;
        internal bool preserveRuntimeStartPosition;
        internal bool preserveRuntimeStartRotation;
        internal bool matchEndRotation;
        internal bool disableUccRootMotion;
        internal bool conformToGround;
        internal LayerMask groundLayers;
        internal float groundRaycastHeight;
        internal float groundRaycastDistance;
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
