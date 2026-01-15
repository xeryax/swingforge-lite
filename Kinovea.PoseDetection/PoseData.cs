using System;
using System.Collections.Generic;

namespace Kinovea.PoseDetection
{
    /// <summary>
    /// Represents pose data for a single video frame.
    /// Contains 17 keypoints (COCO format) and calculated angles.
    /// </summary>
    public class PoseData
    {
        /// <summary>
        /// Frame number this pose corresponds to.
        /// </summary>
        public long FrameNumber { get; set; }

        /// <summary>
        /// Timestamp in milliseconds.
        /// </summary>
        public double TimestampMs { get; set; }

        /// <summary>
        /// The 17 detected keypoints.
        /// </summary>
        public KeyPoint[] KeyPoints { get; set; }

        /// <summary>
        /// Overall detection confidence.
        /// </summary>
        public float DetectionConfidence { get; set; }

        /// <summary>
        /// Bounding box [x, y, width, height] normalized 0-1.
        /// </summary>
        public float[] BoundingBox { get; set; }

        // Calculated angles (in degrees)
        public float? ShoulderAngle { get; set; }
        public float? WaistAngle { get; set; }
        public float? SpineAngle { get; set; }
        public float? LeftElbowAngle { get; set; }
        public float? RightElbowAngle { get; set; }
        public float? LeftKneeAngle { get; set; }
        public float? RightKneeAngle { get; set; }

        public PoseData()
        {
            KeyPoints = new KeyPoint[17];
            for (int i = 0; i < 17; i++)
            {
                KeyPoints[i] = new KeyPoint(i, 0, 0, 0);
            }
        }

        /// <summary>
        /// Gets a keypoint by index.
        /// </summary>
        public KeyPoint GetKeyPoint(int index)
        {
            if (index < 0 || index >= KeyPoints.Length)
                return null;
            return KeyPoints[index];
        }

        /// <summary>
        /// Gets the left shoulder keypoint.
        /// </summary>
        public KeyPoint LeftShoulder => GetKeyPoint(5);

        /// <summary>
        /// Gets the right shoulder keypoint.
        /// </summary>
        public KeyPoint RightShoulder => GetKeyPoint(6);

        /// <summary>
        /// Gets the left hip keypoint.
        /// </summary>
        public KeyPoint LeftHip => GetKeyPoint(11);

        /// <summary>
        /// Gets the right hip keypoint.
        /// </summary>
        public KeyPoint RightHip => GetKeyPoint(12);

        /// <summary>
        /// Check if the pose has valid keypoints for angle calculation.
        /// </summary>
        public bool HasValidPose(float threshold = 0.25f)
        {
            // Need at least shoulders and hips for basic pose
            return LeftShoulder.IsValid(threshold) && RightShoulder.IsValid(threshold) &&
                   LeftHip.IsValid(threshold) && RightHip.IsValid(threshold);
        }
    }
}
