using Opsive.UltimateCharacterController.Character;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Lit.Timeline
{
    /// <summary>
    /// Explicit cinematic movement for a character. Unlike root motion, it has
    /// the same authored start/end points in Timeline preview and at runtime.
    /// </summary>
    [TrackColor(0.25f, 0.8f, 0.45f)]
    [TrackClipType(typeof(TimelinePlayerMoveClip))]
    [TrackBindingType(typeof(Transform))]
    public sealed class TimelinePlayerMoveTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            return ScriptPlayable<TimelinePlayerMoveMixer>.Create(graph, inputCount);
        }
    }

    public sealed class TimelinePlayerMoveMixer : PlayableBehaviour
    {
        private UltimateCharacterLocomotion overriddenLocomotion;
        private bool rootMotionWasEnabled;

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            if (!(playerData is Transform target))
            {
                return;
            }

            float totalWeight = 0f;
            Vector3 position = Vector3.zero;
            Quaternion rotation = Quaternion.identity;
            bool hasRotation = false;
            bool disableRootMotion = false;

            for (int i = 0; i < playable.GetInputCount(); i++)
            {
                float weight = playable.GetInputWeight(i);
                if (weight <= 0f)
                {
                    continue;
                }

                ScriptPlayable<TimelinePlayerMoveBehaviour> input =
                    (ScriptPlayable<TimelinePlayerMoveBehaviour>)playable.GetInput(i);

                TimelinePlayerMoveBehaviour behaviour = input.GetBehaviour();
                if (!behaviour.TryEvaluate(input.GetTime(), input.GetDuration(), out Vector3 clipPosition, out Quaternion clipRotation))
                {
                    continue;
                }

                position += clipPosition * weight;
                if (behaviour.matchEndRotation)
                {
                    rotation = hasRotation ? Quaternion.Slerp(rotation, clipRotation, weight / (totalWeight + weight)) : clipRotation;
                    hasRotation = true;
                }

                disableRootMotion |= behaviour.disableUccRootMotion;
                totalWeight += weight;
            }

            if (totalWeight <= 0f)
            {
                RestoreRootMotion();
                return;
            }

            position /= totalWeight;
            Apply(target, position, hasRotation ? rotation : target.rotation, disableRootMotion);
        }

        public override void OnPlayableDestroy(Playable playable)
        {
            RestoreRootMotion();
        }

        private void Apply(Transform target, Vector3 position, Quaternion rotation, bool disableRootMotion)
        {
            if (Application.isPlaying && target.TryGetComponent(out UltimateCharacterLocomotion locomotion))
            {
                if (disableRootMotion && overriddenLocomotion == null)
                {
                    overriddenLocomotion = locomotion;
                    rootMotionWasEnabled = locomotion.UseRootMotionPosition;
                    locomotion.UseRootMotionPosition = false;
                }

                locomotion.SetPositionAndRotation(position, rotation, snapAnimator: false, stopAllAbilities: false);
                return;
            }

            target.SetPositionAndRotation(position, rotation);
        }

        private void RestoreRootMotion()
        {
            if (overriddenLocomotion != null)
            {
                overriddenLocomotion.UseRootMotionPosition = rootMotionWasEnabled;
                overriddenLocomotion = null;
            }
        }
    }

}
