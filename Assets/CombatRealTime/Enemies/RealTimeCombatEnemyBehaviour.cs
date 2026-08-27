using System;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RealTimeCombatEnemy))]
public sealed class RealTimeCombatEnemyBehaviour : MonoBehaviour
{
    [SerializeField] private RealTimeCombatEnemy enemy;
    [SerializeField] private NavMeshAgent navigationAgent;
    [SerializeField, Min(0.05f), Tooltip("Distance de recherche pour recaler l'agent sur le NavMesh avant activation.")]
    private float navMeshSampleDistance = 1.5f;
    [SerializeField, Min(0f), Tooltip("Ecart maximal autorise pour raccrocher l'agent au NavMesh sans deplacer l'ActorRoot.")]
    private float navMeshReattachTolerance = 0.15f;
    [SerializeField, Min(0.02f), Tooltip("Delai entre deux tentatives de recalage quand le NavMesh n'est pas encore pret.")]
    private float navMeshRetryInterval = 0.25f;
    [SerializeField, Min(0.1f), Tooltip("Delai minimal entre deux demandes de reconstruction du NavMesh local.")]
    private float navMeshRebuildRequestInterval = 1f;
    [SerializeField, Tooltip("Point de retour optionnel. La position initiale est utilisee s'il est vide.")]
    private Transform patrolPoint;
    [SerializeField, Min(0.1f)] private float meleeAttackDistance = 2.6f;
    [SerializeField, Min(0.1f)] private float rangedAttackDistance = 8f;
    [Header("Pursuit Zone")]
    [SerializeField, Min(0.1f), Tooltip("Rayon horizontal autour du spawn. Hors de cette zone, l'ennemi cesse son engagement puis revient a son origine.")]
    private float pursuitRadius = 20f;
    [SerializeField, Min(0f), Tooltip("Temps d'arret apres la derniere attaque avant le retour au spawn.")]
    private float disengagePauseSeconds = 1f;
    [Header("Movement")]
    [SerializeField, Tooltip("Autorise l'ennemi a poursuivre Lucian, rechercher sa derniere position et retourner a sa patrouille pendant son alerte.")]
    private bool canPursuePlayer = true;
    [Header("Alertness")]
    [SerializeField, Min(0.1f)] private float alertedVisionDistance = 18f;
    [SerializeField, Min(0f)] private float alertMemorySeconds = 10f;
    [Header("Search")]
    [SerializeField, Min(0f)] private float searchLastKnownPositionSeconds = 3f;
    [SerializeField, Min(0.05f)] private float searchArrivalDistance = 0.65f;
    [Header("Attack Selection")]
    [SerializeField, Range(0f, 100f), Tooltip("Chance de privilegier une attaque melee quand les deux familles sont disponibles.")]
    private float meleeAttackPreferencePercent = 70f;
    [Header("Combat Decision")]
    [SerializeField, Min(0f), Tooltip("Temps minimal d'observation/repositionnement avant une attaque disponible.")]
    private float minimumObserveSeconds = 0.18f;
    [SerializeField, Min(0f), Tooltip("Temps maximal d'observation/repositionnement avant une attaque disponible.")]
    private float maximumObserveSeconds = 0.48f;
    [SerializeField, Min(0f), Tooltip("Temps minimal entre la fin d'une action et la prochaine decision offensive.")]
    private float minimumRecoverySeconds = 0.3f;
    [SerializeField, Min(0f), Tooltip("Temps maximal entre la fin d'une action et la prochaine decision offensive.")]
    private float maximumRecoverySeconds = 0.65f;
    [SerializeField, Tooltip("Active un journal compact de decision et de navigation pour cet ennemi.")]
    private bool logCombatDiagnostics;
    [SerializeField, Min(0.05f)] private float patrolArrivalDistance = 0.15f;
    [SerializeField, Min(0f)] private float turnSpeedDegreesPerSecond = 540f;
    [SerializeField, Tooltip("Journalise le replacement direct de l'ennemi par une LightSkill.")]
    private bool logCinematicPlacementDiagnostics = true;

