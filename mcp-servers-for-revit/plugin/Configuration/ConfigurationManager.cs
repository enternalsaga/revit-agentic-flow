using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RevitMCPSDK.API.Interfaces;
using revit_mcp_plugin.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace revit_mcp_plugin.Configuration
{
    public class ConfigurationManager
    {
        private readonly ILogger _logger;
        private readonly string _configPath;

        public FrameworkConfig Config { get; private set; }

        public ConfigurationManager(ILogger logger)
        {
            _logger = logger;

            // 配置文件路径
            // Configuration file path.
            _configPath = PathManager.GetCommandRegistryFilePath();
        }

        /// <summary>
        /// <para>加载配置</para>
        /// <para>Load configuration from a JSON file.</para>
        /// </summary>
        public void LoadConfiguration()
        {
            try
            {
                SyncWithCommandSets();

                if (File.Exists(_configPath))
                {
                    string json = File.ReadAllText(_configPath);
                    Config = JsonConvert.DeserializeObject<FrameworkConfig>(json);
                    _logger.Info("已加载配置文件: {0}\nConfiguration file loaded: {0}", _configPath);
                }
                else
                {
                    _logger.Error("未找到配置文件\nNo configuration file found.");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("加载配置文件失败: {0}\nFailed to load configuration file: {0}", ex.Message);
            }

            // 记录加载时间
            // Register load time.
            _lastConfigLoadTime = DateTime.Now;
        }

        /// <summary>
        /// Scans command.json files in Commands/ subdirectories and adds any new commands
        /// to the registry with enabled=true. Existing registry entries are preserved.
        /// </summary>
        private void SyncWithCommandSets()
        {
            try
            {
                string commandsDir = PathManager.GetCommandsDirectoryPath();
                if (!Directory.Exists(commandsDir))
                    return;

                var existingCommands = new Dictionary<string, CommandConfig>();
                if (File.Exists(_configPath))
                {
                    string registryJson = File.ReadAllText(_configPath);
                    var registry = JsonConvert.DeserializeObject<FrameworkConfig>(registryJson);
                    if (registry?.Commands != null)
                    {
                        foreach (var cmd in registry.Commands)
                            existingCommands[cmd.CommandName] = cmd;
                    }
                }

                bool changed = false;

                foreach (var dir in Directory.GetDirectories(commandsDir))
                {
                    if (Path.GetFileName(dir).StartsWith("."))
                        continue;

                    string commandJsonPath = Path.Combine(dir, "command.json");
                    if (!File.Exists(commandJsonPath))
                        continue;

                    string json = File.ReadAllText(commandJsonPath);
                    var parsed = JObject.Parse(json);
                    string setName = parsed["name"]?.ToString() ?? Path.GetFileName(dir);
                    var developer = parsed["developer"]?.ToObject<DeveloperInfo>() ?? new DeveloperInfo();
                    var commands = parsed["commands"]?.ToObject<List<JObject>>() ?? new List<JObject>();

                    var versionDirs = Directory.GetDirectories(dir)
                        .Select(Path.GetFileName)
                        .Where(name => int.TryParse(name, out _))
                        .ToList();

                    foreach (var cmdObj in commands)
                    {
                        string cmdName = cmdObj["commandName"]?.ToString();
                        if (string.IsNullOrEmpty(cmdName))
                            continue;

                        if (existingCommands.ContainsKey(cmdName))
                            continue;

                        string assemblyPath = cmdObj["assemblyPath"]?.ToString() ?? "";
                        string description = cmdObj["description"]?.ToString() ?? "";

                        var supportedVersions = new List<string>();
                        string dllBasePath = null;

                        foreach (var version in versionDirs)
                        {
                            string versionDllPath = Path.Combine(dir, version, assemblyPath);
                            if (File.Exists(versionDllPath))
                            {
                                if (dllBasePath == null)
                                    dllBasePath = Path.Combine(setName, "{VERSION}", assemblyPath);
                                supportedVersions.Add(version);
                            }
                        }

                        if (supportedVersions.Count > 0 && dllBasePath != null)
                        {
                            var newConfig = new CommandConfig
                            {
                                CommandName = cmdName,
                                AssemblyPath = dllBasePath,
                                Enabled = true,
                                Description = description,
                                SupportedRevitVersions = supportedVersions.ToArray(),
                                Developer = developer
                            };
                            existingCommands[cmdName] = newConfig;
                            changed = true;
                            _logger.Info("Auto-enabled new command: {0}", cmdName);
                        }
                    }
                }

                if (changed)
                {
                    var updatedRegistry = new FrameworkConfig
                    {
                        Commands = existingCommands.Values.ToList()
                    };
                    string updatedJson = JsonConvert.SerializeObject(updatedRegistry, Formatting.Indented);
                    File.WriteAllText(_configPath, updatedJson);
                    _logger.Info("Registry synced: {0} commands total", existingCommands.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to sync command registry: {0}", ex.Message);
            }
        }

        private DateTime _lastConfigLoadTime;
    }
}
