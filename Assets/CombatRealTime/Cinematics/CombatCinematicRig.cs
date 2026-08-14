using System;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[Serializable]
public sealed class CombatCinematicCameraBinding
{
    [Tooltip("Cle exposedName du CinemachineShot dans la Timeline.")]
    public string timelineCameraKey;
    public CinemachineCamera camera;
}

[Serializable]
public sealed class CombatCinematicTrackBinding
{
    [Tooltip("Nom unique de la piste Timeline liee a un objet exporte du package runtime.")]
    public string trackName;
    public UnityEngine.Object target;
}

/// <summary>Immutable runtime data supplied to a pooled combat cinematic rig.</summary>
public sealed class CombatCinematicContext
{
    public RealTimeCombatManager CombatManager { get; }
    public UnityEngine.Object Definition { get; }
    public Transform PlayerRoot { get; }
    public Animator PlayerAnimator { get; }
    public RealTimeCombatEnemy TargetEnemy { get; }
    public Animator TargetAnimator { get; }
    public Transform TargetLockPoint { get; }
    public Action ResolveImpact { get; }

    public CombatCinematicContext(
        RealTimeCombatManager manager,
        UnityEngine.Object definition,
        Action resolveImpact = null)
    {
        CombatManager = manager;
        Definition = definition;
        PlayerRoot = manager != null ? manager.PlayerRoot : null;
        PlayerAnimator = manager != null ? manager.PlayerAnimator : null;
        TargetEnemy = manager != null ? manager.LockedEnemy : null;
        TargetAnimator = TargetEnemy != null ? TargetEnemy.Animator : null;
        TargetLockPoint = TargetEnemy != null ? TargetEnemy.LockPoint : null;
        ResolveImpact = resolveImpact;
    }
}

public interface ICombatCinematicParticipant
{
    bool Begin(CombatCinematicContext context);
    void End();
}

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayableDirector), typeof(SignalReceiver), typeof(LitTimelineCinemachineBridge))]
public sealed class CombatCinematicRig : MonoBehaviour
{
    [SerializeField] private PlayableDirector director;
    [SerializeField] private SignalReceiver signalReceiver;
    [SerializeField] private List<CombatCinematicCameraBinding> cameraBindings = new List<CombatCinematicCameraBinding>();
    [SerializeField] private List<CombatCinematicTrackBinding> trackBindings = new List<CombatCinematicTrackBinding>();

    private readonly List<ICombatCinematicParticipant> participants = new List<ICombatCinematicParticipant>();
    private CombatCinematicContext context;
    private PlayableAsset bakedTimeline;

    public PlayableDirector Director => director;
    public SignalReceiver SignalReceiver => signalReceiver;
    public IReadOnlyList<CombatCinematicCameraBinding> CameraBindings => cameraBindings;
    public IReadOnlyList<CombatCinematicTrackBinding> TrackBindings => trackBindings;
    public event Action<CombatCinematicRig> Stopped;

    private void Reset()
    {
        director = GetComponent<PlayableDirector>();
        signalReceiver = GetComponent<SignalReceiver>();
    }

