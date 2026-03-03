using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

[RequireComponent(typeof(Collider))]
// Interaction pour ajouter/retirer un compagnon dans la squad depuis le hub.
public class HubCompanionSwapTrigger : MonoBehaviour
{
    [Header("Character")]
    [Tooltip("Personnage associe a ce point de swap.")]
    public CharacterData characterData;
    [Tooltip("Cache le modele si le personnage est deja dans la squad.")]
    public bool hideWhenInSquad = false;
    [Tooltip("Deplace l'instance vers le point hub si dans la squad.")]
    public bool moveInstanceWhenInSquad = true;
    [Tooltip("Desactive l'interaction si deja dans la squad.")]
    public bool disableInteractionWhenInSquad = true;
    [Tooltip("N'autorise le swap que dans le hub.")]
    public bool requireHubZone = true;
    [Tooltip("Transform override pour la position hub.")]
    public Transform hubHomeOverride;

    [Header("UI - Interaction")]
    [Tooltip("Prefab/objet UI d'interaction.")]
    public GameObject interactionBox;
    [Tooltip("Offset en world pour la box d'interaction.")]
    public Vector3 interactionOffset = new Vector3(0f, 2f, 0f);

    [Header("UI - Parent")]
    [Tooltip("Parent des boxes UI.")]
    public Transform boxesPanel;

    [Header("Camera")]
    [Tooltip("Camera UI/world pour positionner l'interaction box.")]
    public Camera targetCamera;

    [Header("Hub")]
    [Tooltip("Manager du hub (fallback sur singleton).")]
    public HubRosterManager hubManager;

    private readonly List<GameObject> charactersInRange = new List<GameObject>();
    private readonly Dictionary<GameObject, int> characterColliderCounts = new Dictionary<GameObject, int>();
    private GameObject currentCharacter;
    private Transform interactionTarget;

    private GameObject interactionBoxInstance;
    private Canvas interactionCanvas;
    private bool isTriggerZone;
    private bool available = true;
    private Collider triggerCollider;
    private bool awaitingServerResponse;
    private uint netcodeId;
    private Vector3 hubHomePosition;
    private Quaternion hubHomeRotation;
    private Transform hubHomeParent;

    private readonly List<Renderer> cachedRenderers = new List<Renderer>();
    private readonly List<Collider> cachedColliders = new List<Collider>();

    public CharacterData CharacterData => characterData;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();
        isTriggerZone = triggerCollider != null && triggerCollider.isTrigger;
        if (triggerCollider != null && !triggerCollider.isTrigger)
        {
            Debug.LogWarning("HubCompanionSwapTrigger: le collider n'est pas en mode Trigger.");
        }

