using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Kinovea.PoseDetection
{
    /// <summary>
    /// Renders pose statistics overlay on video frame.
    /// Shows current, min/max, impact, and overall angles.
    /// </summary>
    public class PoseStatsRenderer
    {
        private Font titleFont;
        private Font dataFont;
        private Brush textBrush;
        private Brush bgBrush;
        private Brush currentBrush;
        private Brush impactBrush;
        private int padding = 10;

        public PoseStatsRenderer()
        {
            titleFont = new Font("Segoe UI", 10f, FontStyle.Bold);
            dataFont = new Font("Consolas", 9f, FontStyle.Regular);
            textBrush = new SolidBrush(Color.White);
            bgBrush = new SolidBrush(Color.FromArgb(180, 0, 0, 0));
            currentBrush = new SolidBrush(Color.FromArgb(255, 100, 200, 100));
            impactBrush = new SolidBrush(Color.FromArgb(255, 255, 100, 100));
        }

        /// <summary>
        /// Draw statistics overlay on the graphics context.
        /// </summary>
        /// <param name="g">Graphics context</param>
        /// <param name="stats">Pose statistics to display</param>
        /// <param name="width">Display width</param>
        /// <param name="height">Display height</param>
        /// <param name="centerPosition">Optional center position. If null, positions in top-right corner.</param>
        public void Draw(Graphics g, PoseStatistics stats, int width, int height, PointF? centerPosition = null)
        {
            if (stats == null)
                return;

            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

            // Calculate panel size
            float lineHeight = dataFont.GetHeight(g) + 2;
            float panelWidth = 280;
            float panelHeight = lineHeight * 6 + titleFont.GetHeight(g) + padding * 2;

            // Position based on centerPosition parameter
            float x, y;
            if (centerPosition.HasValue)
            {
                // Center around the specified point
                x = centerPosition.Value.X - panelWidth / 2;
                y = centerPosition.Value.Y - panelHeight / 2;
            }
            else
            {
                // Position in top-right corner
                x = width - panelWidth - padding;
                y = padding;
            }

            // Draw background
            RectangleF bgRect = new RectangleF(x, y, panelWidth, panelHeight);
            g.FillRectangle(bgBrush, bgRect);

            // Draw border
            using (var borderPen = new Pen(Color.FromArgb(200, 100, 100, 100), 1))
            {
                g.DrawRectangle(borderPen, x, y, panelWidth, panelHeight);
            }

            float textX = x + padding;
            float textY = y + padding;

            // Title
            g.DrawString("Pose Statistics", titleFont, textBrush, textX, textY);
            textY += titleFont.GetHeight(g) + 4;

            // Header row
            string header = String.Format("{0,-10} {1,8} {2,8} {3,8}", "", "Shoulder", "Hips", "L.Elbow");
            g.DrawString(header, dataFont, textBrush, textX, textY);
            textY += lineHeight;

            // Current row
            string currentRow = String.Format("{0,-10} {1,8} {2,8} {3,8}",
                "Current:",
                FormatAngle(stats.CurrentShoulderAngle),
                FormatAngle(stats.CurrentHipAngle),
                FormatAngle(stats.CurrentLeftElbowAngle));
            g.DrawString(currentRow, dataFont, currentBrush, textX, textY);
            textY += lineHeight;

            // Min row
            string minRow = String.Format("{0,-10} {1,8} {2,8} {3,8}",
                "Min:",
                FormatAngle(stats.ShoulderMin),
                FormatAngle(stats.HipMin),
                FormatAngle(stats.ElbowMin));
            g.DrawString(minRow, dataFont, textBrush, textX, textY);
            textY += lineHeight;

            // Max row
            string maxRow = String.Format("{0,-10} {1,8} {2,8} {3,8}",
                "Max:",
                FormatAngle(stats.ShoulderMax),
                FormatAngle(stats.HipMax),
                FormatAngle(stats.ElbowMax));
            g.DrawString(maxRow, dataFont, textBrush, textX, textY);
            textY += lineHeight;

            // Impact row
            string impactRow = String.Format("{0,-10} {1,8} {2,8} {3,8}",
                "Impact:",
                FormatAngle(stats.ShoulderAtImpact),
                FormatAngle(stats.HipAtImpact),
                FormatAngle(stats.ElbowAtImpact));
            g.DrawString(impactRow, dataFont, impactBrush, textX, textY);
            textY += lineHeight;

            // Overall row
            string overallRow = String.Format("{0,-10} {1,8} {2,8} {3,8}",
                "Overall:",
                FormatAngle(stats.OverallShoulderAngle),
                FormatAngle(stats.OverallHipAngle),
                FormatAngle(stats.OverallElbowAngle));
            g.DrawString(overallRow, dataFont, textBrush, textX, textY);
        }

        private string FormatAngle(float? angle)
        {
            if (!angle.HasValue)
                return "---";
            return String.Format("{0:+0.0;-0.0}°", angle.Value);
        }

        public void Dispose()
        {
            titleFont?.Dispose();
            dataFont?.Dispose();
            textBrush?.Dispose();
            bgBrush?.Dispose();
            currentBrush?.Dispose();
            impactBrush?.Dispose();
        }
    }
}
