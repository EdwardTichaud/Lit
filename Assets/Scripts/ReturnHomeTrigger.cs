using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using Unity.Netcode;

// Zone d'interaction pour renvoyer un personnage a la maison et vider l'inventaire.
[RequireComponent(typeof(Collider))]
public class ReturnHomeTrigger : MonoBehaviour
{
    [Header("UI - Interaction")]
    [Tooltip("Prefab/objet UI d'interaction.")]
    public GameObject interactionBox;
    [Tooltip("Offset en world pour la box d'interaction.")]
    public Vector3 interactionOffset = new Vector3(0f, 2f, 0f);

    [Header("UI - Confirmation")]
    [Tooltip("Panel parent de la confirmation.")]
    [FormerlySerializedAs("confirmPanel")]
    public GameObject confirmationPanel;
    [Tooltip("Prefab de la confirmation (Oui/Non).")]
    public GameObject confirmationBox;
    [Tooltip("Force un sorting order elevé.")]
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

    [Header("UI - Stockage plein")]
    [Tooltip("Panel d'alerte stockage plein.")]
    public GameObject storageFullPanel;
    [Tooltip("Parent pour instancier le panel.")]
    public Transform storageFullPanelParent;
    [Tooltip("Ouvre automatiquement l'inventaire si plein.")]
    public bool autoOpenInventoryOnStorageFull = false;
    [Tooltip("Reference a l'inventaire (fallback auto-find).")]
    public InventoryPanelController inventoryPanelController;

    [Header("UI - Parent")]
    [Tooltip("Parent des boxes UI.")]
    public Transform boxesPanel;
    [Tooltip("Parent des confirmations UI.")]
    public Transform confirmationBoxes;

    [Header("Camera")]
    [Tooltip("Camera UI/world pour positionner l'interaction box.")]
    public Camera targetCamera;

    [Header("Maison - Stockage")]
    [Tooltip("Coffre maison principal.")]
    public LootContainer maisonLootContainer;

    private readonly List<GameObject> charactersInRange = new List<GameObject>();
    private readonly Dictionary<GameObject, int> characterColliderCounts = new Dictionary<GameObject, int>();
    private GameObject currentCharacter;
    private Transform interactionTarget;

    private GameObject interactionBoxInstance;
    private GameObject confirmationBoxInstance;
    private GameObject storageFullPanelInstance;
    private bool confirmVisible;
    private Canvas interactionCanvas;
    private bool isTriggerZone;
    private Collider triggerCollider;
    private CursorController confirmationCursor;
    private CanvasGroup confirmationCanvasGroup;
    private Coroutine confirmationFadeRoutine;
    private bool confirmationInputLocked;
    private bool awaitingServerResponse;
    private uint netcodeId;

    private enum ConfirmationChoice
    {
        Unknown,
        Yes,
        No
    }

    void Awake()
    {
        triggerCollider = GetComponent<Collider>();
        isTriggerZone = triggerCollider != null && triggerCollider.isTrigger;
        if (triggerCollider != null && !triggerCollider.isTrigger)
        {
            Debug.LogWarning("ReturnHomeTrigger: le collider n'est pas en mode Trigger.");
        }

        netcodeId = NetcodeSceneIdUtility.GetStableId(transform);
        InitializeConfirmationFade();
    }

    void OnEnable()
    {
        LocalInputRouter.EnsureInitialized();
        LocalInputRouter.Interact += OnInteractPerformed;
        NetcodeTriggerRegistry.Register(this, netcodeId);
    }

    void OnDisable()
    {
        LocalInputRouter.Interact -= OnInteractPerformed;
        NetcodeTriggerRegistry.Unregister(this, netcodeId);

        ResetUIState();
    }

    private void Update()
    {
        RefreshCurrentCharacter();
    }

    void LateUpdate()
    {
        // Aligne la box d'interaction sur le personnage cible.
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

    void OnTriggerEnter(Collider other)
    {
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
        RefreshCurrentCharacter();
    }

    void OnTriggerExit(Collider other)
    {
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

        RefreshCurrentCharacter();
        if (currentCharacter == null && charactersInRange.Count == 0)
        {
            ResetUIState();
        }
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        HandleInteract();
    }

    void OnInteract()
    {
        HandleInteract();
    }

    private void HandleInteract()
    {
        if (InputFocusStack.HasAnyFocus() && !InputFocusStack.HasFocus(this))
        {
            return;
        }

        RefreshCurrentCharacter();
        if (currentCharacter == null)
        {
            return;
        }

        LocalInputRouter.ConsumeInteract();

        ShowStorageFull(false);

        if (!confirmVisible)
        {
            ShowConfirm(true);
            return;
        }

        ConfirmationChoice choice = GetConfirmationChoice();
        if (choice == ConfirmationChoice.No)
        {
            ShowConfirm(false);
            ShowInteraction(true);
            return;
        }

        if (choice == ConfirmationChoice.Unknown)
        {
            Debug.LogWarning("ReturnHomeTrigger: confirmationBox ou CursorController non configure.");
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
                service.RequestReturnHomeServerRpc(netcodeId);
            }
            else
            {
                awaitingServerResponse = false;
            }
            return;
        }

        if (SquadManager.Instance != null)
        {
            SquadManager.SendHomeResult result = SquadManager.Instance.TrySendCharacterHome(currentCharacter, maisonLootContainer);
            if (result == SquadManager.SendHomeResult.StorageFull)
            {
                ShowConfirm(false);
                ShowInteraction(true);
                ShowStorageFull(true);
                if (autoOpenInventoryOnStorageFull)
                {
                    InventoryPanelController inventory = inventoryPanelController;
                    if (inventory == null)
                    {
#if UNITY_2023_1_OR_NEWER
                        inventory = FindFirstObjectByType<InventoryPanelController>();
#else
                        inventory = FindObjectOfType<InventoryPanelController>();
#endif
                    }
                    if (inventory != null)
                    {
                        inventory.TryOpenInventory();
                    }
                }
                return;
            }

            if (result == SquadManager.SendHomeResult.Success)
            {
                charactersInRange.Remove(currentCharacter);
                characterColliderCounts.Remove(currentCharacter);
                currentCharacter = null;
                interactionTarget = null;
                ShowConfirm(false);
                ShowInteraction(false);

                if (charactersInRange.Count > 0)
                {
                    RefreshCurrentCharacter();
                }
            }
        }
    }

