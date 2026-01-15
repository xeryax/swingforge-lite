using System;
using System.Collections.Generic;

namespace Kinovea.PoseDetection
{
    /// <summary>
    /// Tracks pose angle statistics across a video including current, min/max, impact, and overall values.
    /// All angles are relative to feet baseline (0 = perfectly aligned with feet).
    /// </summary>
    public class PoseStatistics
    {
        // Current angles (relative to feet)
        public float? CurrentShoulderAngle { get; set; }
        public float? CurrentHipAngle { get; set; }
        public float? CurrentLeftElbowAngle { get; set; }

        // Min/Max across entire video
        public float? ShoulderMin { get; set; }
        public float? ShoulderMax { get; set; }
        public float? HipMin { get; set; }
        public float? HipMax { get; set; }
        public float? ElbowMin { get; set; }
        public float? ElbowMax { get; set; }

        // At impact frame
        public float? ShoulderAtImpact { get; set; }
        public float? HipAtImpact { get; set; }
        public float? ElbowAtImpact { get; set; }
        public long ImpactFrame { get; set; } = -1;

        // Weighted confidence overall
        public float? OverallShoulderAngle { get; set; }
        public float? OverallHipAngle { get; set; }
        public float? OverallElbowAngle { get; set; }

        // For calculating weighted averages
        private float shoulderSum, shoulderWeightSum;
        private float hipSum, hipWeightSum;
        private float elbowSum, elbowWeightSum;

        /// <summary>
        /// Reset all statistics.
        /// </summary>
        public void Reset()
        {
            CurrentShoulderAngle = CurrentHipAngle = CurrentLeftElbowAngle = null;
            ShoulderMin = ShoulderMax = null;
            HipMin = HipMax = null;
            ElbowMin = ElbowMax = null;
            ShoulderAtImpact = HipAtImpact = ElbowAtImpact = null;
            ImpactFrame = -1;
            OverallShoulderAngle = OverallHipAngle = OverallElbowAngle = null;
            shoulderSum = shoulderWeightSum = 0;
            hipSum = hipWeightSum = 0;
            elbowSum = elbowWeightSum = 0;
        }

        /// <summary>
        /// Update statistics from a pose frame.
        /// </summary>
        public void UpdateFromPose(PoseData pose, float threshold = 0.25f)
        {
            if (pose == null)
                return;

            // Calculate relative angles
            var shoulder = AngleCalculator.CalculateRelativeShoulderAngle(pose, threshold);
            var hip = AngleCalculator.CalculateRelativeHipAngle(pose, threshold);
            var elbow = AngleCalculator.CalculateLeftElbowAngle(pose, threshold);

            // Update current
            CurrentShoulderAngle = shoulder;
            CurrentHipAngle = hip;
            CurrentLeftElbowAngle = elbow;

            // Get confidence for weighting
            float confidence = pose.DetectionConfidence > 0 ? pose.DetectionConfidence : 0.5f;

            // Update min/max for shoulder
            if (shoulder.HasValue)
            {
                if (!ShoulderMin.HasValue || shoulder.Value < ShoulderMin.Value)
                    ShoulderMin = shoulder.Value;
                if (!ShoulderMax.HasValue || shoulder.Value > ShoulderMax.Value)
                    ShoulderMax = shoulder.Value;

                shoulderSum += shoulder.Value * confidence;
                shoulderWeightSum += confidence;
            }

            // Update min/max for hip
            if (hip.HasValue)
            {
                if (!HipMin.HasValue || hip.Value < HipMin.Value)
                    HipMin = hip.Value;
                if (!HipMax.HasValue || hip.Value > HipMax.Value)
                    HipMax = hip.Value;

                hipSum += hip.Value * confidence;
                hipWeightSum += confidence;
            }

            // Update min/max for elbow
            if (elbow.HasValue)
            {
                if (!ElbowMin.HasValue || elbow.Value < ElbowMin.Value)
                    ElbowMin = elbow.Value;
                if (!ElbowMax.HasValue || elbow.Value > ElbowMax.Value)
                    ElbowMax = elbow.Value;

                elbowSum += elbow.Value * confidence;
                elbowWeightSum += confidence;
            }
        }

