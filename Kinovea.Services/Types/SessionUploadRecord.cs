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
using System.Runtime.Serialization;

namespace Kinovea.Services
{
    /// <summary>
    /// Tracks one capture session (face-on + down-the-line pair) for cloud upload.
    /// </summary>
    [DataContract]
    public class SessionUploadRecord
    {
        [DataMember(Name = "session_id")]
        public string SessionId { get; set; }

        [DataMember(Name = "video_a_path")]
        public string VideoAPath { get; set; }

        [DataMember(Name = "video_b_path")]
        public string VideoBPath { get; set; }

        [DataMember(Name = "captured_at")]
        public DateTime CapturedAt { get; set; }

        [DataMember(Name = "synced")]
        public bool Synced { get; set; }

        [DataMember(Name = "synced_at")]
        public DateTime? SyncedAt { get; set; }

        [DataMember(Name = "sync_attempts")]
        public int SyncAttempts { get; set; }

        [DataMember(Name = "last_error")]
        public string LastError { get; set; }
    }
}
