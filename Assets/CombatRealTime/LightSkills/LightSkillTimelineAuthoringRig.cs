using System;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// Editor-facing holder for a Light Skill Timeline preview. This object is not
/// used by gameplay: LightSkillCombatController remains the only runtime
/// binding point.
/// </summary>
[DisallowMultipleComponent]
public sealed class LightSkillTimelineAuthoringRig : MonoBehaviour
{
    [SerializeField] private LightSkillSO lightSkill;
    [SerializeField] private PlayableDirector director;
    [SerializeField] private Animator previewPlayerAnimator;
    [SerializeField] private Animator previewEnemyAnimator;
    [SerializeField, Tooltip("Root de gameplay preview de Lucian. L'Animator doit etre porte par ce root.")]
    private Transform previewPlayerActorRoot;
    [SerializeField, Tooltip("Root de gameplay preview de l'ennemi. L'Animator doit etre porte par ce root.")]
    private Transform previewEnemyActorRoot;
    [Header("Runtime Stage Anchors")]
    [Tooltip("Repere canonique du root Lucian, relatif au root AnimationLab.")]
    [SerializeField] private Transform previewPlayerAnchor;
    [Tooltip("Repere canonique du root Enemy, relatif au root AnimationLab.")]
    [SerializeField] private Transform previewEnemyAnchor;
    [SerializeField] private Transform previewEnemyLockPoint;
    [SerializeField] private CinemachineBrain previewCameraBrain;
    [SerializeField] private CinemachineCamera previewVirtualCamera;
    [SerializeField] private SignalReceiver previewSignalReceiver;

    public LightSkillSO LightSkill => lightSkill;
    public PlayableDirector Director => director;
    public Transform PreviewPlayerActorRoot => CombatCinematicAuthoringActorResolver.ResolveActorRoot(
        previewPlayerActorRoot, previewPlayerAnchor, previewPlayerAnimator);
    public Transform PreviewEnemyActorRoot => CombatCinematicAuthoringActorResolver.ResolveActorRoot(
        previewEnemyActorRoot, previewEnemyAnchor, previewEnemyAnimator);
    public Animator PreviewPlayerAnimator => CombatCinematicAuthoringActorResolver.ResolveAnimator(
        PreviewPlayerActorRoot, previewPlayerAnimator);
    public Animator PreviewEnemyAnimator => CombatCinematicAuthoringActorResolver.ResolveAnimator(
        PreviewEnemyActorRoot, previewEnemyAnimator);
    public Transform PreviewPlayerAnchor => previewPlayerAnchor;
    public Transform PreviewEnemyAnchor => previewEnemyAnchor;
    public Transform PreviewEnemyLockPoint => previewEnemyLockPoint != null
        ? previewEnemyLockPoint
        : PreviewEnemyActorRoot;
    public CinemachineBrain PreviewCameraBrain => previewCameraBrain;
    public CinemachineCamera PreviewVirtualCamera => previewVirtualCamera;
    public SignalReceiver PreviewSignalReceiver => previewSignalReceiver;

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Existing AnimationLab scenes gain the explicit contract without a
        // manual migration. Future labs still expose the two fields normally.
        previewPlayerAnchor ??= FindStageAnchor("Lucian_Anchor");
        previewEnemyAnchor ??= FindStageAnchor("Enemy_Anchor");
        previewPlayerActorRoot ??= CombatCinematicAuthoringActorResolver.ResolveActorRoot(
            null, previewPlayerAnchor, previewPlayerAnimator);
        previewEnemyActorRoot ??= CombatCinematicAuthoringActorResolver.ResolveActorRoot(
            null, previewEnemyAnchor, previewEnemyAnimator);
        previewPlayerAnimator = CombatCinematicAuthoringActorResolver.ResolveAnimator(
            previewPlayerActorRoot, previewPlayerAnimator);
        previewEnemyAnimator = CombatCinematicAuthoringActorResolver.ResolveAnimator(
            previewEnemyActorRoot, previewEnemyAnimator);
    }

    private Transform FindStageAnchor(string anchorName)
    {
        Transform[] transforms = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (string.Equals(transforms[i].name, anchorName, StringComparison.Ordinal))
                return transforms[i];
        }

        return null;
    }
