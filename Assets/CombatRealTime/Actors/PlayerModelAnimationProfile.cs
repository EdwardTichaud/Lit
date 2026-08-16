using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Canonical animation-state contract for the gameplay Player_Model controller.
/// Keep controller paths here instead of duplicating them across combat features.
/// </summary>
[CreateAssetMenu(fileName = "PlayerModelAnimationProfile", menuName = "Lit/Combat/Player Model Animation Profile")]
public sealed class PlayerModelAnimationProfile : ScriptableObject
{
    public const string LegacyEclairState = "Base Layer.AnimationClip_Skill_Eclair";

    [Header("Core")]
    [SerializeField] private string locomotionState = "Base Layer.Locomotion";
    [SerializeField] private string deathState = "Base Layer.Death";
    [SerializeField] private string hurtState = "Base Layer.RealTimeCombat_RootMotion.TwinSword_Defense_Hit_Root";

    [Header("Guard")]
    [SerializeField] private string guardState = "Base Layer.RealTimeCombat_RootMotion.Guard_Block";
    [SerializeField] private string guardFallbackState = "Base Layer.RealTimeCombat_RootMotion.Twinblades_Defense_Hit_Root";
    [SerializeField] private string guardReleaseState = "Base Layer.RealTimeCombat_RootMotion.Twinblades_Idle_Root";

    [Header("Dodge")]
    [SerializeField] private string dodgeForwardState = "Base Layer.RealTimeCombat_RootMotion.TwinSword_Dodge_F_Root";
    [SerializeField] private string dodgeBackwardState = "Base Layer.RealTimeCombat_RootMotion.TwinSword_Dodge_B_Root";
    [SerializeField] private string dodgeLeftState = "Base Layer.RealTimeCombat_RootMotion.TwinSword_Dodge_L_Root";
    [SerializeField] private string dodgeRightState = "Base Layer.RealTimeCombat_RootMotion.TwinSword_Dodge_R_Root";

    public bool TryGetState(PlayerModelAnimationState state, out string statePath)
    {
        statePath = state switch
        {
            PlayerModelAnimationState.Locomotion => locomotionState,
            PlayerModelAnimationState.Death => deathState,
            PlayerModelAnimationState.Hurt => hurtState,
            PlayerModelAnimationState.Guard => guardState,
            PlayerModelAnimationState.GuardFallback => guardFallbackState,
            PlayerModelAnimationState.GuardRelease => guardReleaseState,
            PlayerModelAnimationState.DodgeForward => dodgeForwardState,
            PlayerModelAnimationState.DodgeBackward => dodgeBackwardState,
            PlayerModelAnimationState.DodgeLeft => dodgeLeftState,
            PlayerModelAnimationState.DodgeRight => dodgeRightState,
            _ => null
        };
        return !string.IsNullOrWhiteSpace(statePath);
    }

    public IEnumerable<string> GetRequiredStatePaths()
    {
        foreach (PlayerModelAnimationState state in Enum.GetValues(typeof(PlayerModelAnimationState)))
        {
            if (TryGetState(state, out string statePath)) yield return statePath;
        }
    }

    public string NormalizeStatePath(string requestedState)
    {
        if (string.IsNullOrWhiteSpace(requestedState)) return null;
        string normalized = requestedState.Trim();
        return normalized == LegacyEclairState ? "Base Layer.Skill_1_Eclair" : normalized;
    }
}

public enum PlayerModelAnimationState
{
    Locomotion,
    Death,
    Hurt,
    Guard,
    GuardFallback,
    GuardRelease,
    DodgeForward,
    DodgeBackward,
    DodgeLeft,
    DodgeRight
}
