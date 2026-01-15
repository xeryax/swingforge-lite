using System;
using OpenCvSharp;
using OpenCvSharp.Aruco;

namespace Kinovea.PoseDetection
{
    /// <summary>
    /// Performs stereo camera calibration using ArUco markers.
    /// </summary>
    public class ArucoCalibrator
    {
        private readonly Dictionary arucoDictionary;
        private readonly DetectorParameters detectorParameters;
        
        // Use marker ID 0 from DICT_6X6_250
        private const int TargetMarkerId = 0;
        
        // Conversion factor: 1 inch = 0.0254 meters
        private const double InchesToMeters = 0.0254;

        public ArucoCalibrator()
        {
            arucoDictionary = CvAruco.GetPredefinedDictionary(PredefinedDictionaryName.Dict6X6_250);
            detectorParameters = new DetectorParameters();
        }

        /// <summary>
        /// Detect ArUco marker in a frame.
        /// </summary>
        /// <param name="frame">Input image (BGR format)</param>
        /// <returns>Tuple of (success, corner points) where corners are in image pixel coordinates</returns>
        public (bool success, Point2f[] corners) DetectMarker(Mat frame)
        {
            if (frame == null || frame.Empty())
                return (false, null);

            Point2f[][] allCorners;
            int[] ids;
            Point2f[][] rejectedPoints;

            try
            {
                CvAruco.DetectMarkers(frame, arucoDictionary, out allCorners, out ids, detectorParameters, out rejectedPoints);

                if (ids == null || ids.Length == 0)
                    return (false, null);

                // Find marker with ID 0
                for (int i = 0; i < ids.Length; i++)
                {
                    if (ids[i] == TargetMarkerId)
                    {
                        return (true, allCorners[i]);
                    }
                }

                return (false, null);
            }
            catch
            {
                return (false, null);
            }
        }

        /// <summary>
        /// Draw detected marker corners on frame for visualization.
        /// </summary>
        public void DrawDetectedMarker(Mat frame, Point2f[] corners, bool detected)
        {
            if (frame == null || frame.Empty())
                return;

            if (detected && corners != null && corners.Length == 4)
            {
                // Draw marker outline in green
                var color = new Scalar(0, 255, 0);
                for (int i = 0; i < 4; i++)
                {
                    Cv2.Line(frame, 
                        new Point((int)corners[i].X, (int)corners[i].Y),
                        new Point((int)corners[(i + 1) % 4].X, (int)corners[(i + 1) % 4].Y),
                        color, 3);
                }

                // Draw corner circles
                for (int i = 0; i < 4; i++)
                {
                    Cv2.Circle(frame, new Point((int)corners[i].X, (int)corners[i].Y), 8, color, -1);
                }

                // Draw "DETECTED" text
                Cv2.PutText(frame, "DETECTED", new Point(20, 50), 
                    HersheyFonts.HersheySimplex, 1.5, color, 3);
            }
            else
            {
                // Draw "NOT FOUND" text in red
                var color = new Scalar(0, 0, 255);
                Cv2.PutText(frame, "MARKER NOT FOUND", new Point(20, 50), 
                    HersheyFonts.HersheySimplex, 1.5, color, 3);
            }
        }

