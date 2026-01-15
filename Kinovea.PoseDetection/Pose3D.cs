using System;
using System.Collections.Generic;

namespace Kinovea.PoseDetection
{
    /// <summary>
    /// A complete 3D pose with 17 keypoints in world coordinates.
    /// </summary>
    public class Pose3D
    {
        // COCO keypoint indices
        public const int NOSE = 0;
        public const int LEFT_EYE = 1;
        public const int RIGHT_EYE = 2;
        public const int LEFT_EAR = 3;
        public const int RIGHT_EAR = 4;
        public const int LEFT_SHOULDER = 5;
        public const int RIGHT_SHOULDER = 6;
        public const int LEFT_ELBOW = 7;
        public const int RIGHT_ELBOW = 8;
        public const int LEFT_WRIST = 9;
        public const int RIGHT_WRIST = 10;
        public const int LEFT_HIP = 11;
        public const int RIGHT_HIP = 12;
        public const int LEFT_KNEE = 13;
        public const int RIGHT_KNEE = 14;
        public const int LEFT_ANKLE = 15;
        public const int RIGHT_ANKLE = 16;

        /// <summary>
        /// Frame number this pose corresponds to.
        /// </summary>
        public long FrameNumber { get; set; }

        /// <summary>
        /// Timestamp in milliseconds.
        /// </summary>
        public double TimestampMs { get; set; }

        /// <summary>
        /// Array of 17 3D keypoints.
        /// </summary>
        public KeyPoint3D[] KeyPoints { get; set; }

        /// <summary>
        /// Overall confidence of the triangulation.
        /// </summary>
        public float Confidence { get; set; }

        public Pose3D()
        {
            KeyPoints = new KeyPoint3D[17];
            for (int i = 0; i < 17; i++)
            {
                KeyPoints[i] = new KeyPoint3D { Id = i, IsValid = false };
            }
        }

        /// <summary>
        /// Get a specific keypoint by index.
        /// </summary>
        public KeyPoint3D GetKeyPoint(int index)
        {
            if (index >= 0 && index < KeyPoints.Length)
                return KeyPoints[index];
            return null;
        }

        /// <summary>
        /// Count of valid (successfully triangulated) keypoints.
        /// </summary>
        public int ValidKeyPointCount
        {
            get
            {
                int count = 0;
                foreach (var kp in KeyPoints)
                    if (kp != null && kp.IsValid) count++;
                return count;
            }
        }

        /// <summary>
        /// Calculate 3D angle between three keypoints (angle at middle point).
        /// Returns angle in degrees.
        /// </summary>
        public double CalculateAngle(int pointA, int pointB, int pointC)
        {
            var a = KeyPoints[pointA];
            var b = KeyPoints[pointB];
            var c = KeyPoints[pointC];

            if (a == null || b == null || c == null || !a.IsValid || !b.IsValid || !c.IsValid)
                return double.NaN;

            // Vectors BA and BC
            double bax = a.X - b.X;
            double bay = a.Y - b.Y;
            double baz = a.Z - b.Z;

            double bcx = c.X - b.X;
            double bcy = c.Y - b.Y;
            double bcz = c.Z - b.Z;

            // Dot product
            double dot = bax * bcx + bay * bcy + baz * bcz;

            // Magnitudes
            double magBA = Math.Sqrt(bax * bax + bay * bay + baz * baz);
            double magBC = Math.Sqrt(bcx * bcx + bcy * bcy + bcz * bcz);

            if (magBA < 1e-10 || magBC < 1e-10)
                return double.NaN;

            double cosAngle = dot / (magBA * magBC);
            cosAngle = Math.Max(-1.0, Math.Min(1.0, cosAngle));

            return Math.Acos(cosAngle) * 180.0 / Math.PI;
        }

        /// <summary>
        /// Calculate shoulder line rotation around vertical axis (degrees).
        /// 0 = facing forward, positive = rotated left, negative = rotated right.
        /// </summary>
        public double CalculateShoulderRotation()
        {
            var ls = KeyPoints[LEFT_SHOULDER];
            var rs = KeyPoints[RIGHT_SHOULDER];

            if (ls == null || rs == null || !ls.IsValid || !rs.IsValid)
                return double.NaN;

            // Vector from right to left shoulder in XZ plane (horizontal)
            double dx = ls.X - rs.X;
            double dz = ls.Z - rs.Z;

            // Angle from Z-axis (forward direction)
            return Math.Atan2(dx, dz) * 180.0 / Math.PI;
        }

        /// <summary>
        /// Calculate hip line rotation around vertical axis (degrees).
        /// </summary>
        public double CalculateHipRotation()
        {
            var lh = KeyPoints[LEFT_HIP];
            var rh = KeyPoints[RIGHT_HIP];

            if (lh == null || rh == null || !lh.IsValid || !rh.IsValid)
                return double.NaN;

            double dx = lh.X - rh.X;
            double dz = lh.Z - rh.Z;

            return Math.Atan2(dx, dz) * 180.0 / Math.PI;
        }

        /// <summary>
        /// Calculate X-factor (shoulder rotation minus hip rotation).
        /// </summary>
        public double CalculateXFactor()
        {
            double shoulder = CalculateShoulderRotation();
            double hip = CalculateHipRotation();

            if (double.IsNaN(shoulder) || double.IsNaN(hip))
                return double.NaN;

            return shoulder - hip;
        }

        /// <summary>
        /// Calculate left elbow angle (degrees).
        /// </summary>
        public double CalculateLeftElbowAngle()
        {
            return CalculateAngle(LEFT_SHOULDER, LEFT_ELBOW, LEFT_WRIST);
        }

        /// <summary>
        /// Calculate right elbow angle (degrees).
        /// </summary>
        public double CalculateRightElbowAngle()
        {
            return CalculateAngle(RIGHT_SHOULDER, RIGHT_ELBOW, RIGHT_WRIST);
        }

        /// <summary>
        /// Get spine midpoint (average of shoulders and hips).
        /// </summary>
        public (double x, double y, double z) GetSpineMidpoint()
        {
            var ls = KeyPoints[LEFT_SHOULDER];
            var rs = KeyPoints[RIGHT_SHOULDER];
            var lh = KeyPoints[LEFT_HIP];
            var rh = KeyPoints[RIGHT_HIP];

            if (ls == null || rs == null || lh == null || rh == null)
                return (0, 0, 0);

            if (!ls.IsValid || !rs.IsValid || !lh.IsValid || !rh.IsValid)
                return (0, 0, 0);

            double x = (ls.X + rs.X + lh.X + rh.X) / 4.0;
            double y = (ls.Y + rs.Y + lh.Y + rh.Y) / 4.0;
            double z = (ls.Z + rs.Z + lh.Z + rh.Z) / 4.0;

            return (x, y, z);
        }
    }
}
