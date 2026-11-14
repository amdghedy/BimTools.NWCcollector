using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using BIMTools.NwcCollector.Core.Contracts;
using BIMTools.NwcCollector.Core.Planning;
using BIMTools.NwcCollector.Core.Protocol;
using BIMTools.NwcCollector.Core.Reporting;

namespace BIMTools.NwcCollector.Worker
{
    /// <summary>Exit codes: 0 all groups ok, 1 at least one group failed/partial, 2 fatal error, 3 cancelled.</summary>
    internal static class Program
    {
        // The Automation API is COM based: keep everything on one STA thread.
        [STAThread]
        private static int Main(string[] args)
        {
            Console.OutputEncoding = new UTF8Encoding(false);
            try
            {
                var cmd = CommandLine.Parse(args);
                if (!cmd.DryRun) NavisworksLocator.Install(cmd.NavisworksDir); // must run before anything uses Navisworks types
                return Run(cmd);
            }
            catch (Exception ex)
            {
                Console.Out.WriteLine(WorkerProtocol.Format(WorkerProtocol.Log, "FATAL " + ex.GetType().Name + ": " + ex.Message));
                Console.Error.WriteLine(ex.ToString());
                return 2;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int Run(CommandLine cmd)
        {
            JobSpec job;
            if (cmd.PocOutput != null) job = BuildPocJob(cmd);
            else if (cmd.Auto) job = BuildAutoJob(cmd);
            else job = JobSerializer.Read(cmd.JobPath);

            if (cmd.DryRun)
            {
                Log("Dry run: " + job.Groups.Count + " output file(s) would be built. Nothing was written.");
                foreach (var g in job.Groups)
                {
                    Log("  " + g.OutputPath + "  (" + g.Files.Count + " file(s))");
                    foreach (var f in g.Files) Log("      " + f);
                }
                return 0;
            }

            if (string.IsNullOrEmpty(job.ReportDirectory)) job.ReportDirectory = job.OutputDirectory;
            Directory.CreateDirectory(job.OutputDirectory);
            Directory.CreateDirectory(job.ReportDirectory);

            var processor = new GroupProcessor(job);
            var results = new List<GroupResult>();
            bool cancelled = false;

            foreach (var group in job.Groups)
            {
                GroupResult r = processor.CancelRequested ? processor.Cancelled(group) : processor.Process(group);
                if (r.Status == GroupStatus.Cancelled) cancelled = true;
                results.Add(r);
            }

            try
            {
                GroupLogWriter.WriteSummary(results, Path.Combine(job.ReportDirectory, "_NwcFolderCollector_summary.txt"));
                if (job.WriteCsv) GroupLogWriter.WriteCsv(results, Path.Combine(job.ReportDirectory, "_NwcFolderCollector_manifest.csv"));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Log("Could not write summary/manifest: " + ex.Message);
            }

            int ok = 0;
            foreach (var r in results)
                if (r.Status == GroupStatus.Success || r.Status == GroupStatus.SkippedExists || r.Status == GroupStatus.SkippedUpToDate) ok++;

            Console.Out.WriteLine(WorkerProtocol.Format(WorkerProtocol.JobDone, ok, results.Count));

            if (cancelled) return 3;
            return ok == results.Count ? 0 : 1;
        }

        private static JobSpec BuildAutoJob(CommandLine cmd)
        {
            CollectorProfile profile = ProfileSerializer.Read(cmd.ProfilePath);
            if (!string.IsNullOrWhiteSpace(cmd.Policy))
                profile.ExistingFilePolicyValue = EnumUtil.Parse(cmd.Policy, ExistingFilePolicy.Skip);
            if (profile.ExistingFilePolicyValue == ExistingFilePolicy.Ask)
                profile.ExistingFilePolicyValue = ExistingFilePolicy.Skip; // unattended: never overwrite without an explicit policy

            Log("Profile '" + profile.Name + "', scenario " + profile.Mode + ", root " + cmd.Root);
            PlanResult plan = new GroupPlanner().Plan(profile, cmd.Root, CancellationToken.None);
            Log("Scanned " + plan.FoldersVisited + " folder(s): " + plan.FilesFound + " file(s) found, " + plan.FilesExcluded + " excluded by filters, " +
                plan.FilesUnassigned + " without a group, " + plan.Groups.Count + " group(s).");
            foreach (var e in plan.Errors) Log("Scan error: " + e);
            foreach (var w in plan.Warnings) Log("Warning: " + w);

            return OutputPlanner.BuildJob(profile, plan.Groups, cmd.Root, cmd.Output);
        }

        private static JobSpec BuildPocJob(CommandLine cmd)
        {
            string output = Path.GetFullPath(cmd.PocOutput);
            return new JobSpec
            {
                OutputDirectory = Path.GetDirectoryName(output),
                ReportDirectory = Path.GetDirectoryName(output),
                ExistingFilePolicy = ExistingFilePolicy.Overwrite.ToString(),
                OpenFirstFile = cmd.OpenFirst,
                WriteCsv = true,
                Groups = new List<GroupJob>
                {
                    new GroupJob
                    {
                        Name = Path.GetFileNameWithoutExtension(output),
                        GroupKey = Path.GetFileNameWithoutExtension(output),
                        SourceFolders = new List<string>(),
                        Files = cmd.PocFiles,
                        OutputPath = output
                    }
                }
            };
        }

        private static void Log(string text)
        {
            Console.Out.WriteLine(WorkerProtocol.Format(WorkerProtocol.Log, text));
        }
    }
}
