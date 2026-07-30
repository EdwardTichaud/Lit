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
    public sealed class StorySequenceRunner : MonoBehaviour
    {
        private static int activeSequenceCount;

        [Header("Sequence")]
        [SerializeField] private StorySequenceAsset sequence;
        [SerializeField] private bool playOnStart = true;
        [SerializeField, Min(0f)] private float startDelay;

        [Header("References")]
        [SerializeField] private StorySequenceSceneBindings bindings;
        [SerializeField] private StorySequenceCameraDriver cameraDriver;
        [SerializeField] private StorySequenceDialoguePresenter dialoguePresenter;
        [SerializeField] private StorySequenceFadeController fadeController;

        [Header("Events")]
        [SerializeField] private UnityEvent onSequenceStarted;
        [SerializeField] private UnityEvent onSequenceCompleted;
        [SerializeField] private UnityEvent onSequenceAborted;

        private Coroutine playbackRoutine;
        private SquadCharacterController lockedPlayerController;
        private bool playerLockHeld;
        private bool advanceRequested;
        private bool currentStepSkippable;
        private int currentStepStartedFrame;
        private bool inputSubscribed;
        private bool registeredAsActive;

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
                sequence.startFromBlack &&
                (!sequence.playOnce || !StorySequenceCompletionStore.IsCompleted(sequence)))
            {
                fadeController.SetImmediate(1f);
            }
        }

        private IEnumerator Start()
        {
            if (!playOnStart || sequence == null)
            {
                yield break;
            }

            if (sequence.playOnce && StorySequenceCompletionStore.IsCompleted(sequence))
            {
                fadeController.SetImmediate(0f);
                yield break;
            }

            if (startDelay > 0f)
            {
                yield return new WaitForSecondsRealtime(startDelay);
            }

            Play();
        }

        public bool Play()
        {
            return Play(sequence);
        }

        public bool Play(StorySequenceAsset targetSequence)
        {
            if (targetSequence == null || IsPlaying)
            {
                return false;
            }

            ResolveReferences();
            sequence = targetSequence;
            if (sequence.playOnce && StorySequenceCompletionStore.IsCompleted(sequence))
            {
                fadeController.SetImmediate(0f);
                return false;
            }

            RegisterActiveSequence();
            playbackRoutine = StartCoroutine(PlayRoutine(sequence));
            return true;
        }

        public void Abort()
        {
            if (playbackRoutine != null)
            {
                StopCoroutine(playbackRoutine);
                playbackRoutine = null;
            }

            dialoguePresenter?.HideImmediate();
            RestoreGameplay();
            UnregisterActiveSequence();
            fadeController?.SetImmediate(0f);
            onSequenceAborted?.Invoke();
        }

        private IEnumerator PlayRoutine(StorySequenceAsset activeSequence)
        {
            advanceRequested = false;
            currentStepSkippable = false;
            PushInputFocus();
            onSequenceStarted?.Invoke();

            if (activeSequence.startFromBlack)
            {
                fadeController.SetImmediate(1f);
            }

            if (activeSequence.waitForLocalPlayer)
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

            bool deferPlayerLock = HasProgressivePlayerStop(activeSequence);
            if (!deferPlayerLock)
            {
                AcquirePlayerLock(activeSequence);
            }
            cameraDriver?.BeginCinematic();

            if (activeSequence.startFromBlack)
            {
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
                    yield return ExecuteStep(step, activeSequence.useUnscaledTime);
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

        private IEnumerator ExecuteStep(StorySequenceStep step, bool useUnscaledTime)
        {
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
                    yield return ExecuteDialogue(step, useUnscaledTime);
                    break;

                case StorySequenceStepType.CameraShot:
                    yield return ExecuteCameraShot(step, useUnscaledTime);
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

                case StorySequenceStepType.ProgressivePlayerStop:
                    yield return ExecuteProgressivePlayerStop(step, useUnscaledTime);
                    break;
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
                playerLockHeld = controller.TryBeginUccProgressiveStop(
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

        private IEnumerator ExecuteDialogue(StorySequenceStep step, bool useUnscaledTime)
        {
            StorySequenceActor actor = bindings.ResolveActor(step.actorId);
            StorySequenceActor listener = bindings.ResolveActor(step.listenerId);
            if (actor == null)
            {
                Debug.LogWarning(
                    $"StorySequenceRunner: acteur '{step.actorId}' introuvable pour le dialogue.",
                    this);
            }

            if (step.focusCameraOnSpeaker && actor != null)
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

            string speakerName = !string.IsNullOrWhiteSpace(step.speakerNameOverride)
                ? step.speakerNameOverride
                : actor != null ? actor.DisplayName : step.actorId;
            Transform audioAnchor = actor != null ? actor.FaceAnchor : null;
            yield return dialoguePresenter.Present(
                step.voiceLine,
                speakerName,
                audioAnchor,
                step.dialogueMaxDisplayDuration,
                useUnscaledTime,
                ConsumeAdvanceRequest);
        }

        private IEnumerator ExecuteCameraShot(StorySequenceStep step, bool useUnscaledTime)
        {
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
                lockedPlayerController.EndUccExternalLock();
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
                playerLockHeld = lockedPlayerController.TryBeginUccExternalLock(
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

            while (!handle.IsDone)
            {
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
            if (!activeSequence.lockPlayerControl || playerLockHeld)
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

            playerLockHeld = lockedPlayerController.TryBeginUccExternalLock(
                disableGameplayInput: true,
                stopActiveAbilities: activeSequence.stopActiveAbilitiesOnLock);
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
            if (playerLockHeld && lockedPlayerController != null)
            {
                lockedPlayerController.EndUccExternalLock();
            }

            playerLockHeld = false;
            lockedPlayerController = null;
            PopInputFocus();
        }

        private void FinishPlayback(bool completed)
        {
            RestoreGameplay();
            playbackRoutine = null;
            UnregisterActiveSequence();
            currentStepSkippable = false;
            advanceRequested = false;
            if (completed)
            {
                onSequenceCompleted?.Invoke();
            }
            else
            {
                fadeController?.SetImmediate(0f);
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

            if (bindings == null || fadeController == null)
            {
                Debug.LogError(
                    "StorySequenceRunner requiert StorySequenceSceneBindings et StorySequenceFadeController preconfigures.",
                    this);
            }
            else if (cameraDriver == null || dialoguePresenter == null)
            {
                Debug.LogWarning(
                    "StorySequenceRunner: CameraDriver et DialoguePresenter sont optionnels pour une sequence ne contenant ni plan camera StorySequence ni dialogue.",
                    this);
            }
        }

        private void OnDisable()
        {
            if (playbackRoutine != null)
            {
                StopCoroutine(playbackRoutine);
                playbackRoutine = null;
            }

            RestoreGameplay();
            UnregisterActiveSequence();
        }
    }
}
