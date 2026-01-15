using System;
using System.Collections.Generic;

namespace Kinovea.PoseDetection
{
    /// <summary>
    /// Matches pose frames from two cameras by timestamp.
    /// </summary>
    public class PoseFrameMatcher
    {
        /// <summary>
        /// Maximum time difference (ms) to consider frames as matching.
        /// </summary>
        public double MaxTimeDifferenceMs { get; set; } = 50.0; // 50ms = ~20fps tolerance

        /// <summary>
        /// Match poses from two caches by timestamp.
        /// Returns list of matched pairs (poseA, poseB).
        /// </summary>
        public List<(PoseData poseA, PoseData poseB)> MatchPoses(PoseCache cacheA, PoseCache cacheB)
        {
            var matches = new List<(PoseData, PoseData)>();

            if (cacheA == null || cacheB == null)
                return matches;

            if (cacheA.Frames == null || cacheB.Frames == null)
                return matches;

            // Build index of cache B by timestamp for fast lookup
            var indexB = new Dictionary<long, PoseData>();
            foreach (var pose in cacheB.Frames)
            {
                indexB[pose.FrameNumber] = pose;
            }

            // For each pose in A, find matching pose in B
            foreach (var poseA in cacheA.Frames)
            {
                // First try exact frame number match
                if (indexB.TryGetValue(poseA.FrameNumber, out var poseB))
                {
                    matches.Add((poseA, poseB));
                    continue;
                }

                // Otherwise find closest by timestamp
                PoseData closest = FindClosestByTimestamp(poseA.TimestampMs, cacheB.Frames);
                if (closest != null && Math.Abs(closest.TimestampMs - poseA.TimestampMs) <= MaxTimeDifferenceMs)
                {
                    matches.Add((poseA, closest));
                }
            }

            return matches;
        }

        /// <summary>
        /// Find the pose with the closest timestamp.
        /// </summary>
        private PoseData FindClosestByTimestamp(double targetMs, List<PoseData> poses)
        {
            if (poses == null || poses.Count == 0)
                return null;

            PoseData closest = null;
            double minDiff = double.MaxValue;

            foreach (var pose in poses)
            {
                double diff = Math.Abs(pose.TimestampMs - targetMs);
                if (diff < minDiff)
                {
                    minDiff = diff;
                    closest = pose;
                }
            }

            return closest;
        }

        /// <summary>
        /// Triangulate all matched poses into 3D.
        /// </summary>
        public List<Pose3D> TriangulateAll(PoseCache cacheA, PoseCache cacheB, StereoTriangulator triangulator)
        {
            var results = new List<Pose3D>();

            if (triangulator == null || !triangulator.IsReady)
                return results;

            var matches = MatchPoses(cacheA, cacheB);

            foreach (var (poseA, poseB) in matches)
            {
                var pose3D = triangulator.Triangulate3DPose(poseA, poseB);
                if (pose3D != null && pose3D.ValidKeyPointCount >= 5) // Need at least 5 valid keypoints
                {
                    results.Add(pose3D);
                }
            }

            return results;
        }
    }
}
