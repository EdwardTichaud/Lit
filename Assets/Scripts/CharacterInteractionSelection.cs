using UnityEngine;

/// <summary>One distance-first order for interaction, prompt and outline selection.</summary>
public static class CharacterInteractionSelection
{
    public static bool IsBetter(float distanceSqr, int priority, int id,
        float bestDistanceSqr, int bestPriority, int bestId)
    {
        int distanceOrder = distanceSqr.CompareTo(bestDistanceSqr);
        if (distanceOrder != 0) return distanceOrder < 0;
        if (priority != bestPriority) return priority > bestPriority;
        return id < bestId;
    }

    public static Vector3 SelectionPoint(Collider collider, Transform anchor, Vector3 rangePoint)
    {
        // A trigger is an access/notification volume, not the object's physical surface.
        // ClosestPoint inside a large portal/flame trigger must not rank it at distance zero.
        return collider != null && collider.isTrigger
            ? anchor != null ? anchor.position : collider.bounds.center
            : rangePoint;
    }

    public static int CollectCandidates(ref Collider[] buffer, Vector3 origin, float radius, int mask)
    {
        if (buffer == null || buffer.Length == 0) buffer = new Collider[128];
        int count;
        // Unity does not order overlap results by distance. A full buffer may omit
        // the closest interactable, especially in densely decorated Maison rooms.
        // Grow only on saturation; subsequent frames reuse the high-water capacity.
        while ((count = Physics.OverlapSphereNonAlloc(origin, radius, buffer, mask, QueryTriggerInteraction.Collide)) == buffer.Length)
            System.Array.Resize(ref buffer, checked(buffer.Length * 2));
        return count;
    }
}
