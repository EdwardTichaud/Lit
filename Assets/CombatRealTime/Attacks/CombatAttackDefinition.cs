using UnityEngine;

[CreateAssetMenu(fileName = "CombatAttack", menuName = "Lit/Combat Real Time/Attack Definition")]
public sealed class CombatAttackDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string displayName;
    [SerializeField, TextArea] private string description;
    [SerializeField] private RealTimeCombatRange range = RealTimeCombatRange.Melee;

    [Header("Light")]
    [SerializeField, Min(1)] private int lightDamage = 1;
    [SerializeField, Min(0f)] private float clarityGain = 1f;
    [SerializeField, Min(0f)] private float cooldownSeconds;
    [SerializeField, Min(0f)] private float maximumRange = 2.5f;

    [Header("Presentation")]
    [SerializeField] private string animatorState;
    [SerializeField] private GameObject impactVfxPrefab;
    [SerializeField] private AudioClipSO impactSfx;

    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public string Description => description;
    public RealTimeCombatRange Range => range;
    public int LightDamage => lightDamage;
    public float ClarityGain => clarityGain;
    public float CooldownSeconds => cooldownSeconds;
    public float MaximumRange => maximumRange;
    public string AnimatorState => animatorState;
    public GameObject ImpactVfxPrefab => impactVfxPrefab;
    public AudioClipSO ImpactSfx => impactSfx;
}
