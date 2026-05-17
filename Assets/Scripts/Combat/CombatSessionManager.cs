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
    private const string DefaultArenaRootName = "Arena";
    private const string DefaultPlayerSpawnPointName = "SpawnPoint_Player";
    private const string DefaultEnemySpawnPointName = "SpawnPoint_Enemy";
    private const float DeathAnimationTransitionDuration = 0.05f;
    private const float DefaultBasicAttackAnimationDuration = 0.75f;
    private const float DefaultDeathAnimationDuration = 1f;
    private const float PostDeathReturnDelaySeconds = 3f;
    private const float LocalEnemyLookupMaxDistance = 6f;
    private static readonly string[] DeathAnimationCandidates = { "Death", "Death_v1", "Death_v2" };

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
            Resolving = false;
            ResolutionPlayerVictory = false;
            HasEnemyPresentation = false;
            EnemyReturnPosition = Vector3.zero;
            PlayerActionLocked = false;
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
    /// Degats de l'attaque de base du joueur.
    /// </summary>
    [SerializeField, Min(1), Tooltip("Degats de base de l'action Attaquer du joueur.")]
    private int defaultPlayerAttackDamage = 3;
    /// <summary>
    /// Intervalle entre deux snapshots HUD/reseau.
    /// </summary>
    [SerializeField, Min(0.05f), Tooltip("Intervalle de rafraichissement du HUD pendant un combat.")]
    private float snapshotInterval = 0.2f;

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
    private readonly LocalCombatPresentationState localCombatPresentation = new LocalCombatPresentationState();
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
        Instance = FindFirstObjectByType<CombatSessionManager>();