    private Transform player;
    private VisionField visionField;
    private CombatEnemyPhysicsMotor physicsMotor;
    private CombatEnemyRuntimeContract runtimeContract;
    private CombatEnemyLocomotionController combatLocomotion;
    private bool attackMode;
    private bool alerted;
    private bool provokedByPlayer;
    private bool searchingLastKnownPosition;
    private bool searchCompletedForCurrentAlert;
    private bool hasLastKnownPlayerPosition;
    private bool returnedToPatrolWhilePlayerVisible;
    private bool cinematicSuspended;
    private bool navigationSuppressedForCinematic;
    private PursuitDisengageState pursuitDisengageState;
    private Vector3 initialPatrolPosition;
    private Quaternion initialPatrolRotation;
    private float lastAttackStartedAt;
    private float lastSeenPlayerAt;
    private float searchEndsAt;
    private float normalVisionDistance;
    private float nextNavMeshRetryTime;
    private float nextNavMeshRebuildRequestTime;
    private Vector3 lastKnownPlayerPosition;
    private float nextAttackDecisionAt;
    private EnemyCombatPhase combatPhase;
    private string combatPhaseReason;
    private string lastNavigationFailure;
    private SquadAIManager navMeshManager;

    private enum PursuitDisengageState
    {
        None,
        AwaitingActiveAttack,
        Pause,
        Returning
    }

    private enum EnemyCombatPhase
    {
        WaitingForRuntimeReady,
        Idle,
        Alert,
        Chase,
        Position,
        Observe,
        Attack,
        Recovery,
        DisengagePause,
        Return
    }

    public event Action<bool> AttackModeChanged;
    public bool IsInAttackMode => attackMode && provokedByPlayer;
    public bool IsAlerted => alerted;
    public bool IsCinematicSuspended => cinematicSuspended;
    public float PursuitRadius => pursuitRadius;
    public bool IsPlayerOutsidePursuitZone => player != null &&
                                               HorizontalDistance(initialPatrolPosition, player.position) > pursuitRadius;
    public bool ShouldEndCombatForPursuit => pursuitDisengageState == PursuitDisengageState.Pause ||
                                              pursuitDisengageState == PursuitDisengageState.Returning;
    public bool IsRuntimeReady => runtimeContract != null && runtimeContract.CanRunCombat &&
                                  navigationAgent != null && navigationAgent.isActiveAndEnabled &&
                                  navigationAgent.isOnNavMesh && !cinematicSuspended;

    /// <summary>Called only when the authored attack has reached a valid ground recovery.</summary>
    public void NotifyAttackCompleted()
    {
        nextAttackDecisionAt = Time.time + UnityEngine.Random.Range(
            Mathf.Min(minimumObserveSeconds, maximumObserveSeconds),
            Mathf.Max(minimumObserveSeconds, maximumObserveSeconds));
        SetCombatPhase(EnemyCombatPhase.Recovery, "attaque terminee");
    }

    private void Reset()
    {
        enemy = GetComponent<RealTimeCombatEnemy>();
        navigationAgent = GetComponent<NavMeshAgent>();
    }

    private void Awake()
    {
        initialPatrolPosition = transform.position;
        initialPatrolRotation = transform.rotation;
        lastAttackStartedAt = Time.time;
        combatPhase = EnemyCombatPhase.Idle;

        if (enemy == null)
        {
            enemy = GetComponent<RealTimeCombatEnemy>();
        }

        visionField = enemy != null ? enemy.VisionField : GetComponent<VisionField>();
        if (visionField != null)
        {
            normalVisionDistance = visionField.MaximumDistance;
        }

        if (navigationAgent == null)
        {
            navigationAgent = GetComponent<NavMeshAgent>();
        }

        physicsMotor = GetComponent<CombatEnemyPhysicsMotor>();
        runtimeContract = GetComponent<CombatEnemyRuntimeContract>();
        combatLocomotion = GetComponent<CombatEnemyLocomotionController>();

        if (navigationAgent != null)
        {
            navigationAgent.updateRotation = false;
            TryPrepareNavigationAgent();
        }
    }

