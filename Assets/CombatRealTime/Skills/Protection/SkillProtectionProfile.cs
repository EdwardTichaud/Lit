using System;
using UnityEngine;

public enum SkillProtectionKind { None, Intercession, Rempart, Sommation, JusteRetour }

[Serializable]
public sealed class SkillProtectionProfile
{
    public SkillProtectionKind kind;
    public AnimationClip animationSlot;
    [Min(0)] public float radius = 6f;
    [Min(0)] public float durationSeconds = 4f;
    [Min(0)] public float maximumLinkDistance = 30f;
    [Range(0, 1)] public float reduction = .3f;
    [Range(0, 1)] public float transferFraction = .75f;
    [Range(0, 1)] public float reserveMaxHealthFraction = .5f;
}
