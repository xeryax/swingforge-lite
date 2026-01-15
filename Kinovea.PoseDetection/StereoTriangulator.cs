using System;

namespace Kinovea.PoseDetection
{
    /// <summary>
    /// Triangulates 2D points from two cameras into 3D world coordinates.
    /// Uses Direct Linear Transform (DLT) method.
    /// </summary>
    public class StereoTriangulator
    {
        private CalibrationData calibration;
        private double[,] P1; // Projection matrix for camera A (3x4)
        private double[,] P2; // Projection matrix for camera B (3x4)

        /// <summary>
        /// Whether the triangulator is ready to use.
        /// </summary>
        public bool IsReady { get; private set; }

        /// <summary>
        /// Last error message if initialization failed.
        /// </summary>
        public string LastError { get; private set; }

        public StereoTriangulator()
        {
            LoadCalibration();
        }

        /// <summary>
        /// Load calibration data from the manager.
        /// </summary>
        public bool LoadCalibration()
        {
            calibration = CalibrationManager.Load();

            if (calibration == null || !calibration.IsValid())
            {
                LastError = "No valid calibration data found";
                IsReady = false;
                return false;
            }

            P1 = calibration.CameraA.ProjectionMatrix;
            P2 = calibration.CameraB.ProjectionMatrix;

            if (P1 == null || P2 == null)
            {
                LastError = "Calibration missing projection matrices";
                IsReady = false;
                return false;
            }

            IsReady = true;
            return true;
        }

        /// <summary>
        /// Triangulate a single 3D point from two 2D observations.
        /// Uses Direct Linear Transform (DLT) method.
        /// </summary>
        /// <param name="x1">X coordinate in camera A (normalized 0-1)</param>
        /// <param name="y1">Y coordinate in camera A (normalized 0-1)</param>
        /// <param name="x2">X coordinate in camera B (normalized 0-1)</param>
        /// <param name="y2">Y coordinate in camera B (normalized 0-1)</param>
        /// <returns>3D point (X, Y, Z) in meters, or null if failed</returns>
        public (double x, double y, double z)? Triangulate3DPoint(double x1, double y1, double x2, double y2)
        {
            if (!IsReady)
                return null;

            // Convert normalized coordinates to pixel coordinates
            double u1 = x1 * calibration.CameraA.ImageWidth;
            double v1 = y1 * calibration.CameraA.ImageHeight;
            double u2 = x2 * calibration.CameraB.ImageWidth;
            double v2 = y2 * calibration.CameraB.ImageHeight;

            return Triangulate3DPointPixels(u1, v1, u2, v2);
        }

        /// <summary>
        /// Triangulate a single 3D point from two 2D pixel observations.
        /// </summary>
        public (double x, double y, double z)? Triangulate3DPointPixels(double u1, double v1, double u2, double v2)
        {
            if (!IsReady)
                return null;

            try
            {
                // Build the DLT matrix A (4x4)
                // Each point gives 2 equations: x*P[2,:] - P[0,:] = 0 and y*P[2,:] - P[1,:] = 0
                double[,] A = new double[4, 4];

                // Camera A constraints
                for (int j = 0; j < 4; j++)
                {
                    A[0, j] = u1 * P1[2, j] - P1[0, j];
                    A[1, j] = v1 * P1[2, j] - P1[1, j];
                }

                // Camera B constraints
                for (int j = 0; j < 4; j++)
                {
                    A[2, j] = u2 * P2[2, j] - P2[0, j];
                    A[3, j] = v2 * P2[2, j] - P2[1, j];
                }

                // Solve using SVD - find null space of A
                // For simplicity, use least squares via normal equations: A^T * A * X = 0
                // The solution is the eigenvector corresponding to smallest eigenvalue
                var result = SolveDLT(A);

                if (result == null)
                    return null;

                return (result[0], result[1], result[2]);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Solve DLT system using SVD-like approach (simplified).
        /// Returns homogeneous 3D point [X, Y, Z, W] normalized to W=1.
        /// </summary>
        private double[] SolveDLT(double[,] A)
        {
            // Compute A^T * A (4x4)
            double[,] AtA = new double[4, 4];
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    double sum = 0;
                    for (int k = 0; k < 4; k++)
                    {
                        sum += A[k, i] * A[k, j];
                    }
                    AtA[i, j] = sum;
                }
            }

            // Find eigenvector for smallest eigenvalue using power iteration on inverse
            // Simplified: Use iterative refinement
            double[] x = { 0, 0, 1, 1 }; // Initial guess

            for (int iter = 0; iter < 100; iter++)
            {
                // Solve AtA * y = x (approximately find inverse eigenvector)
                double[] y = SolveLinearSystem4x4(AtA, x);
                if (y == null) break;

                // Normalize
                double norm = Math.Sqrt(y[0] * y[0] + y[1] * y[1] + y[2] * y[2] + y[3] * y[3]);
                if (norm < 1e-10) break;

                for (int i = 0; i < 4; i++)
                    x[i] = y[i] / norm;
            }

            // Convert from homogeneous coordinates
            if (Math.Abs(x[3]) < 1e-10)
                return null;

            return new double[] { x[0] / x[3], x[1] / x[3], x[2] / x[3] };
        }

