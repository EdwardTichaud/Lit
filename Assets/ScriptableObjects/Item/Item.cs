using System.Collections.Generic;
using UnityEngine;

// Donnees d'un item utilisees par l'inventaire et le loot.
[CreateAssetMenu(fileName = "Item", menuName = "Scriptable Objects/Item")]
public class Item : ScriptableObject
{
    private const string ItemNamePlaceholder = "-ItemName-";
    private const string DefaultCannotPlaceMessage = "Impossible de placer ici";
    private const string DefaultCannotPlaceWhileEquippedMessage = "La torche équipée empêche la pose";
    private const string DefaultCannotDropMessage = "Cet objet ne peut pas être jeté";
    private const string DefaultCannotUseMessage = "Cet objet ne peut pas être utilisé";
    private const string DefaultUseFailedMessage = "Echec d'utilisation";
    private const string DefaultCannotPickupMessage = "Cet objet ne peut pas être récupéré";
    private const string DefaultCannotDepositMessage = "Cet objet ne pas pas être déposé";
    private const string DefaultCannotTakeMessage = "Cet item ne peut pas être pris";
    private const string DefaultCannotBreakMessage = "Cet objet est incassable";
    private const string DefaultBreakFailedMessage = "Echec de casse";
    private const string DefaultUseSuccessMessage = ItemNamePlaceholder + " utilisé";
    private const string DefaultPlaceSuccessMessage = ItemNamePlaceholder + " posé";
    private const string DefaultDropSuccessMessage = ItemNamePlaceholder + " jeté";
    private const string DefaultDepositSuccessMessage = ItemNamePlaceholder + " déposé";
    private const string DefaultTakeSuccessMessage = ItemNamePlaceholder + " pris";
    private const string DefaultBreakSuccessMessage = ItemNamePlaceholder + " cassé";

    public enum PlacementSurfaceMode
    {
        None = 0,
        HorizontalOnly = 1,
        HorizontalOrWall = 2
    }

    public enum ReadableKind
    {
        None = 0,
        Book = 1,
        Parchment = 2
    }

    [System.Serializable]
    public class BreakResult
    {
        [Tooltip("Item obtenu apres casse.")]
        public Item item;
        [Tooltip("Quantite obtenue.")]
        public int quantity = 1;
    }

    [System.Serializable]
    public class BuildingRequirement
    {
        [Tooltip("Item requis pour construire/ameliorer.")]
        public Item item;
        [Tooltip("Quantite requise par niveau.")]
        public int quantity = 1;
    }

    [System.Serializable]
    public class BuildingLevelConfig
    {
        [Min(1)]
        [Tooltip("Niveau concerne par cette configuration.")]
        public int level = 1;
        [Min(0)]
        [Tooltip("Nombre de slots de craft disponibles a ce niveau (0 = utilise tous les crafts).")]
        public int craftSlots = 0;
        [TextArea]
        [Tooltip("Description d'effet specifique au niveau (optionnel).")]
        public string effectDescription;
        [TextArea]
        [Tooltip("Description du bonus au niveau (optionnel).")]
        public string bonusDescription;
        [Tooltip("Effets declenches a l'obtention de ce niveau.")]
        public List<Effect> effects = new List<Effect>();
        [Tooltip("Crafts debloques a ce niveau (optionnel).")]
        public List<Item> unlockedCrafts = new List<Item>();
    }

    [System.Serializable]
    public class ReadablePage
    {
        [TextArea(8, 20)]
        [Tooltip("Texte affiche sur cette page.")]
        public string text;
    }

    [System.Serializable]
    public class ReadableSentence
    {
        [TextArea(2, 8)]
        [Tooltip("Phrase candidate pour la generation aleatoire.")]
        public string text;
    }

    [Header("Identity")]
    [Tooltip("Identifiant unique (optionnel).")]
    public string itemId;
    [Tooltip("Nom affiche dans l'UI.")]
    public string itemName;

