using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RevitMcpPlugin.Configuration;

public class ConfigurationManager
{
    private readonly string _configPath;

    public ConfigurationManager()
    {
        _configPath = PathManager.GetCommandRegistryFilePath();
    }

    public FrameworkConfig Config { get; private set; } = new();

    public void LoadConfiguration()
    {
        SyncWithCommandSets();

        if (!File.Exists(_configPath))
        {
            Config = new FrameworkConfig();
            return;
        }

        var json = File.ReadAllText(_configPath);
        Config = JsonConvert.DeserializeObject<FrameworkConfig>(json) ?? new FrameworkConfig();
    }

    private void SyncWithCommandSets()
    {
        var commandsDir = PathManager.GetCommandsDirectoryPath();
        if (!Directory.Exists(commandsDir))
            return;

        var existing = LoadExistingCommands();
        var changed = false;

        foreach (var dir in Directory.GetDirectories(commandsDir))
        {
            if (Path.GetFileName(dir).StartsWith(".", StringComparison.Ordinal))
                continue;

            var commandJsonPath = Path.Combine(dir, "command.json");
            if (!File.Exists(commandJsonPath))
                continue;

            var parsed = JObject.Parse(File.ReadAllText(commandJsonPath));
            var setName = parsed["name"]?.ToString() ?? Path.GetFileName(dir);
            var developer = parsed["developer"]?.ToObject<DeveloperInfo>() ?? new DeveloperInfo();
            var commands = parsed["commands"]?.ToObject<List<JObject>>() ?? [];
            var versionDirs = Directory.GetDirectories(dir)
                .Select(Path.GetFileName)
                .Where(name => int.TryParse(name, out _))
                .ToList();

            foreach (var commandObject in commands)
            {
                var commandName = commandObject["commandName"]?.ToString();
                if (string.IsNullOrWhiteSpace(commandName) || existing.ContainsKey(commandName))
                    continue;

                var assemblyPath = commandObject["assemblyPath"]?.ToString() ?? "";
                var supportedVersions = new List<string>();
                string? dllBasePath = null;

                foreach (var version in versionDirs)
                {
                    var versionDllPath = Path.Combine(dir, version!, assemblyPath);
                    if (!File.Exists(versionDllPath))
                        continue;

                    dllBasePath ??= Path.Combine(setName, "{VERSION}", assemblyPath);
                    supportedVersions.Add(version!);
                }

                if (dllBasePath == null)
                    continue;

                existing[commandName] = new CommandConfig
                {
                    CommandName = commandName,
                    AssemblyPath = dllBasePath,
                    Enabled = true,
                    SupportedRevitVersions = supportedVersions.ToArray(),
                    Description = commandObject["description"]?.ToString() ?? "",
                    Developer = developer
                };
                changed = true;
            }
        }

        if (changed)
        {
            var updated = new FrameworkConfig { Commands = existing.Values.ToList() };
            File.WriteAllText(_configPath, JsonConvert.SerializeObject(updated, Formatting.Indented));
        }
    }

    private Dictionary<string, CommandConfig> LoadExistingCommands()
    {
        if (!File.Exists(_configPath))
            return new Dictionary<string, CommandConfig>(StringComparer.OrdinalIgnoreCase);

        var json = File.ReadAllText(_configPath);
        var config = JsonConvert.DeserializeObject<FrameworkConfig>(json) ?? new FrameworkConfig();
        return config.Commands.ToDictionary(c => c.CommandName, StringComparer.OrdinalIgnoreCase);
    }
}
