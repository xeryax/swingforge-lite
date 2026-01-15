using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using Kinovea.Services;
using Kinovea.PoseDetection;

namespace Kinovea.ScreenManager
{
    /// <summary>
    /// Import pose data from a .pose.json cache file created by SwingForge's pose detection.
    /// This creates YOLOv8Pose drawings (COCO 17-keypoint format) on keyframes.
    /// </summary>
    public static class MetadataImporterYoloV8Pose
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly float confidenceThreshold = 0.25f;

        // Map COCO keypoint indices to option names
        private static readonly Dictionary<int, string> options = new Dictionary<int, string>
        {
            { 0, "showNose" },
            { 1, "showLEye" },
            { 2, "showREye" },
            { 3, "showLEar" },
            { 4, "showREar" },
            { 5, "showLShoulder" },
            { 6, "showRShoulder" },
            { 7, "showLElbow" },
            { 8, "showRElbow" },
            { 9, "showLWrist" },
            { 10, "showRWrist" },
            { 11, "showLHip" },
            { 12, "showRHip" },
            { 13, "showLKnee" },
            { 14, "showRKnee" },
            { 15, "showLAnkle" },
            { 16, "showRAnkle" }
        };

        /// <summary>
        /// Import pose data from a .pose.json file.
        /// </summary>
        public static void Import(Metadata metadata, string videoPath)
        {
            string cachePath = PoseCache.GetCachePath(videoPath);
            if (!File.Exists(cachePath))
            {
                log.WarnFormat("Pose cache not found: {0}", cachePath);
                return;
            }

            PoseCache cache = PoseCache.Load(videoPath);
            if (cache == null || cache.Frames.Count == 0)
            {
                log.WarnFormat("Failed to load pose cache or cache is empty.");
                return;
            }

            log.DebugFormat("Importing {0} pose frames from cache.", cache.Frames.Count);

            // Calculate statistics and detect impact frame
            var stats = PoseStatistics.CalculateFromPoses(cache.Frames);

            foreach (var poseData in cache.Frames)
            {
                bool isImpact = (poseData.FrameNumber == stats.ImpactFrame);
                ImportFrame(metadata, poseData, isImpact);
            }
        }

        /// <summary>
        /// Get statistics from a video's pose cache.
        /// </summary>
        public static PoseStatistics GetStatistics(string videoPath)
        {
            string cachePath = PoseCache.GetCachePath(videoPath);
            if (!File.Exists(cachePath))
                return null;

            PoseCache cache = PoseCache.Load(videoPath);
            if (cache == null || cache.Frames.Count == 0)
                return null;

            return PoseStatistics.CalculateFromPoses(cache.Frames);
        }

        /// <summary>
        /// Import a single pose detection result directly.
        /// </summary>
        public static void ImportPoseData(Metadata metadata, PoseData poseData, long timestamp)
        {
            if (poseData == null)
                return;

            poseData.FrameNumber = 0; // Will be overridden by timestamp
            ImportFrameAtTimestamp(metadata, poseData, timestamp);
        }

        private static void ImportFrame(Metadata metadata, PoseData poseData, bool isImpact = false)
        {
            long timestamp = metadata.FirstTimeStamp + (poseData.FrameNumber * metadata.AverageTimeStampsPerFrame);
            ImportFrameAtTimestamp(metadata, poseData, timestamp, isImpact);
        }

        private static void ImportFrameAtTimestamp(Metadata metadata, PoseData poseData, long timestamp, bool isImpact = false)
        {
            AbstractDrawing drawing = CreateDrawing(metadata, timestamp, poseData);
            if (drawing == null)
                return;

            // Create a keyframe and add the drawing to it.
            Guid id = Guid.NewGuid();
            string title = isImpact ? "Impact" : null;
            Color color = isImpact ? Color.Red : Keyframe.DefaultColor;
            string comments = isImpact ? "Auto-detected impact frame (peak wrist velocity)" : "";
            List<AbstractDrawing> drawings = new List<AbstractDrawing> { drawing };
            Keyframe keyframe = new Keyframe(id, timestamp, title, color, comments, drawings, metadata);

            metadata.MergeInsertKeyframe(keyframe);
        }

        private static AbstractDrawing CreateDrawing(Metadata metadata, long timestamp, PoseData poseData)
        {
            if (poseData.KeyPoints == null || poseData.KeyPoints.Length != 17)
                return null;

            string toolName = "YOLOv8Pose";
            DrawingToolGenericPosture tool = ToolManager.Tools[toolName] as DrawingToolGenericPosture;
            if (tool == null)
            {
                log.ErrorFormat("Tool not found: {0}. Make sure yolov8_coco17.xml is in the DrawingTools/Custom folder.", toolName);
                return null;
            }

            GenericPosture posture = GenericPostureManager.Instanciate(tool.ToolId, true);
            ParsePosture(posture, poseData, metadata);

            DrawingGenericPosture drawing = new DrawingGenericPosture(
                tool.ToolId, 
                PointF.Empty, 
                posture, 
                timestamp, 
                metadata.AverageTimeStampsPerFrame, 
                ToolManager.GetDefaultStyleElements(toolName));
            
            drawing.Name = "Pose";

            // Configure fading - visible for ~5 frames then fade out before next pose
            drawing.InfosFading.UseDefault = false;
            drawing.InfosFading.ReferenceTimestamp = timestamp;
            drawing.InfosFading.AverageTimeStampsPerFrame = metadata.AverageTimeStampsPerFrame;
            drawing.InfosFading.AlwaysVisible = false;
            drawing.InfosFading.OpaqueFrames = 3;
            drawing.InfosFading.FadingFrames = 2;

            return drawing;
        }

        private static void ParsePosture(GenericPosture posture, PoseData poseData, Metadata metadata)
        {
            // Get the reference image size for denormalization
            Size imageSize = metadata.ImageSize;

            for (int i = 0; i < poseData.KeyPoints.Length && i < 17; i++)
            {
                var kp = poseData.KeyPoints[i];
                
                // Convert normalized coordinates (0-1) to pixel coordinates
                float x = kp.X * imageSize.Width;
                float y = kp.Y * imageSize.Height;

                posture.PointList[i] = new PointF(x, y);

                // Set visibility based on confidence
                if (options.ContainsKey(i) && posture.Options.ContainsKey(options[i]))
                {
                    posture.Options[options[i]].Value = kp.Confidence >= confidenceThreshold;
                }
            }
        }
    }
}
