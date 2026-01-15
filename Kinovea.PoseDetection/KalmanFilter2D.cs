using System;
using System.Drawing;

namespace Kinovea.PoseDetection
{
    /// <summary>
    /// 2D Kalman filter for smoothing keypoint positions.
    /// Tracks position (x, y) and velocity (vx, vy) for each point.
    /// </summary>
    public class KalmanFilter2D
    {
        // State vector: [x, y, vx, vy]
        private float x, y;     // Position estimate
        private float vx, vy;   // Velocity estimate

        // Error covariance matrix (4x4 simplified to relevant terms)
        private float pX, pY;       // Position variance
        private float pVx, pVy;     // Velocity variance
        private float pXVx, pYVy;   // Position-velocity covariance

        // Filter parameters
        private float processNoise;
        private float measurementNoise;

        private bool initialized = false;

        /// <summary>
        /// Create a new Kalman filter for 2D point tracking.
        /// </summary>
        /// <param name="processNoise">Process noise (lower = smoother, higher = more responsive). Default: 0.01</param>
        /// <param name="measurementNoise">Measurement noise (higher = trust predictions more). Default: 0.1</param>
        public KalmanFilter2D(float processNoise = 0.01f, float measurementNoise = 0.1f)
        {
            this.processNoise = processNoise;
            this.measurementNoise = measurementNoise;
            Reset();
        }

        /// <summary>
        /// Reset the filter to uninitialized state.
        /// </summary>
        public void Reset()
        {
            initialized = false;
            x = y = 0;
            vx = vy = 0;
            pX = pY = 1.0f;
            pVx = pVy = 1.0f;
            pXVx = pYVy = 0;
        }

        /// <summary>
        /// Predict the next state based on constant velocity model.
        /// </summary>
        /// <param name="dt">Time delta between frames (typically 1.0 for frame-based)</param>
        public void Predict(float dt = 1.0f)
        {
            if (!initialized)
                return;

            // State prediction: x = x + vx*dt
            x += vx * dt;
            y += vy * dt;

            // Covariance prediction (simplified)
            pX += 2 * pXVx * dt + pVx * dt * dt + processNoise;
            pY += 2 * pYVy * dt + pVy * dt * dt + processNoise;
            pXVx += pVx * dt;
            pYVy += pVy * dt;
            pVx += processNoise;
            pVy += processNoise;
        }

        /// <summary>
        /// Update the filter with a new measurement.
        /// </summary>
        /// <param name="measuredX">Measured X position (normalized 0-1)</param>
        /// <param name="measuredY">Measured Y position (normalized 0-1)</param>
        /// <param name="confidence">Detection confidence (0-1), affects measurement noise</param>
        /// <returns>Smoothed position</returns>
        public PointF Update(float measuredX, float measuredY, float confidence = 1.0f)
        {
            // Initialize on first measurement
            if (!initialized)
            {
                x = measuredX;
                y = measuredY;
                vx = vy = 0;
                pX = pY = measurementNoise;
                pVx = pVy = measurementNoise;
                pXVx = pYVy = 0;
                initialized = true;
                return new PointF(x, y);
            }

            // Adjust measurement noise based on confidence
            // Low confidence = high noise = trust prediction more
            float adjustedNoise = measurementNoise / Math.Max(confidence, 0.1f);

            // Kalman gain for position
            float kX = pX / (pX + adjustedNoise);
            float kY = pY / (pY + adjustedNoise);

            // Also update velocity based on position innovation
            float kVx = pXVx / (pX + adjustedNoise);
            float kVy = pYVy / (pY + adjustedNoise);

            // Innovation (measurement residual)
            float innovX = measuredX - x;
            float innovY = measuredY - y;

            // State update
            x += kX * innovX;
            y += kY * innovY;
            vx += kVx * innovX;
            vy += kVy * innovY;

            // Covariance update (simplified Joseph form)
            float oneMinusKx = 1 - kX;
            float oneMinusKy = 1 - kY;
            pX *= oneMinusKx;
            pY *= oneMinusKy;
            pXVx *= oneMinusKx;
            pYVy *= oneMinusKy;

            return new PointF(x, y);
        }

        /// <summary>
        /// Get the current estimated position without updating.
        /// </summary>
        public PointF CurrentPosition => new PointF(x, y);

        /// <summary>
        /// Get the current estimated velocity.
        /// </summary>
        public PointF CurrentVelocity => new PointF(vx, vy);

        /// <summary>
        /// Whether the filter has been initialized with at least one measurement.
        /// </summary>
        public bool IsInitialized => initialized;
    }
}
