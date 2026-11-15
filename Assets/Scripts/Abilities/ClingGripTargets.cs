using UnityEngine;

namespace Lit.Traversal
{
    /// <summary>The four verified IK targets and the hip anchor, in support-local space.</summary>
    internal struct ClingGripTargets
    {
        public Vector3 Hips, LeftHand, RightHand, LeftFoot, RightFoot;
        public bool TopBrace;

        public Vector3 Limb(int index) => index switch
        {
            0 => LeftHand, 1 => RightHand, 2 => LeftFoot, _ => RightFoot
        };

        private static readonly float[] Heights = { 0f, .10f, -.10f };
        private static readonly float[] Widths = { .22f, .16f, .28f, .13f };

        internal static bool TryBuildTop(Collider surface, Pose pose, Vector3 bottom, Vector3 normal, float height,
            Vector2 limbReach, out ClingGripTargets targets)
        {
            targets = default;
            float scale = height / 1.8f;
            Vector3 forward = Vector3.ProjectOnPlane(pose.rotation * Vector3.forward, normal).normalized;
            Vector3 right = Vector3.Cross(normal, forward).normalized;
            Vector3 hips = bottom + (normal * .29f - forward * .075f) * scale;
            if (!Find(true, -1, out Vector3 lh) || !Find(true, 1, out Vector3 rh) ||
                !Find(false, -1, out Vector3 lf) || !Find(false, 1, out Vector3 rf)) return false;
            var support = surface.transform;
            targets = new ClingGripTargets
            {
                TopBrace = true,
                Hips = support.InverseTransformPoint(hips),
                LeftHand = support.InverseTransformPoint(lh), RightHand = support.InverseTransformPoint(rh),
                LeftFoot = support.InverseTransformPoint(lf), RightFoot = support.InverseTransformPoint(rf)
            };
            return true;

            bool Find(bool hand, float sign, out Vector3 goal)
            {
                Vector3 joint = hips + (right * (sign * (hand ? .16f : .10f)) +
                    (hand ? forward * .34f + normal * .08f : Vector3.zero)) * scale;
                float reach = hand ? limbReach.x : limbReach.y;
                foreach (float width in Widths)
                foreach (float delta in Heights)
                {
                    Vector3 origin = bottom + (normal * .32f + right * (sign * width) +
                        forward * ((hand ? .42f : -.41f) + delta)) * scale;
                    if (!surface.Raycast(new Ray(origin, -normal), out var hit, .57f * scale) ||
                        Vector3.Dot(hit.normal, normal) < .70f) continue;
                    goal = hit.point + hit.normal * ((hand ? .04f : .07f) * scale);
                    if ((goal - joint).sqrMagnitude <= reach * reach) return true;
                }
                goal = default;
                return false;
            }
        }

        internal static bool TryBuild(Collider surface, Pose pose, Vector3 center, Vector3 normal, float radius, float height,
            Vector2 limbReach, out ClingGripTargets targets)
        {
            targets = default;
            if (surface == null || !surface.enabled || surface.isTrigger || !surface.gameObject.activeInHierarchy) return false;
            // The upright climb pose cannot place both its raised hands and lower feet on a flat ceiling.
            if (Mathf.Abs(normal.y) > .72f) return false;
            float scale = height / 1.8f;
            Vector3 forward = pose.rotation * Vector3.forward;
            Vector3 right = pose.rotation * Vector3.right;
            Vector3 hips = center - Vector3.up * (.12f * scale) + forward * Mathf.Clamp(radius - .22f * scale, 0f, .24f * scale);
            Vector3 toward = -normal.normalized;
            if (!Find(true, -1f, out var lh) || !Find(true, 1f, out var rh) ||
                !Find(false, -1f, out var lf) || !Find(false, 1f, out var rf)) return false;
            if (Vector3.Dot(rh - lh, right) < .16f * scale || Vector3.Dot(rf - lf, right) < .16f * scale ||
                Mathf.Min(lh.y, rh.y) - Mathf.Max(lf.y, rf.y) < .45f * scale) return false;
            var t = surface.transform;
            targets = new ClingGripTargets
            {
                Hips = t.InverseTransformPoint(hips),
                LeftHand = t.InverseTransformPoint(lh), RightHand = t.InverseTransformPoint(rh),
                LeftFoot = t.InverseTransformPoint(lf), RightFoot = t.InverseTransformPoint(rf)
            };
            return true;

            bool Find(bool hand, float sign, out Vector3 target)
            {
                target = default;
                Vector3 joint = hips + right * (sign * (hand ? .18f : .10f) * scale) +
                    Vector3.up * (hand ? .38f * scale : 0f);
                float reach = hand ? limbReach.x : limbReach.y;
                foreach (float width in Widths)
                foreach (float y in Heights)
                {
                    var probe = center + Vector3.up * (((hand ? .55f : -.54f) + y) * scale) + right * (sign * width * scale);
                    var origin = probe - toward * (.25f * scale);
                    if (!surface.Raycast(new Ray(origin, toward), out var hit, radius + .48f * scale) ||
                        Vector3.Dot(hit.normal, toward) > -.28f) continue;
                    Vector3 goal = hit.point + hit.normal * ((hand ? .04f : .07f) * scale);
                    if ((goal - joint).sqrMagnitude > reach * reach ||
                        Vector3.Dot(goal - center, right) * sign < .08f * scale) continue;
                    target = goal;
                    return true;
                }
                return false;
            }
        }
    }
}
