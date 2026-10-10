using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

namespace Ultrabolt.BrainsAI
{
    /// <summary>
    /// Field Of View (FOV) detection system for AI.
    /// - Detects targets within a radius and angle.
    /// - Tracks current visible target.
    /// - Switches between states: None, Alerted, Investigate.
    /// - Investigation happens when the target is lost (or manually triggered).
    /// </summary>
    public class FOV : MonoBehaviour
    {
        /// <summary>
        /// AI perception state:
        /// None        → No target detected.
        /// Alerted     → Target is currently visible.
        /// Investigate → Target lost or triggered, AI checks last known location.
        /// </summary>
        public enum Status { None, Alerted, Investigate }
        [Tooltip("Read Only: Current perception status of this AI\n{ None, Alerted, Investigate }.")]
        public Status status;

        // Investigate Settings.
        [Tooltip("Allow this AI to investigate after losing sight of a target.")]
        public bool canInvestigate = true;
        [Tooltip("How long to stay in Investigate state before returning to None (if unresolved).")]
        public float investigateDuration = 3f;

        // FOV Settings.
        [Tooltip("Show gizmos in the Scene view for debugging.")]
        public bool drawGizmos = true;
        [Tooltip("Field of view angle in degrees.")]
        [Range(0, 360)]
        public float viewAngle = 150f;
        [Tooltip("Detection radius for targets.")]
        public float viewRadius = 20f;
        [Tooltip("Scale the view radius and angle from behind.")]
        public float awarenessScale = 1f;
        [Tooltip("Offset from the object's origin when calculating FOV.")]
        public Vector3 fovOffset;

        // Layers.
        [Tooltip("Which layers are considered valid targets.")]
        public LayerMask targetMask;
        [Tooltip("Which layers block the line of sight.")]
        public LayerMask obstacleMask = 1 << 0;

        // Events.
        [HideInInspector] public bool showEvents; // Editor only
        [Tooltip("Called when a target is first detected.")]
        public UnityEvent onDetectTarget;
        [Tooltip("Called when a target is lost.")]
        public UnityEvent onLostTarget;
        [Tooltip("Called when entering None state.")]
        public UnityEvent onEnterNone;
        [Tooltip("Called when entering Alerted state.")]
        public UnityEvent onEnterAlerted;
        [Tooltip("Called when entering Investigate state.")]
        public UnityEvent onEnterInvestigate;

        // Read only info for debugging or external logic
        [HideInInspector] public List<Transform> visibleTargets = new();
        [HideInInspector] public Transform previousTarget;

        public Transform CurrentTarget { get; private set; }
        [Tooltip("Let an integration supply targets instead of using the demo perception/input.")]
        public bool useExternalDetection;
        public Vector3 lastKnownPosition { get; private set; }

        // Internal values
        private float delayBetweenChecks = 0.2f;
        private Vector3 Position => transform.position + fovOffset;

        private float investigateTimer = 0f;
        [HideInInspector] public bool suspectLocation; // true while AI is moving toward a suspicious location
        private float ViewRadiusBack => -viewRadius * (awarenessScale / 10);
        private float ViewAngleBack => 360 - viewAngle;

        /// <summary>
        /// Resets the last known position to the current position.
        /// </summary>
        public void ResetLastKnownPosition() => lastKnownPosition = Position;

        private void Start()
        {
            // Ensure initial state is None
            SetStatus(Status.None);
        }

        private void Update()
        {
            if (useExternalDetection)
            {
                if (suspectLocation) ResolveInvestigation();
                if (status == Status.Investigate && !float.IsInfinity(investigateTimer))
                {
                    investigateTimer -= Time.deltaTime;
                    if (investigateTimer <= 0f) SetStatus(Status.None);
                }
                return;
            }
            // Example test trigger (press W to simulate suspicious event)
            if (Input.GetKeyDown(KeyCode.W))
                SetInvestigate(new Vector3(20, 0, 20));

            if (suspectLocation)
                ResolveInvestigation();

            // Check vision on a timed interval
            delayBetweenChecks = Mathf.MoveTowards(delayBetweenChecks, 0, Time.deltaTime);
            if (delayBetweenChecks <= 0)
            {
                FindVisibleTargets();
                delayBetweenChecks = 0.2f;
            }

            // Investigate countdown (only if using time-based investigation)
            if (status == Status.Investigate && !float.IsInfinity(investigateTimer))
            {
                investigateTimer -= Time.deltaTime;
                if (investigateTimer <= 0f)
                    SetStatus(Status.None);
            }
        }

