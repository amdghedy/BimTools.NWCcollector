<#
 Builds the solution.
 Usage:  .\build.ps1 [-NavisworksDir "C:\Program Files\Autodesk\Navisworks Manage 2025"] [-Configuration Release]
 Needs: Visual Studio 2022 (or the .NET SDK) with the .NET Framework 4.8 targeting pack, and a Navisworks 2025 install.
#>
param(
    [string]$NavisworksDir = "C:\Program Files\Autodesk\Navisworks Manage 2025",
    [string]$Configuration = "Release"
)
$ErrorActionPreference = "Stop"

foreach ($dll in "Autodesk.Navisworks.Api.dll", "Autodesk.Navisworks.Automation.dll") {
    $p = Join-Path $NavisworksDir $dll
    if (-not (Test-Path $p)) { throw "Missing $p - check -NavisworksDir and the exact DLL names in your 2025 install." }
}

$sln = Join-Path $PSScriptRoot "NwcFolderCollector.sln"
dotnet build $sln -c $Configuration -p:NavisworksDir="$NavisworksDir"
if ($LASTEXITCODE -ne 0) { throw "Build failed." }
Write-Host "Build succeeded." -ForegroundColor Green
