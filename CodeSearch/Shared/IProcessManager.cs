using System;
using System.Diagnostics;
using System.Threading;

namespace CSUpdater.Console
{
    public interface IProcessManager
    {
        void Start();

        void Dispose();
        void StartProcess(ProcessStartInfo processInfo, object cancellationToken, string logger, System.Action postProcess , TimeSpan timeOut);

        void StartProcess(ProcessStartInfo processInfo,
            object cancellationToken);

        void StartProcess(ProcessStartInfo processInfo,
            object cancellationToken,
            string logger);

        void StartProcess(ProcessStartInfo processInfo,
            object cancellationToken,
            Action postProcess);

        void StartProcess(ProcessStartInfo processInfo,
            object cancellationToken,
            TimeSpan timeOut);

        void StartProcess(ProcessStartInfo processInfo,
            object cancellationToken,
            string logger,
            Action postProcess);
      

        ManualResetEvent ConnectedToSentryEvent
        {
            get; set;
        }

    }
}