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

[Serializable]
public sealed class CombatCinematicFramingReference
{
    public double timelineTime;
    public string cameraKey;
    public Vector3 playerAnimatorStagePosition;
    public Quaternion playerAnimatorStageRotation = Quaternion.identity;
    public Vector3 enemyAnimatorStagePosition;
    public Quaternion enemyAnimatorStageRotation = Quaternion.identity;
    public Vector3 cameraStagePosition;
    public Quaternion cameraStageRotation = Quaternion.identity;
    public float cameraFieldOfView;
}

public enum CombatCinematicTimeMode
{
    Unscaled,
    GlobalScaled
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
    public CombatCinematicCasterRole CasterRole { get; }
    public RealTimeCombatEnemy CasterEnemy { get; }
    public bool AllowGameplayCameraFallback { get; }
    public bool TransferTimelineRootMotion { get; }
    public bool AllowMissingTimelineBindings { get; }
    public CombatCinematicTimeMode TimeMode { get; }

    public CombatCinematicContext(
        RealTimeCombatManager manager,
        UnityEngine.Object definition,
        Action resolveImpact = null,
        CombatCinematicCasterRole casterRole = CombatCinematicCasterRole.Player,
        RealTimeCombatEnemy casterEnemy = null,
        bool allowGameplayCameraFallback = false,
        bool transferTimelineRootMotion = true,
        bool allowMissingTimelineBindings = false,
        CombatCinematicTimeMode timeMode = CombatCinematicTimeMode.Unscaled)
    {
        CombatManager = manager;
        Definition = definition;
        PlayerRoot = manager != null ? manager.PlayerRoot : null;
        PlayerAnimator = manager != null ? manager.PlayerAnimator : null;
        TargetEnemy = manager != null ? (manager.EngagedEnemy ?? manager.LockedEnemy) : null;
        TargetAnimator = TargetEnemy != null ? TargetEnemy.Animator : null;
        TargetLockPoint = TargetEnemy != null ? TargetEnemy.LockPoint : null;
        ResolveImpact = resolveImpact;
        CasterRole = casterRole;
        CasterEnemy = casterEnemy;
        AllowGameplayCameraFallback = allowGameplayCameraFallback;
        TransferTimelineRootMotion = transferTimelineRootMotion;
        AllowMissingTimelineBindings = allowMissingTimelineBindings;
        TimeMode = timeMode;
    }
}

public enum CombatCinematicCasterRole
{
    Player,
    Enemy
}

public interface ICombatCinematicParticipant
{
    bool Begin(CombatCinematicContext context);
    void End();
}

/// <summary>Optional completion hook, invoked only after a Timeline ends naturally.</summary>
public interface ICombatCinematicCompletionParticipant
{
    void Complete(CombatCinematicContext context);
}

public enum CombatCinematicEndReason
{
    Completed,
    Interrupted,
    Failed
}

/// <summary>Relative authoring formation resolved directly at the live combat midpoint.</summary>
public readonly struct CombatCinematicPlacement
{
    public readonly Vector3 RigPosition;
    public readonly Quaternion RigRotation;
    public readonly Vector3 PlayerPosition;
    public readonly Quaternion PlayerRotation;
    public readonly Vector3 EnemyPosition;
    public readonly Quaternion EnemyRotation;

    public CombatCinematicPlacement(
        Vector3 rigPosition,
        Quaternion rigRotation,
        Vector3 playerPosition,
        Quaternion playerRotation,
        Vector3 enemyPosition,
        Quaternion enemyRotation)
    {
        RigPosition = rigPosition;
        RigRotation = rigRotation;
        PlayerPosition = playerPosition;
        PlayerRotation = playerRotation;
        EnemyPosition = enemyPosition;
        EnemyRotation = enemyRotation;
    }
}

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayableDirector), typeof(SignalReceiver), typeof(LitTimelineCinemachineBridge))]
public sealed class CombatCinematicRig : MonoBehaviour
{
    [SerializeField] private PlayableDirector director;
    [SerializeField] private SignalReceiver signalReceiver;
    [SerializeField] private List<CombatCinematicCameraBinding> cameraBindings = new List<CombatCinematicCameraBinding>();
    [SerializeField] private List<CombatCinematicTrackBinding> trackBindings = new List<CombatCinematicTrackBinding>();
    [SerializeField, Tooltip("Releves de cadrage captures dans AnimationLab et compares pendant la lecture runtime.")]
    private List<CombatCinematicFramingReference> framingReferences = new List<CombatCinematicFramingReference>();
    [Header("Authoring Stage Layout")]
    [Tooltip("Repere runtime du root Player, copie de Lucian_Anchor au bake.")]
    [SerializeField] private Transform playerStageAnchor;
    [Tooltip("Repere runtime du root Enemy, copie de Enemy_Anchor au bake.")]
    [SerializeField] private Transform enemyStageAnchor;
    [Tooltip("Pose locale de Lucian_Anchor dans AnimationLab.")]
    [SerializeField] private Vector3 authoringPlayerLocalPosition;
    [SerializeField] private Quaternion authoringPlayerLocalRotation = Quaternion.identity;
    [SerializeField, Tooltip("Orientation monde du repere d'auteur, utilisee pour convertir les deltas Timeline vers le plateau runtime.")]
    private Quaternion authoringStageWorldRotation = Quaternion.identity;
    [Tooltip("Pose locale du preview Enemy dans AnimationLab.")]
    [SerializeField] private Vector3 authoringEnemyLocalPosition;
    [SerializeField] private Quaternion authoringEnemyLocalRotation = Quaternion.identity;
    [Tooltip("Milieu local de la formation Player/Enemy bakee.")]
    [SerializeField] private Vector3 authoringFormationCenter;
    [Tooltip("Axe local Player vers Enemy de la formation bakee.")]
    [SerializeField] private Vector3 authoringFormationForward = Vector3.forward;
    [SerializeField, HideInInspector] private int authoringStageLayoutVersion;
    [SerializeField, Tooltip("Transfere le root motion relatif de la Timeline vers le root UCC de Lucian.")]
    private bool drivePlayerRootMotionFromTimeline = true;
    [SerializeField] private bool logCameraDiagnostics = true;
    [SerializeField, Tooltip("Journalise les poses bakees et les poses appliquees aux acteurs pendant une LightSkill.")]
    private bool logPlacementDiagnostics = true;

