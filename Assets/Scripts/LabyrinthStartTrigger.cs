using System.Collections.Generic;
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

    [Header("Camera")]
    [Tooltip("Camera UI/world pour positionner l'interaction box.")]
    public Camera targetCamera;

    private readonly List<GameObject> charactersInRange = new List<GameObject>();
    private readonly Dictionary<GameObject, int> characterColliderCounts = new Dictionary<GameObject, int>();
    private GameObject currentCharacter;
    private Transform interactionTarget;

    private GameObject interactionBoxInstance;
    private Canvas interactionCanvas;
    private bool isTriggerZone;
    private Collider triggerCollider;
    private bool awaitingServerResponse;
    private uint netcodeId;
    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();
        isTriggerZone = triggerCollider != null && triggerCollider.isTrigger;
        if (triggerCollider != null && !triggerCollider.isTrigger)
        {
            Debug.LogWarning("LabyrinthStartTrigger: le collider n'est pas en mode Trigger.");
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

        ResetUIState();
    }

    private void Update()
    {
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
        HandleInteract();
    }

    private void HandleInteract()
    {
        if (InputFocusStack.HasAnyFocus())
        {
            return;
        }

        RefreshCurrentCharacter(true);
        if (currentCharacter == null)
        {
            return;
        }

        LocalInputRouter.ConsumeInteract();

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
            }
            return;
        }

        StartLabyrinth();
    }

    private void StartLabyrinth()
    {
        if (labyrinthRoot != null)
        {
            labyrinthRoot.SetActive(true);
        }

        TeleportSquadToSpawn();
        ResetUIState();
    }

    private void RefreshCurrentCharacter(bool allowShow)
    {
        GameObject controlled = GetControlledCharacter();
        if (controlled != null && charactersInRange.Contains(controlled))
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

    private static GameObject GetControlledCharacter()
    {
        return LocalPlayerUtils.GetControlledCharacter();
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

    public void ServerStartLabyrinth()
    {
        awaitingServerResponse = false;
        StartLabyrinth();
    }

    public void ClientHandleLabyrinthStarted()
    {
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

        Rigidbody rb = character.GetComponent<Rigidbody>();
        CharacterController controller = character.GetComponent<CharacterController>();
        if (controller != null)
        {
            controller.enabled = false;
        }

        if (rb != null)
        {
            rb.position = position;
            rb.rotation = rotation;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        character.transform.SetPositionAndRotation(position, rotation);

        if (controller != null)
        {
            controller.enabled = true;
        }

        SquadCharacterController squadController = character.GetComponent<SquadCharacterController>();
        if (squadController != null)
        {
            squadController.Stop();
        }
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
