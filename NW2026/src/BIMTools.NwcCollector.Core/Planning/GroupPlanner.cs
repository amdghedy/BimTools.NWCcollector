using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using BIMTools.NwcCollector.Core.Contracts;
using BIMTools.NwcCollector.Core.Scanning;

namespace BIMTools.NwcCollector.Core.Planning
{
    public sealed class PlanResult
    {
        public List<FolderGroup> Groups { get; } = new List<FolderGroup>();
        public List<string> Errors { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();
        public int FoldersVisited { get; set; }
        public int FilesFound { get; set; }
        public int FilesExcluded { get; set; }
        public int FilesUnassigned { get; set; }
        public int FilesSuperseded { get; set; }
    }

    /// <summary>
    /// Turns a profile + root folder into groups of files: collect -> assign to groups (by scenario) ->
    /// optional latest-revision filter -> ordering. Contains no Navisworks code, so the same planner runs
    /// in the launcher (preview) and in the worker (headless runs).
    /// </summary>
    public sealed class GroupPlanner
    {
        private sealed class Context
        {
            public string Root;
            public string RootName;
            public CollectorProfile Profile;
            public HashSet<string> Names;
            public Regex FolderRx;
            public Regex FileRx;
            public char[] Delimiters;
        }

        public PlanResult Plan(CollectorProfile profile, string root, CancellationToken ct, IProgress<string> progress = null)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            profile.Normalize();
            var ctx = BuildContext(profile, root);

            var collected = new FileCollector().Collect(profile, ctx.Root, ct, progress);
            var plan = new PlanResult
            {
                FoldersVisited = collected.FoldersVisited,
                FilesFound = collected.Files.Count,
                FilesExcluded = collected.FilesExcluded
            };
            plan.Errors.AddRange(collected.Errors);

            var groups = new Dictionary<string, FolderGroup>(StringComparer.OrdinalIgnoreCase);
            var seen = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            var assignments = new List<KeyValuePair<string, string>>();

            foreach (var file in collected.Files)
            {
                ct.ThrowIfCancellationRequested();
                assignments.Clear();
                Assign(ctx, file, assignments);
                if (assignments.Count == 0) { plan.FilesUnassigned++; continue; }

                foreach (var a in assignments)
                {
                    FolderGroup g;
                    if (!groups.TryGetValue(a.Key, out g))
                    {
                        g = new FolderGroup { Name = a.Key };
                        groups[a.Key] = g;
                        seen[a.Key] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    }
                    if (!string.IsNullOrEmpty(a.Value) && !g.SourceFolders.Contains(a.Value, StringComparer.OrdinalIgnoreCase))
                        g.SourceFolders.Add(a.Value);

                    string full = System.IO.Path.GetFullPath(file.Path);
                    if (!seen[a.Key].Add(full)) continue; // identity is the full path, never the file name
                    g.Files.Add(new NwcFileInfo
                    {
                        Path = full,
                        Length = file.Length,
                        LastWriteUtc = file.LastWriteUtc,
                        IsLocal = file.IsLocal,
                        UnavailableReason = file.Reason
                    });
                }
            }

            Regex revision = profile.LatestRevisionOnly ? RegexUtil.Make(profile.RevisionRegex, "Revision pattern") : null;
            foreach (var g in groups.Values)
            {
                if (revision != null) KeepLatestRevisions(g, revision, plan);
                var ordered = OrderFiles(g.Files, profile).ToList();
                g.Files.Clear();
                g.Files.AddRange(ordered);
            }

            IEnumerable<FolderGroup> orderedGroups;
            switch (profile.GroupOrderValue)
            {
                case GroupOrder.FileCountDescending:
                    orderedGroups = groups.Values.OrderByDescending(g => g.TotalCount).ThenBy(g => g.Name, NaturalComparer.Instance);
                    break;
                case GroupOrder.FileCountAscending:
                    orderedGroups = groups.Values.OrderBy(g => g.TotalCount).ThenBy(g => g.Name, NaturalComparer.Instance);
                    break;
                default:
                    orderedGroups = groups.Values.OrderBy(g => g.Name, NaturalComparer.Instance);
                    break;
            }
            plan.Groups.AddRange(orderedGroups);

            if (plan.FilesUnassigned > 0)
                plan.Warnings.Add(plan.FilesUnassigned + " file(s) passed the filters but belong to no group (for example files in the root folder " +
                                  "while 'include root files' is off, or file names that do not match the scenario rule).");
            if (plan.Groups.Count == 0 && collected.Files.Count == 0)
                plan.Warnings.Add("No files with the selected extension(s) were found under the root folder.");
            return plan;
        }

