using UnityEngine;
using System.Linq;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

namespace Ultrabolt.BrainsAI
{
    /// <summary>
    /// Brain is the core AI controller for all agents.
    /// It manages perception, movement, communication, attack logic,
    /// and supports multiple behavior types (Engager, Ranger, Coward).
    /// 
    /// This component uses NavMeshAgent for navigation and relies on the FOV 
    /// (Field of View) system to update its state and react to the environment.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(NavMeshAgent))]
    public partial class Brain : MonoBehaviour
    {
        #region ===== Variables =====

        public enum BrainType { Engager, Ranger, Coward }

        // ───── Core & FOV ─────
        [HideInInspector] public bool showFov; // Editor-only toggle
        [Tooltip("Reference to the Field of View component used for target detection.")]
        public FOV fov;

        [Tooltip("Defines how this character behaves when interacting with targets.")]
        public BrainType brainType;

        // ───── Communication ─────
        [Tooltip("Draws gizmos to visualize alarm range in the Scene view.")]
        public bool drawAlarmGizmos;

        [Tooltip("Radius in which other agents will be alarmed when this one detects a target.")]
        public float alarmRadius = 10f;

        [Tooltip("Group this character belongs to (e.g., Enemy, Companion, etc.).")]
        public string groupName = "Enemy";

        [Tooltip("Groups that can hear this character's alarm.")]
        public string[] alarmGroups = new string[] { "Enemy", "Companion" };

        // ───── Movement ─────
        [Tooltip("Draws movement-related gizmos in the Scene view.")]
        public bool drawMovementGizmos;

        [Tooltip("Speed when sneaking.")]
        public float sneakSpeed = 1f;

        [Tooltip("Speed when walking.")]
        public float walkSpeed = 2f;

        [Tooltip("Speed when running.")]
        public float runSpeed = 4f;

        [Tooltip("X = stopping distance from target, Y = max distance to resume chasing if lost.")]
        public Vector2 moveRange = new(1f, 3f);

        // ───── Jump ─────
        [Tooltip("Controls the jump shape (Y-axis offset over time).")]
        public AnimationCurve jumpCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Tooltip("Maximum jump height, scaled by the jump curve.")]
        public float jumpHeight = 2f;

        [Tooltip("Multiplier that affects how long the jump lasts.")]
        public float durationMultiplier = 1f;

        // ───── Patrol ─────
        [Tooltip("If true, the agent will patrol between waypoints or within a radius.")]
        public bool canPatrol;

        [Tooltip("Draws patrol path gizmos in the Scene view.")]
        public bool drawPatrolGizmos;

        [Tooltip("Optional path reference used for waypoint-based patrols.")]
        public PatrolPath patrolPath;

        [Tooltip("Radius around the origin used for random patrols (when no path is set).")]
        public float patrolRadius = 5f;

        [Tooltip("X = minimum idle time, Y = maximum idle time between patrol points.")]
        public Vector2 standingTime = new(2f, 4f);

        // ───── Attack ─────
        [Tooltip("Enables attack behavior for this character.")]
        public bool canAttack;

        public enum AttackCallType { External, Automatical }

        [Tooltip("Defines how attack is triggered: manually (External) or automatically.")]
        public AttackCallType attackCallType;

        [Tooltip("Maximum number of attack attempts per cycle."), Min(1)]
        public int maxAttacks = 1;

        [Tooltip("Damage dealt per attack. X = min, Y = max.")]
        public Vector2Int damage = new(5, 10);

        [Tooltip("X = minimum attack cooldown, Y = maximum attack cooldown.")]
        public Vector2 attackTime = new(3f, 5f);

        // ───── Internals ─────
        protected Animator anim;
        protected NavMeshAgent agent;
        protected Vector3 storedMovePosition;

        protected float currentSpeed; // Currently active movement speed (sneak, walk, run)
        protected float standingTimer, attackTimer;
        protected float defaultAngularSpeed;
        protected bool canChase, retreat, reversedPath;
        protected bool jumping;
        protected int patrolIndex, selectedAttack;
        private float currentAnimatorSpeed; // Animator blending speed cache

        // ───── Events & Data ─────
        public event System.Action OnAttack;
        public Dictionary<string, bool> StopControlConditions = new();

        // ───── Global Brain List ─────
        protected static readonly List<Brain> brains = new();
        public static IReadOnlyList<Brain> Brains => brains;

        // ───── Coroutines ─────
        public Coroutine alarmRoutine;

        private const float GroundedDistance = 0.5f;

        #endregion

        #region ===== Unity Methods =====

        protected virtual void Start()
        {
            agent = GetComponent<NavMeshAgent>();
            defaultAngularSpeed = agent.angularSpeed;

            // Init timers
            standingTimer = Random.Range(standingTime.x, standingTime.y);
            attackTimer = Random.Range(attackTime.x, attackTime.y);

            agent.autoTraverseOffMeshLink = false;

            // Setup attack event
            OnAttack += () => ApplyAttack(Random.Range(damage.x, damage.y));

            // Reset path when target is lost for non-coward brains
            if (brainType != BrainType.Coward)
                fov.onEnterNone.AddListener(() => agent.ResetPath());

            // Patrol support
            if (patrolPath != null)
            {
                patrolPath.GetNearestPoint(transform.position, out patrolIndex);
                fov.onLostTarget.AddListener(() =>
                    patrolPath.GetNearestPoint(transform.position, out patrolIndex));
            }

            fov.onEnterInvestigate.AddListener(() => standingTimer = 0f);

            anim = GetComponent<Animator>();
        }

