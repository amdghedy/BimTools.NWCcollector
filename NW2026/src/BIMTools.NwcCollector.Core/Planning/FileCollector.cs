using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using BIMTools.NwcCollector.Core.Contracts;
using BIMTools.NwcCollector.Core.Scanning;

namespace BIMTools.NwcCollector.Core.Planning
{
    public sealed class FileCandidate
    {
        public string Path { get; set; }
        public string Directory { get; set; }

        /// <summary>Folder names between the root and the file (empty for a file directly in the root).</summary>
        public string[] DirParts { get; set; }

        public long Length { get; set; }
        public DateTime LastWriteUtc { get; set; }
        public bool IsLocal { get; set; }
        public string Reason { get; set; }
    }

    public sealed class CollectResult
    {
        public List<FileCandidate> Files { get; } = new List<FileCandidate>();
        public List<string> Errors { get; } = new List<string>();
        public int FoldersVisited { get; set; }
        public int FilesExcluded { get; set; }
        public int FoldersExcluded { get; set; }
    }

    /// <summary>
    /// Walks the root once (no fixed hierarchy assumed) and returns every file that passes the profile's filters.
    /// Excluded folders are never entered, which keeps scans of large drives fast.
    /// </summary>
    public sealed class FileCollector
    {
        private const int HardDepthLimit = 64;

        private sealed class Frame
        {
            public string Dir;
            public string[] Parts;
        }

        public CollectResult Collect(CollectorProfile p, string root, CancellationToken ct, IProgress<string> progress)
        {
            var result = new CollectResult();
            string fullRoot = System.IO.Path.GetFullPath(root);

            var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in p.Extensions)
            {
                string x = (e ?? "").Trim();
                if (x.Length == 0) continue;
                extensions.Add(x.StartsWith(".", StringComparison.Ordinal) ? x : "." + x);
            }
            if (extensions.Count == 0) extensions.Add(".nwc");

            Regex include = RegexUtil.Make(p.IncludeFileRegex, "Include file names");
            Regex exclude = RegexUtil.Make(p.ExcludeFileRegex, "Exclude file names");
            Regex excludeFolderRx = RegexUtil.Make(p.ExcludeFolderRegex, "Exclude folders");
            var excludedFolders = new HashSet<string>(
                p.ExcludeFolderNames.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim()),
                StringComparer.OrdinalIgnoreCase);

            long minBytes = Math.Max(0, p.MinSizeKb) * 1024;
            DateTime? modifiedAfter = ParseDate(p.ModifiedAfter);
            int limit = p.RootFolderOnly ? 0 : (p.MaxScanDepth > 0 ? p.MaxScanDepth : int.MaxValue);

            var stack = new Stack<Frame>();
            stack.Push(new Frame { Dir = fullRoot, Parts = new string[0] });

            while (stack.Count > 0)
            {
                ct.ThrowIfCancellationRequested();
                var frame = stack.Pop();
                result.FoldersVisited++;
                if (progress != null && result.FoldersVisited % 25 == 0) progress.Report(frame.Dir);

                string[] files;
                try { files = System.IO.Directory.GetFiles(frame.Dir); }
                catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException)
                {
                    result.Errors.Add("Cannot read folder '" + frame.Dir + "': " + ex.Message);
                    files = new string[0];
                }

                foreach (var file in files)
                {
                    if (!extensions.Contains(System.IO.Path.GetExtension(file))) continue;

                    string name = System.IO.Path.GetFileName(file);
                    if ((include != null && !include.IsMatch(name)) || (exclude != null && exclude.IsMatch(name)))
                    {
                        result.FilesExcluded++;
                        continue;
                    }

                    string reason;
                    long length;
                    DateTime lastWrite;
                    bool local = FileAvailability.Check(file, out reason, out length, out lastWrite);

                    if ((minBytes > 0 && length < minBytes) || (modifiedAfter.HasValue && lastWrite < modifiedAfter.Value))
                    {
                        result.FilesExcluded++;
                        continue;
                    }

                    result.Files.Add(new FileCandidate
                    {
                        Path = file,
                        Directory = frame.Dir,
                        DirParts = frame.Parts,
                        Length = length,
                        LastWriteUtc = lastWrite,
                        IsLocal = local,
                        Reason = reason
                    });
                }

                if (frame.Parts.Length >= limit) continue;
                if (frame.Parts.Length >= HardDepthLimit)
                {
                    result.Errors.Add("Maximum folder depth (" + HardDepthLimit + ") reached, not descending into: " + frame.Dir);
                    continue;
                }

                string[] subs;
                try { subs = System.IO.Directory.GetDirectories(frame.Dir); }
                catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException)
                {
                    result.Errors.Add("Cannot read folder '" + frame.Dir + "': " + ex.Message);
                    continue;
                }

                foreach (var sub in subs)
                {
                    string subName = System.IO.Path.GetFileName(sub);
                    if (excludedFolders.Contains(subName) || (excludeFolderRx != null && excludeFolderRx.IsMatch(subName)))
                    {
                        result.FoldersExcluded++;
                        continue;
                    }
                    var parts = new string[frame.Parts.Length + 1];
                    Array.Copy(frame.Parts, parts, frame.Parts.Length);
                    parts[parts.Length - 1] = subName;
                    stack.Push(new Frame { Dir = sub, Parts = parts });
                }
            }
            return result;
        }

        private static DateTime? ParseDate(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            DateTime d;
            if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal | DateTimeStyles.AdjustToUniversal, out d))
                return d;
            throw new ArgumentException("Invalid 'Modified after' date: " + text + " (use yyyy-MM-dd).");
        }
    }
}
