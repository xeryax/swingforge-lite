#region License
/*
Copyright © Joan Charmant 2012.
Kinovea is distributed under the GPL v2.
*/
#endregion

using System;
using System.Windows.Forms;
using Kinovea.Services;

namespace Kinovea.Root
{
    public partial class FormCloudSyncDetails : Form
    {
        public FormCloudSyncDetails()
        {
            InitializeComponent();
            RefreshStats();
        }

        private void RefreshStats()
        {
            int success = CloudSyncStats.TotalSuccessfulUploads;
            int failed = CloudSyncStats.TotalFailedUploads;
            double speed = CloudSyncStats.LastUploadSpeedBytesPerSec;
            var lastBatch = CloudSyncStats.LastBatchTimeUtc;

            lblSuccessfulUploads.Text = success.ToString();
            lblFailedUploads.Text = failed.ToString();
            if (speed >= 1024 * 1024)
                lblUploadSpeed.Text = string.Format("{0:F2} MB/s", speed / (1024 * 1024));
            else if (speed >= 1024)
                lblUploadSpeed.Text = string.Format("{0:F2} KB/s", speed / 1024);
            else
                lblUploadSpeed.Text = string.Format("{0:F0} B/s", speed);
            lblLastBatch.Text = lastBatch.HasValue ? lastBatch.Value.ToLocalTime().ToString("g") : "(never)";
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}
