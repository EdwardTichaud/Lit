using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lit.Traversal
{
    /// <summary>One local scanner per session. A confirmation is consumed only for the selected distant anchor.</summary>
    public sealed class GrappleController : MonoBehaviour
    {
        private GrappleAnchor[] anchors = System.Array.Empty<GrappleAnchor>();
        private GrappleAnchor selected;
        private float nextScan, nextDiscovery, nextUse, nextSnapshot;
        private GameObject prompt;
        private TMP_Text label;
        private bool snapshotRequested;
        private UnityEngine.SceneManagement.Scene snapshotScene;
        private bool selectedUsable;
        private AbilityService abilities;
        private void OnEnable()
        {
            abilities = GetComponent<AbilityService>();
            LocalInputRouter.EnsureInitialized();
            LocalInputRouter.PrioritizedInteract += Interact;
        }
        private void OnDisable()
        {
            LocalInputRouter.PrioritizedInteract -= Interact;
            if (prompt != null) Destroy(prompt);
        }
        private SquadCharacterController Actor => LocalPlayerContext.LocalCharacterRoot != null
            ? LocalPlayerContext.LocalCharacterRoot.GetComponent<SquadCharacterController>() : null;
        private bool Ready => abilities != null && abilities.IsReady && Actor != null && Actor.CurrentHp > 0 &&
            InputModeCoordinator.CurrentMode == InputMode.Exploration && !InputFocusStack.HasAnyFocus() &&
            GameFlowService.Instance != null && !GameFlowService.Instance.IsTransitioning && !JoinSyncSystem.IsGameplayBlocked;
        private void Update()
        {
            if (Time.unscaledTime >= nextScan)
            {
                nextScan = Time.unscaledTime + .2f;
                anchors = FindObjectsByType<GrappleAnchor>();
                if (anchors.Length == 0) snapshotRequested = false;
                Scan();
            }
            if (prompt == null) return;
            var travel = Actor != null ? Actor.GetComponent<GrappleTravel>() : null;
            bool show = Ready && selected != null && selected.Revealed && (travel == null || !travel.IsTravelling);
            prompt.SetActive(show);
            if (!show) return;
            var camera = Camera.main;
            prompt.transform.position = selected.HookPosition;
            if (camera != null) prompt.transform.rotation = Quaternion.LookRotation(prompt.transform.position - camera.transform.position, camera.transform.up);
            bool bond = selected.HasTraversalCapability(Actor);
            if (label != null) label.text = !bond ? "Ancrage repéré — Bondisseur requis pour préparer la traversée" : selectedUsable
                ? "A · Se propulser" : selected.launch != null && Vector3.Distance(Actor.transform.position, selected.launch.position) > selected.launchRadius
                    ? "Ancrage repéré — rejoindre le bord de départ" : "Trajet obstrué ou action en cours — réessayer une fois libre";
        }
        private void Scan()
        {
            selected = null;
            selectedUsable = false;
            if (!Ready) return;
            var service = WorldInteractionService.Instance;
            bool online = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
            if (anchors.Length > 0 && snapshotScene != anchors[0].gameObject.scene)
            { snapshotScene = anchors[0].gameObject.scene; snapshotRequested = false; }
            if (online && (!snapshotRequested || Time.unscaledTime >= nextSnapshot) && anchors.Length > 0 && service != null && service.IsSpawned && anchors[0].Rules != null)
            { snapshotRequested = true; nextSnapshot = Time.unscaledTime + 2; service.RequestGrappleSnapshotServerRpc(); }
            var observer = abilities.Resolve("observateur");
            float best = float.NegativeInfinity;
            var camera = Camera.main;
            foreach (var anchor in anchors)
            {
                if (!anchor.CanObserve(Actor)) continue;
                Vector3 delta = anchor.HookPosition - (camera != null ? camera.transform.position : Actor.transform.position);
                float alignment = Vector3.Dot((camera != null ? camera.transform.forward : Actor.transform.forward), delta.normalized);
                if (alignment < .65f) continue;
                if (!anchor.Revealed && abilities.HasEquipped(Actor.gameObject, observer) && Time.unscaledTime >= nextDiscovery)
                {
                    nextDiscovery = Time.unscaledTime + .3f;
                    if (online) { if (service != null && service.IsSpawned) service.RequestGrappleServerRpc(anchor.anchorId, false); }
                    else anchor.RevealRoute();
                }
                float score = alignment * 100 - delta.magnitude * .05f;
                if (anchor.Revealed && score > best) { best = score; selected = anchor; }
            }
            if (selected != null) selectedUsable = selected.CanUse(Actor);
            if (selected != null && prompt == null)
            {
                prompt = WorldInteractionUiSettings.Create(transform);
                if (prompt != null) label = prompt.GetComponentInChildren<TMP_Text>(true);
            }
        }
        private void Interact(InputAction.CallbackContext context)
        {
            if (!Ready || selected == null || LocalInputRouter.IsInteractConsumed ||
                !selected.HasTraversalCapability(Actor) || !selected.CanUse(Actor)) return;
            LocalInputRouter.ConsumeInteract();
            if (Time.unscaledTime < nextUse) return;
            nextUse = Time.unscaledTime + .4f;
            var service = WorldInteractionService.Instance;
            if (!abilities.IsSolo) { if (service != null && service.IsSpawned) service.RequestGrappleServerRpc(selected.anchorId, true); }
            else { selected.Prepare(); StartTravel(selected); }
        }
        public static void StartTravel(GrappleAnchor anchor)
        {
            var root = LocalPlayerContext.LocalCharacterRoot;
            var actor = root != null ? root.GetComponent<SquadCharacterController>() : null;
            if (actor == null || anchor == null || !anchor.HasTraversalCapability(actor)) return;
            var travel = actor.GetComponent<GrappleTravel>() ?? actor.gameObject.AddComponent<GrappleTravel>();
            travel.Begin(anchor);
        }
    }
}
