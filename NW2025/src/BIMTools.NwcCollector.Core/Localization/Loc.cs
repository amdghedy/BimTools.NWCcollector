using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace BIMTools.NwcCollector.Core.Localization
{
    /// <summary>
    /// UI text lookup. English is built in. Every other language is a plain UTF-8 text file
    /// "Languages\xx.lang" with lines "key=value" (first line "lang.name=Display name"), so any company can add or
    /// correct a translation without recompiling. Missing keys fall back to English.
    /// </summary>
    public static class Loc
    {
        private static readonly Dictionary<string, string> English = BuildEnglish();
        private static readonly Dictionary<string, string> Current = new Dictionary<string, string>();

        public static string Language { get; private set; } = "en";

        public static bool IsRtl
        {
            get { return Language == "ar" || Language == "he" || Language == "fa" || Language == "ur"; }
        }

        /// <summary>Language codes that have a .lang file, with their display names.</summary>
        public static List<KeyValuePair<string, string>> Discover(string languagesDir)
        {
            var list = new List<KeyValuePair<string, string>>();
            if (!Directory.Exists(languagesDir)) return list;
            foreach (var file in Directory.GetFiles(languagesDir, "*.lang"))
            {
                string code = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                if (code == "en") continue;
                var map = Parse(file);
                string name;
                list.Add(new KeyValuePair<string, string>(code, map.TryGetValue("lang.name", out name) ? name : code));
            }
            list.Sort((a, b) => string.Compare(a.Key, b.Key, StringComparison.Ordinal));
            return list;
        }

        /// <summary>"auto" picks the Windows display language when a matching file exists, otherwise English.</summary>
        public static string Resolve(string setting, string languagesDir)
        {
            if (string.IsNullOrWhiteSpace(setting) || setting.Equals("auto", StringComparison.OrdinalIgnoreCase))
            {
                string two = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
                return File.Exists(Path.Combine(languagesDir, two + ".lang")) ? two : "en";
            }
            return setting.ToLowerInvariant();
        }

        public static void Load(string code, string languagesDir)
        {
            Current.Clear();
            Language = "en";
            if (string.IsNullOrWhiteSpace(code) || code.Equals("en", StringComparison.OrdinalIgnoreCase)) return;

            string file = Path.Combine(languagesDir, code.ToLowerInvariant() + ".lang");
            if (!File.Exists(file)) return;
            foreach (var kv in Parse(file)) Current[kv.Key] = kv.Value;
            Language = code.ToLowerInvariant();
        }

        public static string T(string key)
        {
            string value;
            if (Current.TryGetValue(key, out value)) return value;
            if (English.TryGetValue(key, out value)) return value;
            return key;
        }

        public static string F(string key, params object[] args)
        {
            return string.Format(CultureInfo.CurrentCulture, T(key), args);
        }

        /// <summary>All built-in English keys (used by tooling that checks translation files for completeness).</summary>
        public static IEnumerable<string> EnglishKeys { get { return English.Keys; } }

        private static Dictionary<string, string> Parse(string file)
        {
            var map = new Dictionary<string, string>();
            foreach (var raw in File.ReadAllLines(file, Encoding.UTF8))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                map[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim().Replace("\\n", "\n");
            }
            return map;
        }

        private static Dictionary<string, string> BuildEnglish()
        {
            return new Dictionary<string, string>
            {
                { "app.title", "NWC Folder Collector - BIM TOOLS" },
                { "tab.source", "Source and grouping" },
                { "tab.filters", "Filters" },
                { "tab.output", "Order and output" },

                { "lbl.preset", "Profile:" },
                { "btn.loadFile", "Open profile file..." },
                { "btn.saveAs", "Save profile..." },
                { "lbl.language", "Language:" },
                { "lang.auto", "Automatic" },
                { "lang.en", "English" },

                { "lbl.root", "Root folder:" },
                { "lbl.output", "Output folder:" },
                { "btn.browse", "Browse..." },
                { "lbl.mode", "Grouping scenario:" },
                { "lbl.folderNames", "Folder names (comma separated):" },
                { "lbl.folderPattern", "or folder-name pattern (regex):" },
                { "hint.folderNames", "Exact names, e.g. 1A1, 1A2. Leave empty to use the pattern." },
                { "lbl.depth", "Folder level (1 = first level below root):" },
                { "lbl.delims", "Name delimiters (characters):" },
                { "lbl.tokenIndex", "Part number (negative = from the end):" },
                { "lbl.fileRegex", "File-name regex (group 1 or (?<group>...)):" },
                { "hint.fileRegex", "Applied to the file name without extension." },
                { "lbl.singleName", "Output name (empty = root folder name):" },
                { "lbl.unmatched", "Name for unmatched files (empty = skip):" },
                { "chk.rootFiles", "Include files directly in the root folder" },
                { "lbl.rootGroup", "as group:" },
                { "chk.rootOnly", "Root folder only (no subfolders)" },
                { "lbl.maxDepth", "Max. scan depth (0 = unlimited):" },

                { "lbl.ext", "File extensions (comma separated):" },
                { "lbl.inc", "Only file names matching (regex):" },
                { "lbl.exc", "Exclude file names matching (regex):" },
                { "lbl.excFolders", "Exclude folder names (comma separated):" },
                { "lbl.excFolderRx", "Exclude folders matching (regex):" },
                { "lbl.minSize", "Minimum file size (KB):" },
                { "lbl.modAfter", "Modified after:" },
                { "chk.latest", "Use only the latest revision of each file" },
                { "lbl.revRegex", "Revision suffix pattern (regex):" },

                { "lbl.fileOrder", "Append order of files:" },
                { "lbl.priority", "Priority words (comma separated):" },
                { "lbl.groupOrder", "Order of output files:" },
                { "lbl.outLocation", "Output location:" },
                { "lbl.template", "Output name template:" },
                { "hint.template", "Tokens: {name} {root} {date} {datetime} {count} {part}" },
                { "lbl.format", "Output format:" },
                { "lbl.policy", "If the output file already exists:" },
                { "chk.upToDate", "Skip outputs whose source files did not change" },
                { "lbl.maxFiles", "Max. files per output (0 = no limit):" },
                { "chk.csv", "Write CSV manifest" },
                { "chk.openFirst", "Open first file instead of Append (fallback)" },
                { "chk.openFolder", "Open output folder when finished" },

                { "btn.scan", "Scan / Preview" },
                { "btn.start", "Start" },
                { "btn.cancel", "Cancel" },

                { "col.name", "Name" },
                { "col.folders", "Folders" },
                { "col.files", "Files" },
                { "col.local", "Local" },
                { "col.notLocal", "Not local" },
                { "col.status", "Status" },

                { "mode.FolderName", "Same-name folders (merge 1A1 from everywhere)" },
                { "mode.EachFolder", "Each folder becomes its own file" },
                { "mode.FolderAtDepth", "By folder at a fixed level" },
                { "mode.FileNameToken", "By part of the file name (e.g. ISO 19650 code)" },
                { "mode.FileNameRegex", "By file-name regex" },
                { "mode.SingleOutput", "Everything into one federated file" },
                { "mode.EachFile", "One file per source file" },
                { "mode.desc.FolderName", "Finds every folder with the same exact name anywhere under the root and merges all files inside them." },
                { "mode.desc.EachFolder", "Every folder that directly contains files produces its own output." },
                { "mode.desc.FolderAtDepth", "Groups by the name of the folder at the chosen level, e.g. level 1 = building or zone." },
                { "mode.desc.FileNameToken", "Splits the file name at delimiters and groups by one part, e.g. the discipline or role code." },
                { "mode.desc.FileNameRegex", "Groups by the text captured by a regular expression on the file name." },
                { "mode.desc.SingleOutput", "All collected files go into one output (a federated model)." },
                { "mode.desc.EachFile", "Every source file is converted into its own output file." },

                { "order.PathAscending", "By full path" },
                { "order.NameAscending", "By file name (A-Z, natural)" },
                { "order.ModifiedAscending", "Oldest first" },
                { "order.ModifiedDescending", "Newest first" },
                { "order.SizeAscending", "Smallest first" },
                { "order.SizeDescending", "Largest first" },
                { "order.PriorityList", "By priority words, then name" },
                { "gorder.NameAscending", "By name" },
                { "gorder.FileCountDescending", "Most files first" },
                { "gorder.FileCountAscending", "Fewest files first" },
                { "loc.SingleFolder", "One output folder" },
                { "loc.SubfolderPerGroup", "A subfolder per output" },
                { "loc.NextToSource", "Next to the source files" },
                { "fmt.Nwd", "NWD (self-contained)" },
                { "fmt.Nwf", "NWF (links to the source files)" },
                { "policy.Ask", "Ask me" },
                { "policy.Overwrite", "Overwrite" },
                { "policy.Skip", "Skip" },
                { "policy.AutoVersion", "Save as new version (_v2, _v3...)" },
                { "policy.ArchiveOld", "Move the old file to _Archive" },

                { "msg.ready", "Ready." },
                { "msg.cannotScan", "Cannot scan" },
                { "msg.scanFirst", "Scan first." },
                { "msg.selectOutput", "Select an output folder." },
                { "msg.noGroups", "No groups with files are selected." },
                { "msg.workerMissing", "Worker not found:" },
                { "msg.existsTitle", "Existing output files" },
                { "msg.existsAsk", "These output files already exist:\n\n{0}\n\nYes = overwrite them\nNo = skip those\nCancel = do not start" },
                { "msg.existsConfirm", "These output files already exist and will be overwritten:\n\n{0}\n\nContinue?" },
                { "msg.scanDone", "Scan finished: {0} group(s), {1} file(s), {2} folder(s) visited." },
                { "msg.scanCancelled", "Scan cancelled." },
                { "msg.finishedOk", "Finished: all outputs OK." },
                { "msg.finishedProblems", "Finished with problems - see the log files in the output folder." },
                { "msg.cancelled", "Cancelled." },
                { "msg.failedExit", "The worker failed (exit code {0})." },
                { "msg.cancelling", "Cancelling..." },
                { "msg.closeRunning", "A build is running. Stop it and close?" },
                { "msg.profileName", "Profile name:" },
                { "msg.saveProfileTitle", "Save profile" },
                { "msg.profileSaved", "Profile saved: {0}" },
                { "msg.profileInvalid", "The profile file could not be read: {0}" }
            };
        }
    }
}
