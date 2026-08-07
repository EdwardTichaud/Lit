using UnityEngine;
using UnityEngine.Playables;

[CreateAssetMenu(fileName = "CounterSkillSO", menuName = "Scriptable Objects/Combat/Counter Skill SO")]
public sealed class CounterSkillSO : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string displayName = "Counter Skill";
    [SerializeField] private Sprite icon;

    [Header("Resolution")]
    [SerializeField] private PlayableAsset timeline;
    [SerializeField, Min(0)] private int damage = 25;
    [SerializeField, Min(0f)] private float clarityGain = 10f;

    [Header("Timeline Bindings")]
    [SerializeField] private string playerAnimatorTrackName = "Player.Animator";
    [SerializeField] private string enemyAnimatorTrackName = "Enemy.Animator";
    [SerializeField] private string cinemachineTrackName = "Cinemachine";

    [Header("Audio")]
    [SerializeField] private AudioClipSO startSfx;
    [SerializeField] private AudioClipSO impactSfx;

    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public Sprite Icon => icon;
    public PlayableAsset Timeline => timeline;
    public int Damage => damage;
    public float ClarityGain => clarityGain;
    public string PlayerAnimatorTrackName => playerAnimatorTrackName;
    public string EnemyAnimatorTrackName => enemyAnimatorTrackName;
    public string CinemachineTrackName => cinemachineTrackName;
    public AudioClipSO StartSfx => startSfx;
    public AudioClipSO ImpactSfx => impactSfx;
}
