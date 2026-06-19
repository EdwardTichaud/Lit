using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Unity.Netcode;
using UnityEngine.UI;

// Objet interactif en monde: contenu, verrouillage, pieges et UI d'interaction.
[RequireComponent(typeof(NetworkObject))]
public class InteractableItem : NetworkBehaviour, ICharacterDetectedInteractable, ILitInfluenceReceiver
{
    public enum InteractableCategory
    {
        Container = 0,
        RecoverableItem = 1
    }

    public enum TrapEffectType
    {
        None = 0,
        TeleportCharacter = 1
    }

    [System.Serializable]
    public class LootItemEntry
    {
        [Tooltip("Item stocké.")]
        public Item item;
        [Tooltip("Quantite stockée.")]
        public int quantity = 1;
    }

    [Header("Category")]
    [Tooltip("Indique si cet objet interactif est pense comme un container ou comme un item recuperable.")]
    public InteractableCategory interactableCategory = InteractableCategory.Container;

    [Header("Represented Item")]
    [Tooltip("Item represente par cet objet interactif pour le nom, l'icone, la description et le pickup monde.")]
    public Item representedItem;

    [Header("Stored Content")]
    [Tooltip("Contenu interne si cet objet interactif sert de container.")]
    public List<LootItemEntry> storedItems = new List<LootItemEntry>();
    [Tooltip("Detruit l'objet interactif lorsque son contenu devient vide.")]
    public bool destroyWhenStorageEmpty = false;
    [Tooltip("Autorise le joueur a recuperer le contenu ou l'objet represente depuis le monde.")]
    public bool allowTake = true;
    [Tooltip("Capacite totale du contenu stocke (0 = infini).")]
    public int maxStoredQuantity = 0;

    [Header("Lock")]
    [Tooltip("Si true, l'objet interactif doit etre deverrouille avant interaction.")]
    public bool isLocked = false;
    [Tooltip("Identifiant de serrure requis pour déverrouiller cet objet interactif.")]
    public string lockId;
    [Tooltip("Consomme la cle utilisee lors du déverrouillage.")]
    public bool consumeKeyOnUse = false;
    [Tooltip("Feedback affiche si la bonne cle n'est pas trouvee.")]
    public string lockedNoKeyMessage = "Le conteneur est verrouillé. Il faut la bonne clé.";
    [Tooltip("Feedback affiche lorsque le conteneur est déverrouillé.")]
    public string unlockSuccessMessage = "Le conteneur est déverrouillé.";

    [Header("Lockpick")]
    [Tooltip("Autorise une tentative de crochetage si le personnage n'a pas la bonne cle.")]
    public bool allowLockpick = true;
    [Min(1)]
    [Tooltip("Difficulte du crochetage (DC).")]
    public int lockDifficulty = 12;
    [Tooltip("Reference explicite vers l'item de crochetage. Laisse vide pour utiliser l'ID par defaut.")]
    public Item lockpickToolItem;
    [Tooltip("ID fallback de l'item de crochetage si aucune reference n'est assignee.")]
    public string lockpickToolItemId = "outils_de_crochetage";
    [Tooltip("Message affiche dans la confirmation avant consommation de l'outil.")]
    public string lockpickConfirmationMessage = "Utiliser un outil de crochetage pour tenter d'ouvrir ce coffre ?";
    [Tooltip("Feedback affiche si aucun outil de crochetage n'est disponible.")]
    public string missingLockpickMessage = "Il manque des outils de crochetage.";
    [Tooltip("Feedback de succes apres un crochetage reussi.")]
    public string lockpickSuccessMessage = "Crochetage réussi.";
    [Tooltip("Feedback d'echec apres un crochetage rate.")]
    public string lockpickFailureMessage = "Crochetage raté, votre outil de crochetage se brise...";

    [Header("Trap")]
    [Tooltip("Si true, cet objet interactif possede un piege.")]
    public bool isTrapped = false;
    [Tooltip("Si true, le piege se declenche a l'ouverture. Sinon, il se declenche sur un échec de crochetage.")]
    public bool triggerTrapOnOpen = false;
    [Tooltip("Desarme le piege apres son premier declenchement.")]
    public bool disarmTrapAfterTrigger = true;
    [Tooltip("Type d'effet applique par le piege.")]
    public TrapEffectType trapEffect = TrapEffectType.TeleportCharacter;
    [Tooltip("Point de teleportation utilise par le piege.")]
    public Transform trapTeleportTarget;
    [Tooltip("Applique la rotation du point de teleportation au personnage teleporte.")]
    public bool trapUseTargetRotation = true;
    [Tooltip("Feedback affiche lorsque le piege se declenche.")]
    public string trapTriggeredMessage = "Un piège de téléportation se déclenche !";

    [Header("Break")]
    [Tooltip("Autorise l'action Casser lorsque la prise est desactivee.")]
    public bool allowBreakWhenTakeDisabled = true;
    [Tooltip("Message si l'item ne peut pas etre casse.")]
    public string breakInvalidMessage = "Cet objet ne peut pas etre casse.";
    [Tooltip("Message si l'objet interactif de destination est plein apres casse.")]
    public string breakNoSpaceMessage = "Pas assez de place dans le coffre.";

    [Header("Feedback")]
    [Tooltip("Message si la prise depuis cet objet interactif est interdite.")]
    public string takeBlockedMessage = "Impossible de prendre cet objet.";
    [Tooltip("Message quand tout le contenu est pris.")]
    public string takeAllSuccessMessage = "Objets récupérés.";
    [Tooltip("Message si la destination de depot est pleine.")]
    public string depositNoSpaceMessage = "Pas assez de place dans le coffre.";

    [Header("World UI")]
    [Tooltip("Affiche le panneau d'information monde/local quand cet objet est detecte. Desactive pour les objets caches.")]
    public bool showWorldInteractionUi = true;

    [Header("Light Influence")]
    [SerializeField, Tooltip("Si actif, cet item n'est detectable/interactif que dans une zone d'influence allumee.")]
    private bool requireLitInfluenceForInteraction = true;
    [SerializeField, Tooltip("Autorise les flammes allumees a rendre cet item interactif.")]
    private bool reactToFlameInfluence = true;

    [Header("Interaction Action Box")]
    [Tooltip("ActionBox utilisee par cet objet interactif. Laisse vide pour auto-detecter.")]
    public GameObject actionBox;
    [Tooltip("Offset en UI par rapport au slot selectionne.")]
    public Vector2 actionBoxOffset = Vector2.zero;
    [Tooltip("Duree du fade de l'ActionBox.")]
    public float actionBoxFadeDuration = 0.15f;
    [Tooltip("Met l'alpha a 0 au demarrage.")]
    public bool actionBoxSetAlphaToZeroOnStart = true;
    [Tooltip("Ajoute un CanvasGroup si manquant.")]
    public bool actionBoxAddCanvasGroupIfMissing = true;
    [Tooltip("Desactive les raycasts quand cache.")]
    public bool actionBoxDisableRaycastsWhenHidden = true;
    [Tooltip("Alpha des actions disponibles.")]
    public float actionBoxEnabledAlpha = 1f;
    [Tooltip("Alpha des actions indisponibles.")]
    public float actionBoxDisabledAlpha = 0.25f;

    [Header("Take Quantity UI")]
    [Tooltip("Offset en UI par rapport au slot selectionne.")]
    public Vector2 takeQuantityPanelOffset = Vector2.zero;
    [Tooltip("Format d'affichage (quantite/total).")]
    public string takeQuantityFormat = "{0}/{1}";

    [Header("Interaction")]
    [Tooltip("Collider de reference pour la detection et la validation d'interaction. Laisse vide pour auto-detecter.")]
    public Collider interactionTrigger;
    [Tooltip("Distance maximale a laquelle le personnage peut interagir avec cet objet.")]
    public float interactionMaxDistance = 1.75f;
    [Tooltip("Panel d'inventaire utilise pour deposer/retirer du contenu.")]
    public InventoryPanelController linkedInventoryPanelController;
    [SerializeField, HideInInspector]
    private BuildingInfoInteractable recoverableWorldInfo;

    private readonly List<GameObject> charactersInRange = new List<GameObject>();
    private readonly Dictionary<GameObject, int> characterColliderCounts = new Dictionary<GameObject, int>();
    private GameObject currentCharacter;
    private bool lootOpen;
    private bool useSelfTriggerEvents;
    private bool depositInventoryOpen;
    private int suppressReturnFrame = -1;
    private bool takeQuantityActive;
    private int takeQuantityMax;
    private Item takeQuantityItem;
    private LootItemEntry takeQuantityEntry;
    private QuantityBox quantityBox;
    private LootSlotUI currentFocusedSlot;
    private bool squadInputLocked;
    private readonly List<LootSlotUI> lootSlots = new List<LootSlotUI>();
    private int currentSlotIndex;
    private int lastMoveDirection;
    private float nextMoveTime;
    private bool cursorDirty;
    private CanvasGroup actionBoxCanvasGroup;
    private Coroutine actionBoxFadeRoutine;
    private bool actionBoxVisible;
    private readonly List<ActionBoxEntry> actionBoxEntries = new List<ActionBoxEntry>();
    private readonly HashSet<int> activeLitInfluenceSourceIds = new HashSet<int>();