    [Header("UI")]
    [Tooltip("Sprite utilise pour les apercus/illustrations.")]
    public Sprite itemSprite;
    [TextArea]
    [Tooltip("Description affichee dans l'UI.")]
    public string description;

    [Header("Readable")]
    [Tooltip("Type de document lisible ouvert depuis l'inventaire.")]
    public ReadableKind readableKind = ReadableKind.None;
    [TextArea(8, 20)]
    [Tooltip("Texte unique d'un parchemin.")]
    public string parchmentText;
    [Tooltip("Pages d'un livre ouvert dans BookPanel.")]
    public List<ReadablePage> bookPages = new List<ReadablePage>();
    [Tooltip("Genere le contenu lisible a partir d'une selection aleatoire de phrases candidates.")]
    public bool useRandomSentences;
    [Tooltip("Phrases candidates disponibles pour generer le contenu lisible.")]
    public List<ReadableSentence> candidateSentences = new List<ReadableSentence>();
    [Tooltip("Nombre de phrases a generer. La valeur est automatiquement bornee au nombre de phrases disponibles.")]
    public int generatedSentenceCount = 1;
    [Tooltip("Cle optionnelle utilisee pour identifier ce contenu lisible genere de maniere unique.")]
    public string readableContentId;
    [Tooltip("Offset optionnel ajoute a la seed de generation pour cette source readable.")]
    public int readableGenerationSeedOffset;
    [Header("Readable Narrative Metadata")]
    [Tooltip("Metadonnees optionnelles pour classer les readables dans l'enquete temporelle.")]
    public TemporalReadableMetadata readableMetadata = new TemporalReadableMetadata();

    [Header("Usage")]
    [Tooltip("Peut etre utilise via l'ActionBox.")]
    public bool canUse;
    [Tooltip("Peut etre pose dans le monde.")]
    public bool canPlace;
    [Tooltip("Est un container (coffre, sac, etc.).")]
    public bool isContainer;
    [Tooltip("Prefab a instancier lors de la pose.")]
    public GameObject worldPrefab;
    [Tooltip("Item special torche.")]
    public bool isTorch;
    [Header("Special Placement")]
    [Tooltip("Item special de type balise.")]
    public bool isBeacon;
    [Tooltip("Autorise l'accroche sur les murs pendant la pose.")]
    public bool allowWallPlacement;
    [Min(0f)]
    [Tooltip("Rayon de pose specifique (0 = utilise le rayon global).")]
    public float placementRadiusOverride = 0f;
    [Header("Preservation")]
    [Tooltip("Argile humide qui peut secher si elle n'est pas preservee.")]
    public bool isWetClay;
    [Tooltip("Item remplaçant l'argile humide lorsqu'elle seche.")]
    public Item driedReplacementItem;
    [Tooltip("Conserve une quantite d'argile humide dans l'inventaire.")]
    public bool preservesWetClay;
    [Min(0)]
    [Tooltip("Quantite d'argile humide preservee par cet item.")]
    public int preservedWetClayCapacity = 1;
    [Header("Lock / Key")]
    [Tooltip("Si true, cet item peut servir de cle.")]
    public bool isKey;
    [Tooltip("Identifiant de serrure compatible avec cette cle.")]
    public string keyId;
    [Header("World Interactions")]
    [Tooltip("Capacites d'interaction accordees lorsque l'objet est equipe.")]
    public InteractionCapability interactionCapabilities = InteractionCapability.None;
    [Tooltip("Utiliser l'objet l'equipe/le desequipe pour les interactions de monde.")]
    public bool toggleEquipOnUse = false;
    [Header("Inventory Rules")]
    [Tooltip("Autorise le drop depuis l'inventaire.")]
    public bool allowDropFromInventory = true;
    [Tooltip("Autorise le placement depuis l'inventaire.")]
    public bool allowPlaceFromInventory = true;
    [Tooltip("Autorise le drop meme sans prefab de monde.")]
    public bool allowInstantDropWithoutPrefab = true;
    [Tooltip("Effets additionnels appliques lors de l'utilisation.")]
    public List<Effect> useEffects = new List<Effect>();
    [Tooltip("Si true, tous les effets doivent reussir pour valider l'utilisation.")]
    public bool requireAllUseEffects = false;
    [Tooltip("Consomme l'item apres utilisation reussie.")]
    public bool consumeOnUse = false;
    [Min(1)]
    [Tooltip("Quantite consommee lors de l'utilisation.")]
    public int consumeQuantity = 1;
    [Tooltip("Effet passif de l'item (optionnel).")]
    public Effect itemPassiveEffect;

