using UnityEngine;
using System;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Unity.Cinemachine;
using UnityEngine.EventSystems;
#if UNITY_EDITOR
using UnityEditor.Animations;
#endif

#region BattleState
public enum BattleState
{
    None,
    Initialization,
    BattleIntro,
    NewTurn,
    EndTurn,

    // SquadUnit Turn
    SquadUnit_MainMenu,
    SquadUnit_SkillsMenu,
    SquadUnit_ItemsMenu,
    SquadUnit_TargetSelectionAmongSquadOrEnemies_OnSquad,
    SquadUnit_TargetSelectionAmongSquadOrEnemies_OnEnemies,
    SquadUnit_TargetSelectionAmongSquadForSkill,
    SquadUnit_TargetSelectionAmongSquadForItem,
    SquadUnit_TargetSelectionAmongEnemiesForSkill,
    SquadUnit_TargetSelectionAmongEnemiesForItem,
    SquadUnit_TargetSelectionForBaseAttack,
    SquadUnit_PerformingMusicalMove,
    SquadUnit_PerformingBaseAttack,
    SquadUnit_Item_Prepare,
    SquadUnit_Item_Use,

    // EnemyUnit Turn
    EnemyUnit_Reflexion,
    EnemyUnit_PerformingMusicalMove,
    EnemyUnit_Item_Prepare,
    EnemyUnit_Item_Use,

    // Game Over
    VictoryScreen_Await,
    VictoryScreen_CanContinue,

    GameOverScreen_Await,
    GameOverScreen_CanContinue,
}
#endregion

/// <summary>
/// Représente l'issue d'un combat afin de déterminer la timeline post-combat à jouer.
/// </summary>
public enum BattleOutcome
{
    None,
    Victory,
    Defeat
}

// Le BattleManager est découpé en partial classes pour isoler les responsabilités
// (spawn, tours, etc.) et faciliter les futures évolutions sans surcharger un seul fichier.
public partial class NewBattleManager : MonoBehaviour
{
    public static NewBattleManager Instance { get; private set; }

    [Header("État du combat")]
    public BattleState currentBattleState;
    private int battleEventMenuLockCount;

    [Header("Apparition des SquadUnits")]
    public GameObject squadUnitRay;
    private List<Transform> playerSpawnPoints = new List<Transform>();
    [Tooltip("Assigné dans la scène pour éviter les recherches au runtime.")]
    [SerializeField] private Transform playerSpawnRoot;

    [Header("Apparition des ennemis")]
    public GameObject enemyUnitRay;
    private List<Transform> enemySpawnPoints = new List<Transform>();
    [Tooltip("Assigné dans la scène pour éviter les recherches via tag si possible.")]
    [SerializeField] private Transform enemySpawnRoot;
    public List<CharacterData> enemyTemplates = new List<CharacterData>();

    [Header("Listes des unités en combat en fonction de leur état")]
    public List<CharacterUnit> unitsInBattle = new(); // Toutes les unités du combat quelque soit leur état
    public List<CharacterUnit> activeCharacterUnits = new List<CharacterUnit>(); // Unités actives en combat (HP > 0)

    [Header("Début de combat")]
    [SerializeField] private GameObject firstStrikeEffect;

    [Tooltip("Durée maximale pendant laquelle l'introduction de combat bloque le démarrage des tours.")]
    [SerializeField] private float battleIntroLockDuration = 2f;

    [Tooltip("Motif de caméra joué pendant l'entrée en combat.")]
    [SerializeField] private CameraMotifSO battleEntranceCameraMotif;
    [Tooltip("Motif de caméra joué lorsque le menu principal de combat est affiché.")]
    [SerializeField] private CameraMotifSO battleMainMenuCameraMotif;

    /// <summary>
    /// Indique si la mise en scène d'introduction verrouille temporairement les menus.
    /// Ce drapeau est consulté par l'InputsManager pour ignorer toute interaction
    /// tant que les animations d'arrivée ne sont pas complètement terminées.
    /// </summary>
    private bool battleIntroMenusLocked = false;

    [Header("Pause du combat")]
    [Tooltip("Suspend la boucle de tours pendant les événements de combat (BattleEventTrigger).")]
    [SerializeField] private bool pauseTurnsDuringBattleEvents = true;
    private readonly Dictionary<int, string> battlePauseRequests = new();
    private int nextBattlePauseToken = 1;

    /// <summary>
    /// Expose l'état du verrou d'introduction afin que les autres systèmes puissent
    /// savoir s'il est pertinent d'accepter ou d'ignorer les entrées de menu.
    /// </summary>
    public bool AreMenusLockedByBattleIntro => battleIntroMenusLocked;

    /// <summary>
    /// Indique si le combat est actuellement en pause logique.
    /// </summary>
    public bool IsBattlePaused => battlePauseRequests.Count > 0;

    /// <summary>
    /// Indique si un événement de combat est en cours.
    /// </summary>
    public bool IsBattleEventActive => battleEventMenuLockCount > 0;

    /// <summary>
    /// Indique si la boucle de tours doit être suspendue.
    /// </summary>
    public bool IsBattleTurnLoopPaused => IsBattlePaused || (pauseTurnsDuringBattleEvents && IsBattleEventActive);

    /// <summary>
    /// Centralise les verrous bloquant les menus et les entrées de combat.
    /// </summary>
    public bool AreBattleMenusLocked => battleIntroMenusLocked || IsBattlePaused || IsBattleEventActive;

    /// <summary>
    /// Indique si un combat est actif (hors écrans de victoire/game over).
    /// </summary>
    public bool IsBattleActive =>
        currentBattleState != BattleState.None
        && currentBattleState != BattleState.VictoryScreen_Await
        && currentBattleState != BattleState.VictoryScreen_CanContinue
        && currentBattleState != BattleState.GameOverScreen_Await
        && currentBattleState != BattleState.GameOverScreen_CanContinue;

    public event Action<bool> BattlePauseStateChanged;

    // Paramètres du ralentissement appliqué lors de l'introduction.
    [Tooltip("Facteur de ralentissement au tout début du combat.")]
    public float equipSlowMotionScale = 1f;

    [Header("Fin de combat")]
    public GameObject victoryScreen;
    public GameObject gameOverScreen;
    public RenderTexture VictoryScreenImage;
    public RenderTexture GameOverScreenImage;

    [Header("Défaite")]
    [Tooltip("Si vrai, une defaite quitte directement le combat (respawn au checkpoint) sans ecran Game Over.")]
    public bool gameOverOnDefeat = false;

    [HideInInspector] public bool respawnAtCheckpointOnExit = false;
    public bool ShouldRespawnAtCheckpoint => respawnAtCheckpointOnExit;

    [Header("Séquences post-combat")]
    [Tooltip("Séquence jouée après une victoire.")]
    public List<TimelineSequenceEntry> victoryTimelineSequence = new();

    [Tooltip("Séquence jouée après une défaite.")]
    public List<TimelineSequenceEntry> defeatTimelineSequence = new();

    [HideInInspector] public TimelineBattleConfigSO activeTimelineBattleConfig;

    [HideInInspector] public BattleOutcome lastBattleOutcome = BattleOutcome.None; // Stocke l'issue du combat
    public event Action<BattleOutcome, TimelineBattleConfigSO> BattleEnded;
    private bool battleEndEventDispatched = false;

    [Header("Récompenses")]
    public List<ItemData> rewardItems = new();
    public int rewardXP = 0;

    [Header("Consultation des harmoniques")]
    [Tooltip("Pénalité de score appliquée à chaque consultation du menu d'harmoniques en combat.")]
    [SerializeField] private int harmonicMenuScorePenalty = 5;
    private int harmonicMenuConsultCount = 0;

    private float battleStartTime = 0f;
    private int currentTurnDamage = 0;
    private int maxTurnDamage = 0;
    private CharacterUnit mvpUnit;
    private Dictionary<CharacterUnit, int> totalDamageDealt = new();

    [Header("Timeline d'objets")]
    public TimelineAsset itemPreparingTimeline;
    [Tooltip("Motif joué pendant la sélection d'objet dans le menu.")]
    public CameraMotifSO itemPreparingCameraMotif;
    [Tooltip("Motif joué pendant la sélection de cible d'un objet.")]
    public CameraMotifSO itemTargetSelectionCameraMotif;
    private bool itemMenuTimelineActive = false;
    [Header("Ciblage de compétences")]
    [Tooltip("Motif joué pendant la sélection de cible d'un MusicalMove.")]
    public CameraMotifSO musicalMoveTargetSelectionCameraMotif;

    /// <summary>
    /// Empêche le lancement multiple de la séquence de victoire.
    /// Ce drapeau garantit que la capture, le switch caméra et l'affichage UI
    /// restent synchronisés même si <see cref="HandleEndOfBattle"/> est évalué
    /// plusieurs fois sur des frames consécutives.
    /// </summary>
    private bool victorySequenceInProgress = false;

    [Header("Références scène")]
    [Tooltip("Racine du rig BattleCamera pour éviter les recherches GameObject.Find.")]
    [SerializeField] private Transform battleCameraRig;

    /// <summary>Nom de la Cinemachine dédiée au menu principal et aux variations associées.</summary>
    private const string MainMenuCameraName = "CMV_MainMenu";
    /// <summary>Nom de la Cinemachine utilisée pour le menu des compétences.</summary>
    private const string SkillsMenuCameraName = "CMV_SkillsMenu";
    /// <summary>Nom explicite de la caméra de mise en scène pour l'écran de victoire.</summary>
    private const string VictoryCameraName = "CMV_Victory";
    /// <summary>Nom de la Cinemachine dédiée au menu des objets.</summary>
    private const string ItemsMenuCameraName = "CMV_ItemsMenu";
    /// <summary>Nom de la Cinemachine utilisée durant les phases de sélection de cible.</summary>
    private const string TargetSelectionCameraName = "CMV_OverShoulder_CasterLookTarget";
    /// <summary>Nom de la Cinemachine prioritaire lors du ciblage d'un objet.</summary>
    private const string ItemTargetSelectionCameraName = "CMV_OrbitAroundUnit";
    /// <summary>
    /// Style de transition privilégié pour les menus. Il est directement récupéré auprès du
    /// <see cref="BattleCameraManager"/> pour rester parfaitement aligné avec la configuration globale du
    /// <see cref="CinemachineBlendSwitcher"/> (smooth obligatoire).
    /// </summary>
    private CinemachineBlendDefinition.Styles MenuCameraBlendStyle =>
        BattleCameraManager.Instance ? BattleCameraManager.Instance.SmoothBlendStyle : CinemachineBlendSwitcher.ResolveSmoothBlendStyle();

    /// <summary>
    /// Durée par défaut des transitions de menu. Toutes les transitions du jeu étant désormais figées à
    /// 0,5 seconde, on utilise la propriété exposée par le gestionnaire caméra pour éviter toute divergence.
    /// </summary>
    private float MenuCameraBlendDuration =>
        BattleCameraManager.Instance ? BattleCameraManager.Instance.SmoothBlendDuration : CinemachineBlendSwitcher.GlobalSmoothBlendDurationSeconds;

    /// <summary>
    /// Durée appliquée aux caméras contextuelles (ciblage, actions). Identique à celle des menus afin de
    /// conserver une expérience fluide et prévisible pour le joueur.
    /// </summary>
    private float ContextCameraBlendDuration => MenuCameraBlendDuration;

    /// <summary>
    /// Style de transition privilégié pour les caméras contextuelles. Même valeur que pour les menus : un
    /// unique réglage smooth garantit la cohérence visuelle quel que soit le rôle de la caméra active.
    /// </summary>
    private CinemachineBlendDefinition.Styles ContextCameraBlendStyle => MenuCameraBlendStyle;
    /// <summary>Hash du state Animator "Item_Prepare" pour lancer rapidement l'animation correspondante.</summary>
    private static readonly int AnimatorStateItemPrepare = Animator.StringToHash("Item_Prepare");
    /// <summary>Hash du state Animator "Idle_Battle" afin de revenir proprement à la pose neutre.</summary>
    private static readonly int AnimatorStateIdleBattle = Animator.StringToHash("Idle_Battle");

    private CharacterUnit previousUnit; // Champ de classe, pas une variable locale
    [HideInInspector] public CharacterUnit currentCharacterUnit;
    private bool isTurnResolving = false;
    private bool interceptionSucceeded = false;
    // File d'attente de timelines à jouer entre deux tours
    /// <summary>
    /// File d'attente des timelines à jouer en fin de tour.
    /// Chaque entrée décrit l'asset à lancer et l'objet servant de référence pour les bindings.
    /// La caméra n'étant plus animée via timeline, aucun tag n'est désormais nécessaire.
    /// </summary>
    private readonly Queue<PendingTimeline> pendingTimelines = new();
    private bool pendingTimelineRoutineRunning;

    [Header("Debug")]
    [SerializeField] private bool debugPendingTimelines = false;

    /// <summary>
    /// Structure stockant les informations nécessaires pour jouer une timeline.
    /// Seul le lanceur est requis : la piste caméra est ignorée.
    /// </summary>
    private struct PendingTimeline
    {
        public TimelineAsset asset;
        public CharacterUnit unit;
        public GameObject casterBinding;

        public PendingTimeline(TimelineAsset asset, CharacterUnit unit, GameObject casterBinding)
        {
            // Référence vers l'asset de timeline à exécuter
            this.asset = asset;
            // Unité propriétaire de la timeline (et donc du PlayableDirector utilisé)
            this.unit = unit;
            // GameObject jouant la piste "Caster" de la timeline
            this.casterBinding = casterBinding;
        }
    }

    private struct IntroAnimationPlayback
    {
        public Animator animator;
        public int layer;

        public IntroAnimationPlayback(Animator animator, int layer)
        {
            this.animator = animator;
            this.layer = layer;
        }
    }

    private const string BattleIntroStateName = "BattleIntro";
    private const string BattleIntroIndexParameterName = "BattleIntroIndex";
    private static readonly int BattleIntroStateHash = Animator.StringToHash(BattleIntroStateName);
    private static readonly int BattleIntroIndexHash = Animator.StringToHash(BattleIntroIndexParameterName);

    private const float ATB_THRESHOLD = 100f;
    // Délai appliqué avant qu'un ennemi n'exécute réellement son attaque
    private const float ENEMY_MOVE_DELAY = 1f;

    [Header("Sprites des touches")]
    [SerializeField] private Sprite inputSprite1;
    [SerializeField] private Sprite inputSprite2;
    [SerializeField] private Sprite inputSprite3;
    [SerializeField] private Sprite inputSprite4;

    [Header("Gestion du curseur de cible")]
    public GameObject targetCursorPrefab;
    [Tooltip("Prefab affichant la fenêtre d'interception")] public GameObject interceptionSignalPrefab;
    [HideInInspector] public GameObject targetCursor;
    [HideInInspector] public List<GameObject> multiTargetCursors = new List<GameObject>();

    [Header("Affichage des coûts de MusicalMove")]
    [SerializeField] private Vector3 targetCostLabelOffset = new Vector3(0.6f, 1.1f, 0f);
    [SerializeField] private float targetCostLabelScale = 0.05f;
    [SerializeField] private float targetCostLabelFontSize = 2.4f;
    [SerializeField] private TMP_FontAsset targetCostLabelFont;
    [SerializeField] private Color targetCostLabelColor = Color.white;
    private TMP_Text targetCostLabel;
    private TMP_FontAsset defaultTargetCostLabelFont;

    [Header("Affichage des dégâts et bonus")]
    [SerializeField] private TMP_FontAsset damagePopupFont;
    [Min(0.1f)][SerializeField] private float damagePopupFontSize = 150f;
    [Min(0.001f)][SerializeField] private float damagePopupTextScale = 1f;
    [SerializeField] private Vector3 damagePopupOffset = new Vector3(0f, 2f, 0f);
    [SerializeField] private bool damagePopupOffsetUsesCameraAxes = false;
    [SerializeField] private bool damagePopupUsePooling = true;
    [SerializeField] private Color damagePopupDamageColor = new Color(1f, 0.35f, 0.35f, 1f);
    [SerializeField] private Color damagePopupHealColor = new Color(0.35f, 1f, 0.55f, 1f);
    [SerializeField] private Color damagePopupBuffColor = new Color(0.4f, 0.75f, 1f, 1f);
    [SerializeField] private Color damagePopupDebuffColor = new Color(1f, 0.6f, 0.2f, 1f);
    [SerializeField] private Color damagePopupStatusColor = new Color(1f, 0.85f, 0.45f, 1f);
    [SerializeField] private Color damagePopupInterceptionPositiveColor = new Color(0.3f, 1f, 0.4f, 1f);
    [SerializeField] private Color damagePopupInterceptionNegativeColor = new Color(1f, 0.25f, 0.25f, 1f);

    [Tooltip("Canvas monde piloté par la caméra de combat. L'indicateur y est instancié pour bénéficier des mêmes réglages de rendu.")]
    [SerializeField] private Transform battleCameraCanvasTransform;
    
    private List<CharacterUnit> filteredUnits = new();
    private DamagePopupManager damagePopupManager;
    // Liste temporaire réutilisée pour éviter des allocations lors du ciblage multiple
    private readonly List<CharacterUnit> multiTargetUnits = new();
    /// <summary>
    /// Mémorise la dernière unité suivie par la Cinemachine de ciblage afin
    /// d'éviter les rafraîchissements inutiles à chaque frame.
    /// </summary>
    private CharacterUnit lastTargetCursorCameraTarget;
    /// <summary>
    /// Indique si la Cinemachine dédiée au suivi du curseur est actuellement
    /// configurée. Cela permet de libérer proprement les overrides lorsque
    /// l'on quitte une phase de ciblage.
    /// </summary>
    private bool isTargetCursorCinemachineActive;
    /// <summary>
    /// Indique si le mode multi-cibles est actif pour la Cinemachine de ciblage.
    /// </summary>
    private bool isMultiTargetCursorCinemachineActive;
    /// <summary>
    /// Mémorise la dernière caméra utilisée pour le recadrage multi-cibles.
    /// </summary>
    private string lastMultiTargetCameraName;
    private int currentTargetIndex = 0;
    private float navigationCooldown = 0.3f;
    private float lastNavTime = 0f;
    private CharacterUnit _currentTargetCharacter;
    public CharacterUnit currentTargetCharacter
    {
        get => _currentTargetCharacter;
        set
        {
            _currentTargetCharacter = value;
            // On met à jour immédiatement le comportement caméra
            UpdateCameraBehaviour(currentBattleState);

            // Oriente toujours le lanceur dès qu'une cible valide est définie.
            if (currentCharacterUnit != null && _currentTargetCharacter != null)
            {
                OrientUnitTowardTarget(currentCharacterUnit, _currentTargetCharacter);
            }

            // A chaque mise à jour du ciblage, on relance systématiquement l'animation de
            // préparation pour souligner visuellement que l'unité est sous la menace d'une
            // attaque imminente. Même si la cible reste identique, relancer l'animation
            // permet de redonner du dynamisme à la scène et d'améliorer la lisibilité pour
            // le joueur, notamment lorsque plusieurs actions consécutives visent la même cible.
            if (_currentTargetCharacter != null)
            {
                // 🎯 Pendant les phases de sélection on relance toujours l'animation
                // de préparation pour souligner la menace immédiate. En revanche,
                // lorsque la séquence du MusicalMove est en cours et que la
                // mise en scène doit garder la priorité, on s'abstient de
                // relancer l'animation pour éviter un doublon.
                bool isSelectionState = IsTargetSelectionState(currentBattleState);
                bool shouldDelayDueToMove = RhythmQTEManager.Instance != null
                    && RhythmQTEManager.Instance.ShouldDelayTargetPreparationAnimation;

                if (isSelectionState || !shouldDelayDueToMove)
                {
                    _currentTargetCharacter.PlayPrepareToUndergoAnimation();
                }
            }
        }
    }
    //-------------------------------------------------------------------------------------

    // Caméra
    [Header("Caméra de combat")]
    // On mémorise l'objet caméra pour éviter de le rechercher à chaque frame
    [SerializeField] private GameObject battleCamera;
    private BattleState lastCameraEvaluatedState = BattleState.None;

    // Compétences et items disponibles pour l’unité qui joue
    // Garder en public
    [HideInInspector] public List<MusicalMoveSO> skillChoices = new List<MusicalMoveSO>();
    // Mouvement spécial actuel (affiché dans le 4e slot du SkillsMenu)
    [HideInInspector] public MusicalMoveSO specialMoveChoice;
    /// <summary>
    ///     Mouvement d'attaque basique affiché dans le premier slot du SkillsMenu.
    ///     Ce champ reste public (en hide) pour simplifier la consultation dans l'inspecteur
    ///     tout en garantissant un point d'accès unique pour l'UI et les inputs.
    /// </summary>
    [HideInInspector] public MusicalMoveSO basicAttackMoveChoice;
    [HideInInspector] public List<ItemData> itemChoices = new List<ItemData>();
    [HideInInspector] public MusicalMoveSO currentMove;
    [HideInInspector] public ItemData currentItem;
    [HideInInspector] public TargetType currentMoveTargetType;
    [HideInInspector] public TargetType currentItemTargetType;
    public int currentMenuIndex;
    // Index de la page actuellement affichée dans le SkillsMenu.
    // Chaque page contient autant de compétences que de slots disponibles.
    [HideInInspector] public int currentSkillPageIndex = 0;
    // Sélection nulle pour remplir les emplacements vides
    public MusicalMoveSO emptyMove;
    [Header("Attaque basique")]
    [Tooltip("Move utilisé lorsque l'unité ne possède pas d'attaque basique explicite dans sa liste personnelle.")]
    [SerializeField] private MusicalMoveSO defaultBasicAttackMove;
    [Header("Repli")]
    [Tooltip("Move déclenché lorsque le joueur utilise l'action Repli (LT).")]
    [SerializeField] private MusicalMoveSO repliMove;
    /// <summary>
    /// Indicateur runtime permettant de savoir si la compétence courante est l'attaque basique.
    /// Ce drapeau nous aide à appliquer les règles historiques (fin de tour immédiate, etc.).
    /// </summary>
    private bool currentMoveIsBasicAttack = false;

    // Menus personnalisés pour l’unité qui joue
    public GameObject currentMainMenuContainer;
    public List<Transform> currentMainMenuSlots;

