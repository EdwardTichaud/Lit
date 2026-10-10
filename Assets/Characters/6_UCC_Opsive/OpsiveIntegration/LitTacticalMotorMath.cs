using UnityEngine;

/// <summary>Pure simulation-space conversions, matching UCC's rotation and motor-force integration.</summary>
public static class LitTacticalMotorMath
{
    public static float ResolveYaw(Quaternion rotation, Vector3 facing)
    {
        Vector3 up = rotation * Vector3.up;
        facing = Vector3.ProjectOnPlane(facing, up);
        if (facing.sqrMagnitude < .0001f) return 0;
        return Vector3.SignedAngle(rotation * Vector3.forward, facing, up);
    }
    public static Quaternion MotorInputRotation(Quaternion current, float deltaYaw, float rotationBlend, float accelerationInfluence)
    {
        Quaternion next = Quaternion.Slerp(current, current * Quaternion.Euler(0, deltaYaw, 0), Mathf.Clamp01(rotationBlend));
        return Quaternion.Slerp(current, next, Mathf.Clamp01(accelerationInfluence));
    }
    public static Vector2 ResolveInput(Vector2 worldInput, float magnitude, Quaternion rotation,
        float deltaYaw, float rotationBlend, float accelerationInfluence)
    {
        if (worldInput.sqrMagnitude < .0001f || magnitude <= 0) return Vector2.zero;
        Quaternion basis = MotorInputRotation(rotation, deltaYaw, rotationBlend, accelerationInfluence);
        Vector3 local = Quaternion.Inverse(basis) * new Vector3(worldInput.x, 0, worldInput.y).normalized;
        Vector2 planar = new Vector2(local.x, local.z);
        return planar.sqrMagnitude < .0001f ? Vector2.zero : planar.normalized * magnitude;
    }
}
