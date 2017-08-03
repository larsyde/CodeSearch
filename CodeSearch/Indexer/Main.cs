using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Topshelf;

namespace CodeSearch
{
    internal class CodeSearch : ServiceControl, IDisposable
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetThreadErrorMode(UInt32 dwNewMode,
            out UInt32 lpOldMode);

        public enum ErrorModes : uint
        {
            SYSTEM_DEFAULT = 0x0,
            SEM_FAILCRITICALERRORS = 0x0001,
            SEM_NOALIGNMENTFAULTEXCEPT = 0x0004,
            SEM_NOGPFAULTERRORBOX = 0x0002,
            SEM_NOOPENFILEERRORBOX = 0x8000
        }

        private bool ConsoleEventCallback(int eventType)
        {
            if (eventType == 2)
            {
                Dispose();
            }
            return false;
        }

        private ConsoleEventDelegate _handler;

        private delegate bool ConsoleEventDelegate(int eventType);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleCtrlHandler(ConsoleEventDelegate callback,
            bool add);

        private static Config _configuration;

        public static Config Configuration
        {
            get
            {
                return _configuration;
            }
        }

        [HandleProcessCorruptedStateExceptions]
        private static void Main(string[] args)
        {
            try
            {
                /*
                    1) get all team project collections in parallel
                    1.1) for each, get all projects in parallel
                    1.1.1) for each, get all extensions in parallel

                */
                _configuration = new Config();
                _configuration.Init();
                HostFactory.Run(configurator =>
                {
                    configurator.Service<CodeSearch>();
                    configurator.RunAsNetworkService();
                    configurator.SetDescription("Runs periodically to update the code search repository");
                    configurator.SetDisplayName("CodeSearch updater service");
                    configurator.SetServiceName("CodeSearch.Update.Service");
                    configurator.UseNLog();
                });
            }
            catch (Exception e)
            {
                e.Error();
                throw;
            }
        }

        public static HostControl TheHostControl { get; private set; } = null;

        private void ConfigureExceptionHandling(HostControl hostControl)
        {
            uint oldMode;
            SetThreadErrorMode((uint)(ErrorModes.SEM_NOGPFAULTERRORBOX | ErrorModes.SEM_FAILCRITICALERRORS | ErrorModes.SEM_NOOPENFILEERRORBOX), out oldMode);
            AppDomain.CurrentDomain.UnhandledException += CurrentDomainOnUnhandledException;
            TaskScheduler.UnobservedTaskException += TaskSchedulerOnUnobservedTaskException;
            _terminationActions.Add(() => TaskScheduler.UnobservedTaskException -= TaskSchedulerOnUnobservedTaskException);
            _terminationActions.Add(() => AppDomain.CurrentDomain.UnhandledException -= CurrentDomainOnUnhandledException);
            AppDomain.CurrentDomain.ProcessExit += (sender,
                args) =>
            { Dispose(); };
            _handler = ConsoleEventCallback;
            SetConsoleCtrlHandler(_handler, true);
            Process.GetCurrentProcess().EnableRaisingEvents = true;
        }

        private void TaskSchedulerOnUnobservedTaskException(object sender,
            UnobservedTaskExceptionEventArgs unobservedTaskExceptionEventArgs)
        {
            var sb = new StringBuilder();
            sb.Append($"Unhandled task exception (sender:{sender})\n");
            foreach (var e in unobservedTaskExceptionEventArgs.Exception.Flatten().InnerExceptions)
            {
                sb.Append($"\t{e.Message}\n");
            }
            $"Unhandled exception from task: {sb}".Warning();
            unobservedTaskExceptionEventArgs.SetObserved();
        }

        private void CurrentDomainOnUnhandledException(object sender,
            UnhandledExceptionEventArgs unhandledExceptionEventArgs)
        {
            $"Unhandled exception: {(unhandledExceptionEventArgs.ExceptionObject as Exception)?.Message} (sender: {sender}}}".Warning();
            TheHostControl?.Stop();
        }

        private readonly IList<Action> _terminationActions = new List<Action>();

        private void Terminate()
        {
            try
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource?.Dispose();
                foreach (var terminationAction in _terminationActions)
                {
                    terminationAction?.Invoke();
                }
                _updater?.Dispose();
                _updater = null;
            }
            catch (OperationCanceledException oe)
            {
                oe.Message.Error();
            }
        }

        public bool Stop(HostControl hostControl)
        {
            "Stopping service".Info();
            "CodeSearch indexer service stop initiated".Info("Eventlog");
            var sw = new Stopwatch();
            sw.Start();
            try
            {
                Terminate();
            }
            catch (Exception e)
            {
                e.Error();
                throw;
            }
            finally
            {
                sw.Stop();
                $"CodeSearch indexer service stop concluded. Stop sequence took {sw.ElapsedMilliseconds}ms".Info("Eventlog");
            }
            return true;
        }

        private Indexer _updater;
        private Thread _updaterThread;

        private CancellationTokenSource _cancellationTokenSource;

        bool ServiceControl.Start(HostControl hostControl)
        {
            "Starting CodeSearch Indexer service".Info("Eventlog");
            "Starting service".Info();
            var sw = new Stopwatch();
            sw.Start();
            TheHostControl = hostControl;
            try
            {
                ConfigureExceptionHandling(hostControl);
                _cancellationTokenSource = new CancellationTokenSource();
                _updater = new Indexer();
                _updaterThread = new Thread(() => _updater.Run(_cancellationTokenSource.Token))
                {
                    Name = "UpdaterThread",
                    IsBackground = true,
                    Priority = ThreadPriority.Normal
                };
                _updaterThread.Start();
            }
            catch (Exception e)
            {
                $"Exception {e.Message}".Info();
                TheHostControl?.Stop();
            }
            finally
            {
                sw.Stop();
                $"Exiting service start. Time elapsed: {sw.ElapsedMilliseconds}ms".Info();
                $"CodeSearch Indexer service start concluded, took {sw.ElapsedMilliseconds}ms".Info("Eventlog");
            }
            return true;
        }

        private bool _disposed;

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }
            if (disposing)
            {
                Terminate();
            }
            _disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}