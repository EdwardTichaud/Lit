using System.Collections.Generic;
using UnityEngine;

// Role: ScriptableObject central de configuration d'un item.
// Usage: reference par l'inventaire, le loot, les readables, le craft, les containers, le combat et les interactions monde.
// Responsibilities: stocker les donnees item, resoudre les regles d'utilisation/pose/drop/casse, fournir le contenu lisible.
// Dependencies: Effect, InteractableItem, SquadCharacterController, ReadableContentRuntime, WorldPickupUtility.
// Precautions: ce fichier a beaucoup de champs publics serialises; ne pas les renommer sans migration d'assets Unity.
/// <summary>
/// Donnees centrales d'un item utilise par les systemes d'inventaire, loot, readable, craft et interaction.
/// </summary>
[CreateAssetMenu(fileName = "Item", menuName = "Scriptable Objects/Item")]
public class Item : ScriptableObject
{
    private const string ItemNamePlaceholder = "-ItemName-";
    private const string DefaultCannotPlaceMessage = "Impossible de placer ici";
    private const string DefaultCannotPlaceWhileEquippedMessage = "La flamme équipée empêche la pose";
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

    /// <summary>
    /// Types de surfaces acceptees quand l'item est place dans le monde.
    /// </summary>
    public enum PlacementSurfaceMode
    {
        /// <summary>Aucune pose supportee.</summary>
        None = 0,
        /// <summary>Pose uniquement sur surface horizontale.</summary>
        HorizontalOnly = 1,
        /// <summary>Pose sur surface horizontale ou murale.</summary>
        HorizontalOrWall = 2
    }

    /// <summary>
    /// Type de document lisible porte par cet item.
    /// </summary>
    public enum ReadableKind
    {
        /// <summary>Item non lisible.</summary>
        None = 0,
        /// <summary>Livre avec pages.</summary>
        Book = 1,
        /// <summary>Parchemin avec texte continu.</summary>
        Parchment = 2,
        /// <summary>Inscription dans le monde, affichee dans la boite de dialogue.</summary>
        Stab = 3
    }

    /// <summary>
    /// Type de reaction speciale declenchee par un item pendant un combat.
    /// </summary>
    public enum CombatReactionKind
    {
        /// <summary>Aucune reaction speciale.</summary>
        None = 0,
        /// <summary>Contre une attaque melee en empalant l'ennemi.</summary>
        MeleeCounterImpale = 1,
        /// <summary>Defense melee avec un objet qui encaisse l'impact.</summary>
        MeleeDefense = 2
    }

    /// <summary>
    /// Resultat donne quand un item est casse.
    /// </summary>
    [System.Serializable]
    public class BreakResult
    {
        /// <summary>Item obtenu apres casse.</summary>
        [Tooltip("Item obtenu apres casse.")]
        public Item item;
        /// <summary>Quantite obtenue.</summary>
        [Tooltip("Quantite obtenue.")]
        public int quantity = 1;
    }

    /// <summary>
    /// Ressource requise pour construire ou ameliorer un building.
    /// </summary>
    [System.Serializable]
    public class BuildingRequirement
    {
        /// <summary>Item requis.</summary>
        [Tooltip("Item requis pour construire/ameliorer.")]
        public Item item;
        /// <summary>Quantite requise.</summary>
        [Tooltip("Quantite requise par niveau.")]
        public int quantity = 1;
    }

    /// <summary>
    /// Configuration specifique d'un niveau de building.
    /// </summary>
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

