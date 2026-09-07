using System.Collections.Generic;
using UnityEngine;

public interface IRuntimeOutlineVisibilityGate
{
    bool AllowsRuntimeOutline { get; }
}

public static class RuntimeOutlineUtility
{
    /// <summary>
    /// Collects targets authored on the selected renderer hierarchy.
    /// This utility is deliberately passive: it never adds targets at runtime.
    /// </summary>
    public static void CollectOutlineTargets(Component owner, List<RuntimeOutlineTarget> results)
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

            RuntimeOutlineTarget explicitTarget = renderer.GetComponent<RuntimeOutlineTarget>();
            if (explicitTarget != null)
            {
                results.Add(explicitTarget);
            }

            return;
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