        /// <summary>
        /// Calibrate both cameras from frames showing the same ArUco marker.
        /// </summary>
        /// <param name="frameA">Frame from Camera A</param>
        /// <param name="frameB">Frame from Camera B</param>
        /// <param name="markerSizeInches">Physical size of the marker in inches</param>
        /// <returns>Complete calibration data, or null if calibration failed</returns>
        public CalibrationData CalibrateFromFrames(Mat frameA, Mat frameB, double markerSizeInches = 12.0)
        {
            // Detect markers in both frames
            var (successA, cornersA) = DetectMarker(frameA);
            var (successB, cornersB) = DetectMarker(frameB);

            if (!successA || !successB)
            {
                LastError = $"Marker detection failed (A:{successA}, B:{successB})";
                return null;
            }

            double markerSizeMeters = markerSizeInches * InchesToMeters;

            // Create calibration data
            var calibration = new CalibrationData
            {
                CalibrationDate = DateTime.UtcNow,
                MarkerSizeInches = markerSizeInches
            };

            // Calibrate each camera
            calibration.CameraA = CalibrateCamera("Camera A", frameA, cornersA, markerSizeMeters);
            calibration.CameraB = CalibrateCamera("Camera B", frameB, cornersB, markerSizeMeters);

            if (calibration.CameraA == null || calibration.CameraB == null)
            {
                if (calibration.CameraA == null)
                    LastError = "Camera A SolvePnP failed: " + (LastError ?? "unknown");
                else
                    LastError = "Camera B SolvePnP failed: " + (LastError ?? "unknown");
                return null;
            }

            // Calculate baseline distance between cameras
            var posA = calibration.CameraA.GetPosition();
            var posB = calibration.CameraB.GetPosition();
            
            double dx = posB.x - posA.x;
            double dy = posB.y - posA.y;
            double dz = posB.z - posA.z;
            calibration.BaselineDistanceMeters = Math.Sqrt(dx * dx + dy * dy + dz * dz);

            // Calculate angle between cameras (simplified - angle in XZ plane)
            // This is approximate; proper calculation would use rotation matrices
            calibration.AngleBetweenCameras = CalculateAngleBetweenCameras(
                calibration.CameraA, calibration.CameraB);

            return calibration;
        }

        /// <summary>
        /// Calibrate a single camera from detected marker corners.
        /// </summary>
        private CameraCalibration CalibrateCamera(string name, Mat frame, Point2f[] corners, double markerSizeMeters)
        {
            try
            {
                var calibration = new CameraCalibration(name)
                {
                    ImageWidth = frame.Width,
                    ImageHeight = frame.Height
                };

                // Estimate camera intrinsics from image dimensions
                var cameraMatrix = EstimateCameraMatrix(frame.Width, frame.Height);
                calibration.CameraMatrix = MatToArray2D(cameraMatrix);
                calibration.DistortionCoeffs = new double[5]; // Assume no distortion for now

                // Define 3D object points for the marker (marker centered at origin, lying in XY plane)
                // Corners are: top-left, top-right, bottom-right, bottom-left
                double half = markerSizeMeters / 2.0;
                var objectPoints = new Point3f[]
                {
                    new Point3f((float)(-half), (float)(half), 0),   // Top-left
                    new Point3f((float)(half), (float)(half), 0),    // Top-right
                    new Point3f((float)(half), (float)(-half), 0),   // Bottom-right
                    new Point3f((float)(-half), (float)(-half), 0)   // Bottom-left
                };

                // Convert corners to Point2f array
                var imagePoints = corners;

                // Solve PnP to get camera pose relative to marker
                using (var rvec = new Mat())
                using (var tvec = new Mat())
                using (var distCoeffs = new Mat(1, 5, MatType.CV_64F, Scalar.All(0)))
                using (var objPts = new Mat(4, 1, MatType.CV_32FC3))
                using (var imgPts = new Mat(4, 1, MatType.CV_32FC2))
                {
                    // Copy object points
                    for (int i = 0; i < 4; i++)
                    {
                        objPts.Set(i, 0, new Vec3f(objectPoints[i].X, objectPoints[i].Y, objectPoints[i].Z));
                    }

                    // Copy image points
                    for (int i = 0; i < 4; i++)
                    {
                        imgPts.Set(i, 0, new Vec2f(imagePoints[i].X, imagePoints[i].Y));
                    }

                    Cv2.SolvePnP(objPts, imgPts, cameraMatrix, distCoeffs, 
                        rvec, tvec, false, SolvePnPFlags.Iterative);

                    // Check if result is valid (rvec and tvec should be non-empty)
                    if (rvec.Empty() || tvec.Empty())
                        return null;

                    // Store rotation and translation vectors
                    calibration.RotationVector = new double[3];
                    calibration.TranslationVector = new double[3];

                    for (int i = 0; i < 3; i++)
                    {
                        calibration.RotationVector[i] = rvec.At<double>(i);
                        calibration.TranslationVector[i] = tvec.At<double>(i);
                    }

                    // Build projection matrix P = K * [R | t]
                    calibration.ProjectionMatrix = BuildProjectionMatrix(
                        cameraMatrix, rvec, tvec);
                }

                return calibration;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CalibrateCamera failed: {ex.Message}");
                LastError = ex.Message;
                return null;
            }
        }

        /// <summary>
        /// Last error message from calibration attempt.
        /// </summary>
        public string LastError { get; private set; }

