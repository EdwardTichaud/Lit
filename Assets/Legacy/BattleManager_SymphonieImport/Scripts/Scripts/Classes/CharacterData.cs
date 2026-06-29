using System;
using UnityEngine;
using System.Collections.Generic; // Permet l'utilisation de listes génériques
using UnityEngine.Serialization; // Facilite la migration des anciens champs (par exemple resonancePoint).

/// <summary>
/// ScriptableObject décrivant complètement un personnage jouable ou ennemi.
/// L'objectif de cette classe est d'offrir des catégories clairement identifiées
/// pour faciliter la maintenance et l'extension du contenu.
/// </summary>
[CreateAssetMenu(fileName = "CharacterData", menuName = "Symphonie/CharacterData")]
public class CharacterData : ScriptableObject, ITargetable
{
    #region Présentation & Classification

    [Header("Identité visuelle")]
    [Tooltip("Nom complet affiché dans l'interface et les dialogues.")]
    public string characterName;

    [Tooltip("Portrait principal utilisé dans les menus et fiches de personnage.")]
    public Sprite portrait;

    [Tooltip("Liste de portraits alternatifs pour l'écran Versus et les points d'apparition.")]
    public List<Sprite> versusSprites = new();

    [Header("Catégories de gameplay")]
    [Tooltip("Type narratif du personnage, utilisé pour filtrer les interactions scénaristiques.")]
    public CharacterType characterType;

    [Tooltip("Style de jeu principal du personnage (ex : Rage, Harmonie...).")]
    public GameplayType gameplayType = GameplayType.Rage;

    [Tooltip("Affinité harmonique majeure qui influence les synergies de l'équipe.")]
    public HarmonicType harmonicType = HarmonicType.Lumiere;

    [Tooltip("Niveau de sophistication de l'IA ennemie contrôlant ce personnage.")]
    public EnemyIntelligenceLevel intelligenceLevel = EnemyIntelligenceLevel.Normal;

    [Header("Représentation 3D")]
    [Tooltip("Prefab affiché dans les environnements explorables.")]
    public GameObject characterWorldModel;

    [Tooltip("Prefab utilisé pendant les combats.")]
    public GameObject characterBattleModel;

    [Header("Positionnement dans les zones")]
    [Tooltip("Indice du slot de départ dans un champ de bataille scénarisé.")]
    public int battlefieldIndex = 0;

    #endregion

    #region Mobilité & Comportement

    [Header("Mobilité")]
    [Tooltip("Vrai si l'unité évolue dans les airs et n'est pas affectée par la gravité.")]
    public bool isAirUnit = false;

    [Tooltip("Hauteur supplémentaire appliquée lors du spawn pour les unités aériennes.")]
    public float distanceFromGround = 0f;

    [Header("Réactions en combat")]
    [Tooltip("Si vrai, ce personnage peut être intercepté lors de ses actions.")]
    public bool interceptable = true;

    [Tooltip("Si vrai, ses attaques sont évitables par les adversaires.")]
    public bool avoidable = true;

    #endregion

    #region Statistiques de base

    [Header("Attributs fondamentaux")]
    [Tooltip("Mesure de la réactivité aux événements (esquives, parades, etc.).")]
    public float baseReflex;

    [Tooltip("Capacité à se déplacer rapidement sur le terrain.")]
    public float baseMobility;

    [Tooltip("Résilience globale qui augmente les points de vie effectifs.")]
    public float baseVitality;

    [Tooltip("Puissance harmonique servant de base aux calculs magiques.")]
    public float basePower;

    [Tooltip("Stabilité émotionnelle réduisant les risques de désaccords harmoniques.")]
    public float baseStability;

    [Tooltip("Perspicacité permettant d'exploiter les faiblesses ennemies.")]
    public float baseSagacity;

    [Header("Paramètres communs")]
    [Tooltip("Points de vie de base hors bonus narratifs.")]
    public float baseHP;

    [Tooltip("Attaque physique moyenne.")]
    public float baseStrength;

    [Tooltip("Défense physique moyenne.")]
    public float baseDefense;

    [Tooltip("Vitesse de déplacement et d'exécution de base.")]
    public float baseSpeed;

    [Tooltip("Portée moyenne des attaques physiques.")]
    public float baseRange;

    [Tooltip("Initiative de départ utilisée dans la timeline de combat.")]
    public float baseInitiative;

    [Tooltip("Portée d'interception utilisée pour stopper une action adverse.")]
    public float baseInterceptionRange;

    [Header("Paramètres spécifiques - Lucian")]
    [Tooltip("Rage initiale de Lucian au début d'un affrontement.")]
    public float baseRage;

    [Tooltip("Réserve maximale de rage que Lucian peut cumuler.")]
    public float maxRage;

    [Tooltip("Multiplicateur appliqué aux dégâts lorsque Lucian consomme sa rage.")]
    public float rageDamageMultiplier;

    [Header("Paramètres spécifiques - Thalia")]
    [Tooltip("Fatigue initiale de Thalia, utile pour calibrer la difficulté d'accès à ses combos.")]
    public float baseFatigue;