#else
        Instance = FindObjectOfType<CombatSessionManager>();
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
        player = null;
        enemy = null;
        playerTurn = false;

        if (CanRunAuthority())
        {
            if (!sessionsByClientId.TryGetValue(ResolveLocalClientId(), out CombatSession session) || session == null || session.State.Finished)
            {
                return false;
            }

            player = session.Player != null ? session.Player.transform : ResolveControllerForClient(ResolveLocalClientId())?.transform;
            enemy = session.SourceEnemy != null ? session.SourceEnemy.transform : null;
            playerTurn = ResolvePresentationTurn(session) == CombatTurn.Player;
            return player != null && enemy != null;
        }

        if (!localCombatPresentation.Active)
        {
            return false;
        }

        player = ResolveControllerForClient(ResolveLocalClientId())?.transform;
        enemy = ResolveLocalCombatEnemyTransform(localCombatPresentation);
        playerTurn = ResolvePresentationTurn(localCombatPresentation) == CombatTurn.Player;
        return player != null && enemy != null;
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
        BeginTurn(session, CombatTurn.Enemy, "L'ennemi ouvre le combat.");
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

        TryPlayerPassForClient(ResolveLocalClientId(), "Tour passe.");
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
            session.State.Turn != CombatTurn.Player ||
            session.State.PlayerActionLocked)
        {
            return;
        }

        BeginTurn(session, CombatTurn.Enemy, "Item utilise. Fin du tour.");
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

        if (!session.State.Finished && session.State.Turn == CombatTurn.Player && !session.State.PlayerActionLocked)
        {
            return true;
        }

        reason = session.State.Finished
            ? "Combat termine."
            : session.State.PlayerActionLocked
                ? "Action de combat deja en cours."
                : "Impossible d'utiliser un item hors du tour joueur.";
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
        TryPlayerPassForClient(rpcParams.Receive.SenderClientId, "Tour passe.");
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

    [ClientRpc]
    private void EnterCombatClientRpc(CombatEnterData data, ClientRpcParams rpcParams = default)
    {
        if (!IsServer)
        {
            // Les clients non serveurs conservent un etat minimal pour HUD/camera.
            localCombatPresentation.SessionId = data.SessionId.ToString();
            localCombatPresentation.Turn = CombatTurn.None;
            localCombatPresentation.Resolving = false;
            localCombatPresentation.ResolutionPlayerVictory = false;
            localCombatPresentation.HasEnemyPresentation = data.HasEnemyPresentation;
            localCombatPresentation.EnemyReturnPosition = data.EnemyReturnPosition;
            localCombatPresentation.PlayerActionLocked = false;
        }

        CombatHudController.EnsureInstance();
        CombatTransitionController.EnsureInstance().PlayEnterTransition(() =>
        {
            if (!IsServer)
            {
                // Le placement local est execute quand la transition couvre l'ecran.
                ApplyLocalEnterCombatPresentation(data);
            }
        });
    }

    [ClientRpc]
    private void ExitCombatClientRpc(CombatExitData data, ClientRpcParams rpcParams = default)
    {
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

            localCombatPresentation.PlayerActionLocked = snapshot.PlayerActionLocked;
        }

        CombatHudController.EnsureInstance().ShowSnapshot(
            sessionId,
            (CombatHudController.TurnState)snapshot.TurnState,
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

        if (session.State.PlayerActionLocked)
        {
            if (Time.time >= session.State.PlayerActionEndsAt)
            {
                CompleteLockedPlayerAttack(session);
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
            TryPlayerPass(session, "Temps ecoule. Tour passe.");
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

        int pendingDamage = ResolvePlayerAttackDamage(session.Player);
        float actionDuration = PlayPlayerBasicAttackPresentation(session);
        session.State.BeginPlayerAction(pendingDamage, Time.time, actionDuration, $"Attaque de base sur {enemy.DisplayName}.");
        SendSnapshot(session, session.State.LastMessage);
        return true;
    }

    private bool TryPlayerPass(CombatSession session, string message)
    {
        if (session == null || !session.State.CanUsePlayerAction())
        {
            return false;
        }

        BeginTurn(session, CombatTurn.Enemy, message);
        return true;
    }

    private void CompleteLockedPlayerAttack(CombatSession session)
    {
        if (session == null || !session.State.PlayerActionLocked || session.State.Finished)
        {
            return;
        }

        CombatRuntimeEnemy enemy = GetActiveEnemy(session);
        if (enemy == null)
        {
            BeginCombatResolution(session, true, "Victoire.");
            return;
        }

        int damage = session.State.ConsumePendingPlayerAttackDamage();
        int applied = enemy.ApplyDamage(damage);
        PlayActionAudio(ActionAudioCue.CombatHit, ResolveCombatAudioPosition(session, preferEnemy: true));

        if (AreAllEnemiesDefeated(session))
        {
            BeginCombatResolution(session, true, $"Victoire. {enemy.DisplayName} subit {applied} degats.");
            return;
        }

        BeginTurn(session, CombatTurn.Enemy, $"{enemy.DisplayName} subit {applied} degats.");
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

        int supportCount = CountPrayerSupport(session);
        float reduction = ResolvePrayerReduction(supportCount);
        int rawDamage = Mathf.Max(0, enemy.AttackDamage);
        int finalDamage = ResolveReducedDamage(rawDamage, reduction);
        int applied = session.Player.ApplyDamage(finalDamage, "combat");
        PlayActionAudio(ActionAudioCue.CombatHit, ResolveCombatAudioPosition(session, preferEnemy: false));

        string message = $"{enemy.DisplayName} inflige {applied} degats.";
        if (supportCount > 0)
        {
            message = $"{message} Prieres: -{Mathf.RoundToInt(reduction * 100f)}%.";
        }

        if (session.Player.CurrentHp <= 0)
        {
            BeginCombatResolution(session, false, $"Defaite. {message}");
            return;
        }

        BeginTurn(session, CombatTurn.Player, message);
    }

    private void BeginTurn(CombatSession session, CombatTurn turn, string message)
    {
        if (session == null || session.State.Finished || session.State.Resolving)
        {
            return;
        }

        session.State.BeginTurn(turn, Time.time, turnDurationSeconds, enemyActionDelay, message);
        PlayActionAudio(ActionAudioCue.CombatTurn, ResolveCombatAudioPosition(session, preferEnemy: turn == CombatTurn.Enemy));
        SendSnapshot(session, session.State.LastMessage);
    }

    private float PlayPlayerBasicAttackPresentation(CombatSession session)
    {
        if (session?.Player == null)
        {
            return DefaultBasicAttackAnimationDuration;
        }

        Animator animator = session.Player.GetComponent<Animator>();
        float duration = ResolveAnimationDuration(animator, BasicAttackAnimationName, DefaultBasicAttackAnimationDuration);
        if (!IsNetworkSessionActive() || session.OwnerClientId == ResolveLocalClientId())
        {
            duration = PlayBasicAttackAnimationLocally(session.Player);
        }

        if (IsNetworkSessionActive() &&
            IsSpawned &&
            session.OwnerClientId != ResolveLocalClientId())
        {
            PlayPlayerBasicAttackClientRpc(session.SessionId, BuildClientRpcParams(session.OwnerClientId));
        }

        return Mathf.Max(0.05f, duration);
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
            PlayBasicAttackAnimationLocally(controller);
        }
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
                session.State.PlayerActionLocked,
                message ?? string.Empty);
            CombatSnapshotClientRpc(snapshot, BuildClientRpcParams(session.OwnerClientId));
            return;
        }

        CombatHudController.EnsureInstance().ShowSnapshot(
            session.SessionId,
            (CombatHudController.TurnState)turn,
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
            session.State.PlayerActionLocked,
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
                runtime.attackDamage));
        }

        return enemies;
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

        MoveTransformTo(controller.transform, position, rotation);
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

    private void SuppressLocalClientMovement(string sessionId, Vector3 combatPosition, Quaternion combatRotation)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || !locallySuppressedSessions.Add(sessionId))
        {
            return;
        }

        SquadCharacterController controller = ResolveControllerForClient(ResolveLocalClientId());
        if (controller == null)
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
        if (IsNetworkSessionActive())
        {
            Transform root = NetcodePlayerUtils.GetPlayerTransform(clientId);
            return root != null ? root.GetComponentInChildren<SquadCharacterController>(true) : null;
        }

        Transform localRoot = LocalPlayerContext.LocalCharacterRoot;
        if (localRoot != null)
        {
            return localRoot.GetComponentInChildren<SquadCharacterController>(true);
        }

