using UnityEngine;

namespace Ultrabolt.BrainsAI
{
    /// <summary>
    /// A collection of helper methods to draw common gizmo shapes for AI visualization,
    /// such as field of view cones, min-max detection ranges, and circles.
    /// </summary>
    public static class CustomGizmos
    {
        /// <summary>
        /// Draws a 3D visualization of a min-max detection or influence range using Gizmos.  
        /// This creates a vertical cylindrical shape composed of inner and outer rings (min & max),  
        /// connected vertically and radially, giving a clear visualization of areas like AI hearing or vision ranges.
        /// </summary>
        /// <param name="transform">The reference transform for positioning and rotation.</param>
        /// <param name="offsetPosition">An offset applied to the base position of the shape.</param>
        /// <param name="range">X = inner radius, Y = outer radius.</param>
        /// <param name="useInnerLines">If true, draws the inner vertical lines and radial connectors.</param>
        /// <param name="viewAngle">The angular span of the arc in degrees. Default is 360 for a full cylinder.</param>
        /// <param name="height">The total height of the 3D shape, drawn upwards from the pivot.</param>
        /// <param name="inversedDirection">If true, flips the direction of the drawn arc.</param>
        public static void Draw3DMinMaxRange(Transform transform, Vector3 offsetPosition, Vector2 range, bool useInnerLines = true, float viewAngle = 360f, float height = 1f, bool inversedDirection = false)
        {
            Vector3 baseCenter = transform.position + offsetPosition;
            Vector3 topCenter = baseCenter + Vector3.up * height;

            float step = 5f;
            float startAngle = -viewAngle / 2f;
            float endAngle = viewAngle / 2f;

            // Draw bottom and top rings (inner & outer)
            DrawCircle3D(transform, offsetPosition, range.x, 0f);                            // Bottom - inner
            DrawCircle3D(transform, offsetPosition, range.y, 0f);                            // Bottom - outer
            DrawCircle3D(transform, offsetPosition + Vector3.up * height, range.x, 0f);      // Top - inner
            DrawCircle3D(transform, offsetPosition + Vector3.up * height, range.y, 0f);      // Top - outer

            // Draw vertical lines and radial connections
            for (float angle = startAngle; angle <= endAngle; angle += step)
            {
                Vector3 dir = inversedDirection ? -DirFromAngle(transform, angle, false) : DirFromAngle(transform, angle, false);

                Vector3 outerBottom = baseCenter + dir * range.y;
                Vector3 outerTop = topCenter + dir * range.y;

                Vector3 innerBottom = baseCenter + dir * range.x;
                Vector3 innerTop = topCenter + dir * range.x;

                // Outer vertical
                Gizmos.DrawLine(outerBottom, outerTop);

                // Inner vertical (optional)
                if (useInnerLines)
                    Gizmos.DrawLine(innerBottom, innerTop);

                // Radial connectors
                Gizmos.DrawLine(outerTop, innerTop);
                Gizmos.DrawLine(outerBottom, innerBottom);
            }
        }

        /// <summary>
        /// Draws a circular outline in 3D space using Gizmos.
        /// </summary>
        /// <param name="transform">The reference transform for orientation.</param>
        /// <param name="offsetPosition">Offset applied to the base position.</param>
        /// <param name="radius">The radius of the circle.</param>
        /// <param name="verticalOffset">Additional vertical offset applied on top of the base position.</param>
        private static void DrawCircle3D(Transform transform, Vector3 offsetPosition, float radius, float verticalOffset)
        {
            Vector3 center = transform.position + offsetPosition + Vector3.up * verticalOffset;
            float step = 5f;
            float startAngle = -360f / 2f;
            float endAngle = 360f / 2f;

            Vector3 previousPoint = center + DirFromAngle(transform, startAngle, false) * radius;

            for (float angle = startAngle + step; angle <= endAngle; angle += step)
            {
                Vector3 currentPoint = center + DirFromAngle(transform, angle, false) * radius;
                Gizmos.DrawLine(previousPoint, currentPoint);
                previousPoint = currentPoint;
            }
        }

