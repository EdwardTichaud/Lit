using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class LadderInteractable : MonoBehaviour, ICharacterDetectedInteractable
{
    [Header("Interaction")]
    [Tooltip("Controller qui pilote la sequence de montee. Laisse vide pour auto-detecter sur ce GameObject, ses parents ou ses enfants.")]
    public LadderController ladderController;
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
    public string interactionText = "Utiliser l'echelle";
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

    private void Reset()
    {
        ladderController = ResolveLadderController();
        interactionCollider = CharacterInteractionDetection.ResolveInteractionCollider(this, interactionCollider);
    }

    private void OnValidate()
    {
        interactionMaxDistance = Mathf.Max(0.1f, interactionMaxDistance);
    }

    private void Awake()
    {
        ladderController = ResolveLadderController();
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
        return controller != null && isActiveAndEnabled && ResolveLadderController() != null;
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
        if (!CanUse(character, requireLocalControl: true, rangePadding: 0f))
        {
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
            LocalInputRouter.ConsumeInteract();
            service.RequestLadderUseServerRpc(netcodeId);
            return;
        }

        if (!TryStartLadderUse(character, driveMotion: true))
        {
            return;
        }

        LocalInputRouter.ConsumeInteract();
    }

    public bool ServerTryUse(GameObject character)
    {
        return CanUse(character, requireLocalControl: false, rangePadding: 0.35f)
            && TryStartLadderUse(character, driveMotion: true);
    }

    public bool IsServerCharacterAllowed(GameObject character)
    {
        return CanUse(character, requireLocalControl: false, rangePadding: 0.35f);
    }

    public void HandleLadderUseResult(bool success)
    {
        awaitingServerResponse = false;
        if (!success)
        {
            return;
        }

        GameObject character = currentCharacter != null
            ? currentCharacter
            : LocalPlayerUtils.GetControlledCharacter();
        TryStartLadderUse(character, driveMotion: false);
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

        if (ResolveLadderController() == null)
        {
            return false;
        }

        return CharacterInteractionDetection.IsCharacterWithinRange(
            character.transform,
            GetInteractionDetectionCollider(),
            GetInteractionAnchor(),
            interactionMaxDistance + Mathf.Max(0f, rangePadding));
    }

    private bool TryStartLadderUse(GameObject character, bool driveMotion)
    {
        if (character == null)
        {
            return false;
        }

        LadderController controller = ResolveLadderController();
        return controller != null && controller.UseLadder(character, driveMotion);
    }

    private LadderController ResolveLadderController()
    {
        if (ladderController != null)
        {
            return ladderController;
        }

        ladderController = GetComponent<LadderController>();
        if (ladderController != null)
        {
            return ladderController;
        }

        ladderController = GetComponentInParent<LadderController>();
        if (ladderController != null)
        {
            return ladderController;
        }

        ladderController = GetComponentInChildren<LadderController>(true);
        return ladderController;
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