    [Header("Container Rules")]
    [Tooltip("Autorise le depot dans les containers.")]
    public bool allowDepositToContainers = true;
    [Tooltip("Autorise la prise depuis les containers.")]
    public bool allowTakeFromContainers = true;

    [Header("Feedback")]
    [Tooltip("Message si l'item ne peut pas etre pose.")]
    public string cannotPlaceMessage = DefaultCannotPlaceMessage;
    [Tooltip("Message si la torche equipee empêche la pose.")]
    public string cannotPlaceWhileEquippedMessage = DefaultCannotPlaceWhileEquippedMessage;
    [Tooltip("Message si l'item ne peut pas etre jete.")]
    public string cannotDropMessage = DefaultCannotDropMessage;
    [Tooltip("Message si l'item ne peut pas etre utilise.")]
    public string cannotUseMessage = DefaultCannotUseMessage;
    [Tooltip("Message si l'utilisation echoue.")]
    public string useFailedMessage = DefaultUseFailedMessage;
    [Tooltip("Message si l'item ne peut pas etre ramasse.")]
    public string cannotPickupMessage = DefaultCannotPickupMessage;
    [Tooltip("Message si l'item ne peut pas etre depose.")]
    public string cannotDepositMessage = DefaultCannotDepositMessage;
    [Tooltip("Message si l'item ne peut pas etre pris.")]
    public string cannotTakeMessage = DefaultCannotTakeMessage;
    [Tooltip("Message si l'item ne peut pas etre casse.")]
    public string cannotBreakMessage = DefaultCannotBreakMessage;
    [Tooltip("Message si la casse echoue.")]
    public string breakFailedMessage = DefaultBreakFailedMessage;
    [Tooltip("Message si l'utilisation reussit.")]
    public string useSuccessMessage = DefaultUseSuccessMessage;
    [Tooltip("Message si la pose reussit.")]
    public string placeSuccessMessage = DefaultPlaceSuccessMessage;
    [Tooltip("Message si le drop reussit.")]
    public string dropSuccessMessage = DefaultDropSuccessMessage;
    [Tooltip("Message si le depot reussit.")]
    public string depositSuccessMessage = DefaultDepositSuccessMessage;
    [Tooltip("Message si la prise reussit.")]
    public string takeSuccessMessage = DefaultTakeSuccessMessage;
    [Tooltip("Message si la casse reussit.")]
    public string breakSuccessMessage = DefaultBreakSuccessMessage;

