using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

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
    [Tooltip("Panel de confirmation.")]
    public GameObject confirmPanel;
    [Tooltip("Force un sorting order elevé.")]
    public bool forceConfirmOnTop = true;
    [Tooltip("Sorting order pour le panel de confirmation.")]
    public int confirmSortingOrder = 100;

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
    private GameObject confirmPanelInstance;
    private GameObject storageFullPanelInstance;
    private bool confirmVisible;
    private Canvas interactionCanvas;
    private bool isTriggerZone;
    private PlayerInputs playerInputs;

    void Awake()
    {
        Collider trigger = GetComponent<Collider>();
        isTriggerZone = trigger != null && trigger.isTrigger;
        if (trigger != null && !trigger.isTrigger)
        {
            Debug.LogWarning("ReturnHomeTrigger: le collider n'est pas en mode Trigger.");
        }

        playerInputs = new PlayerInputs();
    }

    void OnEnable()
    {
        if (playerInputs == null)
        {
            playerInputs = new PlayerInputs();
        }

        playerInputs.Enable();
        playerInputs.Player.Interact.performed += OnInteractPerformed;
    }

    void OnDisable()
    {
        if (playerInputs != null)
        {
            playerInputs.Player.Interact.performed -= OnInteractPerformed;
            playerInputs.Disable();
        }

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
        if (InputFocusStack.HasAnyFocus())
        {
            return;
        }

        RefreshCurrentCharacter();
        if (currentCharacter == null)
        {
            return;
        }

        ShowStorageFull(false);

        if (!confirmVisible)
        {
            ShowConfirm(true);
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
        return SquadManager.Instance != null ? SquadManager.Instance.currentCharacter : null;
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
            DestroyConfirmInstance();
            confirmVisible = false;
            RefreshCurrentCharacter();
            return;
        }

        DestroyInteractionInstance();

        if (confirmPanelInstance == null)
        {
            confirmPanelInstance = CreateInstance(confirmPanel, confirmationBoxes);
        }

        if (confirmPanelInstance == null)
        {
            Debug.LogWarning("ReturnHomeTrigger: confirmPanel non assigne.");
            confirmVisible = false;
            return;
        }

        confirmPanelInstance.SetActive(true);
        BringConfirmToFront();
        confirmVisible = true;
    }

    private void BringConfirmToFront()
    {
        if (confirmPanelInstance == null)
        {
            return;
        }

        if (confirmPanelInstance.transform.parent != null)
        {
            confirmPanelInstance.transform.SetAsLastSibling();
        }

        if (!forceConfirmOnTop)
        {
            return;
        }

        Canvas canvas = confirmPanelInstance.GetComponent<Canvas>();
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
        if (confirmPanelInstance != null)
        {
            Destroy(confirmPanelInstance);
            confirmPanelInstance = null;
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
            Transform parent = storageFullPanelParent != null ? storageFullPanelParent : confirmationBoxes;
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
        confirmVisible = false;
        charactersInRange.Clear();
        characterColliderCounts.Clear();
        currentCharacter = null;
        interactionTarget = null;
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
