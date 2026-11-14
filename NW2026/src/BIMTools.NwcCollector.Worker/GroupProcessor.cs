using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BIMTools.NwcCollector.Core.Contracts;
using BIMTools.NwcCollector.Core.Planning;
using BIMTools.NwcCollector.Core.Protocol;
using BIMTools.NwcCollector.Core.Reporting;
using BIMTools.NwcCollector.Core.Scanning;

namespace BIMTools.NwcCollector.Worker
{
    /// <summary>
    /// Builds one output file: fresh Navisworks instance -> append every source file in the planned order (as-is,
    /// no transforms) -> save to a temporary name -> validate (size + reopen in a second fresh instance) ->
    /// apply the existing-file policy -> rename to the final name.
    /// A failure in one group never stops the next group.
    /// </summary>
    internal sealed class GroupProcessor
    {
        private readonly JobSpec _job;
        private readonly ExistingFilePolicy _policy;

        public GroupProcessor(JobSpec job)
        {
            _job = job;
            var policy = EnumUtil.Parse(job.ExistingFilePolicy, ExistingFilePolicy.Skip);
            _policy = policy == ExistingFilePolicy.Ask ? ExistingFilePolicy.Skip : policy; // nobody to ask in a worker
        }

        public bool CancelRequested
        {
            get { return !string.IsNullOrEmpty(_job.CancelFlagPath) && File.Exists(_job.CancelFlagPath); }
        }

        private string ReportDirectory
        {
            get { return string.IsNullOrEmpty(_job.ReportDirectory) ? _job.OutputDirectory : _job.ReportDirectory; }
        }

        public GroupResult Cancelled(GroupJob group)
        {
            var r = NewResult(group);
            r.Status = GroupStatus.Cancelled;
            r.Message = "Cancelled before this group started.";
            return Finish(r);
        }

