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
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;

namespace Kinovea.Services
{
    /// <summary>
    /// Calls API Gateway for presigned URLs and uploads files to S3.
    /// </summary>
    public class CloudUploadService
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private readonly HttpClient httpClient;
        private const int MaxRetries = 3;
        private const int BaseDelayMs = 500;

        public CloudUploadService()
        {
            httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        /// <summary>
        /// Request presigned URLs from the API. Base URL must not have a trailing slash.
        /// Pass filenames to keep original names in S3 (e.g. from Path.GetFileName of video paths).
        /// </summary>
        public async Task<PresignedUrlsResult> RequestUploadUrlsAsync(string baseUrl, string userId, string sessionId, string faceOnFilename = null, string downTheLineFilename = null)
        {
            string url = baseUrl.TrimEnd('/') + "/request-upload";
            var body = new StringBuilder();
            body.Append("{\"user_id\":\"").Append(EscapeJson(userId)).Append("\",\"session_id\":\"").Append(EscapeJson(sessionId)).Append("\"");
            if (!string.IsNullOrEmpty(faceOnFilename))
                body.Append(",\"face_on_filename\":\"").Append(EscapeJson(faceOnFilename)).Append("\"");
            if (!string.IsNullOrEmpty(downTheLineFilename))
                body.Append(",\"down_the_line_filename\":\"").Append(EscapeJson(downTheLineFilename)).Append("\"");
            body.Append("}");
            var content = new StringContent(body.ToString(), Encoding.UTF8, "application/json");

            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    var response = await httpClient.PostAsync(url, content).ConfigureAwait(false);
                    string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode)
                    {
                        log.WarnFormat("RequestUploadUrls attempt {0}: {1} {2}", attempt, response.StatusCode, responseBody);
                        if (attempt < MaxRetries)
                            await Task.Delay(BaseDelayMs * attempt).ConfigureAwait(false);
                        continue;
                    }

                    var parsed = DeserializePresignedResponse(responseBody);
                    if (parsed == null)
                    {
                        log.WarnFormat("RequestUploadUrls attempt {0}: failed to parse response", attempt);
                        if (attempt < MaxRetries)
                            await Task.Delay(BaseDelayMs * attempt).ConfigureAwait(false);
                        continue;
                    }

                    return new PresignedUrlsResult
                    {
                        SessionId = parsed.SessionId,
                        FaceOnUrl = parsed.FaceOnUrl,
                        DownTheLineUrl = parsed.DownTheLineUrl,
                        MetadataUrl = parsed.MetadataUrl
                    };
                }
                catch (Exception ex)
                {
                    log.WarnFormat("RequestUploadUrls attempt {0}: {1}", attempt, ex.Message);
                    if (attempt < MaxRetries)
                        await Task.Delay(BaseDelayMs * attempt).ConfigureAwait(false);
                }
            }

            return null;
        }

        /// <summary>
        /// Upload a file to a presigned PUT URL. Retries up to MaxRetries with exponential backoff.
        /// </summary>
        public async Task<bool> UploadFileAsync(string localPath, string presignedUrl)
        {
            if (!File.Exists(localPath))
            {
                log.ErrorFormat("UploadFile: file not found: {0}", localPath);
                return false;
            }

            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    using (var stream = new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (var request = new HttpRequestMessage(HttpMethod.Put, presignedUrl))
                    {
                        request.Content = new StreamContent(stream);
                        // Must match ContentType used when generating presigned URL in Lambda (application/octet-stream).
                        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                        // Set Content-Length so S3 accepts the request (curl sends it; without it HttpClient may use chunked encoding and S3 returns 403).
                        request.Content.Headers.ContentLength = stream.Length;
                        var response = await httpClient.SendAsync(request).ConfigureAwait(false);
                        if (response.IsSuccessStatusCode)
                            return true;
                        log.WarnFormat("UploadFile attempt {0}: {1} for {2}", attempt, response.StatusCode, localPath);
                    }
                }
                catch (Exception ex)
                {
                    log.WarnFormat("UploadFile attempt {0}: {1} for {2}", attempt, ex.Message, localPath);
                }

                if (attempt < MaxRetries)
                    await Task.Delay(BaseDelayMs * attempt).ConfigureAwait(false);
            }

            return false;
        }

        /// <summary>
        /// Upload metadata JSON to the presigned URL. Retries up to MaxRetries.
        /// </summary>
        public async Task<bool> UploadMetadataAsync(UploadMetadata metadata, string presignedUrl)
        {
            string json = SerializeMetadata(metadata);
            if (string.IsNullOrEmpty(json))
                return false;

            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    var request = new HttpRequestMessage(HttpMethod.Put, presignedUrl) { Content = content };
                    var response = await httpClient.SendAsync(request).ConfigureAwait(false);
                    if (response.IsSuccessStatusCode)
                        return true;
                    log.WarnFormat("UploadMetadata attempt {0}: {1}", attempt, response.StatusCode);
                }
                catch (Exception ex)
                {
                    log.WarnFormat("UploadMetadata attempt {0}: {1}", attempt, ex.Message);
                }

                if (attempt < MaxRetries)
                    await Task.Delay(BaseDelayMs * attempt).ConfigureAwait(false);
            }

            return false;
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static PresignedUrlsResponse DeserializePresignedResponse(string json)
        {
            try
            {
                var serializer = new DataContractJsonSerializer(typeof(PresignedUrlsResponse));
                using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                    return (PresignedUrlsResponse)serializer.ReadObject(ms);
            }
            catch
            {
                return null;
            }
        }

        private static string SerializeMetadata(UploadMetadata metadata)
        {
            try
            {
                var serializer = new DataContractJsonSerializer(typeof(UploadMetadata));
                using (var ms = new MemoryStream())
                {
                    serializer.WriteObject(ms, metadata);
                    return Encoding.UTF8.GetString(ms.ToArray());
                }
            }
            catch (Exception ex)
            {
                log.Error("SerializeMetadata failed", ex);
                return null;
            }
        }
    }

    public class PresignedUrlsResult
    {
        public string SessionId { get; set; }
        public string FaceOnUrl { get; set; }
        public string DownTheLineUrl { get; set; }
        public string MetadataUrl { get; set; }
    }
}
