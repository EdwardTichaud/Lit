using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Role: autorite principale des sessions de combat tour par tour.
// Usage: singleton scene/runtime appele par CombatAggroEnemy, CombatHudController, items et idoles Iustia.
// Responsibilities: creer sessions, piloter tours, synchroniser clients Netcode, deplacer presentations, appliquer resultats.
// Dependencies: Unity Netcode, CombatHudController, CombatTransitionController, SquadCharacterController, CombatAggroEnemy.
// Precautions: ce script coordonne beaucoup de systemes; privilegier des changements petits et tester solo + host/client.
/// <summary>
/// Manager central des combats tour par tour, compatible solo et Netcode.
/// </summary>
public class CombatSessionManager : NetworkBehaviour
{
    private const string BasicAttackAnimationName = "Attack_Base";
    private const string CounterAnimationName = "Counter";
    private const string DefenseAnimationName = "Defense";
    private const string BlockAnimationName = "Block";
    private const string JuggernautGriffeDisplayName = "Griffe";
    private const string JuggernautGriffeAnimationName = "Attack_Griffe";
    private const string DefaultEnemyAttackDisplayName = "Attaque";
    private const string EnemyAttackWarningMessage = "Attention l’ennemi attaque:";
    private const string DefaultArenaRootName = "Arena";
    private const string DefaultPlayerSpawnPointName = "SpawnPoint_Player";
    private const string DefaultEnemySpawnPointName = "SpawnPoint_Enemy";
    private const float DeathAnimationTransitionDuration = 0.05f;
    private const float DefaultBasicAttackAnimationDuration = 0.75f;
    private const float DefaultDefenseAnimationDuration = 0.5f;
    private const float DefaultEnemyAttackAnimationDuration = 0.75f;
    private const float DefaultDeathAnimationDuration = 1f;
    private const float PostDeathReturnDelaySeconds = 3f;
    private const float LocalEnemyLookupMaxDistance = 6f;
    private const float ActionMoveCompleteThreshold = 0.03f;
    private const float ActionReturnDisplacementThreshold = 0.12f;
    private const float ActionAlreadyInRangePadding = 0.15f;
    private static readonly string[] DeathAnimationCandidates = { "Death", "Death_v1", "Death_v2" };
    private static readonly string[] RangedAttackNameHints =
    {
        "ranged", "range", "distance", "projectile", "shoot", "shot", "fireball", "bolt", "arrow", "tir", "fleche", "lancer"
    };

    private static readonly string[] SupportAttackNameHints =
    {
        "support", "heal", "soin", "buff", "priere", "shield", "protection"
    };
    private static readonly string[] MovementAnimationNameHints =
    {
        "advance", "approach", "dash", "charge", "lunge", "leap", "jump", "rush", "forward", "avance", "bond", "saut", "assaut"
    };

    /// <summary>
    /// Choix court du joueur pendant la fenetre de reaction ennemie.
    /// </summary>
    private enum EncounterReactionChoice
    {
        None = 0,
        Counter = 1,
        Defend = 2
    }

    /// <summary>
    /// Donnees completes d'une session de combat active cote autorite.
    /// </summary>
    private sealed class CombatSession
    {
        /// <summary>Identifiant unique de la session.</summary>
        public string SessionId;
        /// <summary>Identifiant stable du personnage joueur.</summary>
        public string CharacterId;
        /// <summary>Client proprietaire du personnage joueur.</summary>
        public ulong OwnerClientId;
        /// <summary>Controleur du personnage joueur engage.</summary>
        public SquadCharacterController Player;
        /// <summary>Ennemi de monde qui a declenche le combat.</summary>
        public CombatAggroEnemy SourceEnemy;
        /// <summary>Position ou replacer le joueur apres combat.</summary>
        public Vector3 ReturnPosition;
        /// <summary>Rotation ou replacer le joueur apres combat.</summary>
        public Quaternion ReturnRotation;
        /// <summary>Position de retour de l'ennemi source.</summary>
        public Vector3 EnemyReturnPosition;
        /// <summary>Rotation de retour de l'ennemi source.</summary>
        public Quaternion EnemyReturnRotation;
        /// <summary>Position du joueur pendant la presentation combat.</summary>
        public Vector3 PlayerCombatPosition;
        /// <summary>Rotation du joueur pendant la presentation combat.</summary>
        public Quaternion PlayerCombatRotation;
        /// <summary>Position de l'ennemi pendant la presentation combat.</summary>
        public Vector3 EnemyCombatPosition;
        /// <summary>Rotation de l'ennemi pendant la presentation combat.</summary>
        public Quaternion EnemyCombatRotation;
        /// <summary>Indique si un ennemi de scene doit etre deplace/restaure.</summary>
        public bool HasEnemyPresentation;
        /// <summary>Machine d'etat de la session.</summary>
        public CombatSessionState State = new CombatSessionState();
        /// <summary>Ennemis runtime encore suivis par cette session.</summary>
        public List<CombatRuntimeEnemy> Enemies = new List<CombatRuntimeEnemy>();
        /// <summary>Index de l'ennemi qui execute l'action verrouillee.</summary>
        public int PendingEnemyIndex = -1;
        /// <summary>Index de l'attaque ennemie verrouillee.</summary>
        public int PendingEnemyAttackIndex = -1;
        /// <summary>Attaque ennemie verrouillee pendant sa presentation.</summary>
        public CombatEnemyAttackDefinition PendingEnemyAttack;
        /// <summary>Moment autoritaire ou appliquer l'impact de l'action joueur.</summary>
        public float PendingPlayerActionImpactAt;
        /// <summary>Indique si l'impact joueur a deja ete resolu.</summary>
        public bool PendingPlayerActionResolved;
        /// <summary>Message issu de l'impact joueur, affiche apres le retour de presentation.</summary>
        public string PendingPlayerActionResultMessage;
        /// <summary>Indique si l'action joueur a vaincu tous les ennemis.</summary>
        public bool PendingPlayerActionVictory;
        /// <summary>Moment autoritaire ou appliquer l'impact de l'action ennemie.</summary>
        public float PendingEnemyActionImpactAt;
        /// <summary>Indique si l'impact ennemi a deja ete resolu.</summary>
        public bool PendingEnemyActionResolved;
        /// <summary>Message issu de l'impact ennemi, affiche apres le retour de presentation.</summary>
        public string PendingEnemyActionResultMessage;
        /// <summary>Indique si l'action ennemie a vaincu le joueur.</summary>
        public bool PendingEnemyActionPlayerDefeated;
        /// <summary>Indique si l'action ennemie defendue ouvre la fenetre d'attaque joueur.</summary>
        public bool PendingEnemyActionOpensPlayerAttackWindow;
        /// <summary>Reaction choisie pendant la fenetre ralentie ennemie.</summary>
        public EncounterReactionChoice PendingEncounterReaction;
        /// <summary>Item defensif choisi pendant la reaction locale ennemie.</summary>
        public Item PendingDefensiveItem;
        /// <summary>PV defensifs de l'unite choisie pour cette attaque.</summary>
        public int PendingDefensiveItemHitPoints;
        /// <summary>Indique si le mouvement joueur a ete supprime par ce combat.</summary>
        public bool SuppressedMovement;
    }

    /// <summary>
    /// Presentation locale d'un ennemi deplace chez un client non serveur.
    /// </summary>
    private sealed class LocalEnemyPresentation
    {
        /// <summary>Ennemi local deplace pour la presentation.</summary>
        public CombatAggroEnemy Enemy;
        /// <summary>Position de retour locale.</summary>
        public Vector3 ReturnPosition;
        /// <summary>Rotation de retour locale.</summary>
        public Quaternion ReturnRotation;
    }

    /// <summary>
    /// Etat minimal conserve par un client pour l'affichage et la camera de combat.
    /// </summary>
    private sealed class LocalCombatPresentationState
    {
        /// <summary>Identifiant de la session affichee localement.</summary>
        public string SessionId;
        /// <summary>Tour affiche localement.</summary>
        public CombatTurn Turn;
        /// <summary>Phase affichee localement.</summary>
        public CombatSessionPhase Phase;
        /// <summary>Indique si la resolution finale est en cours.</summary>
        public bool Resolving;
        /// <summary>Resultat final connu localement.</summary>
        public bool ResolutionPlayerVictory;
        /// <summary>Indique si l'ennemi a une presentation locale.</summary>
        public bool HasEnemyPresentation;
        /// <summary>Position de retour locale de l'ennemi.</summary>
        public Vector3 EnemyReturnPosition;
        /// <summary>Indique si l'action joueur est bloquee dans le HUD local.</summary>
        public bool PlayerActionLocked;
        /// <summary>Vrai si une session locale est suivie.</summary>
        public bool Active => !string.IsNullOrWhiteSpace(SessionId);

        /// <summary>
        /// Reinitialise la presentation locale quand le combat se termine ou que le reseau despawn.
        /// </summary>
        public void Reset()
        {
            SessionId = null;
            Turn = CombatTurn.None;
            Phase = CombatSessionPhase.Finished;
            Resolving = false;
            ResolutionPlayerVictory = false;
            HasEnemyPresentation = false;
            EnemyReturnPosition = Vector3.zero;
            PlayerActionLocked = false;
        }
    }

    /// <summary>
    /// Plan local de mouvement pour une action de combat.
    /// </summary>
    private struct CombatActionMotionPlan
    {
        public Vector3 StartPosition;
        public Quaternion StartRotation;
        public Vector3 ApproachPosition;
        public Quaternion ApproachRotation;
        public Quaternion AttackRotation;
        public bool UseScriptedApproach;
        public bool ReturnToStart;
        public float ApproachDuration;
        public float AttackDuration;
        public float ReturnDuration;

        public float ImpactDelay(float impactNormalizedTime)
        {
            float normalizedImpact = Mathf.Clamp01(impactNormalizedTime);
            return Mathf.Max(0.05f, ApproachDuration + AttackDuration * normalizedImpact);
        }

        public float TotalDuration()
        {
            return Mathf.Max(0.05f, ApproachDuration + AttackDuration + ReturnDuration);
        }
    }

    /// <summary>
    /// Timings autoritaires utilises pour appliquer l'impact puis liberer l'action.
    /// </summary>
    private struct CombatActionTiming
    {
        public float ImpactDelay;
        public float TotalDuration;

        public CombatActionTiming(float impactDelay, float totalDuration)
        {
            ImpactDelay = Mathf.Max(0.05f, impactDelay);
            TotalDuration = Mathf.Max(ImpactDelay, totalDuration);
        }
    }

    /// <summary>
    /// Etat d'une priere de soutien active pour un client.
    /// </summary>
    private sealed class PrayerState
    {
        /// <summary>Client qui maintient la priere.</summary>
        public ulong ClientId;
        /// <summary>Personnage associe a la priere.</summary>
        public string CharacterId;
        /// <summary>Dernier moment ou la priere a ete validee.</summary>
        public float LastValidationTime;
    }

    /// <summary>
    /// Instance singleton active du manager de combat.
    /// </summary>
    public static CombatSessionManager Instance { get; private set; }

    [Header("Turns")]
    /// <summary>
    /// Duree maximale d'un tour avant passage automatique.
    /// </summary>
    [SerializeField, Min(1f), Tooltip("Duree maximale d'un tour.")]
    private float turnDurationSeconds = 30f;
    /// <summary>
    /// Delai avant que l'ennemi applique son action automatique.
    /// </summary>
    [SerializeField, Min(0f), Tooltip("Delai court avant l'action automatique ennemie.")]
    private float enemyActionDelay = 1f;
    /// <summary>
    /// Duree locale de presentation suspendue avant un choix ou une action.
    /// </summary>
    [SerializeField, Min(0f), Tooltip("Duree de presentation locale suspendue avant les actions de combat.")]
    private float decisionPresentationSeconds = 2f;
    /// <summary>
    /// Duree minimale de reaction defensive quand l'ennemi prepare son attaque.
    /// </summary>
    [SerializeField, Min(0f), Tooltip("Duree minimale de reaction defensive avant l'attaque ennemie.")]
    private float defensiveReactionSeconds = 2f;
    /// <summary>
    /// Fenetre courte accordee au joueur apres une defense reussie.
    /// </summary>
    [SerializeField, Min(0.25f), Tooltip("Fenetre courte d'attaque apres une defense reussie.")]
    private float encounterPlayerAttackWindowSeconds = 2f;
    /// <summary>
    /// Degats de l'attaque de base du joueur.
    /// </summary>
    [SerializeField, Min(1), Tooltip("Degats de base de l'action Attaquer du joueur.")]
    private int defaultPlayerAttackDamage = 3;
    /// <summary>
    /// Intervalle entre deux snapshots HUD/reseau.
    /// </summary>
    [SerializeField, Min(0.05f), Tooltip("Intervalle de rafraichissement du HUD pendant un combat.")]
    private float snapshotInterval = 0.2f;

    [Header("Action Presentation")]
    /// <summary>
    /// Distance de contact par defaut conservee apres une approche de melee.
    /// </summary>
    [SerializeField, Min(0.1f), Tooltip("Distance de contact par defaut conservee apres une approche de melee.")]
    private float defaultMeleeApproachDistance = 1.25f;
    /// <summary>
    /// Vitesse de deplacement de presentation pour l'approche et le retour.
    /// </summary>
    [SerializeField, Min(0.1f), Tooltip("Vitesse de deplacement de presentation pour l'approche et le retour.")]
    private float actionPresentationMoveSpeed = 3.5f;
    /// <summary>
    /// Duree maximale d'un aller ou retour de presentation.
    /// </summary>
    [SerializeField, Min(0.05f), Tooltip("Duree maximale d'un aller ou retour de presentation.")]
    private float maxActionPresentationMoveSeconds = 0.75f;
    /// <summary>
    /// Moment normalise de l'animation ou l'impact autoritaire est applique.
    /// </summary>
    [SerializeField, Range(0.1f, 1f), Tooltip("Moment normalise de l'animation ou l'impact autoritaire est applique.")]
    private float actionImpactNormalizedTime = 0.75f;

    [Header("Arena Scene")]
    /// <summary>
    /// Racine optionnelle de l'arene de combat.
    /// </summary>
    [SerializeField, Tooltip("Racine de l'arene de combat dans la scene.")]
    private Transform arenaRoot;
    /// <summary>
    /// Point de placement du joueur dans l'arene.
    /// </summary>
    [SerializeField, Tooltip("Spawn point scene du joueur pour les combats. Si vide, cherche 'Arena/SpawnPoint_Player'.")]
    private Transform spawnPointPlayer;
    /// <summary>
    /// Point de placement de l'ennemi dans l'arene.
    /// </summary>
    [SerializeField, Tooltip("Spawn point scene de l'ennemi pour les combats. Si vide, cherche 'Arena/SpawnPoint_Enemy'.")]
    private Transform spawnPointEnemy;

    [Header("Idoles de Iustia")]
    /// <summary>
    /// Reduction de degats ajoutee par chaque joueur en priere.
    /// </summary>
    [SerializeField, Range(0f, 1f), Tooltip("Reduction de degats accordee par joueur en priere.")]
    private float prayerDamageReductionPerPlayer = 0.2f;
    /// <summary>
    /// Limite maximale de reduction de degats.
    /// </summary>
    [SerializeField, Range(0f, 1f), Tooltip("Cap de reduction idole. 0.8 signifie au moins 20% des degats passent toujours.")]
    private float maxPrayerDamageReduction = 0.8f;

    private readonly Dictionary<string, CombatSession> sessionsByCharacterId = new Dictionary<string, CombatSession>();
    private readonly Dictionary<ulong, CombatSession> sessionsByClientId = new Dictionary<ulong, CombatSession>();
    private readonly Dictionary<ulong, PrayerState> activePrayersByClientId = new Dictionary<ulong, PrayerState>();
    private readonly List<CombatSession> tickSessions = new List<CombatSession>();
    private readonly HashSet<string> locallySuppressedSessions = new HashSet<string>();
    private readonly Dictionary<string, LocalEnemyPresentation> localEnemyPresentationsBySessionId = new Dictionary<string, LocalEnemyPresentation>();
    private readonly Dictionary<Transform, Coroutine> actionPresentationCoroutinesByActor = new Dictionary<Transform, Coroutine>();
    private readonly LocalCombatPresentationState localCombatPresentation = new LocalCombatPresentationState();
    private bool globalCombatDefensiveReactionActive;
    private int nextSessionId = 1;

