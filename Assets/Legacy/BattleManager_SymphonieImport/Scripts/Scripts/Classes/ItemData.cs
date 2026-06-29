using UnityEngine;
using UnityEngine.Serialization;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor; // Nécessaire uniquement dans l'éditeur pour charger l'item de référence.
#endif

[CreateAssetMenu(fileName = "New Item", menuName = "Symphonie/Item")]
public class ItemData : ScriptableObject
{
    [Header("Identifiant Unique")]
    public string itemID; // String pour correspondre au système de sauvegarde

    public string itemName;
    [TextArea] public string description;
    public Sprite itemIcon;

    [Header("Inventaire & Accessibilité")]
    [Tooltip("Catégorie affichée dans l'inventaire pour guider les nouveaux joueurs.")]
    public string inventoryCategory = "Objet";
    [TextArea, Tooltip("Résumé court utilisé dans l'inventaire et les aides contextuelles.")]
    public string inventorySummary;
    [Tooltip("Si désactivé, l'objet reste dans l'inventaire après utilisation.")]
    public bool consumeOnUse = true;
    [Tooltip("Moves conseillés pour créer des combos efficaces avec cet objet.")]
    public List<MusicalMoveSO> recommendedMusicalMoves = new();

    [System.Serializable]
    public class EffectDefinition
    {
        [Tooltip("Type d'effet appliqué lorsque l'objet est utilisé.")]
        public ItemEffectType type = ItemEffectType.None;
        [Tooltip("Valeur de référence associée à l'effet (dégâts, portée supplémentaire, durée de sommeil...).")]
        public int value = 10;
    }

    [Header("Effets principaux")]
    [Tooltip("Liste des effets appliqués lors de l'utilisation de l'objet. L'ordre détermine l'effet principal utilisé par les systèmes existants.")]
    public List<EffectDefinition> effects = new();

    [SerializeField, HideInInspector, FormerlySerializedAs("effectType")]
    private ItemEffectType legacyEffectType = ItemEffectType.None;
    [SerializeField, HideInInspector, FormerlySerializedAs("effectValue")]
    private int legacyEffectValue = 10;

    private void EnsureEffectsInitialized()
    {
        if (effects == null)
            effects = new List<EffectDefinition>();

        if (effects.Count == 0)
        {
            effects.Add(new EffectDefinition
            {
                type = legacyEffectType,
                value = legacyEffectValue
            });
        }
        else if (effects[0] == null)
        {
            effects[0] = new EffectDefinition
            {
                type = legacyEffectType,
                value = legacyEffectValue
            };
        }
    }

    private EffectDefinition GetPrimaryEffect()
    {
        EnsureEffectsInitialized();
        return effects[0];
    }

    public IReadOnlyList<EffectDefinition> GetEffects()
    {
        EnsureEffectsInitialized();
        return effects;
    }

    public bool HasEffect(ItemEffectType type)
    {
        EnsureEffectsInitialized();
        foreach (var effect in effects)
        {
            if (effect == null)
                continue;

            if (effect.type == type)
                return true;
        }

        return false;
    }

    public int GetEffectValue(ItemEffectType type, int defaultValue = 0)
    {
        EnsureEffectsInitialized();
        foreach (var effect in effects)
        {
            if (effect == null)
                continue;

            if (effect.type == type)
                return effect.value;
        }

        return defaultValue;
    }

    public int GetTotalEffectValue(ItemEffectType type)
    {
        EnsureEffectsInitialized();
        int total = 0;
        foreach (var effect in effects)
        {
            if (effect == null || effect.type != type)
                continue;

            total += effect.value;
        }

        return total;
    }

    public ItemEffectType PrimaryEffectType => GetPrimaryEffect().type;
    public int PrimaryEffectValue => GetPrimaryEffect().value;

    // Compatibilité avec l'ancien champ unique.
    public ItemEffectType effectType => PrimaryEffectType;
    public int effectValue => PrimaryEffectValue;

    public bool isUsableInBattle = true;
    public float moveSpeed;
    public float castDistance;

    [Tooltip("Si vrai, l'utilisateur doit se déplacer ou se téléporter vers la cible pour l'utiliser")]
    public bool requiresMovement = true;

    [Header("Effets spéciaux")]
    // Délai entre disparition et réapparition lors d'un téléport
    // Utile pour synchroniser les effets visuels/sonores
    [Tooltip("Durée en secondes avant la réapparition après un téléport (0 = instantané)")]
    public float teleportDelay = 0f;

    [Header("Limitations d'utilisation")]
    [Tooltip("Nombre maximum d'utilisations par tour (0 = illimité)")]
    public int maxUsesPerTurn = 0;
    [Tooltip("Nombre maximum d'utilisations par combat (0 = illimité)")]
    public int maxUsesPerBattle = 0;