    private void OnEnable()
    {
        if (enemy == null)
        {
            enemy = GetComponent<RealTimeCombatEnemy>();
        }

        if (enemy != null)
        {
            enemy.LightAbsorbed += OnLightAbsorbed;
        }

        BindNavMeshManager(FindFirstObjectByType<SquadAIManager>());
        TryPrepareNavigationAgent();
    }

    private void Update()
    {
        if (navMeshManager == null)
        {
            BindNavMeshManager(FindFirstObjectByType<SquadAIManager>());
        }

        player = LocalPlayerContext.LocalCharacterRoot;
        if (enemy == null || player == null || (enemy.Health != null && enemy.Health.IsDead))
        {
            ExitAlert();
            SetAttackMode(false);
            StopMovement();
            return;
        }

        if (cinematicSuspended)
        {
            StopMovement();
            return;
        }

        if (runtimeContract != null && !runtimeContract.CanRunCombat)
        {
            StopMovement();
            SetCombatPhase(EnemyCombatPhase.WaitingForRuntimeReady, "contrat runtime invalide");
            return;
        }

        if (!TryPrepareNavigationAgent())
        {
            StopMovement();
            SetCombatPhase(EnemyCombatPhase.WaitingForRuntimeReady, "NavMesh local indisponible");
            return;
        }

        if (TickPursuitDisengagement())
        {
            return;
        }

        float distance = HorizontalDistance(transform.position, player.position);
        combatLocomotion?.SetCombatTarget(player);
        if (enemy.CanSeePlayer)
        {
            lastKnownPlayerPosition = player.position;
            hasLastKnownPlayerPosition = true;
            searchingLastKnownPosition = false;
            searchCompletedForCurrentAlert = false;
            EnterAlert();
            // La detection seule suffit a faire vivre l'ennemi, meme sans riposte stockee.
            FacePlayer();
        }
        else
        {
            returnedToPatrolWhilePlayerVisible = false;
            UpdateAlertness();
        }

        RealTimeCombatManager manager = RealTimeCombatManager.Instance;
        bool canRetaliate = manager != null
            && manager.IsCombatActive
            && manager.EngagedEnemy == enemy
            && enemy.IsRetaliationReady;
        // After a real hit, the engagement and its spawn zone are authoritative.
        // A transient raycast/FOV loss must not make a provoked enemy forget the player.
        bool hasCombatTarget = enemy.CanSeePlayer || provokedByPlayer;

        if (provokedByPlayer && IsPlayerOutsidePursuitZone)
        {
            BeginPursuitDisengagement();
            return;
        }

        if (!attackMode && provokedByPlayer)
        {
            SetAttackMode(true);
        }
        if (!attackMode)
        {
            if (canPursuePlayer && !SearchLastKnownPosition())
            {
                ReturnToPatrol();
            }
            else if (!canPursuePlayer)
            {
                StopMovement();
            }

            if (enemy.CanSeePlayer)
            {
                FacePlayer();
            }
            return;
        }

        if (enemy.HasRetaliationPending)
        {
            SetCombatPhase(EnemyCombatPhase.Attack, "skill actif");
            StopMovement();
            return;
        }

        // A root attack can leave the enemy briefly turned away from Lucian. While
        // its alert memory is active, keep reacquiring the combat target instead of
        // blocking the next stored retaliation on the field-of-view check alone.
        if (hasCombatTarget)
        {
            FacePlayer();
        }

        // Voir Lucian rend l'ennemi alerte, mais seule une attaque de lumiere
        // recue le fait poursuivre pour convertir son ledger en riposte.
        if (!provokedByPlayer)
        {
            SetCombatPhase(EnemyCombatPhase.Alert, "pas encore provoque");
            StopMovement();
            return;
        }

        if (!canRetaliate)
        {
            SetCombatPhase(EnemyCombatPhase.Recovery, "riposte indisponible");
            if (canPursuePlayer)
            {
                MoveTowardsPlayer(Mathf.Max(meleeAttackDistance, combatLocomotion != null
                    ? combatLocomotion.Positioning.preferredDistance
                    : meleeAttackDistance));
            }
            else
            {
                StopMovement();
            }
            return;
        }

        SkillSO plannedSkill = enemy.PeekRetaliationSkill(meleeAttackPreferencePercent * 0.01f);
        if (plannedSkill == null)
        {
            StopMovement();
            return;
        }

        // La portee du SkillSO est la source auteur de l'attaque. Ainsi une
        // attaque melee peut aussi etre lancee a distance si son clip le permet
        // (par exemple le saut du GiantJuggernaut), tout en restant valable au
        // corps a corps grace a sa distance minimale.
        float fallbackAttackDistance = plannedSkill.EnemyRange == RealTimeCombatRange.Ranged
            ? rangedAttackDistance
            : meleeAttackDistance;
        float attackMinimumDistance = plannedSkill.MinimumHitDistance;
        float attackDistance = Mathf.Max(fallbackAttackDistance, plannedSkill.MaximumHitDistance);

        if (distance < attackMinimumDistance || distance > attackDistance)
        {
            SetCombatPhase(EnemyCombatPhase.Chase, distance < attackMinimumDistance ? "trop proche" : "hors portee");
            if (canPursuePlayer)
            {
                MoveTowardsPlayer(attackDistance);
            }
            else
            {
                StopMovement();
            }
            return;
        }

        // A readable enemy positions itself briefly in range before committing.
        // The locomotion controller uses this period to orbit, approach or retreat.
        if (Time.time < nextAttackDecisionAt)
        {
            SetCombatPhase(EnemyCombatPhase.Observe, "fenetre de decision");
            if (canPursuePlayer)
            {
                MoveTowardsPlayer(attackDistance);
            }
            else
            {
                StopMovement();
            }
            return;
        }

        if (hasCombatTarget && enemy.TryStartRetaliation(meleeAttackPreferencePercent * 0.01f))
        {
            StopMovement();
            lastAttackStartedAt = Time.time;
            nextAttackDecisionAt = Time.time + UnityEngine.Random.Range(
                Mathf.Min(minimumRecoverySeconds, maximumRecoverySeconds),
                Mathf.Max(minimumRecoverySeconds, maximumRecoverySeconds));
            SetCombatPhase(EnemyCombatPhase.Attack, "attaque lancee");
            AudioClipSO attackSfx = enemy.ActiveSkill != null ? enemy.ActiveSkill.EnemyAttackSfx : null;
            if (attackSfx != null)
            {
                AudioManager.PlayClipAtPoint(attackSfx, transform.position);
            }

            return;
        }

        SetCombatPhase(EnemyCombatPhase.Position, "skill non lance");
        if (canPursuePlayer)
        {
            MoveTowardsPlayer(attackDistance);
        }
        else
        {
            StopMovement();
        }
    }

