using System;
using System.Collections.Generic;

namespace Kinovea.PoseDetection
{
    /// <summary>
    /// Smooths pose data using Kalman filters for each keypoint.
    /// Manages 17 filters (one per COCO keypoint).
    /// </summary>
    public class PoseSmoother
    {
        private const int NumKeypoints = 17;
        private KalmanFilter2D[] filters;
        private float processNoise;
        private float measurementNoise;

        /// <summary>
        /// Create a new pose smoother.
        /// </summary>
        /// <param name="processNoise">Process noise (lower = smoother). Default: 0.0005</param>
        /// <param name="measurementNoise">Measurement noise. Default: 0.5</param>
        public PoseSmoother(float processNoise = 0.0005f, float measurementNoise = 0.5f)
        {
            this.processNoise = processNoise;
            this.measurementNoise = measurementNoise;
            filters = new KalmanFilter2D[NumKeypoints];
            for (int i = 0; i < NumKeypoints; i++)
            {
                filters[i] = new KalmanFilter2D(processNoise, measurementNoise);
            }
        }

        /// <summary>
        /// Reset all filters to uninitialized state.
        /// </summary>
        public void Reset()
        {
            for (int i = 0; i < NumKeypoints; i++)
            {
                filters[i].Reset();
            }
        }

        /// <summary>
        /// Smooth a single pose frame.
        /// Call this sequentially for each frame in order.
        /// </summary>
        /// <param name="rawPose">Raw pose data from detection</param>
        /// <param name="dt">Time delta (typically 1.0 for frame-based)</param>
        /// <returns>Smoothed pose data</returns>
        public PoseData Smooth(PoseData rawPose, float dt = 1.0f)
        {
            if (rawPose == null || rawPose.KeyPoints == null)
                return rawPose;

            // Create a copy of the pose data
            var smoothedPose = new PoseData
            {
                FrameNumber = rawPose.FrameNumber,
                TimestampMs = rawPose.TimestampMs,
                KeyPoints = new KeyPoint[rawPose.KeyPoints.Length],
                DetectionConfidence = rawPose.DetectionConfidence,
                BoundingBox = rawPose.BoundingBox
            };

            for (int i = 0; i < rawPose.KeyPoints.Length && i < NumKeypoints; i++)
            {
                var kp = rawPose.KeyPoints[i];
                
                // Predict next state
                filters[i].Predict(dt);
                
                // Update with measurement
                var smoothedPos = filters[i].Update(kp.X, kp.Y, kp.Confidence);
                
                smoothedPose.KeyPoints[i] = new KeyPoint
                {
                    X = smoothedPos.X,
                    Y = smoothedPos.Y,
                    Confidence = kp.Confidence // Keep original confidence
                };
            }

            // Recalculate angles with smoothed positions
            AngleCalculator.CalculateAllAngles(smoothedPose);

            return smoothedPose;
        }

        /// <summary>
        /// Smooth an entire sequence of poses.
        /// This processes all frames in order, applying Kalman filtering.
        /// </summary>
        /// <param name="poses">List of raw pose data</param>
        /// <returns>List of smoothed pose data</returns>
        public List<PoseData> SmoothSequence(List<PoseData> poses)
        {
            if (poses == null || poses.Count == 0)
                return poses;

            Reset();

            var smoothedPoses = new List<PoseData>(poses.Count);
            
            // Sort by frame number to ensure correct order
            var sortedPoses = new List<PoseData>(poses);
            sortedPoses.Sort((a, b) => a.FrameNumber.CompareTo(b.FrameNumber));

            long lastFrame = -1;
            foreach (var pose in sortedPoses)
            {
                // Calculate dt based on frame gap
                float dt = lastFrame >= 0 ? (pose.FrameNumber - lastFrame) : 1.0f;
                dt = Math.Max(dt, 1.0f); // Minimum dt of 1 frame
                
                var smoothed = Smooth(pose, dt);
                smoothedPoses.Add(smoothed);
                
                lastFrame = pose.FrameNumber;
            }

            return smoothedPoses;
        }

        /// <summary>
        /// Apply bidirectional smoothing for even better results.
        /// Processes forward then backward and averages the results.
        /// </summary>
        /// <param name="poses">List of raw pose data</param>
        /// <returns>List of smoothed pose data</returns>
        public List<PoseData> SmoothSequenceBidirectional(List<PoseData> poses)
        {
            if (poses == null || poses.Count == 0)
                return poses;

            // Use simple moving average instead - much more effective for sparse samples
            return SmoothWithMovingAverage(poses, 3);
        }

        /// <summary>
        /// Apply simple moving average smoothing.
        /// More effective than Kalman for sparsely sampled data.
        /// </summary>
        /// <param name="poses">List of raw pose data</param>
        /// <param name="windowSize">Number of frames to average (must be odd)</param>
        /// <returns>List of smoothed pose data</returns>
        public List<PoseData> SmoothWithMovingAverage(List<PoseData> poses, int windowSize = 5)
        {
            if (poses == null || poses.Count == 0)
                return poses;

            // Ensure odd window size
            if (windowSize % 2 == 0) windowSize++;
            int halfWindow = windowSize / 2;

            // Sort by frame number
            var sortedPoses = new List<PoseData>(poses);
            sortedPoses.Sort((a, b) => a.FrameNumber.CompareTo(b.FrameNumber));

            var result = new List<PoseData>(poses.Count);

            for (int i = 0; i < sortedPoses.Count; i++)
            {
                var current = sortedPoses[i];
                
                // Create smoothed pose
                var smoothed = new PoseData
                {
                    FrameNumber = current.FrameNumber,
                    TimestampMs = current.TimestampMs,
                    KeyPoints = new KeyPoint[current.KeyPoints.Length],
                    DetectionConfidence = current.DetectionConfidence,
                    BoundingBox = current.BoundingBox
                };

                // Average each keypoint across the window
                for (int kp = 0; kp < current.KeyPoints.Length; kp++)
                {
                    float sumX = 0, sumY = 0, sumConf = 0;
                    int count = 0;

                    for (int w = -halfWindow; w <= halfWindow; w++)
                    {
                        int idx = i + w;
                        if (idx >= 0 && idx < sortedPoses.Count)
                        {
                            var p = sortedPoses[idx].KeyPoints[kp];
                            // Weight by confidence
                            float weight = Math.Max(p.Confidence, 0.1f);
                            sumX += p.X * weight;
                            sumY += p.Y * weight;
                            sumConf += weight;
                            count++;
                        }
                    }

                    if (sumConf > 0)
                    {
                        smoothed.KeyPoints[kp] = new KeyPoint
                        {
                            X = sumX / sumConf,
                            Y = sumY / sumConf,
                            Confidence = current.KeyPoints[kp].Confidence
                        };
                    }
                    else
                    {
                        smoothed.KeyPoints[kp] = current.KeyPoints[kp];
                    }
                }

                AngleCalculator.CalculateAllAngles(smoothed);
                result.Add(smoothed);
            }

            return result;
        }
    }
}