    private readonly List<ICombatCinematicParticipant> participants = new List<ICombatCinematicParticipant>();
    private CombatCinematicContext context;
    private PlayableAsset bakedTimeline;
    private CinemachineBrain gameplayBrain;
    private CombatLockOnCameraController gameplayLockCamera;
    private bool sessionActive;
    private bool stopRaised;
    private bool cameraMismatchReported;
    private int sessionToken;
    private float nextRootMotionLogTime;
    private Vector3 playerAnimatorRestLocalPosition;
    private Quaternion playerAnimatorRestLocalRotation;
    private Vector3 enemyAnimatorRestLocalPosition;
    private Quaternion enemyAnimatorRestLocalRotation;
    private bool hasEnemyAnimatorRestPose;
    private bool skipFirstPlayerRootMotionDelta;
    private bool skipFirstEnemyRootMotionDelta;
    // A staged LightSkill uses Timeline scene offsets. Timeline is then the sole
    // owner of the actor roots for the duration of the cinematic.
    private bool timelineOwnsStagedActorTransforms;
    private int nextFramingReferenceIndex;
    private CombatCinematicEndReason? requestedEndReason;
    private float localPlaybackScale = 1f;
    private bool localPlaybackManualClock;

    // Prefab assets do not execute Awake while displayed in an Inspector. Keep
    // this accessor lazy so authoring validation observes the same baked
    // PlayableDirector that runtime playback resolves in Awake.
    public PlayableDirector Director => director != null ? director : GetComponent<PlayableDirector>();
    public SignalReceiver SignalReceiver => signalReceiver;
    public IReadOnlyList<CombatCinematicCameraBinding> CameraBindings => cameraBindings;
    public IReadOnlyList<CombatCinematicTrackBinding> TrackBindings => trackBindings;
    public bool HasAuthoringStageLayout => authoringStageLayoutVersion >= 3 &&
                                           playerStageAnchor != null && enemyStageAnchor != null;
    public CombatCinematicEndReason LastEndReason { get; private set; } = CombatCinematicEndReason.Interrupted;
    public event Action<CombatCinematicRig> Stopped;

    /// <summary>Applies a local presentation rate without changing global time.</summary>
    public void SetLocalPlaybackScale(float scale)
    {
        localPlaybackScale = Mathf.Clamp(scale, 0.01f, 1f);
        ApplyLocalPlaybackScale();
        TracePlacement("Echelle Timeline locale=" + localPlaybackScale.ToString("F2") +
                       " | mode=" + (localPlaybackManualClock ? "Manual" : "normal") + ".");
    }

    public void ConfigureFramingReferences(IEnumerable<CombatCinematicFramingReference> references)
    {
        framingReferences.Clear();
        if (references == null) return;

        foreach (CombatCinematicFramingReference reference in references)
        {
            if (reference == null) continue;
            framingReferences.Add(new CombatCinematicFramingReference
            {
                timelineTime = reference.timelineTime,
                cameraKey = reference.cameraKey,
                playerAnimatorStagePosition = reference.playerAnimatorStagePosition,
                playerAnimatorStageRotation = reference.playerAnimatorStageRotation,
                enemyAnimatorStagePosition = reference.enemyAnimatorStagePosition,
                enemyAnimatorStageRotation = reference.enemyAnimatorStageRotation,
                cameraStagePosition = reference.cameraStagePosition,
                cameraStageRotation = reference.cameraStageRotation,
                cameraFieldOfView = reference.cameraFieldOfView
            });
        }

        framingReferences.Sort((left, right) => left.timelineTime.CompareTo(right.timelineTime));
    }

