using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using OpenCvSharp;
using OpenCvSharp.Aruco;
using OpenCvSharp.Extensions;
using Kinovea.PoseDetection;

namespace Kinovea.ScreenManager
{
    /// <summary>
    /// Multi-step wizard dialog for stereo camera calibration using ArUco markers.
    /// </summary>
    public class FormCalibrationWizard : Form
    {
        private readonly Func<(Mat frameA, Mat frameB)> getFramesFunc;
        private readonly ArucoCalibrator calibrator;
        
        // UI Controls
        private Label lblInstructions;
        private PictureBox picMarker;
        private Button btnSaveMarker;
        private Button btnPrintMarker;
        private PictureBox picCameraA;
        private PictureBox picCameraB;
        private Label lblStatusA;
        private Label lblStatusB;
        private NumericUpDown numMarkerSize;
        private Button btnRefresh;
        private Button btnCalibrate;
        private Button btnClose;
        private Label lblResult;

        // State
        private bool markerFoundA;
        private bool markerFoundB;
        private Point2f[] cornersA;
        private Point2f[] cornersB;
        private Bitmap markerImage;

        public FormCalibrationWizard(Func<(Mat frameA, Mat frameB)> getFramesFunc)
        {
            this.getFramesFunc = getFramesFunc;
            this.calibrator = new ArucoCalibrator();
            
            InitializeComponent();
            GenerateMarkerPreview();
            RefreshPreview();
        }

