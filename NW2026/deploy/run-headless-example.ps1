<#
 Example: unattended run for Windows Task Scheduler or a build server (no Navisworks window, no user prompts).
 The worker plans from a profile (same rules as the window), builds every output file and writes logs, a summary
 and a CSV manifest into the output folder. Exit code: 0 ok, 1 some outputs failed, 2 fatal, 3 cancelled.

 Schedule it, for example, nightly:
   schtasks /Create /SC DAILY /ST 02:00 /TN "NWC Folder Collector" /TR "powershell -File C:\Tools\run-headless-example.ps1"
#>
param(
    [string]$Root    = "D:\Project\NWC",
    [string]$Output  = "D:\Project\NWD",
    [string]$Profile = "$env:ProgramData\Autodesk\ApplicationPlugins\BIMTools.NwcFolderCollector.bundle\Contents\Profiles\09-nightly-latest-revisions.json",
    [string]$Policy  = "ArchiveOld",
    [string]$NavisworksDir = "C:\Program Files\Autodesk\Navisworks Manage 2026",
    [switch]$DryRun
)
$worker = "$env:ProgramData\Autodesk\ApplicationPlugins\BIMTools.NwcFolderCollector.bundle\Contents\Worker\BIMTools.NwcCollector.Worker.exe"

$args = @("--auto", "--profile", $Profile, "--root", $Root, "--output", $Output, "--policy", $Policy, "--nwdir", $NavisworksDir)
if ($DryRun) { $args += "--dry-run" }   # prints the plan only; Navisworks is not started

& $worker @args
exit $LASTEXITCODE
