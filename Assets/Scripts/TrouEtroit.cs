using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider))]
// Passage etroit detecte par skill, avec interaction et teleport.
public class TrouEtroit : MonoBehaviour
{
    [Header("Skills")]
    [Tooltip("Skill utilise pour detecter le passage.")]
    public Skill observateurSkill;
    [Tooltip("Skill requis pour l'utiliser.")]
    public Skill saufConduitSkill;
    [Tooltip("Si true, le passage doit etre detecte avant interaction.")]
    public bool requireDetection = true;

    [Header("Detection")]
    [Tooltip("Demarre deja detecte.")]
    public bool startDetected = false;

    [Header("Visibility")]
    [Tooltip("Masque le mesh tant que non detecte.")]
    public bool hideWhenUndetected = true;
    [Tooltip("Inclut les renderers inactifs dans le cache.")]
    public bool includeInactiveRenderers = true;

    [Header("Glow")]
    [Tooltip("Outline utilise pour le glow.")]
    public Outline outline;
    [Tooltip("Cree un Outline si manquant.")]
    public bool createOutlineIfMissing = true;
    [Tooltip("Couleur du glow.")]
    public Color glowColor = Color.white;
    [Tooltip("Epaisseur du glow.")]
    public float glowWidth = 6f;
    [Tooltip("Mode d'outline.")]
    public Outline.Mode glowMode = Outline.Mode.OutlineAll;

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

    [Header("Teleport")]
    [Tooltip("Point A du passage. Si A et B sont assignes, le personnage est teleporte vers l'autre point selon celui dont il est le plus proche.")]
    public Transform teleportPointA;
    [Tooltip("Point B du passage. Si A et B sont assignes, le personnage est teleporte vers l'autre point selon celui dont il est le plus proche.")]
    public Transform teleportPointB;
    [Tooltip("Distance de teleport de l'autre cote.")]
    public float teleportDistance = 1.5f;
    [Tooltip("Offset additionnel applique a la destination.")]
    public Vector3 teleportOffset = Vector3.zero;
    [Tooltip("Garde la hauteur Y du personnage.")]
    public bool keepOriginalHeight = true;
    [Tooltip("Oriente le personnage vers la sortie.")]
    public bool faceExitDirection = true;

    private readonly List<GameObject> charactersInRange = new List<GameObject>();
    private readonly Dictionary<GameObject, int> characterColliderCounts = new Dictionary<GameObject, int>();
    private GameObject currentCharacter;
    private bool isTriggerZone;
    private bool detected;
    private Renderer[] cachedRenderers;
    private bool[] cachedRendererStates;
    private Transform interactionTarget;
    private GameObject interactionBoxInstance;
    private Canvas interactionCanvas;

    public bool IsDetected => detected;

    private void Awake()
    {
        Collider trigger = EnsureTriggerCollider();
        isTriggerZone = trigger != null;

        detected = startDetected;
        CacheRenderers();
        UpdateGlow();
        ApplyVisibility();
    }

    private void OnEnable()
    {
        LocalInputRouter.EnsureInitialized();
        LocalInputRouter.Interact += OnInteractPerformed;
    }

    private void OnDisable()
    {
        LocalInputRouter.Interact -= OnInteractPerformed;

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
        RefreshCurrentCharacter();

        // Detection auto si skill configure et activation par proximite.
        if (!detected && observateurSkill != null && observateurSkill.autoRollOnProximity && TryCheckSkill(character, observateurSkill))
        {
            Detect();
        }
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
            DestroyInteractionInstance();
        }
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        if (InputFocusStack.HasAnyFocus())
        {
            return;
        }

        // Interaction disponible uniquement si detecte (optionnel) et skill valide.
        if (requireDetection && !detected)
        {
            return;
        }

        RefreshCurrentCharacter();

        if (currentCharacter == null)
        {
            return;
        }

        if (!TryCheckSkill(currentCharacter, saufConduitSkill))
        {
            return;
        }

