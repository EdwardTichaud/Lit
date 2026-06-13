using System.Collections.Generic;
using UnityEngine;

public static class RuntimeOutlineSelectionManager
{
    private static readonly List<RuntimeOutlineTarget> ActiveTargets = new List<RuntimeOutlineTarget>();
    private static readonly List<RuntimeOutlineTarget> CandidateTargets = new List<RuntimeOutlineTarget>();
    private static Object activeOwner;
    private static ICharacterDetectedInteractable activeInteractable;

    public static ICharacterDetectedInteractable ActiveInteractable => activeInteractable;

    public static void SetActiveInteractable(ICharacterDetectedInteractable interactable)
    {
        SetActiveInteractable(null, interactable);
    }

    public static void SetActiveInteractable(Object owner, ICharacterDetectedInteractable interactable)
    {
        SetActiveComponent(owner, interactable as Component);
        activeInteractable = interactable;
    }

    public static bool IsActiveInteractable(ICharacterDetectedInteractable interactable)
    {
        return interactable != null && ReferenceEquals(activeInteractable, interactable);
    }

    public static void RefreshActiveInteractable()
    {
        if (activeInteractable == null)
        {
            return;
        }

        CandidateTargets.Clear();
        RuntimeOutlineUtility.CollectOutlineTargets(activeInteractable as Component, CandidateTargets, ensureTargets: true);
        SetActiveTargets(activeOwner, CandidateTargets);
    }

    public static void SetActiveComponent(Component component)
    {
        SetActiveComponent(null, component);
    }

    public static void SetActiveComponent(Object owner, Component component)
    {
        activeInteractable = null;
        CandidateTargets.Clear();
        RuntimeOutlineUtility.CollectOutlineTargets(component, CandidateTargets, ensureTargets: true);
        SetActiveTargets(owner, CandidateTargets);
    }

    public static void Clear()
    {
        activeInteractable = null;
        SetActiveTargets(null, null);
    }

    public static void Clear(Object owner)
    {
        if (activeOwner != null && owner != null && activeOwner != owner)
        {
            return;
        }

        activeInteractable = null;
        SetActiveTargets(owner, null);
    }

    private static void SetActiveTargets(Object owner, List<RuntimeOutlineTarget> nextTargets)
    {
        bool hasNextTargets = nextTargets != null && nextTargets.Count > 0;
        if (hasNextTargets)
        {
            activeOwner = owner;
        }
        else if (activeOwner != null && owner != null && activeOwner != owner)
        {
            return;
        }

        for (int i = ActiveTargets.Count - 1; i >= 0; i--)
        {
            RuntimeOutlineTarget target = ActiveTargets[i];
            if (target == null)
            {
                ActiveTargets.RemoveAt(i);
                continue;
            }

            if (nextTargets == null || !nextTargets.Contains(target))
            {
                target.SetOutlined(false);
                ActiveTargets.RemoveAt(i);
            }
        }

        if (!hasNextTargets)
        {
            activeOwner = null;
            return;
        }

        for (int i = 0; i < nextTargets.Count; i++)
        {
            RuntimeOutlineTarget target = nextTargets[i];
            if (target == null)
            {
                continue;
            }

            if (!ActiveTargets.Contains(target))
            {
                ActiveTargets.Add(target);
            }

            target.SetOutlined(true);
        }
    }
}
