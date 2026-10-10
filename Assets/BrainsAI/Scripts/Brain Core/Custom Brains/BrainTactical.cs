using System.Linq;
using UnityEngine;

namespace Ultrabolt.BrainsAI
{
    /// <summary>
    /// A tactical AI brain that uses hiding and peeking behaviors 
    /// to engage the target strategically. Instead of rushing in, 
    /// the agent will retreat to safe spots, peek periodically to 
    /// check the target’s position, and re-engage intelligently.
    /// 
    /// Ideal for ranged enemies or intelligent soldiers that use cover.
    /// </summary>
    public class BrainTactical : Brain
    {
        #region Variables

        [HideInInspector]
        public bool drawTacticalSettings; // Editor only - used by custom inspector

        [Tooltip("Time to stay hidden before peeking at the target's last known position.")]
        public float hidingTime = 5f;

        [Tooltip("Time to spend peeking at the target's last known position.")]
        public float visibleTime = 5f;

        // Internal tactical states for controlling hiding/peeking behavior
        private enum TacticalSubState { None, Hiding, Peeking }
        private TacticalSubState subState = TacticalSubState.None;

        // Cached references
        private TacticalSpots spotFinder;
        private Transform lastTarget;

        // Tactical memory
        private Vector3 lastTargetPosition;
        private float timer;

        #endregion

        #region Unity Methods

        private void Awake() =>
            spotFinder = FindObjectOfType<TacticalSpots>();

        protected override void Update()
        {
            // Stop behavior if any stop-control condition is active
            if (StopControlConditions.Values.Any(v => v))
                return;

            if (fov.CurrentTarget == null)
                HandleNoTarget();
            else
                HandleWithTarget();

            // Reset sub-state if the agent is no longer in alert mode
            if (fov.status == FOV.Status.None)
                subState = TacticalSubState.None;

            // Handle jumping on off-mesh links
            if (!jumping && agent.isOnOffMeshLink)
                StartCoroutine(JumpAcross());

            UpdateAnimator();
        }

        #endregion

        #region Behavior - No Target

        /// <summary>
        /// Handles tactical behavior when there is no current target.
        /// </summary>
        private void HandleNoTarget()
        {
            // Only handle sub-state transitions when the agent is idle (not moving)
            if (!AgentIsMoving)
            {
                switch (subState)
                {
                    case TacticalSubState.Peeking:
                        // After peeking without finding the target, investigate last known area
                        Investigate();
                        break;

                    case TacticalSubState.Hiding:
                        // Countdown hiding time before peeking again
                        timer -= Time.deltaTime;
                        if (timer <= 0f)
                        {
                            Vector3 peekSpot = spotFinder.NearestVisibleSpot(
                                transform.position,
                                lastTargetPosition,
                                fov.viewRadius / 2f
                            );

                            MoveTo(peekSpot, runSpeed);
                            subState = TacticalSubState.Peeking;
                            timer = visibleTime;
                        }
                        break;

                    case TacticalSubState.None:
                        // Default idle state
                        Idle();
                        break;
                }
            }
            else if (subState == TacticalSubState.None)
            {
                // If moving but not in any tactical sub-state, idle when done
                Idle();
            }
        }

        #endregion

        #region Behavior - With Target

        /// <summary>
        /// Handles tactical behavior when there is a current target.
        /// </summary>
        private void HandleWithTarget()
        {
            // Cache target data
            lastTarget = fov.CurrentTarget;
            spotFinder.target = lastTarget;
            lastTargetPosition = lastTarget.position;

            LookAtTarget(lastTargetPosition);

            float distanceToTarget = Vector3.Distance(transform.position, lastTargetPosition);
            float retreatRadius = fov.viewRadius / 2f;

            // If ranger is too close, retreat immediately
            if (distanceToTarget <= moveRange.x && brainType == BrainType.Ranger)
            {
                Retreat(moveRange.y);
                return;
            }

            switch (subState)
            {
                case TacticalSubState.Peeking:
                    // Countdown visible time before hiding again
                    timer -= Time.deltaTime;
                    if (timer <= 0f)
                    {
                        RetreatToSafeSpot(retreatRadius);
                    }
                    else
                    {
                        float dis = Vector3.Distance(transform.position, lastTarget.position);

                        if (dis <= moveRange.y)
                        {
                            // Peek and attack if within range
                            HandleAttack();
                            agent.ResetPath();
                        }
                        else
                        {
                            // Reposition to last known target position while peeking
                            timer = visibleTime;
                            MoveTo(lastTarget.position, runSpeed);
                        }
                    }
                    break;

                case TacticalSubState.None:
                case TacticalSubState.Hiding:
                    // If hiding or in none state when target spotted, retreat to cover first
                    RetreatToSafeSpot(retreatRadius);
                    break;
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Sends the agent to the nearest safe spot and enters hiding state.
        /// </summary>
        private void RetreatToSafeSpot(float radius)
        {
            if (spotFinder.IsThereSafeSpotNearby(transform.position, fov.CurrentTarget.position, fov.viewRadius / 2f))
            {
                Vector3 safeSpot = spotFinder.NearestSafeSpot(transform.position, lastTargetPosition, radius);

                MoveTo(safeSpot, runSpeed);
                subState = TacticalSubState.Hiding;
                timer = hidingTime;
            }
            else
            {
                // If no safe spot found, fallback to peeking
                subState = TacticalSubState.Peeking;
                timer = visibleTime;
            }
        }

        /// <summary>
        /// Overridden to disable default Alerted behavior for more direct Update control.
        /// </summary>
        protected override void Alerted() { }

        #endregion
    }
}
