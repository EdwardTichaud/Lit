using UnityEngine;
using UnityEngine.Serialization;
using System.Collections.Generic;
// Les directives UnityEditor sont réservées à l'éditeur et ne doivent pas
// être incluses dans le build du joueur. Elles ont été retirées pour éviter
// les erreurs de compilation lors de l'export.
#if UNITY_EDITOR
using UnityEditor; // Utilisé uniquement pour charger les assets de référence.
#endif

[CreateAssetMenu(fileName = "NewMusicalMove", menuName = "Symphonie/Musical Move")]
public class MusicalMoveSO : ScriptableObject
{
    [Header("Identité et Catégorie")]
    [Tooltip("Nom de l'action affiché dans les menus et interfaces.")]
    public string moveName;
    [Tooltip("Catégorie générale permettant de regrouper les moves pour les joueurs novices comme expérimentés.")]
    public MoveType moveType = MoveType.Attack;
    [Tooltip("Vrai si l'attaque n'est disponible qu'en état Awake")]
    public bool onlyAwake = false;
    [Tooltip("Icône utilisée dans les interfaces pour représenter le move.")]
    public Sprite moveIcon;
    [TextArea, Tooltip("Description textuelle détaillant l'effet et l'utilisation du move.")]
    public string description;
    [Header("Inventaire & Accessibilité")]
    [Tooltip("Catégorie affichée dans l'inventaire pour guider les nouveaux joueurs.")]
    public string inventoryCategory = "Objet";
    [TextArea, Tooltip("Résumé court utilisé dans l'inventaire et les aides contextuelles.")]
    public string inventorySummary;
    [Tooltip("Si désactivé, l'objet reste dans l'inventaire après utilisation.")]
    public bool consumeOnUse = true;
    [Tooltip("Moves conseillés pour créer des combos efficaces avec cet objet.")]
    public List<MusicalMoveSO> recommendedMusicalMoves = new();
    // L'animation de ciblage est désormais pilotée par les AnimationEvents du move
    [Tooltip("Si vrai, le lanceur garde constamment la cible en ligne de mire lors du move.")]
    public bool stayFaceToTarget = true;

    [System.Serializable]
    public class NoteData
    {
        [Tooltip("Input à utiliser pour réussir le QTE (sprite + input Battle).")]
        public QTEInputSO qteInput;
        [Min(0f)]
        [Tooltip("Écart d'apparition avec la note suivante (en secondes).")]
        public float beatSpacing = 1f;
        [Min(0f)]
        [Tooltip("Temps de réponse avant que le cercle atteigne la scale 2 (en secondes).")]
        [FormerlySerializedAs("rhythm")]
        public float responseDelay = 0.5f;
    }

    [System.Serializable]
    public class PreparationFailureCondition
    {
        [Tooltip(
            "Type d'événement qui annule la préparation du move."
            + " Permet d'expliquer clairement aux débutants pourquoi la charge s'interrompt" 
            + " tout en laissant aux vétérans la possibilité de planifier autour de ces risques.")]
        public PreparationFailureConditionType conditionType = PreparationFailureConditionType.None;

        [Tooltip(
            "Valeur seuil utilisée par certaines conditions (par exemple quantité de dégâts reçus en une attaque)."
            + " Laisser à 0 si la condition n'utilise pas de valeur numérique.")]
        public float thresholdValue = 0f;

        [Tooltip(
            "Débuff précis qui annule la préparation lorsqu'il est appliqué."
            + " Ignoré pour les autres types de conditions mais utile pour refléter les enjeux narratifs décrits"
            + " dans l'Histoire de Symphonie.")]
        public DebuffStatType debuffType = DebuffStatType.None;

        [TextArea]
        [Tooltip(
            "Note de conception destinée à rappeler le contexte narratif ou stratégique de l'échec."
            + " Encourage les équipes à garder une cohérence avec l'histoire tout en documentant les combinaisons avancées.")]
        public string designerNote;
    }