        /// <summary>
        /// Draws a field-of-view shape using Gizmos (a cone or arc).
        /// </summary>
        /// <param name="transform">The reference transform for orientation.</param>
        /// <param name="offsetPosition">Offset from the transform position.</param>
        /// <param name="viewRadius">The radius of the view arc.</param>
        /// <param name="useInnerLines">Whether to draw lines from the center to each step.</param>
        /// <param name="viewAngle">The total view angle (default 360°).</param>
        /// <param name="inversedDirection">If true, the shape is flipped (useful for back-facing vision).</param>
        public static void DrawFieldOfView(Transform transform, Vector3 offsetPosition, float viewRadius, bool useInnerLines = true, float viewAngle = 360f, bool inversedDirection = false)
        {
            Vector3 center = transform.position + offsetPosition;
            float step = 3f; // Angle step for smoothness

            float startAngle = -viewAngle / 2f;
            float endAngle = viewAngle / 2f;

            // First edge
            Vector3 firstDir = inversedDirection ? -DirFromAngle(transform, startAngle, false)
                : DirFromAngle(transform, startAngle, false);

            Vector3 previousPoint = center + firstDir * viewRadius;
            Gizmos.DrawLine(center, previousPoint);

            // Arc points
            for (float angle = startAngle + step; angle <= endAngle; angle += step)
            {
                Vector3 dir = inversedDirection ? -DirFromAngle(transform, angle, false)
                    : DirFromAngle(transform, angle, false);

                Vector3 currentPoint = center + dir * viewRadius;

                Gizmos.DrawLine(previousPoint, currentPoint);
                if (useInnerLines)
                    Gizmos.DrawLine(center, currentPoint);

                previousPoint = currentPoint;
            }
        }

        /// <summary>
        /// Draws a two-layer ring representing a min and max range (e.g., detection zone),
        /// optionally with inner connecting lines.
        /// </summary>
        /// <param name="transform">Reference transform for orientation.</param>
        /// <param name="offsetPosition">Offset from transform position.</param>
        /// <param name="range">X = inner radius, Y = outer radius.</param>
        /// <param name="useInnerLines">Whether to draw connecting lines between rings.</param>
        /// <param name="viewAngle">The angle coverage (default 360°).</param>
        /// <param name="inversedDirection">If true, the shape is flipped.</param>
        public static void DrawMinMaxRange(Transform transform, Vector3 offsetPosition, Vector2 range, bool useInnerLines = true, float viewAngle = 360f, bool inversedDirection = false)
        {
            Vector3 center = transform.position + offsetPosition;
            float step = 3f;

            float startAngle = -viewAngle / 2f;
            float endAngle = viewAngle / 2f;

            // Draw inner circle first if not inversed
            if (!inversedDirection)
                DrawCircle(transform, offsetPosition, range.x);

            // First direction
            Vector3 firstDir = inversedDirection ? -DirFromAngle(transform, startAngle, false)
                : DirFromAngle(transform, startAngle, false);

            Vector3 prevOuterPoint = center + firstDir * range.y;
            Vector3 prevInnerPoint = center + firstDir * range.x;

            // Connect inner and outer at the start
            Gizmos.DrawLine(useInnerLines ? prevInnerPoint : center, prevOuterPoint);

            // Arc loop
            for (float angle = startAngle + step; angle <= endAngle; angle += step)
            {
                Vector3 dir = inversedDirection ? -DirFromAngle(transform, angle, false)
                    : DirFromAngle(transform, angle, false);

                Vector3 outerPoint = center + dir * range.y;
                Vector3 innerPoint = center + dir * range.x;

                // Outer arc
                Gizmos.DrawLine(prevOuterPoint, outerPoint);

                // Connect inner to outer
                Gizmos.DrawLine(useInnerLines ? innerPoint : center, outerPoint);

                prevOuterPoint = outerPoint;
            }
        }

        /// <summary>
        /// Draws a full circle on the XZ plane at a given position and radius.
        /// </summary>
        public static void DrawCircle(Transform transform, Vector3 offsetPosition, float radius)
        {
            Vector3 center = transform.position + offsetPosition;
            float step = 3f;

            float startAngle = -180f;
            float endAngle = 180f;

            Vector3 previousPoint = center + DirFromAngle(transform, startAngle, false) * radius;

            for (float angle = startAngle + step; angle <= endAngle; angle += step)
            {
                Vector3 currentPoint = center + DirFromAngle(transform, angle, false) * radius;
                Gizmos.DrawLine(previousPoint, currentPoint);
                previousPoint = currentPoint;
            }
        }

        /// <summary>
        /// Converts an angle in degrees into a direction vector on the XZ plane.
        /// </summary>
        /// <param name="transform">The reference transform.</param>
        /// <param name="angleInDegrees">The angle to convert.</param>
        /// <param name="angleIsGlobal">If true, uses world space; otherwise uses local rotation.</param>
        public static Vector3 DirFromAngle(Transform transform, float angleInDegrees, bool angleIsGlobal)
        {
            if (!angleIsGlobal)
                angleInDegrees += transform.eulerAngles.y;

            float rad = angleInDegrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
        }
    }
}
