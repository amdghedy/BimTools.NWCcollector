using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BIMTools.NwcCollector.Core.Contracts;

namespace BIMTools.NwcCollector.Core.Reporting
{
    public static class GroupLogWriter
    {
        public static string FormatGroup(GroupResult r)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Target name        : " + r.Name);
            sb.AppendLine("Status             : " + r.Status);
            if (!string.IsNullOrEmpty(r.Message)) sb.AppendLine("Message            : " + r.Message);
            sb.AppendLine("Output file         : " + r.OutputPath);
            sb.AppendLine("Started (UTC)       : " + r.StartedUtc.ToString("u"));
            sb.AppendLine("Finished (UTC)      : " + r.FinishedUtc.ToString("u"));
            sb.AppendLine();
            sb.AppendLine("Counts: expected " + r.ExpectedCount + ", appended " + r.AppendedFiles.Count +
                          ", skipped " + r.SkippedFiles.Count + ", failed " + r.FailedFiles.Count);
            sb.AppendLine("Validation         : " + (r.Validated ? "PASSED" : "NOT PASSED") +
                          (string.IsNullOrEmpty(r.ValidationMessage) ? "" : " - " + r.ValidationMessage));
            sb.AppendLine();

            sb.AppendLine("Source folders (" + r.SourceFolders.Count + "):");
            foreach (var f in r.SourceFolders) sb.AppendLine("  " + f);
            sb.AppendLine();

            sb.AppendLine("Collected files, in append order (" + r.CollectedFiles.Count + "):");
            foreach (var f in r.CollectedFiles) sb.AppendLine("  " + f);
            sb.AppendLine();

            sb.AppendLine("Appended successfully (" + r.AppendedFiles.Count + "):");
            foreach (var f in r.AppendedFiles) sb.AppendLine("  " + f);
            sb.AppendLine();

            sb.AppendLine("Skipped (" + r.SkippedFiles.Count + "):");
            foreach (var i in r.SkippedFiles) sb.AppendLine("  " + i.Path + "  ->  " + i.Reason);
            sb.AppendLine();

            sb.AppendLine("Failed (" + r.FailedFiles.Count + "):");
            foreach (var i in r.FailedFiles) sb.AppendLine("  " + i.Path + "  ->  " + i.Reason);
            return sb.ToString();
        }

        public static string LogPathFor(string reportDirectory, string groupName)
        {
            return Path.Combine(reportDirectory, PathUtil.SafeFileName(groupName) + ".log.txt");
        }

        public static void WriteGroup(GroupResult r, string path)
        {
            File.WriteAllText(path, FormatGroup(r), new UTF8Encoding(true));
        }

        public static void WriteSummary(IList<GroupResult> results, string path)
        {
            var sb = new StringBuilder();
            sb.AppendLine("NWC Folder Collector - summary " + DateTime.UtcNow.ToString("u"));
            sb.AppendLine();
            int expected = 0, appended = 0;
            foreach (var r in results)
            {
                expected += r.ExpectedCount;
                appended += r.AppendedFiles.Count;
                sb.AppendLine(r.Name.PadRight(24) + r.Status.ToString().PadRight(18) +
                              "expected " + r.ExpectedCount + " / appended " + r.AppendedFiles.Count +
                              " / skipped " + r.SkippedFiles.Count + " / failed " + r.FailedFiles.Count +
                              "  validated=" + r.Validated + "  " + r.OutputPath);
            }
            sb.AppendLine();
            sb.AppendLine("Total files: expected " + expected + ", appended " + appended);
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
        }

        /// <summary>One row per source file: handy for Excel, audits and hand-over reports.</summary>
        public static void WriteCsv(IList<GroupResult> results, string path)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Output,OutputFile,GroupStatus,SourceFile,FileStatus,Reason");
            foreach (var r in results)
            {
                int rows = 0;
                foreach (var f in r.AppendedFiles) { Row(sb, r, f, "Appended", ""); rows++; }
                foreach (var i in r.SkippedFiles) { Row(sb, r, i.Path, "Skipped", i.Reason); rows++; }
                foreach (var i in r.FailedFiles) { Row(sb, r, i.Path, "Failed", i.Reason); rows++; }
                if (rows == 0)
                    foreach (var f in r.CollectedFiles) Row(sb, r, f, "NotProcessed", r.Message);
            }
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true)); // BOM so Excel reads Unicode names correctly
        }

        private static void Row(StringBuilder sb, GroupResult r, string file, string status, string reason)
        {
            sb.AppendLine(Csv(r.Name) + "," + Csv(r.OutputPath) + "," + Csv(r.Status.ToString()) + "," + Csv(file) + "," + Csv(status) + "," + Csv(reason));
        }

        private static string Csv(string s)
        {
            if (s == null) return "";
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }
    }
}
