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
using System.Threading;
using System.Xml;

namespace Kinovea.Services
{
    public class CloudPreferences : IPreferenceSerializer
    {
        #region Properties

        public string Name
        {
            get { return "Cloud"; }
        }

        public bool CloudBackupEnabled
        {
            get { BeforeRead(); return cloudBackupEnabled; }
            set { cloudBackupEnabled = value; Save(); }
        }

        public string CloudApiEndpoint
        {
            get { BeforeRead(); return cloudApiEndpoint; }
            set { cloudApiEndpoint = value ?? ""; Save(); }
        }

        /// <summary>
        /// Auto-generated GUID on first use; persisted so the same user_id is used across runs.
        /// </summary>
        public string UserId
        {
            get { BeforeRead(); return userId; }
            set { userId = value ?? ""; Save(); }
        }

        public int UploadBatchIntervalMinutes
        {
            get { BeforeRead(); return uploadBatchIntervalMinutes; }
            set { uploadBatchIntervalMinutes = Math.Max(1, Math.Min(1440, value)); Save(); }
        }

        #endregion

        #region Members

        private bool cloudBackupEnabled = true;
        private string cloudApiEndpoint = "https://fb92snno31.execute-api.us-east-2.amazonaws.com";
        private string userId = "";
        private int uploadBatchIntervalMinutes = 30;

        #endregion

        private void Save()
        {
            PreferencesManager.Save();
        }

        private void BeforeRead()
        {
            PreferencesManager.BeforeRead();
        }

        /// <summary>
        /// Ensures UserId is set; generates a new GUID if empty. Call when enabling cloud or before first upload.
        /// </summary>
        public void EnsureUserId()
        {
            BeforeRead();
            if (string.IsNullOrEmpty(userId))
            {
                userId = Guid.NewGuid().ToString();
                Save();
            }
        }

        #region Serialization

        public void WriteXML(XmlWriter writer)
        {
            writer.WriteElementString("CloudBackupEnabled", XmlHelper.WriteBoolean(cloudBackupEnabled));
            writer.WriteElementString("CloudApiEndpoint", cloudApiEndpoint ?? "");
            writer.WriteElementString("UserId", userId ?? "");
            writer.WriteElementString("UploadBatchIntervalMinutes", uploadBatchIntervalMinutes.ToString());
        }

        public void ReadXML(XmlReader reader)
        {
            reader.ReadStartElement();

            while (reader.NodeType == XmlNodeType.Element)
            {
                switch (reader.Name)
                {
                    case "CloudBackupEnabled":
                        cloudBackupEnabled = XmlHelper.ParseBoolean(reader.ReadElementContentAsString());
                        break;
                    case "CloudApiEndpoint":
                        cloudApiEndpoint = reader.ReadElementContentAsString();
                        break;
                    case "UserId":
                        userId = reader.ReadElementContentAsString();
                        break;
                    case "UploadBatchIntervalMinutes":
                        int.TryParse(reader.ReadElementContentAsString(), out uploadBatchIntervalMinutes);
                        if (uploadBatchIntervalMinutes < 1) uploadBatchIntervalMinutes = 30;
                        if (uploadBatchIntervalMinutes > 1440) uploadBatchIntervalMinutes = 1440;
                        break;
                    default:
                        reader.ReadOuterXml();
                        break;
                }
            }

            reader.ReadEndElement();
        }

        #endregion
    }
}
