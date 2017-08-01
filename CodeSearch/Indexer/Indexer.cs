using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.Framework.Common;
using Microsoft.TeamFoundation.VersionControl.Client;
using Microsoft.TeamFoundation.VersionControl.Common;
using StackExchange.Profiling;

namespace CodeSearch
{
    // TODO: compose with an extendederrorhandler, dont inherit from it
    public class Indexer : ExtendedErrorHandler
    {


        public static readonly string[] Extensions =
        {
            "txt", "cs", "config", "xml", "sql", "xaml", "manifest", "resx", "sln", "*proj", "wxs",
            "java", "mak", "fakes", "*html", "*js", "*css", "C", "H", "rc", "log",
            "CPP", "HPP", "CC", "C++", "HH", "CXX", "HXX", "TXX", "PDF", "", "m", "swift"
        };


        private ProcessManager ProcessManager { get; } = new ProcessManager("CSUpdaterPipeTemp");


        public static Config Configuration
        {
            get; private set;
        }

        public Uri TfsUri
        {
            get; private set;
        }

        public TfsConfigurationServer ConfigurationServer
        {
            get; private set;
        }

        public CancellationToken CancellationToken { get; private set; }

        public void Run(CancellationToken ct)
        {
            CancellationToken = ct;
            try
            {
                Init();
                ProcessManager.Start();
                for (uint i = 1; ; i++)
                {
                    if (CancellationToken.IsCancellationRequested)
                    {
                        return;
                    }
                    var j = i % 2;
                    Globals.ConfigurationXml = Globals.ConfigurationXmlConst + j + "." + Globals.ConfigurationConstXmlExtension;
                    $"{nameof(Globals.ConfigurationXml)} = {Globals.ConfigurationXml}".Info();
                    Globals.DataRoot = Globals.DataRootConst + j;
                    $"{nameof(Globals.DataRoot)} = {Globals.DataRoot}".Info();
                    Globals.TfsRoot = Globals.TfsRootConst + j;
                    $"{nameof(Globals.TfsRoot)} = {Globals.TfsRoot}".Info();
                    $"{nameof(DeleteDirs)} with '{Globals.DataRoot}'".Info();
                    DeleteDirs(Globals.DataRoot);
                    $"{nameof(DoFullGet)}".Info();
                    DoFullGet();
                    $"{nameof(DoFullIndex)}".Info();
                    DoFullIndex();
                    $"{nameof(TouchWebConfig)} with '{j}'".Info();
                    TouchWebConfig(j);
                }
            }
            catch (Exception e)
            {
                e.Error();
            }
        }

        private void TouchWebConfig(uint alternation)
        {
            var parts = Globals.WebConfigBaseName.Split('.');
            var name = parts[0] + alternation + "." + parts[1];
            var fullName = Environment.CurrentDirectory + Path.Combine(Globals.WebConfigPath, name);
            if (!File.Exists(fullName))
            {
                $"{fullName} does not exist".Error();
                throw new InvalidOperationException($"{fullName} does not exist");
            }
            var time = DateTime.UtcNow;
            $"{fullName} setting timestamp to {time.ToLongTimeString()}".Info();
            File.SetLastWriteTime(fullName, time);
        }

        /// <summary>
        ///     http://mattpilz.com/fastest-way-to-delete-large-folders-windows/
        /// </summary>
        /// <param name="dataRoot"></param>
        private void DeleteDirs(string dataRoot)
        {
            try
            {
                if (Directory.Exists(dataRoot))
                {
                    var pInfoDelete = new ProcessStartInfo();
                    var cts = new CancellationTokenSource();
                    pInfoDelete.FileName = "cmd";
                    pInfoDelete.CreateNoWindow = true;
                    pInfoDelete.WindowStyle = ProcessWindowStyle.Hidden;
                    pInfoDelete.Arguments = $"/c del /f /q /s {dataRoot}"; // > nul if you want silent
                    $"Deleting all files: arguments = {pInfoDelete.Arguments}".Trace();
                    ProcessManager.StartProcess(pInfoDelete, cts.Token, TimeSpan.FromSeconds(60.0));
                    var pInfoRemove = new ProcessStartInfo();
                    pInfoRemove.FileName = "cmd";
                    pInfoRemove.CreateNoWindow = true;
                    pInfoRemove.WindowStyle = ProcessWindowStyle.Hidden;
                    pInfoRemove.Arguments = $"/c rmdir /q /s {dataRoot}";
                    $"Removing folders: arguments = {pInfoRemove.Arguments}".Trace();
                    ProcessManager.StartProcess(pInfoRemove, cts.Token, TimeSpan.FromSeconds(60.0));
                }
            }
            catch (Exception e)
            {
                e.Error();
            }
        }


