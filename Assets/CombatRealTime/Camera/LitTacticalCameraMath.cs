using UnityEngine;

public static class LitTacticalCameraMath
{
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