#endif

    public void Configure(
        LightSkillSO skill,
        PlayableDirector playableDirector,
        Animator playerAnimator,
        Animator enemyAnimator,
        Transform playerAnchor,
        Transform enemyAnchor,
        CinemachineBrain cameraBrain,
        CinemachineCamera virtualCamera,
        SignalReceiver signalReceiver,
        Transform enemyLockPoint = null)
    {
        lightSkill = skill;
        director = playableDirector;
        previewPlayerAnimator = playerAnimator;
        previewEnemyAnimator = enemyAnimator;
        previewPlayerAnchor = playerAnchor;
        previewEnemyAnchor = enemyAnchor;
        previewPlayerActorRoot = CombatCinematicAuthoringActorResolver.ResolveActorRoot(null, playerAnchor, playerAnimator);
        previewEnemyActorRoot = CombatCinematicAuthoringActorResolver.ResolveActorRoot(null, enemyAnchor, enemyAnimator);
        previewEnemyLockPoint = enemyLockPoint;
        previewCameraBrain = cameraBrain;
        previewVirtualCamera = virtualCamera;
        previewSignalReceiver = signalReceiver;
    }

    public bool ApplyPreviewBindings(out string error)
    {
        error = null;
        TimelineAsset timeline = lightSkill != null ? lightSkill.Timeline as TimelineAsset : null;
        if (timeline == null)
        {
            error = "LightSkillSO ou TimelineAsset manquant.";
            return false;
        }

        Animator playerAnimator = PreviewPlayerAnimator;
        Animator enemyAnimator = PreviewEnemyAnimator;
        if (director == null || playerAnimator == null || enemyAnimator == null ||
            previewPlayerAnchor == null || previewEnemyAnchor == null ||
            previewCameraBrain == null || previewSignalReceiver == null)
        {
            error = "Le rig d'auteur n'a pas toutes ses references de previsualisation.";
            return false;
        }

        List<string> issues = LightSkillTimelineContract.GetIssues(timeline, lightSkill);
        if (issues.Count > 0)
        {
            error = string.Join("\n", issues);
            return false;
        }

        director.playableAsset = timeline;
        foreach (PlayableBinding output in timeline.outputs)
        {
            if (output.sourceObject is AnimationTrack animationTrack)
            {
                if (animationTrack.name == lightSkill.PlayerAnimatorTrackName)
                {
                    director.SetGenericBinding(animationTrack, playerAnimator);
                }
                else if (animationTrack.name == lightSkill.EnemyAnimatorTrackName)
                {
                    director.SetGenericBinding(animationTrack, enemyAnimator);
                }
            }
            else if (output.sourceObject is CinemachineTrack cinemachineTrack)
            {
                director.SetGenericBinding(cinemachineTrack, previewCameraBrain);
                CinemachineCamera[] cameras = GetComponentsInChildren<CinemachineCamera>(true);
                if (cameras.Length == 0)
                {
                    error = "Le rig d'auteur ne contient aucune CinemachineCamera de preview.";
                    return false;
                }

                foreach (TimelineClip clip in cinemachineTrack.GetClips())
                {
                    if (clip.asset is CinemachineShot shot)
                    {
                        bool assigned;
                        CinemachineCamera assignedCamera = director.GetReferenceValue(
                            shot.VirtualCamera.exposedName, out assigned) as CinemachineCamera;
                        if (!assigned || assignedCamera == null || !assignedCamera.transform.IsChildOf(transform))
                        {
                            error = "Camera de preview non resolue pour la cle Timeline '" +
                                    shot.VirtualCamera.exposedName + "'.";
                            return false;
                        }
                    }
                }
            }
            else if (output.sourceObject is SignalTrack signalTrack)
            {
                director.SetGenericBinding(signalTrack, previewSignalReceiver);
            }
        }

        director.RebuildGraph();
        return true;
    }
}

/// <summary>Shared, intentionally small contract used by authoring validation.</summary>
public static class LightSkillTimelineContract
{
    public const string PlayerAnimatorTrack = "Player.Animator";
    public const string EnemyAnimatorTrack = "Enemy.Animator";
    public const string CinemachineTrack = "Cinemachine";
    public const string SignalsTrack = "Signals";

