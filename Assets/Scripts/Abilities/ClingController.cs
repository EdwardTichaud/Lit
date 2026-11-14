using UnityEngine;

namespace Lit.Traversal
{
    /// <summary>Finds nearby grip contacts and reveals authored grips for Observateur.</summary>
    internal sealed class ClingController : MonoBehaviour
    {
        private float nextObservation;
        private float nextRequest;
        private float nextSync;

        internal static bool CanPlay => InputModeCoordinator.CurrentMode == InputMode.Exploration &&
            !InputFocusStack.HasAnyFocus() && !JoinSyncSystem.IsGameplayBlocked &&
            GameFlowService.Instance != null && GameFlowService.Instance.HasGameplaySession &&
            !GameFlowService.Instance.IsTransitioning;

        private void Update()
        {
            if (ClingPoint.Active.Count == 0) return;
            var abilities = AbilityService.Instance;
            var root = LocalPlayerUtils.GetControlledCharacter();
            var actor = root != null ? root.GetComponent<SquadCharacterController>() : null;
            bool ready = CanPlay && abilities != null && abilities.IsReady && actor != null && actor.CurrentHp > 0;
            bool observe = ready && abilities.HasEquipped(root, abilities.Resolve("observateur"));
            bool canGrab = ready && abilities.HasEquipped(root, abilities.Resolve("grimpeur"));
            if (canGrab && root.TryGetComponent(out ClingTraversal traversal) && !traversal.CanStart)
                canGrab = false;
            var service = WorldInteractionService.Instance;
            bool online = abilities != null && !abilities.IsSolo;
            float now = Time.unscaledTime;
            if (ready && online && service != null && service.IsSpawned && now >= nextSync)
            {
                nextSync = now + 3f;
                service.RequestClingDiscoveriesServerRpc();
            }

            bool scan = now >= nextObservation;
            if (scan) nextObservation = now + .2f;
            ClingPoint closest = null;
            float distance = float.PositiveInfinity;
            foreach (ClingPoint point in ClingPoint.Active)
            {
                if (scan)
                {
                    bool visible = ready && point.CanObserve(actor);
                    if (visible && observe && !point.Revealed)
                    {
                        if (online && service != null && service.IsSpawned)
                            service.RequestClingObservationServerRpc(point.PointId);
                        else if (!online) point.Reveal();
                    }
                    point.SetHighlighted(visible && point.Revealed);
                }
                if (!canGrab || !point.CanGrab(actor, true, out Pose pose)) continue;
                float candidate = (pose.position - actor.transform.position).sqrMagnitude;
                if (candidate >= distance) continue;
                closest = point;
                distance = candidate;
            }
            if (closest == null || now < nextRequest) return;
            nextRequest = now + .12f;
            if (online)
            {
                if (service != null && service.IsSpawned) service.RequestClingGrabServerRpc(closest.PointId);
            }
            else BeginLocalGrab(closest);
        }

        internal static void BeginLocalGrab(ClingPoint point)
        {
            if (point == null || !CanPlay) return;
            var root = LocalPlayerUtils.GetControlledCharacter();
            if (root == null) return;
            var traversal = root.GetComponent<ClingTraversal>() ?? root.AddComponent<ClingTraversal>();
            traversal.Begin(point);
        }

        private void OnDisable()
        {
            foreach (ClingPoint point in ClingPoint.Active) point.SetHighlighted(false);
        }
    }
}
