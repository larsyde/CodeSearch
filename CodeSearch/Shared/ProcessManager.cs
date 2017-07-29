using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CSUpdater.Sentry;
using NamedPipeWrapper;

namespace CSUpdater.Console
{
    public class ProcessManager : IDisposable, IProcessManager
    {
        private bool _disposed = false;
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }
            if (disposing)
            {
                foreach (var action in _terminationActions)
                {
                    action?.Invoke();
                }
            }
            _disposed = true;
            return;
        }

        ~ProcessManager()
        {
            Dispose();
        }

        public ManualResetEvent ConnectedToSentryEvent { get; set; }
        private NamedPipeServer<ProcessDescriptor> _pipeServer; 
        private Process _sentryProcess;
        private void Init(string pipeName)
        {
            ConnectedToSentryEvent = new ManualResetEvent(false);
            _pipeServer = new NamedPipeServer<ProcessDescriptor>(pipeName);
            _pipeServer.ClientConnected += connection =>
            {
                $"Client connected to pipe: {connection.Name}".Info();
                ConnectedToSentryEvent.Set();
            };
            _pipeServer.ClientMessage += (connection,
                message) =>
            {
                $"Message from sentry: {message.ProcessId}".Info();
            };
            _pipeServer.Start();
            var pInfo = new ProcessStartInfo();
            pInfo.UseShellExecute = false;
            pInfo.ErrorDialog = false;
            pInfo.RedirectStandardError = true;
            pInfo.RedirectStandardOutput = true;
            pInfo.CreateNoWindow = true;
            pInfo.Arguments = $"{Process.GetCurrentProcess().Id},{pipeName}";
            pInfo.FileName = "CSUpdater.Sentry.exe";
            _sentryProcess = new Process();
            _sentryProcess.StartInfo = pInfo;
            "Starting sentry".Info();
            _sentryProcess.Start();
            Task.Run(() =>
            {
                var b = ConnectedToSentryEvent.WaitOne(TimeSpan.FromMilliseconds(5000));
                if (!b)
                {
                    "Could not connect sentry pipe".Info();
                    Environment.Exit(0);
                }
                "Connected to sentry".Info("Eventlog").Info();
            });
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private readonly IList<Action> _terminationActions = new List<Action>(); 
        private readonly IList<Process> _processes = new List<Process>();
        private readonly object _processListLockingObject = new object();
        private readonly string _pipeName = string.Empty;

        public ProcessManager(string pipeName)
        {
            _pipeName = pipeName;
        }

        public void Start()
        {
            Init(_pipeName);
        }

        public void StartProcess(ProcessStartInfo processInfo, object cancellationToken)
        {
            StartProcess(processInfo, cancellationToken, "Main", null, TimeSpan.MaxValue);
        }

        public void StartProcess(ProcessStartInfo processInfo, object cancellationToken, string logger)
        {
            StartProcess(processInfo, cancellationToken, logger, null, TimeSpan.MaxValue);
        }

        public void StartProcess(ProcessStartInfo processInfo, object cancellationToken, Action postProcess)
        {
            StartProcess(processInfo, cancellationToken, "Main", postProcess, TimeSpan.MaxValue);
        }

        public void StartProcess(ProcessStartInfo processInfo, object cancellationToken, TimeSpan timeOut)
        {
            StartProcess(processInfo, cancellationToken, "Main", null, timeOut);
        }

        public void StartProcess(ProcessStartInfo processInfo, object cancellationToken, string logger, Action postProcess)
        {
            StartProcess(processInfo, cancellationToken, logger, postProcess, TimeSpan.MaxValue);
        }

        public void StartProcess(
            ProcessStartInfo processInfo, 
            object cancellationToken, 
            string logger, 
            Action postProcess,
            TimeSpan timeOut)
        {
            Process process = null;
            try
            {
                if (processInfo == null)
                {
                       throw new ArgumentNullException($"{nameof(processInfo)} was null");
                }
                var processName = "<unidentified>";
                var processId = "<N/A>";
                var sw = new Stopwatch();
                var token = (CancellationToken)cancellationToken;
                process = new Process { StartInfo = processInfo };
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.EnableRaisingEvents = true;
                process.Exited += (sender,
                    args) =>
                {
                    sw.Stop();
                    $"'{processName}' with id '{processId}' has exited. Time elapsed (ms) = {sw.ElapsedMilliseconds.ToString("F")}".Info(logger);
                };
                process.Disposed += (sender,
                    args) =>
                {
                    $"'{processName}' with id '{processId}' has been disposed (event)".Info(logger);
                };
                process.ErrorDataReceived += (sender,
                    args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        $"Error data received on '{processName}' with id '{processId}' : {args.Data}".Info(logger);
                    }
                };
                process.OutputDataReceived += (sender,
                    args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        $"Output data received on '{processName}' with id '{processId}': {args.Data}".Info(logger);
                    }
                };
                lock (_processListLockingObject)
                {
                    _processes.Add(process);
                }
                sw.Start();
                if (!process.Start() && process.HasExited)
                {
                    $"Process has exited immediately after start".Info();
                }
                else
                {

                    processName = process.ProcessName;
                    processId = process.Id.ToString();
                    $"Starting process '{processName}' with id '{processId}' using file '{process.StartInfo.FileName}' and arguments '{process.StartInfo.Arguments ?? "<none>"} in working dir {process.StartInfo.WorkingDirectory}".Info();

                }
                process.BeginErrorReadLine();
                process.BeginOutputReadLine();
                _pipeServer?.PushMessage(new ProcessDescriptor() { ProcessId = process.Id });
                postProcess?.Invoke();
                var startTime = DateTime.UtcNow;
                while (!token.IsCancellationRequested)
                {
                    if (process.HasExited)
                    {
                        break;
                    }
                    if ((DateTime.UtcNow - startTime) > timeOut)
                    {
                        $"Killing process {process.Id} because its runtime exceeded timeout {timeOut.TotalSeconds}s".Info();
                        throw new TimeoutException("Process timed out");
                    }
                    token.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(1000));
                }
            }
            catch (Exception e)
            {
                e.Error(logger);
            }
            finally
            {
                if (process != null && !process.HasExited)
                {
                    process.Kill();
                    process.Dispose();
                    if (!process.WaitForExit(60000))
                    {
                        throw new InvalidOperationException($"Could not terminate '{process.ProcessName}' process");
                    }
                }
            }
        }
    }
}