    public bool HasCameraBinding(string cameraKey)
    {
        if (string.IsNullOrEmpty(cameraKey)) return false;

        for (int i = 0; i < cameraBindings.Count; i++)
        {
            if (cameraBindings[i] != null && string.Equals(cameraBindings[i].timelineCameraKey, cameraKey, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public void ConfigureAuthoringStageLayout(
        Transform authoringRoot,
        Transform playerAnchor,
        Transform enemyAnchor)
    {
        if (authoringRoot == null || playerAnchor == null || enemyAnchor == null)
        {
            authoringStageLayoutVersion = 0;
            return;
        }

        authoringPlayerLocalPosition = authoringRoot.InverseTransformPoint(playerAnchor.position);
        authoringPlayerLocalRotation = Quaternion.Inverse(authoringRoot.rotation) * playerAnchor.rotation;
        authoringStageWorldRotation = authoringRoot.rotation;
        authoringEnemyLocalPosition = authoringRoot.InverseTransformPoint(enemyAnchor.position);
        authoringEnemyLocalRotation = Quaternion.Inverse(authoringRoot.rotation) * enemyAnchor.rotation;
        ConfigureRuntimeAnchor(ref playerStageAnchor, "PlayerStageAnchor", authoringPlayerLocalPosition, authoringPlayerLocalRotation);
        ConfigureRuntimeAnchor(ref enemyStageAnchor, "EnemyStageAnchor", authoringEnemyLocalPosition, authoringEnemyLocalRotation);
        authoringFormationCenter = (authoringPlayerLocalPosition + authoringEnemyLocalPosition) * 0.5f;
        authoringFormationForward = authoringEnemyLocalPosition - authoringPlayerLocalPosition;
        authoringFormationForward.y = 0f;
        if (authoringFormationForward.sqrMagnitude <= 0.0001f) authoringFormationForward = Vector3.forward;
        else authoringFormationForward.Normalize();
        authoringStageLayoutVersion = 3;
    }

    private void ConfigureRuntimeAnchor(ref Transform anchor, string anchorName, Vector3 localPosition, Quaternion localRotation)
    {
        if (anchor == null)
        {
            Transform existing = transform.Find(anchorName);
            anchor = existing != null ? existing : new GameObject(anchorName).transform;
            anchor.SetParent(transform, false);
        }

        anchor.SetLocalPositionAndRotation(localPosition, localRotation);
        anchor.localScale = Vector3.one;
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

    public bool TryGetMidpointPlacement(CombatCinematicContext playbackContext, out CombatCinematicPlacement placement, out string error)
    {
        placement = default;
        error = null;
        if (!HasAuthoringStageLayout)
        {
            error = "Le rig cinematographique doit etre rebake avec les poses Player et Enemy.";
            return false;
        }
        if (playbackContext == null || playbackContext.PlayerRoot == null || playbackContext.TargetEnemy == null)
        {
            error = "Lucian ou l'ennemi verrouille est introuvable.";
            return false;
        }

        Vector3 playerPosition = playbackContext.PlayerRoot.position;
        Vector3 enemyPosition = playbackContext.TargetEnemy.transform.position;
        Vector3 direction = enemyPosition - playerPosition;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = playbackContext.PlayerRoot.forward;
            direction.y = 0f;
        }

        Quaternion stageRotation = GetStageRotationForFacing(direction);
        Vector3 midpoint = Vector3.Lerp(playerPosition, enemyPosition, 0.5f);
        Vector3 rigPosition = midpoint;
        Vector3 stagedPlayerPosition = rigPosition + stageRotation * playerStageAnchor.localPosition;
        Quaternion stagedPlayerRotation = stageRotation * playerStageAnchor.localRotation;
        Vector3 stagedEnemyPosition = rigPosition + stageRotation * enemyStageAnchor.localPosition;
        Quaternion stagedEnemyRotation = stageRotation * enemyStageAnchor.localRotation;
        placement = new CombatCinematicPlacement(
            rigPosition, stageRotation,
            stagedPlayerPosition, stagedPlayerRotation,
            stagedEnemyPosition, stagedEnemyRotation);
        TracePlacement("Placement midpoint calcule | layoutVersion=" + authoringStageLayoutVersion +
                       " | playerLocal=" + authoringPlayerLocalPosition + " | enemyLocal=" + authoringEnemyLocalPosition +
                       " | rig=" + rigPosition + " | player=" + stagedPlayerPosition + " | enemy=" + stagedEnemyPosition + ".");
        return true;
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
        NormalizeRuntimeDirector();
        MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is ICombatCinematicParticipant participant)
                participants.Add(participant);
        }
    }

    private void LateUpdate()
    {
        if (!sessionActive || context == null)
        {
            return;
        }

        LitTimelineCinemachineBridge cameraBridge = GetComponent<LitTimelineCinemachineBridge>();
        cameraBridge?.UpdateTimelineCameraNow();

        if (timelineOwnsStagedActorTransforms)
        {
            return;
        }

        if (!context.TransferTimelineRootMotion)
        {
            return;
        }

        AdvancePlayerRootFromTimelineMotion();
        AdvanceEnemyRootFromTimelineMotion();
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
        CombatCinematicPlacement? placement,
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

        if (timeline == null)
        {
            error = "La sequence de combat est introuvable.";
            return false;
        }

        context = playbackContext;
        sessionToken++;
        sessionActive = true;
        stopRaised = false;
        requestedEndReason = null;
        cameraMismatchReported = false;
        nextFramingReferenceIndex = 0;
        TracePlacement("TryPlay | token=" + sessionToken + " | timeline='" + timeline.name + "' | type=" + timeline.GetType().Name +
                       " | placement=" + placement.HasValue + ".");
        if (placement.HasValue)
        {
            if (!ApplyPlacement(placement.Value, out error))
            {
                AbortStart("Placement cinematographique invalide");
                return false;
            }

        }
        else
        {
            PositionAtPlayerFacingTarget();
        }

        // Timeline samples Animator.deltaPosition itself in LateUpdate and
        // transfers it to ActorRoot. Staged LightSkills and the new in-place
        // SkillSO path therefore disable the normal relay to avoid a double
        // application. CounterSkill keeps its established path unchanged.
        if (placement.HasValue || playbackContext.Definition is SkillSO || !playbackContext.TransferTimelineRootMotion)
        {
            BeginContractCinematicMotion();
            SetContractRootMotionRelayEnabled(false);
        }
        if (!BeginParticipants())
        {
            error = "Un participant de cinematique a refuse le contexte courant.";
            AbortStart("Participant refuse");
            return false;
        }
        TracePlacement("Participants acceptes.");

        director.playableAsset = timeline;
        bakedTimeline ??= timeline;
        NormalizeRuntimeDirector();
        if (!TryBindTimeline(timeline, playerAnimatorTrack, enemyAnimatorTrack, out error))
        {
            AbortStart("Binding Timeline invalide");
            return false;
        }
        if (placement.HasValue && !UsesSceneRelativeActorTracks(timeline, playerAnimatorTrack, enemyAnimatorTrack))
        {
            error = "Le package LightSkill utilise encore des pistes acteur non relatives. Rebakez le LightSkill depuis AnimationLab.";
            AbortStart("Package runtime obsolete");
            return false;
        }
        TracePlacement("Bindings Timeline acceptes | playerTrack='" + playerAnimatorTrack + "' | enemyTrack='" + enemyAnimatorTrack + "'.");
        TraceActorTrackAuthority(timeline, playerAnimatorTrack, enemyAnimatorTrack);

        CinemachineCamera expectedOpeningCamera = ResolveOpeningCamera();
        bool useGameplayCameraFallback = expectedOpeningCamera == null && context.AllowGameplayCameraFallback;
        LitTimelineCinemachineBridge cameraBridge = GetComponent<LitTimelineCinemachineBridge>();
        if (!useGameplayCameraFallback && (cameraBridge == null || !cameraBridge.BeginCameraControlNow(gameplayBrain)))
        {
            error = "Impossible de donner le controle de la camera gameplay explicite a la Timeline.";
            AbortStart("Camera gameplay invalide");
            return false;
        }

        if (useGameplayCameraFallback)
        {
            TraceCamera("Camera cinematique absente : lecture maintenue sur la camera gameplay", false);
        }

        // The rig is the single owner of all Timeline bindings and graph setup.
        // The bridge only hands the gameplay camera to Cinemachine.
        director.RebuildGraph();
        ApplyLocalPlaybackScale();
        TraceCamera("Bind Timeline -> Brain -> controle Timeline -> RebuildGraph", false);
        director.time = 0d;
        CaptureActorAnimatorRestPoses();
        director.Evaluate();
        if (placement.HasValue)
        {
            // The t=0 evaluation may still touch the actor roots. Restore the
            // canonical anchors once, then let scene-relative Timeline tracks own
            // the transforms until the sequence ends. Never feed a moved UCC root
            // back into Timeline every frame.
            if (!ApplyPlacement(placement.Value, out error))
            {
                AbortStart("Reinitialisation des acteurs apres Evaluate invalide");
                return false;
            }
            timelineOwnsStagedActorTransforms = true;
        }
        skipFirstPlayerRootMotionDelta = true;
        skipFirstEnemyRootMotionDelta = true;
        if (!useGameplayCameraFallback && !cameraBridge.UpdateTimelineCameraNow())
        {
            error = "La Brain Cinemachine gameplay n'a pas pu evaluer le pre-roll.";
            AbortStart("Pre-roll Brain indisponible");
            return false;
        }

        CinemachineCamera activeCamera = gameplayBrain.ActiveVirtualCamera as CinemachineCamera;
        if (!useGameplayCameraFallback && (expectedOpeningCamera == null || activeCamera != expectedOpeningCamera))
        {
            error = "Pre-roll Cinemachine invalide : attendue='" +
                    (expectedOpeningCamera != null ? expectedOpeningCamera.name : "None") +
                    "', active='" + (activeCamera != null ? activeCamera.name : "None") + "'.";
            AbortStart("Camera d'ouverture non stabilisee");
            return false;
        }

        TracePlacement(useGameplayCameraFallback
            ? "Pre-roll Timeline valide | camera gameplay conservee."
            : "Pre-roll Cinemachine valide | camera='" + activeCamera.name + "'.");
        CaptureReachedFramingReferences();
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
        RequestEnd(CombatCinematicEndReason.Interrupted);
    }

    public void ResetForPool()
    {
        localPlaybackScale = 1f;
        localPlaybackManualClock = false;
        RequestEnd(CombatCinematicEndReason.Interrupted, false);
        EndCameraSession("Remise en pool", CombatCinematicEndReason.Interrupted);
        ClearTimelineBindings();
        if (director != null && director.playableAsset != null)
        {
            director.Stop();
            director.time = 0d;
            director.playableAsset = bakedTimeline;
            NormalizeRuntimeDirector();
            director.RebuildGraph();
            ApplyLocalPlaybackScale();
        }
        context = null;
        gameplayBrain = null;
        gameplayLockCamera = null;
        ResetTimelineActorTransformSampling();
        nextFramingReferenceIndex = 0;
        transform.SetParent(null, false);
        transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        gameObject.SetActive(false);
    }

    private void ClearTimelineBindings()
    {
        if (director == null || director.playableAsset == null)
        {
            return;
        }

        foreach (PlayableBinding output in director.playableAsset.outputs)
        {
            director.ClearGenericBinding(output.sourceObject);
            if (output.sourceObject is not CinemachineTrack cameraTrack)
            {
                continue;
            }

            foreach (TimelineClip clip in cameraTrack.GetClips())
            {
                if (clip.asset is CinemachineShot shot)
                {
                    director.ClearReferenceValue(shot.VirtualCamera.exposedName);
                }
            }
        }
    }

    private bool TryBindTimeline(PlayableAsset timeline, string playerTrack, string enemyTrack, out string error)
    {
        error = null;
        bool allowPartialBindings = context != null && context.AllowMissingTimelineBindings;
        gameplayBrain = ResolveGameplayBrain();
        if (gameplayBrain == null && !allowPartialBindings)
        {
            error = "CinemachineBrain de la camera de jeu introuvable.";
            return false;
        }

        if (gameplayBrain == null)
        {
            TracePlacement("Binding Cinemachine ignore : Brain gameplay absente | fallback partiel autorise.");
        }

        bool playerBound = false;
        bool enemyBound = false;
        bool cameraTrackBound = false;
        bool signalBound = false;
        List<string> skippedBindings = allowPartialBindings ? new List<string>() : null;
        foreach (PlayableBinding output in timeline.outputs)
        {
            if (output.sourceObject == null) continue;
            if (output.sourceObject is AnimationTrack && output.streamName == playerTrack)
            {
                if (context.PlayerAnimator != null)
                {
                    director.SetGenericBinding(output.sourceObject, context.PlayerAnimator);
                    playerBound = true;
                    TracePlacement("Binding Timeline | piste='" + output.streamName + "' | Animator='" + context.PlayerAnimator.name + "'.");
                }
                else if (allowPartialBindings)
                {
                    skippedBindings.Add("Player.Animator (Animator runtime absent)");
                }
            }
            else if (output.sourceObject is AnimationTrack && output.streamName == enemyTrack)
            {
                if (context.TargetAnimator != null)
                {
                    director.SetGenericBinding(output.sourceObject, context.TargetAnimator);
                    enemyBound = true;
                    TracePlacement("Binding Timeline | piste='" + output.streamName + "' | Animator='" + context.TargetAnimator.name + "'.");
                }
                else if (allowPartialBindings)
                {
                    skippedBindings.Add("Enemy.Animator (Animator runtime absent)");
                }
            }
            else if (output.sourceObject is SignalTrack)
            {
                if (signalReceiver != null)
                {
                    director.SetGenericBinding(output.sourceObject, signalReceiver);
                    signalBound = true;
                }
                else if (allowPartialBindings)
                {
                    skippedBindings.Add("Signals (SignalReceiver runtime absent)");
                }
            }
            else if (output.sourceObject is CinemachineTrack cameraTrack)
            {
                if (gameplayBrain != null)
                {
                    director.SetGenericBinding(output.sourceObject, gameplayBrain);
                }
                else if (allowPartialBindings)
                {
                    skippedBindings.Add("Cinemachine (Brain gameplay absente)");
                }
                foreach (TimelineClip clip in cameraTrack.GetClips())
                {
                    if (!(clip.asset is CinemachineShot shot)) continue;
                    CinemachineCamera camera = ResolveCamera(shot.VirtualCamera.exposedName.ToString());
                    if (camera == null)
                    {
                        if (context != null && context.AllowGameplayCameraFallback)
                        {
                            TracePlacement("Binding Cinemachine absent | cle='" + shot.VirtualCamera.exposedName + "' | fallback camera gameplay.");
                            continue;
                        }
                        error = "Camera introuvable pour la cle Timeline '" + shot.VirtualCamera.exposedName + "'.";
                        return false;
                    }
                    director.SetReferenceValue(shot.VirtualCamera.exposedName, camera);
                    TracePlacement("Binding Cinemachine | cle='" + shot.VirtualCamera.exposedName + "' | camera='" + camera.name + "'.");
                }
                cameraTrackBound = true;
            }
            else if (TryResolveTrackBinding(output.streamName, out UnityEngine.Object target))
            {
                director.SetGenericBinding(output.sourceObject, target);
                TracePlacement("Binding piste additionnelle | piste='" + output.streamName + "' | cible='" + target.name + "' (" + target.GetType().Name + ").");
            }
        }

        if (!allowPartialBindings && (!playerBound || !enemyBound || !cameraTrackBound || !signalBound))
        {
            error = "Pistes requises manquantes :" +
                    (!playerBound ? " Player.Animator" : string.Empty) +
                    (!enemyBound ? " Enemy.Animator" : string.Empty) +
                    (!cameraTrackBound ? " Cinemachine" : string.Empty) +
                    (!signalBound ? " Signals" : string.Empty);
            return false;
        }

        if (allowPartialBindings)
        {
            if (!playerBound && !string.IsNullOrWhiteSpace(playerTrack)) skippedBindings.Add("Player.Animator (piste absente)");
            if (!enemyBound && !string.IsNullOrWhiteSpace(enemyTrack)) skippedBindings.Add("Enemy.Animator (piste absente)");
            if (!cameraTrackBound) skippedBindings.Add("Cinemachine (piste absente)");
            if (!signalBound) skippedBindings.Add("Signals (piste absente)");
            TracePlacement(skippedBindings.Count == 0
                ? "Bindings Timeline partiels : toutes les pistes presentes ont ete liees."
                : "Bindings Timeline partiels ignores : " + string.Join(" | ", skippedBindings) + ".");
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
        // Stage placement is defined from gameplay roots. A root Animator is
        // now the enemy convention, while Lucian may still expose a visual
        // child Animator; neither topology may change the cinematic frame.
        Transform playerAnchor = context.PlayerRoot;
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

    private bool ApplyPlacement(CombatCinematicPlacement placement, out string error)
    {
        error = null;
        TracePlacement("Application | avant player=" + context.PlayerRoot.position + " enemy=" + context.TargetEnemy.transform.position +
                        " | rig=" + placement.RigPosition + ".");
        transform.SetPositionAndRotation(placement.RigPosition, placement.RigRotation);
        // Anchors define actor ROOT poses. No Animator-to-root conversion is
        // allowed here: that conversion was the source of world-space drift.
        Vector3 playerRootPosition = placement.PlayerPosition;
        Quaternion playerRootRotation = placement.PlayerRotation;
        Vector3 enemyRootPosition = placement.EnemyPosition;
        Quaternion enemyRootRotation = placement.EnemyRotation;
        TracePlacement("Anchors -> roots | PlayerStageAnchor=" + playerStageAnchor.localPosition +
                       " EnemyStageAnchor=" + enemyStageAnchor.localPosition +
                       " | playerRoot=" + playerRootPosition + " | enemyRoot=" + enemyRootPosition + ".");

        CombatActorAnimationRoot playerContract = context.PlayerRoot.GetComponent<CombatActorAnimationRoot>();
        if (playerContract != null && playerContract.ValidateContract(out _))
        {
            if (!playerContract.SetActorPose(playerRootPosition, playerRootRotation))
            {
                error = "Le contrat d'animation de Lucian ne peut pas poser son ActorRoot.";
                return false;
            }
            playerContract.ResetAnimationRootPose();
        }
        else
        {
            LitOpsiveLocomotionBridge playerBridge = context.PlayerRoot.GetComponent<LitOpsiveLocomotionBridge>();
            if (playerBridge != null && !playerBridge.SetCinematicPositionAndRotation(playerRootPosition, playerRootRotation, true))
            {
                error = "UCC ne possede pas de locomotion disponible pour le placement cinematographique de Lucian.";
                return false;
            }
            if (playerBridge == null)
            {
                context.PlayerRoot.SetPositionAndRotation(playerRootPosition, playerRootRotation);
            }
        }

        CombatActorAnimationRoot enemyContract = context.TargetEnemy.GetComponent<CombatActorAnimationRoot>();
        if (enemyContract != null && enemyContract.ValidateContract(out _))
        {
            if (!enemyContract.SetActorPose(enemyRootPosition, enemyRootRotation))
            {
                error = "Le contrat d'animation ennemi ne peut pas poser son ActorRoot.";
                return false;
            }
            enemyContract.ResetAnimationRootPose();
        }
        else
        {
            RealTimeCombatEnemyBehaviour enemyBehaviour = context.TargetEnemy.GetComponent<RealTimeCombatEnemyBehaviour>();
            if (enemyBehaviour != null && !enemyBehaviour.PlaceForCinematic(
                    enemyRootPosition,
                    enemyRootRotation))
            {
                error = "Le NavMesh de l'ennemi refuse le plateau cinematographique.";
                return false;
            }
            if (enemyBehaviour == null)
            {
                context.TargetEnemy.transform.SetPositionAndRotation(enemyRootPosition, enemyRootRotation);
            }
        }

        Physics.SyncTransforms();
        TracePlacement("Application terminee | rig=" + transform.position + " | player=" + context.PlayerRoot.position +
                       " playerAnimator=" + context.PlayerAnimator.transform.position +
                       " | enemy=" + context.TargetEnemy.transform.position +
                       " enemyAnimator=" + context.TargetAnimator.transform.position + ".");
        return true;
    }

    private void AdvancePlayerRootFromTimelineMotion()
    {
        if (!drivePlayerRootMotionFromTimeline ||
            context.PlayerRoot == null || context.PlayerAnimator == null)
        {
            return;
        }

        Animator playerAnimator = context.PlayerAnimator;
        // Timeline may write the authoring Transform pose directly to the child
        // Animator. It is presentation-only: restore its hierarchy pose and
        // transport only Animator.deltaPosition to the actual UCC root.
        RestorePlayerAnimatorRestLocalPose();
        if (skipFirstPlayerRootMotionDelta)
        {
            skipFirstPlayerRootMotionDelta = false;
            return;
        }
        Vector3 deltaPosition = playerAnimator.deltaPosition;
        Quaternion deltaRotation = playerAnimator.deltaRotation;
        bool hasPositionDelta = deltaPosition.sqrMagnitude > 0.00000025f;
        bool hasRotationDelta = Quaternion.Angle(deltaRotation, Quaternion.identity) > 0.01f;
        if (!hasPositionDelta && !hasRotationDelta)
        {
            return;
        }

        LitOpsiveLocomotionBridge bridge = context.PlayerRoot.GetComponent<LitOpsiveLocomotionBridge>();
        bool applied = bridge != null
            ? bridge.ApplyCinematicRootMotion(deltaPosition, deltaRotation)
            : ApplyTransformDelta(context.PlayerRoot, deltaPosition, deltaRotation);
        if (!applied) return;
        TraceRootMotion("Root motion relatif applique | deltaPosition=" + deltaPosition +
                        " | deltaRotation=" + deltaRotation.eulerAngles +
                        " | root=" + context.PlayerRoot.position + ".");
    }

    private void AdvanceEnemyRootFromTimelineMotion()
    {
        if (context?.TargetEnemy == null || context.TargetAnimator == null) return;

        RestoreEnemyAnimatorRestLocalPose();
        if (skipFirstEnemyRootMotionDelta)
        {
            skipFirstEnemyRootMotionDelta = false;
            return;
        }

        Vector3 deltaPosition = context.TargetAnimator.deltaPosition;
        Quaternion deltaRotation = context.TargetAnimator.deltaRotation;
        if (deltaPosition.sqrMagnitude <= 0.00000025f &&
            Quaternion.Angle(deltaRotation, Quaternion.identity) <= 0.01f)
        {
            return;
        }

        RealTimeCombatEnemyBehaviour behaviour = context.TargetEnemy.GetComponent<RealTimeCombatEnemyBehaviour>();
        if (behaviour != null)
        {
            behaviour.ApplyCinematicRootMotion(deltaPosition, deltaRotation);
        }
        else
        {
            ApplyTransformDelta(context.TargetEnemy.transform, deltaPosition, deltaRotation);
        }
    }

    private static bool ApplyTransformDelta(Transform actorRoot, Vector3 deltaPosition, Quaternion deltaRotation)
    {
        if (actorRoot == null) return false;
        actorRoot.SetPositionAndRotation(actorRoot.position + deltaPosition, deltaRotation * actorRoot.rotation);
        return true;
    }

    private void ResetTimelineActorTransformSampling()
    {
        timelineOwnsStagedActorTransforms = false;
    }

    private void BeginContractCinematicMotion()
    {
        context?.PlayerRoot?.GetComponent<CombatActorAnimationRoot>()?.BeginCinematicMotion(sessionToken);
        context?.TargetEnemy?.GetComponent<CombatActorAnimationRoot>()?.BeginCinematicMotion(sessionToken);
    }

    private void EndContractCinematicMotion()
    {
        context?.PlayerRoot?.GetComponent<CombatActorAnimationRoot>()?.EndCinematicMotion(sessionToken);
        context?.TargetEnemy?.GetComponent<CombatActorAnimationRoot>()?.EndCinematicMotion(sessionToken);
    }

    private void SetContractRootMotionRelayEnabled(bool enabled)
    {
        context?.PlayerRoot?.GetComponent<CombatActorAnimationRoot>()?.SetCinematicRootMotionRelayEnabled(enabled);
        context?.TargetEnemy?.GetComponent<CombatActorAnimationRoot>()?.SetCinematicRootMotionRelayEnabled(enabled);
        TracePlacement("Relais root motion cinematographique=" + enabled + ".");
    }

    private static bool UsesSceneRelativeActorTracks(PlayableAsset timeline, string playerTrack, string enemyTrack)
    {
        if (timeline == null) return false;

        bool playerRelative = false;
        bool enemyRelative = false;
        foreach (PlayableBinding output in timeline.outputs)
        {
            if (output.sourceObject is not AnimationTrack track) continue;
            if (output.streamName == playerTrack)
                playerRelative = track.trackOffset == TrackOffset.ApplySceneOffsets;
            else if (output.streamName == enemyTrack)
                enemyRelative = track.trackOffset == TrackOffset.ApplySceneOffsets;
        }

        return playerRelative && enemyRelative;
    }

    private void TraceActorTrackAuthority(PlayableAsset timeline, string playerTrack, string enemyTrack)
    {
        if (!logPlacementDiagnostics || timeline == null) return;

        foreach (PlayableBinding output in timeline.outputs)
        {
            if (output.sourceObject is not AnimationTrack track ||
                (output.streamName != playerTrack && output.streamName != enemyTrack))
            {
                continue;
            }

            int clipCount = 0;
            foreach (TimelineClip ignored in track.GetClips()) clipCount++;
            TracePlacement("Autorite piste acteur | piste='" + output.streamName + "' | offset=" + track.trackOffset +
                           " | clips=" + clipCount + " | infiniteClip=" + (track.infiniteClip != null) + ".");
        }
    }

    private void CaptureActorAnimatorRestPoses()
    {
        if (context == null || context.PlayerAnimator == null)
        {
            return;
        }

        Transform animatorTransform = context.PlayerAnimator.transform;
        playerAnimatorRestLocalPosition = animatorTransform.localPosition;
        playerAnimatorRestLocalRotation = animatorTransform.localRotation;

        hasEnemyAnimatorRestPose = false;
        if (context.TargetEnemy == null || context.TargetAnimator == null ||
            context.TargetAnimator.transform == context.TargetEnemy.transform)
        {
            return;
        }

        Transform enemyAnimatorTransform = context.TargetAnimator.transform;
        enemyAnimatorRestLocalPosition = enemyAnimatorTransform.localPosition;
        enemyAnimatorRestLocalRotation = enemyAnimatorTransform.localRotation;
        hasEnemyAnimatorRestPose = true;
    }

    private void RestorePlayerAnimatorRestLocalPose()
    {
        if (context == null || context.PlayerAnimator == null ||
            context.PlayerAnimator.transform == context.PlayerRoot)
        {
            return;
        }

        Transform animatorTransform = context.PlayerAnimator.transform;
        animatorTransform.localPosition = playerAnimatorRestLocalPosition;
        animatorTransform.localRotation = playerAnimatorRestLocalRotation;
    }

    private void RestoreEnemyAnimatorRestLocalPose()
    {
        if (!hasEnemyAnimatorRestPose || context == null || context.TargetEnemy == null || context.TargetAnimator == null)
        {
            return;
        }

        Transform animatorTransform = context.TargetAnimator.transform;
        if (animatorTransform == context.TargetEnemy.transform)
        {
            return;
        }

        animatorTransform.localPosition = enemyAnimatorRestLocalPosition;
        animatorTransform.localRotation = enemyAnimatorRestLocalRotation;
    }

    private void TraceRootMotion(string message)
    {
        if (logPlacementDiagnostics && Time.unscaledTime >= nextRootMotionLogTime)
        {
            nextRootMotionLogTime = Time.unscaledTime + 0.5f;
            TracePlacement(message);
        }
    }

    private void OnDirectorStopped(PlayableDirector stoppedDirector)
    {
        if (stoppedDirector != director || !sessionActive) return;

        CombatCinematicEndReason reason = requestedEndReason ?? CombatCinematicEndReason.Completed;
        FinalizeSession(reason, true);
    }

    private void OnDirectorPlayed(PlayableDirector playedDirector)
    {
        if (playedDirector != director || !sessionActive) return;
        TraceCamera("PlayableDirector.played", true, true);
    }

    private CinemachineCamera ResolveOpeningCamera()
    {
        if (framingReferences.Count > 0)
        {
            return ResolveCamera(framingReferences[0].cameraKey);
        }

        return cameraBindings.Count > 0 && cameraBindings[0] != null
            ? cameraBindings[0].camera
            : null;
    }

    private void RequestEnd(CombatCinematicEndReason reason, bool notifyStopped = true)
    {
        if (!sessionActive || stopRaised) return;

        requestedEndReason = reason;
        if (director != null && director.state == PlayState.Playing)
        {
            director.Stop();
            return;
        }

        FinalizeSession(reason, notifyStopped);
    }

    private void FinalizeSession(CombatCinematicEndReason reason, bool notifyStopped)
    {
        if (!sessionActive || stopRaised) return;

        stopRaised = true;
        LastEndReason = reason;
        EndCameraSession("Fin Timeline: " + reason, reason);
        if (notifyStopped)
        {
            Stopped?.Invoke(this);
        }
    }

    private void AbortStart(string reason)
    {
        FinalizeSession(CombatCinematicEndReason.Failed, false);
        context = null;
        gameplayBrain = null;
        gameplayLockCamera = null;
    }

    private void EndCameraSession(string reason, CombatCinematicEndReason endReason = CombatCinematicEndReason.Interrupted)
    {
        if (!sessionActive && context == null) return;

        // Animation Tracks can leave bound child transforms on an authored pose.
        // Reset their local offsets before pooling so a later cinematic cannot
        // mistake a previous Timeline sample for hierarchy data.
        RestorePlayerAnimatorRestLocalPose();
        RestoreEnemyAnimatorRestLocalPose();
        if (endReason == CombatCinematicEndReason.Completed)
        {
            CompleteParticipants();
        }
        EndContractCinematicMotion();
        Physics.SyncTransforms();

        LitTimelineCinemachineBridge bridge = GetComponent<LitTimelineCinemachineBridge>();
        bridge?.EndCameraControlNow();
        EndParticipants();
        skipFirstPlayerRootMotionDelta = false;
        skipFirstEnemyRootMotionDelta = false;
        hasEnemyAnimatorRestPose = false;
        ResetTimelineActorTransformSampling();
        TraceCamera("Restitution camera: " + reason, false);
        sessionActive = false;
        requestedEndReason = null;
    }

    private void CompleteParticipants()
    {
        for (int i = 0; i < participants.Count; i++)
        {
            if (participants[i] is ICombatCinematicCompletionParticipant completionParticipant)
            {
                completionParticipant.Complete(context);
            }
        }
    }

    private void NormalizeRuntimeDirector()
    {
        if (director == null) return;
        director.playOnAwake = false;
        director.timeUpdateMode = context != null && context.TimeMode == CombatCinematicTimeMode.GlobalScaled
            ? DirectorUpdateMode.GameTime
            : DirectorUpdateMode.UnscaledGameTime;
        director.extrapolationMode = DirectorWrapMode.None;
    }

    private void ApplyLocalPlaybackScale()
    {
        if (director == null) return;

        localPlaybackManualClock = localPlaybackScale < 0.999f;
        if (localPlaybackManualClock)
        {
            director.timeUpdateMode = DirectorUpdateMode.Manual;
            return;
        }

        NormalizeRuntimeDirector();
    }

    private void Update()
    {
        if (sessionActive && director != null && director.state == PlayState.Playing)
        {
            if (localPlaybackManualClock)
            {
                AdvanceLocalPlaybackClock();
            }

            TraceCamera("Verification runtime", true);
            CaptureReachedFramingReferences();
        }
    }

    private void AdvanceLocalPlaybackClock()
    {
        if (director.playableAsset == null) return;

        double nextTime = director.time + Time.unscaledDeltaTime * localPlaybackScale;
        double duration = director.playableAsset.duration;
        bool reachedEnd = duration > 0d && nextTime >= duration;
        director.time = reachedEnd ? duration : nextTime;
        director.Evaluate();

        LitTimelineCinemachineBridge bridge = GetComponent<LitTimelineCinemachineBridge>();
        bridge?.UpdateTimelineCameraNow();
        if (reachedEnd)
        {
            director.Stop();
        }
    }

    private void CaptureReachedFramingReferences()
    {
        if (!logPlacementDiagnostics || director == null || framingReferences.Count == 0) return;

        const double tolerance = 0.02d;
        while (nextFramingReferenceIndex < framingReferences.Count &&
               director.time + tolerance >= framingReferences[nextFramingReferenceIndex].timelineTime)
        {
            LogFramingReference(framingReferences[nextFramingReferenceIndex]);
            nextFramingReferenceIndex++;
        }
    }

    private void LogFramingReference(CombatCinematicFramingReference reference)
    {
        if (context == null || context.PlayerAnimator == null || context.TargetAnimator == null) return;

        CinemachineCamera expectedCamera = ResolveCamera(reference.cameraKey);
        CinemachineCamera activeCamera = gameplayBrain != null ? gameplayBrain.ActiveVirtualCamera as CinemachineCamera : null;
        Transform playerAnimator = context.PlayerAnimator.transform;
        Transform enemyAnimator = context.TargetAnimator.transform;
        Vector3 playerStagePosition = transform.InverseTransformPoint(playerAnimator.position);
        Vector3 enemyStagePosition = transform.InverseTransformPoint(enemyAnimator.position);
        Vector3 cameraStagePosition = expectedCamera != null
            ? transform.InverseTransformPoint(expectedCamera.transform.position)
            : Vector3.zero;
        Quaternion cameraStageRotation = expectedCamera != null
            ? Quaternion.Inverse(transform.rotation) * expectedCamera.transform.rotation
            : Quaternion.identity;
        float cameraFov = expectedCamera != null ? expectedCamera.Lens.FieldOfView : 0f;
        float playerPositionError = Vector3.Distance(playerStagePosition, reference.playerAnimatorStagePosition);
        float enemyPositionError = Vector3.Distance(enemyStagePosition, reference.enemyAnimatorStagePosition);
        float cameraPositionError = Vector3.Distance(cameraStagePosition, reference.cameraStagePosition);
        float cameraRotationError = Quaternion.Angle(cameraStageRotation, reference.cameraStageRotation);
        float cameraFovError = Mathf.Abs(cameraFov - reference.cameraFieldOfView);
        LitOpsiveLocomotionBridge bridge = context.PlayerRoot != null
            ? context.PlayerRoot.GetComponent<LitOpsiveLocomotionBridge>()
            : null;

        Debug.Log("[LightSkill Framing Runtime] session=" + sessionToken +
                  " t=" + director.time.ToString("0.000") +
                  " ref=" + reference.timelineTime.ToString("0.000") +
                  " timeline='" + (director.playableAsset != null ? director.playableAsset.name : "None") + "'" +
                  " cameraExpected='" + (expectedCamera != null ? expectedCamera.name : "None") + "'" +
                  " cameraActive='" + (activeCamera != null ? activeCamera.name : "None") + "'" +
                  " playerPosError=" + playerPositionError.ToString("0.0000") +
                  " enemyPosError=" + enemyPositionError.ToString("0.0000") +
                  " cameraPosError=" + cameraPositionError.ToString("0.0000") +
                  " cameraRotError=" + cameraRotationError.ToString("0.000") +
                  " cameraFovError=" + cameraFovError.ToString("0.000") +
                  " uccExternalLock=" + (bridge != null && bridge.IsExternalLockActive) +
                  " uccTraversal=" + (bridge != null && bridge.IsScriptedTraversalActive) + ".", this);
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

    private void TracePlacement(string message)
    {
        if (logPlacementDiagnostics)
        {
            Debug.Log("[LightSkill Debug] Rig '" + name + "' | " + message, this);
        }
    }
}
