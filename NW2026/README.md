# NWC Folder Collector (Navisworks 2026, BIM TOOLS tab) - version 2

Collects model files (NWC by default) from a project folder tree and builds NWD files from them. How the files are
grouped, filtered, ordered, named and saved is controlled by **profiles**, so the same tool fits different companies,
naming standards and folder structures, in several languages.

> **Status: written, NOT compiled, NOT tested.** The code was produced in an environment without the .NET SDK or any
> Navisworks assemblies. Expect a few compile errors on first build, mostly in the files listed under
> "API items to verify". Nothing here has been run against Navisworks.

## What is new in version 2

| Area | Options |
|---|---|
| **Grouping scenarios** | Same-name folders (1A1 everywhere) - each folder its own file - folder at a fixed level - by part of the file name (ISO 19650 codes, delimiter + part number) - by file-name regex - everything into one federated file - one file per source file |
| **Root-level files** | Include files that sit directly in the root folder (own group name), or scan the root folder only |
| **Filters** | Extensions (.nwc, .nwd, .ifc, .rvt, .dwg...), include / exclude file-name regex, exclude folder names / regex (defaults skip `$RECYCLE.BIN`, `System Volume Information`, `_Archive`), minimum size, modified-after date, max scan depth, latest revision only (`_R03`, `-v2`, `Rev 4`) |
| **Ordering** | Files: full path, name (natural: Level 2 before Level 10), oldest/newest, smallest/largest, **priority words** (e.g. ARC, STR, MEP first). Outputs: by name or by file count |
| **Output** | Name template `{name} {root} {date} {datetime} {count} {part}`, location (one folder / subfolder per output / next to the sources), NWD or NWF, split large groups into parts (max files per output) |
| **Existing files** | Ask, overwrite (confirmed), skip, save as new version `_v2`, move old file to `_Archive`, **skip outputs whose sources did not change** |
| **Reports** | Per-output log, summary, **CSV manifest** (one row per source file) |
| **Profiles** | 11 ready-made profiles, save your own, open a profile file, share company profiles through a network folder |
| **Headless mode** | `Worker.exe --auto` for Task Scheduler / build servers, `--dry-run` to print the plan without Navisworks |
| **International** | UI in English, Arabic (right-to-left), French, Spanish, German; add a language by dropping a `.lang` text file; long paths (>260 characters); Unicode names |

## Architecture

```
Navisworks 2026 (running)                          Separate process (headless, one instance per output)
+------------------------------+   job.json       +------------------------------------------+
| Launcher (plug-in DLL)       | ---------------> | Worker.exe                               |
|  BIM TOOLS > NWC Folder      |                  |  for each output file:                   |
|  Collector window            | <--------------- |   NavisworksApplication (Automation API) |
|  profile / scan / progress   |   stdout lines   |   AppendFile x N -> SaveFile .partial    |
+------------------------------+                  |   validate (size + reopen) -> rename     |
        uses Core                                 +------------------------------------------+
                                                          uses Core (also plans by itself in --auto mode)
```

* **Core** (`netstandard2.0`, no Navisworks dependency): profile model, file collector, grouping planner, output
  planner, source signatures, job contract, stdout protocol, log/CSV writers, localization.
* **Worker** (`net48` exe, x64): the only code that touches the Automation API (`AutomationSession.cs`).
* **Launcher** (plug-in DLL): ribbon command and window; no Navisworks document operations in-process.

## Build

Requirements: Visual Studio 2022 (or .NET SDK), .NET Framework 4.8 targeting pack, Navisworks Manage 2026 installed.

```powershell
.\build.ps1 -NavisworksDir "C:\Program Files\Autodesk\Navisworks Manage 2026"
```

If the 2026 SDK needs another target framework, change `AppTargetFramework` in `Directory.Build.props`.

## Step 1: proof of concept (do this before anything else)