    [Header("Timing")]
    // ⏱️ Délai ajouté avant l'animation du move.
    //    Permet d'insérer un temps mort contrôlé (par exemple pour une
    //    annonce ou un effet visuel) avant que le move ne commence
    //    réellement.
    [Tooltip("Temps en secondes à attendre AVANT de lancer l'animation du move")]
    [Min(0f)]
    [FormerlySerializedAs("startDelay")]
    public float animationDelay = 2f;
    [Tooltip("Délai (en secondes) avant le lancement des QTE.")]
    [Min(0f)]
    public float qteDelay = 0f;

    [Header("Partition musicale")]
    [Tooltip("Suite des notes à jouer pour réussir le QTE du move.")]
    public List<NoteData> notes = new();

    [Header("Ressources - Common")]
    [Tooltip("Type d'harmonique consommé pour payer le coût du move. Permet de guider les joueurs vers les bonnes synergies.")]
    public HarmonicType consumedHarmonicType = HarmonicType.Lumiere;
    [Tooltip("Coût en harmonie pour exécuter ce move.")]
    public int harmonicCost = 1;

    [Tooltip("Type d'harmonique ajouté à la réserve lorsqu'un gain est généré. Même si le gain est nul, cette information aide la lecture stratégique.")]
    public HarmonicType generatedHarmonicType = HarmonicType.Lumiere;
    [Tooltip("Gain d'harmonie généré lors de l'utilisation du move.")]
    public int harmonicGeneration = 0;

    [Header("Ressources - Special")]
    [Tooltip("Coût en fatigue pour exécuter ce move.")]
    public float fatigueCost = 1f;

    [System.Serializable]
    public class EffectDefinition
    {
        [Tooltip("Type d'effet appliqué lors de la résolution principale.")]
        public MusicalEffectType type = MusicalEffectType.Damage;
        [Tooltip("Valeur numérique associée à l'effet. Interprétation dépendante du type.")]
        public int value = 2000;
    }

    [Header("Effets principaux")]
    [Tooltip("Liste ordonnée des effets appliqués lors de l'utilisation du move. Le premier élément reste l'effet principal.")]
    public List<EffectDefinition> effects = new();

    [SerializeField, HideInInspector, FormerlySerializedAs("effectType")]
    private MusicalEffectType legacyEffectType = MusicalEffectType.Damage;
    [SerializeField, HideInInspector, FormerlySerializedAs("effectValue")]
    private int legacyEffectValue = 2000;

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

    public bool HasEffect(MusicalEffectType type)
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

    public int GetEffectValue(MusicalEffectType type, int defaultValue = 0)
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

    public MusicalEffectType PrimaryEffectType => GetPrimaryEffect().type;
    public int PrimaryEffectValue => GetPrimaryEffect().value;

    // Compatibilité avec l'ancien champ public unique.
    public MusicalEffectType effectType => PrimaryEffectType;
    public int effectValue => PrimaryEffectValue;

    [Header("Effets spécifiques")]
    [Tooltip("Prefab visuel utilise par certains effets passifs (LoyaltyMark, PrestoForcedAttack). Laisser vide si inutile.")]
    public GameObject passiveEffectPrefab;
    [Tooltip("Décalage vertical supplémentaire pour positionner le prefab passif sans chevaucher la cible.")]
    public float passiveEffectVerticalOffset = 0.5f;

    // ------------------------------------------------------------------
    // Buffs et débuffs supplémentaires
    // ------------------------------------------------------------------
    [Header("Effets secondaires optionnels")]
    [Tooltip("Stat à augmenter après l'application de l'effet principal (None = aucun bonus).")]
    public BuffStatType buffStat = BuffStatType.None;
    [Tooltip("Valeur brute ajoutée par le buff (en points ou pourcentage selon buffIsPercentage).")]
    public int buffAmount = 0;
    [Tooltip("Durée du buff en tours.")]
    public float buffDuration = 0f;
    [Tooltip("Interpréter buffAmount comme un pourcentage ?")]
    public bool buffIsPercentage = false;

