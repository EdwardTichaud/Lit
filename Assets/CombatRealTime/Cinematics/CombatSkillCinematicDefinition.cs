using System;
using UnityEngine;
using UnityEngine.Playables;

[Serializable]
public sealed class CombatCinematicPostTimelineState
{
    [Tooltip("State Animator a jouer apres une fin naturelle de Timeline. Vide = conserver la pose finale.")]
    [SerializeField] private string animatorStateName;
    [SerializeField, Min(0f)] private float transitionSeconds = 0.08f;
    [SerializeField, Range(0f, 1f)] private float normalizedStartTime;

    public string AnimatorStateName => animatorStateName;
    public float TransitionSeconds => transitionSeconds;
    public float NormalizedStartTime => normalizedStartTime;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(animatorStateName);
}

/// <summary>
/// Optional runtime package for a regular SkillSO. Unlike a LightSkill, it is
/// played in place and never relocates either actor before its Timeline starts.
/// </summary>
[Serializable]
public sealed class CombatSkillCinematicDefinition
{
    [SerializeField] private PlayableAsset timeline;
    [SerializeField, Tooltip("Rig runtime bake contenant Director, SignalReceiver et Cinemachine.")]
    private CombatCinematicRig combatCinematicRigPrefab;
    [Header("Timeline Bindings")]
    [SerializeField] private string playerAnimatorTrackName = "Player.Animator";
    [SerializeField] private string enemyAnimatorTrackName = "Enemy.Animator";
    [SerializeField] private string cinemachineTrackName = "Cinemachine";
    [Header("Post Timeline States")]
    [SerializeField] private CombatCinematicPostTimelineState postTimelinePlayerState = new CombatCinematicPostTimelineState();
    [SerializeField] private CombatCinematicPostTimelineState postTimelineEnemyState = new CombatCinematicPostTimelineState();

    public PlayableAsset Timeline => timeline;
    public CombatCinematicRig CombatCinematicRigPrefab => combatCinematicRigPrefab;
    public string PlayerAnimatorTrackName => string.IsNullOrWhiteSpace(playerAnimatorTrackName) ? "Player.Animator" : playerAnimatorTrackName;
    public string EnemyAnimatorTrackName => string.IsNullOrWhiteSpace(enemyAnimatorTrackName) ? "Enemy.Animator" : enemyAnimatorTrackName;
    public string CinemachineTrackName => string.IsNullOrWhiteSpace(cinemachineTrackName) ? "Cinemachine" : cinemachineTrackName;
    public CombatCinematicPostTimelineState PostTimelinePlayerState => postTimelinePlayerState;
    public CombatCinematicPostTimelineState PostTimelineEnemyState => postTimelineEnemyState;
    public bool IsConfigured => timeline != null && combatCinematicRigPrefab != null;
}
