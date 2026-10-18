using UnityEditor.Animations;
using UnityEngine;

/// <summary>Explicit reviewed destination assets. No prototype, clip or character-name fallback.</summary>
[CreateAssetMenu(menuName = "Lit/Brains AI/Production Conversion Request")]
public sealed class ProductionBrainsEnemyConversionRequest : ScriptableObject
{
    public CharacterData character;
    public AnimatorController sourceController;
    public bool requiresFlameInfluence = true;
    public LitBrainsCombatProfile reviewedProfile;
    public AnimatorController reviewedController;
}
