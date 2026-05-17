using System;
using System.Globalization;
using System.Text;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Role: interaction qui demande au joueur de recopier une phrase d'un item lisible.
// Usage: attache a un objet de scene detecte par le systeme d'interaction personnage.
// Responsibilities: afficher le prompt, valider la reponse, synchroniser l'etat resolu en solo/netcode.
// Dependencies: ReadableSentenceReference, ReadableSentencePuzzleUI, LocalInputRouter, WorldInteractionService, Netcode.
// Precautions: ne pas modifier les noms des champs serialises; ils sont probablement relies a des prefabs/scenes.
/// <summary>
/// Puzzle de phrase lie a un document lisible genere.
/// </summary>
[DisallowMultipleComponent]
public class ReadableSentencePuzzle : MonoBehaviour, ICharacterDetectedInteractable
{
    /// <summary>
    /// Resultat normalise d'une tentative de resolution.
    /// </summary>
    public enum SolveAttemptResult
    {
        /// <summary>La reponse est correcte.</summary>
        Success = 0,
        /// <summary>La phrase envoyee ne correspond pas a la phrase attendue.</summary>
        IncorrectAnswer = 1,
        /// <summary>La phrase cible n'a pas pu etre generee ou retrouvee.</summary>
        SentenceUnavailable = 2,
        /// <summary>Le puzzle est deja resolu et ne peut etre rejoue.</summary>
        PuzzleAlreadySolved = 3,
        /// <summary>Le personnage n'est pas autorise ou pas a portee.</summary>
        InvalidCharacter = 4,
        /// <summary>La configuration de scene est incomplete.</summary>
        InvalidConfiguration = 5
    }

    [Header("Reference")]
    /// <summary>
    /// Phrase d'item lisible que le joueur doit recopier.
    /// </summary>
    [SerializeField] private ReadableSentenceReference requiredSentence;
    /// <summary>
    /// Titre affiche dans le panneau de puzzle.
    /// </summary>
    [SerializeField] private string panelTitle = "Enigme";
    /// <summary>
    /// Texte de prompt optionnel. Vide, il est genere depuis la reference de phrase.
    /// </summary>
    [SerializeField, TextArea(2, 4)] private string promptOverride;
    /// <summary>
    /// Si vrai, le puzzle reste resolu apres une bonne reponse.
    /// </summary>
    [SerializeField] private bool playOnce = true;

    [Header("Validation")]
    [SerializeField] private bool ignoreCase = true;
    [SerializeField] private bool ignoreAccents = true;
    [SerializeField] private bool ignorePunctuation = true;
    [SerializeField] private bool collapseWhitespace = true;
    [SerializeField] private bool trimWhitespace = true;

    [Header("Interaction")]
    /// <summary>
    /// Collider utilise pour mesurer la distance d'interaction.
    /// </summary>
    [SerializeField] private Collider interactionCollider;
    /// <summary>
    /// Distance maximale entre le personnage et le point d'interaction.
    /// </summary>
    [SerializeField] private float interactionMaxDistance = 2.25f;
    /// <summary>
    /// Priorite utilisee si plusieurs interactables sont detectes.
    /// </summary>
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

    /// <summary>
    /// Etat resolu local du puzzle.
    /// </summary>
    public bool IsSolved => isSolved;
    /// <summary>
    /// Indique si une seule resolution est autorisee.
    /// </summary>
    public bool PlayOnce => playOnce;
    /// <summary>
    /// Reference vers la phrase attendue.
    /// </summary>
    public ReadableSentenceReference RequiredSentence => requiredSentence;

    private void Reset()
    {
        // Unity appelle Reset quand le composant est ajoute ou reinitialise dans l'editeur.
        interactionCollider = CharacterInteractionDetection.ResolveInteractionCollider(this, interactionCollider);
    }

    private void OnValidate()
    {
        // Unity appelle OnValidate dans l'editeur; on garde une distance toujours positive.
        interactionMaxDistance = Mathf.Max(0.1f, interactionMaxDistance);
    }

