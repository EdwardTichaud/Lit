using System;
using UnityEngine;

[Serializable]
public sealed class PlayerGroundImpactProfile
{
    public bool enabled;
    public SkillSO interruptibleSkill;
    [Min(.1f)] public float radius = 5f;
    [Min(1f)] public float airborneDamageMultiplier = 2f;
    [Min(0f)] public float groundPushDistance = 1.5f;
    [Min(0f)] public float airbornePushDistance = 3f;
    [Min(1f)] public float descentSpeed = 25f;
    [Min(.1f)] public float maximumDescentSeconds = 3f;
    public LayerMask groundMask = ~0;
}
