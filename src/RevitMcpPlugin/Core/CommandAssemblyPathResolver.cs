using System.IO;

namespace RevitMcpPlugin.Core;

public static class CommandAssemblyPathResolver
{
    public static string Resolve(string commandsDirectory, string assemblyPath, string revitVersion)
    {
        if (Path.IsPathRooted(assemblyPath))
            return assemblyPath;

        var versionedPath = assemblyPath.Replace("{VERSION}", revitVersion);
        return Path.Combine(commandsDirectory, versionedPath);
    }
}
