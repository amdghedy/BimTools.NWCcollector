<#
 Assembles the plug-in bundle in .\dist and (optionally) installs it.
 Usage:  .\deploy.ps1 [-Configuration Release] [-Install]
 -Install copies the bundle to %ProgramData%\Autodesk\ApplicationPlugins (run PowerShell as Administrator).
#>
param(
    [string]$Configuration = "Release",
    [switch]$Install
)
$ErrorActionPreference = "Stop"
$root    = $PSScriptRoot
$tfm     = "net48"   # keep in sync with AppTargetFramework in Directory.Build.props
$launcher = Join-Path $root "src\BIMTools.NwcCollector.Launcher\bin\$Configuration\$tfm"
$worker   = Join-Path $root "src\BIMTools.NwcCollector.Worker\bin\$Configuration\$tfm"

foreach ($p in $launcher, $worker) { if (-not (Test-Path $p)) { throw "Build output not found: $p. Run build.ps1 first." } }

$bundle   = Join-Path $root "dist\BIMTools.NwcFolderCollector.bundle"
$contents = Join-Path $bundle "Contents"
if (Test-Path $bundle) { Remove-Item $bundle -Recurse -Force }
New-Item -ItemType Directory -Path $contents | Out-Null

Copy-Item (Join-Path $root "deploy\PackageContents.xml") $bundle
Copy-Item (Join-Path $launcher "BIMTools.NwcCollector.Launcher.dll") $contents
Copy-Item (Join-Path $launcher "BIMTools.NwcCollector.Core.dll") $contents
Copy-Item (Join-Path $launcher "en-US") $contents -Recurse
Copy-Item (Join-Path $launcher "Profiles") $contents -Recurse    # ready-made scenarios (editable JSON)
Copy-Item (Join-Path $launcher "Languages") $contents -Recurse   # UI translations (editable text files)

$workerDest = Join-Path $contents "Worker"
New-Item -ItemType Directory -Path $workerDest | Out-Null
Copy-Item (Join-Path $worker "BIMTools.NwcCollector.Worker.exe") $workerDest
Copy-Item (Join-Path $worker "BIMTools.NwcCollector.Worker.exe.config") $workerDest -ErrorAction SilentlyContinue
Copy-Item (Join-Path $worker "BIMTools.NwcCollector.Core.dll") $workerDest

Write-Host "Bundle created: $bundle" -ForegroundColor Green

if ($Install) {
    $target = Join-Path $env:ProgramData "Autodesk\ApplicationPlugins\BIMTools.NwcFolderCollector.bundle"
    if (Test-Path $target) { Remove-Item $target -Recurse -Force }
    Copy-Item $bundle $target -Recurse
    Write-Host "Installed to $target. Restart Navisworks 2026." -ForegroundColor Green
}
