using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Interaction sur un batiment pour ouvrir le panel d'informations.
[RequireComponent(typeof(Collider))]
public class BuildingInfoInteractable : MonoBehaviour
{
    [Header("Building Data")]
    [SerializeField, Tooltip("Identifiant de construction (fallback si Item manquant).")]
    private string buildId;
    [SerializeField, Tooltip("Item building associe.")]
    private Item buildingItem;
    [SerializeField, Tooltip("Niveau actuel de cette instance.")]
    private int level = 1;

    [Header("Info")]
    [Tooltip("Prefab du panel d'informations local.")]
    public GameObject localInformationPanelPrefab;
    [Tooltip("Parent du panel instancie.")]
    public Transform localPanelParent;
    [Tooltip("Point d'ancrage du panel local.")]
    public Transform informationAnchor;
    [Tooltip("Offset du panel local.")]
    public Vector3 informationOffset = new Vector3(0f, 2f, 0f);
    [Tooltip("Camera utilisee pour placer le panel local en screen space.")]
    public Camera targetCamera;
    [Tooltip("Detruit le panel local a la sortie.")]
    public bool destroyPanelOnExit = false;

    [Header("Interaction")]
    [Tooltip("Trigger d'interaction. Laisse vide pour auto-detecter.")]
    public Collider interactionTrigger;
    [Tooltip("Ferme le panel quand le joueur quitte la zone.")]
    public bool closePanelOnExit = true;
    [Tooltip("Ouvre automatiquement le panel quand le joueur est proche.")]
    public bool openOnProximity = true;

    private readonly List<GameObject> charactersInRange = new List<GameObject>();
    private readonly Dictionary<GameObject, int> characterColliderCounts = new Dictionary<GameObject, int>();
    private GameObject currentCharacter;
    private bool useSelfTriggerEvents;
    private PlayerInputs playerInputs;
    private LocalBuildingInformationsPanelController localPanelInstance;
    private bool warnedMissingPrefab;

    public string BuildId => buildId;
    public Item BuildingItem => buildingItem;
    public int Level => level;
    public string BuildingItemId => ResolveBuildingItemId(buildingItem, buildId);
    public bool IsHomeChest => buildingItem != null && buildingItem.isHomeChest;

    private void Awake()
    {
        EnsureBuildingData();

        InitializeInteractionTrigger();
        playerInputs = new PlayerInputs();
    }

    private void OnEnable()
    {
        if (playerInputs == null)
        {
            playerInputs = new PlayerInputs();
        }

        playerInputs.Enable();
        playerInputs.Player.Interact.performed += OnInteractPerformed;
    }

    private void OnDisable()
    {
        if (playerInputs != null)
        {
            playerInputs.Player.Interact.performed -= OnInteractPerformed;
            playerInputs.Disable();
        }

        ResetState();
    }

    private void Update()
    {
        if (!openOnProximity)
        {
            return;
        }

        RefreshCurrentCharacter();
        EnsureBuildingData();
        EnsureLocalPanel();

        if (currentCharacter != null && HasBuildingData())
        {
            if (localPanelInstance != null)
            {
                UpdateLocalPanelAnchor();
                if (!localPanelInstance.IsOpen || localPanelInstance.CurrentBuilding != this)
                {
                    localPanelInstance.OpenPanel(this);
                }
                else
                {
                    localPanelInstance.RefreshPanel();
                }
            }
        }
        else
        {
            CloseInfoPanels();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!useSelfTriggerEvents)
        {
            return;
        }

        HandleCharacterEnter(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!useSelfTriggerEvents)
        {
            return;
        }

        HandleCharacterExit(other);
    }

    public void NotifyTriggerEnter(Collider other)
    {
        HandleCharacterEnter(other);
    }