    /// <summary>Suspends only this behaviour while a player cinematic owns the encounter.</summary>
    public void SetCinematicSuspended(bool suspended)
    {
        cinematicSuspended = suspended;
        if (suspended)
        {
            physicsMotor?.EnterCinematic();
            StopMovement();
            return;
        }

        physicsMotor?.ExitCinematic();

        if (navigationSuppressedForCinematic)
        {
            navigationSuppressedForCinematic = false;
            nextNavMeshRetryTime = 0f;
            TryPrepareNavigationAgent();
            if (logCinematicPlacementDiagnostics)
            {
                Debug.Log("[LightSkill Debug] Enemy '" + name + "' IA restauree | position=" + transform.position +
                          " | navEnabled=" + (navigationAgent != null && navigationAgent.enabled) + ".", this);
            }
        }
    }

    /// <summary>Places the enemy directly for a LightSkill and keeps its AI suspended for the Timeline.</summary>
    public bool PlaceForCinematic(Vector3 position, Quaternion rotation)
    {
        if (logCinematicPlacementDiagnostics)
        {
            Debug.Log("[LightSkill Debug] Enemy '" + name + "' placement | avant=" + transform.position +
                      " | cible=" + position + " | navEnabled=" + (navigationAgent != null && navigationAgent.enabled) + ".", this);
        }
        SetCinematicSuspended(true);
        StopMovement();
        if (navigationAgent != null && navigationAgent.enabled)
        {
            navigationAgent.enabled = false;
            navigationSuppressedForCinematic = true;
        }
        transform.position = position;
        transform.rotation = rotation;
        Physics.SyncTransforms();
        if (logCinematicPlacementDiagnostics)
        {
            Debug.Log("[LightSkill Debug] Enemy '" + name + "' placement termine | apres=" + transform.position +
                      " | navSuppressed=" + navigationSuppressedForCinematic + ".", this);
        }
        return true;
    }

