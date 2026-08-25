using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// AnimationLab authoring holder for an optional SkillSO cinematic. Unlike the
/// LightSkill rig, this contract never exports a staging layout: it plays at
/// the actors' live positions.
/// </summary>
[DisallowMultipleComponent]
public sealed class CombatSkillTimelineAuthoringRig : MonoBehaviour
{
    [SerializeField] private SkillSO skill;
    [SerializeField] private PlayableDirector director;
    [SerializeField] private Animator previewPlayerAnimator;
    [SerializeField] private Animator previewEnemyAnimator;
    [SerializeField] private Transform previewPlayerActorRoot;
    [SerializeField] private Transform previewEnemyActorRoot;
    [SerializeField] private CinemachineBrain previewCameraBrain;
    [SerializeField] private SignalReceiver previewSignalReceiver;

    public SkillSO Skill => skill;
    public PlayableDirector Director => director;
    public Transform PreviewPlayerActorRoot => CombatCinematicAuthoringActorResolver.ResolveActorRoot(
        previewPlayerActorRoot, null, previewPlayerAnimator);
    public Transform PreviewEnemyActorRoot => CombatCinematicAuthoringActorResolver.ResolveActorRoot(
        previewEnemyActorRoot, null, previewEnemyAnimator);
    public Animator PreviewPlayerAnimator => CombatCinematicAuthoringActorResolver.ResolveAnimator(
        PreviewPlayerActorRoot, previewPlayerAnimator);
    public Animator PreviewEnemyAnimator => CombatCinematicAuthoringActorResolver.ResolveAnimator(
        PreviewEnemyActorRoot, previewEnemyAnimator);
    public CinemachineBrain PreviewCameraBrain => previewCameraBrain;
    public SignalReceiver PreviewSignalReceiver => previewSignalReceiver;

#if UNITY_EDITOR
    private void OnValidate()
    {
        previewPlayerActorRoot ??= FindPreviewRoot("Lucian_Preview", previewPlayerAnimator);
        previewEnemyActorRoot ??= FindPreviewRoot("Enemy_Preview", previewEnemyAnimator);
        previewPlayerAnimator = CombatCinematicAuthoringActorResolver.ResolveAnimator(
            previewPlayerActorRoot, previewPlayerAnimator);
        previewEnemyAnimator = CombatCinematicAuthoringActorResolver.ResolveAnimator(
            previewEnemyActorRoot, previewEnemyAnimator);
        previewCameraBrain ??= GetComponentInChildren<CinemachineBrain>(true);
        previewSignalReceiver ??= GetComponentInChildren<SignalReceiver>(true);
    }

    private Transform FindPreviewRoot(string rootName, Animator fallback)
    {
        Transform[] transforms = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i].name == rootName)
            {
                return transforms[i];
            }
        }

        return CombatCinematicAuthoringActorResolver.ResolveActorRoot(null, null, fallback);
    }
#endif

    public bool ApplyPreviewBindings(out string error)
    {
        error = null;
        CombatSkillCinematicDefinition cinematic = skill != null ? skill.Cinematic : null;
        TimelineAsset timeline = cinematic != null ? cinematic.Timeline as TimelineAsset : null;
        Animator playerAnimator = PreviewPlayerAnimator;
        Animator enemyAnimator = PreviewEnemyAnimator;
        if (timeline == null || director == null || playerAnimator == null || enemyAnimator == null ||
            previewCameraBrain == null || previewSignalReceiver == null)
        {
            error = "SkillSO, Timeline ou references de preview manquantes.";
            return false;
        }

        if (!CombatSkillTimelineContract.Validate(timeline, cinematic, out error)) return false;

        director.playableAsset = timeline;
        foreach (PlayableBinding output in timeline.outputs)
        {
            if (output.sourceObject is AnimationTrack track)
            {
                if (track.name == cinematic.PlayerAnimatorTrackName) director.SetGenericBinding(track, playerAnimator);
                else if (track.name == cinematic.EnemyAnimatorTrackName) director.SetGenericBinding(track, enemyAnimator);
            }
            else if (output.sourceObject is CinemachineTrack cameraTrack)
            {
                director.SetGenericBinding(cameraTrack, previewCameraBrain);
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

public static class CombatSkillTimelineContract
{
    public static bool Validate(TimelineAsset timeline, CombatSkillCinematicDefinition cinematic, out string error)
    {
        error = null;
        if (timeline == null || cinematic == null)
        {
            error = "Timeline ou definition cinematographique manquante.";
            return false;
        }

        bool player = false;
        bool enemy = false;
        bool camera = false;
        bool signals = false;
        foreach (PlayableBinding output in timeline.outputs)
        {
            if (output.sourceObject is AnimationTrack)
            {
                player |= output.streamName == cinematic.PlayerAnimatorTrackName;
                enemy |= output.streamName == cinematic.EnemyAnimatorTrackName;
            }
            else if (output.sourceObject is CinemachineTrack)
            {
                camera |= output.streamName == cinematic.CinemachineTrackName;
            }
            else if (output.sourceObject is SignalTrack)
            {
                signals = true;
            }
        }

        if (!player || !enemy || !camera || !signals)
        {
            error = "Pistes requises: Player.Animator, Enemy.Animator, Cinemachine et Signals.";
            return false;
        }

        return true;
    }
}
