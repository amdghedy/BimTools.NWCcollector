using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BIMTools.NwcCollector.Core.Contracts;
using BIMTools.NwcCollector.Core.Scanning;

namespace BIMTools.NwcCollector.Core.Planning
{
    /// <summary>
    /// Turns planned groups into a job: output names (template), location, format, splitting of very large
    /// groups, and guaranteed-unique names.
    /// Template tokens: {name} {root} {date} {datetime} {count} {part}
    /// </summary>
    public static class OutputPlanner
    {
        public static JobSpec BuildJob(CollectorProfile profile, IEnumerable<FolderGroup> groups, string rootDir, string outputDir)
        {
            profile.Normalize();
            string ext = profile.OutputFormatValue == OutputFormat.Nwf ? ".nwf" : ".nwd";
            string rootName = Path.GetFileName(Path.GetFullPath(rootDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrEmpty(rootName)) rootName = "Root";

            var job = new JobSpec
            {
                OutputDirectory = outputDir,
                ReportDirectory = outputDir,
                ExistingFilePolicy = profile.ExistingFilePolicy,
                SkipUpToDate = profile.SkipUpToDate,
                WriteCsv = profile.WriteCsv,
                OpenFirstFile = profile.OpenFirstFile,
                Groups = new List<GroupJob>()
            };

            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            DateTime now = DateTime.Now;

            foreach (var g in groups)
            {
                if (g.Files.Count == 0) continue;

                var paths = g.Files.Select(f => f.Path).ToList();
                int max = profile.MaxFilesPerOutput;
                int parts = (max > 0 && paths.Count > max) ? (int)Math.Ceiling(paths.Count / (double)max) : 1;

                for (int part = 1; part <= parts; part++)
                {
                    var slice = parts == 1 ? paths : paths.Skip((part - 1) * max).Take(max).ToList();
                    string baseName = ApplyTemplate(profile.OutputNameTemplate, g.Name, rootName, slice.Count, parts > 1 ? part : 0, now);
                    string unique = MakeUnique(usedNames, PathUtil.SafeFileName(baseName));

                    string dir;
                    switch (profile.OutputLocationValue)
                    {
                        case OutputLocation.SubfolderPerGroup:
                            dir = Path.Combine(outputDir, PathUtil.SafeFileName(g.Name));
                            break;
                        case OutputLocation.NextToSource:
                            dir = g.SourceFolders.FirstOrDefault() ?? outputDir;
                            break;
                        default:
                            dir = outputDir;
                            break;
                    }

                    job.Groups.Add(new GroupJob
                    {
                        Name = unique,
                        GroupKey = g.Name,
                        SourceFolders = g.SourceFolders.ToList(),
                        Files = slice,
                        OutputPath = Path.Combine(dir, unique + ext)
                    });
                }
            }
            return job;
        }

        public static string ApplyTemplate(string template, string name, string root, int count, int part, DateTime now)
        {
            string t = string.IsNullOrWhiteSpace(template) ? "{name}" : template;
            bool hasPart = t.IndexOf("{part}", StringComparison.OrdinalIgnoreCase) >= 0;

            t = Replace(t, "{name}", name);
            t = Replace(t, "{root}", root);
            t = Replace(t, "{datetime}", now.ToString("yyyyMMdd_HHmm", CultureInfo.InvariantCulture));
            t = Replace(t, "{date}", now.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
            t = Replace(t, "{count}", count.ToString(CultureInfo.InvariantCulture));
            t = Replace(t, "{part}", part > 0 ? part.ToString("00", CultureInfo.InvariantCulture) : "");

            t = t.Trim().TrimEnd('_', '-', ' ', '.');
            if (part > 0 && !hasPart) t += "_part" + part.ToString("00", CultureInfo.InvariantCulture);
            return string.IsNullOrWhiteSpace(t) ? "output" : t;
        }

        private static string Replace(string input, string token, string value)
        {
            return Regex.Replace(input, Regex.Escape(token), m => value, RegexOptions.IgnoreCase);
        }

        private static string MakeUnique(HashSet<string> used, string name)
        {
            string candidate = name;
            int i = 2;
            while (!used.Add(candidate)) candidate = name + "_" + i++;
            return candidate;
        }
    }
}
