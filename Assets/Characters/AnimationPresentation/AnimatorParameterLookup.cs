using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Per-owner parameter metadata; avoids allocating Animator.parameters on every property write.</summary>
public sealed class AnimatorParameterLookup
{
    private readonly Dictionary<string, AnimatorControllerParameterType> types =
        new Dictionary<string, AnimatorControllerParameterType>(StringComparer.Ordinal);
    private Animator cachedAnimator;
    private RuntimeAnimatorController cachedController, cachedBaseController, mutableOverride;
    private int cachedCount = -1, cachedRevision = -1;
    private static int revision;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession() => revision++;

#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
    private static void ObserveAuthoringChanges()
    {
        UnityEditor.EditorApplication.projectChanged -= InvalidateAuthoring;
        UnityEditor.EditorApplication.projectChanged += InvalidateAuthoring;
    }
    private static void InvalidateAuthoring() => revision++;
#endif

    public bool Has(Animator animator, string name, AnimatorControllerParameterType type)
    {
        if (animator == null || string.IsNullOrWhiteSpace(name)) return false;
        RuntimeAnimatorController controller = animator.runtimeAnimatorController;
        RuntimeAnimatorController baseController = controller;
        while (baseController is AnimatorOverrideController overrides)
            baseController = overrides.runtimeAnimatorController;
        if (cachedController != controller) mutableOverride = null;
        else if (cachedBaseController != baseController && controller is AnimatorOverrideController)
            mutableOverride = controller;
        // An override's base can be mutated in place and rebound later while
        // keeping its identity/count. Read its live schema to preserve that
        // transition exactly. Stable clip overrides still use the cached path.
        if (controller != null && controller == mutableOverride)
        {
            foreach (AnimatorControllerParameter parameter in animator.parameters)
                if (parameter != null && parameter.type == type && string.Equals(parameter.name, name, StringComparison.Ordinal)) return true;
            return false;
        }
        int count = animator.parameterCount;
        if (cachedAnimator != animator || cachedController != controller || cachedBaseController != baseController ||
            cachedCount != count || cachedRevision != revision)
        {
            types.Clear();
            foreach (AnimatorControllerParameter parameter in animator.parameters)
                if (parameter != null) types[parameter.name] = parameter.type;
            cachedAnimator = animator;
            cachedController = controller;
            cachedBaseController = baseController;
            cachedCount = count;
            cachedRevision = revision;
        }
        return types.TryGetValue(name, out AnimatorControllerParameterType actual) && actual == type;
    }
}
