using UnityEngine;

/// <summary>
/// Shared authoring contract for the actors placed under AnimationLab anchors.
/// A preview actor mirrors gameplay: its root owns the Animator and the
/// skeleton remains beneath it. Baking therefore never derives a stage pose
/// from a mesh or an arbitrary child Animator.
/// </summary>
public static class CombatCinematicAuthoringActorResolver
{
    public static Transform ResolveActorRoot(Transform explicitRoot, Transform anchor, Animator fallbackAnimator)
    {
        if (explicitRoot != null)
        {
            return explicitRoot;
        }

        CombatActorAnimationRoot contract = fallbackAnimator != null
            ? fallbackAnimator.GetComponentInParent<CombatActorAnimationRoot>()
            : null;
        if (contract != null)
        {
            return contract.ActorRoot;
        }

        if (fallbackAnimator == null)
        {
            return null;
        }

        Transform candidate = fallbackAnimator.transform;
        if (anchor == null || candidate == anchor)
        {
            return candidate;
        }

        while (candidate.parent != null && candidate.parent != anchor)
        {
            candidate = candidate.parent;
        }

        return candidate;
    }

    public static Animator ResolveAnimator(Transform actorRoot, Animator fallbackAnimator)
    {
        if (actorRoot != null)
        {
            CombatActorAnimationRoot contract = actorRoot.GetComponent<CombatActorAnimationRoot>();
            if (contract != null && contract.Animator != null)
            {
                return contract.Animator;
            }

            Animator rootAnimator = actorRoot.GetComponent<Animator>();
            if (rootAnimator != null)
            {
                return rootAnimator;
            }
        }

        return fallbackAnimator;
    }

    public static bool ValidateRootAnimator(
        Transform actorRoot,
        Transform anchor,
        Animator animator,
        string label,
        out string error)
    {
        error = null;
        if (actorRoot == null || animator == null)
        {
            error = label + " preview root ou Animator manquant.";
            return false;
        }

        if (anchor != null && actorRoot != anchor && !actorRoot.IsChildOf(anchor))
        {
            error = label + " preview root doit etre enfant de son anchor de plateau.";
            return false;
        }

        if (animator.transform != actorRoot)
        {
            error = label + " preview doit porter son Animator sur son ActorRoot, comme Juggernaut_Combat.";
            return false;
        }

        if (animator.runtimeAnimatorController == null)
        {
            error = label + " preview Animator n'a pas de RuntimeAnimatorController.";
            return false;
        }

        return true;
    }
}