        // ------------------------------------------------------------------ validation

        private static Context BuildContext(CollectorProfile p, string root)
        {
            if (string.IsNullOrWhiteSpace(root)) throw new ArgumentException("Root directory is not set.");
            if (!System.IO.Directory.Exists(root)) throw new ArgumentException("Root directory does not exist: " + root);

            var ctx = new Context { Profile = p, Root = System.IO.Path.GetFullPath(root) };
            string trimmed = ctx.Root.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
            string rootName = System.IO.Path.GetFileName(trimmed);
            if (string.IsNullOrEmpty(rootName)) rootName = trimmed.Replace(":", "");
            ctx.RootName = string.IsNullOrEmpty(rootName) ? "Root" : rootName;

            switch (p.ModeValue)
            {
                case GroupingMode.FolderName:
                    ctx.Names = new HashSet<string>(p.FolderNames.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim()), StringComparer.OrdinalIgnoreCase);
                    if (ctx.Names.Count == 0)
                    {
                        if (string.IsNullOrWhiteSpace(p.FolderNamePattern))
                            throw new ArgumentException("Enter folder names or a folder-name pattern.");
                        ctx.FolderRx = RegexUtil.Make(p.FolderNamePattern, "Folder-name pattern");
                    }
                    break;
                case GroupingMode.FolderAtDepth:
                    if (p.Depth < 1) throw new ArgumentException("Folder depth must be 1 or more.");
                    break;
                case GroupingMode.FileNameToken:
                    if (string.IsNullOrEmpty(p.TokenDelimiters)) throw new ArgumentException("Enter at least one name delimiter.");
                    ctx.Delimiters = p.TokenDelimiters.ToCharArray();
                    break;
                case GroupingMode.FileNameRegex:
                    if (string.IsNullOrWhiteSpace(p.FileNameRegex)) throw new ArgumentException("Enter a file-name regular expression.");
                    ctx.FileRx = RegexUtil.Make(p.FileNameRegex, "File-name regex");
                    break;
            }
            return ctx;
        }

        // ------------------------------------------------------------------ assignment