    void OnSouthButton()
    {
        OnInteract();
    }

    void OnEastButton()
    {
        if (!confirmVisible)
        {
            return;
        }

        ShowConfirm(false);
    }

    private void SetCurrentCharacter(GameObject character)
    {
        if (character == null || !IsControlledCharacter(character))
        {
            return;
        }

        currentCharacter = character;
        interactionTarget = character.transform;
        ShowInteraction(true);
        DestroyConfirmInstance();
        confirmVisible = false;
    }

    private void RefreshCurrentCharacter()
    {
        GameObject controlled = GetControlledCharacter();
        if (controlled != null && charactersInRange.Contains(controlled))
        {
            if (currentCharacter != controlled)
            {
                currentCharacter = controlled;
                interactionTarget = controlled.transform;
            }

            ShowInteraction(true);
            return;
        }

        currentCharacter = null;
        interactionTarget = null;
        ShowInteraction(false);
    }

    private static GameObject GetControlledCharacter()
    {
        return LocalPlayerUtils.GetControlledCharacter();
    }

    private static bool IsControlledCharacter(GameObject character)
    {
        return SquadManager.Instance != null && SquadManager.Instance.currentCharacter == character;
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
            RefreshCurrentCharacter();
            return;
        }

        DestroyInteractionInstance();

        if (confirmationPanel == null)
        {
            Debug.LogWarning("ReturnHomeTrigger: confirmationPanel non assigne.");
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
            confirmationBoxInstance = CreateInstance(confirmationBox, confirmationPanel.transform);
        }

        if (confirmationBoxInstance == null)
        {
            Debug.LogWarning("ReturnHomeTrigger: confirmationBox non assignee.");
            SetConfirmationInputLock(false);
            confirmVisible = false;
            return;
        }

        confirmationBoxInstance.SetActive(true);
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
        }
    }

    private void ShowStorageFull(bool show)
    {
        if (!show)
        {
            DestroyStorageFullInstance();
            return;
        }

        if (storageFullPanelInstance == null)
        {
            Transform parent = storageFullPanelParent != null
                ? storageFullPanelParent
                : confirmationBoxes != null
                    ? confirmationBoxes
                    : confirmationPanel != null
                        ? confirmationPanel.transform
                        : null;
            storageFullPanelInstance = CreateInstance(storageFullPanel, parent);
        }

        if (storageFullPanelInstance != null)
        {
            storageFullPanelInstance.SetActive(true);
        }
    }

    private void DestroyStorageFullInstance()
    {
        if (storageFullPanelInstance != null)
        {
            Destroy(storageFullPanelInstance);
            storageFullPanelInstance = null;
        }
    }

    private void ResetUIState()
    {
        DestroyInteractionInstance();
        DestroyConfirmInstance();
        DestroyStorageFullInstance();
        FadeConfirmationPanelTo(0f, 0f);
        SetConfirmationInputLock(false);
        confirmVisible = false;
        charactersInRange.Clear();
        characterColliderCounts.Clear();
        currentCharacter = null;
        interactionTarget = null;
        awaitingServerResponse = false;
    }

    public void HandleReturnHomeResult(SquadManager.SendHomeResult result)
    {
        awaitingServerResponse = false;

        if (result == SquadManager.SendHomeResult.StorageFull)
        {
            ShowConfirm(false);
            ShowInteraction(true);
            ShowStorageFull(true);
            if (autoOpenInventoryOnStorageFull)
            {
                InventoryPanelController inventory = inventoryPanelController;
                if (inventory == null)
                {
#if UNITY_2023_1_OR_NEWER
                    inventory = FindFirstObjectByType<InventoryPanelController>();
#else
                    inventory = FindObjectOfType<InventoryPanelController>();
#endif
                }
                if (inventory != null)
                {
                    inventory.TryOpenInventory();
                }
            }
            return;
        }

        if (result == SquadManager.SendHomeResult.Success)
        {
            charactersInRange.Remove(currentCharacter);
            characterColliderCounts.Remove(currentCharacter);
            currentCharacter = null;
            interactionTarget = null;
            ShowConfirm(false);
            ShowInteraction(false);

            if (charactersInRange.Count > 0)
            {
                RefreshCurrentCharacter();
            }
            return;
        }

        ShowConfirm(false);
        ShowInteraction(true);
    }

    public SquadManager.SendHomeResult ServerTrySendHome(GameObject character)
    {
        if (character == null || SquadManager.Instance == null)
        {
            return SquadManager.SendHomeResult.InvalidCharacter;
        }

        if (!IsServerCharacterAllowed(character))
        {
            return SquadManager.SendHomeResult.InvalidCharacter;
        }

        return SquadManager.Instance.TrySendCharacterHome(character, maisonLootContainer);
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

        Vector3 position = character.transform.position;
        float distance = triggerCollider.bounds.SqrDistance(position);
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
