using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
using System.Threading;
using CodeSearch;
using Topshelf;

namespace CodeSearch.WebHost.Console
{
    public class CsWebHost : ServiceControl, IDisposable
    {
        public const string LoggerName = @"Webhost";

        private const string _pipeName = "CsWebHostPipetemp";

        private  CancellationTokenSource _cancellationToken = new CancellationTokenSource();
        private readonly Lazy<IProcessManager> _processManager = new Lazy<IProcessManager>(() =>
        {
            var p = new ProcessManager(_pipeName);
            p.Start();
            return p;
        });

        private bool _disposed;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private Thread _starterThread;
        private HostControl _hostControl;

        public bool Start(HostControl hostControl)
        {
            _hostControl = hostControl;
            "Webhost start".Info(LoggerName);
            // TODO: make into process instead
            _starterThread = new Thread(ThreadStarter);
            _starterThread.IsBackground = true;
            _starterThread.Name = "ThreadStarter";
            _starterThread.Priority = ThreadPriority.Normal;
            _starterThread.Start();
            return true;
        }

        private Thread _webHostThread = null;

        private void ThreadStarter()
        {
            try
            {
#if DEBUG
                _starterThread.Join(TimeSpan.FromSeconds(10.0));
#endif
                $"{nameof(ThreadStarter)} start".Info(LoggerName);
                var p = Environment.CurrentDirectory + Globals.WebConfigPath;
                var files =
                    from f in Directory.GetFiles(p, "web?.xml", SearchOption.TopDirectoryOnly)
                    select f;
                $"{nameof(ThreadStarter)}: found the following files {string.Join(",", files.ToArray())}".Info(LoggerName);
                CancellationTokenSource ts = null;
                do
                {
                    var before = (from f in files select Tuple.Create(f, File.GetLastWriteTimeUtc(f))).ToArray();
                    _starterThread.Join(TimeSpan.FromSeconds(5.0));
                    var changedFiles = from f in before
                                       where Regex.IsMatch(f.Item1, @"web[0-9]{1}\.xml") 
                                       && File.GetLastWriteTimeUtc(f.Item1) != f.Item2
                                       select f.Item1;
                    if (!changedFiles.Any())
                    {
                        continue;
                    }
                    if (changedFiles.Count() > 1)
                    {
                        $"More than one changed file in {nameof(ThreadStarter)}: {string.Join(",", changedFiles.ToArray())}".Error(LoggerName);
                        _hostControl.Stop();
                        break;
                    }
                    if (_webHostThread != null)
                    {
                        ts?.Cancel();
                        if (!_webHostThread.Join(TimeSpan.FromSeconds(10.0)))
                        {
                            $"Timeout on webhost thread join after cancellation".Error(LoggerName);
                            _hostControl.Stop();
                            break;
                        }
                        $"Cancelled webhost thread".Info(LoggerName);
                    }
                    try
                    {
                        var target = Environment.CurrentDirectory + Path.Combine(Globals.WebConfigPath, Globals.WebConfigBaseName);
                        if (File.Exists(target))
                        {
                            // TODO: handle when delete doesn't work
                            File.Delete(target);
                            $"Deleted target {target}".Info(LoggerName);
                        }
                        File.Copy(changedFiles.Single(), target, true);
                        $"Copied {changedFiles.Single()} to {target}".Info(LoggerName);
                    }
                    catch (Exception e)
                    {
                        $"Error doing move of {changedFiles.SingleOrDefault() ?? "<undefined>"} to {Path.Combine(Globals.WebConfigPath, Globals.WebConfigBaseName)}: {e.Message}".Error(LoggerName);
                        _hostControl.Stop();
                        break;
                    }
                    ts = new CancellationTokenSource();
                    _webHostThread = new Thread(() => StartWebHost(ts.Token))
                    {
                        IsBackground = true,
                        Name = "Web host thread",
                        Priority = ThreadPriority.Normal
                    };
                    _webHostThread.Start();
                    $"Restarted webhost thread".Info(LoggerName);
                } while (!_cancellationToken.IsCancellationRequested);
                ts?.Cancel();
                $"{nameof(ThreadStarter)} end".Info(LoggerName);
            }
            catch (Exception e)
            {
                e.Error(LoggerName);
            }
        }

        public bool Stop(HostControl hostControl)
        {
            "Webhost stop".Info(LoggerName);
            _cancellationToken.Cancel();
            _starterThread.Join(TimeSpan.FromSeconds(10.0));
            _starterThread = null;
            _webHostThread = null;
            Environment.Exit(0);
            return true;
        }

        private void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }
            if (disposing)
            {
            }
            _disposed = true;
        }

        private  void StartWebHost(object cancellationToken)
        {
            "Starting web host".Info("Eventlog");
            var sw = new Stopwatch();
            sw.Start();
            try
            {
                if (!_processManager.Value.ConnectedToSentryEvent.WaitOne(TimeSpan.FromSeconds(10)))
                {
                    throw new InvalidOperationException("Cannot start webhost because of missing sentry connection");
                }
                var processInfo = new ProcessStartInfo
                {
                    CreateNoWindow = true,
                    ErrorDialog = false,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    WorkingDirectory = Environment.CurrentDirectory,
                    FileName = Globals.JavaExe,
                    Arguments = "-jar " + Globals.JettyRunnerJar + $@" --port {Globals.Port}" + " " + Globals.WebDir
                };
                _processManager.Value.StartProcess(
                    processInfo, 
                    cancellationToken, 
                    "Main", 
                    async () =>
                    {
                        try
                        {
                            Thread.Sleep(TimeSpan.FromSeconds(10.0));
                            var s = $"http://localhost:{Globals.Port}/index.jsp";
                            $"Getting initial webpage from {s}".Info(LoggerName);
                         //   Process.Start(s);
                            var hc = new HttpClient();
                            await hc.GetStringAsync(s);
                        }
                        catch (Exception e)
                        {
                            $"Could not complete post-process action: {e.Message}".Error();
                        }
                    });
            }
            catch (Exception e)
            {
                e.Error();
            }
            finally
            {
                sw.Stop();
                $"Web host start sequence conclude. Time elapsed: {sw.ElapsedMilliseconds}ms".Info("Eventlog");
            }
        }
    }

    public class Program
    {
        private static Config _configuration;

        [HandleProcessCorruptedStateExceptions]
        private static void Main(string[] args)
        {
            try
            {
          //      _configuration = new Config();
           //     _configuration.Init();
                "Webhost Main".Info();
                var h = HostFactory.Run(configurator =>
                {
                    configurator.Service<CsWebHost>();
                    configurator.RunAsNetworkService();
                    configurator.SetDescription("Serves out web pages with code search content");
                    configurator.SetDisplayName(Globals.WebhostDisplayName + "temp");
                    configurator.SetServiceName(Globals.WebhostServiceName + "temp");
                    configurator.UseNLog();
                    "Running webhost service".Info();
                });
            }
            catch (Exception e)
            {
                e.Error();
                throw;
            }
        }
    }
}