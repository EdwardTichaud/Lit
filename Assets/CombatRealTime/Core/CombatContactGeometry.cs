using UnityEngine;

public static class CombatContactGeometry
{
    public static bool HasContact(Transform attacker, Transform target, float reach, float arc, float heightTolerance = float.PositiveInfinity)
    {
        if (attacker == null || target == null || Mathf.Abs(target.position.y - attacker.position.y) > heightTolerance) return false;
        Vector3 delta = Vector3.ProjectOnPlane(target.position - attacker.position, Vector3.up);
        if (delta.magnitude > reach || Vector3.Angle(attacker.forward, delta) > arc * .5f) return false;
        return !Physics.Linecast(attacker.position + Vector3.up, target.position + Vector3.up,
            LayerMask.GetMask("Default", "Ground", "Obstacle", "CameraObstruction"), QueryTriggerInteraction.Ignore);
    }
}
