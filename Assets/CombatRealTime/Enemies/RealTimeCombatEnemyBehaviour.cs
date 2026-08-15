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
    [SerializeField, Min(0.02f), Tooltip("Delai entre deux tentatives de recalage quand le NavMesh n'est pas encore pret.")]
    private float navMeshRetryInterval = 0.25f;
    [SerializeField, Tooltip("Point de retour optionnel. La position initiale est utilisee s'il est vide.")]
    private Transform patrolPoint;
    [SerializeField, Min(0.1f)] private float meleeAttackDistance = 2.6f;
    [SerializeField, Min(0.1f)] private float rangedAttackDistance = 8f;
    [SerializeField, Min(0.1f)] private float disengageDistance = 14f;
    [SerializeField, Min(0f)] private float returnToPatrolAfterSeconds = 5f;
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
    [SerializeField, Min(0.05f)] private float patrolArrivalDistance = 0.15f;
    [SerializeField, Min(0f)] private float directMoveSpeed = 3.6f;
    [SerializeField, Min(0f)] private float turnSpeedDegreesPerSecond = 540f;
    [Header("Physical Presence")]
    [SerializeField, Tooltip("Ajoute un corps de collision non-trigger pour empecher Lucian de traverser ou de monter sur cet ennemi.")]
    private bool ensurePhysicalBodyCollider = true;
    [SerializeField, Min(0f), Tooltip("Epaissit legerement le rayon physique derive du NavMeshAgent.")]
    private float physicalBodyRadiusPadding = 0.05f;
    [SerializeField, Tooltip("Journalise le replacement direct de l'ennemi par une LightSkill.")]
    private bool logCinematicPlacementDiagnostics = true;

    private Transform player;
    private VisionField visionField;
    private CapsuleCollider physicalBodyCollider;
    private Rigidbody physicalBodyRigidbody;
    private bool attackMode;
    private bool alerted;
    private bool provokedByPlayer;
    private bool searchingLastKnownPosition;
    private bool searchCompletedForCurrentAlert;
    private bool hasLastKnownPlayerPosition;
    private bool returnedToPatrolWhilePlayerVisible;
    private bool cinematicSuspended;
    private bool navigationSuppressedForCinematic;
    private Vector3 initialPatrolPosition;
    private Quaternion initialPatrolRotation;
    private float lastAttackStartedAt;
    private float lastSeenPlayerAt;
    private float searchEndsAt;
    private float normalVisionDistance;
    private float nextNavMeshRetryTime;
    private Vector3 lastKnownPlayerPosition;

    public event Action<bool> AttackModeChanged;
    public bool IsInAttackMode => attackMode && provokedByPlayer;
    public bool IsAlerted => alerted;
    public bool IsCinematicSuspended => cinematicSuspended;
    public float CurrentDisengageDistance => alerted
        ? Mathf.Max(disengageDistance, alertedVisionDistance)
        : disengageDistance;

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

        EnsurePhysicalBodyCollider();

        if (navigationAgent != null)
        {
            navigationAgent.updateRotation = false;
            TryPrepareNavigationAgent();
        }
    }

    private void EnsurePhysicalBodyCollider()
    {
        if (!ensurePhysicalBodyCollider)
        {
            return;
        }

        Collider[] colliders = GetComponents<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] is CapsuleCollider capsule && !capsule.isTrigger)
            {
                physicalBodyCollider = capsule;
                break;
            }
        }

        if (physicalBodyCollider == null)
        {
            physicalBodyCollider = gameObject.AddComponent<CapsuleCollider>();
        }

        float radius = navigationAgent != null ? navigationAgent.radius : 0.7f;
        float height = navigationAgent != null ? navigationAgent.height : 2.2f;
        physicalBodyCollider.isTrigger = false;
        physicalBodyCollider.direction = 1;
        physicalBodyCollider.radius = Mathf.Max(0.15f, radius + physicalBodyRadiusPadding);
        physicalBodyCollider.height = Mathf.Max(physicalBodyCollider.radius * 2f, height);
        physicalBodyCollider.center = new Vector3(0f, physicalBodyCollider.height * 0.5f, 0f);

        physicalBodyRigidbody = GetComponent<Rigidbody>();
        if (physicalBodyRigidbody == null)
        {
            physicalBodyRigidbody = gameObject.AddComponent<Rigidbody>();
        }

        physicalBodyRigidbody.isKinematic = true;
        physicalBodyRigidbody.useGravity = false;
        physicalBodyRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        physicalBodyRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
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

        TryPrepareNavigationAgent();
    }

    private void Update()
    {
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

        float distance = HorizontalDistance(transform.position, player.position);
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
            && manager.LockedEnemy == enemy
            && enemy.IsRetaliationReady;
        bool hasCombatTarget = enemy.CanSeePlayer || (provokedByPlayer && alerted);

        if (!attackMode && enemy.CanSeePlayer)
        {
            if (!returnedToPatrolWhilePlayerVisible || canRetaliate)
            {
                SetAttackMode(true);
            }
        }
        else if (attackMode && distance > CurrentDisengageDistance)
        {
            SetAttackMode(false);
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

        bool hasRetaliationToResolve = enemy.HasStoredLightDamage || enemy.HasRetaliationPending || canRetaliate;
        if (!hasRetaliationToResolve && Time.time - lastAttackStartedAt >= returnToPatrolAfterSeconds)
        {
            returnedToPatrolWhilePlayerVisible = enemy.CanSeePlayer;
            SetAttackMode(false);
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

        // Voir Lucian rend l'ennemi alerte, mais seule une attaque de lumiere
        // recue le fait poursuivre pour convertir son ledger en riposte.
        if (!provokedByPlayer || !canRetaliate)
        {
            StopMovement();
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
        float attackDistance = Mathf.Max(fallbackAttackDistance, plannedSkill.MaximumHitDistance);

        if (hasCombatTarget && distance <= attackDistance && enemy.TryStartRetaliation(meleeAttackPreferencePercent * 0.01f))
        {
            StopMovement();
            lastAttackStartedAt = Time.time;
            AudioClipSO attackSfx = enemy.ActiveSkill != null ? enemy.ActiveSkill.EnemyAttackSfx : null;
            if (attackSfx != null)
            {
                AudioManager.PlayClipAtPoint(attackSfx, transform.position);
            }

            return;
        }

        if (canPursuePlayer && (!enemy.CanSeePlayer || distance > attackDistance))
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
            StopMovement();
            return;
        }

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
        provokedByPlayer = true;
        EnterAlert();
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
        if (!alerted || Time.time - lastSeenPlayerAt < alertMemorySeconds)
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

    private void MoveTowardsPlayer(float attackDistance)
    {
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
        if (TryPrepareNavigationAgent())
        {
            navigationAgent.isStopped = false;
            navigationAgent.stoppingDistance = destinationStoppingDistance;
            if (NavMesh.SamplePosition(destination, out NavMeshHit destinationHit, navMeshSampleDistance, navigationAgent.areaMask))
            {
                destination = destinationHit.position;
            }

            navigationAgent.SetDestination(destination);
            return;
        }

        Vector3 direction = destination - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= Mathf.Epsilon)
        {
            return;
        }

        transform.position += direction.normalized * (directMoveSpeed * Time.deltaTime);
    }

    private void StopMovement()
    {
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
            return false;
        }

        navigationAgent.updateRotation = false;
        if (navigationAgent.isActiveAndEnabled && navigationAgent.isOnNavMesh)
        {
            return true;
        }

        if (Time.time < nextNavMeshRetryTime)
        {
            return false;
        }

        int areaMask = navigationAgent.areaMask == 0 ? NavMesh.AllAreas : navigationAgent.areaMask;
        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, navMeshSampleDistance, areaMask))
        {
            if (navigationAgent.enabled)
            {
                navigationAgent.enabled = false;
            }

            nextNavMeshRetryTime = Time.time + Mathf.Max(0.02f, navMeshRetryInterval);
            return false;
        }

        transform.position = hit.position;
        if (!navigationAgent.enabled)
        {
            navigationAgent.enabled = true;
        }

        if (navigationAgent.isOnNavMesh)
        {
            return true;
        }

        if (navigationAgent.Warp(hit.position))
        {
            return navigationAgent.isOnNavMesh;
        }

        navigationAgent.enabled = false;
        nextNavMeshRetryTime = Time.time + Mathf.Max(0.02f, navMeshRetryInterval);
        return false;
    }

    private void FacePlayer()
    {
        Face(player.position);
    }

    private void Face(Vector3 targetPosition)
    {
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

    private static float HorizontalDistance(Vector3 first, Vector3 second)
    {
        first.y = 0f;
        second.y = 0f;
        return Vector3.Distance(first, second);
    }
}
