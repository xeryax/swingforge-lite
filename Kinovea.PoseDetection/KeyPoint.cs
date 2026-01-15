using System;

namespace Kinovea.PoseDetection
{
    /// <summary>
    /// Represents a single detected keypoint from pose estimation.
    /// Uses COCO 17-keypoint format.
    /// </summary>
    public class KeyPoint
    {
        /// <summary>
        /// Keypoint index (0-16 for COCO format).
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Normalized X coordinate (0-1).
        /// </summary>
        public float X { get; set; }

        /// <summary>
        /// Normalized Y coordinate (0-1).
        /// </summary>
        public float Y { get; set; }

        /// <summary>
        /// Detection confidence score (0-1).
        /// </summary>
        public float Confidence { get; set; }

        /// <summary>
        /// Human-readable name for this keypoint.
        /// </summary>
        public string Name { get; set; }

        public KeyPoint() { }

        public KeyPoint(int id, float x, float y, float confidence)
        {
            Id = id;
            X = x;
            Y = y;
            Confidence = confidence;
            Name = GetKeypointName(id);
        }

        /// <summary>
        /// Returns the COCO keypoint name for the given index.
        /// </summary>
        public static string GetKeypointName(int id)
        {
            switch (id)
            {
                case 0: return "nose";
                case 1: return "left_eye";
                case 2: return "right_eye";
                case 3: return "left_ear";
                case 4: return "right_ear";
                case 5: return "left_shoulder";
                case 6: return "right_shoulder";
                case 7: return "left_elbow";
                case 8: return "right_elbow";
                case 9: return "left_wrist";
                case 10: return "right_wrist";
                case 11: return "left_hip";
                case 12: return "right_hip";
                case 13: return "left_knee";
                case 14: return "right_knee";
                case 15: return "left_ankle";
                case 16: return "right_ankle";
                default: return $"keypoint_{id}";
            }
        }

        /// <summary>
        /// Check if the keypoint is valid (has sufficient confidence).
        /// </summary>
        public bool IsValid(float threshold = 0.25f)
        {
            return Confidence >= threshold;
        }
    }
}