        /// <summary>
        /// Estimate camera intrinsic matrix from image dimensions.
        /// Uses a reasonable approximation for typical cameras.
        /// </summary>
        private Mat EstimateCameraMatrix(int width, int height)
        {
            // Focal length approximation: assume ~60 degree horizontal FOV
            // For 60 deg FOV: f = (width/2) / tan(30 deg) ≈ width * 0.866
            // More conservative: f ≈ max(width, height)
            double focalLength = Math.Max(width, height);
            
            double cx = width / 2.0;
            double cy = height / 2.0;

            var K = new Mat(3, 3, MatType.CV_64F);
            K.Set(0, 0, focalLength);
            K.Set(0, 1, 0);
            K.Set(0, 2, cx);
            K.Set(1, 0, 0);
            K.Set(1, 1, focalLength);
            K.Set(1, 2, cy);
            K.Set(2, 0, 0);
            K.Set(2, 1, 0);
            K.Set(2, 2, 1);

            return K;
        }

        /// <summary>
        /// Build projection matrix P = K * [R | t]
        /// </summary>
        private double[,] BuildProjectionMatrix(Mat cameraMatrix, Mat rvec, Mat tvec)
        {
            var P = new double[3, 4];

            using (var R = new Mat())
            {
                // Convert rotation vector to rotation matrix
                Cv2.Rodrigues(rvec, R);

                // Build [R | t] (3x4 matrix)
                var Rt = new Mat(3, 4, MatType.CV_64F);
                for (int i = 0; i < 3; i++)
                {
                    for (int j = 0; j < 3; j++)
                    {
                        Rt.Set(i, j, R.At<double>(i, j));
                    }
                    Rt.Set(i, 3, tvec.At<double>(i));
                }

                // P = K * [R | t] - manual matrix multiplication (3x3 * 3x4 = 3x4)
                for (int i = 0; i < 3; i++)
                {
                    for (int j = 0; j < 4; j++)
                    {
                        double sum = 0;
                        for (int k = 0; k < 3; k++)
                        {
                            sum += cameraMatrix.At<double>(i, k) * Rt.At<double>(k, j);
                        }
                        P[i, j] = sum;
                    }
                }

                Rt.Dispose();
            }

            return P;
        }

        /// <summary>
        /// Convert Mat to 2D double array.
        /// </summary>
        private double[,] MatToArray2D(Mat mat)
        {
            var arr = new double[mat.Rows, mat.Cols];
            for (int i = 0; i < mat.Rows; i++)
            {
                for (int j = 0; j < mat.Cols; j++)
                {
                    arr[i, j] = mat.At<double>(i, j);
                }
            }
            return arr;
        }

        /// <summary>
        /// Calculate approximate angle between two cameras based on their poses.
        /// </summary>
        private double CalculateAngleBetweenCameras(CameraCalibration camA, CameraCalibration camB)
        {
            // Simplified: calculate angle based on the viewing directions
            // The Z-axis of each camera (after rotation) points in the viewing direction
            
            try
            {
                using (var rvecA = new Mat(3, 1, MatType.CV_64F))
                using (var rvecB = new Mat(3, 1, MatType.CV_64F))
                using (var RA = new Mat())
                using (var RB = new Mat())
                {
                    for (int i = 0; i < 3; i++)
                    {
                        rvecA.Set(i, 0, camA.RotationVector[i]);
                        rvecB.Set(i, 0, camB.RotationVector[i]);
                    }

                    Cv2.Rodrigues(rvecA, RA);
                    Cv2.Rodrigues(rvecB, RB);

                    // Get viewing direction (third column of rotation matrix, negated)
                    double[] viewA = { -RA.At<double>(0, 2), -RA.At<double>(1, 2), -RA.At<double>(2, 2) };
                    double[] viewB = { -RB.At<double>(0, 2), -RB.At<double>(1, 2), -RB.At<double>(2, 2) };

                    // Dot product
                    double dot = viewA[0] * viewB[0] + viewA[1] * viewB[1] + viewA[2] * viewB[2];
                    
                    // Clamp to valid range
                    dot = Math.Max(-1.0, Math.Min(1.0, dot));

                    // Angle in degrees
                    return Math.Acos(dot) * 180.0 / Math.PI;
                }
            }
            catch
            {
                return 0;
            }
        }
    }
}
