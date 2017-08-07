using Indexer;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.Framework.Common;
using Microsoft.TeamFoundation.VersionControl.Client;
using Microsoft.TeamFoundation.VersionControl.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace CodeSearch
{
    public class TfsHelpers
    {
        private TfsConfigurationServer _configServer;

        public TfsHelpers(TfsConfigurationServer configServer)
        {
            _configServer = configServer;
        }

        public IEnumerable<TfsTeamProjectCollection> GetTeamProjectCollections()
        {
            var topNode = _configServer.CatalogNode;
            var tpcNodes = topNode.QueryChildren(new[] { CatalogResourceTypes.ProjectCollection }, false, CatalogQueryOptions.None);
            foreach (var node in tpcNodes)
            {
                var instanceIdGuid = new Guid(node.Resource.Properties["InstanceId"]);
                var projCollection = _configServer.GetTeamProjectCollection(instanceIdGuid);
                yield return projCollection;
            }
        }

        public string GetWorkspaceName(TfsTeamProjectCollection projColl)
        {
            var str = $"{Guid.NewGuid()}_{projColl.GetProjectCollectionName()}";
            $"workspaceName: {str}".Trace();
            return str;
        }

        public Workspace CreateWorkSpace(VersionControlServer vcs, string wsName)
        {
            try
            {
                if (vcs.DeleteWorkspace(wsName, vcs.AuthorizedUser))
                {
                    $"Successfully deleted workspace {wsName}".Info();
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
        }

        private void DropWorkspaces(TfsTeamProjectCollection collection)
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
        }

        private void DropMappings(TfsTeamProjectCollection collection)
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
        }

        public Workspace MapWorkspace(Workspace ws, TfsTeamProjectCollection pc)
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
        }

        public GetFilterCallback GetFilterCallback(
            Dictionary<StatTypes, int> filterStats,
            CancellationToken token)
        {
            var fc = new GetFilterCallback((workspace,
                operations,
                userData) =>
            {
                if (token.IsCancellationRequested)
                {
                    foreach (var o in operations)
                    {
                        o.Ignore = true;
                    }
                    return;
                }
                var spec = userData as ItemSpec;
                $"TFS filter callback: workspace name = {workspace.Name} | spec.item = {spec?.Item} | number of operations {operations.Length}".Trace();
                foreach (var operation in operations)
                {
                    if (
                        operation.TargetServerItem != null
                        && Constants.Exclusions.Any(s1 => operation.TargetServerItem.ToLowerInvariant().Contains(s1))
                        && !Constants.Exceptions.Any(s2 => operation.TargetServerItem.ToLowerInvariant().Contains(s2)))
                    {
                        operation.Ignore = true;
                        filterStats[StatTypes.Skipped]++;
                    }
                    if (
                        operation.SourceLocalItem != null
                        && File.Exists(operation.SourceLocalItem)
                        && (DateTime.UtcNow - File.GetLastWriteTimeUtc(operation.SourceLocalItem) < Constants.MaxTfsItemAge)
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
            return fc;
        }
    }
}