    [Tooltip("Limite de fatigue avant les malus narratifs ou mécaniques.")]
    public float maxFatigue;

    #endregion

    #region Configurations de combat

    [Header("Attaques musicales")]

    [Tooltip("Attaque de base toujours disponible quel que soit l'équipement.")]
    public MusicalMoveSO[] basicAttack;

    [Tooltip("Répertoire complet des attaques disponibles pour ce personnage.")]
    public MusicalMoveSO[] musicalAttacks;

    [Tooltip("Attaque musicale toujours disponible quel que soit l'équipement.")]
    public MusicalMoveSO specialMusicalMove;

    [Header("Jeux d'actions favoris")]
    [Tooltip("Configurations d'attaques musicales favorites pour accélérer la sélection en combat.")]
    public List<CharacterMusicalMoveSet> musicalMoveSets = new();

    [Tooltip("Index utilisé au chargement pour activer un set musical spécifique. -1 pour aucun.")]
    public int defaultMusicalMoveSetIndex = -1;

    [Tooltip("Configurations d'items favorites pour mettre en avant les objets clefs en combat.")]
    public List<CharacterItemSet> itemSets = new();

    [Tooltip("Index utilisé au chargement pour activer un set d'objets spécifique. -1 pour aucun.")]
    public int defaultItemSetIndex = -1;

    #endregion

    #region Contrôle & runtime

    [Header("Contrôle")]
    [Tooltip("Indique si l'unité est contrôlée par le joueur à l'instant T.")]
    public bool isPlayerControlled;

    // Les valeurs dynamiques (PV, stats, sets actifs, etc.) ont été déplacées dans
    // CharacterUnit afin d'éviter de muter les ScriptableObjects en jeu.

    #endregion

    #region Audio & Effets

    [Header("Impacts & réactions")]

    public AudioClipSO turnStartVoiceline;
    public AudioClipSO turnEndVoiceline;
    public AudioClipSO baseAttackVoiceline;

    [Tooltip("Effet sonore principal joué lors d'un coup reçu.")]
    public AudioClipSO hitSound;

    [Tooltip("Cri joué lors d'un coup critique infligé.")]
    public AudioClipSO criticalHitSound;

    [Tooltip("Voix jouée lorsque l'unité est interceptée.")]
    public AudioClipSO interceptedSound;

    [Tooltip("Voix jouée lorsque l'unité intercepte une autre unité.")]
    public AudioClipSO interceptionSound;

    [Tooltip("Réplique jouée si la cible meurt avant la fin d'une attaque.")]
    public AudioClipSO prematureDeathTaunt;

    [Tooltip("Réplique jouée si la cible devait subir des dégâts mais a une LoyaltyMark sur elle. La cible doit être alliée du lanceur, sinon pas de son joué.")]
    public AudioClipSO loyaltyMarkTargetAlly;

    [Tooltip("Son joue lorsque l'unite est la derniere survivante de son camp.")]
    public AudioClipSO lastStandAudioClip;

    [Tooltip("Effet visuel déclenché lorsqu'un coup touche la cible.")]
    public GameObject hitEffect;

    [Tooltip("Effet visuel déclenché lors de la mort du personnage.")]
    public GameObject deathEffect;

    [Header("Déplacements rapides")]
    [Tooltip("Système de particules utilisé pendant les déplacements rapides de cette unité.")]
    public GameObject dashParticlesPrefab;

    [Tooltip("Effet sonore joué lorsque les particules de dash sont générées pour cette unité.")]
    public AudioClipSO dashSoundClip;

    [Tooltip("Effet visuel déclenché au début de l'éveil (Awake).")]
    public GameObject awakenEffect_Start;

    [Tooltip("Effet visuel maintenu durant l'éveil (Awake).")]
    public GameObject awakenEffect_Loop;

    [Tooltip("Effet visuel utilisé à la fin de l'éveil (Awake).")]
    public GameObject awakenEffect_End;

    [Tooltip("Effet visuel déclenché lorsque l'unité sombre en dissonance.")]
    public GameObject dissonanceEffect_Start;

    [Tooltip("Effet visuel maintenu tant que l'unité est dissonante.")]
    public GameObject dissonanceEffect_Loop;

    [Tooltip("Effet visuel joué lorsque l'unité se libère de la dissonance.")]
    public GameObject dissonanceEffect_End;

    [Header("Réglages Awake & Dissonance")]
    [Tooltip("Multiplicateur appliqué à toutes les statistiques courantes lorsque l'unité entre en état Awake.")]
    public float awakeStatMultiplier = 1.5f;

    #endregion

    #region Animations

    [Header("Animations d'impact")]

    [Tooltip("Animation jouée quand l'unité est interceptée.")]
    public AnimationClip interceptedAnimation;

    [Tooltip("Animation jouée quand l'unité subit des dégâts.")]
    public AnimationClip hitAnimation;

    [Tooltip("Animation de décès.")]
    public AnimationClip deathAnimation;