        /// <summary>
        /// Finds all targets inside radius and checks visibility/line of sight.
        /// Handles state transitions (Alerted, Investigate, None).
        /// </summary>
        private void FindVisibleTargets()
        {
            List<Transform> newTargets = new();

            // Find potential targets in radius
            Collider[] targetsInRadius = Physics.OverlapSphere(Position, viewRadius, targetMask);
            foreach (Collider collider in targetsInRadius)
            {
                Transform target = collider.transform;
                if (IsTargetVisible(target, viewAngle, out float dstToTarget))
                {
                    newTargets.Add(target);
                    float currentDistance = CurrentTarget != null ? Vector3.Distance(Position, CurrentTarget.position) : float.MaxValue;
                    if (dstToTarget < currentDistance)
                        CurrentTarget = target;
                }
            }

            if (status == Status.Alerted)
            {
                // Find potential targets in radius
                Collider[] targetsInRadiusBack = Physics.OverlapSphere(Position, ViewRadiusBack, targetMask);
                foreach (Collider collider in targetsInRadiusBack)
                {
                    Transform target = collider.transform;
                    if (IsTargetVisible(target, ViewAngleBack, out float dstToTarget, true))
                    {
                        newTargets.Add(target);
                        float currentDistance = CurrentTarget != null ? Vector3.Distance(Position, CurrentTarget.position) : float.MaxValue;
                        if (dstToTarget < currentDistance)
                            CurrentTarget = target;
                    }
                }
            }

            // If we lost sight of all targets
            if (newTargets.Count == 0 && visibleTargets.Count > 0)
            {
                // Save last known position before clearing target
                if (CurrentTarget != null)
                    lastKnownPosition = CurrentTarget.position;
                else if (visibleTargets.Count > 0)
                    lastKnownPosition = visibleTargets[0].position;
                else
                    lastKnownPosition = Position;

                onLostTarget?.Invoke();
                CurrentTarget = null;

                if (canInvestigate)
                    SetInvestigate(lastKnownPosition);
                else
                    SetStatus(Status.None);
            }
            // If we currently see at least one target
            else if (newTargets.Count > 0)
            {
                if (CurrentTarget != previousTarget)
                    onDetectTarget?.Invoke();

                SetStatus(Status.Alerted);
                investigateTimer = 0f; // cancel investigation
            }

            visibleTargets = newTargets;
            previousTarget = CurrentTarget;
        }

        /// <summary>
        /// Checks if a target is within view angle and not blocked by obstacles.
        /// </summary>
        private bool IsTargetVisible(Transform target, float angle, out float dstToTarget, bool checkBack = false)
        {
            Vector3 origin = Position;
            Vector3 dirToTarget = (target.position - origin).normalized;
            dstToTarget = Vector3.Distance(origin, target.position);

            bool inAngle = angle >= 360f || Vector3.Angle(checkBack ? -transform.forward : transform.forward, dirToTarget) < angle / 2f;
            bool noObstruction = !Physics.Raycast(origin, dirToTarget, dstToTarget, obstacleMask);

            return inAngle && noObstruction;
        }

        /// <summary>
        /// Changes AI state and invokes the appropriate UnityEvents.
        /// </summary>
        private void SetStatus(Status newStatus)
        {
            if (status == newStatus) return;

            status = newStatus;

            switch (status)
            {
                case Status.None:
                    onEnterNone?.Invoke();
                    suspectLocation = false;
                    break;
                case Status.Alerted:
                    onEnterAlerted?.Invoke();
                    suspectLocation = false;
                    break;
                case Status.Investigate:
                    onEnterInvestigate?.Invoke();
                    break;
            }
        }

        /// <summary>Integration seam: perception remains external, behavior remains in Brain.</summary>
        public void SetExternalTarget(Transform target)
        {
            if (!useExternalDetection) return;
            if (target != null)
            {
                bool changed = CurrentTarget != target;
                CurrentTarget = target;
                visibleTargets.Clear();
                visibleTargets.Add(target);
                previousTarget = target;
                lastKnownPosition = target.position;
                investigateTimer = 0f;
                SetStatus(Status.Alerted);
                if (changed) onDetectTarget?.Invoke();
            }
            else if (CurrentTarget != null || visibleTargets.Count > 0)
            {
                if (CurrentTarget != null) lastKnownPosition = CurrentTarget.position;
                CurrentTarget = null;
                previousTarget = null;
                visibleTargets.Clear();
                onLostTarget?.Invoke();
                if (canInvestigate) SetInvestigate(lastKnownPosition);
                else SetStatus(Status.None);
            }
        }

        /// <summary>
        /// Manually force investigation at a specific location.
        /// Investigation lasts until:
        /// - A target is detected again (becomes Alerted).
        /// - Or ResolveInvestigation() confirms the location was checked.
        /// </summary>
        public void SetInvestigate(Vector3 location)
        {
            lastKnownPosition = location;
            CurrentTarget = null;
            investigateTimer = Mathf.Infinity; // stays active until resolved manually
            suspectLocation = true;
            SetStatus(Status.Investigate);
        }

        /// <summary>
        /// Call when the AI reaches close enough to the lastKnownPosition.
        /// </summary>
        public void ResolveInvestigation(float stopDistance = 3f)
        {
            if (status == Status.Investigate)
            {
                float dist = Vector3.Distance(Position, lastKnownPosition);
                if (dist <= stopDistance)
                {
                    suspectLocation = false;
                    investigateTimer = investigateDuration; // start countdown to None
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;

            // Draw FOV cone
            Gizmos.color = CurrentTarget ? Color.red : Color.white;
            CustomGizmos.DrawFieldOfView(transform, fovOffset, viewRadius, true, viewAngle);

            // Draw Awareness cone
            if (CurrentTarget != null || !Application.isPlaying)
            {
                Gizmos.color = Color.black;
                CustomGizmos.DrawFieldOfView(transform, fovOffset, ViewRadiusBack, true, ViewAngleBack);
            }

            // Draw lines to visible targets
            foreach (Transform visibleTarget in visibleTargets)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(Position, visibleTarget.position);
            }

            // Draw investigation marker
            if (status == Status.Investigate)
            {
                Gizmos.color = suspectLocation ? Color.magenta : Color.cyan;
                Gizmos.DrawSphere(lastKnownPosition, 0.3f);
                Gizmos.DrawLine(Position, lastKnownPosition);
            }
        }
    }
}
