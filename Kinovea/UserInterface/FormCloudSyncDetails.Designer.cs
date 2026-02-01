#region License
/*
Copyright © Joan Charmant 2012.
Kinovea is distributed under the GPL v2.
*/
#endregion

namespace Kinovea.Root
{
    partial class FormCloudSyncDetails
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null)
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.lblSuccessfulLabel = new System.Windows.Forms.Label();
            this.lblSuccessfulUploads = new System.Windows.Forms.Label();
            this.lblFailedLabel = new System.Windows.Forms.Label();
            this.lblFailedUploads = new System.Windows.Forms.Label();
            this.lblSpeedLabel = new System.Windows.Forms.Label();
            this.lblUploadSpeed = new System.Windows.Forms.Label();
            this.lblLastBatchLabel = new System.Windows.Forms.Label();
            this.lblLastBatch = new System.Windows.Forms.Label();
            this.btnOK = new System.Windows.Forms.Button();
            this.SuspendLayout();
            //
            // lblSuccessfulLabel
            //
            this.lblSuccessfulLabel.AutoSize = true;
            this.lblSuccessfulLabel.Location = new System.Drawing.Point(20, 20);
            this.lblSuccessfulLabel.Name = "lblSuccessfulLabel";
            this.lblSuccessfulLabel.Size = new System.Drawing.Size(100, 13);
            this.lblSuccessfulLabel.TabIndex = 0;
            this.lblSuccessfulLabel.Text = "Successful uploads:";
            //
            // lblSuccessfulUploads
            //
            this.lblSuccessfulUploads.AutoSize = true;
            this.lblSuccessfulUploads.Location = new System.Drawing.Point(140, 20);
            this.lblSuccessfulUploads.Name = "lblSuccessfulUploads";
            this.lblSuccessfulUploads.Size = new System.Drawing.Size(13, 13);
            this.lblSuccessfulUploads.TabIndex = 1;
            this.lblSuccessfulUploads.Text = "0";
            //
            // lblFailedLabel
            //
            this.lblFailedLabel.AutoSize = true;
            this.lblFailedLabel.Location = new System.Drawing.Point(20, 45);
            this.lblFailedLabel.Name = "lblFailedLabel";
            this.lblFailedLabel.Size = new System.Drawing.Size(73, 13);
            this.lblFailedLabel.TabIndex = 2;
            this.lblFailedLabel.Text = "Failed uploads:";
            //
            // lblFailedUploads
            //
            this.lblFailedUploads.AutoSize = true;
            this.lblFailedUploads.Location = new System.Drawing.Point(140, 45);
            this.lblFailedUploads.Name = "lblFailedUploads";
            this.lblFailedUploads.Size = new System.Drawing.Size(13, 13);
            this.lblFailedUploads.TabIndex = 3;
            this.lblFailedUploads.Text = "0";
            //
            // lblSpeedLabel
            //
            this.lblSpeedLabel.AutoSize = true;
            this.lblSpeedLabel.Location = new System.Drawing.Point(20, 70);
            this.lblSpeedLabel.Name = "lblSpeedLabel";
            this.lblSpeedLabel.Size = new System.Drawing.Size(79, 13);
            this.lblSpeedLabel.TabIndex = 4;
            this.lblSpeedLabel.Text = "Last upload speed:";
            //
            // lblUploadSpeed
            //
            this.lblUploadSpeed.AutoSize = true;
            this.lblUploadSpeed.Location = new System.Drawing.Point(140, 70);
            this.lblUploadSpeed.Name = "lblUploadSpeed";
            this.lblUploadSpeed.Size = new System.Drawing.Size(28, 13);
            this.lblUploadSpeed.TabIndex = 5;
            this.lblUploadSpeed.Text = "0 B/s";
            //
            // lblLastBatchLabel
            //
            this.lblLastBatchLabel.AutoSize = true;
            this.lblLastBatchLabel.Location = new System.Drawing.Point(20, 95);
            this.lblLastBatchLabel.Name = "lblLastBatchLabel";
            this.lblLastBatchLabel.Size = new System.Drawing.Size(60, 13);
            this.lblLastBatchLabel.TabIndex = 6;
            this.lblLastBatchLabel.Text = "Last batch:";
            //
            // lblLastBatch
            //
            this.lblLastBatch.AutoSize = true;
            this.lblLastBatch.Location = new System.Drawing.Point(140, 95);
            this.lblLastBatch.Name = "lblLastBatch";
            this.lblLastBatch.Size = new System.Drawing.Size(38, 13);
            this.lblLastBatch.TabIndex = 7;
            this.lblLastBatch.Text = "(never)";
            //
            // btnOK
            //
            this.btnOK.Location = new System.Drawing.Point(180, 130);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(75, 25);
            this.btnOK.TabIndex = 8;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            //
            // FormCloudSyncDetails
            //
            this.AcceptButton = this.btnOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(284, 171);
            this.Controls.Add(this.lblSuccessfulLabel);
            this.Controls.Add(this.lblSuccessfulUploads);
            this.Controls.Add(this.lblFailedLabel);
            this.Controls.Add(this.lblFailedUploads);
            this.Controls.Add(this.lblSpeedLabel);
            this.Controls.Add(this.lblUploadSpeed);
            this.Controls.Add(this.lblLastBatchLabel);
            this.Controls.Add(this.lblLastBatch);
            this.Controls.Add(this.btnOK);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FormCloudSyncDetails";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Cloud sync details";
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private System.Windows.Forms.Label lblSuccessfulLabel;
        private System.Windows.Forms.Label lblSuccessfulUploads;
        private System.Windows.Forms.Label lblFailedLabel;
        private System.Windows.Forms.Label lblFailedUploads;
        private System.Windows.Forms.Label lblSpeedLabel;
        private System.Windows.Forms.Label lblUploadSpeed;
        private System.Windows.Forms.Label lblLastBatchLabel;
        private System.Windows.Forms.Label lblLastBatch;
        private System.Windows.Forms.Button btnOK;
    }
}
