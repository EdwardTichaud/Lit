using UnityEngine;

/// <summary>Non-allocating after warm-up; never silently truncate a stack of occluders.</summary>
public static class LitTacticalMaskQueries
{
    public static int Raycast(ref RaycastHit[] hits, Vector3 origin, Vector3 direction, float distance, int mask)
    {
        if (hits == null || hits.Length == 0) hits = new RaycastHit[128];
        int count;
        while ((count = Physics.RaycastNonAlloc(origin, direction, hits, distance, mask, QueryTriggerInteraction.Ignore)) == hits.Length)
            System.Array.Resize(ref hits, checked(hits.Length * 2));
        return count;
    }
    public static int Overlap(ref Collider[] hits, Vector3 position, float radius, int mask)
    {
        if (hits == null || hits.Length == 0) hits = new Collider[64];
        int count;
        while ((count = Physics.OverlapSphereNonAlloc(position, radius, hits, mask, QueryTriggerInteraction.Ignore)) == hits.Length)
            System.Array.Resize(ref hits, checked(hits.Length * 2));
        return count;
    }
}