    public GameObject currentSkillsMenuContainer;
    public List<Transform> currentSkillsMenuSlots;
    private readonly Dictionary<RectTransform, float> baseMenuSlotHeights = new();
    private readonly Dictionary<TextMeshProUGUI, float> baseMenuTextHeights = new();
    private const float MinMenuFontSize = 40f;
    private const float MenuSlotVerticalPadding = 30f;

    public GameObject currentItemsMenuContainer;
    public List<Transform> currentItemsMenuSlots;

    [Header("Effets sonores de navigation en combat")]
    [SerializeField, Tooltip("Clip joué lorsque le menu principal s'affiche ou lorsque l'on y retourne.")]
    private AudioClipSO mainMenuOpenClip;
    [SerializeField, Tooltip("Clip joué dès que le joueur ouvre le menu des compétences.")]
    private AudioClipSO skillsMenuOpenClip;
    [SerializeField, Tooltip("Clip joué lors de l'accès au menu des objets.")]
    private AudioClipSO itemsMenuOpenClip;
    [SerializeField, Tooltip("Clip déclenché lorsque l'on passe en mode sélection de cible (compétence ou objet).")]
    private AudioClipSO targetSelectionClip;
    [SerializeField, Tooltip("Clip joué à chaque changement de cible pendant la navigation.")]
    private AudioClipSO targetChangeClip;
    [SerializeField, Tooltip("Clip joué lorsqu'une nouvelle page de compétences est affichée.")]
    private AudioClipSO skillPageChangeClip;
    [SerializeField, Tooltip("Effet utilisé lorsque l'unité active ne possède pas de clip personnalisé pour signaler sa sélection.")]
    private AudioClipSO defaultCharacterSelectionClip;

    // -----------------------------------------------------------------------------------

    #region Awake/Start/Update()
    /// <summary>
    /// Initialise le singleton et persiste à travers les scènes.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 🗑️ Les expérimentations de tours lents ayant été supprimées, le BattleManager
        //     fonctionne désormais exclusivement avec la boucle classique.
        //     Nous profitons de l'Awake pour le rappeler explicitement aux futurs mainteneurs.
    }

    /// <summary>
    /// Instancie le curseur de cible au lancement de la scène de combat.
    /// </summary>
    private void Start()
    {
        EnsureTargetCursor();
        // Recherche initiale de la caméra de combat pour éviter de la chercher à chaque frame
        EnsureBattleCamera();
    }

    /// <summary>
    ///     Calcule la position d'apparition finale d'un personnage en fonction de son type.
    ///     Les unités aériennes reçoivent une translation verticale additionnelle afin
    ///     de rester en sustentation au-dessus du sol dès l'instanciation.
    /// </summary>
    /// <param name="data">Données du personnage à instancier.</param>
    /// <param name="spawnPoint">Point d'apparition défini dans la scène.</param>
    /// <returns>Position monde ajustée respectant la hauteur requise.</returns>
    private Vector3 ComputeSpawnPosition(CharacterData data, Transform spawnPoint)
    {
        // 🚨 Sécurise l'appel : si le point de spawn n'est pas disponible, on renvoie
        //     une position nulle afin d'éviter toute NullReference lors de l'instanciation.
        if (spawnPoint == null)
            return Vector3.zero;

        // Base : on part de la position exacte du point défini par le level design.
        //         Cette position correspond explicitement au pivot de PlayerPosition_X
        //         ou EnemyPosition_X. Il est indispensable de conserver cette
        //         référence plutôt que de se baser sur le centre géométrique
        //         d'un éventuel volume (MeshRenderer, Collider...) afin de
        //         garantir que les artistes puissent ajuster librement leurs
        //         pivots dans l'éditeur.
        var finalPosition = spawnPoint.position;

        // ✈️ Si l'unité est aérienne, on applique directement la distance prévue par le Game Design.
        if (data != null && data.isAirUnit)
        {
            // Clamp à zéro pour éviter les valeurs négatives involontaires tout en permettant
            // aux designers d'ajuster librement la hauteur des ennemis volants.
            var positiveOffset = Mathf.Max(0f, data.distanceFromGround);
            // 🧭 Utilise explicitement l'axe "Up" du point d'apparition afin de rester
            //     parfaitement aligné sur le pivot défini dans la scène. En pratique,
            //     cela signifie que l'offset sera toujours appliqué relativement au
            //     Transform PlayerPosition_X / EnemyPosition_X, même si ce dernier est
            //     incliné ou animé par rapport au monde. On évite ainsi les glissements
            //     visuels observés lorsque l'on additionnait directement sur l'axe Y
            //     global (qui correspondait au centre du monde et non au pivot local).
            finalPosition += spawnPoint.up * positiveOffset;
        }

        return finalPosition;
    }

    /// <summary>
    /// Gère les sélections de cible pendant le combat.
    /// </summary>
    private void Update()
    {
        ApplyDamagePopupDisplaySettings();
        if (IsBattlePaused)
            return;

        HandleTargetCursor();
        HandleTargetNavigation();
        BattleEventTrigger.EvaluateAll(this);
    }

    public int RequestBattlePause(string reason)
    {
        if (string.IsNullOrEmpty(reason))
            reason = "Unknown";

        int token = nextBattlePauseToken++;
        battlePauseRequests[token] = reason;

        if (battlePauseRequests.Count == 1)
            BattlePauseStateChanged?.Invoke(true);

        return token;
    }

    public void ReleaseBattlePause(int token)
    {
        if (!battlePauseRequests.Remove(token))
            return;

        if (battlePauseRequests.Count == 0)
        {
            BattlePauseStateChanged?.Invoke(false);
            TryStartPendingTimelines();
        }
    }

    private void ResetBattlePauseState()
    {
        if (battlePauseRequests.Count > 0)
        {
            battlePauseRequests.Clear();
            BattlePauseStateChanged?.Invoke(false);
        }

        nextBattlePauseToken = 1;
    }

    private IEnumerator WaitWhileBattlePaused()
    {
        while (IsBattleTurnLoopPaused)
            yield return null;
    }
    #endregion

    #region Mise en scène de la scène de bataille
    private static bool HasAnimatorParameter(Animator animator, int parameterHash)
    {
        if (animator == null)
            return false;

        foreach (var parameter in animator.parameters)
        {
            if (parameter.nameHash == parameterHash)
                return true;
        }

        return false;
    }

    private static bool TryResolveBattleIntroLayer(Animator animator, out int layer)
    {
        layer = 0;
        if (animator == null)
            return false;

#if UNITY_EDITOR
        if (TryGetBattleIntroState(animator, out _, out int editorLayer))
        {
            layer = editorLayer;
            return true;
        }
#endif

        int stateHash = BattleIntroStateHash;
        for (int i = 0; i < animator.layerCount; i++)
        {
            if (animator.HasState(i, stateHash))
            {
                layer = i;
                return true;
            }
        }

        return false;
    }

    private static int ResolveBattleIntroMotionCount(Animator animator)
    {
        if (animator == null)
            return 0;

#if UNITY_EDITOR
        if (TryGetBattleIntroBlendTree(animator, out BlendTree blendTree))
        {
            int count = 0;
            foreach (var child in blendTree.children)
            {
                if (child.motion != null)
                    count++;
            }

            return count;
        }
#endif

        RuntimeAnimatorController controller = animator.runtimeAnimatorController;
        if (controller is AnimatorOverrideController overrideController)
            controller = overrideController.runtimeAnimatorController;

        if (controller == null)
            return 0;

        int fallbackCount = 0;
        foreach (var clip in controller.animationClips)
        {
            if (clip == null)
                continue;

            if (clip.name.IndexOf(BattleIntroStateName, StringComparison.OrdinalIgnoreCase) >= 0)
                fallbackCount++;
        }

        return fallbackCount;
    }

#if UNITY_EDITOR
    private static AnimatorController ResolveAnimatorController(Animator animator)
    {
        if (animator == null)
            return null;

        RuntimeAnimatorController controller = animator.runtimeAnimatorController;
        if (controller is AnimatorOverrideController overrideController)
            controller = overrideController.runtimeAnimatorController;

        return controller as AnimatorController;
    }

    private static bool TryGetBattleIntroState(Animator animator, out AnimatorState state, out int layerIndex)
    {
        state = null;
        layerIndex = -1;

        AnimatorController controller = ResolveAnimatorController(animator);
        if (controller == null)
            return false;

        for (int i = 0; i < controller.layers.Length; i++)
        {
            if (TryFindState(controller.layers[i].stateMachine, BattleIntroStateName, out state))
            {
                layerIndex = i;
                return true;
            }
        }

        state = null;
        return false;
    }

    private static bool TryGetBattleIntroBlendTree(Animator animator, out BlendTree blendTree)
    {
        blendTree = null;
        if (!TryGetBattleIntroState(animator, out AnimatorState state, out _))
            return false;

        blendTree = state.motion as BlendTree;
        return blendTree != null;
    }

    private static bool TryFindState(AnimatorStateMachine stateMachine, string stateName, out AnimatorState state)
    {
        state = null;
        if (stateMachine == null)
            return false;

        foreach (var childState in stateMachine.states)
        {
            if (childState.state != null &&
                string.Equals(childState.state.name, stateName, StringComparison.OrdinalIgnoreCase))
            {
                state = childState.state;
                return true;
            }
        }

        foreach (var childMachine in stateMachine.stateMachines)
        {
            if (TryFindState(childMachine.stateMachine, stateName, out state))
                return true;
        }

        return false;
    }
