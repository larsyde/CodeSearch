using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.WorkItemTracking.Client;

namespace CSUpdater.Console
{
    public static class WorkspaceHelpers
    {
        public static string GetProjectCollectionName(this TfsTeamProjectCollection pc)
        {
            pc.NullCheck();
            $"{nameof(GetProjectCollectionName)}: {nameof(pc)} = {pc.Name}".Trace();
            var index = pc.Name.LastIndexOf('/') + 1;
            $"{nameof(GetProjectCollectionName)}: {nameof(index)} = {index}".Trace();
            var collName = pc.Name.Substring(index, pc.Name.Length - index);
            $"{nameof(GetProjectCollectionName)}: {nameof(collName)} = {collName}".Trace();
            return collName;
        }

        public static string GetProjectCollectionLocalPath(this TfsTeamProjectCollection pc, string localRoot)
        {
            pc.NullCheck();
            localRoot.NullCheck();
            $"{nameof(GetProjectCollectionLocalPath)}: {nameof(pc)} = {pc.Name}".Trace();
            $"{nameof(GetProjectCollectionLocalPath)}: {nameof(localRoot)} = {localRoot}".Trace();
            var collName = GetProjectCollectionName(pc);
            $"{nameof(GetProjectCollectionLocalPath)}: {nameof(collName)} = {collName}".Trace();
            var basePath = Path.Combine(Environment.CurrentDirectory, localRoot);
            $"{nameof(GetProjectCollectionLocalPath)}: {nameof(basePath)} = {basePath}".Trace();
            var collPath = Path.Combine(basePath, collName);
            $"{nameof(GetProjectCollectionLocalPath)}: {nameof(collPath)} = {collPath}".Trace();
            return collPath;
        }

        public static string GetProjectLocalPath(this Project p, TfsTeamProjectCollection pc)
        {
            $"{nameof(GetProjectLocalPath)}: {nameof(p)} = {p.Name}, {nameof(pc)} = {pc.Name}".Trace();
            var collPath = GetProjectCollectionLocalPath(pc, Globals.TfsRoot);
            $"{nameof(GetProjectLocalPath)}: {nameof(collPath)} = {collPath}".Trace();
            var projPath = Path.Combine(collPath, p.Name);
            $"{nameof(GetProjectLocalPath)}: {nameof(projPath)} = {projPath}".Trace();
            return projPath;
        }

        public static string GetProjectServerPath(this Project p, TfsTeamProjectCollection pc)
        {
            $"{nameof(GetProjectServerPath)}: {nameof(p)} = {p.Name}, {nameof(pc)} = {pc.Name}".Trace();
            return $@"$/{p.Name}";
        }

        public static IEnumerable<Project> GetTeamProjects(this TfsTeamProjectCollection projColl)
        {
            projColl.NullCheck();
            $"{nameof(GetTeamProjects)}: {nameof(projColl)} = {projColl.Name}".Trace();
            var wis = projColl.GetService<WorkItemStore>();
            for (int i = 0; i < wis.Projects.Count; i++)
            {
                $"{nameof(GetTeamProjects)}: wis.Projects[{i}] = {wis.Projects[i].Name}".Trace();
                yield return wis.Projects[i];
            }
        }

        public static string GetWorkspaceName(this TfsTeamProjectCollection projColl)
        {
            projColl.NullCheck();
            $"{nameof(GetWorkspaceName)}: {nameof(projColl)} = {projColl.Name}".Trace();
            return $"{Globals.WorkspacePrefix}_{projColl.GetProjectCollectionName()}_{Environment.MachineName}";
        }

    }
}