        private static void Assign(Context c, FileCandidate f, List<KeyValuePair<string, string>> output)
        {
            var p = c.Profile;
            string[] parts = f.DirParts;
            string nameNoExt = System.IO.Path.GetFileNameWithoutExtension(f.Path);

            switch (p.ModeValue)
            {
                case GroupingMode.FolderName:
                {
                    if (parts.Length == 0) { AddRoot(c, output); return; }
                    var added = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < parts.Length; i++)
                    {
                        bool target = c.Names != null && c.Names.Count > 0 ? c.Names.Contains(parts[i]) : (c.FolderRx != null && c.FolderRx.IsMatch(parts[i]));
                        if (target && added.Add(parts[i]))
                            output.Add(new KeyValuePair<string, string>(parts[i], AncestorPath(c, parts, i + 1)));
                    }
                    return;
                }
                case GroupingMode.EachFolder:
                    if (parts.Length == 0) { AddRoot(c, output); return; }
                    output.Add(new KeyValuePair<string, string>(string.Join("_", parts), f.Directory));
                    return;

                case GroupingMode.FolderAtDepth:
                    if (parts.Length == 0) { AddRoot(c, output); return; }
                    if (parts.Length >= p.Depth)
                        output.Add(new KeyValuePair<string, string>(parts[p.Depth - 1], AncestorPath(c, parts, p.Depth)));
                    return;

                case GroupingMode.FileNameToken:
                {
                    var tokens = nameNoExt.Split(c.Delimiters, StringSplitOptions.RemoveEmptyEntries);
                    int idx = p.TokenIndex > 0 ? p.TokenIndex - 1 : (p.TokenIndex < 0 ? tokens.Length + p.TokenIndex : 0);
                    if (idx >= 0 && idx < tokens.Length) output.Add(new KeyValuePair<string, string>(tokens[idx], f.Directory));
                    else AddUnmatched(p, f, output);
                    return;
                }
                case GroupingMode.FileNameRegex:
                {
                    var m = c.FileRx.Match(nameNoExt);
                    string key = null;
                    if (m.Success)
                    {
                        var named = m.Groups["group"];
                        if (named.Success && named.Length > 0) key = named.Value;
                        else if (m.Groups.Count > 1 && m.Groups[1].Success && m.Groups[1].Length > 0) key = m.Groups[1].Value;
                    }
                    if (key != null) output.Add(new KeyValuePair<string, string>(key, f.Directory));
                    else AddUnmatched(p, f, output);
                    return;
                }
                case GroupingMode.SingleOutput:
                    output.Add(new KeyValuePair<string, string>(string.IsNullOrWhiteSpace(p.SingleOutputName) ? c.RootName : p.SingleOutputName.Trim(), c.Root));
                    return;

                case GroupingMode.EachFile:
                    output.Add(new KeyValuePair<string, string>(nameNoExt, f.Directory));
                    return;
            }
        }

        private static void AddRoot(Context c, List<KeyValuePair<string, string>> output)
        {
            if (!c.Profile.IncludeRootFiles) return;
            string name = string.IsNullOrWhiteSpace(c.Profile.RootFilesGroupName) ? "Root" : c.Profile.RootFilesGroupName.Trim();
            output.Add(new KeyValuePair<string, string>(name, c.Root));
        }

        private static void AddUnmatched(CollectorProfile p, FileCandidate f, List<KeyValuePair<string, string>> output)
        {
            if (string.IsNullOrWhiteSpace(p.UnmatchedGroupName)) return;
            output.Add(new KeyValuePair<string, string>(p.UnmatchedGroupName.Trim(), f.Directory));
        }

        private static string AncestorPath(Context c, string[] parts, int count)
        {
            string path = c.Root;
            for (int i = 0; i < count; i++) path = System.IO.Path.Combine(path, parts[i]);
            return path;
        }

        // ------------------------------------------------------------------ revisions and ordering

        /// <summary>Among files that differ only by a revision suffix (_R03, -v2, Rev 4), keep the newest one.</summary>
        private static void KeepLatestRevisions(FolderGroup g, Regex revision, PlanResult plan)
        {
            var buckets = g.Files.GroupBy(f => revision.Replace(System.IO.Path.GetFileNameWithoutExtension(f.Path), "").Trim().ToLowerInvariant()).ToList();
            var kept = new List<NwcFileInfo>();
            foreach (var b in buckets)
            {
                var winner = b.OrderByDescending(f => f.LastWriteUtc).ThenByDescending(f => f.Path, StringComparer.OrdinalIgnoreCase).First();
                kept.Add(winner);
                foreach (var loser in b.Where(f => !ReferenceEquals(f, winner)))
                {
                    plan.FilesSuperseded++;
                    plan.Warnings.Add("Older revision not used in '" + g.Name + "': " + loser.Path);
                }
            }
            g.Files.Clear();
            g.Files.AddRange(kept);
        }

        private static IEnumerable<NwcFileInfo> OrderFiles(IEnumerable<NwcFileInfo> files, CollectorProfile p)
        {
            var natural = NaturalComparer.Instance;
            switch (p.FileOrderValue)
            {
                case FileOrder.NameAscending:
                    return files.OrderBy(f => System.IO.Path.GetFileName(f.Path), natural).ThenBy(f => f.Path, natural);
                case FileOrder.ModifiedAscending:
                    return files.OrderBy(f => f.LastWriteUtc).ThenBy(f => f.Path, natural);
                case FileOrder.ModifiedDescending:
                    return files.OrderByDescending(f => f.LastWriteUtc).ThenBy(f => f.Path, natural);
                case FileOrder.SizeAscending:
                    return files.OrderBy(f => f.Length).ThenBy(f => f.Path, natural);
                case FileOrder.SizeDescending:
                    return files.OrderByDescending(f => f.Length).ThenBy(f => f.Path, natural);
                case FileOrder.PriorityList:
                {
                    var words = p.PriorityWords.Where(w => !string.IsNullOrWhiteSpace(w)).Select(w => w.Trim()).ToList();
                    return files.OrderBy(f => PriorityIndex(System.IO.Path.GetFileName(f.Path), words))
                                .ThenBy(f => System.IO.Path.GetFileName(f.Path), natural)
                                .ThenBy(f => f.Path, natural);
                }
                default:
                    return files.OrderBy(f => f.Path, natural);
            }
        }

        private static int PriorityIndex(string fileName, IList<string> words)
        {
            for (int i = 0; i < words.Count; i++)
                if (fileName.IndexOf(words[i], StringComparison.OrdinalIgnoreCase) >= 0) return i;
            return words.Count;
        }
    }
}
