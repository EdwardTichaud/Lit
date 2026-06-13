using System.Collections.Generic;
using UnityEngine;

public static class CameraVisibilityProtection
{
    private sealed class RendererProtection
    {
        public int RefCount;
        public readonly HashSet<Object> Owners = new HashSet<Object>();
    }

    private static readonly Dictionary<Renderer, RendererProtection> ProtectedRenderers =
        new Dictionary<Renderer, RendererProtection>();

    private static readonly List<Renderer> RendererBuffer = new List<Renderer>(32);
    private static readonly List<Renderer> RemovalBuffer = new List<Renderer>(32);

    public static bool HasProtectedRenderers => ProtectedRenderers.Count > 0;

    public static void RegisterObstacle(GameObject root, Object owner, bool includeChildren)
    {
        if (root == null)
        {
            return;
        }

        if (!includeChildren)
        {
            RegisterRenderer(root.GetComponent<Renderer>(), owner);
            return;
        }

        RendererBuffer.Clear();
        root.GetComponentsInChildren(includeInactive: true, RendererBuffer);
        for (int i = 0; i < RendererBuffer.Count; i++)
        {
            RegisterRenderer(RendererBuffer[i], owner);
        }

        RendererBuffer.Clear();
    }

    public static void UnregisterObstacle(GameObject root, Object owner, bool includeChildren)
    {
        if (root == null)
        {
            return;
        }

        if (!includeChildren)
        {
            UnregisterRenderer(root.GetComponent<Renderer>(), owner);
            return;
        }

        RendererBuffer.Clear();
        root.GetComponentsInChildren(includeInactive: true, RendererBuffer);
        for (int i = 0; i < RendererBuffer.Count; i++)
        {
            UnregisterRenderer(RendererBuffer[i], owner);
        }

        RendererBuffer.Clear();
    }

    public static void RegisterRenderer(Renderer renderer, Object owner)
    {
        if (renderer == null)
        {
            return;
        }

        if (!ProtectedRenderers.TryGetValue(renderer, out RendererProtection protection))
        {
            protection = new RendererProtection();
            ProtectedRenderers.Add(renderer, protection);
        }

        if (owner != null)
        {
            protection.Owners.Add(owner);
            protection.RefCount = Mathf.Max(protection.RefCount, protection.Owners.Count);
            return;
        }

        protection.RefCount = Mathf.Max(protection.RefCount + 1, protection.Owners.Count);
    }

    public static void UnregisterRenderer(Renderer renderer, Object owner)
    {
        if (renderer == null || !ProtectedRenderers.TryGetValue(renderer, out RendererProtection protection))
        {
            return;
        }

        if (owner != null && protection.Owners.Remove(owner))
        {
            protection.RefCount = Mathf.Max(0, protection.Owners.Count);
        }
        else
        {
            protection.RefCount = Mathf.Max(0, protection.RefCount - 1);
        }

        if (protection.RefCount == 0 && protection.Owners.Count == 0)
        {
            ProtectedRenderers.Remove(renderer);
        }
    }

    public static bool IsRendererProtected(Renderer renderer)
    {
        if (renderer == null)
        {
            return false;
        }

        PruneNullRenderers();
        return ProtectedRenderers.ContainsKey(renderer);
    }

    public static void ClearOwner(Object owner)
    {
        if (owner == null || ProtectedRenderers.Count == 0)
        {
            return;
        }

        RemovalBuffer.Clear();
        foreach (KeyValuePair<Renderer, RendererProtection> pair in ProtectedRenderers)
        {
            RendererProtection protection = pair.Value;
            if (protection == null)
            {
                RemovalBuffer.Add(pair.Key);
                continue;
            }

            if (protection.Owners.Remove(owner))
            {
                protection.RefCount = Mathf.Max(0, protection.Owners.Count);
            }

            if (pair.Key == null || protection.RefCount == 0 && protection.Owners.Count == 0)
            {
                RemovalBuffer.Add(pair.Key);
            }
        }

        for (int i = 0; i < RemovalBuffer.Count; i++)
        {
            ProtectedRenderers.Remove(RemovalBuffer[i]);
        }

        RemovalBuffer.Clear();
    }

    private static void PruneNullRenderers()
    {
        if (ProtectedRenderers.Count == 0)
        {
            return;
        }

        RemovalBuffer.Clear();
        foreach (KeyValuePair<Renderer, RendererProtection> pair in ProtectedRenderers)
        {
            if (pair.Key == null)
            {
                RemovalBuffer.Add(pair.Key);
            }
        }

        for (int i = 0; i < RemovalBuffer.Count; i++)
        {
            ProtectedRenderers.Remove(RemovalBuffer[i]);
        }

        RemovalBuffer.Clear();
    }
}
