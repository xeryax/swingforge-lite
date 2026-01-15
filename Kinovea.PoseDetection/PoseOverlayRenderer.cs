using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Kinovea.PoseDetection
{
    /// <summary>
    /// Renders pose skeleton overlay on video frames.
    /// </summary>
    public class PoseOverlayRenderer
    {
        // Skeleton connections (COCO format)
        private static readonly int[][] SkeletonConnections = new int[][]
        {
            new[] { 0, 1 },   // nose -> left_eye
            new[] { 0, 2 },   // nose -> right_eye
            new[] { 1, 3 },   // left_eye -> left_ear
            new[] { 2, 4 },   // right_eye -> right_ear
            new[] { 5, 6 },   // left_shoulder -> right_shoulder
            new[] { 5, 7 },   // left_shoulder -> left_elbow
            new[] { 7, 9 },   // left_elbow -> left_wrist
            new[] { 6, 8 },   // right_shoulder -> right_elbow
            new[] { 8, 10 },  // right_elbow -> right_wrist
            new[] { 5, 11 },  // left_shoulder -> left_hip
            new[] { 6, 12 },  // right_shoulder -> right_hip
            new[] { 11, 12 }, // left_hip -> right_hip
            new[] { 11, 13 }, // left_hip -> left_knee
            new[] { 13, 15 }, // left_knee -> left_ankle
            new[] { 12, 14 }, // right_hip -> right_knee
            new[] { 14, 16 }, // right_knee -> right_ankle
        };

        // Colors for different body parts
        private static readonly Color[] KeypointColors = new Color[]
        {
            Color.Red,        // 0: nose
            Color.Orange,     // 1: left_eye
            Color.Orange,     // 2: right_eye
            Color.Yellow,     // 3: left_ear
            Color.Yellow,     // 4: right_ear
            Color.Lime,       // 5: left_shoulder
            Color.Lime,       // 6: right_shoulder
            Color.Cyan,       // 7: left_elbow
            Color.Cyan,       // 8: right_elbow
            Color.Blue,       // 9: left_wrist
            Color.Blue,       // 10: right_wrist
            Color.Magenta,    // 11: left_hip
            Color.Magenta,    // 12: right_hip
            Color.Purple,     // 13: left_knee
            Color.Purple,     // 14: right_knee
            Color.Pink,       // 15: left_ankle
            Color.Pink,       // 16: right_ankle
        };

        /// <summary>
        /// Confidence threshold for drawing keypoints.
        /// </summary>
        public float ConfidenceThreshold { get; set; } = 0.25f;

        /// <summary>
        /// Keypoint circle radius.
        /// </summary>
        public int KeypointRadius { get; set; } = 5;

        /// <summary>
        /// Skeleton line thickness.
        /// </summary>
        public int LineThickness { get; set; } = 2;

        /// <summary>
        /// Whether to draw keypoint labels.
        /// </summary>
        public bool ShowLabels { get; set; } = false;

        /// <summary>
        /// Whether to draw angle values.
        /// </summary>
        public bool ShowAngles { get; set; } = true;

        /// <summary>
        /// Draw pose overlay on a graphics context.
        /// </summary>
        public void Draw(Graphics g, PoseData pose, int displayWidth, int displayHeight)
        {
            if (pose == null || pose.KeyPoints == null)
                return;

            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Draw skeleton connections first (behind keypoints)
            DrawSkeleton(g, pose, displayWidth, displayHeight);

            // Draw keypoints
            DrawKeypoints(g, pose, displayWidth, displayHeight);

            // Draw angles if enabled
            if (ShowAngles)
            {
                DrawAngles(g, pose, displayWidth, displayHeight);
            }
        }

        private void DrawSkeleton(Graphics g, PoseData pose, int width, int height)
        {
            using (var pen = new Pen(Color.White, LineThickness))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;

                foreach (var connection in SkeletonConnections)
                {
                    var kp1 = pose.KeyPoints[connection[0]];
                    var kp2 = pose.KeyPoints[connection[1]];

                    if (!kp1.IsValid(ConfidenceThreshold) || !kp2.IsValid(ConfidenceThreshold))
                        continue;

                    float x1 = kp1.X * width;
                    float y1 = kp1.Y * height;
                    float x2 = kp2.X * width;
                    float y2 = kp2.Y * height;

                    // Use average color of endpoints
                    var color = BlendColors(KeypointColors[connection[0]], KeypointColors[connection[1]]);
                    pen.Color = color;

                    g.DrawLine(pen, x1, y1, x2, y2);
                }
            }
        }

        private void DrawKeypoints(Graphics g, PoseData pose, int width, int height)
        {
            for (int i = 0; i < pose.KeyPoints.Length; i++)
            {
                var kp = pose.KeyPoints[i];
                if (!kp.IsValid(ConfidenceThreshold))
                    continue;

                float x = kp.X * width;
                float y = kp.Y * height;

                // Draw filled circle
                using (var brush = new SolidBrush(KeypointColors[i]))
                {
                    g.FillEllipse(brush, 
                        x - KeypointRadius, 
                        y - KeypointRadius, 
                        KeypointRadius * 2, 
                        KeypointRadius * 2);
                }

                // Draw outline
                using (var pen = new Pen(Color.White, 1))
                {
                    g.DrawEllipse(pen, 
                        x - KeypointRadius, 
                        y - KeypointRadius, 
                        KeypointRadius * 2, 
                        KeypointRadius * 2);
                }

                // Draw label if enabled
                if (ShowLabels)
                {
                    using (var font = new Font("Arial", 8))
                    using (var brush = new SolidBrush(Color.White))
                    {
                        g.DrawString(kp.Name, font, brush, x + KeypointRadius + 2, y - 6);
                    }
                }
            }
        }

        private void DrawAngles(Graphics g, PoseData pose, int width, int height)
        {
            using (var font = new Font("Arial", 10, FontStyle.Bold))
            using (var brush = new SolidBrush(Color.Yellow))
            using (var bgBrush = new SolidBrush(Color.FromArgb(128, 0, 0, 0)))
            {
                int y = 10;
                int lineHeight = 18;

                // Draw angle values in top-left corner
                if (pose.ShoulderAngle.HasValue)
                {
                    string text = $"Shoulder: {pose.ShoulderAngle.Value:F1}°";
                    DrawTextWithBackground(g, text, 10, y, font, brush, bgBrush);
                    y += lineHeight;
                }

                if (pose.WaistAngle.HasValue)
                {
                    string text = $"Waist: {pose.WaistAngle.Value:F1}°";
                    DrawTextWithBackground(g, text, 10, y, font, brush, bgBrush);
                    y += lineHeight;
                }

                if (pose.SpineAngle.HasValue)
                {
                    string text = $"Spine: {pose.SpineAngle.Value:F1}°";
                    DrawTextWithBackground(g, text, 10, y, font, brush, bgBrush);
                    y += lineHeight;
                }

                if (pose.LeftElbowAngle.HasValue)
                {
                    string text = $"L Elbow: {pose.LeftElbowAngle.Value:F1}°";
                    DrawTextWithBackground(g, text, 10, y, font, brush, bgBrush);
                    y += lineHeight;
                }

                if (pose.RightElbowAngle.HasValue)
                {
                    string text = $"R Elbow: {pose.RightElbowAngle.Value:F1}°";
                    DrawTextWithBackground(g, text, 10, y, font, brush, bgBrush);
                    y += lineHeight;
                }
            }
        }

        private void DrawTextWithBackground(Graphics g, string text, float x, float y, 
            Font font, Brush textBrush, Brush bgBrush)
        {
            var size = g.MeasureString(text, font);
            g.FillRectangle(bgBrush, x - 2, y - 1, size.Width + 4, size.Height + 2);
            g.DrawString(text, font, textBrush, x, y);
        }

        private Color BlendColors(Color c1, Color c2)
        {
            return Color.FromArgb(
                (c1.A + c2.A) / 2,
                (c1.R + c2.R) / 2,
                (c1.G + c2.G) / 2,
                (c1.B + c2.B) / 2);
        }
    }
}
