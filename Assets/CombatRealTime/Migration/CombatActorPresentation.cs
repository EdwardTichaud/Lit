using UnityEngine;

/// <summary>Actor-authored presentation shared by local and server-owned combat execution.</summary>
public static class CombatActorPresentation
{
    public static string DamageReactionState(SquadCharacterController actor) =>
        actor != null ? actor.CharacterData?.playerSettings?.combatPresentation?.damageReactionAnimatorState ??
            PlayerCombatPresentationSettings.DefaultDamageReactionState : null;

    public static bool TryDamageReaction(SquadCharacterController actor)
    {
        string state = DamageReactionState(actor);
        if (string.IsNullOrWhiteSpace(state) || actor == null) return false;
        var presentation = actor.GetComponent<PlayerActionPresentationController>();
        return presentation != null && presentation.TryReplaceWithDamageReaction(state, PlayerActionPresentationProfile.CreateDefault());
    }
}