        public GroupResult Process(GroupJob group)
        {
            var r = NewResult(group);
            string partial = null;

            Console.Out.WriteLine(WorkerProtocol.Format(WorkerProtocol.GroupStart, group.Name, r.ExpectedCount));

            try
            {
                if (r.ExpectedCount == 0)
                {
                    r.Status = GroupStatus.Failed;
                    r.Message = "No source files were collected for this group.";
                    return Finish(r);
                }

                string final = r.OutputPath;
                Directory.CreateDirectory(Path.GetDirectoryName(final));

                if (File.Exists(final))
                {
                    if (_job.SkipUpToDate && SourceSignature.IsUpToDate(final, group.Files))
                    {
                        r.Status = GroupStatus.SkippedUpToDate;
                        r.Message = "Output is up to date: no source file changed since it was built.";
                        return Finish(r);
                    }
                    if (_policy == ExistingFilePolicy.Skip)
                    {
                        r.Status = GroupStatus.SkippedExists;
                        r.Message = "Output file already exists and the policy is to skip.";
                        return Finish(r);
                    }
                    if (_policy == ExistingFilePolicy.AutoVersion)
                    {
                        final = NextFreeName(final);
                        r.OutputPath = final;
                        r.Message = "Existing file kept; new version written as " + Path.GetFileName(final) + ".";
                    }
                }

                string ext = Path.GetExtension(final);
                partial = Path.Combine(Path.GetDirectoryName(final), Path.GetFileNameWithoutExtension(final) + ".partial" + ext);
                TryDelete(partial);

                if (!BuildAndSave(r, group.Files, partial))
                {
                    TryDelete(partial);
                    return Finish(r);
                }

                if (!Validate(r, partial))
                {
                    r.Status = GroupStatus.ValidationFailed;
                    TryDelete(partial);
                    return Finish(r);
                }

                Publish(final, partial);

                r.Validated = true;
                bool allAppended = r.SkippedFiles.Count + r.FailedFiles.Count == 0;
                r.Status = allAppended ? GroupStatus.Success : GroupStatus.PartialSuccess;

                // Only a complete build is recorded as "up to date" for the next run.
                if (allAppended)
                {
                    try { SourceSignature.Write(final, group.Files); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
                return Finish(r);
            }
            catch (Exception ex)
            {
                r.Status = GroupStatus.Failed;
                r.Message = ex.GetType().Name + ": " + ex.Message;
                if (partial != null) TryDelete(partial);
                return Finish(r);
            }
        }

        // Returns true when a partial output was saved. On false, r.Status / r.Message are already set.
        private bool BuildAndSave(GroupResult r, IList<string> files, string partialPath)
        {
            using (var session = new AutomationSession())
            {
                bool first = true;
                for (int i = 0; i < files.Count; i++)
                {
                    if (CancelRequested)
                    {
                        r.Status = GroupStatus.Cancelled;
                        r.Message = "Cancelled by user after " + r.AppendedFiles.Count + " appended file(s).";
                        return false;
                    }

                    string file = files[i];
                    string reason;
                    long length;
                    if (!FileAvailability.Check(file, out reason, out length))
                    {
                        r.SkippedFiles.Add(new FileIssue { Path = file, Reason = reason });
                        EmitFile(r.Name, i + 1, files.Count, "skip", file, reason);
                        continue;
                    }

                    try
                    {
                        if (first && _job.OpenFirstFile) session.OpenFile(file);
                        else session.AppendFile(file);

                        first = false;
                        r.AppendedFiles.Add(file);
                        EmitFile(r.Name, i + 1, files.Count, "ok", file, "");
                    }
                    catch (Exception ex)
                    {
                        r.FailedFiles.Add(new FileIssue { Path = file, Reason = ex.GetType().Name + ": " + ex.Message });
                        EmitFile(r.Name, i + 1, files.Count, "fail", file, ex.Message);
                    }
                }

                if (r.AppendedFiles.Count == 0)
                {
                    r.Status = GroupStatus.Failed;
                    r.Message = "No source file could be appended; nothing was saved.";
                    return false;
                }

                session.SaveFile(partialPath);
            }
            return true;
        }

        private static bool Validate(GroupResult r, string partialPath)
        {
            var fi = new FileInfo(partialPath);
            if (!fi.Exists || fi.Length == 0)
            {
                r.ValidationMessage = "Saved file is missing or empty.";
                return false;
            }

            try
            {
                using (var session = new AutomationSession())
                    session.OpenFile(partialPath);
            }
            catch (Exception ex)
            {
                r.ValidationMessage = "Reopen failed: " + ex.GetType().Name + ": " + ex.Message;
                return false;
            }

            r.ValidationMessage = "Saved " + fi.Length.ToString("N0", CultureInfo.InvariantCulture) + " bytes; reopened successfully in a fresh Navisworks instance.";
            return true;
        }

        private void Publish(string final, string partial)
        {
            if (File.Exists(final))
            {
                if (_policy == ExistingFilePolicy.ArchiveOld)
                {
                    string archiveDir = Path.Combine(Path.GetDirectoryName(final), "_Archive");
                    Directory.CreateDirectory(archiveDir);
                    string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
                    string dest = Path.Combine(archiveDir, Path.GetFileNameWithoutExtension(final) + "_" + stamp + Path.GetExtension(final));
                    File.Move(final, dest);
                }
                else
                {
                    File.Delete(final); // Overwrite (the launcher asked the user to confirm)
                }
            }
            TryDelete(SourceSignature.ManifestPath(final));
            File.Move(partial, final);
        }

        private static string NextFreeName(string path)
        {
            string dir = Path.GetDirectoryName(path);
            string name = Path.GetFileNameWithoutExtension(path);
            string ext = Path.GetExtension(path);
            for (int i = 2; i < 10000; i++)
            {
                string candidate = Path.Combine(dir, name + "_v" + i + ext);
                if (!File.Exists(candidate)) return candidate;
            }
            throw new IOException("Could not find a free file name for " + path);
        }

        private GroupResult NewResult(GroupJob group)
        {
            var r = new GroupResult { Name = group.Name, StartedUtc = DateTime.UtcNow };
            r.SourceFolders.AddRange(group.SourceFolders);
            r.CollectedFiles.AddRange(group.Files);
            r.OutputPath = group.OutputPath;
            return r;
        }

        private GroupResult Finish(GroupResult r)
        {
            r.FinishedUtc = DateTime.UtcNow;
            try
            {
                Directory.CreateDirectory(ReportDirectory);
                GroupLogWriter.WriteGroup(r, GroupLogWriter.LogPathFor(ReportDirectory, r.Name));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Console.Out.WriteLine(WorkerProtocol.Format(WorkerProtocol.Log, "Could not write log for " + r.Name + ": " + ex.Message));
            }

            Console.Out.WriteLine(WorkerProtocol.Format(WorkerProtocol.GroupDone,
                r.Name, r.Status, r.ExpectedCount, r.AppendedFiles.Count, r.SkippedFiles.Count, r.FailedFiles.Count, r.OutputPath));
            return r;
        }

        private static void EmitFile(string group, int index, int total, string state, string path, string reason)
        {
            Console.Out.WriteLine(WorkerProtocol.Format(WorkerProtocol.File, group, index, total, state, path, reason));
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
