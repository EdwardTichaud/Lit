using UnityEngine;

public static class LitTacticalCameraMath
{
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
