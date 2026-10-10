#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

namespace Ultrabolt.BrainsAI
{
    /// <summary>
    /// TacticalSpots is a helper component responsible for generating and categorizing
    /// positions in the environment into "Safe" and "Danger" spots relative to a target.
    ///
    /// - Safe spots are fully hidden from the target by obstacles.
    /// - Danger spots are visible (partially or fully) to the target.
    ///
    /// These points are generated on a 3D grid around the TacticalSpots object and checked
    /// against the NavMesh and obstacle geometry. Other brains (like BrainTactical) use
    /// this data to retreat, peek, or engage strategically.
    /// 
    /// Editor gizmos visualize these points for debugging and tuning.
    /// </summary>
    public class TacticalSpots : MonoBehaviour
    {
        #region Inspector Settings

        // Gizmo Visualization
        public bool drawGizmos;
        [Tooltip("Only draw spot gizmos when the Scene view camera is close.\nImproves performance during editing.")]
        public bool useDrawingRange;

        // Target Settings
        public Transform target;
        [Tooltip("Layers considered as obstacles when checking for cover.")]
        public LayerMask obstacleMask;

        // Grid Settings
        [Tooltip("Size of the square grid used to generate spots.")]
        public float gridSize = 50f;
        [Range(1, 100), Tooltip("Number of horizontal grid steps along X and Z axes.")]
        public int horizontalResolution = 50;
        [Tooltip("Vertical height range used to generate multi-level spots.")]
        public float verticalRange = 5f;
        [Range(1, 10), Tooltip("Number of vertical grid steps.")]
        public int verticalResolution = 5;

        // Agent Settings
        [Tooltip("Approximate height of the agent used for visibility checks.")]
        public float agentHeight = 1.8f;
        [Tooltip("Small buffer to shift ray origins to avoid precision issues.")]
        public float shadowBuffer = 0.25f;

        [HideInInspector] public bool pauseCalculations;

        #endregion

        #region Private Data

        private readonly List<Vector3> safeSpots = new();
        private readonly List<Vector3> dangerSpots = new();

        private float Spacing => gridSize / horizontalResolution;

        #endregion

        #region Gizmos (Editor Only)

        private void OnDrawGizmos()
        {
#if UNITY_EDITOR
            if (!drawGizmos || target == null) return;
            if (SceneView.lastActiveSceneView == null) return;

            GenerateSpots(target.position);

            Camera sceneCam = SceneView.lastActiveSceneView.camera;
            Vector3 camPos = sceneCam.transform.position;

            // Draw danger spots in red
            Gizmos.color = Color.red;
            foreach (Vector3 spot in dangerSpots)
            {
                if (useDrawingRange && Vector3.Distance(camPos, spot) > 50f)
                    continue;

                Gizmos.DrawSphere(spot, 0.1f);
            }

            // Draw safe spots in green
            Gizmos.color = Color.green;
            foreach (Vector3 spot in safeSpots)
            {
                if (useDrawingRange && Vector3.Distance(camPos, spot) > 50f)
                    continue;

                Gizmos.DrawSphere(spot, 0.1f);
            }
#endif
        }

        #endregion

        #region Spot Generation

        /// <summary>
        /// Generates both safe and danger spots on a 3D grid centered on this object.
        /// Spots are classified based on visibility from the target position.
        /// </summary>
        private void GenerateSpots(Vector3 targetPosition)
        {
            if (pauseCalculations) return;

            safeSpots.Clear();
            dangerSpots.Clear();

            // Bottom-left-back corner of the grid
            Vector3 gridOrigin = transform.position
                               - Vector3.right * (gridSize / 2f)
                               - Vector3.forward * (gridSize / 2f);

            float verticalSpacing = verticalRange / verticalResolution;

            for (int x = 0; x < horizontalResolution; x++)
            {
                for (int z = 0; z < horizontalResolution; z++)
                {
                    for (int y = 0; y < verticalResolution; y++)
                    {
                        Vector3 candidatePos = gridOrigin + new Vector3(x * Spacing, y * verticalSpacing, z * Spacing);

                        // Check if point is on NavMesh
                        if (NavMesh.SamplePosition(candidatePos, out var hit, 1f, NavMesh.AllAreas))
                        {
                            bool fullyCovered =
                                SafeSpot(hit.position + Vector3.forward * shadowBuffer, targetPosition) &&
                                SafeSpot(hit.position + Vector3.right * shadowBuffer, targetPosition) &&
                                SafeSpot(hit.position + Vector3.back * shadowBuffer, targetPosition) &&
                                SafeSpot(hit.position + Vector3.left * shadowBuffer, targetPosition);

                            if (fullyCovered)
                                safeSpots.Add(hit.position);
                            else
                                dangerSpots.Add(hit.position);
                        }
                    }
                }
            }
        }

