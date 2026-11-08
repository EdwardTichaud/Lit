using UnityEngine;

namespace Lit.Traversal
{
    /// <summary>Only owns its own UCC traversal lock. No transform motion or collider disabling.</summary>
    public sealed class GrappleTravel : MonoBehaviour
    {
        private LitOpsiveLocomotionBridge bridge;
        private GrappleAnchor anchor;
        private Vector3 origin, lastSafe;
        private Quaternion originalRotation;
        private float elapsed;
        private bool ownsTraversal;
        private LineRenderer tether;
        private Animator animator;
        private bool hasAirbornePose;
        public bool IsTravelling => ownsTraversal;
        public bool Begin(GrappleAnchor target)
        {
            var actor = GetComponent<SquadCharacterController>();
            bridge = GetComponent<LitOpsiveLocomotionBridge>();
            if (ownsTraversal || target == null || actor == null || !target.CanUse(actor) || bridge == null ||
                !bridge.TryBeginScriptedTraversal(out _)) return false;
            ownsTraversal = true; anchor = target; elapsed = 0;
            origin = lastSafe = transform.position; originalRotation = transform.rotation;
            animator = GetComponentInChildren<Animator>();
            hasAirbornePose = animator != null && animator.HasState(0, Animator.StringToHash("Falling"));
            if (hasAirbornePose) animator.CrossFadeInFixedTime("Falling", .12f, 0);
            if (target.visual != null)
            {
                var line = new GameObject("GrappleTether"); line.transform.SetParent(transform, false);
                tether = line.AddComponent<LineRenderer>(); tether.positionCount = 2;
                tether.startWidth = .035f; tether.endWidth = .015f;
                var renderer = target.visual.GetComponentInChildren<Renderer>();
                if (renderer != null) tether.sharedMaterial = renderer.sharedMaterial;
            }
            return true;
        }
        private void Update()
        {
            if (!ownsTraversal) return;
            var actor = GetComponent<SquadCharacterController>();
            if (anchor == null || !anchor.isActiveAndEnabled || actor == null || actor.CurrentHp <= 0 ||
                GameFlowService.Instance != null && GameFlowService.Instance.IsTransitioning) { Cancel(); return; }
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(.2f, anchor.duration));
            Vector3 next = anchor.Position(origin, t);
            if (!GrappleAnchor.ClearBody(lastSafe, next, transform)) { Cancel(); return; }
            Vector3 direction = anchor.landing.position - origin; direction.y = 0;
            bridge.ApplyScriptedTraversalPose(next, direction.sqrMagnitude > .001f ? Quaternion.LookRotation(direction) : originalRotation);
            lastSafe = next;
            if (tether != null) { tether.SetPosition(0, transform.position + Vector3.up * 1.3f); tether.SetPosition(1, anchor.HookPosition); }
            if (t >= 1) Finish();
        }
        public void Cancel()
        {
            if (!ownsTraversal) return;
            // Abort to the known departure floor, never leave the player suspended above the void.
            if (bridge != null && gameObject.activeInHierarchy) bridge.ApplyScriptedTraversalPose(origin, originalRotation);
            Finish();
        }
        private void Finish()
        {
            // An inactive bridge clears its own traversal state in OnDisable;
            // never ask it to start its fixed-step release coroutine while inactive.
            if (ownsTraversal && bridge != null && bridge.isActiveAndEnabled) bridge.EndScriptedTraversal();
            var actor = GetComponent<SquadCharacterController>();
            if (hasAirbornePose && animator != null && actor != null && actor.CurrentHp > 0 &&
                (bridge == null || !bridge.IsCinematicMotionSessionActive) && InputModeCoordinator.CurrentMode != InputMode.Cinematic &&
                animator.HasState(0, Animator.StringToHash("Locomotion")))
                animator.CrossFadeInFixedTime("Locomotion", .12f, 0);
            hasAirbornePose = false;
            ownsTraversal = false; anchor = null;
            if (tether != null) Destroy(tether.gameObject);
            tether = null;
        }
        private void OnDisable() => Cancel();
    }
}
