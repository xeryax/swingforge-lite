using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace Kinovea.PoseDetection
{
    /// <summary>
    /// Cache for 3D pose triangulation results. Stores unified 3D poses
    /// for a pair of videos in a JSON file ({video_pair}.pose3d.json).
    /// </summary>
    public class Pose3DCache
    {
        /// <summary>
        /// Version of the cache format.
        /// </summary>
        public int Version { get; set; } = 1;

        /// <summary>
        /// Video file paths for the pair.
        /// </summary>
        public string VideoPathA { get; set; }
        public string VideoPathB { get; set; }

        /// <summary>
        /// Dictionary of 3D poses keyed by frame number.
        /// </summary>
        public Dictionary<long, Pose3D> Poses { get; set; } = new Dictionary<long, Pose3D>();

        /// <summary>
        /// Gets the cache file path for a given video pair.
        /// </summary>
        public static string GetCachePath(string videoPathA, string videoPathB)
        {
            // Create a unique filename based on both video paths
            string nameA = Path.GetFileNameWithoutExtension(videoPathA);
            string nameB = Path.GetFileNameWithoutExtension(videoPathB);
            string dir = Path.GetDirectoryName(videoPathA);
            string combinedName = $"{nameA}_{nameB}.pose3d.json";
            return Path.Combine(dir, combinedName);
        }

        /// <summary>
        /// Check if a valid cache exists for the given video pair.
        /// </summary>
        public static bool IsValid(string videoPathA, string videoPathB)
        {
            string cachePath = GetCachePath(videoPathA, videoPathB);
            if (!File.Exists(cachePath))
                return false;

            try
            {
                var cache = Load(videoPathA, videoPathB);
                if (cache == null || cache.Version != 1)
                    return false;

                return cache.Poses != null && cache.Poses.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Load cache from disk.
        /// </summary>
        public static Pose3DCache Load(string videoPathA, string videoPathB)
        {
            string cachePath = GetCachePath(videoPathA, videoPathB);
            if (!File.Exists(cachePath))
                return null;

            try
            {
                string json = File.ReadAllText(cachePath);
                var cache = JsonConvert.DeserializeObject<Pose3DCache>(json);
                if (cache != null)
                {
                    cache.VideoPathA = videoPathA;
                    cache.VideoPathB = videoPathB;
                }
                return cache;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Save cache to disk.
        /// </summary>
        public void Save(string videoPathA, string videoPathB)
        {
            string cachePath = GetCachePath(videoPathA, videoPathB);
            VideoPathA = videoPathA;
            VideoPathB = videoPathB;

            var settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore
            };

            string json = JsonConvert.SerializeObject(this, settings);
            File.WriteAllText(cachePath, json);
        }

        /// <summary>
        /// Get 3D pose for a specific frame number.
        /// </summary>
        public Pose3D GetPose3D(long frameNumber)
        {
            if (Poses == null)
                return null;

            Poses.TryGetValue(frameNumber, out Pose3D pose);
            return pose;
        }

        /// <summary>
        /// Add or update a 3D pose in the cache.
        /// </summary>
        public void AddOrUpdate(Pose3D pose)
        {
            if (pose == null)
                return;

            if (Poses == null)
                Poses = new Dictionary<long, Pose3D>();

            Poses[pose.FrameNumber] = pose;
        }

        /// <summary>
        /// Get statistics about the cache.
        /// </summary>
        public (int total, int valid, int invalid) GetStatistics()
        {
            if (Poses == null)
                return (0, 0, 0);

            int total = Poses.Count;
            int valid = Poses.Values.Count(p => p != null && p.ValidKeyPointCount > 0);
            int invalid = total - valid;

            return (total, valid, invalid);
        }
    }
}