    /// <summary>
    /// Retourne le manager existant ou cree un objet runtime minimal.
    /// </summary>
    public static CombatSessionManager EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

#if UNITY_2023_1_OR_NEWER
        Instance = FindAnyObjectByType<CombatSessionManager>();
#else
        Instance = FindAnyObjectByType<CombatSessionManager>();
#endif
        if (Instance != null)
        {
            return Instance;
        }

        GameObject host = new GameObject("CombatSessionManager");
        DontDestroyOnLoad(host);
        Instance = host.AddComponent<CombatSessionManager>();
        return Instance;
    }

    /// <summary>
    /// Indique si ce controleur de personnage est deja engage dans un combat.
    /// </summary>
    public static bool IsCharacterInCombat(SquadCharacterController controller)
    {
        if (controller == null || Instance == null)
        {
            return false;
        }

        return Instance.TryGetSession(controller, out _);
    }

    /// <summary>
    /// Indique si le joueur local est actuellement dans un combat actif.
    /// </summary>
    public bool IsLocalCombatActive()
    {
        if (CanRunAuthority())
        {
            return sessionsByClientId.TryGetValue(ResolveLocalClientId(), out CombatSession session) &&
                   session != null &&
                   !session.State.Finished;
        }

        return localCombatPresentation.Active;
    }

    /// <summary>
    /// Retourne les transforms joueur/ennemi utiles a une camera de combat locale.
    /// </summary>
    public bool TryGetLocalCombatCameraContext(out Transform player, out Transform enemy, out bool playerTurn)
    {
        return TryGetLocalCombatCameraContext(out player, out enemy, out playerTurn, out _);
    }

    /// <summary>
    /// Retourne les transforms et la phase utiles a une presentation de camera de combat locale.
    /// </summary>
    public bool TryGetLocalCombatCameraContext(out Transform player, out Transform enemy, out bool playerTurn, out CombatSessionPhase phase)
    {
        player = null;
        enemy = null;
        playerTurn = false;
        phase = CombatSessionPhase.Finished;

        if (CanRunAuthority())
        {
            if (!sessionsByClientId.TryGetValue(ResolveLocalClientId(), out CombatSession session) || session == null || session.State.Finished)
            {
                return false;
            }

            player = session.Player != null ? session.Player.transform : ResolveControllerForClient(ResolveLocalClientId())?.transform;
            enemy = session.SourceEnemy != null ? session.SourceEnemy.transform : null;
            playerTurn = ResolvePresentationTurn(session) == CombatTurn.Player;
            phase = session.State.Phase;
            return player != null && enemy != null;
        }

        if (!localCombatPresentation.Active)
        {
            return false;
        }

        player = ResolveControllerForClient(ResolveLocalClientId())?.transform;
        enemy = ResolveLocalCombatEnemyTransform(localCombatPresentation);
        playerTurn = ResolvePresentationTurn(localCombatPresentation) == CombatTurn.Player;
        phase = localCombatPresentation.Phase;
        return player != null && enemy != null;
    }

    public void NotifyLocalCombatAnimationImpact(Transform actor)
    {
        if (CanRunAuthority())
        {
            if (TryResolveLocalCombatAnimationImpactSession(actor, out CombatSession session))
            {
                TryResolveCombatAnimationImpact(session);
            }

            return;
        }

        if (!IsNetworkSessionActive() || !IsSpawned || !localCombatPresentation.Active)
        {
            return;
        }

        NotifyCombatAnimationImpactServerRpc(localCombatPresentation.SessionId);
    }

    private void Awake()
    {
        // Unity appelle Awake au chargement; le singleton peut provenir de la scene ou etre cree au runtime.
        if (Instance != null && Instance != this)
        {
            if (ShouldReplaceExistingInstance(Instance, this))
            {
                Destroy(Instance.gameObject);
                Instance = this;
            }
            else
            {
                Destroy(this);
                return;
            }
        }
        else
        {
            Instance = this;
        }
    }

    public override void OnNetworkSpawn()
    {
        // Netcode appelle OnNetworkSpawn quand l'objet reseau devient actif.
        Instance = this;
        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
    }

    public override void OnNetworkDespawn()
    {
        // Netcode appelle OnNetworkDespawn avant destruction/desactivation reseau; il faut restaurer les presentations locales.
        ApplyGlobalCombatDefensiveReaction(false, force: true, broadcast: false);
        StopAllCombatActionPresentations();
        ReleaseAllLocalClientMovement();
        RestoreAllLocalEnemyPresentations();
        localCombatPresentation.Reset();
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    public override void OnDestroy()
    {
        // Unity appelle OnDestroy; on repete le nettoyage pour couvrir le mode non reseau.
        ApplyGlobalCombatDefensiveReaction(false, force: true, broadcast: false);
        StopAllCombatActionPresentations();
        ReleaseAllLocalClientMovement();
        RestoreAllLocalEnemyPresentations();
        localCombatPresentation.Reset();
        base.OnDestroy();
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        if (!CanRunAuthority())
        {
            return;
        }

        ValidatePrayerStates();
        // On copie les sessions avant iteration car TickSession peut terminer et retirer une session.
        tickSessions.Clear();
        foreach (CombatSession session in sessionsByCharacterId.Values)
        {
            tickSessions.Add(session);
        }

        for (int i = 0; i < tickSessions.Count; i++)
        {
            TickSession(tickSessions[i]);
        }

        RefreshGlobalCombatDefensiveReaction();
    }

    /// <summary>
    /// Tente de demarrer un combat entre un personnage joueur et un ennemi de monde.
    /// </summary>
    public bool TryStartCombat(SquadCharacterController player, CombatAggroEnemy sourceEnemy)
    {
        if (!CanRunAuthority() || player == null || player.CurrentHp <= 0)
        {
            return false;
        }

        string characterId = ResolveCharacterId(player);
        if (string.IsNullOrWhiteSpace(characterId) || sessionsByCharacterId.ContainsKey(characterId))
        {
            return false;
        }

        ulong ownerClientId = ResolveOwnerClientId(player);
        if (sessionsByClientId.ContainsKey(ownerClientId))
        {
            return false;
        }

        List<CombatRuntimeEnemy> enemies = BuildRuntimeEnemies(sourceEnemy);
        if (enemies.Count == 0)
        {
            return false;
        }

        StopPrayer(ownerClientId, sendFeedback: false);

        // Les positions de combat peuvent venir de l'arene configuree ou de fallbacks proches du combat.
        ResolveCombatPositions(
            player,
            out Vector3 playerCombatPosition,
            out Quaternion playerCombatRotation,
            out Vector3 enemyCombatPosition,
            out Quaternion enemyCombatRotation);

        CombatSession session = new CombatSession
        {
            SessionId = $"combat_{nextSessionId++}",
            CharacterId = characterId,
            OwnerClientId = ownerClientId,
            Player = player,
            SourceEnemy = sourceEnemy,
            ReturnPosition = player.transform.position,
            ReturnRotation = player.transform.rotation,
            EnemyReturnPosition = sourceEnemy != null ? sourceEnemy.transform.position : Vector3.zero,
            EnemyReturnRotation = sourceEnemy != null ? sourceEnemy.transform.rotation : Quaternion.identity,
            PlayerCombatPosition = playerCombatPosition,
            PlayerCombatRotation = playerCombatRotation,
            EnemyCombatPosition = enemyCombatPosition,
            EnemyCombatRotation = enemyCombatRotation,
            HasEnemyPresentation = sourceEnemy != null,
            Enemies = enemies
        };

        sessionsByCharacterId[characterId] = session;
        sessionsByClientId[ownerClientId] = session;

        // Le mouvement est bloque pendant la session pour eviter que le joueur sorte de la presentation.
        player.PushScriptedMovementSuppression();
        session.SuppressedMovement = true;
        player.Stop();

        MoveCharacterTo(player, session.PlayerCombatPosition, session.PlayerCombatRotation);
        MoveCombatAggroEnemyTo(sourceEnemy, session.EnemyCombatPosition, session.EnemyCombatRotation);

        SendEnterCombat(session);
        BeginTurn(session, CombatTurn.Enemy, EnemyAttackWarningMessage);
        RefreshGlobalCombatDefensiveReaction();
        return true;
    }

    /// <summary>
    /// Demande une attaque du joueur local, en RPC si le client n'est pas autorite.
    /// </summary>
    public void RequestLocalPlayerAttack()
    {
        if (IsNetworkSessionActive() && IsSpawned && !IsServer)
        {
            RequestPlayerAttackServerRpc();
            return;
        }

        TryPlayerAttackForClient(ResolveLocalClientId());
    }

    /// <summary>
    /// Demande de passer le tour du joueur local.
    /// </summary>
    public void RequestLocalPlayerPass()
    {
        if (IsNetworkSessionActive() && IsSpawned && !IsServer)
        {
            RequestPlayerPassServerRpc();
            return;
        }

        TryPlayerPassForClient(ResolveLocalClientId(), "Defaite. Fenetre d'attaque manquee.");
    }

    /// <summary>
    /// Demande un contre pendant la fenetre de reaction ennemie.
    /// </summary>
    public void RequestLocalCounter()
    {
        if (IsNetworkSessionActive() && IsSpawned && !IsServer)
        {
            RequestCounterServerRpc();
            return;
        }

        TrySelectEncounterReactionForClient(ResolveLocalClientId(), EncounterReactionChoice.Counter);
    }

    /// <summary>
    /// Demande une defense simple pendant la fenetre de reaction ennemie.
    /// </summary>
    public void RequestLocalDefense()
    {
        if (IsNetworkSessionActive() && IsSpawned && !IsServer)
        {
            RequestDefenseServerRpc();
            return;
        }

        TrySelectEncounterReactionForClient(ResolveLocalClientId(), EncounterReactionChoice.Defend);
    }

    /// <summary>
    /// Notifie le combat qu'un item vient d'etre utilise par un personnage.
    /// </summary>
    public void NotifyInventoryItemUsed(SquadCharacterController controller)
    {
        if (!CanRunAuthority() || controller == null)
        {
            return;
        }

        if (!TryGetSession(controller, out CombatSession session) ||
            !session.State.CanUsePlayerAction())
        {
            return;
        }

        BeginEncounterPlayerDeath(session, "Defaite. Fenetre d'attaque consommee.");
    }

    /// <summary>
    /// Indique si un personnage peut utiliser un item dans l'etat de combat actuel.
    /// </summary>
    public bool CanUseItemNow(SquadCharacterController controller, out string reason)
    {
        reason = string.Empty;
        if (!CanRunAuthority() || controller == null)
        {
            return true;
        }

        if (!TryGetSession(controller, out CombatSession session))
        {
            return true;
        }

        reason = session.State.CanUsePlayerAction()
            ? "Attaque uniquement pendant cette fenetre."
            : session.State.Finished
            ? "Combat termine."
            : session.State.ActionLocked
                ? "Action de combat deja en cours."
                : "Impossible d'utiliser un item hors de la fenetre de reaction.";
        return false;
    }

    /// <summary>
    /// Indique si le joueur local peut actuellement choisir un item defensif.
    /// </summary>
    public bool IsLocalDefensiveReactionActive()
    {
        if (CanRunAuthority())
        {
            return sessionsByClientId.TryGetValue(ResolveLocalClientId(), out CombatSession session) &&
                   IsDefensiveReactionActive(session);
        }

        return localCombatPresentation.Active &&
               localCombatPresentation.Turn == CombatTurn.Enemy &&
               localCombatPresentation.Phase == CombatSessionPhase.Decision;
    }

    /// <summary>
    /// Verifie localement ou cote autorite si un item defensif peut etre choisi maintenant.
    /// </summary>
    public bool CanUseDefensiveItemNow(SquadCharacterController controller, Item item, out string reason)
    {
        reason = string.Empty;
        if (controller == null)
        {
            reason = "Personnage introuvable.";
            return false;
        }

        if (item == null || !item.CanDefendInCombat())
        {
            reason = "Cet item ne peut pas absorber une attaque.";
            return false;
        }

        if (!ControllerHasItem(controller, item))
        {
            reason = "Item absent de l'inventaire.";
            return false;
        }

        if (CanRunAuthority())
        {
            if (!TryGetSession(controller, out CombatSession session) || !IsDefensiveReactionActive(session))
            {
                reason = "Aucune reaction defensive n'est active.";
                return false;
            }

            if (session.PendingDefensiveItem != null)
            {
                reason = "Un item defensif est deja choisi.";
                return false;
            }

            return true;
        }

        if (!IsLocalDefensiveReactionActive())
        {
            reason = "Aucune reaction defensive n'est active.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Demande au serveur, ou a l'autorite locale, de retenir un item defensif pour l'attaque ennemie.
    /// </summary>
    public bool RequestLocalDefensiveItem(Item item)
    {
        SquadCharacterController controller = ResolveLocalControllerFromContext();
        if (!CanUseDefensiveItemNow(controller, item, out string reason))
        {
            if (!string.IsNullOrWhiteSpace(reason))
            {
                InfoBoxUI.TryShow(reason);
            }

            return false;
        }

        if (IsNetworkSessionActive() && IsSpawned && !IsServer)
        {
            string itemId = ItemIdUtils.GetItemId(item);
            if (string.IsNullOrWhiteSpace(itemId))
            {
                InfoBoxUI.TryShow("Item defensif sans identifiant.");
                return false;
            }

            RequestDefensiveItemServerRpc(itemId);
            return true;
        }

        if (TrySelectDefensiveItemForClient(ResolveLocalClientId(), item, out string feedback))
        {
            SendDefensiveItemFeedback(ResolveLocalClientId(), feedback, ActionAudioCue.InventoryUse);
            return true;
        }

        if (!string.IsNullOrWhiteSpace(feedback))
        {
            InfoBoxUI.TryShow(feedback);
        }

        return false;
    }

    /// <summary>
    /// Demande l'activation ou l'arret de la priere locale associee a une idole.
    /// </summary>
    public void RequestTogglePrayerFromLocal(SquadCharacterController controller, IustiaIdolPrayer idol)
    {
        if (IsNetworkSessionActive() && IsSpawned && !IsServer)
        {
            RequestTogglePrayerServerRpc();
            return;
        }

        ulong clientId = ResolveLocalClientId();
        SquadCharacterController resolved = controller != null ? controller : ResolveControllerForClient(clientId);
        bool shouldStart = !activePrayersByClientId.ContainsKey(clientId);
        SetPrayerState(clientId, resolved, shouldStart, sendFeedback: true);
    }

    /// <summary>
    /// Demande l'arret de la priere locale.
    /// </summary>
    public void RequestStopPrayerFromLocal()
    {
        if (IsNetworkSessionActive() && IsSpawned && !IsServer)
        {
            RequestStopPrayerServerRpc();
            return;
        }

        StopPrayer(ResolveLocalClientId(), sendFeedback: true);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestPlayerAttackServerRpc(ServerRpcParams rpcParams = default)
    {
        // Le serveur valide l'action avec l'identite du client emetteur.
        TryPlayerAttackForClient(rpcParams.Receive.SenderClientId);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestPlayerPassServerRpc(ServerRpcParams rpcParams = default)
    {
        // Passer le tour suit le meme chemin de validation que l'attaque.
        TryPlayerPassForClient(rpcParams.Receive.SenderClientId, "Defaite. Fenetre d'attaque manquee.");
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestCounterServerRpc(ServerRpcParams rpcParams = default)
    {
        TrySelectEncounterReactionForClient(rpcParams.Receive.SenderClientId, EncounterReactionChoice.Counter);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestDefenseServerRpc(ServerRpcParams rpcParams = default)
    {
        TrySelectEncounterReactionForClient(rpcParams.Receive.SenderClientId, EncounterReactionChoice.Defend);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestTogglePrayerServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        bool shouldStart = !activePrayersByClientId.ContainsKey(clientId);
        SetPrayerState(clientId, ResolveControllerForClient(clientId), shouldStart, sendFeedback: true);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestStopPrayerServerRpc(ServerRpcParams rpcParams = default)
    {
        StopPrayer(rpcParams.Receive.SenderClientId, sendFeedback: true);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestDefensiveItemServerRpc(string itemId, ServerRpcParams rpcParams = default)
    {
        Item item = ItemRegistry.Resolve(itemId);
        if (item == null)
        {
            SendDefensiveItemFeedback(rpcParams.Receive.SenderClientId, "Item defensif introuvable.", ActionAudioCue.UiInvalid);
            return;
        }

        if (TrySelectDefensiveItemForClient(rpcParams.Receive.SenderClientId, item, out string feedback))
        {
            SendDefensiveItemFeedback(rpcParams.Receive.SenderClientId, feedback, ActionAudioCue.InventoryUse);
            return;
        }

        SendDefensiveItemFeedback(rpcParams.Receive.SenderClientId, feedback, ActionAudioCue.UiInvalid);
    }

    [ServerRpc(RequireOwnership = false)]
    private void NotifyCombatAnimationImpactServerRpc(string sessionId, ServerRpcParams rpcParams = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId) ||
            !sessionsByClientId.TryGetValue(rpcParams.Receive.SenderClientId, out CombatSession session) ||
            session == null ||
            !string.Equals(session.SessionId, sessionId, StringComparison.Ordinal))
        {
            return;
        }

        TryResolveCombatAnimationImpact(session);
    }

    [ClientRpc]
    private void EnterCombatClientRpc(CombatEnterData data, ClientRpcParams rpcParams = default)
    {
        if (!IsServer)
        {
            // Les clients non serveurs conservent un etat minimal pour HUD/camera.
            localCombatPresentation.SessionId = data.SessionId.ToString();
            localCombatPresentation.Turn = CombatTurn.None;
            localCombatPresentation.Phase = CombatSessionPhase.Created;
            localCombatPresentation.Resolving = false;
            localCombatPresentation.ResolutionPlayerVictory = false;
            localCombatPresentation.HasEnemyPresentation = data.HasEnemyPresentation;
            localCombatPresentation.EnemyReturnPosition = data.EnemyReturnPosition;
            localCombatPresentation.PlayerActionLocked = false;
        }

        CombatHudController.EnsureInstance();
        CombatCameraPresentationController.EnsureInstance();
        if (!IsServer)
        {
            ApplyLocalEnterCombatPresentation(data);
        }

        CombatTransitionController.EnsureInstance().PlayEnterTransition();
    }

    [ClientRpc]
    private void ExitCombatClientRpc(CombatExitData data, ClientRpcParams rpcParams = default)
    {
        StopAllCombatActionPresentations();
        CombatTransitionController.EnsureInstance().PlayExitTransition(() =>
        {
            ApplyExitCombatPresentation(data, restoreLocalClient: !IsServer);
        });
    }

    private void ApplyLocalEnterCombatPresentation(CombatEnterData data)
    {
        string sessionId = data.SessionId.ToString();
        SuppressLocalClientMovement(sessionId, data.PlayerCombatPosition, data.PlayerCombatRotation);
        if (!data.HasEnemyPresentation)
        {
            return;
        }

        MoveLocalEnemyIntoCombat(
            sessionId,
            data.EnemyReturnPosition,
            data.EnemyReturnRotation,
            data.EnemyCombatPosition,
            data.EnemyCombatRotation);
    }

    private void ApplyExitCombatPresentation(CombatExitData data, bool restoreLocalClient)
    {
        string sessionId = data.SessionId.ToString();
        StopAllCombatActionPresentations();
        if (restoreLocalClient)
        {
            localCombatPresentation.Reset();
            RestoreLocalClientMovement(
                sessionId,
                data.PlayerReturnPosition,
                data.PlayerReturnRotation,
                data.PlayerHp,
                data.PlayerMaxHp);
            RestoreLocalEnemyPresentation(
                sessionId,
                data.HasEnemyPresentation,
                data.EnemyReturnPosition,
                data.EnemyReturnRotation,
                data.PlayerVictory,
                data.EnemyRemainingHp);
        }

        CombatHudController.HideActive(sessionId);
        string message = data.Message.ToString();
        if (!string.IsNullOrWhiteSpace(message))
        {
            InfoBoxUI.TryShow(message);
        }
    }

    [ClientRpc]
    private void CombatSnapshotClientRpc(CombatSnapshotData snapshot, ClientRpcParams rpcParams = default)
    {
        string sessionId = snapshot.SessionId.ToString();
        if (!IsServer && localCombatPresentation.Active && localCombatPresentation.SessionId == sessionId)
        {
            CombatTurn snapshotTurn = snapshot.TurnState;
            if (snapshotTurn != CombatTurn.None && snapshotTurn != CombatTurn.Finished)
            {
                localCombatPresentation.Turn = snapshotTurn;
            }

            localCombatPresentation.Phase = snapshot.PhaseState;
            localCombatPresentation.PlayerActionLocked = snapshot.PlayerActionLocked;
        }

        CombatHudController.EnsureInstance().ShowSnapshot(
            sessionId,
            (CombatHudController.TurnState)snapshot.TurnState,
            snapshot.PhaseState,
            snapshot.TimerRemaining,
            snapshot.PlayerHp,
            snapshot.PlayerMaxHp,
            snapshot.EnemyName.ToString(),
            snapshot.EnemyHp,
            snapshot.EnemyMaxHp,
            snapshot.AliveEnemies,
            snapshot.TotalEnemies,
            snapshot.PrayerSupportCount,
            snapshot.DamageReduction,
            snapshot.PlayerActionLocked,
            snapshot.Message.ToString());
    }

    [ClientRpc]
    private void PrayerFeedbackClientRpc(bool active, string message, ClientRpcParams rpcParams = default)
    {
        IustiaIdolPrayer.SetLocalPrayerState(active);
        if (!string.IsNullOrWhiteSpace(message))
        {
            InfoBoxUI.TryShow(message);
        }
    }

    [ClientRpc]
    private void DefensiveItemFeedbackClientRpc(string message, ActionAudioCue cue, ClientRpcParams rpcParams = default)
    {
        PlayUiFeedbackAudio(cue);
        if (!string.IsNullOrWhiteSpace(message))
        {
            InfoBoxUI.TryShow(message);
        }
    }

    [ClientRpc]
    private void CombatDefensiveReactionTimeClientRpc(bool active)
    {
        ApplyGlobalCombatDefensiveReaction(active, force: false, broadcast: false);
    }

    private void RefreshGlobalCombatDefensiveReaction()
    {
        if (!CanRunAuthority())
        {
            return;
        }

        ApplyGlobalCombatDefensiveReaction(HasAnyGlobalCombatDefensiveReaction(), force: false);
    }

    private bool HasAnyGlobalCombatDefensiveReaction()
    {
        foreach (CombatSession session in sessionsByCharacterId.Values)
        {
            if (IsDefensiveReactionActive(session))
            {
                return true;
            }
        }

        return false;
    }

    private void ApplyGlobalCombatDefensiveReaction(bool active, bool force, bool broadcast = true)
    {
        if (!force && globalCombatDefensiveReactionActive == active)
        {
            return;
        }

        globalCombatDefensiveReactionActive = active;
        if (active)
        {
            TimeManager.EnsureInstance().SetGlobalCombatDefensiveReaction(true);
        }
        else
        {
            TimeManager.Instance?.RestoreGlobalCombatTime();
        }

        if (broadcast && CanRunAuthority() && IsNetworkSessionActive() && IsSpawned)
        {
            CombatDefensiveReactionTimeClientRpc(active);
        }
    }

    private void TickSession(CombatSession session)
    {
        if (session == null || session.State.Finished)
        {
            return;
        }

        if (session.State.Resolving)
        {
            if (Time.time >= session.State.ResolutionEndsAt)
            {
                EndCombat(session, session.State.ResolutionPlayerVictory, session.State.LastMessage, notifyClient: true);
            }

            return;
        }

        if (session.Player == null)
        {
            EndCombat(session, false, "Combat interrompu.", notifyClient: true);
            return;
        }

        if (session.State.DecisionActive)
        {
            if (Time.time >= session.State.DecisionEndsAt)
            {
                CompleteDecisionPhase(session);
            }

            return;
        }

        if (session.State.PlayerActionLocked)
        {
            if (!session.PendingPlayerActionResolved && Time.time >= session.PendingPlayerActionImpactAt)
            {
                ResolveLockedPlayerAttackImpact(session);
            }

            if (Time.time >= session.State.PlayerActionEndsAt)
            {
                CompleteLockedPlayerAttack(session);
            }

            return;
        }

        if (session.State.EnemyActionLocked)
        {
            if (!session.PendingEnemyActionResolved && Time.time >= session.PendingEnemyActionImpactAt)
            {
                ResolveEnemyActionImpact(session);
            }

            if (Time.time >= session.State.EnemyActionEndsAt)
            {
                CompleteEnemyAction(session);
            }

            return;
        }

        if (Time.time >= session.State.NextSnapshotAt)
        {
            SendSnapshot(session, session.State.LastMessage);
        }

        if (session.State.Turn == CombatTurn.Enemy && Time.time >= session.State.NextEnemyActionAt)
        {
            ExecuteEnemyTurn(session);
            return;
        }

        if (Time.time < session.State.TurnEndsAt)
        {
            return;
        }

        if (session.State.Turn == CombatTurn.Player)
        {
            BeginEncounterPlayerDeath(session, "Defaite. Fenetre d'attaque manquee.");
            return;
        }

        if (session.State.Turn == CombatTurn.Enemy)
        {
            ExecuteEnemyTurn(session);
        }
    }

    private bool TryPlayerAttackForClient(ulong clientId)
    {
        if (!sessionsByClientId.TryGetValue(clientId, out CombatSession session))
        {
            return false;
        }

        return TryPlayerAttack(session);
    }

    private bool TryPlayerPassForClient(ulong clientId, string message)
    {
        if (!sessionsByClientId.TryGetValue(clientId, out CombatSession session))
        {
            return false;
        }

        return TryPlayerPass(session, message);
    }

    private bool TrySelectEncounterReactionForClient(ulong clientId, EncounterReactionChoice reaction)
    {
        if (!sessionsByClientId.TryGetValue(clientId, out CombatSession session) || session == null)
        {
            return false;
        }

        if (!IsDefensiveReactionActive(session) || reaction == EncounterReactionChoice.None)
        {
            return false;
        }

        if (session.PendingEncounterReaction != EncounterReactionChoice.None)
        {
            return false;
        }

        if (reaction == EncounterReactionChoice.Counter && session.PendingDefensiveItem != null)
        {
            return false;
        }

        session.PendingEncounterReaction = reaction;
        string message = reaction == EncounterReactionChoice.Counter
            ? "Contre prepare."
            : "Defense preparee.";
        session.State.SetMessage(message);
        SendSnapshot(session, message);
        return true;
    }

    private bool TrySelectDefensiveItemForClient(ulong clientId, Item item, out string feedback)
    {
        feedback = string.Empty;
        if (!sessionsByClientId.TryGetValue(clientId, out CombatSession session) || session == null)
        {
            feedback = "Aucun combat actif.";
            return false;
        }

        if (!IsDefensiveReactionActive(session))
        {
            feedback = "Aucune reaction defensive n'est active.";
            return false;
        }

        if (session.PendingEncounterReaction == EncounterReactionChoice.Counter)
        {
            feedback = "Un contre est deja prepare.";
            return false;
        }

        if (session.PendingDefensiveItem != null)
        {
            feedback = "Un item defensif est deja choisi.";
            return false;
        }

        if (item == null || !item.CanDefendInCombat())
        {
            feedback = "Cet item ne peut pas absorber une attaque.";
            return false;
        }

        if (!ControllerHasItem(session.Player, item))
        {
            feedback = "Item absent de l'inventaire.";
            return false;
        }

        session.PendingEncounterReaction = EncounterReactionChoice.Defend;
        session.PendingDefensiveItem = item;
        session.PendingDefensiveItemHitPoints = item.GetCombatDefenseHitPoints();
        feedback = $"{ResolveItemDisplayName(item)} prepare la defense.";
        session.State.SetMessage(feedback);
        SendSnapshot(session, feedback);
        return true;
    }

    private bool TryPlayerAttack(CombatSession session)
    {
        if (session == null || !session.State.CanUsePlayerAction())
        {
            return false;
        }

        CombatRuntimeEnemy enemy = GetActiveEnemy(session);
        if (enemy == null)
        {
            BeginCombatResolution(session, true, "Victoire.");
            return true;
        }

        int pendingDamage = Mathf.Max(ResolvePlayerAttackDamage(session.Player), enemy.CurrentHp);
        CombatActionTiming actionTiming = PlayPlayerBasicAttackPresentation(session);
        PreparePendingPlayerAction(session, actionTiming);
        session.State.BeginPlayerAction(pendingDamage, Time.time, actionTiming.TotalDuration, $"Attaque decisive sur {enemy.DisplayName}.");
        SendSnapshot(session, session.State.LastMessage);
        return true;
    }

    private bool TryPlayerPass(CombatSession session, string message)
    {
        if (session == null || !session.State.CanUsePlayerAction())
        {
            return false;
        }

        BeginEncounterPlayerDeath(session, message);
        return true;
    }

    private void CompleteDecisionPhase(CombatSession session)
    {
        if (session == null || !session.State.DecisionActive || session.State.Finished)
        {
            return;
        }

        if (session.State.Turn == CombatTurn.Enemy)
        {
            if (session.PendingEncounterReaction == EncounterReactionChoice.Counter)
            {
                BeginCounterAction(session);
                return;
            }

            BeginEnemyAction(session);
            return;
        }

        session.State.CompleteDecision(Time.time, encounterPlayerAttackWindowSeconds);
        SendSnapshot(session, session.State.LastMessage);
    }

    private void CompleteLockedPlayerAttack(CombatSession session)
    {
        if (session == null || !session.State.PlayerActionLocked || session.State.Finished)
        {
            return;
        }

        if (!session.PendingPlayerActionResolved)
        {
            CombatRuntimeEnemy activeEnemy = GetActiveEnemy(session);
            if (activeEnemy == null)
            {
                BeginCombatResolution(session, true, "Victoire.");
                return;
            }

            ResolveLockedPlayerAttackImpact(session);
        }

        string message = string.IsNullOrWhiteSpace(session.PendingPlayerActionResultMessage)
            ? "L'attaque se termine."
            : session.PendingPlayerActionResultMessage;
        ClearPendingPlayerAction(session);

        BeginCombatResolution(session, true, $"Victoire. {message}");
    }

    private void BeginCounterAction(CombatSession session)
    {
        if (session == null || session.State.Finished)
        {
            return;
        }

        CombatRuntimeEnemy enemy = GetActiveEnemy(session);
        if (enemy == null)
        {
            BeginCombatResolution(session, true, "Victoire.");
            return;
        }

        int pendingDamage = Mathf.Max(1, enemy.CurrentHp);
        CombatActionTiming actionTiming = PlayPlayerCounterPresentation(session);
        session.State.BeginActiveTurn(CombatTurn.Player, Time.time, actionTiming.TotalDuration, "Contre reussi.");
        PreparePendingPlayerAction(session, actionTiming);
        session.State.BeginPlayerAction(pendingDamage, Time.time, actionTiming.TotalDuration, $"Contre sur {enemy.DisplayName}.");
        SendSnapshot(session, session.State.LastMessage);
        RefreshGlobalCombatDefensiveReaction();
    }

    private void PreparePendingPlayerAction(CombatSession session, CombatActionTiming timing)
    {
        if (session == null)
        {
            return;
        }

        session.PendingPlayerActionImpactAt = Time.time + timing.ImpactDelay;
        session.PendingPlayerActionResolved = false;
        session.PendingPlayerActionResultMessage = string.Empty;
        session.PendingPlayerActionVictory = false;
    }

    private void ResolveLockedPlayerAttackImpact(CombatSession session)
    {
        if (session == null || session.PendingPlayerActionResolved || session.State.Finished)
        {
            return;
        }

        session.PendingPlayerActionResolved = true;
        CombatRuntimeEnemy enemy = GetActiveEnemy(session);
        if (enemy == null)
        {
            session.PendingPlayerActionVictory = true;
            session.PendingPlayerActionResultMessage = "Victoire.";
            return;
        }

        int damage = session.State.ConsumePendingPlayerAttackDamage(clearActionTimer: false);
        int applied = enemy.ApplyDamage(damage);
        PlayActionAudio(ActionAudioCue.CombatHit, ResolveCombatAudioPosition(session, preferEnemy: true));
        TimeManager.EnsureInstance().TriggerCombatHitStop(
            session.Player != null ? session.Player.transform : null,
            session.SourceEnemy != null ? session.SourceEnemy.transform : null);

        session.PendingPlayerActionVictory = AreAllEnemiesDefeated(session);
        session.PendingPlayerActionResultMessage = $"{enemy.DisplayName} subit {applied} degats.";
        session.State.SetMessage(session.PendingPlayerActionResultMessage);
        SendSnapshot(session, session.PendingPlayerActionResultMessage);
    }

    private void ExecuteEnemyTurn(CombatSession session)
    {
        if (session == null || session.State.Finished || session.State.Turn != CombatTurn.Enemy)
        {
            return;
        }

        CombatRuntimeEnemy enemy = GetActiveEnemy(session);
        if (enemy == null)
        {
            BeginCombatResolution(session, true, "Victoire.");
            return;
        }

        BeginEnemyAction(session);
    }

    private void BeginEnemyAction(CombatSession session)
    {
        if (session == null || session.State.Finished || session.State.Turn != CombatTurn.Enemy)
        {
            return;
        }

        CombatRuntimeEnemy enemy = GetActiveEnemy(session);
        if (enemy == null)
        {
            BeginCombatResolution(session, true, "Victoire.");
            return;
        }

        int enemyIndex = GetActiveEnemyIndex(session);
        CombatEnemyAttackDefinition attack = enemy.SelectNextAttack(out int attackIndex);
        session.PendingEnemyIndex = enemyIndex;
        session.PendingEnemyAttackIndex = attackIndex;
        session.PendingEnemyAttack = attack;

        string attackName = ResolveEnemyAttackDisplayName(attack);
        CombatActionTiming actionTiming = PlayEnemyBasicAttackPresentation(
            session,
            attack,
            enemyIndex,
            attackIndex);
        if (session.PendingEncounterReaction == EncounterReactionChoice.Defend)
        {
            PlayPlayerDefensePresentation(session);
        }

        PreparePendingEnemyAction(session, actionTiming);
        session.State.BeginEnemyAction(Time.time, actionTiming.TotalDuration, $"{enemy.DisplayName} utilise {attackName}.");
        SendSnapshot(session, session.State.LastMessage);
        RefreshGlobalCombatDefensiveReaction();
    }

    private void PreparePendingEnemyAction(CombatSession session, CombatActionTiming timing)
    {
        if (session == null)
        {
            return;
        }

        session.PendingEnemyActionImpactAt = Time.time + timing.ImpactDelay;
        session.PendingEnemyActionResolved = false;
        session.PendingEnemyActionResultMessage = string.Empty;
        session.PendingEnemyActionPlayerDefeated = false;
        session.PendingEnemyActionOpensPlayerAttackWindow = false;
    }

    private void ResolveEnemyActionImpact(CombatSession session)
    {
        if (session == null || session.PendingEnemyActionResolved || session.State.Finished)
        {
            return;
        }

        session.PendingEnemyActionResolved = true;
        CombatRuntimeEnemy enemy = GetActiveEnemy(session);
        if (enemy == null)
        {
            session.PendingEnemyActionResultMessage = "Victoire.";
            return;
        }

        int supportCount = CountPrayerSupport(session);
        float reduction = ResolvePrayerReduction(supportCount);
        CombatEnemyAttackDefinition attack = session.PendingEnemyAttack;
        string attackName = ResolveEnemyAttackDisplayName(attack);
        int rawDamage = ResolveEnemyAttackDamage(enemy, attack);
        int finalDamage = ResolveReducedDamage(rawDamage, reduction);
        int absorbedDamage = 0;
        bool defensiveItemBroken = false;
        Item defensiveItem = session.PendingDefensiveItem;
        EncounterReactionChoice reaction = ResolveEncounterReaction(session);
        if (reaction == EncounterReactionChoice.Defend)
        {
            if (TryApplyPendingDefensiveItem(session, ref finalDamage, out absorbedDamage, out defensiveItemBroken))
            {
                session.State.SetMessage($"{ResolveItemDisplayName(defensiveItem)} encaisse l'attaque.");
            }

            finalDamage = 0;
            session.PendingEnemyActionOpensPlayerAttackWindow = true;
        }
        else
        {
            finalDamage = session.Player != null ? Mathf.Max(1, session.Player.CurrentHp) : Mathf.Max(1, finalDamage);
        }

        int applied = session.Player != null ? session.Player.ApplyDamage(finalDamage, "combat") : 0;
        PlayActionAudio(ActionAudioCue.CombatHit, ResolveCombatAudioPosition(session, preferEnemy: false));
        TimeManager.EnsureInstance().TriggerCombatHitStop(
            session.SourceEnemy != null ? session.SourceEnemy.transform : null,
            session.Player != null ? session.Player.transform : null);

        string message = reaction == EncounterReactionChoice.Defend
            ? BuildDefendedEnemyAttackResultMessage(enemy.DisplayName, attackName, defensiveItem, absorbedDamage, defensiveItemBroken)
            : BuildEnemyAttackResultMessage(
                enemy.DisplayName,
                attackName,
                applied,
                defensiveItem,
                absorbedDamage,
                defensiveItemBroken);
        if (supportCount > 0 && reaction == EncounterReactionChoice.Defend)
        {
            message = $"{message} Prieres: -{Mathf.RoundToInt(reduction * 100f)}%.";
        }

        session.PendingEnemyActionPlayerDefeated = session.Player == null || session.Player.CurrentHp <= 0;
        session.PendingEnemyActionResultMessage = message;
        session.State.SetMessage(message);
        SendSnapshot(session, message);
    }

    private bool TryResolveCombatAnimationImpact(CombatSession session)
    {
        if (session == null || session.State.Finished)
        {
            return false;
        }

        if (session.State.PlayerActionLocked && !session.PendingPlayerActionResolved)
        {
            ResolveLockedPlayerAttackImpact(session);
            return true;
        }

        if (session.State.EnemyActionLocked && !session.PendingEnemyActionResolved)
        {
            ResolveEnemyActionImpact(session);
            return true;
        }

        return false;
    }

    private void CompleteEnemyAction(CombatSession session)
    {
        if (session == null || !session.State.EnemyActionLocked || session.State.Finished)
        {
            return;
        }

        CombatRuntimeEnemy enemy = GetActiveEnemy(session);
        if (enemy == null)
        {
            BeginCombatResolution(session, true, "Victoire.");
            return;
        }

        if (!session.PendingEnemyActionResolved)
        {
            ResolveEnemyActionImpact(session);
        }

        session.State.CompleteEnemyAction();

        string message = string.IsNullOrWhiteSpace(session.PendingEnemyActionResultMessage)
            ? $"{enemy.DisplayName} termine son attaque."
            : session.PendingEnemyActionResultMessage;
        bool playerDefeated = session.PendingEnemyActionPlayerDefeated;
        bool opensAttackWindow = session.PendingEnemyActionOpensPlayerAttackWindow;
        ClearPendingEnemyAttack(session);
        ClearPendingDefensiveItem(session);
        ClearPendingEnemyAction(session);
        ClearPendingEncounterReaction(session);

        if (playerDefeated)
        {
            BeginCombatResolution(session, false, $"Defaite. {message}");
            return;
        }

        if (opensAttackWindow)
        {
            BeginPlayerAttackWindow(session, $"{message} Attaquez maintenant.");
            return;
        }

        BeginEncounterPlayerDeath(session, message);
    }

    private void BeginTurn(CombatSession session, CombatTurn turn, string message)
    {
        if (session == null || session.State.Finished || session.State.Resolving)
        {
            return;
        }

        ClearPendingEnemyAttack(session);
        ClearPendingDefensiveItem(session);
        ClearPendingPlayerAction(session);
        ClearPendingEnemyAction(session);
        ClearPendingEncounterReaction(session);
        float decisionSeconds = turn == CombatTurn.Enemy
            ? Mathf.Max(Mathf.Max(decisionPresentationSeconds, enemyActionDelay), defensiveReactionSeconds)
            : decisionPresentationSeconds;
        session.State.BeginDecision(turn, Time.time, decisionSeconds, turnDurationSeconds, message);
        PlayActionAudio(ActionAudioCue.CombatTurn, ResolveCombatAudioPosition(session, preferEnemy: turn == CombatTurn.Enemy));
        SendSnapshot(session, session.State.LastMessage);
        RefreshGlobalCombatDefensiveReaction();
    }

    private void BeginPlayerAttackWindow(CombatSession session, string message)
    {
        if (session == null || session.State.Finished || session.State.Resolving)
        {
            return;
        }

        ClearPendingEnemyAttack(session);
        ClearPendingDefensiveItem(session);
        ClearPendingPlayerAction(session);
        ClearPendingEnemyAction(session);
        ClearPendingEncounterReaction(session);
        session.State.BeginActiveTurn(CombatTurn.Player, Time.time, encounterPlayerAttackWindowSeconds, message);
        PlayActionAudio(ActionAudioCue.CombatTurn, ResolveCombatAudioPosition(session, preferEnemy: false));
        SendSnapshot(session, session.State.LastMessage);
        RefreshGlobalCombatDefensiveReaction();
    }

    private CombatActionTiming PlayPlayerBasicAttackPresentation(CombatSession session)
    {
        if (session?.Player == null)
        {
            return CreateActionTiming(DefaultBasicAttackAnimationDuration);
        }

        Animator animator = session.Player.GetComponent<Animator>();
        float animationDuration = ResolveAnimationDuration(animator, BasicAttackAnimationName, DefaultBasicAttackAnimationDuration);
        CombatActionMotionPlan motion = BuildCombatActionMotionPlan(
            session.Player.transform,
            session.SourceEnemy != null ? session.SourceEnemy.transform : null,
            animator,
            BasicAttackAnimationName,
            CombatAttackRangeType.Melee,
            CombatAttackMovementMode.Auto,
            defaultMeleeApproachDistance,
            animationDuration);

        if (!IsNetworkSessionActive() || session.OwnerClientId == ResolveLocalClientId())
        {
            StartCombatActionPresentation(
                session.Player.transform,
                session.Player,
                null,
                session.SourceEnemy != null ? session.SourceEnemy.transform : null,
                motion,
                () =>
                {
                    PlayBasicAttackAnimationLocally(session.Player);
                });
        }

        if (IsNetworkSessionActive() &&
            IsSpawned &&
            session.OwnerClientId != ResolveLocalClientId())
        {
            PlayPlayerBasicAttackClientRpc(session.SessionId, BuildClientRpcParams(session.OwnerClientId));
        }

        return CreateActionTiming(motion);
    }

    private CombatActionTiming PlayPlayerCounterPresentation(CombatSession session)
    {
        if (session?.Player == null)
        {
            return CreateActionTiming(DefaultBasicAttackAnimationDuration);
        }

        Animator animator = session.Player.GetComponent<Animator>();
        string animationName = ResolveAvailableActionAnimationName(animator, CounterAnimationName, BasicAttackAnimationName);
        float animationDuration = ResolveAnimationDuration(animator, animationName, DefaultBasicAttackAnimationDuration);
        CombatActionMotionPlan motion = BuildCombatActionMotionPlan(
            session.Player.transform,
            session.SourceEnemy != null ? session.SourceEnemy.transform : null,
            animator,
            animationName,
            CombatAttackRangeType.Melee,
            CombatAttackMovementMode.Auto,
            defaultMeleeApproachDistance,
            animationDuration);

        if (!IsNetworkSessionActive() || session.OwnerClientId == ResolveLocalClientId())
        {
            StartCombatActionPresentation(
                session.Player.transform,
                session.Player,
                null,
                session.SourceEnemy != null ? session.SourceEnemy.transform : null,
                motion,
                () =>
                {
                    PlayPlayerActionAnimationLocally(session.Player, animationName, DefaultBasicAttackAnimationDuration);
                });
        }

        if (IsNetworkSessionActive() &&
            IsSpawned &&
            session.OwnerClientId != ResolveLocalClientId())
        {
            PlayPlayerCounterClientRpc(session.SessionId, BuildClientRpcParams(session.OwnerClientId));
        }

        return CreateActionTiming(motion);
    }

    private void PlayPlayerDefensePresentation(CombatSession session)
    {
        if (session?.Player == null)
        {
            return;
        }

        if (!IsNetworkSessionActive() || session.OwnerClientId == ResolveLocalClientId())
        {
            PlayDefenseAnimationLocally(session.Player);
        }

        if (IsNetworkSessionActive() &&
            IsSpawned &&
            session.OwnerClientId != ResolveLocalClientId())
        {
            PlayPlayerDefenseClientRpc(session.SessionId, BuildClientRpcParams(session.OwnerClientId));
        }
    }

    private CombatActionTiming PlayEnemyBasicAttackPresentation(
        CombatSession session,
        CombatEnemyAttackDefinition attack,
        int enemyIndex,
        int attackIndex)
    {
        Animator animator = session?.SourceEnemy != null ? session.SourceEnemy.ResolveAnimator() : null;
        string animationName = ResolveEnemyPresentationAnimationName(attack, animator);
        float animationDuration = ResolveAnimationDuration(animator, animationName, DefaultEnemyAttackAnimationDuration);
        bool useAnimationEventPresentation = IsJuggernautGriffeAttack(attack, animator);
        CombatActionMotionPlan motion = useAnimationEventPresentation
            ? default
            : BuildCombatActionMotionPlan(
                session?.SourceEnemy != null ? session.SourceEnemy.transform : null,
                session?.Player != null ? session.Player.transform : null,
                animator,
                animationName,
                ResolveEnemyAttackRangeType(attack, animationName),
                ResolveEnemyAttackMovementMode(attack),
                ResolveEnemyAttackApproachDistance(attack),
                animationDuration);

        if (!IsNetworkSessionActive() || session.OwnerClientId == ResolveLocalClientId())
        {
            Transform actor = session.SourceEnemy != null ? session.SourceEnemy.transform : null;
            Transform target = session.Player != null ? session.Player.transform : null;
            Action playAttack = useAnimationEventPresentation
                ? () => PlayNamedAnimation(animator, animationName, DefaultEnemyAttackAnimationDuration)
                : () => PlayEnemyBasicAttackAnimationLocally(session, attack, animationName);

            if (useAnimationEventPresentation)
            {
                playAttack();
            }
            else
            {
                StartCombatActionPresentation(
                    actor,
                    null,
                    session.SourceEnemy,
                    target,
                    motion,
                    playAttack);
            }
        }

        if (IsNetworkSessionActive() &&
            IsSpawned &&
            session.OwnerClientId != ResolveLocalClientId())
        {
            PlayEnemyBasicAttackClientRpc(
                session.SessionId,
                enemyIndex,
                attackIndex,
                BuildClientRpcParams(session.OwnerClientId));
        }

        CombatActionTiming timing = useAnimationEventPresentation
            ? CreateActionTiming(animationDuration)
            : CreateActionTiming(motion);
        return ApplyCombatTimeProfile(timing, TimeManager.CombatPresentationTimeProfile.EnemyAction);
    }

    [ClientRpc]
    private void PlayPlayerBasicAttackClientRpc(string sessionId, ClientRpcParams rpcParams = default)
    {
        if (IsServer ||
            !localCombatPresentation.Active ||
            localCombatPresentation.SessionId != sessionId)
        {
            return;
        }

        SquadCharacterController controller = ResolveControllerForClient(ResolveLocalClientId());
        if (controller != null)
        {
            Transform target = ResolveLocalCombatEnemyTransform(localCombatPresentation);
            Animator animator = controller.GetComponent<Animator>();
            float animationDuration = ResolveAnimationDuration(animator, BasicAttackAnimationName, DefaultBasicAttackAnimationDuration);
            CombatActionMotionPlan motion = BuildCombatActionMotionPlan(
                controller.transform,
                target,
                animator,
                BasicAttackAnimationName,
                CombatAttackRangeType.Melee,
                CombatAttackMovementMode.Auto,
                defaultMeleeApproachDistance,
                animationDuration);
            StartCombatActionPresentation(
                controller.transform,
                controller,
                null,
                target,
                motion,
                () =>
                {
                    PlayBasicAttackAnimationLocally(controller);
            });
        }
    }

    [ClientRpc]
    private void PlayPlayerCounterClientRpc(string sessionId, ClientRpcParams rpcParams = default)
    {
        if (IsServer ||
            !localCombatPresentation.Active ||
            localCombatPresentation.SessionId != sessionId)
        {
            return;
        }

        SquadCharacterController controller = ResolveControllerForClient(ResolveLocalClientId());
        if (controller == null)
        {
            return;
        }

        Transform target = ResolveLocalCombatEnemyTransform(localCombatPresentation);
        Animator animator = controller.GetComponent<Animator>();
        string animationName = ResolveAvailableActionAnimationName(animator, CounterAnimationName, BasicAttackAnimationName);
        float animationDuration = ResolveAnimationDuration(animator, animationName, DefaultBasicAttackAnimationDuration);
        CombatActionMotionPlan motion = BuildCombatActionMotionPlan(
            controller.transform,
            target,
            animator,
            animationName,
            CombatAttackRangeType.Melee,
            CombatAttackMovementMode.Auto,
            defaultMeleeApproachDistance,
            animationDuration);
        StartCombatActionPresentation(
            controller.transform,
            controller,
            null,
            target,
            motion,
            () =>
            {
                PlayPlayerActionAnimationLocally(controller, animationName, DefaultBasicAttackAnimationDuration);
            });
    }

    [ClientRpc]
    private void PlayPlayerDefenseClientRpc(string sessionId, ClientRpcParams rpcParams = default)
    {
        if (IsServer ||
            !localCombatPresentation.Active ||
            localCombatPresentation.SessionId != sessionId)
        {
            return;
        }

        PlayDefenseAnimationLocally(ResolveControllerForClient(ResolveLocalClientId()));
    }

    [ClientRpc]
    private void PlayEnemyBasicAttackClientRpc(
        string sessionId,
        int enemyIndex,
        int attackIndex,
        ClientRpcParams rpcParams = default)
    {
        if (IsServer ||
            !localCombatPresentation.Active ||
            localCombatPresentation.SessionId != sessionId)
        {
            return;
        }

        PlayEnemyBasicAttackPresentationLocally(sessionId, enemyIndex, attackIndex);
    }

    private void BeginCombatResolution(CombatSession session, bool playerVictory, string message)
    {
        if (session == null || session.State.Finished || session.State.Resolving)
        {
            return;
        }

        session.State.BeginResolution(
            playerVictory,
            Time.time,
            ResolveCombatResolutionDuration(session, playerVictory),
            message);
        RefreshGlobalCombatDefensiveReaction();

        PlayActionAudio(
            playerVictory ? ActionAudioCue.CombatVictory : ActionAudioCue.CombatDefeat,
            ResolveCombatAudioPosition(session, preferEnemy: playerVictory));
        SendSnapshot(session, session.State.LastMessage);

        if (IsNetworkSessionActive() && IsSpawned)
        {
            CombatResolutionClientRpc(
                session.SessionId,
                playerVictory,
                session.Player != null ? session.Player.CurrentHp : 0,
                session.Player != null ? session.Player.MaxHp : 1,
                BuildClientRpcParams(session.OwnerClientId));
        }
    }

    private void BeginEncounterPlayerDeath(CombatSession session, string message)
    {
        if (session?.Player != null && session.Player.CurrentHp > 0)
        {
            session.Player.ApplyDamage(session.Player.CurrentHp, "combat");
        }

        BeginCombatResolution(session, false, message);
    }

    [ClientRpc]
    private void CombatResolutionClientRpc(
        string sessionId,
        bool playerVictory,
        int playerHp,
        int playerMaxHp,
        ClientRpcParams rpcParams = default)
    {
        if (IsServer)
        {
            return;
        }

        if (localCombatPresentation.Active && localCombatPresentation.SessionId == sessionId)
        {
            localCombatPresentation.Resolving = true;
            localCombatPresentation.Phase = CombatSessionPhase.Resolving;
            localCombatPresentation.ResolutionPlayerVictory = playerVictory;
        }

        AudioManager.EnsureInstance()?.PlayActionCue(
            playerVictory ? ActionAudioCue.CombatVictory : ActionAudioCue.CombatDefeat,
            ResolveLocalCombatAudioPosition(playerVictory));

        SquadCharacterController controller = ResolveControllerForClient(ResolveLocalClientId());
        if (controller != null)
        {
            controller.SetHealth(playerHp, Mathf.Max(1, playerMaxHp));
        }

        if (playerVictory)
        {
            if (localEnemyPresentationsBySessionId.TryGetValue(sessionId, out LocalEnemyPresentation presentation))
            {
                PlayDeathAnimation(presentation.Enemy != null ? presentation.Enemy.ResolveAnimator() : null);
            }

            return;
        }

        if (controller != null)
        {
            controller.Stop();
            PlayDeathAnimation(controller.GetComponent<Animator>());
        }
    }

    private void EndCombat(CombatSession session, bool playerVictory, string message, bool notifyClient)
    {
        if (session == null || session.State.Finished)
        {
            return;
        }

        StopCombatActionPresentations(session);
        session.State.Finish();
        SendSnapshot(session, message);

        int playerHp = session.Player != null ? session.Player.CurrentHp : 0;
        int playerMaxHp = session.Player != null ? session.Player.MaxHp : 1;
        int enemyRemainingHp = playerVictory ? 0 : ResolveDisplayedEnemyRemainingHp(session);

        if (session.Player != null)
        {
            MoveCharacterTo(session.Player, session.ReturnPosition, session.ReturnRotation);
            session.Player.SetHealth(playerHp, Mathf.Max(1, playerMaxHp));
            session.Player.Stop();
            if (session.SuppressedMovement)
            {
                session.Player.PopScriptedMovementSuppression();
            }
        }

        if (session.SourceEnemy != null && session.HasEnemyPresentation)
        {
            MoveCombatAggroEnemyTo(session.SourceEnemy, session.EnemyReturnPosition, session.EnemyReturnRotation);
        }

        session.SourceEnemy?.FinalizeCombatResult(playerVictory, enemyRemainingHp);
        sessionsByCharacterId.Remove(session.CharacterId);
        sessionsByClientId.Remove(session.OwnerClientId);
        RefreshGlobalCombatDefensiveReaction();

        if (notifyClient)
        {
            SendExitCombat(session, message, playerVictory, enemyRemainingHp);
        }
    }

    private void SendEnterCombat(CombatSession session)
    {
        if (session == null)
        {
            return;
        }

        if (IsNetworkSessionActive() && IsSpawned)
        {
            CombatEnterData enterData = new CombatEnterData(
                session.SessionId,
                session.PlayerCombatPosition,
                session.PlayerCombatRotation,
                session.HasEnemyPresentation,
                session.EnemyReturnPosition,
                session.EnemyReturnRotation,
                session.EnemyCombatPosition,
                session.EnemyCombatRotation);
            EnterCombatClientRpc(enterData, BuildClientRpcParams(session.OwnerClientId));
            return;
        }

        CombatHudController.EnsureInstance();
        CombatCameraPresentationController.EnsureInstance();
        CombatTransitionController.EnsureInstance().PlayEnterTransition();
    }

    private void SendExitCombat(CombatSession session, string message, bool playerVictory, int enemyRemainingHp)
    {
        if (session == null)
        {
            return;
        }

        if (IsNetworkSessionActive() && IsSpawned)
        {
            CombatExitData exitData = new CombatExitData(
                session.SessionId,
                message,
                session.ReturnPosition,
                session.ReturnRotation,
                session.Player != null ? session.Player.CurrentHp : 0,
                session.Player != null ? session.Player.MaxHp : 1,
                session.HasEnemyPresentation,
                session.EnemyReturnPosition,
                session.EnemyReturnRotation,
                enemyRemainingHp,
                playerVictory);
            ExitCombatClientRpc(exitData, BuildClientRpcParams(session.OwnerClientId));
            return;
        }

        CombatExitData localExitData = new CombatExitData(
            session.SessionId,
            message,
            session.ReturnPosition,
            session.ReturnRotation,
            session.Player != null ? session.Player.CurrentHp : 0,
            session.Player != null ? session.Player.MaxHp : 1,
            session.HasEnemyPresentation,
            session.EnemyReturnPosition,
            session.EnemyReturnRotation,
            enemyRemainingHp,
            playerVictory);
        CombatTransitionController.EnsureInstance().PlayExitTransition(() =>
        {
            ApplyExitCombatPresentation(localExitData, restoreLocalClient: false);
        });
    }

    private void SendSnapshot(CombatSession session, string message)
    {
        if (session == null)
        {
            return;
        }

        session.State.ScheduleNextSnapshot(Time.time, snapshotInterval);
        CombatRuntimeEnemy enemy = GetActiveEnemy(session);
        int aliveEnemies = CountAliveEnemies(session);
        int totalEnemies = session.Enemies != null ? session.Enemies.Count : 0;
        int supportCount = CountPrayerSupport(session);
        float reduction = ResolvePrayerReduction(supportCount);
        float timerRemaining = session.State.GetTimerRemaining(Time.time);
        CombatTurn turn = session.State.Turn;

        if (IsNetworkSessionActive() && IsSpawned)
        {
            CombatSnapshotData snapshot = new CombatSnapshotData(
                session.SessionId,
                turn,
                session.State.Phase,
                timerRemaining,
                session.Player != null ? session.Player.CurrentHp : 0,
                session.Player != null ? session.Player.MaxHp : 1,
                enemy != null ? enemy.DisplayName : "Ennemi",
                enemy != null ? enemy.CurrentHp : 0,
                enemy != null ? enemy.MaxHp : 1,
                aliveEnemies,
                totalEnemies,
                supportCount,
                reduction,
                session.State.ActionLocked,
                message ?? string.Empty);
            CombatSnapshotClientRpc(snapshot, BuildClientRpcParams(session.OwnerClientId));
            return;
        }

        CombatHudController.EnsureInstance().ShowSnapshot(
            session.SessionId,
            (CombatHudController.TurnState)turn,
            session.State.Phase,
            timerRemaining,
            session.Player != null ? session.Player.CurrentHp : 0,
            session.Player != null ? session.Player.MaxHp : 1,
            enemy != null ? enemy.DisplayName : "Ennemi",
            enemy != null ? enemy.CurrentHp : 0,
            enemy != null ? enemy.MaxHp : 1,
            aliveEnemies,
            totalEnemies,
            supportCount,
            reduction,
            session.State.ActionLocked,
            message ?? string.Empty);
    }

    private void SetPrayerState(ulong clientId, SquadCharacterController controller, bool active, bool sendFeedback)
    {
        if (!active)
        {
            StopPrayer(clientId, sendFeedback);
            return;
        }

        if (controller == null)
        {
            SendPrayerFeedback(clientId, false, "Personnage introuvable.");
            return;
        }

        if (TryGetSession(controller, out _))
        {
            SendPrayerFeedback(clientId, false, "Impossible de prier en combat.");
            return;
        }

        if (!IustiaIdolPrayer.IsAnyIdolInRange(controller))
        {
            SendPrayerFeedback(clientId, false, "Aucune Idole de Iustia a portee.");
            return;
        }

        activePrayersByClientId[clientId] = new PrayerState
        {
            ClientId = clientId,
            CharacterId = ResolveCharacterId(controller),
            LastValidationTime = Time.time
        };

        if (sendFeedback)
        {
            SendPrayerFeedback(clientId, true, "Priere a Iustia commencee.");
        }
    }

    private void StopPrayer(ulong clientId, bool sendFeedback)
    {
        bool removed = activePrayersByClientId.Remove(clientId);
        if (sendFeedback)
        {
            SendPrayerFeedback(clientId, false, removed ? "Priere interrompue." : string.Empty);
        }
    }

    private void SendPrayerFeedback(ulong clientId, bool active, string message)
    {
        if (IsNetworkSessionActive() && IsSpawned)
        {
            PrayerFeedbackClientRpc(active, message ?? string.Empty, BuildClientRpcParams(clientId));
            return;
        }

        IustiaIdolPrayer.SetLocalPrayerState(active);
        if (!string.IsNullOrWhiteSpace(message))
        {
            InfoBoxUI.TryShow(message);
        }
    }

    private void SendDefensiveItemFeedback(ulong clientId, string message, ActionAudioCue cue)
    {
        if (IsNetworkSessionActive() && IsSpawned)
        {
            DefensiveItemFeedbackClientRpc(message ?? string.Empty, cue, BuildClientRpcParams(clientId));
            return;
        }

        PlayUiFeedbackAudio(cue);
        if (!string.IsNullOrWhiteSpace(message))
        {
            InfoBoxUI.TryShow(message);
        }
    }

    private static void PlayUiFeedbackAudio(ActionAudioCue cue)
    {
        if (cue == ActionAudioCue.None)
        {
            return;
        }

        AudioManager manager = AudioManager.EnsureInstance();
        if (manager != null)
        {
            manager.PlayUiActionCue(cue);
        }
    }

    private void ValidatePrayerStates()
    {
        if (activePrayersByClientId.Count == 0)
        {
            return;
        }

        List<ulong> toRemove = null;
        foreach (KeyValuePair<ulong, PrayerState> pair in activePrayersByClientId)
        {
            if (Time.time < pair.Value.LastValidationTime + 0.25f)
            {
                continue;
            }

            SquadCharacterController controller = ResolveControllerForClient(pair.Key);
            bool valid = controller != null
                && !TryGetSession(controller, out _)
                && IustiaIdolPrayer.IsAnyIdolInRange(controller);

            pair.Value.LastValidationTime = Time.time;
            if (valid)
            {
                continue;
            }

            toRemove ??= new List<ulong>();
            toRemove.Add(pair.Key);
        }

        if (toRemove == null)
        {
            return;
        }

        for (int i = 0; i < toRemove.Count; i++)
        {
            StopPrayer(toRemove[i], sendFeedback: true);
        }
    }

    private int CountPrayerSupport(CombatSession session)
    {
        if (session == null || activePrayersByClientId.Count == 0)
        {
            return 0;
        }

        int count = 0;
        foreach (PrayerState state in activePrayersByClientId.Values)
        {
            if (state.ClientId == session.OwnerClientId)
            {
                continue;
            }

            count++;
        }

        return count;
    }

    private float ResolvePrayerReduction(int supportCount)
    {
        return Mathf.Clamp(supportCount * Mathf.Max(0f, prayerDamageReductionPerPlayer), 0f, Mathf.Clamp01(maxPrayerDamageReduction));
    }

    private int ResolveReducedDamage(int rawDamage, float reduction)
    {
        if (rawDamage <= 0)
        {
            return 0;
        }

        int reduced = Mathf.FloorToInt(rawDamage * (1f - Mathf.Clamp01(reduction)));
        return Mathf.Max(1, reduced);
    }

    private bool TryApplyPendingDefensiveItem(
        CombatSession session,
        ref int finalDamage,
        out int absorbedDamage,
        out bool itemBroken)
    {
        absorbedDamage = 0;
        itemBroken = false;
        if (session == null || session.PendingDefensiveItem == null || finalDamage <= 0)
        {
            return false;
        }

        Item item = session.PendingDefensiveItem;
        int itemHitPoints = Mathf.Max(0, session.PendingDefensiveItemHitPoints);
        if (itemHitPoints <= 0 || !TryFindControllerItem(session.Player, item, out Item inventoryItem))
        {
            return false;
        }

        absorbedDamage = Mathf.Min(finalDamage, itemHitPoints);
        itemBroken = finalDamage >= itemHitPoints;
        if (itemBroken && !TryConsumeBrokenDefensiveItem(session, inventoryItem))
        {
            absorbedDamage = 0;
            itemBroken = false;
            return false;
        }

        finalDamage = Mathf.Max(0, finalDamage - absorbedDamage);
        return true;
    }

    private bool TryConsumeBrokenDefensiveItem(CombatSession session, Item item)
    {
        if (session?.Player == null || item == null)
        {
            return false;
        }

        if (!session.Player.TryRemoveItemQuantity(item, 1))
        {
            return false;
        }

        SyncNetworkInventory(session.Player);
        return true;
    }

    private static string BuildEnemyAttackResultMessage(
        string enemyName,
        string attackName,
        int appliedDamage,
        Item defensiveItem,
        int absorbedDamage,
        bool defensiveItemBroken)
    {
        string resolvedEnemyName = string.IsNullOrWhiteSpace(enemyName) ? "Ennemi" : enemyName;
        string resolvedAttackName = string.IsNullOrWhiteSpace(attackName) ? DefaultEnemyAttackDisplayName : attackName;
        if (defensiveItem == null || absorbedDamage <= 0)
        {
            return $"{resolvedEnemyName} utilise {resolvedAttackName} et inflige {appliedDamage} degats.";
        }

        string itemName = ResolveItemDisplayName(defensiveItem);
        string breakText = defensiveItemBroken ? " et se casse" : string.Empty;
        string passText = appliedDamage > 0
            ? $"{appliedDamage} degats passent."
            : "Aucun degat ne passe.";
        return $"{resolvedEnemyName} utilise {resolvedAttackName}. {itemName} absorbe {absorbedDamage} degats{breakText}. {passText}";
    }

    private static string BuildDefendedEnemyAttackResultMessage(
        string enemyName,
        string attackName,
        Item defensiveItem,
        int absorbedDamage,
        bool defensiveItemBroken)
    {
        string resolvedEnemyName = string.IsNullOrWhiteSpace(enemyName) ? "Ennemi" : enemyName;
        string resolvedAttackName = string.IsNullOrWhiteSpace(attackName) ? DefaultEnemyAttackDisplayName : attackName;
        if (defensiveItem == null || absorbedDamage <= 0)
        {
            return $"{resolvedEnemyName} utilise {resolvedAttackName}. Defense reussie.";
        }

        string itemName = ResolveItemDisplayName(defensiveItem);
        string breakText = defensiveItemBroken ? " et se casse" : string.Empty;
        return $"{resolvedEnemyName} utilise {resolvedAttackName}. {itemName} absorbe {absorbedDamage} degats{breakText}. Defense reussie.";
    }

    private static int ResolveEnemyAttackDamage(CombatRuntimeEnemy enemy, CombatEnemyAttackDefinition attack)
    {
        if (attack != null)
        {
            return Mathf.Max(0, attack.damage);
        }

        return Mathf.Max(0, enemy != null ? enemy.AttackDamage : 0);
    }

    private static string ResolveEnemyAttackDisplayName(CombatEnemyAttackDefinition attack)
    {
        if (attack != null && !string.IsNullOrWhiteSpace(attack.displayName))
        {
            return attack.displayName;
        }

        return DefaultEnemyAttackDisplayName;
    }

    private static string ResolveEnemyAttackAnimationName(CombatEnemyAttackDefinition attack)
    {
        if (attack != null && !string.IsNullOrWhiteSpace(attack.animationName))
        {
            return attack.animationName;
        }

        return BasicAttackAnimationName;
    }

    private static string ResolveEnemyPresentationAnimationName(CombatEnemyAttackDefinition attack, Animator animator)
    {
        return IsJuggernautGriffeAttack(attack, animator)
            ? JuggernautGriffeAnimationName
            : ResolveEnemyAttackAnimationName(attack);
    }

    private static bool IsJuggernautGriffeAttack(CombatEnemyAttackDefinition attack, Animator animator)
    {
        if (attack == null)
        {
            return false;
        }

        bool attackNameMatches =
            string.Equals(attack.displayName, JuggernautGriffeDisplayName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(attack.animationName, JuggernautGriffeDisplayName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(attack.animationName, JuggernautGriffeAnimationName, StringComparison.OrdinalIgnoreCase);
        return attackNameMatches && HasAnimatorStateOrTrigger(animator, JuggernautGriffeAnimationName);
    }

    private static void ClearPendingEnemyAttack(CombatSession session)
    {
        if (session == null)
        {
            return;
        }

        session.PendingEnemyIndex = -1;
        session.PendingEnemyAttackIndex = -1;
        session.PendingEnemyAttack = null;
    }

    private static void ClearPendingPlayerAction(CombatSession session)
    {
        if (session == null)
        {
            return;
        }

        session.PendingPlayerActionImpactAt = 0f;
        session.PendingPlayerActionResolved = false;
        session.PendingPlayerActionResultMessage = string.Empty;
        session.PendingPlayerActionVictory = false;
    }

    private static void ClearPendingEnemyAction(CombatSession session)
    {
        if (session == null)
        {
            return;
        }

        session.PendingEnemyActionImpactAt = 0f;
        session.PendingEnemyActionResolved = false;
        session.PendingEnemyActionResultMessage = string.Empty;
        session.PendingEnemyActionPlayerDefeated = false;
        session.PendingEnemyActionOpensPlayerAttackWindow = false;
    }

    private static void ClearPendingDefensiveItem(CombatSession session)
    {
        if (session == null)
        {
            return;
        }

        session.PendingDefensiveItem = null;
        session.PendingDefensiveItemHitPoints = 0;
    }

    private static void ClearPendingEncounterReaction(CombatSession session)
    {
        if (session == null)
        {
            return;
        }

        session.PendingEncounterReaction = EncounterReactionChoice.None;
    }

    private static EncounterReactionChoice ResolveEncounterReaction(CombatSession session)
    {
        if (session == null)
        {
            return EncounterReactionChoice.None;
        }

        if (session.PendingEncounterReaction != EncounterReactionChoice.None)
        {
            return session.PendingEncounterReaction;
        }

        return session.PendingDefensiveItem != null
            ? EncounterReactionChoice.Defend
            : EncounterReactionChoice.None;
    }

    private static bool IsDefensiveReactionActive(CombatSession session)
    {
        return session != null &&
               !session.State.Finished &&
               session.State.DecisionActive &&
               session.State.Turn == CombatTurn.Enemy;
    }

    private static bool ControllerHasItem(SquadCharacterController controller, Item item)
    {
        return TryFindControllerItem(controller, item, out _);
    }

    private static bool TryFindControllerItem(SquadCharacterController controller, Item item, out Item inventoryItem)
    {
        inventoryItem = null;
        if (controller == null || item == null)
        {
            return false;
        }

        string expectedItemId = ItemIdUtils.GetItemId(item);
        IReadOnlyList<Item> items = controller.Items;
        if (items == null)
        {
            return false;
        }

        for (int i = 0; i < items.Count; i++)
        {
            Item candidate = items[i];
            if (candidate == item)
            {
                inventoryItem = candidate;
                return true;
            }

            if (!string.IsNullOrWhiteSpace(expectedItemId) &&
                string.Equals(ItemIdUtils.GetItemId(candidate), expectedItemId, StringComparison.Ordinal))
            {
                inventoryItem = candidate;
                return true;
            }
        }

        return false;
    }

    private static string ResolveItemDisplayName(Item item)
    {
        if (item == null)
        {
            return "Item defensif";
        }

        if (!string.IsNullOrWhiteSpace(item.itemName))
        {
            return item.itemName;
        }

        return !string.IsNullOrWhiteSpace(item.name) ? item.name : "Item defensif";
    }

    private int ResolvePlayerAttackDamage(SquadCharacterController player)
    {
        int modifier = player != null ? global::CharacterData.GetStatModifier(player.GetStatValue(StatType.Strength)) : 0;
        return Mathf.Max(1, defaultPlayerAttackDamage + modifier);
    }

    private List<CombatRuntimeEnemy> BuildRuntimeEnemies(CombatAggroEnemy sourceEnemy)
    {
        List<CombatEnemyDefinition> definitions = sourceEnemy != null
            ? sourceEnemy.CreateEnemyDefinitions()
            : new List<CombatEnemyDefinition> { new CombatEnemyDefinition("Ennemi", 8, 8, 4) };

        List<CombatRuntimeEnemy> enemies = new List<CombatRuntimeEnemy>();
        for (int i = 0; i < definitions.Count; i++)
        {
            CombatEnemyDefinition definition = definitions[i];
            if (definition == null)
            {
                continue;
            }

            CombatEnemyDefinition runtime = definition.CreateRuntimeCopy(i, definitions.Count);
            if (runtime.currentHp <= 0)
            {
                continue;
            }

            enemies.Add(new CombatRuntimeEnemy(
                runtime.displayName,
                runtime.currentHp,
                runtime.maxHp,
                runtime.attackDamage,
                runtime.attacks));
        }

        return enemies;
    }

    private int GetActiveEnemyIndex(CombatSession session)
    {
        if (session?.Enemies == null)
        {
            return -1;
        }

        for (int i = 0; i < session.Enemies.Count; i++)
        {
            CombatRuntimeEnemy enemy = session.Enemies[i];
            if (enemy != null && enemy.IsAlive)
            {
                return i;
            }
        }

        return -1;
    }

    private CombatRuntimeEnemy GetActiveEnemy(CombatSession session)
    {
        if (session?.Enemies == null)
        {
            return null;
        }

        for (int i = 0; i < session.Enemies.Count; i++)
        {
            CombatRuntimeEnemy enemy = session.Enemies[i];
            if (enemy != null && enemy.IsAlive)
            {
                return enemy;
            }
        }

        return null;
    }

    private bool AreAllEnemiesDefeated(CombatSession session)
    {
        return CountAliveEnemies(session) == 0;
    }

    private int CountAliveEnemies(CombatSession session)
    {
        if (session?.Enemies == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < session.Enemies.Count; i++)
        {
            if (session.Enemies[i] != null && session.Enemies[i].IsAlive)
            {
                count++;
            }
        }

        return count;
    }

    private bool TryGetSession(SquadCharacterController controller, out CombatSession session)
    {
        session = null;
        if (controller == null)
        {
            return false;
        }

        string characterId = ResolveCharacterId(controller);
        return !string.IsNullOrWhiteSpace(characterId) && sessionsByCharacterId.TryGetValue(characterId, out session);
    }

    private bool TryResolveLocalCombatAnimationImpactSession(Transform actor, out CombatSession session)
    {
        session = null;
        if (sessionsByClientId.TryGetValue(ResolveLocalClientId(), out session) &&
            session != null &&
            !session.State.Finished)
        {
            return true;
        }

        if (actor == null)
        {
            return false;
        }

        SquadCharacterController controller = actor.GetComponentInParent<SquadCharacterController>();
        if (controller != null &&
            TryGetSession(controller, out session) &&
            session != null &&
            !session.State.Finished)
        {
            return true;
        }

        foreach (CombatSession candidate in sessionsByCharacterId.Values)
        {
            if (candidate == null ||
                candidate.State.Finished ||
                candidate.SourceEnemy == null ||
                !BelongsToTransform(actor, candidate.SourceEnemy.transform))
            {
                continue;
            }

            session = candidate;
            return true;
        }

        session = null;
        return false;
    }

    private static bool BelongsToTransform(Transform actor, Transform candidate)
    {
        return actor != null &&
               candidate != null &&
               (actor == candidate || actor.IsChildOf(candidate) || candidate.IsChildOf(actor));
    }

    private void ResolveCombatPositions(
        SquadCharacterController player,
        out Vector3 playerPosition,
        out Quaternion playerRotation,
        out Vector3 enemyPosition,
        out Quaternion enemyRotation)
    {
        Transform playerSpawnPoint = ResolvePlayerCombatSpawnPoint();
        Transform enemySpawnPoint = ResolveEnemyCombatSpawnPoint();
        if (playerSpawnPoint != null)
        {
            playerPosition = playerSpawnPoint.position;
            playerRotation = playerSpawnPoint.rotation;
        }
        else if (player != null)
        {
            playerPosition = player.transform.position;
            playerRotation = player.transform.rotation;
        }
        else
        {
            playerPosition = Vector3.zero;
            playerRotation = Quaternion.identity;
        }

        if (enemySpawnPoint != null)
        {
            enemyPosition = enemySpawnPoint.position;
            enemyRotation = enemySpawnPoint.rotation;
            return;
        }

        Vector3 forward = Vector3.ProjectOnPlane(playerRotation * Vector3.forward, Vector3.up);
        if (forward.sqrMagnitude <= 0.0001f && player != null)
        {
            forward = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up);
        }

        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = Vector3.forward;
        }

        enemyPosition = playerPosition + forward.normalized * 2f;
        playerRotation = ResolveFacingRotation(playerPosition, enemyPosition);
        enemyRotation = ResolveFacingRotation(enemyPosition, playerPosition);
    }

    private Transform ResolvePlayerCombatSpawnPoint()
    {
        if (spawnPointPlayer != null)
        {
            return spawnPointPlayer;
        }

        Transform arena = ResolveArenaRoot();
        spawnPointPlayer = FindNamedTransform(arena, DefaultPlayerSpawnPointName);
        if (spawnPointPlayer == null)
        {
            GameObject namedSpawnPoint = GameObject.Find(DefaultPlayerSpawnPointName);
            if (namedSpawnPoint != null)
            {
                spawnPointPlayer = namedSpawnPoint.transform;
            }
        }

        return spawnPointPlayer;
    }

    private Transform ResolveEnemyCombatSpawnPoint()
    {
        if (spawnPointEnemy != null)
        {
            return spawnPointEnemy;
        }

        Transform arena = ResolveArenaRoot();
        spawnPointEnemy = FindNamedTransform(arena, DefaultEnemySpawnPointName);
        if (spawnPointEnemy == null)
        {
            GameObject namedSpawnPoint = GameObject.Find(DefaultEnemySpawnPointName);
            if (namedSpawnPoint != null)
            {
                spawnPointEnemy = namedSpawnPoint.transform;
            }
        }

        return spawnPointEnemy;
    }

    private Transform ResolveArenaRoot()
    {
        if (arenaRoot != null)
        {
            return arenaRoot;
        }

        GameObject namedArena = GameObject.Find(DefaultArenaRootName);
        if (namedArena != null)
        {
            arenaRoot = namedArena.transform;
        }

        return arenaRoot;
    }

    private static Transform FindNamedTransform(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrWhiteSpace(targetName))
        {
            return null;
        }

        if (string.Equals(root.name, targetName, StringComparison.Ordinal))
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform match = FindNamedTransform(root.GetChild(i), targetName);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private static Quaternion ResolveFacingRotation(Vector3 origin, Vector3 target)
    {
        Vector3 direction = target - origin;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = Vector3.forward;
        }

        return Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private static void MoveCharacterTo(SquadCharacterController controller, Vector3 position, Quaternion rotation)
    {
        if (controller == null)
        {
            return;
        }

        if (!controller.TrySetUccExternalPositionAndRotation(position, rotation, stopActiveAbilities: true))
        {
            return;
        }
    }

    private static void MoveCombatAggroEnemyTo(CombatAggroEnemy enemy, Vector3 position, Quaternion rotation)
    {
        if (enemy == null)
        {
            return;
        }

        MoveTransformTo(enemy.transform, position, rotation);
    }

    private static void MoveTransformTo(Transform target, Vector3 position, Quaternion rotation)
    {
        if (target == null)
        {
            return;
        }

        Rigidbody body = target.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = position;
            body.rotation = rotation;
        }

        target.SetPositionAndRotation(position, rotation);
    }

    private static void SyncNetworkInventory(SquadCharacterController controller)
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (controller == null || manager == null || !manager.IsListening || !manager.IsServer)
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

    private void SuppressLocalClientMovement(string sessionId, Vector3 combatPosition, Quaternion combatRotation)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return;
        }

        SquadCharacterController controller = ResolveControllerForClient(ResolveLocalClientId());
        if (controller == null)
        {
            return;
        }

        if (!locallySuppressedSessions.Add(sessionId))
        {
            return;
        }

        controller.PushScriptedMovementSuppression();
        controller.Stop();
        MoveCharacterTo(controller, combatPosition, combatRotation);
    }

    private void RestoreLocalClientMovement(
        string sessionId,
        Vector3 returnPosition,
        Quaternion returnRotation,
        int playerHp,
        int playerMaxHp)
    {
        bool hadSuppression = !string.IsNullOrWhiteSpace(sessionId) && locallySuppressedSessions.Remove(sessionId);
        SquadCharacterController controller = ResolveControllerForClient(ResolveLocalClientId());
        if (controller == null)
        {
            return;
        }

        controller.SetHealth(playerHp, Mathf.Max(1, playerMaxHp));
        MoveCharacterTo(controller, returnPosition, returnRotation);
        controller.Stop();

        if (hadSuppression)
        {
            controller.PopScriptedMovementSuppression();
        }
    }

    private void ReleaseAllLocalClientMovement()
    {
        if (locallySuppressedSessions.Count == 0)
        {
            return;
        }

        int count = locallySuppressedSessions.Count;
        locallySuppressedSessions.Clear();
        SquadCharacterController controller = ResolveControllerForClient(ResolveLocalClientId());
        if (controller == null)
        {
            return;
        }

        for (int i = 0; i < count; i++)
        {
            controller.PopScriptedMovementSuppression();
        }

        controller.Stop();
    }

    private void MoveLocalEnemyIntoCombat(
        string sessionId,
        Vector3 enemyReturnPosition,
        Quaternion enemyReturnRotation,
        Vector3 combatPosition,
        Quaternion combatRotation)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return;
        }

        if (localEnemyPresentationsBySessionId.TryGetValue(sessionId, out LocalEnemyPresentation existing) && existing.Enemy != null)
        {
            MoveCombatAggroEnemyTo(existing.Enemy, combatPosition, combatRotation);
            return;
        }

        CombatAggroEnemy enemy = FindLocalEnemyPresentation(enemyReturnPosition);
        if (enemy == null)
        {
            return;
        }

        localEnemyPresentationsBySessionId[sessionId] = new LocalEnemyPresentation
        {
            Enemy = enemy,
            ReturnPosition = enemyReturnPosition,
            ReturnRotation = enemyReturnRotation
        };

        MoveCombatAggroEnemyTo(enemy, combatPosition, combatRotation);
    }

    private void RestoreLocalEnemyPresentation(
        string sessionId,
        bool hasEnemyPresentation,
        Vector3 enemyReturnPosition,
        Quaternion enemyReturnRotation,
        bool playerVictory,
        int enemyRemainingHp)
    {
        CombatAggroEnemy enemy = null;
        if (localEnemyPresentationsBySessionId.TryGetValue(sessionId, out LocalEnemyPresentation presentation))
        {
            enemy = presentation.Enemy;
            enemyReturnPosition = presentation.ReturnPosition;
            enemyReturnRotation = presentation.ReturnRotation;
            localEnemyPresentationsBySessionId.Remove(sessionId);
        }
        else if (hasEnemyPresentation)
        {
            enemy = FindLocalEnemyPresentation(enemyReturnPosition);
        }

        if (enemy == null)
        {
            return;
        }

        MoveCombatAggroEnemyTo(enemy, enemyReturnPosition, enemyReturnRotation);
        enemy.FinalizeCombatResult(playerVictory, enemyRemainingHp);
    }

    private void RestoreAllLocalEnemyPresentations()
    {
        if (localEnemyPresentationsBySessionId.Count == 0)
        {
            return;
        }

        foreach (LocalEnemyPresentation presentation in localEnemyPresentationsBySessionId.Values)
        {
            if (presentation?.Enemy == null)
            {
                continue;
            }

            MoveCombatAggroEnemyTo(presentation.Enemy, presentation.ReturnPosition, presentation.ReturnRotation);
        }

        localEnemyPresentationsBySessionId.Clear();
    }

    private Transform ResolveLocalCombatEnemyTransform(LocalCombatPresentationState presentation)
    {
        if (presentation == null || !presentation.Active)
        {
            return null;
        }

        if (localEnemyPresentationsBySessionId.TryGetValue(presentation.SessionId, out LocalEnemyPresentation tracked) &&
            tracked != null &&
            tracked.Enemy != null)
        {
            return tracked.Enemy.transform;
        }

        if (!presentation.HasEnemyPresentation)
        {
            return null;
        }

        CombatAggroEnemy enemy = FindLocalEnemyPresentation(presentation.EnemyReturnPosition);
        return enemy != null ? enemy.transform : null;
    }

    private CombatAggroEnemy FindLocalEnemyPresentation(Vector3 expectedPosition)
    {
        CombatAggroEnemy[] candidates = Resources.FindObjectsOfTypeAll<CombatAggroEnemy>();
        CombatAggroEnemy best = null;
        float bestSqrDistance = float.PositiveInfinity;

        for (int i = 0; i < candidates.Length; i++)
        {
            CombatAggroEnemy candidate = candidates[i];
            if (candidate == null || !candidate.gameObject.scene.IsValid())
            {
                continue;
            }

            if (IsEnemyAlreadyTrackedLocally(candidate))
            {
                continue;
            }

            float sqrDistance = (candidate.transform.position - expectedPosition).sqrMagnitude;
            if (sqrDistance >= bestSqrDistance)
            {
                continue;
            }

            best = candidate;
            bestSqrDistance = sqrDistance;
        }

        return bestSqrDistance <= LocalEnemyLookupMaxDistance * LocalEnemyLookupMaxDistance ? best : null;
    }

    private bool IsEnemyAlreadyTrackedLocally(CombatAggroEnemy enemy)
    {
        foreach (LocalEnemyPresentation presentation in localEnemyPresentationsBySessionId.Values)
        {
            if (presentation != null && presentation.Enemy == enemy)
            {
                return true;
            }
        }

        return false;
    }

    private SquadCharacterController ResolveControllerForClient(ulong clientId)
    {
        if (clientId == ResolveLocalClientId())
        {
            return ResolveLocalControllerFromContext();
        }

        if (IsNetworkSessionActive())
        {
            Transform root = NetcodePlayerUtils.GetPlayerTransform(clientId);
            return root != null ? root.GetComponentInChildren<SquadCharacterController>(true) : null;
        }

        return ResolveLocalControllerFromContext();
    }

    private static SquadCharacterController ResolveLocalControllerFromContext()
    {
        Transform localRoot = LocalPlayerContext.LocalCharacterRoot;
        if (localRoot != null)
        {
            return localRoot.GetComponentInChildren<SquadCharacterController>(true);
        }

        return null;
    }

    private ulong ResolveOwnerClientId(SquadCharacterController controller)
    {
        NetworkObject networkObject = controller != null ? controller.GetComponent<NetworkObject>() : null;
        if (networkObject != null && networkObject.IsSpawned)
        {
            return networkObject.OwnerClientId;
        }

        return ResolveLocalClientId();
    }

    private ulong ResolveLocalClientId()
    {
        NetworkManager manager = NetworkManager.Singleton;
        return manager != null && manager.IsListening ? manager.LocalClientId : 0UL;
    }

    private string ResolveCharacterId(SquadCharacterController controller)
    {
        if (controller == null)
        {
            return string.Empty;
        }

        NetcodeCharacterIdentity identity = controller.GetComponent<NetcodeCharacterIdentity>();
        if (identity != null && !string.IsNullOrWhiteSpace(identity.CharacterId))
        {
            return identity.CharacterId;
        }

        string id = NetcodeCharacterIdentity.GetCharacterId(controller.CharacterData);
        return !string.IsNullOrWhiteSpace(id) ? id : controller.GetInstanceID().ToString();
    }

    private bool CanRunAuthority()
    {
        return !IsNetworkSessionActive() || IsServer;
    }

    private bool IsNetworkSessionActive()
    {
        NetworkManager manager = NetworkManager.Singleton;
        return manager != null && manager.IsListening;
    }

    private static bool ShouldReplaceExistingInstance(CombatSessionManager current, CombatSessionManager candidate)
    {
        if (current == null || candidate == null)
        {
            return false;
        }

        bool currentNetworked = current.GetComponent<NetworkObject>() != null;
        bool candidateNetworked = candidate.GetComponent<NetworkObject>() != null;
        return candidateNetworked && !currentNetworked;
    }

    private static CombatTurn ResolvePresentationTurn(CombatSession session)
    {
        if (session == null)
        {
            return CombatTurn.None;
        }

        if (session.State.Resolving)
        {
            return session.State.ResolutionPlayerVictory ? CombatTurn.Player : CombatTurn.Enemy;
        }

        return session.State.Turn;
    }

    private static CombatTurn ResolvePresentationTurn(LocalCombatPresentationState presentation)
    {
        if (presentation == null || !presentation.Active)
        {
            return CombatTurn.None;
        }

        if (presentation.Resolving)
        {
            return presentation.ResolutionPlayerVictory ? CombatTurn.Player : CombatTurn.Enemy;
        }

        return presentation.Turn;
    }

    private Vector3 ResolveCombatAudioPosition(CombatSession session, bool preferEnemy)
    {
        if (session == null)
        {
            return transform.position;
        }

        if (preferEnemy && session.SourceEnemy != null)
        {
            return session.SourceEnemy.transform.position;
        }

        if (session.Player != null)
        {
            return session.Player.transform.position;
        }

        if (session.SourceEnemy != null)
        {
            return session.SourceEnemy.transform.position;
        }

        return transform.position;
    }

    private Vector3 ResolveLocalCombatAudioPosition(bool preferEnemy)
    {
        if (preferEnemy)
        {
            Transform enemy = ResolveLocalCombatEnemyTransform(localCombatPresentation);
            if (enemy != null)
            {
                return enemy.position;
            }
        }

        SquadCharacterController controller = ResolveControllerForClient(ResolveLocalClientId());
        if (controller != null)
        {
            return controller.transform.position;
        }

        return transform.position;
    }

    private void PlayActionAudio(ActionAudioCue cue, Vector3 position)
    {
        if (cue == ActionAudioCue.None)
        {
            return;
        }

        AudioManager manager = AudioManager.EnsureInstance();
        if (manager != null)
        {
            manager.PlayActionCue(cue, position);
        }
    }

    private CombatActionTiming CreateActionTiming(float animationDuration)
    {
        float duration = Mathf.Max(0.05f, animationDuration);
        float impactDelay = duration * Mathf.Clamp01(actionImpactNormalizedTime);
        return new CombatActionTiming(impactDelay, duration);
    }

    private CombatActionTiming CreateActionTiming(CombatActionMotionPlan motion)
    {
        return new CombatActionTiming(
            motion.ImpactDelay(actionImpactNormalizedTime),
            motion.TotalDuration());
    }

    private static CombatActionTiming ApplyCombatTimeProfile(
        CombatActionTiming timing,
        TimeManager.CombatPresentationTimeProfile profile)
    {
        if (profile == TimeManager.CombatPresentationTimeProfile.None)
        {
            return timing;
        }

        return new CombatActionTiming(
            TimeManager.EstimateCombatPresentationDuration(timing.ImpactDelay, profile),
            TimeManager.EstimateCombatPresentationDuration(timing.TotalDuration, profile));
    }

    private CombatActionMotionPlan BuildCombatActionMotionPlan(
        Transform actor,
        Transform target,
        Animator animator,
        string animationName,
        CombatAttackRangeType rangeType,
        CombatAttackMovementMode movementMode,
        float desiredDistance,
        float animationDuration)
    {
        CombatActionMotionPlan plan = new CombatActionMotionPlan
        {
            StartPosition = actor != null ? actor.position : Vector3.zero,
            StartRotation = actor != null ? actor.rotation : Quaternion.identity,
            ApproachPosition = actor != null ? actor.position : Vector3.zero,
            ApproachRotation = actor != null ? actor.rotation : Quaternion.identity,
            AttackRotation = actor != null ? actor.rotation : Quaternion.identity,
            AttackDuration = Mathf.Max(0.05f, animationDuration)
        };

        if (actor == null || target == null)
        {
            return plan;
        }

        plan.AttackRotation = ResolveFacingRotation(actor.position, target.position);
        bool isMelee = IsMeleeAttack(rangeType, animationName);
        bool animationIncludesMovement = DoesAttackAnimationIncludeMovement(animator, animationName, movementMode);

        if (!isMelee || movementMode == CombatAttackMovementMode.None)
        {
            return plan;
        }

        if (animationIncludesMovement)
        {
            plan.ReturnToStart = true;
            plan.ReturnDuration = EstimateActionMoveDuration(Mathf.Max(0.1f, desiredDistance > 0f ? desiredDistance : defaultMeleeApproachDistance));
            return plan;
        }

        if (!TryResolveApproachDestination(actor.position, target.position, desiredDistance, out Vector3 approachPosition))
        {
            return plan;
        }

        float sqrDistanceToApproach = (approachPosition - actor.position).sqrMagnitude;
        if (sqrDistanceToApproach <= ActionAlreadyInRangePadding * ActionAlreadyInRangePadding)
        {
            return plan;
        }

        plan.UseScriptedApproach = true;
        plan.ReturnToStart = true;
        plan.ApproachPosition = approachPosition;
        plan.ApproachRotation = ResolveFacingRotation(approachPosition, target.position);
        plan.ApproachDuration = EstimateActionMoveDuration(actor.position, approachPosition);
        plan.ReturnDuration = EstimateActionMoveDuration(approachPosition, actor.position);
        return plan;
    }

    private bool TryResolveApproachDestination(Vector3 actorPosition, Vector3 targetPosition, float desiredDistance, out Vector3 destination)
    {
        destination = actorPosition;
        float resolvedDistance = Mathf.Max(0.1f, desiredDistance > 0f ? desiredDistance : defaultMeleeApproachDistance);
        Vector3 directionFromTarget = Vector3.ProjectOnPlane(actorPosition - targetPosition, Vector3.up);
        if (directionFromTarget.sqrMagnitude <= 0.0001f)
        {
            directionFromTarget = Vector3.ProjectOnPlane(-transform.forward, Vector3.up);
        }

        if (directionFromTarget.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        destination = targetPosition + directionFromTarget.normalized * resolvedDistance;
        destination.y = actorPosition.y;
        return true;
    }

    private float EstimateActionMoveDuration(Vector3 from, Vector3 to)
    {
        return EstimateActionMoveDuration(Vector3.Distance(from, to));
    }

    private float EstimateActionMoveDuration(float distance)
    {
        if (distance <= ActionMoveCompleteThreshold)
        {
            return 0f;
        }

        float speed = Mathf.Max(0.1f, actionPresentationMoveSpeed);
        return Mathf.Clamp(distance / speed, 0.05f, Mathf.Max(0.05f, maxActionPresentationMoveSeconds));
    }

    private static bool IsMeleeAttack(CombatAttackRangeType rangeType, string animationName)
    {
        if (rangeType == CombatAttackRangeType.Melee)
        {
            return true;
        }

        if (rangeType == CombatAttackRangeType.Ranged || rangeType == CombatAttackRangeType.Support)
        {
            return false;
        }

        return !ContainsAnyNameHint(animationName, RangedAttackNameHints) &&
               !ContainsAnyNameHint(animationName, SupportAttackNameHints);
    }

    private static bool DoesAttackAnimationIncludeMovement(
        Animator animator,
        string animationName,
        CombatAttackMovementMode movementMode)
    {
        if (movementMode == CombatAttackMovementMode.ScriptedApproach)
        {
            return false;
        }

        if (movementMode == CombatAttackMovementMode.AnimationIncludesMovement)
        {
            return true;
        }

        return animator != null && animator.applyRootMotion ||
               ContainsAnyNameHint(animationName, MovementAnimationNameHints);
    }

    private static CombatAttackRangeType ResolveEnemyAttackRangeType(CombatEnemyAttackDefinition attack, string animationName)
    {
        if (attack == null)
        {
            return CombatAttackRangeType.Melee;
        }

        if (attack.rangeType != CombatAttackRangeType.Auto)
        {
            return attack.rangeType;
        }

        string combinedName = $"{attack.displayName} {animationName}";
        if (ContainsAnyNameHint(combinedName, SupportAttackNameHints))
        {
            return CombatAttackRangeType.Support;
        }

        if (ContainsAnyNameHint(combinedName, RangedAttackNameHints))
        {
            return CombatAttackRangeType.Ranged;
        }

        return CombatAttackRangeType.Melee;
    }

    private static CombatAttackMovementMode ResolveEnemyAttackMovementMode(CombatEnemyAttackDefinition attack)
    {
        return attack != null ? attack.movementMode : CombatAttackMovementMode.Auto;
    }

    private float ResolveEnemyAttackApproachDistance(CombatEnemyAttackDefinition attack)
    {
        return Mathf.Max(0.1f, attack != null && attack.approachDistance > 0f
            ? attack.approachDistance
            : defaultMeleeApproachDistance);
    }

    private static bool ContainsAnyNameHint(string value, string[] hints)
    {
        if (string.IsNullOrWhiteSpace(value) || hints == null)
        {
            return false;
        }

        for (int i = 0; i < hints.Length; i++)
        {
            string hint = hints[i];
            if (!string.IsNullOrWhiteSpace(hint) &&
                value.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private void StartCombatActionPresentation(
        Transform actor,
        SquadCharacterController playerController,
        CombatAggroEnemy enemyController,
        Transform target,
        CombatActionMotionPlan motion,
        Action playAttack)
    {
        if (actor == null)
        {
            playAttack?.Invoke();
            return;
        }

        StopCombatActionPresentation(actor);
        Coroutine routine = StartCoroutine(PlayCombatActionPresentationRoutine(
            actor,
            playerController,
            enemyController,
            target,
            motion,
            playAttack));
        actionPresentationCoroutinesByActor[actor] = routine;
    }

    private IEnumerator PlayCombatActionPresentationRoutine(
        Transform actor,
        SquadCharacterController playerController,
        CombatAggroEnemy enemyController,
        Transform target,
        CombatActionMotionPlan motion,
        Action playAttack)
    {
        if (actor == null)
        {
            yield break;
        }

        if (motion.UseScriptedApproach)
        {
            yield return MoveCombatActionActorRoutine(
                actor,
                playerController,
                enemyController,
                target,
                motion.StartPosition,
                motion.StartRotation,
                motion.ApproachPosition,
                motion.ApproachRotation,
                motion.ApproachDuration);
        }
        else
        {
            MoveCombatActionActorTo(actor, playerController, enemyController, actor.position, motion.AttackRotation);
        }

        playAttack?.Invoke();

        float elapsed = 0f;
        while (actor != null && elapsed < motion.AttackDuration)
        {
            elapsed += TimeManager.GetCombatPresentationDeltaTime();
            yield return null;
        }

        if (actor != null && motion.ReturnToStart)
        {
            float sqrDisplacement = (actor.position - motion.StartPosition).sqrMagnitude;
            if (motion.UseScriptedApproach ||
                sqrDisplacement > ActionReturnDisplacementThreshold * ActionReturnDisplacementThreshold)
            {
                yield return MoveCombatActionActorRoutine(
                    actor,
                    playerController,
                    enemyController,
                    target,
                    actor.position,
                    actor.rotation,
                    motion.StartPosition,
                    motion.StartRotation,
                    motion.ReturnDuration);
            }
        }

        if (actor != null)
        {
            actionPresentationCoroutinesByActor.Remove(actor);
        }
    }

    private IEnumerator MoveCombatActionActorRoutine(
        Transform actor,
        SquadCharacterController playerController,
        CombatAggroEnemy enemyController,
        Transform target,
        Vector3 start,
        Quaternion startRotation,
        Vector3 destination,
        Quaternion destinationRotation,
        float duration)
    {
        if (actor == null)
        {
            yield break;
        }

        if (duration <= 0f || (destination - start).sqrMagnitude <= ActionMoveCompleteThreshold * ActionMoveCompleteThreshold)
        {
            MoveCombatActionActorTo(actor, playerController, enemyController, destination, destinationRotation);
            yield break;
        }

        float elapsed = 0f;
        while (actor != null && elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            Vector3 position = Vector3.Lerp(start, destination, t);
            Quaternion rotation = ResolvePresentationFacingRotation(position, target, destinationRotation);
            MoveCombatActionActorTo(actor, playerController, enemyController, position, rotation);
            elapsed += TimeManager.GetCombatPresentationDeltaTime();
            yield return null;
        }

        if (actor != null)
        {
            MoveCombatActionActorTo(actor, playerController, enemyController, destination, destinationRotation);
        }
    }

    private static Quaternion ResolvePresentationFacingRotation(Vector3 actorPosition, Transform target, Quaternion fallbackRotation)
    {
        if (target == null)
        {
            return fallbackRotation;
        }

        return ResolveFacingRotation(actorPosition, target.position);
    }

    private static void MoveCombatActionActorTo(
        Transform actor,
        SquadCharacterController playerController,
        CombatAggroEnemy enemyController,
        Vector3 position,
        Quaternion rotation)
    {
        if (playerController != null)
        {
            MoveCharacterTo(playerController, position, rotation);
            return;
        }

        if (enemyController != null)
        {
            MoveCombatAggroEnemyTo(enemyController, position, rotation);
            return;
        }

        MoveTransformTo(actor, position, rotation);
    }

    private void StopCombatActionPresentation(Transform actor)
    {
        if (actor == null)
        {
            return;
        }

        if (!actionPresentationCoroutinesByActor.TryGetValue(actor, out Coroutine routine) || routine == null)
        {
            return;
        }

        StopCoroutine(routine);
        actionPresentationCoroutinesByActor.Remove(actor);
        TimeManager.Instance?.SetCombatPresentationTimeScale(null, 1f, active: false);
    }

    private void StopCombatActionPresentations(CombatSession session)
    {
        if (session == null)
        {
            return;
        }

        StopCombatActionPresentation(session.Player != null ? session.Player.transform : null);
        StopCombatActionPresentation(session.SourceEnemy != null ? session.SourceEnemy.transform : null);
    }

    private void StopAllCombatActionPresentations()
    {
        if (actionPresentationCoroutinesByActor.Count == 0)
        {
            return;
        }

        foreach (Coroutine routine in actionPresentationCoroutinesByActor.Values)
        {
            if (routine != null)
            {
                StopCoroutine(routine);
            }
        }

        actionPresentationCoroutinesByActor.Clear();
        TimeManager.Instance?.SetCombatPresentationTimeScale(null, 1f, active: false);
    }

    private float PlayBasicAttackAnimationLocally(SquadCharacterController controller)
    {
        return PlayPlayerActionAnimationLocally(controller, BasicAttackAnimationName, DefaultBasicAttackAnimationDuration);
    }

    private float PlayPlayerActionAnimationLocally(SquadCharacterController controller, string animationName, float fallbackDuration)
    {
        if (controller != null)
        {
            controller.Stop();
        }

        PlayActionAudio(
            ActionAudioCue.CombatAttack,
            controller != null ? controller.transform.position : transform.position);

        Animator animator = controller != null ? controller.GetComponent<Animator>() : null;
        return PlayNamedAnimation(animator, animationName, fallbackDuration);
    }

    private float PlayDefenseAnimationLocally(SquadCharacterController controller)
    {
        if (controller != null)
        {
            controller.Stop();
        }

        PlayActionAudio(
            ActionAudioCue.CombatTurn,
            controller != null ? controller.transform.position : transform.position);

        Animator animator = controller != null ? controller.GetComponent<Animator>() : null;
        string animationName = ResolveAvailableActionAnimationName(animator, DefenseAnimationName, BlockAnimationName);
        return PlayNamedAnimation(animator, animationName, DefaultDefenseAnimationDuration);
    }

    private float PlayEnemyBasicAttackAnimationLocally(
        CombatSession session,
        CombatEnemyAttackDefinition attack,
        string animationName = null)
    {
        if (session?.SourceEnemy == null)
        {
            return DefaultEnemyAttackAnimationDuration;
        }

        PlayActionAudio(ActionAudioCue.CombatAttack, session.SourceEnemy.transform.position);
        PlayEnemyAttackVfx(session.SourceEnemy.transform, attack);
        return PlayNamedAnimation(
            session.SourceEnemy.ResolveAnimator(),
            string.IsNullOrWhiteSpace(animationName) ? ResolveEnemyAttackAnimationName(attack) : animationName,
            DefaultEnemyAttackAnimationDuration);
    }

    private void PlayEnemyBasicAttackPresentationLocally(
        string sessionId,
        int enemyIndex,
        int attackIndex)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return;
        }

        if (!localEnemyPresentationsBySessionId.TryGetValue(sessionId, out LocalEnemyPresentation presentation) ||
            presentation?.Enemy == null)
        {
            return;
        }

        CombatEnemyAttackDefinition attack = ResolvePresentationEnemyAttack(presentation.Enemy, enemyIndex, attackIndex);
        Animator animator = presentation.Enemy.ResolveAnimator();
        string animationName = ResolveEnemyPresentationAnimationName(attack, animator);
        float animationDuration = ResolveAnimationDuration(animator, animationName, DefaultEnemyAttackAnimationDuration);
        Transform target = ResolveControllerForClient(ResolveLocalClientId())?.transform;
        bool useAnimationEventPresentation = IsJuggernautGriffeAttack(attack, animator);

        Action playAttack = useAnimationEventPresentation
            ? () => PlayNamedAnimation(animator, animationName, DefaultEnemyAttackAnimationDuration)
            : () =>
            {
                PlayActionAudio(ActionAudioCue.CombatAttack, presentation.Enemy.transform.position);
                PlayEnemyAttackVfx(presentation.Enemy.transform, attack);
                PlayNamedAnimation(animator, animationName, DefaultEnemyAttackAnimationDuration);
            };

        if (useAnimationEventPresentation)
        {
            playAttack();
            return;
        }

        CombatActionMotionPlan motion = BuildCombatActionMotionPlan(
            presentation.Enemy.transform,
            target,
            animator,
            animationName,
            ResolveEnemyAttackRangeType(attack, animationName),
            ResolveEnemyAttackMovementMode(attack),
            ResolveEnemyAttackApproachDistance(attack),
            animationDuration);
        StartCombatActionPresentation(
            presentation.Enemy.transform,
            null,
            presentation.Enemy,
            target,
            motion,
            playAttack);
    }

    private CombatEnemyAttackDefinition ResolvePresentationEnemyAttack(
        CombatAggroEnemy enemy,
        int enemyIndex,
        int attackIndex)
    {
        if (enemy == null || enemyIndex < 0 || attackIndex < 0)
        {
            return null;
        }

        List<CombatEnemyDefinition> definitions = enemy.CreateEnemyDefinitions();
        if (definitions == null || enemyIndex >= definitions.Count)
        {
            return null;
        }

        CombatEnemyDefinition runtime = definitions[enemyIndex]?.CreateRuntimeCopy(enemyIndex, definitions.Count);
        if (runtime?.attacks == null || attackIndex >= runtime.attacks.Count)
        {
            return null;
        }

        return runtime.attacks[attackIndex];
    }

    private void PlayEnemyAttackVfx(Transform origin, CombatEnemyAttackDefinition attack)
    {
        if (origin == null || attack?.vfxPrefab == null)
        {
            return;
        }

        Vector3 position = origin.TransformPoint(attack.vfxLocalOffset);
        Quaternion rotation = origin.rotation * Quaternion.Euler(attack.vfxLocalEulerAngles);
        GameObject instance = Instantiate(attack.vfxPrefab, position, rotation);
        float lifetime = Mathf.Max(0f, attack.vfxLifetime);
        if (lifetime > 0f)
        {
            Destroy(instance, lifetime);
        }
    }

    private float ResolveCombatResolutionDuration(CombatSession session, bool playerVictory)
    {
        Animator loserAnimator = null;
        if (playerVictory)
        {
            loserAnimator = session?.SourceEnemy != null ? session.SourceEnemy.ResolveAnimator() : null;
        }
        else if (session?.Player != null)
        {
            loserAnimator = session.Player.GetComponent<Animator>();
            session.Player.Stop();
        }

        return Mathf.Max(0f, PlayDeathAnimation(loserAnimator)) + PostDeathReturnDelaySeconds;
    }

    private float PlayDeathAnimation(Animator animator)
    {
        if (animator == null || !animator.isActiveAndEnabled)
        {
            return 0f;
        }

        for (int layerIndex = 0; layerIndex < animator.layerCount; layerIndex++)
        {
            for (int candidateIndex = 0; candidateIndex < DeathAnimationCandidates.Length; candidateIndex++)
            {
                string stateName = DeathAnimationCandidates[candidateIndex];
                if (!TryCrossFadeAnimatorState(animator, layerIndex, stateName))
                {
                    continue;
                }

                return ResolveAnimationDuration(animator, stateName, DefaultDeathAnimationDuration);
            }
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.type != AnimatorControllerParameterType.Trigger ||
                !string.Equals(parameter.name, "Death", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            animator.ResetTrigger(parameter.name);
            animator.SetTrigger(parameter.name);
            return ResolveAnimationDuration(animator, parameter.name, DefaultDeathAnimationDuration);
        }

        return ResolveAnimationDuration(animator, "Death", DefaultDeathAnimationDuration);
    }

    private float PlayNamedAnimation(Animator animator, string animationName, float fallbackDuration)
    {
        if (animator == null || !animator.isActiveAndEnabled || string.IsNullOrWhiteSpace(animationName))
        {
            return fallbackDuration;
        }

        for (int layerIndex = 0; layerIndex < animator.layerCount; layerIndex++)
        {
            if (!TryCrossFadeAnimatorState(animator, layerIndex, animationName))
            {
                continue;
            }

            return ResolveAnimationDuration(animator, animationName, fallbackDuration);
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.type != AnimatorControllerParameterType.Trigger ||
                !string.Equals(parameter.name, animationName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            animator.ResetTrigger(parameter.name);
            animator.SetTrigger(parameter.name);
            return ResolveAnimationDuration(animator, parameter.name, fallbackDuration);
        }

        return ResolveAnimationDuration(animator, animationName, fallbackDuration);
    }

    private static string ResolveAvailableActionAnimationName(Animator animator, string preferredName, string fallbackName)
    {
        if (HasAnimatorStateOrTrigger(animator, preferredName))
        {
            return preferredName;
        }

        if (HasAnimatorStateOrTrigger(animator, fallbackName))
        {
            return fallbackName;
        }

        return string.IsNullOrWhiteSpace(preferredName) ? fallbackName : preferredName;
    }

    private static bool HasAnimatorStateOrTrigger(Animator animator, string animationName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(animationName))
        {
            return false;
        }

        for (int layerIndex = 0; layerIndex < animator.layerCount; layerIndex++)
        {
            string layerPath = animator.GetLayerName(layerIndex) + "." + animationName;
            if (animator.HasState(layerIndex, Animator.StringToHash(layerPath)) ||
                animator.HasState(layerIndex, Animator.StringToHash(animationName)))
            {
                return true;
            }
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.type == AnimatorControllerParameterType.Trigger &&
                string.Equals(parameter.name, animationName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryCrossFadeAnimatorState(Animator animator, int layerIndex, string stateName)
    {
        if (animator == null ||
            string.IsNullOrWhiteSpace(stateName) ||
            layerIndex < 0 ||
            layerIndex >= animator.layerCount)
        {
            return false;
        }

        string layerPath = animator.GetLayerName(layerIndex) + "." + stateName;
        int fullPathHash = Animator.StringToHash(layerPath);
        if (animator.HasState(layerIndex, fullPathHash))
        {
            animator.CrossFadeInFixedTime(fullPathHash, DeathAnimationTransitionDuration, layerIndex, 0f);
            return true;
        }

        int shortNameHash = Animator.StringToHash(stateName);
        if (!animator.HasState(layerIndex, shortNameHash))
        {
            return false;
        }

        animator.CrossFadeInFixedTime(shortNameHash, DeathAnimationTransitionDuration, layerIndex, 0f);
        return true;
    }

    private static float ResolveAnimationDuration(Animator animator, string preferredName, float fallbackDuration)
    {
        RuntimeAnimatorController controller = animator != null ? animator.runtimeAnimatorController : null;
        if (controller == null)
        {
            return fallbackDuration;
        }

        AnimationClip[] clips = controller.animationClips;
        if (clips == null || clips.Length == 0)
        {
            return fallbackDuration;
        }

        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip clip = clips[i];
            if (clip == null || string.IsNullOrWhiteSpace(clip.name))
            {
                continue;
            }

            if (!string.Equals(clip.name, preferredName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return Mathf.Max(0.05f, clip.length);
        }

        if (preferredName.IndexOf("Death", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            for (int i = 0; i < clips.Length; i++)
            {
                AnimationClip clip = clips[i];
                if (clip == null || string.IsNullOrWhiteSpace(clip.name))
                {
                    continue;
                }

                if (clip.name.IndexOf("Death", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                return Mathf.Max(0.05f, clip.length);
            }
        }

        return fallbackDuration;
    }

    private int ResolveDisplayedEnemyRemainingHp(CombatSession session)
    {
        CombatRuntimeEnemy activeEnemy = GetActiveEnemy(session);
        if (activeEnemy != null)
        {
            return Mathf.Max(0, activeEnemy.CurrentHp);
        }

        if (session?.Enemies == null || session.Enemies.Count == 0 || session.Enemies[0] == null)
        {
            return 0;
        }

        return Mathf.Max(0, session.Enemies[0].CurrentHp);
    }

    private ClientRpcParams BuildClientRpcParams(ulong clientId)
    {
        return new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new[] { clientId }
            }
        };
    }

    private void OnClientDisconnected(ulong clientId)
    {
        StopPrayer(clientId, sendFeedback: false);
        if (!sessionsByClientId.TryGetValue(clientId, out CombatSession session))
        {
            return;
        }

        EndCombat(session, false, "Combat interrompu.", notifyClient: false);
    }
}