        LocalInputRouter.ConsumeInteract();

        // Teleport apres un check reussi.
        TeleportCharacter(currentCharacter);
    }

    public void Detect()
    {
        if (detected)
        {
            return;
        }

        detected = true;
        UpdateGlow();
        ApplyVisibility();

        if (charactersInRange.Count > 0)
        {
            RefreshCurrentCharacter();
        }
    }

    public void RestoreDetectedState(bool detectedState)
    {
        detected = detectedState;
        UpdateGlow();
        ApplyVisibility();

        if (charactersInRange.Count > 0)
        {
            RefreshCurrentCharacter();
            return;
        }

        DestroyInteractionInstance();
    }

    private void UpdateGlow()
    {
        if (!detected)
        {
            if (outline != null)
            {
                outline.enabled = false;
            }
            return;
        }

        if (outline == null && createOutlineIfMissing)
        {
            outline = GetComponentInChildren<Outline>(true);
            if (outline == null)
            {
                outline = gameObject.AddComponent<Outline>();
            }
        }

        if (outline != null)
        {
            outline.OutlineColor = glowColor;
            outline.OutlineWidth = glowWidth;
            outline.OutlineMode = glowMode;
            outline.enabled = true;
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

    private void ShowInteraction(bool show)
    {
        if (!show || !CanShowInteraction())
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

    private bool CanShowInteraction()
    {
        if (interactionBox == null)
        {
            return false;
        }

        if (requireDetection && !detected)
        {
            return false;
        }

        return IsControlledCharacter(currentCharacter);
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

    private void CacheRenderers()
    {
        cachedRenderers = GetComponentsInChildren<Renderer>(includeInactiveRenderers);
        if (cachedRenderers == null || cachedRenderers.Length == 0)
        {
            cachedRendererStates = null;
            return;
        }

        cachedRendererStates = new bool[cachedRenderers.Length];
        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            Renderer renderer = cachedRenderers[i];
            cachedRendererStates[i] = renderer != null && renderer.enabled;
        }
    }

    private void ApplyVisibility()
    {
        if (!hideWhenUndetected)
        {
            return;
        }

        if (cachedRenderers == null || cachedRenderers.Length == 0)
        {
            return;
        }

        if (!detected)
        {
            for (int i = 0; i < cachedRenderers.Length; i++)
            {
                Renderer renderer = cachedRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                renderer.enabled = false;
            }
            return;
        }

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            Renderer renderer = cachedRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            bool enabled = cachedRendererStates != null && i < cachedRendererStates.Length
                ? cachedRendererStates[i]
                : true;
            renderer.enabled = enabled;
        }
    }

    private Collider EnsureTriggerCollider()
    {
        Collider[] colliders = GetComponents<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null && colliders[i].isTrigger && !IsConcaveMeshCollider(colliders[i]))
            {
                return colliders[i];
            }
        }

        Collider primary = GetComponent<Collider>();
        if (primary == null)
        {
            return null;
        }

        if (IsConcaveMeshCollider(primary))
        {
            Debug.LogWarning("TrouEtroit: MeshCollider concave detecte, ajout d'un BoxCollider Trigger pour l'interaction.", this);
            return CreateBoxTrigger(primary);
        }

        if (!primary.isTrigger)
        {
            Debug.LogWarning("TrouEtroit: le collider d'interaction n'etait pas en Trigger. Il a ete force en Trigger.", this);
        }
        primary.isTrigger = true;
        return primary;
    }

    private static bool IsConcaveMeshCollider(Collider collider)
    {
        MeshCollider meshCollider = collider as MeshCollider;
        return meshCollider != null && !meshCollider.convex;
    }

    private Collider CreateBoxTrigger(Collider reference)
    {
        BoxCollider box = gameObject.AddComponent<BoxCollider>();
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
        box.center = transform.InverseTransformPoint(bounds.center);
        Vector3 localSize = transform.InverseTransformVector(bounds.size);
        box.size = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));
    }

    private void TeleportCharacter(GameObject character)
    {
        if (character == null)
        {
            return;
        }

        Transform t = character.transform;
        Quaternion rotation = t.rotation;
        Vector3 destination;
        if (!TryGetTransformTeleportDestination(t, out destination, out Quaternion targetRotation))
        {
            Vector3 toCharacter = t.position - transform.position;
            float side = Vector3.Dot(transform.forward, toCharacter) >= 0f ? 1f : -1f;
            Vector3 exitDir = side >= 0f ? -transform.forward : transform.forward;

            destination = transform.position + exitDir * Mathf.Max(0f, teleportDistance);
            if (keepOriginalHeight)
            {
                destination.y = t.position.y;
            }

            destination += teleportOffset;

            if (faceExitDirection && exitDir.sqrMagnitude > 0.001f)
            {
                rotation = Quaternion.LookRotation(exitDir.normalized, Vector3.up);
            }
        }
        else if (faceExitDirection)
        {
            rotation = targetRotation;
        }

        Rigidbody rb = character.GetComponent<Rigidbody>();
        CharacterController controller = character.GetComponent<CharacterController>();

        if (controller != null)
        {
            controller.enabled = false;
        }

        if (rb != null)
        {
            rb.position = destination;
            rb.rotation = rotation;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        t.SetPositionAndRotation(destination, rotation);

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

    private bool TryGetTransformTeleportDestination(Transform character, out Vector3 destination, out Quaternion rotation)
    {
        destination = default;
        rotation = Quaternion.identity;

        if (character == null || teleportPointA == null || teleportPointB == null)
        {
            return false;
        }

        Vector3 characterPosition = character.position;
        float distanceToA = (characterPosition - teleportPointA.position).sqrMagnitude;
        float distanceToB = (characterPosition - teleportPointB.position).sqrMagnitude;
        Transform targetPoint = distanceToA <= distanceToB ? teleportPointB : teleportPointA;
        if (targetPoint == null)
        {
            return false;
        }

        destination = targetPoint.position + teleportOffset;
        if (keepOriginalHeight)
        {
            destination.y = characterPosition.y;
        }

        Vector3 flatForward = targetPoint.forward;
        flatForward.y = 0f;
        rotation = flatForward.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(flatForward.normalized, Vector3.up)
            : character.rotation;
        return true;
    }

    private bool TryCheckSkill(GameObject character, Skill skill)
    {
        return SkillCheckSystem.TryCheck(character, skill, out _, out _, out _);
    }

    private GameObject GetSquadCharacter(Collider other)
    {
        if (other == null)
        {
            return null;
        }

        SquadManager manager = SquadManager.Instance;
        Transform current = other.transform;
        bool hasPlayerTag = false;
        GameObject taggedRoot = null;
        GameObject squadRoot = null;

        while (current != null)
        {
            if (current.CompareTag("Player"))
            {
                hasPlayerTag = true;
                taggedRoot = current.gameObject;
            }

            if (manager != null && manager.squadCharacters != null && manager.squadCharacters.Contains(current.gameObject))
            {
                squadRoot = current.gameObject;
            }

            current = current.parent;
        }

        if (squadRoot == null && manager != null && manager.squadCharacters != null)
        {
            Transform root = other.transform.root;
            if (root != null)
            {
                if (root.CompareTag("Player"))
                {
                    hasPlayerTag = true;
                    taggedRoot = root.gameObject;
                }

                for (int i = 0; i < manager.squadCharacters.Count; i++)
                {
                    GameObject candidate = manager.squadCharacters[i];
                    if (candidate != null && candidate.transform.IsChildOf(root))
                    {
                        squadRoot = candidate;
                        break;
                    }
                }
            }
        }

        if (squadRoot != null)
        {
            return squadRoot;
        }

        if (hasPlayerTag)
        {
            return taggedRoot;
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