        protected virtual void Update()
        {
            // Run behavior only if nothing is stopping control
            if (StopControlConditions.Values.All(v => !v))
            {
                switch (fov.status)
                {
                    case FOV.Status.None: Idle(); break;
                    case FOV.Status.Investigate: Investigate(); break;
                    case FOV.Status.Alerted: Alerted(); break;
                }
            }

            // Handle OffMesh jump
            if (!jumping && agent.isOnOffMeshLink)
                StartCoroutine(JumpAcross());

            if (fov.CurrentTarget == null)
                agent.angularSpeed = defaultAngularSpeed;

            UpdateAnimator();
        }

        protected virtual void OnDrawGizmosSelected()
        {
            if (drawMovementGizmos)
            {
                Gizmos.color = new Color(1f, 0.3f, 0f);
                CustomGizmos.Draw3DMinMaxRange(transform, Vector3.zero, moveRange);
                Gizmos.color = Color.magenta;
                CustomGizmos.DrawCircle(transform, Vector3.zero, (moveRange.x + moveRange.y) * 0.5f);
            }

            if (drawPatrolGizmos)
            {
                Gizmos.color = Color.yellow;
                CustomGizmos.DrawCircle(transform, Vector3.zero, patrolRadius);
            }

            if (drawAlarmGizmos)
            {
                Gizmos.color = Color.black;
                Gizmos.DrawWireSphere(transform.position, alarmRadius);
            }

            Gizmos.color = Color.green;
            if (agent == null) agent = GetComponent<NavMeshAgent>();

            DrawRay(transform.forward);
            DrawRay(-transform.forward);
            DrawRay(transform.right);
            DrawRay(-transform.right);

            void DrawRay(Vector3 dir) =>
                Gizmos.DrawRay(transform.position + dir * agent.radius, Vector3.down * GroundedDistance);
        }


        #endregion

        #region ===== Abilities =====

        /// <summary>
        /// Smoothly jumps across OffMesh links using a curve for vertical movement.
        /// </summary>
        protected IEnumerator JumpAcross()
        {
            jumping = true;

            OffMeshLinkData data = agent.currentOffMeshLinkData;
            Vector3 startPos = agent.transform.position;
            Vector3 endPos = data.endPos;

            // Temporarily disable rotation control
            float previousAngularSpeed = agent.angularSpeed;
            agent.angularSpeed = 0;

            // Calculate duration based on distance
            float distance = Vector3.Distance(startPos, endPos);
            float duration = distance * durationMultiplier;
            float time = 0f;

            while (time < duration)
            {
                float t = time / duration;

                // Horizontal Lerp
                Vector3 pos = Vector3.Lerp(startPos, endPos, t);

                // Vertical curve offset
                float yOffset = jumpCurve.Evaluate(t) * jumpHeight;
                pos.y += yOffset;

                agent.transform.position = pos;
                LookAtTarget(endPos);

                time += Time.deltaTime;
                yield return null;
            }

            // Snap position to end
            agent.transform.position = endPos;
            LookAtTarget(endPos);

            // Restore rotation
            agent.angularSpeed = previousAngularSpeed;

            agent.CompleteOffMeshLink();
            jumping = false;
        }

        /// <summary>
        /// First call of attack triggers the OnAttack event.
        /// </summary>
        public void Attack()
        {
            if (canAttack)
                OnAttack?.Invoke();
        }

        /// <summary>
        /// Called to apply damage at a specific time (e.g., animation event).
        /// </summary>
        public void ApplyAttack(int damage)
        {
            // The damage will apply here, so if you add target damage just inject your methods here.
            // this 'ApplyAttack' is already controlled via Brain script so no need to call it again.

            Debug.Log($"Attack with {damage} damage.");
        }

        #endregion

        #region ===== Utility Methods =====

        /// <summary>
        /// Returns a random valid NavMesh position around a point.
        /// </summary>
        public Vector3 GetRandomNavPosition(Vector3 origin, float radius) =>
            NavMesh.SamplePosition(Random.insideUnitSphere * radius + origin, out NavMeshHit hit, radius, NavMesh.AllAreas)
                ? hit.position : origin;

        /// <summary>
        /// Returns true if the agent is moving.
        /// </summary>
        protected bool AgentIsMoving =>
            Vector3.Distance(transform.position, storedMovePosition) > agent.stoppingDistance + 0.1f &&
            agent.velocity.sqrMagnitude > 0.01f &&
            agent.remainingDistance > 0.1f;

        /// <summary>
        /// Checks if the agent can reach the given destination.
        /// </summary>
        protected bool IsPathReachable(Vector3 destination)
        {
            NavMeshPath path = new();
            agent.CalculatePath(destination, path);
            return path.status == NavMeshPathStatus.PathComplete;
        }

        /// <summary>
        /// Simple grounded check using a downward raycast.
        /// </summary>
        protected bool IsGrounded
        {
            get
            {
                float radius = agent.radius;
                Vector3 origin = transform.position;

                // Offsets in 4 directions (forward, back, right, left)
                Vector3[] offsets =
                {
                    Vector3.forward * radius,
                    Vector3.back * radius,
                    Vector3.right * radius,
                    Vector3.left * radius
                };

                // Cast a ray downward from each offset position
                foreach (var offset in offsets)
                {
                    if (Physics.Raycast(origin + offset, Vector3.down, GroundedDistance))
                        return true;
                }

                return false;
            }
        }

        #endregion
    }
}
