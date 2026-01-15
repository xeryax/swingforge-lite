using System;

namespace Kinovea.PoseDetection
{
    /// <summary>
    /// A single 3D keypoint in world coordinates (meters).
    /// </summary>
    public class KeyPoint3D
    {
        /// <summary>
        /// Keypoint ID (0-16 for COCO format).
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// X coordinate in world space (meters).
        /// </summary>
        public double X { get; set; }

        /// <summary>
        /// Y coordinate in world space (meters).
        /// </summary>
        public double Y { get; set; }

        /// <summary>
        /// Z coordinate in world space (meters).
        /// </summary>
        public double Z { get; set; }

        /// <summary>
        /// Confidence score (average of both 2D detections).
        /// </summary>
        public float Confidence { get; set; }

        /// <summary>
        /// Whether this keypoint was successfully triangulated.
        /// </summary>
        public bool IsValid { get; set; }

        public KeyPoint3D()
        {
        }

        public KeyPoint3D(int id, double x, double y, double z, float confidence = 1.0f)
        {
            Id = id;
            X = x;
            Y = y;
            Z = z;
            Confidence = confidence;
            IsValid = true;
        }

        /// <summary>
        /// Get the COCO keypoint name for this ID.
        /// </summary>
        public string GetName()
        {
            string[] names = {
                "nose", "left_eye", "right_eye", "left_ear", "right_ear",
                "left_shoulder", "right_shoulder", "left_elbow", "right_elbow",
                "left_wrist", "right_wrist", "left_hip", "right_hip",
                "left_knee", "right_knee", "left_ankle", "right_ankle"
            };
            return Id >= 0 && Id < names.Length ? names[Id] : $"keypoint_{Id}";
        }

        /// <summary>
        /// Calculate Euclidean distance to another 3D point.
        /// </summary>
        public double DistanceTo(KeyPoint3D other)
        {
            double dx = X - other.X;
            double dy = Y - other.Y;
            double dz = Z - other.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        public override string ToString()
        {
            return $"{GetName()}: ({X:F3}, {Y:F3}, {Z:F3}) conf={Confidence:F2}";
        }
    }
}