    /// <summary>Applies a relative Timeline root-motion sample while navigation is suspended.</summary>
    public void ApplyCinematicRootMotion(Vector3 worldDeltaPosition, Quaternion deltaRotation)
    {
        if (!cinematicSuspended)
        {
            return;
        }

        transform.SetPositionAndRotation(transform.position + worldDeltaPosition, deltaRotation * transform.rotation);
        Physics.SyncTransforms();
    }

    private void OnDisable()
    {
        BindNavMeshManager(null);
        if (enemy != null)
        {
            enemy.LightAbsorbed -= OnLightAbsorbed;
        }

        ExitAlert();
        StopMovement();
        SetAttackMode(false);
    }

    private void OnLightAbsorbed(int _)
    {
        if (runtimeContract != null && !runtimeContract.CanRunCombat)
        {
            Debug.LogError("[RealTimeCombatEnemyBehaviour] Provocation ignoree sur '" + name +
                           "' : contrat runtime ennemi invalide.", this);
            return;
        }

        CancelPursuitDisengagement();
        provokedByPlayer = true;
        EnterAlert();
        SetAttackMode(true);
        nextAttackDecisionAt = Time.time + UnityEngine.Random.Range(
            Mathf.Min(minimumObserveSeconds, maximumObserveSeconds),
            Mathf.Max(minimumObserveSeconds, maximumObserveSeconds));
        SetCombatPhase(EnemyCombatPhase.Observe, "provocation lumineuse");
        RealTimeCombatManager.Instance?.SetEnemyAttackMode(enemy, true);
    }

    private void EnterAlert()
    {
        alerted = true;
        lastSeenPlayerAt = Time.time;

        if (visionField != null)
        {
            visionField.SetMaximumDistance(Mathf.Max(normalVisionDistance, alertedVisionDistance));
        }
    }

    private void UpdateAlertness()
    {
        // Once Lucian has damaged this enemy, the pursuit zone, rather than
        // the short visual-memory timer, is the only automatic disengagement.
        if (!alerted || provokedByPlayer || Time.time - lastSeenPlayerAt < alertMemorySeconds)
        {
            return;
        }

        ExitAlert();
        returnedToPatrolWhilePlayerVisible = false;
        SetAttackMode(false);
    }

    private void ExitAlert()
    {
        bool leavingAlert = alerted;
        alerted = false;
        provokedByPlayer = false;
        searchingLastKnownPosition = false;
        searchCompletedForCurrentAlert = false;
        if (visionField != null)
        {
            visionField.SetMaximumDistance(normalVisionDistance);
        }

        if (leavingAlert)
        {
            RealTimeCombatManager.Instance?.SetEnemyAttackMode(enemy, false);
        }
    }

