using UnityEngine;
using UnityEngine.AI;
using System.Collections;

namespace Ultrabolt.BrainsAI
{
    /// <summary>
    /// Handles brain registration, alarm broadcasting to nearby agents,
    /// and the reaction behavior of other agents upon hearing the alarm.
    /// </summary>
    public partial class Brain : MonoBehaviour
    {
        #region === Unity Lifecycle ===

        private void OnEnable()
        {
            // Register this brain in the global list
            brains.Add(this);
        }

        private void OnDisable()
        {
            // Stop any ongoing alarm reaction
            StopAllCoroutines();

            // Unregister this brain safely
            if (brains.Contains(this))
                brains.Remove(this);
        }

        #endregion

        #region === Alarm System ===

        /// <summary>
        /// Broadcasts an alarm from this agent's position to all nearby agents
        /// in the same alarm groups.
        /// </summary>
        public void CallAgents() => CallAgents(transform.position);

        /// <summary>
        /// Broadcasts an alarm from a specific location to all agents that
        /// belong to a group included in this brain's <see cref="alarmGroups"/>.
        /// Agents within the <see cref="alarmRadius"/> will react by investigating the location.
        /// </summary>
        /// <param name="location">The world position of the alarm source.</param>
        public void CallAgents(Vector3 location)
        {
            foreach (var brain in Brains)
            {
                // Skip self
                if (brain == this)
                    continue;

                // Check if this brain should react based on group membership
                bool canHear = false;
                foreach (var group in alarmGroups)
                {
                    if (brain.groupName == group)
                    {
                        canHear = true;
                        break;
                    }
                }

                if (!canHear)
                    continue;

                // Check if the other brain is within the alarm radius
                float distance = Vector3.Distance(location, brain.transform.position);
                if (distance <= alarmRadius)
                {
                    // Start the reaction coroutine on the receiving brain
                    brain.alarmRoutine = StartCoroutine(brain.OnAlarmHeard(location));
                }
            }
        }

        /// <summary>
        /// Called on other agents when they hear an alarm within their range.
        /// Makes the agent investigate the alarm location after a short randomized delay.
        /// </summary>
        /// <param name="location">The location of the alarm sound.</param>
        public IEnumerator OnAlarmHeard(Vector3 location)
        {
            // Project the location onto the NavMesh to ensure it's reachable
            if (NavMesh.SamplePosition(location, out NavMeshHit hit, 5f, NavMesh.AllAreas) && IsPathReachable(hit.position))
            {
                // Optional: Play alert animation or reaction here, it'll not move yet will 'waitTime'.
                // And also it'll not move if the animation's tag is not 'Moveable' so don't worry if your animation quite long

                agent.ResetPath();

                // Set the FOV system to "investigate" mode at the alarm location
                fov.SetInvestigate(hit.position);

                // Add a slight delay to avoid robotic synchronized movement
                float waitTime = Random.Range(1f, 2f);
                yield return new WaitForSeconds(waitTime);

                // Move toward the alarm location
                MoveTo(hit.position, runSpeed, 1f);
            }

            // Clear the alarm routine reference when done
            alarmRoutine = null;
        }

        #endregion
    }
}
