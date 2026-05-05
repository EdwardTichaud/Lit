using System;
using System.Globalization;
using System.Text;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ReadableSentencePuzzle : MonoBehaviour, ICharacterDetectedInteractable
{
    public enum SolveAttemptResult
    {
        Success = 0,
        IncorrectAnswer = 1,
        SentenceUnavailable = 2,
        PuzzleAlreadySolved = 3,
        InvalidCharacter = 4,
        InvalidConfiguration = 5
    }

    [Header("Reference")]
    [SerializeField] private ReadableSentenceReference requiredSentence;
    [SerializeField] private string panelTitle = "Enigme";
    [SerializeField, TextArea(2, 4)] private string promptOverride;
    [SerializeField] private bool playOnce = true;

    [Header("Validation")]
    [SerializeField] private bool ignoreCase = true;
    [SerializeField] private bool ignoreAccents = true;
    [SerializeField] private bool ignorePunctuation = true;
    [SerializeField] private bool collapseWhitespace = true;
    [SerializeField] private bool trimWhitespace = true;

    [Header("Interaction")]
    [SerializeField] private Collider interactionCollider;
    [SerializeField] private float interactionMaxDistance = 2.25f;
    [SerializeField] private int interactionPriority = 40;

    [Header("UI - Interaction")]
    [SerializeField] private GameObject interactionBox;
    [SerializeField] private string interactionText = "Resoudre l'enigme";
    [SerializeField] private Vector3 interactionOffset = new Vector3(0f, 2f, 0f);

    [Header("UI - Feedback")]
    [SerializeField] private string solvedMessage = "Enigme resolue.";
    [SerializeField, Min(0f)] private float solvedFeedbackDelay = 2f;
    [SerializeField, Min(0f)] private float attemptFeedbackDuration = 1f;
    [SerializeField] private string incorrectAnswerMessage = "Ce n'est pas la bonne phrase.";
    [SerializeField] private string sentenceUnavailableMessage = "La phrase demandee n'est pas disponible.";
    [SerializeField] private string alreadySolvedMessage = "Cette enigme a deja ete resolue.";
    [SerializeField] private string invalidConfigurationMessage = "Cette enigme n'est pas configuree correctement.";

    [Header("Audio")]
    [SerializeField] private AudioClipSO successSfx;
    [SerializeField] private AudioClipSO failureSfx;

    [Header("UI - Parent")]
    [SerializeField] private Transform boxesPanel;

    [Header("Camera")]
    [SerializeField] private Camera targetCamera;

    [Header("Events")]
    [Tooltip("Evenement local declenche quand l'etat passe a resolu sur ce client.")]
    [SerializeField] private UnityEvent onSolved;
    [Tooltip("Evenement declenche sur le serveur quand une bonne reponse est validee.")]
    [SerializeField] private UnityEvent onSolvedServer;
    [SerializeField] private bool verboseLogs;

    private GameObject currentCharacter;
    private Transform interactionTarget;
    private GameObject interactionBoxInstance;
    private Canvas interactionCanvas;
    private Collider resolvedInteractionCollider;
    private uint netcodeId;
    private bool awaitingServerResponse;
    private bool isSolved;
    private bool waitingForSolvedFeedbackClose;
    private bool applySolvedStateAfterFeedbackClose;
    private bool invokeSolvedEventAfterFeedbackClose;

    public bool IsSolved => isSolved;
    public bool PlayOnce => playOnce;
    public ReadableSentenceReference RequiredSentence => requiredSentence;

    private void Reset()
    {
        interactionCollider = CharacterInteractionDetection.ResolveInteractionCollider(this, interactionCollider);
    }

    private void OnValidate()
    {
        interactionMaxDistance = Mathf.Max(0.1f, interactionMaxDistance);
    }

    private void Awake()
    {
        resolvedInteractionCollider = CharacterInteractionDetection.ResolveInteractionCollider(this, interactionCollider);
        if (interactionCollider == null)
        {
            interactionCollider = resolvedInteractionCollider;
        }

        netcodeId = NetcodeSceneIdUtility.GetStableId(transform);
    }

    private void OnEnable()
    {
        LocalInputRouter.EnsureInitialized();
        LocalInputRouter.Interact += OnInteractPerformed;
        NetcodeTriggerRegistry.Register(this, netcodeId);
    }

    private void OnDisable()
    {
        LocalInputRouter.Interact -= OnInteractPerformed;
        NetcodeTriggerRegistry.Unregister(this, netcodeId);
        ReadableSentencePuzzleUI.Dismiss(this);
        ResetUIState();
    }

    private void LateUpdate()
    {
        if (interactionBoxInstance == null || !interactionBoxInstance.activeSelf)
        {
            return;
        }

        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null || interactionTarget == null)
        {
            return;
        }

        Vector3 worldPosition = interactionTarget.position + interactionOffset;
        Canvas canvas = interactionCanvas != null ? interactionCanvas : interactionBoxInstance.GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode != RenderMode.WorldSpace)
        {
            RectTransform rect = interactionBoxInstance.GetComponent<RectTransform>();
            if (rect == null)
            {
                return;
            }

            Vector3 screenPos = cam.WorldToScreenPoint(worldPosition);
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                rect.position = screenPos;
                return;
            }

            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            Camera uiCamera = canvas.worldCamera != null ? canvas.worldCamera : cam;
            if (canvasRect != null &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, uiCamera, out Vector2 localPoint))
            {
                rect.localPosition = localPoint;
            }

            return;
        }

        interactionBoxInstance.transform.position = worldPosition;

        Vector3 toCamera = interactionBoxInstance.transform.position - cam.transform.position;
        if (toCamera.sqrMagnitude < 0.0001f)
        {
            return;
        }

        interactionBoxInstance.transform.rotation = Quaternion.LookRotation(toCamera);
    }

    public bool CanBeDetectedBy(SquadCharacterController controller)
    {
        return controller != null
            && isActiveAndEnabled
            && requiredSentence.IsConfigured
            && (!playOnce || !isSolved);
    }

    public Collider GetInteractionDetectionCollider()
    {
        if (resolvedInteractionCollider == null)
        {
            resolvedInteractionCollider = CharacterInteractionDetection.ResolveInteractionCollider(this, interactionCollider);
        }

        return resolvedInteractionCollider;
    }

    public Transform GetInteractionAnchor()
    {
        return transform;
    }

    public float GetInteractionMaxDistance(SquadCharacterController controller)
    {
        return Mathf.Max(0.1f, interactionMaxDistance);
    }

    public int GetInteractionPriority(SquadCharacterController controller)
    {
        return interactionPriority;
    }

    public void SetDetectedCharacter(GameObject character)
    {
        if (currentCharacter == character)
        {
            return;
        }

        currentCharacter = character;
        interactionTarget = currentCharacter != null ? currentCharacter.transform : null;
        ShowInteraction(currentCharacter != null && (!playOnce || !isSolved));
    }

    public bool TryResolveExpectedSentence(out string sentence)
    {
        sentence = string.Empty;
        return requiredSentence.TryGetGeneratedSentence(out sentence);
    }

    public string BuildPrompt()
    {
        if (!string.IsNullOrWhiteSpace(promptOverride))
        {
            return promptOverride.Trim();
        }

        string readableName = requiredSentence.ResolveDisplayName();
        if (string.IsNullOrWhiteSpace(readableName))
        {
            readableName = "ce document";
        }

        return $"Entrez la phrase n°{requiredSentence.SentenceNumber} de \"{readableName}\".";
    }

    public void RestoreSolvedState(bool solved)
    {
        ApplySolvedState(solved, invokeLocalEvent: false);
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        if (InputFocusStack.HasAnyFocus())
        {
            return;
        }

        GameObject character = currentCharacter;
        if (!CanUse(character, requireLocalControl: true, rangePadding: 0f))
        {
            return;
        }

        if (playOnce && isSolved)
        {
            InfoBoxUI.TryShow(alreadySolvedMessage);
            return;
        }

        if (!ShowPuzzleUi())
        {
            return;
        }

        LocalInputRouter.ConsumeInteract();
    }

    private bool ShowPuzzleUi()
    {
        return ReadableSentencePuzzleUI.TryShow(
            this,
            string.IsNullOrWhiteSpace(panelTitle) ? "Enigme" : panelTitle.Trim(),
            BuildPrompt(),
            SubmitAnswer,
            HandlePuzzleUiCancelled,
            debugContext: name);
    }

    private void SubmitAnswer(string answer)
    {
        if (playOnce && isSolved)
        {
            awaitingServerResponse = false;
            ShowAttemptFailureFeedback(alreadySolvedMessage);
            return;
        }

        GameObject character = currentCharacter != null
            ? currentCharacter
            : LocalPlayerUtils.GetControlledCharacter();
        if (!CanUse(character, requireLocalControl: true, rangePadding: 0f))
        {
            awaitingServerResponse = false;
            ShowAttemptFailureFeedback(invalidConfigurationMessage);
            return;
        }

        if (IsNetworked() && !NetworkManager.Singleton.IsServer)
        {
            if (awaitingServerResponse)
            {
                return;
            }

            WorldInteractionService service = WorldInteractionService.Instance;
            if (service == null)
            {
                return;
            }

            awaitingServerResponse = true;
            ReadableSentencePuzzleUI.SetInteractable(this, false);
            service.RequestReadableSentencePuzzleAttemptServerRpc(netcodeId, answer ?? string.Empty);
            return;
        }

        SolveAttemptResult result = ServerTrySubmitAnswer(character, answer ?? string.Empty);
        HandleSolveAttemptResult(result);
    }

    public SolveAttemptResult ServerTrySubmitAnswer(GameObject character, string answer)
    {
        if (!CanUse(character, requireLocalControl: false, rangePadding: 0.35f))
        {
            return SolveAttemptResult.InvalidCharacter;
        }

        if (!requiredSentence.IsConfigured)
        {
            return SolveAttemptResult.InvalidConfiguration;
        }

        if (playOnce && isSolved)
        {
            return SolveAttemptResult.PuzzleAlreadySolved;
        }

        if (!TryResolveExpectedSentence(out string expectedSentence))
        {
            return SolveAttemptResult.SentenceUnavailable;
        }

        if (!IsAnswerCorrect(answer, expectedSentence))
        {
            return SolveAttemptResult.IncorrectAnswer;
        }

        if (playOnce)
        {
            isSolved = true;
        }

        if (verboseLogs)
        {
            Debug.Log(
                $"[ReadableSentencePuzzle] solve_success name='{name}' sentenceNumber={requiredSentence.SentenceNumber} playOnce={playOnce} solved={isSolved}",
                this);
        }

        SafeInvoke(onSolvedServer, "server");
        return SolveAttemptResult.Success;
    }

    public void HandleSolveAttemptResult(SolveAttemptResult result)
    {
        awaitingServerResponse = false;

        if (result == SolveAttemptResult.Success)
        {
            PlayResultSfx(successSfx);
            bool delayPanelClose = TryShowSolvedFeedbackBeforeDismiss();
            if (!IsNetworked())
            {
                if (delayPanelClose)
                {
                    DeferSolvedStateApply(playOnce && isSolved, invokeLocalEvent: playOnce && isSolved);
                    return;
                }

                ApplySolvedState(playOnce && isSolved, invokeLocalEvent: playOnce && isSolved);
                return;
            }

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer && playOnce && isSolved)
            {
                WorldInteractionService service = WorldInteractionService.Instance;
                if (service != null)
                {
                    service.NotifyReadableSentencePuzzleSolved(netcodeId);
                }
                else
                {
                    if (delayPanelClose)
                    {
                        DeferSolvedStateApply(true, invokeLocalEvent: true);
                    }
                    else
                    {
                        ApplySolvedState(true, invokeLocalEvent: true);
                    }
                }
            }
            return;
        }

        ShowAttemptFailureFeedback(ResolveFailureMessage(result));
    }

    private void ShowAttemptFailureFeedback(string message)
    {
        if (!ReadableSentencePuzzleUI.IsShowingFor(this))
        {
            return;
        }

        PlayResultSfx(failureSfx);
        ReadableSentencePuzzleUI.BeginFeedback(
            this,
            message,
            isError: true,
            GetAttemptFeedbackDuration());
    }

    private void ShowSolvedFeedback()
    {
        if (!string.IsNullOrWhiteSpace(solvedMessage))
        {
            InfoBoxUI.TryShow(solvedMessage);
        }
    }

    private void PlayResultSfx(AudioClipSO clip)
    {
        if (clip == null || clip.audioClip == null)
        {
            return;
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayClip(clip, transform.position);
            return;
        }

        AudioSource.PlayClipAtPoint(clip.audioClip, transform.position, Mathf.Clamp01(clip.volume));
    }

    public void HandleSolvedStateReplicated()
    {
        if (waitingForSolvedFeedbackClose)
        {
            DeferSolvedStateApply(true, invokeLocalEvent: true);
            return;
        }

        ApplySolvedState(true, invokeLocalEvent: true);
    }

    private void HandlePuzzleUiCancelled()
    {
        awaitingServerResponse = false;
    }

    private bool CanUse(GameObject character, bool requireLocalControl, float rangePadding)
    {
        if (character == null)
        {
            return false;
        }

        if (requireLocalControl && !IsControlledCharacter(character))
        {
            return false;
        }

        if (GetController(character) == null)
        {
            return false;
        }

        if (!requiredSentence.IsConfigured)
        {
            return false;
        }

        return CharacterInteractionDetection.IsCharacterWithinRange(
            character.transform,
            GetInteractionDetectionCollider(),
            GetInteractionAnchor(),
            interactionMaxDistance + Mathf.Max(0f, rangePadding));
    }

    private bool IsAnswerCorrect(string submittedAnswer, string expectedSentence)
    {
        string normalizedSubmitted = NormalizeAnswer(submittedAnswer);
        string normalizedExpected = NormalizeAnswer(expectedSentence);
        return string.Equals(normalizedSubmitted, normalizedExpected, StringComparison.Ordinal);
    }

    private string NormalizeAnswer(string value)
    {
        string normalized = value ?? string.Empty;
        if (trimWhitespace)
        {
            normalized = normalized.Trim();
        }

        if (ignoreAccents)
        {
            normalized = RemoveAccents(normalized);
        }

        StringBuilder builder = new StringBuilder(normalized.Length);
        bool previousWasWhitespace = false;
        for (int i = 0; i < normalized.Length; i++)
        {
            char character = normalized[i];
            if (ignorePunctuation && (char.IsPunctuation(character) || char.IsSymbol(character)))
            {
                continue;
            }

            if (char.IsWhiteSpace(character))
            {
                if (!collapseWhitespace || !previousWasWhitespace)
                {
                    builder.Append(' ');
                }

                previousWasWhitespace = true;
                continue;
            }

            previousWasWhitespace = false;
            builder.Append(ignoreCase ? char.ToLowerInvariant(character) : character);
        }

        string result = builder.ToString();
        return trimWhitespace ? result.Trim() : result;
    }

    private static string RemoveAccents(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        string formD = value.Normalize(NormalizationForm.FormD);
        StringBuilder builder = new StringBuilder(formD.Length);
        for (int i = 0; i < formD.Length; i++)
        {
            char character = formD[i];
            UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private void ApplySolvedState(bool solved, bool invokeLocalEvent)
    {
        isSolved = solved;
        if (playOnce && isSolved && !waitingForSolvedFeedbackClose)
        {
            ReadableSentencePuzzleUI.Dismiss(this);
        }

        ShowInteraction(currentCharacter != null && (!playOnce || !isSolved));
        if (invokeLocalEvent && isSolved)
        {
            SafeInvoke(onSolved, "client");
        }
    }

    private bool TryShowSolvedFeedbackBeforeDismiss()
    {
        if (string.IsNullOrWhiteSpace(solvedMessage) || !ReadableSentencePuzzleUI.IsShowingFor(this))
        {
            ReadableSentencePuzzleUI.Dismiss(this);
            ShowSolvedFeedback();
            return false;
        }

        applySolvedStateAfterFeedbackClose = false;
        invokeSolvedEventAfterFeedbackClose = false;
        waitingForSolvedFeedbackClose = ReadableSentencePuzzleUI.BeginFeedbackAndDismiss(
            this,
            solvedMessage,
            isError: false,
            GetAttemptFeedbackDuration(),
            OnSolvedFeedbackDismissed);

        if (!waitingForSolvedFeedbackClose)
        {
            ReadableSentencePuzzleUI.Dismiss(this);
            ShowSolvedFeedback();
            return false;
        }

        return true;
    }

    private void OnSolvedFeedbackDismissed()
    {
        waitingForSolvedFeedbackClose = false;

        if (!applySolvedStateAfterFeedbackClose)
        {
            invokeSolvedEventAfterFeedbackClose = false;
            return;
        }

        bool invokeLocalEvent = invokeSolvedEventAfterFeedbackClose;
        applySolvedStateAfterFeedbackClose = false;
        invokeSolvedEventAfterFeedbackClose = false;
        ApplySolvedState(true, invokeLocalEvent);
    }

    private void DeferSolvedStateApply(bool solved, bool invokeLocalEvent)
    {
        if (!waitingForSolvedFeedbackClose)
        {
            ApplySolvedState(solved, invokeLocalEvent);
            return;
        }

        applySolvedStateAfterFeedbackClose |= solved;
        invokeSolvedEventAfterFeedbackClose |= invokeLocalEvent;
    }

    private float GetAttemptFeedbackDuration()
    {
        if (attemptFeedbackDuration > 0f)
        {
            return attemptFeedbackDuration;
        }

        return Mathf.Max(0f, solvedFeedbackDelay);
    }

    private string ResolveFailureMessage(SolveAttemptResult result)
    {
        switch (result)
        {
            case SolveAttemptResult.IncorrectAnswer:
                return incorrectAnswerMessage;
            case SolveAttemptResult.SentenceUnavailable:
                return sentenceUnavailableMessage;
            case SolveAttemptResult.PuzzleAlreadySolved:
                return alreadySolvedMessage;
            case SolveAttemptResult.InvalidCharacter:
            case SolveAttemptResult.InvalidConfiguration:
            default:
                return invalidConfigurationMessage;
        }
    }

    private void ShowInteraction(bool show)
    {
        if (!show)
        {
            DestroyInteractionInstance();
            return;
        }

        if (interactionBoxInstance == null)
        {
            interactionBoxInstance = CreateInstance(interactionBox, boxesPanel);
            if (interactionBoxInstance == null)
            {
                interactionBoxInstance = CreateFallbackInteractionBox(boxesPanel);
            }

            if (interactionBoxInstance != null)
            {
                interactionCanvas = interactionBoxInstance.GetComponentInParent<Canvas>();
                ApplyInteractionText(interactionBoxInstance);
            }
        }

        if (interactionBoxInstance != null)
        {
            interactionBoxInstance.SetActive(true);
        }
    }

    private void ApplyInteractionText(GameObject instance)
    {
        if (instance == null)
        {
            return;
        }

        TMP_Text tmp = instance.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null)
        {
            tmp.text = interactionText;
            return;
        }

        Text legacyText = instance.GetComponentInChildren<Text>(true);
        if (legacyText != null)
        {
            legacyText.text = interactionText;
        }
    }

    private void DestroyInteractionInstance()
    {
        if (interactionBoxInstance == null)
        {
            return;
        }

        Destroy(interactionBoxInstance);
        interactionBoxInstance = null;
        interactionCanvas = null;
    }

    private void ResetUIState()
    {
        DestroyInteractionInstance();
        currentCharacter = null;
        interactionTarget = null;
        awaitingServerResponse = false;
        waitingForSolvedFeedbackClose = false;
        applySolvedStateAfterFeedbackClose = false;
        invokeSolvedEventAfterFeedbackClose = false;
    }

    private GameObject CreateInstance(GameObject source, Transform parent)
    {
        if (source == null)
        {
            return null;
        }

        return parent != null ? Instantiate(source, parent) : Instantiate(source);
    }

    private GameObject CreateFallbackInteractionBox(Transform parent)
    {
        GameObject instance = new GameObject("ReadableSentencePuzzleInteractionBox", typeof(RectTransform), typeof(Canvas), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(GraphicRaycaster));
        if (parent != null)
        {
            instance.transform.SetParent(parent, false);
        }

        RectTransform rect = instance.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(280f, 50f);
        rect.localScale = Vector3.one * 0.03f;

        Canvas canvas = instance.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 100;

        TextMeshProUGUI label = instance.GetComponent<TextMeshProUGUI>();
        label.text = interactionText;
        label.fontSize = 18f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;

        return instance;
    }

    private void SafeInvoke(UnityEvent unityEvent, string source)
    {
        if (unityEvent == null)
        {
            return;
        }

        try
        {
            unityEvent.Invoke();
        }
        catch (Exception ex)
        {
            Debug.LogException(new Exception($"[ReadableSentencePuzzle] event invocation failed on source='{source}' for '{name}'.", ex), this);
        }
    }

    private static SquadCharacterController GetController(GameObject character)
    {
        if (character == null)
        {
            return null;
        }

        SquadCharacterController controller = character.GetComponent<SquadCharacterController>();
        if (controller != null)
        {
            return controller;
        }

        controller = character.GetComponentInChildren<SquadCharacterController>(true);
        if (controller != null)
        {
            return controller;
        }

        return character.GetComponentInParent<SquadCharacterController>();
    }

    private static bool IsControlledCharacter(GameObject character)
    {
        if (character == null)
        {
            return false;
        }

        GameObject controlled = LocalPlayerUtils.GetControlledCharacter();
        if (controlled == null)
        {
            return false;
        }

        Transform controlledTransform = controlled.transform;
        Transform characterTransform = character.transform;
        return controlledTransform == characterTransform
            || controlledTransform.IsChildOf(characterTransform)
            || characterTransform.IsChildOf(controlledTransform);
    }

    private static bool IsNetworked()
    {
        return NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
    }
}