    [Tooltip("Stat à réduire après l'effet principal (None = aucun malus).")]
    public DebuffStatType debuffStat = DebuffStatType.None;
    [Tooltip("Valeur brute retirée par le débuff (en points ou pourcentage selon debuffIsPercentage).")]
    public int debuffAmount = 0;
    [Tooltip("Durée du débuff en tours.")]
    public float debuffDuration = 0f;
    [Tooltip("Interpréter debuffAmount comme un pourcentage ?")]
    public bool debuffIsPercentage = false;

#if UNITY_EDITOR
    [SerializeField, HideInInspector] private MusicalEffectType previousEffectType = MusicalEffectType.Damage;
#endif
    [Header("Coup Critique")]
    [Tooltip("Active une variante lorsque le QTE est réussi")]
    public bool useCriticalVariant = false;
    public MusicalEffectType criticalEffectType = MusicalEffectType.Damage;
    public int criticalEffectValue = 20;
    public float criticalFatigueCost = 1f;
    public int criticalHarmonicCost = 1;
    public int criticalHarmonicGeneration = 0;

    [Header("Temps de recharge")]
    [Tooltip("Nombre de tours avant de pouvoir réutiliser le move")]
    public int cooldown = 0;

    [Header("Limitations d'utilisation")]
    [Tooltip("Nombre maximum d'utilisations par tour (0 = illimité)")]
    public int maxUsesPerTurn = 0;
    [Tooltip("Nombre maximum d'utilisations par combat (0 = illimité)")]
    public int maxUsesPerBattle = 0;

    [Header("Conditions de ciblage")]
    [Tooltip("Détermine si le move est utilisable lorsque la cible est au sol, en l'air ou dans les deux cas")]
    public AltitudeCondition altitudeCondition = AltitudeCondition.GroundOrAir; // Par défaut: aucune restriction
    [Tooltip("Type de cible autorisé pour ce move.")]
    public TargetType targetType = TargetType.SingleEnemy;
    [Tooltip("Type de cible utilisé pour positionner le curseur au début de la sélection.")]
    public TargetType defaultTargetType = TargetType.SingleEnemy;
    [Tooltip("Autorise la sélection d'unités KO (utile pour la résurrection).")]
    public bool usableOnDeathUnits = false;

    [Header("Déplacement")]
    // La gestion des effets visuels est désormais confiée aux AnimationEvents,
    // seul le déplacement reste paramétrable dans ce ScriptableObject.
    [Tooltip("Temps total (en secondes) pour atteindre la position cible. Mettre 0 pour une téléportation instantanée.")]
    public float travelTime = 0f;
    [FormerlySerializedAs("moveSpeed"), SerializeField, HideInInspector]
    private float legacyMoveSpeed = 0f;
    [SerializeField, HideInInspector]
    private bool legacyMoveSpeedConverted = false;
    [Tooltip("Distance maximale de lancement lorsque le move le nécessite")]
    public float castDistance;
    [Tooltip("Si vrai, le lanceur doit se déplacer ou se téléporter pour exécuter ce move")]
    public bool requiresMovement = true;
    // Temps entre la disparition et la réapparition lors d'une téléportation
    // Permet de laisser les effets visuels/sonores se jouer avant le déplacement
    [Tooltip("Délai en secondes entre la disparition et la réapparition (0 = instantané)")]
    public float teleportDelay = 0.2f;
    [Tooltip("Si vrai, le lanceur reste à la position cible en fin de move")]
    public bool stayInPlace = false;
    [Tooltip("Si faux, le move ne peut pas être intercepté")]
    public bool interceptable = true;
    [Header("Placement autour de la cible")]
    [Tooltip("Position relative prise par le lanceur autour de la cible lors de l'exécution")]
    public RelativePosition relativePosition = RelativePosition.Front;

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

