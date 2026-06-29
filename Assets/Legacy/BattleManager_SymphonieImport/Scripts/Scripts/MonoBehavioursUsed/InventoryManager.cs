using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI; // Requis pour manipuler les ScrollRect/LayoutElement lors de la navigation.
using TMPro;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    [Header("Configuration initiale")]
    [FormerlySerializedAs("inventoryItems")]
    [SerializeField, HideInInspector] private List<ItemData> legacySerializedItems = new();

    [Tooltip("Piles d'objets disponibles au lancement. Chaque entrée référence un ScriptableObject ItemData.")]
    [SerializeField] private List<InventoryItemStack> itemStacks = new();

    private const int MaxEquippedSealCount = 10;

    /// <summary>
    /// Expose le plafond de Sceaux équipables défini par la documentation de design.
    /// </summary>
    public static int MaxEquippedSeals => MaxEquippedSealCount;

    [Header("Sceaux - Accessibilité & Défi")]
    [Tooltip("Catalogue complet des Sceaux débloqués par le joueur. Chaque entrée référence un ScriptableObject SceauSO.")]
    [SerializeField] private List<SceauSO> unlockedSeals = new();

    [Tooltip("Sceaux actuellement équipés. Ils modulent la difficulté et sont pris en compte lors des combats.")]
    [SerializeField] private List<SceauSO> equippedSeals = new();

    [Header("Répertoire des Sceaux disponibles")]
    [Tooltip("Liste de référence utilisée pour restaurer les Sceaux via leurs IDs de sauvegarde.")]
    [SerializeField] private List<SceauSO> registeredSeals = new();

    /// <summary>
    /// Permet de consulter la liste des Sceaux débloqués sans exposer la liste modifiable.
    /// </summary>
    public IReadOnlyList<SceauSO> UnlockedSeals => unlockedSeals;

    /// <summary>
    /// Permet de connaître rapidement les Sceaux actifs à appliquer lors d'un combat.
    /// </summary>
    public IReadOnlyList<SceauSO> EquippedSeals => equippedSeals;

    /// <summary>
    /// Jeux de données internes destinés à accélérer les recherches et éviter les doublons.
    /// </summary>
    private readonly HashSet<SceauSO> unlockedSealsLookup = new();
    private readonly HashSet<SceauSO> equippedSealsLookup = new();

    [Header("Interface utilisateur - Inventaire")]
    [Tooltip("CanvasGroup principal de l'inventaire. Il est fondu lors de l'ouverture/fermeture.")]
    [SerializeField] private CanvasGroup inventoryCanvasGroup;
    [Tooltip("Durée en secondes du fondu d'ouverture/fermeture de l'inventaire.")]
    [SerializeField] private float inventoryFadeDuration = 2f;
    [Tooltip("Définit si l'inventaire doit être visible au lancement.")]
    [SerializeField] private bool openInventoryOnStart = false;

    [Header("Interface utilisateur - Navigation avancée")]
    [Tooltip("Temps minimal (en secondes) entre deux déplacements consécutifs du curseur de slot. Un petit délai évite les défilements trop rapides lorsque le joystick est fortement incliné.")]
    [SerializeField] private float navigationRepeatDelay = 0.25f;

    [Tooltip("Nom du panel sélectionné par défaut lors de l'ouverture de l'inventaire.")]
    [SerializeField] private string defaultPanelName = "Inventory_Sets_Panel";

    [Tooltip("Nom du sous-panel sélectionné par défaut lors de l'ouverture de l'inventaire.")]
    [SerializeField] private string defaultSubPanelName = "Inventory_MusicalMovesSet_SubPanel";

    private InventoryDataService inventoryData;

    /// <summary>
    /// Permet d'interroger directement les piles (utile pour les interfaces avancées).
    /// </summary>
    public IReadOnlyList<InventoryItemStack> ItemStacks => inventoryData?.ItemStacks ?? itemStacks;

    /// <remarks>
    /// La gestion des buffs, débuffs et autres états temporaires a été extraite dans
    /// <see cref="CharacterStatusEffectController"/> afin de clarifier les responsabilités.
    /// Conserver cette classe centrée sur l'inventaire améliore la maintenabilité globale.
    /// </remarks>

    /// <summary>
    /// Coroutine en cours responsable du fondu d'alpha du CanvasGroup.
    /// On la conserve pour éviter d'empiler plusieurs transitions concurrentes.
    /// </summary>
    private Coroutine inventoryFadeRoutine;

    /// <summary>
    /// Lorsque ce drapeau est à <c>true</c>, l'inventaire est pleinement affiché.
    /// Il nous permet de connaître l'état actuel sans nous baser uniquement sur l'alpha.
    /// </summary>
    private bool isInventoryVisible;

    /// <summary>
    /// Action d'input utilisée pour ouvrir/fermer l'inventaire.
    /// Stockée pour pouvoir se désabonner proprement.
    /// </summary>
    private InputAction inventoryToggleAction;

    /// <summary>
    /// Routine utilisée pour attendre que l'InputsManager soit disponible si nécessaire.
    /// </summary>
    private Coroutine waitForInputsRoutine;

    /// <summary>
    /// Curseur visuel dédié au panel sélectionné. On le positionne dynamiquement pour offrir un retour clair au joueur.
    /// </summary>
    private RectTransform panelCursorRect;

    /// <summary>
    /// Curseur visuel dédié au sous-panel actuellement ciblé.
    /// </summary>
    private RectTransform subPanelCursorRect;

    /// <summary>
    /// Curseur visuel affichant le slot choisi (item ou MusicalMove).
    /// </summary>
    private RectTransform slotCursorRect;

    /// <summary>
    /// Racine contenant les différents panels d'inventaire (Sets, Items, etc.).
    /// </summary>
    private RectTransform panelsRootRect;

    /// <summary>
    /// Liste ordonnée des panels actuellement détectés. Elle est recalculée à chaque ouverture pour tenir compte des ajouts/suppressions en scène.
    /// </summary>
    private readonly List<RectTransform> orderedPanels = new();

    /// <summary>
    /// Association entre un panel et ses sous-panels (s'il en possède). Un panel sans sous-panels est représenté par une liste vide.
    /// </summary>
    private readonly Dictionary<RectTransform, List<RectTransform>> orderedSubPanels = new();

    /// <summary>
    /// Association entre un conteneur (panel ou sous-panel) et ses slots disponibles.
    /// </summary>
    private readonly Dictionary<RectTransform, List<RectTransform>> orderedSlots = new();

    /// <summary>
    /// Index courant dans orderedPanels. On le conserve pour faciliter les déplacements gauche/droite.
    /// </summary>
    private int currentPanelIndex;

    /// <summary>
    /// Index courant dans la liste des sous-panels du panel sélectionné.
    /// </summary>
    private int currentSubPanelIndex;

    /// <summary>
    /// Index courant dans la liste des slots affichés.
    /// </summary>
    private int currentSlotIndex;

    /// <summary>
    /// Panel actuellement sélectionné (référence directe pour accélérer les comparaisons et repositionner le curseur).
    /// </summary>
    private RectTransform currentPanelRect;

    /// <summary>
    /// Sous-panel actuellement sélectionné (peut être null si le panel n'en possède pas).
    /// </summary>
    private RectTransform currentSubPanelRect;

    /// <summary>
    /// Slot actuellement mis en avant.
    /// </summary>
    private RectTransform currentSlotRect;

    /// <summary>
    /// Horodatage (en temps non-scalé) du dernier déplacement de slot. Permet de filtrer les répétitions trop rapides du joystick.
    /// </summary>
    private float lastNavigationTime;

    /// <summary>
    /// Lorsque vrai, les callbacks d'inputs de l'ActionMap Inventory ont déjà été branchés.
    /// </summary>
    private bool inventoryActionsHooked;

    /// <summary>
    /// Actions supplémentaires du mapping Inventory utilisées pour la navigation.
    /// </summary>
    private InputAction inventorySelectPanelLeftAction;
    private InputAction inventorySelectPanelRightAction;
    private InputAction inventorySelectSubPanelLeftAction;
    private InputAction inventorySelectSubPanelRightAction;
    private InputAction inventoryNavigateAction;
    private InputAction inventoryConfirmAction;
    private InputAction inventoryCancelAction;

    [Header("Inventaire - Colonnes et descriptions")]
    [SerializeField] private string itemColumnSubPanelName = "Inventory_ItemSet_SubPanel";
    [SerializeField] private string moveColumnSubPanelName = "Inventory_MusicalMovesSet_SubPanel";
    [SerializeField] private string itemColumnTitle = "Items";
    [SerializeField] private string moveColumnTitle = "Mouvements";
    [SerializeField] private TMP_Text descriptionTitleText;
    [SerializeField] private TMP_Text descriptionBodyText;
    [SerializeField] private Image descriptionIcon;

    private RectTransform itemSubPanelRect;
    private RectTransform moveSubPanelRect;
    private RectTransform itemContentRect;
    private RectTransform moveContentRect;

    private readonly Dictionary<RectTransform, InventoryEntry> slotEntries = new();
    private readonly Dictionary<RectTransform, int> slotIndexByOwner = new();

    private enum InventoryEntryType
    {
        Item,
        Move
    }

    private struct InventoryEntry
    {
        public InventoryEntryType Type;
        public ItemData Item;
        public MusicalMoveSO Move;
        public string DisplayName;
        public string Description;
        public Sprite Icon;
        public int Quantity;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        inventoryData = new InventoryDataService(itemStacks);

        // Conversion automatique des anciennes sauvegardes qui utilisaient une
        // simple liste de ScriptableObjects pour représenter l'inventaire.
        if (legacySerializedItems != null && legacySerializedItems.Count > 0)
        {
            foreach (var legacyItem in legacySerializedItems)
                AddItemInternal(legacyItem, 1, false);
            legacySerializedItems.Clear();
        }

        // Les Sceaux suivent la même logique : on nettoie les données, on reconstruit les caches
        // et on impose une hiérarchie déterministe pour l'interface et les sauvegardes.
        InitializeSealCollections();

        // Applique l'état chargé si une sauvegarde a déjà été lue.
        if (GameManager.Instance != null && GameManager.Instance.gameData != null)
            GameManager.Instance.gameData.ApplySealsTo(this);

        // On prépare l'état visuel initial de l'inventaire (ouvert ou fermé).
        InitializeInventoryUI();

        // Dès que possible, on s'abonne à l'input d'ouverture/fermeture.
        EnsureInventoryInputSubscription();
    }

    private void OnEnable()
    {
        // Lors d'un rechargement de scène ou d'une réactivation du GameObject,
        // on vérifie que le CanvasGroup est correctement configuré.
        InitializeInventoryUI();

        // On relance la souscription à l'action d'inventaire si nécessaire.
        EnsureInventoryInputSubscription();
    }

    private void OnDisable()
    {
        // On évite les fuites d'évènements en se désabonnant systématiquement.
        ReleaseInventoryInputSubscription();

        // Si une routine d'attente était active (InputsManager manquant), on l'annule.
        if (waitForInputsRoutine != null)
        {
            StopCoroutine(waitForInputsRoutine);
            waitForInputsRoutine = null;
        }
    }

    /// <summary>
    /// Renvoie la liste des items que le joueur possède.
    /// </summary>
    public IReadOnlyList<ItemData> GetInventoryItems() =>
        inventoryData?.BuildInventorySnapshot() ?? Array.Empty<ItemData>();

    /// <summary>
    /// Renvoie la liste des items possédés et utilisables en combat.
    /// Lorsque <paramref name="prioritizedFor"/> est défini, les items sont
    /// réordonnés pour placer les favoris du set actif en tête de liste.
    /// </summary>
    public List<ItemData> GetUsableItems(CharacterUnit prioritizedFor = null)
    {
        List<ItemData> usable = inventoryData != null
            ? inventoryData.BuildUsableItems()
            : new List<ItemData>();

        if (prioritizedFor == null || prioritizedFor.Data == null)
            return usable;

        return prioritizedFor.OrderItemsForCurrentSet(usable);
    }

    /// <summary>
    /// Ajoute un item à l'inventaire (uniquement si c'est un item valide du jeu).
    /// </summary>
    public void AddItem(ItemData item, int quantity = 1)
    {
        AddItemInternal(item, quantity, true);
    }

    private void AddItemInternal(ItemData item, int quantity, bool logAddition)
    {
        if (inventoryData == null)
            return;

        if (!inventoryData.AddItem(item, quantity))
            return;

        if (logAddition)
            Debug.Log($"[Inventory] Ajout de l'objet : {item.itemName} (x{quantity}).");

        RefreshInventoryIfVisible();
    }

    /// <summary>
    /// Ajoute une série d'items à l'inventaire (conserve la compatibilité avec l'ancien appel).
    /// </summary>
    public void AddItem(List<ItemData> items)
    {
        foreach (ItemData item in items)
        {
            AddItem(item);
        }        
    }

    /// <summary>
    /// Utilise un item (et l'enlève de l'inventaire). La cible doit être définie à l'avance.
    /// </summary>
    public void UseItem(ItemData item, CharacterUnit caster, CharacterUnit target, bool isCritical)
    {
        if (!CanUseItem(item))
        {
            Debug.LogWarning($"Limite d'utilisation atteinte pour {item.itemName}.");
            return;
        }

        Debug.Log($"[Inventory] Utilisation de l'objet : {item.itemName} sur {target.Data.characterName}");

        ItemEffectExecutor.ApplyEffect(item, caster, target, isCritical);
        RegisterItemUse(item);
    }

    /// <summary>
    /// Utilise un item sans le consommer (debug ou test).
    /// </summary>
    public void PreviewItemEffect(ItemData item, CharacterUnit target)
    {
        Debug.Log($"[Inventory] Aperçu de l'effet de {item.itemName} sur {target.Data.characterName}");
        ItemEffectExecutor.ApplyEffect(item, null, target, false);
    }

    // ------------------------------------------------------------------
    // Gestion des limitations d'utilisation des items
    // ------------------------------------------------------------------

    /// <summary>
    /// Vérifie si l'item peut être utilisé en fonction des limites définies.
    /// </summary>
    public bool CanUseItem(ItemData item)
    {
        return inventoryData != null && inventoryData.CanUseItem(item);
    }

    /// <summary>
    /// Enregistre l'utilisation de l'item.
    /// </summary>
    public void RegisterItemUse(ItemData item)
    {
        inventoryData?.RegisterItemUse(item);
        RefreshInventoryIfVisible();
    }

    /// <summary>
    /// Réinitialise les compteurs d'utilisation par tour.
    /// </summary>
    public void ResetTurnItemUsage()
    {
        inventoryData?.ResetTurnUsage();
    }

    /// <summary>
    /// Réinitialise les compteurs d'utilisation par combat.
    /// </summary>
    public void ResetBattleItemUsage()
    {
        inventoryData?.ResetBattleUsage();
    }

    /// <summary>
    /// Configure l'apparence initiale de l'inventaire et sécurise la référence au CanvasGroup.
    /// </summary>
    private void InitializeInventoryUI()
    {
        // Si aucun CanvasGroup n'est assigné dans l'inspecteur, on tente d'en trouver un
        // parmi les enfants pour éviter une erreur bloquante. Cette solution de repli
        // reste volontairement verbeuse afin de faciliter les diagnostics.
        if (inventoryCanvasGroup == null)
        {
            inventoryCanvasGroup = GetComponentInChildren<CanvasGroup>(true);
            if (inventoryCanvasGroup == null)
            {
                Debug.LogWarning("[InventoryManager] Aucun CanvasGroup assigné pour l'inventaire. Le fondu sera désactivé.");
                return;
            }
        }

        // On force l'alpha et l'interactivité selon l'état initial souhaité.
        isInventoryVisible = openInventoryOnStart;
        float initialAlpha = isInventoryVisible ? 1f : 0f;
        inventoryCanvasGroup.alpha = initialAlpha;
        inventoryCanvasGroup.interactable = isInventoryVisible;
        inventoryCanvasGroup.blocksRaycasts = isInventoryVisible;
    }

    /// <summary>
    /// Assure l'abonnement à l'action d'input "Inventory" du mapping Inventory.
    /// </summary>
    private void EnsureInventoryInputSubscription()
    {
        if (!isActiveAndEnabled)
            return;

        var inputsManager = InputsManager.Instance;
        if (inputsManager == null || inputsManager.playerInputs == null)
        {
            // L'InputsManager peut être instancié plus tard dans le cycle de vie.
            // On patiente donc via une coroutine dédiée.
            if (waitForInputsRoutine == null)
                waitForInputsRoutine = StartCoroutine(WaitForInputsManager());
            return;
        }

        var inventoryActions = inputsManager.playerInputs.Inventory;

        // On crée dynamiquement la référence à l'action Inventory si nécessaire.
        if (inventoryToggleAction == null)
        {
            inventoryToggleAction = inventoryActions.Inventory;
            if (inventoryToggleAction == null)
            {
                Debug.LogWarning("[InventoryManager] L'action 'Inventory' est introuvable dans l'ActionMap Inventory.");
            }
            else
            {
                inventoryToggleAction.performed += OnInventoryActionPerformed;
                inventoryToggleAction.Enable();
            }
        }

        // Même si l'action Inventory n'est pas disponible, on tente de brancher les callbacks
        // secondaires afin d'éviter de multiples tentatives lors des prochains Awake/OnEnable.
        if (!inventoryActionsHooked)
        {
            HookInventoryNavigationActions(inventoryActions);
        }
    }

    /// <summary>
    /// Coroutine d'attente qui surveille l'apparition de l'InputsManager avant de s'inscrire aux inputs.
    /// </summary>
    private IEnumerator WaitForInputsManager()
    {
        while (InputsManager.Instance == null || InputsManager.Instance.playerInputs == null)
            yield return null;

        // La routine n'est plus nécessaire une fois l'InputsManager prêt.
        waitForInputsRoutine = null;
        EnsureInventoryInputSubscription();
    }

    /// <summary>
    /// Branche l'ensemble des callbacks nécessaires à la navigation dans l'inventaire.
    /// </summary>
    /// <param name="inventoryActions">Structure générée par l'Input System exposant les actions du mapping Inventory.</param>
    private void HookInventoryNavigationActions(PlayerInputs.InventoryActions inventoryActions)
    {
        // On protège les appels multiples afin d'éviter une accumulation des abonnements lors d'Awake/OnEnable successifs.
        if (inventoryActionsHooked)
            return;

        inventorySelectPanelLeftAction = inventoryActions.SelectPanel_Left;
        inventorySelectPanelRightAction = inventoryActions.SelectPanel_Right;
        inventorySelectSubPanelLeftAction = inventoryActions.SelectSubPanel_Left;
        inventorySelectSubPanelRightAction = inventoryActions.SelectSubPanel_Right;
        inventoryNavigateAction = inventoryActions.Navigate;
        inventoryConfirmAction = inventoryActions.Confirm;
        inventoryCancelAction = inventoryActions.Cancel;

        if (inventorySelectPanelLeftAction != null)
        {
            // On réactive explicitement l'action au cas où elle aurait été désactivée lors d'un précédent OnDisable.
            // Sans cela, le simple fait de réactiver l'ActionMap ne suffit pas et la navigation reste bloquée.
            if (!inventorySelectPanelLeftAction.enabled)
                inventorySelectPanelLeftAction.Enable();

            inventorySelectPanelLeftAction.performed += OnInventorySelectPanelLeft;
        }

        if (inventorySelectPanelRightAction != null)
        {
            if (!inventorySelectPanelRightAction.enabled)
                inventorySelectPanelRightAction.Enable();

            inventorySelectPanelRightAction.performed += OnInventorySelectPanelRight;
        }

        if (inventorySelectSubPanelLeftAction != null)
        {
            if (!inventorySelectSubPanelLeftAction.enabled)
                inventorySelectSubPanelLeftAction.Enable();

            inventorySelectSubPanelLeftAction.performed += OnInventorySelectSubPanelLeft;
        }

        if (inventorySelectSubPanelRightAction != null)
        {
            if (!inventorySelectSubPanelRightAction.enabled)
                inventorySelectSubPanelRightAction.Enable();

            inventorySelectSubPanelRightAction.performed += OnInventorySelectSubPanelRight;
        }

        if (inventoryNavigateAction != null)
        {
            if (!inventoryNavigateAction.enabled)
                inventoryNavigateAction.Enable();

            inventoryNavigateAction.performed += OnInventoryNavigate;
        }

        if (inventoryConfirmAction != null)
        {
            if (!inventoryConfirmAction.enabled)
                inventoryConfirmAction.Enable();

            inventoryConfirmAction.performed += OnInventoryConfirm;
        }

        if (inventoryCancelAction != null)
        {
            if (!inventoryCancelAction.enabled)
                inventoryCancelAction.Enable();

            inventoryCancelAction.performed += OnInventoryCancel;
        }

        inventoryActionsHooked = true;
    }

    /// <summary>
    /// Désabonne proprement toutes les actions du mapping Inventory pour éviter les fuites d'évènements.
    /// </summary>
    private void UnhookInventoryNavigationActions()
    {
        if (!inventoryActionsHooked)
            return;

        if (inventorySelectPanelLeftAction != null)
        {
            inventorySelectPanelLeftAction.performed -= OnInventorySelectPanelLeft;
            if (inventorySelectPanelLeftAction.enabled)
                inventorySelectPanelLeftAction.Disable();
            inventorySelectPanelLeftAction = null;
        }

        if (inventorySelectPanelRightAction != null)
        {
            inventorySelectPanelRightAction.performed -= OnInventorySelectPanelRight;
            if (inventorySelectPanelRightAction.enabled)
                inventorySelectPanelRightAction.Disable();
            inventorySelectPanelRightAction = null;
        }

        if (inventorySelectSubPanelLeftAction != null)
        {
            inventorySelectSubPanelLeftAction.performed -= OnInventorySelectSubPanelLeft;
            if (inventorySelectSubPanelLeftAction.enabled)
                inventorySelectSubPanelLeftAction.Disable();
            inventorySelectSubPanelLeftAction = null;
        }

        if (inventorySelectSubPanelRightAction != null)
        {
            inventorySelectSubPanelRightAction.performed -= OnInventorySelectSubPanelRight;
            if (inventorySelectSubPanelRightAction.enabled)
                inventorySelectSubPanelRightAction.Disable();
            inventorySelectSubPanelRightAction = null;
        }

        if (inventoryNavigateAction != null)
        {
            inventoryNavigateAction.performed -= OnInventoryNavigate;
            if (inventoryNavigateAction.enabled)
                inventoryNavigateAction.Disable();
            inventoryNavigateAction = null;
        }

        if (inventoryConfirmAction != null)
        {
            inventoryConfirmAction.performed -= OnInventoryConfirm;
            if (inventoryConfirmAction.enabled)
                inventoryConfirmAction.Disable();
            inventoryConfirmAction = null;
        }

        if (inventoryCancelAction != null)
        {
            inventoryCancelAction.performed -= OnInventoryCancel;
            if (inventoryCancelAction.enabled)
                inventoryCancelAction.Disable();
            inventoryCancelAction = null;
        }

        inventoryActionsHooked = false;
    }

    /// <summary>
    /// Désabonne l'action d'input et réinitialise les références associées.
    /// </summary>
    private void ReleaseInventoryInputSubscription()
    {
        if (inventoryToggleAction != null)
        {
            inventoryToggleAction.performed -= OnInventoryActionPerformed;
            inventoryToggleAction.Disable();
            inventoryToggleAction = null;
        }

        UnhookInventoryNavigationActions();
    }

    /// <summary>
    /// Callback invoqué par l'Input System lorsqu'un appui valide est détecté.
    /// </summary>
    private void OnInventoryActionPerformed(InputAction.CallbackContext context)
    {
        // La méthode n'est appelée que pour l'évènement "performed", mais ce test garde
        // la fonction robuste en cas de changement futur.
        if (!context.performed)
            return;

        ToggleInventory();
    }

    /// <summary>
    /// Navigation vers le panel précédent.
    /// </summary>
    private void OnInventorySelectPanelLeft(InputAction.CallbackContext context)
    {
        if (!context.performed || !isInventoryVisible)
            return;

        ChangePanel(-1);
    }

    /// <summary>
    /// Navigation vers le panel suivant.
    /// </summary>
    private void OnInventorySelectPanelRight(InputAction.CallbackContext context)
    {
        if (!context.performed || !isInventoryVisible)
            return;

        ChangePanel(1);
    }

    /// <summary>
    /// Navigation vers le sous-panel précédent.
    /// </summary>
    private void OnInventorySelectSubPanelLeft(InputAction.CallbackContext context)
    {
        if (!context.performed || !isInventoryVisible)
            return;

        ChangeSubPanel(-1);
    }

    /// <summary>
    /// Navigation vers le sous-panel suivant.
    /// </summary>
    private void OnInventorySelectSubPanelRight(InputAction.CallbackContext context)
    {
        if (!context.performed || !isInventoryVisible)
            return;

        ChangeSubPanel(1);
    }

    /// <summary>
    /// Navigation dans la liste des slots via le joystick.
    /// </summary>
    private void OnInventoryNavigate(InputAction.CallbackContext context)
    {
        if (!isInventoryVisible)
            return;

        Vector2 input = context.ReadValue<Vector2>();
        if (input.sqrMagnitude < 0.25f)
            return; // Mouvement trop faible pour être pris en compte.

        if (Time.unscaledTime < lastNavigationTime + navigationRepeatDelay)
            return;

        bool horizontal = Mathf.Abs(input.x) > Mathf.Abs(input.y);
        if (horizontal)
        {
            if (input.x > 0.5f)
                ChangeSubPanel(1);
            else if (input.x < -0.5f)
                ChangeSubPanel(-1);
            else
                return;
        }
        else
        {
            if (input.y > 0.5f)
                MoveSlotSelection(-1); // Vers le haut : slot précédent.
            else if (input.y < -0.5f)
                MoveSlotSelection(1);  // Vers le bas : slot suivant.
            else
                return;
        }

        lastNavigationTime = Time.unscaledTime;
    }

    private void OnInventoryConfirm(InputAction.CallbackContext context)
    {
        if (!context.performed || !isInventoryVisible)
            return;

        if (!TryGetEntryForSlot(currentSlotRect, out var entry))
            return;

        if (HasDescriptionUI())
            return;

        InfoBoxManager.Instance?.OpenInfoBox(entry.DisplayName, entry.Description, entry.Icon);
    }

    private void OnInventoryCancel(InputAction.CallbackContext context)
    {
        if (!context.performed || !isInventoryVisible)
            return;

        SetInventoryVisibility(false);
    }

    /// <summary>
    /// Inverse l'état d'affichage de l'inventaire.
    /// </summary>
    public void ToggleInventory()
    {
        SetInventoryVisibility(!isInventoryVisible);
    }

    /// <summary>
    /// Appelé par l'InputsManager lorsqu'un appui sur World/Inventory est détecté.
    /// Permet d'ouvrir l'inventaire depuis l'exploration en s'assurant de ne pas tenter
    /// une ouverture multiple.
    /// </summary>
    public void OpenInventoryFromWorldInput()
    {
        if (isInventoryVisible)
            return;

        SetInventoryVisibility(true);
    }

    /// <summary>
    /// Applique un état ouvert/fermé et gère le fondu correspondant.
    /// </summary>
    /// <param name="visible">Nouvel état souhaité.</param>
    private void SetInventoryVisibility(bool visible)
    {
        if (inventoryCanvasGroup == null)
        {
            Debug.LogWarning("[InventoryManager] Impossible de modifier l'UI d'inventaire : CanvasGroup manquant.");
            return;
        }

        bool wasVisible = isInventoryVisible;
        isInventoryVisible = visible;

        // Lorsqu'on ouvre l'inventaire, on active immédiatement l'ActionMap dédiée
        // et on prépare l'état de sélection afin que le joueur comprenne où il se situe.
        if (visible && !wasVisible)
        {
            InputsManager.Instance?.EnterInventoryMode();
            PrepareInventoryNavigation();
        }
        else if (!visible && wasVisible)
        {
            InputsManager.Instance?.ExitInventoryMode();
            ClearInventorySelectionState();
        }

        // On interrompt toute transition précédente pour garantir un comportement déterministe.
        if (inventoryFadeRoutine != null)
        {
            StopCoroutine(inventoryFadeRoutine);
            inventoryFadeRoutine = null;
        }

        float targetAlpha = visible ? 1f : 0f;

        // Si la durée est quasi nulle, on applique immédiatement le résultat final.
        if (inventoryFadeDuration <= 0f)
        {
            inventoryCanvasGroup.alpha = targetAlpha;
            inventoryCanvasGroup.interactable = visible;
            inventoryCanvasGroup.blocksRaycasts = visible;
            return;
        }

        // Lors de l'ouverture on bloque immédiatement les clics sur le monde.
        if (visible)
        {
            inventoryCanvasGroup.blocksRaycasts = true;
        }
        else
        {
            // À la fermeture, l'interactivité est coupée dès le début du fondu pour éviter
            // qu'un clic résiduel déclenche une action pendant la transition.
            inventoryCanvasGroup.interactable = false;
        }

        inventoryFadeRoutine = StartCoroutine(FadeInventoryCanvas(targetAlpha, visible));
    }

    private void RefreshInventoryIfVisible()
    {
        if (!isInventoryVisible)
            return;

        RefreshInventoryColumns();
        RebuildNavigationCaches();

        if (currentPanelRect != null)
            RefreshSlotSelection(true);
    }

    /// <summary>
    /// Coroutine responsable de l'interpolation de l'alpha du CanvasGroup.
    /// </summary>
    /// <param name="targetAlpha">Alpha final à atteindre.</param>
    /// <param name="targetVisible">État logique attendu à la fin du fondu.</param>
    private IEnumerator FadeInventoryCanvas(float targetAlpha, bool targetVisible)
    {
        float startingAlpha = inventoryCanvasGroup.alpha;
        float elapsed = 0f;
        float duration = Mathf.Max(0.0001f, inventoryFadeDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime; // On utilise le temps non-scalé pour garder le fondu actif même en pause.
            float t = Mathf.Clamp01(elapsed / duration);
            inventoryCanvasGroup.alpha = Mathf.Lerp(startingAlpha, targetAlpha, t);
            yield return null;
        }

        // On force la valeur finale pour éviter les approximations flottantes.
        inventoryCanvasGroup.alpha = targetAlpha;
        inventoryCanvasGroup.interactable = targetVisible;
        inventoryCanvasGroup.blocksRaycasts = targetVisible;

        inventoryFadeRoutine = null;
    }

    /// <summary>
    /// Prépare l'état initial de la navigation lors de l'ouverture de l'inventaire.
    /// Cette méthode s'occupe de retrouver les différents panneaux, de les trier et
    /// de positionner les curseurs sur les éléments par défaut exigés par le cahier des charges.
    /// </summary>
    private void PrepareInventoryNavigation()
    {
        // Un délai trop court rendrait le joystick difficile à contrôler ; on impose donc un minimum raisonnable.
        navigationRepeatDelay = Mathf.Max(0.05f, navigationRepeatDelay);

        if (!EnsureNavigationReferences())
            return;

        orderedPanels.Clear();
        orderedSubPanels.Clear();
        orderedSlots.Clear();
        currentPanelIndex = 0;
        currentSubPanelIndex = 0;
        currentSlotIndex = 0;
        currentPanelRect = null;
        currentSubPanelRect = null;
        currentSlotRect = null;
        lastNavigationTime = Time.unscaledTime - navigationRepeatDelay;

        RefreshInventoryColumns();
        RebuildNavigationCaches();

        if (orderedPanels.Count == 0)
        {
            // Aucun panel détecté : on masque les curseurs pour éviter d'induire le joueur en erreur.
            UpdatePanelCursor();
            UpdateSubPanelCursor();
            UpdateSlotCursor();
            return;
        }

        // On sélectionne en priorité le panel par défaut demandé ; si absent, on retombe sur le premier panel disponible.
        SelectPanelByName(defaultPanelName);
    }

    /// <summary>
    /// Réinitialise l'état de navigation lors de la fermeture de l'inventaire.
    /// </summary>
    private void ClearInventorySelectionState()
    {
        currentPanelIndex = 0;
        currentSubPanelIndex = 0;
        currentSlotIndex = 0;
        currentPanelRect = null;
        currentSubPanelRect = null;
        currentSlotRect = null;
        orderedPanels.Clear();
        orderedSubPanels.Clear();
        orderedSlots.Clear();
        slotEntries.Clear();
        slotIndexByOwner.Clear();
        ClearSelectionDetails();

        if (panelCursorRect != null)
            panelCursorRect.gameObject.SetActive(false);

        if (subPanelCursorRect != null)
            subPanelCursorRect.gameObject.SetActive(false);

        if (slotCursorRect != null)
            slotCursorRect.gameObject.SetActive(false);
    }

    /// <summary>
    /// Analyse la hiérarchie de l'inventaire pour constituer les listes de panels, sous-panels et slots.
    /// </summary>
    private void RebuildNavigationCaches()
    {
        if (panelsRootRect == null)
            return;

        foreach (Transform child in panelsRootRect)
        {
            if (child is not RectTransform rect)
                continue;

            // On ne retient que les objets explicitement identifiés comme panels.
            if (!rect.gameObject.name.EndsWith("_Panel", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!rect.gameObject.activeInHierarchy)
                continue;

            orderedPanels.Add(rect);

            var subPanels = new List<RectTransform>();
            foreach (Transform sub in rect)
            {
                if (sub is RectTransform subRect && subRect.gameObject.name.EndsWith("_SubPanel", StringComparison.OrdinalIgnoreCase) && subRect.gameObject.activeInHierarchy)
                    subPanels.Add(subRect);
            }

            orderedSubPanels[rect] = subPanels;

            if (subPanels.Count == 0)
            {
                // Panel sans sous-panel : on travaille directement avec ses propres slots.
                orderedSlots[rect] = BuildSlotList(rect);
            }
            else
            {
                foreach (var sub in subPanels)
                    orderedSlots[sub] = BuildSlotList(sub);
            }
        }
    }

    private void RefreshInventoryColumns()
    {
        EnsureColumnReferences();
        slotEntries.Clear();

        UpdateColumnTitle(itemSubPanelRect, itemColumnTitle);
        UpdateColumnTitle(moveSubPanelRect, moveColumnTitle);

        PopulateColumn(itemContentRect, BuildItemEntries());
        PopulateColumn(moveContentRect, BuildMoveEntries());

        orderedSlots.Clear();
    }

    private void EnsureColumnReferences()
    {
        if (!string.IsNullOrEmpty(itemColumnSubPanelName))
            itemSubPanelRect ??= FindRectTransformInChildren(itemColumnSubPanelName);
        if (!string.IsNullOrEmpty(moveColumnSubPanelName))
            moveSubPanelRect ??= FindRectTransformInChildren(moveColumnSubPanelName);

        itemContentRect ??= FindContentTransform(itemSubPanelRect);
        moveContentRect ??= FindContentTransform(moveSubPanelRect);
    }

    private void UpdateColumnTitle(RectTransform subPanel, string title)
    {
        if (subPanel == null || string.IsNullOrEmpty(title))
            return;

        var titleTransform = subPanel.Find("IMM_Title");
        if (titleTransform == null)
            return;

        if (titleTransform.TryGetComponent(out TMP_Text titleText))
            titleText.text = title;
    }

    private List<InventoryEntry> BuildItemEntries()
    {
        var entries = new List<InventoryEntry>();
        var stacks = ItemStacks ?? Array.Empty<InventoryItemStack>();

        foreach (var stack in stacks)
        {
            if (stack == null || stack.item == null || stack.quantity <= 0)
                continue;

            string name = string.IsNullOrWhiteSpace(stack.item.itemName) ? stack.item.name : stack.item.itemName;
            string display = stack.quantity > 1 ? $"{name} x{stack.quantity}" : name;

            entries.Add(new InventoryEntry
            {
                Type = InventoryEntryType.Item,
                Item = stack.item,
                DisplayName = display,
                Description = stack.item.description ?? string.Empty,
                Icon = stack.item.itemIcon,
                Quantity = stack.quantity
            });
        }

        entries.Sort((a, b) => string.Compare(a.Item?.itemName ?? a.DisplayName, b.Item?.itemName ?? b.DisplayName, StringComparison.OrdinalIgnoreCase));
        return entries;
    }

    private List<InventoryEntry> BuildMoveEntries()
    {
        var entries = new List<InventoryEntry>();
        var codex = MusicalCodexManager.Instance;
        var moves = codex != null ? codex.knownMoves : null;

        if (moves != null)
        {
            var unique = new HashSet<MusicalMoveSO>();
            foreach (var move in moves)
            {
                if (move == null || !unique.Add(move))
                    continue;

                string displayName = string.IsNullOrWhiteSpace(move.moveName) ? move.name : move.moveName;
                entries.Add(new InventoryEntry
                {
                    Type = InventoryEntryType.Move,
                    Move = move,
                    DisplayName = displayName,
                    Description = move.description ?? string.Empty,
                    Icon = move.moveIcon
                });
            }
        }

        entries.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
        return entries;
    }

    private void PopulateColumn(RectTransform content, List<InventoryEntry> entries)
    {
        if (content == null)
            return;

        EnsureSlotCount(content, entries.Count);
        var slots = GetSlotsFromContent(content);

        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot == null)
                continue;

            if (i < entries.Count)
            {
                slot.gameObject.SetActive(true);
                ApplySlotVisuals(slot, entries[i]);
                slotEntries[slot] = entries[i];
            }
            else
            {
                slot.gameObject.SetActive(false);
                slotEntries.Remove(slot);
            }
        }
    }

    private static List<RectTransform> GetSlotsFromContent(RectTransform content)
    {
        var slots = new List<RectTransform>();
        if (content == null)
            return slots;

        for (int i = 0; i < content.childCount; i++)
        {
            if (content.GetChild(i) is RectTransform rect)
                slots.Add(rect);
        }

        return slots;
    }

    private void EnsureSlotCount(RectTransform content, int required)
    {
        if (required <= 0 || content == null)
            return;

        var slots = GetSlotsFromContent(content);
        if (slots.Count == 0)
        {
            Debug.LogWarning("[InventoryManager] Aucun slot disponible pour peupler l'inventaire.");
            return;
        }

        var template = slots[0];
        while (slots.Count < required)
        {
            var clone = Instantiate(template, content);
            clone.name = $"{template.name}_{slots.Count}";
            slots.Add(clone);
        }
    }

    private void ApplySlotVisuals(RectTransform slot, InventoryEntry entry)
    {
        if (slot == null)
            return;

        var label = slot.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
            label.text = entry.DisplayName;

        var icon = FindSlotIcon(slot);
        if (icon != null)
        {
            icon.sprite = entry.Icon;
            icon.enabled = entry.Icon != null;
        }
    }

    private static Image FindSlotIcon(RectTransform slot)
    {
        if (slot == null)
            return null;

        var images = slot.GetComponentsInChildren<Image>(true);
        foreach (var image in images)
        {
            if (image == null)
                continue;

            if (image.gameObject.name.IndexOf("Icon", StringComparison.OrdinalIgnoreCase) >= 0)
                return image;
        }

        return null;
    }

    private bool TryGetEntryForSlot(RectTransform slot, out InventoryEntry entry)
    {
        if (slot != null && slotEntries.TryGetValue(slot, out entry))
            return true;

        entry = default;
        return false;
    }

    private bool HasDescriptionUI() =>
        descriptionTitleText != null || descriptionBodyText != null || descriptionIcon != null;

    private void UpdateSelectionDetails()
    {
        if (!TryGetEntryForSlot(currentSlotRect, out var entry))
        {
            ClearSelectionDetails();
            return;
        }

        if (descriptionTitleText != null)
            descriptionTitleText.text = entry.DisplayName ?? string.Empty;

        if (descriptionBodyText != null)
            descriptionBodyText.text = entry.Description ?? string.Empty;

        if (descriptionIcon != null)
        {
            if (entry.Icon != null)
            {
                descriptionIcon.sprite = entry.Icon;
                descriptionIcon.gameObject.SetActive(true);
            }
            else
            {
                descriptionIcon.gameObject.SetActive(false);
            }
        }
    }

    private void ClearSelectionDetails()
    {
        if (descriptionTitleText != null)
            descriptionTitleText.text = string.Empty;

        if (descriptionBodyText != null)
            descriptionBodyText.text = string.Empty;

        if (descriptionIcon != null)
            descriptionIcon.gameObject.SetActive(false);
    }

    /// <summary>
    /// Sélectionne le panel correspondant au nom fourni. En l'absence de correspondance, on choisit le premier panel de la liste.
    /// </summary>
    private void SelectPanelByName(string panelName)
    {
        if (orderedPanels.Count == 0)
        {
            currentPanelRect = null;
            UpdatePanelCursor();
            UpdateSubPanelCursor();
            UpdateSlotCursor();
            return;
        }

        int targetIndex = 0;
        if (!string.IsNullOrEmpty(panelName))
        {
            for (int i = 0; i < orderedPanels.Count; i++)
            {
                if (string.Equals(orderedPanels[i]?.gameObject.name, panelName, StringComparison.OrdinalIgnoreCase))
                {
                    targetIndex = i;
                    break;
                }
            }
        }

        currentPanelIndex = WrapIndex(targetIndex, orderedPanels.Count);
        currentPanelRect = orderedPanels[currentPanelIndex];
        UpdatePanelCursor();
        SelectDefaultSubPanelForCurrentPanel();
    }

    /// <summary>
    /// Choisit le sous-panel par défaut pour le panel courant (ou aucun si le panel n'en possède pas).
    /// </summary>
    private void SelectDefaultSubPanelForCurrentPanel()
    {
        currentSubPanelRect = null;
        currentSubPanelIndex = 0;

        if (currentPanelRect == null)
        {
            UpdateSubPanelCursor();
            RefreshSlotSelection(false);
            return;
        }

        if (orderedSubPanels.TryGetValue(currentPanelRect, out var subPanels) && subPanels != null && subPanels.Count > 0)
        {
            int targetIndex = 0;
            if (!string.IsNullOrEmpty(defaultSubPanelName))
            {
                for (int i = 0; i < subPanels.Count; i++)
                {
                    if (string.Equals(subPanels[i]?.gameObject.name, defaultSubPanelName, StringComparison.OrdinalIgnoreCase))
                    {
                        targetIndex = i;
                        break;
                    }
                }
            }

            currentSubPanelIndex = WrapIndex(targetIndex, subPanels.Count);
            currentSubPanelRect = subPanels[currentSubPanelIndex];
        }

        UpdateSubPanelCursor();
        RestoreSlotIndexForOwner(currentSubPanelRect);
        RefreshSlotSelection(true);
    }

    private RectTransform GetCurrentSlotOwner() =>
        currentSubPanelRect != null ? currentSubPanelRect : currentPanelRect;

    private void StoreSlotIndexForOwner()
    {
        var owner = GetCurrentSlotOwner();
        if (owner == null || currentSlotIndex < 0)
            return;

        slotIndexByOwner[owner] = currentSlotIndex;
    }

    private void RestoreSlotIndexForOwner(RectTransform owner)
    {
        if (owner != null && slotIndexByOwner.TryGetValue(owner, out int index))
            currentSlotIndex = index;
        else
            currentSlotIndex = 0;
    }

    /// <summary>
    /// Déplacement cyclique entre les panels disponibles.
    /// </summary>
    private void ChangePanel(int direction)
    {
        if (orderedPanels.Count == 0)
            return;

        StoreSlotIndexForOwner();
        currentPanelIndex = WrapIndex(currentPanelIndex + direction, orderedPanels.Count);
        currentPanelRect = orderedPanels[currentPanelIndex];
        UpdatePanelCursor();
        SelectDefaultSubPanelForCurrentPanel();
    }

    /// <summary>
    /// Déplacement cyclique entre les sous-panels du panel actif.
    /// </summary>
    private void ChangeSubPanel(int direction)
    {
        if (currentPanelRect == null)
            return;

        if (!orderedSubPanels.TryGetValue(currentPanelRect, out var subPanels) || subPanels == null || subPanels.Count == 0)
            return;

        StoreSlotIndexForOwner();
        currentSubPanelIndex = WrapIndex(currentSubPanelIndex + direction, subPanels.Count);
        currentSubPanelRect = subPanels[currentSubPanelIndex];
        UpdateSubPanelCursor();
        RestoreSlotIndexForOwner(currentSubPanelRect);
        RefreshSlotSelection(true);
    }

    /// <summary>
    /// Met à jour la position du curseur de panel.
    /// </summary>
    private void UpdatePanelCursor()
    {
        if (panelCursorRect == null)
            return;

        bool hasTarget = currentPanelRect != null;
        panelCursorRect.gameObject.SetActive(hasTarget);
        if (hasTarget)
            PositionCursor(panelCursorRect, currentPanelRect);
    }

    /// <summary>
    /// Met à jour la position du curseur de sous-panel (ou le masque si aucun sous-panel n'est sélectionné).
    /// </summary>
    private void UpdateSubPanelCursor()
    {
        if (subPanelCursorRect == null)
            return;

        bool hasTarget = currentSubPanelRect != null;
        subPanelCursorRect.gameObject.SetActive(hasTarget);
        if (hasTarget)
            PositionCursor(subPanelCursorRect, currentSubPanelRect);
    }

    /// <summary>
    /// Met à jour la position du curseur de slot pour refléter la sélection courante.
    /// </summary>
    private void UpdateSlotCursor()
    {
        if (slotCursorRect == null)
            return;

        bool hasTarget = currentSlotRect != null;
        slotCursorRect.gameObject.SetActive(hasTarget);
        if (hasTarget)
            PositionCursor(slotCursorRect, currentSlotRect);
    }

    /// <summary>
    /// Applique la sélection d'un slot et repositionne le curseur associé.
    /// </summary>
    /// <param name="preserveIndex">Si vrai, conserve l'index actuel lorsque la liste change (dans la limite des bornes).</param>
    private void RefreshSlotSelection(bool preserveIndex)
    {
        var owner = currentSubPanelRect != null ? currentSubPanelRect : currentPanelRect;
        var slots = GetOrRebuildSlotList(owner);

        if (slots.Count == 0)
        {
            currentSlotRect = null;
            currentSlotIndex = -1;
            UpdateSlotCursor();
            UpdateSelectionDetails();
            return;
        }

        if (!preserveIndex || currentSlotIndex < 0 || currentSlotIndex >= slots.Count)
            currentSlotIndex = 0;

        currentSlotRect = slots[Mathf.Clamp(currentSlotIndex, 0, slots.Count - 1)];
        currentSlotIndex = Mathf.Clamp(currentSlotIndex, 0, slots.Count - 1);
        UpdateSlotCursor();
        StoreSlotIndexForOwner();
        UpdateSelectionDetails();
    }

    /// <summary>
    /// Déplace la sélection de slot vers la gauche/droite ou haut/bas en respectant l'ordre alphabétique.
    /// </summary>
    private void MoveSlotSelection(int direction)
    {
        if (direction == 0)
            return;

        var owner = currentSubPanelRect != null ? currentSubPanelRect : currentPanelRect;
        var slots = GetOrRebuildSlotList(owner);
        if (slots.Count == 0)
        {
            currentSlotRect = null;
            currentSlotIndex = -1;
            UpdateSlotCursor();
            return;
        }

        if (currentSlotRect != null)
        {
            int existingIndex = slots.IndexOf(currentSlotRect);
            if (existingIndex >= 0)
                currentSlotIndex = existingIndex;
        }

        currentSlotIndex = WrapIndex(currentSlotIndex + direction, slots.Count);
        currentSlotRect = slots[currentSlotIndex];
        UpdateSlotCursor();
        StoreSlotIndexForOwner();
        UpdateSelectionDetails();
    }

    /// <summary>
    /// S'assure que le curseur visuel épouse parfaitement le RectTransform cible.
    /// </summary>
    private void PositionCursor(RectTransform cursor, RectTransform target)
    {
        if (cursor == null || target == null)
            return;

        var targetParent = target.parent as RectTransform;
        if (targetParent == null)
            return;

        // On ajoute dynamiquement un LayoutElement afin que le curseur soit ignoré par les LayoutGroup.
        // Sans cela, le déplacement du curseur comme frère du sous-panel modifie la répartition dans les HorizontalLayoutGroup.
        if (!cursor.TryGetComponent(out LayoutElement layoutElement))
            layoutElement = cursor.gameObject.AddComponent<LayoutElement>();

        layoutElement.ignoreLayout = true; // Toujours vrai pour neutraliser l'influence du curseur sur la mise en page.

        cursor.SetParent(targetParent, false);
        cursor.SetSiblingIndex(target.GetSiblingIndex() + 1);
        cursor.anchorMin = target.anchorMin;
        cursor.anchorMax = target.anchorMax;
        cursor.pivot = target.pivot;
        cursor.sizeDelta = target.sizeDelta;
        cursor.localScale = target.localScale;
        cursor.localRotation = target.localRotation;
        cursor.anchoredPosition = target.anchoredPosition;
    }

    /// <summary>
    /// Retrouve un RectTransform donné par son nom dans la hiérarchie du gestionnaire d'inventaire.
    /// </summary>
    private RectTransform FindRectTransformInChildren(string targetName)
    {
        if (string.IsNullOrEmpty(targetName))
            return null;

        var rects = GetComponentsInChildren<RectTransform>(true);
        foreach (var rect in rects)
        {
            if (rect != null && rect.gameObject.name == targetName)
                return rect;
        }

        return null;
    }

    /// <summary>
    /// S'assure que les références indispensables à la navigation sont bien initialisées.
    /// </summary>
    private bool EnsureNavigationReferences()
    {
        panelsRootRect ??= FindRectTransformInChildren("InventoryPanel");
        panelCursorRect ??= FindRectTransformInChildren("InventoryCursor_Panel");
        subPanelCursorRect ??= FindRectTransformInChildren("InventoryCursor_SubPanel");
        slotCursorRect ??= FindRectTransformInChildren("InventoryCursor_Slot");

        if (panelsRootRect == null)
        {
            Debug.LogWarning("[InventoryManager] Impossible de configurer la navigation : 'InventoryPanel' est introuvable dans la hiérarchie.");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Recherche récursivement l'enfant nommé "Content" à partir d'un RectTransform donné.
    /// </summary>
    private RectTransform FindContentTransform(RectTransform root)
    {
        if (root == null)
            return null;

        // 1) Prioriser la configuration d'un ScrollRect : c'est la source de vérité de la zone défilable.
        var scrollRect = root.GetComponentInChildren<ScrollRect>(true);
        if (scrollRect != null && scrollRect.content != null && scrollRect.content.IsChildOf(root))
            return scrollRect.content;

        // 2) Tenter de trouver un enfant direct nommé "Content" (structure courante de l'inventaire).
        for (int i = 0; i < root.childCount; i++)
        {
            if (root.GetChild(i) is RectTransform directChild && directChild.gameObject.name == "Content")
                return directChild;
        }

        // 3) Parcourir les descendants en ignorant le noeud racine pour éviter les faux positifs sur des slots imbriqués.
        var queue = new Queue<Transform>();
        for (int i = 0; i < root.childCount; i++)
            queue.Enqueue(root.GetChild(i));

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current is RectTransform rect && rect.gameObject.name == "Content")
                return rect;

            for (int i = 0; i < current.childCount; i++)
                queue.Enqueue(current.GetChild(i));
        }

        return null;
    }

    /// <summary>
    /// Construit la liste triée des slots associés à un panel ou sous-panel donné.
    /// </summary>
    private List<RectTransform> BuildSlotList(RectTransform owner)
    {
        var result = new List<RectTransform>();
        if (owner == null)
            return result;

        var content = FindContentTransform(owner);
        if (content == null)
            return result;

        for (int i = 0; i < content.childCount; i++)
        {
            if (content.GetChild(i) is RectTransform childRect && childRect.gameObject.activeInHierarchy)
                result.Add(childRect);
        }

        result.RemoveAll(r => r == null);

        result.Sort((a, b) => string.Compare(GetSlotDisplayName(a), GetSlotDisplayName(b), StringComparison.OrdinalIgnoreCase));

        for (int i = 0; i < result.Count; i++)
        {
            // On force l'ordre dans la hiérarchie pour que l'affichage corresponde bien au tri alphabétique.
            result[i].SetSiblingIndex(i);
        }

        return result;
    }

    /// <summary>
    /// Récupère le texte associé à un slot pour effectuer le tri alphabétique.
    /// </summary>
    private string GetSlotDisplayName(RectTransform slot)
    {
        if (slot == null)
            return string.Empty;

        var text = slot.GetComponentInChildren<TMP_Text>(true);
        if (text != null && !string.IsNullOrWhiteSpace(text.text))
            return text.text.Trim();

        return slot.gameObject.name;
    }

    /// <summary>
    /// Retourne (et reconstruit au besoin) la liste des slots pour un panel donné.
    /// </summary>
    private List<RectTransform> GetOrRebuildSlotList(RectTransform owner)
    {
        if (owner == null)
            return EmptySlotList;

        var list = BuildSlotList(owner);
        orderedSlots[owner] = list;
        return list;
    }

    /// <summary>
    /// Applique un modulo positif pour gérer les navigations circulaires.
    /// </summary>
    private static int WrapIndex(int value, int count)
    {
        if (count <= 0)
            return 0;

        int result = value % count;
        if (result < 0)
            result += count;
        return result;
    }

    /// <summary>
    /// Liste vide réutilisable afin d'éviter des allocations inutiles.
    /// </summary>
    private static readonly List<RectTransform> EmptySlotList = new();

    /// <summary>
    /// Prépare les données liées aux Sceaux (nettoyage, tri et constitution des dictionnaires d'accès rapide).
    /// </summary>
    private void InitializeSealCollections()
    {
        unlockedSeals ??= new List<SceauSO>();
        equippedSeals ??= new List<SceauSO>();

        // On purge immédiatement les entrées nulles afin d'éviter des exceptions lors des accès.
        unlockedSeals.RemoveAll(seal => seal == null);
        equippedSeals.RemoveAll(seal => seal == null);

        SanitizeUnlockedSeals();
        ClampEquippedSeals();
        SortSeals();
        RebuildSealLookups();
    }

    /// <summary>
    /// Ajoute un Sceau au catalogue du joueur si ce n'est pas déjà fait.
    /// </summary>
    /// <returns>Vrai si le Sceau a été ajouté, faux s'il était déjà présent ou invalide.</returns>
    public bool UnlockSeal(SceauSO seal)
    {
        if (seal == null)
            return false;

        if (!unlockedSealsLookup.Add(seal))
            return false;

        unlockedSeals.Add(seal);
        SortSeals();
        RebuildSealLookups();
        return true;
    }

    /// <summary>
    /// Retire un Sceau du catalogue (utile pour les tests ou les scripts narratifs).
    /// </summary>
    /// <param name="seal">Sceau à retirer.</param>
    /// <returns>Vrai si le Sceau a été supprimé.</returns>
    public bool RemoveSeal(SceauSO seal)
    {
        if (seal == null)
            return false;

        bool removed = unlockedSealsLookup.Remove(seal);
        if (!removed)
            return false;

        unlockedSeals.Remove(seal);
        UnequipSeal(seal);
        SortSeals();
        RebuildSealLookups();
        return true;
    }

    /// <summary>
    /// Équipe un Sceau si la limite n'est pas atteinte et si le joueur le possède.
    /// </summary>
    /// <param name="seal">Sceau à équiper.</param>
    /// <returns>Vrai si l'opération a réussi.</returns>
    public bool EquipSeal(SceauSO seal)
    {
        if (seal == null)
            return false;

        if (!unlockedSealsLookup.Contains(seal))
        {
            Debug.LogWarning($"[Inventory] Impossible d'équiper le Sceau '{seal.name}' : il n'est pas débloqué.");
            return false;
        }

        if (equippedSealsLookup.Contains(seal))
            return false;

        if (equippedSeals.Count >= MaxEquippedSealCount)
        {
            Debug.LogWarning($"[Inventory] Limite de {MaxEquippedSealCount} Sceaux équipés atteinte.");
            return false;
        }

        equippedSeals.Add(seal);
        equippedSealsLookup.Add(seal);
        SortSeals();
        RebuildSealLookups();
        return true;
    }

    /// <summary>
    /// Déséquipe un Sceau spécifique.
    /// </summary>
    /// <param name="seal">Sceau à retirer de la sélection.</param>
    /// <returns>Vrai si le Sceau était équipé et a bien été retiré.</returns>
    public bool UnequipSeal(SceauSO seal)
    {
        if (seal == null)
            return false;

        bool removed = equippedSeals.Remove(seal);
        if (!removed)
            return false;

        equippedSealsLookup.Remove(seal);
        RebuildSealLookups();
        return true;
    }

    /// <summary>
    /// Retire tous les Sceaux actifs d'un coup (utile pour restaurer la difficulté d'origine).
    /// </summary>
    public void UnequipAllSeals()
    {
        equippedSeals.Clear();
        equippedSealsLookup.Clear();
        RebuildSealLookups();
    }

    /// <summary>
    /// Indique si un Sceau est déjà débloqué.
    /// </summary>
    public bool IsSealUnlocked(SceauSO seal) => seal != null && unlockedSealsLookup.Contains(seal);

    /// <summary>
    /// Indique si un Sceau fait actuellement partie de la sélection active.
    /// </summary>
    public bool IsSealEquipped(SceauSO seal) => seal != null && equippedSealsLookup.Contains(seal);

    /// <summary>
    /// Additionne les modificateurs de score appliqués par l'ensemble des Sceaux actifs.
    /// </summary>
    /// <returns>Somme des pourcentages de modification de score.</returns>
    public float GetEquippedSealsScoreModifier()
    {
        float total = 0f;
        foreach (var seal in equippedSeals)
        {
            if (seal == null)
                continue;
            total += seal.scoreModifierPercent;
        }

        return total;
    }

    /// <summary>
    /// Supprime les doublons potentiels dans la liste sérialisée et reconstruit le cache correspondant.
    /// </summary>
    private void SanitizeUnlockedSeals()
    {
        var unique = new HashSet<SceauSO>();
        var sanitized = new List<SceauSO>(unlockedSeals.Count);

        foreach (var seal in unlockedSeals)
        {
            if (seal == null)
                continue;

            if (!unique.Add(seal))
                continue; // Une même référence ne doit exister qu'une seule fois dans le catalogue.

            sanitized.Add(seal);
        }

        unlockedSeals.Clear();
        unlockedSeals.AddRange(sanitized);

        unlockedSealsLookup.Clear();
        foreach (var seal in unlockedSeals)
            unlockedSealsLookup.Add(seal);
    }

    /// <summary>
    /// Reconstruit les dictionnaires internes afin de garantir la cohérence après un chargement.
    /// </summary>
    private void RebuildSealLookups()
    {
        unlockedSealsLookup.Clear();
        foreach (var seal in unlockedSeals)
        {
            if (seal == null)
                continue;
            unlockedSealsLookup.Add(seal);
        }

        equippedSealsLookup.Clear();
        foreach (var seal in equippedSeals)
        {
            if (seal == null)
                continue;
            if (!unlockedSealsLookup.Contains(seal))
                continue; // On ignore les Sceaux orphelins qui ne sont plus débloqués.
            equippedSealsLookup.Add(seal);
        }
    }

    /// <summary>
    /// S'assure que la sélection respecte la limite maximale de Sceaux équipés et supprime les doublons.
    /// </summary>
    private void ClampEquippedSeals()
    {
        var sanitized = new List<SceauSO>(Mathf.Min(equippedSeals.Count, MaxEquippedSealCount));
        var unique = new HashSet<SceauSO>();

        foreach (var seal in equippedSeals)
        {
            if (seal == null)
                continue;

            if (!unlockedSealsLookup.Contains(seal))
                continue; // Ne jamais conserver un Sceau que le joueur n'a plus.

            if (!unique.Add(seal))
                continue; // Doublon détecté : on le saute.

            sanitized.Add(seal);

            if (sanitized.Count >= MaxEquippedSealCount)
                break; // On ne dépasse jamais la limite annoncée dans le GDD.
        }

        equippedSeals.Clear();
        equippedSeals.AddRange(sanitized);

        // On reconstruit le cache pour refléter la nouvelle réalité.
        equippedSealsLookup.Clear();
        foreach (var seal in equippedSeals)
            equippedSealsLookup.Add(seal);
    }

    /// <summary>
    /// Trie à la fois les Sceaux débloqués et ceux qui sont équipés afin de conserver un affichage stable.
    /// </summary>
    private void SortSeals()
    {
        unlockedSeals.Sort(CompareSealsByName);
        equippedSeals.Sort(CompareSealsByName);
    }

    /// <summary>
    /// Comparateur commun pour ordonner les Sceaux par nom lisible.
    /// </summary>
    private static int CompareSealsByName(SceauSO a, SceauSO b)
    {
        string aName = a != null ? a.sealName : string.Empty;
        string bName = b != null ? b.sealName : string.Empty;
        return string.Compare(aName, bName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Construit la liste des IDs des Sceaux débloqués pour la sauvegarde.
    /// </summary>
    public List<string> BuildUnlockedSealIds()
    {
        var ids = new HashSet<string>();
        foreach (var seal in unlockedSeals)
        {
            string id = ResolveSealId(seal);
            if (!string.IsNullOrEmpty(id))
                ids.Add(id);
        }

        return new List<string>(ids);
    }

    /// <summary>
    /// Construit la liste des IDs des Sceaux équipés pour la sauvegarde.
    /// </summary>
    public List<string> BuildEquippedSealIds()
    {
        var ids = new HashSet<string>();
        foreach (var seal in equippedSeals)
        {
            string id = ResolveSealId(seal);
            if (!string.IsNullOrEmpty(id))
                ids.Add(id);
        }

        return new List<string>(ids);
    }

    /// <summary>
    /// Applique l'état des Sceaux à partir des IDs sauvegardés.
    /// </summary>
    public void ApplySealIds(IEnumerable<string> unlockedIds, IEnumerable<string> equippedIds)
    {
        var unlockedSet = new HashSet<string>(unlockedIds ?? System.Array.Empty<string>(), System.StringComparer.OrdinalIgnoreCase);
        var equippedSet = new HashSet<string>(equippedIds ?? System.Array.Empty<string>(), System.StringComparer.OrdinalIgnoreCase);
        var lookup = BuildSealLookup();

        unlockedSeals.Clear();
        equippedSeals.Clear();

        foreach (var id in unlockedSet)
        {
            if (string.IsNullOrEmpty(id))
                continue;

            if (lookup.TryGetValue(id, out var seal))
                unlockedSeals.Add(seal);
            else
                Debug.LogWarning($"[Inventory] Sceau introuvable pour l'ID '{id}'.");
        }

        foreach (var id in equippedSet)
        {
            if (string.IsNullOrEmpty(id))
                continue;

            if (!lookup.TryGetValue(id, out var seal))
            {
                Debug.LogWarning($"[Inventory] Sceau équipé introuvable pour l'ID '{id}'.");
                continue;
            }

            // On s'assure que tout Sceau équipé est aussi débloqué.
            if (!unlockedSeals.Contains(seal))
                unlockedSeals.Add(seal);

            equippedSeals.Add(seal);
        }

        InitializeSealCollections();
    }

    private Dictionary<string, SceauSO> BuildSealLookup()
    {
        var lookup = new Dictionary<string, SceauSO>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var seal in GetSealRegistry())
        {
            string id = ResolveSealId(seal);
            if (string.IsNullOrEmpty(id))
                continue;

            if (!lookup.ContainsKey(id))
                lookup.Add(id, seal);
        }

        return lookup;
    }

    private IEnumerable<SceauSO> GetSealRegistry()
    {
        var unique = new HashSet<SceauSO>();

        if (registeredSeals != null)
        {
            foreach (var seal in registeredSeals)
            {
                if (seal != null && unique.Add(seal))
                    yield return seal;
            }
        }

        if (unlockedSeals != null)
        {
            foreach (var seal in unlockedSeals)
            {
                if (seal != null && unique.Add(seal))
                    yield return seal;
            }
        }
    }

    private static string ResolveSealId(SceauSO seal)
    {
        if (seal == null)
            return string.Empty;

        // L'asset name sert d'ID stable tant qu'il ne change pas.
        return seal.name;
    }
}
