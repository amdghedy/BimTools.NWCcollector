using System;
using System.IO;

namespace BIMTools.NwcCollector.Core.Scanning
{
    /// <summary>
    /// Detects files that are visible in Explorer but not stored locally (Desktop Connector / cloud placeholders).
    /// Only file attributes are inspected, so the check never triggers a download.
    /// </summary>
    public static class FileAvailability
    {
        private const FileAttributes Offline = (FileAttributes)0x00001000;            // FILE_ATTRIBUTE_OFFLINE
        private const FileAttributes RecallOnOpen = (FileAttributes)0x00040000;       // FILE_ATTRIBUTE_RECALL_ON_OPEN
        private const FileAttributes RecallOnDataAccess = (FileAttributes)0x00400000; // FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS

        public static bool Check(string path, out string reason, out long length)
        {
            DateTime lastWriteUtc;
            return Check(path, out reason, out length, out lastWriteUtc);
        }

        public static bool Check(string path, out string reason, out long length, out DateTime lastWriteUtc)
        {
            reason = null;
            length = 0;
            lastWriteUtc = DateTime.MinValue;
            try
            {
                var fi = new FileInfo(path);
                if (!fi.Exists) { reason = "File not found."; return false; }

                length = fi.Length;
                lastWriteUtc = fi.LastWriteTimeUtc;
                var attrs = fi.Attributes;
                if ((attrs & (Offline | RecallOnOpen | RecallOnDataAccess)) != 0)
                {
                    reason = "Cloud-only placeholder: the file is not downloaded to this computer.";
                    return false;
                }
                if (length == 0)
                {
                    reason = "File is empty (0 bytes); it may not be fully synchronized.";
                    return false;
                }
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is System.Security.SecurityException)
            {
                reason = "File is not accessible: " + ex.Message;
                return false;
            }
        }
    }
}