    public void NotifyTriggerExit(Collider other)
    {
        HandleCharacterExit(other);
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        EnsureBuildingData();
        if (openOnProximity)
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

            if (!HasBuildingData())
            {
                Debug.LogWarning("BuildingInfoInteractable: aucune donnee de construction.", this);
                return;
            }

            TryApplyInteractEffects();
            return;
        }

        EnsureLocalPanel();
        HandleInteract();
    }

    private void HandleInteract()
    {
        if (!CanProcessInteract())
        {
            return;
        }

        RefreshCurrentCharacter();
        if (currentCharacter == null)
        {
            return;
        }

        EnsureBuildingData();
        if (!HasBuildingData())
        {
            Debug.LogWarning("BuildingInfoInteractable: aucune donnee de construction.", this);
            return;
        }

        TryApplyInteractEffects();

        EnsureLocalPanel();
        if (localPanelInstance != null)
        {
            if (localPanelInstance.IsOpen && localPanelInstance.CurrentBuilding == this)
            {
                localPanelInstance.ClosePanel();
                return;
            }

            localPanelInstance.OpenPanel(this);
            return;
        }
    }

    private bool TryApplyInteractEffects()
    {
        if (buildingItem == null || !buildingItem.isBuilding)
        {
            return false;
        }

        if (buildingItem.buildingEffects == null || buildingItem.buildingEffects.Count == 0)
        {
            return false;
        }

        if (currentCharacter == null)
        {
            return false;
        }

        SquadCharacterController controller = currentCharacter.GetComponent<SquadCharacterController>();
        if (controller == null)
        {
            return false;
        }

        bool applied = false;
        for (int i = 0; i < buildingItem.buildingEffects.Count; i++)
        {
            Effect effect = buildingItem.buildingEffects[i];
            if (effect == null)
            {
                continue;
            }

            if (effect is IBuildingInteractEffect interactEffect)
            {
                if (interactEffect.ApplyOnInteract(controller, buildingItem, level))
                {
                    applied = true;
                }
            }
        }

        return applied;
    }

    private bool CanProcessInteract()
    {
        if (openOnProximity)
        {
            return false;
        }

        return !InputFocusStack.HasAnyFocus();
    }

    private void HandleCharacterEnter(Collider other)
    {
        if (other == null || other.isTrigger)
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

    private void HandleCharacterExit(Collider other)
    {
        if (other == null || other.isTrigger)
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
        }

        RefreshCurrentCharacter();
        if (currentCharacter == null && closePanelOnExit)
        {
            CloseInfoPanels();
            if (destroyPanelOnExit && localPanelInstance != null)
            {
                Destroy(localPanelInstance.gameObject);
                localPanelInstance = null;
            }
        }
    }

    private void RefreshCurrentCharacter()
    {
        GameObject controlled = GetControlledCharacter();
        if (controlled != null && charactersInRange.Contains(controlled))
        {
            currentCharacter = controlled;
            return;
        }

        currentCharacter = null;
    }

    private static GameObject GetControlledCharacter()
    {
        return SquadManager.Instance != null ? SquadManager.Instance.currentCharacter : null;
    }

    private void ResetState()
    {
        charactersInRange.Clear();
        characterColliderCounts.Clear();
        currentCharacter = null;
    }

    private void EnsureBuildingData()
    {
        if (level < 1)
        {
            level = 1;
        }

        if (string.IsNullOrWhiteSpace(buildId) && buildingItem != null)
        {
            buildId = ResolveBuildingItemId(buildingItem, string.Empty);
        }
    }

    private bool HasBuildingData()
    {
        return buildingItem != null || !string.IsNullOrWhiteSpace(buildId);
    }

    private void EnsureLocalPanel()
    {
        if (localInformationPanelPrefab == null)
        {
            if (!warnedMissingPrefab)
            {
                Debug.LogWarning("BuildingInfoInteractable: prefab LocalBuildingInformationPanel manquant.", this);
                warnedMissingPrefab = true;
            }
            return;
        }

        if (localPanelInstance != null)
        {
            UpdateLocalPanelAnchor();
            return;
        }

        Transform parent = localPanelParent != null ? localPanelParent : null;
        GameObject instance = Instantiate(localInformationPanelPrefab, parent);
        if (instance == null)
        {
            return;
        }

        CanvasGroup group = instance.GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = instance.AddComponent<CanvasGroup>();
        }

        localPanelInstance = instance.GetComponent<LocalBuildingInformationsPanelController>();
        if (localPanelInstance == null)
        {
            localPanelInstance = instance.AddComponent<LocalBuildingInformationsPanelController>();
        }

        localPanelInstance.informationPanel = instance;
    }

    private void UpdateLocalPanelAnchor()
    {
        if (localPanelInstance == null)
        {
            return;
        }

        Transform anchor = informationAnchor != null ? informationAnchor : transform;
        PositionLocalPanel(localPanelInstance.transform, anchor, informationOffset);
    }

    private void PositionLocalPanel(Transform panelTransform, Transform anchor, Vector3 offset)
    {
        if (panelTransform == null || anchor == null)
        {
            return;
        }

        Vector3 worldPosition = anchor.position + offset;
        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        Canvas canvas = panelTransform.GetComponentInParent<Canvas>();
        RectTransform rect = panelTransform as RectTransform;

        if (canvas != null && canvas.renderMode != RenderMode.WorldSpace)
        {
            if (cam == null || rect == null)
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
                RectTransform canvasRect = canvas.transform as RectTransform;
                if (canvasRect == null)
                {
                    rect.position = screenPos;
                }
                else if (RectTransformUtility.ScreenPointToWorldPointInRectangle(canvasRect, screenPos, cam, out Vector3 worldPoint))
                {
                    rect.position = worldPoint;
                }
            }

            return;
        }

        panelTransform.position = worldPosition;
        if (cam != null && localPanelInstance != null && localPanelInstance.faceCamera)
        {
            Vector3 toCamera = panelTransform.position - cam.transform.position;
            if (toCamera.sqrMagnitude > 0.0001f)
            {
                panelTransform.rotation = Quaternion.LookRotation(toCamera);
            }
        }
    }

    private void CloseInfoPanels()
    {
        if (localPanelInstance != null && localPanelInstance.IsOpen && localPanelInstance.CurrentBuilding == this)
        {
            localPanelInstance.ClosePanel();
        }
    }

    public void Initialize(Item item, int levelValue = 1)
    {
        Initialize(string.Empty, item, levelValue);
    }

    public void Initialize(string buildIdValue, Item item, int levelValue = 1)
    {
        buildingItem = item;
        buildId = !string.IsNullOrWhiteSpace(buildIdValue) ? buildIdValue : ResolveBuildingItemId(item, buildId);
        level = Mathf.Max(1, levelValue);
    }

    public void SetLevel(int levelValue)
    {
        level = Mathf.Max(1, levelValue);
    }

    private static string ResolveBuildingItemId(Item item, string fallback)
    {
        if (item != null)
        {
            if (!string.IsNullOrWhiteSpace(item.itemId))
            {
                return item.itemId;
            }

            if (!string.IsNullOrWhiteSpace(item.itemName))
            {
                return item.itemName;
            }

            if (!string.IsNullOrWhiteSpace(item.name))
            {
                return item.name;
            }
        }

        return fallback ?? string.Empty;
    }

    private void InitializeInteractionTrigger()
    {
        if (interactionTrigger == null)
        {
            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null && colliders[i].isTrigger && !IsConcaveMeshCollider(colliders[i]))
                {
                    interactionTrigger = colliders[i];
                    break;
                }
            }

            if (interactionTrigger == null)
            {
                for (int i = 0; i < colliders.Length; i++)
                {
                    if (colliders[i] != null && !IsConcaveMeshCollider(colliders[i]))
                    {
                        interactionTrigger = colliders[i];
                        break;
                    }
                }
            }

            if (interactionTrigger == null && colliders.Length > 0)
            {
                interactionTrigger = colliders[0];
            }
        }

        if (interactionTrigger == null)
        {
            interactionTrigger = CreateFallbackTrigger();
        }

        if (interactionTrigger == null)
        {
            Debug.LogWarning("BuildingInfoInteractable: aucun collider trouve pour l'interaction.", this);
            useSelfTriggerEvents = false;
            return;
        }

        if (IsConcaveMeshCollider(interactionTrigger))
        {
            Collider fallback = CreateBoxTrigger(interactionTrigger);
            if (fallback != null)
            {
                interactionTrigger = fallback;
                Debug.LogWarning("BuildingInfoInteractable: MeshCollider concave detecte, ajout d'un BoxCollider Trigger pour l'interaction.", this);
            }
        }
        else if (!interactionTrigger.isTrigger)
        {
            interactionTrigger.isTrigger = true;
            Debug.LogWarning("BuildingInfoInteractable: le collider d'interaction n'etait pas en Trigger. Il a ete force en Trigger.", this);
        }

        useSelfTriggerEvents = interactionTrigger.gameObject == gameObject;
        if (!useSelfTriggerEvents)
        {
            BuildingInfoTriggerProxy proxy = interactionTrigger.GetComponent<BuildingInfoTriggerProxy>();
            if (proxy == null)
            {
                proxy = interactionTrigger.gameObject.AddComponent<BuildingInfoTriggerProxy>();
            }
            proxy.Owner = this;
        }
    }

    private Collider CreateFallbackTrigger()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        Bounds bounds = new Bounds(transform.position, Vector3.one);
        bool hasBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderers[i].bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
        }

        BoxCollider box = gameObject.AddComponent<BoxCollider>();
        box.isTrigger = true;
        if (hasBounds)
        {
            box.center = transform.InverseTransformPoint(bounds.center);
            Vector3 localSize = transform.InverseTransformVector(bounds.size);
            box.size = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));
        }

        return box;
    }

    private static bool IsConcaveMeshCollider(Collider collider)
    {
        MeshCollider meshCollider = collider as MeshCollider;
        return meshCollider != null && !meshCollider.convex;
    }

    private Collider CreateBoxTrigger(Collider reference)
    {
        if (reference == null)
        {
            return null;
        }

        BoxCollider box = reference.gameObject.AddComponent<BoxCollider>();
        box.isTrigger = true;
        FitBoxToCollider(box, reference);
        return box;
    }

    private void FitBoxToCollider(BoxCollider box, Collider reference)
    {
        if (box == null)
        {
            return;
        }

        if (reference == null)
        {
            box.center = Vector3.zero;
            box.size = Vector3.one;
            return;
        }

        if (reference is BoxCollider boxCollider)
        {
            box.center = boxCollider.center;
            box.size = boxCollider.size;
            return;
        }

        if (reference is SphereCollider sphereCollider)
        {
            float diameter = sphereCollider.radius * 2f;
            box.center = sphereCollider.center;
            box.size = new Vector3(diameter, diameter, diameter);
            return;
        }

        if (reference is CapsuleCollider capsuleCollider)
        {
            float diameter = capsuleCollider.radius * 2f;
            box.center = capsuleCollider.center;
            box.size = new Vector3(diameter, capsuleCollider.height, diameter);
            return;
        }

        Bounds bounds = reference.bounds;
        box.center = reference.transform.InverseTransformPoint(bounds.center);
        Vector3 localSize = reference.transform.InverseTransformVector(bounds.size);
        box.size = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));
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

public class BuildingInfoTriggerProxy : MonoBehaviour
{
    public BuildingInfoInteractable Owner { get; set; }

    private void OnTriggerEnter(Collider other)
    {
        if (Owner != null)
        {
            Owner.NotifyTriggerEnter(other);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (Owner != null)
        {
            Owner.NotifyTriggerExit(other);
        }
    }
}
