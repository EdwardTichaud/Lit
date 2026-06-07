using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

[RequireComponent(typeof(Collider))]
[DisallowMultipleComponent]
// Zone d'interaction pour teleporter la squad vers un point de spawn unique.
public class LabyrinthStartTrigger : MonoBehaviour
{
    [Header("UI - Interaction")]
    [Tooltip("Prefab/objet UI d'interaction.")]
    public GameObject interactionBox;
    [Tooltip("Offset en world pour la box d'interaction.")]
    public Vector3 interactionOffset = new Vector3(0f, 2f, 0f);

    [Header("Interaction Validation")]
    [Tooltip("Distance horizontale maximale depuis le centre du trigger pour accepter Interact.")]
    public float interactionMaxDistance = 2.25f;

    [Header("UI - Confirmation")]
    [Tooltip("Panel parent de la confirmation.")]
    public GameObject confirmationPanel;
    [Tooltip("Prefab de la confirmation (Oui/Non).")]
    public GameObject confirmationBox;
    [Tooltip("Texte affiche dans la confirmation.")]
    public string confirmationMessage = "Partir en exploration?";
    [Tooltip("Force un sorting order eleve.")]
    public bool forceConfirmOnTop = true;
    [Tooltip("Sorting order pour le panel de confirmation.")]
    public int confirmSortingOrder = 100;
    [Tooltip("Index de l'option Oui dans la confirmationBox.")]
    public int confirmationYesIndex = 0;
    [Tooltip("Index de l'option Non dans la confirmationBox.")]
    public int confirmationNoIndex = 1;

    [Header("UI - Confirmation Fade")]
    [Tooltip("Duree du fade de la confirmation.")]
    public float confirmationFadeDuration = 0.5f;
    [Tooltip("Ajoute un CanvasGroup si manquant.")]
    public bool confirmationAddCanvasGroupIfMissing = true;
    [Tooltip("Desactive les raycasts quand cache.")]
    public bool confirmationDisableRaycastsWhenHidden = true;
    [Tooltip("Met l'alpha a 0 au demarrage.")]
    public bool confirmationSetAlphaToZeroOnStart = true;

    [Header("Destination")]
    [Tooltip("Racine du labyrinthe/chateau (optionnel).")]
    public GameObject labyrinthRoot;
    [Tooltip("Point de spawn force (optionnel).")]
    public Transform spawnPointOverride;
    [Tooltip("Tag utilise pour trouver le point de spawn.")]
    public string spawnPointTag = "SpawnPoint";
    [Tooltip("Nom utilise pour trouver le point de spawn si le tag est absent.")]
    public string spawnPointName = "Labyrinth_SpawnPoint";
    [Tooltip("Offset applique au point de spawn.")]
    public Vector3 spawnPointOffset = Vector3.zero;
    [Tooltip("Rayon de dispersion de la squad.")]
    public float spawnSpreadRadius = 1.5f;

    [Header("VFX")]
    [Tooltip("Prefab VFX instancie au spawn.")]
    public GameObject teleportVfxPrefab;
    [Tooltip("Offset applique au VFX.")]
    public Vector3 teleportVfxOffset = Vector3.zero;
    [Tooltip("Parent des VFX.")]
    public Transform teleportVfxParent;
    [Tooltip("Duree de vie du VFX.")]
    public float teleportVfxLifetime = 2.5f;

    [Header("UI - Parent")]
    [Tooltip("Parent des boxes UI.")]
    public Transform boxesPanel;
    [Tooltip("Parent des confirmations UI.")]
    public Transform confirmationBoxes;

    [Header("Camera")]
    [Tooltip("Camera UI/world pour positionner l'interaction box.")]
    public Camera targetCamera;

    private readonly List<GameObject> charactersInRange = new List<GameObject>();
    private readonly Dictionary<GameObject, int> characterColliderCounts = new Dictionary<GameObject, int>();
    private GameObject currentCharacter;
    private Transform interactionTarget;

