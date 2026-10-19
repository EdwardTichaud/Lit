using UnityEngine;

/// <summary>Connect an existing host-side gameplay UnityEvent to Unlock. No client can grant it.</summary>
public sealed class AbilityUnlock : MonoBehaviour
{
    public AbilitySO ability;
    public void Unlock() => AbilityService.Ensure().Unlock(ability);
}