    private void Awake()
    {
        if (director == null) director = GetComponent<PlayableDirector>();
        if (signalReceiver == null) signalReceiver = GetComponent<SignalReceiver>();
        bakedTimeline = director != null ? director.playableAsset : null;
        MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is ICombatCinematicParticipant participant)
                participants.Add(participant);
        }
    }

    private void OnEnable()
    {
        if (director != null) director.stopped += OnDirectorStopped;
    }

    private void OnDisable()
    {
        if (director != null) director.stopped -= OnDirectorStopped;
        EndParticipants();
    }

    public bool TryPlay(
        CombatCinematicContext playbackContext,
        PlayableAsset timeline,
        string playerAnimatorTrack,
        string enemyAnimatorTrack,
        out string error)
    {
        error = null;
        if (timeline == null) timeline = bakedTimeline;
        if (playbackContext == null || playbackContext.PlayerRoot == null || playbackContext.PlayerAnimator == null ||
            playbackContext.TargetEnemy == null || playbackContext.TargetAnimator == null ||
            director == null || signalReceiver == null)
        {
            error = "Contexte, cibles ou composants de rig manquants.";
            return false;
        }

        TimelineAsset timelineAsset = timeline as TimelineAsset;
        if (timelineAsset == null)
        {
            error = "La sequence de combat doit etre un TimelineAsset.";
            return false;
        }

        context = playbackContext;
        PositionAtPlayerFacingTarget();
        if (!BeginParticipants())
        {
            error = "Un participant de cinematique a refuse le contexte courant.";
            EndParticipants();
            context = null;
            return false;
        }

        director.playableAsset = timelineAsset;
        bakedTimeline ??= timelineAsset;
        if (!TryBindTimeline(timelineAsset, playerAnimatorTrack, enemyAnimatorTrack, out error))
        {
            EndParticipants();
            context = null;
            return false;
        }

        director.time = 0d;
        director.Evaluate();
        director.Play();
        return true;
    }

    public void Stop()
    {
        if (director != null && director.state == PlayState.Playing)
        {
            director.Stop();
        }
        else
        {
            CompleteStop();
        }
    }

    public void ResetForPool()
    {
        if (director != null && director.playableAsset is TimelineAsset timeline)
        {
            foreach (PlayableBinding output in timeline.outputs)
            {
                director.ClearGenericBinding(output.sourceObject);
                if (output.sourceObject is CinemachineTrack cameraTrack)
                {
                    foreach (TimelineClip clip in cameraTrack.GetClips())
                    {
                        if (clip.asset is CinemachineShot shot)
                            director.ClearReferenceValue(shot.VirtualCamera.exposedName);
                    }
                }
            }
        }

        EndParticipants();
        director.playableAsset = bakedTimeline;
        context = null;
        transform.SetParent(null, true);
        gameObject.SetActive(false);
    }

    private bool TryBindTimeline(TimelineAsset timeline, string playerTrack, string enemyTrack, out string error)
    {
        error = null;
        CinemachineBrain brain = LitCameraDirector.EnsureInstance()?.CinemachineBrain;
        if (brain == null)
        {
            error = "CinemachineBrain de la camera de jeu introuvable.";
            return false;
        }

        bool playerBound = false;
        bool enemyBound = false;
        bool cameraTrackBound = false;
        bool signalBound = false;
        foreach (PlayableBinding output in timeline.outputs)
        {
            if (output.sourceObject == null) continue;
            if (output.sourceObject is AnimationTrack && output.streamName == playerTrack)
            {
                director.SetGenericBinding(output.sourceObject, context.PlayerAnimator);
                playerBound = true;
            }
            else if (output.sourceObject is AnimationTrack && output.streamName == enemyTrack)
            {
                director.SetGenericBinding(output.sourceObject, context.TargetAnimator);
                enemyBound = true;
            }
            else if (output.sourceObject is SignalTrack)
            {
                director.SetGenericBinding(output.sourceObject, signalReceiver);
                signalBound = true;
            }
            else if (output.sourceObject is CinemachineTrack cameraTrack)
            {
                director.SetGenericBinding(output.sourceObject, brain);
                foreach (TimelineClip clip in cameraTrack.GetClips())
                {
                    if (!(clip.asset is CinemachineShot shot)) continue;
                    CinemachineCamera camera = ResolveCamera(shot.VirtualCamera.exposedName.ToString());
                    if (camera == null)
                    {
                        error = "Camera introuvable pour la cle Timeline '" + shot.VirtualCamera.exposedName + "'.";
                        return false;
                    }
                    director.SetReferenceValue(shot.VirtualCamera.exposedName, camera);
                }
                cameraTrackBound = true;
            }
            else if (TryResolveTrackBinding(output.streamName, out UnityEngine.Object target))
            {
                director.SetGenericBinding(output.sourceObject, target);
            }
        }

        if (!playerBound || !enemyBound || !cameraTrackBound || !signalBound)
        {
            error = "Pistes requises manquantes :" +
                    (!playerBound ? " Player.Animator" : string.Empty) +
                    (!enemyBound ? " Enemy.Animator" : string.Empty) +
                    (!cameraTrackBound ? " Cinemachine" : string.Empty) +
                    (!signalBound ? " Signals" : string.Empty);
            return false;
        }
        return true;
    }

    private CinemachineCamera ResolveCamera(string key)
    {
        for (int i = 0; i < cameraBindings.Count; i++)
        {
            CombatCinematicCameraBinding binding = cameraBindings[i];
            if (binding != null && binding.camera != null && string.Equals(binding.timelineCameraKey, key, StringComparison.Ordinal))
                return binding.camera;
        }
        return null;
    }

    private bool TryResolveTrackBinding(string trackName, out UnityEngine.Object target)
    {
        target = null;
        if (string.IsNullOrWhiteSpace(trackName)) return false;
        for (int i = 0; i < trackBindings.Count; i++)
        {
            CombatCinematicTrackBinding binding = trackBindings[i];
            if (binding == null || binding.target == null || !string.Equals(binding.trackName, trackName, StringComparison.Ordinal)) continue;
            target = binding.target;
            return true;
        }
        return false;
    }

    private bool BeginParticipants()
    {
        for (int i = 0; i < participants.Count; i++)
        {
            if (participants[i] != null && !participants[i].Begin(context)) return false;
        }
        return true;
    }

    private void EndParticipants()
    {
        for (int i = participants.Count - 1; i >= 0; i--)
        {
            participants[i]?.End();
        }
    }

    private void PositionAtPlayerFacingTarget()
    {
        transform.position = context.PlayerRoot.position;
        Transform target = context.TargetLockPoint != null ? context.TargetLockPoint : context.TargetEnemy.transform;
        Vector3 direction = target.position - context.PlayerRoot.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private void OnDirectorStopped(PlayableDirector stoppedDirector)
    {
        if (stoppedDirector == director) CompleteStop();
    }

    private void CompleteStop()
    {
        Stopped?.Invoke(this);
    }
}
