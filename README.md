# BimTools.NWCcollector

**BIM Tools NWC Folder Collector** – A Navisworks plug-in that collects NWC files from repeated folder names and builds one NWD per name.

> Built by **BIM TOOLS** | Author: [@amdghedy](https://github.com/amdghedy)

---

## Repository Structure

This repository contains two independent builds of the same plug-in, each targeting a different version of Autodesk Navisworks Manage:

| Folder | Target Version | .NET Target |
|--------|---------------|-------------|
| [`NW2026/`](./NW2026) | Navisworks Manage **2026** | .NET Framework 4.8 |
| [`NW2025/`](./NW2025) | Navisworks Manage **2025** | .NET Framework 4.8 |

Both builds share the same source code and project structure — only the Navisworks installation path, DLL references, and `PackageContents.xml` series version differ.

---

## Quick Start

### Build for Navisworks 2026

```powershell
cd NW2026
.\build.ps1
```

Requires Navisworks Manage 2026 installed at `C:\Program Files\Autodesk\Navisworks Manage 2026`  
(override with `-NavisworksDir "D:\your\path"`)

### Build for Navisworks 2025

```powershell
cd NW2025
.\build.ps1
```

Requires Navisworks Manage 2025 installed at `C:\Program Files\Autodesk\Navisworks Manage 2025`  
(override with `-NavisworksDir "D:\your\path"`)

---

## Projects

Each version folder contains:

- **BIMTools.NwcCollector.Core** – Shared business logic (folder scanning, NWD grouping)
- **BIMTools.NwcCollector.Launcher** – Navisworks plug-in entry point + ribbon UI
- **BIMTools.NwcCollector.Worker** – Headless automation worker (Roamer.exe-based)

---

## Requirements

- Windows 10/11 (64-bit)
- Autodesk Navisworks Manage 2025 **or** 2026
- .NET Framework 4.8
- Visual Studio 2022 or .NET SDK (for building from source)

---

## License

License not yet specified. Contact the maintainer before redistributing.
