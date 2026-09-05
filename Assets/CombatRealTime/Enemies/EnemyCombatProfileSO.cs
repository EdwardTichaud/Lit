using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class EnemyCombatPattern
{
    [Min(0f)] public float minimumStartDistance;
    public string name;
    public List<SkillSO> skills = new List<SkillSO>();
    [Min(1)] public int weight = 10;
    [Min(0f)] public float cooldownSeconds = 2f;
    [Min(0f)] public float recoverySeconds = .7f;
    [Min(1)] public int maximumConsecutiveUses = 1;
    [Range(0f, 180f)] public float maximumStartAngle = 35f;
    public bool IsConfigured => skills != null && skills.Count > 0 &&
        skills.TrueForAll(s => s != null && s.AnimationClip != null);
}

[CreateAssetMenu(fileName = "EnemyCombatProfile", menuName = "Lit/Combat Real Time/Enemy Combat Profile")]
public sealed class EnemyCombatProfileSO : ScriptableObject
{
    public List<EnemyCombatPattern> patterns = new List<EnemyCombatPattern>();
    public bool preferMeleeApproach;
    [Range(0f, 1f)] public float airborneAlternativeChance = .25f;
    [Min(.1f)] public float preferredCombatDistance = 2.6f;
    [Min(1f)] public float pursuitRadius = 20f;
    [Min(0f)] public float disengagePauseSeconds = 1f;
    public Vector2 observationSeconds = new Vector2(.4f, .8f);
    [Range(0f, 1f)] public float guardChance = .08f;
    [Min(0f)] public float guardCooldownSeconds = 4f;
    [Min(.1f)] public float guardDurationSeconds = .6f;
    [Range(0f, 1f)] public float guardedDamageMultiplier = .45f;
    [Range(1f, 360f)] public float trackingDegreesPerSecond = 180f;
}