        #endregion

        #region Spot Queries

        /// <summary>
        /// Finds the nearest safe spot that the agent can reach within the given radius.
        /// </summary>
        public Vector3 NearestSafeSpot(Vector3 position, Vector3 targetPosition, float radius)
        {
            GenerateSpots(targetPosition);

            Vector3 closestPoint = Vector3.zero;
            float closestDist = float.MaxValue;

            foreach (Vector3 point in safeSpots)
            {
                float dist = Vector3.Distance(position, point);
                if (dist > radius || dist >= closestDist)
                    continue;

                if (IsReachable(position, point))
                {
                    closestDist = dist;
                    closestPoint = point;
                }
            }

            return closestPoint;
        }

        /// <summary>
        /// Finds the nearest danger spot that is visible to the target 
        /// and reachable by the agent within the given radius.
        /// </summary>
        public Vector3 NearestVisibleSpot(Vector3 position, Vector3 targetPosition, float radius)
        {
            GenerateSpots(targetPosition);

            Vector3 closestPoint = Vector3.zero;
            float closestDist = float.MaxValue;

            foreach (Vector3 point in dangerSpots)
            {
                if (!IsDirectlyVisible(point, targetPosition))
                    continue;

                float dist = Vector3.Distance(position, point);
                if (dist > radius || dist >= closestDist)
                    continue;

                if (IsReachable(position, point))
                {
                    closestDist = dist;
                    closestPoint = point;
                }
            }

            return closestPoint;
        }

        /// <summary>
        /// Checks if there is at least one reachable safe spot within the given radius.
        /// Used by tactical AI to decide if it can retreat to cover.
        /// </summary>
        public bool IsThereSafeSpotNearby(Vector3 position, Vector3 targetPosition, float radius)
        {
            GenerateSpots(targetPosition);

            foreach (Vector3 point in safeSpots)
            {
                if (Vector3.Distance(position, point) > radius)
                    continue;

                if (IsReachable(position, point))
                    return true;
            }

            return false;
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Checks whether the agent can reach the target point using the NavMesh.
        /// </summary>
        private bool IsReachable(Vector3 from, Vector3 to)
        {
            if (!NavMesh.SamplePosition(to, out var hit, 1f, NavMesh.AllAreas))
                return false;

            NavMeshPath path = new NavMeshPath();
            return NavMesh.CalculatePath(from, hit.position, NavMesh.AllAreas, path) &&
                   path.status == NavMeshPathStatus.PathComplete;
        }

        /// <summary>
        /// Checks if there is a direct line of sight between a point and a position,
        /// considering agent height and obstacles.
        /// </summary>
        private bool IsDirectlyVisible(Vector3 point, Vector3 fromPosition)
        {
            Vector3 origin = fromPosition + Vector3.up * agentHeight;
            Vector3 destination = point + Vector3.up * agentHeight;

            Vector3 direction = destination - origin;
            float distance = direction.magnitude;

            return !Physics.Raycast(origin, direction.normalized, distance, obstacleMask);
        }

        /// <summary>
        /// Determines whether the given position qualifies as a "safe spot" relative to a target,
        /// meaning it is fully hidden (both at ground and agent height) from the target's view.
        /// </summary>
        public bool SafeSpot(Vector3 position, Vector3 targetPosition)
        {
            // Ground-level ray
            Vector3 dir = (position - targetPosition).normalized;
            Vector3 origin = targetPosition - dir * shadowBuffer;
            float dist = Vector3.Distance(origin, position);
            bool groundBlocked = Physics.Raycast(origin, dir, out _, dist, obstacleMask);

            // Elevated ray (agent eye level)
            Vector3 elevatedPos = position + Vector3.up * agentHeight;
            dir = (elevatedPos - targetPosition).normalized;
            origin = targetPosition - dir * shadowBuffer;
            dist = Vector3.Distance(origin, elevatedPos);
            bool elevatedBlocked = Physics.Raycast(origin, dir, out _, dist, obstacleMask);

            return groundBlocked && elevatedBlocked;
        }

        #endregion
    }
}
