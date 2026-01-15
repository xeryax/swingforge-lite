using System;
using System.IO;

namespace Kinovea.PoseDetection
{
    /// <summary>
    /// Manages saving and loading of stereo camera calibration data.
    /// </summary>
    public static class CalibrationManager
    {
        private static readonly string CalibrationFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SwingForge");

        private static readonly string CalibrationFilePath = Path.Combine(
            CalibrationFolder, "camera_calibration.json");

        private static CalibrationData cachedCalibration = null;

        /// <summary>
        /// Get the path to the calibration file.
        /// </summary>
        public static string FilePath => CalibrationFilePath;

        /// <summary>
        /// Check if calibration data exists.
        /// </summary>
        public static bool IsCalibrated()
        {
            if (cachedCalibration != null && cachedCalibration.IsValid())
                return true;

            return File.Exists(CalibrationFilePath) && Load() != null;
        }

        /// <summary>
        /// Check if calibration is stale (older than specified days).
        /// </summary>
        public static bool IsStale(int maxDaysOld = 7)
        {
            var calibration = Load();
            if (calibration == null)
                return true;

            return calibration.IsStale(maxDaysOld);
        }

        /// <summary>
        /// Get the number of days since last calibration.
        /// </summary>
        public static int GetDaysSinceCalibration()
        {
            var calibration = Load();
            if (calibration == null)
                return -1;

            return calibration.GetDaysSinceCalibration();
        }

        /// <summary>
        /// Load calibration data from file.
        /// </summary>
        public static CalibrationData Load()
        {
            if (cachedCalibration != null)
                return cachedCalibration;

            if (!File.Exists(CalibrationFilePath))
                return null;

            try
            {
                string json = File.ReadAllText(CalibrationFilePath);
                cachedCalibration = CalibrationData.FromJson(json);
                
                if (cachedCalibration != null && !cachedCalibration.IsValid())
                {
                    cachedCalibration = null;
                }

                return cachedCalibration;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Save calibration data to file.
        /// </summary>
        public static bool Save(CalibrationData calibration)
        {
            if (calibration == null)
                return false;

            try
            {
                // Ensure directory exists
                if (!Directory.Exists(CalibrationFolder))
                {
                    Directory.CreateDirectory(CalibrationFolder);
                }

                string json = calibration.ToJson();
                File.WriteAllText(CalibrationFilePath, json);

                // Update cache
                cachedCalibration = calibration;

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Delete calibration data.
        /// </summary>
        public static bool Delete()
        {
            cachedCalibration = null;

            if (!File.Exists(CalibrationFilePath))
                return true;

            try
            {
                File.Delete(CalibrationFilePath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Clear the cached calibration (forces reload from file).
        /// </summary>
        public static void ClearCache()
        {
            cachedCalibration = null;
        }

        /// <summary>
        /// Get a status string for display.
        /// </summary>
        public static string GetStatusString()
        {
            var calibration = Load();
            if (calibration == null)
                return "Not Calibrated";

            int days = calibration.GetDaysSinceCalibration();
            if (days == 0)
                return "Calibrated today";
            else if (days == 1)
                return "Calibrated yesterday";
            else if (days <= 7)
                return $"Calibrated {days} days ago";
            else
                return $"Calibrated {calibration.CalibrationDate:MMM d} (stale)";
        }
    }
}