    private GameObject interactionBoxInstance;
    private GameObject confirmationBoxInstance;
    private Canvas interactionCanvas;
    private bool isTriggerZone;
    private Collider triggerCollider;
    private bool confirmVisible;
    private CursorController confirmationCursor;
    private CanvasGroup confirmationCanvasGroup;
    private Coroutine confirmationFadeRoutine;
    private bool confirmationInputLocked;
    private TMP_Text confirmationMessageText;
    private bool awaitingServerResponse;
    private uint netcodeId;
    private bool ignoreAsNestedTrigger;

    private enum ConfirmationChoice
    {
        Unknown,
        Yes,
        No
    }

    private void Awake()
    {
        ignoreAsNestedTrigger = HasParentLabyrinthStartTrigger();
        triggerCollider = GetComponent<Collider>();
        isTriggerZone = triggerCollider != null && triggerCollider.isTrigger;
        if (triggerCollider != null && !triggerCollider.isTrigger)
        {
            Debug.LogWarning("LabyrinthStartTrigger: le collider n'est pas en mode Trigger.");
        }

        netcodeId = NetcodeSceneIdUtility.GetStableId(transform);
        InitializeConfirmationFade();
    }

    private void OnEnable()
    {
        if (ignoreAsNestedTrigger)
        {
            return;
        }

        LocalInputRouter.EnsureInitialized();
        LocalInputRouter.Interact += OnInteractPerformed;
        NetcodeTriggerRegistry.Register(this, netcodeId);
    }

    private void OnDisable()
    {
        LocalInputRouter.Interact -= OnInteractPerformed;
        NetcodeTriggerRegistry.Unregister(this, netcodeId);
        ConfirmationManager.Dismiss(this);

        ResetUIState();
    }

    private void Update()
    {
        if (ignoreAsNestedTrigger)
        {
            return;
        }

        RefreshCurrentCharacter(true);
    }

    private void LateUpdate()
    {
        // Aligne la box d'interaction sur la cible.
        if (interactionBoxInstance == null || !interactionBoxInstance.activeSelf)
        {
            return;
        }

        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null)
        {
            return;
        }