    [Header("Building")]
    [Tooltip("Si true, l'item est traite comme un building.")]
    public bool isBuilding = false;
    [Tooltip("Si true, le building ouvre un panel de craft.")]
    public bool isCraftingBuilding = false;
    [Tooltip("Liste des crafts possibles")]
    public List<Item> availableCrafts;
    [Tooltip("Prefab instancie lors de la construction (fallback: worldPrefab).")]
    public GameObject buildingPrefab;
    [Min(1)]
    [Tooltip("Niveau maximal du building.")]
    public int buildingMaxLevel = 10;
    [Min(0)]
    [Tooltip("Niveau courant (0 = non construit).")]
    public int buildingCurrentLevel = 0;
    [Tooltip("Ressources necessaires pour construire ou ameliorer (par niveau).")]
    public List<BuildingRequirement> buildingRequirements = new List<BuildingRequirement>();
    [Tooltip("Effets appliques a chaque niveau gagne.")]
    public List<Effect> buildingEffects = new List<Effect>();
    [Tooltip("Configuration par niveau (prioritaire sur les effets globaux).")]
    public List<BuildingLevelConfig> buildingLevelConfigs = new List<BuildingLevelConfig>();
    [Tooltip("Si true, la construction est un coffre maison.")]
    public bool isHomeChest = false;

    [Header("Break")]
    [Tooltip("Peut etre casse via l'ActionBox.")]
    public bool canBreak;
    [Tooltip("Resultats de la casse.")]
    public List<BreakResult> breakResults = new List<BreakResult>();

    private void OnValidate()
    {
        if (!useRandomSentences)
        {
            if (generatedSentenceCount < 0)
            {
                generatedSentenceCount = 0;
            }

            return;
        }

        generatedSentenceCount = GetValidatedGeneratedSentenceCount(GetAvailableReadableSentenceCount());
    }

    public bool HasBreakResults()
    {
        if (!canBreak || breakResults == null || breakResults.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < breakResults.Count; i++)
        {
            BreakResult result = breakResults[i];
            if (result != null && result.item != null && result.quantity > 0)
            {
                return true;
            }
        }

        return false;
    }

