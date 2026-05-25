using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public class Door : NetworkBehaviour, ICharacterDetectedInteractable, ILeverTarget, ILitInfluenceReceiver
{
    public enum DoorMotionMode
    {
        Rotate,
        Slide
    }

    public enum DoorInteractMode
    {
        Toggle,
        OpenOnly,
        CloseOnly
    }

    [Header("Door")]
    [SerializeField, Tooltip("Transform anime. Laisse vide pour utiliser ce GameObject comme pivot.")]
    private Transform doorTransform;
    [SerializeField, Tooltip("Etat initial applique au lancement.")]
    private bool startOpen;
    [SerializeField, Tooltip("Si true, l'interaction directe est refusee.")]
    private bool locked;
    [SerializeField, Tooltip("Bloque les interactions directes apres la premiere activation reussie.")]
    private bool singleUse;
    [SerializeField, Tooltip("Mode d'interaction directe.")]
    private DoorInteractMode interactMode = DoorInteractMode.Toggle;
    [SerializeField, Tooltip("Capacite optionnelle requise sur l'objet equipe du personnage.")]
    private InteractionCapability requiredCapability = InteractionCapability.None;

    [Header("Motion")]
    [SerializeField, Tooltip("Type de mouvement de la porte.")]
    private DoorMotionMode motionMode = DoorMotionMode.Rotate;
    [SerializeField, Min(0f), Tooltip("Duree de transition en secondes. 0 = instantane.")]
    private float transitionDuration = 0.45f;
    [SerializeField, Tooltip("Axe local de rotation pour une porte battante.")]
    private Vector3 localRotationAxis = Vector3.up;
    [SerializeField, Tooltip("Angle d'ouverture en degres autour de l'axe local.")]
    private float openAngle = 90f;
    [SerializeField, Tooltip("Offset local pour une porte coulissante.")]
    private Vector3 openLocalOffset = Vector3.zero;

    [Header("Interaction")]
    [SerializeField, Tooltip("Ecoute l'input Interact quand cette porte est ciblee.")]
    private bool useInteractInput = true;
    [SerializeField, Tooltip("Collider de reference pour la detection. Laisse vide pour auto-detecter.")]
    private Collider interactionCollider;
    [SerializeField, Min(0.1f), Tooltip("Distance maximale d'interaction.")]
    private float interactionMaxDistance = 2.25f;
    [SerializeField, Tooltip("Priorite de selection si plusieurs interactions sont proches.")]
    private int interactionPriority = 45;

    [Header("Influence")]
    [SerializeField, FormerlySerializedAs("requireActiveFlameForInteraction"), Tooltip("Si true, la porte reste bloquee hors zone d'influence d'une torche ou d'un brasero allume.")]
    private bool requireLitInfluenceForInteraction = true;
    [SerializeField, Tooltip("La porte reagit a la zone d'influence des braseros allumes.")]
    private bool reactToBraseroInfluence = true;
    [SerializeField, Tooltip("La porte reagit a la zone d'influence des torches allumees.")]
    private bool reactToTorchInfluence = true;

    [Header("Key")]
    [SerializeField, Tooltip("Identifiant de serrure. Vide = cette porte n'a pas besoin de cle.")]
    private string lockId;
    [SerializeField, Tooltip("Consomme la cle compatible quand la porte s'ouvre.")]
    private bool consumeKeyOnUse;
    [SerializeField, Tooltip("Message affiche si le personnage n'a pas la cle requise.")]
    private string missingKeyMessage = "Il faut une cl\u00e9 pour ouvrir cette porte.";
    [SerializeField, Tooltip("Message affiche si la cle est compatible mais que la porte est hors influence.")]
    private string keyCompatibleButFrozenMessage = "La cl\u00e9 semble \u00eatre compatible mais la porte reste fig\u00e9e";
    [SerializeField, Tooltip("Message affiche si la porte sans cle est hors influence.")]
    private string frozenDoorMessage = "La porte semble fig\u00e9e.";

    [Header("Interaction UI")]
    [SerializeField, Tooltip("Affiche une InteractionBox quand la porte est ciblee.")]
    private bool showInteractionUi = true;
    [SerializeField, Tooltip("Prefab/objet UI d'interaction optionnel.")]
    private GameObject interactionBox;
    [SerializeField, Tooltip("Texte quand la porte peut etre ouverte.")]
    private string openInteractionText = "Ouvrir";
    [SerializeField, Tooltip("Texte quand la porte peut etre fermee.")]
    private string closeInteractionText = "Fermer";
    [SerializeField, Tooltip("Texte quand la porte est verrouillee.")]
    private string lockedInteractionText = "Verrouille";
    [SerializeField, Tooltip("Texte quand la porte est hors zone d'influence.")]
    private string frozenInteractionText = "Fig\u00e9e";
    [SerializeField, Tooltip("Offset en world pour la box d'interaction.")]
    private Vector3 interactionOffset = new Vector3(0f, 2f, 0f);
    [SerializeField, Tooltip("Parent des boxes UI.")]
    private Transform boxesPanel;
    [SerializeField, Tooltip("Camera UI/world pour positionner l'interaction box.")]
    private Camera targetCamera;

    [Header("Collision")]
    [SerializeField, Tooltip("Colliders bloquants a desactiver quand la porte est suffisamment ouverte.")]
    private Collider[] blockingColliders;
    [SerializeField, Tooltip("Desactive les colliders bloquants quand la porte est ouverte.")]
    private bool disableBlockingCollidersWhenOpen;
    [SerializeField, Range(0f, 1f), Tooltip("Seuil d'ouverture a partir duquel les colliders bloquants sont desactives.")]
    private float colliderDisableOpenThreshold = 0.75f;

    [Header("Lever")]
    [SerializeField, Tooltip("Un levier actif ouvre la porte.")]
    private bool openWhenLeverActivated = true;
    [SerializeField, Tooltip("Un levier inactif referme la porte.")]
    private bool closeWhenLeverDeactivated;

    [Header("Audio")]
    [SerializeField, Tooltip("Son joue a l'ouverture.")]
    private AudioClipSO openSfx;
    [SerializeField, Tooltip("Son joue a la fermeture.")]
    private AudioClipSO closeSfx;
    [SerializeField, Tooltip("Son joue si l'interaction est refusee.")]
    private AudioClipSO lockedSfx;

    [Header("Events")]
    [SerializeField] private UnityEvent onOpened;
    [SerializeField] private UnityEvent onClosed;
    [SerializeField] private UnityEvent onLockedInteract;

    [Header("Debug")]
    [SerializeField, Tooltip("Etat courant de la porte.")]
    private bool isOpen;

    private readonly NetworkVariable<bool> netIsOpen = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private GameObject currentCharacter;
    private GameObject interactionBoxInstance;
    private Canvas interactionCanvas;
    private Collider resolvedInteractionCollider;
    private Vector3 closedLocalPosition;
    private Quaternion closedLocalRotation;
    private float currentOpenAmount;
    private bool wasInteractedOnce;
    private bool initialized;
    private readonly HashSet<int> activeLitInfluenceSourceIds = new HashSet<int>();

    public bool IsOpen => isOpen;
    public bool IsLocked => locked;

    private void Reset()
    {
        doorTransform = transform;
        interactionCollider = CharacterInteractionDetection.ResolveInteractionCollider(this, interactionCollider);
    }

    private void Awake()
    {
        RuntimeOutlineUtility.EnsureOutlineTargets(gameObject);
        InitializeRuntime();
    }

    private void OnValidate()
    {
        transitionDuration = Mathf.Max(0f, transitionDuration);
        interactionMaxDistance = Mathf.Max(0.1f, interactionMaxDistance);
        colliderDisableOpenThreshold = Mathf.Clamp01(colliderDisableOpenThreshold);
        if (localRotationAxis.sqrMagnitude <= 0.0001f)
        {
            localRotationAxis = Vector3.up;
        }

        if (doorTransform == null)
        {
            doorTransform = transform;
        }
    }

    private void OnEnable()
    {
        LocalInputRouter.EnsureInitialized();
        if (useInteractInput)
        {
            LocalInputRouter.Interact += OnInteractPerformed;
        }

        InitializeRuntime();
    }

    private void OnDisable()
    {
        if (useInteractInput)
        {
            LocalInputRouter.Interact -= OnInteractPerformed;
        }

        activeLitInfluenceSourceIds.Clear();
        ResetInteractionUi();
    }

    private void Update()
    {
        InitializeRuntime();
        TickMotion(Time.deltaTime);
    }

    private void LateUpdate()
    {
        UpdateInteractionUiPosition();
    }

    public override void OnNetworkSpawn()
    {
        InitializeRuntime();
        netIsOpen.OnValueChanged += OnNetworkOpenChanged;
        if (IsServer)
        {
            netIsOpen.Value = isOpen;
        }
        else
        {
            ApplyOpenState(netIsOpen.Value, instant: true, emitEvents: false, playFeedback: false);
        }
    }

    public override void OnNetworkDespawn()
    {
        netIsOpen.OnValueChanged -= OnNetworkOpenChanged;
    }

    public bool CanBeDetectedBy(SquadCharacterController controller)
    {
        // La detection reste volontairement active hors influence: l'interaction
        // doit pouvoir expliquer au joueur que la porte est figee.
        return controller != null
            && isActiveAndEnabled
            && useInteractInput
            && HasAvailableInteractionAction();
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
        return doorTransform != null ? doorTransform : transform;
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
        ShowInteraction(currentCharacter != null && showInteractionUi);
    }

    public void HandleLeverStateChanged(Lever lever, bool active)
    {
        if (active && openWhenLeverActivated)
        {
            SetOpen(true);
            return;
        }

        if (!active && closeWhenLeverDeactivated)
        {
            SetOpen(false);
        }
    }

    public void SetLocked(bool value)
    {
        locked = value;
        RefreshInteractionText();
    }

    public void Toggle()
    {
        SetOpen(!isOpen);
    }

    public void Open()
    {
        SetOpen(true);
    }

    public void Close()
    {
        SetOpen(false);
    }

    public void SetOpen(bool open)
    {
        InitializeRuntime();
        if (IsNetworked() && !IsServer)
        {
            RequestSetOpenServerRpc(open);
            return;
        }

        ApplyOpenState(open, instant: false, emitEvents: true, playFeedback: true);
        if (IsNetworked() && IsServer)
        {
            netIsOpen.Value = isOpen;
        }
    }

    private void InitializeRuntime()
    {
        if (initialized)
        {
            return;
        }

        if (doorTransform == null)
        {
            doorTransform = transform;
        }

        resolvedInteractionCollider = CharacterInteractionDetection.ResolveInteractionCollider(this, interactionCollider);
        if (interactionCollider == null)
        {
            interactionCollider = resolvedInteractionCollider;
        }

        closedLocalPosition = doorTransform.localPosition;
        closedLocalRotation = doorTransform.localRotation;
        isOpen = startOpen;
        currentOpenAmount = isOpen ? 1f : 0f;
        ApplyDoorPose(currentOpenAmount);
        UpdateBlockingColliders();
        initialized = true;
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        if (!useInteractInput || LocalInputRouter.IsInteractConsumed || InputFocusStack.HasAnyFocus())
        {
            return;
        }

        GameObject character = ResolveInteractionCharacter();
        if (character == null)
        {
            return;
        }

        if (!TryResolveNextInteractionState(out bool nextOpen))
        {
            return;
        }

        if (!LocalInputRouter.TryConsumeInteract())
        {
            return;
        }

        if (TryGetInteractionBlockMessage(character, nextOpen, out string blockMessage))
        {
            HandleBlockedInteraction(blockMessage);
            return;
        }

        if (IsNetworked() && !IsServer)
        {
            RequestInteractServerRpc(nextOpen);
            return;
        }

        if (!TryConsumeRequiredKey(character, nextOpen))
        {
            HandleBlockedInteraction(missingKeyMessage);
            return;
        }

        wasInteractedOnce = true;
        ApplyOpenState(nextOpen, instant: false, emitEvents: true, playFeedback: true);
        if (IsNetworked() && IsServer)
        {
            netIsOpen.Value = isOpen;
        }
    }

    private bool TryResolveNextInteractionState(out bool nextOpen)
    {
        nextOpen = isOpen;
        if (singleUse && wasInteractedOnce)
        {
            return false;
        }

        switch (interactMode)
        {
            case DoorInteractMode.Toggle:
                nextOpen = !isOpen;
                return true;

            case DoorInteractMode.OpenOnly:
                nextOpen = true;
                return !isOpen;

            case DoorInteractMode.CloseOnly:
                nextOpen = false;
                return isOpen;
        }

        return false;
    }

    private bool HasAvailableInteractionAction()
    {
        if (singleUse && wasInteractedOnce)
        {
            return false;
        }

        switch (interactMode)
        {
            case DoorInteractMode.Toggle:
                return true;

            case DoorInteractMode.OpenOnly:
                return !isOpen;

            case DoorInteractMode.CloseOnly:
                return isOpen;
        }

        return false;
    }

    private GameObject ResolveInteractionCharacter()
    {
        if (CanUseCharacter(currentCharacter, requireLocalControl: true))
        {
            return currentCharacter;
        }

        GameObject controlled = LocalPlayerUtils.GetControlledCharacter();
        return CanUseCharacter(controlled, requireLocalControl: true) ? controlled : null;
    }

    private bool CanUseCharacter(GameObject character, bool requireLocalControl)
    {
        if (character == null)
        {
            return false;
        }

        if (requireLocalControl)
        {
            GameObject controlled = LocalPlayerUtils.GetControlledCharacter();
            if (!IsSameCharacter(controlled, character))
            {
                return false;
            }
        }

        return CharacterInteractionDetection.IsCharacterWithinRange(
            character.transform,
            GetInteractionDetectionCollider(),
            GetInteractionAnchor(),
            interactionMaxDistance);
    }

    private static bool IsSameCharacter(GameObject controlled, GameObject candidate)
    {
        if (controlled == null || candidate == null)
        {
            return false;
        }

        if (controlled == candidate)
        {
            return true;
        }

        Transform controlledTransform = controlled.transform;
        Transform candidateTransform = candidate.transform;
        return controlledTransform.IsChildOf(candidateTransform) || candidateTransform.IsChildOf(controlledTransform);
    }

    private bool CanOpenWith(GameObject character)
    {
        return CanOpenWith(character, opening: true);
    }

    private bool CanOpenWith(GameObject character, bool opening)
    {
        return !TryGetInteractionBlockMessage(character, opening, out _);
    }

    private bool TryGetInteractionBlockMessage(GameObject character, bool opening, out string message)
    {
        message = null;

        if (locked)
        {
            return true;
        }

        SquadCharacterController controller = ResolveCharacterController(character);
        bool needsKey = opening && RequiresKey();
        bool hasRequiredKey = !needsKey || (controller != null && controller.HasMatchingKey(lockId));
        if (needsKey && !hasRequiredKey)
        {
            message = missingKeyMessage;
            return true;
        }

        if (!HasRequiredLitInfluence())
        {
            message = needsKey ? keyCompatibleButFrozenMessage : frozenDoorMessage;
            return true;
        }

        if (requiredCapability != InteractionCapability.None &&
            (controller == null || !controller.HasEquippedInteractionCapability(requiredCapability)))
        {
            return true;
        }

        return false;
    }

    private bool TryConsumeRequiredKey(GameObject character, bool opening)
    {
        if (!opening || !RequiresKey() || !consumeKeyOnUse)
        {
            return true;
        }

        SquadCharacterController controller = ResolveCharacterController(character);
        return controller != null && controller.TryUseMatchingKey(lockId, consumeKeyOnUse, out _);
    }

    private bool RequiresKey()
    {
        return !string.IsNullOrWhiteSpace(lockId);
    }

    private bool HasRequiredLitInfluence()
    {
        return !requireLitInfluenceForInteraction || activeLitInfluenceSourceIds.Count > 0;
    }

    private bool IsFrozenByMissingInfluence()
    {
        return requireLitInfluenceForInteraction && activeLitInfluenceSourceIds.Count == 0;
    }

    private void HandleBlockedInteraction(string message)
    {
        PlaySfx(lockedSfx);
        onLockedInteract?.Invoke();

        if (!string.IsNullOrWhiteSpace(message))
        {
            InfoBoxUI.TryShow(message);
        }
    }

    public void OnLitInfluenceEnter(LitInfluenceInfo info)
    {
        if (!ShouldReactToLitInfluence(info) || info.SourceId == 0)
        {
            return;
        }

        if (activeLitInfluenceSourceIds.Add(info.SourceId))
        {
            RefreshInteractionText();
        }
    }

    public void OnLitInfluenceStay(LitInfluenceInfo info)
    {
        if (!ShouldReactToLitInfluence(info) || info.SourceId == 0)
        {
            return;
        }

        if (activeLitInfluenceSourceIds.Add(info.SourceId))
        {
            RefreshInteractionText();
        }
    }

    public void OnLitInfluenceExit(LitInfluenceInfo info)
    {
        if (info.SourceId == 0)
        {
            return;
        }

        if (activeLitInfluenceSourceIds.Remove(info.SourceId))
        {
            RefreshInteractionText();
        }
    }

    private bool ShouldReactToLitInfluence(LitInfluenceInfo info)
    {
        switch (info.SourceKind)
        {
            case LitInfluenceSourceKind.Brasero:
                return reactToBraseroInfluence;

            case LitInfluenceSourceKind.Torch:
                return reactToTorchInfluence;

            default:
                return false;
        }
    }

    private static SquadCharacterController ResolveCharacterController(GameObject character)
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

        controller = character.GetComponentInParent<SquadCharacterController>();
        if (controller != null)
        {
            return controller;
        }

        return character.GetComponentInChildren<SquadCharacterController>();
    }

    private void ApplyOpenState(bool open, bool instant, bool emitEvents, bool playFeedback)
    {
        if (isOpen == open)
        {
            if (instant)
            {
                currentOpenAmount = isOpen ? 1f : 0f;
                ApplyDoorPose(currentOpenAmount);
                UpdateBlockingColliders();
            }

            return;
        }

        isOpen = open;
        if (instant)
        {
            currentOpenAmount = isOpen ? 1f : 0f;
            ApplyDoorPose(currentOpenAmount);
            UpdateBlockingColliders();
        }

        if (emitEvents)
        {
            if (isOpen)
            {
                onOpened?.Invoke();
            }
            else
            {
                onClosed?.Invoke();
            }
        }

        if (playFeedback)
        {
            PlaySfx(isOpen ? openSfx : closeSfx);
        }

        RefreshInteractionText();
    }

    private void TickMotion(float deltaTime)
    {
        float target = isOpen ? 1f : 0f;
        if (Mathf.Approximately(currentOpenAmount, target))
        {
            return;
        }

        if (transitionDuration <= 0f || deltaTime <= 0f)
        {
            currentOpenAmount = target;
        }
        else
        {
            currentOpenAmount = Mathf.MoveTowards(currentOpenAmount, target, deltaTime / transitionDuration);
        }

        ApplyDoorPose(currentOpenAmount);
        UpdateBlockingColliders();
    }

    private void ApplyDoorPose(float openAmount)
    {
        if (doorTransform == null)
        {
            return;
        }

        float eased = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(openAmount));
        if (motionMode == DoorMotionMode.Slide)
        {
            doorTransform.localPosition = closedLocalPosition + openLocalOffset * eased;
            doorTransform.localRotation = closedLocalRotation;
            return;
        }

        Vector3 axis = localRotationAxis.sqrMagnitude > 0.0001f ? localRotationAxis.normalized : Vector3.up;
        doorTransform.localPosition = closedLocalPosition;
        doorTransform.localRotation = closedLocalRotation * Quaternion.AngleAxis(openAngle * eased, axis);
    }

    private void UpdateBlockingColliders()
    {
        if (!disableBlockingCollidersWhenOpen || blockingColliders == null)
        {
            return;
        }

        bool enabled = currentOpenAmount < colliderDisableOpenThreshold;
        for (int i = 0; i < blockingColliders.Length; i++)
        {
            Collider blockingCollider = blockingColliders[i];
            if (blockingCollider == null || blockingCollider == interactionCollider)
            {
                continue;
            }

            blockingCollider.enabled = enabled;
        }
    }

    private void OnNetworkOpenChanged(bool previous, bool current)
    {
        ApplyOpenState(current, instant: false, emitEvents: true, playFeedback: true);
    }

    private bool IsNetworked()
    {
        return NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && IsSpawned;
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestInteractServerRpc(bool nextOpen, ServerRpcParams rpcParams = default)
    {
        Transform playerRoot = NetcodePlayerUtils.GetPlayerTransform(rpcParams.Receive.SenderClientId);
        GameObject character = playerRoot != null ? playerRoot.gameObject : null;
        if (playerRoot == null ||
            !IsCharacterTransformInRange(playerRoot) ||
            !CanOpenWith(character, nextOpen) ||
            !TryConsumeRequiredKey(character, nextOpen))
        {
            return;
        }

        wasInteractedOnce = true;
        ApplyOpenState(nextOpen, instant: false, emitEvents: true, playFeedback: true);
        netIsOpen.Value = isOpen;
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestSetOpenServerRpc(bool open, ServerRpcParams rpcParams = default)
    {
        Transform playerRoot = NetcodePlayerUtils.GetPlayerTransform(rpcParams.Receive.SenderClientId);
        GameObject character = playerRoot != null ? playerRoot.gameObject : null;
        if (playerRoot == null ||
            !IsCharacterTransformInRange(playerRoot) ||
            !CanOpenWith(character, open) ||
            !TryConsumeRequiredKey(character, open))
        {
            return;
        }

        ApplyOpenState(open, instant: false, emitEvents: true, playFeedback: true);
        netIsOpen.Value = isOpen;
    }

    private bool IsCharacterTransformInRange(Transform characterRoot)
    {
        return CharacterInteractionDetection.IsCharacterWithinRange(
            characterRoot,
            GetInteractionDetectionCollider(),
            GetInteractionAnchor(),
            interactionMaxDistance);
    }

    private void PlaySfx(AudioClipSO clip)
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
            }
        }

        RefreshInteractionText();
        if (interactionBoxInstance != null)
        {
            interactionBoxInstance.SetActive(true);
        }
    }

    private void RefreshInteractionText()
    {
        if (interactionBoxInstance == null)
        {
            return;
        }

        string text = ResolveInteractionText();
        TMP_Text tmp = interactionBoxInstance.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null)
        {
            tmp.text = text;
            return;
        }

        Text fallbackText = interactionBoxInstance.GetComponentInChildren<Text>(true);
        if (fallbackText != null)
        {
            fallbackText.text = text;
        }
    }

    private string ResolveInteractionText()
    {
        if (locked)
        {
            return lockedInteractionText;
        }

        if (IsFrozenByMissingInfluence())
        {
            return frozenInteractionText;
        }

        if (interactMode == DoorInteractMode.CloseOnly)
        {
            return closeInteractionText;
        }

        if (interactMode == DoorInteractMode.OpenOnly)
        {
            return openInteractionText;
        }

        if (isOpen)
        {
            return closeInteractionText;
        }

        return openInteractionText;
    }

    private void UpdateInteractionUiPosition()
    {
        if (interactionBoxInstance == null || !interactionBoxInstance.activeSelf)
        {
            return;
        }

        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        Transform anchor = GetInteractionAnchor();
        if (cam == null || anchor == null)
        {
            return;
        }

        Vector3 worldPosition = anchor.position + interactionOffset;
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
        if (toCamera.sqrMagnitude > 0.0001f)
        {
            interactionBoxInstance.transform.rotation = Quaternion.LookRotation(toCamera);
        }
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
        GameObject instance = new GameObject("DoorInteractionBox", typeof(RectTransform), typeof(Canvas), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(GraphicRaycaster));
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
        label.text = ResolveInteractionText();
        label.fontSize = 18f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;

        return instance;
    }

    private void ResetInteractionUi()
    {
        DestroyInteractionInstance();
        currentCharacter = null;
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
}
