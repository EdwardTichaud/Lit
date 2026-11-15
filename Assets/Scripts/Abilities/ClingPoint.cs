using System.Collections.Generic;
using Opsive.UltimateCharacterController.Character;
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
        [Tooltip("Autorise une prise au contact des colliders solides de l'objet, sans pose prédéfinie.")]
        [SerializeField] private bool grabAlongSurface;
        [SerializeField, Min(1f)] private float observationRange = 12f;
        [Tooltip("Direction locale du saut sans entrée de déplacement.")]
        [SerializeField] private Vector3 defaultJumpDirection = Vector3.back;
        [SerializeField, Min(.1f)] private float jumpSpeed = 8f;
        [SerializeField, Min(.1f)] private float jumpUpSpeed = 7f;
        private WorldRulesStateManager rules;
        private bool highlighted;
        private Collider[] contactSurfaces;

        public string PointId => pointId;
        private string DiscoveryKey => "world.cling." + pointId;
        private WorldRulesStateManager Rules => rules != null ? rules : rules = FindAnyObjectByType<WorldRulesStateManager>();
        public bool Revealed => Rules != null && Rules.TryGetBool(DiscoveryKey, out bool value) && value;
        public Vector3 GripPosition => grip != null ? grip.position : transform.position;
        internal Collider SupportSurface => supportSurface;
        internal bool GrabAlongSurface => grabAlongSurface;

        private void OnEnable()
        {
            if (grabAlongSurface) contactSurfaces = GetComponentsInChildren<Collider>(true);
            Active.Add(this);
        }
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
        private const float MagnetReach = .42f;
        private const float MaxMagnetShift = .62f;

        internal sealed class AnimatedContacts
        {
            internal static readonly AvatarIKGoal[] Goals = { AvatarIKGoal.LeftHand, AvatarIKGoal.RightHand, AvatarIKGoal.LeftFoot, AvatarIKGoal.RightFoot };
            internal readonly Vector3[] positions = new Vector3[Goals.Length];
            internal readonly bool[] valid = new bool[Goals.Length];
            internal ClingPoint point;
            internal Vector3 localBody;
            internal Quaternion localBodyRotation;
        }

        internal void FitAnimatedContacts(SquadCharacterController actor, Animator animator, AnimatedContacts contacts)
        {
            var traversal = actor.GetComponent<ClingTraversal>();
            bool hasLocalGrip = traversal != null && traversal.HeldSurface != null;
            // Remote characters only replicate the grip ID and pose; preserve their authored IK fallback.
            var collider = hasLocalGrip ? traversal.HeldSurface : supportSurface;
            if (collider == null || !collider.enabled) return;
            if (hasLocalGrip)
            {
                var targets = traversal.Targets;
                var model = collider.transform;
                if (contacts.point != this)
                {
                    var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                    Vector3 bodyOffset = hips != null ? animator.bodyPosition - hips.position : Vector3.zero;
                    if (targets.TopBrace)
                    {
                        // Place the spine over the hands without tilting the locomotion capsule.
                        Quaternion pitch = Quaternion.AngleAxis(74f, actor.transform.right);
                        contacts.localBodyRotation = Quaternion.Inverse(model.rotation) * pitch * animator.bodyRotation;
                        bodyOffset = pitch * bodyOffset;
                    }
                    contacts.localBody = targets.Hips + model.InverseTransformVector(bodyOffset);
                    contacts.point = this;
                }
                if (targets.TopBrace) animator.bodyRotation = model.rotation * contacts.localBodyRotation;
                animator.bodyPosition = model.TransformPoint(contacts.localBody);
                for (int i = 0; i < AnimatedContacts.Goals.Length; i++)
                {
                    var goal = AnimatedContacts.Goals[i];
                    animator.SetIKPositionWeight(goal, 1f);
                    animator.SetIKPosition(goal, model.TransformPoint(targets.Limb(i)));
                }
                return;
            }
            if (contacts.point != this)
                CaptureContacts(actor, animator, contacts, collider, -actor.transform.forward);
            Transform surface = collider.transform;
            animator.bodyPosition = surface.TransformPoint(contacts.localBody);
            for (int i = 0; i < AnimatedContacts.Goals.Length; i++)
            {
                var goal = AnimatedContacts.Goals[i];
                animator.SetIKPositionWeight(goal, contacts.valid[i] ? 1f : 0f);
                if (contacts.valid[i]) animator.SetIKPosition(goal, surface.TransformPoint(contacts.positions[i]));
            }
        }

        private void CaptureContacts(SquadCharacterController actor, Animator animator, AnimatedContacts contacts,
            Collider collider, Vector3 surfaceNormal)
        {
            GetBody(actor, out Vector3 centerOffset, out float radius, out _);
            Vector3 forward = -surfaceNormal;
            Vector3 center = actor.transform.position + centerOffset;
            Vector3 body = animator.bodyPosition;
            // The Mixamo clip is a pose at the pelvis, not a feet-on-floor pose.
            // Bring the pelvis into the capsule and the chest toward the support without moving UCC.
            Vector3 shift = Vector3.up * Mathf.Clamp(center.y - .10f - body.y, -.35f, 1.15f);
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (hips != null)
                shift.y = Mathf.Clamp(center.y - .14f - hips.position.y, -.35f, 1.15f);
            if (collider.Raycast(new Ray(center, forward), out var torso, radius + ContactTolerance))
            {
                float bodyDepth = Vector3.Dot(torso.point - body, forward);
                shift += forward * Mathf.Clamp(bodyDepth - .19f, -.25f, .32f);
            }
            Transform surface = collider.transform;
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
                collider.Raycast(new Ray(origin, forward), out hit, 1.2f) &&
                Vector3.Dot(hit.normal, forward) <= -.35f;
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

        internal bool CanGrab(SquadCharacterController actor, bool requireLocalDriver, out Pose pose) =>
            TryResolveGrab(actor, requireLocalDriver, out pose, out _, out _, out _);

        internal bool TryResolveGrab(SquadCharacterController actor, bool requireLocalDriver,
            out Pose pose, out Collider surface, out Vector3 normal, out ClingGripTargets targets)
        {
            pose = default;
            surface = null;
            normal = default;
            targets = default;
            if (actor == null || actor.CurrentHp <= 0 || actor.IsMovementInputSuppressed ||
                actor.IsFlightActive || !isActiveAndEnabled || string.IsNullOrWhiteSpace(pointId)) return false;
            Vector3 feet = actor.transform.position;
            bool nearAnchor = IsNear(feet);
            if (!nearAnchor && !grabAlongSurface) return false;
            var abilities = AbilityService.Instance;
            var bridge = actor.GetComponent<LitOpsiveLocomotionBridge>();
            if (abilities == null || !abilities.HasEquipped(actor.gameObject, abilities.Resolve("grimpeur")) ||
                bridge == null || bridge.IsInputSuppressedByUcc || bridge.IsCombatLockActive ||
                bridge.IsCinematicMotionSessionActive || requireLocalDriver && !bridge.IsDriving) return false;
            // The Seuil beam is moved by Transform each frame; the ray and swept capsule
            // must see the same current collider pose.
            GetBody(actor, out Vector3 offset, out float radius, out float height);
            Physics.SyncTransforms();
            var localCenter = Quaternion.Inverse(actor.transform.rotation) * offset;
            Pose bestPose = default;
            Collider bestSurface = null;
            Vector3 bestNormal = default;
            ClingGripTargets bestTargets = default;
            float best = float.PositiveInfinity;
            if (!actor.TryGetComponent(out PlayerAnimationController animation) || !animation.CanPresentCling) return false;
            Vector2 limbReach = GetLimbReach(animation.Animator, height);
            if (grabAlongSurface)
            {
                Evaluate(supportSurface);
                if (contactSurfaces != null)
                    foreach (var collider in contactSurfaces)
                        if (collider != supportSurface) Evaluate(collider);
                pose = bestPose;
                surface = bestSurface;
                normal = bestNormal;
                targets = bestTargets;
                return surface != null;
            }

            if (!nearAnchor || !TryGetHoldPose(actor, out pose) ||
                (feet - pose.position).sqrMagnitude > contactRadius * contactRadius ||
                !IsClear(feet, pose, actor)) return false;
            surface = supportSurface;
            normal = -(pose.rotation * Vector3.forward);
            return ClingGripTargets.TryBuild(surface, pose, pose.position + pose.rotation * localCenter,
                normal, radius, height, limbReach, out targets);

            void Evaluate(Collider candidate)
            {
                Pose fitted;
                Vector3 facing;
                ClingGripTargets anchors;
                // A support directly under the player's feet takes precedence over competing flanks.
                bool top = TryFindTopBrace(candidate, feet, offset, localCenter, radius, height, limbReach, actor.transform.forward,
                    p => IsClear(feet, p, actor), out fitted, out facing, out anchors);
                if (!top && !TryFindCapsuleContact(candidate, feet, offset, localCenter, radius, height, actor.transform.forward,
                        p => IsClear(feet, p, actor, candidate), out fitted, out facing, out anchors,
                        (Pose proposed, Vector3 n, out Pose safe, out ClingGripTargets contacts) =>
                            TrySeatOnSurface(candidate, proposed, n, feet, localCenter, radius, height, limbReach,
                                p => IsClear(feet, p, actor, candidate), out safe, out contacts))) return;
                float distance = (fitted.position - feet).sqrMagnitude - (top ? MaxMagnetShift * MaxMagnetShift : 0f);
                if (distance >= best) return;
                best = distance;
                bestPose = fitted;
                bestSurface = candidate;
                bestNormal = facing;
                bestTargets = anchors;
            }
        }

        private static readonly float[] BraceAngles = { 0f, 45f, -45f, 90f, -90f, 135f, -135f, 180f };
        private static readonly float[] BraceSteps = { 0f, .16f, -.16f, .31f, -.31f };

        internal static bool TryFindTopBrace(Collider surface, Vector3 feet, Vector3 offset, Vector3 localCenter, float radius, float height,
            Vector2 limbReach, Vector3 look, System.Predicate<Pose> isClear, out Pose pose, out Vector3 normal,
            out ClingGripTargets targets)
        {
            pose = default;
            normal = default;
            targets = default;
            if (surface == null || !surface.enabled || surface.isTrigger || !surface.gameObject.activeInHierarchy) return false;
            Vector3 bottom = feet + offset - Vector3.up * (height * .5f);
            const float lift = .16f;
            if (!surface.Raycast(new Ray(bottom + Vector3.up * lift, Vector3.down), out var first, lift + .15f) ||
                first.normal.y < .65f) return false;
            look = Vector3.ProjectOnPlane(look, Vector3.up).normalized;
            if (look.sqrMagnitude < .5f) look = Vector3.forward;
            foreach (float step in BraceSteps)
            foreach (float angle in BraceAngles)
            {
                Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * look;
                var origin = first.point + direction * step + Vector3.up * lift;
                if (!surface.Raycast(new Ray(origin, Vector3.down), out var contact, lift + .18f) ||
                    contact.normal.y < .65f) continue;
                var rotation = Quaternion.LookRotation(direction, Vector3.up);
                // For slopes, keep the lower sphere of the upright capsule outside the support.
                var adjustedBottom = contact.point + Vector3.up * (SurfaceClearance + (1f / contact.normal.y - 1f) * radius);
                var p = new Pose(adjustedBottom + Vector3.up * (height * .5f) - rotation * localCenter, rotation);
                if ((p.position - feet).sqrMagnitude > MaxMagnetShift * MaxMagnetShift ||
                    !ClingGripTargets.TryBuildTop(surface, p, contact.point, contact.normal, height, limbReach, out targets) ||
                    !isClear(p)) continue;
                pose = p;
                normal = contact.normal;
                return true;
            }
            return false;
        }

        private static Vector2 GetLimbReach(Animator animator, float height)
        {
            float scale = height / 1.8f;
            float arm = Mathf.Min(Span(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, .62f),
                Span(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, .62f));
            float leg = Mathf.Min(Span(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, .83f),
                Span(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, .83f));
            return new Vector2(arm, leg);

            float Span(HumanBodyBones start, HumanBodyBones middle, HumanBodyBones end, float fallback)
            {
                var a = animator.GetBoneTransform(start);
                var b = animator.GetBoneTransform(middle);
                var c = animator.GetBoneTransform(end);
                return a == null || b == null || c == null ? fallback * scale :
                    (Vector3.Distance(a.position, b.position) + Vector3.Distance(b.position, c.position)) * .97f;
            }
        }

        private static readonly Vector2[] SeatOffsets =
        {
            Vector2.zero, new(.13f, 0f), new(-.13f, 0f), new(0f, .16f), new(0f, -.16f),
            new(.26f, 0f), new(-.26f, 0f), new(.16f, .16f), new(-.16f, .16f),
            new(.16f, -.16f), new(-.16f, -.16f), new(0f, .30f), new(0f, -.30f),
            new(.38f, 0f), new(-.38f, 0f), new(.25f, .27f), new(-.25f, .27f),
            new(.25f, -.27f), new(-.25f, -.27f)
        };

        private static bool TrySeatOnSurface(Collider surface, Pose proposed, Vector3 normal, Vector3 feet,
            Vector3 localCenter, float radius, float height, Vector2 limbReach, System.Predicate<Pose> isClear,
            out Pose safe, out ClingGripTargets contacts)
        {
            Vector3 right = proposed.rotation * Vector3.right;
            Vector3 up = Vector3.ProjectOnPlane(Vector3.up, normal).normalized;
            foreach (var offset in SeatOffsets)
            {
                var p = new Pose(proposed.position + right * offset.x + up * offset.y, proposed.rotation);
                if ((p.position - feet).sqrMagnitude > MaxMagnetShift * MaxMagnetShift ||
                    !ClingGripTargets.TryBuild(surface, p, p.position + p.rotation * localCenter, normal, radius, height,
                        limbReach, out contacts) || !isClear(p)) continue;
                safe = p;
                return true;
            }
            safe = default;
            contacts = default;
            return false;
        }

        private delegate bool FitGrip(Pose proposed, Vector3 normal, out Pose fitted, out ClingGripTargets targets);

        private static readonly RaycastHit[] ContactHits = new RaycastHit[16];

        internal static bool TryFindContactPose(Collider surface, Vector3 feet, Vector3 offset, Vector3 localCenter,
            float radius, float height, Vector3 referenceForward, System.Predicate<Pose> isClear, out Pose pose) =>
            TryFindCapsuleContact(surface, feet, offset, localCenter, radius, height, referenceForward, isClear, out pose, out _, out _);

        private static bool TryFindCapsuleContact(Collider surface, Vector3 feet, Vector3 offset, Vector3 localCenter,
            float radius, float height, Vector3 referenceForward, System.Predicate<Pose> isClear, out Pose pose, out Vector3 normal,
            out ClingGripTargets targets, FitGrip fitGrip = null)
        {
            pose = default;
            normal = default;
            targets = default;
            if (surface == null || !surface.enabled || !surface.gameObject.activeInHierarchy || surface.isTrigger) return false;
            Vector3 center = feet + offset;
            float reach = fitGrip != null ? MagnetReach : .25f;
            float broadReach = height * .5f + reach;
            if (surface.bounds.SqrDistance(center) > broadReach * broadReach) return false;
            Vector3 spine = Vector3.up * Mathf.Max(0f, height * .5f - radius);
            Vector3 forward = Vector3.ProjectOnPlane(referenceForward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .5f) forward = Vector3.forward;
            float best = MaxMagnetShift * MaxMagnetShift;
            Pose bestPose = default;
            Vector3 bestNormal = default;
            ClingGripTargets bestTargets = default;
            // Shape casts cover the full torso and both ends of the capsule. A smaller
            // second pass can recover a real contact when a moving support already overlaps.
            int fitAttempts = 0;
            for (int pass = 0; pass < 2; pass++)
            {
                float castRadius = radius * (pass == 0 ? .90f : .55f);
                float padding = radius - castRadius;
                for (int i = 0; i < 17; i++)
                {
                    Vector3 direction = i == 16 ? Vector3.up : Quaternion.AngleAxis(i % 8 * 45f, Vector3.up) * forward;
                    if (i >= 8 && i < 16) direction = (direction + Vector3.up).normalized;
                    int mask = 1 << surface.gameObject.layer;
                    int count = Physics.CapsuleCastNonAlloc(center - spine, center + spine, castRadius,
                        direction, ContactHits, reach + padding, mask, QueryTriggerInteraction.Ignore);
                    var hits = count < ContactHits.Length ? ContactHits : Physics.CapsuleCastAll(center - spine, center + spine,
                        castRadius, direction, reach + padding, mask, QueryTriggerInteraction.Ignore);
                    if (hits != ContactHits) count = hits.Length;
                    for (int h = 0; h < count; h++) Evaluate(hits[h], direction, padding);
                }
                if (bestNormal != Vector3.zero) break;
            }
            pose = bestPose;
            normal = bestNormal;
            targets = bestTargets;
            return normal != Vector3.zero;

            void Evaluate(RaycastHit hit, Vector3 direction, float padding)
            {
                if (hit.collider != surface || hit.distance <= .0001f || hit.normal.y > .65f) return;
                if (fitGrip != null && (Mathf.Abs(hit.normal.y) > .72f || fitAttempts >= 18)) return;
                Vector3 facing = Vector3.ProjectOnPlane(-hit.normal, Vector3.up);
                Quaternion rotation = Quaternion.LookRotation(facing.sqrMagnitude > .025f ? facing : forward, Vector3.up);
                // Correct from cast contact to the full-sized capsule, not to a model pivot or marker.
                float extra = padding + SurfaceClearance;
                for (int step = 0; step < 3; step++, extra += .065f)
                {
                    var candidate = new Pose(center + direction * hit.distance + hit.normal * extra - rotation * localCenter, rotation);
                    ClingGripTargets fittedTargets = default;
                    if (fitGrip != null)
                    {
                        if (fitAttempts++ >= 18) break;
                        if (!fitGrip(candidate, hit.normal, out candidate, out fittedTargets)) continue;
                    }
                    else if (!isClear(candidate)) continue;
                    float distance = (candidate.position - feet).sqrMagnitude;
                    if (distance >= best) continue;
                    best = distance;
                    bestPose = candidate;
                    bestNormal = hit.normal;
                    bestTargets = fittedTargets;
                    break;
                }
            }
        }

        private static bool GetBody(SquadCharacterController actor, out Vector3 offset, out float radius, out float height)
        {
            // UCC uses a child capsule on Lucian, not a collider on the player root.
            var locomotion = actor.GetComponent<UltimateCharacterLocomotion>();
            var colliders = locomotion != null ? locomotion.Colliders : null;
            for (int i = 0; colliders != null && i < locomotion.ColliderCount && i < colliders.Length; i++)
            {
                if (colliders[i] is not CapsuleCollider capsule || capsule.direction != 1 ||
                    !capsule.enabled || capsule.isTrigger || !capsule.gameObject.activeInHierarchy) continue;
                Vector3 scale = capsule.transform.lossyScale;
                radius = capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
                height = Mathf.Max(capsule.height * Mathf.Abs(scale.y), radius * 2f);
                offset = capsule.transform.TransformPoint(capsule.center) - actor.transform.position;
                return true;
            }
            if (actor.TryGetLocomotionCapsule(out Vector3 center, out radius, out height))
            {
                offset = center - actor.transform.position;
                return true;
            }
            offset = Vector3.up * .9f;
            radius = .3f;
            height = 1.8f;
            return false;
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