        if (interactionTarget == null)
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
            }
            else
            {
                RectTransform canvasRect = canvas.GetComponent<RectTransform>();
                Camera uiCamera = canvas.worldCamera != null ? canvas.worldCamera : cam;
                if (canvasRect != null
                    && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        canvasRect,
                        screenPos,
                        uiCamera,
                        out Vector2 localPoint))
                {
                    rect.localPosition = localPoint;
                }
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

    private void OnTriggerEnter(Collider other)
    {
        if (ignoreAsNestedTrigger)
        {
            return;
        }

        if (other == null || other.isTrigger)
        {
            return;
        }

        if (!isTriggerZone)
        {
            return;
        }

        GameObject character = GetSquadCharacter(other);
        if (character == null)
        {
            return;
        }

        bool firstCollider = RegisterCharacterCollider(character);
        if (firstCollider && !charactersInRange.Contains(character))
        {
            charactersInRange.Add(character);
        }
        RefreshCurrentCharacter(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (ignoreAsNestedTrigger)
        {
            return;
        }

        if (other == null || other.isTrigger)
        {
            return;
        }

        if (!isTriggerZone)
        {
            return;
        }

        GameObject character = GetSquadCharacter(other);
        if (character == null)
        {
            return;
        }

        if (!UnregisterCharacterCollider(character))
        {
            return;
        }

        charactersInRange.Remove(character);
        if (character == currentCharacter)
        {
            currentCharacter = null;
            interactionTarget = null;
        }

        RefreshCurrentCharacter(true);
        if (currentCharacter == null && charactersInRange.Count == 0)
        {
            ResetUIState();
        }
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        if (ignoreAsNestedTrigger || LocalInputRouter.IsInteractConsumed)
        {
            return;
        }

        HandleInteract(true);
    }

    private void HandleInteract(bool consumeRouterInput)
    {
        if (ignoreAsNestedTrigger || (consumeRouterInput && LocalInputRouter.IsInteractConsumed))
        {
            return;
        }

        if (InputFocusStack.HasAnyFocus() && !InputFocusStack.HasFocus(this))
        {
            return;
        }

        RefreshCurrentCharacter(true);
        if (currentCharacter == null)
        {
            return;
        }

        if (consumeRouterInput && !LocalInputRouter.TryConsumeInteract())
        {
            return;
        }

        RequestCentralConfirmation();
    }

    private void OnInteract()
    {
        HandleInteract(false);
    }

    private void OnSouthButton()
    {
        OnInteract();
    }

    private void OnEastButton()
    {
        if (!confirmVisible)
        {
            return;
        }

        ConfirmationManager.Dismiss(this, true);
    }

    private void RequestCentralConfirmation()
    {
        if (confirmVisible || awaitingServerResponse)
        {
            return;
        }

        bool shown = ConfirmationManager.TryShow(
            new ConfirmationRequest(this, confirmationMessage, ConfirmStartLabyrinth, CancelStartLabyrinth)
            {
                Title = "Confirmation",
                ConfirmLabel = "Oui",
                CancelLabel = "Non",
                DebugContext = "LabyrinthStartTrigger.Start"
            });

        if (!shown)
        {
            Debug.LogWarning($"[Confirmation] LabyrinthStartTrigger failed to open confirmation for '{name}'.", this);
            ShowInteraction(true);
            return;
        }

        confirmVisible = true;
        PlayUiActionAudio(ActionAudioCue.UiOpen);
        ShowInteraction(false);
    }

    private void ConfirmStartLabyrinth()
    {
        confirmVisible = false;
        PlayUiActionAudio(ActionAudioCue.UiConfirm);
        RefreshCurrentCharacter(true);
        if (currentCharacter == null)
        {
            return;
        }

        if (IsNetworked())
        {
            if (awaitingServerResponse)
            {
                return;
            }

            awaitingServerResponse = true;
            WorldInteractionService service = WorldInteractionService.Instance;
            if (service != null)
            {
                service.RequestLabyrinthStartServerRpc(netcodeId);
            }
            else
            {
                awaitingServerResponse = false;
                ShowInteraction(true);
            }
            return;
        }

        StartLabyrinth();
    }

    private void CancelStartLabyrinth()
    {
        confirmVisible = false;
        PlayUiActionAudio(ActionAudioCue.UiCancel);
        RefreshCurrentCharacter(true);
    }

    private void StartLabyrinth()
    {
        if (labyrinthRoot != null)
        {
            labyrinthRoot.SetActive(true);
        }

        TeleportSquadToSpawn();
        PlayActionAudio(ActionAudioCue.LabyrinthStart, transform.position);
        ResetUIState();
    }

    private void RefreshCurrentCharacter(bool allowShow)
    {
        PruneInvalidTrackedCharacters();

        GameObject controlled = GetControlledCharacter();
        if (controlled != null && charactersInRange.Contains(controlled) && IsCharacterWithinInteractionRange(controlled))
        {
            if (currentCharacter != controlled)
            {
                currentCharacter = controlled;
                interactionTarget = controlled.transform;
            }

            ShowInteraction(allowShow);
            return;
        }

        currentCharacter = null;
        interactionTarget = null;
        ShowInteraction(false);
    }

    private void PruneInvalidTrackedCharacters()
    {
        for (int i = charactersInRange.Count - 1; i >= 0; i--)
        {
            GameObject character = charactersInRange[i];
            if (character != null && IsCharacterWithinInteractionRange(character))
            {
                continue;
            }

            charactersInRange.RemoveAt(i);
            if (character != null)
            {
                characterColliderCounts.Remove(character);
            }

            if (character == currentCharacter)
            {
                currentCharacter = null;
                interactionTarget = null;
            }
        }
    }

    private bool IsCharacterWithinInteractionRange(GameObject character)
    {
        if (character == null)
        {
            return false;
        }

        if (triggerCollider == null)
        {
            return true;
        }

        Vector3 characterPosition = GetCharacterInteractionPosition(character);
        Vector3 triggerCenter = triggerCollider.bounds.center;
        Vector2 horizontalDelta = new Vector2(
            characterPosition.x - triggerCenter.x,
            characterPosition.z - triggerCenter.z);
        float maxDistance = Mathf.Max(0.05f, interactionMaxDistance);
        return horizontalDelta.sqrMagnitude <= maxDistance * maxDistance;
    }

    private static Vector3 GetCharacterInteractionPosition(GameObject character)
    {
        if (character == null)
        {
            return Vector3.zero;
        }

        SquadCharacterController controller = character.GetComponent<SquadCharacterController>();
        return controller != null ? controller.GetInteractionOriginWorldPosition() : character.transform.position;
    }

    private bool HasParentLabyrinthStartTrigger()
    {
        Transform parent = transform.parent;
        while (parent != null)
        {
            if (parent.GetComponent<LabyrinthStartTrigger>() != null)
            {
                return true;
            }

            parent = parent.parent;
        }

        return false;
    }

    private static GameObject GetControlledCharacter()
    {
        return LocalPlayerUtils.GetControlledCharacter();
    }

    private void ShowInteraction(bool show)
    {
        if (confirmVisible && show)
        {
            return;
        }

        if (!show)
        {
            DestroyInteractionInstance();
            return;
        }

        if (interactionBoxInstance == null)
        {
            interactionBoxInstance = CreateInstance(interactionBox, boxesPanel);
            if (interactionBoxInstance != null)
            {
                interactionCanvas = interactionBoxInstance.GetComponentInParent<Canvas>();
            }
        }

        if (interactionBoxInstance != null)
        {
            interactionBoxInstance.SetActive(true);
        }
    }

    private void ShowConfirm(bool show)
    {
        if (!show)
        {
            FadeConfirmationPanelTo(0f, confirmationFadeDuration);
            SetConfirmationInputLock(false);
            DestroyConfirmInstance();
            confirmVisible = false;
            RefreshCurrentCharacter(true);
            return;
        }

        DestroyInteractionInstance();

        if (confirmationPanel == null)
        {
            Debug.LogWarning("LabyrinthStartTrigger: confirmationPanel non assigne.");
            SetConfirmationInputLock(false);
            confirmVisible = false;
            return;
        }

        if (!confirmationPanel.activeSelf)
        {
            confirmationPanel.SetActive(true);
        }

        if (confirmationBoxInstance == null)
        {
            Transform parent = confirmationBoxes != null ? confirmationBoxes : confirmationPanel.transform;
            confirmationBoxInstance = CreateInstance(confirmationBox, parent);
        }

        if (confirmationBoxInstance == null)
        {
            Debug.LogWarning("LabyrinthStartTrigger: confirmationBox non assignee.");
            SetConfirmationInputLock(false);
            confirmVisible = false;
            return;
        }

        confirmationBoxInstance.SetActive(true);
        ApplyConfirmationMessage();
        confirmationCursor = confirmationBoxInstance.GetComponentInChildren<CursorController>(true);
        if (confirmationCursor != null)
        {
            confirmationCursor.Refresh();
        }

        SetConfirmationInputLock(true);
        FadeConfirmationPanelTo(1f, confirmationFadeDuration);
        BringConfirmToFront();
        confirmVisible = true;
    }

    private void BringConfirmToFront()
    {
        if (confirmationBoxInstance == null && confirmationPanel == null)
        {
            return;
        }

        if (confirmationBoxInstance != null && confirmationBoxInstance.transform.parent != null)
        {
            confirmationBoxInstance.transform.SetAsLastSibling();
        }

        if (!forceConfirmOnTop)
        {
            return;
        }

        Canvas canvas = null;
        if (confirmationPanel != null)
        {
            canvas = confirmationPanel.GetComponent<Canvas>();
        }

        if (canvas == null && confirmationBoxInstance != null)
        {
            canvas = confirmationBoxInstance.GetComponentInParent<Canvas>();
        }

        if (canvas != null)
        {
            canvas.overrideSorting = true;
            canvas.sortingOrder = confirmSortingOrder;
        }
    }

    private void DestroyInteractionInstance()
    {
        if (interactionBoxInstance != null)
        {
            Destroy(interactionBoxInstance);
            interactionBoxInstance = null;
            interactionCanvas = null;
        }
    }

    private void DestroyConfirmInstance()
    {
        if (confirmationBoxInstance != null)
        {
            Destroy(confirmationBoxInstance);
            confirmationBoxInstance = null;
            confirmationCursor = null;
            confirmationMessageText = null;
        }
    }

    private void ResetUIState()
    {
        ConfirmationManager.Dismiss(this);
        DestroyInteractionInstance();
        DestroyConfirmInstance();
        FadeConfirmationPanelTo(0f, 0f);
        SetConfirmationInputLock(false);
        confirmVisible = false;
        charactersInRange.Clear();
        characterColliderCounts.Clear();
        currentCharacter = null;
        interactionTarget = null;
        awaitingServerResponse = false;
    }

    public void ServerStartLabyrinth()
    {
        ConfirmationManager.Dismiss(this);
        awaitingServerResponse = false;
        StartLabyrinth();
    }

    public void ClientHandleLabyrinthStarted()
    {
        ConfirmationManager.Dismiss(this);
        awaitingServerResponse = false;
        if (labyrinthRoot != null)
        {
            labyrinthRoot.SetActive(true);
        }

        ResetUIState();
    }

    public bool IsServerCharacterAllowed(GameObject character)
    {
        if (character == null)
        {
            return false;
        }

        if (triggerCollider == null)
        {
            return true;
        }

        float distance = triggerCollider.bounds.SqrDistance(character.transform.position);
        return distance <= 0.25f;
    }

    private static bool IsNetworked()
    {
        return NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
    }

    private GameObject CreateInstance(GameObject source, Transform parent)
    {
        if (source == null)
        {
            return null;
        }

        if (parent != null)
        {
            return Instantiate(source, parent);
        }

        return Instantiate(source);
    }

    private void InitializeConfirmationFade()
    {
        confirmationCanvasGroup = GetConfirmationCanvasGroup();
        if (confirmationCanvasGroup != null && confirmationSetAlphaToZeroOnStart)
        {
            confirmationCanvasGroup.alpha = 0f;
            if (confirmationDisableRaycastsWhenHidden)
            {
                confirmationCanvasGroup.interactable = false;
                confirmationCanvasGroup.blocksRaycasts = false;
            }
        }
    }

    private CanvasGroup GetConfirmationCanvasGroup()
    {
        if (confirmationPanel == null)
        {
            return null;
        }

        if (confirmationCanvasGroup != null)
        {
            return confirmationCanvasGroup;
        }

        confirmationCanvasGroup = confirmationPanel.GetComponent<CanvasGroup>();
        if (confirmationCanvasGroup == null && confirmationAddCanvasGroupIfMissing)
        {
            confirmationCanvasGroup = confirmationPanel.AddComponent<CanvasGroup>();
        }

        return confirmationCanvasGroup;
    }

    private void FadeConfirmationPanelTo(float targetAlpha, float duration)
    {
        CanvasGroup canvasGroup = GetConfirmationCanvasGroup();
        if (canvasGroup == null)
        {
            return;
        }

        if (!CanRunCoroutines())
        {
            canvasGroup.alpha = targetAlpha;
            if (confirmationDisableRaycastsWhenHidden)
            {
                bool visible = targetAlpha > 0.001f;
                canvasGroup.interactable = visible;
                canvasGroup.blocksRaycasts = visible;
            }
            return;
        }

        if (confirmationFadeRoutine != null)
        {
            StopCoroutine(confirmationFadeRoutine);
        }

        float startAlpha = canvasGroup.alpha;
        if (duration <= 0f)
        {
            canvasGroup.alpha = targetAlpha;
            if (confirmationDisableRaycastsWhenHidden)
            {
                bool visible = targetAlpha > 0.001f;
                canvasGroup.interactable = visible;
                canvasGroup.blocksRaycasts = visible;
            }
            return;
        }

        confirmationFadeRoutine = StartCoroutine(FadeConfirmationRoutine(canvasGroup, startAlpha, targetAlpha, duration));
    }

    private IEnumerator FadeConfirmationRoutine(CanvasGroup canvasGroup, float startAlpha, float targetAlpha, float duration)
    {
        if (canvasGroup == null)
        {
            yield break;
        }

        float time = 0f;
        if (confirmationDisableRaycastsWhenHidden)
        {
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / duration);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
        if (confirmationDisableRaycastsWhenHidden)
        {
            bool visible = targetAlpha > 0.001f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }
    }

    private bool CanRunCoroutines()
    {
        return isActiveAndEnabled && gameObject.activeInHierarchy;
    }

    private void SetConfirmationInputLock(bool locked)
    {
        if (locked)
        {
            if (confirmationInputLocked)
            {
                return;
            }

            confirmationInputLocked = true;
            InputFocusStack.Push(this);
            if (SquadManager.Instance != null)
            {
                SquadManager.Instance.SetInputLocked(true);
            }
            return;
        }

        if (!confirmationInputLocked)
        {
            InputFocusStack.Pop(this);
            return;
        }

        confirmationInputLocked = false;
        InputFocusStack.Pop(this);
        if (SquadManager.Instance != null)
        {
            SquadManager.Instance.SetInputLocked(false);
        }
    }

    private ConfirmationChoice GetConfirmationChoice()
    {
        if (confirmationBoxInstance == null)
        {
            return ConfirmationChoice.Unknown;
        }

        if (confirmationCursor == null)
        {
            confirmationCursor = confirmationBoxInstance.GetComponentInChildren<CursorController>(true);
        }

        if (confirmationCursor == null)
        {
            return ConfirmationChoice.Unknown;
        }

        int index = confirmationCursor.CurrentIndex;
        if (index == confirmationYesIndex)
        {
            return ConfirmationChoice.Yes;
        }

        if (index == confirmationNoIndex)
        {
            return ConfirmationChoice.No;
        }

        return ConfirmationChoice.Unknown;
    }

    private void ApplyConfirmationMessage()
    {
        if (confirmationBoxInstance == null || string.IsNullOrWhiteSpace(confirmationMessage))
        {
            return;
        }

        TMP_Text textTarget = FindConfirmationMessageText();
        if (textTarget != null)
        {
            textTarget.text = confirmationMessage;
        }
    }

    private TMP_Text FindConfirmationMessageText()
    {
        if (confirmationMessageText != null)
        {
            return confirmationMessageText;
        }

        if (confirmationBoxInstance == null)
        {
            return null;
        }

        TMP_Text[] texts = confirmationBoxInstance.GetComponentsInChildren<TMP_Text>(true);
        TMP_Text fallback = null;
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null)
            {
                continue;
            }

            string value = text.text != null ? text.text.Trim() : string.Empty;
            string objectName = text.gameObject.name != null ? text.gameObject.name.Trim() : string.Empty;
            if (IsConfirmationChoiceLabel(value) || IsConfirmationChoiceLabel(objectName))
            {
                continue;
            }

            if (fallback == null)
            {
                fallback = text;
            }

            if (!string.IsNullOrWhiteSpace(value))
            {
                confirmationMessageText = text;
                return confirmationMessageText;
            }
        }

        confirmationMessageText = fallback;
        return confirmationMessageText;
    }

    private static bool IsConfirmationChoiceLabel(string value)
    {
        return string.Equals(value, "Oui", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "Non", System.StringComparison.OrdinalIgnoreCase);
    }

    private void TeleportSquadToSpawn()
    {
        if (SquadManager.Instance == null)
        {
            return;
        }

        List<GameObject> squad = CollectSquadInstances();
        if (squad.Count == 0)
        {
            return;
        }

        Transform spawnPoint = ResolveSpawnPoint();
        Vector3 basePosition;
        Quaternion baseRotation;
        if (spawnPoint != null)
        {
            basePosition = spawnPoint.position;
            baseRotation = spawnPoint.rotation;
        }
        else if (labyrinthRoot != null)
        {
            basePosition = labyrinthRoot.transform.position;
            baseRotation = labyrinthRoot.transform.rotation;
        }
        else
        {
            basePosition = transform.position;
            baseRotation = transform.rotation;
        }

        basePosition += baseRotation * spawnPointOffset;

        for (int i = 0; i < squad.Count; i++)
        {
            GameObject character = squad[i];
            if (character == null)
            {
                continue;
            }

            Vector3 offset = GetFormationOffset(i);
            Vector3 worldOffset = baseRotation * offset;
            Vector3 finalPosition = basePosition + worldOffset;
            TeleportCharacter(character, finalPosition, baseRotation);
            SpawnTeleportVfx(finalPosition, baseRotation);
        }

        Physics.SyncTransforms();
    }

    private void PlayActionAudio(ActionAudioCue cue, Vector3 position)
    {
        if (cue == ActionAudioCue.None)
        {
            return;
        }

        AudioManager manager = AudioManager.EnsureInstance();
        if (manager != null)
        {
            manager.PlayActionCue(cue, position);
        }
    }

    private void PlayUiActionAudio(ActionAudioCue cue)
    {
        if (cue == ActionAudioCue.None)
        {
            return;
        }

        AudioManager manager = AudioManager.EnsureInstance();
        if (manager != null)
        {
            manager.PlayUiActionCue(cue);
        }
    }

    private Transform ResolveSpawnPoint()
    {
        if (spawnPointOverride != null)
        {
            return spawnPointOverride;
        }

        Transform found = null;
        if (labyrinthRoot != null)
        {
            found = FindSpawnPointInRoot(labyrinthRoot);
        }

        if (found != null)
        {
            return found;
        }

        if (!string.IsNullOrWhiteSpace(spawnPointTag))
        {
            try
            {
                GameObject tagged = GameObject.FindGameObjectWithTag(spawnPointTag);
                if (tagged != null)
                {
                    return tagged.transform;
                }
            }
            catch (UnityException)
            {
                // Tag missing.
            }
        }

        if (!string.IsNullOrWhiteSpace(spawnPointName))
        {
            GameObject named = GameObject.Find(spawnPointName);
            if (named != null)
            {
                return named.transform;
            }
        }

        return null;
    }

    private Transform FindSpawnPointInRoot(GameObject root)
    {
        if (root == null)
        {
            return null;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        if (children == null || children.Length == 0)
        {
            return null;
        }

        bool tagValid = true;
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == null)
            {
                continue;
            }

            if (tagValid && !string.IsNullOrWhiteSpace(spawnPointTag))
            {
                try
                {
                    if (child.CompareTag(spawnPointTag))
                    {
                        return child;
                    }
                }
                catch (UnityException)
                {
                    tagValid = false;
                }
            }

            if (!string.IsNullOrWhiteSpace(spawnPointName) && child.name == spawnPointName)
            {
                return child;
            }
        }

        return null;
    }

    private List<GameObject> CollectSquadInstances()
    {
        List<GameObject> results = new List<GameObject>();
        SquadManager manager = SquadManager.Instance;
        if (manager == null)
        {
            return results;
        }

        if (manager.squadCharacters != null)
        {
            for (int i = 0; i < manager.squadCharacters.Count; i++)
            {
                GameObject instance = manager.squadCharacters[i];
                if (instance != null && !results.Contains(instance))
                {
                    results.Add(instance);
                }
            }
        }

        if (results.Count == 0 && manager.currentSquad != null)
        {
            for (int i = 0; i < manager.currentSquad.Count; i++)
            {
                CharacterData data = manager.currentSquad[i];
                if (data == null)
                {
                    continue;
                }

                GameObject instance = manager.GetCharacterInstance(data);
                if (instance != null && !results.Contains(instance))
                {
                    results.Add(instance);
                }
            }
        }

        if (results.Count == 0)
        {
            try
            {
                GameObject[] tagged = GameObject.FindGameObjectsWithTag("Player");
                for (int i = 0; i < tagged.Length; i++)
                {
                    GameObject instance = tagged[i];
                    if (instance != null && instance.GetComponent<SquadCharacterController>() != null && !results.Contains(instance))
                    {
                        results.Add(instance);
                    }
                }
            }
            catch (UnityException)
            {
                // Tag missing, ignore.
            }
        }

        return results;
    }

    private Vector3 GetFormationOffset(int index)
    {
        if (index <= 0)
        {
            return Vector3.zero;
        }

        float angle = (index - 1) * 60f * Mathf.Deg2Rad;
        float radius = Mathf.Max(0f, spawnSpreadRadius);
        return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
    }

    private void TeleportCharacter(GameObject character, Vector3 position, Quaternion rotation)
    {
        if (character == null)
        {
            return;
        }

        SquadCharacterController squadController = character.GetComponent<SquadCharacterController>();
        if (squadController == null ||
            !squadController.TrySetUccExternalPositionAndRotation(position, rotation, stopActiveAbilities: true))
        {
            Debug.LogWarning($"[LabyrinthStartTrigger] teleport_skipped character='{character.name}' reason='ucc_locomotion_unavailable'", this);
            return;
        }

        squadController.Stop();
    }

    private void SpawnTeleportVfx(Vector3 position, Quaternion rotation)
    {
        if (teleportVfxPrefab == null)
        {
            return;
        }

        Transform parent = teleportVfxParent != null ? teleportVfxParent : null;
        GameObject instance = Instantiate(teleportVfxPrefab, position + teleportVfxOffset, rotation, parent);
        if (teleportVfxLifetime > 0f)
        {
            Destroy(instance, teleportVfxLifetime);
        }
    }

    private GameObject GetSquadCharacter(Collider other)
    {
        if (other == null)
        {
            return null;
        }

        if (SquadManager.Instance == null || SquadManager.Instance.squadCharacters == null)
        {
            return null;
        }

        Transform current = other.transform;
        bool hasPlayerTag = false;
        GameObject squadRoot = null;
        while (current != null)
        {
            if (current.CompareTag("Player"))
            {
                hasPlayerTag = true;
            }

            if (SquadManager.Instance.squadCharacters.Contains(current.gameObject))
            {
                squadRoot = current.gameObject;
            }

            current = current.parent;
        }

        if (squadRoot == null)
        {
            Transform root = other.transform.root;
            if (root != null)
            {
                if (root.CompareTag("Player"))
                {
                    hasPlayerTag = true;
                }

                for (int i = 0; i < SquadManager.Instance.squadCharacters.Count; i++)
                {
                    GameObject candidate = SquadManager.Instance.squadCharacters[i];
                    if (candidate != null && candidate.transform.IsChildOf(root))
                    {
                        squadRoot = candidate;
                        break;
                    }
                }
            }
        }

        if (hasPlayerTag && squadRoot != null)
        {
            return squadRoot;
        }

        return null;
    }

    private bool RegisterCharacterCollider(GameObject character)
    {
        if (character == null)
        {
            return false;
        }

        if (!characterColliderCounts.TryGetValue(character, out int count))
        {
            characterColliderCounts[character] = 1;
            return true;
        }

        characterColliderCounts[character] = count + 1;
        return false;
    }

    private bool UnregisterCharacterCollider(GameObject character)
    {
        if (character == null)
        {
            return false;
        }

        if (!characterColliderCounts.TryGetValue(character, out int count))
        {
            return false;
        }

        count -= 1;
        if (count > 0)
        {
            characterColliderCounts[character] = count;
            return false;
        }

        characterColliderCounts.Remove(character);
        return true;
    }
}
