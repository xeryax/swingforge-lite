using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace Kinovea.PoseDetection
{
    /// <summary>
    /// Cache for pose detection results. Stores pose data in a JSON file
    /// alongside the video file ({video}.pose.json).
    /// </summary>
    public class PoseCache
    {
        /// <summary>
        /// Version of the cache format.
        /// </summary>
        public int Version { get; set; } = 1;

        /// <summary>
        /// Original video file path.
        /// </summary>
        public string VideoPath { get; set; }

        /// <summary>
        /// Video file hash for invalidation.
        /// </summary>
        public string VideoHash { get; set; }

        /// <summary>
        /// Video duration in milliseconds.
        /// </summary>
        public double VideoDurationMs { get; set; }

        /// <summary>
        /// Total frame count of the video.
        /// </summary>
        public long TotalFrames { get; set; }

        /// <summary>
        /// Sample rate used (every Nth frame).
        /// </summary>
        public int SampleRate { get; set; }

        /// <summary>
        /// Model name used for detection.
        /// </summary>
        public string ModelName { get; set; } = "yolov8n-pose";

        /// <summary>
        /// Timestamp when the cache was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// List of pose data for sampled frames.
        /// </summary>
        public List<PoseData> Frames { get; set; } = new List<PoseData>();

        /// <summary>
        /// Gets the cache file path for a given video path.
        /// </summary>
        public static string GetCachePath(string videoPath)
        {
            return videoPath + ".pose.json";
        }

        /// <summary>
        /// Check if a valid cache exists for the given video.
        /// </summary>
        public static bool IsValid(string videoPath)
        {
            string cachePath = GetCachePath(videoPath);
            if (!File.Exists(cachePath))
                return false;

            try
            {
                var cache = Load(videoPath);
                if (cache == null || cache.Version != 1)
                    return false;

                // Check if video file was modified
                var videoInfo = new FileInfo(videoPath);
                var cacheInfo = new FileInfo(cachePath);
                
                if (videoInfo.LastWriteTimeUtc > cacheInfo.LastWriteTimeUtc)
                    return false;

                return cache.Frames.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Load cache from disk.
        /// </summary>
        public static PoseCache Load(string videoPath)
        {
            string cachePath = GetCachePath(videoPath);
            if (!File.Exists(cachePath))
                return null;

            try
            {
                string json = File.ReadAllText(cachePath);
                return JsonConvert.DeserializeObject<PoseCache>(json);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Save cache to disk.
        /// </summary>
        public void Save(string videoPath)
        {
            string cachePath = GetCachePath(videoPath);
            VideoPath = videoPath;
            CreatedAt = DateTime.UtcNow;

            var settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore
            };

            string json = JsonConvert.SerializeObject(this, settings);
            File.WriteAllText(cachePath, json);
        }

        /// <summary>
        /// Get pose data for a specific frame number.
        /// Returns the nearest sampled frame if exact match not found.
        /// </summary>
        public PoseData GetPoseForFrame(long frameNumber)
        {
            if (Frames == null || Frames.Count == 0)
                return null;

            // Binary search for nearest frame
            int low = 0;
            int high = Frames.Count - 1;

            while (low < high)
            {
                int mid = (low + high) / 2;
                if (Frames[mid].FrameNumber < frameNumber)
                    low = mid + 1;
                else
                    high = mid;
            }

            // Return closest match
            if (low > 0)
            {
                long diffLow = Math.Abs(Frames[low].FrameNumber - frameNumber);
                long diffPrev = Math.Abs(Frames[low - 1].FrameNumber - frameNumber);
                if (diffPrev < diffLow)
                    return Frames[low - 1];
            }

            return Frames[low];
        }

        /// <summary>
        /// Get interpolated pose data for a specific frame.
        /// </summary>
        public PoseData GetInterpolatedPose(long frameNumber)
        {
            if (Frames == null || Frames.Count == 0)
                return null;

            // Find surrounding frames
            int idx = 0;
            for (int i = 0; i < Frames.Count; i++)
            {
                if (Frames[i].FrameNumber >= frameNumber)
                {
                    idx = i;
                    break;
                }
                idx = i;
            }

            // If exact match or at boundaries, return as-is
            if (idx == 0 || Frames[idx].FrameNumber == frameNumber)
                return Frames[idx];

            // Linear interpolation between frames
            var prev = Frames[idx - 1];
            var next = Frames[idx];

            float t = (float)(frameNumber - prev.FrameNumber) / (next.FrameNumber - prev.FrameNumber);

            var interpolated = new PoseData
            {
                FrameNumber = frameNumber,
                TimestampMs = prev.TimestampMs + t * (next.TimestampMs - prev.TimestampMs),
                DetectionConfidence = prev.DetectionConfidence + t * (next.DetectionConfidence - prev.DetectionConfidence)
            };

            // Interpolate keypoints
            for (int i = 0; i < 17; i++)
            {
                var kp1 = prev.KeyPoints[i];
                var kp2 = next.KeyPoints[i];

                interpolated.KeyPoints[i] = new KeyPoint(
                    i,
                    kp1.X + t * (kp2.X - kp1.X),
                    kp1.Y + t * (kp2.Y - kp1.Y),
                    kp1.Confidence + t * (kp2.Confidence - kp1.Confidence)
                );
            }

            return interpolated;
        }
    }
}
