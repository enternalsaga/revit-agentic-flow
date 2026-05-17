using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RevitMcpSdk.Models;

public class JsonRpcRequest
{
    [JsonProperty("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    [JsonProperty("method")]
    public string Method { get; set; } = "";

    [JsonProperty("params")]
    public JToken? Params { get; set; }

    [JsonProperty("id")]
    public string Id { get; set; } = "";

    public JObject GetParamsObject() => Params as JObject ?? new JObject();
}
