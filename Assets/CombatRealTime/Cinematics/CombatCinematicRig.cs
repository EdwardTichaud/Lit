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
    [Header("Authoring Stage Layout")]
    [Tooltip("Pose locale du preview Player dans AnimationLab.")]
    [SerializeField] private Vector3 authoringPlayerLocalPosition;
    [SerializeField] private Quaternion authoringPlayerLocalRotation = Quaternion.identity;
    [Tooltip("Pose locale du preview Enemy dans AnimationLab.")]
    [SerializeField] private Vector3 authoringEnemyLocalPosition;
    [SerializeField] private Quaternion authoringEnemyLocalRotation = Quaternion.identity;
    [Tooltip("Milieu local de la formation Player/Enemy bakee.")]
    [SerializeField] private Vector3 authoringFormationCenter;
    [Tooltip("Axe local Player vers Enemy de la formation bakee.")]
    [SerializeField] private Vector3 authoringFormationForward = Vector3.forward;
    [SerializeField] private bool logCameraDiagnostics = true;

    private readonly List<ICombatCinematicParticipant> participants = new List<ICombatCinematicParticipant>();
    private CombatCinematicContext context;
    private PlayableAsset bakedTimeline;
    private CinemachineBrain gameplayBrain;
    private CombatLockOnCameraController gameplayLockCamera;
    private bool sessionActive;
    private bool stopRaised;
    private bool cameraMismatchReported;
    private int sessionToken;

    public PlayableDirector Director => director;
    public SignalReceiver SignalReceiver => signalReceiver;
    public IReadOnlyList<CombatCinematicCameraBinding> CameraBindings => cameraBindings;
    public IReadOnlyList<CombatCinematicTrackBinding> TrackBindings => trackBindings;
    public bool HasAuthoringStageLayout => (authoringEnemyLocalPosition - authoringPlayerLocalPosition).sqrMagnitude > 0.0001f;
    public event Action<CombatCinematicRig> Stopped;

    public void ConfigureAuthoringStageLayout(
        Vector3 playerLocalPosition,
        Quaternion playerLocalRotation,
        Vector3 enemyLocalPosition,
        Quaternion enemyLocalRotation)
    {
        authoringPlayerLocalPosition = playerLocalPosition;
        authoringPlayerLocalRotation = playerLocalRotation;
        authoringEnemyLocalPosition = enemyLocalPosition;
        authoringEnemyLocalRotation = enemyLocalRotation;
        authoringFormationCenter = (playerLocalPosition + enemyLocalPosition) * 0.5f;
        authoringFormationForward = enemyLocalPosition - playerLocalPosition;
        authoringFormationForward.y = 0f;
        if (authoringFormationForward.sqrMagnitude <= 0.0001f) authoringFormationForward = Vector3.forward;
        else authoringFormationForward.Normalize();
    }

    public Quaternion GetStageRotationForFacing(Vector3 liveFacing)
    {
        liveFacing.y = 0f;
        if (liveFacing.sqrMagnitude <= 0.0001f) liveFacing = Vector3.forward;
        Vector3 authoringFacing = authoringFormationForward.sqrMagnitude <= 0.0001f
            ? Vector3.forward
            : authoringFormationForward;
        return Quaternion.LookRotation(liveFacing.normalized, Vector3.up) *
               Quaternion.Inverse(Quaternion.LookRotation(authoringFacing.normalized, Vector3.up));
    }

    public void GetStageActorPoses(
        Vector3 formationCenterWorld,
        Quaternion stageRotation,
        out Vector3 rigPosition,
        out Vector3 playerPosition,
        out Quaternion playerRotation,
        out Vector3 enemyPosition,
        out Quaternion enemyRotation)
    {
        rigPosition = formationCenterWorld - stageRotation * authoringFormationCenter;
        playerPosition = rigPosition + stageRotation * authoringPlayerLocalPosition;
        playerRotation = stageRotation * authoringPlayerLocalRotation;
        enemyPosition = rigPosition + stageRotation * authoringEnemyLocalPosition;
        enemyRotation = stageRotation * authoringEnemyLocalRotation;
    }

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
        if (director != null)
        {
            director.played += OnDirectorPlayed;
            director.stopped += OnDirectorStopped;
        }
    }

    private void OnDisable()
    {
        if (director != null)
        {
            director.played -= OnDirectorPlayed;
            director.stopped -= OnDirectorStopped;
        }
        EndCameraSession("OnDisable");
    }

    public bool TryPlay(
        CombatCinematicContext playbackContext,
        PlayableAsset timeline,
        string playerAnimatorTrack,
        string enemyAnimatorTrack,
        CombatCinematicStagePlacement? stagePlacement,
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
        sessionToken++;
        sessionActive = true;
        stopRaised = false;
        cameraMismatchReported = false;
        if (stagePlacement.HasValue)
        {
            if (!ApplyStagePlacement(stagePlacement.Value, out error))
            {
                AbortStart("Placement cinematographique invalide");
                return false;
            }
        }
        else
        {
            PositionAtPlayerFacingTarget();
        }
        if (!BeginParticipants())
        {
            error = "Un participant de cinematique a refuse le contexte courant.";
            AbortStart("Participant refuse");
            return false;
        }

        director.playableAsset = timelineAsset;
        bakedTimeline ??= timelineAsset;
        if (!TryBindTimeline(timelineAsset, playerAnimatorTrack, enemyAnimatorTrack, out error))
        {
            AbortStart("Binding Timeline invalide");
            return false;
        }

        LitTimelineCinemachineBridge cameraBridge = GetComponent<LitTimelineCinemachineBridge>();
        if (cameraBridge == null || !cameraBridge.BeginCameraControlNow(gameplayBrain))
        {
            error = "Impossible de donner le controle de la camera gameplay explicite a la Timeline.";
            AbortStart("Camera gameplay invalide");
            return false;
        }

        TraceCamera("Bind Timeline -> Brain -> controle Timeline", false);
        director.time = 0d;
        director.Evaluate();
        TraceCamera("Premiere Evaluate", false);
        director.Play();
        return true;
    }

    public bool TryPlay(
        CombatCinematicContext playbackContext,
        PlayableAsset timeline,
        string playerAnimatorTrack,
        string enemyAnimatorTrack,
        out string error)
    {
        return TryPlay(playbackContext, timeline, playerAnimatorTrack, enemyAnimatorTrack, null, out error);
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

        EndCameraSession("Remise en pool");
        director.playableAsset = bakedTimeline;
        context = null;
        gameplayBrain = null;
        gameplayLockCamera = null;
        transform.SetParent(null, true);
        gameObject.SetActive(false);
    }

    private bool TryBindTimeline(TimelineAsset timeline, string playerTrack, string enemyTrack, out string error)
    {
        error = null;
        gameplayBrain = ResolveGameplayBrain();
        if (gameplayBrain == null)
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
                director.SetGenericBinding(output.sourceObject, gameplayBrain);
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

    private CinemachineBrain ResolveGameplayBrain()
    {
        gameplayLockCamera = context != null && context.CombatManager != null
            ? context.CombatManager.GetComponent<CombatLockOnCameraController>()
            : null;
        Camera gameplayCamera = gameplayLockCamera != null ? gameplayLockCamera.ControlledCamera : null;
        return gameplayCamera != null ? gameplayCamera.GetComponent<CinemachineBrain>() : null;
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
        Transform playerAnchor = context.PlayerAnimator != null
            ? context.PlayerAnimator.transform
            : context.PlayerRoot;
        Transform target = context.TargetLockPoint != null ? context.TargetLockPoint : context.TargetEnemy.transform;
        Vector3 direction = target.position - playerAnchor.position;
        direction.y = 0f;
        Quaternion playerFacing = direction.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(direction.normalized, Vector3.up)
            : playerAnchor.rotation;

        // AnimationLab records cameras in its stage coordinate system, where the
        // preview player is not necessarily at the stage origin. Recreate that
        // exact frame around the live player before Timeline evaluates Camera_1.
        transform.rotation = playerFacing * Quaternion.Inverse(authoringPlayerLocalRotation);
        transform.position = playerAnchor.position - transform.rotation * authoringPlayerLocalPosition;
    }

    private bool ApplyStagePlacement(CombatCinematicStagePlacement placement, out string error)
    {
        error = null;
        transform.SetPositionAndRotation(placement.RigPosition, placement.RigRotation);

        LitOpsiveLocomotionBridge playerBridge = context.PlayerRoot.GetComponent<LitOpsiveLocomotionBridge>();
        if (playerBridge != null)
        {
            if (!playerBridge.SetExternalPositionAndRotation(placement.PlayerPosition, placement.PlayerRotation, true))
            {
                error = "UCC a refuse le placement cinematographique de Lucian.";
                return false;
            }
        }
        else
        {
            context.PlayerRoot.SetPositionAndRotation(placement.PlayerPosition, placement.PlayerRotation);
        }

        RealTimeCombatEnemyBehaviour enemyBehaviour = context.TargetEnemy.GetComponent<RealTimeCombatEnemyBehaviour>();
        if (enemyBehaviour != null)
        {
            if (!enemyBehaviour.PlaceForCinematic(placement.EnemyPosition, placement.EnemyRotation))
            {
                error = "Le NavMesh de l'ennemi refuse le plateau cinematographique.";
                return false;
            }
        }
        else
        {
            context.TargetEnemy.transform.SetPositionAndRotation(placement.EnemyPosition, placement.EnemyRotation);
        }

        Physics.SyncTransforms();
        return true;
    }

    private void OnDirectorStopped(PlayableDirector stoppedDirector)
    {
        if (stoppedDirector == director) CompleteStop();
    }

    private void OnDirectorPlayed(PlayableDirector playedDirector)
    {
        if (playedDirector != director || !sessionActive) return;
        TraceCamera("PlayableDirector.played", true, true);
    }

    private void CompleteStop()
    {
        if (!sessionActive || stopRaised) return;
        stopRaised = true;
        EndCameraSession("Fin Timeline");
        Stopped?.Invoke(this);
    }

    private void AbortStart(string reason)
    {
        EndCameraSession(reason);
        context = null;
        gameplayBrain = null;
        gameplayLockCamera = null;
    }

    private void EndCameraSession(string reason)
    {
        if (!sessionActive && context == null) return;

        LitTimelineCinemachineBridge bridge = GetComponent<LitTimelineCinemachineBridge>();
        bridge?.EndCameraControlNow();
        EndParticipants();
        TraceCamera("Restitution camera: " + reason, false);
        sessionActive = false;
    }

    private void Update()
    {
        if (sessionActive && director != null && director.state == PlayState.Playing)
            TraceCamera("Verification runtime", true);
    }

    private void TraceCamera(string phase, bool validateAuthority, bool logDetail = false)
    {
        if (!logCameraDiagnostics || gameplayBrain == null) return;

        CinemachineCamera active = gameplayBrain.ActiveVirtualCamera as CinemachineCamera;
        bool activeCameraMatches = active == null;
        for (int i = 0; i < cameraBindings.Count; i++)
        {
            if (cameraBindings[i] != null && cameraBindings[i].camera == active)
            {
                activeCameraMatches = true;
                break;
            }
        }

        if (validateAuthority && !activeCameraMatches && !cameraMismatchReported)
        {
            cameraMismatchReported = true;
            Debug.LogError("[CombatCinematic Camera] Autorite invalide. Brain='" + gameplayBrain.name +
                           "', active='" + (active != null ? active.name : "None") +
                           "', rig='" + name + "', token=" + sessionToken + ".", this);
        }

        if (!validateAuthority || logDetail)
        {
            Debug.Log("[CombatCinematic Camera] " + phase + " | rig='" + name + "' | timeline='" +
                      (director != null && director.playableAsset != null ? director.playableAsset.name : "None") +
                      "' | brain='" + gameplayBrain.name + "' | gameplayCamera='" +
                      (gameplayLockCamera != null && gameplayLockCamera.ControlledCamera != null
                          ? gameplayLockCamera.ControlledCamera.name : "None") +
                      "' | active='" + (active != null ? active.name : "None") + "' | player='" +
                      (context != null && context.PlayerRoot != null ? context.PlayerRoot.name : "None") +
                      "' | enemyLockPoint='" +
                      (context != null && context.TargetLockPoint != null ? context.TargetLockPoint.name : "None") +
                      "' | token=" + sessionToken + ".", this);
        }
    }
}