        /// <summary>
        /// Set impact frame and record angles at that frame.
        /// </summary>
        public void SetImpact(PoseData pose, long frameNumber, float threshold = 0.25f)
        {
            if (pose == null)
                return;

            ImpactFrame = frameNumber;
            ShoulderAtImpact = AngleCalculator.CalculateRelativeShoulderAngle(pose, threshold);
            HipAtImpact = AngleCalculator.CalculateRelativeHipAngle(pose, threshold);
            ElbowAtImpact = AngleCalculator.CalculateLeftElbowAngle(pose, threshold);
        }

        /// <summary>
        /// Finalize overall weighted averages after processing all frames.
        /// </summary>
        public void FinalizeOverall()
        {
            if (shoulderWeightSum > 0)
                OverallShoulderAngle = shoulderSum / shoulderWeightSum;
            if (hipWeightSum > 0)
                OverallHipAngle = hipSum / hipWeightSum;
            if (elbowWeightSum > 0)
                OverallElbowAngle = elbowSum / elbowWeightSum;
        }

        /// <summary>
        /// Calculate statistics from a list of pose data including impact detection.
        /// </summary>
        public static PoseStatistics CalculateFromPoses(List<PoseData> poses, float threshold = 0.25f)
        {
            var stats = new PoseStatistics();
            if (poses == null || poses.Count == 0)
                return stats;

            // Detect impact frame via wrist velocity peak
            long impactFrame = DetectImpactFrame(poses, threshold);

            // Process all poses
            foreach (var pose in poses)
            {
                stats.UpdateFromPose(pose, threshold);

                // Check if this is the impact frame
                if (pose.FrameNumber == impactFrame)
                {
                    stats.SetImpact(pose, impactFrame, threshold);
                }
            }

            stats.FinalizeOverall();
            return stats;
        }

        /// <summary>
        /// Detect impact frame by finding peak wrist velocity.
        /// </summary>
        private static long DetectImpactFrame(List<PoseData> poses, float threshold = 0.25f)
        {
            if (poses == null || poses.Count < 2)
                return poses?.Count > 0 ? poses[0].FrameNumber : -1;

            // Sort by frame number
            var sorted = new List<PoseData>(poses);
            sorted.Sort((a, b) => a.FrameNumber.CompareTo(b.FrameNumber));

            float maxVelocity = 0;
            long impactFrame = sorted[0].FrameNumber;

            for (int i = 1; i < sorted.Count; i++)
            {
                var prev = sorted[i - 1];
                var curr = sorted[i];

                // Get left and right wrist positions
                var prevLWrist = prev.GetKeyPoint(9);
                var prevRWrist = prev.GetKeyPoint(10);
                var currLWrist = curr.GetKeyPoint(9);
                var currRWrist = curr.GetKeyPoint(10);

                // Calculate velocity for both wrists
                float lVel = 0, rVel = 0;

                if (prevLWrist.IsValid(threshold) && currLWrist.IsValid(threshold))
                {
                    float dx = currLWrist.X - prevLWrist.X;
                    float dy = currLWrist.Y - prevLWrist.Y;
                    lVel = (float)Math.Sqrt(dx * dx + dy * dy);
                }

                if (prevRWrist.IsValid(threshold) && currRWrist.IsValid(threshold))
                {
                    float dx = currRWrist.X - prevRWrist.X;
                    float dy = currRWrist.Y - prevRWrist.Y;
                    rVel = (float)Math.Sqrt(dx * dx + dy * dy);
                }

                // Use max of both wrists
                float velocity = Math.Max(lVel, rVel);

                if (velocity > maxVelocity)
                {
                    maxVelocity = velocity;
                    impactFrame = curr.FrameNumber;
                }
            }

            return impactFrame;
        }
    }
}
