#region License
/*
Copyright © Joan Charmant 2012.
jcharmant@gmail.com

This file is part of Kinovea.

Kinovea is distributed under the GPL v2. See license.md.
*/
#endregion

using System;

namespace Kinovea.Services
{
    /// <summary>
    /// Aggregates cloud sync statistics (success/fail counts, last upload speed) for display in Cloud preferences.
    /// </summary>
    public static class CloudSyncStats
    {
        private static readonly object Lock = new object();
        private static int totalSuccessfulUploads;
        private static int totalFailedUploads;
        private static double lastUploadSpeedBytesPerSec;
        private static DateTime? lastBatchTimeUtc;

        public static int TotalSuccessfulUploads
        {
            get { lock (Lock) return totalSuccessfulUploads; }
        }

        public static int TotalFailedUploads
        {
            get { lock (Lock) return totalFailedUploads; }
        }

        public static double LastUploadSpeedBytesPerSec
        {
            get { lock (Lock) return lastUploadSpeedBytesPerSec; }
        }

        public static DateTime? LastBatchTimeUtc
        {
            get { lock (Lock) return lastBatchTimeUtc; }
        }

        public static void RecordSessionSuccess()
        {
            lock (Lock)
                totalSuccessfulUploads++;
        }

        public static void RecordSessionFailure()
        {
            lock (Lock)
                totalFailedUploads++;
        }

        public static void RecordBatchComplete(long bytesUploaded, TimeSpan elapsed)
        {
            lock (Lock)
            {
                lastBatchTimeUtc = DateTime.UtcNow;
                lastUploadSpeedBytesPerSec = elapsed.TotalSeconds > 0 ? bytesUploaded / elapsed.TotalSeconds : 0;
            }
        }
    }
}