    private void Awake()
    {
        // Awake cache le collider et l'identifiant stable avant les interactions.
        resolvedInteractionCollider = CharacterInteractionDetection.ResolveInteractionCollider(this, interactionCollider);
        if (interactionCollider == null)
        {
            interactionCollider = resolvedInteractionCollider;
        }

        netcodeId = NetcodeSceneIdUtility.GetStableId(transform);
    }

    private void OnEnable()
    {
        // OnEnable raccorde l'input et enregistre ce puzzle pour les appels Netcode par id stable.
        LocalInputRouter.EnsureInitialized();
        LocalInputRouter.Interact += OnInteractPerformed;
        NetcodeTriggerRegistry.Register(this, netcodeId);
    }

    private void OnDisable()
    {
        // OnDisable libere l'input, le registre Netcode et les UI eventuellement ouvertes.
        LocalInputRouter.Interact -= OnInteractPerformed;
        NetcodeTriggerRegistry.Unregister(this, netcodeId);
        ReadableSentencePuzzleUI.Dismiss(this);
        ResetUIState();
    }

    private void LateUpdate()
    {
        // LateUpdate positionne la bulle d'interaction apres les mouvements de camera/personnage.
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

    /// <summary>
    /// Indique au systeme de detection si ce puzzle peut etre propose au personnage.
    /// </summary>
    public bool CanBeDetectedBy(SquadCharacterController controller)
    {
        return controller != null
            && isActiveAndEnabled
            && requiredSentence.IsConfigured
            && (!playOnce || !isSolved);
    }

    /// <summary>
    /// Retourne le collider utilise pour calculer la detection et la distance.
    /// </summary>
    public Collider GetInteractionDetectionCollider()
    {
        if (resolvedInteractionCollider == null)
        {
            resolvedInteractionCollider = CharacterInteractionDetection.ResolveInteractionCollider(this, interactionCollider);
        }

        return resolvedInteractionCollider;
    }

    /// <summary>
    /// Retourne le transform qui sert d'ancre d'interaction.
    /// </summary>
    public Transform GetInteractionAnchor()
    {
        return transform;
    }

    /// <summary>
    /// Retourne la distance maximale autorisee pour interagir.
    /// </summary>
    public float GetInteractionMaxDistance(SquadCharacterController controller)
    {
        return Mathf.Max(0.1f, interactionMaxDistance);
    }

    /// <summary>
    /// Retourne la priorite d'interaction utilisee par la detection.
    /// </summary>
    public int GetInteractionPriority(SquadCharacterController controller)
    {
        return interactionPriority;
    }

    /// <summary>
    /// Recoit le personnage actuellement detecte et affiche/masque l'aide d'interaction.
    /// </summary>
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

    /// <summary>
    /// Tente de resoudre la phrase attendue depuis le document lisible reference.
    /// </summary>
    public bool TryResolveExpectedSentence(out string sentence)
    {
        sentence = string.Empty;
        return requiredSentence.TryGetGeneratedSentence(out sentence);
    }

    /// <summary>
    /// Construit le texte de consigne affiche dans le panneau du puzzle.
    /// </summary>
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

    /// <summary>
    /// Restaure l'etat resolu sans redeclencher l'evenement local.
    /// </summary>
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

    /// <summary>
    /// Valide une tentative de reponse cote serveur ou en mode solo.
    /// </summary>
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

    /// <summary>
    /// Applique localement le resultat d'une tentative de resolution.
    /// </summary>
    public void HandleSolveAttemptResult(SolveAttemptResult result)
    {
        awaitingServerResponse = false;

        if (result == SolveAttemptResult.Success)
        {
            PlayResultSfx(successSfx);
            bool delayPanelClose = TryShowSolvedFeedbackBeforeDismiss();
            // En solo, l'etat peut etre applique immediatement ou apres le feedback UI.
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
                    // En reseau, le serveur propage l'etat resolu aux clients via le service d'interaction.
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

    /// <summary>
    /// Recoit la replication Netcode indiquant que le puzzle est resolu.
    /// </summary>
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
        // La normalisation doit rester identique cote client et serveur pour eviter les faux refus.
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

        Text fallbackText = instance.GetComponentInChildren<Text>(true);
        if (fallbackText != null)
        {
            fallbackText.text = interactionText;
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
