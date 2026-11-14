using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BIMTools.NwcCollector.Core.Planning
{
    /// <summary>
    /// A small sidecar file (&lt;output&gt;.sources) records path, size and timestamp of every source file used for an
    /// output. "Skip groups whose sources did not change" compares the current state with it.
    /// </summary>
    public static class SourceSignature
    {
        public static string ManifestPath(string outputPath)
        {
            return outputPath + ".sources";
        }

        public static string Compute(IEnumerable<string> files)
        {
            var lines = new List<string>();
            foreach (var f in files)
            {
                try
                {
                    var fi = new FileInfo(f);
                    lines.Add(f.ToLowerInvariant() + "|" + fi.Length + "|" + fi.LastWriteTimeUtc.Ticks);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    lines.Add(f.ToLowerInvariant() + "|?|?");
                }
            }
            lines.Sort(StringComparer.Ordinal);
            return string.Join("\n", lines);
        }

        public static bool IsUpToDate(string outputPath, IEnumerable<string> files)
        {
            string manifest = ManifestPath(outputPath);
            if (!File.Exists(outputPath) || !File.Exists(manifest)) return false;
            string stored = File.ReadAllText(manifest).Replace("\r\n", "\n").TrimEnd('\n');
            return string.Equals(stored, Compute(files), StringComparison.Ordinal);
        }

        public static void Write(string outputPath, IEnumerable<string> files)
        {
            File.WriteAllText(ManifestPath(outputPath), Compute(files), new UTF8Encoding(false));
        }
    }
}
