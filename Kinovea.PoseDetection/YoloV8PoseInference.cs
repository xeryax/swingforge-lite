using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Kinovea.PoseDetection
{
    /// <summary>
    /// YOLOv8-Pose inference wrapper using ONNX Runtime.
    /// </summary>
    public class YoloV8PoseInference : IDisposable
    {
        private InferenceSession session;
        private string inputName;
        private int inputWidth = 640;
        private int inputHeight = 640;
        private bool disposed = false;

        /// <summary>
        /// Confidence threshold for detection.
        /// </summary>
        public float ConfidenceThreshold { get; set; } = 0.25f;

        /// <summary>
        /// IOU threshold for NMS.
        /// </summary>
        public float IouThreshold { get; set; } = 0.45f;

        /// <summary>
        /// Whether the model is loaded and ready.
        /// </summary>
        public bool IsLoaded => session != null;

        /// <summary>
        /// Initialize the inference engine with a model file.
        /// </summary>
        public bool LoadModel(string modelPath)
        {
            try
            {
                if (!File.Exists(modelPath))
                    return false;

                var options = new SessionOptions();
                options.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;

                // Try to use GPU if available
                try
                {
                    options.AppendExecutionProvider_CUDA();
                }
                catch
                {
                    // GPU not available, use CPU
                }

                session = new InferenceSession(modelPath, options);
                inputName = session.InputMetadata.Keys.First();

                // Get input dimensions
                var inputMeta = session.InputMetadata[inputName];
                if (inputMeta.Dimensions.Length >= 4)
                {
                    inputHeight = inputMeta.Dimensions[2];
                    inputWidth = inputMeta.Dimensions[3];
                }

                return true;
            }
            catch (Exception)
            {
                session = null;
                return false;
            }
        }

        /// <summary>
        /// Run inference on a bitmap image.
        /// </summary>
        public PoseData Detect(Bitmap image)
        {
            if (session == null || image == null)
                return null;

            try
            {
                // Preprocess image
                var inputTensor = PreprocessImage(image);

                // Run inference
                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor(inputName, inputTensor)
                };

                using (var results = session.Run(inputs))
                {
                    var output = results.First().AsTensor<float>();
                    return PostprocessOutput(output, image.Width, image.Height);
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Preprocess image for YOLOv8 input.
        /// </summary>
        private DenseTensor<float> PreprocessImage(Bitmap image)
        {
            // Resize to model input size
            using (var resized = new Bitmap(inputWidth, inputHeight))
            using (var g = Graphics.FromImage(resized))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Bilinear;
                g.DrawImage(image, 0, 0, inputWidth, inputHeight);

                var tensor = new DenseTensor<float>(new[] { 1, 3, inputHeight, inputWidth });

                var bmpData = resized.LockBits(
                    new Rectangle(0, 0, inputWidth, inputHeight),
                    ImageLockMode.ReadOnly,
                    PixelFormat.Format24bppRgb);

                try
                {
                    int stride = bmpData.Stride;
                    byte[] pixelData = new byte[stride * inputHeight];
                    Marshal.Copy(bmpData.Scan0, pixelData, 0, pixelData.Length);

                    for (int y = 0; y < inputHeight; y++)
                    {
                        for (int x = 0; x < inputWidth; x++)
                        {
                            int offset = y * stride + x * 3;
                            // BGR to RGB and normalize to 0-1
                            tensor[0, 0, y, x] = pixelData[offset + 2] / 255f; // R
                            tensor[0, 1, y, x] = pixelData[offset + 1] / 255f; // G
                            tensor[0, 2, y, x] = pixelData[offset + 0] / 255f; // B
                        }
                    }
                }
                finally
                {
                    resized.UnlockBits(bmpData);
                }

                return tensor;
            }
        }

        /// <summary>
        /// Parse YOLOv8-pose output to extract best detection.
        /// Output shape: [1, 56, 8400] where 56 = 4 (bbox) + 1 (conf) + 17*3 (keypoints x,y,conf)
        /// </summary>
        private PoseData PostprocessOutput(Tensor<float> output, int originalWidth, int originalHeight)
        {
            var dims = output.Dimensions.ToArray();
            
            // YOLOv8-pose output is [1, 56, 8400]
            int numDetections = dims[2];
            int numFeatures = dims[1];

            float bestConfidence = ConfidenceThreshold;
            int bestIdx = -1;

            // Find best detection
            for (int i = 0; i < numDetections; i++)
            {
                float conf = output[0, 4, i];
                if (conf > bestConfidence)
                {
                    bestConfidence = conf;
                    bestIdx = i;
                }
            }

            if (bestIdx < 0)
                return null;

            var pose = new PoseData
            {
                DetectionConfidence = bestConfidence
            };

            // Extract bounding box (center x, center y, width, height)
            float cx = output[0, 0, bestIdx];
            float cy = output[0, 1, bestIdx];
            float w = output[0, 2, bestIdx];
            float h = output[0, 3, bestIdx];

            // Convert to normalized coordinates
            pose.BoundingBox = new float[]
            {
                (cx - w / 2) / inputWidth,
                (cy - h / 2) / inputHeight,
                w / inputWidth,
                h / inputHeight
            };

            // Extract keypoints (17 keypoints, each with x, y, confidence)
            // YOLOv8-pose outputs keypoint confidence values (typically already in 0-1 range)
            float maxRawConf = 0;
            float minRawConf = float.MaxValue;
            for (int k = 0; k < 17; k++)
            {
                int baseIdx = 5 + k * 3;
                float kpX = output[0, baseIdx, bestIdx] / inputWidth;
                float kpY = output[0, baseIdx + 1, bestIdx] / inputHeight;
                float kpConfRaw = output[0, baseIdx + 2, bestIdx];
                
                // Track raw confidence range for diagnostics
                if (kpConfRaw > maxRawConf) maxRawConf = kpConfRaw;
                if (kpConfRaw < minRawConf) minRawConf = kpConfRaw;
                
                // YOLOv8-pose typically outputs confidence in 0-1 range already
                // But some models might output logits that need sigmoid
                float kpConf = kpConfRaw;
                if (kpConfRaw < -5.0f || kpConfRaw > 5.0f)
                {
                    // Likely logit form, apply sigmoid: 1 / (1 + exp(-x))
                    kpConf = 1.0f / (1.0f + (float)Math.Exp(-kpConfRaw));
                }
                // Clamp to [0, 1] range
                kpConf = Math.Max(0.0f, Math.Min(1.0f, kpConf));

                pose.KeyPoints[k] = new KeyPoint(k, kpX, kpY, kpConf);
            }
            
            // Note: Raw confidence values are logged in DualPlayerController if they seem suspiciously low

            // Calculate angles
            AngleCalculator.CalculateAllAngles(pose, ConfidenceThreshold);

            return pose;
        }

        public void Dispose()
        {
            if (!disposed)
            {
                session?.Dispose();
                session = null;
                disposed = true;
            }
        }
    }
}
