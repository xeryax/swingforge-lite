using System;

namespace Kinovea.PoseDetection
{
    /// <summary>
    /// Per-camera calibration data including intrinsic and extrinsic parameters.
    /// </summary>
    public class CameraCalibration
    {
        /// <summary>
        /// Camera name (e.g., "Camera A", "Camera B").
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Camera intrinsic matrix (3x3).
        /// [fx,  0, cx]
        /// [ 0, fy, cy]
        /// [ 0,  0,  1]
        /// </summary>
        public double[,] CameraMatrix { get; set; }

        /// <summary>
        /// Lens distortion coefficients (k1, k2, p1, p2, k3).
        /// </summary>
        public double[] DistortionCoeffs { get; set; }

        /// <summary>
        /// Rotation vector (Rodrigues format) relative to calibration marker.
        /// </summary>
        public double[] RotationVector { get; set; }

        /// <summary>
        /// Translation vector relative to calibration marker (meters).
        /// </summary>
        public double[] TranslationVector { get; set; }

        /// <summary>
        /// Projection matrix (3x4) for triangulation: P = K * [R | t]
        /// </summary>
        public double[,] ProjectionMatrix { get; set; }

        /// <summary>
        /// Image width used during calibration.
        /// </summary>
        public int ImageWidth { get; set; }

        /// <summary>
        /// Image height used during calibration.
        /// </summary>
        public int ImageHeight { get; set; }

        public CameraCalibration()
        {
            CameraMatrix = new double[3, 3];
            DistortionCoeffs = new double[5];
            RotationVector = new double[3];
            TranslationVector = new double[3];
            ProjectionMatrix = new double[3, 4];
        }

        public CameraCalibration(string name) : this()
        {
            Name = name;
        }

        /// <summary>
        /// Check if calibration data is valid.
        /// </summary>
        public bool IsValid()
        {
            return !string.IsNullOrEmpty(Name) &&
                   CameraMatrix != null && CameraMatrix.GetLength(0) == 3 && CameraMatrix.GetLength(1) == 3 &&
                   RotationVector != null && RotationVector.Length == 3 &&
                   TranslationVector != null && TranslationVector.Length == 3 &&
                   ProjectionMatrix != null && ProjectionMatrix.GetLength(0) == 3 && ProjectionMatrix.GetLength(1) == 4;
        }

        /// <summary>
        /// Get camera position in world coordinates (relative to marker).
        /// </summary>
        public (double x, double y, double z) GetPosition()
        {
            if (TranslationVector == null || TranslationVector.Length < 3)
                return (0, 0, 0);

            return (TranslationVector[0], TranslationVector[1], TranslationVector[2]);
        }
    }
}