    private void BeginPursuitDisengagement()
    {
        if (pursuitDisengageState != PursuitDisengageState.None)
        {
            return;
        }

        StopMovement();
        SetCombatPhase(EnemyCombatPhase.DisengagePause, "hors zone de poursuite");
        pursuitDisengageState = enemy != null && enemy.HasRetaliationPending
            ? PursuitDisengageState.AwaitingActiveAttack
            : PursuitDisengageState.Pause;
        if (pursuitDisengageState == PursuitDisengageState.Pause)
        {
            BeginPursuitDisengagePause();
        }
    }

    private bool TickPursuitDisengagement()
    {
        switch (pursuitDisengageState)
        {
            case PursuitDisengageState.None:
                return false;
            case PursuitDisengageState.AwaitingActiveAttack:
                StopMovement();
                if (enemy == null || !enemy.HasRetaliationPending)
                {
                    pursuitDisengageState = PursuitDisengageState.Pause;
                    BeginPursuitDisengagePause();
                }
                return true;
            case PursuitDisengageState.Pause:
                StopMovement();
                if (Time.time >= searchEndsAt)
                {
                    pursuitDisengageState = PursuitDisengageState.Returning;
                }
                return true;
            case PursuitDisengageState.Returning:
                SetCombatPhase(EnemyCombatPhase.Return, "retour au spawn");
                ReturnToPatrol();
                if (HorizontalDistance(transform.position, initialPatrolPosition) <= patrolArrivalDistance)
                {
                    pursuitDisengageState = PursuitDisengageState.None;
                }
                return true;
            default:
                return false;
        }
    }

    private void BeginPursuitDisengagePause()
    {
        searchEndsAt = Time.time + disengagePauseSeconds;
        ExitAlert();
        SetAttackMode(false);
    }

    private void CancelPursuitDisengagement()
    {
        pursuitDisengageState = PursuitDisengageState.None;
    }

    private void MoveTowardsPlayer(float attackDistance)
    {
        if (combatLocomotion != null && player != null)
        {
            combatLocomotion.NavigateTowardsTarget(attackDistance);
            return;
        }

        Vector3 destination = enemy.CanSeePlayer || !hasLastKnownPlayerPosition
            ? player.position
            : lastKnownPlayerPosition;
        MoveTowards(destination, attackDistance);
    }

    private bool SearchLastKnownPosition()
    {
        if (enemy.CanSeePlayer || !alerted || enemy.HasStoredLightDamage || enemy.HasRetaliationPending)
        {
            return false;
        }

        if (!searchingLastKnownPosition && !searchCompletedForCurrentAlert && hasLastKnownPlayerPosition)
        {
            searchingLastKnownPosition = searchLastKnownPositionSeconds > 0f;
            searchEndsAt = Time.time + searchLastKnownPositionSeconds;
        }

        if (!searchingLastKnownPosition)
        {
            return false;
        }

        if (Time.time >= searchEndsAt ||
            HorizontalDistance(transform.position, lastKnownPlayerPosition) <= searchArrivalDistance)
        {
            searchingLastKnownPosition = false;
            searchCompletedForCurrentAlert = true;
            StopMovement();
            return false;
        }

        Face(lastKnownPlayerPosition);
        MoveTowards(lastKnownPlayerPosition, searchArrivalDistance);
        return true;
    }

    private void ReturnToPatrol()
    {
        Vector3 destination = patrolPoint != null ? patrolPoint.position : initialPatrolPosition;
        if (HorizontalDistance(transform.position, destination) <= patrolArrivalDistance)
        {
            SetCombatPhase(EnemyCombatPhase.Idle, "patrouille atteinte");
            StopMovement();
            if (patrolPoint == null)
            {
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    initialPatrolRotation,
                    turnSpeedDegreesPerSecond * Time.deltaTime);
            }

            return;
        }

