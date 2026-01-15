using System;
using Newtonsoft.Json;

namespace Kinovea.PoseDetection
{
    /// <summary>
    /// Complete stereo calibration data for both cameras.
    /// </summary>
    public class CalibrationData
    {
        /// <summary>
        /// Version number for calibration format.
        /// </summary>
        public int Version { get; set; } = 1;

        /// <summary>
        /// Date and time when calibration was performed.
        /// </summary>
        public DateTime CalibrationDate { get; set; }

        /// <summary>
        /// Size of the ArUco marker used for calibration (inches).
        /// </summary>
        public double MarkerSizeInches { get; set; } = 12.0;

        /// <summary>
        /// Calibration data for Camera A (typically facing camera).
        /// </summary>
        public CameraCalibration CameraA { get; set; }

        /// <summary>
        /// Calibration data for Camera B (typically down-the-line camera).
        /// </summary>
        public CameraCalibration CameraB { get; set; }

        /// <summary>
        /// Distance between the two cameras (meters).
        /// </summary>
        public double BaselineDistanceMeters { get; set; }

        /// <summary>
        /// Angle between camera viewing directions (degrees).
        /// </summary>
        public double AngleBetweenCameras { get; set; }

        public CalibrationData()
        {
            CameraA = new CameraCalibration("Camera A");
            CameraB = new CameraCalibration("Camera B");
            CalibrationDate = DateTime.UtcNow;
        }

        /// <summary>
        /// Check if calibration data is valid and complete.
        /// </summary>
        public bool IsValid()
        {
            return CameraA != null && CameraA.IsValid() &&
                   CameraB != null && CameraB.IsValid() &&
                   BaselineDistanceMeters >= 0;  // Allow zero baseline
        }

        /// <summary>
        /// Get the number of days since calibration was performed.
        /// </summary>
        public int GetDaysSinceCalibration()
        {
            return (int)(DateTime.UtcNow - CalibrationDate).TotalDays;
        }

        /// <summary>
        /// Check if calibration is stale (older than specified days).
        /// </summary>
        public bool IsStale(int maxDaysOld = 7)
        {
            return GetDaysSinceCalibration() > maxDaysOld;
        }

        /// <summary>
        /// Serialize calibration data to JSON string.
        /// </summary>
        public string ToJson()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }

        /// <summary>
        /// Deserialize calibration data from JSON string.
        /// </summary>
        public static CalibrationData FromJson(string json)
        {
            try
            {
                return JsonConvert.DeserializeObject<CalibrationData>(json);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get a summary string for display.
        /// </summary>
        public string GetSummary()
        {
            if (!IsValid())
                return "Invalid calibration data";

            var posA = CameraA.GetPosition();
            var posB = CameraB.GetPosition();

            return $"Calibrated: {CalibrationDate:yyyy-MM-dd HH:mm}\n" +
                   $"Camera A: ({posA.x:F2}, {posA.y:F2}, {posA.z:F2}) m\n" +
                   $"Camera B: ({posB.x:F2}, {posB.y:F2}, {posB.z:F2}) m\n" +
                   $"Baseline: {BaselineDistanceMeters:F2} m\n" +
                   $"Angle: {AngleBetweenCameras:F1}°";
        }
    }
}
