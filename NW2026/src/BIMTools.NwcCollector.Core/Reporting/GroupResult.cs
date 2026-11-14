using System;
using System.Collections.Generic;
using BIMTools.NwcCollector.Core.Contracts;

namespace BIMTools.NwcCollector.Core.Reporting
{
    public sealed class FileIssue
    {
        public string Path { get; set; }
        public string Reason { get; set; }
    }

    /// <summary>Everything that happened while building one NWD; this is what the per-group log is written from.</summary>
    public sealed class GroupResult
    {
        public string Name { get; set; }
        public List<string> SourceFolders { get; } = new List<string>();
        public List<string> CollectedFiles { get; } = new List<string>();
        public List<string> AppendedFiles { get; } = new List<string>();
        public List<FileIssue> SkippedFiles { get; } = new List<FileIssue>();
        public List<FileIssue> FailedFiles { get; } = new List<FileIssue>();

        public string OutputPath { get; set; }
        public GroupStatus Status { get; set; } = GroupStatus.Failed;
        public string Message { get; set; }

        public bool Validated { get; set; }
        public string ValidationMessage { get; set; }

        public DateTime StartedUtc { get; set; }
        public DateTime FinishedUtc { get; set; }

        public int ExpectedCount { get { return CollectedFiles.Count; } }
    }
}
