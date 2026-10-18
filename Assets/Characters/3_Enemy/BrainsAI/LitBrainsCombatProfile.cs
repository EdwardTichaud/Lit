using System;
using UnityEngine;

[Serializable]
public sealed class LitBrainsAttackProfile
{
    public SkillSO skill;
    public string stateName = "Attack";
    public AnimationClip clip;
    [Min(0)] public float reactionSeconds;
    [Min(0)] public float strikeSeconds;
    [Min(0)] public float impactSeconds;
    [Min(0)] public float recoverySeconds;
    [Min(.01f)] public float recoveryDurationSeconds = .3f;
    public VisualActionHandoff visualHandoff = new VisualActionHandoff();
    [Min(0)] public float preparationAdvanceDistance;
    [Min(0)] public float advanceDistance = .4f;
    [Min(.01f)] public float advanceSeconds = .18f;
    [Min(.1f)] public float reach = 2.6f;
    [Range(1, 360)] public float arcDegrees = 110;
    [Min(.1f)] public float heightTolerance = 1.8f;
}

[CreateAssetMenu(menuName = "Lit/Brains AI/Combat Profile")]
public sealed class LitBrainsCombatProfile : ScriptableObject
{
    [Header("Presentation")]
    public string locomotionState = "Locomotion";
    public string hurtState = "Hurt";
    public string knockoutState = "Knocked Out";
    public string deathState = "Base Layer.Death";
    [Min(0)] public float hurtBlendSeconds = .04f;
    [Tooltip("Original authoring evidence only; does not enable legacy AI or synthesize its behaviours.")]
    public EnemyCombatProfileSO productionSourceProfile;
    public bool executeProductionPatterns;
    public string idleState = "Locomotion";
    public string guardState = "Guard";
    public float attackStartDistance = 2.2f;
    [Min(.1f)] public float approachStopDistance = 2.05f;
    [Min(0)] public float initialAttackDelay = .2f;
    public float attackFacingDegrees = 25f;
    public float turnDegreesPerSecond = 540f;
    public Vector2 attackInterval = new Vector2(2.4f, 3f);
    public float resistance = 100f;
    public float resistanceRecoveryDelay = 1f;
    public float resistanceRecoveryPerSecond = 50f;
    public float hurtSeconds = .35f;
    public float interruptionProtectionSeconds = .75f;
    public LitBrainsAttackProfile[] attacks = Array.Empty<LitBrainsAttackProfile>();

    public LitBrainsAttackProfile Find(int stateHash)
    {
        foreach (var attack in attacks)
            if (attack != null && Animator.StringToHash(attack.stateName) == stateHash) return attack;
        return null;
    }
}
