using System;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RealTimeCombatEnemy))]
public sealed class RealTimeCombatEnemyBehaviour : MonoBehaviour
{
    [SerializeField] private RealTimeCombatEnemy enemy;
    [SerializeField] private NavMeshAgent navigationAgent;
    [SerializeField, Tooltip("Point de retour optionnel. La position initiale est utilisee s'il est vide.")]
    private Transform patrolPoint;
    [SerializeField, Min(0.1f)] private float meleeAttackDistance = 2.6f;
    [SerializeField, Min(0.1f)] private float rangedAttackDistance = 8f;
    [SerializeField, Min(0.1f)] private float disengageDistance = 14f;
    [SerializeField, Min(0f)] private float returnToPatrolAfterSeconds = 5f;
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

    private Transform player;
    private VisionField visionField;
    private bool attackMode;
    private bool alerted;
    private bool provokedByPlayer;
    private bool searchingLastKnownPosition;
    private bool searchCompletedForCurrentAlert;
    private bool hasLastKnownPlayerPosition;
    private bool returnedToPatrolWhilePlayerVisible;
    private Vector3 initialPatrolPosition;
    private Quaternion initialPatrolRotation;
    private float lastAttackStartedAt;
    private float lastSeenPlayerAt;
    private float searchEndsAt;
    private float normalVisionDistance;
    private Vector3 lastKnownPlayerPosition;

    public event Action<bool> AttackModeChanged;
    public bool IsInAttackMode => attackMode && provokedByPlayer;
    public bool IsAlerted => alerted;
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

        if (navigationAgent != null)
        {
            navigationAgent.updateRotation = false;
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
            if (!SearchLastKnownPosition())
            {
                ReturnToPatrol();
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

        bool hasRetaliationToResolve = enemy.HasStoredLightDamage || enemy.HasRetaliationPending || canRetaliate;
        if (!hasRetaliationToResolve && Time.time - lastAttackStartedAt >= returnToPatrolAfterSeconds)
        {
            returnedToPatrolWhilePlayerVisible = enemy.CanSeePlayer;
            SetAttackMode(false);
            if (!SearchLastKnownPosition())
            {
                ReturnToPatrol();
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

        float attackDistance = plannedSkill.EnemyRange == RealTimeCombatRange.Ranged
            ? rangedAttackDistance
            : meleeAttackDistance;

        if (enemy.CanSeePlayer && distance <= attackDistance && enemy.TryStartRetaliation(meleeAttackPreferencePercent * 0.01f))
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

        if (!enemy.CanSeePlayer || distance > attackDistance)
        {
            MoveTowardsPlayer(attackDistance);
        }
        else
        {
            StopMovement();
        }
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
        if (navigationAgent != null && navigationAgent.isActiveAndEnabled && navigationAgent.isOnNavMesh)
        {
            navigationAgent.isStopped = false;
            navigationAgent.stoppingDistance = destinationStoppingDistance;
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
