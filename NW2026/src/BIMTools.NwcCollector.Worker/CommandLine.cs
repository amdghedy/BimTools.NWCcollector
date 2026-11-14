using System;
using System.Collections.Generic;

namespace BIMTools.NwcCollector.Worker
{
    /// <summary>
    /// Worker.exe --job job.json [--nwdir "C:\...\Navisworks Manage 2026"]
    ///     Run a job file written by the launcher.
    /// Worker.exe --auto --profile profile.json --root "D:\Project" --output "D:\NWD" [--policy Overwrite|Skip|AutoVersion|ArchiveOld] [--dry-run] [--nwdir ...]
    ///     Headless run for Task Scheduler / build servers: plans from the profile, then builds. --dry-run only prints the plan.
    /// Worker.exe --poc out.nwd a.nwc b.nwc ... [--open-first] [--nwdir "..."]
    ///     Proof of concept: one NWD, no planning.
    /// </summary>
    internal sealed class CommandLine
    {
        public string JobPath { get; private set; }
        public string NavisworksDir { get; private set; }
        public string PocOutput { get; private set; }
        public List<string> PocFiles { get; } = new List<string>();
        public bool OpenFirst { get; private set; }

        public bool Auto { get; private set; }
        public string ProfilePath { get; private set; }
        public string Root { get; private set; }
        public string Output { get; private set; }
        public string Policy { get; private set; }
        public bool DryRun { get; private set; }

        public static CommandLine Parse(string[] args)
        {
            var c = new CommandLine();
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (a == "--job" && i + 1 < args.Length) c.JobPath = args[++i];
                else if (a == "--nwdir" && i + 1 < args.Length) c.NavisworksDir = args[++i];
                else if (a == "--open-first") c.OpenFirst = true;
                else if (a == "--poc" && i + 1 < args.Length) c.PocOutput = args[++i];
                else if (a == "--auto") c.Auto = true;
                else if (a == "--profile" && i + 1 < args.Length) c.ProfilePath = args[++i];
                else if (a == "--root" && i + 1 < args.Length) c.Root = args[++i];
                else if (a == "--output" && i + 1 < args.Length) c.Output = args[++i];
                else if (a == "--policy" && i + 1 < args.Length) c.Policy = args[++i];
                else if (a == "--dry-run") c.DryRun = true;
                else if (c.PocOutput != null && !a.StartsWith("--", StringComparison.Ordinal)) c.PocFiles.Add(a);
                else throw new ArgumentException("Unknown or incomplete argument: " + a);
            }

            if (c.Auto)
            {
                if (c.ProfilePath == null || c.Root == null || c.Output == null)
                    throw new ArgumentException("--auto needs --profile <file.json> --root <folder> --output <folder>.");
            }
            else if (c.JobPath == null && c.PocOutput == null)
            {
                throw new ArgumentException("Usage: --job <job.json> | --auto --profile <p.json> --root <dir> --output <dir> [--policy X] [--dry-run] | --poc <out.nwd> <a.nwc> ... [--open-first]   (all accept --nwdir <dir>)");
            }
            if (c.PocOutput != null && c.PocFiles.Count == 0)
                throw new ArgumentException("--poc needs at least one source file.");
            return c;
        }
    }
}