    [Header("Coup Critique")]
    [Tooltip("Active une variante lorsque le QTE est réussi")]
    public bool useCriticalVariant = false;
    public ItemEffectType criticalEffectType = ItemEffectType.Damage;
    public int criticalEffectValue = 20;

    [Tooltip("Si vrai, le lanceur reste à la position cible après l'action")] public bool stayInPlace;

    [Header("Heal Settings")]
    public int healAmount = 0;
    public bool healIsPercentage = false;

    [Header("Revive Settings")]
    public int revivePercentage = 50;

    [Header("Buff Settings")]
    public BuffStatType buffStat;              // Statistique augmentée
    public int buffAmount;                     // Valeur du bonus
    public float buffDuration;                 // Durée du bonus en tours
    public bool buffIsPercentage = false;      // Interpréter buffAmount en pourcentage ?

    [Header("Debuff Settings")]
    public DebuffStatType debuffStat;          // Statistique diminuée
    public int debuffAmount;                   // Valeur du malus
    public float debuffDuration;               // Durée du malus en tours
    public bool debuffIsPercentage = false;    // Interpréter debuffAmount en pourcentage ?

    [Header("Timing Boost Settings")]
    public TimingBoostType timingType;
    public float timingBoostAmount;
    public float timingDuration;

    [Header("Ciblage")]
    public TargetType defaultTargetType = TargetType.SingleAlly;
    public List<TargetType> targetTypes = new List<TargetType>() { TargetType.SingleAlly };
    [Tooltip("Autorise la sélection d'unités KO (utile pour la résurrection).")]
    public bool usableOnDeathUnits = false;

    [Header("Téléportation")]
    // Effet visuel déclenché au départ et à l'arrivée du téléport
    public GameObject tpVfx;
    // Son joué au départ du téléport
    public AudioClipSO tpSFx_Start;
    // Son joué à l'arrivée du téléport
    public AudioClipSO tpSFx_End;

    [FormerlySerializedAs("tpVfx_Start")]
    [SerializeField, HideInInspector] private GameObject legacyTpVfxStart;
    [FormerlySerializedAs("tpVfx_End")]
    [SerializeField, HideInInspector] private GameObject legacyTpVfxEnd;

    [Header("VFX")]
    [Tooltip("VFX instancié lors de l'utilisation de l'item.")]
    public GameObject itemVFX;
    [Min(0f)]
    [Tooltip("Délai (en secondes) avant d'instancier le VFX après le début de l'animation.")]
    public float vfxDelay = 0f;

    [Header("Motif de caméra")]
    [Tooltip("Motif utilisé pendant toute l'utilisation de l'item.")]
    public CameraMotifSO cameraMotif;

    [FormerlySerializedAs("preparingCameraMotif")]
    [SerializeField, HideInInspector] private CameraMotifSO legacyPreparingCameraMotif;
    [FormerlySerializedAs("performingCameraMotif")]
    [SerializeField, HideInInspector] private CameraMotifSO legacyPerformingCameraMotif;
    [FormerlySerializedAs("retreatCameraMotif")]
    [SerializeField, HideInInspector] private CameraMotifSO legacyRetreatCameraMotif;

#if UNITY_EDITOR
    // ------------------------------------------------------------------
    // Références par défaut
    // ------------------------------------------------------------------
    // Les nouveaux Items doivent initialiser leurs caméras en se basant sur
    // l'objet d'exemple "Item_LonguePortee". Cela évite de créer des
    // variations involontaires et facilite le travail des game designers.
    private const string LONGUE_PORTEE_PATH =
        "Assets/Items/Item_LonguePortee/Item_LonguePortee.asset";

    private void OnValidate()
    {
        EnsureEffectsInitialized();
        MigrateLegacyCameraMotif(markDirty: true);
        MigrateLegacyTeleportVfx(markDirty: true);

        // Récupère l'Item de référence. Si introuvable, on ne fait rien pour
        // éviter les messages d'erreur intempestifs.
        var reference = AssetDatabase.LoadAssetAtPath<ItemData>(LONGUE_PORTEE_PATH);
        if (reference != null)
        {
            reference.MigrateLegacyCameraMotif(markDirty: false);
            // Applique les valeurs par défaut uniquement si les champs sont vides.
            if (cameraMotif == null)
                cameraMotif = reference.cameraMotif;
        }

        ValidateConfiguration();
    }

