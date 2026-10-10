using UnityEngine;

namespace Ultrabolt.BrainsAI
{
    /// <summary>
    /// Core Brain behavior logic.
    /// Handles idle behavior, investigation, alert reactions,
    /// and specific combat / movement behaviors for different brain types
    /// (Engager, Ranger, Coward).
    /// </summary>
    public partial class Brain : MonoBehaviour
    {
        // Position to retreat to when escaping (used by Coward brain)
        private Vector3 stepBack;

        // Whether the coward brain is currently running away
        public bool escaping;

        #region === Status States ===

        /// <summary>
        /// Runs when the agent has no target in FOV.
        /// Can perform patrol or stay idle depending on settings and brain type.
        /// </summary>
        protected virtual void Idle()
        {
            // Coward brain: return to a safe point after escaping
            if (brainType == BrainType.Coward && escaping)
            {
                float distance = Vector3.Distance(transform.position, stepBack);

                if (distance > 2f)
                {
                    // Continue escaping to the stepBack position
                    MoveTo(stepBack, runSpeed, 0f);

                    // Prevent issues if the agent reaches a navmesh border
                    if (!AgentIsMoving)
                        stepBack = transform.position;

                    return;
                }
                else
                {
                    // Escape completed
                    escaping = false;
                }
            }

            // Patrol if enabled
            if (canPatrol)
            {
                if (patrolPath == null)
                    Patrol(patrolRadius, standingTime, walkSpeed);
                else
                    PatrolPath();
            }
        }

        /// <summary>
        /// Investigation behavior when a suspicious location is detected.
        /// Typically involves short-range patrolling around the last known position.
        /// </summary>
        protected virtual void Investigate()
        {
            switch (brainType)
            {
                case BrainType.Engager:
                case BrainType.Ranger:
                    if (fov.suspectLocation)
                        MoveTo(fov.lastKnownPosition, runSpeed, 2f);
                    else if (fov.canInvestigate)
                        Patrol(5f, new Vector2(1, 2), sneakSpeed);
                    else
                        fov.ResetLastKnownPosition();
                    break;

                case BrainType.Coward:
                    // Cowards don’t investigate, just clear the suspect location
                    fov.ResetLastKnownPosition();
                    break;
            }
        }

        /// <summary>
        /// Alerted behavior — runs when a target is visible.
        /// Routes to specific handler based on brain type.
        /// </summary>
        protected virtual void Alerted()
        {
            float distance = Vector3.Distance(transform.position, fov.CurrentTarget.position);
            float mid = (moveRange.x + moveRange.y) * 0.5f;

            // Simple chase distance check
            if (distance <= mid) canChase = false;
            if (distance > moveRange.y) canChase = true;

            switch (brainType)
            {
                case BrainType.Engager: HandleEngager(); break;
                case BrainType.Ranger: HandleRanger(distance, mid); break;
                case BrainType.Coward: HandleCoward(distance, mid); break;
            }
        }

        #endregion

        #region === Brain Type Handlers ===

        /// <summary>
        /// Engager: chases target and attacks at close range.
        /// </summary>
        private void HandleEngager()
        {
            if (canChase)
                MoveTo(fov.CurrentTarget.position, runSpeed, moveRange.x);
            else
            {
                agent.ResetPath();
                HandleAttack();
            }

            LookAtTarget(fov.CurrentTarget.position);
        }

        /// <summary>
        /// Ranger: keeps mid-range distance and attacks from afar.
        /// </summary>
        private void HandleRanger(float distance, float mid)
        {
            if (distance <= moveRange.x) retreat = true;
            if (distance >= mid) retreat = false;

            if (canChase)
            {
                // Too far → chase again
                MoveTo(fov.CurrentTarget.position, runSpeed, moveRange.x);
                retreat = false;
            }
            else if (retreat)
            {
                // Step back to keep distance
                Retreat(2f);
            }
            else
            {
                agent.ResetPath();
                HandleAttack();
            }

            LookAtTarget(fov.CurrentTarget.position);
        }

        /// <summary>
        /// Handles the attack cycle:
        ///  - Countdown attack timer
        ///  - Random attack selection
        ///  - Trigger animation & damage call
        /// </summary>
        protected virtual void HandleAttack()
        {
            if (!canAttack) return;

            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0f)
            {
                attackTimer = Random.Range(attackTime.x, attackTime.y);
                selectedAttack = Random.Range(1, maxAttacks + 1);
                anim.SetInteger("Attack ID", selectedAttack);
                anim.SetTrigger("Attack");

                if (attackCallType == AttackCallType.Automatical)
                    Attack();
            }
        }

        /// <summary>
        /// Coward brain handler: always retreats on seeing target.
        /// </summary>
        protected void HandleCoward(float distance, float mid)
        {
            Retreat(15f);
            escaping = true;
        }

        /// <summary>
        /// Retreat in the opposite direction of the target.
        /// </summary>
        protected void Retreat(float amount)
        {
            Vector3 dir = (transform.position - fov.CurrentTarget.position).normalized;
            dir.y = 0f;

            stepBack = transform.position + dir * amount;
            MoveTo(stepBack, runSpeed, 0f);
        }

        #endregion

        #region === Shared Behaviors ===

        /// <summary>
        /// Simple random patrol within a radius.
        /// </summary>
        protected void Patrol(float radius, Vector2 waitTime, float speed)
        {
            agent.angularSpeed = defaultAngularSpeed;

            if (!AgentIsMoving)
            {
                standingTimer -= Time.deltaTime;
                if (standingTimer <= 0f)
                {
                    Vector3 randomPoint = GetRandomNavPosition(transform.position, radius);
                    if (IsPathReachable(randomPoint))
                    {
                        MoveTo(randomPoint, speed);
                        standingTimer = Random.Range(waitTime.x, waitTime.y);
                    }
                }
            }
        }

        /// <summary>
        /// Patrol using a predefined path (PatrolPath component).
        /// </summary>
        protected void PatrolPath()
        {
            agent.angularSpeed = defaultAngularSpeed;

            if (!AgentIsMoving && IsGrounded)
            {
                standingTimer -= Time.deltaTime;
                if (standingTimer <= 0f)
                {
                    Vector3 nextPoint = patrolPath.GetNextPoint(ref patrolIndex, ref reversedPath);
                    if (IsPathReachable(nextPoint))
                    {
                        MoveTo(nextPoint, walkSpeed);
                        standingTimer = Random.Range(standingTime.x, standingTime.y);
                    }
                }
            }
        }

        /// <summary>
        /// Move the agent to a target position with a given speed and stopping distance.
        /// </summary>
        public void MoveTo(Vector3 position, float speed = 1f, float stoppingDistance = 0f)
        {
            agent.speed = speed;
            currentSpeed = speed;

            agent.stoppingDistance = stoppingDistance;
            agent.SetDestination(position);
            storedMovePosition = position;
        }

        /// <summary>
        /// Smoothly rotate to face a target position (ignores vertical difference).
        /// </summary>
        protected void LookAtTarget(Vector3 targetPosition)
        {
            agent.angularSpeed = 0;

            Vector3 direction = targetPosition - transform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    Time.deltaTime * 5f
                );
            }
        }

        #endregion
    }
}
