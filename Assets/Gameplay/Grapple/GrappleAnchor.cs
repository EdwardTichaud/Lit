using UnityEngine;

namespace Lit.Traversal
{
    /// <summary>Authored traversal, discovery is a shared persistent world fact, not a renderer state.</summary>
    public sealed class GrappleAnchor : MonoBehaviour
    {
        public string anchorId;
        public string routeId;
        public Transform launch;
        public Transform landing;
        public Transform hook;
        public GameObject visual;
        [Min(1)] public float detectionRange = 65;
        [Min(1)] public float useRange = 40;
        [Min(.2f)] public float launchRadius = 5;
        [Min(.2f)] public float duration = 1.1f;
        [Min(0)] public float arcHeight = 4;
        private WorldRulesStateManager rules;
        public string LastUseFailure { get; private set; }
        public string StateKey => "world.grapple." + anchorId;
        public WorldRulesStateManager Rules => rules != null ? rules : rules = FindAnyObjectByType<WorldRulesStateManager>();
        public bool Revealed => Rules != null && Rules.TryGetBool(StateKey, out bool value) && value;
        public string PreparedKey => "world.grapple.route." + (string.IsNullOrWhiteSpace(routeId) ? anchorId : routeId);
        public bool Prepared => Rules != null && Rules.TryGetBool(PreparedKey, out bool value) && value;
        public void ApplyPreparation(bool value) { if (Rules != null) Rules.SetBool(PreparedKey, value); }
        public void Prepare() => ApplyPreparation(true);
        public bool HasTraversalCapability(SquadCharacterController actor)
        {
            var abilities = AbilityService.Instance;
            return abilities != null && actor != null && (abilities.HasEquipped(actor.gameObject, abilities.Resolve("bondisseur")) ||
                !abilities.IsSolo && Prepared);
        }
        public Vector3 HookPosition => hook != null ? hook.position : transform.position;
        private void OnEnable() { if (visual != null) visual.SetActive(false); }
        private void Update() { if (visual != null && visual.activeSelf != Revealed) visual.SetActive(Revealed); }
        public void ApplyDiscovery(bool value) { if (Rules != null) Rules.SetBool(StateKey, value); }
        public void RevealRoute()
        {
            ApplyDiscovery(true);
            if (string.IsNullOrWhiteSpace(routeId)) return;
            foreach (var other in FindObjectsByType<GrappleAnchor>())
                if (other.routeId == routeId) other.ApplyDiscovery(true);
        }
        public static GrappleAnchor Find(string id)
        {
            foreach (var anchor in FindObjectsByType<GrappleAnchor>())
                if (anchor.anchorId == id) return anchor;
            return null;
        }
        public bool CanObserve(SquadCharacterController actor)
        {
            if (actor == null || actor.CurrentHp <= 0 || string.IsNullOrWhiteSpace(anchorId) || !isActiveAndEnabled) return false;
            Vector3 eye = actor.transform.position + Vector3.up * 1.4f;
            return Vector3.Distance(eye, HookPosition) <= detectionRange && ClearLine(eye, HookPosition, actor.transform);
        }
        public bool CanUse(SquadCharacterController actor)
        {
            LastUseFailure = null;
            bool Refuse(string reason) { LastUseFailure = reason; return false; }
            if (!Revealed || launch == null || landing == null) return Refuse("Undiscovered or missing endpoints");
            if (!CanObserve(actor)) return Refuse("Observation range/line of sight/life");
            if (!HasTraversalCapability(actor)) return Refuse("Bondisseur required");
            if (Vector3.Distance(actor.transform.position, launch.position) > launchRadius) return Refuse("Launch radius");
            if (Vector3.Distance(actor.transform.position, landing.position) > useRange) return Refuse("Traversal range");
            if (actor.IsMovementInputSuppressed || actor.IsFlightActive) return Refuse("Actor input suppressed/flight");
            var bridge = actor.GetComponent<LitOpsiveLocomotionBridge>();
            var jump = actor.GetComponent<PlayerScriptedJumpController>();
            if (bridge == null || !bridge.Grounded || bridge.IsInputSuppressedByUcc || bridge.IsCombatLockActive || bridge.IsCinematicMotionSessionActive)
                return Refuse("UCC grounded/input/combat/cinematic lock");
            if (jump != null && jump.IsActive) return Refuse("Jump active");
            if (!SafeLanding()) return Refuse("Missing arrival floor");
            return ClearPath(actor);
        }
        public bool SafeLanding()
        {
            if (landing == null) return false;
            foreach (var hit in Physics.RaycastAll(landing.position + Vector3.up, Vector3.down, 1.6f, ~0, QueryTriggerInteraction.Ignore))
                if (hit.normal.y > .65f && hit.collider.GetComponentInParent<SquadCharacterController>() == null) return true;
            return false;
        }
        public Vector3 Position(Vector3 start, float t) => Evaluate(start, landing.position, arcHeight, t);
        public static Vector3 Evaluate(Vector3 start, Vector3 end, float height, float t)
        {
            t = Mathf.Clamp01(t);
            float s = t * t * (3 - 2 * t);
            return Vector3.Lerp(start, end, s) + Vector3.up * (4 * height * s * (1 - s));
        }
        public bool ClearPath(SquadCharacterController actor)
        {
            Vector3 previous = actor.transform.position;
            for (int i = 1; i <= 32; i++)
            {
                Vector3 next = Position(actor.transform.position, i / 32f);
                if (!ClearBody(previous, next, actor.transform, out string obstacle))
                { LastUseFailure = "Arc step " + i + ": " + obstacle; return false; }
                previous = next;
            }
            return true;
        }
        public static bool ClearBody(Vector3 a, Vector3 b, Transform actor)
            => ClearBody(a, b, actor, out _);
        private static bool ClearBody(Vector3 a, Vector3 b, Transform actor, out string obstacle)
        {
            obstacle = null;
            Vector3 delta = b - a;
            if (delta.sqrMagnitude < .000001f) return true;
            foreach (var hit in Physics.CapsuleCastAll(a + Vector3.up * .4f, a + Vector3.up * 1.5f,
                         .28f, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.collider.transform.IsChildOf(actor))
                { obstacle = hit.collider.name + " at " + hit.point.ToString("F2"); return false; }
            return true;
        }
        private static bool ClearLine(Vector3 a, Vector3 b, Transform actor)
        {
            foreach (var hit in Physics.RaycastAll(a, (b - a).normalized, Vector3.Distance(a, b), ~0, QueryTriggerInteraction.Ignore))
                if (!hit.collider.transform.IsChildOf(actor)) return false;
            return true;
        }
        private void OnDrawGizmosSelected()
        {
            if (launch == null || landing == null) return;
            Gizmos.color = Color.cyan; Vector3 previous = launch.position;
            for (int i = 1; i <= 32; i++) { var p = Position(launch.position, i / 32f); Gizmos.DrawLine(previous, p); previous = p; }
            Gizmos.DrawWireSphere(launch.position, launchRadius);
        }
    }
}
