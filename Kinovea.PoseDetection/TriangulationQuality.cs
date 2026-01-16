using System;

namespace Kinovea.PoseDetection
{
    /// <summary>
    /// Quality metrics for a triangulated 3D pose.
    /// </summary>
    public class TriangulationQuality
    {
        /// <summary>
        /// Number of valid keypoints in the pose.
        /// </summary>
        public int ValidKeypointCount { get; set; }

        /// <summary>
        /// Average confidence across all valid keypoints.
        /// </summary>
        public float AverageConfidence { get; set; }

        /// <summary>
        /// Minimum X coordinate.
        /// </summary>
        public double MinX { get; set; }

        /// <summary>
        /// Maximum X coordinate.
        /// </summary>
        public double MaxX { get; set; }

        /// <summary>
        /// Minimum Y coordinate.
        /// </summary>
        public double MinY { get; set; }

        /// <summary>
        /// Maximum Y coordinate.
        /// </summary>
        public double MaxY { get; set; }

        /// <summary>
        /// Minimum Z coordinate.
        /// </summary>
        public double MinZ { get; set; }

        /// <summary>
        /// Maximum Z coordinate.
        /// </summary>
        public double MaxZ { get; set; }

        /// <summary>
        /// Get a formatted string of the coordinate ranges.
        /// </summary>
        public string GetCoordinateRange()
        {
            if (double.IsNaN(MinX) || double.IsNaN(MaxX))
                return "X: [NaN, NaN], Y: [NaN, NaN], Z: [NaN, NaN]";

            return $"X: [{MinX:F4}, {MaxX:F4}], Y: [{MinY:F4}, {MaxY:F4}], Z: [{MinZ:F4}, {MaxZ:F4}]";
        }
    }
}
