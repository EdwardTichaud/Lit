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
        private bool carryBlocked;
        private Vector3 supportLocalPosition;
        private Vector3 supportLocalForward;
        private Vector3 supportLocalNormal;
        internal Collider HeldSurface { get; private set; }
        internal ClingGripTargets Targets { get; private set; }
        internal Vector3 HeldNormal => HeldSurface != null ?
            HeldSurface.transform.TransformDirection(supportLocalNormal).normalized : -transform.forward;
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
            if (!CanStart || target == null || !ClingController.CanPlay ||
                !target.TryResolveGrab(actor, true, out Pose pose, out Collider surface, out Vector3 normal,
                    out ClingGripTargets targets)) return false;
            bridge = GetComponent<LitOpsiveLocomotionBridge>();
            if (TryGetComponent(out PlayerScriptedJumpController jump)) jump.EndPresentationForCling();
            if (bridge == null) return false;
            animationController = GetComponent<PlayerAnimationController>();
            point = target;
            HeldSurface = surface;
            Targets = targets;
            var support = surface.transform;
            supportLocalPosition = support.InverseTransformPoint(pose.position);
            supportLocalForward = support.InverseTransformDirection(pose.rotation * Vector3.forward);
            supportLocalNormal = support.InverseTransformDirection(normal);
            holding = true;
            carryBlocked = false;
            if (!bridge.TryBeginClingCarry(pose, TryResolveCarryPose))
            {
                holding = false;
                point = null;
                HeldSurface = null;
                Targets = default;
                return false;
            }
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
                yield return fixedStep;
                // Release only outside the UCC ability iteration.
                if (carryBlocked || !CanContinue() || !bridge.IsCarriedByCling) break;
            }
            holdRoutine = null;
            Release();
        }

        private bool TryResolveCarryPose(Vector3 position, Quaternion rotation, out Pose pose)
        {
            pose = default;
            if (carryBlocked || !CanContinue() || point == null || !point.isActiveAndEnabled ||
                HeldSurface == null || !HeldSurface.enabled || !HeldSurface.gameObject.activeInHierarchy)
            {
                carryBlocked = true;
                return false;
            }
            Physics.SyncTransforms();
            if (point.GrabAlongSurface)
            {
                var support = HeldSurface.transform;
                Vector3 facing = Vector3.ProjectOnPlane(support.TransformDirection(supportLocalForward), Vector3.up);
                if (facing.sqrMagnitude < .1f) { carryBlocked = true; return false; }
                pose = new Pose(support.TransformPoint(supportLocalPosition), Quaternion.LookRotation(facing, Vector3.up));
            }
            else if (!point.TryGetHoldPose(actor, out pose)) { carryBlocked = true; return false; }
            // Keep the existing final overlap and sweep before submitting motion to UCC.
            carryBlocked = PoseChanged(position, rotation, pose) && !ClingPoint.IsClear(position, pose, actor, HeldSurface);
            return !carryBlocked;
        }

        internal static bool PoseChanged(Vector3 position, Quaternion rotation, Pose pose) =>
            (position - pose.position).sqrMagnitude > .000001f || Quaternion.Angle(rotation, pose.rotation) > .1f;

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
            Vector3 normal = HeldNormal;
            Vector3 velocity = point.JumpVelocity(actor.GetWorldSpaceInput(LocalInputRouter.MoveValue), normal);
            if (normal.y < -.3f)
                velocity += normal * Mathf.Max(0f, 1.5f - Vector3.Dot(velocity, normal));
            Vector3 direction = Vector3.ProjectOnPlane(velocity, Vector3.up);
            if (direction.sqrMagnitude > .001f)
                bridge.ApplyScriptedTraversalPose(transform.position, Quaternion.LookRotation(direction));
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
            HeldSurface = null;
            Targets = default;
            grabAllowedAt = Time.unscaledTime + .45f;
            if (bridge != null && bridge.isActiveAndEnabled) bridge.EndClingCarry();
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
