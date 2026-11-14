using UnityEngine;

public static class LitTacticalCameraMath
{
    public static float ResolveOrbitFieldOfView(float distance, Vector2 limits, float closeFov, float farFov)
    {
        float far = Mathf.Clamp(farFov, 20f, 60f);
        float near = Mathf.Clamp(closeFov, 20f, far);
        if (limits.y <= limits.x) return far;
        return Mathf.SmoothStep(near, far, Mathf.InverseLerp(limits.x, limits.y, distance));
    }

    // Root-owned target: no animated bone and no feedback from the camera's corrected rotation.
    public static Vector3 StableFollowAnchor(Vector3 rootPosition, Quaternion rootRotation, Quaternion orbitRotation, Vector3 offset)
        => rootPosition + rootRotation * Vector3.up * offset.y +
            orbitRotation * Vector3.right * offset.x + orbitRotation * Vector3.forward * offset.z;
    // Positive zoom input decreases the view's distance.
    public static float TriggerZoomDelta(float left, float right, float speed, float dt)
        => (Mathf.Clamp01(left) - Mathf.Clamp01(right)) * Mathf.Max(0, speed) * Mathf.Max(0, dt);
    public static bool TryMovementBasis(Quaternion cameraRotation, out Vector3 forward, out Vector3 right)
    {
        forward = Vector3.ProjectOnPlane(cameraRotation * Vector3.forward, Vector3.up);
        right = Vector3.ProjectOnPlane(cameraRotation * Vector3.right, Vector3.up);
        if (forward.sqrMagnitude < .0001f) forward = Vector3.Cross(right, Vector3.up);
        if (forward.sqrMagnitude < .0001f) { right = Vector3.zero; return false; }
        forward.Normalize();
        right = Vector3.Cross(Vector3.up, forward).normalized;
        return true;
    }

    public static Vector2 ScreenToWorldInput(Vector2 input, Vector3 forward, Vector3 right)
    {
        input = Vector2.ClampMagnitude(input, 1);
        Vector3 world = right * input.x + forward * input.y;
        return new Vector2(world.x, world.z);
    }

    public static Vector3 ClampCameraDistance(Vector3 camera, Vector3 player, float radius)
        => player + Vector3.ClampMagnitude(camera - player, Mathf.Max(0, radius));

    public static Vector3 SlideMotion(Vector3 motion, Vector3 surfaceNormal)
        => Vector3.ProjectOnPlane(motion, surfaceNormal);

    public static bool IsOrbitFloor(Vector3 normal) => normal.y >= .45f;

    public static float LimitLowOrbitPitch(float pitch, float distance, float pivotHeightAboveFeet, float clearance)
    {
        float ratio = (clearance - pivotHeightAboveFeet) / Mathf.Max(.01f, distance);
        return Mathf.Max(pitch, Mathf.Asin(Mathf.Clamp(ratio, -1f, 0f)) * Mathf.Rad2Deg);
    }

    // Lift only when the requested pose would overlap the floor. Keep the
    // horizontal orbit intact, and never search above the current boom height.
    public static Vector3 KeepOrbitAboveGround(Vector3 pivot, Vector3 desired, float radius, float minimumHeight, int mask,
        System.Predicate<Collider> ignore, ref RaycastHit[] hits)
    {
        float clearance = Mathf.Max(minimumHeight, Mathf.Max(.01f, radius) + .05f);
        float top = Mathf.Max(pivot.y, desired.y + clearance);
        float bottom = desired.y - clearance;
        Vector3 origin = new Vector3(desired.x, top, desired.z);
        int count = LitTacticalMaskQueries.Raycast(ref hits, origin, Vector3.down, top - bottom, mask);
        for (int i = 0; i < count; i++)
        {
            var hit = hits[i];
            if (!IsOrbitFloor(hit.normal) || (ignore != null && ignore(hit.collider))) continue;
            // On a slope the centre needs more vertical clearance than on flat ground.
            float safeHeight = hit.point.y + Mathf.Max(minimumHeight, (Mathf.Max(.01f, radius) + .05f) / hit.normal.y);
            desired.y = Mathf.Max(desired.y, safeHeight);
        }
        return desired;
    }

    public static Vector3 ClampPivot(Vector3 pivot, Vector3 player, float radius)
    {
        Vector3 delta = pivot - player;
        delta.y = 0;
        Vector3 limited = Vector3.ClampMagnitude(delta, Mathf.Max(0, radius));
        return new Vector3(player.x + limited.x, pivot.y, player.z + limited.z);
    }

    public static Vector3 SlowOutwardPan(Vector3 motion, Vector3 offset, float radius, float slowZone)
    {
        offset.y = 0;
        if (Vector3.Dot(motion, offset) <= 0) return motion;
        float factor = Mathf.Clamp01((radius - offset.magnitude) / Mathf.Max(.01f, slowZone));
        return motion * factor;
    }
}