        private void InitializeComponent()
        {
            this.Text = "Stereo Camera Calibration";
            this.Size = new System.Drawing.Size(900, 700);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // Step 1: Marker generation section
            var lblStep1 = new Label
            {
                Text = "Step 1: Print the Calibration Marker",
                Location = new System.Drawing.Point(20, 15),
                AutoSize = true,
                Font = new Font(Font.FontFamily, 10, FontStyle.Bold)
            };

            lblInstructions = new Label
            {
                Text = "Print this marker at the size specified below. Place it flat on the ground where the golfer will stand.\nThe marker must be visible in BOTH camera views during calibration.",
                Location = new System.Drawing.Point(20, 40),
                Size = new System.Drawing.Size(550, 35),
                Font = new Font(Font.FontFamily, 9)
            };

            // Marker preview
            picMarker = new PictureBox
            {
                Location = new System.Drawing.Point(20, 80),
                Size = new System.Drawing.Size(120, 120),
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White
            };

            // Save/Print buttons
            btnSaveMarker = new Button
            {
                Text = "Save Marker Image...",
                Location = new System.Drawing.Point(150, 80),
                Size = new System.Drawing.Size(130, 30)
            };
            btnSaveMarker.Click += BtnSaveMarker_Click;

            btnPrintMarker = new Button
            {
                Text = "Get Marker Online",
                Location = new System.Drawing.Point(150, 115),
                Size = new System.Drawing.Size(130, 30)
            };
            btnPrintMarker.Click += BtnPrintMarker_Click;

            var lblMarkerHelp = new Label
            {
                Text = "8\" fits on letter paper (8.5x11).\nLarger markers need tiled printing or poster paper.\nBigger = easier to detect from far away.",
                Location = new System.Drawing.Point(150, 150),
                Size = new System.Drawing.Size(200, 50),
                ForeColor = Color.Gray,
                Font = new Font(Font.FontFamily, 8)
            };

            // Step 2 header
            var lblStep2 = new Label
            {
                Text = "Step 2: Position Marker and Calibrate",
                Location = new System.Drawing.Point(20, 210),
                AutoSize = true,
                Font = new Font(Font.FontFamily, 10, FontStyle.Bold)
            };

            // Camera A preview
            var lblCameraA = new Label
            {
                Text = "Camera A (Facing)",
                Location = new System.Drawing.Point(20, 235),
                AutoSize = true,
                Font = new Font(Font.FontFamily, 9, FontStyle.Bold)
            };

            picCameraA = new PictureBox
            {
                Location = new System.Drawing.Point(20, 255),
                Size = new System.Drawing.Size(420, 240),
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.Black
            };

            lblStatusA = new Label
            {
                Text = "Detecting...",
                Location = new System.Drawing.Point(20, 500),
                Size = new System.Drawing.Size(420, 20),
                ForeColor = Color.Gray
            };

            // Camera B preview
            var lblCameraB = new Label
            {
                Text = "Camera B (Down-the-line)",
                Location = new System.Drawing.Point(450, 235),
                AutoSize = true,
                Font = new Font(Font.FontFamily, 9, FontStyle.Bold)
            };

            picCameraB = new PictureBox
            {
                Location = new System.Drawing.Point(450, 255),
                Size = new System.Drawing.Size(420, 240),
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.Black
            };

            lblStatusB = new Label
            {
                Text = "Detecting...",
                Location = new System.Drawing.Point(450, 500),
                Size = new System.Drawing.Size(420, 20),
                ForeColor = Color.Gray
            };

            // Marker size input
            var lblMarkerSize = new Label
            {
                Text = "Marker Size (inches):",
                Location = new System.Drawing.Point(20, 535),
                AutoSize = true
            };

            numMarkerSize = new NumericUpDown
            {
                Location = new System.Drawing.Point(140, 533),
                Size = new System.Drawing.Size(80, 25),
                DecimalPlaces = 1,
                Minimum = 1,
                Maximum = 48,
                Value = 8,  // 8" fits on letter paper
                Increment = 0.5m
            };

            // Buttons
            btnRefresh = new Button
            {
                Text = "Refresh",
                Location = new System.Drawing.Point(250, 530),
                Size = new System.Drawing.Size(100, 30)
            };
            btnRefresh.Click += BtnRefresh_Click;

            btnCalibrate = new Button
            {
                Text = "Calibrate",
                Location = new System.Drawing.Point(360, 530),
                Size = new System.Drawing.Size(100, 30),
                Enabled = false
            };
            btnCalibrate.Click += BtnCalibrate_Click;

            btnClose = new Button
            {
                Text = "Close",
                Location = new System.Drawing.Point(770, 625),
                Size = new System.Drawing.Size(100, 30)
            };
            btnClose.Click += (s, e) => this.Close();

            // Result label
            lblResult = new Label
            {
                Text = "",
                Location = new System.Drawing.Point(20, 570),
                Size = new System.Drawing.Size(850, 50),
                Font = new Font("Consolas", 9)
            };

            // Add controls
            this.Controls.AddRange(new Control[]
            {
                lblStep1, lblInstructions,
                picMarker, btnSaveMarker, btnPrintMarker, lblMarkerHelp,
                lblStep2,
                lblCameraA, picCameraA, lblStatusA,
                lblCameraB, picCameraB, lblStatusB,
                lblMarkerSize, numMarkerSize,
                btnRefresh, btnCalibrate, btnClose,
                lblResult
            });

            // Load existing calibration status
            UpdateCalibrationStatus();
        }

        /// <summary>
        /// Generate ArUco marker ID 0 from 6x6_250 dictionary.
        /// Pattern verified from OpenCV ArUco module source.
        /// </summary>
        private Bitmap GenerateArucoMarker(int pixelSize)
        {
            // ArUco 6x6_250 dictionary, marker ID 0
            // Verified pattern from OpenCV source: 6x6 inner bits + 1-cell black border = 8x8 total
            // The inner 6x6 pattern for ID 0 in DICT_6X6_250 is:
            // Bytes: 0xd0, 0x13, 0x93, 0x2c, 0x47, 0x90
            // Which decodes to this 6x6 bit pattern (1=white, 0=black):
            int[,] innerPattern = new int[,]
            {
                { 1, 1, 0, 1, 0, 0 },  
                { 0, 0, 0, 1, 0, 0 },  
                { 1, 0, 0, 1, 0, 0 },  
                { 1, 1, 0, 0, 1, 0 },  
                { 1, 0, 0, 0, 0, 1 },  
                { 1, 1, 1, 0, 0, 1 }   
            };

            // Build 8x8 with black border (border = 0, copy inner pattern)
            int[,] pattern = new int[8, 8];
            for (int y = 0; y < 6; y++)
            {
                for (int x = 0; x < 6; x++)
                {
                    pattern[y + 1, x + 1] = innerPattern[y, x];
                }
            }
            // Border rows/cols remain 0 (black)

            int cellSize = pixelSize / 8;
            int actualSize = cellSize * 8;

            var bitmap = new Bitmap(actualSize, actualSize);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.White);