    /// <summary>
    /// Profil optionnel de reaction combat porte par un item.
    /// </summary>
    [System.Serializable]
    public class CombatReactionProfile
    {
        [Tooltip("Type de reaction speciale de cet item en combat.")]
        public CombatReactionKind reactionKind = CombatReactionKind.None;
        [Tooltip("Animation jouee par le joueur quand la reaction se declenche.")]
        public string playerAnimationName;
        [Tooltip("Animation joueur de secours si l'animation principale est absente.")]
        public string fallbackPlayerAnimationName;
        [Tooltip("Animation jouee par l'ennemi touche par la reaction.")]
        public string enemyAnimationName;
        [Tooltip("Clip ennemi joue directement via Playables. Prioritaire sur la state/trigger par nom.")]
        public AnimationClip enemyAnimationClip;
        [Min(0.05f)]
        [Tooltip("Duree de secours de l'animation joueur.")]
        public float fallbackPlayerAnimationDuration = 1.2f;
        [Min(0.05f)]
        [Tooltip("Duree de secours de l'animation ennemie.")]
        public float fallbackEnemyAnimationDuration = 1.35f;
        [Range(0.01f, 1f)]
        [Tooltip("Multiplicateur local de presentation pendant la reaction.")]
        public float slowTimeScale = 0.2f;
        [Min(0f)]
        [Tooltip("Duree du ralenti local de reaction.")]
        public float slowDurationSeconds = 0.5f;
        [Tooltip("Declenche le shot camera CounterAction pendant cette reaction.")]
        public bool playCounterActionCameraShot = true;
        [Tooltip("Prefab visuel optionnel a utiliser pendant la reaction. Fallback: prefab monde de l'item.")]
        public GameObject combatVisualPrefab;
        [Tooltip("Nom de l'os ou transform de main joueur qui porte le visuel.")]
        public string playerAttachBoneName = "RightHand";
        [Tooltip("Offset local du visuel dans la main joueur.")]
        public Vector3 playerAttachLocalPosition;
        [Tooltip("Rotation locale du visuel dans la main joueur.")]
        public Vector3 playerAttachLocalEulerAngles;
        [Tooltip("Nom de l'os ou transform ennemi ou le visuel reste plante.")]
        public string enemyAttachBoneName = "spine_03";
        [Tooltip("Offset local du visuel sur l'ennemi.")]
        public Vector3 enemyAttachLocalPosition;
        [Tooltip("Rotation locale du visuel sur l'ennemi.")]
        public Vector3 enemyAttachLocalEulerAngles;
        [Tooltip("Cue audio joue au declenchement. Ignore si un clip direct est assigne.")]
        public ActionAudioCue startAudioCue = ActionAudioCue.CombatAttack;
        [Tooltip("Cue audio joue a l'impact. Ignore si un clip direct est assigne.")]
        public ActionAudioCue impactAudioCue = ActionAudioCue.CombatHit;
        [Tooltip("SFX direct joue au declenchement de la reaction.")]
        public AudioClipSO startSfx;
        [Tooltip("SFX direct joue a l'impact de la reaction.")]
        public AudioClipSO impactSfx;
        [Tooltip("Voix optionnelle jouee a l'impact.")]
        public AudioClipSO voiceClip;
        [Tooltip("VFX optionnel instancie a l'impact.")]
        public GameObject impactVfxPrefab;
        [Tooltip("Offset local du VFX d'impact depuis le point d'attache ennemi.")]
        public Vector3 impactVfxLocalOffset;
        [Tooltip("Rotation locale du VFX d'impact depuis le point d'attache ennemi.")]
        public Vector3 impactVfxLocalEulerAngles;
        [Min(0f)]
        [Tooltip("Duree de vie du VFX d'impact. 0 = ne pas detruire automatiquement.")]
        public float impactVfxLifetime = 2f;

        public bool IsEnabled()
        {
            return reactionKind != CombatReactionKind.None;
        }

        public bool IsMeleeCounter()
        {
            return reactionKind == CombatReactionKind.MeleeCounterImpale;
        }

        public bool IsMeleeDefense()
        {
            return reactionKind == CombatReactionKind.MeleeDefense;
        }

        public string ResolvePlayerAnimationName(string fallback)
        {
            return string.IsNullOrWhiteSpace(playerAnimationName) ? fallback : playerAnimationName;
        }

        public string ResolveFallbackPlayerAnimationName(string fallback)
        {
            return string.IsNullOrWhiteSpace(fallbackPlayerAnimationName) ? fallback : fallbackPlayerAnimationName;
        }

