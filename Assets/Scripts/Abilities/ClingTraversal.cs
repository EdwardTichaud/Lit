using System.Collections;
using UnityEngine;

namespace Lit.Traversal
{
    [DisallowMultipleComponent]
    internal sealed class ClingTraversal : MonoBehaviour
    {
        private SquadCharacterController actor;
        private LitOpsiveLocomotionBridge bridge;
        private PlayerAnimationController animationController;
        private ClingPoint point;
        private bool holding;
        private bool launching;
        private float grabAllowedAt;
        private Vector3 lastPosition;
        private Quaternion lastRotation;
        private Coroutine holdRoutine;
        internal bool CanStart => !holding && !launching && Time.unscaledTime >= grabAllowedAt;

        internal static bool TryJumpForLocalPlayer()
        {
            var character = LocalPlayerUtils.GetControlledCharacter();
            return character != null && character.TryGetComponent(out ClingTraversal traversal) && traversal.TryJump();
        }

        internal bool Begin(ClingPoint target)
        {
            actor = GetComponent<SquadCharacterController>();
            if (!CanStart || target == null || !ClingController.CanPlay || !target.CanGrab(actor, true, out Pose pose)) return false;
            bridge = GetComponent<LitOpsiveLocomotionBridge>();
            GetComponent<PlayerScriptedJumpController>()?.EndPresentationForCling();
            if (bridge == null || !bridge.TryBeginScriptedTraversal(out _)) return false;
            animationController = GetComponent<PlayerAnimationController>();
            point = target;
            holding = true;
            ApplyPose(pose);
            actor.HideLocalInteractionPresentation();
            animationController?.SetClingPose(true, point);
            holdRoutine = StartCoroutine(MaintainHold());
            return true;
        }

        private IEnumerator MaintainHold()
        {
            var fixedStep = new WaitForFixedUpdate();
            while (holding)
            {
                // Submit alongside other scripted traversals: after UCC's fixed
                // tick, before its render interpolation and Animator IK.
                yield return fixedStep;
                if (!CanContinue() || !bridge.IsScriptedTraversalActive || point == null)
                    break;
                Physics.SyncTransforms();
                if (!point.TryGetHoldPose(actor, out Pose pose))
                    break;
                bool gripMoved = PoseChanged(lastPosition, lastRotation, pose);
                bool actorMoved = PoseChanged(transform.position, transform.rotation, pose);
                // The support already moved to its next pose. Still check the final capsule
                // against it; sweep against other obstacles rather than against its motion.
                if ((gripMoved || actorMoved) && !ClingPoint.IsClear(transform.position, pose, actor, point.SupportSurface))
                    break;
                if (gripMoved || actorMoved) ApplyPose(pose);
            }
            holdRoutine = null;
            Release();
        }

        internal static bool PoseChanged(Vector3 position, Quaternion rotation, Pose pose) =>
            (position - pose.position).sqrMagnitude > .000001f || Quaternion.Angle(rotation, pose.rotation) > .1f;

        private void ApplyPose(Pose pose)
        {
            lastPosition = pose.position;
            lastRotation = pose.rotation;
            bridge.ApplyScriptedTraversalPose(pose.position, pose.rotation);
        }

        private bool CanContinue()
        {
            // Suspending UCC input disables its normal locomotion handler.
            return actor != null && actor.CurrentHp > 0 && bridge != null && bridge.CanDriveScriptedTraversal &&
                !bridge.IsCombatLockActive && !bridge.IsCinematicMotionSessionActive &&
                LocalPlayerUtils.GetControlledCharacter() == gameObject &&
                GameFlowService.Instance != null && !GameFlowService.Instance.IsTransitioning;
        }

        private bool TryJump()
        {
            if (launching) return true;
            if (!holding || !ClingController.CanPlay || !CanContinue() || point == null) return false;
            Vector3 velocity = point.JumpVelocity(actor.GetWorldSpaceInput(LocalInputRouter.MoveValue), -transform.forward);
            Vector3 direction = Vector3.ProjectOnPlane(velocity, Vector3.up);
            if (direction.sqrMagnitude > .001f)
                ApplyPose(new Pose(transform.position, Quaternion.LookRotation(direction)));
            Release();
            launching = true;
            StartCoroutine(JumpAfterRelease(velocity));
            return true;
        }

        private IEnumerator JumpAfterRelease(Vector3 velocity)
        {
            // UCC restores gravity and the last grip pose after its fixed tick.
            while (bridge != null && bridge.IsScriptedTraversalActive && CanContinue()) yield return null;
            launching = false;
            if (!CanContinue() || !ClingController.CanPlay || bridge.IsInputSuppressedByUcc) yield break;
            bridge.AddExternalImpulseUntilGrounded(velocity, ForceMode.VelocityChange, .15f, 1.25f,
                airborneInertiaSeconds: .85f, airborneInertiaEndSpeedMultiplier: .8f);
        }

        private void Release()
        {
            if (!holding) return;
            holding = false;
            if (holdRoutine != null) StopCoroutine(holdRoutine);
            holdRoutine = null;
            point = null;
            grabAllowedAt = Time.unscaledTime + .45f;
            if (bridge != null && bridge.isActiveAndEnabled) bridge.EndScriptedTraversal();
            animationController?.SetClingPose(false);
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            launching = false;
            Release();
        }
    }
}
