using Newtonsoft.Json;

namespace RevitMcpPlugin.Configuration;

public class FrameworkConfig
{
    [JsonProperty("commands")]
    public List<CommandConfig> Commands { get; set; } = [];
}
