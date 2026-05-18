using Newtonsoft.Json;

namespace RevitMcpPlugin.Configuration;

public class DeveloperInfo
{
    [JsonProperty("name")]
    public string Name { get; set; } = "";

    [JsonProperty("email")]
    public string Email { get; set; } = "";

    [JsonProperty("url")]
    public string Url { get; set; } = "";
}
