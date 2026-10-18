using UnityEngine;

/// <summary>Owns only the rendering camera's near plane, never its pose or projection type.</summary>
public sealed class LitTacticalNearClip
{
    private readonly Camera camera;
    private float baseline, velocity;
    private bool ownsPlane;
    public float Baseline => ownsPlane ? baseline : camera != null ? camera.nearClipPlane : 0;
    public float Effective => camera != null ? camera.nearClipPlane : 0;

    public LitTacticalNearClip(Camera camera) { this.camera = camera; }

    public void Restore()
    {
        if (ownsPlane && camera != null) camera.nearClipPlane = baseline;
        ownsPlane = false;
        velocity = 0;
    }

    public void Apply(float playerDepth, float bodyDepth, float margin, float safety, float smoothTime, float dt)
    {
        if (camera == null) return;
        if (!ownsPlane) { baseline = camera.nearClipPlane; ownsPlane = true; velocity = 0; }
        float target = ResolveTarget(baseline, camera.farClipPlane, playerDepth, bodyDepth, margin, safety);
        float current = camera.nearClipPlane;
        // Retraction is a visibility safety operation, not a second delayed filter.
        if (target <= current) { camera.nearClipPlane = target; velocity = 0; }
        else camera.nearClipPlane = Mathf.Min(target, Mathf.SmoothDamp(current, target, ref velocity,
            Mathf.Max(.001f, smoothTime), Mathf.Infinity, Mathf.Max(0, dt)));
    }

    public static float ResolveTarget(float baseline, float far, float playerDepth, float bodyDepth, float margin, float safety)
    {
        if (playerDepth <= 0 || float.IsInfinity(bodyDepth) || float.IsNaN(bodyDepth)) return baseline;
        float target = Mathf.Min(playerDepth - Mathf.Max(0, margin), bodyDepth - Mathf.Max(0, safety));
        return Mathf.Max(baseline, Mathf.Min(target, far - .01f));
    }

    public static float NearestDepth(Bounds localBounds, Transform root, Vector3 cameraPosition, Vector3 forward)
    {
        float depth = float.PositiveInfinity;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = localBounds.center + Vector3.Scale(localBounds.extents,
                new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            depth = Mathf.Min(depth, Vector3.Dot(root.TransformPoint(corner) - cameraPosition, forward));
        }
        return depth;
    }

    // Capture once at bind, in root space: no renderer bounds, animated bones or per-frame physics poses.
    public static bool TryCaptureBody(Transform root, out Bounds body, Collider[] motorColliders = null)
    {
        body = default;
        if (root == null) return false;
        bool found = false;
        foreach (var collider in motorColliders ?? root.GetComponentsInChildren<Collider>(false))
        {
            if (collider == null || !collider.enabled || collider.isTrigger) continue;
            if (collider.attachedRigidbody != null && collider.attachedRigidbody.transform != root) continue;
            bool animated = false;
            for (var parent = collider.transform; parent != null && parent != root; parent = parent.parent)
                if (parent.GetComponent<Animator>() != null) { animated = true; break; }
            if (animated) continue;
            Bounds shape;
            if (collider is CapsuleCollider capsule)
            {
                Vector3 size = Vector3.one * capsule.radius * 2;
                size[capsule.direction] = Mathf.Max(capsule.height, capsule.radius * 2);
                shape = new Bounds(capsule.center, size);
            }
            else if (collider is BoxCollider box) shape = new Bounds(box.center, box.size);
            else if (collider is SphereCollider sphere) shape = new Bounds(sphere.center, Vector3.one * sphere.radius * 2);
            else continue;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = shape.center + Vector3.Scale(shape.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 point = root.InverseTransformPoint(collider.transform.TransformPoint(corner));
                if (!found) { body = new Bounds(point, Vector3.zero); found = true; }
                else body.Encapsulate(point);
            }
        }
        return found;
    }
}