    [Header("Effets sonores")]
    [Tooltip("Son d'avertissement joué avant l'attaque pour prévenir le joueur")]
    public AudioClipSO warningClip;

    [Header("Préparation sur plusieurs tours")]
    [Tooltip(
        "Active une phase de charge qui doit être menée à terme avant l'exécution réelle du move."
        + " Si cette option est cochée, le move démarre lors du tour choisi mais ne se résout qu'après la préparation.")]
    public bool requiresPreparationBeforeExecution = false;

    [Tooltip(
        "Nombre de tours complets nécessaires pour terminer la préparation."
        + " Une valeur de 0 laisse l'exécution se faire immédiatement, même si la charge est activée.")]
    [Min(0)]
    public int preparationTurnCount = 0;

    [Tooltip(
        "Liste des événements qui annulent la préparation si elle survient pendant la charge."
        + " Permet de configurer des seuils de dégâts, des débuffs ou tout déclencheur scénarisé.")]
    public List<PreparationFailureCondition> preparationFailureConditions = new();

    // Valeur plancher appliquée automatiquement aux moves à charge afin d'assurer
    // que les dégâts massifs interrompent toujours la préparation à moins que les
    // concepteurs n'en décident autrement explicitement.
    private const float DefaultDamageInterruptionThreshold = 20000f;


    [Header("Awake")]
    [Tooltip("Si vrai, ce move fait entrer le lanceur en mode Awake")] public bool enterAwake = false;

    [Header("Animation")]
    public AnimationClip preparingAnimation;
    [Tooltip("Animation principale jouée lors de l'exécution du move.")]
    public AnimationClip performingAnimation;

    [Header("Motif de caméra")]
    [Tooltip("Motif utilisé dès la sélection du move et conservé jusqu'à un changement explicite.")]
    public CameraMotifSO cameraMotif;


#if UNITY_EDITOR
    // ------------------------------------------------------------------
    // Références par défaut
    // ------------------------------------------------------------------
    // Lorsque l'on crée un nouveau MusicalMove, le motif de caméra
    // doit automatiquement reprendre la valeur de l'exemple
    // "MusicalMove_Rhapsodie" afin d'assurer une cohérence visuelle dans
    // tout le projet. Cette méthode est uniquement exécutée dans l'éditeur
    // afin d'éviter toute dépendance aux fichiers lors du build final.
    private const string RHAPSODIE_PATH =
        "Assets/MusicalMoves/MusicalMove_Rhapsodie_Lumiere/MusicalMove_Rhapsodie_Lumiere.asset";