    public bool HasBuildingLevelConfigs()
    {
        if (buildingLevelConfigs == null || buildingLevelConfigs.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < buildingLevelConfigs.Count; i++)
        {
            if (buildingLevelConfigs[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    public bool HasCraftUnlocks()
    {
        if (!HasBuildingLevelConfigs())
        {
            return false;
        }

        for (int i = 0; i < buildingLevelConfigs.Count; i++)
        {
            BuildingLevelConfig config = buildingLevelConfigs[i];
            if (config != null && config.unlockedCrafts != null && config.unlockedCrafts.Count > 0)
            {
                return true;
            }
        }

        return false;
    }

    public BuildingLevelConfig GetBuildingLevelConfig(int level)
    {
        if (!HasBuildingLevelConfigs())
        {
            return null;
        }

        int targetLevel = Mathf.Max(1, level);
        BuildingLevelConfig bestBelow = null;
        int bestBelowLevel = int.MinValue;
        BuildingLevelConfig lowest = null;
        int lowestLevel = int.MaxValue;

        for (int i = 0; i < buildingLevelConfigs.Count; i++)
        {
            BuildingLevelConfig config = buildingLevelConfigs[i];
            if (config == null)
            {
                continue;
            }

            int configLevel = Mathf.Max(1, config.level);
            if (configLevel == targetLevel)
            {
                return config;
            }

            if (configLevel < targetLevel && configLevel > bestBelowLevel)
            {
                bestBelow = config;
                bestBelowLevel = configLevel;
            }

            if (configLevel < lowestLevel)
            {
                lowest = config;
                lowestLevel = configLevel;
            }
        }

        return bestBelow != null ? bestBelow : lowest;
    }

    public int GetCraftSlotsForLevel(int level)
    {
        int total = availableCrafts != null ? availableCrafts.Count : 0;
        BuildingLevelConfig config = GetBuildingLevelConfig(level);
        if (config != null && config.craftSlots > 0)
        {
            return Mathf.Clamp(config.craftSlots, 0, total);
        }

        return total;
    }

    public List<Item> GetUnlockedCraftsForLevel(int level)
    {
        List<Item> result = new List<Item>();
        if (availableCrafts == null || availableCrafts.Count == 0)
        {
            return result;
        }

        if (!HasBuildingLevelConfigs())
        {
            result.AddRange(availableCrafts);
            return result;
        }

        if (!HasCraftUnlocks())
        {
            int count = GetCraftSlotsForLevel(level);
            int limit = Mathf.Clamp(count, 0, availableCrafts.Count);
            for (int i = 0; i < limit; i++)
            {
                Item craft = availableCrafts[i];
                if (craft != null)
                {
                    result.Add(craft);
                }
            }

            return result;
        }

        int targetLevel = Mathf.Max(1, level);
        HashSet<Item> unlockedSet = new HashSet<Item>();
        for (int i = 0; i < buildingLevelConfigs.Count; i++)
        {
            BuildingLevelConfig config = buildingLevelConfigs[i];
            if (config == null)
            {
                continue;
            }

            int configLevel = Mathf.Max(1, config.level);
            if (configLevel > targetLevel)
            {
                continue;
            }

            if (config.unlockedCrafts == null || config.unlockedCrafts.Count == 0)
            {
                continue;
            }

            for (int c = 0; c < config.unlockedCrafts.Count; c++)
            {
                Item craft = config.unlockedCrafts[c];
                if (craft != null)
                {
                    unlockedSet.Add(craft);
                }
            }
        }

        if (unlockedSet.Count == 0)
        {
            return result;
        }

        for (int i = 0; i < availableCrafts.Count; i++)
        {
            Item craft = availableCrafts[i];
            if (craft != null && unlockedSet.Contains(craft))
            {
                result.Add(craft);
            }
        }

        return result;
    }

    public IReadOnlyList<Effect> GetBuildingEffectsForLevel(int level)
    {
        if (!HasBuildingLevelConfigs())
        {
            return buildingEffects;
        }

        BuildingLevelConfig config = GetBuildingLevelConfig(level);
        return config != null ? config.effects : null;
    }

    public bool CanUse()
    {
        if (CanToggleEquipOnUse())
        {
            return true;
        }

        if (!canUse)
        {
            return false;
        }

        return useEffects != null && useEffects.Count > 0;
    }

    public bool IsReadable()
    {
        return readableKind != ReadableKind.None;
    }

    public bool IsReadableBook()
    {
        return readableKind == ReadableKind.Book;
    }

    public bool IsReadableParchment()
    {
        return readableKind == ReadableKind.Parchment;
    }

    public bool UsesRandomReadableSentences()
    {
        return IsReadable() && useRandomSentences && GetValidatedGeneratedSentenceCount() > 0;
    }

    public void EnsureReadableContentGenerated()
    {
        if (UsesRandomReadableSentences())
        {
            ReadableContentRuntime.EnsureGenerated(this);
        }
    }

    public int GetGeneratedSentenceCount()
    {
        return ReadableContentRuntime.GetGeneratedSentenceCount(this);
    }

    public string GetGeneratedSentence(int index)
    {
        return ReadableContentRuntime.GetGeneratedSentence(this, index);
    }

    public string GetReadableContentKey()
    {
        if (!string.IsNullOrWhiteSpace(readableContentId))
        {
            return readableContentId.Trim();
        }

        if (!string.IsNullOrWhiteSpace(itemId))
        {
            return $"{itemId.Trim()}|{name}";
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        return itemName ?? string.Empty;
    }

    public int GetBookPageCount()
    {
        if (UsesRandomReadableSentences())
        {
            return ReadableContentRuntime.GetBookPageCount(this);
        }

        if (!IsReadableBook() || bookPages == null || bookPages.Count == 0)
        {
            return 0;
        }

        int count = bookPages.Count;
        while (count > 0)
        {
            ReadablePage page = bookPages[count - 1];
            if (page != null && !string.IsNullOrWhiteSpace(page.text))
            {
                break;
            }

            count--;
        }

        return count;
    }

    public string GetBookPageText(int pageIndex)
    {
        if (UsesRandomReadableSentences())
        {
            return ReadableContentRuntime.GetBookPageText(this, pageIndex);
        }

        if (pageIndex < 0 || bookPages == null || pageIndex >= bookPages.Count)
        {
            return string.Empty;
        }

        ReadablePage page = bookPages[pageIndex];
        return page != null ? page.text ?? string.Empty : string.Empty;
    }

    public string GetParchmentText()
    {
        if (UsesRandomReadableSentences())
        {
            return ReadableContentRuntime.GetParchmentText(this);
        }

        return parchmentText ?? string.Empty;
    }

    internal List<string> CollectReadableSentenceCandidates()
    {
        List<string> result = new List<string>();
        if (candidateSentences == null || candidateSentences.Count == 0)
        {
            return result;
        }

        for (int i = 0; i < candidateSentences.Count; i++)
        {
            ReadableSentence candidate = candidateSentences[i];
            if (candidate == null || string.IsNullOrWhiteSpace(candidate.text))
            {
                continue;
            }

            result.Add(candidate.text.Trim());
        }

        return result;
    }

    internal int GetValidatedGeneratedSentenceCount()
    {
        return GetValidatedGeneratedSentenceCount(GetAvailableReadableSentenceCount());
    }

    internal int GetValidatedGeneratedSentenceCount(int availableSentenceCount)
    {
        if (availableSentenceCount <= 0)
        {
            return 0;
        }

        return Mathf.Clamp(Mathf.Max(1, generatedSentenceCount), 1, availableSentenceCount);
    }

    private int GetAvailableReadableSentenceCount()
    {
        if (candidateSentences == null || candidateSentences.Count == 0)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < candidateSentences.Count; i++)
        {
            ReadableSentence candidate = candidateSentences[i];
            if (candidate != null && !string.IsNullOrWhiteSpace(candidate.text))
            {
                count++;
            }
        }

        return count;
    }

    public bool TryUse(SquadCharacterController controller)
    {
        return TryUse(controller, out _);
    }

    public bool TryUse(SquadCharacterController controller, out string reason)
    {
        if (controller == null)
        {
            reason = ResolveMessage(cannotUseMessage, DefaultCannotUseMessage);
            return false;
        }

        if (CanToggleEquipOnUse())
        {
            return controller.TryToggleEquippedInteractionItem(this, out reason);
        }

        if (!CanUse())
        {
            reason = ResolveMessage(cannotUseMessage, DefaultCannotUseMessage);
            return false;
        }

        bool appliedAny = false;
        bool allApplied = true;
        bool hasAnyEffect = false;

        if (useEffects != null && useEffects.Count > 0)
        {
            for (int i = 0; i < useEffects.Count; i++)
            {
                Effect effect = useEffects[i];
                if (effect == null)
                {
                    continue;
                }

                hasAnyEffect = true;
                bool applied = effect.Apply(controller, this);
                appliedAny |= applied;
                allApplied &= applied;
            }

            bool success = requireAllUseEffects ? (hasAnyEffect && allApplied) : appliedAny;
            if (success)
            {
                ConsumeAfterUse(controller);
                reason = string.Empty;
                return true;
            }
        }

        reason = ResolveMessage(useFailedMessage, DefaultUseFailedMessage);
        return false;
    }

    public bool CanBreak(out string reason)
    {
        if (!canBreak || !HasBreakResults())
        {
            reason = ResolveMessage(cannotBreakMessage, DefaultCannotBreakMessage);
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public bool TryBreak(SquadCharacterController controller, out string reason)
    {
        if (controller == null)
        {
            reason = ResolveMessage(breakFailedMessage, DefaultBreakFailedMessage);
            return false;
        }

        if (!CanBreak(out reason))
        {
            return false;
        }

        if (controller.TryBreakItem(this))
        {
            reason = string.Empty;
            return true;
        }

        reason = ResolveMessage(breakFailedMessage, DefaultBreakFailedMessage);
        return false;
    }

    public string GetUseSuccessMessage()
    {
        return ResolveMessage(useSuccessMessage, DefaultUseSuccessMessage);
    }

    public string GetPlaceSuccessMessage()
    {
        return ResolveMessage(placeSuccessMessage, DefaultPlaceSuccessMessage);
    }

    public string GetDropSuccessMessage()
    {
        return ResolveMessage(dropSuccessMessage, DefaultDropSuccessMessage);
    }

    public string GetDepositSuccessMessage()
    {
        return ResolveMessage(depositSuccessMessage, DefaultDepositSuccessMessage);
    }

    public string GetTakeSuccessMessage()
    {
        return ResolveMessage(takeSuccessMessage, DefaultTakeSuccessMessage);
    }

    public string GetBreakSuccessMessage()
    {
        return ResolveMessage(breakSuccessMessage, DefaultBreakSuccessMessage);
    }

    public bool HasInteractionCapabilities()
    {
        return interactionCapabilities != InteractionCapability.None;
    }

    public bool GrantsInteractionCapability(InteractionCapability capability)
    {
        if (capability == InteractionCapability.None)
        {
            return true;
        }

        return (interactionCapabilities & capability) == capability;
    }

    public bool IsMatchingKey(string requiredLockId)
    {
        if (!isKey)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(keyId) || string.IsNullOrWhiteSpace(requiredLockId))
        {
            return false;
        }

        return string.Equals(keyId.Trim(), requiredLockId.Trim(), System.StringComparison.Ordinal);
    }

    public bool CanToggleEquipOnUse()
    {
        return toggleEquipOnUse && HasInteractionCapabilities();
    }

    public bool CanDepositToContainer(InteractableItem container)
    {
        return CanDepositToContainer(container, out _);
    }

    public bool CanDepositToContainer(InteractableItem container, out string reason)
    {
        if (!allowDepositToContainers)
        {
            reason = ResolveMessage(cannotDepositMessage, DefaultCannotDepositMessage);
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public bool CanTakeFromContainer(InteractableItem container)
    {
        return CanTakeFromContainer(container, out _);
    }

    public bool CanTakeFromContainer(InteractableItem container, out string reason)
    {
        if (!allowTakeFromContainers)
        {
            string fallback = DefaultCannotTakeMessage;
            string custom = !string.IsNullOrWhiteSpace(cannotTakeMessage) ? cannotTakeMessage : cannotPickupMessage;
            reason = ResolveMessage(custom, fallback);
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public bool CanPlaceFromInventory(SquadCharacterController controller)
    {
        return CanPlaceFromInventory(controller, out _);
    }

    public bool CanPlaceFromInventory(SquadCharacterController controller, out string reason)
    {
        if (!allowPlaceFromInventory)
        {
            reason = ResolveMessage(cannotPlaceMessage, DefaultCannotPlaceMessage);
            return false;
        }

        if (isTorch && controller != null && controller.IsTorchEquipped)
        {
            reason = ResolveMessage(cannotPlaceWhileEquippedMessage, DefaultCannotPlaceWhileEquippedMessage);
            return false;
        }

        if (ResolveWorldPrefab() == null)
        {
            reason = ResolveMessage(cannotPlaceMessage, DefaultCannotPlaceMessage);
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public PlacementSurfaceMode GetPlacementSurfaceMode()
    {
        if (isBeacon || allowWallPlacement)
        {
            return PlacementSurfaceMode.HorizontalOrWall;
        }

        if (isBuilding || canPlace || worldPrefab != null || buildingPrefab != null)
        {
            return PlacementSurfaceMode.HorizontalOnly;
        }

        return PlacementSurfaceMode.None;
    }

    public bool SupportsWallPlacement()
    {
        return GetPlacementSurfaceMode() == PlacementSurfaceMode.HorizontalOrWall;
    }

    public bool RequiresPlacementSurfaceSupport()
    {
        return GetPlacementSurfaceMode() != PlacementSurfaceMode.None;
    }

    public float GetPlacementRadius(float fallbackRadius)
    {
        if (placementRadiusOverride > 0f)
        {
            return Mathf.Max(0f, placementRadiusOverride);
        }

        return Mathf.Max(0f, fallbackRadius);
    }

    public bool CanInstantDropFromInventory(SquadCharacterController controller, bool allowDropWithoutPrefab)
    {
        return CanInstantDropFromInventory(controller, allowDropWithoutPrefab, out _);
    }

    public bool CanInstantDropFromInventory(SquadCharacterController controller, bool allowDropWithoutPrefab, out string reason)
    {
        if (!allowDropFromInventory)
        {
            reason = ResolveMessage(cannotDropMessage, DefaultCannotDropMessage);
            return false;
        }

        if (isTorch)
        {
            reason = ResolveMessage(cannotDropMessage, DefaultCannotDropMessage);
            return false;
        }

        if (isBuilding)
        {
            reason = ResolveMessage(cannotDropMessage, DefaultCannotDropMessage);
            return false;
        }

        if (ResolveWorldPrefab() != null)
        {
            reason = string.Empty;
            return true;
        }

        if (allowDropWithoutPrefab && allowInstantDropWithoutPrefab)
        {
            reason = string.Empty;
            return true;
        }

        reason = ResolveMessage(cannotDropMessage, DefaultCannotDropMessage);
        return false;
    }

    public bool ShouldInstantDropInsteadOfPlacement(SquadCharacterController controller, bool allowDropWithoutPrefab)
    {
        if (!CanInstantDropFromInventory(controller, allowDropWithoutPrefab))
        {
            return false;
        }

        return !CanPlaceFromInventory(controller);
    }

    public GameObject ResolveWorldPrefab()
    {
        if (isBuilding && buildingPrefab != null)
        {
            return buildingPrefab;
        }

        if (worldPrefab != null)
        {
            return worldPrefab;
        }

        if (buildingPrefab != null)
        {
            return buildingPrefab;
        }

        return null;
    }

    public GameObject CreateWorldInstance(Vector3 position, Quaternion rotation)
    {
        GameObject prefab = ResolveWorldPrefab();
        if (prefab != null)
        {
            return Object.Instantiate(prefab, position, rotation);
        }

        GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fallback.transform.SetPositionAndRotation(position, rotation);
        fallback.transform.localScale = Vector3.one * 0.4f;
        return fallback;
    }

    public InteractableItem CreateDroppedLootContainer(GameObject instance, int quantity, bool destroyWhenEmpty, bool collectable = true)
    {
        return WorldPickupUtility.CreateOrConfigureDroppedPickup(instance, this, quantity, destroyWhenEmpty, collectable);
    }

    public void ConfigureDroppedLootContainer(InteractableItem container, int quantity, bool destroyWhenEmpty, bool collectable = true)
    {
        WorldPickupUtility.ConfigureLootContainer(container, this, quantity, destroyWhenEmpty, collectable);
    }

    private string ResolveMessage(string custom, string fallback)
    {
        string message = !string.IsNullOrWhiteSpace(custom) ? custom : fallback;
        return FormatFeedbackMessage(message);
    }

    private string FormatFeedbackMessage(string message)
    {
        if (string.IsNullOrEmpty(message) || !message.Contains(ItemNamePlaceholder))
        {
            return message;
        }

        string display = !string.IsNullOrWhiteSpace(itemName) ? itemName : name;
        return message.Replace(ItemNamePlaceholder, display ?? string.Empty);
    }

    private void ConsumeAfterUse(SquadCharacterController controller)
    {
        if (!consumeOnUse || controller == null)
        {
            return;
        }

        int quantity = Mathf.Max(1, consumeQuantity);
        controller.TryRemoveItemQuantity(this, quantity);
    }
}
