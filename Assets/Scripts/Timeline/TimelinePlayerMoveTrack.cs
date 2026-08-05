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
        private Transform lastTarget;
        private Vector3 lastPosition;
        private Quaternion lastRotation;
        private bool hasLastAppliedPose;
        private bool runtimeStartCaptured;
        private Vector3 runtimeStartPositionOffset;
        private Quaternion runtimeStartRotationOffset = Quaternion.identity;

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
            bool preserveRuntimeRotation = false;

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

                CaptureRuntimeStart(
                    target,
                    behaviour.startPoint,
                    behaviour.preserveRuntimeStartPosition,
                    behaviour.preserveRuntimeStartRotation);
                if (behaviour.preserveRuntimeStartPosition && runtimeStartCaptured)
                {
                    clipPosition += runtimeStartPositionOffset;
                }

                if (behaviour.conformToGround)
                {
                    clipPosition = ProjectToGround(target, clipPosition, behaviour.groundLayers,
                        behaviour.groundRaycastHeight, behaviour.groundRaycastDistance);
                }

                position += clipPosition * weight;
                if (behaviour.matchEndRotation)
                {
                    rotation = hasRotation ? Quaternion.Slerp(rotation, clipRotation, weight / (totalWeight + weight)) : clipRotation;
                    hasRotation = true;
                }

                disableRootMotion |= behaviour.disableUccRootMotion;
                preserveRuntimeRotation |= behaviour.preserveRuntimeStartRotation;
                totalWeight += weight;
            }

            if (totalWeight <= 0f)
            {
                RestoreRootMotion();
                ReapplyLastPose();
                return;
            }

            position /= totalWeight;
            if (hasRotation && preserveRuntimeRotation && runtimeStartCaptured)
            {
                rotation = runtimeStartRotationOffset * rotation;
            }

            Apply(target, position, hasRotation ? rotation : target.rotation, disableRootMotion);
        }

        public override void OnPlayableDestroy(Playable playable)
        {
            RestoreRootMotion();
            ReapplyLastPose();
        }

        private void Apply(Transform target, Vector3 position, Quaternion rotation, bool disableRootMotion)
        {
            lastTarget = target;
            lastPosition = position;
            lastRotation = rotation;
            hasLastAppliedPose = true;

            if (Application.isPlaying && target.TryGetComponent(out UltimateCharacterLocomotion locomotion))
            {
                if (disableRootMotion && overriddenLocomotion == null)
                {
                    overriddenLocomotion = locomotion;
                    rootMotionWasEnabled = locomotion.UseRootMotionPosition;
                    locomotion.UseRootMotionPosition = false;
                }

                // AnimatorMonitor continues to report root-motion deltas even
                // when UCC is told not to use them. They must be discarded;
                // otherwise UCC can apply the whole accumulated delta on the
                // frame where Timeline releases control.
                if (disableRootMotion)
                {
                    locomotion.RootMotionDeltaPosition = Vector3.zero;
                    locomotion.RootMotionDeltaRotation = Quaternion.identity;
                }

                // The player is normally under LitOpsiveLocomotionBridge
                // during a StorySequence lock. Going through it keeps its
                // cached pose, camera and input state in sync with UCC.
                LitOpsiveLocomotionBridge bridge = target.GetComponent<LitOpsiveLocomotionBridge>();
                if (bridge != null && bridge.SetExternalPositionAndRotation(position, rotation, stopActiveAbilities: false))
                {
                    return;
                }

                locomotion.SetPositionAndRotation(position, rotation, snapAnimator: false, stopAllAbilities: false);
                return;
            }

            target.SetPositionAndRotation(position, rotation);
        }

        private void CaptureRuntimeStart(
            Transform target,
            Transform authoredStartPoint,
            bool preservePosition,
            bool preserveRotation)
        {
            if (runtimeStartCaptured || authoredStartPoint == null || (!preservePosition && !preserveRotation))
            {
                return;
            }

            // The first Timeline evaluation previously imposed point A on the
            // UCC character. Store the delta instead: the authored path stays
            // A→B, while its origin becomes the player's actual runtime pose.
            runtimeStartPositionOffset = preservePosition
                ? target.position - authoredStartPoint.position
                : Vector3.zero;
            runtimeStartRotationOffset = preserveRotation
                ? target.rotation * Quaternion.Inverse(authoredStartPoint.rotation)
                : Quaternion.identity;
            runtimeStartCaptured = true;
        }

        private void ReapplyLastPose()
        {
            // Quand le graphe Timeline est detruit, UCC peut restaurer sa
            // derniere pose de root motion dans le meme frame. Reposer la
            // derniere position evaluee preserve le resultat exact de la
            // courbe Progress, y compris pour un arret anticipe entre A et B.
            if (!Application.isPlaying || !hasLastAppliedPose || lastTarget == null)
            {
                return;
            }

            if (lastTarget.TryGetComponent(out UltimateCharacterLocomotion locomotion))
            {
                LitOpsiveLocomotionBridge bridge = lastTarget.GetComponent<LitOpsiveLocomotionBridge>();
                if (bridge != null && bridge.SetExternalPositionAndRotation(lastPosition, lastRotation, stopActiveAbilities: false))
                {
                    return;
                }

                locomotion.SetPositionAndRotation(lastPosition, lastRotation, snapAnimator: false, stopAllAbilities: false);
                return;
            }

            lastTarget.SetPositionAndRotation(lastPosition, lastRotation);
        }

        private void RestoreRootMotion()
        {
            if (overriddenLocomotion != null)
            {
                overriddenLocomotion.RootMotionDeltaPosition = Vector3.zero;
                overriddenLocomotion.RootMotionDeltaRotation = Quaternion.identity;
                overriddenLocomotion.UseRootMotionPosition = rootMotionWasEnabled;
                overriddenLocomotion = null;
            }
        }

        private static Vector3 ProjectToGround(
            Transform target,
            Vector3 desiredPosition,
            LayerMask groundLayers,
            float raycastHeight,
            float raycastDistance)
        {
            Vector3 origin = new Vector3(desiredPosition.x, target.position.y + Mathf.Max(0.01f, raycastHeight), desiredPosition.z);
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, Mathf.Max(0.01f, raycastDistance),
                groundLayers, QueryTriggerInteraction.Ignore);
            float nearestDistance = float.MaxValue;
            RaycastHit nearestHit = default;
            bool foundGround = false;

            for (int i = 0; i < hits.Length; i++)
            {
                Collider collider = hits[i].collider;
                if (collider == null || collider.transform == target || collider.transform.IsChildOf(target))
                {
                    continue;
                }

                if (hits[i].distance < nearestDistance)
                {
                    nearestDistance = hits[i].distance;
                    nearestHit = hits[i];
                    foundGround = true;
                }
            }

            // No hit: retain the character's present height. Point A/B never dictate Y.
            return foundGround
                ? new Vector3(desiredPosition.x, nearestHit.point.y, desiredPosition.z)
                : new Vector3(desiredPosition.x, target.position.y, desiredPosition.z);
        }
    }

}
