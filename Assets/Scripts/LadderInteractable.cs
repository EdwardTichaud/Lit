using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class LadderInteractable : MonoBehaviour, ICharacterDetectedInteractable
{
    [Header("Anchors")]
    [Tooltip("Base basse de l'echelle. La position et la rotation servent au placement du personnage avant la montee.")]
    public Transform bottomBase;
    [Tooltip("Base haute de l'echelle. La position et la rotation servent au placement du personnage avant la descente.")]
    public Transform topBase;
    [SerializeField, Tooltip("Cote utilise pour les bases auto si aucun point n'est assigne.")]
    private bool fallbackAnchorsOnNegativeForwardSide = true;
    [SerializeField, Tooltip("Distance entre l'echelle et les bases auto si aucun point n'est assigne.")]
    private float fallbackAnchorStandOff = 0.45f;
    [SerializeField, Tooltip("Marge verticale appliquee aux bases auto depuis les bounds.")]
    private float fallbackAnchorVerticalPadding = 0.05f;

    [Header("Interaction")]
    [Tooltip("Collider de reference pour la detection. Laisse vide pour auto-detecter un collider non-trigger.")]
    public Collider interactionCollider;
    [Tooltip("Distance maximale a laquelle le personnage peut cibler cette echelle.")]
    public float interactionMaxDistance = 2.25f;
    [Tooltip("Priorite de selection si plusieurs interactions sont proches.")]
    public int interactionPriority = 35;

    [Header("UI - Interaction")]
    [Tooltip("Prefab/objet UI d'interaction.")]
    public GameObject interactionBox;
    [Tooltip("Texte affiche dans l'InteractionBox.")]
    public string interactionText = "Utiliser l'échelle";
    [Tooltip("Offset en world pour la box d'interaction.")]
    public Vector3 interactionOffset = new Vector3(0f, 2f, 0f);

    [Header("UI - Parent")]
    [Tooltip("Parent des boxes UI.")]
    public Transform boxesPanel;

    [Header("Camera")]
    [Tooltip("Camera UI/world pour positionner l'interaction box.")]
    public Camera targetCamera;

    private GameObject currentCharacter;
    private Transform interactionTarget;
    private GameObject interactionBoxInstance;
    private Canvas interactionCanvas;
    private Collider resolvedInteractionCollider;
    private uint netcodeId;
    private bool awaitingServerResponse;

    private struct LadderPose
    {
        public Vector3 Position;
        public Quaternion Rotation;
    }

    private void Reset()
    {
        interactionCollider = CharacterInteractionDetection.ResolveInteractionCollider(this, interactionCollider);
    }

    private void OnValidate()
    {
        fallbackAnchorStandOff = Mathf.Max(0f, fallbackAnchorStandOff);
        fallbackAnchorVerticalPadding = Mathf.Max(0f, fallbackAnchorVerticalPadding);
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
            if (canvasRect != null
                && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect,
                    screenPos,
                    uiCamera,
                    out Vector2 localPoint))
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
            && !controller.IsLadderTraversalActive
            && TryResolveBasePoses(out _, out _);
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
        ShowInteraction(currentCharacter != null);
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        if (InputFocusStack.HasAnyFocus())
        {
            return;
        }

        GameObject character = currentCharacter;
        if (character == null || !IsControlledCharacter(character))
        {
            return;
        }

        SquadCharacterController controller = GetController(character);
        if (controller == null || controller.IsLadderTraversalActive)
        {
            return;
        }

        if (!CharacterInteractionDetection.IsCharacterWithinRange(
                character.transform,
                GetInteractionDetectionCollider(),
                GetInteractionAnchor(),
                interactionMaxDistance))
        {
            return;
        }

        if (IsNetworked() && !NetworkManager.Singleton.IsServer)
        {
            if (awaitingServerResponse)
            {
                return;
            }

            if (!TryResolveTraversal(character, out _, out _, out _))
            {
                return;
            }

            WorldInteractionService service = WorldInteractionService.Instance;
            if (service == null)
            {
                return;
            }

            awaitingServerResponse = true;
            LocalInputRouter.ConsumeInteract();
            service.RequestLadderUseServerRpc(netcodeId);
            return;
        }

        if (!TryStartTraversalForCharacter(character, requireLocalControl: true))
        {
            return;
        }

        LocalInputRouter.ConsumeInteract();
        DestroyInteractionInstance();
    }

    public bool ServerTryUse(GameObject character)
    {
        return TryStartTraversalForCharacter(character, requireLocalControl: false);
    }

    public bool IsServerCharacterAllowed(GameObject character)
    {
        if (character == null)
        {
            return false;
        }

        return CharacterInteractionDetection.IsCharacterWithinRange(
            character.transform,
            GetInteractionDetectionCollider(),
            GetInteractionAnchor(),
            interactionMaxDistance + 0.35f);
    }

    public void HandleLadderUseResult(bool success)
    {
        awaitingServerResponse = false;
        if (success)
        {
            DestroyInteractionInstance();
        }
    }

    private bool TryStartTraversalForCharacter(GameObject character, bool requireLocalControl)
    {
        if (character == null)
        {
            return false;
        }

        if (requireLocalControl && !IsControlledCharacter(character))
        {
            return false;
        }

        SquadCharacterController controller = GetController(character);
        if (controller == null || controller.IsLadderTraversalActive)
        {
            return false;
        }

        if (!CharacterInteractionDetection.IsCharacterWithinRange(
                character.transform,
                GetInteractionDetectionCollider(),
                GetInteractionAnchor(),
                interactionMaxDistance))
        {
            return false;
        }

        if (!TryResolveTraversal(character, out LadderPose startPose, out LadderPose endPose, out bool ascending))
        {
            return false;
        }

        return controller.TryStartLadderTraversal(this, startPose.Position, startPose.Rotation, endPose.Position, endPose.Rotation, ascending);
    }

    private bool TryResolveTraversal(GameObject character, out LadderPose startPose, out LadderPose endPose, out bool ascending)
    {
        startPose = default;
        endPose = default;
        ascending = true;

        if (character == null || !TryResolveBasePoses(out LadderPose bottomPose, out LadderPose topPose))
        {
            return false;
        }

        Vector3 origin = ResolveCharacterOrigin(character);
        float distanceToBottom = (origin - bottomPose.Position).sqrMagnitude;
        float distanceToTop = (origin - topPose.Position).sqrMagnitude;

        ascending = distanceToBottom <= distanceToTop;
        startPose = ascending ? bottomPose : topPose;
        endPose = ascending ? topPose : bottomPose;
        return true;
    }

    private bool TryResolveBasePoses(out LadderPose bottomPose, out LadderPose topPose)
    {
        bottomPose = default;
        topPose = default;

        bool hasBottom = bottomBase != null;
        bool hasTop = topBase != null;
        if (hasBottom && hasTop)
        {
            bottomPose = new LadderPose { Position = bottomBase.position, Rotation = bottomBase.rotation };
            topPose = new LadderPose { Position = topBase.position, Rotation = topBase.rotation };
            return true;
        }

        if (!TryBuildFallbackPoses(out LadderPose fallbackBottom, out LadderPose fallbackTop))
        {
            return false;
        }

        bottomPose = hasBottom
            ? new LadderPose { Position = bottomBase.position, Rotation = bottomBase.rotation }
            : fallbackBottom;
        topPose = hasTop
            ? new LadderPose { Position = topBase.position, Rotation = topBase.rotation }
            : fallbackTop;
        return true;
    }

    private bool TryBuildFallbackPoses(out LadderPose bottomPose, out LadderPose topPose)
    {
        bottomPose = default;
        topPose = default;

        if (!TryGetFallbackBounds(out Bounds bounds))
        {
            return false;
        }

        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.forward;
        }
        else
        {
            forward.Normalize();
        }

        Vector3 positionOffset = (fallbackAnchorsOnNegativeForwardSide ? -forward : forward) * fallbackAnchorStandOff;
        Vector3 facing = fallbackAnchorsOnNegativeForwardSide ? forward : -forward;
        Quaternion rotation = Quaternion.LookRotation(facing, Vector3.up);

        Vector3 center = bounds.center + positionOffset;
        float bottomY = bounds.min.y + fallbackAnchorVerticalPadding;
        float topY = bounds.max.y - fallbackAnchorVerticalPadding;
        if (topY < bottomY)
        {
            float middleY = bounds.center.y;
            bottomY = middleY;
            topY = middleY;
        }

        bottomPose = new LadderPose
        {
            Position = new Vector3(center.x, bottomY, center.z),
            Rotation = rotation
        };
        topPose = new LadderPose
        {
            Position = new Vector3(center.x, topY, center.z),
            Rotation = rotation
        };
        return true;
    }

    private bool TryGetFallbackBounds(out Bounds bounds)
    {
        Collider collider = GetInteractionDetectionCollider();
        if (collider != null)
        {
            bounds = collider.bounds;
            return true;
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        bounds = default;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
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
        GameObject instance = new GameObject("LadderInteractionBox", typeof(RectTransform), typeof(Canvas), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(GraphicRaycaster));
        if (parent != null)
        {
            instance.transform.SetParent(parent, false);
        }

        RectTransform rect = instance.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(240f, 50f);
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

    private static Vector3 ResolveCharacterOrigin(GameObject character)
    {
        if (character == null)
        {
            return Vector3.zero;
        }

        SquadCharacterController controller = GetController(character);
        return controller != null ? controller.GetInteractionOriginWorldPosition() : character.transform.position;
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