    private void ValidateConfiguration()
    {
        bool dirty = false;

        if (string.IsNullOrWhiteSpace(itemID))
            Debug.LogWarning($"[ItemData] itemID est vide pour '{name}'.", this);
        if (string.IsNullOrWhiteSpace(itemName))
            Debug.LogWarning($"[ItemData] itemName est vide pour '{name}'.", this);
        if (itemIcon == null)
            Debug.LogWarning($"[ItemData] itemIcon manquant pour '{name}'.", this);

        if (maxUsesPerTurn < 0)
        {
            maxUsesPerTurn = 0;
            dirty = true;
            Debug.LogWarning($"[ItemData] maxUsesPerTurn negatif corrige pour '{name}'.", this);
        }
        if (maxUsesPerBattle < 0)
        {
            maxUsesPerBattle = 0;
            dirty = true;
            Debug.LogWarning($"[ItemData] maxUsesPerBattle negatif corrige pour '{name}'.", this);
        }

        if (buffDuration < 0f)
        {
            buffDuration = 0f;
            dirty = true;
            Debug.LogWarning($"[ItemData] buffDuration negatif corrige pour '{name}'.", this);
        }
        if (debuffDuration < 0f)
        {
            debuffDuration = 0f;
            dirty = true;
            Debug.LogWarning($"[ItemData] debuffDuration negatif corrige pour '{name}'.", this);
        }

        if (castDistance < 0f)
        {
            castDistance = 0f;
            dirty = true;
            Debug.LogWarning($"[ItemData] castDistance negatif corrige pour '{name}'.", this);
        }

        if (moveSpeed < 0f)
        {
            moveSpeed = 0f;
            dirty = true;
            Debug.LogWarning($"[ItemData] moveSpeed negatif corrige pour '{name}'.", this);
        }

        if (targetTypes == null)
        {
            targetTypes = new List<TargetType>();
            dirty = true;
        }
        if (targetTypes.Count == 0)
        {
            targetTypes.Add(defaultTargetType);
            dirty = true;
            Debug.LogWarning($"[ItemData] targetTypes vide, ajout du defaultTargetType pour '{name}'.", this);
        }
        else if (!targetTypes.Contains(defaultTargetType))
        {
            targetTypes.Add(defaultTargetType);
            dirty = true;
            Debug.LogWarning($"[ItemData] defaultTargetType manquant dans targetTypes pour '{name}'.", this);
        }

        if (requiresMovement && castDistance <= 0f)
            Debug.LogWarning($"[ItemData] requiresMovement actif avec castDistance <= 0 pour '{name}'.", this);

        if (beatPattern != null)
        {
            for (int i = 0; i < beatPattern.Count; i++)
            {
                if (beatPattern[i] <= 0f)
                {
                    Debug.LogWarning($"[ItemData] beatPattern contient une valeur <= 0 pour '{name}'.", this);
                    break;
                }
            }
        }

        if (dirty)
            EditorUtility.SetDirty(this);
    }
#endif

    private void OnEnable()
    {
        MigrateLegacyCameraMotif(markDirty: false);
        MigrateLegacyTeleportVfx(markDirty: false);
    }

    private void MigrateLegacyCameraMotif(bool markDirty)
    {
        if (cameraMotif != null)
            return;

        cameraMotif = legacyPerformingCameraMotif != null
            ? legacyPerformingCameraMotif
            : legacyPreparingCameraMotif != null
                ? legacyPreparingCameraMotif
                : legacyRetreatCameraMotif;

#if UNITY_EDITOR
        if (markDirty && cameraMotif != null)
        {
            legacyPreparingCameraMotif = null;
            legacyPerformingCameraMotif = null;
            legacyRetreatCameraMotif = null;
            EditorUtility.SetDirty(this);
        }
#endif
    }

    private void MigrateLegacyTeleportVfx(bool markDirty)
    {
        if (tpVfx != null)
            return;

        tpVfx = legacyTpVfxStart != null ? legacyTpVfxStart : legacyTpVfxEnd;

#if UNITY_EDITOR
        if (markDirty && tpVfx != null)
        {
            legacyTpVfxStart = null;
            legacyTpVfxEnd = null;
            EditorUtility.SetDirty(this);
        }
#endif
    }

    [Header("QTE Pattern")]
    public List<float> beatPattern;

    [System.Serializable]
    public class NoteVariant
    {
        public string label = "Frappe 1";
        public AudioClipSO baseNote;
        public AudioClipSO onParry;
        public AudioClipSO onEvade;
        public AudioClipSO onHit;
    }

    [Header("Notes avec Variantes Sonores")]
    public List<NoteVariant> notes;


}

public enum ItemEffectType { None, Heal, Revive, Buff, Debuff, BoostTiming, Damage, IncreaseRange, PreventInterception, ExtendEffects, Sleep, WakeUp, Stun }
public enum BuffStatType { None, Strength, Defense, Initiative, MaxHP, Power, CriticalRate }
public enum DebuffStatType { None, Strength, Defense, Initiative, MaxHP, Power, CriticalRate }
public enum TimingBoostType { None, ParryWindow, DodgeWindow }
