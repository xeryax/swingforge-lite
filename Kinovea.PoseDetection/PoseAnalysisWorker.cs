using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using OpenCvSharp;

namespace Kinovea.PoseDetection
{
    /// <summary>
    /// Background worker for processing video frames and detecting poses.
    /// </summary>
    public class PoseAnalysisWorker : IDisposable
    {
        private BackgroundWorker worker;
        private YoloV8PoseInference inference;
        private bool disposed = false;
        private volatile bool cancelRequested = false;

        /// <summary>
        /// Progress update event (percentage complete).
        /// </summary>
        public event EventHandler<int> ProgressChanged;

        /// <summary>
        /// Analysis complete event.
        /// </summary>
        public event EventHandler<PoseCache> Completed;

        /// <summary>
        /// Error event.
        /// </summary>
        public event EventHandler<string> Error;

        /// <summary>
        /// Sample rate (process every Nth frame).
        /// </summary>
        public int SampleRate { get; set; } = 5;

        /// <summary>
        /// Confidence threshold for detection.
        /// </summary>
        public float ConfidenceThreshold { get; set; } = 0.25f;

        /// <summary>
        /// Path to the ONNX model file.
        /// </summary>
        public string ModelPath { get; set; }

        /// <summary>
        /// Whether the worker is currently running.
        /// </summary>
        public bool IsBusy => worker?.IsBusy ?? false;

        public PoseAnalysisWorker()
        {
            worker = new BackgroundWorker();
            worker.WorkerReportsProgress = true;
            worker.WorkerSupportsCancellation = true;
            worker.DoWork += Worker_DoWork;
            worker.ProgressChanged += Worker_ProgressChanged;
            worker.RunWorkerCompleted += Worker_RunWorkerCompleted;
        }

        /// <summary>
        /// Start analyzing a video file.
        /// </summary>
        public void StartAnalysis(string videoPath)
        {
            if (worker.IsBusy)
                return;

            cancelRequested = false;
            worker.RunWorkerAsync(videoPath);
        }

        /// <summary>
        /// Cancel the current analysis.
        /// </summary>
        public void Cancel()
        {
            cancelRequested = true;
            worker.CancelAsync();
        }

        private void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            string videoPath = (string)e.Argument;
            var cache = new PoseCache();

            try
            {
                // Initialize inference
                inference = new YoloV8PoseInference();
                inference.ConfidenceThreshold = ConfidenceThreshold;

                if (!inference.LoadModel(ModelPath))
                {
                    e.Result = new Exception("Failed to load model: " + ModelPath);
                    return;
                }

                // Open video with OpenCV
                using (var capture = new VideoCapture(videoPath))
                {
                    if (!capture.IsOpened())
                    {
                        e.Result = new Exception("Failed to open video: " + videoPath);
                        return;
                    }

                    int totalFrames = (int)capture.Get(VideoCaptureProperties.FrameCount);
                    double fps = capture.Get(VideoCaptureProperties.Fps);
                    double durationMs = (totalFrames / fps) * 1000.0;

                    cache.VideoPath = videoPath;
                    cache.TotalFrames = totalFrames;
                    cache.VideoDurationMs = durationMs;
                    cache.SampleRate = SampleRate;

                    int processedFrames = 0;
                    int lastProgress = 0;

                    using (var frame = new Mat())
                    {
                        for (int frameNum = 0; frameNum < totalFrames; frameNum += SampleRate)
                        {
                            if (cancelRequested || worker.CancellationPending)
                            {
                                e.Cancel = true;
                                return;
                            }

                            // Seek to frame
                            capture.Set(VideoCaptureProperties.PosFrames, frameNum);
                            if (!capture.Read(frame) || frame.Empty())
                                continue;

                            // Convert to Bitmap
                            using (var bitmap = MatToBitmap(frame))
                            {
                                if (bitmap == null)
                                    continue;

                                // Run inference
                                var pose = inference.Detect(bitmap);
                                if (pose != null)
                                {
                                    pose.FrameNumber = frameNum;
                                    pose.TimestampMs = (frameNum / fps) * 1000.0;
                                    cache.Frames.Add(pose);
                                }
                            }

                            processedFrames++;

                            // Report progress
                            int progress = (int)((frameNum * 100.0) / totalFrames);
                            if (progress > lastProgress)
                            {
                                lastProgress = progress;
                                worker.ReportProgress(progress);
                            }
                        }
                    }
                }

                // Apply Kalman filter smoothing to reduce jitter
                var smoother = new PoseSmoother();
                var smoothedFrames = smoother.SmoothSequenceBidirectional(cache.Frames);
                cache.Frames = smoothedFrames;

                // Save cache
                cache.Save(videoPath);
                e.Result = cache;
            }
            catch (Exception ex)
            {
                e.Result = ex;
            }
            finally
            {
                inference?.Dispose();
                inference = null;
            }
        }

        private void Worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            ProgressChanged?.Invoke(this, e.ProgressPercentage);
        }

        private void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Cancelled)
            {
                Error?.Invoke(this, "Analysis cancelled.");
            }
            else if (e.Result is Exception ex)
            {
                Error?.Invoke(this, ex.Message);
            }
            else if (e.Result is PoseCache cache)
            {
                Completed?.Invoke(this, cache);
            }
        }

        /// <summary>
        /// Convert OpenCV Mat to System.Drawing.Bitmap.
        /// </summary>
        private Bitmap MatToBitmap(Mat mat)
        {
            if (mat == null || mat.Empty())
                return null;

            try
            {
                // Convert to BGR if necessary
                Mat bgr = mat;
                if (mat.Channels() == 4)
                {
                    bgr = new Mat();
                    Cv2.CvtColor(mat, bgr, ColorConversionCodes.BGRA2BGR);
                }
                else if (mat.Channels() == 1)
                {
                    bgr = new Mat();
                    Cv2.CvtColor(mat, bgr, ColorConversionCodes.GRAY2BGR);
                }

                var bitmap = new Bitmap(bgr.Width, bgr.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                var bmpData = bitmap.LockBits(
                    new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                    System.Drawing.Imaging.ImageLockMode.WriteOnly,
                    bitmap.PixelFormat);

                try
                {
                    int srcStride = (int)bgr.Step();
                    int dstStride = bmpData.Stride;
                    
                    unsafe
                    {
                        byte* srcPtr = (byte*)bgr.DataPointer;
                        byte* dstPtr = (byte*)bmpData.Scan0;

                        for (int y = 0; y < bitmap.Height; y++)
                        {
                            Buffer.MemoryCopy(
                                srcPtr + y * srcStride,
                                dstPtr + y * dstStride,
                                bitmap.Width * 3,
                                bitmap.Width * 3);
                        }
                    }
                }
                finally
                {
                    bitmap.UnlockBits(bmpData);
                }

                if (bgr != mat)
                    bgr.Dispose();

                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        public void Dispose()
        {
            if (!disposed)
            {
                Cancel();
                worker?.Dispose();
                inference?.Dispose();
                disposed = true;
            }
        }
    }
}
