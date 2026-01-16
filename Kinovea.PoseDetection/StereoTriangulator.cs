using System;
using System.Collections.Generic;
using OpenCvSharp;
using log4net;

namespace Kinovea.PoseDetection
{
    /// <summary>
    /// Triangulates 2D points from two cameras into 3D world coordinates.
    /// Uses Direct Linear Transform (DLT) method.
    /// </summary>
    public class StereoTriangulator
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private CalibrationData calibration;
        private double[,] P1; // Projection matrix for camera A (3x4)
        private double[,] P2; // Projection matrix for camera B (3x4)
        private int debugCount = 0; // Debug counter for DLT logging (instance variable, resets per instance)

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

            // Validate projection matrices
            if (P1.GetLength(0) != 3 || P1.GetLength(1) != 4 ||
                P2.GetLength(0) != 3 || P2.GetLength(1) != 4)
            {
                LastError = string.Format("Invalid projection matrix dimensions: P1={0}x{1}, P2={2}x{3}", 
                    P1.GetLength(0), P1.GetLength(1), P2.GetLength(0), P2.GetLength(1));
                IsReady = false;
                return false;
            }

            // Check for NaN/Infinity in projection matrices
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    if (double.IsNaN(P1[i, j]) || double.IsInfinity(P1[i, j]) ||
                        double.IsNaN(P2[i, j]) || double.IsInfinity(P2[i, j]))
                    {
                        LastError = string.Format("Projection matrix contains NaN/Infinity at P1[{0},{1}]={2}, P2[{0},{1}]={3}", 
                            i, j, P1[i, j], P2[i, j]);
                        IsReady = false;
                        return false;
                    }
                }
            }

            IsReady = true;
            LastError = null;
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

                // After building matrix A, before SolveDLT call:
                if (debugCount < 3)
                {
                    debugCount++;
                    log.DebugFormat("=== DLT DEBUG #{0} ===", debugCount);
                    log.DebugFormat("Input: u1={0:F1}, v1={1:F1}, u2={2:F1}, v2={3:F1}", u1, v1, u2, v2);
                    log.DebugFormat("A[0]: [{0:F4}, {1:F4}, {2:F4}, {3:F4}]", A[0,0], A[0,1], A[0,2], A[0,3]);
                    log.DebugFormat("A[1]: [{0:F4}, {1:F4}, {2:F4}, {3:F4}]", A[1,0], A[1,1], A[1,2], A[1,3]);
                    log.DebugFormat("A[2]: [{0:F4}, {1:F4}, {2:F4}, {3:F4}]", A[2,0], A[2,1], A[2,2], A[2,3]);
                    log.DebugFormat("A[3]: [{0:F4}, {1:F4}, {2:F4}, {3:F4}]", A[3,0], A[3,1], A[3,2], A[3,3]);
                }

                // Solve using SVD - find null space of A
                // For simplicity, use least squares via normal equations: A^T * A * X = 0
                // The solution is the eigenvector corresponding to smallest eigenvalue
                var result = SolveDLT(A);

                if (debugCount <= 3)
                {
                    if (result == null)
                        log.Debug("SolveDLT returned NULL");
                    else
                        log.DebugFormat("SolveDLT result: ({0:F4}, {1:F4}, {2:F4})", result[0], result[1], result[2]);
                }

                if (result == null)
                {
                    LastError = LastError ?? "DLT solve failed";
                    return null;
                }

                return (result[0], result[1], result[2]);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Solve DLT system using SVD-like approach.
        /// Returns 3D point [X, Y, Z] in meters.
        /// The solution is the null space of A (eigenvector of AtA with smallest eigenvalue).
        /// </summary>
        private double[] SolveDLT(double[,] A)
        {
            // Check for NaN/Infinity in input matrix
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    if (double.IsNaN(A[i, j]) || double.IsInfinity(A[i, j]))
                        return null;
                }
            }

            // Use OpenCV SVD to find null space (most reliable method)
            // The null space is the right singular vector corresponding to the smallest singular value
            try
            {
                // Convert A to OpenCV Mat
                Mat A_mat = new Mat(4, 4, MatType.CV_64F);
                for (int i = 0; i < 4; i++)
                {
                    for (int j = 0; j < 4; j++)
                    {
                        A_mat.Set(i, j, A[i, j]);
                    }
                }

                // Perform SVD: A = U * S * V^T
                // The null space is the last column of V (corresponding to smallest singular value)
                Mat w = new Mat(); // singular values
                Mat u = new Mat(); // left singular vectors
                Mat vt = new Mat(); // right singular vectors (transposed)
                
                Cv2.SVDecomp(A_mat, w, u, vt);

                // Get the last column of V (which is the last row of V^T)
                // This corresponds to the smallest singular value
                Mat v = vt.T(); // Transpose to get V
                double x0 = v.At<double>(0, 3);
                double x1 = v.At<double>(1, 3);
                double x2 = v.At<double>(2, 3);
                double x3 = v.At<double>(3, 3);

                // Clean up
                A_mat.Dispose();
                w.Dispose();
                u.Dispose();
                vt.Dispose();
                v.Dispose();

                // Check if homogeneous coordinate is too small
                if (Math.Abs(x3) < 1e-10)
                {
                    System.Diagnostics.Debug.WriteLine($"SolveDLT: SVD result has small W coordinate: {x3:E2}");
                    return SolveDLT_Fallback(A);
                }

                // Convert from homogeneous coordinates
                double x3d = x0 / x3;
                double y3d = x1 / x3;
                double z3d = x2 / x3;

                // Check for NaN or Infinity
                if (double.IsNaN(x3d) || double.IsNaN(y3d) || double.IsNaN(z3d) ||
                    double.IsInfinity(x3d) || double.IsInfinity(y3d) || double.IsInfinity(z3d))
                {
                    System.Diagnostics.Debug.WriteLine($"SolveDLT: SVD result contains NaN/Inf: ({x3d}, {y3d}, {z3d})");
                    return SolveDLT_Fallback(A);
                }
                
                // Check if result is reasonable
                double dist = Math.Sqrt(x3d * x3d + y3d * y3d + z3d * z3d);
                if (dist < 1e-6 || dist > 1000.0)  // Reasonable range: 1mm to 1000m
                {
                    System.Diagnostics.Debug.WriteLine($"SolveDLT: SVD result distance out of range: {dist:F4} m");
                    return SolveDLT_Fallback(A);
                }

                return new double[] { x3d, y3d, z3d };
            }
            catch (Exception ex)
            {
                // Fallback to original method if SVD fails
                System.Diagnostics.Debug.WriteLine($"SolveDLT: SVD exception: {ex.Message}");
                return SolveDLT_Fallback(A);
            }
        }

        /// <summary>
        /// Fallback DLT solve using inverse power iteration (original method).
        /// </summary>
        private double[] SolveDLT_Fallback(double[,] A)
        {
            // Try direct solve first: set W=1, solve for X, Y, Z
            // Build 3x3 system: A[0:3, 0:3] * [X, Y, Z]^T = -A[0:3, 3]
            double[,] A3x3 = new double[3, 3];
            double[] b = new double[3];
            
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    A3x3[i, j] = A[i, j];
                }
                b[i] = -A[i, 3];
            }
            
            // Try solving the 3x3 system
            double[] xyz = SolveLinearSystem3x3(A3x3, b);
            if (xyz != null && !double.IsNaN(xyz[0]) && !double.IsNaN(xyz[1]) && !double.IsNaN(xyz[2]))
            {
                // Check if result is reasonable
                double dist3x3 = Math.Sqrt(xyz[0] * xyz[0] + xyz[1] * xyz[1] + xyz[2] * xyz[2]);
                if (dist3x3 >= 1e-6 && dist3x3 <= 1000.0)
                {
                    return new double[] { xyz[0], xyz[1], xyz[2] };
                }
            }
            
            // Fallback: Use inverse power iteration for null space
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
                    
                    // Check for NaN in AtA
                    if (double.IsNaN(AtA[i, j]) || double.IsInfinity(AtA[i, j]))
                        return null;
                }
            }

            // Use inverse power iteration with better initialization
            Random rnd = new Random((int)(DateTime.Now.Ticks % int.MaxValue));
            double[] x = { rnd.NextDouble() - 0.5, rnd.NextDouble() - 0.5, rnd.NextDouble() - 0.5, 1.0 };
            
            // Normalize initial guess
            double initNorm = Math.Sqrt(x[0] * x[0] + x[1] * x[1] + x[2] * x[2] + x[3] * x[3]);
            if (initNorm < 1e-10) return null;
            for (int i = 0; i < 4; i++)
                x[i] /= initNorm;

            double[] prevX = new double[4];

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
                    break;
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
            
            // Check if result is reasonable
            double dist = Math.Sqrt(x3d * x3d + y3d * y3d + z3d * z3d);
            if (dist < 1e-6 || dist > 1000.0)  // Reasonable range: 1mm to 1000m
                return null;
            
            // Check if result looks like initial guess (indicates failure)
            if (Math.Abs(x3d) < 1e-6 && Math.Abs(y3d) < 1e-6 && Math.Abs(z3d - 1.0) < 1e-6)
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
            {
                LastError = "Triangulator not ready or null input poses";
                return null;
            }

            if (poseA.KeyPoints == null || poseB.KeyPoints == null)
            {
                LastError = "Input poses missing keypoints";
                return null;
            }

            int numKeypoints = Math.Min(poseA.KeyPoints.Length, poseB.KeyPoints.Length);
            numKeypoints = Math.Min(numKeypoints, 17); // COCO has 17

            var pose3D = new Pose3D
            {
                FrameNumber = poseA.FrameNumber,
                TimestampMs = poseA.TimestampMs
            };

            int validCount = 0;
            float totalConfidence = 0;

            int lowConfidenceCount = 0;
            int triangulationFailedCount = 0;
            int zeroConfidenceCount = 0;
            
            for (int i = 0; i < numKeypoints; i++)
            {
                var kpA = poseA.KeyPoints[i];
                var kpB = poseB.KeyPoints[i];

                // Check if keypoints are null or have zero confidence (not detected)
                if (kpA == null || kpB == null || kpA.Confidence <= 0.0f || kpB.Confidence <= 0.0f)
                {
                    pose3D.KeyPoints[i] = new KeyPoint3D { Id = i, IsValid = false };
                    zeroConfidenceCount++;
                    continue;
                }

                // Lower confidence threshold to 0.10f to allow more keypoints through
                // Camera B often has lower confidence, so we need to be more lenient
                // The validation will catch truly bad triangulations
                if (kpA.Confidence < 0.10f || kpB.Confidence < 0.10f)
                {
                    pose3D.KeyPoints[i] = new KeyPoint3D { Id = i, IsValid = false };
                    lowConfidenceCount++;
                    continue;
                }

                var result = Triangulate3DPoint(kpA.X, kpA.Y, kpB.X, kpB.Y);

                // DEBUG: Log every keypoint result
                log.DebugFormat("KP{0}: A=({1:F3},{2:F3}) B=({3:F3},{4:F3}) -> {5}", 
                    i,
                    kpA.X, kpA.Y, 
                    kpB.X, kpB.Y,
                    result.HasValue 
                        ? string.Format("({0:F3},{1:F3},{2:F3})", result.Value.x, result.Value.y, result.Value.z)
                        : "NULL");

                if (result.HasValue)
                {
                    bool hasNaN = double.IsNaN(result.Value.x) || double.IsNaN(result.Value.y) || double.IsNaN(result.Value.z);
                    bool hasInf = double.IsInfinity(result.Value.x) || double.IsInfinity(result.Value.y) || double.IsInfinity(result.Value.z);
                    if (hasNaN || hasInf)
                        log.DebugFormat("  REJECTED: NaN={0}, Inf={1}", hasNaN, hasInf);
                }

                if (result.HasValue)
                {
                    // Check for NaN/Infinity in result
                    if (double.IsNaN(result.Value.x) || double.IsNaN(result.Value.y) || double.IsNaN(result.Value.z) ||
                        double.IsInfinity(result.Value.x) || double.IsInfinity(result.Value.y) || double.IsInfinity(result.Value.z))
                    {
                        pose3D.KeyPoints[i] = new KeyPoint3D { Id = i, IsValid = false };
                        triangulationFailedCount++;
                        // Log first few failures for debugging
                        if (triangulationFailedCount <= 3 && poseA.FrameNumber == 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"Triangulation returned NaN/Inf for keypoint {i}: A=({kpA.X:F3},{kpA.Y:F3},conf={kpA.Confidence:F3}), B=({kpB.X:F3},{kpB.Y:F3},conf={kpB.Confidence:F3}), result=({result.Value.x},{result.Value.y},{result.Value.z})");
                        }
                    }
                    else
                    {
                        float avgConf = (kpA.Confidence + kpB.Confidence) / 2.0f;
                        pose3D.KeyPoints[i] = new KeyPoint3D(i, result.Value.x, result.Value.y, result.Value.z, avgConf);
                        validCount++;
                        totalConfidence += avgConf;
                    }
                }
                else
                {
                    pose3D.KeyPoints[i] = new KeyPoint3D { Id = i, IsValid = false };
                    triangulationFailedCount++;
                    // Log first few failures for debugging
                    if (triangulationFailedCount <= 3 && poseA.FrameNumber == 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"Triangulation returned null for keypoint {i}: A=({kpA.X:F3},{kpA.Y:F3},conf={kpA.Confidence:F3}), B=({kpB.X:F3},{kpB.Y:F3},conf={kpB.Confidence:F3}), LastError={LastError}");
                    }
                }
            }
            
            // Count non-NaN valid keypoints (same check as DualPlayerController uses)
            int nonNaNCount = 0;
            if (pose3D.KeyPoints != null)
            {
                // DEBUG: Log what we actually stored
                log.DebugFormat("=== AFTER TRIANGULATION LOOP (Frame {0}) ===", poseA.FrameNumber);
                log.DebugFormat("pose3D.KeyPoints.Length = {0}", pose3D.KeyPoints.Length);
                log.DebugFormat("validCount (from loop) = {0}", validCount);
                
                for (int i = 0; i < pose3D.KeyPoints.Length; i++)
                {
                    var kp = pose3D.KeyPoints[i];
                    if (kp != null)
                    {
                        bool isValid = kp.IsValid;
                        bool hasNaN = double.IsNaN(kp.X) || double.IsNaN(kp.Y) || double.IsNaN(kp.Z);
                        bool hasInf = double.IsInfinity(kp.X) || double.IsInfinity(kp.Y) || double.IsInfinity(kp.Z);
                        
                        log.DebugFormat("KP{0}: IsValid={1}, hasNaN={2}, hasInf={3}, coords=({4:F3},{5:F3},{6:F3})",
                            i, isValid, hasNaN, hasInf, kp.X, kp.Y, kp.Z);
                        
                        if (kp.IsValid && !hasNaN && !hasInf)
                        {
                            nonNaNCount++;
                        }
                    }
                    else
                    {
                        log.DebugFormat("KP{0}: NULL", i);
                    }
                }
                log.DebugFormat("nonNaNCount = {0}", nonNaNCount);
            }
            
            // Store diagnostic info in LastError if we have very few valid (non-NaN) keypoints
            // This will be picked up by DualPlayerController logging
            // Use nonNaNCount instead of validCount to match DualPlayerController's validation
            // ALWAYS set LastError when nonNaNCount < 5, even if it was previously null
            if (nonNaNCount < 5)
            {
                LastError = string.Format("valid={0}, zeroConf={1}, lowConf={2}, failed={3}, total={4}", 
                    nonNaNCount, zeroConfidenceCount, lowConfidenceCount, 
                    triangulationFailedCount, numKeypoints);
                // Force LastError to be set - don't allow it to be null
                if (LastError == null)
                    LastError = "ERROR: LastError was null after assignment!";
            }
            else
            {
                // Clear LastError on success (only if we have enough valid keypoints)
                LastError = null;
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