        MoveTowards(destination, patrolArrivalDistance);
    }

    private void MoveTowards(Vector3 destination, float destinationStoppingDistance)
    {
        if (combatLocomotion != null)
        {
            combatLocomotion.NavigateTo(destination, destinationStoppingDistance);
            return;
        }

        if (TryPrepareNavigationAgent())
        {
            navigationAgent.isStopped = false;
            navigationAgent.stoppingDistance = destinationStoppingDistance;
            if (NavMesh.SamplePosition(destination, out NavMeshHit destinationHit, navMeshSampleDistance, navigationAgent.areaMask))
            {
                destination = destinationHit.position;
            }

            navigationAgent.SetDestination(destination);
            combatLocomotion?.FaceTarget(player != null ? player.position : destination);
            return;
        }

        // A missing NavMesh must stop this actor. Direct Transform movement here
        // used to compete with the physics motor and could move an enemy through
        // a floor or into a different NavMesh island.
        StopMovement();
    }

    private void StopMovement()
    {
        combatLocomotion?.StopNavigation();
        if (navigationAgent != null && navigationAgent.isActiveAndEnabled && navigationAgent.isOnNavMesh)
        {
            navigationAgent.isStopped = true;
            navigationAgent.ResetPath();
        }
    }

    private bool TryPrepareNavigationAgent()
    {
        if (navigationAgent == null || !navigationAgent.gameObject.activeInHierarchy)
        {
            ReportNavigationFailure("NavMeshAgent absent ou GameObject inactif");
            return false;
        }

        navigationAgent.updateRotation = false;
        if (navigationAgent.isActiveAndEnabled && navigationAgent.isOnNavMesh)
        {
            lastNavigationFailure = null;
            return true;
        }

        if (Time.time < nextNavMeshRetryTime)
        {
            ReportNavigationFailure("attente prochaine tentative NavMesh");
            return false;
        }

        int areaMask = navigationAgent.areaMask == 0 ? NavMesh.AllAreas : navigationAgent.areaMask;
        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, navMeshSampleDistance, areaMask))
        {
            if (navigationAgent.enabled)
            {
                physicsMotor?.AuditPose("NavMesh:desactive, aucune projection proche");
                navigationAgent.enabled = false;
            }

            nextNavMeshRetryTime = Time.time + Mathf.Max(0.02f, navMeshRetryInterval);
            RequestLocalNavMeshRebuild();
            ReportNavigationFailure("aucune projection NavMesh locale");
            return false;
        }

        if (!navigationAgent.enabled)
        {
            physicsMotor?.AuditPose("NavMesh:activation demandee");
            navigationAgent.enabled = true;
        }

        if (navigationAgent.isOnNavMesh)
        {
            lastNavigationFailure = null;
            return true;
        }

        // The gameplay root must never be projected onto an arbitrary NavMesh
        // polygon. A transient off-mesh state after an animation must not turn
        // into a visible teleport to another floor or corridor.
        Vector3 offset = hit.position - transform.position;
        offset.y = 0f;
        if (offset.sqrMagnitude <= navMeshReattachTolerance * navMeshReattachTolerance &&
            Mathf.Abs(hit.position.y - transform.position.y) <= navMeshReattachTolerance &&
            TryWarpNavigationAgent(hit.position))
        {
            return navigationAgent.isOnNavMesh;
        }

        physicsMotor?.AuditPose("NavMesh:desactive, projection trop eloignee=" + hit.position);
        navigationAgent.enabled = false;
        nextNavMeshRetryTime = Time.time + Mathf.Max(0.02f, navMeshRetryInterval);
        ReportNavigationFailure("projection NavMesh trop eloignee: " + hit.position);
        return false;
    }

    private void RequestLocalNavMeshRebuild()
    {
        if (Time.time < nextNavMeshRebuildRequestTime)
        {
            return;
        }

        nextNavMeshRebuildRequestTime = Time.time + Mathf.Max(0.1f, navMeshRebuildRequestInterval);
        SquadAIManager manager = FindFirstObjectByType<SquadAIManager>();
        if (manager == null)
        {
            ReportNavigationFailure("SquadAIManager introuvable pour rebuild NavMesh");
            return;
        }

        BindNavMeshManager(manager);
        manager.RequestNavMeshRebuild("ennemi en attente: " + name);
        if (logCombatDiagnostics)
        {
            Debug.Log("[CombatEnemyAI] " + name + " demande un rebuild NavMesh local | position=" +
                      transform.position + ".", this);
        }
    }

    private bool TryWarpNavigationAgent(Vector3 position)
    {
        physicsMotor?.AuditPose("NavMesh:avant Warp=" + position);
        bool warped = navigationAgent.Warp(position);
        physicsMotor?.AuditPose("NavMesh:apres Warp=" + warped);
        return warped;
    }

    private void FacePlayer()
    {
        Face(player.position);
    }

    private void Face(Vector3 targetPosition)
    {
        if (combatLocomotion != null)
        {
            combatLocomotion.FaceTarget(targetPosition);
            return;
        }
        Vector3 direction = targetPosition - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= Mathf.Epsilon)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            turnSpeedDegreesPerSecond * Time.deltaTime);
    }

    private void SetAttackMode(bool value)
    {
        if (attackMode == value)
        {
            return;
        }

        attackMode = value;
        if (attackMode)
        {
            lastAttackStartedAt = Time.time;
        }

        AttackModeChanged?.Invoke(value);
    }

    private void SetCombatPhase(EnemyCombatPhase next, string reason)
    {
        if (combatPhase == next && combatPhaseReason == reason)
        {
            return;
        }

        combatPhase = next;
        combatPhaseReason = reason;
        if (logCombatDiagnostics)
        {
            Debug.Log("[CombatEnemyAI] " + name + " -> " + next + " | " + reason +
                      " | nav=" + (navigationAgent != null && navigationAgent.isActiveAndEnabled && navigationAgent.isOnNavMesh) +
                      " | velocity=" + (navigationAgent != null && navigationAgent.isActiveAndEnabled && navigationAgent.isOnNavMesh
                          ? navigationAgent.velocity.ToString()
                          : "n/a") +
                      " | provoked=" + provokedByPlayer +
                      " | ledger=" + (enemy != null && enemy.HasStoredLightDamage) +
                      " | activeSkill=" + (enemy != null && enemy.ActiveSkill != null ? enemy.ActiveSkill.SkillName : "none") +
                      " | retaliationReady=" + (enemy != null && enemy.IsRetaliationReady), this);
        }
    }

    private void BindNavMeshManager(SquadAIManager nextManager)
    {
        if (navMeshManager == nextManager)
        {
            return;
        }

        if (navMeshManager != null)
        {
            navMeshManager.NavMeshRebuildCompleted -= OnNavMeshRebuildCompleted;
        }

        navMeshManager = nextManager;
        if (navMeshManager != null)
        {
            navMeshManager.NavMeshRebuildCompleted += OnNavMeshRebuildCompleted;
        }
    }

    private void OnNavMeshRebuildCompleted(SquadAIManager.NavMeshBuildReport report)
    {
        nextNavMeshRetryTime = 0f;
        if (!report.succeeded)
        {
            ReportNavigationFailure("bake NavMesh echoue: " + report.reason +
                                    " (sources=" + report.sourceCount + ")");
            return;
        }

        lastNavigationFailure = null;
        if (logCombatDiagnostics)
        {
            Debug.Log("[CombatEnemyAI] " + name + " recu NavMesh valide | sources=" +
                      report.sourceCount + " | bounds=" + report.bounds + ".", this);
        }
    }

    private void ReportNavigationFailure(string reason)
    {
        if (lastNavigationFailure == reason)
        {
            return;
        }

        lastNavigationFailure = reason;
        if (logCombatDiagnostics)
        {
            Debug.LogWarning("[CombatEnemyAI] " + name + " navigation indisponible: " + reason +
                             " | position=" + transform.position + ".", this);
        }
    }

    private static float HorizontalDistance(Vector3 first, Vector3 second)
    {
        first.y = 0f;
        second.y = 0f;
        return Vector3.Distance(first, second);
    }
}
