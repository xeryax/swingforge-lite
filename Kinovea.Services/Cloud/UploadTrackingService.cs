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
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;

namespace Kinovea.Services
{
    /// <summary>
    /// Tracks which sessions have been synced to the cloud. Persists to upload-tracking.json in settings directory.
    /// </summary>
    public class UploadTrackingService
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private readonly string filePath;
        private readonly object locker = new object();
        private List<SessionUploadRecord> sessions = new List<SessionUploadRecord>();

        public UploadTrackingService()
        {
            filePath = Path.Combine(Software.SettingsDirectory, "upload-tracking.json");
            Load();
        }

        public IReadOnlyList<SessionUploadRecord> GetUnsyncedSessions()
        {
            lock (locker)
            {
                Load();
                return sessions.Where(s => !s.Synced).ToList();
            }
        }

        /// <summary>
        /// Add a new session (dual capture pair). SessionId is generated if not provided.
        /// </summary>
        public void AddSession(string sessionId, string videoAPath, string videoBPath, DateTime capturedAt)
        {
            if (string.IsNullOrEmpty(sessionId))
                sessionId = Guid.NewGuid().ToString();

            lock (locker)
            {
                Load();
                sessions.Add(new SessionUploadRecord
                {
                    SessionId = sessionId,
                    VideoAPath = videoAPath ?? "",
                    VideoBPath = videoBPath ?? "",
                    CapturedAt = capturedAt,
                    Synced = false,
                    SyncedAt = null,
                    SyncAttempts = 0,
                    LastError = null
                });
                Save();
            }
        }

        public void MarkSynced(string sessionId)
        {
            lock (locker)
            {
                Load();
                var session = sessions.FirstOrDefault(s => string.Equals(s.SessionId, sessionId, StringComparison.OrdinalIgnoreCase));
                if (session != null)
                {
                    session.Synced = true;
                    session.SyncedAt = DateTime.UtcNow;
                    session.LastError = null;
                    Save();
                }
            }
        }

        public void RecordFailure(string sessionId, string error)
        {
            lock (locker)
            {
                Load();
                var session = sessions.FirstOrDefault(s => string.Equals(s.SessionId, sessionId, StringComparison.OrdinalIgnoreCase));
                if (session != null)
                {
                    session.SyncAttempts++;
                    session.LastError = error;
                    Save();
                }
            }
        }

        private void Load()
        {
            if (!File.Exists(filePath))
            {
                sessions = new List<SessionUploadRecord>();
                return;
            }

            try
            {
                string json = File.ReadAllText(filePath, Encoding.UTF8);
                var wrapper = Deserialize(json);
                sessions = wrapper?.Sessions ?? new List<SessionUploadRecord>();
            }
            catch (Exception ex)
            {
                log.ErrorFormat("UploadTrackingService Load failed: {0}", ex.Message);
                sessions = new List<SessionUploadRecord>();
            }
        }

        private void Save()
        {
            try
            {
                string json = Serialize(new UploadTrackingFile { Sessions = sessions });
                File.WriteAllText(filePath, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                log.ErrorFormat("UploadTrackingService Save failed: {0}", ex.Message);
            }
        }

        private static UploadTrackingFile Deserialize(string json)
        {
            try
            {
                var serializer = new DataContractJsonSerializer(typeof(UploadTrackingFile));
                using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                    return (UploadTrackingFile)serializer.ReadObject(ms);
            }
            catch
            {
                return null;
            }
        }

        private static string Serialize(UploadTrackingFile file)
        {
            var serializer = new DataContractJsonSerializer(typeof(UploadTrackingFile));
            using (var ms = new MemoryStream())
            {
                serializer.WriteObject(ms, file);
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }
    }
}
