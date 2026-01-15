using System;

namespace Kinovea.PoseDetection
{
    /// <summary>
    /// Calculates body angles from pose keypoints for golf swing analysis.
    /// </summary>
    public static class AngleCalculator
    {
        private const float RadToDeg = 180f / (float)Math.PI;

        /// <summary>
        /// Calculate the shoulder line angle (horizontal reference).
        /// Angle between left shoulder (kp5) and right shoulder (kp6).
        /// </summary>
        public static float? CalculateShoulderAngle(PoseData pose, float threshold = 0.25f)
        {
            var leftShoulder = pose.GetKeyPoint(5);
            var rightShoulder = pose.GetKeyPoint(6);

            if (!leftShoulder.IsValid(threshold) || !rightShoulder.IsValid(threshold))
                return null;

            return CalculateLineAngle(leftShoulder.X, leftShoulder.Y, rightShoulder.X, rightShoulder.Y);
        }

        /// <summary>
        /// Calculate the waist/hip line angle (horizontal reference).
        /// Angle between left hip (kp11) and right hip (kp12).
        /// </summary>
        public static float? CalculateWaistAngle(PoseData pose, float threshold = 0.25f)
        {
            var leftHip = pose.GetKeyPoint(11);
            var rightHip = pose.GetKeyPoint(12);

            if (!leftHip.IsValid(threshold) || !rightHip.IsValid(threshold))
                return null;

            return CalculateLineAngle(leftHip.X, leftHip.Y, rightHip.X, rightHip.Y);
        }

        /// <summary>
        /// Calculate the spine angle relative to vertical.
        /// Angle from midpoint of hips to midpoint of shoulders.
        /// </summary>
        public static float? CalculateSpineAngle(PoseData pose, float threshold = 0.25f)
        {
            var leftShoulder = pose.GetKeyPoint(5);
            var rightShoulder = pose.GetKeyPoint(6);
            var leftHip = pose.GetKeyPoint(11);
            var rightHip = pose.GetKeyPoint(12);

            if (!leftShoulder.IsValid(threshold) || !rightShoulder.IsValid(threshold) ||
                !leftHip.IsValid(threshold) || !rightHip.IsValid(threshold))
                return null;

            // Calculate midpoints
            float shoulderMidX = (leftShoulder.X + rightShoulder.X) / 2f;
            float shoulderMidY = (leftShoulder.Y + rightShoulder.Y) / 2f;
            float hipMidX = (leftHip.X + rightHip.X) / 2f;
            float hipMidY = (leftHip.Y + rightHip.Y) / 2f;

            // Calculate angle relative to vertical (negative Y is up)
            float dx = shoulderMidX - hipMidX;
            float dy = shoulderMidY - hipMidY;

            // Angle from vertical (0 = straight up)
            float angle = (float)Math.Atan2(dx, -dy) * RadToDeg;
            return angle;
        }

        /// <summary>
        /// Calculate left elbow angle (shoulder-elbow-wrist).
        /// </summary>
        public static float? CalculateLeftElbowAngle(PoseData pose, float threshold = 0.25f)
        {
            var shoulder = pose.GetKeyPoint(5);  // left shoulder
            var elbow = pose.GetKeyPoint(7);     // left elbow
            var wrist = pose.GetKeyPoint(9);     // left wrist

            if (!shoulder.IsValid(threshold) || !elbow.IsValid(threshold) || !wrist.IsValid(threshold))
                return null;

            return CalculateJointAngle(shoulder.X, shoulder.Y, elbow.X, elbow.Y, wrist.X, wrist.Y);
        }

        /// <summary>
        /// Calculate right elbow angle (shoulder-elbow-wrist).
        /// </summary>
        public static float? CalculateRightElbowAngle(PoseData pose, float threshold = 0.25f)
        {
            var shoulder = pose.GetKeyPoint(6);  // right shoulder
            var elbow = pose.GetKeyPoint(8);     // right elbow
            var wrist = pose.GetKeyPoint(10);    // right wrist

            if (!shoulder.IsValid(threshold) || !elbow.IsValid(threshold) || !wrist.IsValid(threshold))
                return null;

            return CalculateJointAngle(shoulder.X, shoulder.Y, elbow.X, elbow.Y, wrist.X, wrist.Y);
        }

        /// <summary>
        /// Calculate left knee angle (hip-knee-ankle).
        /// </summary>
        public static float? CalculateLeftKneeAngle(PoseData pose, float threshold = 0.25f)
        {
            var hip = pose.GetKeyPoint(11);      // left hip
            var knee = pose.GetKeyPoint(13);     // left knee
            var ankle = pose.GetKeyPoint(15);    // left ankle

            if (!hip.IsValid(threshold) || !knee.IsValid(threshold) || !ankle.IsValid(threshold))
                return null;

            return CalculateJointAngle(hip.X, hip.Y, knee.X, knee.Y, ankle.X, ankle.Y);
        }