        public string ResolveEnemyAnimationName(string fallback)
        {
            return string.IsNullOrWhiteSpace(enemyAnimationName) ? fallback : enemyAnimationName;
        }

        public GameObject ResolveVisualPrefab(Item owner)
        {
            return combatVisualPrefab != null ? combatVisualPrefab : owner != null ? owner.ResolveWorldPrefab() : null;
        }
    }

    /// <summary>
    /// Page fixe d'un livre lisible.
    /// </summary>
    [System.Serializable]
    public class ReadablePage
    {
        /// <summary>Texte affiche sur cette page.</summary>
        [TextArea(8, 20)]
        [Tooltip("Texte affiche sur cette page.")]
        public string text;
    }

    /// <summary>
    /// Phrase candidate pour la generation aleatoire d'un readable.
    /// </summary>
    [System.Serializable]
    public class ReadableSentence
    {
        /// <summary>Phrase candidate utilisee par ReadableContentRuntime.</summary>
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
    [Header("Knowledge Unlocks")]
    [Tooltip("Connaissances debloquees quand ce readable est ouvert.")]
    public List<KnowledgeSO> knowledgeUnlockedOnRead = new List<KnowledgeSO>();
    [Tooltip("Connaissances debloquees des que cet item est recupere depuis le monde ou un conteneur.")]
    public List<KnowledgeSO> knowledgeUnlockedOnPickup = new List<KnowledgeSO>();
    [Header("Temporal District Registry")]
    [Tooltip("Source de donnees optionnelle pour generer ce livre selon l'age temporel actif.")]
    public DistrictRegistry temporalDistrictRegistry;
    [Tooltip("Si actif, l'ouverture du livre reconstruit ses pages selon l'age dominant ou local disponible.")]
    public bool refreshTemporalDistrictRegistryOnRead = true;

    [Header("Usage")]
    [Tooltip("Peut etre utilise via l'ActionBox.")]
    public bool canUse;
    [Header("Combat Defense")]
    [Tooltip("Peut etre choisi pendant une reaction defensive en combat.")]
    public bool canDefendInCombat;
    [Min(0)]
    [Tooltip("PV defensifs de chaque unite de cet item pendant une attaque ennemie.")]
    public int combatDefenseHitPoints;
    [Header("Combat Reaction")]
    [Tooltip("Profil optionnel de contre/reaction speciale utilisable dans les 3 items combat.")]
    public CombatReactionProfile combatReactionProfile = new CombatReactionProfile();
    [Header("Placement")]
    [Tooltip("Peut etre pose dans le monde.")]
    public bool canPlace;
    [Tooltip("Est un container (coffre, sac, etc.).")]
    public bool isContainer;
    [Tooltip("Prefab a instancier lors de la pose.")]
    public GameObject worldPrefab;
    [Tooltip("Item special flamme.")]
    public bool isFlame;
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
    [Tooltip("Message si la flamme equipee empêche la pose.")]
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
        // Unity appelle OnValidate dans l'editeur; on borne la generation readable avant sauvegarde d'asset.
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

    /// <summary>
    /// Indique si la casse de cet item produit au moins un resultat valide.
    /// </summary>
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

    /// <summary>
    /// Indique si au moins une configuration de niveau building est definie.
    /// </summary>
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

    /// <summary>
    /// Indique si les niveaux de building debloquent explicitement des crafts.
    /// </summary>
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

    /// <summary>
    /// Retourne la configuration de niveau la plus pertinente pour un niveau donne.
    /// </summary>
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

        // On prend d'abord le niveau exact, sinon le plus proche niveau inferieur, sinon le plus bas.
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

    /// <summary>
    /// Retourne le nombre de slots de craft disponibles a un niveau de building.
    /// </summary>
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

    /// <summary>
    /// Retourne les crafts debloques pour un niveau de building.
    /// </summary>
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
            // Sans liste de crafts par niveau, les premiers slots de availableCrafts font foi.
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

