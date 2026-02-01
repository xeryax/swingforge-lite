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
    /// Metadata JSON uploaded to S3 alongside the two videos (face-on, down-the-line).
    /// Property names match the API/metadata schema (snake_case in JSON).
    /// </summary>
    [DataContract]
    public class UploadMetadata
    {
        [DataMember(Name = "user_id")]
        public string UserId { get; set; }

        [DataMember(Name = "session_id")]
        public string SessionId { get; set; }

        [DataMember(Name = "upload_timestamp")]
        public string UploadTimestamp { get; set; }

        [DataMember(Name = "client_version")]
        public string ClientVersion { get; set; }

        [DataMember(Name = "capture_timestamp")]
        public string CaptureTimestamp { get; set; }

        [DataMember(Name = "camera_settings")]
        public CameraSettingsDto CameraSettings { get; set; }

        [DataMember(Name = "club_type")]
        public string ClubType { get; set; }

        [DataMember(Name = "notes")]
        public string Notes { get; set; }
    }

    [DataContract]
    public class CameraSettingsDto
    {
        [DataMember(Name = "fps")]
        public double Fps { get; set; }

        [DataMember(Name = "resolution")]
        public string Resolution { get; set; }
    }
}
