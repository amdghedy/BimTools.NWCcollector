using System;
using System.Collections.Generic;
using System.Linq;

namespace BIMTools.NwcCollector.Core.Scanning
{
    public sealed class NwcFileInfo
    {
        public string Path { get; set; }
        public long Length { get; set; }
        public DateTime LastWriteUtc { get; set; }

        /// <summary>False for missing, empty or cloud-only (not downloaded) files.</summary>
        public bool IsLocal { get; set; }

        public string UnavailableReason { get; set; }
    }

    /// <summary>One future output: a named set of source files plus the folders they came from.</summary>
    public sealed class FolderGroup
    {
        public string Name { get; set; }
        public List<string> SourceFolders { get; } = new List<string>();
        public List<NwcFileInfo> Files { get; } = new List<NwcFileInfo>();

        public int TotalCount { get { return Files.Count; } }
        public int LocalCount { get { return Files.Count(f => f.IsLocal); } }
        public int NotLocalCount { get { return Files.Count(f => !f.IsLocal); } }
    }
}
