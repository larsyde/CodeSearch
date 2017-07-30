using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace CodeSearch
{
    public abstract class ExtendedErrorHandler : IDisposable
    {

        protected ExtendedErrorHandler()
        {
            ConfigureExceptionHandling();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private bool _disposed = false;
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }
            if (disposing)
            {

            }
            Terminate();
            _disposed = true;
        }

        private void Terminate()
        {
            try
            {
                foreach (var a in _terminationActions)
                {
                    a?.Invoke();
                }
            }
            catch (Exception e)
            {
               
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool SetThreadErrorMode(UInt32 dwNewMode,
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

        private readonly IList<Action> _terminationActions = new List<Action>();


        private delegate bool ConsoleEventDelegate(int eventType);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleCtrlHandler(ConsoleEventDelegate callback,
            bool add);

        private void ConfigureExceptionHandling()
        {
            uint oldMode;
            SetThreadErrorMode((uint)(ErrorModes.SEM_NOGPFAULTERRORBOX | ErrorModes.SEM_FAILCRITICALERRORS | ErrorModes.SEM_NOOPENFILEERRORBOX), out oldMode);
            AppDomain.CurrentDomain.UnhandledException += CurrentDomainOnUnhandledException;
            TaskScheduler.UnobservedTaskException += TaskSchedulerOnUnobservedTaskException;
            _terminationActions.Add(() => TaskScheduler.UnobservedTaskException -= TaskSchedulerOnUnobservedTaskException);
            _terminationActions.Add(() => AppDomain.CurrentDomain.UnhandledException -= CurrentDomainOnUnhandledException);
            AppDomain.CurrentDomain.ProcessExit += (sender,
                args) =>
            {
                Dispose();
            };
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
        }
    }
}
