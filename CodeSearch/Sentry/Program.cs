using NamedPipeWrapper;
using NLog;
using NLog.Config;
using NLog.Targets;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace CodeSearch
{
    public static class Helpers
    {
        public const string LoggerName = "SentryLogger";

        public static string Log(this string s)
        {
            var logger = LogManager.GetLogger(LoggerName);
            logger.Info(s);
            return s;
        }
    }

    public class Sentry : IDisposable
    {
        private readonly AutoResetEvent _wakeUpEvent = new AutoResetEvent(false);

        private DateTime _lastInfoTime = DateTime.UtcNow;

        protected NamedPipeClient<ProcessDescriptor> _pipeClient;
        private readonly IDictionary<int, TimeSpan> _totalProcessorTimes = new Dictionary<int, TimeSpan>();
        private uint _wakeCount;
        private Timer _wakeTimer;

        public Sentry(int parentProcessId,
            string pipeName)
        {
            ParentProcessId = parentProcessId;
            PipeName = pipeName;
        }

        public int ParentProcessId { get; }
        public Process ParentProcess { get; private set; }

        public string PipeName { get; }

        public IList<ProcessDescriptor> ProcessDescriptors { get; private set; }

        public bool Go()
        {
            try
            {
                ParentProcess = Process.GetProcessById(ParentProcessId);
                if (ParentProcess == null)
                {
                    return false;
                }
                var target = new FileTarget();
                LogManager.Configuration.Reload();
                target.Layout = "${longdate} ${uppercase:${level}} ${message}";
                target.FileName = "${basedir}/logs/${shortdate}_sentry.log";
                target.CreateDirs = true;
                target.Name = Helpers.LoggerName;
                LogManager.Configuration.AddTarget(target);
                var rule = new LoggingRule("*", LogLevel.Trace, target);
                LogManager.Configuration.LoggingRules.Add(rule);
                LogManager.Configuration.Reload();

                _wakeTimer = new Timer(_ =>
                {
                    _wakeCount++;
                    _wakeUpEvent.Set();
                }
                    , null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1.0));
                $"Sentry Go for parent process {ParentProcess.Id}".Log();
                ProcessDescriptors = new List<ProcessDescriptor>();
                _pipeClient = new NamedPipeClient<ProcessDescriptor>(PipeName);
                _pipeClient.AutoReconnect = true;
                _pipeClient.ServerMessage += PipeClientOnServerMessage;
                _pipeClient.Start();
                _pipeClient.WaitForConnection(TimeSpan.FromMilliseconds(1000));
                while (!ParentProcess.HasExited)
                {
                    if (!_wakeUpEvent.WaitOne(TimeSpan.FromSeconds(5.0)))
                    {
                        $"Error: sentry waited too long for timer wakeup".Log();
                    }
                    if (_wakeCount % 60 == 0)
                    {
                        // dump process and thread information
                        GetProcessAndThreadInformation().Log();
                    }
                }
            }
            catch (Exception e)
            {
                LogManager.GetCurrentClassLogger().Error(e);
            }
            finally
            {
                Dispose();
                Environment.Exit(0);
            }
            return false;
        }

        private bool _disposed = false;

        private void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }
            if (disposing)
            {
                try
                {
                    var pps = from p in Process.GetProcesses()
                              where ProcessDescriptors.Any(descriptor => descriptor.ProcessId == p.Id)
                              select p;
                    foreach (var p in pps)
                    {
                        $"Sentry finalization of process (id = {p.Id}). Name = '{p.ProcessName}'".Log();
                        if (!p.HasExited)
                        {
                            p.Kill();
                            $"Killed process {p.Id} with name '{p.ProcessName}'".Log();
                        }
                    }
                }
                catch (Exception e)
                {
                    LogManager.GetCurrentClassLogger().Error(e);
                }
                "Stopping Sentry pipe client".Log();
                _pipeClient.Stop();
                _pipeClient.WaitForDisconnection(TimeSpan.FromMilliseconds(1000));
            }
            _disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private string GetProcessAndThreadInformation()
        {
            var elapsed = DateTime.UtcNow - _lastInfoTime;
            var totalProcTime = TimeSpan.Zero;
            var sb = new StringBuilder();
            sb.AppendLine("");
            sb.AppendLine("---=== Sentry process and thread status ===---");
            try
            {
                sb.AppendLine($"- Parent Id {ParentProcess.Id} and name {ParentProcess.ProcessName}");
                var pt = from p in Process.GetProcesses()
                         let t = p.Threads
                         where ProcessDescriptors.Any(descriptor => descriptor.ProcessId == p.Id || p.Id == ParentProcessId)
                         select new
                         {
                             p,
                             t
                         };

                foreach (var procAndThreads in pt)
                {
                    totalProcTime += procAndThreads.p.TotalProcessorTime - (_totalProcessorTimes.ContainsKey(procAndThreads.p.Id) ? _totalProcessorTimes[procAndThreads.p.Id] : TimeSpan.Zero);
                    _totalProcessorTimes[procAndThreads.p.Id] = procAndThreads.p.TotalProcessorTime;
                    sb.AppendLine($"-- Process Id '{procAndThreads.p.Id}' and name '{procAndThreads.p.ProcessName}' : mem={procAndThreads.p.PrivateMemorySize64}, cpu={procAndThreads.p.TotalProcessorTime.TotalMilliseconds}ms");
                }
            }
            catch (Exception e)
            {
                sb.AppendLine(e.Message);
                e.Message.Log();
            }
            finally
            {
                _lastInfoTime = DateTime.UtcNow;
                sb.AppendLine($"Total processor time (parent + children): {totalProcTime.TotalSeconds:F2} secs");
                sb.AppendLine($"Per core ({Environment.ProcessorCount}) average: {totalProcTime.TotalSeconds / Environment.ProcessorCount:F2} secs");
                sb.AppendLine($"Time since previous stat: {elapsed.TotalSeconds:F2} secs");
                sb.AppendLine($"Percentage used (parent + children): {totalProcTime.TotalSeconds / Environment.ProcessorCount / elapsed.TotalSeconds:p}");
                sb.AppendLine("                    ---===---");
            }
            return sb.ToString();
        }

        private void PipeClientOnServerMessage(NamedPipeConnection<ProcessDescriptor, ProcessDescriptor> connection,
            ProcessDescriptor message)
        {
            $"Received process descriptor {message.ProcessId} over Sentry pipe".Log();
            ProcessDescriptors.Add(message);
        }
    }

    internal class Program
    {
        private static Sentry sentry;

        private static void Main(string[] args)
        {
            Debug.Assert(args.Length == 1);
            var splitArgs = args[0].Split(',');
            Debug.Assert(splitArgs.Length == 2);
            int pp;
            if (!int.TryParse(splitArgs[0], out pp))
            {
                throw new InvalidDataException($"could not parse {args[0]}");
            }
            var pipeName = splitArgs[1];
            sentry = new Sentry(pp, pipeName);
            var b = sentry.Go();
            $"Exiting Sentry Main".Log();
            Environment.Exit(b ? 0 : 1);
        }
    }
}