#if UNITY_2023_1_OR_NEWER
        return FindFirstObjectByType<SquadCharacterController>();
#else
        return FindObjectOfType<SquadCharacterController>();
#endif
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

    private float PlayBasicAttackAnimationLocally(SquadCharacterController controller)
    {
        if (controller != null)
        {
            controller.Stop();
        }

        PlayActionAudio(
            ActionAudioCue.CombatAttack,
            controller != null ? controller.transform.position : transform.position);

        Animator animator = controller != null ? controller.GetComponent<Animator>() : null;
        StarterMotorAnimatorDriver animatorDriver = controller != null
            ? controller.GetComponent<StarterMotorAnimatorDriver>()
            : null;

        float duration = PlayNamedAnimation(animator, BasicAttackAnimationName, DefaultBasicAttackAnimationDuration);
        if (animatorDriver != null)
        {
            StartCoroutine(RestoreAnimatorDriverAfterDelay(animatorDriver, duration));
        }

        return duration;
    }

    private IEnumerator RestoreAnimatorDriverAfterDelay(StarterMotorAnimatorDriver animatorDriver, float duration)
    {
        if (animatorDriver == null)
        {
            yield break;
        }

        bool wasEnabled = animatorDriver.enabled;
        if (!wasEnabled)
        {
            yield break;
        }

        animatorDriver.enabled = false;
        yield return new WaitForSeconds(Mathf.Max(0.05f, duration));

        if (animatorDriver != null)
        {
            animatorDriver.enabled = true;
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