        public void Init()
        {
            try
            {
                Configuration = new Config();
                Configuration.Init();
                TfsUri = new Uri(Configuration.ServerUrl); //new Uri(@"http://localhost:8088/tfs/");
                var credentials = new NetworkCredential(Configuration.TfsUserName, Configuration.TfsPassword); //new NetworkCredential("milestone\\lay", "Nors1975!");
                ConfigurationServer = TfsConfigurationServerFactory.GetConfigurationServer(TfsUri);
                ConfigurationServer.Credentials = credentials;
                ConfigurationServer.Authenticate();
                //               _topLevelStatsTimer = new Timer(PrintTopLevelStats, null, 0, 30*1000);
            }
            catch (Exception e)
            {
                e.Error();
                throw;
            }
        }

        public IEnumerable<TfsTeamProjectCollection> GetTeamProjectCollections()
        {
            var topNode = ConfigurationServer.CatalogNode;
            var tpcNodes = topNode.QueryChildren(new[] { CatalogResourceTypes.ProjectCollection }, false, CatalogQueryOptions.None);
            foreach (var node in tpcNodes)
            {
                var instanceIdGuid = new Guid(node.Resource.Properties["InstanceId"]);
                var projCollection = ConfigurationServer.GetTeamProjectCollection(instanceIdGuid);
                yield return projCollection;
            }
        }