    [Header("Animations de déplacement")]
    [Tooltip("Animation de marche utilisée pendant les déplacements.")]
    public AnimationClip walkAnimation;

    [FormerlySerializedAs("movementAnimation")]
    [Tooltip("Animation de course utilisée pendant les déplacements.")]
    public AnimationClip runAnimation;

    [Header("Animations de téléportation")]
    [Tooltip("Animation jouée avant la téléportation.")]
    public AnimationClip TPAnimation_Start;

    [Tooltip("Animation jouée juste après la téléportation.")]
    public AnimationClip TPAnimation_Destination;

    [Header("Animations de ciblage")]
    [Tooltip("Animation utilisée pour montrer que le caster va cibler une cible.")]
    public AnimationClip prepareToTargetAnimation;

    [Tooltip("Animation utilisée pour préparer la cible à subir un coup.")]
    public AnimationClip prepareToUndergoAnimation;

    [Header("Animations d'états spéciaux")]
    [Tooltip("Animation Idle utilisée lorsque l'unité est en état Awake.")]
    public AnimationClip awakenIdleAnimation;

    [Tooltip("Animation Idle utilisée lorsque l'unité est dissonante.")]
    public AnimationClip dissonantIdleAnimation;

    [Tooltip("Animation d'entrée en Awake utilisée pour overrider l'Animator si nécessaire.")]
    public AnimationClip awakeEnterAnimation;

    [Tooltip("Animation de sortie de l'Awake utilisée pour overrider l'Animator si nécessaire.")]
    public AnimationClip awakeExitAnimation;

    [Tooltip("Animation d'entrée dans la dissonance.")]
    public AnimationClip dissonanceEnterAnimation;

    [Tooltip("Animation jouée lorsque la dissonance prend fin.")]
    public AnimationClip dissonanceExitAnimation;

    #endregion

    #region Audio contextuel

    [Header("Sons de déplacement")]
    [Tooltip("Clip joué au début d'un mouvement.")]
    public AudioClipSO moveStartClip;

    [Tooltip("Clip joué à la fin d'un mouvement.")]
    public AudioClipSO moveEndClip;

    [Header("Interface & sélection")]
    [Tooltip("Clip joué lorsque cette unité devient active dans les menus de combat.")]
    public AudioClipSO menuSelectionClip;

    [Header("Transitions d'état")] // Clips joués lors des transitions entre les états majeurs de l'unité.
    [Tooltip("Clip joué lorsque l'unité entre automatiquement en éveil (Awake).")]
    public AudioClipSO awakeEnterClip;

    [Tooltip("Clip joué lorsque l'unité quitte l'éveil (Awake).")]
    public AudioClipSO awakeExitClip;

    [Tooltip("Clip joué lorsque l'unité devient dissonante.")]
    public AudioClipSO dissonanceEnterClip;

    [Tooltip("Clip joué lorsque l'unité sort de la dissonance.")]
    public AudioClipSO dissonanceExitClip;

    [Tooltip("Clip joué lorsqu'une animation d'Idle démarre (tous états confondus).")]
    public AudioClipSO idleEnterClip;

    [Tooltip("Clip joué lorsqu'une animation d'Idle est interrompue.")]
    public AudioClipSO idleExitClip;

    [Header("Voix de deuil")]
    [Tooltip("Lamentation jouée par ce personnage quand Lucian meurt.")]
    public AudioClipSO weepForLucianDeath;

    [Tooltip("Lamentation jouée par ce personnage quand Thalia meurt.")]
    public AudioClipSO weepForThaliaDeath;

    [Tooltip("Lamentation jouée par ce personnage quand Kael meurt.")]
    public AudioClipSO weepForKaelDeath;

    [Tooltip("Lamentation jouée par ce personnage quand Link meurt.")]
    public AudioClipSO weepForLinkDeath;

    [Tooltip("Lamentation jouée par ce personnage quand Luna meurt.")]
    public AudioClipSO weepForLunaDeath;

    #endregion

    #region Gestion harmonique & métadonnées

    [Header("Gestion des harmoniques")]
    [FormerlySerializedAs("resonancePoint"), Tooltip("Quantité d'harmoniques du type signature à accumuler pour pouvoir déclencher l'état Awake.")]
    public int awakeHarmonicThreshold = 1;

    [Tooltip("Seuil minimal d'harmoniques avant de sortir d'Awake.")]
    public int dissonancePoint = 0;

    [Tooltip("Réserve d'harmoniques disponible au début d'un combat avant toute génération supplémentaire.")]
    public int baseHarmonicCharge = 1;

    [Header("Référence runtime")]
    [Tooltip("Composant possédant cette instance pour faciliter les recherches à chaud.")]
    public MonoBehaviour owner;

    #endregion

    public Transform GetTransform()
    {
        return owner != null ? owner.transform : null;
    }
}

public enum CharacterType { SquadUnit, EnemyUnit }
public enum GameplayType { Rage, Fatigue, Concentration, Sacrifice }
public enum EnemyIntelligenceLevel { Beast, Normal, Intelligent }
