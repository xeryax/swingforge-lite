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

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Kinovea.Root.Properties;
using Kinovea.Services;

namespace Kinovea.Root
{
    public partial class PreferencePanelCloud : UserControl, IPreferencePanel
    {
        #region IPreferencePanel properties

        public string Description { get { return description; } }
        public Bitmap Icon { get { return icon; } }
        public List<PreferenceTab> Tabs { get { return tabs; } }

        #endregion

        #region Members

        private string description;
        private Bitmap icon;
        private List<PreferenceTab> tabs = new List<PreferenceTab> { PreferenceTab.Cloud_General };
        private bool cloudBackupEnabled;
        private string cloudApiEndpoint;

        #endregion

        public PreferencePanelCloud()
        {
            InitializeComponent();
            this.BackColor = Color.White;
            description = "Cloud backup";
            icon = Resources.tools_30;
            ImportPreferences();
            InitPage();
            NotificationCenter.CloudSyncBatchCompleted += OnCloudSyncBatchCompleted;
        }

        public void OpenTab(PreferenceTab tab)
        {
        }

        public void Close()
        {
        }

        private void ImportPreferences()
        {
            PreferencesManager.BeforeRead();
            cloudBackupEnabled = PreferencesManager.CloudPreferences.CloudBackupEnabled;
            cloudApiEndpoint = PreferencesManager.CloudPreferences.CloudApiEndpoint ?? "";
        }

        private void InitPage()
        {
            chkCloudBackupEnabled.Checked = cloudBackupEnabled;
            txtCloudApiEndpoint.Text = cloudApiEndpoint;
            RefreshUserIdLabel();
            RefreshPendingStatus();
        }

        private void RefreshUserIdLabel()
        {
            PreferencesManager.BeforeRead();
            string userId = PreferencesManager.CloudPreferences.UserId;
            lblUserIdValue.Text = string.IsNullOrEmpty(userId) ? "(not set)" : userId;
        }

        private void RefreshPendingStatus()
        {
            var tracking = new UploadTrackingService();
            int count = tracking.GetUnsyncedSessions().Count;
            lblPendingStatus.Text = count == 0
                ? "No sessions pending upload"
                : string.Format("{0} session(s) pending upload", count);
        }

        private void chkCloudBackupEnabled_CheckedChanged(object sender, EventArgs e)
        {
            cloudBackupEnabled = chkCloudBackupEnabled.Checked;
        }

        private void txtCloudApiEndpoint_TextChanged(object sender, EventArgs e)
        {
            cloudApiEndpoint = txtCloudApiEndpoint.Text ?? "";
        }

        private void btnSyncNow_Click(object sender, EventArgs e)
        {
            NotificationCenter.RaiseCloudSyncNowAsked();
            RefreshPendingStatus();
        }

        private void btnDetails_Click(object sender, EventArgs e)
        {
            using (var dlg = new FormCloudSyncDetails())
            {
                dlg.ShowDialog(this);
            }
        }

        private void PreferencePanelCloud_VisibleChanged(object sender, EventArgs e)
        {
            if (Visible)
                RefreshPendingStatus();
        }

        private void OnCloudSyncBatchCompleted(object sender, EventArgs e)
        {
            RefreshPendingStatus();
        }

        public void CommitChanges()
        {
            PreferencesManager.CloudPreferences.CloudBackupEnabled = cloudBackupEnabled;
            PreferencesManager.CloudPreferences.CloudApiEndpoint = cloudApiEndpoint?.Trim() ?? "";
            if (cloudBackupEnabled)
                PreferencesManager.CloudPreferences.EnsureUserId();
        }
    }
}
