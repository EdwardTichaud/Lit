using System.Collections.Generic;
using UnityEngine;

namespace Lit.Traversal
{
    [DisallowMultipleComponent]
    public sealed class ClingPoint : MonoBehaviour
    {
        internal static readonly HashSet<ClingPoint> Active = new();
        [SerializeField] private string pointId;
        [Tooltip("Hauteur et côté d'approche de la pose ; la surface détermine l'écart et l'orientation.")]
        [SerializeField] private Transform holdPose;
        [SerializeField] private Transform grip;
        [Tooltip("Collider solide de la face d'appui, non pas le volume de détection.")]
        [SerializeField] private Collider supportSurface;
        [SerializeField] private RuntimeOutlineTarget[] outlines;
        [SerializeField, Range(.1f, .75f)] private float contactRadius = .45f;
        [SerializeField, Min(1f)] private float observationRange = 12f;
        [Tooltip("Direction locale du saut sans entrée de déplacement.")]
        [SerializeField] private Vector3 defaultJumpDirection = Vector3.back;
        [SerializeField, Min(.1f)] private float jumpSpeed = 8f;
        [SerializeField, Min(.1f)] private float jumpUpSpeed = 7f;
        private WorldRulesStateManager rules;
        private bool highlighted;

        public string PointId => pointId;
        private string DiscoveryKey => "world.cling." + pointId;
        private WorldRulesStateManager Rules => rules != null ? rules : rules = FindAnyObjectByType<WorldRulesStateManager>();
        public bool Revealed => Rules != null && Rules.TryGetBool(DiscoveryKey, out bool value) && value;
        public Vector3 GripPosition => grip != null ? grip.position : transform.position;
        internal Collider SupportSurface => supportSurface;

        private void OnEnable() => Active.Add(this);
        private void OnDisable()
        {
            Active.Remove(this);
            SetHighlighted(false);
        }

        public static ClingPoint Find(string id)
        {
            foreach (ClingPoint point in Active)
                if (point.pointId == id) return point;
            return null;
        }

        internal void Reveal()
        {
            if (!string.IsNullOrWhiteSpace(pointId) && Rules != null) Rules.SetBool(DiscoveryKey, true);
        }

        internal void SetHighlighted(bool value)
        {
            if (highlighted == value) return;
            highlighted = value;
            if (outlines == null) return;
            foreach (RuntimeOutlineTarget outline in outlines)
                if (outline != null) outline.SetOutlined(value);
        }

