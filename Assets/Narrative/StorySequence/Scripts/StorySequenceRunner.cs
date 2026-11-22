using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using Lit.Timeline;

namespace Lit.Story
{
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public sealed partial class StorySequenceRunner : MonoBehaviour
    {
        private static int activeSequenceCount;

        [Header("Sequence")]
        [SerializeField] private StorySequenceAsset sequence;
        [SerializeField] private bool playOnStart = true;
        [SerializeField, Min(0f)] private float startDelay;
        [SerializeField, Tooltip("Place le personnage local en pose assise avant le fondu d'ouverture et le fait se lever au retour au jeu.")]
        private bool startWithLocalPlayerSeated;

        [Header("References")]
        [SerializeField] private StorySequenceSceneBindings bindings;
        [SerializeField] private StorySequenceCameraDriver cameraDriver;
        [SerializeField] private StorySequenceDialoguePresenter dialoguePresenter;
        [SerializeField] private StorySequenceFadeController fadeController;

        [Header("Events")]
        [SerializeField] private UnityEvent onSequenceStarted;
        [SerializeField] private UnityEvent onSequenceCompleted;
        [SerializeField] private UnityEvent onSequenceAborted;

        [SerializeField] private StorySequenceNamePrompt namePrompt;
        private bool stepFailed;
        private Coroutine playbackRoutine;
        private Coroutine deferredPlaybackRoutine;
        private SquadCharacterController lockedPlayerController;
        private SquadCharacterController seatedOpeningPlayer;
        private bool playerLockHeld;
        private LitOpsiveLocomotionBridge.ExternalLockHandle playerLock;
        private bool advanceRequested;
        private bool currentStepSkippable;
        private int currentStepStartedFrame;
        private bool inputSubscribed;
        private bool registeredAsActive;
        private bool launchCombatOnSequenceEnd;
        private float launchCombatDamageOnSequenceEnd;

        public bool IsPlaying => playbackRoutine != null;
        public static bool IsAnySequencePlaying => activeSequenceCount > 0;
        public StorySequenceAsset Sequence => sequence;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActiveSequenceCount()
        {
            activeSequenceCount = 0;
        }

        private void Awake()
        {
            ResolveReferences();
            if (playOnStart &&
                sequence != null &&
                sequence.TryValidate(out _) &&
                sequence.startFromBlack && playerTransform == null &&
                (!sequence.playOnce || !StorySequenceCompletionStore.IsCompleted(sequence)))
            {
                fadeController.SetImmediate(1f);
            }
        }

        private IEnumerator Start()
        {
            if (sequence != null && sequence.playOnce && StorySequenceCompletionStore.IsCompleted(sequence))
            {
                yield return LoadingScreenService.WaitUntilHidden();
                RestoreCompletedSpawns();
                fadeController.SetImmediate(0f);
                yield break;
            }

            if (!playOnStart || sequence == null) yield break;

            if (deferredPlaybackRoutine != null) yield break;

            if (startDelay > 0f)
            {
                yield return new WaitForSecondsRealtime(startDelay);
            }

            Play();
        }

        // Called explicitly once the destination actors and navigation have been prepared.
        // Owned by this runner so unloading the session cancels the deferred playback too.
        public void PlayAfterLoading()
        {
            if (!isActiveAndEnabled || IsPlaying || deferredPlaybackRoutine != null) return;
            string issue = null;
            if (sequence == null || !sequence.TryValidate(out issue))
            {
                Debug.LogWarning($"StorySequenceRunner: '{sequence?.name}' non prete. " +
                    (sequence == null ? "Sequence manquante." : issue), this);
                return;
            }
            ResolveReferences();
            if (sequence.startFromBlack && playerTransform == null) fadeController?.SetImmediate(1f);
            deferredPlaybackRoutine = StartCoroutine(PlayAfterLoadingRoutine());
        }

        private IEnumerator PlayAfterLoadingRoutine()
        {
            // Avoid a synchronous completion overwriting the routine handle.
            yield return null;
            yield return LoadingScreenService.WaitUntilHidden();
            deferredPlaybackRoutine = null;
            if (!Play()) fadeController?.SetImmediate(0f);
        }

        public bool Play()
        {
            return Play(sequence);
        }

        public bool Play(StorySequenceAsset targetSequence)
        {
            if (!isActiveAndEnabled || targetSequence == null || IsPlaying)
            {
                return false;
            }

            ResolveReferences();
            if (!targetSequence.TryValidate(out string issue) || bindings == null ||
                fadeController == null || cameraDriver == null || dialoguePresenter == null ||
                (targetSequence.RequiresNamePrompt && (namePrompt == null || !namePrompt.HasKeyboard)))
            {
                Debug.LogWarning($"StorySequenceRunner: '{targetSequence.name}' refusee : " +
                    (issue ?? "rig incomplet"), this);
                fadeController?.SetImmediate(0f);
                return false;
            }
            sequence = targetSequence;
            if (sequence.playOnce && StorySequenceCompletionStore.IsCompleted(sequence))
            {
                fadeController.SetImmediate(0f);
                return false;
            }

            sequenceContentStarted=false;
            hasPlayerPlacement=playerTransform!=null;
            if(hasPlayerPlacement)playerPlacementPose=new Pose(playerTransform.position,playerTransform.rotation);
            int run=++playbackGeneration;
            RegisterActiveSequence();
            playbackRoutine = StartCoroutine(PlayRoutine(sequence,run));
            return true;
        }

        private void CancelDeferredPlayback()
        {
            if (deferredPlaybackRoutine == null) return;
            StopCoroutine(deferredPlaybackRoutine);
            deferredPlaybackRoutine = null;
        }

        public void Abort()
        {
            ReleaseSequenceSpawns(false);
            playbackGeneration++;
            CancelDeferredPlayback();
            if (playbackRoutine != null)
            {
                StopCoroutine(playbackRoutine);
                playbackRoutine = null;
            }

            dialoguePresenter?.HideImmediate();
            RestoreGameplay();
            launchCombatOnSequenceEnd = false;
            launchCombatDamageOnSequenceEnd = 0f;
            UnregisterActiveSequence();
            fadeController?.SetImmediate(0f);
            if(!sequenceContentStarted)PreparationFailed?.Invoke();
            onSequenceAborted?.Invoke();
        }

        private IEnumerator PlayRoutine(StorySequenceAsset activeSequence,int run)
        {
            stepFailed = false;
            advanceRequested = false;
            currentStepSkippable = false;
            launchCombatOnSequenceEnd = false;
            launchCombatDamageOnSequenceEnd = 0f;
            PushInputFocus();
            // Reserve the runner immediately, but start authored content only after approach.
            yield return null;
            if(!hasPlayerPlacement)
            {
                sequenceContentStarted=true;
                onSequenceStarted?.Invoke();PlaybackStarted?.Invoke();
                if(run!=playbackGeneration || !isActiveAndEnabled)yield break;
            }
            if (activeSequence.startFromBlack && !hasPlayerPlacement)
            {
                fadeController.SetImmediate(1f);
            }

            if (activeSequence.waitForLocalPlayer || hasPlayerPlacement)
            {
                yield return WaitForLocalPlayer(activeSequence.localPlayerWaitTimeout);
                if (LocalPlayerContext.LocalCharacterRoot == null)
                {
                    Debug.LogWarning(
                        $"StorySequenceRunner: personnage local introuvable pour '{activeSequence.name}'.",
                        this);
                    FinishPlayback(completed: false);
                    yield break;
                }
            }

            if (hasPlayerPlacement)
            {
                yield return PreparePlayerPlacement();
                if (stepFailed) { FinishPlayback(false); yield break; }
                if (activeSequence.startFromBlack)
                    yield return fadeController.FadeTo(1f,activeSequence.openingFadeDuration,activeSequence.useUnscaledTime);
            }
            if(hasPlayerPlacement)
            {
                sequenceContentStarted=true;
                onSequenceStarted?.Invoke();PlaybackStarted?.Invoke();
                if(run!=playbackGeneration || !isActiveAndEnabled)yield break;
            }

            bool deferPlayerLock = HasProgressivePlayerStop(activeSequence);
            if (!deferPlayerLock)
            {
                AcquirePlayerLock(activeSequence);
            }
            // Dialogue without an authored view keeps the gameplay camera.

            if (startWithLocalPlayerSeated)
            {
                // The avatar can still settle onto the floor with gameplay input locked.
                double physicsDeadline = Time.fixedTimeAsDouble + 2;
                double realtimeDeadline = Time.realtimeSinceStartupAsDouble + 10;
                string reason = null;
                do
                {
                    var root = LocalPlayerContext.LocalCharacterRoot;
                    var player = root != null ? root.GetComponent<SquadCharacterController>() : null;
                    reason = player != null ? player.GetScriptedSittingBlockReason() : "personnage absent";
                    if (player != null && (reason == null || player.IsSittingRequested))
                    {
                        if (player.TrySetSittingImmediate(holdUntilStandUp: true))
                        {
                            seatedOpeningPlayer = player;
                            break;
                        }
                        reason = player.GetScriptedSittingBlockReason();
                    }

                    yield return null;
                }
                while (Time.fixedTimeAsDouble < physicsDeadline && Time.realtimeSinceStartupAsDouble < realtimeDeadline);
                if (seatedOpeningPlayer == null)
                    Debug.LogWarning($"StorySequenceRunner: pose assise indisponible pour '{activeSequence.name}' : {reason}.", this);
            }

            int openingShotIndex = -1;
            if (activeSequence.startFromBlack)
            {
                openingShotIndex = PrepareOpeningShot(activeSequence);
                yield return fadeController.FadeTo(
                    0f,
                    activeSequence.openingFadeDuration,
                    activeSequence.useUnscaledTime);
            }

            if (activeSequence.steps != null)
            {
                for (int i = 0; i < activeSequence.steps.Count; i++)
                {
                    StorySequenceStep step = activeSequence.steps[i];
                    if (step == null)
                    {
                        continue;
                    }

                    currentStepSkippable = step.skippable;
                    currentStepStartedFrame = Time.frameCount;
                    advanceRequested = false;
                    yield return ExecuteStep(step, activeSequence.useUnscaledTime, cameraPrepared: i == openingShotIndex);
                    if (stepFailed) { FinishPlayback(completed: false); yield break; }
                    if (deferPlayerLock &&
                        step.type == StorySequenceStepType.ProgressivePlayerStop &&
                        !playerLockHeld)
                    {
                        AcquirePlayerLock(activeSequence);
                    }
                }
            }

            currentStepSkippable = false;
            if (activeSequence.fadeToBlackBeforeGameplay)
            {
                yield return fadeController.FadeTo(
                    1f,
                    activeSequence.closingFadeDuration,
                    activeSequence.useUnscaledTime);
            }

            RestoreCamera();

            if (activeSequence.fadeFromBlackAfterGameplayRestore)
            {
                yield return fadeController.FadeTo(
                    0f,
                    activeSequence.gameplayFadeInDuration,
                    activeSequence.useUnscaledTime);
            }
            else if (!activeSequence.fadeToBlackBeforeGameplay)
            {
                fadeController.SetImmediate(0f);
            }

            ReleasePlayerControl();

            if (activeSequence.playOnce)
            {
                StorySequenceCompletionStore.MarkCompleted(activeSequence);
            }

            FinishPlayback(completed: true);
        }

        // Only pre-stage the first nonempty step. Do not jump over authored
        // waits, Timelines or events to reach a later camera instruction.
        private int PrepareOpeningShot(StorySequenceAsset activeSequence)
        {
            if (cameraDriver == null || activeSequence.steps == null) return -1;
            for (int i = 0; i < activeSequence.steps.Count; i++)
            {
                var step = activeSequence.steps[i];
                if (step == null) continue;
                if (step.type == StorySequenceStepType.CameraShot)
                {
                    var point = bindings.ResolveCameraPoint(step.cameraPointId);
                    if (point != null)
                    {
                        cameraDriver.SetCameraPoint(point, 0f);
                        return cameraDriver.IsCinematicActive ? i : -1;
                    }
                }
                else if (step.type != StorySequenceStepType.Dialogue || !step.focusCameraOnSpeaker) return -1;

                var actor = bindings.ResolveActor(step.actorId);
                if (actor == null) return -1;
                var profile = step.type == StorySequenceStepType.Dialogue && step.dialogueCameraProfile != null
                    ? step.dialogueCameraProfile : step.cameraProfile;
                cameraDriver.SetActorShot(actor, bindings.ResolveActor(step.listenerId), profile, 0f);
                return cameraDriver.IsCinematicActive ? i : -1;
            }
            return -1;
        }

        private IEnumerator ExecuteStep(StorySequenceStep step, bool useUnscaledTime, bool cameraPrepared = false)
        {
            PlayStepOneShots(step);

            switch (step.type)
            {
                case StorySequenceStepType.Fade:
                    yield return fadeController.FadeTo(
                        step.fadeAlpha,
                        step.fadeDuration,
                        useUnscaledTime,
                        ConsumeAdvanceRequest);
                    break;

                case StorySequenceStepType.Dialogue:
                    yield return ExecuteDialogue(step, useUnscaledTime, cameraPrepared);
                    break;

                case StorySequenceStepType.CameraShot:
                    yield return ExecuteCameraShot(step, useUnscaledTime, cameraPrepared);
                    break;

                case StorySequenceStepType.Wait:
                    yield return WaitSkippable(step.duration, useUnscaledTime);
                    break;

                case StorySequenceStepType.AnimatorTrigger:
                    yield return ExecuteAnimatorTrigger(step, useUnscaledTime);
                    break;

                case StorySequenceStepType.Sitting:
                    yield return ExecuteSitting(step, useUnscaledTime);
                    break;

                case StorySequenceStepType.Timeline:
                    yield return ExecuteTimeline(step);
                    break;

                case StorySequenceStepType.SceneEvent:
                    if (!bindings.InvokeEvent(step.eventId))
                    {
                        Debug.LogWarning(
                            $"StorySequenceRunner: event '{step.eventId}' introuvable.",
                            this);
                    }
                    break;

                case StorySequenceStepType.InstantiateGameObject:
                    stepFailed = !TryInstantiateStep(step);
                    break;

                case StorySequenceStepType.ProgressivePlayerStop:
                    yield return ExecuteProgressivePlayerStop(step, useUnscaledTime);
                    break;

                case StorySequenceStepType.ChooseMuninName:
                    yield return namePrompt.ChooseName();
                    stepFailed = !namePrompt.Confirmed;
                    break;

                case StorySequenceStepType.LaunchCombat:
                    launchCombatOnSequenceEnd = true;
                    launchCombatDamageOnSequenceEnd = step.openingCombatDamage;
                    break;
            }
        }

        private void PlayStepOneShots(StorySequenceStep step)
        {
            if (step == null || step.oneShotAudioClips == null || step.oneShotAudioClips.Count == 0)
            {
                return;
            }

            AudioManager manager = AudioManager.Instance;
            if (manager == null)
            {
                Debug.LogWarning("StorySequenceRunner: AudioManager Bootstrap absent, one-shots d'etape ignores.", this);
                return;
            }

            Vector3 position = transform.position;
            for (int i = 0; i < step.oneShotAudioClips.Count; i++)
            {
                AudioClipSO clip = step.oneShotAudioClips[i];
                if (clip != null && clip.audioClip != null)
                {
                    manager.PlayOneShotClip(clip, position);
                }
            }
        }

        private IEnumerator ExecuteProgressivePlayerStop(StorySequenceStep step, bool useUnscaledTime)
        {
            Transform localRoot = LocalPlayerContext.LocalCharacterRoot;
            SquadCharacterController controller = localRoot != null
                ? localRoot.GetComponent<SquadCharacterController>()
                : null;
            if (controller == null)
            {
                Debug.LogWarning("StorySequenceRunner: impossible d'arreter progressivement le joueur local.", this);
                yield break;
            }

            if (!playerLockHeld)
            {
                lockedPlayerController = controller;
                playerLockHeld = controller.TryBeginUccProgressiveStop(this, out playerLock, 
                    disableGameplayInput: true,
                    stopActiveAbilities: true);
            }

            if (!playerLockHeld)
            {
                Debug.LogWarning("StorySequenceRunner: verrou UCC progressif refuse pour le joueur local.", this);
                yield break;
            }

            float elapsed = 0f;
            float timeout = Mathf.Max(0.05f, step.playerStopTimeout);
            while (!controller.IsUccProgressiveStopComplete(step.playerStopVelocityThreshold) && elapsed < timeout)
            {
                elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                yield return null;
            }

            if (!controller.IsUccProgressiveStopComplete(step.playerStopVelocityThreshold))
            {
                Debug.LogWarning("StorySequenceRunner: arret progressif expire; arret complet applique.", this);
                controller.CompleteUccProgressiveStop();
            }
        }

        private IEnumerator ExecuteDialogue(StorySequenceStep step, bool useUnscaledTime, bool cameraPrepared)
        {
            StorySequenceActor actor = bindings.ResolveActor(step.actorId);
            StorySequenceActor listener = bindings.ResolveActor(step.listenerId);
            if (actor == null)
            {
                Debug.LogWarning(
                    $"StorySequenceRunner: acteur '{step.actorId}' introuvable pour le dialogue.",
                    this);
            }

            if (!cameraPrepared && step.focusCameraOnSpeaker && actor != null)
            {
                StorySequenceCameraProfile profile = step.dialogueCameraProfile != null
                    ? step.dialogueCameraProfile
                    : step.cameraProfile;
                cameraDriver.SetActorShot(
                    actor,
                    listener,
                    profile,
                    step.cameraTransitionDuration);
            }

            CharacterData speaker = bindings.ResolveDialogueSpeaker(step, actor);
            var physicalSpeaker = step.speakerCharacterData != null ? DialoguePresentationSession.ResolveSpeaker(speaker) : null;
            if(step.speakerCharacterData!=null && physicalSpeaker==null && speaker!=null)
            {
                var boundSpeaker=bindings.ResolveActor(speaker.characterId);
                if(boundSpeaker!=null)physicalSpeaker=boundSpeaker.Root;
            }
            if(step.speakerCharacterData==null && actor!=null)physicalSpeaker=actor.Root;
            if(conversationLook==null)BeginPlayerConversationLook(sequence);
            conversationLook?.SetSequenceSpeaker(conversationLookToken,physicalSpeaker);
            string speakerName = !string.IsNullOrWhiteSpace(step.speakerNameOverride)
                ? step.speakerNameOverride
                : step.speakerCharacterData != null ? speaker.ResolveDisplayName()
                : actor != null ? actor.DisplayName : step.actorId;
            Transform audioAnchor = actor != null ? actor.FaceAnchor : null;
            yield return dialoguePresenter.Present(
                step.voiceLine,
                speakerName,
                audioAnchor,
                step.dialogueMaxDisplayDuration,
                useUnscaledTime,
                ConsumeAdvanceRequest,
                speaker,
                actor != null ? actor.Root : null,
                listener != null ? listener.Root : null,
                sequence != null && (sequence.lockPlayerControl || hasPlayerPlacement),
                step.overrideDialogueDirection ? step.dialogueDirection : step.voiceLine.dialogueDirection,
                this);
            // Play the response after the line has closed, before cutting away.
            if (cameraDriver.PlayNod(step.dialogueNodDegrees, step.dialogueNodDuration))
            {
                while (cameraDriver.IsNodding)
                    yield return null;
            }
        }

        private IEnumerator ExecuteCameraShot(StorySequenceStep step, bool useUnscaledTime, bool cameraPrepared)
        {
            if (cameraPrepared)
            {
                yield return WaitSkippable(step.cameraHoldDuration > 0f ? step.cameraHoldDuration : step.cameraTransitionDuration, useUnscaledTime);
                yield break;
            }
            StorySequenceCameraPoint point = bindings.ResolveCameraPoint(step.cameraPointId);
            if (point != null)
            {
                cameraDriver.SetCameraPoint(point, step.cameraTransitionDuration);
            }
            else
            {
                StorySequenceActor actor = bindings.ResolveActor(step.actorId);
                StorySequenceActor listener = bindings.ResolveActor(step.listenerId);
                if (actor != null)
                {
                    cameraDriver.SetActorShot(
                        actor,
                        listener,
                        step.cameraProfile,
                        step.cameraTransitionDuration);
                }
                else
                {
                    Debug.LogWarning(
                        $"StorySequenceRunner: plan camera sans point '{step.cameraPointId}' ni acteur '{step.actorId}'.",
                        this);
                }
            }

            float waitDuration = step.cameraHoldDuration > 0f
                ? step.cameraHoldDuration
                : step.cameraTransitionDuration;
            yield return WaitSkippable(waitDuration, useUnscaledTime);
        }

        private IEnumerator ExecuteAnimatorTrigger(StorySequenceStep step, bool useUnscaledTime)
        {
            StorySequenceActor actor = bindings.ResolveActor(step.actorId);
            Animator animator = actor != null ? actor.Animator : null;
            if (animator != null && !string.IsNullOrWhiteSpace(step.animatorTrigger))
            {
                animator.SetTrigger(step.animatorTrigger);
            }
            else
            {
                Debug.LogWarning(
                    $"StorySequenceRunner: Animator/trigger manquant pour '{step.actorId}'.",
                    this);
            }

            yield return WaitSkippable(step.duration, useUnscaledTime);
        }

        private IEnumerator ExecuteSitting(StorySequenceStep step, bool useUnscaledTime)
        {
            bool useImmediateIdle = step.sitting && step.startDirectlyInSittingIdle;
            bool restorePlayerExternalLock =
                !useImmediateIdle &&
                playerLockHeld &&
                lockedPlayerController != null;
            if (restorePlayerExternalLock)
            {
                playerLock?.Dispose();
                playerLockHeld = false;
            }

            float retryDuration = Mathf.Max(0f, step.duration);
            float elapsed = 0f;
            int targetCount;
            int affectedCount;
            bool completed = false;
            do
            {
                affectedCount = 0;
                targetCount = 0;
                if (step.applyToWholeSquad)
                {
                    SquadManager manager = SquadManager.Instance;
                    if (manager != null && manager.squadCharacters != null)
                    {
                        for (int i = 0; i < manager.squadCharacters.Count; i++)
                        {
                            GameObject character = manager.squadCharacters[i];
                            SquadCharacterController controller = character != null
                                ? character.GetComponent<SquadCharacterController>()
                                : null;
                            if (controller == null)
                            {
                                continue;
                            }

                            targetCount++;
                            bool applied = useImmediateIdle
                                ? controller.TrySetSittingImmediate()
                                : ApplySittingState(controller, step.sitting);
                            if (applied)
                            {
                                affectedCount++;
                            }
                        }
                    }
                }
                else
                {
                    StorySequenceActor actor = bindings.ResolveActor(step.actorId);
                    SquadCharacterController controller = actor != null
                        ? actor.GetComponent<SquadCharacterController>()
                        : null;
                    if (controller != null)
                    {
                        targetCount = 1;
                        bool applied = useImmediateIdle
                            ? controller.TrySetSittingImmediate()
                            : ApplySittingState(controller, step.sitting);
                        if (applied)
                        {
                            affectedCount = 1;
                        }
                    }
                }

                if (targetCount > 0 && affectedCount >= targetCount)
                {
                    completed = true;
                    break;
                }

                if (ConsumeAdvanceRequest())
                {
                    break;
                }

                if (elapsed >= retryDuration)
                {
                    break;
                }

                elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                yield return null;
            }
            while (true);

            if (restorePlayerExternalLock && lockedPlayerController != null)
            {
                playerLockHeld = lockedPlayerController.TryBeginUccExternalLock(this, out playerLock, 
                    disableGameplayInput: true,
                    stopActiveAbilities: false);
            }

            if (!completed && (affectedCount < targetCount || targetCount == 0))
            {
                Debug.LogWarning(
                    $"StorySequenceRunner: etat assis={step.sitting} applique a {affectedCount}/{targetCount} personnage(s).",
                    this);
            }
        }

        private static bool ApplySittingState(
            SquadCharacterController controller,
            bool sitting)
        {
            bool alreadyApplied = sitting
                ? controller.IsSittingRequested
                : !controller.IsSittingRequested;
            return alreadyApplied || controller.TrySetSitting(sitting);
        }

        private IEnumerator ExecuteTimeline(StorySequenceStep step)
        {
            conversationLook?.EndSequenceLook(conversationLookToken);
            conversationLook=null;conversationLookToken=0;
            PlayableDirector director = bindings.ResolveDirector(step.directorId);
            if (director == null || step.timeline == null || step.timelineBindingProfile == null)
            {
                Debug.LogWarning(
                    $"StorySequenceRunner: Timeline, profile de bindings ou PlayableDirector manquant pour '{step.label}'.",
                    this);
                yield break;
            }

            if (Lit.Timeline.TimelineManager.Instance == null)
            {
                Debug.LogError("StorySequenceRunner: TimelineManager Bootstrap est absent.", this);
                yield break;
            }

            director.playableAsset = step.timeline;
            director.extrapolationMode = DirectorWrapMode.None;
            TimelineBindingContext context = new TimelineBindingContext();
            Transform localPlayer = LocalPlayerContext.LocalCharacterRoot;
            Animator playerAnimator = localPlayer != null
                ? localPlayer.GetComponentInChildren<Animator>(true)
                : null;
            if (playerAnimator != null)
            {
                context.Bind("Player.Animator", playerAnimator);
            }
            if (localPlayer != null)
            {
                context.Bind("Player.Transform", localPlayer);
            }

            TimelinePlaybackHandle handle = Lit.Timeline.TimelineManager.Instance.Play(
                director,
                step.timelineBindingProfile,
                context,
                TimelinePlaybackOptions.Default);

            if (!step.waitForTimelineCompletion)
            {
                yield break;
            }

            bool timelineEndFadeStarted = false;
            double timelineEndFadeStartTime = System.Math.Max(
                0d,
                director.duration - Mathf.Max(0f, step.timelineEndFadeDuration));
            while (!handle.IsDone)
            {
                // StorySequence starts the fade before Timeline hands the
                // camera back to UCC, so the hand-off is never visible.
                if (step.fadeToBlackBeforeTimelineEnd &&
                    !timelineEndFadeStarted &&
                    director.state == PlayState.Playing &&
                    director.time >= timelineEndFadeStartTime)
                {
                    timelineEndFadeStarted = true;
                    yield return fadeController.FadeTo(
                        1f,
                        Mathf.Max(0f, step.timelineEndFadeDuration),
                        sequence != null && sequence.useUnscaledTime);
                }

                if (ConsumeAdvanceRequest())
                {
                    handle.Skip();
                    break;
                }

                yield return null;
            }

            if (handle.State == TimelinePlaybackState.Failed)
            {
                Debug.LogWarning(
                    $"StorySequenceRunner: Timeline '{step.label}' non jouee : {handle.FailureReason}",
                    this);
            }
        }

        private IEnumerator WaitSkippable(float duration, bool useUnscaledTime)
        {
            float elapsed = 0f;
            float wait = Mathf.Max(0f, duration);
            while (elapsed < wait)
            {
                if (ConsumeAdvanceRequest())
                {
                    yield break;
                }

                elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                yield return null;
            }
        }

        private IEnumerator WaitForLocalPlayer(float timeout)
        {
            float startedAt = Time.unscaledTime;
            while (LocalPlayerContext.LocalCharacterRoot == null)
            {
                if (timeout > 0f && Time.unscaledTime - startedAt >= timeout)
                {
                    yield break;
                }

                yield return null;
            }

            // Laisse UCC, l'Animator et le binder camera terminer leur initialisation.
            yield return null;
        }

        private void AcquirePlayerLock(StorySequenceAsset activeSequence)
        {
            if ((!activeSequence.lockPlayerControl && !hasPlayerPlacement) || playerLockHeld)
            {
                return;
            }

            Transform localRoot = LocalPlayerContext.LocalCharacterRoot;
            lockedPlayerController = localRoot != null
                ? localRoot.GetComponent<SquadCharacterController>()
                : null;
            if (lockedPlayerController == null)
            {
                Debug.LogWarning(
                    "StorySequenceRunner: SquadCharacterController local introuvable; seul InputFocusStack verrouille le gameplay.",
                    this);
                return;
            }

            // An ordinary conversation keeps the motor's existing deceleration. Authored
            // Timeline, sitting and fade openings retain their explicit staging contract.
            bool conversation = !activeSequence.startFromBlack && !startWithLocalPlayerSeated &&
                activeSequence.steps != null && activeSequence.steps.Exists(s => s != null && s.type == StorySequenceStepType.Dialogue) &&
                !activeSequence.steps.Exists(s => s != null && (s.type == StorySequenceStepType.Timeline ||
                    s.type == StorySequenceStepType.Sitting || s.type == StorySequenceStepType.ProgressivePlayerStop));
            playerLockHeld = conversation
                ? lockedPlayerController.TryBeginUccProgressiveStop(this, out playerLock,
                    disableGameplayInput: true, stopActiveAbilities: activeSequence.stopActiveAbilitiesOnLock)
                : lockedPlayerController.TryBeginUccExternalLock(this, out playerLock,
                    disableGameplayInput: true, stopActiveAbilities: activeSequence.stopActiveAbilitiesOnLock);
            if (playerLockHeld && !conversation)
            {
                lockedPlayerController.GetComponent<PlayerAnimationController>()?.PrepareCinematicIdle();
            }
        }

        private static bool HasProgressivePlayerStop(StorySequenceAsset sequence)
        {
            if (sequence == null || sequence.steps == null)
            {
                return false;
            }

            for (int i = 0; i < sequence.steps.Count; i++)
            {
                if (sequence.steps[i] != null &&
                    sequence.steps[i].type == StorySequenceStepType.ProgressivePlayerStop)
                {
                    return true;
                }
            }

            return false;
        }

        private void RestoreGameplay()
        {
            ReleasePlayerPreparation();
            conversationLook?.EndSequenceLook(conversationLookToken);
            conversationLook=null;conversationLookToken=0;
            namePrompt?.Cancel();
            dialoguePresenter?.HideImmediate();
            RestoreCamera();
            ReleasePlayerControl();
        }

        private void RestoreCamera()
        {
            dialoguePresenter?.HideImmediate();
            cameraDriver?.EndCinematic(snapBackToGameplay: true);
        }

        private void ReleasePlayerControl()
        {
            if (seatedOpeningPlayer != null)
                seatedOpeningPlayer.TrySetSitting(false);
            seatedOpeningPlayer = null;

            if (playerLockHeld && lockedPlayerController != null)
            {
                playerLock?.Dispose();
            }

            playerLockHeld = false;
            lockedPlayerController = null;
            PopInputFocus();
        }

        private void FinishPlayback(bool completed)
        {
            ReleaseSequenceSpawns(completed);
            bool launchCombat = completed && launchCombatOnSequenceEnd;
            float openingCombatDamage = launchCombatDamageOnSequenceEnd;
            RestoreGameplay();
            playbackRoutine = null;
            launchCombatOnSequenceEnd = false;
            launchCombatDamageOnSequenceEnd = 0f;
            UnregisterActiveSequence();
            currentStepSkippable = false;
            advanceRequested = false;
            if (completed)
            {
                if (launchCombat)
                {
                    RealTimeCombatManager.Instance?.LaunchCombat(openingCombatDamage);
                }

                onSequenceCompleted?.Invoke();
            }
            else
            {
                fadeController?.SetImmediate(0f);
                if(!sequenceContentStarted)PreparationFailed?.Invoke();
                onSequenceAborted?.Invoke();
            }
        }

        private void RegisterActiveSequence()
        {
            if (registeredAsActive)
            {
                return;
            }

            registeredAsActive = true;
            activeSequenceCount++;
        }

        private void UnregisterActiveSequence()
        {
            if (!registeredAsActive)
            {
                return;
            }

            registeredAsActive = false;
            activeSequenceCount = Mathf.Max(0, activeSequenceCount - 1);
        }

        private bool ConsumeAdvanceRequest()
        {
            if (!currentStepSkippable || !advanceRequested)
            {
                return false;
            }

            advanceRequested = false;
            return true;
        }

        private void OnInteractPerformed(InputAction.CallbackContext context)
        {
            if (!IsPlaying ||
                !currentStepSkippable ||
                Time.frameCount == currentStepStartedFrame)
            {
                return;
            }

            if (LocalInputRouter.TryConsumeInteract())
            {
                advanceRequested = true;
            }
        }

        private void PushInputFocus()
        {
            InputFocusStack.Push(this);
            InputModeCoordinator.Enter(this, InputMode.Cinematic);
            if (inputSubscribed)
            {
                return;
            }

            LocalInputRouter.EnsureInitialized();
            LocalInputRouter.Interact += OnInteractPerformed;
            inputSubscribed = true;
        }

        private void PopInputFocus()
        {
            InputFocusStack.Pop(this);
            if (!inputSubscribed)
            {
                return;
            }

            LocalInputRouter.Interact -= OnInteractPerformed;
            inputSubscribed = false;
        }

        private void ResolveReferences()
        {
            if (bindings == null)
            {
                bindings = GetComponent<StorySequenceSceneBindings>();
            }

            if (cameraDriver == null)
            {
                cameraDriver = GetComponent<StorySequenceCameraDriver>();
            }

            if (dialoguePresenter == null)
            {
                dialoguePresenter = GetComponent<StorySequenceDialoguePresenter>();
            }

            if (fadeController == null)
            {
                fadeController = GetComponent<StorySequenceFadeController>();
            }

            if (namePrompt == null) namePrompt = GetComponent<StorySequenceNamePrompt>();

            if (bindings == null || fadeController == null)
            {
                Debug.LogError(
                    "StorySequenceRunner requiert StorySequenceSceneBindings et StorySequenceFadeController preconfigures.",
                    this);
            }
        }

        private void OnDisable()
        {
            ReleaseSequenceSpawns(false);
            playbackGeneration++;
            if(IsPreparingPlayer)PreparationFailed?.Invoke();
            CancelDeferredPlayback();
            fadeController?.SetImmediate(0f);
            if (playbackRoutine != null)
            {
                StopCoroutine(playbackRoutine);
                playbackRoutine = null;
            }

            RestoreGameplay();
            launchCombatOnSequenceEnd = false;
            launchCombatDamageOnSequenceEnd = 0f;
            UnregisterActiveSequence();
        }
    }
}
