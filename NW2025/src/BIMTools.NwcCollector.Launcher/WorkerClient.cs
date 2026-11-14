using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using BIMTools.NwcCollector.Core.Contracts;

namespace BIMTools.NwcCollector.Launcher
{
    /// <summary>Starts the worker exe, streams its stdout lines to a callback and supports cancel / kill.</summary>
    internal sealed class WorkerClient
    {
        private Process _process;
        private string _cancelFlag;

        public bool IsRunning
        {
            get { return _process != null && !_process.HasExited; }
        }

        /// <summary>Callback runs on a thread-pool thread; marshal to the UI thread yourself.</summary>
        public Task<int> RunAsync(JobSpec job, string workerExe, string navisworksDir, Action<string> onLine)
        {
            string id = Guid.NewGuid().ToString("N");
            string jobPath = Path.Combine(Path.GetTempPath(), "NwcCollector_" + id + ".job.json");
            _cancelFlag = Path.Combine(Path.GetTempPath(), "NwcCollector_" + id + ".cancel");
            job.CancelFlagPath = _cancelFlag;
            JobSerializer.Write(job, jobPath);

            var psi = new ProcessStartInfo
            {
                FileName = workerExe,
                Arguments = "--job \"" + jobPath + "\" --nwdir \"" + navisworksDir + "\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = new UTF8Encoding(false),
                StandardErrorEncoding = new UTF8Encoding(false)
            };

            var tcs = new TaskCompletionSource<int>();
            var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            proc.OutputDataReceived += (s, e) => { if (e.Data != null) onLine(e.Data); };
            proc.ErrorDataReceived += (s, e) => { if (e.Data != null) onLine("LOG\t[stderr] " + e.Data); };
            proc.Exited += (s, e) =>
            {
                try
                {
                    proc.WaitForExit(); // makes sure all redirected output was delivered
                    tcs.TrySetResult(proc.ExitCode);
                }
                catch (Exception ex) { tcs.TrySetException(ex); }
                finally
                {
                    TryDelete(jobPath);
                    TryDelete(_cancelFlag);
                }
            };

            _process = proc;
            try
            {
                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
            }
            catch (Exception ex)
            {
                TryDelete(jobPath);
                tcs.TrySetException(ex);
            }
            return tcs.Task;
        }

        /// <summary>Graceful: the worker stops after the file it is currently appending.</summary>
        public void RequestCancel()
        {
            try { if (!string.IsNullOrEmpty(_cancelFlag)) File.WriteAllText(_cancelFlag, "cancel"); }
            catch (IOException) { }
        }

        /// <summary>Hard stop: kills the worker (and with it its Navisworks instance).</summary>
        public void Kill()
        {
            try { if (IsRunning) _process.Kill(); }
            catch (InvalidOperationException) { }
        }

        private static void TryDelete(string path)
        {
            try { if (!string.IsNullOrEmpty(path) && File.Exists(path)) File.Delete(path); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
