using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace BIMTools.NwcCollector.Core.Contracts
{
    /// <summary>How source files are split into output files.</summary>
    public enum GroupingMode
    {
        /// <summary>One output per folder name, merging every folder that has that exact name (1A1 in many places).</summary>
        FolderName,
        /// <summary>One output per folder that directly contains files.</summary>
        EachFolder,
        /// <summary>One output per folder name found at a fixed level below the root.</summary>
        FolderAtDepth,
        /// <summary>One output per value of a delimited part of the file name (ISO 19650 style codes).</summary>
        FileNameToken,
        /// <summary>One output per value captured by a regular expression on the file name.</summary>
        FileNameRegex,
        /// <summary>Everything into one federated output.</summary>
        SingleOutput,
        /// <summary>One output per source file.</summary>
        EachFile
    }

    public enum FileOrder { PathAscending, NameAscending, ModifiedAscending, ModifiedDescending, SizeAscending, SizeDescending, PriorityList }
    public enum GroupOrder { NameAscending, FileCountDescending, FileCountAscending }
    public enum OutputLocation { SingleFolder, SubfolderPerGroup, NextToSource }
    public enum ExistingFilePolicy { Ask, Overwrite, Skip, AutoVersion, ArchiveOld }
    public enum OutputFormat { Nwd, Nwf }

    public static class EnumUtil
    {
        public static T Parse<T>(string text, T fallback) where T : struct
        {
            T value;
            if (!string.IsNullOrWhiteSpace(text) && Enum.TryParse(text.Trim(), true, out value) && Enum.IsDefined(typeof(T), value))
                return value;
            return fallback;
        }
    }

    /// <summary>
    /// Every user-selectable rule of the tool. A profile is saved as a small JSON file, so companies can share
    /// their own conventions (naming scheme, discipline order, archive rules) without changing code.
    /// Folder paths (root / output) are deliberately NOT part of a profile.
    /// </summary>
    [DataContract]
    public sealed class CollectorProfile
    {
        public const string DefaultFolderPattern = @"^\d+[A-Za-z]+\d+$";
        public const string DefaultRevisionRegex = @"[\s._\-]*(?:[Rr](?:ev)?|[Vv])[\s._\-]*\d+$";

        public CollectorProfile() { SetDefaults(); }

        [OnDeserializing]
        private void OnDeserializing(StreamingContext context) { SetDefaults(); }

        private void SetDefaults()
        {
            Name = "Custom";
            Description = "";
            Mode = GroupingMode.FolderName.ToString();
            FolderNames = new List<string>();
            FolderNamePattern = DefaultFolderPattern;
            Depth = 1;
            TokenDelimiters = "-";
            TokenIndex = 1;
            FileNameRegex = "";
            UnmatchedGroupName = "_Unmatched";
            SingleOutputName = "";
            IncludeRootFiles = false;
            RootFilesGroupName = "Root";
            RootFolderOnly = false;
            MaxScanDepth = 0;

            Extensions = new List<string> { ".nwc" };
            IncludeFileRegex = "";
            ExcludeFileRegex = "";
            ExcludeFolderNames = new List<string> { "$RECYCLE.BIN", "System Volume Information", "_Archive" };
            ExcludeFolderRegex = "";
            MinSizeKb = 0;
            ModifiedAfter = "";
            LatestRevisionOnly = false;
            RevisionRegex = DefaultRevisionRegex;

            FileOrder = Contracts.FileOrder.PathAscending.ToString();
            PriorityWords = new List<string>();
            GroupOrder = Contracts.GroupOrder.NameAscending.ToString();

            OutputLocation = Contracts.OutputLocation.SingleFolder.ToString();
            OutputNameTemplate = "{name}";
            OutputFormat = Contracts.OutputFormat.Nwd.ToString();
            ExistingFilePolicy = Contracts.ExistingFilePolicy.Ask.ToString();
            SkipUpToDate = false;
            MaxFilesPerOutput = 0;
            WriteCsv = true;
            OpenFirstFile = false;
        }

        /// <summary>Replaces nulls (a hand-edited JSON may contain them) with safe values.</summary>
        public void Normalize()
        {
            if (Name == null) Name = "Custom";
            if (Description == null) Description = "";
            if (FolderNames == null) FolderNames = new List<string>();
            if (Extensions == null || Extensions.Count == 0) Extensions = new List<string> { ".nwc" };
            if (ExcludeFolderNames == null) ExcludeFolderNames = new List<string>();
            if (PriorityWords == null) PriorityWords = new List<string>();
            if (FolderNamePattern == null) FolderNamePattern = "";
            if (TokenDelimiters == null) TokenDelimiters = "";
            if (FileNameRegex == null) FileNameRegex = "";
            if (UnmatchedGroupName == null) UnmatchedGroupName = "";
            if (SingleOutputName == null) SingleOutputName = "";
            if (RootFilesGroupName == null) RootFilesGroupName = "Root";
            if (IncludeFileRegex == null) IncludeFileRegex = "";
            if (ExcludeFileRegex == null) ExcludeFileRegex = "";
            if (ExcludeFolderRegex == null) ExcludeFolderRegex = "";
            if (ModifiedAfter == null) ModifiedAfter = "";
            if (RevisionRegex == null) RevisionRegex = DefaultRevisionRegex;
            if (string.IsNullOrWhiteSpace(OutputNameTemplate)) OutputNameTemplate = "{name}";
        }

        [DataMember] public string Name { get; set; }
        [DataMember] public string Description { get; set; }

        // ---- grouping scenario
        [DataMember] public string Mode { get; set; }
        [DataMember] public List<string> FolderNames { get; set; }
        [DataMember] public string FolderNamePattern { get; set; }
        [DataMember] public int Depth { get; set; }
        [DataMember] public string TokenDelimiters { get; set; }
        [DataMember] public int TokenIndex { get; set; }
        [DataMember] public string FileNameRegex { get; set; }
        [DataMember] public string UnmatchedGroupName { get; set; }
        [DataMember] public string SingleOutputName { get; set; }
        [DataMember] public bool IncludeRootFiles { get; set; }
        [DataMember] public string RootFilesGroupName { get; set; }
        [DataMember] public bool RootFolderOnly { get; set; }
        [DataMember] public int MaxScanDepth { get; set; }

        // ---- filters
        [DataMember] public List<string> Extensions { get; set; }
        [DataMember] public string IncludeFileRegex { get; set; }
        [DataMember] public string ExcludeFileRegex { get; set; }
        [DataMember] public List<string> ExcludeFolderNames { get; set; }
        [DataMember] public string ExcludeFolderRegex { get; set; }
        [DataMember] public long MinSizeKb { get; set; }
        [DataMember] public string ModifiedAfter { get; set; }
        [DataMember] public bool LatestRevisionOnly { get; set; }
        [DataMember] public string RevisionRegex { get; set; }

        // ---- ordering
        [DataMember] public string FileOrder { get; set; }
        [DataMember] public List<string> PriorityWords { get; set; }
        [DataMember] public string GroupOrder { get; set; }

        // ---- output
        [DataMember] public string OutputLocation { get; set; }
        [DataMember] public string OutputNameTemplate { get; set; }
        [DataMember] public string OutputFormat { get; set; }
        [DataMember] public string ExistingFilePolicy { get; set; }
        [DataMember] public bool SkipUpToDate { get; set; }
        [DataMember] public int MaxFilesPerOutput { get; set; }
        [DataMember] public bool WriteCsv { get; set; }
        [DataMember] public bool OpenFirstFile { get; set; }

        // ---- typed views of the string settings (JSON stays readable: "Mode": "FolderName")
        [IgnoreDataMember] public GroupingMode ModeValue { get { return EnumUtil.Parse(Mode, GroupingMode.FolderName); } set { Mode = value.ToString(); } }
        [IgnoreDataMember] public FileOrder FileOrderValue { get { return EnumUtil.Parse(FileOrder, Contracts.FileOrder.PathAscending); } set { FileOrder = value.ToString(); } }
        [IgnoreDataMember] public GroupOrder GroupOrderValue { get { return EnumUtil.Parse(GroupOrder, Contracts.GroupOrder.NameAscending); } set { GroupOrder = value.ToString(); } }
        [IgnoreDataMember] public OutputLocation OutputLocationValue { get { return EnumUtil.Parse(OutputLocation, Contracts.OutputLocation.SingleFolder); } set { OutputLocation = value.ToString(); } }
        [IgnoreDataMember] public OutputFormat OutputFormatValue { get { return EnumUtil.Parse(OutputFormat, Contracts.OutputFormat.Nwd); } set { OutputFormat = value.ToString(); } }
        [IgnoreDataMember] public ExistingFilePolicy ExistingFilePolicyValue { get { return EnumUtil.Parse(ExistingFilePolicy, Contracts.ExistingFilePolicy.Ask); } set { ExistingFilePolicy = value.ToString(); } }
    }

    public static class ProfileSerializer
    {
        public static CollectorProfile Read(string path)
        {
            var ser = new DataContractJsonSerializer(typeof(CollectorProfile));
            CollectorProfile p;
            using (var fs = File.OpenRead(path))
                p = (CollectorProfile)ser.ReadObject(fs);
            p.Normalize();
            return p;
        }

        public static void Write(CollectorProfile profile, string path)
        {
            var ser = new DataContractJsonSerializer(typeof(CollectorProfile));
            string json;
            using (var ms = new MemoryStream())
            {
                ser.WriteObject(ms, profile);
                json = Encoding.UTF8.GetString(ms.ToArray());
            }
            File.WriteAllText(path, JsonPretty.Format(json), new UTF8Encoding(false));
        }
    }

    internal static class JsonPretty
    {
        public static string Format(string json)
        {
            var sb = new StringBuilder();
            int indent = 0;
            bool inString = false;
            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];
                if (inString)
                {
                    sb.Append(c);
                    if (c == '\\' && i + 1 < json.Length) sb.Append(json[++i]);
                    else if (c == '"') inString = false;
                    continue;
                }
                switch (c)
                {
                    case '"': inString = true; sb.Append(c); break;
                    case '{':
                    case '[':
                        sb.Append(c);
                        if (i + 1 < json.Length && (json[i + 1] == '}' || json[i + 1] == ']')) { sb.Append(json[++i]); break; }
                        indent++;
                        NewLine(sb, indent);
                        break;
                    case '}':
                    case ']':
                        indent--;
                        NewLine(sb, indent);
                        sb.Append(c);
                        break;
                    case ',': sb.Append(c); NewLine(sb, indent); break;
                    case ':': sb.Append(": "); break;
                    default: if (!char.IsWhiteSpace(c)) sb.Append(c); break;
                }
            }
            return sb.ToString();
        }

        private static void NewLine(StringBuilder sb, int indent)
        {
            sb.AppendLine();
            sb.Append(' ', Math.Max(0, indent) * 2);
        }
    }
}
