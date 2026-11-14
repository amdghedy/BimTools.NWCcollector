# BimTools.NWCcollector

**A focused BIM utility for collecting and organizing Navisworks Cache (NWC) exports from distributed project folders.**

[![GitHub](https://img.shields.io/badge/GitHub-BimTools.NWCcollector-181717?logo=github)](https://github.com/amdghedy/BimTools.NWCcollector) [![License](https://img.shields.io/badge/License-To%20be%20defined-lightgrey)](#license) [![Status](https://img.shields.io/badge/Status-Initial%20repository-blue)](#project-status)

> **Project status:** This repository is currently at its initial documentation stage. The implementation, supported operating systems, runtime requirements, and tested workflows have not yet been verified from source code. This README describes the intended purpose and proposed product direction; it does not claim that the features below are already implemented.

---

## 1. Overview

BIM coordination workflows often produce NWC files in multiple discipline, model, package, and revision folders. Finding the correct exports and assembling a consistent set for coordination can become repetitive and error-prone.

**BimTools.NWCcollector** is intended to make that preparation process easier by providing a controlled way to discover, collect, and organize NWC files from user-selected locations.

The goal is to:
- Reduce manual browsing across project directories.
- Make the collection process repeatable.
- Help users identify which NWC files have been gathered.
- Preserve source information so collected files can be traced back to their origin.
- Avoid silently overwriting files when names collide.

The tool is intended to support BIM/VDC coordination workflows. It is not intended to replace the authoring application or the coordination platform that consumes the collected NWC files.

## 2. Intended workflow

The proposed workflow is:
1. Select one or more source folders containing NWC exports.
2. Scan the selected locations for eligible NWC files.
3. Review the discovered files before collection.
4. Select the files to include.
5. Choose a destination folder.
6. Collect the selected files using a predictable conflict-handling policy.
7. Review a collection summary and any warnings.

The exact workflow and available controls will be documented against the implemented application once the source code is available.

## 3. Intended capabilities

The following items describe the product direction. They are **planned capabilities, not verified implementation claims**.

| Capability | Intended behavior | Status |
|---|---|---|
| Folder selection | Let users choose source locations | Planned / unverified |
| NWC discovery | Find files with the .nwc extension in selected locations | Planned / unverified |
| Preview before collection | Show discovered files before copying | Planned / unverified |
| Centralized collection | Gather selected files in a destination folder | Planned / unverified |
| Duplicate-name handling | Detect collisions and avoid accidental overwrites | Planned / unverified |
| Source traceability | Retain source-path information in the collection report | Planned / unverified |
| Collection report | Summarize collected, skipped, and failed files | Planned / unverified |
| Large-folder resilience | Handle inaccessible folders and file-system errors safely | Planned / unverified |

Features will be marked as available only after they are implemented and tested.

## 4. File handling principles

A reliable collector must treat source files carefully. The implementation should follow these principles:
- **No silent overwrite:** an existing destination file must not be replaced without an explicit, documented policy.
- **Source preservation:** collection should not modify or delete the original NWC files.
- **Visible outcomes:** users should be able to distinguish successful, skipped, and failed operations.
- **Error isolation:** one inaccessible file or folder should not unnecessarily terminate an entire batch.
- **Traceability:** where practical, the report should identify the source and destination paths.
- **Safe paths:** invalid paths, long paths, locked files, and insufficient permissions should be handled explicitly.
- **Predictable reruns:** running the same collection again should have documented behavior for files already present.

These are engineering requirements for the project, not assertions that every safeguard is already present.

## 5. Technology and compatibility

The implementation technology and minimum requirements have not yet been confirmed from repository source.

| Item | Verified information |
|---|---|
| Programming language | Not yet confirmed |
| Application type | Not yet confirmed |
| Minimum Windows version | Not yet confirmed |
| Required runtime | Not yet confirmed |
| CPU architecture | Not yet confirmed |
| External dependencies | Not yet confirmed |
| Supported NWC file versions | Not yet confirmed |
| Installer / portable distribution | Not yet confirmed |

Do not assume that installing a particular BIM application is required or that a standalone executable is available until the release documentation explicitly confirms it.

## 6. Getting started

There is no verified release or installation package documented in this repository yet.

Once a tested build is published, this section will contain:
1. The supported download link.
2. Any runtime prerequisites.
3. Installation or portable execution instructions.
4. A first-run walkthrough.
5. A sample collection workflow.
6. Troubleshooting guidance.

For now, use the repository's source and release pages to follow development:
- [Source repository](https://github.com/amdghedy/BimTools.NWCcollector)
- [Releases](https://github.com/amdghedy/BimTools.NWCcollector/releases)
- [Issues and bug reports](https://github.com/amdghedy/BimTools.NWCcollector/issues)

## 7. Recommended validation before production use

Before using a build on live project data, validate it with a controlled test set:
- A folder containing several valid NWC files.
- Nested folders with NWC files at different levels.
- Two files with the same filename in different source folders.
- Empty folders and folders without NWC files.
- A source file that is locked or inaccessible.
- A destination containing a file with the same name.
- A path containing spaces and non-English characters.
- A large collection with many files.
- A destination with insufficient free space or write permissions.

Confirm that original source files remain unchanged and that the report accurately reflects every operation.

## 8. Troubleshooting

Because the application implementation is not yet available for verification, the table below lists diagnostic scenarios rather than application-specific error messages.

| Scenario | What to check |
|---|---|
| No files are discovered | Confirm the selected source path and .nwc file extensions |
| A folder cannot be scanned | Check permissions, network availability, and whether the folder still exists |
| A file cannot be copied | Check whether it is locked, in use, or inaccessible |
| A destination conflict occurs | Check the existing destination file and the application's conflict policy |
| Collection is incomplete | Review the operation report and available disk space |
| A network source is unavailable | Confirm connectivity and access rights before retrying |

When reporting a defect, include the application version or commit, Windows version, steps to reproduce, expected result, actual result, and relevant log output. Remove confidential project names and paths before sharing logs publicly.

## 9. Reporting issues

Please use [GitHub Issues](https://github.com/amdghedy/BimTools.NWCcollector/issues) to report defects or request improvements.

A useful bug report should include:
- A clear and descriptive title.
- The exact steps required to reproduce the issue.
- Expected behavior and actual behavior.
- Application version or commit identifier.
- Operating system and runtime details, when known.
- A sanitized error message or log excerpt.
- A small sample dataset where it is safe and practical to share one.

Do not upload confidential models, client information, or proprietary project files to public issues.

## 10. Development roadmap

The roadmap below is a proposed sequence. Items should be moved to **Completed** only after the corresponding code and tests exist.

### Phase 1 — Repository and application foundation
- [ ] Define the application type and supported platform.
- [ ] Establish the solution and project structure.
- [ ] Define the NWC discovery and collection requirements.
- [ ] Add a reproducible build.
- [ ] Add baseline automated tests.

### Phase 2 — Core collection workflow
- [ ] Implement source-folder selection.
- [ ] Implement NWC file discovery.
- [ ] Add a reviewable file list.
- [ ] Implement destination selection.
- [ ] Implement safe file collection.
- [ ] Define duplicate and overwrite behavior.

### Phase 3 — Reliability and reporting
- [ ] Handle inaccessible folders and locked files.
- [ ] Add cancellation and progress reporting.
- [ ] Add collection summaries and diagnostic logs.
- [ ] Test large collections and network locations.
- [ ] Verify source-file preservation.

### Phase 4 — Distribution and documentation
- [ ] Publish verified prerequisites.
- [ ] Produce a repeatable release build.
- [ ] Document installation and upgrade behavior.
- [ ] Publish a sample workflow.
- [ ] Add release notes and known limitations.

## 11. Contribution

Contributions, issue reports, and technical suggestions are welcome.

Before submitting a change:
1. Keep changes focused and explain the user problem they solve.
2. Avoid introducing dependencies without a clear technical reason.
3. Handle file-system errors explicitly.
4. Add or update tests for behavior changes.
5. Update this README when actual capabilities, prerequisites, or workflows change.
6. Do not document a feature as supported until it has been verified.

For substantial changes, open an issue first to discuss the intended behavior.

## 12. Security and project data

This utility is intended to work with project files that may contain commercially sensitive information.

- Use only source and destination locations you are authorized to access.
- Do not include client data, credentials, or confidential file paths in public issue reports.
- Review any collection destination before sharing its contents.
- Treat downloaded binaries and third-party dependencies according to your organization's security procedures.

A security issue should be reported privately to the repository maintainer rather than disclosed publicly before it can be assessed.

## 13. License

**License: not yet specified.**

Until a license file is added, do not assume that the repository grants permission to redistribute, modify, or commercially use its contents. A clear license should be selected and committed before inviting external redistribution or contributions.

---

## Maintainer

**Repository:** [amdghedy/BimTools.NWCcollector](https://github.com/amdghedy/BimTools.NWCcollector)

**Maintainer:** [@amdghedy](https://github.com/amdghedy)

---

*This documentation is deliberately transparent about the current repository status. As implementation details become available, replace planned and unverified statements with tested behavior, exact requirements, screenshots, and release instructions.*