    private void OnValidate()
    {
        MigrateLegacyTeleportVfx(markDirty: true);
        EnsureEffectsInitialized();
        var primaryEffect = GetPrimaryEffect();
        if (previousEffectType != primaryEffect.type)
        {
            primaryEffect.value = ResolveDefaultEffectValue(primaryEffect.type);
            previousEffectType = primaryEffect.type;
            EditorUtility.SetDirty(this);
        }

        // Conversion automatique des anciens assets qui utilisaient une vitesse de déplacement.
        // Cette opération ne s'effectue qu'une seule fois pour préserver les réglages manuels.
        if (!legacyMoveSpeedConverted)
        {
            if (travelTime <= 0f && legacyMoveSpeed > 0f)
            {
                const float defaultReferenceDistance = 1.5f;
                float referenceDistance = castDistance > 0f ? castDistance : defaultReferenceDistance;

                const float minimumTravelTime = 0.05f;
                travelTime = Mathf.Max(referenceDistance / legacyMoveSpeed, minimumTravelTime);
                EditorUtility.SetDirty(this);
            }

            legacyMoveSpeedConverted = true;
        }

        if (cameraMotif == null)
        {
            var serialized = new SerializedObject(this);
            serialized.Update();
            var performing = serialized.FindProperty("performingCameraMotif");
            var preparing = serialized.FindProperty("preparingCameraMotif");
            var retreat = serialized.FindProperty("retreatCameraMotif");

            cameraMotif = performing != null && performing.objectReferenceValue != null
                ? performing.objectReferenceValue as CameraMotifSO
                : preparing != null && preparing.objectReferenceValue != null
                    ? preparing.objectReferenceValue as CameraMotifSO
                    : retreat != null && retreat.objectReferenceValue != null
                        ? retreat.objectReferenceValue as CameraMotifSO
                        : cameraMotif;

            if (cameraMotif != null)
            {
                if (performing != null)
                    performing.objectReferenceValue = null;
                if (preparing != null)
                    preparing.objectReferenceValue = null;
                if (retreat != null)
                    retreat.objectReferenceValue = null;

                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(this);
            }
        }

        // Charge le ScriptableObject de référence. Si le fichier est déplacé
        // ou supprimé, aucune action n'est effectuée pour ne pas provoquer
        // d'erreur dans l'éditeur.
        var reference = AssetDatabase.LoadAssetAtPath<MusicalMoveSO>(RHAPSODIE_PATH);
        if (reference != null)
        {
            // Pour chaque champ, si aucune valeur n'est renseignée, on copie
            // celle de "MusicalMove_Rhapsodie". Les concepteurs peuvent ensuite
            // remplacer ces valeurs manuellement selon les besoins spécifiques
            // du nouveau move.
            if (cameraMotif == null)
                cameraMotif = reference.cameraMotif;
        }

        // Enfin, on sécurise la configuration des moves nécessitant une préparation
        // multi-tour : deux garde-fous sont ajoutés ou mis à jour pour garantir que
        // des dégâts colossaux ou une interruption explicite stoppe bien la charge.
        EnsureDefaultPreparationFailureConditions();

        ValidateConfiguration();
    }

    private static bool IsDurationBasedEffect(MusicalEffectType type)
    {
        return type == MusicalEffectType.Sleep
               || type == MusicalEffectType.LoyaltyMark
               || type == MusicalEffectType.LinkMark
               || type == MusicalEffectType.AnchorGround
               || type == MusicalEffectType.SuspendAir
               || type == MusicalEffectType.PrestoForcedAttack
               || type == MusicalEffectType.Stun;
    }

    private static bool IsPercentageBasedEffect(MusicalEffectType type)
    {
        return type == MusicalEffectType.IncreaseDamage
               || type == MusicalEffectType.DecreaseDamage
               || type == MusicalEffectType.IncreaseDefense
               || type == MusicalEffectType.DecreaseDefense
               || type == MusicalEffectType.IncreaseInitiative
               || type == MusicalEffectType.DecreaseInitiative
               || type == MusicalEffectType.IncreaseMaxHP
               || type == MusicalEffectType.DecreaseMaxHP;
    }

    private static int ResolveDefaultEffectValue(MusicalEffectType type)
    {
        if (type == MusicalEffectType.FillATB)
            return 100;

        if (IsDurationBasedEffect(type))
            return 2;

        if (IsPercentageBasedEffect(type))
            return 10;

        return 2000;
    }

