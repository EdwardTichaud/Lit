using UnityEngine;

public static class SurfaceResolver
{
    public static SurfaceDefinition Resolve(RaycastHit hit, SurfaceDefinition fallbackSurface = null)
    {
        return TryResolve(hit, out SurfaceDefinition surface) ? surface : fallbackSurface;
    }

    public static bool TryResolve(RaycastHit hit, out SurfaceDefinition surface)
    {
        surface = null;
        Collider hitCollider = hit.collider;
        if (hitCollider == null)
        {
            return false;
        }

        SurfaceProvider provider = hitCollider.GetComponent<SurfaceProvider>();
        if (provider != null && provider.Surface != null)
        {
            surface = provider.Surface;
            return true;
        }

        provider = hitCollider.GetComponentInParent<SurfaceProvider>();
        if (provider != null && provider.Surface != null)
        {
            surface = provider.Surface;
            return true;
        }

        return false;
    }
}
