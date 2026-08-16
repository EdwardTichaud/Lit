using UnityEngine;

/// <summary>Single runtime validation path for states played on Player_Model.</summary>
public static class PlayerAnimatorStateResolver
{
    public static bool TryResolve(
        Animator animator,
        PlayerModelAnimationProfile profile,
        string requestedState,
        out int stateHash,
        out string resolvedState)
    {
        stateHash = 0;
        resolvedState = profile != null ? profile.NormalizeStatePath(requestedState) : requestedState?.Trim();
        if (animator == null || string.IsNullOrWhiteSpace(resolvedState)) return false;

        stateHash = Animator.StringToHash(resolvedState);
        return animator.HasState(0, stateHash);
    }

    public static bool TryResolve(
        Animator animator,
        PlayerModelAnimationProfile profile,
        PlayerModelAnimationState state,
        out int stateHash,
        out string resolvedState)
    {
        stateHash = 0;
        resolvedState = null;
        return profile != null && profile.TryGetState(state, out string requestedState) &&
               TryResolve(animator, profile, requestedState, out stateHash, out resolvedState);
    }
}