    private void ValidateConfiguration()
    {
        bool dirty = false;

        if (string.IsNullOrWhiteSpace(moveName))
            Debug.LogWarning("[MusicalMoveSO] moveName est vide.", this);
        if (moveIcon == null)
            Debug.LogWarning($"[MusicalMoveSO] moveIcon manquant pour '{name}'.", this);

        if (fatigueCost < 0f)
        {
            fatigueCost = 0f;
            dirty = true;
            Debug.LogWarning($"[MusicalMoveSO] fatigueCost negatif corrige pour '{name}'.", this);
        }
        if (harmonicCost < 0)
        {
            harmonicCost = 0;
            dirty = true;
            Debug.LogWarning($"[MusicalMoveSO] harmonicCost negatif corrige pour '{name}'.", this);
        }
        if (maxUsesPerTurn < 0)
        {
            maxUsesPerTurn = 0;
            dirty = true;
            Debug.LogWarning($"[MusicalMoveSO] maxUsesPerTurn negatif corrige pour '{name}'.", this);
        }
        if (maxUsesPerBattle < 0)
        {
            maxUsesPerBattle = 0;
            dirty = true;
            Debug.LogWarning($"[MusicalMoveSO] maxUsesPerBattle negatif corrige pour '{name}'.", this);
        }

        if (requiresPreparationBeforeExecution)
        {
            if (preparationTurnCount <= 0)
                Debug.LogWarning($"[MusicalMoveSO] preparationTurnCount <= 0 alors que la preparation est active pour '{name}'.", this);
            if (performingAnimation == null)
                Debug.LogWarning($"[MusicalMoveSO] performingAnimation manquante pour '{name}'.", this);
        }

        if (castDistance < 0f)
        {
            castDistance = 0f;
            dirty = true;
            Debug.LogWarning($"[MusicalMoveSO] castDistance negatif corrige pour '{name}'.", this);
        }
        if (travelTime < 0f)
        {
            travelTime = 0f;
            dirty = true;
            Debug.LogWarning($"[MusicalMoveSO] travelTime negatif corrige pour '{name}'.", this);
        }

        if (dirty)
            EditorUtility.SetDirty(this);
    }
#endif

