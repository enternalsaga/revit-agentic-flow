using System.IO;
using Newtonsoft.Json;

namespace RevitMcpPlugin.Configuration;

public static class PathManager
{
    public static string GetAppDataDirectoryPath()
    {
        var assemblyPath = typeof(PathManager).Assembly.Location;
        return Path.GetDirectoryName(assemblyPath) ?? AppContext.BaseDirectory;
    }

    public static string GetCommandsDirectoryPath()
    {
        var path = Path.Combine(GetAppDataDirectoryPath(), "Commands");
        Directory.CreateDirectory(path);
        return path;
    }

    public static string GetCommandRegistryFilePath(bool createIfNotExists = true)
    {
        var path = Path.Combine(GetCommandsDirectoryPath(), "commandRegistry.json");
        if (createIfNotExists && !File.Exists(path))
        {
            var defaultRegistry = new FrameworkConfig();
            File.WriteAllText(path, JsonConvert.SerializeObject(defaultRegistry, Formatting.Indented));
        }

        return path;
    }
}