    public static List<string> GetIssues(TimelineAsset timeline, LightSkillSO skill)
    {
        List<string> issues = new List<string>();
        if (timeline == null)
        {
            issues.Add("TimelineAsset manquant.");
            return issues;
        }

        string playerTrack = skill != null ? skill.PlayerAnimatorTrackName : PlayerAnimatorTrack;
        string enemyTrack = skill != null ? skill.EnemyAnimatorTrackName : EnemyAnimatorTrack;
        string cameraTrack = skill != null ? skill.CinemachineTrackName : CinemachineTrack;

        if (!HasTrack<AnimationTrack>(timeline, playerTrack))
            issues.Add("Piste Animation requise absente : '" + playerTrack + "'.");
        if (!HasTrack<AnimationTrack>(timeline, enemyTrack))
            issues.Add("Piste Animation requise absente : '" + enemyTrack + "'.");
        if (!HasTrack<CinemachineTrack>(timeline, cameraTrack))
            issues.Add("Piste Cinemachine requise absente : '" + cameraTrack + "'.");
        if (!HasTrack<SignalTrack>(timeline, SignalsTrack))
            issues.Add("Piste Signal requise absente : '" + SignalsTrack + "'.");

        SignalTrack signals = FindTrack<SignalTrack>(timeline, SignalsTrack);
        if (skill != null && HasDevastationSignalConfiguration(skill))
        {
            ValidateSignal(signals, skill.SpawnProjectileSignal, "SpawnProjectile", issues);
            ValidateSignal(signals, skill.LaunchProjectileSignal, "LaunchProjectile", issues);
            ValidateSignal(signals, skill.SpawnImpactVfxSignal, "SpawnImpactVfx", issues);
            ValidateSignal(signals, skill.ResolveDamageSignal, "ResolveDamage", issues);
        }

        CinemachineTrack cinemachine = FindTrack<CinemachineTrack>(timeline, cameraTrack);
        if (cinemachine != null)
        {
            bool hasShot = false;
            foreach (TimelineClip clip in cinemachine.GetClips())
            {
                if (clip.asset is CinemachineShot shot)
                {
                    hasShot = true;
                    if (string.IsNullOrEmpty(shot.VirtualCamera.exposedName.ToString()))
                        issues.Add("Un CinemachineShot n'a pas de camera virtuelle exposee.");
                }
            }
            if (!hasShot)
                issues.Add("La piste '" + cameraTrack + "' doit contenir au moins un CinemachineShot.");
        }

        return issues;
    }

    private static bool HasDevastationSignalConfiguration(LightSkillSO skill)
    {
        return skill.SpawnProjectileSignal != null || skill.LaunchProjectileSignal != null ||
               skill.SpawnImpactVfxSignal != null || skill.ResolveDamageSignal != null;
    }

    private static void ValidateSignal(SignalTrack track, SignalAsset signal, string label, List<string> issues)
    {
        if (signal == null)
        {
            issues.Add("Signal Devastation manquant : '" + label + "'.");
            return;
        }

        if (track == null)
        {
            return;
        }

        foreach (IMarker marker in track.GetMarkers())
        {
            if (marker is SignalEmitter emitter && emitter.asset == signal)
            {
                return;
            }
        }

        issues.Add("Le signal '" + signal.name + "' ('" + label + "') n'est pas place sur la piste Signals.");
    }

    public static bool HasTrack<TTrack>(TimelineAsset timeline, string trackName) where TTrack : TrackAsset
    {
        return FindTrack<TTrack>(timeline, trackName) != null;
    }

    public static TTrack FindTrack<TTrack>(TimelineAsset timeline, string trackName) where TTrack : TrackAsset
    {
        if (timeline == null || string.IsNullOrWhiteSpace(trackName))
            return null;

        foreach (PlayableBinding output in timeline.outputs)
        {
            if (output.sourceObject is TTrack track && string.Equals(track.name, trackName, StringComparison.Ordinal))
                return track;
        }
        return null;
    }
}