        private void DoFullGet()
        {
            try
            {
                Configuration = new Config();
                Configuration.Init();
                $"Get info for all TPCs and all projects".Info();
                var sw = new Stopwatch();
                sw.Start();
                var lpc = GetTeamProjectCollections().AsParallel();
                Func<TfsTeamProjectCollection, string> workspaceName = projColl =>
                {
                    var str = $"{Guid.NewGuid()}_{projColl.GetProjectCollectionName()}";
                    $"workspaceName: {str}".Trace();
                    return str;
                };
                Func<VersionControlServer, string, Workspace> createWorkSpace = (vcs,
                    wsName) =>
                {
                    try
                    {
                        if (vcs.DeleteWorkspace(wsName, vcs.AuthorizedUser))
                        {
                            // TODO log
                        }
                    }
                    catch (Exception e)
                    {
                        $"createWorkspace: {e.Message}".Error();
                    }
                    try
                    {

                        var options = new CreateWorkspaceParameters(wsName)
                        {
                            WorkspaceOptions = WorkspaceOptions.None,
                            OwnerName = vcs.AuthorizedUser,
                            Location = WorkspaceLocation.Server
                        };
                        var workSpace = vcs.CreateWorkspace(options);
                        $"Workspace created: {workSpace.Name}".Trace();
                        return workSpace;
                    }
                    catch (Exception e)
                    {
                        $"createWorkspace: {e.Message}".Error();
                        throw;
                    }
                };

                Action<TfsTeamProjectCollection> dropWorkspaces = collection =>
                {
                    try
                    {
                        var vcs = collection.GetService<VersionControlServer>();
                        var wss = vcs.QueryWorkspaces(null, vcs.AuthorizedUser, Environment.MachineName);
                        foreach (var workspace in wss)
                        {
                            if (!workspace.Delete())
                            {
                                $"Failed to delete {workspace.Name}".Error();
                            }
                            else
                            {
                                $"Deleted {workspace.Name}".Info();
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        e.Error();
                        throw;
                    }
                };

                Action<TfsTeamProjectCollection> dropMappings = (collection) =>
                {
                    try
                    {

                        var vcs = collection.GetService<VersionControlServer>();
                        var wss = vcs.QueryWorkspaces(null, vcs.AuthorizedUser, null);
                        var localPath = collection.GetProjectCollectionLocalPath(Globals.TfsRoot);
                        var withPath = wss.Select(workspace => Tuple.Create(workspace, workspace.TryGetWorkingFolderForServerItem(collection.Uri.AbsoluteUri)));
                        foreach (var tuple in withPath)
                        {
                            if (tuple.Item1 != null && tuple.Item2 != null)
                            {
                                $"Dropping {tuple.Item2.LocalItem} for workspace {tuple.Item1.Name}".Info();
                                tuple.Item1.DeleteMapping(tuple.Item2);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        e.Error();
                    }
                };

                Func<Workspace, TfsTeamProjectCollection, Workspace> mapWorkspace = (ws,
                    pc) =>
                {
                    try
                    {
                        var serverPath = "$/";
                        var localPath = pc.GetProjectCollectionLocalPath(Globals.TfsRoot);
                        $"mapWorkspace: {nameof(serverPath)} = {serverPath}".Trace();
                        $"mapWorkspace: {nameof(localPath)} = {localPath}".Trace();
                        var vcs = pc.GetService<VersionControlServer>();
                        var lws = Workstation.Current.RemoveCachedWorkspaceInfo(vcs);
                        var wss = vcs.QueryWorkspaces(null, vcs.AuthorizedUser, null);
                        foreach (var workspace in wss)
                        {
                            $"Checking workspace {workspace.Name} for mapping of local path {localPath}".Info();
                            if (workspace.IsLocalPathMapped(localPath))
                            {
                                var wf = workspace.TryGetWorkingFolderForLocalItem(localPath);
                                workspace.DeleteMapping(wf);
                                $"Deleted {localPath} mapping from workspace {workspace.Name}".Info();
                            }
                        }

                        ws.Map(serverPath, localPath);
                        return ws;
                    }
                    catch (Exception e)
                    {
                        e.Error();
                    }
                    return null;
                };

                if (CancellationToken.IsCancellationRequested)
                {
                    return;
                }
                // get the workspaces for this user on this machine and their working folders, if any, for the project collection they belong to 
                var pcsAndAllTheirWorkspaces =
                    from pc in lpc.AsParallel()
                    let vcs = pc.GetService<VersionControlServer>()
                    let localPath = pc.GetProjectCollectionLocalPath(Globals.TfsRoot)
                    select new
                    {
                        LocalPath = localPath,
                        VersionControlServer = vcs,
                        ProjectCollection = pc,
                        WorkspaceAndWorkingFolderList = vcs
                            .QueryWorkspaces(null, vcs.AuthorizedUser, null)
                            .Where(workspace => workspace.Computer.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase))
                            .Select(workspace => Tuple.Create(workspace, workspace.TryGetWorkingFolderForLocalItem(localPath)))
                    };
                $"{nameof(pcsAndAllTheirWorkspaces)}: {string.Join(",", pcsAndAllTheirWorkspaces.Select(arg => arg.LocalPath))}".Trace();
                $"{nameof(pcsAndAllTheirWorkspaces)}: {string.Join(",", pcsAndAllTheirWorkspaces.Select(arg => arg.WorkspaceAndWorkingFolderList.Count()))}".Trace();
                // select the relevant information from the above gross and create workspace and mapping where necessary
                var pcsAndMappedWorkspaces =
                    from a in pcsAndAllTheirWorkspaces
                    let workspaceAndFolder = a.WorkspaceAndWorkingFolderList.SingleOrDefault(tuple => tuple.Item2 != null)
                    let name = workspaceAndFolder?.Item1.Name ?? workspaceName(a.ProjectCollection)
                    let workspace = workspaceAndFolder?.Item1 ?? mapWorkspace(createWorkSpace(a.VersionControlServer, name), a.ProjectCollection)
                    let workingFolder = workspaceAndFolder?.Item2 ?? workspace.TryGetWorkingFolderForLocalItem(a.LocalPath)
                    select new
                    {
                        a.ProjectCollection,
                        a.VersionControlServer,
                        Workspace = workspace,
                        WorkingFolder = workingFolder
                    };
                $"{nameof(pcsAndMappedWorkspaces)}: {string.Join(",", pcsAndMappedWorkspaces.Select(arg => arg.WorkingFolder.LocalItem))}".Trace();
                var allProjects =
                    from p in pcsAndMappedWorkspaces
                    from pp in p.ProjectCollection.GetTeamProjects()
                    let sp = pp.GetProjectServerPath(p.ProjectCollection)
                    let wf = p.Workspace.TryGetWorkingFolderForServerItem(sp)
                    where wf.LocalItem.ToLowerInvariant().Contains(Configuration.SourceRoot.ToLowerInvariant())
                    select new
                    {
                        ProjectCollection = p,
                        Project = pp,
                        LocalPath = pp.GetProjectLocalPath(p.ProjectCollection),
                        ServerPath = sp,
                        WorkspaceInfo = p.Workspace,
                        WorkingFolder = wf
                    };
                $"{nameof(allProjects)}: {string.Join(",", allProjects.Select(arg => arg.LocalPath))}".Trace();
                var filterStats = new Dictionary<StatTypes, int>
                {
                    [StatTypes.Added] = 0,
                    [StatTypes.Deleted] = 0,
                    [StatTypes.Ignored] = 0,
                    [StatTypes.Skipped] = 0
                };

                string[] exceptions = { @"vnext", @"-oem" };
                string[] exclusions = { @"/development/", @"/release/", @"/team/" };
                var maxAge = TimeSpan.FromHours(6.0);

                var fc = new GetFilterCallback((Workspace workspace,
                    ILocalUpdateOperation[] operations,
                    object userData) =>
                {
                    if (CancellationToken.IsCancellationRequested)
                    {
                        foreach (var o in operations)
                        {
                            o.Ignore = true;
                        }
                        return;
                    }
                    var spec = userData as ItemSpec;
                    $"TFS filter callback: workspace name = {workspace.Name} | spec.item = {spec.Item} | number of operations {operations.Length}".Trace();
                    foreach (var operation in operations)
                    {
                        if (
                            operation.TargetServerItem != null
                            && exclusions.Any(s1 => operation.TargetServerItem.ToLowerInvariant().Contains(s1))
                            && !exceptions.Any(s2 => operation.TargetServerItem.ToLowerInvariant().Contains(s2)))
                        {
                            operation.Ignore = true;
                            filterStats[StatTypes.Skipped]++;
                        }
                        if (
                            operation.SourceLocalItem != null
                            && File.Exists(operation.SourceLocalItem)
                            && (DateTime.UtcNow - File.GetLastWriteTimeUtc(operation.SourceLocalItem) < maxAge)
                            )
                        {
                            filterStats[StatTypes.Skipped]++;
                            operation.Ignore = true;
                        }
                        if (operation.TargetLocalItem == null)
                        {
                            filterStats[StatTypes.Deleted]++;
                        }
                        filterStats[StatTypes.Added]++;
                    }
                });

                $"Getting all files from {allProjects.Count()} projects".Trace();
                // get all the files
                allProjects.AsParallel().WithDegreeOfParallelism(1).ForAll(obj =>
                {
                    var subs = obj.ProjectCollection.VersionControlServer.GetItems($"{obj.WorkingFolder.ServerItem}/*");
                    for (int i = 0; i < subs.Items.Length; i++)
                    {
                        foreach (var ext in Extensions)
                        {
                            $"get files: {nameof(ext)} = {ext}".Trace();
                            var pattern = $"{subs.Items[i].ServerItem}/*.{ext}";
                            var itemSpec = new ItemSpec(pattern, RecursionType.Full);
                            var getRequest = new GetRequest(itemSpec, VersionSpec.Latest);
                            $"get files: {nameof(pattern)} = {pattern}".Trace();
                            $"get files: {nameof(itemSpec)} = {itemSpec.Item}".Trace();
                            try
                            {
                                if (CancellationToken.IsCancellationRequested)
                                {
                                    return;
                                }
                                obj.WorkspaceInfo.Get(getRequest, GetOptions.GetAll, fc, itemSpec);
                            }
                            catch (Exception e)
                            {
                                e.Error();
                            }
                        }
                    }
                });
                sw.Stop();
                $"Finished get info for all TPCs and all projects. Time elapsed: {sw.ElapsedMilliseconds.ToString("F")}".Info();
            }
            catch (Exception e)
            {
                e.Error();
                throw;
            }
        }


        private void DoFullIndex()
        {
            try
            {
                var sw = new Stopwatch();
                sw.Start();
                var cancellationTokenSource = new CancellationTokenSource();
                var indexProcess = new Action<CancellationToken>(
                    cs =>
                    {
                        var processInfo = new ProcessStartInfo
                        {
                            CreateNoWindow = true,
                            ErrorDialog = false,
                            WindowStyle = ProcessWindowStyle.Hidden
                        };
                        processInfo.EnvironmentVariables["_JAVA_OPTIONS"] = "-Xmx2048m -Xms256m";
                        processInfo.EnvironmentVariables["JAVA_TOOL_OPTIONS"] = "-Xmx2048m -Xms256m";
                        processInfo.WorkingDirectory = Environment.CurrentDirectory;
                        processInfo.FileName = Globals.JavaExe;
                        var sPath = Path.Combine(Globals.TfsRoot, Configuration.SourceRoot);
                        processInfo.Arguments = $"-jar {Globals.OpenGrokJar} -W {Globals.ConfigurationXml} -m 256 -e -i d:packages -C -P -c {Globals.CTags} -s {sPath} -d {Globals.DataRoot}";
                        ProcessManager.StartProcess(processInfo, cs, "Indexer");
                    }
                    );
                indexProcess.Invoke(cancellationTokenSource.Token); // TODO: add some monitoring and potential cancellation if danger
                sw.Stop();
                $"Finished indexing all the source. Time elapsed: {sw.ElapsedMilliseconds.ToString("F")}".Info();
            }
            catch (Exception e)
            {
                $"Error {e.Message} while indexing".Error();
                throw;
            }
        }
    }
}