    private readonly NetworkList<NetItemStack> netLootItems = new NetworkList<NetItemStack>(
        null, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<bool> netIsLocked = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<bool> netTrapTriggered = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private const int MinLockDifficulty = 1;
    private const int MaxLockDifficulty = 30;
    private const int MinDexterityBonus = -5;
    private const int MaxDexterityBonus = 10;
    private bool applyingNetLoot;
    private bool unlockAttemptInProgress;
    private bool trapTriggered;

    private void Awake()
    {
        RuntimeOutlineUtility.EnsureOutlineTargets(gameObject);
        InitializeInteractionTrigger();
        RefreshRecoverableWorldInfo();

        LootUISettings settings = GetSettings();
        if (settings != null)
        {
            settings.InitializePanel();
        }

        InitializeActionBox();
    }

    public bool CanBeDetectedBy(SquadCharacterController controller)
    {
        return controller != null && isActiveAndEnabled && CanInteractInCurrentInfluence();
    }

    public Collider GetInteractionDetectionCollider()
    {
        return ResolveInteractionColliderReference();
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
        return interactableCategory == InteractableCategory.RecoverableItem ? 120 : 100;
    }

    public void SetDetectedCharacter(GameObject character)
    {
        if (character != null && !CanInteractInCurrentInfluence())
        {
            character = null;
        }

        if (currentCharacter == character)
        {
            ForwardDetectedCharacterToRecoverableInfo(character);
            return;
        }

        GameObject previousCharacter = currentCharacter;
        currentCharacter = character;
        if (previousCharacter != null && currentCharacter == null)
        {
            HandleCharacterNoLongerInRange();
        }

        ForwardDetectedCharacterToRecoverableInfo(character);
    }

    private void OnEnable()
    {
        RefreshRecoverableWorldInfo();
        LocalInputRouter.EnsureInitialized();
        LocalInputRouter.Interact += OnInteractPerformed;
        LocalInputRouter.TakeAll += OnTakeAllPerformed;
        LocalInputRouter.Return += OnReturnPerformed;
        LocalInputRouter.TriggerMunin += OnTriggerMuninPerformed;
    }

    private void OnDisable()
    {
        LocalInputRouter.Interact -= OnInteractPerformed;
        LocalInputRouter.TakeAll -= OnTakeAllPerformed;
        LocalInputRouter.Return -= OnReturnPerformed;
        LocalInputRouter.TriggerMunin -= OnTriggerMuninPerformed;

        InputFocusStack.Pop(this);
        CloseLoot();
        HideActionBoxImmediate();
        charactersInRange.Clear();
        characterColliderCounts.Clear();
        currentCharacter = null;
        depositInventoryOpen = false;
        unlockAttemptInProgress = false;
        ConfirmationManager.Dismiss(this);
        activeLitInfluenceSourceIds.Clear();
    }

    public void OnLitInfluenceEnter(LitInfluenceInfo info)
    {
        if (!ShouldReactToLitInfluence(info) || info.SourceId == 0)
        {
            return;
        }

        activeLitInfluenceSourceIds.Add(info.SourceId);
    }

    public void OnLitInfluenceStay(LitInfluenceInfo info)
    {
        if (!ShouldReactToLitInfluence(info) || info.SourceId == 0)
        {
            return;
        }

        activeLitInfluenceSourceIds.Add(info.SourceId);
    }

    public void OnLitInfluenceExit(LitInfluenceInfo info)
    {
        if (info.SourceId == 0 || !activeLitInfluenceSourceIds.Remove(info.SourceId))
        {
            return;
        }

        if (!CanInteractInCurrentInfluence())
        {
            HandleLitInfluenceLostForInteraction();
        }
    }

    public override void OnNetworkSpawn()
    {
        netLootItems.OnListChanged += OnNetLootChanged;
        netIsLocked.OnValueChanged += OnNetIsLockedChanged;
        netTrapTriggered.OnValueChanged += OnNetTrapTriggeredChanged;
        if (IsServer)
        {
            netIsLocked.Value = isLocked;
            netTrapTriggered.Value = trapTriggered;
            SyncNetFromLootItems();
        }
        else
        {
            isLocked = netIsLocked.Value;
            trapTriggered = netTrapTriggered.Value;
            ApplyLootFromNet();
        }

        RefreshRecoverableWorldInfo();
    }

    public override void OnNetworkDespawn()
    {
        netLootItems.OnListChanged -= OnNetLootChanged;
        netIsLocked.OnValueChanged -= OnNetIsLockedChanged;
        netTrapTriggered.OnValueChanged -= OnNetTrapTriggeredChanged;
    }

    private void LateUpdate()
    {
        UpdateCurrentCharacter();

        if (lootOpen && HasInputFocus())
        {
            UpdateCursorVisual();
            if (actionBoxVisible)
            {
                PositionActionBox();
            }
        }
    }

    private void Update()
    {
        // Input loop: only when loot is open and this container has focus.
        if (!lootOpen)
        {
            return;
        }

        if (!HasInputFocus())
        {
            return;
        }

        if (depositInventoryOpen)
        {
            return;
        }

        if (takeQuantityActive)
        {
            HandleTakeQuantityInput();
            return;
        }

        HandleLootNavigation();
    }

    private LootUISettings GetSettings(bool logWarning = false)
    {
        LootUISettings settings = LootUISettings.Instance;

        if (logWarning && settings == null)
        {
            Debug.LogWarning("InteractableItem: LootUISettings manquant.");
        }

        return settings;
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

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        if (!CanProcessInteract())
        {
            return;
        }

        if (!CanInteractInCurrentInfluence())
        {
            return;
        }

        UpdateCurrentCharacter();
        if (currentCharacter != null)
        {
            LocalInputRouter.ConsumeInteract();
        }

        if (takeQuantityActive)
        {
            ConfirmTakeQuantity();
            return;
        }

        HandleInteract();
    }

    private void OnTakeAllPerformed(InputAction.CallbackContext context)
    {
        if (!HasInputFocus())
        {
            return;
        }

        if (!CanInteractInCurrentInfluence())
        {
            return;
        }

        HandleTakeAll();
    }

    private void OnReturnPerformed(InputAction.CallbackContext context)
    {
        if (!HasInputFocus())
        {
            return;
        }

        if (!lootOpen)
        {
            return;
        }

        if (depositInventoryOpen)
        {
            return;
        }

        if (Time.frameCount == suppressReturnFrame)
        {
            return;
        }

        if (takeQuantityActive)
        {
            CancelTakeQuantity();
            return;
        }

        if (actionBoxVisible)
        {
            HideActionBox();
            return;
        }

        CloseLoot();
    }

    private void OnTriggerMuninPerformed(InputAction.CallbackContext context)
    {
        if (!HasInputFocus())
        {
            return;
        }

        if (!lootOpen)
        {
            return;
        }

        if (depositInventoryOpen)
        {
            return;
        }

        if (!CanInteractInCurrentInfluence())
        {
            return;
        }

        LocalInputRouter.ConsumeTriggerMunin();
        HideActionBox();

        UpdateCurrentCharacter();
        if (currentCharacter == null)
        {
            return;
        }

        InventoryPanelController inventory = GetInventoryPanelController();
        if (inventory == null)
        {
            return;
        }

        if (inventory.TryOpenForLootDeposit(this))
        {
            depositInventoryOpen = true;
        }
    }

    private void HandleInteract()
    {
        if (!CanInteractInCurrentInfluence())
        {
            return;
        }

        if (!lootOpen && InputFocusStack.HasAnyFocus())
        {
            return;
        }

        if (unlockAttemptInProgress)
        {
            return;
        }

        if (depositInventoryOpen)
        {
            return;
        }

        UpdateCurrentCharacter();
        if (currentCharacter == null)
        {
            return;
        }

        LootUISettings settings = GetSettings();
        if (lootOpen)
        {
            if (!allowTake)
            {
                if (allowBreakWhenTakeDisabled)
                {
                    if (actionBoxVisible)
                    {
                        if (TryBreakFocusedItem())
                        {
                            return;
                        }
                    }
                    else
                    {
                        ShowActionBox();
                    }
                }

                return;
            }

            if (TryTakeFocusedItem())
            {
                return;
            }

            if (settings != null && settings.toggleLootOnInteract)
            {
                CloseLoot();
            }

            return;
        }

        if (IsLockedForInteraction())
        {
            if (IsNetworked() && !IsServer)
            {
                HandleLockedInteractClient();
                return;
            }

            SquadCharacterController controller = GetCurrentCharacterController();
            if (TryUnlockWithKey(controller, out bool inventoryChanged))
            {
                PlayActionAudio(ActionAudioCue.InventoryUnlock);
                if (inventoryChanged)
                {
                    SyncNetworkInventoryForCurrentCharacter();
                }

                if (TryTriggerTrapOnOpen(controller, out string trapFeedback))
                {
                    ShowActionFeedback(trapFeedback);
                    return;
                }

                CompletePrimaryInteraction();
                return;
            }

            if (!CanOfferLockpick(controller, out int availableTools, out string lockpickFeedback))
            {
                ShowActionFeedback(lockpickFeedback);
                return;
            }

            BeginLockpickConfirmation(availableTools);
            return;
        }

        if (IsNetworked() && !IsServer && CanTriggerTrapOnOpen())
        {
            unlockAttemptInProgress = true;
            RequestUnlockAndOpenServerRpc(false);
            return;
        }

        if (TryTriggerTrapOnOpen(GetCurrentCharacterController(), out string openTrapFeedback))
        {
            ShowActionFeedback(openTrapFeedback);
            return;
        }

        CompletePrimaryInteraction();
    }

    private void HandleLockedInteractClient()
    {
        SquadCharacterController controller = GetCurrentCharacterController();
        if (controller == null)
        {
            ShowActionFeedback(GetMissingKeyFeedback());
            return;
        }

        if (CanUnlockWithKey(controller))
        {
            unlockAttemptInProgress = true;
            RequestUnlockAndOpenServerRpc(false);
            return;
        }

        if (!CanOfferLockpick(controller, out int availableTools, out string lockpickFeedback))
        {
            ShowActionFeedback(lockpickFeedback);
            return;
        }

        BeginLockpickConfirmation(availableTools);
    }

    private void BeginLockpickConfirmation(int availableTools)
    {
        bool shown = ConfirmationManager.TryShow(
            this,
            BuildLockpickConfirmationMessage(availableTools),
            OnLockpickConfirmed,
            OnLockpickCancelled,
            "Utiliser",
            "Annuler",
            "Crochetage",
            "InteractableItem.Lockpick");

        if (shown)
        {
            PlayActionAudio(ActionAudioCue.UiOpen);
            Debug.Log($"[Lockpick] confirmation_shown container='{name}' availableTools={availableTools}", this);
            return;
        }

        ShowActionFeedback("Confirmation de crochetage indisponible.");
    }

    private void OnLockpickConfirmed()
    {
        if (this == null || !isActiveAndEnabled)
        {
            return;
        }

        UpdateCurrentCharacter();
        SquadCharacterController controller = GetCurrentCharacterController();
        if (controller == null)
        {
            ShowActionFeedback(GetMissingLockpickFeedback());
            return;
        }

        if (IsNetworked() && !IsServer)
        {
            unlockAttemptInProgress = true;
            RequestUnlockAndOpenServerRpc(true);
            return;
        }

        if (TryUnlockWithKey(controller, out bool keyInventoryChanged))
        {
            PlayActionAudio(ActionAudioCue.InventoryUnlock);
            if (keyInventoryChanged)
            {
                SyncNetworkInventoryForCurrentCharacter();
            }

            if (TryTriggerTrapOnOpen(controller, out string keyTrapFeedback))
            {
                ShowActionFeedback(keyTrapFeedback);
                return;
            }

            CompletePrimaryInteraction();
            return;
        }

        if (!TryPerformLockpickAttempt(controller, out string feedback, out bool inventoryChanged))
        {
            if (inventoryChanged)
            {
                SyncNetworkInventoryForCurrentCharacter();
            }

            PlayActionAudio(ActionAudioCue.InventoryLockpickFailure);
            ShowActionFeedback(feedback);
            return;
        }

        if (inventoryChanged)
        {
            SyncNetworkInventoryForCurrentCharacter();
        }

        if (TryTriggerTrapOnOpen(controller, out string openTrapFeedback))
        {
            ShowActionFeedback(CombineFeedbackMessages(feedback, openTrapFeedback));
            return;
        }

        PlayActionAudio(ActionAudioCue.InventoryLockpickSuccess);
        ShowActionFeedback(feedback);
        CompletePrimaryInteraction();
    }

    private void OnLockpickCancelled()
    {
        PlayActionAudio(ActionAudioCue.UiCancel);
        Debug.Log($"[Lockpick] confirmation_cancelled container='{name}'", this);
    }

    private void HandleTakeAll()
    {
        UpdateCurrentCharacter();
        if (currentCharacter == null || !lootOpen)
        {
            return;
        }

        if (depositInventoryOpen)
        {
            return;
        }

        TakeAllItems();
    }

    private void UpdateCurrentCharacter()
    {
        if (UsesControllerDrivenDetection())
        {
            return;
        }

        GameObject previousCharacter = currentCharacter;
        PruneCharactersInRange();

        if (charactersInRange.Count == 0)
        {
            currentCharacter = null;
            if (previousCharacter != null)
            {
                HandleCharacterNoLongerInRange();
            }
            return;
        }

        GameObject controlled = LocalPlayerUtils.GetControlledCharacter();
        if (controlled != null)
        {
            currentCharacter = charactersInRange.Contains(controlled) ? controlled : null;
            if (previousCharacter != null && currentCharacter == null)
            {
                HandleCharacterNoLongerInRange();
            }
            return;
        }

        currentCharacter = charactersInRange[0];
    }

    private void PruneCharactersInRange()
    {
        for (int i = charactersInRange.Count - 1; i >= 0; i--)
        {
            GameObject character = charactersInRange[i];
            if (character != null && IsCharacterInRange(character.transform))
            {
                continue;
            }

            charactersInRange.RemoveAt(i);
            if (character != null)
            {
                characterColliderCounts.Remove(character);
            }
        }
    }

    private void HandleCharacterNoLongerInRange()
    {
        HideActionBoxImmediate();
        ConfirmationManager.Dismiss(this);
        unlockAttemptInProgress = false;

        if (!lootOpen)
        {
            return;
        }

        LootUISettings settings = GetSettings();
        if (settings != null && settings.closeLootWhenLeaving)
        {
            CloseLoot();
        }
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

        UpdateCurrentCharacter();
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
        UpdateCurrentCharacter();

        if (currentCharacter == null)
        {
            HandleCharacterNoLongerInRange();
        }
    }

    public void NotifyTriggerEnter(Collider other)
    {
        HandleCharacterEnter(other);
    }

    public void NotifyTriggerExit(Collider other)
    {
        HandleCharacterExit(other);
    }

    private void InitializeInteractionTrigger()
    {
        interactionTrigger = ResolveInteractionColliderReference();
        useSelfTriggerEvents = false;

        if (interactionTrigger == null)
        {
            Debug.LogWarning("InteractableItem: aucun collider trouve pour l'interaction.");
        }
    }

    private Collider ResolveInteractionColliderReference()
    {
        interactionTrigger = CharacterInteractionDetection.ResolveInteractionCollider(this, interactionTrigger);
        return interactionTrigger;
    }

    private static bool UsesControllerDrivenDetection()
    {
        return true;
    }

    private void ForwardDetectedCharacterToRecoverableInfo(GameObject character)
    {
        if (!showWorldInteractionUi || recoverableWorldInfo == null || !recoverableWorldInfo.isActiveAndEnabled)
        {
            return;
        }

        recoverableWorldInfo.SetDetectedCharacter(character);
    }

    private bool CanInteractInCurrentInfluence()
    {
        return !requireLitInfluenceForInteraction || activeLitInfluenceSourceIds.Count > 0;
    }

    private bool ShouldReactToLitInfluence(LitInfluenceInfo info)
    {
        switch (info.SourceKind)
        {
            case LitInfluenceSourceKind.Flame:
            case LitInfluenceSourceKind.AncientFlame:
                return reactToFlameInfluence;

            default:
                return false;
        }
    }

    private void HandleLitInfluenceLostForInteraction()
    {
        GameObject previousCharacter = currentCharacter;
        currentCharacter = null;
        ForwardDetectedCharacterToRecoverableInfo(null);

        if (lootOpen)
        {
            CloseLoot();
            return;
        }

        if (previousCharacter != null)
        {
            HandleCharacterNoLongerInRange();
        }
    }

    private void OpenLoot()
    {
        LootUISettings settings = GetSettings(true);
        if (settings == null || settings.lootPanel == null)
        {
            Debug.LogWarning("InteractableItem: aucun lootPanel defini.");
            return;
        }

        settings.OpenPanel();
        PlayActionAudio(ActionAudioCue.InventoryOpen);
        HideActionBoxImmediate();
        lootOpen = true;
        InputFocusStack.Push(this);
        SetSquadInputLock(true);
        settings.UpdateContainerHeader(this);

        RebuildLootSlots(null);
    }

    private void CompletePrimaryInteraction()
    {
        if (interactableCategory == InteractableCategory.RecoverableItem)
        {
            TakeAllItems();
            return;
        }

        OpenLoot();
    }

    private void CloseLoot()
    {
        if (lootOpen)
        {
            PlayActionAudio(ActionAudioCue.InventoryClose);
        }

        HideActionBoxImmediate();
        CloseQuantityBox();
        ResetTakeQuantityState();
        if (depositInventoryOpen)
        {
            InventoryPanelController inventory = GetInventoryPanelController();
            if (inventory != null)
            {
                inventory.CloseDepositInventory();
            }
            depositInventoryOpen = false;
        }

        LootUISettings settings = GetSettings();
        if (settings != null)
        {
            settings.ClosePanel();
        }

        lootOpen = false;
        InputFocusStack.Pop(this);
        SetSquadInputLock(false);
        currentFocusedSlot = null;
        currentSlotIndex = 0;
        lastMoveDirection = 0;
        nextMoveTime = 0f;
        cursorDirty = false;
        if (settings != null)
        {
            settings.UpdateDescription(null);
            settings.HideCursor();
        }
    }

    private bool HasInputFocus()
    {
        return InputFocusStack.HasFocus(this);
    }

    private bool CanProcessInteract()
    {
        if (InputFocusStack.HasFocus(this))
        {
            return true;
        }

        return !lootOpen && !InputFocusStack.HasAnyFocus();
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

    private GameObject CreateTextEntry(Transform parent)
    {
        GameObject obj = new GameObject("LootItem");
        if (parent != null)
        {
            obj.transform.SetParent(parent, false);
        }

        TextMeshProUGUI text = obj.AddComponent<TextMeshProUGUI>();
        text.fontSize = 24f;
        text.alignment = TextAlignmentOptions.Left;
        return obj;
    }

    private void SetEntryText(GameObject entry, string text)
    {
        if (entry == null)
        {
            return;
        }

        TextMeshProUGUI tmp = FindEntryQuantityText(entry);
        if (tmp != null)
        {
            tmp.text = text;
        }
    }

    private TextMeshProUGUI FindEntryQuantityText(GameObject entry)
    {
        TextMeshProUGUI[] texts = entry.GetComponentsInChildren<TextMeshProUGUI>(true);
        if (texts == null || texts.Length == 0)
        {
            return null;
        }

        for (int i = 0; i < texts.Length; i++)
        {
            TextMeshProUGUI tmp = texts[i];
            if (tmp == null)
            {
                continue;
            }

            string name = tmp.name;
            if (name.IndexOf("quantity", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("count", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("qty", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return tmp;
            }
        }

        return texts[0];
    }

    private void SetEntrySprite(GameObject entry, Item item)
    {
        if (entry == null)
        {
            return;
        }

        Image targetImage = FindEntrySpriteImage(entry);
        if (targetImage == null)
        {
            return;
        }

        Sprite sprite = null;
        if (item != null)
        {
            sprite = item.itemSprite;
        }

        targetImage.sprite = sprite;
        if (sprite == null)
        {
            targetImage.enabled = false;
        }
        else
        {
            targetImage.enabled = true;
        }
    }

    private Image FindEntrySpriteImage(GameObject entry)
    {
        Image[] images = entry.GetComponentsInChildren<Image>(true);
        if (images == null || images.Length == 0)
        {
            return null;
        }

        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null)
            {
                continue;
            }

            string name = image.name;
            if (name.IndexOf("itemsprite", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("icon", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return image;
            }
        }

        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null)
            {
                continue;
            }

            string name = image.name;
            if (name.IndexOf("frame", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("font", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("background", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("bg", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                continue;
            }

            return image;
        }

        return images[0];
    }

    public void FocusSlot(LootSlotUI slot)
    {
        if (slot == null || slot.SlotRect == null)
        {
            return;
        }

        currentFocusedSlot = slot;
        cursorDirty = true;
        if (lootSlots.Count > 0)
        {
            int index = lootSlots.IndexOf(slot);
            if (index >= 0)
            {
                currentSlotIndex = index;
            }
        }

        LootUISettings settings = GetSettings();
        if (settings != null)
        {
            settings.UpdateDescription(slot.Item);
        }
    }

    private void RebuildLootSlots(Item preferredItem, int preferredIndex = -1)
    {
        currentFocusedSlot = null;
        currentSlotIndex = 0;
        lootSlots.Clear();

        LootUISettings settings = GetSettings(true);
        if (settings == null || settings.lootItemsParent == null)
        {
            Debug.LogWarning("InteractableItem: lootItemsParent manquant.");
            return;
        }

        Transform itemsParent = settings.lootItemsParent;
        GameObject itemPrefab = settings.lootItemPrefab;

        for (int i = itemsParent.childCount - 1; i >= 0; i--)
        {
            Destroy(itemsParent.GetChild(i).gameObject);
        }

        LootSlotUI firstSlot = null;
        LootSlotUI preferredSlot = null;
        for (int i = 0; i < storedItems.Count; i++)
        {
            LootItemEntry entryData = storedItems[i];
            if (entryData == null || entryData.item == null)
            {
                continue;
            }

            int quantity = Mathf.Max(0, entryData.quantity);
            if (quantity <= 0)
            {
                continue;
            }

            Item item = entryData.item;
            GameObject entry = CreateInstance(itemPrefab, itemsParent);
            if (entry == null)
            {
                entry = CreateTextEntry(itemsParent);
            }

            if (entry != null)
            {
                SetEntryText(entry, quantity.ToString());
                SetEntrySprite(entry, item);
                LootSlotUI slotUi = entry.GetComponent<LootSlotUI>();
                if (slotUi == null)
                {
                    slotUi = entry.AddComponent<LootSlotUI>();
                }
                slotUi.Initialize(this, entryData);
                lootSlots.Add(slotUi);
                if (firstSlot == null)
                {
                    firstSlot = slotUi;
                }
                if (preferredSlot == null && preferredItem != null && preferredItem == item)
                {
                    preferredSlot = slotUi;
                }
            }
        }

        if (lootSlots.Count > 0 && preferredIndex >= 0)
        {
            int clampedIndex = Mathf.Clamp(preferredIndex, 0, lootSlots.Count - 1);
            preferredSlot = lootSlots[clampedIndex];
        }

        if (preferredSlot != null)
        {
            FocusSlot(preferredSlot);
        }
        else if (firstSlot != null)
        {
            FocusSlot(firstSlot);
        }
        else
        {
            if (settings != null)
            {
                settings.UpdateDescription(null);
            }
            if (settings != null)
            {
                settings.HideCursor();
            }
        }
    }

    private void UpdateCursorVisual()
    {
        LootUISettings settings = GetSettings();
        if (settings == null)
        {
            return;
        }

        if (currentFocusedSlot == null)
        {
            GetFocusedSlot();
        }

        LootSlotUI slot = currentFocusedSlot;
        if (slot == null || slot.SlotRect == null)
        {
            settings.HideCursor();
            cursorDirty = false;
            return;
        }

        if (cursorDirty)
        {
            Canvas.ForceUpdateCanvases();
            RectTransform itemsRect = settings.lootItemsParent != null ? settings.lootItemsParent as RectTransform : null;
            if (itemsRect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(itemsRect);
            }
        }

        Transform itemsParent = settings.lootItemsParent;
        Transform cursorParent = itemsParent != null ? itemsParent.parent : slot.SlotRect.parent;
        RectTransform cursor = settings.EnsureSlotCursor(cursorParent);
        if (cursor == null)
        {
            cursorDirty = false;
            return;
        }

        cursor.gameObject.SetActive(true);
        if (cursorParent != null)
        {
            cursor.SetParent(cursorParent, false);
        }
        cursor.SetAsLastSibling();
        cursor.pivot = new Vector2(0.5f, 0.5f);
        cursor.position = slot.SlotRect.position;
        Vector2 size = slot.SlotRect.rect.size;
        Vector2 padding = settings.cursorPadding;
        cursor.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x + padding.x);
        cursor.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y + padding.y);

        cursorDirty = false;
    }

    private bool TryTakeFocusedItem()
    {
        if (!allowTake)
        {
            ShowActionFeedback(takeBlockedMessage);
            return false;
        }

        LootSlotUI focusedSlot = GetFocusedSlot();
        if (focusedSlot == null || focusedSlot.Entry == null)
        {
            return false;
        }

        LootItemEntry entry = focusedSlot.Entry;
        Item item = entry.item;
        if (item == null)
        {
            return false;
        }

        if (!item.CanTakeFromContainer(this, out string reason))
        {
            ShowActionFeedback(reason);
            return false;
        }

        int quantity = Mathf.Max(0, entry.quantity);
        if (quantity <= 0)
        {
            return false;
        }

        if (quantity > 1)
        {
            BeginTakeQuantity(entry, quantity);
            return true;
        }

        return PerformTake(entry, 1);
    }

    private void BeginTakeQuantity(LootItemEntry entry, int maxQuantity)
    {
        if (entry == null || entry.item == null)
        {
            return;
        }

        QuantityBox box = GetQuantityBox();
        if (box == null)
        {
            PerformTake(entry, maxQuantity);
            return;
        }

        takeQuantityEntry = entry;
        takeQuantityItem = entry.item;
        takeQuantityMax = Mathf.Max(1, maxQuantity);
        int startQuantity = Mathf.Clamp(entry.quantity, 1, takeQuantityMax);
        takeQuantityActive = true;
        box.Open(currentFocusedSlot != null ? currentFocusedSlot.SlotRect : null,
            takeQuantityPanelOffset,
            startQuantity,
            takeQuantityMax,
            "{0}/{1}");
    }

    private void CancelTakeQuantity()
    {
        if (!takeQuantityActive)
        {
            return;
        }

        takeQuantityActive = false;
        CloseQuantityBox();
        ResetTakeQuantityState();
    }

    private void ConfirmTakeQuantity()
    {
        if (!takeQuantityActive)
        {
            return;
        }

        QuantityBox box = GetQuantityBox();
        int quantity = box != null ? box.CurrentQuantity : 1;
        quantity = Mathf.Clamp(quantity, 1, takeQuantityMax);
        LootItemEntry entry = takeQuantityEntry;
        takeQuantityActive = false;
        CloseQuantityBox();
        ResetTakeQuantityState();
        PerformTake(entry, quantity);
    }

    private bool PerformTake(LootItemEntry entry, int quantity)
    {
        if (entry == null || entry.item == null || quantity <= 0)
        {
            return false;
        }

        if (IsLockedForInteraction())
        {
            ShowActionFeedback(GetMissingKeyFeedback());
            return false;
        }

        if (IsNetworked() && !IsServer)
        {
            RequestTakeServerRpc(ItemIdUtils.GetItemId(entry.item), quantity);
            return true;
        }

        Item item = entry.item;
        if (!TryAddItemToCurrentCharacter(item, quantity))
        {
            return false;
        }

        entry.quantity = Mathf.Max(0, entry.quantity - quantity);
        if (entry.quantity <= 0)
        {
            storedItems.Remove(entry);
        }

        RebuildLootSlots(item, currentSlotIndex);
        HandleEmptyContainer();
        SyncNetFromLootItems();
        SyncNetworkInventoryForCurrentCharacter();
        PlayActionAudio(ActionAudioCue.InventoryTake);
        ShowActionFeedback(item.GetTakeSuccessMessage());
        return true;
    }

    private void HandleTakeQuantityInput()
    {
        if (!takeQuantityActive)
        {
            return;
        }

        LootUISettings settings = GetSettings();
        if (settings == null)
        {
            return;
        }

        Vector2 moveInput = LocalInputRouter.MoveValue;
        QuantityBox box = GetQuantityBox();
        if (box == null)
        {
            return;
        }

        box.HandleInput(moveInput, settings.moveDeadzone, settings.initialRepeatDelay, settings.repeatInterval);
    }

    private void ResetTakeQuantityState()
    {
        takeQuantityActive = false;
        takeQuantityMax = 1;
        takeQuantityItem = null;
        takeQuantityEntry = null;
    }

    private bool TryBreakFocusedItem()
    {
        if (IsLockedForInteraction())
        {
            ShowActionFeedback(GetMissingKeyFeedback());
            return false;
        }

        LootSlotUI focusedSlot = GetFocusedSlot();
        if (focusedSlot == null || focusedSlot.Entry == null)
        {
            return false;
        }

        LootItemEntry entry = focusedSlot.Entry;
        Item item = entry.item;
        if (item == null)
        {
            return false;
        }

        if (IsNetworked() && !IsServer)
        {
            RequestBreakServerRpc(ItemIdUtils.GetItemId(item));
            return true;
        }

        if (!item.HasBreakResults())
        {
            ShowBreakFeedback(breakInvalidMessage);
            return false;
        }

        int totalResults = GetBreakResultTotal(item);
        if (maxStoredQuantity > 0)
        {
            int remaining = GetRemainingCapacity();
            int effectiveRemaining = remaining + 1;
            if (totalResults > effectiveRemaining)
            {
                ShowBreakFeedback(breakNoSpaceMessage);
                return false;
            }
        }

        entry.quantity = Mathf.Max(0, entry.quantity - 1);
        if (entry.quantity <= 0)
        {
            storedItems.Remove(entry);
        }

        ApplyBreakResults(item);
        RebuildLootSlots(null, currentSlotIndex);
        HandleEmptyContainer();
        SyncNetFromLootItems();
        PlayActionAudio(ActionAudioCue.InventoryBreak);
        ShowBreakFeedback(item.GetBreakSuccessMessage());
        return true;
    }

    private int GetBreakResultTotal(Item item)
    {
        if (item == null || item.breakResults == null)
        {
            return 0;
        }

        int total = 0;
        for (int i = 0; i < item.breakResults.Count; i++)
        {
            Item.BreakResult result = item.breakResults[i];
            if (result == null || result.item == null)
            {
                continue;
            }

            int amount = Mathf.Max(0, result.quantity);
            total += amount;
        }

        return total;
    }

    private void ApplyBreakResults(Item item)
    {
        if (item == null || item.breakResults == null)
        {
            return;
        }

        for (int i = 0; i < item.breakResults.Count; i++)
        {
            Item.BreakResult result = item.breakResults[i];
            if (result == null || result.item == null)
            {
                continue;
            }

            int amount = Mathf.Max(0, result.quantity);
            if (amount <= 0)
            {
                continue;
            }

            AddItemsInternal(result.item, amount);
        }
    }

    private void AddItemsInternal(Item item, int quantity)
    {
        if (item == null || quantity <= 0)
        {
            return;
        }

        if (storedItems == null)
        {
            storedItems = new List<LootItemEntry>();
        }

        LootItemEntry existing = null;
        for (int i = 0; i < storedItems.Count; i++)
        {
            LootItemEntry entry = storedItems[i];
            if (entry != null && entry.item == item)
            {
                existing = entry;
                break;
            }
        }

        if (existing != null)
        {
            existing.quantity = Mathf.Max(0, existing.quantity + quantity);
        }
        else
        {
            storedItems.Add(new LootItemEntry { item = item, quantity = quantity });
        }
    }

    private void ShowBreakFeedback(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        InfoBoxUI.TryShow(message);
    }

    private void InitializeActionBox()
    {
        if (actionBox == null)
        {
            LootUISettings settings = GetSettings();
            if (settings != null && settings.lootPanel != null)
            {
                Transform found = settings.lootPanel.transform.Find("ActionBox");
                if (found != null)
                {
                    actionBox = found.gameObject;
                }
            }
        }

        if (actionBox == null)
        {
            return;
        }

        actionBoxCanvasGroup = GetActionBoxCanvasGroup();
        if (actionBoxCanvasGroup != null && actionBoxSetAlphaToZeroOnStart)
        {
            SetActionBoxAlpha(0f);
        }

        BuildActionBoxEntries();
        HideActionBoxCursor();
        actionBoxVisible = false;
    }

    private QuantityBox GetQuantityBox()
    {
        if (quantityBox == null)
        {
            quantityBox = QuantityBox.Resolve();
        }

        return quantityBox;
    }

    private void CloseQuantityBox()
    {
        QuantityBox box = GetQuantityBox();
        if (box != null)
        {
            box.Close();
        }
    }

    private void EnsureActionBox()
    {
        if (actionBox == null)
        {
            LootUISettings settings = GetSettings();
            if (settings != null && settings.lootPanel != null)
            {
                Transform found = settings.lootPanel.transform.Find("ActionBox");
                if (found != null)
                {
                    actionBox = found.gameObject;
                }
            }
        }

        if (actionBox == null)
        {
            GameObject source = GameObject.Find("ActionBox");
            if (source != null)
            {
                Transform parent = null;
                LootUISettings settings = GetSettings();
                if (settings != null && settings.lootPanel != null)
                {
                    parent = settings.lootPanel.transform.parent;
                }

                actionBox = Instantiate(source, parent != null ? parent : source.transform.parent);
                actionBox.name = "LootActionBox";
            }
        }

        if (actionBox == null)
        {
            return;
        }

        actionBoxCanvasGroup = GetActionBoxCanvasGroup();
        if (actionBoxCanvasGroup != null && actionBoxSetAlphaToZeroOnStart)
        {
            SetActionBoxAlpha(0f);
        }

        BuildActionBoxEntries();
        HideActionBoxCursor();
    }

    private void ShowActionBox()
    {
        if (actionBoxVisible)
        {
            return;
        }

        EnsureActionBox();
        if (actionBox == null)
        {
            return;
        }

        actionBox.SetActive(true);
        PositionActionBox();
        ApplyActionBoxVisuals();
        FadeActionBoxTo(1f, actionBoxFadeDuration);
        actionBoxVisible = true;
    }

    private void HideActionBox()
    {
        if (!actionBoxVisible)
        {
            return;
        }

        FadeActionBoxTo(0f, actionBoxFadeDuration);
        actionBoxVisible = false;
    }

    private void HideActionBoxImmediate()
    {
        if (actionBox == null)
        {
            return;
        }

        FadeActionBoxTo(0f, 0f);
        actionBoxVisible = false;
    }

    private void PositionActionBox()
    {
        if (actionBox == null || currentFocusedSlot == null || currentFocusedSlot.SlotRect == null)
        {
            return;
        }

        RectTransform actionRect = actionBox.GetComponent<RectTransform>();
        RectTransform slotRect = currentFocusedSlot.SlotRect;
        Transform parent = actionRect != null ? actionRect.parent : actionBox.transform.parent;
        RectTransform parentRect = parent as RectTransform;

        Canvas canvas = actionBox.GetComponentInParent<Canvas>();
        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, slotRect.position);
        if (parentRect != null
            && RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPoint, uiCamera, out Vector2 localPoint))
        {
            Vector2 anchored = localPoint + actionBoxOffset;
            if (actionRect != null)
            {
                actionRect.anchoredPosition = anchored;
            }
            else
            {
                actionBox.transform.localPosition = anchored;
            }
            return;
        }

        Vector3 fallbackWorld = slotRect.position + (Vector3)actionBoxOffset;
        actionBox.transform.position = fallbackWorld;
    }

    private void BuildActionBoxEntries()
    {
        actionBoxEntries.Clear();
        if (actionBox == null)
        {
            return;
        }

        Transform container = actionBox.transform.Find("ActionBox_Frame");
        if (container == null)
        {
            container = actionBox.transform;
        }

        EnsureBreakActionBoxEntry(container);

        for (int i = 0; i < container.childCount; i++)
        {
            Transform child = container.GetChild(i);
            if (child == null)
            {
                continue;
            }

            string name = child.name;
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            if (name.IndexOf("ActionBox_", System.StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            if (name.IndexOf("_Frame", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("_Text", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Cursor", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                continue;
            }

            RectTransform rect = child as RectTransform;
            Image frame = FindActionBoxFrame(child);
            TextMeshProUGUI label = FindActionBoxLabel(child);
            ActionBoxEntry entry = new ActionBoxEntry(rect, frame, label, name);
            actionBoxEntries.Add(entry);
        }
    }

    private void ApplyActionBoxVisuals()
    {
        for (int i = 0; i < actionBoxEntries.Count; i++)
        {
            ActionBoxEntry entry = actionBoxEntries[i];
            bool isBreak = entry.Name.IndexOf("casser", System.StringComparison.OrdinalIgnoreCase) >= 0
                || entry.Name.IndexOf("break", System.StringComparison.OrdinalIgnoreCase) >= 0;
            float alpha = isBreak ? actionBoxEnabledAlpha : actionBoxDisabledAlpha;

            if (entry.Frame != null)
            {
                Color color = entry.FrameBaseColor;
                color.a *= alpha;
                entry.Frame.color = color;
            }

            if (entry.Label != null)
            {
                Color color = entry.LabelBaseColor;
                color.a *= alpha;
                entry.Label.color = color;
            }
        }
    }

    private Image FindActionBoxFrame(Transform root)
    {
        if (root == null)
        {
            return null;
        }

        Image[] images = root.GetComponentsInChildren<Image>(true);
        if (images == null || images.Length == 0)
        {
            return null;
        }

        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null)
            {
                continue;
            }

            if (image.name.IndexOf("frame", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return image;
            }
        }

        return images[0];
    }

    private TextMeshProUGUI FindActionBoxLabel(Transform root)
    {
        if (root == null)
        {
            return null;
        }

        TextMeshProUGUI[] labels = root.GetComponentsInChildren<TextMeshProUGUI>(true);
        if (labels == null || labels.Length == 0)
        {
            return null;
        }

        for (int i = 0; i < labels.Length; i++)
        {
            TextMeshProUGUI label = labels[i];
            if (label == null)
            {
                continue;
            }

            if (label.name.IndexOf("text", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return label;
            }
        }

        return labels[0];
    }

    private void EnsureBreakActionBoxEntry(Transform container)
    {
        if (container == null)
        {
            return;
        }

        Transform existing = FindActionBoxEntry(container, "ActionBox_Casser");
        if (existing != null)
        {
            EnsureActionBoxLabelText(existing, "Casser");
            return;
        }

        Transform template = FindFirstActionBoxEntry(container);
        if (template == null)
        {
            return;
        }

        GameObject clone = Instantiate(template.gameObject, container);
        clone.name = "ActionBox_Casser";
        RenameActionBoxChildren(clone.transform, "ActionBox_Casser");
        EnsureActionBoxLabelText(clone.transform, "Casser");
    }

    private Transform FindActionBoxEntry(Transform container, string name)
    {
        if (container == null || string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        for (int i = 0; i < container.childCount; i++)
        {
            Transform child = container.GetChild(i);
            if (child == null)
            {
                continue;
            }

            if (child.name.Equals(name, System.StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }
        }

        return null;
    }

    private Transform FindFirstActionBoxEntry(Transform container)
    {
        if (container == null)
        {
            return null;
        }

        for (int i = 0; i < container.childCount; i++)
        {
            Transform child = container.GetChild(i);
            if (child == null)
            {
                continue;
            }

            string name = child.name;
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            if (name.IndexOf("ActionBox_", System.StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            if (name.IndexOf("_Frame", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("_Text", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Cursor", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                continue;
            }

            return child;
        }

        return null;
    }

    private void EnsureActionBoxLabelText(Transform entry, string labelText)
    {
        if (entry == null)
        {
            return;
        }

        TextMeshProUGUI label = FindActionBoxLabel(entry);
        if (label == null)
        {
            return;
        }

        label.text = labelText;
    }

    private void RenameActionBoxChildren(Transform entry, string baseName)
    {
        if (entry == null || string.IsNullOrWhiteSpace(baseName))
        {
            return;
        }

        for (int i = 0; i < entry.childCount; i++)
        {
            Transform child = entry.GetChild(i);
            if (child == null)
            {
                continue;
            }

            string name = child.name;
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            if (name.IndexOf("frame", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                child.name = $"{baseName}_Frame";
            }
            else if (name.IndexOf("text", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                child.name = $"{baseName}_Text";
            }
        }
    }

    private CanvasGroup GetActionBoxCanvasGroup()
    {
        if (actionBox == null)
        {
            return null;
        }

        CanvasGroup canvasGroup = actionBox.GetComponent<CanvasGroup>();
        if (canvasGroup == null && actionBoxAddCanvasGroupIfMissing)
        {
            canvasGroup = actionBox.AddComponent<CanvasGroup>();
        }

        return canvasGroup;
    }

    private void FadeActionBoxTo(float targetAlpha, float duration)
    {
        CanvasGroup canvasGroup = GetActionBoxCanvasGroup();
        if (canvasGroup == null)
        {
            return;
        }

        if (!CanRunCoroutines())
        {
            SetActionBoxAlpha(targetAlpha);
            return;
        }

        if (actionBoxFadeRoutine != null)
        {
            StopCoroutine(actionBoxFadeRoutine);
        }

        float startAlpha = canvasGroup.alpha;
        if (duration <= 0f)
        {
            SetActionBoxAlpha(targetAlpha);
            return;
        }

        actionBoxFadeRoutine = StartCoroutine(FadeActionBoxRoutine(canvasGroup, startAlpha, targetAlpha, duration));
    }

    private IEnumerator FadeActionBoxRoutine(CanvasGroup canvasGroup, float startAlpha, float targetAlpha, float duration)
    {
        if (canvasGroup == null)
        {
            yield break;
        }

        float time = 0f;
        if (actionBoxDisableRaycastsWhenHidden)
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

        SetActionBoxAlpha(targetAlpha);
    }

    private void SetActionBoxAlpha(float alpha)
    {
        CanvasGroup canvasGroup = GetActionBoxCanvasGroup();
        if (canvasGroup == null)
        {
            return;
        }

        canvasGroup.alpha = alpha;
        if (actionBoxDisableRaycastsWhenHidden)
        {
            bool visible = alpha > 0.001f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }
    }

    private void HideActionBoxCursor()
    {
        if (actionBox == null)
        {
            return;
        }

        Transform cursor = actionBox.transform.Find("ActionBox_Cursor");
        if (cursor == null)
        {
            return;
        }

        cursor.gameObject.SetActive(false);
    }

    private bool CanRunCoroutines()
    {
        return isActiveAndEnabled && gameObject.activeInHierarchy;
    }

    private LootSlotUI GetFocusedSlot()
    {
        if (currentFocusedSlot != null)
        {
            return currentFocusedSlot;
        }

        if (lootSlots.Count > 0)
        {
            int clampedIndex = Mathf.Clamp(currentSlotIndex, 0, lootSlots.Count - 1);
            FocusSlot(lootSlots[clampedIndex]);
            return currentFocusedSlot;
        }

        LootUISettings settings = GetSettings();
        Transform itemsParent = settings != null ? settings.lootItemsParent : null;
        if (itemsParent == null)
        {
            return null;
        }

        LootSlotUI slot = itemsParent.GetComponentInChildren<LootSlotUI>();
        if (slot != null)
        {
            FocusSlot(slot);
        }

        return slot;
    }

    public bool TryDepositItem(Item item, int quantity)
    {
        if (item == null || quantity <= 0)
        {
            return false;
        }

        if (IsLockedForInteraction())
        {
            ShowActionFeedback(GetMissingKeyFeedback());
            return false;
        }

        if (IsNetworked() && !IsServer)
        {
            RequestDepositServerRpc(ItemIdUtils.GetItemId(item), quantity);
            return true;
        }

        if (!item.CanDepositToContainer(this, out string reason))
        {
            ShowActionFeedback(reason);
            return false;
        }

        SquadCharacterController controller = GetCurrentCharacterController();
        if (controller == null)
        {
            return false;
        }

        int remainingCapacity = GetRemainingCapacity();
        if (remainingCapacity <= 0)
        {
            ShowActionFeedback(depositNoSpaceMessage);
            return false;
        }

        if (quantity > remainingCapacity)
        {
            ShowActionFeedback(depositNoSpaceMessage);
            return false;
        }

        if (!controller.TryRemoveItemQuantity(item, quantity))
        {
            return false;
        }

        if (storedItems == null)
        {
            storedItems = new List<LootItemEntry>();
        }

        LootItemEntry existing = null;
        for (int i = 0; i < storedItems.Count; i++)
        {
            LootItemEntry entry = storedItems[i];
            if (entry != null && entry.item == item)
            {
                existing = entry;
                break;
            }
        }

        if (existing != null)
        {
            existing.quantity = Mathf.Max(0, existing.quantity + quantity);
        }
        else
        {
            storedItems.Add(new LootItemEntry { item = item, quantity = quantity });
        }

        if (lootOpen)
        {
            RebuildLootSlots(item, currentSlotIndex);
        }

        SyncNetFromLootItems();
        SyncNetworkInventoryForCurrentCharacter();
        PlayActionAudio(ActionAudioCue.InventoryDeposit);
        ShowActionFeedback(item.GetDepositSuccessMessage());
        return true;
    }

    public void AddItems(Item item, int quantity)
    {
        if (item == null || quantity <= 0)
        {
            return;
        }
        AddItemsInternal(item, quantity);

        if (lootOpen)
        {
            RebuildLootSlots(item, currentSlotIndex);
        }

        SyncNetFromLootItems();
    }

    public int AddItemsWithCapacity(Item item, int quantity)
    {
        if (item == null || quantity <= 0)
        {
            return 0;
        }

        int remaining = GetRemainingCapacity();
        if (remaining <= 0)
        {
            return 0;
        }

        int toAdd = maxStoredQuantity > 0 ? Mathf.Min(quantity, remaining) : quantity;
        if (toAdd <= 0)
        {
            return 0;
        }

        AddItems(item, toAdd);
        return toAdd;
    }

    public int GetTotalQuantity()
    {
        if (storedItems == null || storedItems.Count == 0)
        {
            return 0;
        }

        int total = 0;
        for (int i = 0; i < storedItems.Count; i++)
        {
            LootItemEntry entry = storedItems[i];
            if (entry == null)
            {
                continue;
            }

            int amount = Mathf.Max(0, entry.quantity);
            total += amount;
        }

        return total;
    }

    public int GetRemainingCapacity()
    {
        if (maxStoredQuantity <= 0)
        {
            return int.MaxValue;
        }

        int used = GetTotalQuantity();
        return Mathf.Max(0, maxStoredQuantity - used);
    }

    public int GetItemCount(Item item)
    {
        if (item == null || storedItems == null || storedItems.Count == 0)
        {
            return 0;
        }

        int total = 0;
        for (int i = 0; i < storedItems.Count; i++)
        {
            LootItemEntry entry = storedItems[i];
            if (entry == null || entry.item != item)
            {
                continue;
            }

            total += Mathf.Max(0, entry.quantity);
        }

        return total;
    }

    public int RemoveItems(Item item, int quantity)
    {
        if (item == null || quantity <= 0 || storedItems == null || storedItems.Count == 0)
        {
            return 0;
        }

        int remaining = quantity;
        for (int i = storedItems.Count - 1; i >= 0 && remaining > 0; i--)
        {
            LootItemEntry entry = storedItems[i];
            if (entry == null || entry.item != item)
            {
                continue;
            }

            int available = Mathf.Max(0, entry.quantity);
            if (available <= 0)
            {
                storedItems.RemoveAt(i);
                continue;
            }

            int toRemove = Mathf.Min(available, remaining);
            entry.quantity = Mathf.Max(0, available - toRemove);
            remaining -= toRemove;

            if (entry.quantity <= 0)
            {
                storedItems.RemoveAt(i);
            }
        }

        if (lootOpen)
        {
            RebuildLootSlots(item, currentSlotIndex);
        }

        SyncNetFromLootItems();
        return quantity - remaining;
    }

    public void SetLootItems(List<LootItemEntry> entries, bool rebuildIfOpen = true)
    {
        storedItems = entries ?? new List<LootItemEntry>();
        if (lootOpen && rebuildIfOpen)
        {
            RebuildLootSlots(null, currentSlotIndex);
        }

        SyncNetFromLootItems();
    }

    private void OnNetLootChanged(NetworkListEvent<NetItemStack> change)
    {
        if (applyingNetLoot)
        {
            return;
        }

        ApplyLootFromNet();
    }

    private void OnNetIsLockedChanged(bool previousValue, bool newValue)
    {
        isLocked = newValue;
    }

    private void OnNetTrapTriggeredChanged(bool previousValue, bool newValue)
    {
        trapTriggered = newValue;
    }

    private void ApplyLootFromNet()
    {
        if (IsServer)
        {
            return;
        }

        applyingNetLoot = true;
        storedItems = new List<LootItemEntry>();
        for (int i = 0; i < netLootItems.Count; i++)
        {
            NetItemStack stack = netLootItems[i];
            if (stack.Quantity <= 0)
            {
                continue;
            }

            Item item = ItemRegistry.Resolve(stack.ItemId.ToString());
            if (item == null)
            {
                continue;
            }

            storedItems.Add(new LootItemEntry { item = item, quantity = stack.Quantity });
        }

        applyingNetLoot = false;
        RefreshRecoverableWorldInfo();

        if (lootOpen)
        {
            RebuildLootSlots(null, currentSlotIndex);
        }
    }

    private void SyncNetFromLootItems()
    {
        RefreshRecoverableWorldInfo();
        if (!IsServer || applyingNetLoot)
        {
            return;
        }

        applyingNetLoot = true;
        netLootItems.Clear();
        if (storedItems != null)
        {
            for (int i = 0; i < storedItems.Count; i++)
            {
                LootItemEntry entry = storedItems[i];
                if (entry == null || entry.item == null)
                {
                    continue;
                }

                string id = ItemIdUtils.GetItemId(entry.item);
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                int quantity = Mathf.Max(0, entry.quantity);
                if (quantity <= 0)
                {
                    continue;
                }

                netLootItems.Add(new NetItemStack(id, quantity));
            }
        }
        applyingNetLoot = false;
    }

    private bool IsNetworked()
    {
        return NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
    }

    private void TakeAllItems()
    {
        if (IsLockedForInteraction())
        {
            ShowActionFeedback(GetMissingKeyFeedback());
            return;
        }

        if (IsNetworked() && !IsServer)
        {
            RequestTakeAllServerRpc();
            return;
        }

        if (!allowTake)
        {
            ShowActionFeedback(takeBlockedMessage);
            return;
        }

        if (!HasAvailableStoredItems())
        {
            TryTakeRepresentedItemDirectLocal();
            return;
        }

        bool showedFeedback = false;
        bool tookAny = false;
        for (int i = storedItems.Count - 1; i >= 0; i--)
        {
            LootItemEntry entry = storedItems[i];
            if (entry == null || entry.item == null)
            {
                storedItems.RemoveAt(i);
                continue;
            }

            int quantity = Mathf.Max(0, entry.quantity);
            if (quantity <= 0)
            {
                storedItems.RemoveAt(i);
                continue;
            }

            if (TryAddItemToCurrentCharacter(entry.item, quantity, !showedFeedback))
            {
                storedItems.RemoveAt(i);
                tookAny = true;
            }
            else
            {
                showedFeedback = true;
            }
        }

        RebuildLootSlots(null, currentSlotIndex);
        HandleEmptyContainer();
        SyncNetFromLootItems();
        SyncNetworkInventoryForCurrentCharacter();
        if (tookAny)
        {
            PlayActionAudio(ActionAudioCue.InventoryTake);
        }
    }

    private void HandleEmptyContainer()
    {
        RefreshRecoverableWorldInfo();
        if (!destroyWhenStorageEmpty && interactableCategory != InteractableCategory.RecoverableItem)
        {
            return;
        }

        if (storedItems == null || storedItems.Count > 0)
        {
            return;
        }

        CloseLoot();
        if (IsNetworked() && IsServer)
        {
            NetworkObject networkObject = GetComponent<NetworkObject>();
            if (networkObject != null)
            {
                networkObject.Despawn(true);
                return;
            }
        }

        Destroy(gameObject);
    }

    private bool HasAvailableStoredItems()
    {
        if (storedItems == null || storedItems.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < storedItems.Count; i++)
        {
            LootItemEntry entry = storedItems[i];
            if (entry != null && entry.item != null && entry.quantity > 0)
            {
                return true;
            }
        }

        return false;
    }

    private bool CanTakeRepresentedItemDirect(out Item item)
    {
        item = null;
        if (interactableCategory != InteractableCategory.RecoverableItem)
        {
            return false;
        }

        if (representedItem == null)
        {
            return false;
        }

        if (HasAvailableStoredItems())
        {
            return false;
        }

        item = representedItem;
        return true;
    }

    private bool TryTakeRepresentedItemDirectLocal()
    {
        if (!CanTakeRepresentedItemDirect(out Item item))
        {
            return false;
        }

        if (!TryAddItemToCurrentCharacter(item, 1))
        {
            return false;
        }

        HandleEmptyContainer();
        SyncNetFromLootItems();
        SyncNetworkInventoryForCurrentCharacter();
        PlayActionAudio(ActionAudioCue.InventoryTake);
        ShowActionFeedback(item.GetTakeSuccessMessage());
        return true;
    }

    private bool TryTakeAllItemsForCharacterServer(
        Transform playerRoot,
        SquadCharacterController controller,
        NetworkInventory inventory,
        ServerRpcParams rpcParams)
    {
        if (controller == null || inventory == null)
        {
            return false;
        }

        ClientRpcParams clientRpcParams = BuildClientRpcParams(rpcParams);

        if (!HasAvailableStoredItems())
        {
            if (!CanTakeRepresentedItemDirect(out Item representedPickup))
            {
                return false;
            }

            if (!representedPickup.CanTakeFromContainer(this, out string representedReason))
            {
                ShowFeedbackClientRpc(representedReason, false, clientRpcParams);
                return true;
            }

            controller.AddItem(representedPickup, 1);
            inventory.SyncFromController();
            HandleEmptyContainer();
            SyncNetFromLootItems();
            ShowFeedbackWithAudioClientRpc(
                representedPickup.GetTakeSuccessMessage(),
                false,
                ActionAudioCue.InventoryTake,
                clientRpcParams);
            return true;
        }

        bool movedAnyItem = false;
        for (int i = storedItems.Count - 1; i >= 0; i--)
        {
            LootItemEntry entry = storedItems[i];
            if (entry == null || entry.item == null)
            {
                storedItems.RemoveAt(i);
                continue;
            }

            int quantity = Mathf.Max(0, entry.quantity);
            if (quantity <= 0)
            {
                storedItems.RemoveAt(i);
                continue;
            }

            controller.AddItem(entry.item, quantity);
            entry.quantity = 0;
            storedItems.RemoveAt(i);
            movedAnyItem = true;
        }

        if (!movedAnyItem)
        {
            return false;
        }

        inventory.SyncFromController();
        SyncNetFromLootItems();
        HandleEmptyContainer();
        ShowFeedbackWithAudioClientRpc(takeAllSuccessMessage, false, ActionAudioCue.InventoryTake, clientRpcParams);
        return true;
    }

    public void RefreshRecoverableWorldInfo()
    {
        Item displayItem = ResolveRecoverableDisplayItem();
        bool shouldShow = showWorldInteractionUi && allowTake && displayItem != null;
        if (shouldShow && displayItem.isBuilding && !LegacyBuildingSystem.Enabled)
        {
            shouldShow = false;
        }

        if (!shouldShow)
        {
            if (recoverableWorldInfo != null)
            {
                recoverableWorldInfo.SetDetectedCharacter(null);
                recoverableWorldInfo.enabled = false;
            }

            return;
        }

        if (recoverableWorldInfo == null)
        {
            recoverableWorldInfo = GetComponent<BuildingInfoInteractable>();
            if (recoverableWorldInfo == null)
            {
                recoverableWorldInfo = gameObject.AddComponent<BuildingInfoInteractable>();
            }
        }

        recoverableWorldInfo.enabled = true;
        recoverableWorldInfo.interactionTrigger = interactionTrigger;
        recoverableWorldInfo.openOnProximity = true;
        recoverableWorldInfo.closePanelOnExit = true;
        recoverableWorldInfo.destroyPanelOnExit = false;
        recoverableWorldInfo.openCraftingPanelOnInteract = false;
        recoverableWorldInfo.consumeInteractOnProximity = false;
        if (!displayItem.isBuilding)
        {
            recoverableWorldInfo.localInformationPanelPrefab = null;
        }
        recoverableWorldInfo.Initialize(displayItem, 1);
        recoverableWorldInfo.MarkPresentationOrigin("recoverable_loot", true);
    }

    private Item ResolveRecoverableDisplayItem()
    {
        if (representedItem != null)
        {
            return representedItem;
        }

        if (storedItems == null)
        {
            return null;
        }

        for (int i = 0; i < storedItems.Count; i++)
        {
            LootItemEntry entry = storedItems[i];
            if (entry != null && entry.item != null && entry.quantity > 0)
            {
                return entry.item;
            }
        }

        return null;
    }

    private void HandleLootNavigation()
    {
        if (lootSlots.Count == 0)
        {
            return;
        }

        LootUISettings settings = GetSettings();
        if (settings == null)
        {
            return;
        }

        Vector2 moveInput = LocalInputRouter.MoveValue;
        int direction = GetMoveDirection(moveInput, settings.moveDeadzone);
        if (direction == 0)
        {
            lastMoveDirection = 0;
            nextMoveTime = 0f;
            return;
        }

        float now = Time.unscaledTime;
        if (direction != lastMoveDirection)
        {
            MoveSlot(direction, settings.wrapCursor);
            lastMoveDirection = direction;
            nextMoveTime = now + settings.initialRepeatDelay;
            return;
        }

        if (now >= nextMoveTime)
        {
            MoveSlot(direction, settings.wrapCursor);
            nextMoveTime = now + settings.repeatInterval;
        }
    }

    private int GetMoveDirection(Vector2 input, float deadzone)
    {
        float absX = Mathf.Abs(input.x);
        float absY = Mathf.Abs(input.y);

        if (absX < deadzone && absY < deadzone)
        {
            return 0;
        }

        if (absX >= absY)
        {
            return input.x > 0f ? 2 : -2;
        }

        return input.y > 0f ? -1 : 1;
    }

    private void MoveSlot(int direction, bool wrap)
    {
        LootSlotUI current = GetFocusedSlot();
        if (current == null)
        {
            return;
        }

        LootSlotUI next = FindNeighborSlot(current, direction, wrap);
        if (next == null || next == current)
        {
            return;
        }

        FocusSlot(next);
    }

    private LootSlotUI FindNeighborSlot(LootSlotUI current, int direction, bool wrap)
    {
        if (current == null || current.SlotRect == null)
        {
            return null;
        }

        LootUISettings settings = GetSettings();
        Canvas canvas = settings != null && settings.lootPanel != null
            ? settings.lootPanel.GetComponentInParent<Canvas>()
            : null;
        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        if (cursorDirty && settings != null && settings.lootItemsParent != null)
        {
            Canvas.ForceUpdateCanvases();
            RectTransform itemsRect = settings.lootItemsParent as RectTransform;
            if (itemsRect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(itemsRect);
            }
        }

        List<SlotInfo> slotInfos = new List<SlotInfo>(lootSlots.Count);
        for (int i = 0; i < lootSlots.Count; i++)
        {
            LootSlotUI slot = lootSlots[i];
            if (slot == null || slot.SlotRect == null)
            {
                continue;
            }

            Vector2 pos = RectTransformUtility.WorldToScreenPoint(uiCamera, slot.SlotRect.position);
            slotInfos.Add(new SlotInfo(slot, pos));
        }

        if (slotInfos.Count == 0)
        {
            return null;
        }

        slotInfos.Sort((a, b) => b.Position.y.CompareTo(a.Position.y));

        float rowTolerance = GetSlotScreenHeight(current, uiCamera) * 0.6f;
        if (rowTolerance <= 0f)
        {
            rowTolerance = 10f;
        }

        List<List<SlotInfo>> rows = new List<List<SlotInfo>>();
        for (int i = 0; i < slotInfos.Count; i++)
        {
            SlotInfo info = slotInfos[i];
            if (rows.Count == 0 || Mathf.Abs(info.Position.y - rows[rows.Count - 1][0].Position.y) > rowTolerance)
            {
                rows.Add(new List<SlotInfo>());
            }

            rows[rows.Count - 1].Add(info);
        }

        int currentRow = -1;
        int currentCol = -1;
        float currentX = 0f;

        for (int r = 0; r < rows.Count; r++)
        {
            rows[r].Sort((a, b) => a.Position.x.CompareTo(b.Position.x));
            for (int c = 0; c < rows[r].Count; c++)
            {
                if (rows[r][c].Slot == current)
                {
                    currentRow = r;
                    currentCol = c;
                    currentX = rows[r][c].Position.x;
                }
            }
        }

        if (currentRow < 0)
        {
            return null;
        }

        if (direction == -2 || direction == 2)
        {
            List<SlotInfo> row = rows[currentRow];
            if (row.Count <= 1)
            {
                return null;
            }

            int nextCol = currentCol + (direction == 2 ? 1 : -1);
            if (nextCol < 0 || nextCol >= row.Count)
            {
                if (!wrap)
                {
                    return null;
                }

                nextCol = direction == 2 ? 0 : row.Count - 1;
                if (nextCol == currentCol)
                {
                    return null;
                }
            }

            return row[nextCol].Slot;
        }

        if (direction == -1 || direction == 1)
        {
            int nextRow = currentRow + (direction == 1 ? 1 : -1);
            if (nextRow < 0 || nextRow >= rows.Count)
            {
                if (!wrap)
                {
                    return null;
                }

                nextRow = direction == 1 ? 0 : rows.Count - 1;
                if (nextRow == currentRow)
                {
                    return null;
                }
            }

            List<SlotInfo> targetRow = rows[nextRow];
            if (targetRow.Count == 0)
            {
                return null;
            }

            SlotInfo best = targetRow[0];
            float bestDx = Mathf.Abs(best.Position.x - currentX);
            for (int i = 1; i < targetRow.Count; i++)
            {
                float dx = Mathf.Abs(targetRow[i].Position.x - currentX);
                if (dx < bestDx)
                {
                    bestDx = dx;
                    best = targetRow[i];
                }
            }

            return best.Slot;
        }

        return null;
    }

    private float GetSlotScreenHeight(LootSlotUI slot, Camera uiCamera)
    {
        if (slot == null || slot.SlotRect == null)
        {
            return 0f;
        }

        Vector3[] corners = new Vector3[4];
        slot.SlotRect.GetWorldCorners(corners);
        float minY = float.MaxValue;
        float maxY = float.MinValue;
        for (int i = 0; i < corners.Length; i++)
        {
            float y = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[i]).y;
            minY = Mathf.Min(minY, y);
            maxY = Mathf.Max(maxY, y);
        }

        return Mathf.Max(0f, maxY - minY);
    }

    private readonly struct SlotInfo
    {
        public SlotInfo(LootSlotUI slot, Vector2 position)
        {
            Slot = slot;
            Position = position;
        }

        public LootSlotUI Slot { get; }
        public Vector2 Position { get; }
    }

    private sealed class ActionBoxEntry
    {
        public ActionBoxEntry(RectTransform rect, Image frame, TextMeshProUGUI label, string name)
        {
            Rect = rect;
            Frame = frame;
            Label = label;
            Name = name;
            FrameBaseColor = frame != null ? frame.color : Color.white;
            LabelBaseColor = label != null ? label.color : Color.white;
        }

        public RectTransform Rect { get; }
        public Image Frame { get; }
        public TextMeshProUGUI Label { get; }
        public string Name { get; }
        public Color FrameBaseColor { get; }
        public Color LabelBaseColor { get; }
    }

    private bool TryAddItemToCurrentCharacter(Item item, int quantity, bool showFeedback = true)
    {
        if (item == null)
        {
            return false;
        }

        if (!item.CanTakeFromContainer(this, out string reason))
        {
            if (showFeedback)
            {
                ShowActionFeedback(reason);
            }
            return false;
        }

        SquadCharacterController controller = GetCurrentCharacterController();
        if (controller == null)
        {
            Debug.LogWarning("InteractableItem: aucun personnage valide pour recevoir l'item.");
            return false;
        }

        controller.AddItem(item, quantity);
        return true;
    }

    public bool IsLockedForInteraction()
    {
        return isLocked;
    }

    public bool HasTriggeredTrap => trapTriggered;

    public bool TryUnlock()
    {
        return TryUnlock(out _);
    }

    public bool TryUnlock(out string feedback)
    {
        SquadCharacterController controller = GetCurrentCharacterController();
        bool success = TryUnlockWithKey(controller, out bool inventoryChanged);
        feedback = success ? GetUnlockSuccessFeedback() : GetMissingKeyFeedback();
        if (inventoryChanged)
        {
            SyncNetworkInventoryForCurrentCharacter();
        }

        return success;
    }

    private bool CanUnlockWithKey(SquadCharacterController controller)
    {
        return controller != null
            && !string.IsNullOrWhiteSpace(lockId)
            && controller.HasMatchingKey(lockId);
    }

    private void SetLockedState(bool locked)
    {
        isLocked = locked;
        if (IsServer)
        {
            netIsLocked.Value = locked;
        }
    }

    private void SetTrapTriggeredState(bool triggered)
    {
        trapTriggered = triggered;
        if (IsServer)
        {
            netTrapTriggered.Value = triggered;
        }
    }

    public void RestoreLockedState(bool locked)
    {
        SetLockedState(locked);
    }

    public void RestoreTrapTriggeredState(bool triggered)
    {
        SetTrapTriggeredState(triggered);
    }

    private string GetMissingKeyFeedback()
    {
        if (!string.IsNullOrWhiteSpace(lockedNoKeyMessage))
        {
            return lockedNoKeyMessage;
        }

        return "Le conteneur est verrouille. Il faut la bonne cle.";
    }

    private string GetUnlockSuccessFeedback()
    {
        if (!string.IsNullOrWhiteSpace(unlockSuccessMessage))
        {
            return unlockSuccessMessage;
        }

        return "Le conteneur est deverrouille.";
    }

    private string GetMissingLockpickFeedback()
    {
        if (!string.IsNullOrWhiteSpace(missingLockpickMessage))
        {
            return missingLockpickMessage;
        }

        return "Il manque des outils de crochetage.";
    }

    private string GetLockpickSuccessFeedback()
    {
        if (!string.IsNullOrWhiteSpace(lockpickSuccessMessage))
        {
            return lockpickSuccessMessage;
        }

        return "Crochetage reussi.";
    }

    private string GetLockpickFailureFeedback()
    {
        if (!string.IsNullOrWhiteSpace(lockpickFailureMessage))
        {
            return lockpickFailureMessage;
        }

        return "Crochetage rat\u00E9, votre outil de crochetage se brise...";
    }

    private int GetNormalizedLockDifficulty()
    {
        return Mathf.Clamp(lockDifficulty, MinLockDifficulty, MaxLockDifficulty);
    }

    private string GetResolvedLockpickToolItemId()
    {
        if (lockpickToolItem != null)
        {
            string explicitId = ItemIdUtils.GetItemId(lockpickToolItem);
            if (!string.IsNullOrWhiteSpace(explicitId))
            {
                return explicitId;
            }
        }

        if (!string.IsNullOrWhiteSpace(lockpickToolItemId))
        {
            return lockpickToolItemId;
        }

        return "outils_de_crochetage";
    }

    private string BuildLockpickConfirmationMessage(int availableTools)
    {
        string question = !string.IsNullOrWhiteSpace(lockpickConfirmationMessage)
            ? lockpickConfirmationMessage
            : "Utiliser 1 outil de crochetage pour tenter d'ouvrir ce coffre ?";
        return $"{question}\n\nOutils disponibles : {Mathf.Max(0, availableTools)}";
    }

    private bool TryConsumeLockpickTool(SquadCharacterController controller, out Item consumedTool)
    {
        consumedTool = null;
        if (controller == null)
        {
            return false;
        }

        if (lockpickToolItem != null)
        {
            if (!controller.TryRemoveItemQuantity(lockpickToolItem, 1))
            {
                return false;
            }

            consumedTool = lockpickToolItem;
            return true;
        }

        string toolId = GetResolvedLockpickToolItemId();
        if (string.IsNullOrWhiteSpace(toolId))
        {
            Debug.LogWarning($"InteractableItem '{name}' ne peut pas tenter de crochetage: aucun item de crochetage configure.", this);
            return false;
        }

        return controller.TryConsumeItemById(toolId, 1, out consumedTool);
    }

    private int CountAvailableLockpickTools(SquadCharacterController controller)
    {
        if (controller == null)
        {
            return 0;
        }

        if (lockpickToolItem != null)
        {
            return controller.CountItem(lockpickToolItem);
        }

        string toolId = GetResolvedLockpickToolItemId();
        if (string.IsNullOrWhiteSpace(toolId))
        {
            Debug.LogWarning($"[Lockpick] no_tool_id_configured container='{name}'", this);
            return 0;
        }

        return controller.CountItemById(toolId);
    }

    private bool CanOfferLockpick(SquadCharacterController controller, out int availableTools, out string feedback)
    {
        availableTools = 0;
        feedback = string.Empty;
        if (controller == null)
        {
            feedback = GetMissingKeyFeedback();
            return false;
        }

        if (!allowLockpick)
        {
            feedback = GetMissingKeyFeedback();
            return false;
        }

        availableTools = CountAvailableLockpickTools(controller);
        if (availableTools > 0)
        {
            return true;
        }

        feedback = GetMissingLockpickFeedback();
        return false;
    }

    private bool TryUnlockWithKey(SquadCharacterController controller, out bool inventoryChanged)
    {
        inventoryChanged = false;
        if (!IsLockedForInteraction())
        {
            return true;
        }

        if (controller == null || string.IsNullOrWhiteSpace(lockId))
        {
            return false;
        }

        if (!controller.TryUseMatchingKey(lockId, consumeKeyOnUse, out Item keyItem))
        {
            return false;
        }

        SetLockedState(false);
        inventoryChanged = consumeKeyOnUse && keyItem != null;
        Debug.Log(
            $"[Lockpick] container='{name}' character='{controller.name}' path='key' lockId='{lockId}' consumeKey={consumeKeyOnUse} inventoryChanged={inventoryChanged}",
            this);
        return true;
    }

    private bool TryPerformLockpickAttempt(SquadCharacterController controller, out string feedback, out bool inventoryChanged)
    {
        feedback = string.Empty;
        inventoryChanged = false;
        if (!allowLockpick)
        {
            feedback = GetMissingKeyFeedback();
            return false;
        }

        if (controller == null)
        {
            feedback = GetMissingLockpickFeedback();
            return false;
        }

        if (!TryConsumeLockpickTool(controller, out _))
        {
            feedback = GetMissingLockpickFeedback();
            return false;
        }

        inventoryChanged = true;
        int difficulty = GetNormalizedLockDifficulty();
        int dexterity = controller.GetDexterityValue();
        int dexterityBonus = Mathf.Clamp(controller.GetDexterityModifier(), MinDexterityBonus, MaxDexterityBonus);
        int roll = Random.Range(1, 21);
        int total = roll + dexterityBonus;
        bool success = total >= difficulty;

        Debug.Log(
            $"[Lockpick] container='{name}' character='{controller.name}' path='lockpick' difficulty={difficulty} roll={roll} dexterity={dexterity} dexBonus={dexterityBonus} total={total} success={success}",
            this);

        if (!success)
        {
            feedback = GetLockpickFailureFeedback();
            if (TryTriggerTrapOnFailedLockpick(controller, out string trapFeedback))
            {
                feedback = CombineFeedbackMessages(feedback, trapFeedback);
            }
            return false;
        }

        SetLockedState(false);
        feedback = GetLockpickSuccessFeedback();
        return true;
    }

    private bool CanTriggerTrapOnOpen()
    {
        return IsTrapArmed() && triggerTrapOnOpen;
    }

    private bool CanTriggerTrapOnFailedLockpick()
    {
        return IsTrapArmed() && !triggerTrapOnOpen;
    }

    private bool IsTrapArmed()
    {
        if (!isTrapped || trapEffect == TrapEffectType.None)
        {
            return false;
        }

        if (!disarmTrapAfterTrigger)
        {
            return true;
        }

        return !trapTriggered;
    }

    private bool TryTriggerTrapOnOpen(SquadCharacterController controller, out string feedback)
    {
        feedback = string.Empty;
        if (!CanTriggerTrapOnOpen())
        {
            return false;
        }

        return TryTriggerTrap(controller, "open", out feedback);
    }

    private bool TryTriggerTrapOnFailedLockpick(SquadCharacterController controller, out string feedback)
    {
        feedback = string.Empty;
        if (!CanTriggerTrapOnFailedLockpick())
        {
            return false;
        }

        return TryTriggerTrap(controller, "failed_lockpick", out feedback);
    }

    private bool TryTriggerTrap(SquadCharacterController controller, string triggerPath, out string feedback)
    {
        feedback = string.Empty;
        if (!IsTrapArmed())
        {
            return false;
        }

        if (controller == null)
        {
            Debug.LogWarning($"[LootTrap] trigger_skipped container='{name}' path='{triggerPath}' reason='missing_controller'", this);
            return false;
        }

        Vector3 destination = Vector3.zero;
        bool executed = false;
        switch (trapEffect)
        {
            case TrapEffectType.TeleportCharacter:
                executed = TryExecuteTeleportTrap(controller, out destination);
                break;
            default:
                Debug.LogWarning($"[LootTrap] trigger_skipped container='{name}' path='{triggerPath}' reason='unsupported_trap_effect' effect='{trapEffect}'", this);
                break;
        }

        if (!executed)
        {
            return false;
        }

        if (disarmTrapAfterTrigger)
        {
            SetTrapTriggeredState(true);
        }

        PlayActionAudio(ActionAudioCue.InventoryTrap);
        feedback = GetTrapTriggeredFeedback();
        Debug.Log(
            $"[LootTrap] triggered container='{name}' character='{controller.name}' path='{triggerPath}' effect='{trapEffect}' disarmed={disarmTrapAfterTrigger} destination='{destination}'",
            this);
        return true;
    }

    private bool TryExecuteTeleportTrap(SquadCharacterController controller, out Vector3 destination)
    {
        destination = Vector3.zero;
        if (trapTeleportTarget == null)
        {
            Debug.LogWarning($"[LootTrap] teleport_trap_skipped container='{name}' reason='missing_target'", this);
            return false;
        }

        GameObject characterRoot = ResolveTrapCharacterRoot(controller);
        if (characterRoot == null)
        {
            Debug.LogWarning($"[LootTrap] teleport_trap_skipped container='{name}' reason='missing_character_root'", this);
            return false;
        }

        destination = trapTeleportTarget.position;
        Quaternion rotation = trapUseTargetRotation
            ? trapTeleportTarget.rotation
            : characterRoot.transform.rotation;

        if (!controller.TrySetUccExternalPositionAndRotation(destination, rotation, stopActiveAbilities: true))
        {
            Debug.LogWarning($"[LootTrap] teleport_trap_skipped container='{name}' reason='ucc_locomotion_unavailable'", this);
            return false;
        }

        controller.Stop();
        Physics.SyncTransforms();
        RemoveTeleportedCharacterFromRange(characterRoot.transform);
        UpdateCurrentCharacter();
        return true;
    }

    private GameObject ResolveTrapCharacterRoot(SquadCharacterController controller)
    {
        if (controller == null)
        {
            return currentCharacter;
        }

        NetworkObject networkRoot = controller.GetComponentInParent<NetworkObject>();
        if (networkRoot != null)
        {
            return networkRoot.gameObject;
        }

        return controller.gameObject;
    }

    private string GetTrapTriggeredFeedback()
    {
        if (!string.IsNullOrWhiteSpace(trapTriggeredMessage))
        {
            return trapTriggeredMessage;
        }

        switch (trapEffect)
        {
            case TrapEffectType.TeleportCharacter:
                return "Un piege de teleportation se declenche !";
            default:
                return "Un piege se declenche !";
        }
    }

    private static string CombineFeedbackMessages(string first, string second)
    {
        bool hasFirst = !string.IsNullOrWhiteSpace(first);
        bool hasSecond = !string.IsNullOrWhiteSpace(second);
        if (!hasFirst)
        {
            return hasSecond ? second : string.Empty;
        }

        if (!hasSecond)
        {
            return first;
        }

        return $"{first}\n{second}";
    }

    private void ClearPendingUnlockAttempt()
    {
        unlockAttemptInProgress = false;
    }

    private void ShowActionFeedback(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        InfoBoxUI.TryShow(message);
    }

    private void PlayActionAudio(ActionAudioCue cue)
    {
        if (cue == ActionAudioCue.None)
        {
            return;
        }

        AudioManager manager = AudioManager.EnsureInstance();
        if (manager != null)
        {
            manager.PlayActionCue(cue, transform.position);
        }
    }

    private SquadCharacterController GetCurrentCharacterController()
    {
        if (currentCharacter == null)
        {
            return null;
        }

        return currentCharacter.GetComponent<SquadCharacterController>();
    }

    private void SyncNetworkInventoryForCurrentCharacter()
    {
        if (!IsServer)
        {
            return;
        }

        SquadCharacterController controller = GetCurrentCharacterController();
        if (controller == null)
        {
            return;
        }

        NetworkInventory inventory = controller.GetComponent<NetworkInventory>();
        if (inventory == null)
        {
            inventory = controller.GetComponentInChildren<NetworkInventory>(true);
        }

        if (inventory != null)
        {
            inventory.SyncFromController();
        }
    }

    private NetworkInventory GetNetworkInventoryForCharacter(Transform playerRoot)
    {
        if (playerRoot == null)
        {
            return null;
        }

        NetworkInventory inventory = playerRoot.GetComponent<NetworkInventory>();
        if (inventory != null)
        {
            return inventory;
        }

        return playerRoot.GetComponentInChildren<NetworkInventory>(true);
    }

    private SquadCharacterController GetControllerFromRoot(Transform playerRoot)
    {
        if (playerRoot == null)
        {
            return null;
        }

        SquadCharacterController controller = playerRoot.GetComponent<SquadCharacterController>();
        if (controller != null)
        {
            return controller;
        }

        return playerRoot.GetComponentInChildren<SquadCharacterController>(true);
    }

    private bool IsCharacterInRange(Transform characterRoot)
    {
        return CharacterInteractionDetection.IsCharacterWithinRange(
            characterRoot,
            ResolveInteractionColliderReference(),
            transform,
            interactionMaxDistance);
    }

    private void RemoveTeleportedCharacterFromRange(Transform characterRoot)
    {
        if (characterRoot == null)
        {
            return;
        }

        if (UsesControllerDrivenDetection()
            && IsSameOrRelatedTransform(currentCharacter != null ? currentCharacter.transform : null, characterRoot))
        {
            SetDetectedCharacter(null);
        }

        for (int i = charactersInRange.Count - 1; i >= 0; i--)
        {
            GameObject trackedCharacter = charactersInRange[i];
            if (!IsSameOrRelatedTransform(trackedCharacter != null ? trackedCharacter.transform : null, characterRoot))
            {
                continue;
            }

            charactersInRange.RemoveAt(i);
            if (trackedCharacter != null)
            {
                characterColliderCounts.Remove(trackedCharacter);
            }
        }
    }

    private static bool IsSameOrRelatedTransform(Transform first, Transform second)
    {
        if (first == null || second == null)
        {
            return false;
        }

        return first == second || first.IsChildOf(second) || second.IsChildOf(first);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestUnlockAndOpenServerRpc(bool confirmedLockpick, ServerRpcParams rpcParams = default)
    {
        Transform playerRoot = NetcodePlayerUtils.GetPlayerTransform(rpcParams.Receive.SenderClientId);
        if (!IsCharacterInRange(playerRoot))
        {
            ShowFeedbackClientRpc(string.Empty, false, BuildClientRpcParams(rpcParams));
            return;
        }

        SquadCharacterController controller = GetControllerFromRoot(playerRoot);
        NetworkInventory inventory = GetNetworkInventoryForCharacter(playerRoot);
        if (controller == null)
        {
            ShowFeedbackClientRpc(string.Empty, false, BuildClientRpcParams(rpcParams));
            return;
        }

        if (IsLockedForInteraction())
        {
            if (TryUnlockWithKey(controller, out bool keyInventoryChanged))
            {
                if (keyInventoryChanged)
                {
                    if (inventory != null)
                    {
                        inventory.SyncFromController();
                    }
                    else
                    {
                        Debug.LogWarning($"[Lockpick] inventory sync skipped for key unlock on container='{name}' because NetworkInventory is missing on '{playerRoot?.name}'.", this);
                    }
                }

                if (TryTriggerTrapOnOpen(controller, out string keyTrapFeedback))
                {
                    ShowFeedbackWithAudioClientRpc(
                        keyTrapFeedback,
                        false,
                        ActionAudioCue.InventoryTrap,
                        BuildClientRpcParams(rpcParams));
                    return;
                }

                if (!TryTakeAllItemsForCharacterServer(playerRoot, controller, inventory, rpcParams)
                    && interactableCategory == InteractableCategory.Container)
                {
                    OpenLootClientRpc(BuildClientRpcParams(rpcParams));
                }
                return;
            }

            if (!confirmedLockpick)
            {
                ShowFeedbackWithAudioClientRpc(
                    GetMissingKeyFeedback(),
                    false,
                    ActionAudioCue.UiInvalid,
                    BuildClientRpcParams(rpcParams));
                return;
            }

            bool success = TryPerformLockpickAttempt(controller, out string feedback, out bool inventoryChanged);
            if (inventoryChanged)
            {
                if (inventory != null)
                {
                    inventory.SyncFromController();
                }
                else
                {
                    Debug.LogWarning($"[Lockpick] inventory sync skipped for lockpick on container='{name}' because NetworkInventory is missing on '{playerRoot?.name}'.", this);
                }
            }

            if (!success)
            {
                ShowFeedbackWithAudioClientRpc(
                    feedback,
                    false,
                    ActionAudioCue.InventoryLockpickFailure,
                    BuildClientRpcParams(rpcParams));
                return;
            }

            if (TryTriggerTrapOnOpen(controller, out string openTrapFeedback))
            {
                ShowFeedbackWithAudioClientRpc(
                    CombineFeedbackMessages(feedback, openTrapFeedback),
                    false,
                    ActionAudioCue.InventoryTrap,
                    BuildClientRpcParams(rpcParams));
                return;
            }

            ShowFeedbackWithAudioClientRpc(
                feedback,
                false,
                ActionAudioCue.InventoryLockpickSuccess,
                BuildClientRpcParams(rpcParams));
        }

        if (TryTriggerTrapOnOpen(controller, out string trapFeedback))
        {
            ShowFeedbackWithAudioClientRpc(
                trapFeedback,
                false,
                ActionAudioCue.InventoryTrap,
                BuildClientRpcParams(rpcParams));
            return;
        }

        if (!TryTakeAllItemsForCharacterServer(playerRoot, controller, inventory, rpcParams)
            && interactableCategory == InteractableCategory.Container)
        {
            OpenLootClientRpc(BuildClientRpcParams(rpcParams));
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestTakeServerRpc(string itemId, int quantity, ServerRpcParams rpcParams = default)
    {
        if (string.IsNullOrWhiteSpace(itemId) || quantity <= 0)
        {
            return;
        }

        Transform playerRoot = NetcodePlayerUtils.GetPlayerTransform(rpcParams.Receive.SenderClientId);
        if (!IsCharacterInRange(playerRoot))
        {
            return;
        }

        if (IsLockedForInteraction())
        {
            ShowFeedbackWithAudioClientRpc(
                GetMissingKeyFeedback(),
                false,
                ActionAudioCue.UiInvalid,
                BuildClientRpcParams(rpcParams));
            return;
        }

        Item item = ItemRegistry.Resolve(itemId);
        if (item == null)
        {
            return;
        }

        LootItemEntry entry = null;
        if (storedItems != null)
        {
            for (int i = 0; i < storedItems.Count; i++)
            {
                LootItemEntry candidate = storedItems[i];
                if (candidate != null && candidate.item == item)
                {
                    entry = candidate;
                    break;
                }
            }
        }

        if (entry == null)
        {
            return;
        }

        if (!item.CanTakeFromContainer(this, out string reason))
        {
            ShowFeedbackClientRpc(reason, false, BuildClientRpcParams(rpcParams));
            return;
        }

        NetworkInventory inventory = GetNetworkInventoryForCharacter(playerRoot);
        SquadCharacterController controller = GetControllerFromRoot(playerRoot);
        if (inventory == null || controller == null)
        {
            return;
        }

        int available = Mathf.Max(0, entry.quantity);
        int toTake = Mathf.Min(available, quantity);
        if (toTake <= 0)
        {
            return;
        }

        controller.AddItem(item, toTake);
        inventory.SyncFromController();

        entry.quantity = Mathf.Max(0, entry.quantity - toTake);
        if (entry.quantity <= 0)
        {
            storedItems.Remove(entry);
        }

        SyncNetFromLootItems();
        HandleEmptyContainer();
        ShowFeedbackWithAudioClientRpc(
            item.GetTakeSuccessMessage(),
            false,
            ActionAudioCue.InventoryTake,
            BuildClientRpcParams(rpcParams));
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestTakeAllServerRpc(ServerRpcParams rpcParams = default)
    {
        Transform playerRoot = NetcodePlayerUtils.GetPlayerTransform(rpcParams.Receive.SenderClientId);
        if (!IsCharacterInRange(playerRoot))
        {
            return;
        }

        if (IsLockedForInteraction())
        {
            ShowFeedbackWithAudioClientRpc(
                GetMissingKeyFeedback(),
                false,
                ActionAudioCue.UiInvalid,
                BuildClientRpcParams(rpcParams));
            return;
        }

        if (!allowTake)
        {
            ShowFeedbackWithAudioClientRpc(
                takeBlockedMessage,
                false,
                ActionAudioCue.UiInvalid,
                BuildClientRpcParams(rpcParams));
            return;
        }

        NetworkInventory inventory = GetNetworkInventoryForCharacter(playerRoot);
        SquadCharacterController controller = GetControllerFromRoot(playerRoot);
        if (!TryTakeAllItemsForCharacterServer(playerRoot, controller, inventory, rpcParams))
        {
            return;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestDepositServerRpc(string itemId, int quantity, ServerRpcParams rpcParams = default)
    {
        if (string.IsNullOrWhiteSpace(itemId) || quantity <= 0)
        {
            return;
        }

        Transform playerRoot = NetcodePlayerUtils.GetPlayerTransform(rpcParams.Receive.SenderClientId);
        if (!IsCharacterInRange(playerRoot))
        {
            return;
        }

        if (IsLockedForInteraction())
        {
            ShowFeedbackWithAudioClientRpc(
                GetMissingKeyFeedback(),
                false,
                ActionAudioCue.UiInvalid,
                BuildClientRpcParams(rpcParams));
            return;
        }

        Item item = ItemRegistry.Resolve(itemId);
        if (item == null)
        {
            return;
        }

        if (!item.CanDepositToContainer(this, out string reason))
        {
            ShowFeedbackClientRpc(reason, false, BuildClientRpcParams(rpcParams));
            return;
        }

        SquadCharacterController controller = GetControllerFromRoot(playerRoot);
        NetworkInventory inventory = GetNetworkInventoryForCharacter(playerRoot);
        if (controller == null || inventory == null)
        {
            return;
        }

        int remainingCapacity = GetRemainingCapacity();
        if (remainingCapacity <= 0 || quantity > remainingCapacity)
        {
            ShowFeedbackWithAudioClientRpc(
                depositNoSpaceMessage,
                false,
                ActionAudioCue.UiInvalid,
                BuildClientRpcParams(rpcParams));
            return;
        }

        if (!controller.TryRemoveItemQuantity(item, quantity))
        {
            return;
        }

        if (storedItems == null)
        {
            storedItems = new List<LootItemEntry>();
        }

        LootItemEntry existing = null;
        for (int i = 0; i < storedItems.Count; i++)
        {
            LootItemEntry entry = storedItems[i];
            if (entry != null && entry.item == item)
            {
                existing = entry;
                break;
            }
        }

        if (existing != null)
        {
            existing.quantity = Mathf.Max(0, existing.quantity + quantity);
        }
        else
        {
            storedItems.Add(new LootItemEntry { item = item, quantity = quantity });
        }

        inventory.SyncFromController();
        SyncNetFromLootItems();
        ShowFeedbackWithAudioClientRpc(
            item.GetDepositSuccessMessage(),
            false,
            ActionAudioCue.InventoryDeposit,
            BuildClientRpcParams(rpcParams));
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestBreakServerRpc(string itemId, ServerRpcParams rpcParams = default)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return;
        }

        Transform playerRoot = NetcodePlayerUtils.GetPlayerTransform(rpcParams.Receive.SenderClientId);
        if (!IsCharacterInRange(playerRoot))
        {
            return;
        }

        if (IsLockedForInteraction())
        {
            ShowFeedbackWithAudioClientRpc(
                GetMissingKeyFeedback(),
                false,
                ActionAudioCue.UiInvalid,
                BuildClientRpcParams(rpcParams));
            return;
        }

        Item item = ItemRegistry.Resolve(itemId);
        if (item == null)
        {
            return;
        }

        LootItemEntry entry = null;
        if (storedItems != null)
        {
            for (int i = 0; i < storedItems.Count; i++)
            {
                LootItemEntry candidate = storedItems[i];
                if (candidate != null && candidate.item == item)
                {
                    entry = candidate;
                    break;
                }
            }
        }

        if (entry == null)
        {
            return;
        }

        if (!item.HasBreakResults())
        {
            ShowFeedbackWithAudioClientRpc(
                breakInvalidMessage,
                true,
                ActionAudioCue.UiInvalid,
                BuildClientRpcParams(rpcParams));
            return;
        }

        int totalResults = GetBreakResultTotal(item);
        if (maxStoredQuantity > 0)
        {
            int remaining = GetRemainingCapacity();
            int effectiveRemaining = remaining + 1;
            if (totalResults > effectiveRemaining)
            {
                ShowFeedbackWithAudioClientRpc(
                    breakNoSpaceMessage,
                    true,
                    ActionAudioCue.UiInvalid,
                    BuildClientRpcParams(rpcParams));
                return;
            }
        }

        entry.quantity = Mathf.Max(0, entry.quantity - 1);
        if (entry.quantity <= 0)
        {
            storedItems.Remove(entry);
        }

        ApplyBreakResults(item);
        SyncNetFromLootItems();
        HandleEmptyContainer();
        ShowFeedbackWithAudioClientRpc(
            item.GetBreakSuccessMessage(),
            true,
            ActionAudioCue.InventoryBreak,
            BuildClientRpcParams(rpcParams));
    }

    [ClientRpc]
    private void ShowFeedbackClientRpc(string message, bool isBreak, ClientRpcParams rpcParams = default)
    {
        if (!isBreak)
        {
            ClearPendingUnlockAttempt();
        }

        ShowFeedbackLocal(message, isBreak);
    }

    private void ShowFeedbackLocal(string message, bool isBreak)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        if (isBreak)
        {
            ShowBreakFeedback(message);
        }
        else
        {
            ShowActionFeedback(message);
        }
    }

    [ClientRpc]
    private void ShowFeedbackWithAudioClientRpc(
        string message,
        bool isBreak,
        ActionAudioCue audioCue,
        ClientRpcParams rpcParams = default)
    {
        if (!isBreak)
        {
            ClearPendingUnlockAttempt();
        }

        if (audioCue != ActionAudioCue.None)
        {
            PlayActionAudio(audioCue);
        }

        ShowFeedbackLocal(message, isBreak);
    }

    [ClientRpc]
    private void OpenLootClientRpc(ClientRpcParams rpcParams = default)
    {
        ClearPendingUnlockAttempt();
        if (!isActiveAndEnabled || lootOpen || interactableCategory != InteractableCategory.Container)
        {
            return;
        }

        OpenLoot();
    }

    private static ClientRpcParams BuildClientRpcParams(ServerRpcParams rpcParams)
    {
        return new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new[] { rpcParams.Receive.SenderClientId }
            }
        };
    }

    private InventoryPanelController GetInventoryPanelController()
    {
        if (linkedInventoryPanelController != null)
        {
            return linkedInventoryPanelController;
        }

#if UNITY_2023_1_OR_NEWER
        linkedInventoryPanelController = FindAnyObjectByType<InventoryPanelController>();
#else
        linkedInventoryPanelController = FindAnyObjectByType<InventoryPanelController>();
#endif
        return linkedInventoryPanelController;
    }

    public void NotifyDepositInventoryClosed()
    {
        depositInventoryOpen = false;
        suppressReturnFrame = Time.frameCount;
    }

    private void SetSquadInputLock(bool locked)
    {
        if (SquadManager.Instance == null)
        {
            return;
        }

        if (locked)
        {
            if (squadInputLocked)
            {
                return;
            }

            SquadManager.Instance.SetInputLocked(true);
            squadInputLocked = true;
            return;
        }

        if (!squadInputLocked)
        {
            return;
        }

        SquadManager.Instance.SetInputLocked(false);
        squadInputLocked = false;
    }

    private GameObject GetSquadCharacter(Collider other)
    {
        if (other == null)
        {
            return null;
        }

        Transform current = other.transform;
        bool hasPlayerTag = false;
        GameObject taggedPlayerRoot = null;
        GameObject squadRoot = null;
        bool hasSquadList = SquadManager.Instance != null && SquadManager.Instance.squadCharacters != null;
        while (current != null)
        {
            if (current.CompareTag("Player"))
            {
                hasPlayerTag = true;
                taggedPlayerRoot = current.gameObject;
            }

            if (hasSquadList && SquadManager.Instance.squadCharacters.Contains(current.gameObject))
            {
                squadRoot = current.gameObject;
            }

            current = current.parent;
        }

        if (squadRoot == null && hasSquadList)
        {
            Transform root = other.transform.root;
            if (root != null)
            {
                if (root.CompareTag("Player"))
                {
                    hasPlayerTag = true;
                    taggedPlayerRoot = root.gameObject;
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

        if (squadRoot != null)
        {
            return squadRoot;
        }

        if (hasPlayerTag && taggedPlayerRoot != null)
        {
            return taggedPlayerRoot;
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

public class LootSlotUI : MonoBehaviour, IPointerEnterHandler, ISelectHandler
{
    public InteractableItem Owner { get; private set; }
    public InteractableItem.LootItemEntry Entry { get; private set; }
    public Item Item { get; private set; }
    public int Quantity { get; private set; }
    public RectTransform SlotRect { get; private set; }

    public void Initialize(InteractableItem owner, InteractableItem.LootItemEntry entry)
    {
        Owner = owner;
        Entry = entry;
        Item = entry != null ? entry.item : null;
        Quantity = entry != null ? Mathf.Max(0, entry.quantity) : 0;
        SlotRect = GetComponent<RectTransform>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (Owner != null)
        {
            Owner.FocusSlot(this);
        }
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (Owner != null)
        {
            Owner.FocusSlot(this);
        }
    }
}
