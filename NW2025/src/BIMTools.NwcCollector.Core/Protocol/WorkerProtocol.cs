using System;
using System.Globalization;
using System.Text;

namespace BIMTools.NwcCollector.Core.Protocol
{
    /// <summary>
    /// Tab-separated, one-event-per-line text protocol written by the worker to stdout and parsed by the launcher.
    ///   LOG        text
    ///   GROUP_START name, expectedCount
    ///   FILE       name, index, expectedCount, ok|skip|fail, path, reason
    ///   GROUP_DONE name, status, expected, appended, skipped, failed, outputPath
    ///   JOB_DONE   groupsSucceeded, groupsTotal
    /// </summary>
    public static class WorkerProtocol
    {
        public const string Log = "LOG";
        public const string GroupStart = "GROUP_START";
        public const string File = "FILE";
        public const string GroupDone = "GROUP_DONE";
        public const string JobDone = "JOB_DONE";

        private const char Sep = '\t';

        public static string Format(string type, params object[] fields)
        {
            var sb = new StringBuilder(type);
            if (fields != null)
            {
                foreach (var f in fields)
                    sb.Append(Sep).Append(Clean(Convert.ToString(f, CultureInfo.InvariantCulture)));
            }
            return sb.ToString();
        }

        public static bool TryParse(string line, out string type, out string[] fields)
        {
            type = null;
            fields = null;
            if (string.IsNullOrEmpty(line)) return false;

            var parts = line.Split(Sep);
            type = parts[0];
            fields = new string[parts.Length - 1];
            Array.Copy(parts, 1, fields, 0, fields.Length);
            return type == Log || type == GroupStart || type == File || type == GroupDone || type == JobDone;
        }

        private static string Clean(string s)
        {
            if (s == null) return string.Empty;
            return s.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
        }
    }
}
