using Newtonsoft.Json;

namespace RevitMcpPlugin.Configuration;

public class CommandConfig
{
    [JsonProperty("commandName")]
    public string CommandName { get; set; } = "";

    [JsonProperty("assemblyPath")]
    public string AssemblyPath { get; set; } = "";

    [JsonProperty("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonProperty("supportedRevitVersions")]
    public string[] SupportedRevitVersions { get; set; } = [];

    [JsonProperty("developer")]
    public DeveloperInfo Developer { get; set; } = new();

    [JsonProperty("description")]
    public string Description { get; set; } = "";
}
