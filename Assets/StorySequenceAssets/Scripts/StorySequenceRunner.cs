using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

namespace Lit.Story
{
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public sealed class StorySequenceRunner : MonoBehaviour
    {
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

        public bool IsPlaying => playbackRoutine != null;
        public StorySequenceAsset Sequence => sequence;

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

            AcquirePlayerLock(activeSequence);
            cameraDriver.BeginCinematic();

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
                step.waitForInteractAfterLine,
                step.duration,
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
            bool restorePlayerExternalLock = playerLockHeld && lockedPlayerController != null;
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
                            bool alreadyApplied = step.sitting
                                ? controller.IsSittingRequested
                                : !controller.IsSittingRequested;
                            if (alreadyApplied || controller.TrySetSitting(step.sitting))
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
                        bool alreadyApplied = step.sitting
                            ? controller.IsSittingRequested
                            : !controller.IsSittingRequested;
                        if (alreadyApplied || controller.TrySetSitting(step.sitting))
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

        private IEnumerator ExecuteTimeline(StorySequenceStep step)
        {
            PlayableDirector director = bindings.ResolveDirector(step.directorId);
            if (director == null || step.timeline == null)
            {
                Debug.LogWarning(
                    $"StorySequenceRunner: Timeline ou PlayableDirector manquant pour '{step.label}'.",
                    this);
                yield break;
            }

            director.playableAsset = step.timeline;
            director.extrapolationMode = DirectorWrapMode.None;
            director.time = 0d;
            director.Play();

            if (!step.waitForTimelineCompletion)
            {
                yield break;
            }

            while (director.state == PlayState.Playing)
            {
                if (ConsumeAdvanceRequest())
                {
                    double duration = director.duration;
                    if (!double.IsNaN(duration) && !double.IsInfinity(duration) && duration > 0d)
                    {
                        director.time = duration;
                        director.Evaluate();
                    }

                    director.Stop();
                    break;
                }

                yield return null;
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
            if (!activeSequence.lockPlayerControl)
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

            if (bindings == null)
            {
                bindings = gameObject.AddComponent<StorySequenceSceneBindings>();
            }

            if (cameraDriver == null)
            {
                cameraDriver = GetComponent<StorySequenceCameraDriver>();
            }

            if (cameraDriver == null)
            {
                cameraDriver = gameObject.AddComponent<StorySequenceCameraDriver>();
            }

            if (dialoguePresenter == null)
            {
                dialoguePresenter = GetComponent<StorySequenceDialoguePresenter>();
            }

            if (dialoguePresenter == null)
            {
                dialoguePresenter = gameObject.AddComponent<StorySequenceDialoguePresenter>();
            }

            if (fadeController == null)
            {
                fadeController = GetComponent<StorySequenceFadeController>();
            }

            if (fadeController == null)
            {
                fadeController = gameObject.AddComponent<StorySequenceFadeController>();
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
        }
    }
}
