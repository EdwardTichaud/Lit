using UnityEngine;

/// <summary>Scene-local receiver; existing skill animation events also retain their normal presentation handlers.</summary>
public sealed class LucianBrainsCombatEvents : MonoBehaviour
{
    public LucianBrainsCombatArena arena;
    public void ResolveSkillImpact(AnimationEvent source) => arena?.ResolvePlayerContact(source);
    public void HitEnemy(AnimationEvent source) => arena?.ResolvePlayerContact(source);
}