    private void OnEnable()
    {
        MigrateLegacyTeleportVfx(markDirty: false);
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

    /// <summary>
    ///     Injecte automatiquement les conditions minimales d'échec de préparation.
    ///     Tous les moves nécessitant une charge multi-tour doivent au moins :
    ///     - être interrompus par une attaque dépassant <see cref="DefaultDamageInterruptionThreshold"/> points de dégâts;
    ///     - échouer lorsqu'un MusicalMove adverse spécifiquement conçu pour casser les préparations est reçu.
    ///     Les concepteurs peuvent ajouter d'autres conditions manuellement, mais ces garde-fous
    ///     garantissent une base cohérente pour l'équilibrage comme pour la narration.
    /// </summary>
    private void EnsureDefaultPreparationFailureConditions()
    {
        if (!requiresPreparationBeforeExecution)
            return;

        if (preparationFailureConditions == null)
            preparationFailureConditions = new List<PreparationFailureCondition>();

        // ------------------------------------------------------------------
        // 1. Vérifie l'interruption par dégâts massifs.
        // ------------------------------------------------------------------
        PreparationFailureCondition damageCondition = null;
        foreach (var condition in preparationFailureConditions)
        {
            if (condition == null)
                continue;

            if (condition.conditionType == PreparationFailureConditionType.DamageFromSingleAttack)
            {
                damageCondition = condition;
                break;
            }
        }

        if (damageCondition == null)
        {
            damageCondition = new PreparationFailureCondition
            {
                conditionType = PreparationFailureConditionType.DamageFromSingleAttack,
                thresholdValue = DefaultDamageInterruptionThreshold,
                designerNote =
                    "Sécurité automatique : dégâts colossaux interrompent la charge pour protéger le lanceur."
            };
            preparationFailureConditions.Add(damageCondition);
        }
        else
        {
            if (damageCondition.thresholdValue < DefaultDamageInterruptionThreshold)
                damageCondition.thresholdValue = DefaultDamageInterruptionThreshold;

            if (string.IsNullOrWhiteSpace(damageCondition.designerNote))
            {
                damageCondition.designerNote =
                    "Sécurité automatique : dégâts colossaux interrompent la charge pour protéger le lanceur.";
            }
        }

        // ------------------------------------------------------------------
        // 2. Vérifie l'interruption par un autre MusicalMove.
        // ------------------------------------------------------------------
        PreparationFailureCondition interruptingMoveCondition = null;
        foreach (var condition in preparationFailureConditions)
        {
            if (condition == null)
                continue;

            if (condition.conditionType == PreparationFailureConditionType.InterruptingMusicalMove)
            {
                interruptingMoveCondition = condition;
                break;
            }
        }

        if (interruptingMoveCondition == null)
        {
            interruptingMoveCondition = new PreparationFailureCondition
            {
                conditionType = PreparationFailureConditionType.InterruptingMusicalMove,
                designerNote =
                    "Sécurité automatique : réactions ennemies prévues pour casser la préparation interrompent le move."
            };
            preparationFailureConditions.Add(interruptingMoveCondition);
        }
        else if (string.IsNullOrWhiteSpace(interruptingMoveCondition.designerNote))
        {
            interruptingMoveCondition.designerNote =
                "Sécurité automatique : réactions ennemies prévues pour casser la préparation interrompent le move.";
        }
    }

    /// <summary>
    ///     Indique si le move doit utiliser une téléportation instantanée pour atteindre sa cible.
    ///     Tient compte du nouveau paramètre de durée et des anciennes données de vitesse.
    /// </summary>
    public bool ShouldTeleportToTarget()
    {
        if (!requiresMovement)
            return false;

        if (Mathf.Approximately(travelTime, 0f))
            return true;

        if (travelTime > 0f)
            return false;

        return legacyMoveSpeed <= 0f;
    }

    /// <summary>
    ///     Retourne la durée du déplacement vers la cible en secondes.
    ///     Si un temps explicite est défini, il est utilisé directement, sinon on retombe sur l'ancienne vitesse.
    /// </summary>
    public float GetTravelDuration(float distance)
    {
        if (!requiresMovement)
            return 0f;

        if (travelTime > 0f)
            return travelTime;

        if (legacyMoveSpeed > 0f && distance > 0f)
            return distance / legacyMoveSpeed;

        return 0f;
    }


}

/// <summary>
/// Catégorie générale permettant de classer les <see cref="MusicalMoveSO"/>.
/// Utile pour organiser les actions et guider le joueur dans ses choix.
/// </summary>
public enum MoveType
{
    Empty,      // Move factice utilisé comme espace réservé.
    Attack,     // Mouvement offensif infligeant des dégâts directs.
    Buff,       // Action accordant un effet bénéfique à un allié.
    Debuff,     // Action affaiblissant un adversaire.
    Alteration  // Modifie le terrain ou l'état d'une cible sans être purement offensif ou défensif.
}

public enum MusicalEffectType
{
    Damage,
    Heal,
    Sleep,
    WakeUpAll,
    LoyaltyMark,
    LinkMark,
    AnchorGround,
    SuspendAir,
    PrestoForcedAttack,
    IncreaseDamage,
    DecreaseDamage,
    IncreaseDefense,
    DecreaseDefense,
    IncreaseInitiative,
    DecreaseInitiative,
    IncreaseMaxHP,
    DecreaseMaxHP,
    Stun,
    FillATB
}

public enum RelativePosition { Front, Back, Left, Right, NC, On }

/// <summary>
/// Définit dans quel contexte d'altitude un <see cref="MusicalMoveSO"/> est réalisable.
/// </summary>
public enum AltitudeCondition { GroundOnly, AirOnly, GroundOrAir }

/// <summary>
/// Liste les événements capables d'annuler une préparation multi-tour d'un <see cref="MusicalMoveSO"/>.
/// </summary>
public enum PreparationFailureConditionType
{
    None,                   // Aucune condition : la charge se déroule sans risque.
    DamageFromSingleAttack, // Un coup dépassant le seuil annule immédiatement la préparation.
    DamageAccumulatedDuringPreparation, // Le cumul de dégâts sur la phase de charge interrompt le move.
    AnyDebuffApplied,       // Tout débuff appliqué au lanceur met fin à la préparation.
    SpecificDebuffApplied,  // Seul un type précis de débuff annule la charge.
    InterruptingMusicalMove, // Un MusicalMove ennemi désigné comme interruptif stoppe la préparation.
    CustomEvent             // Condition narrative ou scriptée, documentée dans designerNote.
}