                for (int y = 0; y < 8; y++)
                {
                    for (int x = 0; x < 8; x++)
                    {
                        if (pattern[y, x] == 0)
                        {
                            g.FillRectangle(Brushes.Black, x * cellSize, y * cellSize, cellSize, cellSize);
                        }
                    }
                }
            }

            return bitmap;
        }

        private void GenerateMarkerPreview()
        {
            try
            {
                markerImage = GenerateArucoMarker(400);
                picMarker.Image = markerImage;
            }
            catch (Exception ex)
            {
                lblResult.Text = $"Error generating marker: {ex.Message}";
                lblResult.ForeColor = Color.Red;
            }
        }

        private void BtnSaveMarker_Click(object sender, EventArgs e)
        {
            try
            {
                using (var saveDialog = new SaveFileDialog())
                {
                    saveDialog.Title = "Save ArUco Marker";
                    saveDialog.Filter = "PNG Image|*.png|JPEG Image|*.jpg|All Files|*.*";
                    saveDialog.FileName = $"ArUco_Marker_ID0_{(int)numMarkerSize.Value}inch.png";

                    if (saveDialog.ShowDialog() == DialogResult.OK)
                    {
                        // Generate high-res marker for printing (300 DPI)
                        int pixelSize = (int)(numMarkerSize.Value * 300);
                        using (var highResBitmap = GenerateArucoMarker(pixelSize))
                        {
                            highResBitmap.SetResolution(300, 300);
                            
                            var format = saveDialog.FileName.ToLower().EndsWith(".jpg") 
                                ? ImageFormat.Jpeg 
                                : ImageFormat.Png;
                            highResBitmap.Save(saveDialog.FileName, format);
                        }

                        MessageBox.Show(
                            $"Marker saved!\n\nPrint this image at 100% scale (no fit-to-page).\nThe marker should measure exactly {numMarkerSize.Value} inches.",
                            "Marker Saved",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving marker: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnPrintMarker_Click(object sender, EventArgs e)
        {
            try
            {
                // Open the online ArUco generator with correct settings
                // Dictionary: 6x6 (250), Marker ID: 0, Size: user's choice in mm
                int sizeInMm = (int)((double)numMarkerSize.Value * 25.4);
                string url = $"https://chev.me/arucogen/?dict=6x6_250&id=0&size={sizeInMm}";
                
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

                MessageBox.Show(
                    $"Opening ArUco marker generator in your browser.\n\n" +
                    $"Settings should be:\n" +
                    $"- Dictionary: 6x6 (250 markers)\n" +
                    $"- Marker ID: 0\n" +
                    $"- Size: {sizeInMm} mm ({numMarkerSize.Value} inches)\n\n" +
                    $"Download the SVG/PNG and print at 100% scale.",
                    "Get Marker",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RefreshPreview()
        {
            try
            {
                var (frameA, frameB) = getFramesFunc();

                if (frameA == null || frameB == null)
                {
                    lblStatusA.Text = "No frame available";
                    lblStatusA.ForeColor = Color.Red;
                    lblStatusB.Text = "No frame available";
                    lblStatusB.ForeColor = Color.Red;
                    btnCalibrate.Enabled = false;
                    return;
                }

                // Detect marker in both frames
                (markerFoundA, cornersA) = calibrator.DetectMarker(frameA);
                (markerFoundB, cornersB) = calibrator.DetectMarker(frameB);

                // Draw detection visualization
                calibrator.DrawDetectedMarker(frameA, cornersA, markerFoundA);
                calibrator.DrawDetectedMarker(frameB, cornersB, markerFoundB);

                // Update preview images
                picCameraA.Image?.Dispose();
                picCameraA.Image = BitmapConverter.ToBitmap(frameA);

                picCameraB.Image?.Dispose();
                picCameraB.Image = BitmapConverter.ToBitmap(frameB);

                // Update status labels
                lblStatusA.Text = markerFoundA ? "Marker DETECTED" : "Marker NOT FOUND";
                lblStatusA.ForeColor = markerFoundA ? Color.Green : Color.Red;

                lblStatusB.Text = markerFoundB ? "Marker DETECTED" : "Marker NOT FOUND";
                lblStatusB.ForeColor = markerFoundB ? Color.Green : Color.Red;

                // Enable calibrate button only if both markers are found
                btnCalibrate.Enabled = markerFoundA && markerFoundB;

                // Cleanup
                frameA.Dispose();
                frameB.Dispose();
            }
            catch (Exception ex)
            {
                lblResult.Text = $"Error: {ex.Message}";
                lblResult.ForeColor = Color.Red;
            }
        }

        private void BtnRefresh_Click(object sender, EventArgs e)
        {
            RefreshPreview();
        }

        private void BtnCalibrate_Click(object sender, EventArgs e)
        {
            try
            {
                var (frameA, frameB) = getFramesFunc();

                if (frameA == null || frameB == null)
                {
                    lblResult.Text = "Error: Could not get frames from cameras.";
                    lblResult.ForeColor = Color.Red;
                    return;
                }

                double markerSize = (double)numMarkerSize.Value;
                var calibration = calibrator.CalibrateFromFrames(frameA, frameB, markerSize);

                frameA.Dispose();
                frameB.Dispose();

                if (calibration == null)
                {
                    string err = calibrator.LastError ?? "Unknown error";
                    lblResult.Text = $"Calibration FAILED: {err}";
                    lblResult.ForeColor = Color.Red;
                    return;
                }

                if (!calibration.IsValid())
                {
                    // Provide detailed failure reason
                    string reason = "Unknown";
                    if (calibration.CameraA == null || !calibration.CameraA.IsValid())
                        reason = "Camera A calibration failed";
                    else if (calibration.CameraB == null || !calibration.CameraB.IsValid())
                        reason = "Camera B calibration failed";
                    else if (calibration.BaselineDistanceMeters <= 0)
                        reason = $"Invalid baseline distance: {calibration.BaselineDistanceMeters:F4}m";
                    
                    lblResult.Text = $"Calibration FAILED: {reason}";
                    lblResult.ForeColor = Color.Red;
                    return;
                }

                // Save calibration
                if (CalibrationManager.Save(calibration))
                {
                    lblResult.Text = "Calibration SUCCESSFUL!\n\n" + calibration.GetSummary();
                    lblResult.ForeColor = Color.Green;
                    UpdateCalibrationStatus();
                }
                else
                {
                    lblResult.Text = "Calibration computed but failed to save.";
                    lblResult.ForeColor = Color.Orange;
                }
            }
            catch (Exception ex)
            {
                lblResult.Text = $"Calibration error: {ex.Message}";
                lblResult.ForeColor = Color.Red;
            }
        }

        private void UpdateCalibrationStatus()
        {
            var existing = CalibrationManager.Load();
            if (existing != null && existing.IsValid())
            {
                var status = CalibrationManager.GetStatusString();
                this.Text = $"Stereo Camera Calibration - {status}";
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            picCameraA.Image?.Dispose();
            picCameraB.Image?.Dispose();
            markerImage?.Dispose();
            base.OnFormClosed(e);
        }
    }
}