        netcodeId = NetcodeSceneIdUtility.GetStableId(transform);
        CacheVisuals();
        CacheHubHome();
    }

    private void OnEnable()
    {
        if (SquadManager.Instance != null)
        {
            CharacterData runtime = SquadManager.Instance.GetRuntimeCharacter(characterData);
            if (runtime != null)
            {
                characterData = runtime;
            }
        }

        LocalInputRouter.EnsureInitialized();
        LocalInputRouter.Interact += OnInteractPerformed;
        NetcodeTriggerRegistry.Register(this, netcodeId);

        HubRosterManager manager = hubManager != null ? hubManager : HubRosterManager.Instance;
        if (manager != null)
        {
            manager.Register(this);
        }

        RegisterWithSquadManager();
        UpdateAvailabilityFromSquad();
    }

    private void OnDisable()
    {
        LocalInputRouter.Interact -= OnInteractPerformed;
        NetcodeTriggerRegistry.Unregister(this, netcodeId);

        HubRosterManager manager = hubManager != null ? hubManager : HubRosterManager.Instance;
        if (manager != null)
        {
            manager.Unregister(this);
        }

        ResetUIState();
    }

    private void Update()
    {
        // Selection du perso controle pour l'interaction.
        RefreshCurrentCharacter();
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
        if (other == null || other.isTrigger)
        {
            return;
        }

        if (!isTriggerZone || !available)
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

    private void OnTriggerExit(Collider other)
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

    private void HandleInteract()
    {
        if (InputFocusStack.HasAnyFocus())
        {
            return;
        }

        if (!available)
        {
            return;
        }

        RefreshCurrentCharacter();
        if (currentCharacter == null)
        {
            return;
        }

        HubRosterManager manager = hubManager != null ? hubManager : HubRosterManager.Instance;
        if (requireHubZone && manager != null && !manager.CanSwap())
        {
            return;
        }

        if (characterData == null || SquadManager.Instance == null)
        {
            return;
        }

        if (SquadManager.Instance.IsInputLocked())
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
                service.RequestHubSwapServerRpc(netcodeId);
            }
            else
            {
                awaitingServerResponse = false;
            }
            return;
        }

        if (SquadManager.Instance.TrySwapWithHubCharacter(characterData))
        {
            ResetUIState();
        }
    }

    public void HandleSwapResult(bool success)
    {
        awaitingServerResponse = false;
        if (success)
        {
            ResetUIState();
        }
    }

    public bool ServerTrySwap(GameObject character)
    {
        if (!available)
        {
            return false;
        }

        if (!IsServerCharacterAllowed(character))
        {
            return false;
        }

        HubRosterManager manager = hubManager != null ? hubManager : HubRosterManager.Instance;
        if (requireHubZone && manager != null && !manager.CanSwap())
        {
            return false;
        }

        if (characterData == null || SquadManager.Instance == null)
        {
            return false;
        }

        if (SquadManager.Instance.IsInputLocked())
        {
            return false;
        }

        return SquadManager.Instance.TrySwapWithHubCharacter(characterData);
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

    public void SetInSquad(bool inSquad)
    {
        if (hideWhenInSquad)
        {
            SetAvailable(!inSquad);
            return;
        }

        available = !inSquad;
        if (disableInteractionWhenInSquad && triggerCollider != null)
        {
            triggerCollider.enabled = !inSquad;
        }

        if (inSquad)
        {
            ResetUIState();
        }
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
    }

    private void RefreshCurrentCharacter()
    {
        if (!available)
        {
            currentCharacter = null;
            interactionTarget = null;
            ShowInteraction(false);
            return;
        }

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

    private void DestroyInteractionInstance()
    {
        if (interactionBoxInstance != null)
        {
            Destroy(interactionBoxInstance);
            interactionBoxInstance = null;
            interactionCanvas = null;
        }
    }

    private void ResetUIState()
    {
        DestroyInteractionInstance();
        charactersInRange.Clear();
        characterColliderCounts.Clear();
        currentCharacter = null;
        interactionTarget = null;
        awaitingServerResponse = false;
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

    private void CacheVisuals()
    {
        cachedRenderers.Clear();
        cachedColliders.Clear();

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                cachedRenderers.Add(renderers[i]);
            }
        }

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                cachedColliders.Add(colliders[i]);
            }
        }
    }

    private void UpdateAvailabilityFromSquad()
    {
        if (characterData == null || SquadManager.Instance == null)
        {
            return;
        }

        CharacterData runtimeCharacter = SquadManager.Instance.GetRuntimeCharacter(characterData);
        if (runtimeCharacter != null)
        {
            characterData = runtimeCharacter;
        }

        bool inSquad = SquadManager.Instance.currentSquad != null
            && SquadManager.Instance.currentSquad.Contains(characterData);

        SetInSquad(inSquad);
    }

    public void SetAvailable(bool isAvailable)
    {
        available = isAvailable;
        for (int i = 0; i < cachedRenderers.Count; i++)
        {
            if (cachedRenderers[i] != null)
            {
                cachedRenderers[i].enabled = available;
            }
        }

        for (int i = 0; i < cachedColliders.Count; i++)
        {
            if (cachedColliders[i] != null)
            {
                cachedColliders[i].enabled = available;
            }
        }

        if (!available)
        {
            ResetUIState();
        }
    }

    public Vector3 GetHubHomePosition()
    {
        return hubHomePosition;
    }

    public Quaternion GetHubHomeRotation()
    {
        return hubHomeRotation;
    }

    public Transform GetHubHomeParent()
    {
        return hubHomeParent;
    }

    private void CacheHubHome()
    {
        Transform home = hubHomeOverride != null ? hubHomeOverride : transform;
        hubHomePosition = home.position;
        hubHomeRotation = home.rotation;
        hubHomeParent = home.parent;
    }

    private void RegisterWithSquadManager()
    {
        if (characterData == null || SquadManager.Instance == null)
        {
            return;
        }

        SquadManager.Instance.RegisterHubCompanion(
            characterData,
            gameObject,
            hubHomePosition,
            hubHomeRotation,
            hubHomeParent);
    }
}