        public bool CanObserve(SquadCharacterController actor)
        {
            if (!isActiveAndEnabled || actor == null || actor.CurrentHp <= 0 || string.IsNullOrWhiteSpace(pointId)) return false;
            Vector3 eye = actor.transform.position + Vector3.up * 1.45f;
            Vector3 delta = GripPosition - eye;
            if (delta.sqrMagnitude > observationRange * observationRange) return false;
            foreach (RaycastHit hit in Physics.RaycastAll(eye, delta.normalized, delta.magnitude,
                         Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(actor.transform) && !hit.transform.IsChildOf(transform)) return false;
            return true;
        }

        private const float SurfaceClearance = .025f;
        private const float ContactTolerance = .075f;

        internal sealed class AnimatedContacts
        {
            internal static readonly AvatarIKGoal[] Goals = { AvatarIKGoal.LeftHand, AvatarIKGoal.RightHand, AvatarIKGoal.LeftFoot, AvatarIKGoal.RightFoot };
            internal readonly Vector3[] positions = new Vector3[Goals.Length];
            internal readonly bool[] valid = new bool[Goals.Length];
            internal ClingPoint point;
            internal Vector3 localBody;
        }

        internal void FitAnimatedContacts(SquadCharacterController actor, Animator animator, AnimatedContacts contacts)
        {
            if (supportSurface == null || !supportSurface.enabled) return;
            if (contacts.point != this) CaptureContacts(actor, animator, contacts);
            Transform surface = supportSurface.transform;
            animator.bodyPosition = surface.TransformPoint(contacts.localBody);
            for (int i = 0; i < AnimatedContacts.Goals.Length; i++)
            {
                var goal = AnimatedContacts.Goals[i];
                animator.SetIKPositionWeight(goal, contacts.valid[i] ? 1f : 0f);
                if (contacts.valid[i]) animator.SetIKPosition(goal, surface.TransformPoint(contacts.positions[i]));
            }
        }

        private void CaptureContacts(SquadCharacterController actor, Animator animator, AnimatedContacts contacts)
        {
            GetBody(actor, out Vector3 centerOffset, out float radius, out _);
            Vector3 forward = actor.transform.forward;
            Vector3 center = actor.transform.position + centerOffset;
            Vector3 body = animator.bodyPosition;
            // The Mixamo clip is a pose at the pelvis, not a feet-on-floor pose.
            // Bring the pelvis into the capsule and the chest toward the support without moving UCC.
            Vector3 shift = Vector3.up * Mathf.Clamp(center.y - .10f - body.y, -.35f, 1.15f);
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (hips != null)
                shift.y = Mathf.Clamp(center.y - .14f - hips.position.y, -.35f, 1.15f);
            if (supportSurface.Raycast(new Ray(center, forward), out var torso, radius + ContactTolerance))
            {
                float bodyDepth = Vector3.Dot(torso.point - body, forward);
                shift += forward * Mathf.Clamp(bodyDepth - .19f, -.25f, .32f);
            }
            Transform surface = supportSurface.transform;
            contacts.point = this;
            contacts.localBody = surface.InverseTransformPoint(body + shift);
            for (int i = 0; i < AnimatedContacts.Goals.Length; i++)
                CaptureLimb(i, i < 2 ? .045f : .10f);

            void CaptureLimb(int index, float spacing)
            {
                Vector3 animated = animator.GetIKPosition(AnimatedContacts.Goals[index]) + shift;
                Vector3 rayStart = animated - forward * .65f;
                if (!FindContact(rayStart, out var hit) &&
                    !FindContact(Vector3.Lerp(rayStart, new Vector3(rayStart.x, center.y, rayStart.z), .22f), out hit))
                {
                    contacts.valid[index] = false;
                    return;
                }
                contacts.valid[index] = true;
                contacts.positions[index] = surface.InverseTransformPoint(hit.point + hit.normal * spacing);
            }

            bool FindContact(Vector3 origin, out RaycastHit hit) =>
                supportSurface.Raycast(new Ray(origin, forward), out hit, 1.2f) &&
                Vector3.Dot(hit.normal, forward) <= -.75f;
        }

        internal bool TryGetHoldPose(SquadCharacterController actor, out Pose pose)
        {
            pose = default;
            if (!isActiveAndEnabled || holdPose == null || supportSurface == null ||
                string.IsNullOrWhiteSpace(pointId) || actor == null) return false;
            GetBody(actor, out Vector3 offset, out float radius, out float height);
            var localCenter = Quaternion.Inverse(actor.transform.rotation) * offset;
            return TryFitSurface(supportSurface, holdPose.position, holdPose.forward, GripPosition.y,
                localCenter, radius, height, out pose);
        }

        // Sample the real collider, not its world-space bounding box: corners and narrow rails must not count as a wall.
        internal static bool TryFitSurface(Collider surface, Vector3 anchor, Vector3 facing, float gripHeight,
            Vector3 localCenter, float radius, float height, out Pose pose)
        {
            pose = default;
            if (surface == null || !surface.enabled || !surface.gameObject.activeInHierarchy || surface.isTrigger) return false;
            Vector3 forward = Vector3.ProjectOnPlane(facing, Vector3.up).normalized;
            if (forward.sqrMagnitude < .5f) return false;
            Vector3 origin = new Vector3(anchor.x, gripHeight, anchor.z) - forward * .4f;
            if (!surface.Raycast(new Ray(origin, forward), out var face, 1.45f) ||
                Mathf.Abs(face.normal.y) > .15f || Vector3.Dot(face.normal, forward) > -.5f) return false;
            Vector3 normal = Vector3.ProjectOnPlane(face.normal, Vector3.up).normalized;
            Quaternion rotation = Quaternion.LookRotation(-normal, Vector3.up);
            Vector3 center = face.point + normal * (radius + SurfaceClearance);
            center.y = anchor.y + localCenter.y;
            int supported = 0;
            for (int slice = 0; slice < 5; slice++)
            {
                Vector3 sample = center + Vector3.up * (height * (-.4f + slice * .2f));
                if (surface.Raycast(new Ray(sample, -normal), out var contact, radius + ContactTolerance) &&
                    Vector3.Dot(contact.normal, normal) >= .85f &&
                    contact.distance >= radius - .005f) supported++;
            }
            if (supported < 4) return false;
            pose = new Pose(center - rotation * localCenter, rotation);
            return true;
        }

        internal bool IsNear(Vector3 feet) => holdPose != null &&
            (feet - holdPose.position).sqrMagnitude <= (contactRadius + .2f) * (contactRadius + .2f);

        internal bool CanGrab(SquadCharacterController actor, bool requireLocalDriver, out Pose pose)
        {
            pose = default;
            if (actor == null || actor.CurrentHp <= 0 || actor.IsMovementInputSuppressed ||
                actor.IsFlightActive || !IsNear(actor.transform.position)) return false;
            var abilities = AbilityService.Instance;
            var bridge = actor.GetComponent<LitOpsiveLocomotionBridge>();
            if (abilities == null || !abilities.HasEquipped(actor.gameObject, abilities.Resolve("grimpeur")) ||
                bridge == null || bridge.IsInputSuppressedByUcc || bridge.IsCombatLockActive ||
                bridge.IsCinematicMotionSessionActive || requireLocalDriver && !bridge.IsDriving) return false;
            // The Seuil beam is moved by Transform each frame; the ray and swept capsule
            // must see the same current collider pose.
            Physics.SyncTransforms();
            return TryGetHoldPose(actor, out pose) &&
                (actor.transform.position - pose.position).sqrMagnitude <= contactRadius * contactRadius &&
                IsClear(actor.transform.position, pose, actor);
        }

        private static void GetBody(SquadCharacterController actor, out Vector3 offset, out float radius, out float height)
        {
            if (actor.TryGetLocomotionCapsule(out Vector3 center, out radius, out height))
                offset = center - actor.transform.position;
            else { offset = Vector3.up * .9f; radius = .3f; height = 1.8f; }
        }

        // The small contact correction and moving grips both keep world collision.
        internal static bool IsClear(Vector3 from, Pose to, SquadCharacterController actor, Collider carriedBy = null)
        {
            GetBody(actor, out Vector3 offset, out float capsuleRadius, out float height);
            Vector3 axis = Vector3.up * Mathf.Max(0f, height * .5f - capsuleRadius);
            Vector3 goalOffset = to.rotation * Quaternion.Inverse(actor.transform.rotation) * offset;
            Vector3 goalCenter = to.position + goalOffset;
            float radius = Mathf.Max(.01f, capsuleRadius - .015f);
            foreach (Collider collider in Physics.OverlapCapsule(goalCenter - axis, goalCenter + axis, radius,
                         Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                if (!collider.transform.IsChildOf(actor.transform)) return false;
            Vector3 startCenter = from + offset;
            Vector3 delta = goalCenter - startCenter;
            if (delta.sqrMagnitude < .000001f) return true;
            foreach (RaycastHit hit in Physics.CapsuleCastAll(startCenter - axis, startCenter + axis, radius,
                         delta.normalized, delta.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                if (hit.collider != carriedBy && !hit.transform.IsChildOf(actor.transform)) return false;
            return true;
        }

        internal Vector3 JumpVelocity(Vector2 worldInput, Vector3 outward = default)
        {
            Vector3 direction = new Vector3(worldInput.x, 0f, worldInput.y);
            if (direction.sqrMagnitude < .04f)
                direction = Vector3.ProjectOnPlane(transform.TransformDirection(defaultJumpDirection), Vector3.up);
            direction.Normalize();
            if (outward.sqrMagnitude > .5f)
                direction += outward * Mathf.Max(0f, .30f - Vector3.Dot(direction, outward));
            return direction.normalized * jumpSpeed + Vector3.up * jumpUpSpeed;
        }

        private void OnDrawGizmosSelected()
        {
            if (holdPose == null) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(holdPose.position, contactRadius);
            Gizmos.DrawLine(holdPose.position, GripPosition);
            Gizmos.DrawRay(holdPose.position, JumpVelocity(Vector2.zero) * .35f);
        }
    }
}