        /// <summary>
        /// Calculate right knee angle (hip-knee-ankle).
        /// </summary>
        public static float? CalculateRightKneeAngle(PoseData pose, float threshold = 0.25f)
        {
            var hip = pose.GetKeyPoint(12);      // right hip
            var knee = pose.GetKeyPoint(14);     // right knee
            var ankle = pose.GetKeyPoint(16);    // right ankle

            if (!hip.IsValid(threshold) || !knee.IsValid(threshold) || !ankle.IsValid(threshold))
                return null;

            return CalculateJointAngle(hip.X, hip.Y, knee.X, knee.Y, ankle.X, ankle.Y);
        }

        /// <summary>
        /// Calculate all angles for a pose and update the PoseData object.
        /// </summary>
        public static void CalculateAllAngles(PoseData pose, float threshold = 0.25f)
        {
            pose.ShoulderAngle = CalculateShoulderAngle(pose, threshold);
            pose.WaistAngle = CalculateWaistAngle(pose, threshold);
            pose.SpineAngle = CalculateSpineAngle(pose, threshold);
            pose.LeftElbowAngle = CalculateLeftElbowAngle(pose, threshold);
            pose.RightElbowAngle = CalculateRightElbowAngle(pose, threshold);
            pose.LeftKneeAngle = CalculateLeftKneeAngle(pose, threshold);
            pose.RightKneeAngle = CalculateRightKneeAngle(pose, threshold);
        }

        /// <summary>
        /// Calculate the feet baseline angle (left ankle to right ankle).
        /// Used as reference for relative angle calculations.
        /// </summary>
        public static float? CalculateFeetAngle(PoseData pose, float threshold = 0.25f)
        {
            var leftAnkle = pose.GetKeyPoint(15);
            var rightAnkle = pose.GetKeyPoint(16);

            if (!leftAnkle.IsValid(threshold) || !rightAnkle.IsValid(threshold))
                return null;

            return CalculateLineAngle(leftAnkle.X, leftAnkle.Y, rightAnkle.X, rightAnkle.Y);
        }

        /// <summary>
        /// Calculate shoulder angle relative to feet baseline.
        /// 0 = shoulders aligned with feet.
        /// </summary>
        public static float? CalculateRelativeShoulderAngle(PoseData pose, float threshold = 0.25f)
        {
            var shoulder = CalculateShoulderAngle(pose, threshold);
            var feet = CalculateFeetAngle(pose, threshold);
            
            if (!shoulder.HasValue || !feet.HasValue)
                return null;

            return NormalizeAngle(shoulder.Value - feet.Value);
        }

        /// <summary>
        /// Calculate hip angle relative to feet baseline.
        /// 0 = hips aligned with feet.
        /// </summary>
        public static float? CalculateRelativeHipAngle(PoseData pose, float threshold = 0.25f)
        {
            var hip = CalculateWaistAngle(pose, threshold);
            var feet = CalculateFeetAngle(pose, threshold);
            
            if (!hip.HasValue || !feet.HasValue)
                return null;

            return NormalizeAngle(hip.Value - feet.Value);
        }

        /// <summary>
        /// Normalize angle to -180 to +180 range.
        /// </summary>
        private static float NormalizeAngle(float angle)
        {
            while (angle > 180f) angle -= 360f;
            while (angle < -180f) angle += 360f;
            return angle;
        }

        /// <summary>
        /// Calculate the angle of a line segment from horizontal.
        /// </summary>
        private static float CalculateLineAngle(float x1, float y1, float x2, float y2)
        {
            float dx = x2 - x1;
            float dy = y2 - y1;
            return (float)Math.Atan2(dy, dx) * RadToDeg;
        }

        /// <summary>
        /// Calculate the angle at a joint (vertex at point 2).
        /// Returns angle in degrees (0-180).
        /// </summary>
        private static float CalculateJointAngle(float x1, float y1, float x2, float y2, float x3, float y3)
        {
            // Vectors from joint to adjacent points
            float v1x = x1 - x2;
            float v1y = y1 - y2;
            float v2x = x3 - x2;
            float v2y = y3 - y2;

            // Dot product and magnitudes
            float dot = v1x * v2x + v1y * v2y;
            float mag1 = (float)Math.Sqrt(v1x * v1x + v1y * v1y);
            float mag2 = (float)Math.Sqrt(v2x * v2x + v2y * v2y);

            if (mag1 < 0.0001f || mag2 < 0.0001f)
                return 180f;

            float cosAngle = dot / (mag1 * mag2);
            cosAngle = Math.Max(-1f, Math.Min(1f, cosAngle)); // Clamp to [-1, 1]

            return (float)Math.Acos(cosAngle) * RadToDeg;
        }
    }
}