```powershell
.\src\BIMTools.NwcCollector.Worker\bin\Release\net48\BIMTools.NwcCollector.Worker.exe `
  --poc C:\Temp\poc\test.nwd C:\path\a.nwc C:\path\b.nwc C:\path\c.nwc `
  --nwdir "C:\Program Files\Autodesk\Navisworks Manage 2026"
```

Open `test.nwd` and confirm all models are present at their original positions. If `AppendFile` fails on the empty
document, retry with `--open-first`.

## Step 2: check the planning on real folders, without Navisworks

`--dry-run` runs only the planner (scan, grouping, filters, order, names) and prints what would be built:

```powershell
BIMTools.NwcCollector.Worker.exe --auto --dry-run --profile ..\Profiles\04-root-files-only.json --root D:\NWC --output D:\NWC\All
```

## Step 3: deploy

```powershell
.\deploy.ps1 -Install      # run PowerShell as Administrator, then restart Navisworks
```

```
BIMTools.NwcFolderCollector.bundle\
  PackageContents.xml
  Contents\
    BIMTools.NwcCollector.Launcher.dll, BIMTools.NwcCollector.Core.dll
    en-US\BimToolsRibbon.xaml
    Profiles\*.json            ready-made scenarios
    Languages\*.lang           ar, fr, es, de
    Worker\                    BIMTools.NwcCollector.Worker.exe (+ .config, Core DLL)
```

## Profiles

* Stored as readable JSON. Folder paths are not part of a profile.
* Locations: built-in `Contents\Profiles`, personal `%AppData%\BIMTools\NwcFolderCollector\Profiles`, and a **company
  share**: set the environment variable `BIMTOOLS_NWC_PROFILES` to a network folder and every profile there appears in
  the list as "(shared)".
* Regular expressions are case-insensitive. Test them with `--dry-run` or **Scan / Preview**; double-click a group to
  list its files in append order.

## Adding or fixing a language

Copy `Languages\fr.lang`, rename it (for example `it.lang`), translate the values (keep `{0}`, `{name}` ... unchanged;
first line `lang.name=Italiano`). It appears in the language list at the next start. Right-to-left layout is applied
for `ar`, `he`, `fa`, `ur`. The shipped translations are machine-drafted: have native speakers review them. Log files
and worker messages stay in English on purpose (easier to support).

## API items to verify against your Navisworks 2026 install (written from memory)

| File | What to check |
|---|---|
| `Worker/AutomationSession.cs` | `Autodesk.Navisworks.Api.Automation.NavisworksApplication`: constructor, `OpenFile`, `AppendFile`, `SaveFile`, `Dispose`. The **only** file that touches the Automation API. |
| `Worker/*.csproj`, `Launcher/*.csproj` | DLL names and location of `Autodesk.Navisworks.Automation.dll` and `Autodesk.Navisworks.Api.dll`. |
| `Launcher/NwcFolderCollectorCommand.cs`, `Ribbon/en-US/BimToolsRibbon.xaml` | Plug-in / ribbon attribute shapes, XAML location. |
| `deploy/PackageContents.xml` | `RuntimeRequirements` for Navisworks 2026. |
| `Directory.Build.props` | Target framework required by the 2026 SDK. |
| Output format NWF | `SaveFile` choosing the format by file extension; an NWF only links to its sources, so moving the sources breaks it. |
| Non-NWC extensions | Whether `AppendFile` accepts .ifc / .rvt / .dwg through Navisworks file readers on your installation. |

## Known limits

* Validation = file exists, non-zero size, reopens in a second fresh Navisworks instance. It does not count models inside
  the saved file. "Appended" means `AppendFile` returned without an exception.
* Cloud-only detection uses file attributes and never triggers a download; test it with your Desktop Connector version.
* Files are appended as-is: no transforms or repositioning.
* Each output uses a Navisworks license seat while its worker instance runs.
* "Latest revision only" keeps the newest file (by modified date) among files whose names differ only by the revision
  suffix; adjust the pattern for your own revision convention.
* A folder name matched inside another matched folder puts its files in both groups (deduplicated per group by full path).