#endif

    /// <summary>
    /// Lance les animations d'introduction pour chaque unité en appliquant
    /// un effet de ralenti global. Toutes les animations démarrent en parallèle
    /// et la coroutine attend leur terminaison avant de poursuivre.
    /// </summary>
    private IEnumerator PlayIntroAnimationsWithSlowTime()
    {
        // Sauvegarde des paramètres temporels actuels pour les restaurer ensuite
        float initialTimeScale = Time.timeScale;
        float initialFixedDelta = Time.fixedDeltaTime;

        // 🕵️‍♂️ Identifie les unités disposant réellement d'un BlendTree d'introduction
        // pour éviter d'appliquer un délai inutile lorsqu'aucune intro n'est configurée.
        List<IntroAnimationPlayback> introCandidates = new();
        foreach (var unit in unitsInBattle)
        {
            if (unit == null)
                continue;

            Animator introAnimator = unit.GetCasterAnimator(forceRefresh: true);
            if (introAnimator == null)
                continue;

            if (TryResolveBattleIntroLayer(introAnimator, out int layer))
                introCandidates.Add(new IntroAnimationPlayback(introAnimator, layer));
        }

        // 🚀 Si aucune intro n'est définie, on quitte immédiatement sans ralentir le jeu.
        if (introCandidates.Count == 0)
            yield break;

        // Application du facteur de ralenti défini dans l'inspecteur uniquement
        // lorsque la mise en scène doit effectivement se jouer.
        Time.timeScale = equipSlowMotionScale;
        Time.fixedDeltaTime = initialFixedDelta * Time.timeScale;

        // Déclenche les animations d'introduction sur chaque unité concernée
        foreach (var intro in introCandidates)
        {
            if (HasAnimatorParameter(intro.animator, BattleIntroIndexHash))
            {
                int motionCount = ResolveBattleIntroMotionCount(intro.animator);
                float parameterValue;
                if (motionCount > 1)
                {
                    int randomIndex = UnityEngine.Random.Range(0, motionCount);
                    parameterValue = randomIndex / Mathf.Max(1f, motionCount - 1f);
                }
                else if (motionCount == 1)
                {
                    parameterValue = 0f;
                }
                else
                {
                    parameterValue = UnityEngine.Random.value;
                }
                intro.animator.SetFloat(BattleIntroIndexHash, parameterValue);
            }

            intro.animator.CrossFade(BattleIntroStateName, 0.05f, intro.layer, 0f);
        }

        // ⏱️ Calcule la durée d'attente maximale en temps réel. L'utilisation de l'unscaled delta
        //     permet de respecter la durée configurée même si le timeScale a été réduit.
        float waitDuration = Mathf.Max(0f, battleIntroLockDuration);
        float elapsedIntroTime = 0f;

        // ⛔ On attend exactement la durée configurée, même si les animations se terminent plus tôt.
        //    Ainsi, les mises en scène longues n'empêchent plus le combat de démarrer : passé ce délai
        //    les tours commencent, tandis que les introductions continuent leur lecture en arrière-plan.
        while (elapsedIntroTime < waitDuration)
        {
            yield return null;
            elapsedIntroTime += Time.unscaledDeltaTime;
        }

        // 🧷 Toutefois, si une animation poursuit encore sa lecture une fois le délai minimal écoulé,
        //    on maintient le verrouillage des menus jusqu'à ce que toutes les mises en scène soient
        //    effectivement terminées. Sans cette sécurité supplémentaire, les joueurs pouvaient
        //    sélectionner des compétences ou des objets pendant les dernières secondes des animations.
        while (IsAnyIntroAnimationStillPlaying(introCandidates))
        {
            yield return null;
        }

        // Restaure les valeurs temporelles initiales une fois le délai écoulé pour que le gameplay
        // retrouve immédiatement sa vitesse normale lorsque le premier tour débute.
        Time.timeScale = initialTimeScale;
        Time.fixedDeltaTime = initialFixedDelta;

        // 📌 Aucun yield supplémentaire n'est nécessaire : la boucle de tours peut démarrer dès
        //     maintenant, les Animator poursuivent leur animation sans bloquer le gameplay.

        // La gestion du changement de caméra est réalisée en dehors de cette
        // coroutine afin de laisser la main à l'appelant sur la transition.
    }

    /// <summary>
    /// Détermine si au moins une des animations d'introduction suit encore sa lecture.
    /// </summary>
    /// <param name="intros">Liste des animators surveillés pendant l'introduction.</param>
    /// <returns><c>true</c> tant qu'une intro est en cours, <c>false</c> lorsque toutes sont stoppées.</returns>
    private static bool IsAnyIntroAnimationStillPlaying(List<IntroAnimationPlayback> intros)
    {
        if (intros == null || intros.Count == 0)
            return false;

        // Parcours l'ensemble des animators actifs pour vérifier l'état du BlendTree d'intro.
        foreach (var intro in intros)
        {
            if (intro.animator == null)
                continue;

            int layer = Mathf.Clamp(intro.layer, 0, Mathf.Max(0, intro.animator.layerCount - 1));
            var stateInfo = intro.animator.GetCurrentAnimatorStateInfo(layer);
            if (stateInfo.shortNameHash == BattleIntroStateHash && stateInfo.normalizedTime < 1f)
                return true;

            if (intro.animator.IsInTransition(layer))
            {
                var nextState = intro.animator.GetNextAnimatorStateInfo(layer);
                if (nextState.shortNameHash == BattleIntroStateHash && nextState.normalizedTime < 1f)
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gère la caméra d'introduction, attend la fin des
    /// déplacements puis lance les animations d'introduction des unités.
    /// </summary>
    private IEnumerator PlayIntroCameraSequence()
    {
        // 🎬 Lance les animations d'introduction en mode ralenti et attend leur terminaison.
        yield return PlayIntroAnimationsWithSlowTime();

        // 📷 Dès que les mises en scène sont terminées, on bascule vers la caméra dédiée au menu principal.
        const float introToMenuBlendDuration = 0.5f;
        BattleCameraManager.Instance?.SwitchToCamera(MainMenuCameraName, introToMenuBlendDuration);
    }
    #endregion

    #region Démarrage du combat
    public IEnumerator StartBattle()
    {
        Debug.Log("[BattleTurnManager] Démarrage du combat");

        // 🎮 Au lancement du combat, on limite les entrées au seul bouton de validation.
        //     Les menus resteront donc parfaitement figés pendant les animations
        //     d'introduction des unités, conformément à la demande de game design.
        InputsManager.Instance?.RestrictInputsToBattleConfirm();

        // 🛡️ Assure que les filtres d'écran sont totalement invisibles au lancement du combat.
        //    Sans cette étape, un résidu de fondu noir ou blanc pourrait persister d'une timeline précédente.
        var fader = FadeChildrenOpacity.Instance;
        if (fader != null)
        {
            // Indice 0 = BlackScreen, indice 1 = WhiteScreen
            // La durée est fixée à 0 pour appliquer la transparence immédiatement.
            fader.EnsureTransparency(0, 0f);
            fader.EnsureTransparency(1, 0f);
        }

        GameManager.Instance?.ResetEnemiesDefeatedCount();

        battleStartTime = Time.time;
        currentTurnDamage = 0;
        maxTurnDamage = 0;
        mvpUnit = null;
        totalDamageDealt.Clear();

        //0 Liste "unitsInBattle" construite avec SpawnAll

        //1 Filtrer pour ne garder que les unités dont les HP sont > 0
        activeCharacterUnits = ReturnActiveUnits();

        // Réinitialise les compteurs d'utilisation des moves et items pour le combat
        foreach (var unit in activeCharacterUnits)
            unit.ResetBattleMoveUsage();
        InventoryManager.Instance?.ResetBattleItemUsage();

        foreach (var unit in activeCharacterUnits.Where(u => u.IsPlayerControlled))
        {
            if (!totalDamageDealt.ContainsKey(unit))
                totalDamageDealt[unit] = 0;
        }

        //2 Initialise l’UI de la timeline de combat via le gestionnaire dédié
        BattleTimelineUIManager.Instance?.Initialize(unitsInBattle);
        // Cache l'UI de timeline jusqu'au premier tour du joueur via le gestionnaire centralisé
        BattleTimelineUIManager.Instance?.SetVisible(false);

        //3 Affecter currentTarget au premier ennemi de la liste
        SetDefaultCurrentTarget();

        // S'assure que le curseur de cible existe pour ce nouveau combat
        EnsureTargetCursor();

        //4 Réinitialise les ATB
        ResetAllATB();

        // ⚡ Identifie au plus tôt l'unité qui ouvrira le bal afin d'orienter le menu sur le bon personnage.
        CharacterUnit upcomingFirstPlayer = ReturnFirstStrikeCharacter();
        PrimeMainMenuCameraForFirstUnit(upcomingFirstPlayer);

        // 🎬 Active le motif d'entrée en combat en se basant sur la SquadUnit_0.
        BattleCameraManager cameraManager = BattleCameraManager.Instance;
        bool entranceMotifActive = false;
        if (cameraManager != null && battleEntranceCameraMotif != null)
        {
            CharacterUnit entranceReferenceUnit = ResolveBattleEntranceReferenceUnit();
            if (entranceReferenceUnit != null)
            {
                cameraManager.ConfigureActionTargets(entranceReferenceUnit, null);
            }
            else
            {
                Debug.LogWarning("[BattleTurnManager] SquadUnit_0 introuvable pour la caméra d'entrée en combat.");
            }

            cameraManager.SetCameraMotif(battleEntranceCameraMotif);
            entranceMotifActive = true;
        }

        //6 Intro Camera
        // Lance la séquence d'introduction des caméras avant de débuter les tours.
        // On verrouille explicitement les menus pendant toute la durée de la cinématique
        // afin d'éviter que le joueur ne sélectionne une action avant la fin des animations.
        battleIntroMenusLocked = true;
        yield return PlayIntroCameraSequence();
        // Les introductions sont terminées : les menus peuvent à présent accepter les entrées.
        battleIntroMenusLocked = false;

        if (entranceMotifActive && cameraManager != null)
        {
            cameraManager.ClearCameraMotif();
            if (upcomingFirstPlayer != null)
                cameraManager.ConfigureActionTargets(upcomingFirstPlayer, null);
        }

        // ✅ Les actions de combat redeviennent disponibles une fois la cinématique bouclée.
        //    Cette remise à zéro intervient immédiatement après le déverrouillage logique des menus.
        InputsManager.Instance?.RestoreBattleInputsAfterIntro();

        //7 Démarre la boucle de tours
        //    Depuis la suppression du mode lent, nous invoquons systématiquement la boucle classique.
        StartCoroutine(TurnLoop());

        //// Change l’état du jeu
        //GameManager.Instance.ChangeGameState(GameState.StartBattle);

        yield break;
    }

    //1 Filtrer pour ne garder que les unités dont les HP sont > 0
    private List<CharacterUnit> ReturnActiveUnits()
    {
        List<CharacterUnit> activeCharacterUnits = unitsInBattle.Where(c => c.currentHP > 0).ToList();
        return activeCharacterUnits;
    }

    //3 Affecter currentTarget au premier ennemi de la liste
    private void SetDefaultCurrentTarget()
    {
        currentTargetCharacter = GetFirstActiveUnit(CharacterType.EnemyUnit);

        if (currentTargetCharacter == null)
        {
            Debug.LogWarning("[BattleTurnManager] Aucun ennemi actif trouvé pour currentTargetCharacter.");
        }
    }

    //4 Réinitialise les ATB
    private void ResetAllATB()
    {
        foreach (var unit in activeCharacterUnits)
        {
            unit.currentATB = 0f;
        }
    }

    private void EvaluateInactiveBattleEventTriggers()
    {
        // Méthode conservée pour compatibilité ; l'évaluation est centralisée dans Update.
        BattleEventTrigger.EvaluateAll(this);
    }

    //5 Détermine quel joueur joue en premier
    public CharacterUnit ReturnFirstStrikeCharacter()
    {
        CharacterUnit firstPlayer = activeCharacterUnits
            .Where(u => u.IsPlayerControlled)
            .OrderByDescending(u => u.currentInitiative)
            .FirstOrDefault();

        return firstPlayer;
    }

    /// <summary>
    /// Pré-positionne la caméra de menu sur l'unité qui agira en premier.
    /// </summary>
    /// <param name="candidate">Unité pressentie pour jouer en ouverture.</param>
    private void PrimeMainMenuCameraForFirstUnit(CharacterUnit candidate)
    {
        if (candidate == null)
        {
            Debug.LogWarning("[BattleTurnManager] Aucun combattant joueur n'est disponible pour préparer la caméra du menu.");
            return;
        }

        var manager = BattleCameraManager.Instance;
        if (manager == null)
            return;

        manager.SetTurnOwner(candidate);
        manager.SetCurrentTarget(null);
        manager.SwitchToCamera(MainMenuCameraName, MenuCameraBlendDuration, MenuCameraBlendStyle);
    }

    private CharacterUnit ResolveBattleEntranceReferenceUnit()
    {
        CharacterUnit squadUnitZero = unitsInBattle.FirstOrDefault(unit =>
            unit != null && string.Equals(unit.gameObject.name, "SquadUnit_0", StringComparison.Ordinal));

        if (squadUnitZero != null)
            return squadUnitZero;

        return unitsInBattle.FirstOrDefault(unit => unit != null && unit.Data != null && unit.IsPlayerControlled);
    }

    //7 Démarre la boucle de tours
    private IEnumerator TurnLoop()
    {
        while (true)
        {
            yield return WaitWhileBattlePaused();

            if (unitsInBattle.All(u => u.currentHP <= 0))
            {
                Debug.LogWarning("[BattleTurnManager] Tous les combattants sont hors combat.");
                yield break;
            }

            // Avant de démarrer le prochain tour, on joue toutes les timelines en attente
            if (pendingTimelines.Count > 0)
                yield return PlayPendingTimelinesIfNeeded();

            yield return WaitWhileBattlePaused();
            yield return ExecuteTurn(CalculateNextUnit());
            // Utilisation du temps non affecté par le timeScale pour ne pas bloquer
            // la boucle si le jeu est mis en pause (fin de combat par exemple)
            yield return new WaitForSecondsRealtime(0.2f);
        }
    }

    /// <summary>
    /// Ajoute une timeline à jouer avant le prochain tour.
    /// </summary>
    /// <param name="asset">Timeline à exécuter.</param>
    /// <param name="casterUnit">Unité propriétaire du PlayableDirector utilisé.</param>
    /// <param name="casterBinding">Objet de référence pour les bindings (Animator par exemple).</param>
    /// La caméra n'est plus liée à la timeline : Munin gère le cadrage automatiquement.
    public void QueueConditionalTimeline(TimelineAsset asset, CharacterUnit casterUnit, GameObject casterBinding = null)
    {
        if (asset == null || casterUnit == null)
            return;

        // Détermine automatiquement l'objet d'animation si aucun binding spécifique n'est fourni.
        // Sélection automatique de l'Animator enfant du lanceur pour respecter la règle de binding.
        GameObject binding = casterBinding ?? casterUnit.GetCasterBindingTarget();

        // Stocke la timeline à jouer et le lanceur associé
        pendingTimelines.Enqueue(new PendingTimeline(asset, casterUnit, binding));
        if (debugPendingTimelines)
            Debug.Log($"[NewBattleManager] queued timeline={asset.name} unit={casterUnit.name}");

        // Lance immédiatement la lecture si possible pour éviter d'attendre
        // un retour en haut de boucle (cas des déclenchements en plein tour).
        TryStartPendingTimelines();
    }

    /// <summary>
    /// Joue séquentiellement toutes les timelines en attente.
    /// </summary>
    private IEnumerator PlayPendingTimelines()
    {
        pendingTimelineRoutineRunning = true;
        while (pendingTimelines.Count > 0)
        {
            yield return WaitWhileBattlePaused();

            var data = pendingTimelines.Dequeue();
            BattleTransitionManager.Instance?.HideBattleUI();

            bool timelinePlayed = false;

            if (BattleTimelineManager.Instance != null && data.unit != null)
            {
                if (debugPendingTimelines)
                    Debug.Log($"[NewBattleManager] play timeline via BattleTimelineManager asset={data.asset?.name} unit={data.unit?.name}");
                BattleTimelineManager.Instance.PlayCasterTimeline(data.asset, data.unit, data.casterBinding);
                timelinePlayed = true;

                // Attente de la fin de la timeline avant de poursuivre
                while (BattleTimelineManager.Instance.IsCasterTimelinePlaying(data.unit))
                    yield return null;
            }

            // Fallback pour les situations de test où le BattleTimelineManager n'est pas présent
            if (!timelinePlayed && data.unit != null)
            {
                if (debugPendingTimelines)
                    Debug.Log($"[NewBattleManager] play timeline via fallback asset={data.asset?.name} unit={data.unit?.name}");
                data.unit.PlayBattleTimeline(data.asset, data.casterBinding);
                while (data.unit.IsBattleTimelinePlaying)
                    yield return null;
            }

            BattleTransitionManager.Instance?.ShowBattleUIIfNeeded();
            // Petite pause pour éviter les enchaînements brusques
            yield return new WaitForSecondsRealtime(0.1f);
        }
        pendingTimelineRoutineRunning = false;
    }

    private IEnumerator PlayPendingTimelinesIfNeeded()
    {
        if (pendingTimelineRoutineRunning || pendingTimelines.Count == 0)
            yield break;

        yield return PlayPendingTimelines();
    }

    public void TryStartPendingTimelines()
    {
        if (pendingTimelineRoutineRunning || pendingTimelines.Count == 0 || IsBattleTurnLoopPaused)
            return;

        StartCoroutine(PlayPendingTimelines());
    }
    #endregion


    #region Gestion de l'orientation des unités
    public void OrientAllUnitsTowardCenter(CharacterUnit activeUnit)
    {
        foreach (var unit in activeCharacterUnits)
        {
            if (unit == null || unit == activeUnit)
                continue;

            // Calcul de la direction vers le centre (0,0,0)
            Vector3 dir = (Vector3.zero - unit.transform.position).normalized;
            if (dir == Vector3.zero)
                continue;

            // Angle entre la direction actuelle de l’unité (forward) et la nouvelle direction
            float angle = Vector3.Angle(unit.transform.forward, dir);
            if (angle > 90f)
            {
                // Si l’angle est > 90°, on déclenche le trigger "isTurning" sur l’Animator enfant
                Animator anim = unit.GetCasterAnimator();
                if (anim != null)
                {
                    anim.SetTrigger("isTurning");
                }
            }

            // On oriente instantanément l’unité vers la nouvelle direction (seulement sur l’axe Y)
            unit.transform.rotation = Quaternion.Euler(0, Quaternion.LookRotation(dir).eulerAngles.y, 0);
        }
    }

    public void OrientAllUnitsTowardEnemyGroupSmooth(float rotationSpeed = 360f)
    {
        foreach (var unit in activeCharacterUnits)
        {
            if (unit == null || unit.currentHP <= 0)
                continue;

            bool isPlayer = unit.IsPlayerControlled;

            // Trouve toutes les unités ennemies vivantes
            var enemies = unitsInBattle
                .Where(u => u != null && u.currentHP > 0 && u.IsPlayerControlled != isPlayer)
                .ToList();

            if (enemies.Count == 0)
                continue;

            // Calcul du barycentre des ennemis
            Vector3 averagePosition = Vector3.zero;
            foreach (var enemy in enemies)
                averagePosition += enemy.transform.position;
            averagePosition /= enemies.Count;

            // Direction vers le barycentre
            Vector3 direction = averagePosition - unit.transform.position;
            direction.y = 0f; // On ignore la hauteur pour une rotation horizontale.
            if (direction == Vector3.zero)
                continue;

            // Calcul de l’angle entre l’orientation actuelle (horizontal) et la cible
            Vector3 forward = unit.transform.forward;
            forward.y = 0f;
            float angle = Vector3.Angle(forward, direction);
            if (angle > 90f)
            {
                // Si > 90°, déclenche "isTurning" sur l’Animator enfant
                Animator anim = unit.GetCasterAnimator();
                if (anim != null)
                {
                    anim.SetTrigger("isTurning");
                }
            }

            // Normalise le vecteur horizontal obtenu.
            direction = direction.normalized;
            // Calcule une rotation ne contenant que l'angle autour de l'axe Y.
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            targetRotation = Quaternion.Euler(0f, targetRotation.eulerAngles.y, 0f);
            // Lance la rotation en douceur vers la rotation filtrée.
            StartCoroutine(RotateUnitSmoothly(unit, targetRotation, rotationSpeed));
        }
    }

    public void OrientTransformTowardEnemyGroupSmoothXY(Transform targetTransform, float rotationSpeed = 360f)
    {
        if (activeCharacterUnits == null || activeCharacterUnits.Count == 0)
            return;

        // Trouve tous les ennemis vivants (ou l'inverse si la cible est un ennemi)
        var enemies = activeCharacterUnits.Where(u => u != null && !u.IsPlayerControlled).ToList();

        if (enemies.Count == 0)
            return;

        // Calcul du barycentre du groupe d'ennemis
        Vector3 averagePosition = Vector3.zero;
        foreach (var enemy in enemies)
            averagePosition += enemy.transform.position;
        averagePosition /= enemies.Count;

        // Direction vers le barycentre
        Vector3 direction = (averagePosition - targetTransform.position).normalized;
        if (direction == Vector3.zero)
            return;

        // Calcul de la rotation visée : LookRotation sans roll
        Quaternion targetRotation = Quaternion.LookRotation(direction);

        // Filtrer pour ne garder que la rotation Yaw (Y) et Pitch (X)
        Vector3 euler = targetRotation.eulerAngles;
        Quaternion filteredRotation = Quaternion.Euler(euler.x, euler.y, 0f);

        // Lancer la coroutine pour tourner en douceur
        StartCoroutine(RotateTransformSmoothlyXY(targetTransform, filteredRotation, rotationSpeed));
    }

    public void OrientTransformTowardUnitSmoothXY(Transform targetTransform, CharacterUnit unit, float rotationSpeed = 360f)
    {
        if (targetTransform == null || unit == null)
            return;

        Vector3 direction = (unit.transform.position - targetTransform.position).normalized;
        if (direction == Vector3.zero)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction);
        Vector3 euler = targetRotation.eulerAngles;
        Quaternion filteredRotation = Quaternion.Euler(euler.x, euler.y, 0f);

        StartCoroutine(RotateTransformSmoothlyXY(targetTransform, filteredRotation, rotationSpeed));
    }

    public void OrientUnitTowardTarget(CharacterUnit unit, CharacterUnit target, float rotationSpeed = 360f)
    {
        if (unit == null || target == null || unit.currentHP <= 0 || target.currentHP <= 0)
            return;

        // Calcule la direction vers la cible en ignorant la hauteur pour éviter toute inclinaison.
        Vector3 direction = target.transform.position - unit.transform.position;
        direction.y = 0f; // Rotation uniquement sur Y.
        if (direction == Vector3.zero)
            return;
        direction = direction.normalized;

        // Génère une rotation horizontale filtrée sur l'axe Y.
        Quaternion targetRotation = Quaternion.LookRotation(direction);
        targetRotation = Quaternion.Euler(0f, targetRotation.eulerAngles.y, 0f);
        StartCoroutine(RotateUnitSmoothly(unit, targetRotation, rotationSpeed));
    }

    public void OrientUnitTowardClosestOpponent(CharacterUnit unit, float rotationSpeed = 360f)
    {
        if (unit == null || unit.Data == null || unit.currentHP <= 0)
            return;

        CharacterUnit targetUnit = null;

        if (unit == currentCharacterUnit && currentTargetCharacter != null && currentTargetCharacter.currentHP > 0
            && currentTargetCharacter.Data != null
            && currentTargetCharacter.IsPlayerControlled != unit.IsPlayerControlled)
        {
            targetUnit = currentTargetCharacter;
        }
        else
        {
            var enemies = unitsInBattle
                .Where(u => u != null && u.currentHP > 0 && u.IsPlayerControlled != unit.IsPlayerControlled)
                .OrderBy(u => Vector3.Distance(unit.transform.position, u.transform.position));

            targetUnit = enemies.FirstOrDefault();
        }

        if (targetUnit == null)
            return;

        Vector3 direction = targetUnit.transform.position - unit.transform.position;
        if (direction == Vector3.zero)
            return;

        // Supprime la composante verticale pour garantir une rotation horizontale.
        direction.y = 0f;
        direction = direction.normalized;

        Quaternion targetRotation = Quaternion.LookRotation(direction);
        targetRotation = Quaternion.Euler(0f, targetRotation.eulerAngles.y, 0f);
        StartCoroutine(RotateUnitSmoothly(unit, targetRotation, rotationSpeed));
    }

    public void OrientAllUnitsTowardClosestOpponent(float rotationSpeed = 360f)
    {
        // Angle minimum exprimé en pourcentage de 180° (écart maximal utile entre deux orientations).
        // 15 % correspond ici à ~27° : en dessous de ce seuil, on considère que la rotation est
        // imperceptible et qu'il vaut mieux éviter de lancer l'animation « Turn_90 » pour ne pas
        // perturber la lisibilité.
        const float minimalAngleRatio = 0.15f;

        foreach (var unit in activeCharacterUnits)
        {
            if (unit == null || unit.Data == null || unit.currentHP <= 0)
                continue;

            // Recherche de l'adversaire le plus proche en réutilisant la logique de ciblage.
            CharacterUnit targetUnit = null;

            if (unit == currentCharacterUnit && currentTargetCharacter != null && currentTargetCharacter.currentHP > 0
                && currentTargetCharacter.Data != null
                && currentTargetCharacter.IsPlayerControlled != unit.IsPlayerControlled)
            {
                targetUnit = currentTargetCharacter;
            }
            else
            {
                targetUnit = unitsInBattle
                    .Where(u => u != null && u.currentHP > 0 && u.IsPlayerControlled != unit.IsPlayerControlled)
                    .OrderBy(u => Vector3.Distance(unit.transform.position, u.transform.position))
                    .FirstOrDefault();
            }

            if (targetUnit == null)
                continue;

            // Calcul de l'angle entre l'orientation actuelle (filtrée sur Y) et la direction vers l'adversaire.
            Vector3 direction = targetUnit.transform.position - unit.transform.position;
            direction.y = 0f;

            if (direction == Vector3.zero)
                continue;

            direction = direction.normalized;
            Quaternion currentRotation = Quaternion.Euler(0f, unit.transform.eulerAngles.y, 0f);
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            targetRotation = Quaternion.Euler(0f, targetRotation.eulerAngles.y, 0f);

            float angleToTarget = Quaternion.Angle(currentRotation, targetRotation);

            // Si l'écart est inférieur à 15 % de 180°, on évite toute réorientation pour ne pas
            // déclencher une animation inutile.
            if (angleToTarget < minimalAngleRatio * 180f)
                continue;

            OrientUnitTowardClosestOpponent(unit, rotationSpeed);
        }
    }

    public void HandleAllegianceChanged(CharacterUnit unit, bool wasPlayerControlled)
    {
        if (unit == null)
            return;

        if (unit.IsPlayerControlled)
        {
            if (!totalDamageDealt.ContainsKey(unit))
                totalDamageDealt[unit] = 0;
        }
        else
        {
            totalDamageDealt.Remove(unit);
        }

        if (currentCharacterUnit == unit
            && wasPlayerControlled
            && !unit.IsPlayerControlled
            && IsPlayerDecisionState(currentBattleState))
        {
            EndTurn(unit);
            return;
        }

        BattleTimelineUIManager.Instance?.Refresh(currentCharacterUnit);
        OrientAllUnitsTowardClosestOpponent();
    }

    private static bool IsPlayerDecisionState(BattleState state)
    {
        switch (state)
        {
            case BattleState.SquadUnit_MainMenu:
            case BattleState.SquadUnit_SkillsMenu:
            case BattleState.SquadUnit_ItemsMenu:
            case BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnSquad:
            case BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnEnemies:
            case BattleState.SquadUnit_TargetSelectionAmongSquadForSkill:
            case BattleState.SquadUnit_TargetSelectionAmongSquadForItem:
            case BattleState.SquadUnit_TargetSelectionAmongEnemiesForSkill:
            case BattleState.SquadUnit_TargetSelectionAmongEnemiesForItem:
            case BattleState.SquadUnit_TargetSelectionForBaseAttack:
                return true;
            default:
                return false;
        }
    }

    private IEnumerator RotateTransformSmoothlyXY(Transform target, Quaternion targetRotation, float speed)
    {
        while (Quaternion.Angle(target.rotation, targetRotation) > 0.1f)
        {
            // Interpolation à vitesse constante
            target.rotation = Quaternion.RotateTowards(target.rotation, targetRotation, speed * Time.deltaTime);

            // En option : forcer Z à 0 à chaque frame pour éviter le roll parasite
            Vector3 euler = target.rotation.eulerAngles;
            target.rotation = Quaternion.Euler(euler.x, euler.y, 0f);

            yield return null;
        }

        // Force la rotation finale propre
        Vector3 finalEuler = targetRotation.eulerAngles;
        target.rotation = Quaternion.Euler(finalEuler.x, finalEuler.y, 0f);
    }


    private IEnumerator RotateUnitSmoothly(CharacterUnit unit, Quaternion targetRotation, float rotationSpeed)
    {
        Animator anim = unit.GetCasterAnimator();
        if (anim != null)
        {
            // Joue l'animation de rotation en boucle pendant la rotation
            anim.Play("Turn_90");
        }

        // Verrouille la rotation initiale sur l'axe Y pour éviter tout tilt.
        unit.transform.rotation = Quaternion.Euler(0f, unit.transform.eulerAngles.y, 0f);

        // Tant que l’angle entre la rotation actuelle et la cible reste significatif
        while (Quaternion.Angle(unit.transform.rotation, targetRotation) > 0.5f)
        {
            // Applique une rotation progressive uniquement sur l'axe Y
            Quaternion current = Quaternion.Euler(0f, unit.transform.eulerAngles.y, 0f);
            unit.transform.rotation = Quaternion.RotateTowards(
                current,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
            yield return null;
        }

        // Orientation finale propre (uniquement sur l’axe Y)
        unit.transform.rotation = Quaternion.Euler(0, targetRotation.eulerAngles.y, 0);

        // Lecture instantanée de l'animation "Idle_Battle" une fois la rotation terminée
        if (anim != null)
        {
            anim.Play("Idle_Battle");
        }
    }
    #endregion

    #region Gestion de la fin du combat
    private void HandleEndOfBattle()
    {
        if (currentBattleState == BattleState.None
            || currentBattleState == BattleState.VictoryScreen_Await
            || currentBattleState == BattleState.VictoryScreen_CanContinue
            || currentBattleState == BattleState.GameOverScreen_Await
            || currentBattleState == BattleState.GameOverScreen_CanContinue)
        {
            return;
        }

        bool allEnemiesDead = unitsInBattle
            .Where(u => u != null)
            .Where(u => u.characterType == CharacterType.EnemyUnit)
            .All(u => u.currentHP <= 0);

        bool allSquadDead = unitsInBattle
            .Where(u => u != null)
            .Where(u => u.characterType == CharacterType.SquadUnit)
            .All(u => u.currentHP <= 0);

        if (allEnemiesDead
            && !victorySequenceInProgress
            && currentBattleState != BattleState.VictoryScreen_Await
            && currentBattleState != BattleState.VictoryScreen_CanContinue)
        {
            Debug.Log("[BattleTurnManager] 🎉 Tous les ennemis sont vaincus !");
            lastBattleOutcome = BattleOutcome.Victory; // Enregistre l'issue du combat
            DispatchBattleEnded(BattleOutcome.Victory);
            StartCoroutine(ReduceTimeAndShowVictoryPanel());
        }
        else if (allSquadDead)
        {
            Debug.Log("[BattleTurnManager] 💀 Tous les alliés sont morts...");
            lastBattleOutcome = BattleOutcome.Defeat;
            DispatchBattleEnded(BattleOutcome.Defeat);

            if (HasPostBattleTimeline(defeatTimelineSequence))
            {
                respawnAtCheckpointOnExit = false;
                // Aucune interface de Game Over, on quitte directement le combat
                if (BattleTransitionManager.Instance != null)
                    BattleTransitionManager.Instance.StartCoroutine(
                        BattleTransitionManager.Instance.ExitVictoryScreenAndBattle());
                else
                    Debug.LogWarning("[BattleTurnManager] BattleTransitionManager introuvable pour quitter le combat.");
            }
            else if (gameOverOnDefeat)
            {
                // Passage direct a l'exploration avec respawn au checkpoint
                respawnAtCheckpointOnExit = true;
                if (BattleTransitionManager.Instance != null)
                    BattleTransitionManager.Instance.StartCoroutine(
                        BattleTransitionManager.Instance.ExitVictoryScreenAndBattle());
                else
                    Debug.LogWarning("[BattleTurnManager] BattleTransitionManager introuvable pour quitter le combat.");
            }
            else
            {
                // Affichage du panneau Game Over permettant de continuer la partie
                respawnAtCheckpointOnExit = true;
                ChangeBattleState(BattleState.GameOverScreen_Await);
                StartCoroutine(ShowGameOverPanel());
            }
        }
    }

    /// <summary>
    /// Coroutine en charge de déclencher la capture une fois que le rendu HDRP
    /// courant est totalement terminé. On évite ainsi d'invoquer Camera.Render()
    /// en plein enregistrement du RenderGraph (source des exceptions rencontrées).
    /// </summary>
    private Coroutine victoryScreenshotCoroutine;

    /// <summary>
    /// Prépare la capture d'écran de la caméra de combat sans forcer un nouveau
    /// rendu immédiat. La capture est effectuée à la fin du frame en cours pour
    /// rester compatible avec le RenderGraph de l'HDRP.
    /// </summary>
    public void TakeVictoryScreenshot()
    {
        if (VictoryScreenImage == null)
        {
            Debug.LogError("VictoryScreenImage n'est pas assigné !");
            return;
        }

        // Empêche le lancement de plusieurs captures simultanées ; seule la plus
        // récente est conservée pour ne pas multiplier les attentes d'une frame.
        if (victoryScreenshotCoroutine != null)
        {
            StopCoroutine(victoryScreenshotCoroutine);
            victoryScreenshotCoroutine = null;
        }

        victoryScreenshotCoroutine = StartCoroutine(CaptureVictoryScreenshotAtFrameEnd());
    }

    /// <summary>
    /// Attend la fin du frame courant puis laisse l'HDRP dessiner naturellement la
    /// caméra dans la RenderTexture désirée. En procédant ainsi, aucune commande
    /// de rendu imbriquée n'est exécutée et l'exception RenderGraph disparaît.
    /// </summary>
    private IEnumerator CaptureVictoryScreenshotAtFrameEnd()
    {
        // Vérifie que le GameObject est actif pour éviter les captures alors que
        // le manager serait désactivé (par exemple lors des transitions de scène).
        if (!isActiveAndEnabled)
        {
            victoryScreenshotCoroutine = null;
            yield break;
        }

        // Utilise la caméra mémorisée ; la recherche est effectuée ponctuellement si nécessaire.
        EnsureBattleCamera();
        Camera screenshotCamera = battleCamera?.GetComponent<Camera>();
        if (screenshotCamera == null)
        {
            Debug.LogError("Aucune caméra trouvée pour la capture !");
            victoryScreenshotCoroutine = null;
            yield break;
        }

        // On redirige la sortie de la caméra vers la RenderTexture demandée.
        if (!VictoryScreenImage.IsCreated())
            VictoryScreenImage.Create();

        RenderTexture previousTarget = screenshotCamera.targetTexture;
        screenshotCamera.targetTexture = VictoryScreenImage;

        // Attendre la fin du frame courant garantit que la caméra sera rendue
        // naturellement par le pipeline HDRP sans appel imbriqué de RenderGraph.
        yield return new WaitForEndOfFrame();

        // Une fois la frame terminée, on restaure la cible initiale pour éviter
        // d'altérer le comportement normal de la caméra dans les frames suivantes.
        screenshotCamera.targetTexture = previousTarget;

        Debug.Log("Screenshot de victoire capturé sans appel direct à Camera.Render().");

        victoryScreenshotCoroutine = null;
    }

    private IEnumerator ReduceTimeAndShowVictoryPanel()
    {
        victorySequenceInProgress = true; // 🔒 Bloque tout nouveau déclenchement pendant la séquence.

        // 1️⃣ Capture la dernière image du combat au moment de la victoire.
        yield return CaptureVictoryScreenshotBeforeCameraSwap();

        // 2️⃣ Ralentit progressivement le temps jusqu'à l'arrêt complet pour figer la scène.
        if (BattleTransitionManager.Instance != null)
        {
            yield return BattleTransitionManager.Instance.StartCoroutine(
                BattleTransitionManager.Instance.SlowTimeScale(0f, 0.5f));
        }
        else
        {
            Time.timeScale = 0f;
        }

        Time.fixedDeltaTime = 0f; // Les physiques sont également gelées.

        // 3️⃣ Demande officiellement la caméra de victoire afin de lancer le travelling.
        ChangeBattleState(BattleState.VictoryScreen_Await);
        battleIntroMenusLocked = false;

        // 4️⃣ Patiente jusqu'à la fin du blend Cinemachine pour que le plan soit parfaitement cadré.
        yield return WaitForVictoryCameraToSettle();

        // 5️⃣ L'interface de victoire peut maintenant se superposer au plan stabilisé.
        victoryScreen.SetActive(true);
        ForceUnscaledAnimators(victoryScreen.transform);
        InputsManager.Instance?.ForceDynamicInputUpdate();
        Transform victoryPanel = victoryScreen.transform.GetChild(0);
        Animator victoryAnim = victoryPanel.GetComponent<Animator>();
        if (victoryAnim != null)
        {
            victoryAnim.updateMode = AnimatorUpdateMode.UnscaledTime;
        }
        victoryPanel.gameObject.SetActive(true);

        InputsManager.Instance?.playerInputs?.Battle.Confirm.Enable();

        // 6️⃣ Distribution des récompenses avant de les afficher sur le panneau.
        int adjustedXP = ApplyHarmonicMenuPenalty(rewardXP);
        GameManager.Instance?.AddXPToSquad(adjustedXP);
        GameManager.Instance?.AddItemsToInventory(rewardItems);

        var panel = victoryScreen.GetComponentInChildren<VictoryPanelManager>();

        float duration = Time.time - battleStartTime;
        int totalEnemies = GameManager.Instance != null ? GameManager.Instance.gameData.enemiesDefeatedCount : 0;
        panel?.DisplayVictory(adjustedXP, rewardItems, totalEnemies, duration, mvpUnit, maxTurnDamage);

        // 7️⃣ Applique la capture sur le panneau de victoire.
        ApplyVictoryScreenTexture(VictoryScreenImage);

        Transform continueButtonTransform = FindChildRecursive(victoryScreen.transform.GetChild(0), "BattleScene_UI_VictoryPanel_Continue");

        if (continueButtonTransform == null)
        {
            // Avertit immédiatement si la hiérarchie UI ne contient plus le bouton attendu :
            // sans cette vérification, un NullReferenceException interrompait la coroutine et
            // empêchait la transition vers l'état VictoryScreen_CanContinue, bloquant ainsi
            // la touche Confirm.
            Debug.LogWarning("[BattleTurnManager] Bouton 'Continue' introuvable dans le panneau de victoire.");
        }
        else
        {
            GameObject continueButton = continueButtonTransform.gameObject;

            // ➡️ Branche un listener explicite sur le bouton Continue pour permettre la sortie via la souris/manette
            Button uiButton = continueButton.GetComponent<Button>();
            if (uiButton != null)
            {
                // On s'assure de ne pas empiler les callbacks si le panneau est affiché plusieurs fois d'affilée
                uiButton.onClick.RemoveListener(HandleVictoryContinueRequested);
                uiButton.onClick.AddListener(HandleVictoryContinueRequested);
            }

            // Sélectionne le bouton dans l'EventSystem afin que la manette/le clavier puisse valider immédiatement
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(continueButton);
            }
        }

        // Le champ de bataille doit disparaitre des l'affichage de la victoire.
        ChangeBattleState(BattleState.VictoryScreen_CanContinue);

        HideBattlefieldVisualsOnVictory();

        victorySequenceInProgress = false;
    }

    /// <summary>
    /// Lance la capture asynchrone du screenshot de victoire puis attend sa complétion.
    /// Cette étape est effectuée avant la transition caméra pour figer exactement le
    /// dernier instant du combat.
    /// </summary>
    private IEnumerator CaptureVictoryScreenshotBeforeCameraSwap()
    {
        // Déclenche la capture si la RenderTexture est correctement configurée.
        TakeVictoryScreenshot();

        // Si aucune coroutine n'a démarré (RenderTexture manquante, manager désactivé...)
        // on attend tout de même une frame pour rester déterministe, puis on abandonne.
        if (victoryScreenshotCoroutine == null)
        {
            yield return null;
            yield break;
        }

        // Boucle d'attente non bloquante : la capture se termine dès la fin du frame courant.
        while (victoryScreenshotCoroutine != null)
        {
            yield return null;
        }
    }

    /// <summary>
    /// Patiente jusqu'à ce que la caméra Cinemachine « CMV_Victory » soit pleinement active
    /// et que son blend lissé soit achevé. On évite ainsi d'afficher l'UI tant que le plan
    /// n'est pas parfaitement cadré.
    /// </summary>
    private IEnumerator WaitForVictoryCameraToSettle()
    {
        var cameraManager = BattleCameraManager.Instance;
        if (cameraManager == null)
        {
            yield break; // Sécurité : aucune caméra gérée, on évite de bloquer la coroutine.
        }

        // Première étape : s'assurer que la caméra de victoire est bien la cible actuelle.
        float ensureTimer = 0f;
        const float ensureTimeout = 1f; // marge suffisante pour couvrir un frame de retard.
        while (!string.Equals(cameraManager.CurrentCinemachineCameraName, VictoryCameraName, StringComparison.OrdinalIgnoreCase)
            && ensureTimer < ensureTimeout)
        {
            ensureTimer += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!string.Equals(cameraManager.CurrentCinemachineCameraName, VictoryCameraName, StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogWarning("[BattleTurnManager] Impossible de confirmer la caméra de victoire active.");
            yield break;
        }

        // Deuxième étape : attendre la fin effective du blend Smooth imposé (0,5 s).
        float blendDuration = Mathf.Max(cameraManager.SmoothBlendDuration, 0f);
        float elapsed = 0f;
        // Ajoute une petite marge pour éviter les erreurs d'arrondi et garantir un plan stabilisé.
        float paddedDuration = blendDuration + 0.05f;
        while (elapsed < paddedDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private static void ForceUnscaledAnimators(Transform root)
    {
        if (root == null)
            return;

        var animators = root.GetComponentsInChildren<Animator>(true);
        foreach (var animator in animators)
        {
            if (animator != null)
                animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        }

        var pulses = root.GetComponentsInChildren<Pulse>(true);
        foreach (var pulse in pulses)
        {
            if (pulse != null)
                pulse.forceUnscaledTime = true;
        }
    }

    private void ApplyVictoryScreenTexture(RenderTexture texture)
    {
        if (texture == null || victoryScreen == null)
            return;

        Transform panel = FindChildRecursive(victoryScreen.transform, "BattleScene_UI_VictoryScreen_Panel");
        RawImage img = panel != null ? panel.GetComponent<RawImage>() : null;

        if (img == null)
            img = victoryScreen.GetComponentInChildren<RawImage>(true);

        if (img != null)
            img.texture = texture;
        else
            Debug.LogWarning("[BattleTurnManager] RawImage introuvable pour le fond de l'ecran de victoire.");
    }

    private void HideBattlefieldVisualsOnVictory()
    {
        BattlefieldManager.Instance?.SetBattlefieldVisible(false);

        foreach (var unit in unitsInBattle)
        {
            if (unit == null)
                continue;

            ToggleUnitRenderers(unit, false);
        }
    }

    private static void ToggleUnitRenderers(CharacterUnit unit, bool visible)
    {
        if (unit == null)
            return;

        var renderers = unit.GetComponentsInChildren<Renderer>(true);
        foreach (var renderer in renderers)
        {
            if (renderer != null)
                renderer.enabled = visible;
        }
    }

    /// <summary>
    /// Callback déclenché par le bouton "Continuer" de l'écran de victoire.
    /// Permet d'appliquer exactement la même logique que la touche de validation.
    /// </summary>
    private void HandleVictoryContinueRequested()
    {
        // Évite les validations intempestives si l'état du combat n'est pas prêt.
        if (currentBattleState != BattleState.VictoryScreen_CanContinue
            && currentBattleState != BattleState.GameOverScreen_CanContinue)
            return;

        ChangeBattleState(BattleState.None);

        // Lancement sécurisé de la transition de sortie de combat
        var transitionManager = BattleTransitionManager.Instance;
        if (transitionManager != null)
        {
            // La coroutine interne est désormais protégée contre les doubles lancements,
            // mais on centralise tout de même l'appel pour éviter toute NullReference.
            transitionManager.StartCoroutine(transitionManager.ExitVictoryScreenAndBattle());
        }
        else
        {
            Debug.LogWarning("[NewBattleManager] BattleTransitionManager introuvable lors de la demande de sortie de combat.");
        }
    }

    IEnumerator ShowGameOverPanel()
    {
        // Ralentit le temps jusqu'à l'arrêt complet avant d'afficher le panneau
        if (BattleTransitionManager.Instance != null)
            yield return BattleTransitionManager.Instance.StartCoroutine(
                BattleTransitionManager.Instance.SlowTimeScale(0f, 0.5f));
        else
            Time.timeScale = 0f;

        Time.fixedDeltaTime = 0f;

        // Activation du panneau GameOver (animation en temps réel)
        Transform panel = gameOverScreen.transform.GetChild(0);
        Animator anim = panel.GetComponent<Animator>();
        if (anim != null)
            anim.updateMode = AnimatorUpdateMode.UnscaledTime;
        panel.gameObject.SetActive(true);
        ForceUnscaledAnimators(gameOverScreen.transform);
        InputsManager.Instance?.ForceDynamicInputUpdate();
        battleIntroMenusLocked = false;
        InputsManager.Instance?.playerInputs?.Battle.Confirm.Enable();

        CleanupAllSpawnedUnits();

        ChangeBattleState(BattleState.GameOverScreen_CanContinue);
    }

    private void CleanupAllSpawnedUnits()
    {
        foreach (var unit in unitsInBattle)
            if (unit != null)
                Destroy(unit.gameObject);

        unitsInBattle.Clear();
        // Évite de conserver des références obsolètes qui empêcheraient un nouveau spawn
        activeCharacterUnits.Clear();
    }
    #endregion

    #region Gestion de l’ouverture des menus

    private Transform ResolveBattleCameraRig()
    {
        if (battleCameraRig != null)
            return battleCameraRig;

        if (battleCamera != null)
        {
            Camera[] cameras = battleCamera.GetComponentsInChildren<Camera>(true);
            foreach (Camera cam in cameras)
            {
                if (cam != null && cam.name == "BattleCamera_Cam")
                {
                    battleCameraRig = cam.transform;
                    return battleCameraRig;
                }
            }
        }

        var cameraGO = GameObject.Find("BattleCamera_Cam");
        if (cameraGO == null)
        {
            Debug.LogWarning("[NewBattleManager] Impossible de localiser 'BattleCamera_Cam' dans la scène.");
            return null;
        }

        battleCameraRig = cameraGO.transform;
        return battleCameraRig;
    }

    private void SetupCurrentUnitMenus()
    {
        // 1) Essaye de récupérer la BattleCamera par tag
        // Utilise la caméra de combat mémorisée
        Transform battleCamCam = ResolveBattleCameraRig();
        if (battleCamCam == null)
        {
            Debug.LogWarning("[SetupCurrentUnitMenus] BattleCamera introuvable.");
            return;
        }

        Transform mainPanel = FindChildRecursive(battleCamCam, "MainMenu_Panel");
        if (mainPanel == null)
        {
            Debug.LogWarning("[SetupCurrentUnitMenus] 'MainMenu_Panel' introuvable sous la BattleCamera.");
            currentMainMenuContainer = null;
            currentMainMenuSlots = new List<Transform>();
        }
        else
        {
            currentMainMenuContainer = mainPanel.gameObject;
            // On recherche ensuite le container « Menu » à l’intérieur du MainMenu_Panel
            Transform mainSlotsParent = FindChildRecursive(mainPanel, "Menu");
            currentMainMenuSlots = (mainSlotsParent != null)
                ? mainSlotsParent.Cast<Transform>().ToList()
                : new List<Transform>();

            if (currentMainMenuSlots.Count > 0)
            {
                while (currentMainMenuSlots.Count < 2)
                {
                    int sourceIndex = currentMainMenuSlots.Count > 1 ? 1 : 0;
                    Transform source = currentMainMenuSlots[sourceIndex];
                    Transform clone = Instantiate(source, source.parent);
                    currentMainMenuSlots.Add(clone);
                }
            }
            else
            {
                Debug.LogWarning("[SetupCurrentUnitMenus] Aucun slot de menu principal détecté ; impossible de garantir les 2 emplacements requis.");
            }
        }

        // 3) Panneau SkillsMenu_Panel
        Transform skillsPanel = FindChildRecursive(battleCamCam, "SkillsMenu_Panel");
        if (skillsPanel == null)
        {
            Debug.LogWarning("[SetupCurrentUnitMenus] 'SkillsMenu_Panel' introuvable sous la BattleCamera.");
            currentSkillsMenuContainer = null;
            currentSkillsMenuSlots = new List<Transform>();
        }
        else
        {
            currentSkillsMenuContainer = skillsPanel.gameObject;
            Transform skillsSlotsParent = FindChildRecursive(skillsPanel, "Menu");
            currentSkillsMenuSlots = (skillsSlotsParent != null)
                ? skillsSlotsParent.Cast<Transform>().ToList()
                : new List<Transform>();

            // S'assure de disposer d'au moins 5 emplacements :
            //  - Slot 0 : texte / action d'attaque de base
            //  - Slots 1 à 3 : attaques musicales
            //  - Slot 4 : move spécial
            // L'objectif est d'éviter tout débordement lorsque nous injectons dynamiquement
            // les attaques de la SquadUnit courante. On privilégie un clonage du premier slot
            // (structure déjà configurée dans l'éditeur) pour conserver les bons réglages d'UI.
            if (currentSkillsMenuSlots.Count > 0)
            {
                while (currentSkillsMenuSlots.Count < 5)
                {
                    // Clone le premier slot pour compléter la liste si besoin et préserver
                    // l'homogénéité visuelle avec les éléments déjà configurés dans la scène.
                    Transform clone = Instantiate(currentSkillsMenuSlots[0], currentSkillsMenuSlots[0].parent);
                    currentSkillsMenuSlots.Add(clone);
                }
            }
            else
            {
                // Aucun slot n'a été trouvé : on enregistre un avertissement explicite pour faciliter
                // le diagnostic (probable régression de la hiérarchie UI) tout en évitant un crash.
                Debug.LogWarning("[SetupCurrentUnitMenus] Aucun slot de compétence détecté ; impossible de garantir les 5 emplacements requis.");
            }
        }

        // 4) Panneau ItemsMenu_Panel
        Transform itemsPanel = FindChildRecursive(battleCamCam, "ItemsMenu_Panel");
        if (itemsPanel == null)
        {
            Debug.LogWarning("[SetupCurrentUnitMenus] 'ItemsMenu_Panel' introuvable sous la BattleCamera.");
            currentItemsMenuContainer = null;
            currentItemsMenuSlots = new List<Transform>();
        }
        else
        {
            currentItemsMenuContainer = itemsPanel.gameObject;
            Transform itemsSlotsParent = FindChildRecursive(itemsPanel, "Menu");
            currentItemsMenuSlots = (itemsSlotsParent != null)
                ? itemsSlotsParent.Cast<Transform>().ToList()
                : new List<Transform>();
        }
    }

    /// <summary>
    /// Vérifie qu'une unité est bien renseignée pour les menus et essaie de la récupérer sinon.
    /// Cette méthode est indispensable pour garantir que les caméras Cinemachine (et notamment
    /// « CMV_ItemsMenu ») disposent toujours d'une unité de référence valide lorsque l'on ouvre un menu.
    /// </summary>
    /// <param name="callerContext">Nom de la méthode appelante (utilisé dans les logs).</param>
    private bool EnsureCurrentCharacterUnitForMenus(string callerContext)
    {
        if (currentCharacterUnit != null)
            return true; // Cas nominal : rien à corriger.

        CharacterUnit resolvedUnit = null;

        // 1) Premier réflexe : demander au gestionnaire de caméras quelle unité il suit encore.
        BattleCameraManager cameraManager = BattleCameraManager.Instance;
        if (cameraManager != null)
            resolvedUnit = cameraManager.CurrentTurnOwner;

        // 2) Si la caméra n'a plus l'information, on tente de détecter l'unité dont la jauge ATB
        //    a atteint le seuil. C'est normalement la seule à pouvoir ouvrir les menus de tour.
        if (resolvedUnit == null)
        {
            resolvedUnit = activeCharacterUnits.FirstOrDefault(u =>
                u != null &&
                u.Data != null &&
                u.IsPlayerControlled &&
                u.currentHP > 0 &&
                u.currentATB >= ATB_THRESHOLD);
        }

        // 3) Fallback supplémentaire : choisir la première unité jouable côté joueur encore en vie.
        if (resolvedUnit == null)
        {
            resolvedUnit = activeCharacterUnits.FirstOrDefault(u =>
                u != null &&
                u.Data != null &&
                u.IsPlayerControlled &&
                u.currentHP > 0);
        }

        if (resolvedUnit == null)
        {
            Debug.LogError($"[{callerContext}] Impossible d'identifier l'unité active avant l'ouverture d'un menu. " +
                           "La caméra ne peut donc pas se caler sur l'unité attendue.");
            return false;
        }

        // On mémorise la nouvelle unité pour que tout le pipeline (menus, caméra, timelines) reste cohérent.
        ChangeCurrentCharacterUnit(resolvedUnit);

        // Le gestionnaire caméra doit également être synchronisé afin de replacer immédiatement les rigs.
        if (cameraManager != null && cameraManager.CurrentTurnOwner != resolvedUnit)
            cameraManager.SetTurnOwner(resolvedUnit);

        return true;
    }

    public void ShowMainMenu()
    {
        if (!EnsureCurrentCharacterUnitForMenus(nameof(ShowMainMenu)))
            return;

        // Réaffiche la jauge de passage de tour si elle existe.
        PassTurnUI.Instance?.Show();
        ActionUIDisplayManager.Instance.DisplayInstruction_SelectItemSkillOrPass();
        ChangeBattleState(BattleState.SquadUnit_MainMenu);
        ToggleMenuContainers(true, false, false);
        // Feedback audio systématique lorsque le menu principal devient visible.
        PlayMenuClip(mainMenuOpenClip);

        // S'assure que l'action "Confirm" est bien active.
        // Elle peut avoir été désactivée à la fin d'un QTE.
        InputsManager.Instance?.playerInputs.Battle.Confirm.Enable();

        // Arrête la Timeline d'attente si elle était en cours
        StopItemPreparingTimeline();

        // Réinitialise les actions sélectionnées afin d'éviter des conflits
        currentMove = null;
        currentItem = null;

        Debug.Log($"[ShowMainMenu] Nombre de slots = {currentMainMenuSlots.Count}");
        for (int i = 0; i < currentMainMenuSlots.Count; i++)
            Debug.Log($"Slot {i} = {currentMainMenuSlots[i].name}, enfants : {currentMainMenuSlots[i].childCount}");

        // Vérifie que la liste contient au moins deux slots avant de tenter de les utiliser
        if (currentMainMenuSlots == null || currentMainMenuSlots.Count < 2)
        {
            Debug.LogWarning("[ShowMainMenu] Impossible d'afficher le menu principal : nombre de slots insuffisant.");
            return; // Évite un ArgumentOutOfRangeException
        }

        // Renseigne les boutons du menu principal
        UpdateButton(currentMainMenuSlots[0], "Compétences", null);
        UpdateButton(currentMainMenuSlots[1], "Objet", null);
        for (int i = 0; i < currentMainMenuSlots.Count; i++)
            currentMainMenuSlots[i].gameObject.SetActive(i < 2);
    }

    public void OpenSkillsMenu()
    {
        if (!EnsureCurrentCharacterUnitForMenus(nameof(OpenSkillsMenu)))
            return;

        // Masque la jauge lorsque l'on ouvre le menu des compétences.
        PassTurnUI.Instance?.Hide();
        ActionUIDisplayManager.Instance.DisplayInstruction_SelectSkill();
        ChangeBattleState(BattleState.SquadUnit_SkillsMenu);
        // S'assure qu'aucun item n'est en cours de sélection
        currentItem = null;
        ToggleMenuContainers(false, true, false);
        // Son distinct pour signaler l'entrée dans le menu des compétences.
        PlayMenuClip(skillsMenuOpenClip);
        currentMenuIndex = 0;

        // Réinitialise la page affichée
        currentSkillPageIndex = 0;

        // Récupère toutes les attaques musicales disponibles (hors move spécial)
        skillChoices = currentCharacterUnit.Data.musicalAttacks
            .Where(m => !m.onlyAwake || currentCharacterUnit.IsAwake)
            .Where(m => !m.enterAwake || !currentCharacterUnit.IsAwake)
            .Where(m => currentCharacterUnit.CanUseMove(m))
            .ToList();

        // Réordonne les attaques selon le set sélectionné afin de mettre en avant les favoris.
        skillChoices = currentCharacterUnit.OrderMovesForCurrentSet(skillChoices);

        // Identifie l'attaque basique dédiée au premier slot du SkillsMenu.
        // Même si cette attaque n'apparaît pas dans la liste (fallback global, limites d'utilisation...),
        // on force son affichage fixe pour respecter les attentes des joueurs.
        basicAttackMoveChoice = ResolveBasicAttackMove(currentCharacterUnit);
        if (basicAttackMoveChoice != null)
        {
            // On retire l'attaque basique de la liste paginée afin qu'elle ne se décale jamais
            // lorsqu'on feuillette les autres compétences musicales. La comparaison est volontairement
            // plus souple (référence + noms) pour couvrir les cas où le ScriptableObject serait dupliqué
            // dans un set tout en pointant vers la même action de base.
            skillChoices.RemoveAll(move => IsEquivalentToBasicAttack(move, basicAttackMoveChoice));
        }

        // Détermine le mouvement spécial autorisé, qui sera affiché dans le 4e slot
        specialMoveChoice = (currentCharacterUnit.Data.specialMusicalMove != null &&
            currentCharacterUnit.CanUseMove(currentCharacterUnit.Data.specialMusicalMove))
            ? currentCharacterUnit.Data.specialMusicalMove
            : null;

        // Affiche la première page de compétences
        RefreshSkillsMenuDisplay();
    }

    /// <summary>
    /// Rafraîchit l'affichage des compétences selon la page courante.
    /// </summary>
    public void RefreshSkillsMenuDisplay()
    {
        // Sécurise l'accès à la liste des slots de compétences
        if (currentSkillsMenuSlots == null || currentSkillsMenuSlots.Count == 0)
        {
            Debug.LogWarning("[RefreshSkillsMenuDisplay] Aucun slot de compétences disponible.");
            return;
        }

        // Le dernier slot du SkillsMenu est réservé au Special Musical Move
        int specialSlotIndex = currentSkillsMenuSlots.Count - 1;
        if (specialSlotIndex < 0)
        {
            Debug.LogWarning("[RefreshSkillsMenuDisplay] Index de slot spécial invalide.");
            return;
        }

        // 0) Assure l'affichage fixe de l'attaque basique dans le tout premier slot
        if (currentSkillsMenuSlots.Count > 0)
        {
            Transform basicSlot = currentSkillsMenuSlots[0];
            if (basicAttackMoveChoice != null && currentCharacterUnit != null && currentCharacterUnit.Data != null)
            {
                UpdateButton(basicSlot, basicAttackMoveChoice.moveName, basicAttackMoveChoice.moveIcon, basicAttackMoveChoice.description);

                bool enoughHarmonic = currentCharacterUnit.GetAvailableHarmonicsForCost(basicAttackMoveChoice.consumedHarmonicType) >= basicAttackMoveChoice.harmonicCost;
                bool resonanceOk = !basicAttackMoveChoice.enterAwake || currentCharacterUnit.GetHarmonicCount(currentCharacterUnit.Data.harmonicType) >= currentCharacterUnit.Data.awakeHarmonicThreshold;
                bool usageOk = currentCharacterUnit.CanUseMove(basicAttackMoveChoice);
                bool cooldownOk = !currentCharacterUnit.IsMoveOnCooldown(basicAttackMoveChoice);
                bool available = enoughHarmonic && resonanceOk && usageOk && cooldownOk;
                SetButtonAvailability(basicSlot, available, false);
            }
            else
            {
                // Fallback visuel en cas d'absence d'attaque basique (situation exceptionnelle)
                if (emptyMove != null)
                    UpdateButton(basicSlot, emptyMove.moveName, emptyMove.moveIcon);
                else
                    UpdateButton(basicSlot, "Indisponible", null);
                SetButtonAvailability(basicSlot, false, false);
            }
        }

        // On récupère dynamiquement les indices réellement disponibles pour les attaques musicales standard. En cas de
        // modification de la hiérarchie (slot manquant, renommé, etc.), la liste s'ajustera automatiquement plutôt que
        // d'écraser les emplacements réservés à l'attaque de base ou au mouvement spécial.
        List<int> paginatedSlotIndices = BuildPaginatedSkillSlotIndices();
        int pageSize = paginatedSlotIndices.Count;

        if (pageSize > 0)
        {
            int maxPage = Mathf.Max(0, (skillChoices.Count - 1) / pageSize);
            currentSkillPageIndex = Mathf.Clamp(currentSkillPageIndex, 0, maxPage);
        }
        else
        {
            currentSkillPageIndex = 0;
        }

        int startIndex = pageSize > 0 ? currentSkillPageIndex * pageSize : 0;

        // 1) Affiche les attaques musicales paginées (hors attaque basique et move spécial)
        for (int i = 0; i < pageSize; i++)
        {
            int slotIndex = paginatedSlotIndices[i];
            Transform slot = currentSkillsMenuSlots[slotIndex];
            int globalIndex = startIndex + i;
            if (globalIndex < skillChoices.Count)
            {
                var move = skillChoices[globalIndex];
                UpdateButton(slot, move.moveName, move.moveIcon, move.description);

                bool enoughHarmonic = currentCharacterUnit.GetAvailableHarmonicsForCost(move.consumedHarmonicType) >= move.harmonicCost;
                bool resonanceOk = !move.enterAwake || currentCharacterUnit.GetHarmonicCount(currentCharacterUnit.Data.harmonicType) >= currentCharacterUnit.Data.awakeHarmonicThreshold;
                bool usageOk = currentCharacterUnit.CanUseMove(move);
                bool available = enoughHarmonic && resonanceOk && usageOk;
                SetButtonAvailability(slot, available, false);
            }
            else
            {
                // Slot vide ou hors de portée
                if (emptyMove != null)
                    UpdateButton(slot, emptyMove.moveName, emptyMove.moveIcon);
                else
                    UpdateButton(slot, "Indisponible", null);
                SetButtonAvailability(slot, false, false);
            }
        }

        // 2) Place le mouvement spécial dans le dernier slot
        if (specialMoveChoice != null)
        {
            UpdateButton(currentSkillsMenuSlots[specialSlotIndex], specialMoveChoice.moveName, specialMoveChoice.moveIcon, specialMoveChoice.description);

            bool enoughHarmonic = currentCharacterUnit.GetAvailableHarmonicsForCost(specialMoveChoice.consumedHarmonicType) >= specialMoveChoice.harmonicCost;
            bool resonanceOk = !specialMoveChoice.enterAwake || currentCharacterUnit.GetHarmonicCount(currentCharacterUnit.Data.harmonicType) >= currentCharacterUnit.Data.awakeHarmonicThreshold;
            bool usageOk = currentCharacterUnit.CanUseMove(specialMoveChoice);
            bool available = enoughHarmonic && resonanceOk && usageOk;
            SetButtonAvailability(currentSkillsMenuSlots[specialSlotIndex], available, available);
        }
        else
        {
            if (emptyMove != null)
                UpdateButton(currentSkillsMenuSlots[specialSlotIndex], emptyMove.moveName, emptyMove.moveIcon);
            else
                UpdateButton(currentSkillsMenuSlots[specialSlotIndex], "Indisponible", null);
            SetButtonAvailability(currentSkillsMenuSlots[specialSlotIndex], false, false);
        }
    }

    /// <summary>
    /// Passe à la page suivante des compétences si possible.
    /// </summary>
    public void NextSkillPage()
    {
        // Calcule le nombre de slots paginés (hors attaque basique et move spécial)
        int pageSize = GetPaginatedSkillSlotCount();
        if (pageSize <= 0)
        {
            // Évite une division par zéro si aucun slot paginé n'est disponible
            Debug.LogWarning("[NextSkillPage] Impossible de changer de page : aucun slot paginé.");
            return;
        }

        int maxPage = Mathf.Max(0, (skillChoices.Count - 1) / pageSize);
        if (currentSkillPageIndex < maxPage)
        {
            currentSkillPageIndex++;
            // Retour audio pour confirmer le changement de page.
            PlayMenuClip(skillPageChangeClip);
            RefreshSkillsMenuDisplay();
        }
    }

    /// <summary>
    /// Revient à la page précédente des compétences si possible.
    /// </summary>
    public void PreviousSkillPage()
    {
        // Empêche toute interaction si aucun slot paginé n'est disponible (par exemple lorsque seul
        // l'attaque de base et le move spécial sont configurés dans l'UI).
        if (GetPaginatedSkillSlotCount() <= 0)
            return;

        // Vérifie qu'il existe réellement des pages avant de revenir en arrière
        if (currentSkillPageIndex > 0)
        {
            currentSkillPageIndex--;
            // Même feedback lors d'un retour vers une page précédente.
            PlayMenuClip(skillPageChangeClip);
            RefreshSkillsMenuDisplay();
        }
    }

    public void OpenItemMenu()
    {
        if (!EnsureCurrentCharacterUnitForMenus(nameof(OpenItemMenu)))
            return;

        ActionUIDisplayManager.Instance.DisplayInstruction_SelectItem();
        ChangeBattleState(BattleState.SquadUnit_ItemsMenu);
        // S'assure qu'aucune compétence n'est en cours de sélection
        currentMove = null;
        ToggleMenuContainers(false, false, true);
        // Clip spécifique pour différencier l'accès à l'inventaire.
        PlayMenuClip(itemsMenuOpenClip);
        currentMenuIndex = 0;

        // Démarre la Timeline d'attente de sélection d'objet
        StartItemPreparingTimeline();

        itemChoices = InventoryManager.Instance.GetUsableItems(currentCharacterUnit);

        // 6) Création des boutons d’items
        for (int i = 0; i < itemChoices.Count && i < currentItemsMenuSlots.Count; i++)
        {
            var item = itemChoices[i];
            UpdateButton(currentItemsMenuSlots[i], item.itemName, item.itemIcon, item.description);
        }

        // Indique les emplacements vides
        for (int j = itemChoices.Count; j < currentItemsMenuSlots.Count; j++)
        {
            if (emptyMove != null)
                UpdateButton(currentItemsMenuSlots[j], emptyMove.moveName, emptyMove.moveIcon);
            else
                UpdateButton(currentItemsMenuSlots[j], "Indisponible", null);
        }
    }

    public void ToggleMenuContainers(bool showMain, bool showSkills, bool showItems)
    {
        if (AreBattleMenusLocked)
        {
            SetMenuContainersActive(false, false, false);
            return;
        }

        // Sécurité supplémentaire : on vérifie que l'unité courante et ses données sont valides
        if (currentCharacterUnit == null || currentCharacterUnit.Data == null)
        {
            Debug.LogWarning("[ToggleMenuContainers] currentCharacterUnit ou ses données sont null.");
            return;
        }

        // Si les références aux menus ne sont pas initialisées, on interrompt l'action
        if (currentMainMenuContainer == null || currentSkillsMenuContainer == null || currentItemsMenuContainer == null)
        {
            Debug.LogWarning("[ToggleMenuContainers] Menus non initialisés correctement.");
            return;
        }

        // Activation/désactivation des différents menus selon les paramètres
        SetMenuContainersActive(showMain, showSkills, showItems);
    }

    private void SetMenuContainersActive(bool showMain, bool showSkills, bool showItems)
    {
        if (currentMainMenuContainer != null)
            currentMainMenuContainer.SetActive(showMain);
        if (currentSkillsMenuContainer != null)
            currentSkillsMenuContainer.SetActive(showSkills);
        if (currentItemsMenuContainer != null)
            currentItemsMenuContainer.SetActive(showItems);
    }

    public void HideMenusForFreeCamera()
    {
        SetMenuContainersActive(false, false, false);
    }

    public void RefreshMenusForCurrentState()
    {
        if (AreBattleMenusLocked)
        {
            SetMenuContainersActive(false, false, false);
            return;
        }

        bool showMain = currentBattleState == BattleState.SquadUnit_MainMenu;
        bool showSkills = currentBattleState == BattleState.SquadUnit_SkillsMenu;
        bool showItems = currentBattleState == BattleState.SquadUnit_ItemsMenu;

        SetMenuContainersActive(showMain, showSkills, showItems);
    }

    public void RegisterBattleEventStart()
    {
        battleEventMenuLockCount++;
        SetMenuContainersActive(false, false, false);
    }

    public void RegisterBattleEventEnd()
    {
        if (battleEventMenuLockCount > 0)
            battleEventMenuLockCount--;

        if (battleEventMenuLockCount > 0)
            return;

        RefreshMenusAfterBattleEvent();
    }

    private void RefreshMenusAfterBattleEvent()
    {
        if (currentCharacterUnit == null || currentCharacterUnit.Data == null)
            return;

        bool showMain = currentBattleState == BattleState.SquadUnit_MainMenu;
        bool showSkills = currentBattleState == BattleState.SquadUnit_SkillsMenu;
        bool showItems = currentBattleState == BattleState.SquadUnit_ItemsMenu;

        SetMenuContainersActive(showMain, showSkills, showItems);
    }

    private void UpdateButton(Transform slot, string label, Sprite icon, string description = null)
    {
        if (slot == null || slot.childCount == 0)
        {
            Debug.LogWarning($"[UpdateButton] Slot invalide ou vide : {slot?.name}");
            return;
        }

        if (slot == null)
        {
            Debug.LogWarning($"[UpdateButton] L’enfant du slot {slot.name} est null.");
            return;
        }

        var txt = slot.GetComponentInChildren<TextMeshProUGUI>();
        var slotRect = slot as RectTransform;
        var img = slot.childCount > 3 ? slot.GetChild(3).GetComponent<Image>() : null;

        if (txt != null)
        {
            ConfigureMenuTextAutosizing(txt);

            if (!string.IsNullOrWhiteSpace(description))
                txt.text = $"{label}\n<color=#CFCFCF>{description}</color>";
            else
                txt.text = label;

            AdjustMenuSlotToText(txt, slotRect);
        }
        if (img != null) img.sprite = icon;
    }

    private void ConfigureMenuTextAutosizing(TextMeshProUGUI txt)
    {
        txt.enableAutoSizing = true;
        txt.fontSizeMin = MinMenuFontSize;
        if (txt.fontSizeMax < txt.fontSize)
            txt.fontSizeMax = txt.fontSize;
    }

    private void AdjustMenuSlotToText(TextMeshProUGUI txt, RectTransform slotRect)
    {
        if (txt == null || slotRect == null)
            return;

        RectTransform textRect = txt.rectTransform;

        if (!baseMenuTextHeights.ContainsKey(txt))
            baseMenuTextHeights[txt] = textRect.sizeDelta.y;

        if (!baseMenuSlotHeights.ContainsKey(slotRect))
            baseMenuSlotHeights[slotRect] = slotRect.sizeDelta.y;

        // Met à jour les métriques après application du nouveau contenu.
        txt.ForceMeshUpdate(true, true);

        float baseTextHeight = baseMenuTextHeights[txt];
        float desiredTextHeight = baseTextHeight;
        bool atMinimumSize = txt.enableAutoSizing && txt.fontSize <= MinMenuFontSize + 0.5f;

        // Si le texte est déjà à la taille minimale et déborde encore, on étend la zone.
        if (atMinimumSize && (txt.isTextOverflowing || txt.preferredHeight > baseTextHeight))
            desiredTextHeight = Mathf.Max(baseTextHeight, txt.preferredHeight);

        textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, desiredTextHeight);

        float targetSlotHeight = Mathf.Max(baseMenuSlotHeights[slotRect], desiredTextHeight + MenuSlotVerticalPadding);
        slotRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetSlotHeight);
    }

    private void PlayTurnStartVoice(CharacterUnit unit)
    {
        PlayTurnVoice(unit, unit?.Data?.turnStartVoiceline);
    }

    private void PlayTurnEndVoice(CharacterUnit unit)
    {
        PlayTurnVoice(unit, unit?.Data?.turnEndVoiceline);
    }

    private void PlayBaseAttackVoice(CharacterUnit unit)
    {
        PlayTurnVoice(unit, unit?.Data?.baseAttackVoiceline);
    }

    private void PlayTurnVoice(CharacterUnit unit, AudioClipSO clip)
    {
        if (unit == null || unit.Data == null || clip == null)
            return;

        AudioManager.Instance?.PlayVoice(clip, unit.Data.characterName);
    }

    private void SetButtonAvailability(Transform slot, bool available, bool highlight = false)
    {
        if (slot == null)
            return;

        var txt = slot.GetComponentInChildren<TextMeshProUGUI>();
        var img = slot.childCount > 3 ? slot.GetChild(3).GetComponent<Image>() : null;

        Color color = available ? (highlight ? Color.yellow : Color.white) : Color.gray;
        if (txt != null) txt.color = color;
        if (img != null) img.color = color;
    }

    /// <summary>
    /// Démarre la séquence d'attente liée au menu d'objets.
    /// - Active la Cinemachine dédiée pour donner la priorité à l'angle "Item_Preparing".
    /// - Lance l'animation "Item_Prepare" du lanceur pour un retour visuel clair.
    /// - Joue la timeline (si disponible) afin de conserver les effets annexes prévus.
    /// </summary>
    private void StartItemPreparingTimeline()
    {
        // Évite de relancer la séquence plusieurs fois si l'on reste dans le menu d'objets.
        if (itemMenuTimelineActive)
            return;

        if (currentCharacterUnit == null)
        {
            Debug.LogWarning("[StartItemPreparingTimeline] Impossible de lancer la séquence : aucune unité courante n'est définie.");
            return;
        }

        // Récupère l'Animator enfant du lanceur via l'utilitaire centralisé pour garantir un binding correct.
        Animator casterAnimator = currentCharacterUnit.GetCasterAnimator();

        if (casterAnimator != null)
        {
            // CrossFade permet d'éviter un cut sec lorsque l'on ouvre le menu après une autre animation.
            if (casterAnimator.HasState(0, AnimatorStateItemPrepare))
                casterAnimator.CrossFade(AnimatorStateItemPrepare, 0.1f, 0, 0f);
            else
                casterAnimator.Play("Item_Prepare"); // Fallback si le hash n'est pas disponible dans le contrôleur.
        }
        else
        {
            Debug.LogWarning("[StartItemPreparingTimeline] Aucun Animator trouvé pour le lanceur : l'animation Item_Prepare ne sera pas jouée.");
        }

        // Confie la priorité caméra à la Cinemachine dédiée à l'attente d'objet.
        var cameraManager = BattleCameraManager.Instance;
        if (cameraManager != null)
        {
            cameraManager.ConfigureActionTargets(currentCharacterUnit, null);
            if (itemPreparingCameraMotif != null)
                cameraManager.SetCameraMotif(itemPreparingCameraMotif);
        }
        // 🛠️ Important : on force explicitement la Cinemachine du menu d'objets. Auparavant, la timeline
        // d'attente rappelait la caméra "MainMenu" via un rôle générique (MainMenuIdle), ce qui volait la
        // priorité à « CMV_ItemsMenu » juste après le changement d'état. En réutilisant le même pipeline
        // que le système de menus (RequestCamera), on garantit que l'angle spécialisé des objets reste actif
        // tant que le joueur consulte l'inventaire.
        RequestCamera(ItemsMenuCameraName, MenuCameraBlendDuration, MenuCameraBlendStyle);

        // L'objet d'animation sert de point d'ancrage pour la timeline.
        GameObject animGO = casterAnimator != null ? casterAnimator.gameObject : currentCharacterUnit.GetCasterBindingTarget();

        // Même si la timeline est optionnelle, on conserve l'exécution des signaux si elle existe.
        if (BattleTimelineManager.Instance != null && animGO != null)
        {
            // La timeline reste utile pour jouer les éventuels signaux ou effets complémentaires.
            if (itemPreparingTimeline != null)
                BattleTimelineManager.Instance.PlayCasterTimeline(itemPreparingTimeline, currentCharacterUnit, animGO);
        }

        itemMenuTimelineActive = true;
    }

    /// <summary>
    /// Arrête la séquence de préparation d'objet.
    /// - Rend la main à la caméra principale.
    /// - Replace le lanceur sur sa pose d'attente.
    /// - Coupe la timeline d'attente si elle était lancée.
    /// </summary>
    private void StopItemPreparingTimeline()
    {
        if (!itemMenuTimelineActive)
            return;

        // On redonne la priorité à la Cinemachine dédiée aux items pour la suite des actions.
        var cameraManager = BattleCameraManager.Instance;
        if (cameraManager != null)
        {
            cameraManager.SwitchToCamera(ItemsMenuCameraName, MenuCameraBlendDuration, MenuCameraBlendStyle);
            if (itemPreparingCameraMotif != null)
                cameraManager.ClearCameraMotif();
            cameraManager.ClearRigTargets();
        }

        if (currentCharacterUnit != null)
        {
            // Même logique que dans Start : priorité au cache, puis recherche de secours.
            Animator casterAnimator = currentCharacterUnit.GetCasterAnimator();
            if (casterAnimator != null)
            {
                if (casterAnimator.HasState(0, AnimatorStateIdleBattle))
                    casterAnimator.CrossFade(AnimatorStateIdleBattle, 0.1f, 0, 0f);
                else
                    casterAnimator.Play("Idle_Battle");
            }
        }

        // Arrête la timeline d'attente lancée sur le caster si elle existe.
        BattleTimelineManager.Instance?.StopCasterTimeline(currentCharacterUnit);
        itemMenuTimelineActive = false;
    }
    #endregion

    #region Gestion de la navigation dans les menus
    private void BuildOrderedUnits(CharacterType requiredType, List<CharacterUnit> buffer, bool includeDeadUnits)
    {
        buffer.Clear();
        IReadOnlyList<CharacterUnit> sourceUnits = includeDeadUnits ? unitsInBattle : activeCharacterUnits;
        foreach (var unit in sourceUnits)
        {
            if (unit == null || unit.IsPermanentlyDead || unit.characterType != requiredType)
                continue;

            if (!includeDeadUnits && unit.currentHP <= 0)
                continue;

            buffer.Add(unit);
        }

        buffer.Sort(CompareTargetSelectionOrder);
    }

    private bool AllowsDeadUnitTargeting()
    {
        if (currentItem != null)
            return currentItem.usableOnDeathUnits;
        if (currentMove != null)
            return currentMove.usableOnDeathUnits;
        return false;
    }

    private int CompareTargetSelectionOrder(CharacterUnit a, CharacterUnit b)
    {
        int orderA = GetSelectionSlotOrder(a);
        int orderB = GetSelectionSlotOrder(b);

        if (orderA != orderB)
            return orderA.CompareTo(orderB);

        if (a == null || b == null)
            return 0;

        return a.transform.position.x.CompareTo(b.transform.position.x);
    }

    private int GetSelectionSlotOrder(CharacterUnit unit)
    {
        if (unit == null)
            return int.MaxValue;

        Transform parent = unit.transform.parent;
        if (parent != null)
        {
            Transform root = parent.parent;
            bool isSpawnSlot = parent.CompareTag("PlayerSpawn") || parent.CompareTag("EnemySpawn");
            bool isSpawnRoot = root != null && (root.CompareTag("PlayerSpawn") || root.CompareTag("EnemySpawn"));
            if (isSpawnSlot || isSpawnRoot)
            {
                if (TryGetSpawnIndexFromName(parent.name, out int spawnIndex))
                {
                    // selection order left->right: 2,1,3
                    switch (spawnIndex)
                    {
                        case 2:
                            return 0;
                        case 1:
                            return 1;
                        case 3:
                            return 2;
                        default:
                            return spawnIndex;
                    }
                }

                return parent.GetSiblingIndex();
            }
        }

        return int.MaxValue;
    }

    private static bool TryGetSpawnIndexFromName(string spawnName, out int spawnIndex)
    {
        spawnIndex = 0;
        if (string.IsNullOrEmpty(spawnName))
            return false;

        int value = 0;
        int multiplier = 1;
        bool foundDigit = false;

        for (int i = spawnName.Length - 1; i >= 0; i--)
        {
            char c = spawnName[i];
            if (!char.IsDigit(c))
            {
                if (foundDigit)
                    break;
                continue;
            }

            foundDigit = true;
            value += (c - '0') * multiplier;
            multiplier *= 10;
        }

        if (!foundDigit)
            return false;

        spawnIndex = value;
        return true;
    }

    private void HandleTargetNavigation()
    {
        // Si les menus sont verrouillés, aucune navigation ne doit être traitée.
        if (AreBattleMenusLocked)
            return;

        if (BattleCameraManager.Instance != null && BattleCameraManager.Instance.IsFreeCameraActive)
            return;

        bool isSkillTargeting = currentBattleState == BattleState.SquadUnit_TargetSelectionAmongEnemiesForSkill ||
                                currentBattleState == BattleState.SquadUnit_TargetSelectionAmongSquadForSkill ||
                                (currentBattleState == BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnSquad && currentMove != null) ||
                                (currentBattleState == BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnEnemies && currentMove != null);

        bool isItemTargeting = currentBattleState == BattleState.SquadUnit_TargetSelectionAmongEnemiesForItem ||
                               currentBattleState == BattleState.SquadUnit_TargetSelectionAmongSquadForItem ||
                               (currentBattleState == BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnSquad && currentItem != null) ||
                               (currentBattleState == BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnEnemies && currentItem != null);

        if (!isSkillTargeting && !isItemTargeting)
            return;

        TargetType type = isSkillTargeting ? currentMoveTargetType : currentItemTargetType;

        if (type == TargetType.Self || type == TargetType.SpawnPosition)
        {
            currentTargetCharacter = currentCharacterUnit;
            return;
        }

        bool targetEnemies = type == TargetType.SingleEnemy || type == TargetType.AllEnemies;
        CharacterType requiredType = targetEnemies ? CharacterType.EnemyUnit : CharacterType.SquadUnit;

        bool includeDeadUnits = AllowsDeadUnitTargeting();
        BuildOrderedUnits(requiredType, filteredUnits, includeDeadUnits);

        if (filteredUnits.Count == 0)
        {
            return;
        }

        Vector2 input = InputsManager.Instance.playerInputs.Battle.HorizontalNav.ReadValue<Vector2>();
        if (Time.time - lastNavTime < navigationCooldown)
        {
            return;
        }

        int direction = 0;

        if (input.x > 0.5f) direction = 1;
        else if (input.x < -0.5f) direction = -1;

        if (direction == 0) return;

        int count = filteredUnits.Count;
        currentTargetIndex = Mathf.Clamp(currentTargetIndex, 0, count - 1);
        int nextIndex = Mathf.Clamp(currentTargetIndex + direction, 0, count - 1);

        if (nextIndex == currentTargetIndex)
            return;

        lastNavTime = Time.time;
        currentTargetIndex = nextIndex;
        CharacterUnit previousTarget = currentTargetCharacter;
        currentTargetCharacter = filteredUnits[currentTargetIndex];

        if (currentTargetCharacter != previousTarget)
        {
            // Joué à chaque bascule de cible pour informer le joueur du changement de focus.
            PlayMenuClip(targetChangeClip);
        }
    }
    #endregion

    #region Gestion de la navigation parmi les unités en combat

    private void ApplyDamagePopupDisplaySettings()
    {
        if (damagePopupManager == null)
            damagePopupManager = DamagePopupManager.Instance;

        if (damagePopupManager == null)
            return;

        float fontSize = damagePopupFontSize > 0.1f ? damagePopupFontSize : 150f;
        float textScale = damagePopupTextScale > 0.001f ? damagePopupTextScale : 1f;

        damagePopupManager.SetDisplaySettings(
            damagePopupFont,
            fontSize,
            textScale,
            damagePopupOffset,
            damagePopupOffsetUsesCameraAxes,
            damagePopupUsePooling);

        damagePopupManager.SetColorSettings(
            damagePopupDamageColor,
            damagePopupHealColor,
            damagePopupBuffColor,
            damagePopupDebuffColor,
            damagePopupStatusColor,
            damagePopupInterceptionPositiveColor,
            damagePopupInterceptionNegativeColor);
    }

    private void HandleTargetCursor()
    {
        // Protection identique pour le curseur de cible : tant que les menus sont verrouillés,
        // les éléments d'interface restent complètement inactifs.
        if (AreBattleMenusLocked)
            return;

        bool isSkillTargeting =
            currentBattleState == BattleState.SquadUnit_TargetSelectionAmongEnemiesForSkill ||
            currentBattleState == BattleState.SquadUnit_TargetSelectionAmongSquadForSkill ||
            (currentBattleState == BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnSquad && currentMove != null) ||
            (currentBattleState == BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnEnemies && currentMove != null);

        bool isItemTargeting =
            currentBattleState == BattleState.SquadUnit_TargetSelectionAmongEnemiesForItem ||
            currentBattleState == BattleState.SquadUnit_TargetSelectionAmongSquadForItem ||
            (currentBattleState == BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnSquad && currentItem != null) ||
            (currentBattleState == BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnEnemies && currentItem != null);

        if (!(isSkillTargeting || isItemTargeting))
        {
            if (targetCursor != null)
            {
                targetCursor.transform.position = Vector3.zero;
                targetCursor.SetActive(false);
            }
            HideMultiTargetCursors();
            HideTargetCostLabel();
            UpdateTargetCursorColor(true);
            DeactivateTargetCursorCinemachine();
            DeactivateMultiTargetCursorCinemachine();
            return;
        }

        TargetType type = isSkillTargeting ? currentMoveTargetType : currentItemTargetType;
        bool includeDeadUnits = AllowsDeadUnitTargeting();

        if (type == TargetType.AllEnemies || type == TargetType.AllAllies || type == TargetType.All)
        {
            targetCursor?.SetActive(false);
            HideTargetCostLabel();

            // Filtre manuel des cibles pour éviter les allocations LINQ à chaque frame
            CollectMultiTargetUnits(type, multiTargetUnits, includeDeadUnits);

            // S'assure qu'il existe assez de curseurs pour toutes les cibles
            EnsureMultiTargetCursors(multiTargetUnits.Count);

            // Positionne chaque curseur sur l'unité correspondante
            for (int i = 0; i < multiTargetCursors.Count; i++)
            {
                if (i < multiTargetUnits.Count)
                {
                    multiTargetCursors[i].SetActive(true);
                    multiTargetCursors[i].transform.position = multiTargetUnits[i].transform.position;
                }
                else
                {
                    multiTargetCursors[i].SetActive(false);
                }
            }

            UpdateMultiTargetCursorCinemachine(multiTargetUnits);
        }
        else
        {
            DeactivateMultiTargetCursorCinemachine();
            HideMultiTargetCursors();

            if (targetCursor != null && currentTargetCharacter != null)
            {
                targetCursor.SetActive(true);

                if (isSkillTargeting && currentMove != null)
                {
                    // Les attaques spéciales utilisent un décalage en fonction
                    // de la position relative et de la distance de lancement.
                    Vector3 cursorBasePosition = currentTargetCharacter.transform.position;
                    Vector3 targetPosition = cursorBasePosition;
                    bool hasTargetPosition = TryResolveMoveTargetPosition(
                        currentCharacterUnit,
                        currentTargetCharacter,
                        currentMove,
                        out targetPosition);
                    if (!hasTargetPosition)
                        targetPosition = cursorBasePosition;

                    Vector3 offsetDir = Vector3.zero;
                    if (currentMove.relativePosition != RelativePosition.On)
                    {
                        Vector3 referenceDir = Vector3.zero;
                        if (currentCharacterUnit != null)
                        {
                            referenceDir = currentCharacterUnit.transform.position - cursorBasePosition;
                            referenceDir.y = 0f;
                        }

                        if (referenceDir.sqrMagnitude <= 0.0001f)
                        {
                            if (currentTargetCharacter != null)
                                referenceDir = currentTargetCharacter.transform.forward;
                            else if (currentCharacterUnit != null)
                                referenceDir = currentCharacterUnit.transform.forward;
                            referenceDir.y = 0f;
                        }

                        if (referenceDir.sqrMagnitude > 0.0001f)
                            referenceDir = referenceDir.normalized;

                        offsetDir = referenceDir;
                        switch (currentMove.relativePosition)
                        {
                            case RelativePosition.Back:
                                offsetDir = -referenceDir;
                                break;
                            case RelativePosition.Left:
                                offsetDir = -Vector3.Cross(Vector3.up, referenceDir);
                                break;
                            case RelativePosition.Right:
                                offsetDir = Vector3.Cross(Vector3.up, referenceDir);
                                break;
                        }
                    }

                    Vector3 cursorPos = cursorBasePosition + offsetDir * currentMove.castDistance;
                    targetCursor.transform.position = cursorPos;

                    float distance = Vector3.Distance(currentCharacterUnit.transform.position,
                        targetPosition);
                    float maxReach = currentCharacterUnit.currentRange + currentMove.castDistance;
                    float requiredDistance = Mathf.Max(0f, distance - maxReach);
                    bool canReach = hasTargetPosition
                                    && (requiredDistance <= 0.01f
                                        || currentCharacterUnit.CanSpendMovementDistance(requiredDistance));
                    if (IsRepliMove(currentMove))
                        canReach = hasTargetPosition;
                    // Vérifie que la position relative est libre avant d'autoriser l'action.
                    bool hasSpace = hasTargetPosition && HasSpaceForMove(currentCharacterUnit, currentTargetCharacter, currentMove);
                    // Vérifie si la cible possède l'altitude adéquate pour ce mouvement.
                    bool altitudeValid = hasTargetPosition && IsTargetAltitudeValid(currentTargetCharacter, currentMove);
                    // La couleur du curseur devient noire si l'une des conditions n'est pas remplie :
                    // distance, espace disponible ou altitude.
                    UpdateTargetCursorColor(canReach && hasSpace && altitudeValid);
                }
                else if (isItemTargeting)
                {
                    // Les items n'utilisent aucun décalage : le curseur se place
                    // directement sur la cible.
                    targetCursor.transform.position = currentTargetCharacter.transform.position;
                    float distance = Vector3.Distance(currentCharacterUnit.transform.position,
                        currentTargetCharacter.transform.position);
                    float maxReach = currentCharacterUnit.currentRange + currentItem.castDistance;
                    bool inRange = distance <= maxReach;
                    UpdateTargetCursorColor(inRange);
                }
                else
                {
                    targetCursor.transform.position = currentTargetCharacter.transform.position;
                    UpdateTargetCursorColor(true);
                }

                if (isSkillTargeting && currentMove != null)
                    UpdateTargetCostLabel(currentCharacterUnit, currentTargetCharacter, currentMove);
                else
                    HideTargetCostLabel();

                UpdateTargetCursorCinemachine(currentTargetCharacter);
            }
            else
            {
                HideTargetCostLabel();
            }
        }
    }

    public void HandleTargetSelection(MusicalMoveSO move, bool isBasicAttack = false)
    {
        currentMove = move;
        currentItem = null; // on annule la sélection d'item précédente
        currentMoveIsBasicAttack = isBasicAttack;

        TargetType allowedType = move != null ? move.targetType : TargetType.SingleEnemy;
        TargetType defaultType = move != null ? move.defaultTargetType : allowedType;
        bool allowGroupSwitch = AllowsMoveGroupSwitch(allowedType);
        bool includeDeadUnits = move != null && move.usableOnDeathUnits;
        currentMoveTargetType = ResolveInitialMoveTargetType(allowedType, defaultType);
        CharacterUnit defaultTarget = ResolveDefaultMoveTarget(currentCharacterUnit, currentMoveTargetType, defaultType, includeDeadUnits);
        // Feedback audio unique pour signifier le passage en mode ciblage.
        PlayMenuClip(targetSelectionClip);
        switch (currentMoveTargetType)
        {
            case TargetType.Self:
            case TargetType.SpawnPosition:
                ChangeBattleState(allowGroupSwitch
                    ? BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnSquad
                    : BattleState.SquadUnit_TargetSelectionAmongSquadForSkill);
                currentTargetCharacter = currentCharacterUnit;
                break;

            case TargetType.SingleEnemy:
                ChangeBattleState(allowGroupSwitch
                    ? BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnEnemies
                    : BattleState.SquadUnit_TargetSelectionAmongEnemiesForSkill);
                currentTargetCharacter = defaultTarget;
                break;

            case TargetType.AllEnemies:
                ChangeBattleState(BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnEnemies);
                currentTargetCharacter = defaultTarget;
                break;

            case TargetType.SingleAlly:
                ChangeBattleState(allowGroupSwitch
                    ? BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnSquad
                    : BattleState.SquadUnit_TargetSelectionAmongSquadForSkill);
                currentTargetCharacter = defaultTarget;
                break;

            case TargetType.AllAllies:
                ChangeBattleState(BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnSquad);
                currentTargetCharacter = defaultTarget;
                break;

            case TargetType.All:
                ChangeBattleState(BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnEnemies);
                currentTargetCharacter = defaultTarget
                    ?? GetFirstSelectableUnit(CharacterType.EnemyUnit, includeDeadUnits)
                    ?? GetFirstSelectableUnit(CharacterType.SquadUnit, includeDeadUnits);
                break;

            default:
                Debug.LogWarning($"[BattleTurnManager] Type de cible non géré : {currentMoveTargetType}");
                return;
        }
        SyncCurrentTargetIndex(currentTargetCharacter, includeDeadUnits);

        // Affiche l'instruction adaptée selon qu'on peut ou non changer de groupe
        if (currentMoveTargetType == TargetType.SpawnPosition)
            ActionUIDisplayManager.Instance.DisplayInstruction("Se replier");
        else if (allowGroupSwitch)
            ActionUIDisplayManager.Instance.DisplayInstruction_SelectGroup();
        else
            ActionUIDisplayManager.Instance.DisplayInstruction_SelectTarget();

        if (musicalMoveTargetSelectionCameraMotif != null)
            BattleCameraManager.Instance?.SetCameraMotif(musicalMoveTargetSelectionCameraMotif);

        // Des l'entree en mode ciblage, on lance la boucle de preparation
        // definie sur le CharacterData du lanceur. Si elle est absente, on
        // garde le fallback sur l'animation specifique au move.
        if (currentCharacterUnit != null)
        {
            if (!currentCharacterUnit.StartPrepareToTargetAnimation())
                currentCharacterUnit.PlayPreparingAnimation(move?.preparingAnimation);
        }
    }

    public void HandleTargetSelection(ItemData item)
    {
        // Contrairement à l'ancien comportement nous ne coupons plus la timeline de préparation ici :
        // cela permet de conserver l'animation « Item_Prepare » (et les éventuels effets annexes)
        // pendant toute la phase de sélection de cible. La timeline sera arrêtée uniquement lorsque
        // le joueur quittera le contexte de l'objet (validation, retour au menu, etc.).
        currentItem = item;
        currentMove = null; // on annule la sélection de compétence précédente
        currentMoveIsBasicAttack = false; // Sélection d'objet : aucune logique spécifique à l'attaque basique.
        currentItemTargetType = item.defaultTargetType;
        bool includeDeadUnits = item != null && item.usableOnDeathUnits;

        bool canTargetEnemies = item.targetTypes.Contains(TargetType.SingleEnemy) ||
                               item.targetTypes.Contains(TargetType.AllEnemies) ||
                               item.targetTypes.Contains(TargetType.All);
        bool canTargetAllies = item.targetTypes.Contains(TargetType.SingleAlly) ||
                              item.targetTypes.Contains(TargetType.AllAllies) ||
                              item.targetTypes.Contains(TargetType.All);
        bool allowGroupSwitch = canTargetEnemies && canTargetAllies;
        // Même signal sonore que pour les compétences afin de rester cohérent.
        PlayMenuClip(targetSelectionClip);

        switch (item.defaultTargetType)
        {
            case TargetType.Self:
                ChangeBattleState(BattleState.SquadUnit_TargetSelectionAmongSquadForItem);
                currentTargetCharacter = currentCharacterUnit;
                break;

            case TargetType.SingleEnemy:
                if (allowGroupSwitch)
                    ChangeBattleState(BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnEnemies);
                else
                    ChangeBattleState(BattleState.SquadUnit_TargetSelectionAmongEnemiesForItem);
                currentTargetCharacter = GetFirstSelectableUnit(CharacterType.EnemyUnit, includeDeadUnits);
                break;

            case TargetType.AllEnemies:
                ChangeBattleState(BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnEnemies);
                currentTargetCharacter = GetFirstSelectableUnit(CharacterType.EnemyUnit, includeDeadUnits);
                break;

            case TargetType.SingleAlly:
                if (allowGroupSwitch)
                    ChangeBattleState(BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnSquad);
                else
                    ChangeBattleState(BattleState.SquadUnit_TargetSelectionAmongSquadForItem);
                currentTargetCharacter = GetFirstSelectableUnit(CharacterType.SquadUnit, includeDeadUnits);
                break;

            case TargetType.AllAllies:
                ChangeBattleState(BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnSquad);
                currentTargetCharacter = GetFirstSelectableUnit(CharacterType.SquadUnit, includeDeadUnits);
                break;

            case TargetType.All:
                ChangeBattleState(BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnEnemies);
                currentTargetCharacter = GetFirstSelectableUnit(CharacterType.EnemyUnit, includeDeadUnits)
                    ?? GetFirstSelectableUnit(CharacterType.SquadUnit, includeDeadUnits);
                break;

            default:
                Debug.LogWarning($"[BattleTurnManager] Type de cible par défaut non géré : {item.defaultTargetType}");
                return;
        }
        currentTargetIndex = 0;

        if (allowGroupSwitch)
            ActionUIDisplayManager.Instance.DisplayInstruction_SelectGroup();
        else
            ActionUIDisplayManager.Instance.DisplayInstruction_SelectTarget();

        if (itemTargetSelectionCameraMotif != null)
            BattleCameraManager.Instance?.SetCameraMotif(itemTargetSelectionCameraMotif);

        // Force immédiatement la synchronisation de la Cinemachine de ciblage avec la cible présélectionnée.
        // Sans ce rappel explicite, la priorité de la caméra de sélection n'était effective qu'après la
        // prochaine Update, ce qui pouvait laisser la caméra précédente active une frame de trop.
        if (currentItemTargetType == TargetType.AllEnemies
            || currentItemTargetType == TargetType.AllAllies
            || currentItemTargetType == TargetType.All)
        {
            CollectMultiTargetUnits(currentItemTargetType, multiTargetUnits, includeDeadUnits);
            UpdateMultiTargetCursorCinemachine(multiTargetUnits);
        }
        else
        {
            DeactivateMultiTargetCursorCinemachine();
            UpdateTargetCursorCinemachine(currentTargetCharacter);
        }
    }

    /// <summary>
    ///     Prépare la sélection d'une cible pour une attaque de base déclenchée via l'input dédié.
    ///     En pratique, on masque les menus et l'on présélectionne le premier ennemi valide pour
    ///     offrir un flux identique à celui d'une compétence classique.
    /// </summary>
    /// <returns>Retourne <c>true</c> si une cible initiale a été trouvée.</returns>
    public bool TryStartBaseAttackSelection()
    {
        if (!EnsureCurrentCharacterUnitForMenus(nameof(TryStartBaseAttackSelection)))
            return false;

        CharacterUnit caster = currentCharacterUnit;
        if (caster == null)
            return false;

        if (caster.Data == null)
        {
            Debug.LogWarning("[TryStartBaseAttackSelection] CharacterData introuvable pour l'unité courante.");
            return false;
        }

        // Résout l'attaque basique à utiliser pour ce personnage (fallback global compris).
        MusicalMoveSO basicMove = ResolveBasicAttackMove(caster);
        if (basicMove == null)
        {
            ActionUIDisplayManager.Instance?.DisplayInstruction("Attaque basique indisponible");
            return false;
        }

        // Vérifie les coûts et limitations avant de masquer les menus.
        if (caster.GetAvailableHarmonicsForCost(basicMove.consumedHarmonicType) < basicMove.harmonicCost)
        {
            ActionUIDisplayManager.Instance.DisplayInstruction_NotEnoughHarmonics();
            return false;
        }

        if (basicMove.enterAwake &&
            caster.GetHarmonicCount(caster.Data.harmonicType) < caster.Data.awakeHarmonicThreshold)
        {
            ActionUIDisplayManager.Instance.DisplayInstruction_NotEnoughHarmonics();
            return false;
        }

        if (caster.IsMoveOnCooldown(basicMove))
        {
            ActionUIDisplayManager.Instance.DisplayInstruction_MoveOnCooldown();
            return false;
        }

        if (!caster.CanUseMove(basicMove))
        {
            ActionUIDisplayManager.Instance.DisplayInstruction("Limite d'utilisation atteinte");
            return false;
        }

        // Tout est prêt : on masque les menus pour se comporter comme une sélection classique de compétence.
        ToggleMenuContainers(false, false, false);
        HandleTargetSelection(basicMove, isBasicAttack: true);

        if (currentTargetCharacter == null)
        {
            ActionUIDisplayManager.Instance?.DisplayInstruction("Aucun ennemi valide");
            OpenSkillsMenu();
            return false;
        }

        return true;
    }

    /// <summary>
    ///     Repositionne l'unité active sur sa position de spawn du début de combat.
    /// </summary>
    public bool TryUseRepli()
    {
        if (!EnsureCurrentCharacterUnitForMenus(nameof(TryUseRepli)))
            return false;

        CharacterUnit unit = currentCharacterUnit;
        if (unit == null || unit.IsDead)
            return false;

        if (repliMove == null)
        {
            Debug.LogWarning("[TryUseRepli] MusicalMove_Repli non assigné.");
            return false;
        }

        if (unit.Data == null)
        {
            Debug.LogWarning("[TryUseRepli] CharacterData introuvable pour l'unité courante.");
            return false;
        }

        Vector3 spawnPosition = Vector3.zero;
        bool hasSpawnPosition = false;
        if (repliMove.targetType == TargetType.SpawnPosition)
        {
            hasSpawnPosition = TryResolveUnitSpawnPosition(unit, out spawnPosition);
            if (!hasSpawnPosition)
            {
                Debug.LogWarning($"[TryUseRepli] Position de spawn introuvable pour {unit.name}.");
                ActionUIDisplayManager.Instance.DisplayInstruction("Position de spawn introuvable");
                return false;
            }

            const float repliAlreadyAtSpawnDistance = 0.1f;
            Vector3 toSpawn = unit.transform.position - spawnPosition;
            toSpawn.y = 0f;
            if (toSpawn.sqrMagnitude <= repliAlreadyAtSpawnDistance * repliAlreadyAtSpawnDistance)
            {
                ActionUIDisplayManager.Instance.DisplayInstruction("Déjà sur la position de repli");
                return false;
            }
        }

        if (repliMove.onlyAwake && !unit.IsAwake)
        {
            ActionUIDisplayManager.Instance.DisplayInstruction("Compétence indisponible");
            return false;
        }

        if (repliMove.enterAwake && unit.IsAwake)
        {
            ActionUIDisplayManager.Instance.DisplayInstruction("Compétence indisponible");
            return false;
        }

        if (repliMove.enterAwake &&
            unit.GetHarmonicCount(unit.Data.harmonicType) < unit.Data.awakeHarmonicThreshold)
        {
            ActionUIDisplayManager.Instance.DisplayInstruction_NotEnoughHarmonics();
            return false;
        }

        if (unit.IsMoveOnCooldown(repliMove))
        {
            ActionUIDisplayManager.Instance.DisplayInstruction_MoveOnCooldown();
            return false;
        }

        if (!unit.CanUseMove(repliMove))
        {
            ActionUIDisplayManager.Instance.DisplayInstruction("Limite d'utilisation atteinte");
            return false;
        }

        ToggleMenuContainers(false, false, false);
        HandleTargetSelection(repliMove);
        return true;
    }

    private bool IsRepliMove(MusicalMoveSO move)
    {
        return move != null && repliMove != null && move == repliMove;
    }

    public bool TryResolveUnitSpawnPosition(CharacterUnit unit, out Vector3 spawnPosition)
    {
        spawnPosition = Vector3.zero;
        if (unit == null || unit.Data == null)
            return false;

        Transform spawnPoint = null;
        Transform parent = unit.transform.parent;
        if (parent != null)
        {
            Transform root = parent.parent;
            bool isSpawnSlot = parent.CompareTag("PlayerSpawn") || parent.CompareTag("EnemySpawn");
            bool isSpawnRoot = root != null && (root.CompareTag("PlayerSpawn") || root.CompareTag("EnemySpawn"));
            if (isSpawnSlot || isSpawnRoot)
                spawnPoint = parent;
        }

        if (spawnPoint == null)
        {
            List<Transform> spawnList = unit.IsPlayerControlled ? playerSpawnPoints : enemySpawnPoints;
            if (TryGetSpawnIndexFromName(unit.name, out int spawnIndex)
                && spawnIndex >= 0 && spawnIndex < spawnList.Count)
            {
                spawnPoint = spawnList[spawnIndex];
            }
        }

        if (spawnPoint == null)
            return false;

        spawnPosition = ComputeSpawnPosition(unit.Data, spawnPoint);
        return true;
    }

    private bool TryResolveMoveTargetPosition(CharacterUnit caster, CharacterUnit target, MusicalMoveSO move, out Vector3 targetPosition)
    {
        targetPosition = Vector3.zero;

        if (move == null)
            return false;

        if (target != null)
        {
            targetPosition = target.transform.position;
            return true;
        }

        if (move.targetType == TargetType.SpawnPosition && caster != null)
            return TryResolveUnitSpawnPosition(caster, out targetPosition);

        return false;
    }

    /// <summary>
    ///     Valide la sélection de cible et applique les dégâts de l'attaque basique du personnage actif.
    ///     Un message utilisateur est affiché en cas d'échec (hors portée, données manquantes...).
    /// </summary>
    /// <returns><c>true</c> si l'attaque a été exécutée.</returns>
    public bool ConfirmBaseAttack()
    {
        CharacterUnit caster = currentCharacterUnit;
        CharacterUnit target = currentTargetCharacter;

        if (caster == null || target == null)
        {
            Debug.LogWarning("[ConfirmBaseAttack] Lanceur ou cible manquant : réouverture du SkillsMenu.");
            OpenSkillsMenu();
            return false;
        }

        if (caster.Data == null)
        {
            Debug.LogWarning("[ConfirmBaseAttack] CharacterData introuvable sur le lanceur.");
            OpenSkillsMenu();
            return false;
        }

        float distance = Vector3.Distance(caster.transform.position, target.transform.position);
        float maxReach = caster.currentRange;
        if (distance > maxReach)
        {
            ActionUIDisplayManager.Instance?.DisplayInstruction_TargetTooFar();
            return false;
        }

        ChangeBattleState(BattleState.SquadUnit_PerformingBaseAttack);
        ToggleMenuContainers(false, false, false);

        bool success = ExecuteBaseAttack(caster, target, displayErrors: true);
        if (!success)
        {
            OpenSkillsMenu();
            return false;
        }

        // L'attaque basique représente l'action principale du tour : on clôt donc immédiatement celui-ci.
        EndTurn();
        return true;
    }

    /// <summary>
    ///     Calcule et applique les dégâts de l'attaque de base d'une unité.
    /// </summary>
    /// <param name="attacker">Lanceur de l'attaque.</param>
    /// <param name="target">Cible qui reçoit les dégâts.</param>
    /// <param name="displayErrors">Affiche les messages d'erreur utilisateur si nécessaire.</param>
    /// <param name="registerStats">Enregistre les dégâts pour le suivi du tour et des statistiques.</param>
    /// <param name="applyFatigue">Déclenche le système de fatigue associé, utile pour Thalia.</param>
    /// <returns><c>true</c> si l'attaque a été effectuée.</returns>
    public bool ExecuteBaseAttack(CharacterUnit attacker, CharacterUnit target, bool displayErrors,
        bool registerStats = true, bool applyFatigue = true)
    {
        if (attacker == null || target == null)
        {
            if (displayErrors)
                Debug.LogWarning("[ExecuteBaseAttack] Lanceur ou cible manquant.");
            return false;
        }

        if (attacker.Data == null)
        {
            if (displayErrors)
                Debug.LogWarning("[ExecuteBaseAttack] CharacterData absent sur le lanceur.");
            return false;
        }

        if (target.currentHP <= 0f)
        {
            if (displayErrors)
                Debug.LogWarning("[ExecuteBaseAttack] La cible est déjà hors combat.");
            return false;
        }

        MusicalMoveSO basicMove = ResolveBasicAttackMove(attacker);
        if (basicMove == null)
        {
            if (displayErrors)
                Debug.LogWarning("[ExecuteBaseAttack] Aucun MusicalMove d'attaque basique n'est disponible.");
            return false;
        }

        float distance = Vector3.Distance(attacker.transform.position, target.transform.position);
        float maxReach = attacker.currentRange + basicMove.castDistance;
        if (distance > maxReach)
        {
            if (displayErrors)
                ActionUIDisplayManager.Instance?.DisplayInstruction_TargetTooFar();
            return false;
        }

        OrientUnitTowardTarget(attacker, target);

        // La résolution passe désormais par le MusicalMove afin de profiter des timelines,
        // effets secondaires et registres de dégâts déjà implémentés. On transmet toutefois
        // les options historiques (fatigue, statistiques) pour conserver le même contrat public.
        PlayBaseAttackVoice(attacker); // doit précéder la timeline Performing du move
        bool ignoreFatigue = !applyFatigue;
        bool skipDamageRegistration = !registerStats;
        MusicalMoveExecutor.ApplyEffect(basicMove, attacker, target, false, ignoreFatigue, skipDamageRegistration);
        OrientUnitTowardClosestOpponent(attacker);

        return true;
    }
    #endregion

    #region Gestion des mouvements de la caméra de combat
    /// <summary>
    /// Met à jour le contexte courant pour que <see cref="BattleCameraManager"/> repositionne correctement
    /// les Cinemachine.
    /// </summary>
    private void UpdateCameraContext(CharacterUnit caster, CharacterUnit target)
    {
        var manager = BattleCameraManager.Instance;
        if (manager == null)
            return;

        if (caster != null)
            manager.SetTurnOwner(caster);

        manager.SetCurrentTarget(target);
    }

    /// <summary>
    /// Active une caméra nommée uniquement si elle n'est pas déjà prioritaire.
    /// </summary>
    private void RequestCamera(string cameraName, float blendDuration, CinemachineBlendDefinition.Styles blendStyle)
    {
        var manager = BattleCameraManager.Instance;
        if (manager == null)
            return;

        if (manager.CurrentCinemachineCameraName == cameraName)
            return;

        manager.SwitchToCamera(cameraName, blendDuration, blendStyle);
    }

    public void UpdateCameraBehaviour(BattleState newState)
    {
        var manager = BattleCameraManager.Instance;
        if (manager == null)
            return;

        bool stateChanged = newState != lastCameraEvaluatedState;
        lastCameraEvaluatedState = newState;

        CharacterUnit caster = currentCharacterUnit;
        CharacterUnit target = currentTargetCharacter;

        UpdateCameraContext(caster, target);

        bool isActionState = newState == BattleState.SquadUnit_PerformingMusicalMove
                             || newState == BattleState.SquadUnit_PerformingBaseAttack
                             || newState == BattleState.SquadUnit_Item_Use
                             || newState == BattleState.EnemyUnit_PerformingMusicalMove
                             || newState == BattleState.EnemyUnit_Item_Use;
        if (stateChanged && !isActionState)
        {
            bool keepMoveMotif = currentMove != null && currentMove.cameraMotif != null;
            if (!keepMoveMotif)
                manager.ClearCameraMotif();
        }

        switch (newState)
        {
            case BattleState.SquadUnit_MainMenu:
                if (stateChanged)
                {
                    manager.SetCurrentTarget(null);
                    RequestCamera(MainMenuCameraName, MenuCameraBlendDuration, MenuCameraBlendStyle);
                }
                if (battleMainMenuCameraMotif != null)
                    manager.SetCameraMotif(battleMainMenuCameraMotif);
                break;

            case BattleState.SquadUnit_SkillsMenu:
                if (stateChanged)
                {
                    manager.SetCurrentTarget(null);
                    RequestCamera(SkillsMenuCameraName, MenuCameraBlendDuration, MenuCameraBlendStyle);
                }
                break;

            case BattleState.SquadUnit_ItemsMenu:
            case BattleState.SquadUnit_Item_Prepare:
                if (stateChanged)
                {
                    manager.SetCurrentTarget(null);
                    RequestCamera(ItemsMenuCameraName, MenuCameraBlendDuration, MenuCameraBlendStyle);
                }
                break;

            case BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnSquad:
            case BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnEnemies:
            case BattleState.SquadUnit_TargetSelectionAmongSquadForSkill:
            case BattleState.SquadUnit_TargetSelectionAmongSquadForItem:
            case BattleState.SquadUnit_TargetSelectionAmongEnemiesForSkill:
            case BattleState.SquadUnit_TargetSelectionAmongEnemiesForItem:
            case BattleState.SquadUnit_TargetSelectionForBaseAttack:
                if (stateChanged)
                {
                    // On active la Cinemachine appropriée en distinguant les cibles d'objet
                    // des attaques classiques.
                    string cameraName = ResolveTargetSelectionCameraName(newState);
                    RequestCamera(cameraName, ContextCameraBlendDuration, ContextCameraBlendStyle);
                    if (IsItemTargetSelectionState(newState) && itemTargetSelectionCameraMotif != null)
                        manager.SetCameraMotif(itemTargetSelectionCameraMotif);
                    else if (IsMusicalMoveTargetSelectionState(newState) && musicalMoveTargetSelectionCameraMotif != null)
                        manager.SetCameraMotif(musicalMoveTargetSelectionCameraMotif);
                }
                break;

            case BattleState.SquadUnit_PerformingMusicalMove:
            case BattleState.SquadUnit_PerformingBaseAttack:
            case BattleState.SquadUnit_Item_Use:
            case BattleState.EnemyUnit_PerformingMusicalMove:
            case BattleState.EnemyUnit_Item_Use:
                manager.ConfigureActionTargets(caster, target);
                break;

            case BattleState.EnemyUnit_Reflexion:
                if (stateChanged)
                {
                    manager.SetCurrentTarget(target);
                    RequestCamera(MainMenuCameraName, MenuCameraBlendDuration, MenuCameraBlendStyle);
                }
                break;

            case BattleState.EnemyUnit_Item_Prepare:
                if (stateChanged)
                {
                    manager.SetCurrentTarget(null);
                    RequestCamera(ItemsMenuCameraName, MenuCameraBlendDuration, MenuCameraBlendStyle);
                }
                break;

            case BattleState.VictoryScreen_Await:
                if (stateChanged)
                    RequestCamera(VictoryCameraName, MenuCameraBlendDuration, MenuCameraBlendStyle);
                break;

            default:
                if (stateChanged)
                    RequestCamera(MainMenuCameraName, ContextCameraBlendDuration, ContextCameraBlendStyle);
                break;
        }
    }

    #endregion

    #region Méthodes utilitaires

    private Transform FindChildRecursive(Transform parent, string targetName)
    {
        if (parent.name == targetName)
            return parent;

        foreach (Transform child in parent)
        {
            Transform result = FindChildRecursive(child, targetName);
            if (result != null)
                return result;
        }
        return null;
    }

    private Sprite GetInputSprite(int index)
    {
        return index switch
        {
            0 => inputSprite1,
            1 => inputSprite2,
            2 => inputSprite3,
            _ => null,
        };
    }

    /// <summary>
    /// Joue un clip lié aux menus de combat via l'AudioManager.
    /// Les garde-fous évitent toute exception lorsque l'audio n'est pas encore initialisé
    /// (ex : scènes de test sans gestionnaire global).
    /// </summary>
    /// <param name="clip">Clip à jouer immédiatement.</param>
    private void PlayMenuClip(AudioClipSO clip)
    {
        if (clip == null)
            return; // Aucun son configuré pour cet évènement.

        AudioManager audioManager = AudioManager.Instance;
        if (audioManager == null)
            return; // Le gestionnaire peut être absent dans certaines scènes d'édition.

        audioManager.PlaySfx(clip);
    }

    /// <summary>
    /// Déclenche l'éventuel clip personnalisé associé au personnage actuellement sélectionné.
    /// Chaque unité peut fournir son propre son via <see cref="CharacterData.menuSelectionClip"/> ;
    /// un fallback global est utilisé si aucun clip n'est défini pour garantir un feedback sonore.
    /// </summary>
    /// <param name="unit">Unité qui vient d'être mise en avant dans les menus.</param>
    private void PlayCharacterSelectionClip(CharacterUnit unit)
    {
        if (unit == null)
            return; // Sécurité : aucun personnage actif, donc aucun son à jouer.

        CharacterData data = unit.Data;
        AudioClipSO clipToPlay = data != null && data.menuSelectionClip != null
            ? data.menuSelectionClip
            : defaultCharacterSelectionClip;

        PlayMenuClip(clipToPlay);
    }

    public void ChangeBattleState(BattleState newState)
    {
        BattleState previousState = currentBattleState;
        currentBattleState = newState;
        Debug.Log("Nouvel état de combat: " + newState);
        if (IsTargetSelectionState(previousState) && !IsTargetSelectionState(newState))
            currentCharacterUnit?.StopPrepareToTargetAnimation();
        UpdateCameraBehaviour(newState);
    }

    /// <summary>
    /// Rend public le changement d'unité active pour permettre aux autres systèmes (UI, caméras, timelines)
    /// de réagir immédiatement à la nouvelle sélection.
    /// </summary>
    public void ChangeCurrentCharacterUnit(CharacterUnit newCurrentCharacterUnit)
    {
        bool unitChanged = currentCharacterUnit != newCurrentCharacterUnit;

        if (!unitChanged)
            return; // Aucun changement : on évite les répétitions sonores inutiles.

        currentCharacterUnit = newCurrentCharacterUnit;

        // Informe immédiatement le statut Presto pour savoir si le lanceur vient de rejouer.
        PrestoForcedAttackSystem.HandleActiveUnitChanged(currentCharacterUnit);

        if (currentCharacterUnit == null)
            return; // Parfois utilisé lors des resets de combat : aucune unité active.

        PlayCharacterSelectionClip(currentCharacterUnit);
    }

    private void EnsureBattleCamera()
    {
        if (battleCamera == null)
        {
            battleCamera = GameObject.FindGameObjectWithTag("BattleCamera");
        }
    }

    /// <summary>
    /// Récupère le canvas utilisé par la caméra de combat afin d'y ancrer les éléments de feedback.
    /// On privilégie une référence configurée dans l'inspecteur mais on prévoit un fallback automatique
    /// pour préserver la compatibilité avec les scènes existantes.
    /// </summary>
    private void EnsureBattleCameraCanvas()
    {
        if (battleCameraCanvasTransform != null)
        {
            return; // Une référence manuelle a peut-être été fournie dans l'inspecteur.
        }

        // Première tentative : on exploite la hiérarchie de la BattleCamera si elle est connue.
        if (battleCamera == null)
        {
            EnsureBattleCamera();
        }

        if (battleCamera != null)
        {
            Canvas[] canvases = battleCamera.GetComponentsInChildren<Canvas>(true);
            foreach (Canvas canvas in canvases)
            {
                if (canvas != null && canvas.name == "BattleCameraCanvas")
                {
                    battleCameraCanvasTransform = canvas.transform;
                    return;
                }
            }
        }

        // Deuxième tentative : recherche directe par nom, adaptée aux scènes déjà configurées.
        GameObject canvasGO = GameObject.Find("BattleCameraCanvas");
        if (canvasGO != null)
        {
            battleCameraCanvasTransform = canvasGO.transform;
            return;
        }

        Debug.LogWarning("[NewBattleManager] Impossible de localiser 'BattleCameraCanvas'. L'indicateur de portée ne sera pas instancié.");
    }

    void EnsureTargetCursor()
    {
        // Si un curseur existe déjà et n'a pas été détruit par ResetBattleInfos(),
        // on évite d'en instancier un doublon. Sans cette vérification, un clone
        // supplémentaire apparaissait au lancement du jeu puis du combat, car
        // EnsureTargetCursor() est appelé dans Start() puis à nouveau dans StartBattle().
        if (targetCursor != null)
        {
            EnsureTargetCostLabel();
            return;
        }

        if (targetCursorPrefab != null)
        {
            // Instanciation « lazy » : on ne crée le visuel que lorsque c'est nécessaire.
            // L'objet est immédiatement désactivé car il ne sera affiché qu'une fois
            // la sélection de cible active.
            targetCursor = Instantiate(targetCursorPrefab, transform.position, Quaternion.identity);
            targetCursor.SetActive(false);
        }

        EnsureTargetCostLabel();
    }

    private void EnsureTargetCostLabel()
    {
        if (targetCostLabel != null || targetCursor == null)
            return;

        Transform existingLabel = targetCursor.transform.Find("TargetCostLabel");
        if (existingLabel != null && existingLabel.TryGetComponent(out TMP_Text existingText))
            targetCostLabel = existingText;
        if (targetCostLabel == null)
        {
            GameObject labelObject = new GameObject("TargetCostLabel");
            labelObject.transform.SetParent(targetCursor.transform, false);
            targetCostLabel = labelObject.AddComponent<TextMeshPro>();
            targetCostLabel.alignment = TextAlignmentOptions.Left;
            targetCostLabel.enableWordWrapping = false;
        }

        targetCostLabel.gameObject.layer = targetCursor.layer;
        if (defaultTargetCostLabelFont == null)
            defaultTargetCostLabelFont = targetCostLabel.font;
        targetCostLabel.color = targetCostLabelColor;
        targetCostLabel.gameObject.SetActive(false);
    }

    private void HideTargetCostLabel()
    {
        if (targetCostLabel != null)
            targetCostLabel.gameObject.SetActive(false);
    }

    private float ComputeMoveMovementCost(CharacterUnit caster, CharacterUnit target, MusicalMoveSO move)
    {
        if (caster == null || move == null)
            return 0f;

        if (IsRepliMove(move))
            return 0f;

        if (!TryResolveMoveTargetPosition(caster, target, move, out Vector3 targetPosition))
            return 0f;

        float distance = Vector3.Distance(caster.transform.position, targetPosition);
        float maxReach = caster.currentRange + move.castDistance;
        return Mathf.Max(0f, distance - maxReach);
    }

    private void UpdateTargetCostLabel(CharacterUnit caster, CharacterUnit target, MusicalMoveSO move)
    {
        if (targetCursor == null || move == null)
        {
            HideTargetCostLabel();
            return;
        }

        EnsureTargetCostLabel();
        if (targetCostLabel == null)
            return;

        targetCostLabel.fontSize = Mathf.Max(0.1f, targetCostLabelFontSize);
        targetCostLabel.color = targetCostLabelColor;
        if (targetCostLabelFont != null)
            targetCostLabel.font = targetCostLabelFont;
        else if (defaultTargetCostLabelFont != null)
            targetCostLabel.font = defaultTargetCostLabelFont;
        targetCostLabel.transform.localScale = Vector3.one * Mathf.Max(0.001f, targetCostLabelScale);

        float movementCost = ComputeMoveMovementCost(caster, target, move);
        int harmonicCost = IsRepliMove(move) ? 0 : move.harmonicCost;
        string harmonicInfo = $"Harmoniques : {harmonicCost}";
        string movementInfo = $"Mouvements : {movementCost:0.#}";
        targetCostLabel.text = $"{harmonicInfo}\n{movementInfo}";

        if (!targetCostLabel.gameObject.activeSelf)
            targetCostLabel.gameObject.SetActive(true);

        Transform cameraRig = ResolveBattleCameraRig();
        if (target != null)
        {
            Bounds bounds = target.GetVisualBounds();
            Vector3 anchor = bounds.center;
            Vector3 offset = targetCostLabelOffset;
            if (cameraRig != null)
            {
                offset = cameraRig.right * targetCostLabelOffset.x
                         + cameraRig.up * targetCostLabelOffset.y
                         + cameraRig.forward * targetCostLabelOffset.z;
            }
            targetCostLabel.transform.position = anchor + offset;
        }

        if (cameraRig != null)
        {
            Vector3 toCamera = targetCostLabel.transform.position - cameraRig.position;
            if (toCamera.sqrMagnitude > 0.0001f)
                targetCostLabel.transform.rotation = Quaternion.LookRotation(toCamera, Vector3.up);
        }
    }

    void EnsureMultiTargetCursors(int count)
    {
        if (targetCursorPrefab == null) return;

        while (multiTargetCursors.Count < count)
        {
            GameObject cursor = Instantiate(targetCursorPrefab, transform.position, Quaternion.identity);
            cursor.SetActive(false);
            multiTargetCursors.Add(cursor);
        }

        for (int i = multiTargetCursors.Count - 1; i >= count; i--)
        {
            Destroy(multiTargetCursors[i]);
            multiTargetCursors.RemoveAt(i);
        }
    }

    void HideMultiTargetCursors()
    {
        foreach (var cursor in multiTargetCursors)
        {
            if (cursor != null)
            {
                cursor.SetActive(false);
            }
        }
    }

    void UpdateTargetCursorColor(bool inRange)
    {
        if (targetCursor == null) return;

        ParticleSystem[] systems = targetCursor.GetComponentsInChildren<ParticleSystem>();
        foreach (var ps in systems)
        {
            var main = ps.main;
            // Blanc lorsque la cible est valide, bleu lorsqu'elle ne peut être touchée
            // (hors de portée, mauvaise altitude, etc.).
            main.startColor = inRange ? Color.white : Color.blue;
        }
    }

    /// <summary>
    /// Libère les informations caméra dédiées au suivi du curseur de cible.
    /// On remet ainsi la main au gestionnaire général pour éviter que la
    /// Cinemachine ne reste verrouillée sur une cible périmée.
    /// </summary>
    private void DeactivateTargetCursorCinemachine()
    {
        var manager = BattleCameraManager.Instance;
        if (manager == null)
            return; // Aucun gestionnaire actif : aucune action requise.

        // Si aucune Cinemachine de ciblage n'était configurée, on évite un
        // rafraîchissement coûteux et on sort immédiatement.
        if (!isTargetCursorCinemachineActive && lastTargetCursorCameraTarget == null)
            return;

        isTargetCursorCinemachineActive = false;
        lastTargetCursorCameraTarget = null;

        // ConfigureActionTargets remettra la cible à null tout en conservant
        // le lanceur actuel comme référence. Cela garantit que les caméras de
        // menu continuent d'utiliser l'unité active.
        if (currentCharacterUnit != null)
            manager.ConfigureActionTargets(currentCharacterUnit, null);
        else
            manager.ClearRigTargets();

        // On notifie également explicitement que plus aucune cible n'est suivie
        // afin que les caméras dépendantes du LookAt puissent relâcher leur focus.
        manager.SetCurrentTarget(null);
    }

    /// <summary>
    /// Libère le mode multi-cibles afin de revenir au suivi standard.
    /// </summary>
    private void DeactivateMultiTargetCursorCinemachine()
    {
        if (!isMultiTargetCursorCinemachineActive)
            return;

        isMultiTargetCursorCinemachineActive = false;
        lastMultiTargetCameraName = null;

    }

    /// <summary>
    /// Informe la <see cref="BattleCameraManager"/> de la cible suivie par le
    /// curseur afin que la Cinemachine adaptée prenne le relais et encadre la
    /// scène. Les multiples garde-fous évitent les appels redondants coûteux.
    /// </summary>
    /// <param name="target">Unité actuellement survolée par le curseur.</param>
    private void UpdateTargetCursorCinemachine(CharacterUnit target)
    {
        var manager = BattleCameraManager.Instance;
        if (manager == null)
            return; // Sans gestionnaire, on ne tente aucune synchronisation caméra.

        if (target == null)
        {
            // En absence de cible (ciblage multiple ou sortie du mode de sélection),
            // on relâche immédiatement la caméra de ciblage.
            DeactivateTargetCursorCinemachine();
            return;
        }

        // Si la Cinemachine suit déjà cette cible,
        // il est inutile de déclencher un nouveau rafraîchissement.
        // On identifie la caméra de ciblage qui devrait être active (item ou compétence).
        string expectedCameraName = ResolveTargetSelectionCameraName(currentBattleState);

        bool alreadyTracking = isTargetCursorCinemachineActive
                               && lastTargetCursorCameraTarget == target
                               && string.Equals(manager.CurrentCinemachineCameraName, expectedCameraName, System.StringComparison.OrdinalIgnoreCase);
        if (alreadyTracking)
            return;

        lastTargetCursorCameraTarget = target;
        isTargetCursorCinemachineActive = true;

        // On délègue la configuration caster/target au gestionnaire central
        // pour que toutes les Cinemachine restent cohérentes.
        manager.ConfigureActionTargets(currentCharacterUnit, target);

        // Mise à jour de la cible suivie pour les caméras qui n'utilisent pas
        // les overrides directs mais se basent sur le contexte courant.
        manager.SetCurrentTarget(target);

        // On s'assure enfin que la Cinemachine dédiée à la sélection est prioritaire
        // durant les phases de ciblage afin d'offrir un retour visuel clair.
        if (IsTargetSelectionState(currentBattleState))
        {
            RequestCamera(expectedCameraName, ContextCameraBlendDuration, ContextCameraBlendStyle);
        }
    }

    /// <summary>
    /// Synchronise la caméra de ciblage pour les sélections multi-cibles.
    /// </summary>
    private void UpdateMultiTargetCursorCinemachine(IReadOnlyList<CharacterUnit> targets)
    {
        var manager = BattleCameraManager.Instance;
        if (manager == null)
            return;

        string expectedCameraName = ResolveTargetSelectionCameraName(currentBattleState);

        if (!isMultiTargetCursorCinemachineActive
            || !string.Equals(lastMultiTargetCameraName, expectedCameraName, System.StringComparison.OrdinalIgnoreCase))
        {
            isMultiTargetCursorCinemachineActive = true;
            lastMultiTargetCameraName = expectedCameraName;
            isTargetCursorCinemachineActive = false;
            lastTargetCursorCameraTarget = null;

            manager.ConfigureActionTargets(currentCharacterUnit, null);
            manager.SetCurrentTarget(null);

            if (IsTargetSelectionState(currentBattleState))
                RequestCamera(expectedCameraName, ContextCameraBlendDuration, ContextCameraBlendStyle);
        }

    }

    /// <summary>
    /// Indique si l'état donné correspond à une phase de sélection de cible.
    /// Permet de conditionner certains comportements (orientation, caméra...).
    /// </summary>
    private bool IsTargetSelectionState(BattleState state)
    {
        return state == BattleState.SquadUnit_TargetSelectionAmongEnemiesForSkill
               || state == BattleState.SquadUnit_TargetSelectionAmongEnemiesForItem
               || state == BattleState.SquadUnit_TargetSelectionAmongSquadForSkill
               || state == BattleState.SquadUnit_TargetSelectionAmongSquadForItem
               || state == BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnSquad
               || state == BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnEnemies;
    }

    /// <summary>
    /// Détermine si la sélection de cible actuelle est déclenchée par un objet.
    /// Cette information permet de prioriser la Cinemachine de ciblage adaptée.
    /// </summary>
    private bool IsItemTargetSelectionState(BattleState state)
    {
        return state == BattleState.SquadUnit_TargetSelectionAmongEnemiesForItem
               || state == BattleState.SquadUnit_TargetSelectionAmongSquadForItem
               || ((state == BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnSquad
                    || state == BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnEnemies)
                   && currentItem != null
                   && currentMove == null);
    }

    /// <summary>
    /// Détermine si la sélection de cible actuelle est déclenchée par un MusicalMove.
    /// </summary>
    private bool IsMusicalMoveTargetSelectionState(BattleState state)
    {
        if (state == BattleState.SquadUnit_TargetSelectionForBaseAttack)
            return true;

        return IsTargetSelectionState(state)
               && !IsItemTargetSelectionState(state)
               && currentMove != null
               && currentItem == null;
    }

    /// <summary>
    /// Choisit dynamiquement la Cinemachine de ciblage à activer en fonction du contexte.
    /// </summary>
    private string ResolveTargetSelectionCameraName(BattleState state)
    {
        return IsItemTargetSelectionState(state)
            ? ItemTargetSelectionCameraName
            : TargetSelectionCameraName;
    }

    private void CollectMultiTargetUnits(TargetType type, List<CharacterUnit> buffer, bool includeDeadUnits)
    {
        if (type == TargetType.AllEnemies)
        {
            BuildOrderedUnits(CharacterType.EnemyUnit, buffer, includeDeadUnits);
            return;
        }

        if (type == TargetType.AllAllies)
        {
            BuildOrderedUnits(CharacterType.SquadUnit, buffer, includeDeadUnits);
            return;
        }

        buffer.Clear();
        IReadOnlyList<CharacterUnit> sourceUnits = includeDeadUnits ? unitsInBattle : activeCharacterUnits;
        foreach (var unit in sourceUnits)
        {
            if (unit == null || unit.IsPermanentlyDead)
                continue;

            if (!includeDeadUnits && unit.currentHP <= 0)
                continue;

            // On conserve uniquement les types d'unités attendus
            if (type == TargetType.AllEnemies && unit.characterType != CharacterType.EnemyUnit)
                continue;
            if (type == TargetType.AllAllies && unit.characterType != CharacterType.SquadUnit)
                continue;

            buffer.Add(unit);
        }
    }

    public void SetCurrentTargetToFirst(CharacterType type)
    {
        currentTargetIndex = 0;
        bool includeDeadUnits = AllowsDeadUnitTargeting();
        currentTargetCharacter = GetFirstSelectableUnit(type, includeDeadUnits);
    }

    public bool IsCurrentItemMultiTargetSelection()
    {
        return currentItemTargetType == TargetType.AllEnemies
               || currentItemTargetType == TargetType.AllAllies
               || currentItemTargetType == TargetType.All;
    }

    public List<CharacterUnit> ResolveItemTargetsForCurrentSelection()
    {
        List<CharacterUnit> targets = new();

        if (currentItem == null)
            return targets;

        bool includeDeadUnits = currentItem.usableOnDeathUnits;

        if (IsCurrentItemMultiTargetSelection())
        {
            CollectMultiTargetUnits(currentItemTargetType, targets, includeDeadUnits);
            return targets;
        }

        CharacterUnit target = currentTargetCharacter;
        if (target == null || (!includeDeadUnits && target.currentHP <= 0))
        {
            switch (currentItemTargetType)
            {
                case TargetType.Self:
                    target = currentCharacterUnit;
                    break;
                case TargetType.SingleEnemy:
                    target = GetFirstSelectableUnit(CharacterType.EnemyUnit, includeDeadUnits);
                    break;
                case TargetType.SingleAlly:
                    target = GetFirstSelectableUnit(CharacterType.SquadUnit, includeDeadUnits);
                    break;
                case TargetType.SingleAllyOrEnemy:
                    target = GetFirstSelectableUnit(CharacterType.EnemyUnit, includeDeadUnits)
                        ?? GetFirstSelectableUnit(CharacterType.SquadUnit, includeDeadUnits);
                    break;
                case TargetType.All:
                    target = GetFirstSelectableUnit(CharacterType.EnemyUnit, includeDeadUnits)
                        ?? GetFirstSelectableUnit(CharacterType.SquadUnit, includeDeadUnits);
                    break;
            }
        }

        if (target != null && (includeDeadUnits || target.currentHP > 0))
            targets.Add(target);

        return targets;
    }

    public void ApplyMoveTargetSelection(TargetType selectionType)
    {
        currentMoveTargetType = selectionType;
        if (currentMove == null)
            return;

        bool includeDeadUnits = currentMove.usableOnDeathUnits;
        currentTargetCharacter = ResolveDefaultMoveTarget(currentCharacterUnit, selectionType, currentMove.defaultTargetType, includeDeadUnits);
        SyncCurrentTargetIndex(currentTargetCharacter, includeDeadUnits);
    }

    private CharacterUnit GetFirstActiveUnit(CharacterType type)
    {
        BuildOrderedUnits(type, filteredUnits, includeDeadUnits: false);
        return filteredUnits.Count > 0 ? filteredUnits[0] : null;
    }

    private CharacterUnit GetFirstSelectableUnit(CharacterType type, bool includeDeadUnits)
    {
        BuildOrderedUnits(type, filteredUnits, includeDeadUnits);
        return filteredUnits.Count > 0 ? filteredUnits[0] : null;
    }

    private bool AllowsMoveGroupSwitch(TargetType allowedType)
    {
        return allowedType == TargetType.SingleAllyOrEnemy
               || allowedType == TargetType.AllEnemies
               || allowedType == TargetType.AllAllies;
    }

    private TargetType ResolveInitialMoveTargetType(TargetType allowedType, TargetType defaultType)
    {
        switch (allowedType)
        {
            case TargetType.SingleAllyOrEnemy:
                if (defaultType == TargetType.SingleAlly || defaultType == TargetType.AllAllies || defaultType == TargetType.Self)
                    return TargetType.SingleAlly;
                if (defaultType == TargetType.SingleEnemy || defaultType == TargetType.AllEnemies)
                    return TargetType.SingleEnemy;
                return TargetType.SingleEnemy;
            case TargetType.AllEnemies:
            case TargetType.AllAllies:
                if (defaultType == TargetType.AllEnemies || defaultType == TargetType.AllAllies)
                    return defaultType;
                if (defaultType == TargetType.SingleEnemy)
                    return TargetType.AllEnemies;
                if (defaultType == TargetType.SingleAlly || defaultType == TargetType.Self)
                    return TargetType.AllAllies;
                return allowedType;
            case TargetType.All:
                return TargetType.All;
            case TargetType.SpawnPosition:
                return TargetType.SpawnPosition;
            default:
                return allowedType;
        }
    }

    private CharacterUnit ResolveDefaultMoveTarget(CharacterUnit caster, TargetType selectionType, TargetType defaultType,
        bool includeDeadUnits)
    {
        if (caster == null)
            return null;

        switch (selectionType)
        {
            case TargetType.Self:
            case TargetType.SpawnPosition:
                return caster;
            case TargetType.SingleEnemy:
            case TargetType.AllEnemies:
                return GetFirstSelectableUnit(CharacterType.EnemyUnit, includeDeadUnits);
            case TargetType.SingleAlly:
            case TargetType.AllAllies:
                if (defaultType == TargetType.Self && caster.currentHP > 0f)
                    return caster;
                return GetFirstSelectableUnit(CharacterType.SquadUnit, includeDeadUnits);
            case TargetType.All:
                if (defaultType == TargetType.Self && caster.currentHP > 0f)
                    return caster;
                if (defaultType == TargetType.SingleEnemy || defaultType == TargetType.AllEnemies)
                    return GetFirstSelectableUnit(CharacterType.EnemyUnit, includeDeadUnits)
                        ?? GetFirstSelectableUnit(CharacterType.SquadUnit, includeDeadUnits)
                        ?? caster;
                if (defaultType == TargetType.SingleAlly || defaultType == TargetType.AllAllies)
                    return GetFirstSelectableUnit(CharacterType.SquadUnit, includeDeadUnits)
                        ?? GetFirstSelectableUnit(CharacterType.EnemyUnit, includeDeadUnits)
                        ?? caster;
                return GetFirstSelectableUnit(CharacterType.EnemyUnit, includeDeadUnits)
                    ?? GetFirstSelectableUnit(CharacterType.SquadUnit, includeDeadUnits)
                    ?? caster;
            default:
                return GetFirstSelectableUnit(CharacterType.EnemyUnit, includeDeadUnits);
        }
    }

    private void SyncCurrentTargetIndex(CharacterUnit target, bool includeDeadUnits)
    {
        if (target == null)
        {
            currentTargetIndex = 0;
            return;
        }

        BuildOrderedUnits(target.characterType, filteredUnits, includeDeadUnits);
        for (int i = 0; i < filteredUnits.Count; i++)
        {
            if (filteredUnits[i] == target)
            {
                currentTargetIndex = i;
                return;
            }
        }

        currentTargetIndex = 0;
    }

    public void ResetBattleInfos()
    {
        // Réinitialise l’état du combat
        ChangeBattleState(BattleState.None);

        BattleCameraManager.Instance?.ClearCameraMotif();

        // Supprime toute trace d'un éventuel verrou d'introduction pour la prochaine rencontre.
        battleIntroMenusLocked = false;

        // Nettoie toute pause logique résiduelle.
        ResetBattlePauseState();

        // 🧹 Si la restriction d'entrées est encore active (ex : interruption prématurée),
        //     on s'assure de rétablir le mapping complet avant de quitter le combat.
        InputsManager.Instance?.RestoreBattleInputsAfterIntro();

        // Nettoie les références
        currentCharacterUnit = null;
        unitsInBattle.Clear();
        activeCharacterUnits.Clear();

        rewardItems.Clear();
        rewardXP = 0;
        harmonicMenuConsultCount = 0;

        battleStartTime = 0f;
        maxTurnDamage = 0;
        currentTurnDamage = 0;
        mvpUnit = null;

        // Réinitialisation de l'interface de timeline via le gestionnaire dédié
        BattleTimelineUIManager.Instance?.Clear();

        // Réinitialise les séquences post-combat et l'issue du combat
        lastBattleOutcome = BattleOutcome.None;
        activeTimelineBattleConfig = null;
        battleEndEventDispatched = false;
        respawnAtCheckpointOnExit = false;
        victoryTimelineSequence.Clear();
        defeatTimelineSequence.Clear();

        // Réinitialise le curseur cible si existant
        if (targetCursor != null)
        {
            Destroy(targetCursor);
            targetCursor = null;
        }
        targetCostLabel = null;
        prepaidMoveCosts.Clear();
    }
    #endregion

    private void DispatchBattleEnded(BattleOutcome outcome)
    {
        if (battleEndEventDispatched)
            return;

        battleEndEventDispatched = true;
        BattleEnded?.Invoke(outcome, activeTimelineBattleConfig);
    }

    private static bool HasPostBattleTimeline(List<TimelineSequenceEntry> sequence)
    {
        if (sequence == null || sequence.Count == 0)
            return false;

        for (int i = 0; i < sequence.Count; i++)
        {
            var entry = sequence[i];
            if (entry != null && entry.HasPlayable)
                return true;
        }

        return false;
    }

    public void ShowHarmonicStatusMenu()
    {
        if (InfoBoxManager.Instance == null)
        {
            ActionUIDisplayManager.Instance?.DisplayInstruction("Consultation des harmoniques indisponible.");
            return;
        }

        if (InfoBoxManager.Instance.isOpen)
            return;

        string message = BuildHarmonicStatusMessage();
        if (string.IsNullOrWhiteSpace(message))
            message = "Aucune unité en combat.";

        InfoBoxManager.Instance.OpenInfoBox("Harmoniques en combat", message, null);
        RegisterHarmonicMenuConsult();
    }

    private void RegisterHarmonicMenuConsult()
    {
        harmonicMenuConsultCount++;
    }

    private int GetHarmonicMenuScorePenalty()
    {
        if (harmonicMenuConsultCount <= 0 || harmonicMenuScorePenalty <= 0)
            return 0;

        return harmonicMenuConsultCount * harmonicMenuScorePenalty;
    }

    private int ApplyHarmonicMenuPenalty(int baseValue)
    {
        int penalty = GetHarmonicMenuScorePenalty();
        if (penalty <= 0)
            return baseValue;

        return Mathf.Max(0, baseValue - penalty);
    }

    private string BuildHarmonicStatusMessage()
    {
        var units = activeCharacterUnits.Count > 0 ? activeCharacterUnits : unitsInBattle;
        if (units == null || units.Count == 0)
            return string.Empty;

        var ordered = units
            .Where(u => u != null && u.Data != null)
            .OrderBy(u => u.characterType)
            .ThenBy(u => u.Data.characterName);

        var builder = new StringBuilder();
        foreach (var unit in ordered)
        {
            builder.Append(unit.Data.characterName).Append(" : ");

            bool showSeparator = false;
            foreach (HarmonicType type in System.Enum.GetValues(typeof(HarmonicType)))
            {
                if (showSeparator)
                    builder.Append(" | ");
                showSeparator = true;
                builder.Append(type).Append(' ').Append(unit.GetHarmonicCount(type));
            }

            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }
}
