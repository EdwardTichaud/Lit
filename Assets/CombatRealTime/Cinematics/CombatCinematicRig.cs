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
    [Header("Authoring Camera Contract")]
    [SerializeField, HideInInspector] private int authoringCameraContractVersion;
    [SerializeField, HideInInspector] private bool authoringCameraUsesPhysicalProperties;
    [SerializeField, HideInInspector] private Vector2 authoringCameraSensorSize = new Vector2(36f, 24f);
    [SerializeField, HideInInspector] private Camera.GateFitMode authoringCameraGateFitMode;
    [SerializeField, HideInInspector] private CinemachineBrain.LensModeOverrideSettings authoringLensModeOverride;
    [SerializeField] private bool logCameraDiagnostics = true;
    [SerializeField, Tooltip("Journalise les poses bakees et les poses appliquees aux acteurs pendant une LightSkill.")]
    private bool logPlacementDiagnostics = true;
    [Header("Framing Diagnostics")]
    [SerializeField, Tooltip("Journalise une pose runtime comparable a AnimationLab a un temps fixe de Timeline.")]
    private bool logFramingSnapshot = true;
    [SerializeField, Min(0f), Tooltip("Temps de Timeline utilise pour comparer AnimationLab et runtime.")]
    private float framingSnapshotTime = 1f;

    private readonly List<ICombatCinematicParticipant> participants = new List<ICombatCinematicParticipant>();
    private CombatCinematicContext context;
    private PlayableAsset bakedTimeline;
    private CinemachineBrain gameplayBrain;
    private CombatLockOnCameraController gameplayLockCamera;
    private bool sessionActive;
    private bool stopRaised;
    private bool cameraMismatchReported;
    private int sessionToken;
    private bool gameplayCameraProjectionStored;
    private bool gameplayCameraUsedPhysicalProperties;
    private Vector2 gameplayCameraSensorSize;
    private Camera.GateFitMode gameplayCameraGateFitMode;
    private CinemachineBrain.LensModeOverrideSettings gameplayBrainLensModeOverride;
    private CinemachineBrain.UpdateMethods gameplayBrainUpdateMethod;
    private CinemachineBrain.BrainUpdateMethods gameplayBrainBlendUpdateMethod;
    private bool framingSnapshotLogged;

    public PlayableDirector Director => director;
    public SignalReceiver SignalReceiver => signalReceiver;
    public IReadOnlyList<CombatCinematicCameraBinding> CameraBindings => cameraBindings;
    public IReadOnlyList<CombatCinematicTrackBinding> TrackBindings => trackBindings;
    public bool HasAuthoringStageLayout => authoringStageLayoutVersion >= 3 &&
                                           playerStageAnchor != null && enemyStageAnchor != null;
    public bool HasAuthoringCameraContract => authoringCameraContractVersion >= 1;
    public event Action<CombatCinematicRig> Stopped;

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

    /// <summary>
    /// Captures only the output-camera projection contract from AnimationLab.
    /// The baked package still has no dependency on the preview camera itself.
    /// </summary>
    public void ConfigureAuthoringCameraContract(CinemachineBrain authoringBrain)
    {
        Camera authoringCamera = authoringBrain != null ? authoringBrain.GetComponent<Camera>() : null;
        if (authoringBrain == null || authoringCamera == null)
        {
            authoringCameraContractVersion = 0;
            return;
        }

        authoringCameraUsesPhysicalProperties = authoringCamera.usePhysicalProperties;
        authoringCameraSensorSize = authoringCamera.sensorSize;
        authoringCameraGateFitMode = authoringCamera.gateFit;
        authoringLensModeOverride = authoringBrain.LensModeOverride;
        authoringCameraContractVersion = 1;
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

        UpdateCinematicBrain();
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

        if (!TryGetActorContract(playbackContext.PlayerRoot, "Lucian", out _) ||
            !TryGetActorContract(playbackContext.TargetEnemy.transform, "L'ennemi verrouille", out _))
        {
            error = "Le contrat ActorRoot > AnimationRoot doit etre valide avant une LightSkill. " +
                    "Executez Lit/Combat/Normalize Actor Animation Hierarchies puis rebakez le package.";
            return false;
        }

        context = playbackContext;
        sessionToken++;
        sessionActive = true;
        stopRaised = false;
        cameraMismatchReported = false;
        framingSnapshotLogged = false;
        TracePlacement("TryPlay | token=" + sessionToken + " | timeline='" + timeline.name + "' | type=" + timeline.GetType().Name +
                       " | placement=" + placement.HasValue + ".");
        if (placement.HasValue)
        {
            if (!ApplyPlacement(placement.Value, out error))
            {
                AbortStart("Placement cinematographique invalide");
                return false;
            }

            BeginContractCinematicMotion();
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
        TracePlacement("Participants acceptes.");

        director.playableAsset = timeline;
        bakedTimeline ??= timeline;
        if (!TryBindTimeline(timeline, playerAnimatorTrack, enemyAnimatorTrack, out error))
        {
            AbortStart("Binding Timeline invalide");
            return false;
        }
        if (placement.HasValue && !UsesRelativeActorTracks(timeline, playerAnimatorTrack, enemyAnimatorTrack))
        {
            error = "Le package LightSkill utilise encore des pistes acteur en Scene Offsets. Rebakez le LightSkill depuis AnimationLab.";
            AbortStart("Package runtime obsolete");
            return false;
        }
        TracePlacement("Bindings Timeline acceptes | playerTrack='" + playerAnimatorTrack + "' | enemyTrack='" + enemyAnimatorTrack + "'.");

        LitTimelineCinemachineBridge cameraBridge = GetComponent<LitTimelineCinemachineBridge>();
        if (cameraBridge == null || !cameraBridge.BeginCameraControlNow(gameplayBrain))
        {
            error = "Impossible de donner le controle de la camera gameplay explicite a la Timeline.";
            AbortStart("Camera gameplay invalide");
            return false;
        }

        BeginCameraProjectionContract();
        TraceCamera("Bind Timeline -> Brain -> controle Timeline", false);
        director.time = 0d;
        director.Evaluate();
        if (placement.HasValue)
        {
            // The initial sample must never become a second movement source.
            // ActorRoots are restored once on their stage anchors, then only the
            // root-motion relay transports Animator deltas for the whole session.
            if (!ApplyPlacement(placement.Value, out error))
            {
                AbortStart("Reinitialisation des acteurs apres Evaluate invalide");
                return false;
            }
            ArmContractCinematicMotion();
        }
        Physics.SyncTransforms();
        UpdateCinematicBrain();
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
        EndCameraSession("Remise en pool");
        ClearTimelineBindings();
        if (director != null && director.playableAsset != null)
        {
            director.Stop();
            director.time = 0d;
            director.playableAsset = bakedTimeline;
        }
        context = null;
        gameplayBrain = null;
        gameplayLockCamera = null;
        framingSnapshotLogged = false;
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

    private void BeginCameraProjectionContract()
    {
        if (gameplayBrain == null || authoringCameraContractVersion < 1 || gameplayCameraProjectionStored)
        {
            return;
        }

        Camera gameplayCamera = gameplayBrain.GetComponent<Camera>();
        if (gameplayCamera == null)
        {
            return;
        }

        gameplayCameraUsedPhysicalProperties = gameplayCamera.usePhysicalProperties;
        gameplayCameraSensorSize = gameplayCamera.sensorSize;
        gameplayCameraGateFitMode = gameplayCamera.gateFit;
        gameplayBrainLensModeOverride = gameplayBrain.LensModeOverride;
        gameplayBrainUpdateMethod = gameplayBrain.UpdateMethod;
        gameplayBrainBlendUpdateMethod = gameplayBrain.BlendUpdateMethod;
        gameplayCameraProjectionStored = true;

        gameplayCamera.usePhysicalProperties = authoringCameraUsesPhysicalProperties;
        gameplayCamera.sensorSize = authoringCameraSensorSize;
        gameplayCamera.gateFit = authoringCameraGateFitMode;
        gameplayBrain.LensModeOverride = authoringLensModeOverride;

        // A Timeline evaluates before LateUpdate. Manual mode gives this rig one
        // deterministic camera pass after the staged actors and animated camera
        // have reached their final pose for the frame.
        gameplayBrain.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate;
        gameplayBrain.BlendUpdateMethod = CinemachineBrain.BrainUpdateMethods.LateUpdate;
    }

    private void UpdateCinematicBrain()
    {
        if (!gameplayCameraProjectionStored || gameplayBrain == null || !gameplayBrain.isActiveAndEnabled)
        {
            return;
        }

        gameplayBrain.ManualUpdate();
    }

    private void RestoreCameraProjectionContract()
    {
        if (!gameplayCameraProjectionStored)
        {
            return;
        }

        if (gameplayBrain != null)
        {
            Camera gameplayCamera = gameplayBrain.GetComponent<Camera>();
            if (gameplayCamera != null)
            {
                gameplayCamera.usePhysicalProperties = gameplayCameraUsedPhysicalProperties;
                gameplayCamera.sensorSize = gameplayCameraSensorSize;
                gameplayCamera.gateFit = gameplayCameraGateFitMode;
            }

            gameplayBrain.LensModeOverride = gameplayBrainLensModeOverride;
            gameplayBrain.UpdateMethod = gameplayBrainUpdateMethod;
            gameplayBrain.BlendUpdateMethod = gameplayBrainBlendUpdateMethod;
        }

        gameplayCameraProjectionStored = false;
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

        if (!TryGetActorContract(context.PlayerRoot, "Lucian", out CombatActorAnimationRoot playerContract))
        {
            error = "Le contrat d'animation de Lucian ne peut pas poser son ActorRoot.";
            return false;
        }
        if (!playerContract.SetActorPose(playerRootPosition, playerRootRotation))
        {
            error = "Le contrat d'animation de Lucian ne peut pas poser son ActorRoot.";
            return false;
        }
        playerContract.ResetAnimationRootPose();

        if (!TryGetActorContract(context.TargetEnemy.transform, "L'ennemi verrouille", out CombatActorAnimationRoot enemyContract))
        {
            error = "Le contrat d'animation ennemi ne peut pas poser son ActorRoot.";
            return false;
        }
        if (!enemyContract.SetActorPose(enemyRootPosition, enemyRootRotation))
        {
            error = "Le contrat d'animation ennemi ne peut pas poser son ActorRoot.";
            return false;
        }
        enemyContract.ResetAnimationRootPose();

        Physics.SyncTransforms();
        TracePlacement("Application terminee | rig=" + transform.position + " | player=" + context.PlayerRoot.position +
                       " playerAnimator=" + context.PlayerAnimator.transform.position +
                       " | enemy=" + context.TargetEnemy.transform.position +
                       " enemyAnimator=" + context.TargetAnimator.transform.position + ".");
        return true;
    }

    private void BeginContractCinematicMotion()
    {
        context?.PlayerRoot?.GetComponent<CombatActorAnimationRoot>()?.BeginCinematicMotion(sessionToken);
        context?.TargetEnemy?.GetComponent<CombatActorAnimationRoot>()?.BeginCinematicMotion(sessionToken);
    }

    private static bool TryGetActorContract(Transform actorRoot, string label, out CombatActorAnimationRoot contract)
    {
        contract = actorRoot != null ? actorRoot.GetComponent<CombatActorAnimationRoot>() : null;
        return contract != null && contract.ValidateContract(out _);
    }

    private void EndContractCinematicMotion()
    {
        context?.PlayerRoot?.GetComponent<CombatActorAnimationRoot>()?.EndCinematicMotion(sessionToken);
        context?.TargetEnemy?.GetComponent<CombatActorAnimationRoot>()?.EndCinematicMotion(sessionToken);
    }

    private void ArmContractCinematicMotion()
    {
        context?.PlayerRoot?.GetComponent<CombatActorAnimationRoot>()?.ArmCinematicMotion(sessionToken);
        context?.TargetEnemy?.GetComponent<CombatActorAnimationRoot>()?.ArmCinematicMotion(sessionToken);
    }

    private static bool UsesRelativeActorTracks(PlayableAsset timeline, string playerTrack, string enemyTrack)
    {
        if (timeline == null) return false;

        bool playerRelative = false;
        bool enemyRelative = false;
        foreach (PlayableBinding output in timeline.outputs)
        {
            if (output.sourceObject is not AnimationTrack track) continue;
            if (output.streamName == playerTrack)
                playerRelative = track.trackOffset == TrackOffset.ApplyTransformOffsets;
            else if (output.streamName == enemyTrack)
                enemyRelative = track.trackOffset == TrackOffset.ApplyTransformOffsets;
        }

        return playerRelative && enemyRelative;
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

        EndContractCinematicMotion();
        Physics.SyncTransforms();

        LitTimelineCinemachineBridge bridge = GetComponent<LitTimelineCinemachineBridge>();
        bridge?.EndCameraControlNow();
        RestoreCameraProjectionContract();
        EndParticipants();
        TraceCamera("Restitution camera: " + reason, false);
        sessionActive = false;
    }

    private void Update()
    {
        if (!sessionActive || director == null || director.state != PlayState.Playing)
        {
            return;
        }

        TraceCamera("Verification runtime", true);
        if (logFramingSnapshot && !framingSnapshotLogged && director.time >= framingSnapshotTime)
        {
            framingSnapshotLogged = true;
            TraceFramingSnapshot();
        }
    }

    private void TraceFramingSnapshot()
    {
        if (context == null || context.PlayerRoot == null || context.TargetEnemy == null)
        {
            return;
        }

        CinemachineCamera camera = gameplayBrain != null
            ? gameplayBrain.ActiveVirtualCamera as CinemachineCamera
            : null;
        if (camera == null && cameraBindings.Count > 0)
        {
            camera = cameraBindings[0]?.camera;
        }

        string cameraDescription = camera != null
            ? "camera='" + camera.name + "' relPos=" + transform.InverseTransformPoint(camera.transform.position).ToString("F4") +
              " relRot=" + (Quaternion.Inverse(transform.rotation) * camera.transform.rotation).eulerAngles.ToString("F4") +
              " fov=" + camera.Lens.FieldOfView.ToString("F3")
            : "camera=None";

        Debug.Log("[LightSkill Framing Runtime] t=" + director.time.ToString("F3") +
                  " | stagePos=" + transform.position.ToString("F4") +
                  " stageRot=" + transform.rotation.eulerAngles.ToString("F4") +
                  " | playerRelPos=" + transform.InverseTransformPoint(context.PlayerRoot.position).ToString("F4") +
                  " playerRelRot=" + (Quaternion.Inverse(transform.rotation) * context.PlayerRoot.rotation).eulerAngles.ToString("F4") +
                  " | enemyRelPos=" + transform.InverseTransformPoint(context.TargetEnemy.transform.position).ToString("F4") +
                  " enemyRelRot=" + (Quaternion.Inverse(transform.rotation) * context.TargetEnemy.transform.rotation).eulerAngles.ToString("F4") +
                  " | " + cameraDescription,
                  this);
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
