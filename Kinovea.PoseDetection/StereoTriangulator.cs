using System;
using System.Collections.Generic;

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

            // Find null space of A (eigenvector of AtA with smallest eigenvalue)
            // Use inverse power iteration: solve AtA * y = x, then normalize
            // Start with a non-zero initial guess
            Random rnd = new Random();
            double[] x = { rnd.NextDouble() - 0.5, rnd.NextDouble() - 0.5, rnd.NextDouble() - 0.5, 1.0 };
            
            // Normalize initial guess
            double initNorm = Math.Sqrt(x[0] * x[0] + x[1] * x[1] + x[2] * x[2] + x[3] * x[3]);
            for (int i = 0; i < 4; i++)
                x[i] /= initNorm;

            double[] prevX = new double[4];
            bool converged = false;

            for (int iter = 0; iter < 200; iter++)
            {
                // Copy current x to prevX
                for (int i = 0; i < 4; i++)
                    prevX[i] = x[i];
                
                // Solve AtA * y = x (inverse iteration for smallest eigenvalue)
                double[] y = SolveLinearSystem4x4(AtA, x);
                if (y == null) break;

                // Check for NaN in solution
                bool hasNaN = false;
                for (int i = 0; i < 4; i++)
                {
                    if (double.IsNaN(y[i]) || double.IsInfinity(y[i]))
                    {
                        hasNaN = true;
                        break;
                    }
                }
                if (hasNaN) break;

                // Normalize
                double norm = Math.Sqrt(y[0] * y[0] + y[1] * y[1] + y[2] * y[2] + y[3] * y[3]);
                if (norm < 1e-10 || double.IsNaN(norm) || double.IsInfinity(norm)) break;

                for (int i = 0; i < 4; i++)
                    x[i] = y[i] / norm;
                
                // Check for convergence
                double diff = 0;
                for (int i = 0; i < 4; i++)
                    diff += Math.Abs(x[i] - prevX[i]);
                if (diff < 1e-10)
                {
                    converged = true;
                    break;
                }
            }

            // Convert from homogeneous coordinates
            if (Math.Abs(x[3]) < 1e-10)
                return null;

            double x3d = x[0] / x[3];
            double y3d = x[1] / x[3];
            double z3d = x[2] / x[3];

            // Check for NaN or Infinity
            if (double.IsNaN(x3d) || double.IsNaN(y3d) || double.IsNaN(z3d) ||
                double.IsInfinity(x3d) || double.IsInfinity(y3d) || double.IsInfinity(z3d))
                return null;
            
            // Check if result is reasonable (not near origin or initial guess)
            double dist = Math.Sqrt(x3d * x3d + y3d * y3d + z3d * z3d);
            if (dist < 1e-6 || dist > 1000.0)  // Reasonable range: 1mm to 1000m
                return null;

            return new double[] { x3d, y3d, z3d };
        }

        /// <summary>
        /// Solve 3x3 linear system Ax = b using Gaussian elimination.
        /// </summary>
        private double[] SolveLinearSystem3x3(double[,] A, double[] b)
        {
            // Check for NaN/Infinity in input
            for (int i = 0; i < 3; i++)
            {
                if (double.IsNaN(b[i]) || double.IsInfinity(b[i]))
                    return null;
                for (int j = 0; j < 3; j++)
                {
                    if (double.IsNaN(A[i, j]) || double.IsInfinity(A[i, j]))
                        return null;
                }
            }

            // Create augmented matrix
            double[,] aug = new double[3, 4];
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                    aug[i, j] = A[i, j];
                aug[i, 3] = b[i];
            }

            // Forward elimination with partial pivoting
            for (int col = 0; col < 3; col++)
            {
                // Find pivot
                int maxRow = col;
                double maxVal = Math.Abs(aug[col, col]);
                for (int row = col + 1; row < 3; row++)
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
                    for (int j = 0; j < 4; j++)
                    {
                        double tmp = aug[col, j];
                        aug[col, j] = aug[maxRow, j];
                        aug[maxRow, j] = tmp;
                    }
                }

                if (Math.Abs(aug[col, col]) < 1e-12)
                    return null; // Singular

                // Eliminate
                for (int row = col + 1; row < 3; row++)
                {
                    double factor = aug[row, col] / aug[col, col];
                    for (int j = col; j < 4; j++)
                        aug[row, j] -= factor * aug[col, j];
                }
            }

            // Back substitution
            double[] x = new double[3];
            for (int i = 2; i >= 0; i--)
            {
                x[i] = aug[i, 3];
                for (int j = i + 1; j < 3; j++)
                    x[i] -= aug[i, j] * x[j];
                x[i] /= aug[i, i];
                
                // Check for NaN/Infinity during back substitution
                if (double.IsNaN(x[i]) || double.IsInfinity(x[i]))
                    return null;
            }

            return x;
        }

        /// <summary>
        /// Solve 4x4 linear system Ax = b using Gaussian elimination.
        /// </summary>
        private double[] SolveLinearSystem4x4(double[,] A, double[] b)
        {
            // Check for NaN/Infinity in input
            for (int i = 0; i < 4; i++)
            {
                if (double.IsNaN(b[i]) || double.IsInfinity(b[i]))
                    return null;
                for (int j = 0; j < 4; j++)
                {
                    if (double.IsNaN(A[i, j]) || double.IsInfinity(A[i, j]))
                        return null;
                }
            }

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

        /// <summary>
        /// Transform 3D pose from camera coordinate system to golf coordinate system.
        /// Golf coords: Z=forward, Y=up, X=left-right.
        /// </summary>
        public Pose3D TransformToGolfCoords(Pose3D pose)
        {
            if (pose == null || !IsReady || calibration == null)
                return pose;

            // For now, return as-is (coordinate transformation would require calibration rotation matrix)
            // This is a placeholder - actual implementation would apply rotation matrix from calibration
            return pose;
        }

        /// <summary>
        /// Validate that a triangulated pose is reasonable.
        /// </summary>
        public bool ValidateTriangulation(Pose3D pose)
        {
            if (pose == null)
                return false;

            // Check for minimum valid keypoints
            if (pose.ValidKeyPointCount < 5)
                return false;

            // Check for NaN/Infinity in keypoints - mark invalid ones
            int validCount = 0;
            foreach (var kp in pose.KeyPoints)
            {
                if (kp != null && kp.IsValid)
                {
                    if (double.IsNaN(kp.X) || double.IsNaN(kp.Y) || double.IsNaN(kp.Z) ||
                        double.IsInfinity(kp.X) || double.IsInfinity(kp.Y) || double.IsInfinity(kp.Z))
                    {
                        // Mark as invalid if NaN/Infinity
                        kp.IsValid = false;
                    }
                    else
                    {
                        validCount++;
                    }
                }
            }

            // Need at least 5 valid keypoints after validation
            return validCount >= 5;
        }

        /// <summary>
        /// Get quality metrics for a triangulated pose.
        /// </summary>
        public TriangulationQuality GetQualityMetrics(Pose3D pose)
        {
            if (pose == null)
                return null;

            var quality = new TriangulationQuality();
            quality.ValidKeypointCount = pose.ValidKeyPointCount;

            // Calculate average confidence
            float totalConf = 0;
            int confCount = 0;
            double minX = double.MaxValue, maxX = double.MinValue;
            double minY = double.MaxValue, maxY = double.MinValue;
            double minZ = double.MaxValue, maxZ = double.MinValue;
            bool hasValid = false;

            foreach (var kp in pose.KeyPoints)
            {
                if (kp != null && kp.IsValid && !double.IsNaN(kp.X) && !double.IsNaN(kp.Y) && !double.IsNaN(kp.Z))
                {
                    totalConf += kp.Confidence;
                    confCount++;
                    minX = Math.Min(minX, kp.X);
                    maxX = Math.Max(maxX, kp.X);
                    minY = Math.Min(minY, kp.Y);
                    maxY = Math.Max(maxY, kp.Y);
                    minZ = Math.Min(minZ, kp.Z);
                    maxZ = Math.Max(maxZ, kp.Z);
                    hasValid = true;
                }
            }

            quality.AverageConfidence = confCount > 0 ? totalConf / confCount : 0;

            if (hasValid)
            {
                quality.MinX = minX;
                quality.MaxX = maxX;
                quality.MinY = minY;
                quality.MaxY = maxY;
                quality.MinZ = minZ;
                quality.MaxZ = maxZ;
            }

            return quality;
        }
    }
}
