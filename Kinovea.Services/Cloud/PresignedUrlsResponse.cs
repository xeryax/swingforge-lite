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

using System.Runtime.Serialization;

namespace Kinovea.Services
{
    [DataContract]
    internal class PresignedUrlsResponse
    {
        [DataMember(Name = "session_id")]
        public string SessionId { get; set; }

        [DataMember(Name = "face_on_url")]
        public string FaceOnUrl { get; set; }

        [DataMember(Name = "down_the_line_url")]
        public string DownTheLineUrl { get; set; }

        [DataMember(Name = "metadata_url")]
        public string MetadataUrl { get; set; }

        [DataMember(Name = "expires_in")]
        public int ExpiresIn { get; set; }
    }
}