    /// <summary>
    /// Retourne les effets de building applicables a un niveau donne.
    /// </summary>
    public IReadOnlyList<Effect> GetBuildingEffectsForLevel(int level)
    {
        if (!HasBuildingLevelConfigs())
        {
            return buildingEffects;
        }

        BuildingLevelConfig config = GetBuildingLevelConfig(level);
        return config != null ? config.effects : null;
    }

    /// <summary>
    /// Indique si l'item peut etre utilise depuis l'inventaire.
    /// </summary>
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

    /// <summary>
    /// Indique si cet item peut absorber une attaque ennemie pendant une reaction defensive.
    /// </summary>
    public bool CanDefendInCombat()
    {
        return canDefendInCombat && combatDefenseHitPoints > 0;
    }

    /// <summary>
    /// Indique si cet item declenche un contre special contre les attaques melee.
    /// </summary>
    public bool CanCounterMeleeInCombat()
    {
        return combatReactionProfile != null && combatReactionProfile.IsMeleeCounter();
    }

    /// <summary>
    /// Indique si cet item bloque une attaque melee avec ses PV defensifs.
    /// </summary>
    public bool CanMeleeDefendInCombat()
    {
        return combatReactionProfile != null &&
               combatReactionProfile.IsMeleeDefense() &&
               combatDefenseHitPoints > 0;
    }

    /// <summary>
    /// Indique si cet item peut occuper un des trois slots rapides de combat.
    /// </summary>
    public bool CanUseInCombatReaction()
    {
        return CanDefendInCombat() ||
               CanMeleeDefendInCombat() ||
               (combatReactionProfile != null &&
                combatReactionProfile.IsEnabled() &&
                !combatReactionProfile.IsMeleeDefense());
    }

    /// <summary>
    /// Retourne le profil de reaction combat optionnel.
    /// </summary>
    public CombatReactionProfile GetCombatReactionProfile()
    {
        return combatReactionProfile;
    }

    /// <summary>
    /// Retourne les PV defensifs d'une unite de cet item.
    /// </summary>
    public int GetCombatDefenseHitPoints()
    {
        return CanDefendInCombat() || CanMeleeDefendInCombat()
            ? Mathf.Max(1, combatDefenseHitPoints)
            : 0;
    }

    /// <summary>
    /// Indique si l'item ouvre une interface de lecture.
    /// </summary>
    public bool IsReadable()
    {
        return readableKind != ReadableKind.None;
    }

    /// <summary>
    /// Indique si l'item est un livre lisible.
    /// </summary>
    public bool IsReadableBook()
    {
        return readableKind == ReadableKind.Book;
    }

    /// <summary>
    /// Indique si l'item est un parchemin lisible.
    /// </summary>
    public bool IsReadableParchment()
    {
        return readableKind == ReadableKind.Parchment;
    }

    /// <summary>Indique si l'item est une inscription affichee dans DialoguePanel.</summary>
    public bool IsReadableStab()
    {
        return readableKind == ReadableKind.Stab;
    }

    /// <summary>
    /// Indique si le contenu lisible doit etre genere depuis les phrases candidates.
    /// </summary>
    public bool UsesRandomReadableSentences()
    {
        return IsReadable() && useRandomSentences && GetValidatedGeneratedSentenceCount() > 0;
    }

    /// <summary>
    /// Force la generation du contenu readable si necessaire.
    /// </summary>
    public void EnsureReadableContentGenerated()
    {
        if (UsesRandomReadableSentences())
        {
            ReadableContentRuntime.EnsureGenerated(this);
        }
    }

    /// <summary>
    /// Retourne le nombre de phrases generees.
    /// </summary>
    public int GetGeneratedSentenceCount()
    {
        return ReadableContentRuntime.GetGeneratedSentenceCount(this);
    }

    /// <summary>
    /// Retourne une phrase generee par index.
    /// </summary>
    public string GetGeneratedSentence(int index)
    {
        return ReadableContentRuntime.GetGeneratedSentence(this, index);
    }

    /// <summary>
    /// Retourne la cle stable utilisee par ReadableContentRuntime pour ce contenu.
    /// </summary>
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

    /// <summary>
    /// Retourne le nombre de pages affichables pour un livre.
    /// </summary>
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
        // Les pages vides finales ne doivent pas creer des pages blanches dans l'UI.
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

