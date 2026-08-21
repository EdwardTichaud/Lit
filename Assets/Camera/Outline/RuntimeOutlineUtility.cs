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

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        int ensuredCount = 0;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (renderer.GetComponent<RuntimeOutlineTarget>() == null)
            {
                renderer.gameObject.AddComponent<RuntimeOutlineTarget>();
            }

            ensuredCount++;
        }

        return ensuredCount;
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

        // GhostController is only passed here after the interaction resolver selected it.
        // Its shader dissolve convention can report the inverse visibility value, so do not
        // suppress the interactable outline for an otherwise valid ghost target.
        if (owner is IRuntimeOutlineVisibilityGate visibilityGate &&
            !(owner is GhostController) &&
            !visibilityGate.AllowsRuntimeOutline)
        {
            return;
        }

        GameObject root = owner.gameObject;
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
