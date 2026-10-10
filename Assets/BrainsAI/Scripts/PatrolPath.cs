using UnityEngine;
using UnityEngine.AI;

namespace Ultrabolt.BrainsAI
{
    /// <summary>
    /// Defines a patrol path for AI agents, made up of a series of editable waypoints.
    /// Agents can find the nearest point on the path and move along it either in a loop
    /// or in a back-and-forth pattern.
    /// </summary>
    [AddComponentMenu("Ultrabolt BrainsAI/AI Utilities/Patrol Path")]
    public class PatrolPath : MonoBehaviour
    {
        [Tooltip("List of patrol points relative to this object's position.\nThese points define the agent's patrol route.")]
        public Vector3[] points = new Vector3[0];

        [Tooltip("When enabled, the path loops back to the start after reaching the last point.\nWhen disabled, the path is patrolled back and forth.")]
        [HideInInspector]
        public bool loopPath = true;

        /// <summary>
        /// Finds the nearest reachable point on the patrol path to the given world position.
        /// </summary>
        /// <param name="position">The world position to compare from (e.g., the agent's position).</param>
        /// <param name="index">Returns the index of the nearest point, or -1 if none found.</param>
        /// <returns>The nearest reachable point in world space.</returns>
        public Vector3 GetNearestPoint(Vector3 position, out int index)
        {
            index = -1;
            float shortestDistance = Mathf.Infinity;
            Vector3 bestPoint = transform.position;
            NavMeshPath path = new();

            for (int i = 0; i < points.Length; i++)
            {
                Vector3 worldPoint = transform.position + points[i];

                // Check if a valid path exists to this patrol point
                if (NavMesh.CalculatePath(position, worldPoint, NavMesh.AllAreas, path)
                    && path.status == NavMeshPathStatus.PathComplete)
                {
                    float pathDistance = 0f;

                    // Calculate actual path distance (corner to corner)
                    for (int j = 1; j < path.corners.Length; j++)
                        pathDistance += Vector3.Distance(path.corners[j - 1], path.corners[j]);

                    if (pathDistance < shortestDistance)
                    {
                        shortestDistance = pathDistance;
                        index = i;
                        bestPoint = worldPoint;
                    }
                }
            }

            return bestPoint;
        }

        /// <summary>
        /// Gets the next patrol point based on the current index and movement direction.
        /// Supports looping or back-and-forth patrol modes.
        /// </summary>
        /// <param name="index">Reference to the current patrol point index. This will be updated to the next index.</param>
        /// <param name="reversedPath">Indicates if the agent is currently moving backward on the path (non-loop mode).</param>
        /// <returns>The next patrol point in world space.</returns>
        public Vector3 GetNextPoint(ref int index, ref bool reversedPath)
        {
            Vector3 nextPoint;

            if (loopPath)
            {
                index = (index + 1) % points.Length;
                nextPoint = points[index];
            }
            else
            {
                index += reversedPath ? -1 : 1;

                if (index < 0)
                {
                    index = 1;
                    reversedPath = false;
                }
                else if (index > points.Length - 1)
                {
                    index = points.Length - 2;
                    reversedPath = true;
                }

                nextPoint = points[index];
            }

            return nextPoint + transform.position;
        }

        private void OnDrawGizmos()
        {
            if (points == null || points.Length == 0) return;

            Vector3 origin = transform.position;

            // Draw lines between patrol points
            Gizmos.color = Color.white;
            for (int i = 0; i < points.Length; i++)
            {
                Vector3 current = points[i] + origin;

                // Draw sphere for each patrol point
                Gizmos.color = Color.gray;
                Gizmos.DrawSphere(current, 0.1f);

                if (i + 1 <= points.Length - 1)
                {
                    Vector3 next = points[i + 1] + origin;
                    Gizmos.color = Color.white;
                    Gizmos.DrawLine(current, next);
                }
            }

            // If looping, draw line back to first point
            if (loopPath && points.Length > 1)
                Gizmos.DrawLine(points[0] + origin, points[points.Length - 1] + origin);
        }
    }
}