    /// <summary>
    /// Retourne le texte d'une page de livre.
    /// </summary>
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

    /// <summary>
    /// Retourne le texte complet d'un parchemin.
    /// </summary>
    public string GetParchmentText()
    {
        if (UsesRandomReadableSentences())
        {
            return ReadableContentRuntime.GetParchmentText(this);
        }

        return parchmentText ?? string.Empty;
    }

    /// <summary>
    /// Collecte les phrases candidates non vides pour la generation readable.
    /// </summary>
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

    /// <summary>
    /// Retourne un nombre de phrases generees valide pour les candidates actuelles.
    /// </summary>
    internal int GetValidatedGeneratedSentenceCount()
    {
        return GetValidatedGeneratedSentenceCount(GetAvailableReadableSentenceCount());
    }

    /// <summary>
    /// Retourne un nombre de phrases generees valide pour un nombre de candidates donne.
    /// </summary>
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

    /// <summary>
    /// Tente d'utiliser l'item sans recuperer le message d'echec.
    /// </summary>
    public bool TryUse(SquadCharacterController controller)
    {
        return TryUse(controller, out _);
    }

    /// <summary>
    /// Tente d'utiliser l'item et retourne la raison en cas d'echec.
    /// </summary>
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
            // Chaque effet decide s'il a reellement fait quelque chose; l'item arbitre ensuite le succes global.
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

    /// <summary>
    /// Indique si l'item peut etre casse et retourne un message si non.
    /// </summary>
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

    /// <summary>
    /// Tente de casser l'item via le controleur de personnage.
    /// </summary>
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

    /// <summary>Retourne le message de succes d'utilisation.</summary>
    public string GetUseSuccessMessage()
    {
        return ResolveMessage(useSuccessMessage, DefaultUseSuccessMessage);
    }

    /// <summary>Retourne le message de succes de pose.</summary>
    public string GetPlaceSuccessMessage()
    {
        return ResolveMessage(placeSuccessMessage, DefaultPlaceSuccessMessage);
    }

    /// <summary>Retourne le message de succes de drop.</summary>
    public string GetDropSuccessMessage()
    {
        return ResolveMessage(dropSuccessMessage, DefaultDropSuccessMessage);
    }

    /// <summary>Retourne le message de succes de depot en container.</summary>
    public string GetDepositSuccessMessage()
    {
        return ResolveMessage(depositSuccessMessage, DefaultDepositSuccessMessage);
    }

    /// <summary>Retourne le message de succes de prise depuis un container.</summary>
    public string GetTakeSuccessMessage()
    {
        return ResolveMessage(takeSuccessMessage, DefaultTakeSuccessMessage);
    }

    /// <summary>Retourne le message de succes de casse.</summary>
    public string GetBreakSuccessMessage()
    {
        return ResolveMessage(breakSuccessMessage, DefaultBreakSuccessMessage);
    }

    /// <summary>
    /// Indique si l'item accorde au moins une capacite d'interaction monde.
    /// </summary>
    public bool HasInteractionCapabilities()
    {
        return interactionCapabilities != InteractionCapability.None;
    }

    /// <summary>
    /// Indique si l'item accorde une capacite d'interaction precise.
    /// </summary>
    public bool GrantsInteractionCapability(InteractionCapability capability)
    {
        if (capability == InteractionCapability.None)
        {
            return true;
        }

        return (interactionCapabilities & capability) == capability;
    }

    /// <summary>
    /// Indique si cet item est une cle compatible avec une serrure donnee.
    /// </summary>
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

    /// <summary>
    /// Indique si utiliser l'item doit l'equiper ou le desequiper.
    /// </summary>
    public bool CanToggleEquipOnUse()
    {
        return toggleEquipOnUse && HasInteractionCapabilities();
    }

    /// <summary>
    /// Indique si l'item peut etre depose dans un container.
    /// </summary>
    public bool CanDepositToContainer(InteractableItem container)
    {
        return CanDepositToContainer(container, out _);
    }

