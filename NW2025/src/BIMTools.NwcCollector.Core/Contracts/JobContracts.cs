using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace BIMTools.NwcCollector.Core.Contracts
{
    public enum GroupStatus
    {
        Success,
        PartialSuccess,
        Failed,
        ValidationFailed,
        SkippedExists,
        SkippedUpToDate,
        Cancelled
    }

    /// <summary>One output file to build: every listed source file is appended to a fresh Navisworks document.</summary>
    [DataContract]
    public sealed class GroupJob
    {
        /// <summary>Unique output base name (no extension); also used for the log file name.</summary>
        [DataMember] public string Name { get; set; }

        /// <summary>The group this output belongs to (differs from Name when a group is split or renamed by a template).</summary>
        [DataMember] public string GroupKey { get; set; }

        [DataMember] public List<string> SourceFolders { get; set; }
        [DataMember] public List<string> Files { get; set; }

        /// <summary>Full path of the final .nwd / .nwf file.</summary>
        [DataMember] public string OutputPath { get; set; }
    }

    /// <summary>The work order the launcher (or the worker's own planner) produces; serialized as a JSON file.</summary>
    [DataContract]
    public sealed class JobSpec
    {
        [DataMember] public string OutputDirectory { get; set; }

        /// <summary>Where logs, summary and CSV go. Defaults to OutputDirectory.</summary>
        [DataMember] public string ReportDirectory { get; set; }

        /// <summary>ExistingFilePolicy name. "Ask" is resolved by the launcher before the job starts.</summary>
        [DataMember] public string ExistingFilePolicy { get; set; }

        [DataMember] public bool SkipUpToDate { get; set; }
        [DataMember] public bool WriteCsv { get; set; }

        /// <summary>Fallback: open the first source file instead of appending it to an empty document.</summary>
        [DataMember] public bool OpenFirstFile { get; set; }

        /// <summary>The worker stops after the current file when this file exists.</summary>
        [DataMember] public string CancelFlagPath { get; set; }

        [DataMember] public List<GroupJob> Groups { get; set; }
    }

    public static class JobSerializer
    {
        public static void Write(JobSpec job, string path)
        {
            var ser = new DataContractJsonSerializer(typeof(JobSpec));
            using (var fs = File.Create(path))
                ser.WriteObject(fs, job);
        }

        public static JobSpec Read(string path)
        {
            var ser = new DataContractJsonSerializer(typeof(JobSpec));
            JobSpec job;
            using (var fs = File.OpenRead(path))
                job = (JobSpec)ser.ReadObject(fs);

            // DataContract deserialization skips constructors; normalise nulls.
            if (job.Groups == null) job.Groups = new List<GroupJob>();
            if (string.IsNullOrEmpty(job.ReportDirectory)) job.ReportDirectory = job.OutputDirectory;
            if (string.IsNullOrEmpty(job.ExistingFilePolicy)) job.ExistingFilePolicy = Contracts.ExistingFilePolicy.Skip.ToString();
            foreach (var g in job.Groups)
            {
                if (g.SourceFolders == null) g.SourceFolders = new List<string>();
                if (g.Files == null) g.Files = new List<string>();
            }
            return job;
        }
    }
}
