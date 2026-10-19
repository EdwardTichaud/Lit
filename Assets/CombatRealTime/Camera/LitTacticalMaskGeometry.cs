using UnityEngine;

public static class LitTacticalMaskGeometry
{
    /// <summary>Conservative screen-circle vs bounds, including colliderless meshes and camera-inside-wall.</summary>
    public static bool IntersectsOpening(Camera camera, Bounds bounds, Vector3 target, float radius)
    {
        Vector3 projectedTarget = camera.WorldToViewportPoint(target);
        if (projectedTarget.z <= camera.nearClipPlane) return false;
        float near = float.PositiveInfinity, far = float.NegativeInfinity;
        Vector2 min = Vector2.one * float.PositiveInfinity, max = Vector2.one * float.NegativeInfinity;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = bounds.center + Vector3.Scale(bounds.extents,
                new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            Vector3 point = camera.WorldToViewportPoint(corner);
            near = Mathf.Min(near, point.z); far = Mathf.Max(far, point.z);
            min = Vector2.Min(min, point); max = Vector2.Max(max, point);
        }
        if (far <= camera.nearClipPlane || near >= projectedTarget.z - .05f) return false;
        if (near <= camera.nearClipPlane) return true;
        Vector2 closest = new Vector2(Mathf.Clamp(projectedTarget.x, min.x, max.x), Mathf.Clamp(projectedTarget.y, min.y, max.y));
        Vector2 delta = closest - new Vector2(projectedTarget.x, projectedTarget.y);
        delta.x *= camera.aspect;
        return delta.sqrMagnitude <= radius * radius;
    }
}