    /// <summary>
    /// Indique si l'item peut etre depose dans un container et retourne la raison si non.
    /// </summary>
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

    /// <summary>
    /// Indique si l'item peut etre pris depuis un container.
    /// </summary>
    public bool CanTakeFromContainer(InteractableItem container)
    {
        return CanTakeFromContainer(container, out _);
    }

    /// <summary>
    /// Indique si l'item peut etre pris depuis un container et retourne la raison si non.
    /// </summary>
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

    /// <summary>
    /// Indique si l'item peut etre place depuis l'inventaire.
    /// </summary>
    public bool CanPlaceFromInventory(SquadCharacterController controller)
    {
        return CanPlaceFromInventory(controller, out _);
    }

    /// <summary>
    /// Indique si l'item peut etre place depuis l'inventaire et retourne la raison si non.
    /// </summary>
    public bool CanPlaceFromInventory(SquadCharacterController controller, out string reason)
    {
        if (!allowPlaceFromInventory)
        {
            reason = ResolveMessage(cannotPlaceMessage, DefaultCannotPlaceMessage);
            return false;
        }

        if (isFlame && controller != null && controller.IsFlameEquipped)
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

    /// <summary>
    /// Retourne le type de surface requis par la pose de cet item.
    /// </summary>
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

    /// <summary>
    /// Indique si l'item peut etre pose sur un mur.
    /// </summary>
    public bool SupportsWallPlacement()
    {
        return GetPlacementSurfaceMode() == PlacementSurfaceMode.HorizontalOrWall;
    }

    /// <summary>
    /// Indique si le placement doit verifier un support physique.
    /// </summary>
    public bool RequiresPlacementSurfaceSupport()
    {
        return GetPlacementSurfaceMode() != PlacementSurfaceMode.None;
    }

    /// <summary>
    /// Retourne le rayon de placement specifique ou le fallback fourni.
    /// </summary>
    public float GetPlacementRadius(float fallbackRadius)
    {
        if (placementRadiusOverride > 0f)
        {
            return Mathf.Max(0f, placementRadiusOverride);
        }

        return Mathf.Max(0f, fallbackRadius);
    }

    /// <summary>
    /// Indique si l'item peut etre drop instantanement depuis l'inventaire.
    /// </summary>
    public bool CanInstantDropFromInventory(SquadCharacterController controller, bool allowDropWithoutPrefab)
    {
        return CanInstantDropFromInventory(controller, allowDropWithoutPrefab, out _);
    }

    /// <summary>
    /// Indique si l'item peut etre drop instantanement et retourne la raison si non.
    /// </summary>
    public bool CanInstantDropFromInventory(SquadCharacterController controller, bool allowDropWithoutPrefab, out string reason)
    {
        if (!allowDropFromInventory)
        {
            reason = ResolveMessage(cannotDropMessage, DefaultCannotDropMessage);
            return false;
        }

        if (isFlame)
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

    /// <summary>
    /// Indique si le drop doit contourner le mode placement.
    /// </summary>
    public bool ShouldInstantDropInsteadOfPlacement(SquadCharacterController controller, bool allowDropWithoutPrefab)
    {
        if (!CanInstantDropFromInventory(controller, allowDropWithoutPrefab))
        {
            return false;
        }

        return !CanPlaceFromInventory(controller);
    }

    /// <summary>
    /// Retourne le prefab monde a instancier pour cet item.
    /// </summary>
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

    /// <summary>
    /// Cree une instance monde de cet item, avec cube fallback si aucun prefab n'existe.
    /// </summary>
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

    /// <summary>
    /// Cree ou configure un InteractableItem comme loot droppe.
    /// </summary>
    public InteractableItem CreateDroppedLootContainer(GameObject instance, int quantity, bool destroyWhenEmpty, bool collectable = true)
    {
        return WorldPickupUtility.CreateOrConfigureDroppedPickup(instance, this, quantity, destroyWhenEmpty, collectable);
    }

    /// <summary>
    /// Configure un container existant pour representer cet item droppe.
    /// </summary>
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
