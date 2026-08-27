using System.Collections.Generic;
using UnityEngine;

public interface IRuntimeOutlineVisibilityGate
{
    bool AllowsRuntimeOutline { get; }
}

public static class RuntimeOutlineUtility
{
    public static int EnsureOutlineTargets(GameObject root)
    {
        if (root == null)
        {
            return 0;
        }

        // An explicit reference deliberately avoids the hierarchy scan. This
        // is the preferred setup for interactables with complex prefabs.
        if (root.TryGetComponent(out RuntimeOutlineRendererReference reference))
        {
            if (reference.outlineRenderer == null)
            {
                return 0;
            }

            EnsureOutlineTarget(reference.outlineRenderer);
            return 1;
        }

        Renderer rootRenderer = root.GetComponent<Renderer>();
        if (rootRenderer != null)
        {
            EnsureOutlineTarget(rootRenderer);
            return 1;
        }

        int ensuredCount = 0;
        Queue<Transform> pendingTransforms = new Queue<Transform>();
        for (int i = 0; i < root.transform.childCount; i++)
        {
            pendingTransforms.Enqueue(root.transform.GetChild(i));
        }

        while (pendingTransforms.Count > 0)
        {
            Transform current = pendingTransforms.Dequeue();
            if (current == null)
            {
                continue;
            }

            Renderer[] renderers = current.GetComponents<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                {
                    continue;
                }

                EnsureOutlineTarget(renderers[i]);
                ensuredCount++;
            }

            for (int i = 0; i < current.childCount; i++)
            {
                pendingTransforms.Enqueue(current.GetChild(i));
            }
        }

        return ensuredCount;
    }

    private static void EnsureOutlineTarget(Renderer renderer)
    {
        if (renderer != null && renderer.GetComponent<RuntimeOutlineTarget>() == null)
        {
            renderer.gameObject.AddComponent<RuntimeOutlineTarget>();
        }
    }

    public static void CollectOutlineTargets(Component owner, List<RuntimeOutlineTarget> results, bool ensureTargets)
    {
        if (results == null)
        {
            return;
        }

        results.Clear();
        if (owner == null)
        {
            return;
        }

        if (owner is IRuntimeOutlineVisibilityGate visibilityGate &&
            !visibilityGate.AllowsRuntimeOutline)
        {
            return;
        }

        GameObject root = owner.gameObject;
        if (root.TryGetComponent(out RuntimeOutlineRendererReference reference))
        {
            Renderer renderer = reference.outlineRenderer;
            if (renderer == null)
            {
                return;
            }

            if (ensureTargets)
            {
                EnsureOutlineTarget(renderer);
            }

            RuntimeOutlineTarget explicitTarget = renderer.GetComponent<RuntimeOutlineTarget>();
            if (explicitTarget != null)
            {
                results.Add(explicitTarget);
            }

            return;
        }

        if (ensureTargets)
        {
            EnsureOutlineTargets(root);
        }

        RuntimeOutlineTarget[] targets = root.GetComponentsInChildren<RuntimeOutlineTarget>(true);
        for (int i = 0; i < targets.Length; i++)
        {
            RuntimeOutlineTarget target = targets[i];
            if (target != null && !results.Contains(target))
            {
                results.Add(target);
            }
        }
    }
}
