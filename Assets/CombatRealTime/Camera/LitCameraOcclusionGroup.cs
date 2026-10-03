using System.Collections.Generic;
using UnityEngine;

/// <summary>Explicit local presentation only: colliders/materials remain untouched.</summary>
[DisallowMultipleComponent]
public sealed class LitCameraOcclusionGroup : MonoBehaviour
{
    [SerializeField] private Renderer[] renderers = new Renderer[0];
    private readonly HashSet<object> owners = new HashSet<object>();
    private bool[] previous;
    public void SetHidden(object owner, bool hidden)
    {
        if (hidden)
        {
            if (!owners.Add(owner) || owners.Count != 1) return;
            previous = new bool[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                previous[i] = renderers[i].forceRenderingOff;
                renderers[i].forceRenderingOff = true;
            }
        }
        else if (owners.Remove(owner) && owners.Count == 0) Restore();
    }
    public void Configure(Renderer[] explicitRenderers) { Restore(); owners.Clear(); renderers = explicitRenderers; }
    private void Restore()
    {
        for (int i = 0; previous != null && i < previous.Length && i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].forceRenderingOff = previous[i];
        previous = null;
    }
    private void OnDisable() { Restore(); owners.Clear(); }
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1, .6f, 0);
        foreach (Renderer item in renderers) if (item != null) Gizmos.DrawWireCube(item.bounds.center, item.bounds.size);
    }
}
