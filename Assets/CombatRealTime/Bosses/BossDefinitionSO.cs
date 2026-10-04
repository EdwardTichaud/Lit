using UnityEngine;

public enum BossDamagePolicy
{
    Normal,
    Invulnerable
}

/// <summary>Authored, reusable rules for a boss encounter. Gameplay scripts never need to know a concrete boss name.</summary>
[CreateAssetMenu(fileName = "BossDefinition", menuName = "Lit/Combat/Boss Definition")]
public sealed class BossDefinitionSO : ScriptableObject
{
    [SerializeField] private string bossId = "boss";
    [SerializeField] private string displayName = "Boss";
    [SerializeField, Min(.1f)] private float engagementDistance = 8f;
    [SerializeField, Min(1)] private int segmentCount = 3;
    [SerializeField] private BossDamagePolicy damagePolicy = BossDamagePolicy.Invulnerable;
    [SerializeField] private bool showBossBar = true;
    [SerializeField] private bool showImmuneImpact = true;
    [SerializeField, Tooltip("Désactive l'IA ordinaire pour un boss piloté entièrement par son comportement dédié.")]
    private bool suppressDefaultEnemyBrain = true;

    public string BossId => bossId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public float EngagementDistance => engagementDistance;
    public int SegmentCount => Mathf.Max(1, segmentCount);
    public BossDamagePolicy DamagePolicy => damagePolicy;
    public bool ShowBossBar => showBossBar;
    public bool ShowImmuneImpact => showImmuneImpact;
    public bool SuppressDefaultEnemyBrain => suppressDefaultEnemyBrain;
}
