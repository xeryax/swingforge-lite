#region License
/*
Copyright © Joan Charmant 2012.
jcharmant@gmail.com

This file is part of Kinovea.

Kinovea is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License version 2
as published by the Free Software Foundation.

Kinovea is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with Kinovea. If not, see http://www.gnu.org/licenses/.
*/
#endregion

using Kinovea.Services;

namespace Kinovea.Root
{
    partial class PreferencePanelCloud
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (components != null)
                    components.Dispose();
                NotificationCenter.CloudSyncBatchCompleted -= OnCloudSyncBatchCompleted;
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.chkCloudBackupEnabled = new System.Windows.Forms.CheckBox();
            this.lblCloudApiEndpoint = new System.Windows.Forms.Label();
            this.txtCloudApiEndpoint = new System.Windows.Forms.TextBox();
            this.lblUserId = new System.Windows.Forms.Label();
            this.lblUserIdValue = new System.Windows.Forms.Label();
            this.btnSyncNow = new System.Windows.Forms.Button();
            this.btnDetails = new System.Windows.Forms.Button();
            this.lblPendingStatus = new System.Windows.Forms.Label();
            this.SuspendLayout();
            //
            // chkCloudBackupEnabled
            //
            this.chkCloudBackupEnabled.AutoSize = true;
            this.chkCloudBackupEnabled.Location = new System.Drawing.Point(29, 30);
            this.chkCloudBackupEnabled.Name = "chkCloudBackupEnabled";
            this.chkCloudBackupEnabled.Size = new System.Drawing.Size(180, 17);
            this.chkCloudBackupEnabled.TabIndex = 0;
            this.chkCloudBackupEnabled.Text = "Enable cloud backup";
            this.chkCloudBackupEnabled.UseVisualStyleBackColor = true;
            this.chkCloudBackupEnabled.CheckedChanged += new System.EventHandler(this.chkCloudBackupEnabled_CheckedChanged);
            //
            // lblCloudApiEndpoint
            //
            this.lblCloudApiEndpoint.AutoSize = true;
            this.lblCloudApiEndpoint.Location = new System.Drawing.Point(29, 65);
            this.lblCloudApiEndpoint.Name = "lblCloudApiEndpoint";
            this.lblCloudApiEndpoint.Size = new System.Drawing.Size(65, 13);
            this.lblCloudApiEndpoint.TabIndex = 1;
            this.lblCloudApiEndpoint.Text = "API endpoint:";
            //
            // txtCloudApiEndpoint
            //
            this.txtCloudApiEndpoint.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.txtCloudApiEndpoint.Location = new System.Drawing.Point(29, 84);
            this.txtCloudApiEndpoint.Name = "txtCloudApiEndpoint";
            this.txtCloudApiEndpoint.Size = new System.Drawing.Size(450, 20);
            this.txtCloudApiEndpoint.TabIndex = 2;
            this.txtCloudApiEndpoint.TextChanged += new System.EventHandler(this.txtCloudApiEndpoint_TextChanged);
            //
            // lblUserId
            //
            this.lblUserId.AutoSize = true;
            this.lblUserId.Location = new System.Drawing.Point(29, 118);
            this.lblUserId.Name = "lblUserId";
            this.lblUserId.Size = new System.Drawing.Size(44, 13);
            this.lblUserId.TabIndex = 3;
            this.lblUserId.Text = "User ID:";
            //
            // lblUserIdValue
            //
            this.lblUserIdValue.AutoSize = true;
            this.lblUserIdValue.Location = new System.Drawing.Point(29, 136);
            this.lblUserIdValue.Name = "lblUserIdValue";
            this.lblUserIdValue.Size = new System.Drawing.Size(50, 13);
            this.lblUserIdValue.TabIndex = 4;
            this.lblUserIdValue.Text = "(not set)";
            //
            // btnSyncNow
            //
            this.btnSyncNow.Location = new System.Drawing.Point(29, 170);
            this.btnSyncNow.Name = "btnSyncNow";
            this.btnSyncNow.Size = new System.Drawing.Size(100, 25);
            this.btnSyncNow.TabIndex = 5;
            this.btnSyncNow.Text = "Sync now";
            this.btnSyncNow.UseVisualStyleBackColor = true;
            this.btnSyncNow.Click += new System.EventHandler(this.btnSyncNow_Click);
            //
            // btnDetails
            //
            this.btnDetails.Location = new System.Drawing.Point(135, 170);
            this.btnDetails.Name = "btnDetails";
            this.btnDetails.Size = new System.Drawing.Size(75, 25);
            this.btnDetails.TabIndex = 7;
            this.btnDetails.Text = "Details";
            this.btnDetails.UseVisualStyleBackColor = true;
            this.btnDetails.Click += new System.EventHandler(this.btnDetails_Click);
            //
            // lblPendingStatus
            //
            this.lblPendingStatus.AutoSize = true;
            this.lblPendingStatus.Location = new System.Drawing.Point(29, 208);
            this.lblPendingStatus.Name = "lblPendingStatus";
            this.lblPendingStatus.Size = new System.Drawing.Size(130, 13);
            this.lblPendingStatus.TabIndex = 6;
            this.lblPendingStatus.Text = "No sessions pending upload";
            //
            // PreferencePanelCloud
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.Gainsboro;
            this.Controls.Add(this.chkCloudBackupEnabled);
            this.Controls.Add(this.lblCloudApiEndpoint);
            this.Controls.Add(this.txtCloudApiEndpoint);
            this.Controls.Add(this.lblUserId);
            this.Controls.Add(this.lblUserIdValue);
            this.Controls.Add(this.btnSyncNow);
            this.Controls.Add(this.btnDetails);
            this.Controls.Add(this.lblPendingStatus);
            this.Name = "PreferencePanelCloud";
            this.Size = new System.Drawing.Size(490, 322);
            this.VisibleChanged += new System.EventHandler(this.PreferencePanelCloud_VisibleChanged);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private System.Windows.Forms.CheckBox chkCloudBackupEnabled;
        private System.Windows.Forms.Label lblCloudApiEndpoint;
        private System.Windows.Forms.TextBox txtCloudApiEndpoint;
        private System.Windows.Forms.Label lblUserId;
        private System.Windows.Forms.Label lblUserIdValue;
        private System.Windows.Forms.Button btnSyncNow;
        private System.Windows.Forms.Button btnDetails;
        private System.Windows.Forms.Label lblPendingStatus;
    }
}