        /// <summary>
        /// Solve 4x4 linear system Ax = b using Gaussian elimination.
        /// </summary>
        private double[] SolveLinearSystem4x4(double[,] A, double[] b)
        {
            // Create augmented matrix
            double[,] aug = new double[4, 5];
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++)
                    aug[i, j] = A[i, j];
                aug[i, 4] = b[i];
            }

            // Forward elimination with partial pivoting
            for (int col = 0; col < 4; col++)
            {
                // Find pivot
                int maxRow = col;
                double maxVal = Math.Abs(aug[col, col]);
                for (int row = col + 1; row < 4; row++)
                {
                    if (Math.Abs(aug[row, col]) > maxVal)
                    {
                        maxVal = Math.Abs(aug[row, col]);
                        maxRow = row;
                    }
                }

                // Swap rows
                if (maxRow != col)
                {
                    for (int j = 0; j < 5; j++)
                    {
                        double tmp = aug[col, j];
                        aug[col, j] = aug[maxRow, j];
                        aug[maxRow, j] = tmp;
                    }
                }

                if (Math.Abs(aug[col, col]) < 1e-12)
                    return null; // Singular

                // Eliminate
                for (int row = col + 1; row < 4; row++)
                {
                    double factor = aug[row, col] / aug[col, col];
                    for (int j = col; j < 5; j++)
                        aug[row, j] -= factor * aug[col, j];
                }
            }

            // Back substitution
            double[] x = new double[4];
            for (int i = 3; i >= 0; i--)
            {
                x[i] = aug[i, 4];
                for (int j = i + 1; j < 4; j++)
                    x[i] -= aug[i, j] * x[j];
                x[i] /= aug[i, i];
            }

            return x;
        }

        /// <summary>
        /// Triangulate a full 3D pose from two 2D poses.
        /// </summary>
        public Pose3D Triangulate3DPose(PoseData poseA, PoseData poseB)
        {
            if (!IsReady || poseA == null || poseB == null)
                return null;

            if (poseA.KeyPoints == null || poseB.KeyPoints == null)
                return null;

            int numKeypoints = Math.Min(poseA.KeyPoints.Length, poseB.KeyPoints.Length);
            numKeypoints = Math.Min(numKeypoints, 17); // COCO has 17

            var pose3D = new Pose3D
            {
                FrameNumber = poseA.FrameNumber,
                TimestampMs = poseA.TimestampMs
            };

            int validCount = 0;
            float totalConfidence = 0;

            for (int i = 0; i < numKeypoints; i++)
            {
                var kpA = poseA.KeyPoints[i];
                var kpB = poseB.KeyPoints[i];

                // Skip if either keypoint has low confidence
                if (kpA.Confidence < 0.3f || kpB.Confidence < 0.3f)
                {
                    pose3D.KeyPoints[i] = new KeyPoint3D { Id = i, IsValid = false };
                    continue;
                }

                var result = Triangulate3DPoint(kpA.X, kpA.Y, kpB.X, kpB.Y);

                if (result.HasValue)
                {
                    float avgConf = (kpA.Confidence + kpB.Confidence) / 2.0f;
                    pose3D.KeyPoints[i] = new KeyPoint3D(i, result.Value.x, result.Value.y, result.Value.z, avgConf);
                    validCount++;
                    totalConfidence += avgConf;
                }
                else
                {
                    pose3D.KeyPoints[i] = new KeyPoint3D { Id = i, IsValid = false };
                }
            }

            pose3D.Confidence = validCount > 0 ? totalConfidence / validCount : 0;

            return pose3D;
        }
    }
}
